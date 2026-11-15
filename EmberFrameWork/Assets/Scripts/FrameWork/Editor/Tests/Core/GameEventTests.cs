using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public class GameEventTests
    {
        private sealed class CaptureLog : GameFrameworkLog.ILogHelper
        {
            public int Fatals;
            public void Log(GameFrameworkLogLevel level, object message)
            {
                if (level == GameFrameworkLogLevel.Fatal) Fatals++;
            }
        }
        private CaptureLog _log;

        [SetUp] public void SetUp()
        {
            GameEvent.EventMgr.Init();
            MemoryPool.ClearAll();
            _log = new CaptureLog();
            GameFrameworkLog.SetLogHelper(_log);
        }

        [TearDown] public void TearDown()
        {
            GameEvent.EventMgr.Init();
            MemoryPool.ClearAll();
            GameFrameworkLog.SetLogHelper(new DefaultLogHelper());
        }

        [Test] public void StringAndIntegerIdsAddressSameEvent()
        {
            const string name = "GameEventTests.Shared";
            int id = RuntimeId.ToRuntimeId(name), count = 0;
            Action listener = () => count++;
            Assert.IsTrue(GameEvent.AddEventListener(name, listener));
            GameEvent.Send(id);
            GameEvent.Send(name);
            Assert.AreEqual(2, count);
            GameEvent.RemoveEventListener(id, listener);
            GameEvent.Send(name);
            Assert.AreEqual(2, count);
        }

        [Test] public void TypedEventsForwardOneThroughFiveArguments()
        {
            int a = 0, b = 0, c = 0, d = 0, e = 0;
            GameEvent.AddEventListener<int>(101, x => a = x);
            GameEvent.AddEventListener<int, int>(102, (x,y) => b = x+y);
            GameEvent.AddEventListener<int, int, int>(103, (x,y,z) => c = x+y+z);
            GameEvent.AddEventListener<int, int, int, int>(104, (w,x,y,z) => d = w+x+y+z);
            GameEvent.AddEventListener<int, int, int, int, int>(105, (v,w,x,y,z) => e = v+w+x+y+z);
            GameEvent.Send(101, 1);
            GameEvent.Send(102, 1, 2);
            GameEvent.Send(103, 1, 2, 3);
            GameEvent.Send(104, 1, 2, 3, 4);
            GameEvent.Send(105, 1, 2, 3, 4, 5);
            CollectionAssert.AreEqual(new[] { 1, 3, 6, 10, 15 }, new[] { a,b,c,d,e });
        }

        [Test] public void RemovingOneListenerKeepsOtherListeners()
        {
            var calls = new List<int>();
            Action first = () => calls.Add(1), second = () => calls.Add(2);
            GameEvent.AddEventListener(201, first);
            GameEvent.AddEventListener(201, second);
            GameEvent.RemoveEventListener(201, first);
            GameEvent.Send(201);
            CollectionAssert.AreEqual(new[] { 2 }, calls);
        }

        [Test] public void ListenerChangesDuringDispatchApplyAfterCurrentSend()
        {
            var calls = new List<string>();
            Action first = null;
            Action second = () => calls.Add("second");
            Action added = () => calls.Add("added");
            first = () => {
                calls.Add("first");
                GameEvent.AddEventListener(301, added);
                GameEvent.RemoveEventListener(301, second);
                GameEvent.RemoveEventListener(301, first);
            };
            GameEvent.AddEventListener(301, first);
            GameEvent.AddEventListener(301, second);
            GameEvent.Send(301);
            GameEvent.Send(301);
            CollectionAssert.AreEqual(new[] { "first", "second", "added" }, calls);
        }

        [Test] public void ReturningGameEventMgrUnsubscribesAndAllowsReuse()
        {
            int count = 0;
            Action listener = () => count++;
            var manager = MemoryPool.Acquire<GameEventMgr>();
            manager.AddEvent(401, listener);
            GameEvent.Send(401);
            MemoryPool.Release(manager);
            GameEvent.Send(401);
            Assert.AreEqual(1, count);
            var reused = MemoryPool.Acquire<GameEventMgr>();
            Assert.AreSame(manager, reused);
            reused.AddEvent(401, listener);
            GameEvent.Send(401);
            Assert.AreEqual(2, count);
            MemoryPool.Release(reused);
        }

        [Test] public void DuplicateRegistrationUsesOriginalFatalAndFalseBehavior()
        {
            int count = 0;
            Action listener = () => count++;
            Assert.IsTrue(GameEvent.AddEventListener(501, listener));
            // 使用不抛异常的自定义日志辅助器，验证 Fatal 调用之后原函数的返回值。
            Assert.IsFalse(GameEvent.AddEventListener(501, listener));
            Assert.AreEqual(1, _log.Fatals);
            GameEvent.Send(501);
            Assert.AreEqual(1, count);
        }

        [Test] public void RuntimeIdMappingIsStableAndReversible()
        {
            int first = RuntimeId.ToRuntimeId("GameEventTests.First");
            int second = RuntimeId.ToRuntimeId("GameEventTests.Second");
            Assert.AreEqual(first, RuntimeId.ToRuntimeId("GameEventTests.First"));
            Assert.AreNotEqual(first, second);
            Assert.AreEqual("GameEventTests.First", RuntimeId.ToString(first));
            Assert.AreEqual(string.Empty, RuntimeId.ToString(int.MinValue));
        }

        private interface IWrapper { }
        private sealed class Wrapper : IWrapper { }

        [Test] public void InitClearsEventsAndRegisteredWrappers()
        {
            var wrapper = new Wrapper();
            GameEvent.EventMgr.RegWrapInterface<IWrapper>(wrapper);
            Assert.AreSame(wrapper, GameEvent.Get<IWrapper>());
            int count = 0;
            GameEvent.AddEventListener(601, () => count++);
            GameEvent.EventMgr.Init();
            GameEvent.Send(601);
            Assert.AreEqual(0, count);
            Assert.IsNull(GameEvent.Get<IWrapper>());
        }

        [Test] public void ListenerExceptionPropagatesAndStopsCurrentDispatch()
        {
            bool secondCalled = false;
            GameEvent.AddEventListener(701, () => { throw new InvalidOperationException("listener"); });
            GameEvent.AddEventListener(701, () => secondCalled = true);
            Assert.Throws<InvalidOperationException>(() => GameEvent.Send(701));
            Assert.IsFalse(secondCalled);
        }
    }
}
