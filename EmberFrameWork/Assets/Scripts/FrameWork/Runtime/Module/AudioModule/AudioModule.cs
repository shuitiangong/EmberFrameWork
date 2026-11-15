// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../Core/LICENSE.txt.
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

namespace EmberFrameWork
{
    [Preserve]
    internal sealed class AudioModule : Module, IAudioModule, IUpdateModule
    {
        private IResourceModule _resourceModule;
        private GameObject _instanceRoot;
        private AudioCategory[] _audioCategories;
        private bool _shutdown;
#if UNITY_EDITOR
        private bool _unityAudioDisabled;
#endif

        public override void OnInit()
        {
            _resourceModule = ModuleSystem.GetModule<IResourceModule>();
#if UNITY_EDITOR
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            _unityAudioDisabled = typeof(AudioSettings).GetProperty("unityAudioDisabled", flags)?.GetValue(null) is true;
#endif
        }

        public override void Shutdown()
        {
            _shutdown = true;
            if (_audioCategories == null)
                return;

            foreach (var category in _audioCategories)
                category.Stop(fadeout: false);

            Object.Destroy(_instanceRoot);
            _instanceRoot = null;
            _audioCategories = null;
        }

        public AudioAgent Play(AudioType type, string path, bool bLoop = false, float volume = 1f)
        {
            if (_shutdown)
                throw new ObjectDisposedException(nameof(AudioModule));
            ValidateAudioType(type);
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("音频定位地址不能为空。", nameof(path));
#if UNITY_EDITOR
            if (_unityAudioDisabled)
                return null;
#endif
            if (!_resourceModule.IsReady)
                throw new InvalidOperationException("默认资源包尚未就绪，无法播放音频。");

            if (_audioCategories == null)
                Initialize();

            var clip = _resourceModule.LoadAsset<AudioClip>(path);
            if (clip == null)
                throw new GameFrameworkException($"无法加载音频片段 '{path}'。");

            return _audioCategories[(int)type].Play(clip, bLoop, volume);
        }

        public void Stop(AudioType type, bool fadeout)
        {
            ValidateAudioType(type);
            _audioCategories?[(int)type].Stop(fadeout);
        }

        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (_audioCategories == null)
                return;

            foreach (var category in _audioCategories)
                category.Update(elapseSeconds);
        }

        private void Initialize()
        {
            var setting = Resources.Load<AudioSetting>("AudioSetting");
            var mixer = Resources.Load<AudioMixer>("AudioMixer");
            if (setting == null || setting.audioGroupConfigs == null || mixer == null)
                throw new GameFrameworkException("AudioSetting 或 AudioMixer 缺失。");

            var root = new GameObject("[AudioModule Instances]");
            try
            {
                var categories = new AudioCategory[(int)AudioType.Max];
                for (int index = 0; index < categories.Length; index++)
                {
                    var type = (AudioType)index;
                    var config = setting.audioGroupConfigs.FirstOrDefault(group => group != null && group.AudioType == type);
                    if (config == null || config.AgentHelperCount <= 0)
                        throw new GameFrameworkException($"音频组 '{type}' 缺少有效的通道配置。");

                    categories[index] = new AudioCategory(mixer, config, root.transform, _resourceModule);
                }

                Object.DontDestroyOnLoad(root);
                _instanceRoot = root;
                _audioCategories = categories;
            }
            catch
            {
                Object.Destroy(root);
                throw;
            }
        }

        private static void ValidateAudioType(AudioType type)
        {
            if ((uint)type >= (uint)AudioType.Max)
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }
}
