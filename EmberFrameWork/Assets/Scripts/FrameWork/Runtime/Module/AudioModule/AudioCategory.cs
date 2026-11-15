// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../Core/LICENSE.txt.
using UnityEngine;
using UnityEngine.Audio;

namespace EmberFrameWork
{
    /// <summary>管理一个 Mixer 分类下的播放通道。</summary>
    internal sealed class AudioCategory
    {
        private readonly AudioAgent[] _audioAgents;

        public AudioCategory(AudioMixer mixer, AudioGroupConfig config, Transform parent, IResourceModule resources)
        {
            var groups = mixer.FindMatchingGroups($"Master/{config.AudioType}");
            var mixerGroup = groups.Length > 0 ? groups[0] : mixer.FindMatchingGroups("Master")[0];

            var root = new GameObject($"Audio Category - {mixerGroup.name}").transform;
            root.SetParent(parent, false);
            _audioAgents = new AudioAgent[config.AgentHelperCount];

            for (int index = 0; index < _audioAgents.Length; index++)
            {
                var host = new GameObject($"Audio Agent Helper - {mixerGroup.name} - {index}");
                host.transform.SetParent(root, false);
                var source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;

                var channel = $"{mixerGroup.name} - {index}";
                var channelGroups = mixer.FindMatchingGroups($"Master/{mixerGroup.name}/{channel}");
                source.outputAudioMixerGroup = channelGroups.Length > 0 ? channelGroups[0] : mixerGroup;
                source.rolloffMode = config.audioRolloffMode;
                source.minDistance = config.minDistance;
                source.maxDistance = config.maxDistance;

                _audioAgents[index] = new AudioAgent(source, resources);
            }
        }

        public AudioAgent Play(AudioClip clip, bool loop, float volume)
        {
            var selected = _audioAgents[0];
            foreach (var agent in _audioAgents)
            {
                if (agent.IsFree)
                {
                    selected = agent;
                    break;
                }

                if (agent.Duration > selected.Duration)
                    selected = agent;
            }

            selected.Play(clip, loop, volume);
            return selected;
        }

        public void Stop(bool fadeout)
        {
            foreach (var agent in _audioAgents)
                agent.Stop(fadeout);
        }

        public void Update(float elapseSeconds)
        {
            foreach (var agent in _audioAgents)
                agent.Update(elapseSeconds);
        }
    }
}
