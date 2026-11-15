using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
namespace EmberFrameWork.Tests
{
    public class ProcedureTests
    {
        public class First : ProcedureBase
        {
            public readonly List<string> Events = new List<string>();
            protected override void OnEnter(IFsm<IProcedureModule> fsm) => Events.Add("enter");
            protected override void OnLeave(IFsm<IProcedureModule> fsm, bool shutdown) => Events.Add("leave:" + shutdown);
            public void Next(IFsm<IProcedureModule> fsm) => ChangeState<Second>(fsm);
        }
        public class Second : ProcedureBase { }
        private IProcedureModule _module;
        private IFsmModule _fsms;
        [SetUp] public void SetUp() { ModuleSystem.Shutdown(); _module = ModuleSystem.GetModule<IProcedureModule>(); _fsms = ModuleSystem.GetModule<IFsmModule>(); }
        [TearDown] public void TearDown() => ModuleSystem.Shutdown();
        [Test] public void AccessBeforeInitializationUsesOriginalException()
        {
            Assert.Throws<GameFrameworkException>(() => { var ignored = _module.CurrentProcedure; });
            Assert.Throws<GameFrameworkException>(() => _module.StartProcedure<First>());
        }
        [Test] public void InitializeStartUpdateAndTransition()
        {
            var first = new First(); _module.Initialize(_fsms, first, new Second());
            Assert.IsTrue(_module.HasProcedure<First>()); Assert.AreSame(first, _module.GetProcedure<First>());
            _module.StartProcedure<First>(); ModuleSystem.Update(2, 3);
            Assert.AreEqual(2, _module.CurrentProcedureTime);
            first.Next(_fsms.GetFsm<IProcedureModule>());
            Assert.IsInstanceOf<Second>(_module.CurrentProcedure);
            CollectionAssert.AreEqual(new[] { "enter", "leave:False" }, first.Events);
        }
        [Test] public void RestartEntersFirstSuppliedProcedure()
        {
            _module.Initialize(_fsms, new First(), new Second()); _module.StartProcedure<First>();
            Assert.IsTrue(_module.RestartProcedure(new Second(), new First()));
            Assert.IsInstanceOf<Second>(_module.CurrentProcedure);
        }
        [Test] public void ShutdownDestroysProcedureFsm()
        {
            _module.Initialize(_fsms, new First()); _module.StartProcedure<First>();
            ((Module)_module).Shutdown(); Assert.IsFalse(_fsms.HasFsm<IProcedureModule>());
            Assert.Throws<GameFrameworkException>(() => { var ignored = _module.CurrentProcedure; });
        }
        [Test] public void SettingsRetainOriginalAsyncContractAndAssemblyLookup()
        {
            Assert.AreEqual(typeof(UniTaskVoid), typeof(ProcedureSetting).GetMethod("StartProcedure").ReturnType);
            Assert.AreEqual(typeof(First), Utility.Assembly.GetType(typeof(First).AssemblyQualifiedName));
            Assert.IsNull(typeof(IProcedureModule).GetProperty("IsInitialized"));
        }
    }
}
