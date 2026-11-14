using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using YooAsset;

namespace EmberFrameWork.Tests
{
    public class StartupProcedureTests
    {
        private sealed class Backend : IResourcePackageBackend
        {
            public TaskCompletionSource<bool> Gate = new TaskCompletionSource<bool>();
            public int Starts;
            public async UniTask InitializeAsync(ResourceOptions options, EPlayMode mode, CancellationToken token)
            {
                Starts++;
                await Gate.Task.AsUniTask(false);
            }
            public UniTask<string> RequestVersionAsync(CancellationToken token) => UniTask.FromResult("test-version");
            public UniTask LoadManifestAsync(string version, CancellationToken token) => UniTask.CompletedTask;
            public void Shutdown() { }
        }
        private Backend _backend;
        private ResourceModule _resource;
        private IProcedureModule _procedures;
        private ProcedureBase _init;
        private static Type ProcedureType(string name) => ProcedureSetting.ResolveProcedureType("Procedure." + name + ", Assembly-CSharp");
        private static ProcedureBase Create(string name) => (ProcedureBase)Activator.CreateInstance(ProcedureType(name));
        private bool Retry() => (bool)_init.GetType().GetMethod("Retry").Invoke(_init, null);
        private string Error => (string)_init.GetType().GetProperty("LastError").GetValue(_init);
        private Task Pending => (Task)_init.GetType().GetField("_pending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(_init);

        [SetUp] public void SetUp()
        {
            ModuleSystem.Shutdown();
            GameFrameworkLog.SetLogHelper(null);
            _backend = new Backend(); _resource = new ResourceModule(_backend);
            ModuleSystem.RegisterModule<IResourceModule>(_resource);
            _resource.Configure(new ResourceOptions());
            _procedures = ModuleSystem.GetModule<IProcedureModule>();
            _init = Create("ProcedureInitResources");
            _procedures.Initialize(ModuleSystem.GetModule<IFsmModule>(), Create("ProcedureLaunch"), _init, Create("ProcedureStartGame"));
            _procedures.StartProcedure(ProcedureType("ProcedureLaunch"));
        }

        [TearDown] public void TearDown()
        {
            try { ModuleSystem.Shutdown(); }
            finally { GameFrameworkLog.ResetLogHelper(); }
        }

        [Test] public async Task StartupWaitsForResourceReadiness()
        {
            Assert.AreEqual("ProcedureLaunch", _procedures.CurrentProcedure.GetType().Name);
            ModuleSystem.Update(0, 0);
            Assert.AreSame(_init, _procedures.CurrentProcedure);
            ModuleSystem.Update(1, 1);
            Assert.AreSame(_init, _procedures.CurrentProcedure);
            Assert.IsFalse(Retry());
            var pending = Pending;
            _backend.Gate.SetResult(true); await pending;
            // 异步结束本身不切状态；下一次流程更新才进入游戏。
            Assert.AreSame(_init, _procedures.CurrentProcedure);
            ModuleSystem.Update(0, 0);
            Assert.AreEqual("ProcedureStartGame", _procedures.CurrentProcedure.GetType().Name);
            Assert.AreEqual(1, _backend.Starts);
        }

        [Test] public async Task FailureStaysInInitializationAndRetryCanRecover()
        {
            ModuleSystem.Update(0, 0);
            var pending = Pending;
            _backend.Gate.SetException(new InvalidOperationException("missing package"));
            try { await pending; Assert.Fail("Expected failure"); } catch (InvalidOperationException) { }
            ModuleSystem.Update(0, 0);
            Assert.AreSame(_init, _procedures.CurrentProcedure);
            StringAssert.Contains("missing package", Error);
            _backend.Gate = new TaskCompletionSource<bool>();
            Assert.IsTrue(Retry()); Assert.IsFalse(Retry());
            Assert.IsNull(Error);
            pending = Pending;
            _backend.Gate.SetResult(true); await pending;
            ModuleSystem.Update(0, 0);
            Assert.AreEqual("ProcedureStartGame", _procedures.CurrentProcedure.GetType().Name);
            Assert.AreEqual(2, _backend.Starts);
        }

        [Test] public async Task ShutdownPreventsLateCompletionFromEnteringGame()
        {
            ModuleSystem.Update(0, 0);
            var pending = Pending;
            ModuleSystem.Shutdown();
            _backend.Gate.SetResult(true);
            try { await pending; Assert.Fail("Expected cancellation"); } catch (OperationCanceledException) { }
            Assert.AreEqual(0, ModuleSystem.Count);
            Assert.IsFalse(_procedures.IsInitialized);
            Assert.IsFalse(Retry());
        }

        [Test] public void StartingGameWithoutResourcesIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => _procedures.RestartProcedure(Create("ProcedureStartGame")));
            Assert.IsFalse(_resource.IsReady);
        }
    }
}
