using System;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using EmberFrameWork;
using EmberFrameWork.Game;
using UnityEditor;
using UnityEngine;

namespace EmberFrameWork.Editor
{
    /// <summary>在 Launch 场景运行后验证真实资源加载、借用计数和空闲缓存回收。</summary>
    public static class ResourceSmokeValidation
    {
        private const string JsonLocation = "Assets/RawAssets/Configs/Json/demo_tbitem.json";
        private const string BinLocation = "Assets/RawAssets/Configs/Bin/demo_tbitem.bytes";
        private static bool _running;

        [Serializable]
        private sealed class Report
        {
            public string package;
            public string version;
            public string playMode;
            public int jsonCharacters;
            public int binaryBytes;
            public bool reusedSameAsset;
            public bool keptWhileBorrowed;
            public int remainingCachedAssets;
        }

        [MenuItem("EmberFrameWork/Resources/验证加载与释放")]
        private static void RunMenu() => RunAndLog().Forget();

        [MenuItem("EmberFrameWork/Resources/验证加载与释放", true)]
        private static bool CanRun() => !_running && Application.isPlaying && GameEntry.Instance != null && GameEntry.Instance.IsGameReady;

        private static async UniTaskVoid RunAndLog()
        {
            try { Debug.Log("[ResourceValidation] " + await ValidateAsync()); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        public static async UniTask<string> ValidateAsync()
        {
            if (!CanRun()) throw new InvalidOperationException("请先运行 Launch 场景并等待游戏就绪，且不能并行执行验证。");
            var resource = ModuleSystem.GetModule<IResourceModule>();
            var pools = ModuleSystem.GetModule<IObjectPoolModule>();
            ObjectPoolBase FindPool() => pools.GetAllObjectPools().SingleOrDefault(p => p.Name == "Resource Assets");
            if (FindPool() is ObjectPoolBase occupied && occupied.Count != 0)
                throw new InvalidOperationException("请在资源缓存为空时验证，避免干扰业务正在持有的资源。");

            _running = true;
            TextAsset json = null;
            TextAsset binary = null;
            int jsonBorrows = 0;
            bool binaryBorrowed = false;
            try
            {
                json = await resource.LoadAssetAsync<TextAsset>(JsonLocation); jsonBorrows++;
                var sameJson = resource.LoadAsset<TextAsset>(JsonLocation); jsonBorrows++;
                if (!ReferenceEquals(json, sameJson)) throw new InvalidOperationException("缓存未复用同一个资源对象。");
                if (json.text != File.ReadAllText(JsonLocation)) throw new InvalidOperationException("JSON 内容与项目源文件不一致。");
                binary = await resource.LoadAssetAsync<TextAsset>(BinLocation); binaryBorrowed = true;
                if (!binary.bytes.SequenceEqual(File.ReadAllBytes(BinLocation))) throw new InvalidOperationException("二进制内容不一致。");

                var report = new Report { package = resource.DefaultPackageName, version = resource.PackageVersion,
                    playMode = resource.PlayMode.ToString(), jsonCharacters = json.text.Length,
                    binaryBytes = binary.bytes.Length, reusedSameAsset = true };
                var pool = FindPool();
                if (pool == null || pool.Count != 2 || pool.GetAllObjectInfos().Sum(i => i.SpawnCount) != 3)
                    throw new InvalidOperationException("资源缓存数量或借用计数不正确。");

                resource.UnloadAsset(json); jsonBorrows--;
                await resource.UnloadUnusedAssetsAsync();
                if (pool.Count != 2 || pool.GetAllObjectInfos().Sum(i => i.SpawnCount) != 2)
                    throw new InvalidOperationException("仍被借用的资源提前释放。");
                report.keptWhileBorrowed = true;

                resource.UnloadAsset(json); jsonBorrows--;
                resource.UnloadAsset(binary); binaryBorrowed = false;
                await resource.UnloadUnusedAssetsAsync();
                report.remainingCachedAssets = pool.Count;
                if (pool.Count != 0) throw new InvalidOperationException("全部归还后缓存仍未清空。");

                // 再次读取，验证释放后重新加载仍可用。
                json = await resource.LoadAssetAsync<TextAsset>(JsonLocation); jsonBorrows++;
                if (json.text != File.ReadAllText(JsonLocation)) throw new InvalidOperationException("再次加载内容不一致。");
                resource.UnloadAsset(json); jsonBorrows--;
                await resource.UnloadUnusedAssetsAsync();
                return JsonUtility.ToJson(report);
            }
            finally
            {
                try
                {
                    if (resource.IsReady)
                    {
                        while (jsonBorrows-- > 0) resource.UnloadAsset(json);
                        if (binaryBorrowed) resource.UnloadAsset(binary);
                        await resource.UnloadUnusedAssetsAsync();
                    }
                }
                finally { _running = false; }
            }
        }
    }
}
