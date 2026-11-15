// Selected from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../Core/LICENSE.txt.
namespace EmberFrameWork
{
    /// <summary>通过默认资源包播放音频，须从 Unity 主线程调用。</summary>
    public interface IAudioModule
    {
        /// <summary>播放音频。通道占满时，最早播放的音频淡出后由新音频接替。</summary>
        AudioAgent Play(AudioType type, string path, bool bLoop = false, float volume = 1f);

        /// <summary>停止指定类别的音频并取消该类别待播放的音频。</summary>
        void Stop(AudioType type, bool fadeout);
    }
}
