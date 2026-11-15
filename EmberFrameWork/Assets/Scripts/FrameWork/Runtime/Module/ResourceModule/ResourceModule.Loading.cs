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
        {
            ValidateLoadRequest(location);

            string assetObjectKey = GetCacheKey(location, packageName);
            if (TrySpawnAsset<T>(assetObjectKey, out var cachedAsset))
                return cachedAsset;

            var package = GetPackageForLoading(packageName);
            var assetInfo = GetAssetInfoForLoading<T>(package, location);
            AssetHandle handle = package.LoadAssetSync(assetInfo);
            try
            {
                return RegisterLoadedAsset<T>(assetObjectKey, location, ref handle);
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
        {
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

            if (TrySpawnAsset<T>(assetObjectKey, out var cachedAsset))
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
                var assetInfo = GetAssetInfoForLoading<T>(package, location);
                handle = package.LoadAssetAsync(assetInfo);
                if (await handle.ToUniTask(cancellationToken: loadToken).SuppressCancellationThrow() || loadToken.IsCancellationRequested)
                    return null;

                // 同步请求可能在异步等待期间先把同一资源放入池中。
                if (TrySpawnAsset<T>(assetObjectKey, out var synchronousAsset))
                    return synchronousAsset;

                return RegisterLoadedAsset<T>(assetObjectKey, location, ref handle);
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

        private bool TrySpawnAsset<T>(string assetObjectKey, out T asset) where T : UnityEngine.Object
        {
            asset = null;
            var assetObject = _assetPool.Spawn(assetObjectKey);
            if (assetObject == null)
                return false;

            if (assetObject.Target is T cachedAsset && cachedAsset != null)
                asset = cachedAsset;
            else
                _assetPool.Unspawn(assetObject.Target);

            return true;
        }

        private T RegisterLoadedAsset<T>(string assetObjectKey, string location, ref AssetHandle handle) where T : UnityEngine.Object
        {
            if (!handle.IsValid)
                throw new GameFrameworkException($"加载资源 '{location}' 失败：资源句柄已失效。");

            if (handle.Status != EOperationStatus.Succeeded)
                throw new GameFrameworkException($"加载资源 '{location}' 失败：{handle.Error}");

            var asset = handle.AssetObject as T;
            if (asset == null)
                throw new GameFrameworkException($"加载资源 '{location}' 失败：未返回 {typeof(T).Name}。");

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
            return YooAssets.GetPackage(string.IsNullOrEmpty(packageName) ? DefaultPackageName : packageName);
        }

        private static AssetInfo GetAssetInfoForLoading<T>(ResourcePackage package, string location) where T : UnityEngine.Object
        {
            var assetInfo = package.GetAssetInfo(location, typeof(T));
            if (!assetInfo.IsValid)
                throw new GameFrameworkException($"加载资源 '{location}' 失败：{assetInfo.Error}");
            return assetInfo;
        }

        private async UniTask<bool> TryWaitingLoading(string assetObjectKey, CancellationToken cancellationToken)
        {
#if UNITY_EDITOR
            double deadline = Time.timeAsDouble + 60d;
            bool CanContinue() => !_assetLoadingList.Contains(assetObjectKey) || Time.timeAsDouble >= deadline;
#else
            bool CanContinue() => !_assetLoadingList.Contains(assetObjectKey);
#endif
            while (_assetLoadingList.Contains(assetObjectKey))
            {
                if (await UniTask.WaitUntil(CanContinue, cancellationToken: cancellationToken).SuppressCancellationThrow())
                    return false;

#if UNITY_EDITOR
                if (_assetLoadingList.Contains(assetObjectKey) && Time.timeAsDouble >= deadline)
                    throw new TimeoutException($"等待已有请求加载资源 '{assetObjectKey}' 超时。");
#endif
            }

            return !cancellationToken.IsCancellationRequested;
        }
    }
}
