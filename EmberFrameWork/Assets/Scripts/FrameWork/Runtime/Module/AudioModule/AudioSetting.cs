// Selected from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../Core/LICENSE.txt.
using UnityEngine;

namespace EmberFrameWork
{
    [CreateAssetMenu(menuName = "EmberFrameWork/AudioSetting", fileName = "AudioSetting")]
    public sealed class AudioSetting : ScriptableObject
    {
        public AudioGroupConfig[] audioGroupConfigs;
    }
}
