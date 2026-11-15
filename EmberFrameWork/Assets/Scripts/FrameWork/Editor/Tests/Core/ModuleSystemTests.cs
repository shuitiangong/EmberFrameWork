using System;
using System.Collections.Generic;
using NUnit.Framework;
namespace EmberFrameWork.Tests
{
    public class ModuleSystemTests
    {
        private interface IFirst { }
        private interface ISecond { }
        private class Probe : Module, IUpdateModule, IFirst, ISecond
        {
            public int Order, InitCount, TickCount, StopCount;
            public bool FailInit;
            public string Name;
            public List<string> Events;
            public override int Priority => Order;
            public override void OnInit() { InitCount++; if (FailInit) throw new InvalidOperationException(); }
            public void Update(float elapsed, float real) { TickCount++; Events?.Add("tick:" + Name); }
            public override void Shutdown() { StopCount++; Events?.Add("stop:" + Name); }
        }
        [SetUp] public void SetUp() { ModuleSystem.Shutdown(); GameFrameworkLog.SetLogHelper(null); }
        [TearDown] public void TearDown() { ModuleSystem.Shutdown(); GameFrameworkLog.SetLogHelper(new DefaultLogHelper()); }
        [Test] public void ConventionResolutionReusesBuiltinModule()
        {
            Assert.AreSame(ModuleSystem.GetModule<IFsmModule>(), ModuleSystem.GetModule<IFsmModule>());
            Assert.Throws<GameFrameworkException>(() => ModuleSystem.GetModule<Probe>());
        }
        [Test] public void UpdatesHighPriorityFirstAndShutsDownInReverseOrder()
        {
            var events = new List<string>();
            var low = new Probe { Order = 1, Name = "low", Events = events };
            var high = new Probe { Order = 5, Name = "high", Events = events };
            ModuleSystem.RegisterModule<IFirst>(low); ModuleSystem.RegisterModule<ISecond>(high);
            ModuleSystem.Update(1, 1); ModuleSystem.Shutdown();
            CollectionAssert.AreEqual(new[] { "tick:high", "tick:low", "stop:low", "stop:high" }, events);
        }
        [Test] public void ConventionResolutionFindsModuleInInterfaceAssembly()
        {
            Assert.AreNotEqual(typeof(ModuleSystem).Assembly, typeof(IAssemblyResolutionProbe).Assembly);
            var module = (AssemblyResolutionProbe)ModuleSystem.GetModule<IAssemblyResolutionProbe>();
            Assert.AreSame(module, ModuleSystem.GetModule<IAssemblyResolutionProbe>());
            Assert.AreEqual(1, module.InitCount);
            ModuleSystem.Update(0, 0);
            Assert.AreEqual(1, module.TickCount);
            ModuleSystem.Shutdown();
            Assert.AreEqual(1, module.StopCount);
            Assert.AreNotSame(module, ModuleSystem.GetModule<IAssemblyResolutionProbe>());
        }
        [Test] public void RegisterPublishesInterfaceAndInitializesImmediately()
        {
            var probe = new Probe();
            Assert.AreSame(probe, ModuleSystem.RegisterModule<IFirst>(probe));
            Assert.AreSame(probe, ModuleSystem.GetModule<IFirst>());
            Assert.AreEqual(1, probe.InitCount);
        }
        [Test] public void RepeatedRegistrationPreservesOriginalDuplicateScheduling()
        {
            var probe = new Probe();
            ModuleSystem.RegisterModule<IFirst>(probe); ModuleSystem.RegisterModule<IFirst>(probe);
            ModuleSystem.Update(0, 0);
            Assert.AreEqual(2, probe.InitCount); Assert.AreEqual(2, probe.TickCount);
        }
        [Test] public void InitializationFailureDoesNotRollbackOriginalRegistration()
        {
            var probe = new Probe { FailInit = true };
            Assert.Throws<InvalidOperationException>(() => ModuleSystem.RegisterModule<IFirst>(probe));
            Assert.AreSame(probe, ModuleSystem.GetModule<IFirst>());
        }
        [Test] public void ShutdownClearsMemoryCaches()
        {
            MemoryPool.Add<MemoryPoolTests.Item>(1);
            ModuleSystem.Shutdown();
            Assert.AreEqual(0, MemoryPool.Count);
        }
    }

    // 接口与实现位于 Editor 程序集，验证 Runtime 中的 ModuleSystem 能按 main 约定定位。
    public interface IAssemblyResolutionProbe { }

    public sealed class AssemblyResolutionProbe : Module, IAssemblyResolutionProbe, IUpdateModule
    {
        public int InitCount, TickCount, StopCount;
        public override void OnInit() { InitCount++; }
        public void Update(float elapsed, float real) { TickCount++; }
        public override void Shutdown() { StopCount++; }
    }
}
