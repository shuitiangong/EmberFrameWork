using System;
using System.Linq;
using System.Threading;
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
            public Action OnClear;
            public void Clear()
            {
                if (FailClear) throw new InvalidOperationException("clear");
                OnClear?.Invoke();
                Value = null;
                Clears++;
            }
            public override bool Equals(object obj) => obj is Item;
            public override int GetHashCode() => 0;
        }
        public class BrokenConstructor : IMemory
        {
            public BrokenConstructor() { throw new InvalidOperationException("constructor"); }
            public void Clear() { }
        }
        public class Hooks : MemoryObject
        {
            public int Stage;
            public override void InitFromPool() { Stage = 1; }
            public override void RecycleToPool() { Assert.AreEqual(1, Stage); Stage = 2; }
            public override void Clear() { Assert.AreEqual(2, Stage); Stage = 0; }
        }
        public class BrokenInit : MemoryObject
        {
            public override void InitFromPool() => throw new InvalidOperationException("init");
            public override void RecycleToPool() { }
            public override void Clear() => throw new InvalidOperationException("clear");
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

        [Test] public void InvalidInputsNeverChangeStatistics()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MemoryPool.Add<Item>(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MemoryPool.Remove<Item>(-1));
            Assert.Throws<ArgumentNullException>(() => MemoryPool.Release(null));
            Assert.Throws<ArgumentException>(() => MemoryPool.Acquire(typeof(string)));
            Assert.Throws<ArgumentException>(() => MemoryPool.Acquire(typeof(IMemory)));
            Assert.AreEqual(0, MemoryPool.Count);
        }

        [Test] public void DuplicateReturnIsCheckedBeforeClear()
        {
            var item = MemoryPool.Acquire<Item>();
            MemoryPool.Release(item);
            Assert.Throws<GameFrameworkException>(() => MemoryPool.Release(item));
            MemoryPool.EnableStrictCheck = false;
            MemoryPool.Release(item);
            Assert.AreEqual(1, item.Clears);
            Assert.AreEqual(1, Info<Item>().UnusedMemoryCount);
            Assert.AreEqual(0, Info<Item>().UsingMemoryCount);
        }

        [Test] public void OwnershipUsesIdentityInsteadOfValueEquality()
        {
            var first = MemoryPool.Acquire<Item>();
            var second = MemoryPool.Acquire<Item>();
            Assert.Throws<GameFrameworkException>(() => MemoryPool.Release(new Item()));
            MemoryPool.Release(first);
            MemoryPool.Release(second);
            Assert.AreEqual(2, Info<Item>().UnusedMemoryCount);
        }

        [Test] public void ConstructorFailureDoesNotCountAsSuccessfulAcquire()
        {
            Assert.Catch(() => MemoryPool.Acquire<BrokenConstructor>());
            var info = Info<BrokenConstructor>();
            Assert.AreEqual(0, info.UsingMemoryCount);
            Assert.AreEqual(0, info.AcquireMemoryCount);
            Assert.AreEqual(0, info.AddMemoryCount);
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

        [Test] public void ClearAllInvalidatesOldLoansWithoutRecreatingPool()
        {
            var item = MemoryPool.Acquire<Item>();
            MemoryPool.ClearAll();
            Assert.Throws<GameFrameworkException>(() => MemoryPool.Release(item));
            MemoryPool.EnableStrictCheck = false;
            MemoryPool.Release(item);
            Assert.AreEqual(0, MemoryPool.Count);
        }

        [Test] public void ReentrantReturnAndClearAllAreRejected()
        {
            var item = MemoryPool.Acquire<Item>();
            item.OnClear = () => {
                Assert.Throws<GameFrameworkException>(() => MemoryPool.Release(item));
                Assert.Throws<InvalidOperationException>(() => MemoryPool.ClearAll());
            };
            MemoryPool.Release(item);
            Assert.AreEqual(1, Info<Item>().ReleaseMemoryCount);
        }

        [Test] public void ExtensionHooksRunInOrderAndFailedInitIsDiscarded()
        {
            var item = MemoryPool.Alloc<Hooks>();
            MemoryPool.Dealloc(item);
            Assert.AreEqual(0, item.Stage);
            Assert.Throws<GameFrameworkException>(() => MemoryPool.Dealloc(item));
            Assert.Throws<AggregateException>(() => MemoryPool.Alloc<BrokenInit>());
            Assert.AreEqual(0, Info<BrokenInit>().UsingMemoryCount);
            Assert.AreEqual(0, Info<BrokenInit>().UnusedMemoryCount);
        }

        [Test] public void ConcurrentUseKeepsCountersConsistent()
        {
            Exception error = null;
            var workers = new Thread[4];
            for (int i = 0; i < workers.Length; i++)
            {
                workers[i] = new Thread(() => {
                    try { for (int n = 0; n < 250; n++) MemoryPool.Release(MemoryPool.Acquire<Item>()); }
                    catch (Exception ex) { Interlocked.CompareExchange(ref error, ex, null); }
                });
                workers[i].Start();
            }
            foreach (var worker in workers) worker.Join();
            if (error != null) throw error;
            var info = Info<Item>();
            Assert.AreEqual(1000, info.AcquireMemoryCount);
            Assert.AreEqual(1000, info.ReleaseMemoryCount);
            Assert.AreEqual(0, info.UsingMemoryCount);
            Assert.AreEqual(info.AddMemoryCount, info.UnusedMemoryCount);
        }
    }
}
