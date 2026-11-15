using System;
using System.Linq;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public class MemoryPoolTests
    {
        public class Item : IMemory
        {
            public object Value;
            public int Clears;
            public bool FailClear;
            public void Clear()
            {
                if (FailClear) throw new InvalidOperationException("clear");
                Value = null;
                Clears++;
            }
        }
        public class EqualItem : Item
        {
            public override bool Equals(object obj) => obj is EqualItem;
            public override int GetHashCode() => 0;
        }
        public class BrokenConstructor : IMemory
        {
            public BrokenConstructor() { throw new InvalidOperationException("constructor"); }
            public void Clear() { }
        }
        private bool _strict;
        [SetUp] public void SetUp() { _strict = MemoryPool.EnableStrictCheck; MemoryPool.ClearAll(); MemoryPool.EnableStrictCheck = true; }
        [TearDown] public void TearDown() { MemoryPool.ClearAll(); MemoryPool.EnableStrictCheck = _strict; }
        private static MemoryPoolInfo Info<T>() => MemoryPool.GetAllMemoryPoolInfos().Single(x => x.Type == typeof(T));

        [Test] public void AcquireReleaseReusesAndClears()
        {
            var item = MemoryPool.Acquire<Item>();
            item.Value = new object();
            MemoryPool.Release(item);
            Assert.IsNull(item.Value);
            Assert.AreSame(item, MemoryPool.Acquire(typeof(Item)));
            Assert.AreEqual(2, Info<Item>().AcquireMemoryCount);
            Assert.AreEqual(1, Info<Item>().AddMemoryCount);
            Assert.AreEqual(1, Info<Item>().UsingMemoryCount);
        }

        [Test] public void PrewarmAndTrimOnlyRemoveUnusedObjects()
        {
            MemoryPool.Add<Item>(2);
            MemoryPool.Add(typeof(Item), 1);
            var item = MemoryPool.Acquire<Item>();
            MemoryPool.Remove<Item>(99);
            Assert.AreEqual(2, Info<Item>().RemoveMemoryCount);
            Assert.AreEqual(1, Info<Item>().UsingMemoryCount);
            MemoryPool.Release(item);
            MemoryPool.RemoveAll(typeof(Item));
            Assert.AreEqual(0, Info<Item>().UnusedMemoryCount);
        }

        [Test] public void TypeChecksAndNegativeCountsMatchOriginal()
        {
            Assert.Throws<Exception>(() => MemoryPool.Release(null));
            Assert.Throws<Exception>(() => MemoryPool.Acquire(typeof(string)));
            Assert.Throws<Exception>(() => MemoryPool.Acquire(typeof(IMemory)));
            Assert.AreEqual(0, MemoryPool.Count);
            // 原版不拒绝负数，直接累计到统计字段。
            MemoryPool.Add<Item>(-1);
            MemoryPool.Remove<Item>(-1);
            Assert.AreEqual(-1, Info<Item>().AddMemoryCount);
            Assert.AreEqual(-1, Info<Item>().RemoveMemoryCount);
        }

        [Test] public void DuplicateReturnClearsBeforeStrictCheck()
        {
            var item = MemoryPool.Acquire<Item>();
            MemoryPool.Release(item);
            Assert.Throws<Exception>(() => MemoryPool.Release(item));
            Assert.AreEqual(2, item.Clears);
            Assert.AreEqual(1, Info<Item>().UnusedMemoryCount);
            MemoryPool.EnableStrictCheck = false;
            MemoryPool.Release(item);
            Assert.AreEqual(3, item.Clears);
            Assert.AreEqual(2, Info<Item>().UnusedMemoryCount);
            Assert.AreEqual(-1, Info<Item>().UsingMemoryCount);
        }

        [Test] public void StrictCheckUsesQueueValueEquality()
        {
            var first = MemoryPool.Acquire<EqualItem>();
            var second = MemoryPool.Acquire<EqualItem>();
            MemoryPool.Release(first);
            Assert.Throws<Exception>(() => MemoryPool.Release(second));
            Assert.AreEqual(1, Info<EqualItem>().UnusedMemoryCount);
            Assert.AreEqual(1, Info<EqualItem>().UsingMemoryCount);
            Assert.AreEqual(1, second.Clears);
        }

        [Test] public void ConstructorFailureKeepsOriginalPreIncrementedCounts()
        {
            Assert.Catch(() => MemoryPool.Acquire<BrokenConstructor>());
            var info = Info<BrokenConstructor>();
            Assert.AreEqual(1, info.UsingMemoryCount);
            Assert.AreEqual(1, info.AcquireMemoryCount);
            Assert.AreEqual(1, info.AddMemoryCount);
        }

        [Test] public void ClearFailureCanBeRetried()
        {
            var item = MemoryPool.Acquire<Item>();
            item.FailClear = true;
            Assert.Throws<InvalidOperationException>(() => MemoryPool.Release(item));
            Assert.AreEqual(1, Info<Item>().UsingMemoryCount);
            item.FailClear = false;
            MemoryPool.Release(item);
            Assert.AreEqual(1, Info<Item>().ReleaseMemoryCount);
        }

        [Test] public void ReturnAfterClearAllCreatesNewCollection()
        {
            var item = MemoryPool.Acquire<Item>();
            MemoryPool.ClearAll();
            MemoryPool.Release(item);
            Assert.AreEqual(1, MemoryPool.Count);
            Assert.AreEqual(1, Info<Item>().UnusedMemoryCount);
            Assert.AreEqual(-1, Info<Item>().UsingMemoryCount);
        }

        [Test] public void CollectionsRemainSeparatedByRuntimeType()
        {
            var first = MemoryPool.Acquire<Item>();
            var second = MemoryPool.Acquire<EqualItem>();
            MemoryPool.Release(first);
            MemoryPool.Release(second);
            Assert.AreEqual(2, MemoryPool.Count);
            Assert.AreEqual(1, Info<Item>().UnusedMemoryCount);
            Assert.AreEqual(1, Info<EqualItem>().UnusedMemoryCount);
        }

        [Test] public void SequentialAcquireReleaseCountersMatch()
        {
            // 原版只有部分队列操作加锁，不再断言新增实现的并发计数保证。
            for (int i = 0; i < 1000; i++) MemoryPool.Release(MemoryPool.Acquire<Item>());
            var info = Info<Item>();
            Assert.AreEqual(1000, info.AcquireMemoryCount);
            Assert.AreEqual(1000, info.ReleaseMemoryCount);
            Assert.AreEqual(0, info.UsingMemoryCount);
            Assert.AreEqual(1, info.UnusedMemoryCount);
        }
    }
}
