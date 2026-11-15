using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using YooAsset;
using Object = UnityEngine.Object;
namespace EmberFrameWork.Tests
{
    public class ResourceInitializationTests
    {
        [Test] public void PackageInitializationRequiresInitialize()
        {
            var resource = new ResourceModule();
            try { Assert.Throws<InvalidOperationException>(() => resource.InitPackage("DefaultPackage", true).GetAwaiter().GetResult()); }
            finally { resource.Shutdown(); }
        }
        [Test] public void ShutdownIsIdempotentAndRejectsInitialization()
        {
            var resource = new ResourceModule();
            resource.Shutdown();
            Assert.DoesNotThrow(() => resource.Shutdown());
            Assert.IsFalse(resource.IsReady);
            Assert.Throws<ObjectDisposedException>(() => resource.Initialize());
            Assert.Throws<ObjectDisposedException>(() => resource.InitPackage("DefaultPackage").GetAwaiter().GetResult());
        }
        // 独立的运行时集成验证入口；从已启动的 Launch 会话调用并等待返回的 Task。
        public static async Task VerifyRunningGame()
        {
            Assert.IsTrue(Application.isPlaying);
            var resource = ModuleSystem.GetModule<IResourceModule>();
            Assert.IsTrue(resource.IsReady);
            Assert.IsTrue(YooAssets.IsInitialized);
            Assert.IsNotEmpty(resource.PackageVersion);
            Assert.IsNotNull(Object.FindObjectOfType<ResourceModuleDriver>());
            const string path = "Assets/RawAssets/UI/UIExampleWindow.prefab";
            var pool = ModuleSystem.GetModule<IObjectPoolModule>().GetAllObjectPools().Single(p => p.Name == "Asset Pool");
            int Count() => pool.GetAllObjectInfos().Where(info => info.Name == path).Sum(info => info.SpawnCount);
            int baseline = Count();
            Assert.IsTrue(resource.CheckLocationValid(path));
            var first = resource.LoadAsset<GameObject>(path);
            var second = resource.LoadAsset(path, typeof(GameObject));
            Assert.AreSame(first, second);
            Assert.AreEqual(baseline + 2, Count());
            resource.UnloadAsset(second);
            resource.UnloadAsset(first);
            Assert.AreEqual(baseline, Count());
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = resource.LoadAssetAsync<GameObject>(path, cancellation.Token);
                cancellation.Cancel();
                Assert.IsNull(await pending);
            }
            Assert.AreEqual(baseline, Count(), "取消缓存命中的请求必须归还借用。");
            var pendingFirst = resource.LoadAssetAsync<GameObject>(path);
            var pendingSecond = resource.LoadAssetAsync<GameObject>(path);
            var pair = await UniTask.WhenAll(pendingFirst, pendingSecond);
            Assert.AreSame(pair.Item1, pair.Item2);
            Assert.AreEqual(baseline + 2, Count());
            resource.UnloadAsset(pair.Item1);
            resource.UnloadAsset(pair.Item2);
            GameObject loaded = null;
            loaded = await resource.LoadGameObjectAsync(path);
            Assert.IsNotNull(loaded.GetComponent<AssetsReference>());
            Object.DestroyImmediate(loaded);
            Assert.AreEqual(baseline, Count(), "实例销毁必须归还 Prefab 借用。");
            var handle = resource.LoadAssetSyncHandle<GameObject>(path);
            Assert.AreEqual(EOperationStatus.Succeeded, handle.Status);
            handle.Release();
            Assert.IsFalse(handle.IsValid);
            Assert.Throws<GameFrameworkException>(() => resource.LoadAsset<GameObject>("missing-resource"));
            await resource.UnloadUnusedAssetsAsync();
            var driver = Object.FindObjectOfType<ResourceModuleDriver>();
            bool automatic = driver.UseSystemUnloadUnusedAssets;
            try
            {
                driver.UseSystemUnloadUnusedAssets = false;
                resource.OnLowMemory();
                await UniTask.NextFrame();
                Assert.Less(driver.LastUnloadUnusedAssetsOperationElapseSeconds, 1f);
            }
            finally { driver.UseSystemUnloadUnusedAssets = automatic; }
        }
    }
}
