// Adapted from TEngine main 3ffab9c6 (MIT). See ../../Core/LICENSE.txt.
using System;
using UnityEngine;
using YooAsset;
namespace EmberFrameWork
{
    [DisallowMultipleComponent]
    public sealed class ResourceModuleDriver : MonoBehaviour
    {
        // 保留已绑定的 mini 配置资产；Driver 负责将配置应用到 main 风格的模块属性。
        [SerializeField] private ResourceSetting resourceSetting;
        private IResourceModule _resourceModule;
        private bool _initialized;
        private bool _forceUnloadUnusedAssets;
        private bool _preorderUnloadUnusedAssets;
        private bool _performGCCollect;
        private AsyncOperation _asyncOperation;
        private float _lastUnloadUnusedAssetsOperationElapseSeconds;
        [SerializeField] private float minUnloadUnusedAssetsInterval = 60f;
        [SerializeField] private float maxUnloadUnusedAssetsInterval = 300f;
        [SerializeField] private bool useSystemUnloadUnusedAssets = true;
        public float MinUnloadUnusedAssetsInterval { get => minUnloadUnusedAssetsInterval; set => minUnloadUnusedAssetsInterval = value; }
        public float MaxUnloadUnusedAssetsInterval { get => maxUnloadUnusedAssetsInterval; set => maxUnloadUnusedAssetsInterval = value; }
        public bool UseSystemUnloadUnusedAssets { get => useSystemUnloadUnusedAssets; set => useSystemUnloadUnusedAssets = value; }
        public float LastUnloadUnusedAssetsOperationElapseSeconds => _lastUnloadUnusedAssetsOperationElapseSeconds;
        public float AssetAutoReleaseInterval { get => _resourceModule.AssetAutoReleaseInterval; set => _resourceModule.AssetAutoReleaseInterval = value; }
        public int AssetCapacity { get => _resourceModule.AssetCapacity; set => _resourceModule.AssetCapacity = value; }
        public float AssetExpireTime { get => _resourceModule.AssetExpireTime; set => _resourceModule.AssetExpireTime = value; }
        public int AssetPriority { get => _resourceModule.AssetPriority; set => _resourceModule.AssetPriority = value; }
        public string PackageName => _resourceModule.DefaultPackageName;
        public string PackageVersion => _resourceModule.PackageVersion;
        public EPlayMode PlayMode => _resourceModule.PlayMode;
        private void Start() => Initialize();
        public void Initialize()
        {
            if (_initialized) return;
            if (resourceSetting == null) throw new GameFrameworkException("ResourceSetting is invalid.");
#if UNITY_EDITOR
            const bool editor = true;
#else
            const bool editor = false;
#endif
#if UNITY_WEBGL
            const bool webGL = true;
#else
            const bool webGL = false;
#endif
            var options = resourceSetting.GetOptions().CreateValidatedCopy(editor, webGL);
            var mode = editor ? options.EditorPlayMode : options.PlayMode;
            if (mode != EPlayMode.EditorSimulateMode && mode != EPlayMode.OfflinePlayMode)
                throw new NotSupportedException("mini 当前支持编辑器模拟和离线模式。");
            _resourceModule = ModuleSystem.GetModule<IResourceModule>();
            _resourceModule.DefaultPackageName = options.DefaultPackageName;
            _resourceModule.PlayMode = mode;
            _resourceModule.Milliseconds = options.Milliseconds;
            _resourceModule.AutoUnloadBundleWhenUnused = options.AutoUnloadBundleWhenUnused;
            _resourceModule.Initialize();
            _resourceModule.AssetAutoReleaseInterval = options.AssetAutoReleaseInterval;
            _resourceModule.AssetCapacity = options.AssetCapacity;
            _resourceModule.AssetExpireTime = options.AssetExpireTime;
            _resourceModule.AssetPriority = options.AssetPriority;
            _resourceModule.SetForceUnloadUnusedAssetsAction(ForceUnloadUnusedAssets);
            _initialized = true;
            Log.Info($"ResourceModule Run Mode：{PlayMode}");
        }
        private void OnDestroy()
        {
            _resourceModule?.SetForceUnloadUnusedAssetsAction(null);
        }
        #region 释放资源

        /// <summary>
        /// 强制执行释放未被使用的资源。
        /// </summary>
        /// <param name="performGCCollect">是否使用垃圾回收。</param>
        public void ForceUnloadUnusedAssets(bool performGCCollect)
        {
            _forceUnloadUnusedAssets = true;
            if (performGCCollect)
            {
                _performGCCollect = true;
            }
        }


        private void Update()
        {
            if (!_initialized) return;
            _lastUnloadUnusedAssetsOperationElapseSeconds += Time.unscaledDeltaTime;
            if (_asyncOperation == null &&
                (_forceUnloadUnusedAssets || useSystemUnloadUnusedAssets && (_lastUnloadUnusedAssetsOperationElapseSeconds >= maxUnloadUnusedAssetsInterval
                                                                             || _preorderUnloadUnusedAssets && _lastUnloadUnusedAssetsOperationElapseSeconds >=
                                                                             minUnloadUnusedAssetsInterval)
                )
               )
            {
                Log.Info("Unload unused assets...");
                _forceUnloadUnusedAssets = false;
                _preorderUnloadUnusedAssets = false;
                _lastUnloadUnusedAssetsOperationElapseSeconds = 0f;
                _asyncOperation = Resources.UnloadUnusedAssets();
                if (useSystemUnloadUnusedAssets)
                {
                    _resourceModule.UnloadUnusedAssets();
                }
            }

            if (_asyncOperation is { isDone: true })
            {
                _asyncOperation = null;
                if (_performGCCollect)
                {
                    Log.Info("GC.Collect...");
                    _performGCCollect = false;
                    GC.Collect();
                }
            }
        }

        #endregion
    }
}
