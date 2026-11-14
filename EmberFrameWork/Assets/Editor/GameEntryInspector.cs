using EmberFrameWork.Game;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameEntry))]
internal sealed class GameEntryInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!Application.isPlaying) return;
        var entry = (GameEntry)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("当前流程", entry.CurrentProcedure?.GetType().Name ?? "未启动");
        EditorGUILayout.LabelField("资源状态", entry.ResourceState.ToString());
        EditorGUILayout.LabelField("游戏就绪", entry.IsGameReady ? "是" : "否");
        if (!string.IsNullOrEmpty(entry.StartupError)) EditorGUILayout.HelpBox(entry.StartupError, MessageType.Error);
        using (new EditorGUI.DisabledScope(!entry.CanRetryResourceInitialization))
            if (GUILayout.Button("重试资源初始化")) entry.RetryResourceInitialization();
        Repaint();
    }
}
