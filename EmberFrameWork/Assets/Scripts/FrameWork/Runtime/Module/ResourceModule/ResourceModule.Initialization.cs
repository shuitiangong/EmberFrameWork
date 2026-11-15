// Adapted from TEngine main 3ffab9c6; YooAsset 3.0.7 API adaptation.
using System;
using Cysharp.Threading.Tasks;
using YooAsset;
namespace EmberFrameWork
{
    internal sealed partial class ResourceModule
    {
        public async UniTask<InitializePackageOperation> InitPackage(string packageName, bool needInitMainFest = false)
        {
            if (_shutdown) throw new ObjectDisposedException(nameof(ResourceModule));
            if (DefaultPackage == null) throw new InvalidOperationException("请先调用 Initialize。");
            if (string.IsNullOrWhiteSpace(packageName)) throw new ArgumentException("资源包名称不能为空。", nameof(packageName));
            if (!_initializingPackages.Add(packageName)) throw new InvalidOperationException($"资源包正在初始化：{packageName}");
            var token = _lifetime.Token;
            try
            {
                if (!PackageMap.TryGetValue(packageName, out var package))
                {
                    if (!YooAssets.TryGetPackage(packageName, out package)) package = YooAssets.CreatePackage(packageName);
                    PackageMap[packageName] = package;
                }
                InitializePackageOperation operation = null;
                if (package.InitializeStatus != EOperationStatus.Succeeded)
                {
                    InitializePackageOptions options;
                    if (PlayMode == EPlayMode.EditorSimulateMode)
                    {
#if UNITY_EDITOR
                        var build = EditorSimulateBuildInvoker.Build(packageName, (int)EBundleType.VirtualAssetBundle);
                        options = new EditorSimulateModeOptions
                        {
                            EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(build.PackageRootDirectory)
                        };
#else
                        throw new PlatformNotSupportedException("编辑器模拟模式不能用于播放器。");
#endif
                    }
                    else if (PlayMode == EPlayMode.OfflinePlayMode)
                    {
                        options = new OfflinePlayModeOptions
                        {
                            BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
                        };
                    }
                    else throw new NotSupportedException("mini 当前支持编辑器模拟和离线模式。");
                    options.AutoUnloadBundleWhenUnused = AutoUnloadBundleWhenUnused;
                    operation = package.InitializePackageAsync(options);
                    await operation.ToUniTask(cancellationToken: token);
                    token.ThrowIfCancellationRequested();
                    CheckOperation(operation, "初始化资源包");
                }
                if (needInitMainFest && !_readyPackages.Contains(packageName))
                {
                    var version = RequestPackageVersionAsync(customPackageName: packageName);
                    await version.ToUniTask(cancellationToken: token);
                    token.ThrowIfCancellationRequested();
                    CheckOperation(version, "获取资源版本");
                    if (string.IsNullOrWhiteSpace(version.PackageVersion)) throw new GameFrameworkException("资源版本号为空。");
                    var manifest = UpdatePackageManifestAsync(version.PackageVersion, customPackageName: packageName);
                    await manifest.ToUniTask(cancellationToken: token);
                    token.ThrowIfCancellationRequested();
                    CheckOperation(manifest, "加载资源清单");
                    _readyPackages.Add(packageName);
                }
                return operation;
            }
            finally { _initializingPackages.Remove(packageName); }
        }
        public string GetPackageVersion(string customPackageName = "") => GetPackageForLoading(customPackageName).GetPackageVersion();
        public RequestPackageVersionOperation RequestPackageVersionAsync(bool appendTimeTicks = false, int timeout = 60, string customPackageName = "")
            => GetPackageForLoading(customPackageName).RequestPackageVersionAsync(new RequestPackageVersionOptions(appendTimeTicks, timeout));
        public LoadPackageManifestOperation UpdatePackageManifestAsync(string packageVersion, int timeout = 60, string customPackageName = "")
        {
            var package = GetPackageForLoading(customPackageName);
            _readyPackages.Remove(package.PackageName);
            var operation = package.LoadPackageManifestAsync(new LoadPackageManifestOptions(packageVersion, timeout));
            operation.Completed += completed =>
            {
                if (!_shutdown && completed.Status == EOperationStatus.Succeeded) _readyPackages.Add(package.PackageName);
            };
            return operation;
        }
        private static void CheckOperation(AsyncOperationBase operation, string stage)
        {
            if (operation.Status != EOperationStatus.Succeeded) throw new GameFrameworkException($"{stage}失败：{operation.Error}");
        }
    }
}
