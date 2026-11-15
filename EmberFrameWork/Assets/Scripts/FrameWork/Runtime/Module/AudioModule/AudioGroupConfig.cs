// Selected from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../Core/LICENSE.txt.
using System;
using UnityEngine;

namespace EmberFrameWork
{
    [Serializable]
    public sealed class AudioGroupConfig
    {
        [SerializeField, Min(1)]
        private int agentHelperCount = 1;

        public AudioType AudioType;
        public AudioRolloffMode audioRolloffMode = AudioRolloffMode.Logarithmic;
        public float minDistance = 1f;
        public float maxDistance = 500f;

        public int AgentHelperCount => agentHelperCount;
    }
}
