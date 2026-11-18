using Animancer;
using UnityEngine;
using UnityEngine.UI;

namespace EmberFrameWork.Demo
{
    [DisallowMultipleComponent]
    public sealed class AnimancerDemo : MonoBehaviour
    {
        [SerializeField] private AnimancerComponent _animancer;
        [SerializeField] private AnimationClip _idle;
        [SerializeField] private AnimationClip _glide;
        [SerializeField] private AnimationClip _hop;
        [SerializeField] private Text _playingLabel;
        [SerializeField] private Camera _stageCamera;

        private AnimationClip _selectedLoop;
        private float _framedAspect;

        private void OnEnable()
        {
            FrameStage();
            _selectedLoop = _idle;
            _animancer.Play(_idle);
            _playingLabel.text = _idle.name;
        }

        private void Update()
        {
            if (_stageCamera.aspect != _framedAspect)
                FrameStage();

            if (Input.GetKeyDown(KeyCode.Alpha1))
                PlayIdle();
            if (Input.GetKeyDown(KeyCode.Alpha2))
                PlayGlide();
            if (Input.GetKeyDown(KeyCode.Space))
                PlayHop();
        }

        public void PlayIdle() => SelectLoop(_idle);

        public void PlayGlide() => SelectLoop(_glide);

        public void PlayHop()
        {
            if (_animancer.States.Current.Clip == _hop)
                return;

            var state = _animancer.Play(_hop, AnimancerGraph.DefaultFadeDuration);
            state.Time = 0;
            state.Events(this).OnEnd = ResumeLoop;
            _playingLabel.text = _hop.name;
        }

        private void SelectLoop(AnimationClip clip)
        {
            _selectedLoop = clip;
            // A loop selected during Hop will start when Hop finishes.
            if (_animancer.States.Current.Clip != _hop)
                ResumeLoop();
        }

        private void ResumeLoop()
        {
            _animancer.Play(_selectedLoop, AnimancerGraph.DefaultFadeDuration);
            _playingLabel.text = _selectedLoop.name;
        }

        private void FrameStage()
        {
            _framedAspect = _stageCamera.aspect;
            const float minimumHalfHeight = 2.85f;
            const float minimumHalfWidth = 2.22f;
            _stageCamera.orthographicSize = Mathf.Max(minimumHalfHeight, minimumHalfWidth / _framedAspect);
        }
    }
}
