using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EmberFrameWork.Tests
{
    public class UpdateDriverTests
    {
        private IUpdateDriver _driver;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ModuleSystem.Shutdown();
            yield return null;
            _driver = ModuleSystem.GetModule<IUpdateDriver>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ModuleSystem.Shutdown();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FrameListenersCanBeRegisteredAndRemoved()
        {
            int update = 0, fixedUpdate = 0, lateUpdate = 0;
            Action onUpdate = () => update++;
            Action onFixedUpdate = () => fixedUpdate++;
            Action onLateUpdate = () => lateUpdate++;
            _driver.AddUpdateListener(onUpdate);
            _driver.AddFixedUpdateListener(onFixedUpdate);
            _driver.AddLateUpdateListener(onLateUpdate);
            // 保留原版 Yield 后才注册的时序。
            float deadline = Time.realtimeSinceStartup + 5f;
            while (update == 0 || fixedUpdate == 0 || lateUpdate == 0)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Frame callbacks did not run.");
                yield return null;
            }
            _driver.RemoveUpdateListener(onUpdate);
            _driver.RemoveFixedUpdateListener(onFixedUpdate);
            _driver.RemoveLateUpdateListener(onLateUpdate);
            int u = update, f = fixedUpdate, l = lateUpdate;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(u, update);
            Assert.AreEqual(f, fixedUpdate);
            Assert.AreEqual(l, lateUpdate);
        }

        [UnityTest]
        public IEnumerator CoroutineCanBeStopped()
        {
            int ticks = 0;
            IEnumerator Routine()
            {
                while (true) { ticks++; yield return null; }
            }
            var coroutine = _driver.StartCoroutine(Routine());
            yield return null;
            yield return null;
            Assert.Greater(ticks, 0);
            _driver.StopCoroutine(coroutine);
            int stoppedAt = ticks;
            yield return null;
            yield return null;
            Assert.AreEqual(stoppedAt, ticks);
        }

        [UnityTest]
        public IEnumerator ShutdownUsesOriginalListenerClearingBehavior()
        {
            Assert.AreSame(_driver, ModuleSystem.GetModule<IUpdateDriver>());
            var entity = GameObject.Find("[UpdateDriver]");
            Assert.IsNotNull(entity);
            int destroyed = 0;
            _driver.AddDestroyListener(() => destroyed++);
            ModuleSystem.Shutdown();
            yield return null;
            Assert.IsTrue(entity == null);
            // 原版 Shutdown 先清空事件，因此不触发已注册的 Destroy 回调。
            Assert.AreEqual(0, destroyed);
        }
    }
}
