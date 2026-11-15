// Adapted from TEngine main 3ffab9c6 (MIT). See ../../Core/LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using YooAsset;
namespace EmberFrameWork
{
    internal sealed partial class ResourceModule : Module, IResourceModule
    {
        public string DefaultPackageName { get; set; } = "DefaultPackage";
        public EPlayMode PlayMode { get; set; } = EPlayMode.OfflinePlayMode;
        public long Milliseconds { get; set; } = 30;
        public bool AutoUnloadBundleWhenUnused { get; set; }
        public override int Priority => 4;
        public string PackageVersion => IsReady ? DefaultPackage.GetPackageVersion() : null;
        public bool IsReady => !_shutdown && DefaultPackage != null && _readyPackages.Contains(DefaultPackageName);
        internal ResourcePackage DefaultPackage { get; private set; }
        private Dictionary<string, ResourcePackage> PackageMap { get; } = new Dictionary<string, ResourcePackage>();
        private readonly HashSet<string> _readyPackages = new HashSet<string>();
        private readonly HashSet<string> _initializingPackages = new HashSet<string>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private bool _shutdown;
        private bool _ownsSystem;
        public override void OnInit() { }

        public void Initialize()
        {
            if (_shutdown) throw new ObjectDisposedException(nameof(ResourceModule));
            if (DefaultPackage != null) return;
            if (!Application.isPlaying) throw new InvalidOperationException("资源系统初始化需要进入 Play Mode。");
            if (YooAssets.IsInitialized) throw new InvalidOperationException("YooAsset 已由其他系统初始化。");
            YooAssets.Initialize();
            _ownsSystem = true;
            YooAssets.SetAsyncOperationMaxTimeSlice(Milliseconds);
            // YooAsset 3 不再提供默认包静态加载入口。
            DefaultPackage = YooAssets.CreatePackage(DefaultPackageName);
            PackageMap[DefaultPackageName] = DefaultPackage;
            _poolManager = ModuleSystem.GetModule<IObjectPoolModule>();
            SetObjectPoolModule(_poolManager);
        }

        public override void Shutdown()
        {
            if (_shutdown) return;
            _shutdown = true;
            _forceUnloadUnusedAssetsAction = null;
            try { _lifetime.Cancel(); }
            finally
            {
                try { ReleaseAssetCache(); }
                finally
                {
                    if (_ownsSystem) YooAssets.Destroy();
                    _ownsSystem = false;
                    DefaultPackage = null;
                    PackageMap.Clear();
                    _readyPackages.Clear();
                    _initializingPackages.Clear();
                    _lifetime.Dispose();
                }
            }
        }
    }
}
