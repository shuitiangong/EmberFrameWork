using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EmberFrameWork
{
    internal sealed partial class ResourceModule
    {
        private readonly Dictionary<AssetKey, CachedAsset> _assetCache = new Dictionary<AssetKey, CachedAsset>();
        private readonly Dictionary<Object, CachedAsset> _assets = new Dictionary<Object, CachedAsset>(new AssetComparer());
        private readonly HashSet<IResourceAssetHandle> _pendingAssets = new HashSet<IResourceAssetHandle>();
        private IObjectPoolModule _poolManager;
        private IObjectPool<CachedAsset> _assetPool;
        private long _assetSequence;

        public T LoadAsset<T>(string location) where T : Object
        {
            var key = ValidateLoad<T>(location);
            if (TryBorrow(key, out T existing)) return existing;
            var handle = AssetBackend.Load(location, typeof(T), true);
            bool retained = false;
            _pendingAssets.Add(handle);
            try { return CacheOrBorrow<T>(key, handle, out retained); }
            finally { _pendingAssets.Remove(handle); if (!retained) handle.Release(); }
        }

        public async UniTask<T> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default) where T : Object
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = ValidateLoad<T>(location);
            if (TryBorrow(key, out T existing)) return existing;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            var handle = AssetBackend.Load(location, typeof(T), false);
            bool retained = false;
            _pendingAssets.Add(handle);
            try
            {
                await handle.WaitAsync(linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                EnsureReady();
                return CacheOrBorrow<T>(key, handle, out retained);
            }
            finally { _pendingAssets.Remove(handle); if (!retained) handle.Release(); }
        }

        public void UnloadAsset(object asset)
        {
            EnsureReady();
            if (!(asset is Object unityAsset) || !_assets.TryGetValue(unityAsset, out var entry))
                throw new ArgumentException("该资源不是由当前模块加载的有效缓存对象。", nameof(asset));
            _assetPool.Unspawn(entry);
        }

        public async UniTask UnloadUnusedAssetsAsync()
        {
            EnsureReady();
            _assetPool?.ReleaseAllUnused();
            await AssetBackend.UnloadUnusedAsync(_lifetime.Token);
        }

        private IResourceAssetBackend AssetBackend => _backend as IResourceAssetBackend ?? throw new NotSupportedException("后端不支持资源加载。");

        private void EnsureReady()
        {
            if (_shutdown) throw new ObjectDisposedException(nameof(ResourceModule));
            if (!IsReady) throw new InvalidOperationException("请先等待 InitPackage 完成，再访问资源。");
        }

        private AssetKey ValidateLoad<T>(string location) where T : Object
        {
            EnsureReady();
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("资源地址不能为空。", nameof(location));
            if (typeof(Behaviour).IsAssignableFrom(typeof(T))) throw new ArgumentException("请加载 Prefab GameObject，再获取其 Behaviour 组件。");
            return new AssetKey(location, typeof(T));
        }

        private bool TryBorrow<T>(AssetKey key, out T asset) where T : Object
        {
            if (_assetCache.TryGetValue(key, out var entry))
            {
                var borrowed = _assetPool.Spawn(entry.Name);
                asset = (T)borrowed.Target;
                return true;
            }
            asset = null;
            return false;
        }

        private T CacheOrBorrow<T>(AssetKey key, IResourceAssetHandle handle, out bool retained) where T : Object
        {
            retained = false;
            var loaded = handle.Asset;
            if (!(loaded is T asset)) throw new InvalidOperationException($"资源 {key.Location} 不是所请求的 {typeof(T).Name} 类型。");
            // 等待期间同一地址可能已经被另一个请求缓存，以先完成的缓存为准。
            if (TryBorrow(key, out T winner)) return winner;
            if (_assets.TryGetValue(asset, out var existing))
            {
                AddAlias(existing, key);
                _assetPool.Spawn(existing.Name);
                return asset;
            }
            EnsureAssetPool();
            var entry = MemoryPool.Acquire<CachedAsset>();
            entry.Setup(this, asset, handle, (++_assetSequence).ToString());
            _assets.Add(asset, entry);
            AddAlias(entry, key);
            try { _assetPool.Register(entry, true); }
            catch { Evict(entry); MemoryPool.Release(entry); throw; }
            retained = true;
            return asset;
        }

        private void AddAlias(CachedAsset entry, AssetKey key)
        {
            if (_assetCache.ContainsKey(key)) return;
            _assetCache.Add(key, entry);
            entry.Keys.Add(key);
        }

        private void EnsureAssetPool()
        {
            if (_assetPool != null) return;
            _poolManager = ModuleSystem.GetModule<IObjectPoolModule>();
            var options = RequireOptions();
            _assetPool = _poolManager.CreateMultiSpawnObjectPool<CachedAsset>("Resource Assets", options.AssetAutoReleaseInterval,
                options.AssetCapacity, options.AssetExpireTime, options.AssetPriority);
        }

        private void Evict(CachedAsset entry)
        {
            foreach (var key in entry.Keys) _assetCache.Remove(key);
            if (entry.Target is Object asset) _assets.Remove(asset);
        }

        private void ReleaseAssetCache()
        {
            List<Exception> errors = null;
            // 关闭期间不再启动加载；快照允许请求 finally 同时从集合移除。
            foreach (var handle in new List<IResourceAssetHandle>(_pendingAssets))
            {
                try { handle.Release(); }
                catch (Exception error) { if (errors == null) errors = new List<Exception>(); errors.Add(error); }
            }
            _pendingAssets.Clear();
            try { if (_assetPool != null) _poolManager.DestroyObjectPool(_assetPool); }
            catch (Exception error) { if (errors == null) errors = new List<Exception>(); errors.Add(error); }
            finally { _assetPool = null; _poolManager = null; _assetCache.Clear(); _assets.Clear(); }
            if (errors != null) throw new AggregateException(errors);
        }

        private readonly struct AssetKey : IEquatable<AssetKey>
        {
            public readonly string Location;
            private readonly Type _type;
            public AssetKey(string location, Type type) { Location = location; _type = type; }
            public bool Equals(AssetKey other) => Location == other.Location && _type == other._type;
            public override bool Equals(object obj) => obj is AssetKey other && Equals(other);
            public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Location) ^ _type.GetHashCode();
        }

        private sealed class AssetComparer : IEqualityComparer<Object>
        {
            public bool Equals(Object x, Object y) => ReferenceEquals(x, y);
            public int GetHashCode(Object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private sealed class CachedAsset : ObjectBase
        {
            public readonly List<AssetKey> Keys = new List<AssetKey>();
            private ResourceModule _owner;
            private IResourceAssetHandle _handle;
            public CachedAsset() { }
            public void Setup(ResourceModule owner, Object asset, IResourceAssetHandle handle, string name)
            {
                Initialize(name, asset); _owner = owner; _handle = handle;
            }
            protected internal override void Release(bool isShutdown)
            {
                _owner.Evict(this);
                _handle.Release();
            }
            public override void Clear()
            {
                base.Clear(); Keys.Clear(); _owner = null; _handle = null;
            }
        }
    }
}
