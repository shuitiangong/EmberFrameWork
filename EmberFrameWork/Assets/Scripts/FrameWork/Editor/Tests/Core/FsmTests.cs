using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public class FsmTests
    {
        private sealed class Owner
        {
            public readonly List<string> Events = new List<string>();
        }

        private abstract class RecordingState : FsmState<Owner>
        {
            public Action<IFsm<Owner>> Init;
            public Action<IFsm<Owner>> Enter;
            public Action<IFsm<Owner>> Tick;
            public Action<IFsm<Owner>> Leave;
            public Action<IFsm<Owner>> Destroy;
            public float RealTime;
            public void Go<TState>(IFsm<Owner> fsm) where TState : FsmState<Owner> => ChangeState<TState>(fsm);
            public void Go(IFsm<Owner> fsm, Type type) => ChangeState(fsm, type);
            protected override void OnInit(IFsm<Owner> fsm) { Record(fsm, "init"); Init?.Invoke(fsm); }
            protected override void OnEnter(IFsm<Owner> fsm) { Record(fsm, "enter"); Enter?.Invoke(fsm); }
            protected override void OnUpdate(IFsm<Owner> fsm, float elapsed, float real)
            {
                RealTime = real;
                Record(fsm, "update");
                Tick?.Invoke(fsm);
            }
            protected override void OnLeave(IFsm<Owner> fsm, bool shutdown)
            {
                Record(fsm, shutdown ? "leave-shutdown" : "leave");
                Leave?.Invoke(fsm);
            }
            protected override void OnDestroy(IFsm<Owner> fsm) { Record(fsm, "destroy"); Destroy?.Invoke(fsm); }
            private void Record(IFsm<Owner> fsm, string action) => fsm.Owner.Events.Add(GetType().Name + ":" + action);
        }

        private sealed class A : RecordingState { }
        private sealed class B : RecordingState { }
        private sealed class Missing : FsmState<Owner> { }

        [Test]
        public void LifecycleOrdersCallbacksAndResetsStateTime()
        {
            var owner = new Owner();
            var a = new A();
            var b = new B();
            var fsm = Fsm<Owner>.Create("Startup", owner, a, b);
            Assert.That(fsm.IsRunning, Is.False);
            fsm.Update(1, 2);
            fsm.Start<A>();
            fsm.Update(0.25f, 0.5f);
            Assert.That(fsm.CurrentStateTime, Is.EqualTo(0.25f));
            Assert.That(a.RealTime, Is.EqualTo(0.5f));
            a.Go<B>(fsm);
            Assert.That(fsm.CurrentState, Is.SameAs(b));
            Assert.That(fsm.CurrentStateTime, Is.Zero);
            fsm.Shutdown();
            fsm.Shutdown();
            fsm.Update(1, 1);
            Assert.That(owner.Events, Is.EqualTo(new[] {
                "A:init", "B:init", "A:enter", "A:update", "A:leave", "B:enter",
                "B:leave-shutdown", "A:destroy", "B:destroy"
            }));
            Assert.That(fsm.IsDestroyed, Is.True);
            Assert.That(fsm.IsRunning, Is.False);
            Assert.That(fsm.Owner, Is.Null);
            Assert.That(fsm.FsmStateCount, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => fsm.Start<A>());
            Assert.Throws<ObjectDisposedException>(() => fsm.SetData("x", 1));
        }

        [Test]
        public void InitCanFindAllStatesAndSetSharedData()
        {
            var a = new A { Init = fsm => {
                Assert.That(fsm.HasState<B>(), Is.True);
                fsm.SetData("value", 42);
            }};
            var fsm = Fsm<Owner>.Create(null, new Owner(), new List<FsmState<Owner>> { a, new B() });
            Assert.That(fsm.Name, Is.Empty);
            Assert.That(fsm.GetData<int>("value"), Is.EqualTo(42));
            Assert.That(fsm.GetData<string>("missing"), Is.Null);
            Assert.That(fsm.HasData("Value"), Is.False);
            Assert.Throws<InvalidCastException>(() => fsm.GetData<string>("value"));
            Assert.That(fsm.RemoveData("value"), Is.True);
            Assert.That(fsm.RemoveData("value"), Is.False);
            var states = new List<FsmState<Owner>> { new Missing() };
            fsm.GetAllStates(states);
            Assert.That(states, Is.EqualTo(fsm.GetAllStates()));
            fsm.Shutdown();
        }

        [Test]
        public void InvalidRegistrationDoesNotInitializeOrBindStates()
        {
            var owner = new Owner();
            var a = new A();
            Assert.Throws<ArgumentNullException>(() => Fsm<Owner>.Create("", null, a));
            Assert.Throws<ArgumentException>(() => Fsm<Owner>.Create("", owner, new FsmState<Owner>[0]));
            Assert.Throws<ArgumentException>(() => Fsm<Owner>.Create("", owner, a, null));
            Assert.Throws<ArgumentException>(() => Fsm<Owner>.Create("", owner, a, new A()));
            Assert.That(owner.Events, Is.Empty);
            var valid = Fsm<Owner>.Create("", owner, a);
            valid.Shutdown();
        }

        [Test]
        public void LiveStateCannotBeSharedButCanBeReusedAfterShutdown()
        {
            var a = new A();
            var first = Fsm<Owner>.Create("First", new Owner(), a);
            Assert.Throws<InvalidOperationException>(() => Fsm<Owner>.Create("Second", new Owner(), a));
            first.Shutdown();
            var second = Fsm<Owner>.Create("Second", new Owner(), a);
            second.Start<A>();
            second.Shutdown();
        }

        [Test]
        public void FailedInitializationCleansAttemptedStatesAndReleasesBindings()
        {
            var owner = new Owner();
            var a = new A();
            var b = new B { Init = _ => throw new InvalidOperationException("init") };
            Assert.Throws<InvalidOperationException>(() => Fsm<Owner>.Create("", owner, a, b));
            Assert.That(owner.Events, Is.EqualTo(new[] { "A:init", "B:init", "B:destroy", "A:destroy" }));
            b.Init = null;
            var fsm = Fsm<Owner>.Create("", new Owner(), a, b);
            fsm.Shutdown();
        }

        [Test]
        public void InvalidTransitionsPreserveTheCurrentState()
        {
            var a = new A();
            var b = new B();
            var fsm = Fsm<Owner>.Create("", new Owner(), a, b);
            Assert.Throws<ArgumentNullException>(() => fsm.Start(null));
            Assert.Throws<ArgumentException>(() => fsm.Start(typeof(string)));
            Assert.Throws<InvalidOperationException>(() => fsm.Start<Missing>());
            fsm.Start(typeof(A));
            Assert.Throws<InvalidOperationException>(() => fsm.Start<A>());
            Assert.Throws<InvalidOperationException>(() => a.Go<Missing>(fsm));
            Assert.Throws<InvalidOperationException>(() => b.Go<A>(fsm));
            Assert.That(fsm.CurrentState, Is.SameAs(a));
            a.Go(fsm, typeof(B));
            Assert.That(fsm.CurrentState, Is.SameAs(b));
            fsm.Shutdown();
        }

        [Test]
        public void OnEnterCanImmediatelyAdvanceToNextState()
        {
            var a = new A();
            a.Enter = fsm => a.Go<B>(fsm);
            var fsm = Fsm<Owner>.Create("", new Owner(), a, new B());
            fsm.Start<A>();
            Assert.That(fsm.CurrentState, Is.TypeOf<B>());
            fsm.Shutdown();
        }

        [Test]
        public void UpdateCanSwitchStateAndShutdownWithoutAnotherTick()
        {
            var a = new A();
            var b = new B { Tick = fsm => fsm.Shutdown() };
            a.Tick = fsm => a.Go<B>(fsm);
            var owner = new Owner();
            var fsm = Fsm<Owner>.Create("", owner, a, b);
            fsm.Start<A>();
            fsm.Update(1, 1);
            Assert.That(fsm.CurrentStateTime, Is.Zero);
            Assert.That(owner.Events, Does.Not.Contain("B:update"));
            fsm.Update(1, 1);
            Assert.That(fsm.IsDestroyed, Is.True);
        }

        [TestCase("enter")]
        [TestCase("update")]
        [TestCase("leave")]
        public void CallbackFailureDestroysFsmWithoutLeakingStates(string phase)
        {
            var a = new A();
            Action<IFsm<Owner>> fail = _ => throw new InvalidOperationException(phase);
            if (phase == "enter") a.Enter = fail;
            if (phase == "update") a.Tick = fail;
            if (phase == "leave") a.Leave = fail;
            var owner = new Owner();
            var fsm = Fsm<Owner>.Create("", owner, a, new B());
            if (phase == "enter") Assert.Throws<InvalidOperationException>(() => fsm.Start<A>());
            else
            {
                fsm.Start<A>();
                if (phase == "update") Assert.Throws<InvalidOperationException>(() => fsm.Update(1, 1));
                else Assert.Throws<InvalidOperationException>(() => a.Go<B>(fsm));
            }
            Assert.That(fsm.IsDestroyed, Is.True);
            Assert.That(fsm.Owner, Is.Null);
            Assert.That(owner.Events, Does.Contain("A:destroy"));
            Assert.That(owner.Events, Does.Contain("B:destroy"));
            if (phase == "leave") Assert.That(owner.Events, Does.Not.Contain("A:leave-shutdown"));
        }

        [Test]
        public void ShutdownContinuesCleanupAfterCallbackErrors()
        {
            var a = new A {
                Leave = fsm => { fsm.Shutdown(); throw new Exception("leave"); },
                Destroy = _ => throw new Exception("destroy")
            };
            var owner = new Owner();
            var fsm = Fsm<Owner>.Create("", owner, a, new B());
            fsm.Start<A>();
            var error = Assert.Throws<AggregateException>(() => fsm.Shutdown());
            Assert.That(error.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(owner.Events, Does.Contain("B:destroy"));
            Assert.That(fsm.Owner, Is.Null);
            Assert.That(fsm.GetAllStates(), Is.Empty);
            Assert.DoesNotThrow(() => fsm.Shutdown());
        }

        [Test]
        public void OnLeaveCannotStartAnotherStateAndResurrectFsm()
        {
            var a = new A { Leave = fsm => fsm.Start<B>() };
            var fsm = Fsm<Owner>.Create("", new Owner(), a, new B());
            fsm.Start<A>();
            Assert.Throws<InvalidOperationException>(() => a.Go<B>(fsm));
            Assert.That(fsm.IsDestroyed, Is.True);
            Assert.That(fsm.IsRunning, Is.False);
        }
    }
}
