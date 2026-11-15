using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YooAsset;
using Object = UnityEngine.Object;

namespace EmberFrameWork.Tests
{
    public class AssetsReferenceTests
    {
        // 仅记录归还；本测试不初始化资源包，也不进行真实资源加载。
        private sealed class RecordingResourceModule : IResourceModule
        {
            public readonly List<object> Released = new List<object>();
            public void UnloadAsset(object asset) => Released.Add(asset);
            public bool IsConfigured => false;
            public string DefaultPackageName => null;
            public EPlayMode PlayMode => default;
            public ResourceOptions Options => null;
            public ResourceInitializationState InitializationState => default;
            public bool IsReady => false;
            public string PackageVersion => null;
            public string LastError => null;
            public float AssetAutoReleaseInterval { get; set; }
            public int AssetCapacity { get; set; }
            public float AssetExpireTime { get; set; }
            public int AssetPriority { get; set; }
            public UniTask<string> InitPackage() => throw new NotSupportedException();
            public T LoadAsset<T>(string location, string packageName = "") where T : Object => throw new NotSupportedException();
            public UniTask<T> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default, string packageName = "") where T : Object => throw new NotSupportedException();
            public GameObject LoadGameObject(string location, Transform parent = null, string packageName = "") => throw new NotSupportedException();
            public UniTask<GameObject> LoadGameObjectAsync(string location, Transform parent = null, CancellationToken cancellationToken = default, string packageName = "") => throw new NotSupportedException();
            public UniTask UnloadUnusedAssetsAsync() => throw new NotSupportedException();
            public void Configure(ResourceOptions options) => throw new NotSupportedException();
            public void Configure(ResourceSetting setting) => throw new NotSupportedException();
        }

        [UnityTest]
        public IEnumerator RebindingAndCloningPreserveResourceOwnership()
        {
            yield return new EnterPlayMode();
            var moduleField = typeof(AssetsReference).GetField("_resourceModule", BindingFlags.Static | BindingFlags.NonPublic);
            var previousModule = moduleField.GetValue(null);
            var resource = new RecordingResourceModule();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RawAssets/UI/UIExampleWindow.prefab");
            var original = new GameObject("AssetsReference Original");
            original.SetActive(false);
            GameObject clone = null;
            GameObject instantiated = null;
            var first = new Texture2D(1, 1);
            var second = new Texture2D(1, 1);
            try
            {
                Assert.IsNotNull(source);
                var reference = original.AddComponent<AssetsReference>();
                Assert.AreSame(reference, AssetsReference.Ref(source, original, resource));
                Assert.AreSame(reference, AssetsReference.Ref(first, original, resource));
                Assert.AreSame(reference, AssetsReference.Ref(second, original, resource));
                Assert.AreEqual(1, original.GetComponents<AssetsReference>().Length);

                // 先绑定再激活，Awake 必须保留原实例的资源记录。
                // 同一帧内销毁测试实例，避免 Launch 的异步场景切换影响归还计数。
                original.SetActive(true);
                clone = Object.Instantiate(original);
                Object.DestroyImmediate(clone);
                Assert.IsEmpty(resource.Released, "克隆实例没有借用资源，销毁时不得重复归还原实例的引用。");

                Object.DestroyImmediate(original);
                CollectionAssert.AreEqual(new object[] { source, first, second }, resource.Released);

                resource.Released.Clear();
                instantiated = AssetsReference.Instantiate(source, resourceModule: resource).gameObject;
                Object.DestroyImmediate(instantiated);
                CollectionAssert.AreEqual(new object[] { source }, resource.Released);
            }
            finally
            {
                if (clone != null) Object.DestroyImmediate(clone);
                if (original != null) Object.DestroyImmediate(original);
                if (instantiated != null) Object.DestroyImmediate(instantiated);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                moduleField.SetValue(null, previousModule);
            }
            yield return new ExitPlayMode();
        }
    }
}
