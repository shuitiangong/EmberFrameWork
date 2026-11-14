using UnityEditor;
using UnityEngine;

namespace EmberFrameWork.Editor
{
    [CustomEditor(typeof(ResourceSetting))]
    internal sealed class ResourceSettingInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var setting = (ResourceSetting)target;
            bool webGL = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;
            if (!setting.TryValidate(true, webGL, out var editorError))
                EditorGUILayout.HelpBox("编辑器配置：" + editorError, MessageType.Error);
            if (!setting.TryValidate(false, webGL, out var playerError))
                EditorGUILayout.HelpBox("当前构建目标的发布配置：" + playerError, MessageType.Error);
        }
    }
}
