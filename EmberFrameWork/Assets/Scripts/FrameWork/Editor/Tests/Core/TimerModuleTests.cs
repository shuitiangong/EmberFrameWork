using System;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public class TimerModuleTests
    {
        private ITimerModule _timer;

        [SetUp] public void SetUp()
        {
            GameFrameworkLog.SetLogHelper(null);
            GameModule.Shutdown();
            ModuleSystem.Shutdown();
            _timer = GameModule.Timer;
        }

        [TearDown] public void TearDown()
        {
            GameModule.Shutdown();
            ModuleSystem.Shutdown();
            GameFrameworkLog.SetLogHelper(new DefaultLogHelper());
        }

        [Test] public void OneShotForwardsOriginalArgumentArrayAndRunsOnce()
        {
            int count = 0;
            object[] args = { "payload", 42 };
            object[] received = null;
            int id = _timer.AddTimer(values => { count++; received = values; }, 1f, args: args);
            ModuleSystem.Update(0.5f, 0.5f);
            Assert.AreEqual(0, count);
            Assert.AreEqual(0.5f, _timer.GetLeftTime(id));
            ModuleSystem.Update(0.5f, 0.5f);
            Assert.AreEqual(1, count);
            Assert.AreSame(args, received);
            Assert.IsFalse(_timer.IsRunning(id));
            ModuleSystem.Update(10f, 10f);
            Assert.AreEqual(1, count);
        }

        [Test] public void ScaledAndUnscaledTimersUseSeparateDeltaValues()
        {
            int scaled = 0, unscaled = 0;
            _timer.AddTimer(_ => scaled++, 1f);
            _timer.AddTimer(_ => unscaled++, 1f, isUnscaled: true);
            ModuleSystem.Update(0f, 1f);
            Assert.AreEqual(0, scaled);
            Assert.AreEqual(1, unscaled);
            ModuleSystem.Update(1f, 0f);
            Assert.AreEqual(1, scaled);
            Assert.AreEqual(1, unscaled);
        }

        [Test] public void LoopTimerCatchesUpUsingOriginalBadFrameBehavior()
        {
            int count = 0;
            int id = _timer.AddTimer(_ => count++, 0.5f, isLoop: true);
            ModuleSystem.Update(1.6f, 1.6f);
            Assert.AreEqual(3, count);
            Assert.That(_timer.GetLeftTime(id), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.IsTrue(_timer.IsRunning(id));
        }

        [Test] public void StopResumeAndRestartPreserveOriginalSemantics()
        {
            int count = 0;
            int id = _timer.AddTimer(_ => count++, 2f);
            ModuleSystem.Update(0.5f, 0.5f);
            _timer.Stop(id);
            ModuleSystem.Update(5f, 5f);
            Assert.AreEqual(1.5f, _timer.GetLeftTime(id));
            Assert.IsFalse(_timer.IsRunning(id));
            _timer.Resume(id);
            ModuleSystem.Update(0.5f, 0.5f);
            Assert.AreEqual(1f, _timer.GetLeftTime(id));
            _timer.Stop(id);
            _timer.Restart(id);
            Assert.IsTrue(_timer.IsRunning(id));
            Assert.AreEqual(2f, _timer.GetLeftTime(id));
            Assert.AreEqual(0, count);
        }

        [Test] public void ResetCanChangeCallbackAndTimeDomainWithoutReplacingArgs()
        {
            int oldCalls = 0, newCalls = 0;
            object[] args = { "kept" };
            int id = _timer.AddTimer(_ => oldCalls++, 5f, args: args);
            _timer.ResetTimer(id, values => { Assert.AreSame(args, values); newCalls++; }, 1f, isUnscaled: true);
            ModuleSystem.Update(10f, 0.5f);
            Assert.AreEqual(0, newCalls);
            ModuleSystem.Update(0f, 0.5f);
            Assert.AreEqual(0, oldCalls);
            Assert.AreEqual(1, newCalls);
        }

        [Test] public void ResetWithoutCallbackDoesNotResumeStoppedTimer()
        {
            int count = 0;
            int id = _timer.AddTimer(_ => count++, 5f);
            _timer.Stop(id);
            _timer.ResetTimer(id, 1f, false, false);
            ModuleSystem.Update(2f, 2f);
            Assert.IsFalse(_timer.IsRunning(id));
            Assert.AreEqual(1f, _timer.GetLeftTime(id));
            _timer.Resume(id);
            ModuleSystem.Update(1f, 1f);
            Assert.AreEqual(1, count);
        }

        [Test] public void RemoveIsDeferredUntilUpdateAndPreventsCallback()
        {
            int count = 0;
            int id = _timer.AddTimer(_ => count++, 1f);
            _timer.RemoveTimer(id);
            Assert.IsTrue(_timer.IsRunning(id));
            ModuleSystem.Update(2f, 2f);
            Assert.IsFalse(_timer.IsRunning(id));
            Assert.AreEqual(0, count);
        }

        [Test] public void RemoveAllClearsBothTimeDomains()
        {
            int count = 0;
            _timer.AddTimer(_ => count++, 1f);
            _timer.AddTimer(_ => count++, 1f, isUnscaled: true);
            _timer.RemoveAllTimer();
            ModuleSystem.Update(2f, 2f);
            Assert.AreEqual(0, count);
        }

        [Test] public void GameModuleCachesAndExplicitlyClearsItsEntry()
        {
            Assert.AreSame(_timer, GameModule.Timer);
            Assert.AreSame(_timer, ModuleSystem.GetModule<ITimerModule>());
            // 仅清空入口不销毁模块。
            GameModule.Shutdown();
            Assert.AreSame(_timer, GameModule.Timer);
            ModuleSystem.Shutdown();
            // 与原版一致：模块关闭不会自动清空 GameModule 的缓存。
            Assert.AreSame(_timer, GameModule.Timer);
            GameModule.Shutdown();
            Assert.AreNotSame(_timer, GameModule.Timer);
        }
    }
}
