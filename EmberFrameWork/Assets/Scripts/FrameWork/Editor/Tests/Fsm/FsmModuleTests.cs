using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public class FsmModuleTests
    {
        private sealed class Owner { }
        private sealed class OtherOwner { }
        private sealed class OtherState : FsmState<OtherOwner> { }
        private sealed class State : FsmState<Owner>
        {
            public Action<IFsm<Owner>> Init;
            public Action<IFsm<Owner>> Tick;
            public Action<IFsm<Owner>> Destroy;
            public int Updates;
            public int Destroys;
            public int ShutdownLeaves;
            public float RealTime;
            protected override void OnInit(IFsm<Owner> fsm) => Init?.Invoke(fsm);
            protected override void OnUpdate(IFsm<Owner> fsm, float elapsed, float real)
            {
                Updates++;
                RealTime = real;
                Tick?.Invoke(fsm);
            }
            protected override void OnLeave(IFsm<Owner> fsm, bool shutdown)
            {
                if (shutdown) ShutdownLeaves++;
            }
            protected override void OnDestroy(IFsm<Owner> fsm)
            {
                Destroys++;
                Destroy?.Invoke(fsm);
            }
        }

        [SetUp] public void SetUp() => ModuleSystem.Shutdown();
        [TearDown] public void TearDown() => ModuleSystem.Shutdown();

        [Test]
        public void ModuleSystemResolvesAndDrivesFsmLifecycle()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            Assert.That(ModuleSystem.GetModule<IFsmModule>(), Is.SameAs(module));
            Assert.That(((Module)module).Priority, Is.EqualTo(1));
            var state = new State();
            var fsm = module.CreateFsm(new Owner(), state);
            ModuleSystem.Update(1, 2);
            Assert.That(state.Updates, Is.Zero);
            fsm.Start<State>();
            ModuleSystem.Update(0.25f, 0.5f);
            Assert.That(state.Updates, Is.EqualTo(1));
            Assert.That(state.RealTime, Is.EqualTo(0.5f));
            Assert.That(fsm.CurrentStateTime, Is.EqualTo(0.25f));
            ModuleSystem.Shutdown();
            Assert.That(fsm.IsDestroyed, Is.True);
            Assert.That(state.ShutdownLeaves, Is.EqualTo(1));
            Assert.That(state.Destroys, Is.EqualTo(1));
            Assert.That(module.Count, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => module.CreateFsm(new Owner(), new State()));
            Assert.That(ModuleSystem.GetModule<IFsmModule>(), Is.Not.SameAs(module));
        }

        [Test]
        public void QueriesUseOwnerTypeAndCaseSensitiveNameAndAllOverloadsAgree()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            var unnamed = module.CreateFsm(new Owner(), new List<FsmState<Owner>> { new State() });
            var named = module.CreateFsm("game", new Owner(), new List<FsmState<Owner>> { new State() });
            var other = module.CreateFsm("game", new OtherOwner(), new OtherState());
            Assert.That(module.Count, Is.EqualTo(3));
            Assert.That(module.GetFsm<Owner>(), Is.SameAs(unnamed));
            Assert.That(module.GetFsm(typeof(Owner)), Is.SameAs(unnamed));
            Assert.That(module.GetFsm<Owner>(null), Is.SameAs(unnamed));
            Assert.That(module.GetFsm<Owner>("game"), Is.SameAs(named));
            Assert.That(module.GetFsm(typeof(OtherOwner), "game"), Is.SameAs(other));
            Assert.That(module.HasFsm<Owner>(), Is.True);
            Assert.That(module.HasFsm(typeof(Owner)), Is.True);
            Assert.That(module.HasFsm<Owner>("game"), Is.True);
            Assert.That(module.HasFsm(typeof(OtherOwner), "game"), Is.True);
            Assert.That(module.HasFsm<Owner>("Game"), Is.False);
            Assert.Throws<InvalidOperationException>(() => module.CreateFsm((string)null, new Owner(), new State()));
            var list = new List<FsmBase> { (FsmBase)named };
            module.GetAllFsms(list);
            Assert.That(list, Is.EqualTo(new object[] { unnamed, named, other }));
            Assert.That(module.GetAllFsms(), Is.EqualTo(list));
        }

        [Test]
        public void DestroyOverloadsRemoveOnlyTheRequestedFsm()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            module.CreateFsm(new Owner(), new State());
            Assert.That(module.DestroyFsm<Owner>(), Is.True);
            module.CreateFsm(new Owner(), new State());
            Assert.That(module.DestroyFsm(typeof(Owner)), Is.True);
            module.CreateFsm("a", new Owner(), new State());
            Assert.That(module.DestroyFsm<Owner>("a"), Is.True);
            module.CreateFsm("a", new Owner(), new State());
            Assert.That(module.DestroyFsm(typeof(Owner), "a"), Is.True);
            var fsm = module.CreateFsm(new Owner(), new State());
            Assert.That(module.DestroyFsm(fsm), Is.True);
            fsm = module.CreateFsm(new Owner(), new State());
            Assert.That(module.DestroyFsm((FsmBase)fsm), Is.True);
            Assert.That(module.DestroyFsm<Owner>(), Is.False);
            Assert.That(module.Count, Is.Zero);
        }

        [Test]
        public void StaleOrExternalInstanceCannotDestroyRegisteredReplacement()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            var old = module.CreateFsm("a", new Owner(), new State());
            module.DestroyFsm(old);
            var replacement = module.CreateFsm("a", new Owner(), new State());
            var external = Fsm<Owner>.Create("a", new Owner(), new State());
            try
            {
                Assert.That(module.DestroyFsm(old), Is.False);
                Assert.That(module.DestroyFsm((FsmBase)external), Is.False);
                Assert.That(module.GetFsm<Owner>("a"), Is.SameAs(replacement));
                Assert.That(replacement.IsDestroyed, Is.False);
            }
            finally { external.Shutdown(); }
        }

        [Test]
        public void InitializationFailureReleasesKeyForRetry()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            var state = new State { Init = _ => throw new InvalidOperationException("init") };
            Assert.Throws<InvalidOperationException>(() => module.CreateFsm("a", new Owner(), state));
            Assert.That(module.Count, Is.Zero);
            Assert.That(state.Destroys, Is.EqualTo(1));
            state.Init = null;
            Assert.DoesNotThrow(() => module.CreateFsm("a", new Owner(), state));
        }

        [Test]
        public void InitializationGuardsDuplicateKeyUpdateAndModuleShutdown()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            var state = new State { Init = _ => {
                Assert.That(module.HasFsm<Owner>("a"), Is.False);
                Assert.Throws<InvalidOperationException>(() => module.CreateFsm("a", new Owner(), new State()));
                Assert.Throws<InvalidOperationException>(() => ((IUpdateModule)module).Update(1, 1));
                Assert.Throws<InvalidOperationException>(() => ((Module)module).Shutdown());
            }};
            module.CreateFsm("a", new Owner(), state);
            Assert.That(module.Count, Is.EqualTo(1));
        }

        [Test]
        public void UpdateMayDestroySelfAndAnotherFsmAndCreateForNextFrame()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            var next = new State();
            var skipped = new State();
            var first = new State { Tick = fsm => {
                module.DestroyFsm(fsm);
                module.DestroyFsm<Owner>("b");
                module.CreateFsm("c", new Owner(), next).Start<State>();
            }};
            module.CreateFsm("a", new Owner(), first).Start<State>();
            module.CreateFsm("b", new Owner(), skipped).Start<State>();
            ModuleSystem.Update(1, 1);
            Assert.That(first.Destroys, Is.EqualTo(1));
            Assert.That(skipped.Updates, Is.Zero);
            Assert.That(skipped.Destroys, Is.EqualTo(1));
            Assert.That(next.Updates, Is.Zero);
            Assert.That(module.Count, Is.EqualTo(1));
            ModuleSystem.Update(1, 1);
            Assert.That(next.Updates, Is.EqualTo(1));
        }

        [Test]
        public void DirectShutdownAndFailedUpdateAreRemovedFromRegistry()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            var direct = module.CreateFsm(new Owner(), new State());
            direct.Shutdown();
            Assert.That(module.Count, Is.Zero);
            Assert.That(module.GetFsm<Owner>(), Is.Null);
            var state = new State { Tick = _ => throw new InvalidOperationException("update") };
            var failing = module.CreateFsm(new Owner(), state);
            failing.Start<State>();
            Assert.Throws<InvalidOperationException>(() => ModuleSystem.Update(1, 1));
            Assert.That(failing.IsDestroyed, Is.True);
            Assert.That(module.GetAllFsms(), Is.Empty);
            module.CreateFsm(new Owner(), new State());
            Assert.DoesNotThrow(() => ModuleSystem.Update(1, 1));
        }

        [Test]
        public void IndividualShutdownErrorStillRemovesDestroyedFsm()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            var fsm = module.CreateFsm(new Owner(), new State { Destroy = _ => throw new Exception("destroy") });
            Assert.Throws<AggregateException>(() => module.DestroyFsm(fsm));
            Assert.That(fsm.IsDestroyed, Is.True);
            Assert.That(module.Count, Is.Zero);
            Assert.DoesNotThrow(() => module.CreateFsm(new Owner(), new State()));
        }

        [Test]
        public void GlobalShutdownContinuesAfterErrorsAndBlocksRecreation()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            var first = new State { Destroy = _ => {
                Assert.That(module.Count, Is.Zero);
                ((Module)module).Shutdown();
                Assert.Throws<ObjectDisposedException>(() => module.CreateFsm("new", new Owner(), new State()));
                throw new Exception("destroy");
            }};
            var second = new State();
            module.CreateFsm("a", new Owner(), first).Start<State>();
            var b = module.CreateFsm("b", new Owner(), second);
            b.Start<State>();
            Assert.Throws<AggregateException>(() => ModuleSystem.Shutdown());
            Assert.That(second.Destroys, Is.EqualTo(1));
            Assert.That(b.IsDestroyed, Is.True);
            Assert.That(module.Count, Is.Zero);
            Assert.That(ModuleSystem.Count, Is.Zero);
        }

        [Test]
        public void GlobalShutdownDuringTickStopsRemainingFsmUpdates()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            var first = new State { Tick = _ => ModuleSystem.Shutdown() };
            var second = new State();
            module.CreateFsm("a", new Owner(), first).Start<State>();
            module.CreateFsm("b", new Owner(), second).Start<State>();
            Assert.DoesNotThrow(() => ModuleSystem.Update(1, 1));
            Assert.That(second.Updates, Is.Zero);
            Assert.That(second.Destroys, Is.EqualTo(1));
            Assert.That(ModuleSystem.Count, Is.Zero);
        }

        [Test]
        public void InvalidInputsDoNotLeaveReservationsOrEntries()
        {
            var module = ModuleSystem.GetModule<IFsmModule>();
            Assert.Throws<ArgumentNullException>(() => module.GetFsm(null));
            Assert.Throws<ArgumentNullException>(() => module.HasFsm(null));
            Assert.Throws<ArgumentNullException>(() => module.DestroyFsm((Type)null));
            Assert.Throws<ArgumentNullException>(() => module.DestroyFsm((FsmBase)null));
            Assert.Throws<ArgumentNullException>(() => module.DestroyFsm<Owner>((IFsm<Owner>)null));
            Assert.Throws<ArgumentNullException>(() => module.GetAllFsms(null));
            Assert.Throws<ArgumentNullException>(() => module.CreateFsm<Owner>(null, new State()));
            Assert.Throws<ArgumentNullException>(() => module.CreateFsm(new Owner(), (List<FsmState<Owner>>)null));
            Assert.Throws<ArgumentException>(() => module.CreateFsm(new Owner(), new FsmState<Owner>[0]));
            Assert.That(module.Count, Is.Zero);
            Assert.DoesNotThrow(() => module.CreateFsm(new Owner(), new State()));
        }
    }
}
