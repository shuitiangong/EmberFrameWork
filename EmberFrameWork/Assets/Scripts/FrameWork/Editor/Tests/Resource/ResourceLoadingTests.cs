using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using YooAsset;
using Object = UnityEngine.Object;

namespace EmberFrameWork.Tests
{
    public class ResourceLoadingTests
    {
        // 不调用 Unity 原生构造函数；只验证模块的身份、类型及句柄所有权逻辑。
        private static T Identity<T>() where T : Object => (T)FormatterServices.GetUninitializedObject(typeof(T));
        private sealed class Handle : IResourceAssetHandle
        {
            public Object Value;
            public bool Fail;
            public int Releases;
            public TaskCompletionSource<bool> Gate;
            public readonly List<string> Events;
            public Handle(List<string> events) { Events = events; }
            public Object Asset => Fail ? throw new InvalidOperationException("load failed") : Value;
            public UniTask WaitAsync(CancellationToken token) => Gate == null ? UniTask.CompletedTask : Gate.Task.AsUniTask(false).AsUniTask().AttachExternalCancellation(token);
            public void Release() { if (Releases != 0) return; Releases++; Events.Add("release"); }
        }
        private sealed class Backend : IResourcePackageBackend, IResourceAssetBackend
        {
            public readonly List<string> Events = new List<string>();
            public readonly List<Handle> Handles = new List<Handle>();
            public Object Value = Identity<UnityEngine.TextAsset>();
            public bool Fail, Wait;
            public UniTask InitializeAsync(ResourceOptions options, EPlayMode mode, CancellationToken token) => UniTask.CompletedTask;
            public UniTask<string> RequestVersionAsync(CancellationToken token) => UniTask.FromResult("v1");
            public UniTask LoadManifestAsync(string version, CancellationToken token) => UniTask.CompletedTask;
            public IResourceAssetHandle Load(string location, Type type, bool synchronous)
            {
                var handle = new Handle(Events) { Value = Value, Fail = Fail,
                    Gate = Wait && !synchronous ? new TaskCompletionSource<bool>() : null };
                Handles.Add(handle); return handle;
            }
            public UniTask UnloadUnusedAsync(CancellationToken token) { Events.Add("unload"); return UniTask.CompletedTask; }
            public void Shutdown() => Events.Add("shutdown");
        }
        private Backend _backend;
        private ResourceModule _module;
        private bool _strict;
        [SetUp] public void SetUp()
        {
            ModuleSystem.Shutdown(); MemoryPool.ClearAll();
            _strict = MemoryPool.EnableStrictCheck; MemoryPool.EnableStrictCheck = true;
            GameFrameworkLog.SetLogHelper(null);
            _backend = new Backend(); _module = new ResourceModule(_backend); _module.Configure(new ResourceOptions());
        }
        [TearDown] public void TearDown()
        {
            try { _module.Shutdown(); ModuleSystem.Shutdown(); }
            finally { MemoryPool.ClearAll(); MemoryPool.EnableStrictCheck = _strict; GameFrameworkLog.ResetLogHelper(); }
        }

        [Test] public async Task CacheHitSharesHandleAndWaitsForLastReturn()
        {
            await _module.InitPackage();
            var first = _module.LoadAsset<UnityEngine.TextAsset>("config");
            Assert.AreSame(first, await _module.LoadAssetAsync<UnityEngine.TextAsset>("config"));
            Assert.AreEqual(1, _backend.Handles.Count);
            _module.UnloadAsset(first); await _module.UnloadUnusedAssetsAsync();
            Assert.AreEqual(0, _backend.Handles[0].Releases);
            _module.UnloadAsset(first); await _module.UnloadUnusedAssetsAsync();
            Assert.AreEqual(1, _backend.Handles[0].Releases);
            _module.LoadAsset<UnityEngine.TextAsset>("config");
            Assert.AreEqual(2, _backend.Handles.Count);
        }

        [Test] public async Task ConcurrentLoadsMergeCacheAndReleaseDuplicateHandle()
        {
            await _module.InitPackage(); _backend.Wait = true;
            var first = _module.LoadAssetAsync<UnityEngine.TextAsset>("config").AsTask();
            var second = _module.LoadAssetAsync<UnityEngine.TextAsset>("config").AsTask();
            _backend.Handles[0].Gate.SetResult(true); await first;
            _backend.Handles[1].Gate.SetResult(true); await second;
            Assert.AreEqual(0, _backend.Handles[0].Releases);
            Assert.AreEqual(1, _backend.Handles[1].Releases);
            _module.UnloadAsset(first.Result); _module.UnloadAsset(second.Result);
            await _module.UnloadUnusedAssetsAsync();
            Assert.AreEqual(1, _backend.Handles[0].Releases);
        }

        [Test] public async Task AliasAndTypeQueriesShareOneCachedAsset()
        {
            await _module.InitPackage();
            var asset = _module.LoadAsset<UnityEngine.TextAsset>("short-name");
            Assert.AreSame(asset, _module.LoadAsset<Object>("Assets/FullPath.txt"));
            Assert.AreSame(asset, _module.LoadAsset<Object>("Assets/FullPath.txt"));
            Assert.AreEqual(2, _backend.Handles.Count);
            Assert.AreEqual(1, _backend.Handles[1].Releases);
            for (int i = 0; i < 3; i++) _module.UnloadAsset(asset);
            await _module.UnloadUnusedAssetsAsync();
            _module.LoadAsset<Object>("Assets/FullPath.txt");
            Assert.AreEqual(3, _backend.Handles.Count);
        }

