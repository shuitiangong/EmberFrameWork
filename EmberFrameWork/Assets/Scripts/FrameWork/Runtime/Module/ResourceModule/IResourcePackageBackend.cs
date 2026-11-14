using System.Threading;
using Cysharp.Threading.Tasks;
using YooAsset;

namespace EmberFrameWork
{
    // 将流程控制与 Unity/YooAsset 的全局状态隔离，以便验证失败、并发等待和退出时序。
    internal interface IResourcePackageBackend
    {
        UniTask InitializeAsync(ResourceOptions options, EPlayMode playMode, CancellationToken cancellationToken);
        UniTask<string> RequestVersionAsync(CancellationToken cancellationToken);
        UniTask LoadManifestAsync(string version, CancellationToken cancellationToken);
        void Shutdown();
    }
}
