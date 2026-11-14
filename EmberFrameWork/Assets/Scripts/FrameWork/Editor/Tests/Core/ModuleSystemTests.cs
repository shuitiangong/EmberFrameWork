using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public interface IConventionModule { }
    public sealed class ConventionModule : Module, IConventionModule
    {
        public int Initializations;
        public int Shutdowns;
        public override void OnInit() => Initializations++;
        public override void Shutdown() => Shutdowns++;
    }

    public interface ICycleModule { }
    public sealed class CycleModule : Module, ICycleModule
    {
        public override void OnInit() => ModuleSystem.GetModule<ICycleModule>();
        public override void Shutdown() { }
    }

    public class ModuleSystemTests
    {
        private interface IFirst { }
        private interface ISecond { }
        private interface IThird { }
        private interface IAlias { }
        private interface IMissing { }

        private abstract class Probe : Module, IUpdateModule
        {
            public int Order;
            public List<string> Events;
            public Action InitAction;
            public Action UpdateAction;
            public Action ShutdownAction;
            public int Initializations;
            public int Updates;
            public int Shutdowns;
            public float Elapsed;
            public float RealElapsed;
            public override int Priority => Order;
            public override void OnInit() { Initializations++; InitAction?.Invoke(); }
            public void Update(float elapsed, float real)
            {
                Updates++;
                Elapsed = elapsed;
                RealElapsed = real;
                Events?.Add(GetType().Name + ":update");
                UpdateAction?.Invoke();
            }
            public override void Shutdown()
            {
                Shutdowns++;
                Events?.Add(GetType().Name + ":shutdown");
                ShutdownAction?.Invoke();
            }
        }
        private sealed class First : Probe, IFirst, IAlias { }
        private sealed class Second : Probe, ISecond { }
        private sealed class Third : Probe, IThird { }

        [SetUp]
        public void SetUp() => ModuleSystem.Shutdown();
        [TearDown]
        public void TearDown() => ModuleSystem.Shutdown();

        [Test]
        public void ConventionLookupUsesInterfaceAssemblyAndInitializesOnce()
        {
            var first = (ConventionModule)ModuleSystem.GetModule<IConventionModule>();
            Assert.That(ModuleSystem.GetModule<IConventionModule>(), Is.SameAs(first));
            Assert.That(first.Initializations, Is.EqualTo(1));
            Assert.That(ModuleSystem.Count, Is.EqualTo(1));
            ModuleSystem.Shutdown();
            Assert.That(first.Shutdowns, Is.EqualTo(1));
            Assert.That(ModuleSystem.GetModule<IConventionModule>(), Is.Not.SameAs(first));
        }

        [Test]
        public void ExplicitRegistrationCachesInterfacesWithoutDuplicateLifecycleCalls()
        {
            var module = new First();
            ModuleSystem.RegisterModule<IFirst>(module);
            ModuleSystem.RegisterModule<IFirst>(module);
            ModuleSystem.RegisterModule<IAlias>(module);
            Assert.That(ModuleSystem.GetModule<IFirst>(), Is.SameAs(module));
            Assert.That(ModuleSystem.GetModule<IAlias>(), Is.SameAs(module));
            Assert.That(ModuleSystem.Count, Is.EqualTo(1));
            ModuleSystem.Update(0.25f, 0.5f);
            Assert.That(module.Initializations, Is.EqualTo(1));
            Assert.That(module.Updates, Is.EqualTo(1));
            Assert.That(module.Elapsed, Is.EqualTo(0.25f));
            Assert.That(module.RealElapsed, Is.EqualTo(0.5f));
            ModuleSystem.Shutdown();
            ModuleSystem.Shutdown();
            Assert.That(module.Shutdowns, Is.EqualTo(1));
        }

        [Test]
        public void PriorityOrdersUpdatesAndReversesShutdownWithStableTies()
        {
            var events = new List<string>();
            ModuleSystem.RegisterModule<IFirst>(new First { Order = 0, Events = events });
            ModuleSystem.RegisterModule<ISecond>(new Second { Order = 10, Events = events });
            ModuleSystem.RegisterModule<IThird>(new Third { Order = 10, Events = events });
            ModuleSystem.Update(1, 1);
            ModuleSystem.Shutdown();
            Assert.That(events, Is.EqualTo(new[] { "Second:update", "Third:update", "First:update",
                "First:shutdown", "Third:shutdown", "Second:shutdown" }));
        }

        [Test]
        public void InvalidAndConflictingRegistrationsDoNotInitializeNewInstances()
        {
            Assert.Throws<ArgumentException>(() => ModuleSystem.GetModule<First>());
            Assert.Throws<ArgumentException>(() => ModuleSystem.RegisterModule<IFirst>(new Second()));
            Assert.Throws<ArgumentNullException>(() => ModuleSystem.RegisterModule<IFirst>(null));
            Assert.Throws<InvalidOperationException>(() => ModuleSystem.GetModule<IMissing>());
            var first = new First();
            var duplicate = new First();
            ModuleSystem.RegisterModule<IFirst>(first);
            Assert.Throws<InvalidOperationException>(() => ModuleSystem.RegisterModule<IFirst>(duplicate));
            Assert.Throws<InvalidOperationException>(() => ModuleSystem.RegisterModule<IAlias>(duplicate));
            Assert.That(duplicate.Initializations, Is.Zero);
            Assert.That(ModuleSystem.Count, Is.EqualTo(1));
        }

        [Test]
        public void InitializationCanResolveAnotherModule()
        {
            IConventionModule dependency = null;
            var first = new First { InitAction = () => dependency = ModuleSystem.GetModule<IConventionModule>() };
            ModuleSystem.RegisterModule<IFirst>(first);
            Assert.That(dependency, Is.Not.Null);
            Assert.That(ModuleSystem.Count, Is.EqualTo(2));
        }

        [Test]
        public void FailedInitializationCleansInstanceAndAllowsRetry()
        {
            var failed = new First { InitAction = () => throw new InvalidOperationException("init failed") };
            Assert.Throws<InvalidOperationException>(() => ModuleSystem.RegisterModule<IFirst>(failed));
            Assert.That(failed.Shutdowns, Is.EqualTo(1));
            Assert.That(ModuleSystem.Count, Is.Zero);
            ModuleSystem.RegisterModule<IFirst>(new First());
        }

        [Test]
        public void FailedInitializationAndCleanupPreserveBothErrors()
        {
            var failed = new First {
                InitAction = () => throw new InvalidOperationException("init"),
                ShutdownAction = () => throw new InvalidOperationException("cleanup")
            };
            var error = Assert.Throws<AggregateException>(() => ModuleSystem.RegisterModule<IFirst>(failed));
            Assert.That(error.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(ModuleSystem.Count, Is.Zero);
            ModuleSystem.RegisterModule<IFirst>(new First());
        }

        [Test]
        public void CircularInitializationIsRejectedWithoutPublishingModule()
        {
            Assert.Throws<InvalidOperationException>(() => ModuleSystem.GetModule<ICycleModule>());
            Assert.That(ModuleSystem.Count, Is.Zero);
            Assert.DoesNotThrow(() => ModuleSystem.GetModule<IConventionModule>());
        }

        [Test]
        public void ModulesCreatedDuringUpdateStartUpdatingNextTick()
        {
            var second = new Second { Order = 100 };
            var first = new First { UpdateAction = () => ModuleSystem.RegisterModule<ISecond>(second) };
            ModuleSystem.RegisterModule<IFirst>(first);
            ModuleSystem.Update(1, 1);
            Assert.That(first.Updates, Is.EqualTo(1));
            Assert.That(second.Initializations, Is.EqualTo(1));
            Assert.That(second.Updates, Is.Zero);
            ModuleSystem.Update(1, 1);
            Assert.That(first.Updates, Is.EqualTo(2));
            Assert.That(second.Updates, Is.EqualTo(1));
        }

        [Test]
        public void ShutdownDuringUpdateStopsOldSnapshotAndAllowsNextSession()
        {
            var third = new Third();
            var first = new First { Order = 10, UpdateAction = () => {
                ModuleSystem.Shutdown();
                ModuleSystem.RegisterModule<IThird>(third);
            }};
            var second = new Second();
            ModuleSystem.RegisterModule<IFirst>(first);
            ModuleSystem.RegisterModule<ISecond>(second);
            ModuleSystem.Update(1, 1);
            Assert.That(second.Updates, Is.Zero);
            Assert.That(first.Shutdowns, Is.EqualTo(1));
            Assert.That(second.Shutdowns, Is.EqualTo(1));
            Assert.That(third.Updates, Is.Zero);
            ModuleSystem.Update(1, 1);
            Assert.That(third.Updates, Is.EqualTo(1));
        }

        [Test]
        public void ShutdownErrorsDoNotPreventCleanupOrReinitialization()
        {
            var first = new First { ShutdownAction = () => throw new Exception("shutdown") };
            var second = new Second { ShutdownAction = ModuleSystem.Shutdown };
            ModuleSystem.RegisterModule<IFirst>(first);
            ModuleSystem.RegisterModule<ISecond>(second);
            Assert.Throws<AggregateException>(() => ModuleSystem.Shutdown());
            Assert.That(first.Shutdowns, Is.EqualTo(1));
            Assert.That(second.Shutdowns, Is.EqualTo(1));
            Assert.That(ModuleSystem.Count, Is.Zero);
            Assert.DoesNotThrow(() => ModuleSystem.GetModule<IConventionModule>());
        }

        [Test]
        public void ShutdownCanResolveExistingDependenciesButCannotCreateModules()
        {
            var first = new First { Order = 10 };
            ModuleSystem.RegisterModule<IFirst>(first);
            ModuleSystem.RegisterModule<ISecond>(new Second { ShutdownAction = () => {
                Assert.That(ModuleSystem.GetModule<IFirst>(), Is.SameAs(first));
                Assert.That(first.Shutdowns, Is.Zero);
                Assert.Throws<InvalidOperationException>(() => ModuleSystem.GetModule<IConventionModule>());
                Assert.Throws<InvalidOperationException>(() => ModuleSystem.RegisterModule<IThird>(new Third()));
            }});
            ModuleSystem.Shutdown();
        }

        [Test]
        public void RecursiveUpdateFailsAndReleasesUpdateGuard()
        {
            var first = new First { UpdateAction = () => ModuleSystem.Update(1, 1) };
            ModuleSystem.RegisterModule<IFirst>(first);
            Assert.Throws<InvalidOperationException>(() => ModuleSystem.Update(1, 1));
            first.UpdateAction = null;
            Assert.DoesNotThrow(() => ModuleSystem.Update(1, 1));
            Assert.That(first.Updates, Is.EqualTo(2));
        }

        [Test]
        public void InitializationRejectsGlobalUpdateAndShutdown()
        {
            ModuleSystem.RegisterModule<IFirst>(new First { InitAction = () => {
                Assert.Throws<InvalidOperationException>(() => ModuleSystem.Update(1, 1));
                Assert.Throws<InvalidOperationException>(() => ModuleSystem.Shutdown());
            }});
            Assert.That(ModuleSystem.Count, Is.EqualTo(1));
        }
    }
}
