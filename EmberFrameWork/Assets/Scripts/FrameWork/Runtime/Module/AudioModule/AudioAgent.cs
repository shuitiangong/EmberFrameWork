// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../Core/LICENSE.txt.
using UnityEngine;

namespace EmberFrameWork
{
    /// <summary>在一个音频通道上播放并持有当前片段。</summary>
    public sealed class AudioAgent
    {
        private const float FadeoutDuration = 0.2f;

        private readonly AudioSource _source;
        private readonly IResourceModule _resources;
        private AudioClip _clip;
        private PlaybackRequest _pending;
        private float _fadeoutTimer;
        private float _volume;
        private PlaybackState _state;

        private enum PlaybackState
        {
            End,
            Playing,
            FadingOut
        }

        private readonly struct PlaybackRequest
        {
            public readonly AudioClip Clip;
            public readonly bool Loop;
            public readonly float Volume;

            public PlaybackRequest(AudioClip clip, bool loop, float volume)
            {
                Clip = clip;
                Loop = loop;
                Volume = Mathf.Clamp01(volume);
            }
        }

        internal AudioAgent(AudioSource source, IResourceModule resources)
        {
            _source = source;
            _resources = resources;
        }

        internal bool IsFree => _state == PlaybackState.End;
        internal float Duration { get; private set; }

        public AudioSource AudioResource() => _source;

        internal void Play(AudioClip clip, bool loop, float volume)
        {
            var request = new PlaybackRequest(clip, loop, volume);
            if (IsFree)
            {
                StartPlayback(request);
                return;
            }

            ClearPending();
            _pending = request;
            BeginFadeOut();
        }

        internal void Stop(bool fadeout)
        {
            ClearPending();
            if (fadeout)
                BeginFadeOut();
            else if (!IsFree)
                StopPlayback();
        }

        internal void Update(float elapseSeconds)
        {
            switch (_state)
            {
                case PlaybackState.Playing:
                    if (!_source.isPlaying)
                        StopPlayback();
                    break;

                case PlaybackState.FadingOut:
                    _fadeoutTimer -= elapseSeconds;
                    _source.volume = _volume * Mathf.Clamp01(_fadeoutTimer / FadeoutDuration);
                    if (_fadeoutTimer <= 0f)
                    {
                        StopPlayback();
                        var pending = _pending;
                        _pending = default;
                        if (pending.Clip != null)
                            StartPlayback(pending);
                        return;
                    }
                    break;
            }

            if (!IsFree)
                Duration += elapseSeconds;
        }

        private void StartPlayback(PlaybackRequest request)
        {
            _clip = request.Clip;
            _volume = request.Volume;
            Duration = 0f;
            _source.clip = _clip;
            _source.loop = request.Loop;
            _source.volume = _volume;
            _source.Play();
            _state = PlaybackState.Playing;
        }

        private void BeginFadeOut()
        {
            if (_state != PlaybackState.Playing)
                return;

            _fadeoutTimer = FadeoutDuration;
            _state = PlaybackState.FadingOut;
        }

        private void StopPlayback()
        {
            if (_source != null)
            {
                _source.Stop();
                _source.clip = null;
            }

            _state = PlaybackState.End;
            _resources.UnloadAsset(_clip);
            _clip = null;
        }

        private void ClearPending()
        {
            if (_pending.Clip != null)
                _resources.UnloadAsset(_pending.Clip);

            _pending = default;
        }
    }
}