        [Test] public async Task CancelOneWaiterDoesNotCancelOtherRequest()
        {
            await _module.InitPackage(); _backend.Wait = true;
            using var cancellation = new CancellationTokenSource();
            var first = _module.LoadAssetAsync<UnityEngine.TextAsset>("config", cancellation.Token).AsTask();
            var second = _module.LoadAssetAsync<UnityEngine.TextAsset>("config").AsTask();
            cancellation.Cancel();
            try { await first; Assert.Fail("Expected cancellation"); } catch (OperationCanceledException) { }
            Assert.AreEqual(1, _backend.Handles[0].Releases);
            Assert.AreEqual(0, _backend.Handles[1].Releases);
            _backend.Handles[1].Gate.SetResult(true);
            Assert.AreSame(_backend.Value, await second);
        }

        [Test] public async Task FailureAndWrongTypeReleaseHandlesAndAllowRetry()
        {
            await _module.InitPackage(); _backend.Fail = true;
            Assert.Throws<InvalidOperationException>(() => _module.LoadAsset<UnityEngine.TextAsset>("bad"));
            try { await _module.LoadAssetAsync<UnityEngine.TextAsset>("bad"); Assert.Fail("Expected failure"); } catch (InvalidOperationException) { }
            _backend.Fail = false;
            Assert.Throws<InvalidOperationException>(() => _module.LoadAsset<UnityEngine.Texture2D>("bad"));
            foreach (var handle in _backend.Handles) Assert.AreEqual(1, handle.Releases);
            Assert.AreSame(_backend.Value, _module.LoadAsset<UnityEngine.TextAsset>("bad"));
        }

        [Test] public async Task PreCancelledAndInvalidCallsDoNotStartLoads()
        {
            Assert.Throws<InvalidOperationException>(() => _module.LoadAsset<Object>("a"));
            await _module.InitPackage();
            Assert.Throws<ArgumentException>(() => _module.LoadAsset<Object>(" "));
            Assert.Throws<ArgumentException>(() => _module.LoadAsset<UnityEngine.MonoBehaviour>("prefab"));
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { await _module.LoadAssetAsync<Object>("a", cancellation.Token); Assert.Fail("Expected cancellation"); } catch (OperationCanceledException) { }
            Assert.IsEmpty(_backend.Handles);
        }

        [Test] public async Task DoubleReturnDoesNotMakeCachedAssetReleasableEarly()
        {
            await _module.InitPackage(); var asset = _module.LoadAsset<Object>("a");
            _module.UnloadAsset(asset);
            Assert.Throws<GameFrameworkException>(() => _module.UnloadAsset(asset));
            Assert.AreSame(asset, _module.LoadAsset<Object>("a"));
            await _module.UnloadUnusedAssetsAsync();
            Assert.AreEqual(0, _backend.Handles[0].Releases);
        }

        [Test] public async Task ShutdownReleasesCachedAndPendingHandlesBeforeBackend()
        {
            await _module.InitPackage(); _module.LoadAsset<Object>("cached"); _backend.Wait = true;
            var pending = _module.LoadAssetAsync<Object>("pending").AsTask();
            _module.Shutdown();
            try { await pending; Assert.Fail("Expected cancellation"); } catch (OperationCanceledException) { }
            foreach (var handle in _backend.Handles) Assert.AreEqual(1, handle.Releases);
            Assert.AreEqual("shutdown", _backend.Events[_backend.Events.Count - 1]);
            Assert.AreEqual(0, ModuleSystem.GetModule<IObjectPoolModule>().Count);
        }

        [Test] public async Task PoolExpiryEvictsModuleLookupAsWellAsHandle()
        {
            _module.Shutdown(); _backend = new Backend(); _module = new ResourceModule(_backend);
            _module.Configure(new ResourceOptions { AssetAutoReleaseInterval = 0, AssetExpireTime = 0 });
            await _module.InitPackage();
            var asset = _module.LoadAsset<Object>("expiring"); _module.UnloadAsset(asset);
            ModuleSystem.Update(0, 0);
            Assert.AreEqual(1, _backend.Handles[0].Releases);
            _module.LoadAsset<Object>("expiring");
            Assert.AreEqual(2, _backend.Handles.Count);
        }

        [Test] public async Task ZeroCapacityReleasesOnLastReturn()
        {
            _module.Shutdown(); _backend = new Backend(); _module = new ResourceModule(_backend);
            _module.Configure(new ResourceOptions { AssetCapacity = 0 });
            await _module.InitPackage();
            var asset = _module.LoadAsset<Object>("a");
            Assert.AreEqual(0, _backend.Handles[0].Releases);
            _module.UnloadAsset(asset);
            Assert.AreEqual(1, _backend.Handles[0].Releases);
        }

        [Test] public async Task LateCompletionUsesExistingLocationCache()
        {
            await _module.InitPackage(); _backend.Wait = true;
            var first = _module.LoadAssetAsync<Object>("a").AsTask();
            _backend.Value = Identity<UnityEngine.TextAsset>();
            var second = _module.LoadAssetAsync<Object>("a").AsTask();
            _backend.Handles[0].Gate.SetResult(true); var result = await first;
            _backend.Handles[1].Gate.SetResult(true);
            Assert.AreSame(result, await second);
            Assert.AreSame(result, _module.LoadAsset<Object>("a"));
            Assert.AreEqual(1, _backend.Handles[1].Releases);
        }
    }
}
