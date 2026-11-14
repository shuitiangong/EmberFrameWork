using UnityEngine;

namespace EmberFrameWork
{
    /// <summary>专用于公共工具协程，避免停止 GameEntry 上其他业务协程。</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class UtilityCoroutineRunner : MonoBehaviour { }
}
