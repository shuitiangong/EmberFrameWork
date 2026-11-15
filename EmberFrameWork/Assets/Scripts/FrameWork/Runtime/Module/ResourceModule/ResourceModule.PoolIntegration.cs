using System;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace EmberFrameWork
{
    internal sealed partial class ResourceModule
    {
        private IObjectPoolModule _poolManager;

        // 当前项目仍使用 ResourceSetting/InitPackage；只把已有配置接到原版池属性。
        private void InitializeAssetPool()
        {
            if (_assetPool != null) return;
            _poolManager = ModuleSystem.GetModule<IObjectPoolModule>();
            SetObjectPoolModule(_poolManager);
            var options = RequireOptions();
            AssetAutoReleaseInterval = options.AssetAutoReleaseInterval;
            AssetCapacity = options.AssetCapacity;
            AssetExpireTime = options.AssetExpireTime;
            AssetPriority = options.AssetPriority;
        }

        // 保留当前项目已使用的显式等待清理入口；不改变原版 UnloadAsset 的计数语义。
        public async UniTask UnloadUnusedAssetsAsync()
        {
            _assetPool.ReleaseAllUnused();
            var operation = YooAssets.GetPackage(DefaultPackageName).UnloadUnusedAssetsAsync();
            await operation.ToUniTask();
            if (operation.Status != EOperationStatus.Succeeded) throw new InvalidOperationException(operation.Error);
        }

        // 初始化/退出流程本次不重写。AssetObject 保留原版 isShutdown 分支，随后由后端销毁 YooAsset。
        private void ReleaseAssetCache()
        {
            if (_assetPool != null && _poolManager != null) _poolManager.DestroyObjectPool(_assetPool);
            _assetPool = null;
            _poolManager = null;
            _assetLoadingList.Clear();
        }
    }
}
