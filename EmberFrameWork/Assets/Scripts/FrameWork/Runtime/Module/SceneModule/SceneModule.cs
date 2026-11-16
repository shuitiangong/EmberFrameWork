// Adapted from TEngine main 3ffab9c6 (MIT). See ../../Core/LICENSE.txt.
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using SceneHandle = YooAsset.SceneHandle;
using YooAsset;

namespace EmberFrameWork
{
    internal class SceneModule : Module, ISceneModule
    {
        private string _currentMainSceneName = string.Empty;

        private SceneHandle _currentMainScene;
        private bool _shutdown;

        private readonly Dictionary<string, SceneHandle> _subScenes = new Dictionary<string, SceneHandle>();

        private readonly HashSet<string> _handlingScene = new HashSet<string>();

        /// <summary>
        /// 当前主场景名称。
        /// </summary>
        public string CurrentMainSceneName => _currentMainSceneName;

        // YooAsset 3 使用明确包引用，allowSceneActivation 与旧 suspendLoad 取反。
        private SceneHandle LoadSceneHandle(string location, LoadSceneMode mode, bool suspendLoad, uint priority)
        {
            if (_shutdown) throw new ObjectDisposedException(nameof(SceneModule));
            var resource = ModuleSystem.GetModule<IResourceModule>();
            if (!resource.IsReady) throw new InvalidOperationException("默认资源包尚未就绪。");
            var package = YooAssets.GetPackage(resource.DefaultPackageName);
            var info = package.GetAssetInfo(location);
            if (!info.IsValid || !info.AssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                throw new GameFrameworkException($"无效场景地址：{location}");
            return package.LoadSceneAsync(info, mode, LocalPhysicsMode.None, !suspendLoad, priority);
        }

        public override void OnInit()
        {
            _currentMainScene = null;
            _currentMainSceneName = SceneManager.GetSceneByBuildIndex(0).name;
        }

        public override void Shutdown()
        {
            if (_shutdown) return;
            _shutdown = true;
            if (_currentMainScene is { IsValid: true }) _currentMainScene.AllowSceneActivation();
            var iter = _subScenes.Values.GetEnumerator();
            while (iter.MoveNext())
            {
                SceneHandle subScene = iter.Current;
                if (subScene is { IsValid: true })
                {
                    subScene.AllowSceneActivation();
                    subScene.UnloadSceneAsync();
                }
            }

            iter.Dispose();
            _subScenes.Clear();
            _handlingScene.Clear();
            _currentMainSceneName = string.Empty;
        }

        /// <summary>
        /// 加载场景。
        /// </summary>
        /// <param name="location">场景的定位地址</param>
        /// <param name="sceneMode">场景加载模式</param>
        /// <param name="suspendLoad">加载完毕时是否主动挂起</param>
        /// <param name="priority">优先级</param>
        /// <param name="gcCollect">加载主场景是否回收垃圾。</param>
        /// <param name="progressCallBack">加载进度回调。</param>
        public async UniTask<Scene> LoadSceneAsync(string location, LoadSceneMode sceneMode = LoadSceneMode.Single, bool suspendLoad = false, uint priority = 100,
            bool gcCollect = true, Action<float> progressCallBack = null)
        {
            if (!_handlingScene.Add(location))
            {
                Log.Error($"Could not load scene while loading. Scene: {location}");
                return default;
            }

            SceneHandle pending = null;
            try
            {
                if (sceneMode == LoadSceneMode.Additive)
                {
                    if (_subScenes.TryGetValue(location, out SceneHandle subScene))
                    {
                        throw new Exception($"Could not load subScene while already loaded. Scene: {location}");
                    }

                    subScene = pending = LoadSceneHandle(location, sceneMode, suspendLoad, priority);

                    //Fix 这里前置，subScene.IsDone在UnSupendLoad之后才会是true
                    _subScenes.Add(location, subScene);

                    if (progressCallBack != null)
                    {
                        while (!subScene.IsDone && subScene.IsValid)
                        {
                            progressCallBack.Invoke(subScene.Progress);
                            await UniTask.Yield();
                        }
                    }
                    else
                    {
                        await subScene.ToUniTask();
                    }

                    _handlingScene.Remove(location);

                    EnsureSceneLoaded(subScene);
                    return subScene.SceneObject;
                }
                else
                {
                    if (_currentMainScene is { IsDone: false })
                    {
                        throw new Exception($"Could not load MainScene while loading. CurrentMainScene: {_currentMainSceneName}.");
                    }

                    _currentMainScene = pending = LoadSceneHandle(location, sceneMode, suspendLoad, priority);
                    _currentMainSceneName = location;

                    if (progressCallBack != null)
                    {
                        while (!_currentMainScene.IsDone && _currentMainScene.IsValid)
                        {
                            progressCallBack.Invoke(_currentMainScene.Progress);
                            await UniTask.Yield();
                        }
                    }
                    else
                    {
                        await _currentMainScene.ToUniTask();
                    }
                    ModuleSystem.GetModule<IResourceModule>().ForceUnloadUnusedAssets(gcCollect);

                    _handlingScene.Remove(location);

                    EnsureSceneLoaded(_currentMainScene);
                    _subScenes.Clear(); // Single 模式已卸载旧的叠加场景。
                    return _currentMainScene.SceneObject;
                }
            }
            catch
            {
                if (pending != null)
                {
                    if (pending.IsValid && pending.Status != EOperationStatus.Succeeded) pending.Release();
                    if (_subScenes.TryGetValue(location, out var tracked) && tracked == pending) _subScenes.Remove(location);
                    if (_currentMainScene == pending) { _currentMainScene = null; _currentMainSceneName = string.Empty; }
                }
                throw;
            }
            finally { _handlingScene.Remove(location); }
        }

        /// <summary>
        /// 加载场景。
        /// </summary>
        /// <param name="location">场景的定位地址</param>
        /// <param name="sceneMode">场景加载模式</param>
        /// <param name="suspendLoad">加载完毕时是否主动挂起</param>
        /// <param name="priority">优先级</param>
        /// <param name="callBack">加载回调。</param>
        /// <param name="gcCollect">加载主场景是否回收垃圾。</param>
        /// <param name="progressCallBack">加载进度回调。</param>
        public void LoadScene(string location, LoadSceneMode sceneMode = LoadSceneMode.Single, bool suspendLoad = false, uint priority = 100,
            Action<Scene> callBack = null,
            bool gcCollect = true, Action<float> progressCallBack = null)
        {
            LoadSceneWithCallback(location, sceneMode, suspendLoad, priority, callBack, gcCollect, progressCallBack).Forget();
        }

        private async UniTask LoadSceneWithCallback(string location, LoadSceneMode mode, bool suspend, uint priority, Action<Scene> callback, bool gc, Action<float> progress)
        {
            var scene = await LoadSceneAsync(location, mode, suspend, priority, gc, progress);
            if (scene.IsValid()) callback?.Invoke(scene);
        }
        private void EnsureSceneLoaded(SceneHandle handle)
        {
            if (_shutdown) throw new ObjectDisposedException(nameof(SceneModule));
            if (!handle.IsValid || handle.Status != EOperationStatus.Succeeded)
                throw new GameFrameworkException($"场景加载失败：{(handle.IsValid ? handle.Error : "句柄已释放")}");
        }
        private async UniTaskVoid InvokeProgress(SceneHandle sceneHandle, Action<float> progress)
        {
            if (sceneHandle == null)
            {
                return;
            }

            while (!sceneHandle.IsDone && sceneHandle.IsValid)
            {
                await UniTask.Yield();

                progress?.Invoke(sceneHandle.Progress);
            }
        }

        /// <summary>
        /// 激活场景（当同时存在多个场景时用于切换激活场景）。
        /// </summary>
        /// <param name="location">场景资源定位地址。</param>
        /// <returns>是否操作成功。</returns>
        public bool ActivateScene(string location)
        {
            if (_currentMainSceneName.Equals(location))
            {
                if (_currentMainScene != null)
                {
                    return _currentMainScene.ActivateScene();
                }

                return false;
            }

            _subScenes.TryGetValue(location, out SceneHandle subScene);
            if (subScene != null)
            {
                return subScene.ActivateScene();
            }

            Log.Warning($"IsMainScene invalid location:{location}");
            return false;
        }

        /// <summary>
        /// 解除场景加载挂起操作。
        /// </summary>
        /// <param name="location">场景资源定位地址。</param>
        /// <returns>是否操作成功。</returns>
        public bool UnSuspend(string location)
        {
            if (_currentMainSceneName.Equals(location))
            {
                if (_currentMainScene != null)
                {
                    return _currentMainScene.AllowSceneActivation();
                }

                return false;
            }

            _subScenes.TryGetValue(location, out SceneHandle subScene);
            if (subScene != null)
            {
                return subScene.AllowSceneActivation();
            }

            Log.Warning($"IsMainScene invalid location:{location}");
            return false;
        }

        /// <summary>
        /// 是否为主场景。
        /// </summary>
        /// <param name="location">场景资源定位地址。</param>
        /// <returns>是否主场景。</returns>
        public bool IsMainScene(string location)
        {
            // 获取当前激活的场景
            Scene currentScene = SceneManager.GetActiveScene();

            if (_currentMainSceneName.Equals(location))
            {
                if (_currentMainScene == null)
                {
                    return false;
                }

                // 判断当前场景是否是主场景
                if (currentScene.name == _currentMainScene.SceneName)
                {
                    return true;
                }

                return _currentMainScene.SceneName == currentScene.name;
            }

            // 判断当前场景是否是主场景
            if (currentScene.name == _currentMainScene?.SceneName)
            {
                return true;
            }

            Log.Warning($"IsMainScene invalid location:{location}");
            return false;
        }

        /// <summary>
        /// 异步卸载子场景。
        /// </summary>
        /// <param name="location">场景资源定位地址。</param>
        /// <param name="progressCallBack">进度回调。</param>
        public async UniTask<bool> UnloadAsync(string location, Action<float> progressCallBack = null)
        {
            _subScenes.TryGetValue(location, out SceneHandle subScene);
            if (subScene != null)
            {
                if (subScene.SceneObject == default)
                {
                    Log.Error($"Could not unload Scene while not loaded. Scene: {location}");
                    return false;
                }

                if (!_handlingScene.Add(location))
                {
                    Log.Warning($"Could not unload Scene while loading. Scene: {location}");
                    return false;
                }

                try
                {
                var unloadOperation = subScene.UnloadSceneAsync();

                if (progressCallBack != null)
                {
                    while (!unloadOperation.IsDone && unloadOperation.Status != EOperationStatus.Failed)
                    {
                        progressCallBack.Invoke(unloadOperation.Progress);
                        await UniTask.Yield();
                    }
                }
                else
                {
                    await unloadOperation.ToUniTask();
                }

                if (unloadOperation.Status != EOperationStatus.Succeeded)
                    throw new GameFrameworkException($"场景卸载失败：{unloadOperation.Error}");
                _subScenes.Remove(location);

                _handlingScene.Remove(location);

                return true;
                }
                finally { _handlingScene.Remove(location); }
            }

            Log.Warning($"UnloadAsync invalid location:{location}");
            return false;
        }

        /// <summary>
        /// 异步卸载子场景。
        /// </summary>
        /// <param name="location">场景资源定位地址。</param>
        /// <param name="callBack">卸载完成回调。</param>
        /// <param name="progressCallBack">进度回调。</param>
        public void Unload(string location, Action callBack = null, Action<float> progressCallBack = null)
        {
            UnloadWithCallback(location, callBack, progressCallBack).Forget();
        }

        /// <summary>
        /// 是否包含场景。
        /// </summary>
        /// <param name="location">场景资源定位地址。</param>
        /// <returns>是否包含场景。</returns>
        private async UniTask UnloadWithCallback(string location, Action callback, Action<float> progress)
        {
            if (await UnloadAsync(location, progress)) callback?.Invoke();
        }
        public bool IsContainScene(string location)
        {
            if (_currentMainSceneName.Equals(location))
            {
                return true;
            }

            return _subScenes.TryGetValue(location, out var _);
        }
    }
}
