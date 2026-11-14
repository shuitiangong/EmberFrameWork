using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace EmberFrameWork
{
    internal sealed partial class ResourceModule
    {
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        // Task 支持多个等待者，避免重复 await 单消费者 UniTask。
        private Task<string> _initializationTask;
        public ResourceInitializationState InitializationState { get; private set; }
        public bool IsReady => InitializationState == ResourceInitializationState.Ready;
        public string PackageVersion { get; private set; }
        public string LastError { get; private set; }

        public UniTask<string> InitPackage()
        {
            if (_shutdown) throw new ObjectDisposedException(nameof(ResourceModule));
            var options = RequireOptions();
            if (_initializationTask == null || _initializationTask.IsCompleted && !IsReady)
                _initializationTask = RunInitialization(options, _lifetime.Token);
            return _initializationTask.AsUniTask(useCurrentSynchronizationContext: SynchronizationContext.Current != null);
        }

        private async Task<string> RunInitialization(ResourceOptions options, CancellationToken cancellationToken)
        {
            LastError = null;
            PackageVersion = null;
            try
            {
                InitializationState = ResourceInitializationState.InitializingPackage;
                await _backend.InitializeAsync(options, PlayMode, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                InitializationState = ResourceInitializationState.RequestingVersion;
                string version = await _backend.RequestVersionAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(version)) throw new InvalidOperationException("资源包返回了空版本号。");
                InitializationState = ResourceInitializationState.LoadingManifest;
                await _backend.LoadManifestAsync(version, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                PackageVersion = version;
                InitializationState = ResourceInitializationState.Ready;
                return version;
            }
            catch (Exception exception)
            {
                if (!_shutdown)
                {
                    LastError = $"[{options.DefaultPackageName}] {InitializationState}: {exception.Message}";
                    InitializationState = ResourceInitializationState.Failed;
                }
                throw;
            }
        }
    }
}
