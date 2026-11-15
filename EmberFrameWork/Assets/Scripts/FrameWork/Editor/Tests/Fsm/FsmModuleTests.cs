using System.Collections.Generic;
using NUnit.Framework;
namespace EmberFrameWork.Tests
{
    public class FsmModuleTests
    {
        private class Owner { }
        private class State : FsmState<Owner> { public int Ticks; protected override void OnUpdate(IFsm<Owner> fsm, float elapsed, float real) { Ticks++; } }
        private IFsmModule _module;
        [SetUp] public void SetUp() { ModuleSystem.Shutdown(); _module = ModuleSystem.GetModule<IFsmModule>(); }
        [TearDown] public void TearDown() => ModuleSystem.Shutdown();
        [Test] public void NamedAndUnnamedMachinesAreSeparate()
        {
            var unnamed = _module.CreateFsm(new Owner(), new State());
            var named = _module.CreateFsm("game", new Owner(), new State());
            Assert.AreEqual(2, _module.Count); Assert.AreSame(unnamed, _module.GetFsm<Owner>());
            Assert.AreSame(named, _module.GetFsm<Owner>("game"));
            Assert.IsFalse(_module.HasFsm<Owner>("Game"));
        }
        [Test] public void ListingAndDestroyFollowRegistry()
        {
            var fsm = _module.CreateFsm("game", new Owner(), new State());
            var all = new List<FsmBase>(); _module.GetAllFsms(all); Assert.AreEqual(1, all.Count);
            Assert.IsTrue(_module.DestroyFsm(fsm)); Assert.AreEqual(0, _module.Count);
            Assert.IsFalse(_module.DestroyFsm<Owner>("game"));
        }
        [Test] public void ModuleSystemDrivesRegisteredMachines()
        {
            var state = new State(); var fsm = _module.CreateFsm(new Owner(), state);
            fsm.Start<State>(); ModuleSystem.Update(1, 1); Assert.AreEqual(1, state.Ticks);
        }
        [Test] public void DuplicateMachineAndInvalidOwnerAreRejected()
        {
            _module.CreateFsm(new Owner(), new State());
            Assert.Throws<GameFrameworkException>(() => _module.CreateFsm(new Owner(), new State()));
            Assert.Throws<GameFrameworkException>(() => _module.CreateFsm<Owner>("other", null, new State()));
        }
    }
}
