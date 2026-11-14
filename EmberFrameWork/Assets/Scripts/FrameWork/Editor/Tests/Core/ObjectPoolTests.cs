using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public class ObjectPoolTests
    {
        public class Item : ObjectBase
        {
            public readonly List<string> Events = new List<string>();
            public bool FailSpawn, FailUnspawn, FailRelease;
            public bool Releasable = true;
            public override bool CustomCanReleaseFlag => Releasable;
            public void Init(string name, object target) => Initialize(name, target);
            protected override void OnSpawn() { if (FailSpawn) throw new InvalidOperationException("spawn"); Events.Add("spawn"); }
            protected override void OnUnspawn() { if (FailUnspawn) throw new InvalidOperationException("unspawn"); Events.Add("unspawn"); }
            protected override void Release(bool shutdown) { Events.Add(shutdown ? "shutdown" : "release"); if (FailRelease) throw new InvalidOperationException("release"); }
        }
        private IObjectPoolModule _module;
        private bool _strict;
        [SetUp] public void SetUp()
        {
            ModuleSystem.Shutdown(); MemoryPool.ClearAll();
            _strict = MemoryPool.EnableStrictCheck; MemoryPool.EnableStrictCheck = true;
            GameFrameworkLog.SetLogHelper(null);
            _module = ModuleSystem.GetModule<IObjectPoolModule>();
        }
        [TearDown] public void TearDown()
        {
            try { ModuleSystem.Shutdown(); }
            finally { MemoryPool.ClearAll(); MemoryPool.EnableStrictCheck = _strict; GameFrameworkLog.ResetLogHelper(); }
        }
        private static Item Make(string name = "asset", bool pooled = true, object target = null)
        {
            var item = pooled ? MemoryPool.Acquire<Item>() : new Item();
            item.Init(name, target ?? new object()); return item;
        }

        [Test] public void SingleSpawnRequiresReturnAndDoesNotCreateMissingObjects()
        {
            var pool = _module.CreateSingleSpawnObjectPool<Item>("single");
            var item = Make(); pool.Register(item, false);
            Assert.AreSame(item, pool.Spawn("asset"));
            Assert.IsNull(pool.Spawn("asset"));
            Assert.IsNull(pool.Spawn("missing"));
            pool.Unspawn(item);
            Assert.Throws<GameFrameworkException>(() => pool.Unspawn(item));
            Assert.AreEqual(0, ((ObjectPoolBase)pool).GetAllObjectInfos()[0].SpawnCount);
            Assert.AreSame(item, pool.Spawn("asset"));
        }

        [Test] public void MultiSpawnOnlyReleasesAfterLastReturn()
        {
            var pool = _module.CreateMultiSpawnObjectPool<Item>(); var item = Make();
            pool.Register(item, true); pool.Spawn("asset");
            Assert.AreEqual(2, ((ObjectPoolBase)pool).GetAllObjectInfos()[0].SpawnCount);
            pool.Unspawn(item); Assert.IsFalse(pool.ReleaseObject(item));
            pool.Unspawn(item); Assert.IsTrue(pool.ReleaseObject(item));
            CollectionAssert.AreEqual(new[] { "spawn", "spawn", "unspawn", "unspawn", "release" }, item.Events);
            Assert.AreEqual(0, MemoryPool.GetAllMemoryPoolInfos().Single(x => x.Type == typeof(Item)).UsingMemoryCount);
        }

        [Test] public void DuplicateAndCrossPoolRegistrationLeaveCollectionsUnchanged()
        {
            var a = _module.CreateSingleSpawnObjectPool<Item>("a");
            var b = _module.CreateSingleSpawnObjectPool<Item>("b");
            var item = Make(); a.Register(item, false);
            Assert.Throws<GameFrameworkException>(() => a.Register(item, false));
            Assert.Throws<GameFrameworkException>(() => b.Register(item, false));
            var duplicate = Make(target: item.Target);
            Assert.Throws<GameFrameworkException>(() => a.Register(duplicate, false));
            MemoryPool.Release(duplicate);
            Assert.AreEqual(1, a.Count); Assert.AreEqual(0, b.Count);
        }

        [Test] public void LocksAndCustomFlagsProtectUnusedObjects()
        {
            var pool = _module.CreateSingleSpawnObjectPool<Item>();
            var locked = Make("locked"); locked.Locked = true;
            var custom = Make("custom"); custom.Releasable = false;
            pool.Register(locked, false); pool.Register(custom, false);
            pool.ReleaseAllUnused(); Assert.AreEqual(2, pool.Count);
            pool.SetLocked(locked, false); custom.Releasable = true;
            pool.ReleaseAllUnused(); Assert.AreEqual(0, pool.Count);
        }

        [Test] public void CapacityReleasesLowerPriorityFirst()
        {
            var pool = _module.CreateSingleSpawnObjectPool<Item>();
            var low = Make("low"); low.Priority = -1;
            var high = Make("high"); high.Priority = 5;
            pool.Register(high, false); pool.Register(low, false);
            pool.Capacity = 1;
            Assert.AreEqual(1, pool.Count); Assert.IsTrue(pool.CanSpawn("high"));
            CollectionAssert.Contains(low.Events, "release");
        }

        [Test] public void ModuleUpdateAutomaticallyReleasesExpiredObjects()
        {
            var pool = _module.CreateSingleSpawnObjectPool<Item>("expiring", 0f, 10, 0f, 0);
            pool.Register(Make(), false);
            ModuleSystem.Update(0, 0);
            Assert.AreEqual(0, pool.Count);
        }

        [Test] public void FailedSpawnAndUnspawnDoNotCorruptCounts()
        {
            var pool = _module.CreateSingleSpawnObjectPool<Item>(); var item = Make();
            item.FailSpawn = true;
            Assert.Throws<InvalidOperationException>(() => pool.Register(item, true));
            Assert.AreEqual(0, pool.Count);
            item.FailSpawn = false; pool.Register(item, false);
            item.FailSpawn = true; Assert.Throws<InvalidOperationException>(() => pool.Spawn("asset"));
            Assert.AreEqual(0, ((ObjectPoolBase)pool).GetAllObjectInfos()[0].SpawnCount);
            item.FailSpawn = false; pool.Spawn("asset");
            item.FailUnspawn = true; Assert.Throws<InvalidOperationException>(() => pool.Unspawn(item));
            Assert.AreEqual(1, ((ObjectPoolBase)pool).GetAllObjectInfos()[0].SpawnCount);
        }

        [Test] public void ShutdownCleansOtherPoolsEvenIfReleaseThrows()
        {
            var a = _module.CreateSingleSpawnObjectPool<Item>("a");
            var b = _module.CreateMultiSpawnObjectPool<Item>("b");
            var bad = Make(); bad.FailRelease = true;
            var good = Make(); a.Register(bad, true); b.Register(good, true);
            Assert.Throws<AggregateException>(() => ModuleSystem.Shutdown());
            CollectionAssert.Contains(good.Events, "shutdown");
            Assert.AreEqual(0, _module.Count);
            Assert.AreEqual(0, MemoryPool.GetAllMemoryPoolInfos().Sum(x => x.UsingMemoryCount));
            Assert.Throws<ObjectDisposedException>(() => a.Register(Make(pooled: false), false));
        }

        [Test] public void DirectlyConstructedWrappersAreClearedWithoutInvalidMemoryReturn()
        {
            var pool = _module.CreateSingleSpawnObjectPool<Item>(); var item = Make(pooled: false);
            pool.Register(item, false); Assert.IsTrue(pool.ReleaseObject(item)); Assert.IsNull(item.Target);
        }

        [Test] public void RuntimeTypeFactoryAndDestroyWork()
        {
            var pool = _module.CreateSingleSpawnObjectPool(typeof(Item), "dynamic");
            Assert.AreSame(pool, _module.GetObjectPool<Item>("dynamic"));
            Assert.IsTrue(_module.DestroyObjectPool<Item>("dynamic"));
            Assert.IsFalse(_module.HasObjectPool<Item>("dynamic"));
        }
    }
}
