// Adapted from TEngine main 3ffab9c6 (MIT). See ../../Core/LICENSE.txt.
using UnityEngine;

namespace EmberFrameWork
{
    [CreateAssetMenu(menuName = "EmberFrameWork/AudioSetting", fileName = "AudioSetting")]
    public class AudioSetting : ScriptableObject
    {
        public AudioGroupConfig[] audioGroupConfigs = null;
    }
}
