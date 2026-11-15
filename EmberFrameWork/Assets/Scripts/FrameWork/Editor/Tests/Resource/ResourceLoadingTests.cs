using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using YooAsset;

namespace EmberFrameWork.Tests
{
    // 原 Backend/Lease 缓存已移除，不再断言额外的别名合并、提前取消抛异常等行为。
    public class ResourceLoadingTests
    {
        private ResourceModule _module;
        [SetUp] public void SetUp()
        {
            GameFrameworkLog.SetLogHelper(null);
            ModuleSystem.Shutdown();
            _module = new ResourceModule();
        }
        [TearDown] public void TearDown()
        {
            _module.Shutdown();
            ModuleSystem.Shutdown();
            GameFrameworkLog.SetLogHelper(new DefaultLogHelper());
        }

        [Test] public void CacheKeyUsesOriginalDefaultAndNamedPackageRules()
        {
            var method = typeof(ResourceModule).GetMethod("GetCacheKey", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.AreEqual("panel", method.Invoke(_module, new object[] { "panel", "" }));
            Assert.AreEqual("panel", method.Invoke(_module, new object[] { "panel", "DefaultPackage" }));
            Assert.AreEqual("Dlc/panel", method.Invoke(_module, new object[] { "panel", "Dlc" }));
        }

        [Test] public void OriginalPoolPropertiesDelegateToMultiSpawnAssetPool()
        {
            var pools = ModuleSystem.GetModule<IObjectPoolModule>();
            _module.SetObjectPoolModule(pools);
            _module.AssetCapacity = 20;
            _module.AssetAutoReleaseInterval = 15f;
            _module.AssetExpireTime = 30f;
            _module.AssetPriority = 3;
            var pool = pools.GetAllObjectPools().Single();
            Assert.AreEqual("Asset Pool", pool.Name);
            Assert.IsTrue(pool.AllowMultiSpawn);
            Assert.AreEqual(20, pool.Capacity);
            Assert.AreEqual(15f, pool.AutoReleaseInterval);
            Assert.AreEqual(30f, pool.ExpireTime);
            Assert.AreEqual(3, pool.Priority);
        }

        [Test] public void EmptyLocationsKeepOriginalFrameworkException()
        {
            Assert.Throws<GameFrameworkException>(() => _module.LoadAsset<UnityEngine.Object>(""));
            Assert.Throws<GameFrameworkException>(() => _module.LoadGameObject(null));
            Assert.Throws<GameFrameworkException>(() => _module.LoadAssetAsync<UnityEngine.Object>(null).GetAwaiter().GetResult());
            Assert.Throws<GameFrameworkException>(() => _module.LoadGameObjectAsync("").GetAwaiter().GetResult());
        }

        [Test] public void UnloadBeforePoolSetupIsOriginalNoOp()
        {
            Assert.DoesNotThrow(() => _module.UnloadAsset(new object()));
            Assert.Throws<GameFrameworkException>(() => _module.SetObjectPoolModule(null));
        }
    }
}
