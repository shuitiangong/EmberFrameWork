// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../LICENSE.txt.
using UnityEngine;

namespace EmberFrameWork
{
    public enum MemoryStrictCheckType : byte
    {
        AlwaysEnable, OnlyEnableWhenDevelopment, OnlyEnableInEditor, AlwaysDisable
    }

    /// <summary>可选的检查策略组件。无需挂载时，编辑器和开发构建默认启用严格检查。</summary>
    [DefaultExecutionOrder(-11000)]
    [DisallowMultipleComponent]
    public sealed class MemoryPoolSetting : MonoBehaviour
    {
        [SerializeField] private MemoryStrictCheckType m_EnableStrictCheck = MemoryStrictCheckType.OnlyEnableWhenDevelopment;
        public bool EnableStrictCheck
        {
            get => MemoryPool.EnableStrictCheck;
            set => MemoryPool.EnableStrictCheck = value;
        }
        private void Awake() => Apply();
        private void OnEnable() => Apply();
        public void Apply()
        {
            EnableStrictCheck = m_EnableStrictCheck == MemoryStrictCheckType.AlwaysEnable ||
                m_EnableStrictCheck == MemoryStrictCheckType.OnlyEnableWhenDevelopment && (Application.isEditor || Debug.isDebugBuild) ||
                m_EnableStrictCheck == MemoryStrictCheckType.OnlyEnableInEditor && Application.isEditor;
        }
    }
}
