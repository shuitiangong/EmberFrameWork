using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using YooAsset;

namespace EmberFrameWork.Tests
{
    public class ResourceInitializationTests
    {
        private sealed class Backend : IResourcePackageBackend
        {
            public readonly List<string> Calls = new List<string>();
            public string Failure;
            public string Version = "1.0";
            public TaskCompletionSource<bool> Gate;
            public CancellationToken Lifetime;
            public async UniTask InitializeAsync(ResourceOptions options, EPlayMode mode, CancellationToken token)
            {
                Calls.Add("init"); Lifetime = token;
                if (Failure == "init") throw new InvalidOperationException("init failed");
                // 故意不自动取消，验证模块能拒绝关闭后到达的成功结果。
                if (Gate != null) await Gate.Task.AsUniTask(false);
            }
            public UniTask<string> RequestVersionAsync(CancellationToken token)
            {
                Calls.Add("version");
                if (Failure == "version") return UniTask.FromException<string>(new InvalidOperationException("version failed"));
                return UniTask.FromResult(Version);
            }
            public UniTask LoadManifestAsync(string version, CancellationToken token)
            {
                Calls.Add("manifest:" + version);
                if (Failure == "manifest") return UniTask.FromException(new InvalidOperationException("manifest failed"));
                return UniTask.CompletedTask;
            }
            public void Shutdown() => Calls.Add("shutdown");
        }

        private Backend _backend;
        private ResourceModule _module;
        [SetUp] public void SetUp() { _backend = new Backend(); _module = new ResourceModule(_backend); _module.Configure(new ResourceOptions()); }
        [TearDown] public void TearDown() => _module.Shutdown();

        [Test] public async Task SuccessRequiresAllThreeStages()
        {
            Assert.AreEqual("1.0", await _module.InitPackage());
            CollectionAssert.AreEqual(new[] { "init", "version", "manifest:1.0" }, _backend.Calls);
            Assert.IsTrue(_module.IsReady);
            Assert.AreEqual("1.0", _module.PackageVersion);
            Assert.IsNull(_module.LastError);
            await _module.InitPackage();
            Assert.AreEqual(3, _backend.Calls.Count);
        }

        [Test] public async Task ConcurrentRequestsShareInitialization()
        {
            _backend.Gate = new TaskCompletionSource<bool>();
            var first = _module.InitPackage().AsTask();
            var second = _module.InitPackage().AsTask();
            Assert.AreEqual(1, _backend.Calls.Count);
            Assert.IsFalse(_module.IsReady);
            _backend.Gate.SetResult(true);
            Assert.AreEqual("1.0", await first);
            Assert.AreEqual("1.0", await second);
            Assert.AreEqual(3, _backend.Calls.Count);
        }

        [Test] public async Task EachFailureStopsSequenceAndCanRetry()
        {
            foreach (string stage in new[] { "init", "version", "manifest" })
            {
                _backend.Failure = stage;
                _backend.Calls.Clear();
                try { await _module.InitPackage(); Assert.Fail("Expected failure"); }
                catch (InvalidOperationException) { }
                Assert.AreEqual(ResourceInitializationState.Failed, _module.InitializationState);
                Assert.IsNull(_module.PackageVersion);
                StringAssert.Contains(stage + " failed", _module.LastError);
                Assert.AreEqual(stage == "init" ? 1 : stage == "version" ? 2 : 3, _backend.Calls.Count);
            }
            _backend.Failure = null;
            Assert.AreEqual("1.0", await _module.InitPackage());
            Assert.IsNull(_module.LastError);
        }

        [Test] public async Task EmptyVersionNeverLoadsManifest()
        {
            _backend.Version = "";
            try { await _module.InitPackage(); Assert.Fail("Expected failure"); }
            catch (InvalidOperationException) { }
            CollectionAssert.AreEqual(new[] { "init", "version" }, _backend.Calls);
            Assert.IsFalse(_module.IsReady);
        }

        [Test] public async Task ShutdownRejectsLateSuccessAndCancelsLifetime()
        {
            _backend.Gate = new TaskCompletionSource<bool>();
            var pending = _module.InitPackage().AsTask();
            _module.Shutdown();
            Assert.IsTrue(_backend.Lifetime.IsCancellationRequested);
            _backend.Gate.SetResult(true);
            try { await pending; Assert.Fail("Expected cancellation"); }
            catch (OperationCanceledException) { }
            Assert.AreEqual(ResourceInitializationState.Shutdown, _module.InitializationState);
            Assert.IsFalse(_module.IsReady);
            CollectionAssert.AreEqual(new[] { "init", "shutdown" }, _backend.Calls);
            Assert.Throws<ObjectDisposedException>(() => _module.InitPackage());
            _module.Shutdown();
            Assert.AreEqual(2, _backend.Calls.Count);
        }

        [Test] public void UnconfiguredModuleDoesNotStartBackend()
        {
            var backend = new Backend(); var module = new ResourceModule(backend);
            Assert.Throws<InvalidOperationException>(() => module.InitPackage());
            Assert.IsEmpty(backend.Calls);
            module.Shutdown();
        }
    }
}
