using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EmberFrameWork.Tests
{
    public class SingletonTests
    {
        private static readonly List<string> Releases = new List<string>();

        public sealed class Probe : Singleton<Probe>, IUpdate, IFixedUpdate, ILateUpdate
        {
            public int Initializations, Activations, Updates, FixedUpdates, LateUpdates;
            protected override void OnInit() { Initializations++; }
            public override void Active() { Activations++; }
            public void OnUpdate() { Updates++; }
            public void OnFixedUpdate() { FixedUpdates++; }
            public void OnLateUpdate() { LateUpdates++; }
            protected override void OnRelease() { Releases.Add("Probe"); }
        }

        public sealed class OtherProbe : Singleton<OtherProbe>
        {
            protected override void OnRelease() { Releases.Add("OtherProbe"); }
        }

        [UnityTest]
        public IEnumerator InstanceLifecycleAndDriverDispatchMatchOriginal()
        {
            Releases.Clear();
            Assert.IsFalse(Probe.IsValid);
            var first = Probe.Instance;
            try
            {
                Assert.AreSame(first, Probe.Instance);
                Assert.AreEqual(1, first.Initializations);
                first.Active();
                Assert.AreEqual(1, first.Activations);

                float deadline = Time.realtimeSinceStartup + 5f;
                while (first.Updates == 0 || first.FixedUpdates == 0 || first.LateUpdates == 0)
                {
                    Assert.Less(Time.realtimeSinceStartup, deadline, "Singleton callbacks did not reach UpdateDriver.");
                    yield return null;
                }

                first.Release();
                Assert.IsFalse(Probe.IsValid);
                int u = first.Updates, f = first.FixedUpdates, l = first.LateUpdates;
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.AreEqual(u, first.Updates);
                Assert.AreEqual(f, first.FixedUpdates);
                Assert.AreEqual(l, first.LateUpdates);

                var second = Probe.Instance;
                Assert.AreNotSame(first, second);
                Assert.AreEqual(1, second.Initializations);
                OtherProbe.Instance.Active();
                Releases.Clear();
                SingletonSystem.Release();
                CollectionAssert.AreEqual(new[] { "OtherProbe", "Probe" }, Releases);
                Assert.IsFalse(Probe.IsValid);
                Assert.IsFalse(OtherProbe.IsValid);

                int oldUpdates = second.Updates;
                int oldFixedUpdates = second.FixedUpdates;
                int oldLateUpdates = second.LateUpdates;
                var restarted = Probe.Instance;
                deadline = Time.realtimeSinceStartup + 5f;
                while (restarted.Updates == 0 || restarted.FixedUpdates == 0 || restarted.LateUpdates == 0)
                {
                    Assert.Less(Time.realtimeSinceStartup, deadline, "Singleton callbacks did not reconnect after system release.");
                    yield return null;
                }
                Assert.AreEqual(oldUpdates, second.Updates);
                Assert.AreEqual(oldFixedUpdates, second.FixedUpdates);
                Assert.AreEqual(oldLateUpdates, second.LateUpdates);
            }
            finally
            {
                if (OtherProbe.IsValid) OtherProbe.Instance.Release();
                if (Probe.IsValid) Probe.Instance.Release();
            }
            // main 的 Release 解除驱动监听；再次 Retain 时恢复连接。
        }
    }
}
