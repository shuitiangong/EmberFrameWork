// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../Core/LICENSE.txt.
using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using YooAsset;

namespace EmberFrameWork
{
    [Preserve]
    internal sealed class SceneModule : Module, ISceneModule
    {
        private bool _isLoading;
        private bool _shutdown;

        public override void OnInit() { }

        public override void Shutdown()
        {
            _shutdown = true;
        }

        public async UniTask<Scene> LoadSceneAsync(string location)
        {
            if (_shutdown)
                throw new ObjectDisposedException(nameof(SceneModule));
            if (string.IsNullOrWhiteSpace(location))
                throw new ArgumentException("场景定位地址不能为空。", nameof(location));
            if (_isLoading)
                throw new InvalidOperationException("已有主场景正在加载。");

            var resource = ModuleSystem.GetModule<IResourceModule>();
            if (!resource.IsReady)
                throw new InvalidOperationException("默认资源包尚未就绪，无法加载场景。");

            var package = YooAssets.GetPackage(resource.DefaultPackageName);
            var assetInfo = package.GetAssetInfo(location);
            if (!assetInfo.IsValid)
                throw new InvalidOperationException($"找不到场景 '{location}'：{assetInfo.Error}");
            if (!assetInfo.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"资源 '{location}' 不是 Unity 场景。", nameof(location));

            SceneHandle handle = null;
            _isLoading = true;
            try
            {
                handle = package.LoadSceneAsync(assetInfo, LoadSceneMode.Single, priority: 100);
                await handle.ToUniTask();
                if (_shutdown)
                    throw new ObjectDisposedException(nameof(SceneModule));
                if (handle.Status != EOperationStatus.Succeeded)
                    throw new InvalidOperationException($"加载场景 '{location}' 失败：{handle.Error}");

                resource.ForceUnloadUnusedAssets(true);
                return handle.SceneObject;
            }
            finally
            {
                _isLoading = false;
                // 已加载场景由 YooAsset 监听 sceneUnloaded 并释放句柄，退出时由资源模块销毁包。
                if (handle != null && handle.IsValid && handle.Status != EOperationStatus.Succeeded)
                    handle.Release();
            }
        }
    }
}
