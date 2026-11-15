// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../Core/LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace EmberFrameWork
{
    internal sealed partial class ResourceModule
    {
        private readonly HashSet<string> _assetLoadingList = new HashSet<string>();

        private string GetCacheKey(string location, string packageName = "")
        {
            if (string.IsNullOrEmpty(packageName) || packageName.Equals(DefaultPackageName))
            {
                return location;
            }

            return $"{packageName}/{location}";
        }

        public T LoadAsset<T>(string location, string packageName = "") where T : UnityEngine.Object
            => LoadAsset(location, typeof(T), packageName) as T;

        public UnityEngine.Object LoadAsset(string location, Type assetType, string packageName = "")
        {
            assetType ??= typeof(UnityEngine.Object);
            ValidateLoadRequest(location);

            string assetObjectKey = GetCacheKey(location, packageName);
            if (TrySpawnAsset(assetObjectKey, assetType, out var cachedAsset))
                return cachedAsset;

            var package = GetPackageForLoading(packageName);
            var assetInfo = GetAssetInfoForLoading(package, location, assetType);
            AssetHandle handle = package.LoadAssetSync(assetInfo);
            try
            {
                return RegisterLoadedAsset(assetObjectKey, location, assetType, ref handle);
            }
            finally
            {
                if (handle is { IsValid: true })
                    handle.Release();
            }
        }

        public GameObject LoadGameObject(string location, Transform parent = null, string packageName = "")
        {
            var prefab = LoadAsset<GameObject>(location, packageName);
            return InstantiateBorrowedPrefab(prefab, parent);
        }

        public async UniTask<T> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default, string packageName = "") where T : UnityEngine.Object
            => await LoadAssetAsync(location, typeof(T), cancellationToken, packageName) as T;

        public async UniTask<UnityEngine.Object> LoadAssetAsync(string location, Type assetType, CancellationToken cancellationToken = default, string packageName = "")
        {
            assetType ??= typeof(UnityEngine.Object);
            ValidateLoadRequest(location);
            if (cancellationToken.IsCancellationRequested)
                return null;

            using var linkedCancellation = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token)
                : null;
            var loadToken = linkedCancellation?.Token ?? _lifetime.Token;
            string assetObjectKey = GetCacheKey(location, packageName);

            if (_assetLoadingList.Contains(assetObjectKey) && !await TryWaitingLoading(assetObjectKey, loadToken))
                return null;
            if (loadToken.IsCancellationRequested)
                return null;

            if (TrySpawnAsset(assetObjectKey, assetType, out var cachedAsset))
            {
                if (await UniTask.Yield(loadToken).SuppressCancellationThrow() || loadToken.IsCancellationRequested)
                {
                    if (cachedAsset != null)
                        UnloadAsset(cachedAsset);
                    return null;
                }

                return cachedAsset;
            }

            _assetLoadingList.Add(assetObjectKey);
            AssetHandle handle = null;
            try
            {
                var package = GetPackageForLoading(packageName);
                var assetInfo = GetAssetInfoForLoading(package, location, assetType);
                handle = package.LoadAssetAsync(assetInfo);
                if (await handle.ToUniTask(cancellationToken: loadToken).SuppressCancellationThrow() || loadToken.IsCancellationRequested)
                    return null;

                // 同步请求可能在异步等待期间先把同一资源放入池中。
                if (TrySpawnAsset(assetObjectKey, assetType, out var synchronousAsset))
                    return synchronousAsset;

                return RegisterLoadedAsset(assetObjectKey, location, assetType, ref handle);
            }
            finally
            {
                _assetLoadingList.Remove(assetObjectKey);
                if (handle is { IsValid: true })
                    handle.Release();
            }
        }

        public async UniTask<GameObject> LoadGameObjectAsync(string location, Transform parent = null, CancellationToken cancellationToken = default, string packageName = "")
        {
            var prefab = await LoadAssetAsync<GameObject>(location, cancellationToken, packageName);
            if (cancellationToken.IsCancellationRequested || _shutdown)
            {
                if (prefab != null)
                    UnloadAsset(prefab);
                return null;
            }

            return InstantiateBorrowedPrefab(prefab, parent);
        }

        private GameObject InstantiateBorrowedPrefab(GameObject prefab, Transform parent)
        {
            try
            {
                return AssetsReference.Instantiate(prefab, parent, this).gameObject;
            }
            catch
            {
                if (prefab != null)
                    UnloadAsset(prefab);
                throw;
            }
        }

        private bool TrySpawnAsset(string assetObjectKey, Type assetType, out UnityEngine.Object asset)
        {
            asset = null;
            var assetObject = _assetPool.Spawn(assetObjectKey);
            if (assetObject == null)
                return false;

            if (assetObject.Target is UnityEngine.Object cachedAsset && cachedAsset != null && assetType.IsInstanceOfType(cachedAsset))
                asset = cachedAsset;
            else
                _assetPool.Unspawn(assetObject.Target);

            return true;
        }

        private UnityEngine.Object RegisterLoadedAsset(string assetObjectKey, string location, Type assetType, ref AssetHandle handle)
        {
            if (!handle.IsValid)
                throw new GameFrameworkException($"加载资源 '{location}' 失败：资源句柄已失效。");

            if (handle.Status != EOperationStatus.Succeeded)
                throw new GameFrameworkException($"加载资源 '{location}' 失败：{handle.Error}");

            var asset = handle.AssetObject;
            if (asset == null || !assetType.IsInstanceOfType(asset))
                throw new GameFrameworkException($"加载资源 '{location}' 失败：未返回 {assetType.Name}。");

            _assetPool.Register(AssetObject.Create(assetObjectKey, asset, handle, this), true);
            handle = null; // 注册成功后句柄由资源对象池持有。
            return asset;
        }

        private void ValidateLoadRequest(string location)
        {
            if (string.IsNullOrEmpty(location))
                throw new GameFrameworkException("Asset name is invalid.");
            if (_shutdown)
                throw new ObjectDisposedException(nameof(ResourceModule));
            if (_assetPool == null)
                throw new InvalidOperationException("资源对象池尚未初始化。");
        }

        private ResourcePackage GetPackageForLoading(string packageName)
        {
            if (_shutdown) throw new ObjectDisposedException(nameof(ResourceModule));
            return string.IsNullOrEmpty(packageName) || packageName == DefaultPackageName
                ? DefaultPackage ?? throw new InvalidOperationException("资源系统尚未初始化。")
                : YooAssets.GetPackage(packageName);
        }

        private static AssetInfo GetAssetInfoForLoading(ResourcePackage package, string location, Type assetType)
        {
            var assetInfo = package.GetAssetInfo(location, assetType);
            if (!assetInfo.IsValid)
                throw new GameFrameworkException($"加载资源 '{location}' 失败：{assetInfo.Error}");
            return assetInfo;
        }

        private async UniTask<bool> TryWaitingLoading(string assetObjectKey, CancellationToken cancellationToken)
        {
#if UNITY_EDITOR
            double deadline = Time.realtimeSinceStartupAsDouble + 60d;
            bool CanContinue() => !_assetLoadingList.Contains(assetObjectKey) || Time.realtimeSinceStartupAsDouble >= deadline;
#else
            bool CanContinue() => !_assetLoadingList.Contains(assetObjectKey);
#endif
            while (_assetLoadingList.Contains(assetObjectKey))
            {
                if (await UniTask.WaitUntil(CanContinue, cancellationToken: cancellationToken).SuppressCancellationThrow())
                    return false;

#if UNITY_EDITOR
                if (_assetLoadingList.Contains(assetObjectKey) && Time.realtimeSinceStartupAsDouble >= deadline)
                    throw new TimeoutException($"等待已有请求加载资源 '{assetObjectKey}' 超时。");
#endif
            }

            return !cancellationToken.IsCancellationRequested;
        }
    }
}
