using System;
using YooAsset;

namespace EmberFrameWork
{
    /// <summary>获取模块和配置均无加载副作用；显式调用 InitPackage 后启动默认包。</summary>
    internal sealed partial class ResourceModule : Module, IResourceModule
    {
        private ResourceOptions _options;
        private bool _shutdown;
        private readonly IResourcePackageBackend _backend;
        public ResourceModule() : this(new YooAssetPackageBackend()) { }
        internal ResourceModule(IResourcePackageBackend backend)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        }
        // 对象池为 6；资源模块关闭后，对象池再执行关闭。
        public override int Priority => 4;
        public bool IsConfigured => _options != null;
        public string DefaultPackageName => RequireOptions().DefaultPackageName;
        public EPlayMode PlayMode => IsEditor ? RequireOptions().EditorPlayMode : RequireOptions().PlayMode;
        public ResourceOptions Options => RequireOptions().Clone();
        public override void OnInit() { }

        public void Configure(ResourceOptions options)
        {
            if (_shutdown) throw new ObjectDisposedException(nameof(ResourceModule));
            if (IsConfigured) throw new InvalidOperationException("资源模块已经配置；请关闭模块后再开始新会话。");
            if (options == null) throw new ArgumentNullException(nameof(options));
            _options = options.CreateValidatedCopy(IsEditor, IsWebGL);
        }

        public void Configure(ResourceSetting setting)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));
            Configure(setting.GetOptions());
        }

        public override void Shutdown()
        {
            if (_shutdown) return;
            _shutdown = true;
            _options = null;
            InitializationState = ResourceInitializationState.Shutdown;
            PackageVersion = null;
            try { _lifetime.Cancel(); }
            finally
            {
                try { ReleaseAssetCache(); }
                finally { try { _backend.Shutdown(); } finally { _lifetime.Dispose(); } }
            }
        }

        private ResourceOptions RequireOptions() => _options ?? throw new InvalidOperationException("资源模块尚未配置或已经关闭。");

#if UNITY_EDITOR
        internal const bool IsEditor = true;
#else
        internal const bool IsEditor = false;
#endif
#if UNITY_WEBGL
        internal const bool IsWebGL = true;
#else
        internal const bool IsWebGL = false;
#endif
    }
}
