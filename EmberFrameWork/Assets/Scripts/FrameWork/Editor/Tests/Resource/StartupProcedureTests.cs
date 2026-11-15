using System;
using System.Collections;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using YooAsset;
namespace EmberFrameWork.Tests
{
    public class StartupProcedureTests
    {
        private sealed class Resource : ResourceTestModule
        {
            public TaskCompletionSource<InitializePackageOperation> Gate = new TaskCompletionSource<InitializePackageOperation>();
            public override UniTask<InitializePackageOperation> InitPackage(string packageName, bool needInitMainFest = false)
            {
                Assert.IsTrue(needInitMainFest);
                return Gate.Task.AsUniTask(false);
            }
        }
        private Resource _resource;
        private IProcedureModule _procedures;
        private ProcedureBase _init;
        private static Type ProcedureType(string name) => Utility.Assembly.GetType("Procedure." + name + ", Assembly-CSharp");
        private static ProcedureBase Create(string name) => (ProcedureBase)Activator.CreateInstance(ProcedureType(name));
        private bool Retry() => (bool)_init.GetType().GetMethod("Retry").Invoke(_init, null);
        private string Error => (string)_init.GetType().GetProperty("LastError").GetValue(_init);
        private Task Pending => (Task)_init.GetType().GetField("_pending", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(_init);
        [SetUp] public void SetUp()
        {
            ModuleSystem.Shutdown();
            GameFrameworkLog.SetLogHelper(null);
            _resource = new Resource { DefaultPackageName = "DefaultPackage", PackageVersion = "test" };
            ModuleSystem.RegisterModule<IResourceModule>(_resource);
            _procedures = ModuleSystem.GetModule<IProcedureModule>();
            _init = Create("ProcedureInitResources");
            _procedures.Initialize(ModuleSystem.GetModule<IFsmModule>(), Create("ProcedureLaunch"), _init, Create("ProcedureStartGame"));
            _procedures.StartProcedure(ProcedureType("ProcedureLaunch"));
        }
        [TearDown] public void TearDown()
        {
            ModuleSystem.Shutdown();
            GameFrameworkLog.SetLogHelper(new DefaultLogHelper());
        }
        [Test] public void StartupWaitsAndFailureCanRetry()
        {
            ModuleSystem.Update(0, 0);
            Assert.AreSame(_init, _procedures.CurrentProcedure);
            ModuleSystem.Update(0, 0);
            Assert.AreSame(_init, _procedures.CurrentProcedure);
            Assert.IsFalse(Retry());
            var pending = Pending;
            _resource.Gate.SetException(new InvalidOperationException("missing manifest"));
            Assert.Throws<InvalidOperationException>(() => pending.GetAwaiter().GetResult());
            ModuleSystem.Update(0, 0);
            StringAssert.Contains("missing manifest", Error);
            _resource.Gate = new TaskCompletionSource<InitializePackageOperation>();
            Assert.IsTrue(Retry());
            Assert.IsFalse(Retry());
            Assert.IsNull(Error);
        }
        [Test] public void StartingGameWithoutManifestIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => _procedures.RestartProcedure(Create("ProcedureStartGame")));
        }
        [Test] public void ShutdownDoesNotEnterGameOnLateCompletion()
        {
            ModuleSystem.Update(0, 0);
            ModuleSystem.Shutdown();
            _resource.Gate.SetResult(null);
            Assert.IsFalse(Retry());
            Assert.Throws<GameFrameworkException>(() => { var unused = _procedures.CurrentProcedure; });
        }
    }
}
