using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;
namespace EmberFrameWork
{
    public interface IResourceModule
    {
        string DefaultPackageName { get; set; }
        EPlayMode PlayMode { get; set; }
        long Milliseconds { get; set; }
        bool AutoUnloadBundleWhenUnused { get; set; }
        bool IsReady { get; }
        string PackageVersion { get; }
        void Initialize();
        UniTask<InitializePackageOperation> InitPackage(string packageName, bool needInitMainFest = false);
        string GetPackageVersion(string customPackageName = "");
        RequestPackageVersionOperation RequestPackageVersionAsync(bool appendTimeTicks = false, int timeout = 60, string customPackageName = "");
        LoadPackageManifestOperation UpdatePackageManifestAsync(string packageVersion, int timeout = 60, string customPackageName = "");
        AssetInfo GetAssetInfo(string location, string packageName = "");
        AssetInfo[] GetAssetInfos(string tag, string packageName = "");
        AssetInfo[] GetAssetInfos(string[] tags, string packageName = "");
        bool CheckLocationValid(string location, string packageName = "");
        // 池化加载每次成功借用对应一次 UnloadAsset。
        T LoadAsset<T>(string location, string packageName = "") where T : UnityEngine.Object;
        UnityEngine.Object LoadAsset(string location, Type assetType, string packageName = "");
        UniTaskVoid LoadAsset<T>(string location, Action<T> callback, string packageName = "") where T : UnityEngine.Object;
        UniTask<T> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default, string packageName = "") where T : UnityEngine.Object;
        UniTask<UnityEngine.Object> LoadAssetAsync(string location, Type assetType, CancellationToken cancellationToken = default, string packageName = "");
        GameObject LoadGameObject(string location, Transform parent = null, string packageName = "");
        UniTask<GameObject> LoadGameObjectAsync(string location, Transform parent = null, CancellationToken cancellationToken = default, string packageName = "");
        // 直接句柄由调用者释放，不进入资源池。
        AssetHandle LoadAssetSyncHandle<T>(string location, string packageName = "") where T : UnityEngine.Object;
        AssetHandle LoadAssetSyncHandle(string location, Type type, string packageName = "");
        AssetHandle LoadAssetAsyncHandle<T>(string location, string packageName = "") where T : UnityEngine.Object;
        AssetHandle LoadAssetAsyncHandle(string location, Type assetType, string packageName = "");
        float AssetAutoReleaseInterval { get; set; }
        int AssetCapacity { get; set; }
        float AssetExpireTime { get; set; }
        int AssetPriority { get; set; }
        void UnloadAsset(object asset);
        void UnloadUnusedAssets();
        UniTask UnloadUnusedAssetsAsync();
        void OnLowMemory();
        void SetForceUnloadUnusedAssetsAction(Action<bool> action);
        void ForceUnloadUnusedAssets(bool performGCCollect);
    }
}
