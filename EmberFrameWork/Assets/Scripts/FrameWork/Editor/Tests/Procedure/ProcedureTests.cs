using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace EmberFrameWork.Tests
{
    public class ProcedureTests
    {
        public class First : ProcedureBase
        {
            public Action<IFsm<IProcedureModule>> InitAction;
            public Action<IFsm<IProcedureModule>> EnterAction;
            public Action<IFsm<IProcedureModule>> UpdateAction;
            public Action<IFsm<IProcedureModule>> DestroyAction;
            public int Enters;
            public int Destroys;
            public bool ShutdownLeave;
            protected override void OnInit(IFsm<IProcedureModule> fsm) => InitAction?.Invoke(fsm);
            protected override void OnEnter(IFsm<IProcedureModule> fsm) { Enters++; EnterAction?.Invoke(fsm); }
            protected override void OnUpdate(IFsm<IProcedureModule> fsm, float elapsed, float real) => UpdateAction?.Invoke(fsm);
            protected override void OnLeave(IFsm<IProcedureModule> fsm, bool shutdown) => ShutdownLeave = shutdown;
            protected override void OnDestroy(IFsm<IProcedureModule> fsm) { Destroys++; DestroyAction?.Invoke(fsm); }
            public void Next(IFsm<IProcedureModule> fsm) => ChangeState<Second>(fsm);
        }
        public class Second : ProcedureBase { }
        public abstract class AbstractProcedure : ProcedureBase { }
        public class GenericProcedure<T> : ProcedureBase { }
        public class NoDefaultConstructor : ProcedureBase { public NoDefaultConstructor(int value) { } }
        public class ThrowsConstructor : ProcedureBase
        {
            public ThrowsConstructor() { throw new InvalidOperationException("constructor failed"); }
        }

        private IProcedureModule _module;
        private IFsmModule _fsms;
        private readonly List<ProcedureSetting> _settings = new List<ProcedureSetting>();

        [SetUp]
        public void SetUp()
        {
            ModuleSystem.Shutdown();
            _module = ModuleSystem.GetModule<IProcedureModule>();
            _fsms = ModuleSystem.GetModule<IFsmModule>();
        }

        [TearDown]
        public void TearDown()
        {
            ModuleSystem.Shutdown();
            foreach (var setting in _settings) UnityEngine.Object.DestroyImmediate(setting);
            _settings.Clear();
        }

        private ProcedureSetting NewSetting()
        {
            var setting = ScriptableObject.CreateInstance<ProcedureSetting>();
            _settings.Add(setting);
            return setting;
        }

        [Test]
        public void ModuleInitializesStartsTransitionsAndUsesFsmUpdate()
        {
            var first = new First();
            var second = new Second();
            _module.Initialize(_fsms, first, second);
            Assert.That(_module.IsInitialized, Is.True);
            Assert.That(_module.CurrentProcedure, Is.Null);
            Assert.That(_module.HasProcedure<First>(), Is.True);
            Assert.That(_module.HasProcedure(typeof(Second)), Is.True);
            Assert.That(_module.GetProcedure<First>(), Is.SameAs(first));
            Assert.That(_module.GetProcedure(typeof(Second)), Is.SameAs(second));
            Assert.That(_fsms.GetFsm<IProcedureModule>().Owner, Is.SameAs(_module));
            _module.StartProcedure<First>();
            ModuleSystem.Update(0.25f, 0.5f);
            Assert.That(_module.CurrentProcedureTime, Is.EqualTo(0.25f));
            first.UpdateAction = first.Next;
            ModuleSystem.Update(0.25f, 0.5f);
            Assert.That(_module.CurrentProcedure, Is.SameAs(second));
            Assert.That(_module.CurrentProcedureTime, Is.Zero);
            Assert.That(first.ShutdownLeave, Is.False);
        }

        [Test]
        public void GuardedOperationsRejectMissingInitializationAndWrongTypes()
        {
            Assert.Throws<InvalidOperationException>(() => _module.StartProcedure<First>());
            Assert.Throws<InvalidOperationException>(() => _module.GetProcedure<First>());
            Assert.Throws<InvalidOperationException>(() => _module.RestartProcedure(new First()));
            Assert.Throws<ArgumentNullException>(() => _module.Initialize(null, new First()));
            Assert.Throws<ArgumentException>(() => _module.Initialize(_fsms, new ProcedureBase[0]));
            Assert.Throws<ArgumentException>(() => _module.Initialize(_fsms, new First(), new First()));
            Assert.That(_fsms.Count, Is.Zero);
            _module.Initialize(_fsms, new First());
            Assert.Throws<InvalidOperationException>(() => _module.Initialize(_fsms, new Second()));
            Assert.Throws<ArgumentNullException>(() => _module.StartProcedure(null));
            Assert.Throws<ArgumentException>(() => _module.GetProcedure(typeof(string)));
            Assert.Throws<InvalidOperationException>(() => _module.StartProcedure<Second>());
            _module.StartProcedure(typeof(First));
            Assert.Throws<InvalidOperationException>(() => _module.StartProcedure<First>());
        }

        [Test]
        public void RestartValidatesBeforeDestroyAndStartsFirstNewProcedure()
        {
            var first = new First();
            _module.Initialize(_fsms, first, new Second());
            _module.StartProcedure<First>();
            Assert.Throws<ArgumentException>(() => _module.RestartProcedure(new First(), new First()));
            Assert.That(_module.CurrentProcedure, Is.SameAs(first));
            Assert.That(first.Destroys, Is.Zero);
            var second = new Second();
            Assert.That(_module.RestartProcedure(second, new First()), Is.True);
            Assert.That(first.Destroys, Is.EqualTo(1));
            Assert.That(first.ShutdownLeave, Is.True);
            Assert.That(_module.CurrentProcedure, Is.SameAs(second));
            Assert.That(_fsms.Count, Is.EqualTo(1));
        }

        [Test]
        public void RestartMayReuseExistingInstancesAfterTheyAreDestroyed()
        {
            var first = new First();
            _module.Initialize(_fsms, first);
            _module.StartProcedure<First>();
            Assert.That(_module.RestartProcedure(first), Is.True);
            Assert.That(first.Enters, Is.EqualTo(2));
            Assert.That(first.Destroys, Is.EqualTo(1));
        }

        [Test]
        public void ProcedureModuleClosesBeforeFsmModuleAndReleasesReferences()
        {
            var first = new First { DestroyAction = _ => Assert.That(_fsms.HasFsm<OtherOwner>("other"), Is.True) };
            var other = _fsms.CreateFsm("other", new OtherOwner(), new OtherState());
            _module.Initialize(_fsms, first);
            _module.StartProcedure<First>();
            Assert.That(((Module)_module).Priority, Is.LessThan(((Module)_fsms).Priority));
            ModuleSystem.Shutdown();
            Assert.That(first.Destroys, Is.EqualTo(1));
            Assert.That(other.IsDestroyed, Is.True);
            Assert.That(_module.IsInitialized, Is.False);
            Assert.Throws<ObjectDisposedException>(() => _module.Initialize(_fsms, new First()));
        }
        private sealed class OtherOwner { }
        private sealed class OtherState : FsmState<OtherOwner> { }

        [Test]
        public void InitFailureDoesNotPublishFsmAndAllowsRetry()
        {
            var first = new First { InitAction = _ => throw new InvalidOperationException("init") };
            Assert.Throws<InvalidOperationException>(() => _module.Initialize(_fsms, first));
            Assert.That(_module.IsInitialized, Is.False);
            Assert.That(_fsms.Count, Is.Zero);
            Assert.That(first.Destroys, Is.EqualTo(1));
            first.InitAction = null;
            _module.Initialize(_fsms, first);
            _module.StartProcedure<First>();
        }

        [Test]
        public void EnterFailureCleansFsmAndAllowsExplicitReinitialization()
        {
            var first = new First { EnterAction = _ => throw new InvalidOperationException("enter") };
            _module.Initialize(_fsms, first);
            Assert.Throws<InvalidOperationException>(() => _module.StartProcedure<First>());
            Assert.That(_module.IsInitialized, Is.False);
            Assert.That(_fsms.Count, Is.Zero);
            _module.Initialize(_fsms, new Second());
            _module.StartProcedure<Second>();
        }

        [Test]
        public void ShutdownErrorStillCleansRemainingModules()
        {
            var first = new First { DestroyAction = _ => throw new InvalidOperationException("destroy") };
            _module.Initialize(_fsms, first);
            _module.StartProcedure<First>();
            Assert.Throws<AggregateException>(() => ModuleSystem.Shutdown());
            Assert.That(_module.IsInitialized, Is.False);
            Assert.That(_fsms.Count, Is.Zero);
            Assert.That(ModuleSystem.Count, Is.Zero);
        }

        [Test]
        public void SettingSelectsEntranceIndependentlyOfListOrderAndCanStartNewSession()
        {
            var setting = NewSetting();
            setting.Configure(new[] { typeof(First).FullName, typeof(Second).AssemblyQualifiedName }, typeof(Second).FullName);
            Assert.That(setting.TryValidate(out _), Is.True);
            setting.StartProcedure();
            Assert.That(setting.CurrentProcedure, Is.TypeOf<Second>());
            ModuleSystem.Update(0.5f, 0.75f);
            Assert.That(setting.CurrentProcedureTime, Is.EqualTo(0.5f));
            Assert.Throws<InvalidOperationException>(() => setting.StartProcedure());
            ModuleSystem.Shutdown();
            Assert.That(setting.CurrentProcedure, Is.Null);
            Assert.That(setting.CurrentProcedureTime, Is.Zero);
            setting.StartProcedure();
            Assert.That(setting.CurrentProcedure, Is.TypeOf<Second>());
        }

        [Test]
        public void SettingValidationRejectsInvalidTypesDuplicatesAndMissingEntranceWithoutMutatingConfiguration()
        {
            var setting = NewSetting();
            Assert.That(setting.TryValidate(out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            setting.Configure(new[] { typeof(First).AssemblyQualifiedName }, typeof(First).AssemblyQualifiedName);
            foreach (var type in new[] { typeof(string), typeof(AbstractProcedure), typeof(GenericProcedure<>), typeof(NoDefaultConstructor) })
                Assert.Throws<InvalidOperationException>(() => setting.Configure(new[] { type.AssemblyQualifiedName }, type.AssemblyQualifiedName));
            Assert.Throws<InvalidOperationException>(() => setting.Configure(new[] { "Missing.Procedure" }, "Missing.Procedure"));
            Assert.Throws<InvalidOperationException>(() => setting.Configure(new[] { typeof(First).FullName, typeof(First).AssemblyQualifiedName }, typeof(First).FullName));
            Assert.Throws<InvalidOperationException>(() => setting.Configure(new[] { typeof(First).FullName }, typeof(Second).FullName));
            setting.StartProcedure();
            Assert.That(setting.CurrentProcedure, Is.TypeOf<First>());
        }

        [Test]
        public void SettingConstructorFailureCreatesNoFsm()
        {
            var setting = NewSetting();
            setting.Configure(new[] { typeof(ThrowsConstructor).AssemblyQualifiedName }, typeof(ThrowsConstructor).AssemblyQualifiedName);
            Assert.Throws<System.Reflection.TargetInvocationException>(() => setting.StartProcedure());
            Assert.That(_fsms.Count, Is.Zero);
            Assert.That(_module.IsInitialized, Is.False);
        }

        [Test]
        public void SettingSerializationRoundTripPreservesTypesAndEntrance()
        {
            var original = NewSetting();
            original.Configure(new[] { typeof(First).AssemblyQualifiedName, typeof(Second).AssemblyQualifiedName }, typeof(Second).AssemblyQualifiedName);
            var restored = NewSetting();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), restored);
            restored.StartProcedure();
            Assert.That(restored.CurrentProcedure, Is.TypeOf<Second>());
        }

        [Test]
        public void NestedRestartDuringRestartCleanupIsRejected()
        {
            var first = new First { DestroyAction = _ => {
                Assert.Throws<InvalidOperationException>(() => _module.RestartProcedure(new First()));
            }};
            _module.Initialize(_fsms, first);
            _module.StartProcedure<First>();
            Assert.That(_module.RestartProcedure(new Second()), Is.True);
            Assert.That(_module.CurrentProcedure, Is.TypeOf<Second>());
        }
    }
}
