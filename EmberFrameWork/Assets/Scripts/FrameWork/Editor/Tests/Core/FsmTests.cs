using System.Collections.Generic;
using NUnit.Framework;
namespace EmberFrameWork.Tests
{
    public class FsmTests
    {
        public class Owner { public readonly List<string> Events = new List<string>(); }
        private class First : FsmState<Owner>
        {
            protected override void OnInit(IFsm<Owner> fsm) => fsm.Owner.Events.Add("init-first:" + fsm.FsmStateCount);
            protected override void OnEnter(IFsm<Owner> fsm) => fsm.Owner.Events.Add("enter-first");
            protected override void OnLeave(IFsm<Owner> fsm, bool shutdown) => fsm.Owner.Events.Add("leave-first:" + shutdown);
            public void Next(IFsm<Owner> fsm) => ChangeState<Second>(fsm);
        }
        private class Second : FsmState<Owner>
        {
            protected override void OnInit(IFsm<Owner> fsm) => fsm.Owner.Events.Add("init-second:" + fsm.FsmStateCount);
            protected override void OnEnter(IFsm<Owner> fsm) => fsm.Owner.Events.Add("enter-second");
        }
        private IFsmModule _manager;
        [SetUp] public void SetUp() { ModuleSystem.Shutdown(); _manager = ModuleSystem.GetModule<IFsmModule>(); }
        [TearDown] public void TearDown() => ModuleSystem.Shutdown();
        [Test] public void StatesAreInitializedAsTheyAreAdded()
        {
            var owner = new Owner(); _manager.CreateFsm(owner, new First(), new Second());
            CollectionAssert.AreEqual(new[] { "init-first:1", "init-second:2" }, owner.Events);
        }
        [Test] public void TransitionCallsLeaveBeforeEnterAndResetsTime()
        {
            var owner = new Owner(); var first = new First(); var fsm = _manager.CreateFsm(owner, first, new Second());
            fsm.Start<First>(); ModuleSystem.Update(2, 3); Assert.AreEqual(2, fsm.CurrentStateTime);
            owner.Events.Clear(); first.Next(fsm);
            CollectionAssert.AreEqual(new[] { "leave-first:False", "enter-second" }, owner.Events);
            Assert.AreEqual(0, fsm.CurrentStateTime); Assert.IsInstanceOf<Second>(fsm.CurrentState);
        }
        [Test] public void DataSupportsSetReplaceAndRemove()
        {
            var fsm = _manager.CreateFsm(new Owner(), new First());
            Assert.IsNull(fsm.GetData<string>("missing"));
            fsm.SetData("score", 1); fsm.SetData("score", 2);
            Assert.AreEqual(2, fsm.GetData<int>("score"));
            Assert.IsTrue(fsm.RemoveData("score")); Assert.IsFalse(fsm.HasData("score"));
        }
        [Test] public void DuplicateStartAndMissingStateUseFrameworkExceptions()
        {
            var fsm = _manager.CreateFsm(new Owner(), new First());
            Assert.Throws<GameFrameworkException>(() => fsm.Start<Second>());
            fsm.Start<First>(); Assert.Throws<GameFrameworkException>(() => fsm.Start<First>());
        }
        [Test] public void DestroyReturnsFsmToMemoryPool()
        {
            var fsm = _manager.CreateFsm(new Owner(), new First()); fsm.Start<First>();
            Assert.IsTrue(_manager.DestroyFsm(fsm)); Assert.IsTrue(fsm.IsDestroyed); Assert.IsNull(fsm.Owner);
            var reused = _manager.CreateFsm(new Owner(), new First());
            Assert.AreSame(fsm, reused); Assert.IsFalse(reused.IsDestroyed);
        }
    }
}
