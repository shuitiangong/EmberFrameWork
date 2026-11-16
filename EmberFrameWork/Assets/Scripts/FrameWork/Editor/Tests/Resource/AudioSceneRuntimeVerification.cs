using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace EmberFrameWork.Tests
{
    // 在已启动 Launch 的会话运行，scenePath 指向收集范围内的临时空场景。
    public static class AudioSceneRuntimeVerification
    {
        private const string Clip = "Assets/RawAssets/BattleDemo/Audios/music_background.wav";
        private static async UniTask Until(Func<bool> predicate)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (!predicate())
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "运行验证等待超时。");
                await UniTask.NextFrame();
            }
        }

        public static async Task VerifyAudio()
        {
            var audio = GameModule.Audio;
            audio.StopAll(false);
            var agent = audio.Play(AudioType.Music, Clip, true, 0.4f);
            Assert.IsNotNull(agent, "Editor 音频被禁用或未创建代理。");
            Assert.IsNotNull(agent.AudioResource().clip);
            Assert.IsTrue(agent.IsLoop);
            Assert.AreEqual(0.4f, agent.AudioResource().volume, 0.001f);
            agent.Pause();
            await UniTask.NextFrame();
            await UniTask.NextFrame();
            Assert.IsFalse(agent.IsFree, "暂停的声道不能被当作空闲声道复用。");
            agent.UnPause();
            agent.Stop(true);
            await Until(() => agent.IsFree);
            var asyncAgent = audio.Play(AudioType.Music, Clip, true, bAsync: true);
            await Until(() => asyncAgent.AudioData != null && !asyncAgent.IsFree);
            audio.MusicVolume = 0.5f;
            Assert.IsTrue(audio.AudioMixer.GetFloat("MusicVolume", out float db));
            Assert.AreEqual(Mathf.Log10(0.5f) * 20, db, 0.01f);
            audio.StopAll(false);
            audio.PutInAudioPool(new List<string> { Clip, Clip });
            await Until(() => audio.AudioClipPool.ContainsKey(Clip));
            var pooledHandle = audio.AudioClipPool[Clip];
            var pooled = audio.Play(AudioType.Music, Clip, true, bInPool: true);
            Assert.AreSame(pooledHandle, pooled.AudioData.AssetHandle);
            pooled.Stop(false);
            audio.Restart();
            await UniTask.NextFrame();
            Assert.IsFalse(pooledHandle.IsValid);
            Assert.AreEqual(0, audio.AudioClipPool.Count);
            var fresh = audio.Play(AudioType.Music, Clip, true, bInPool: true);
            Assert.IsTrue(audio.AudioClipPool.ContainsKey(Clip), "池键应使用调用方传入的定位地址。");
            Assert.AreSame(fresh.AudioData.AssetHandle, audio.AudioClipPool[Clip]);
            audio.StopAll(false);
        }

        public static async Task VerifyScene(string scenePath)
        {
            var scenes = GameModule.Scene;
            var main = SceneManager.GetActiveScene();
            int progress = 0;
            var loaded = await scenes.LoadSceneAsync(scenePath, LoadSceneMode.Additive, progressCallBack: value => progress++);
            Assert.IsTrue(loaded.isLoaded);
            Assert.IsTrue(scenes.IsContainScene(scenePath));
            Assert.IsTrue(scenes.ActivateScene(scenePath));
            Assert.AreEqual(loaded, SceneManager.GetActiveScene());
            SceneManager.SetActiveScene(main);
            Assert.IsTrue(await scenes.UnloadAsync(scenePath));
            Assert.IsFalse(scenes.IsContainScene(scenePath));

            var pending = scenes.LoadSceneAsync(scenePath, LoadSceneMode.Additive, suspendLoad: true).AsTask();
            await UniTask.Delay(150, ignoreTimeScale: true);
            Assert.IsFalse(pending.IsCompleted, "挂起的场景不能提前完成。");
            Assert.IsTrue(scenes.UnSuspend(scenePath));
            await Until(() => pending.IsCompleted);
            Assert.IsTrue((await pending).isLoaded);
            bool unloaded = false;
            scenes.Unload(scenePath, () => unloaded = true);
            await Until(() => unloaded);
            Assert.IsFalse(scenes.IsContainScene(scenePath));

            bool callback = false;
            scenes.LoadScene(scenePath, LoadSceneMode.Additive, callBack: scene => callback = scene.isLoaded);
            await Until(() => callback);
            Assert.IsTrue(await scenes.UnloadAsync(scenePath));
        }
    }
}
