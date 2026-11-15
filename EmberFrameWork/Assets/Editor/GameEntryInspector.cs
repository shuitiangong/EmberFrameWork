using EmberFrameWork;
using EmberFrameWork.Game;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameEntry))]
internal sealed class GameEntryInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying || Settings.Instance == null || Settings.ProcedureSetting == null) return;
        var procedure = Settings.ProcedureSetting.CurrentProcedure;
        var resource = ModuleSystem.GetModule<IResourceModule>();
        var initialization = procedure as Procedure.ProcedureInitResources;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("当前流程", procedure?.GetType().Name ?? "未启动");
        EditorGUILayout.LabelField("资源状态", resource.InitializationState.ToString());
        if (initialization != null && !string.IsNullOrEmpty(initialization.LastError))
            EditorGUILayout.HelpBox(initialization.LastError, MessageType.Error);
        using (new EditorGUI.DisabledScope(initialization == null || !initialization.CanRetry))
            if (GUILayout.Button("重试资源初始化")) initialization.Retry();
        Repaint();
    }
}
