using YooAsset;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace EmberFrameWork
{
    /// <summary>资源模块的配置及默认资源包初始化入口；所有方法应从 Unity 主线程调用。</summary>
    public interface IResourceModule
    {
        /// <summary>仅表示配置已接收，不表示资源包已初始化。</summary>
        bool IsConfigured { get; }
        string DefaultPackageName { get; }
        /// <summary>当前实际使用的模式：编辑器取 EditorPlayMode，播放器取 PlayMode。</summary>
        EPlayMode PlayMode { get; }
        /// <summary>返回配置副本，修改副本不会影响模块。</summary>
        ResourceOptions Options { get; }
        ResourceInitializationState InitializationState { get; }
        bool IsReady { get; }
        string PackageVersion { get; }
        string LastError { get; }
        /// <summary>初始化默认包、获取版本并加载清单。重复调用共享任务；失败后可重试。</summary>
        UniTask<string> InitPackage();
        /// <summary>加载资源；每次成功调用都需要对应一次 UnloadAsset。指定包须已初始化。</summary>
        T LoadAsset<T>(string location, string packageName = "") where T : UnityEngine.Object;
        /// <summary>保留 TEngine 原等待和取消语义；加载等待取消时返回 null，缓存命中后 Yield。</summary>
        UniTask<T> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default, string packageName = "") where T : UnityEngine.Object;
        /// <summary>加载 Prefab 并实例化；实例的 AssetsReference 在 OnDestroy 中归还资源。</summary>
        UnityEngine.GameObject LoadGameObject(string location, UnityEngine.Transform parent = null, string packageName = "");
        UniTask<UnityEngine.GameObject> LoadGameObjectAsync(string location, UnityEngine.Transform parent = null,
            CancellationToken cancellationToken = default, string packageName = "");
        float AssetAutoReleaseInterval { get; set; }
        int AssetCapacity { get; set; }
        float AssetExpireTime { get; set; }
        int AssetPriority { get; set; }
        /// <summary>归还一次资源借用；引用归零后仍可缓存，直到过期或主动清理。</summary>
        void UnloadAsset(object asset);
        /// <summary>释放所有空闲缓存，再等待 YooAsset 清理未使用资源。</summary>
        UniTask UnloadUnusedAssetsAsync();
        void Configure(ResourceOptions options);
        void Configure(ResourceSetting setting);
    }
}
