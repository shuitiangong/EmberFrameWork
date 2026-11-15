// Adapted from TEngine main 3ffab9c6. Shutdown and awaitable cleanup adapt YooAsset 3.
using System;
using Cysharp.Threading.Tasks;
using YooAsset;
namespace EmberFrameWork
{
    internal sealed partial class ResourceModule
    {
        private IObjectPoolModule _poolManager;
        private Action<bool> _forceUnloadUnusedAssetsAction;
        public void OnLowMemory()
        {
            Log.Warning("Low memory reported...");
            _forceUnloadUnusedAssetsAction?.Invoke(true);
        }
        public void SetForceUnloadUnusedAssetsAction(Action<bool> action) => _forceUnloadUnusedAssetsAction = action;
        public void ForceUnloadUnusedAssets(bool performGCCollect) => _forceUnloadUnusedAssetsAction?.Invoke(performGCCollect);
        public void UnloadUnusedAssets()
        {
            if (_shutdown) return;
            _assetPool?.ReleaseAllUnused();
            foreach (var package in PackageMap.Values)
                if (package.InitializeStatus == EOperationStatus.Succeeded) package.UnloadUnusedAssetsAsync();
        }
        public async UniTask UnloadUnusedAssetsAsync()
        {
            if (_shutdown) return;
            var token = _lifetime.Token;
            _assetPool?.ReleaseAllUnused();
            // 清理等待期间可能初始化新包，使用快照避免枚举失效。
            foreach (var package in new System.Collections.Generic.List<ResourcePackage>(PackageMap.Values))
            {
                token.ThrowIfCancellationRequested();
                if (package.InitializeStatus != EOperationStatus.Succeeded) continue;
                var operation = package.UnloadUnusedAssetsAsync();
                await operation.ToUniTask(cancellationToken: token);
                token.ThrowIfCancellationRequested();
                CheckOperation(operation, "清理未使用资源");
            }
        }
        private void ReleaseAssetCache()
        {
            if (_assetPool != null && _poolManager != null) _poolManager.DestroyObjectPool(_assetPool);
            _assetPool = null;
            _poolManager = null;
            _assetLoadingList.Clear();
        }
    }
}
