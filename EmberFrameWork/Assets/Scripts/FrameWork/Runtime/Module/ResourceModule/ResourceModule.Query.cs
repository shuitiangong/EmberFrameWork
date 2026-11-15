// Selected from TEngine main 3ffab9c6; YooAsset 3 uses explicit package access.
using System;
using Cysharp.Threading.Tasks;
using YooAsset;
namespace EmberFrameWork
{
    internal sealed partial class ResourceModule
    {
        public AssetInfo GetAssetInfo(string location, string packageName = "") => GetPackageForLoading(packageName).GetAssetInfo(location);
        public AssetInfo[] GetAssetInfos(string tag, string packageName = "") => GetPackageForLoading(packageName).GetAssetInfos(tag);
        public AssetInfo[] GetAssetInfos(string[] tags, string packageName = "") => GetPackageForLoading(packageName).GetAssetInfos(tags);
        public bool CheckLocationValid(string location, string packageName = "") => GetAssetInfo(location, packageName).IsValid;
        // 直接句柄不进入资源池，由调用者 Dispose/Release；不可用 UnloadAsset 归还。
        public AssetHandle LoadAssetSyncHandle<T>(string location, string packageName = "") where T : UnityEngine.Object
            => LoadAssetSyncHandle(location, typeof(T), packageName);
        public AssetHandle LoadAssetSyncHandle(string location, Type type, string packageName = "")
            => GetPackageForLoading(packageName).LoadAssetSync(location, type);
        public AssetHandle LoadAssetAsyncHandle<T>(string location, string packageName = "") where T : UnityEngine.Object
            => LoadAssetAsyncHandle(location, typeof(T), packageName);
        public AssetHandle LoadAssetAsyncHandle(string location, Type assetType, string packageName = "")
            => GetPackageForLoading(packageName).LoadAssetAsync(location, assetType);
        public async UniTaskVoid LoadAsset<T>(string location, Action<T> callback, string packageName = "") where T : UnityEngine.Object
        {
            var asset = await LoadAssetAsync<T>(location, packageName: packageName);
            if (callback == null)
            {
                if (asset != null) UnloadAsset(asset);
                return;
            }
            callback(asset);
        }
    }
}
