using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;
namespace EmberFrameWork.Tests
{
    internal class ResourceTestModule : Module, IResourceModule
    {
        public readonly List<object> Released = new List<object>();
        public override void OnInit() { }
        public override void Shutdown() { IsReady = false; }
        public string DefaultPackageName { get; set; }
        public EPlayMode PlayMode { get; set; }
        public long Milliseconds { get; set; }
        public bool AutoUnloadBundleWhenUnused { get; set; }
        public bool IsReady { get; set; }
        public string PackageVersion { get; set; }
        public virtual void Initialize() { }
        public virtual UniTask<InitializePackageOperation> InitPackage(string packageName, bool needInitMainFest = false) => throw new NotSupportedException();
        public virtual string GetPackageVersion(string customPackageName = "") => throw new NotSupportedException();
        public virtual RequestPackageVersionOperation RequestPackageVersionAsync(bool appendTimeTicks = false, int timeout = 60, string customPackageName = "") => throw new NotSupportedException();
        public virtual LoadPackageManifestOperation UpdatePackageManifestAsync(string packageVersion, int timeout = 60, string customPackageName = "") => throw new NotSupportedException();
        public virtual AssetInfo GetAssetInfo(string location, string packageName = "") => throw new NotSupportedException();
        public virtual AssetInfo[] GetAssetInfos(string tag, string packageName = "") => throw new NotSupportedException();
        public virtual AssetInfo[] GetAssetInfos(string[] tags, string packageName = "") => throw new NotSupportedException();
        public virtual bool CheckLocationValid(string location, string packageName = "") => throw new NotSupportedException();
        public virtual T LoadAsset<T>(string location, string packageName = "") where T : UnityEngine.Object => throw new NotSupportedException();
        public virtual UnityEngine.Object LoadAsset(string location, Type assetType, string packageName = "") => throw new NotSupportedException();
        public virtual UniTaskVoid LoadAsset<T>(string location, Action<T> callback, string packageName = "") where T : UnityEngine.Object => throw new NotSupportedException();
        public virtual UniTask<T> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default, string packageName = "") where T : UnityEngine.Object => throw new NotSupportedException();
        public virtual UniTask<UnityEngine.Object> LoadAssetAsync(string location, Type assetType, CancellationToken cancellationToken = default, string packageName = "") => throw new NotSupportedException();
        public virtual GameObject LoadGameObject(string location, Transform parent = null, string packageName = "") => throw new NotSupportedException();
        public virtual UniTask<GameObject> LoadGameObjectAsync(string location, Transform parent = null, CancellationToken cancellationToken = default, string packageName = "") => throw new NotSupportedException();
        public virtual AssetHandle LoadAssetSyncHandle<T>(string location, string packageName = "") where T : UnityEngine.Object => throw new NotSupportedException();
        public virtual AssetHandle LoadAssetSyncHandle(string location, Type type, string packageName = "") => throw new NotSupportedException();
        public virtual AssetHandle LoadAssetAsyncHandle<T>(string location, string packageName = "") where T : UnityEngine.Object => throw new NotSupportedException();
        public virtual AssetHandle LoadAssetAsyncHandle(string location, Type assetType, string packageName = "") => throw new NotSupportedException();
        public float AssetAutoReleaseInterval { get; set; }
        public int AssetCapacity { get; set; }
        public float AssetExpireTime { get; set; }
        public int AssetPriority { get; set; }
        public virtual void UnloadAsset(object asset) => Released.Add(asset);
        public virtual void UnloadUnusedAssets() => throw new NotSupportedException();
        public virtual UniTask UnloadUnusedAssetsAsync() => throw new NotSupportedException();
        public virtual void OnLowMemory() => throw new NotSupportedException();
        public virtual void SetForceUnloadUnusedAssetsAction(Action<bool> action) => throw new NotSupportedException();
        public virtual void ForceUnloadUnusedAssets(bool performGCCollect) => throw new NotSupportedException();
    }
}
