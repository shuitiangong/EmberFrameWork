using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;

namespace EmberFrameWork
{
    /// <summary>当前默认包由框架独占管理 YooAsset 生命周期，不接管外部已经初始化的系统。</summary>
    internal sealed class YooAssetPackageBackend : IResourcePackageBackend
    {
        private ResourcePackage _package;
        private bool _ownsSystem;

        public async UniTask InitializeAsync(ResourceOptions options, EPlayMode playMode, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Application.isPlaying) throw new InvalidOperationException("资源包初始化需要进入 Play Mode。");
            if (playMode != EPlayMode.EditorSimulateMode && playMode != EPlayMode.OfflinePlayMode)
                throw new NotSupportedException("当前阶段支持模拟和离线模式；在线/Web 初始化将在在线更新阶段接入。");
            if (!_ownsSystem)
            {
                if (YooAssets.IsInitialized) throw new InvalidOperationException("YooAsset 已由其他系统初始化，资源模块不会接管其生命周期。");
                YooAssets.Initialize();
                _ownsSystem = true;
                YooAssets.SetAsyncOperationMaxTimeSlice(options.Milliseconds);
            }
            if (_package == null) _package = YooAssets.CreatePackage(options.DefaultPackageName);
            if (_package.InitializeStatus == EOperationStatus.Succeeded) return;

            InitializePackageOptions parameters;
            if (playMode == EPlayMode.EditorSimulateMode)
            {
#if UNITY_EDITOR
                var build = EditorSimulateBuildInvoker.Build(options.DefaultPackageName, (int)EBundleType.VirtualAssetBundle);
                if (build == null || string.IsNullOrEmpty(build.PackageRootDirectory))
                    throw new InvalidOperationException("模拟构建未返回资源目录，请检查 YooAsset 收集器的包名和收集配置。");
                parameters = new EditorSimulateModeOptions
                {
                    EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(build.PackageRootDirectory)
                };
#else
                throw new PlatformNotSupportedException("编辑器模拟模式不能用于播放器。");
#endif
            }
            else
            {
                parameters = new OfflinePlayModeOptions
                {
                    BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
                };
            }
            parameters.AutoUnloadBundleWhenUnused = true;
            var operation = _package.InitializePackageAsync(parameters);
            await operation.ToUniTask(cancellationToken: cancellationToken);
            Check(operation, "初始化资源包");
        }

        public async UniTask<string> RequestVersionAsync(CancellationToken cancellationToken)
        {
            var operation = _package.RequestPackageVersionAsync(new RequestPackageVersionOptions(false, 60));
            await operation.ToUniTask(cancellationToken: cancellationToken);
            Check(operation, "获取资源版本");
            return operation.PackageVersion;
        }

        public async UniTask LoadManifestAsync(string version, CancellationToken cancellationToken)
        {
            var operation = _package.LoadPackageManifestAsync(new LoadPackageManifestOptions(version, 60));
            await operation.ToUniTask(cancellationToken: cancellationToken);
            Check(operation, "加载资源清单");
        }

        private static void Check(AsyncOperationBase operation, string stage)
        {
            if (operation.Status != EOperationStatus.Succeeded)
                throw new InvalidOperationException($"{stage}失败：{operation.Error}");
        }

        public void Shutdown()
        {
            if (!_ownsSystem) return;
            _ownsSystem = false;
            _package = null;
            // 整个框架退出时同步终止自有系统，避免驱动停止后等待异步销毁无法完成。
            YooAssets.Destroy();
        }
    }
}
