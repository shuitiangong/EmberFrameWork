// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT).
// See Runtime/Module/ProcedureModule/LICENSE.txt.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace EmberFrameWork.Editor
{
    [CustomEditor(typeof(ProcedureSetting))]
    internal sealed class ProcedureSettingInspector : UnityEditor.Editor
    {
        private SerializedProperty _available;
        private SerializedProperty _entrance;
        private Type[] _types;

        private void OnEnable()
        {
            _available = serializedObject.FindProperty("availableProcedureTypeNames");
            _entrance = serializedObject.FindProperty("entranceProcedureTypeName");
            var runtimeAssemblies = new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.Player).Select(a => a.name));
            _types = TypeCache.GetTypesDerivedFrom<ProcedureBase>()
                .Where(type => ProcedureSetting.IsProcedureType(type) && runtimeAssemblies.Contains(type.Assembly.GetName().Name))
                .OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var selected = new List<string>();
            for (int i = 0; i < _available.arraySize; i++) selected.Add(_available.GetArrayElementAtIndex(i).stringValue);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.LabelField("可用流程", EditorStyles.boldLabel);
                if (_types.Length == 0)
                    EditorGUILayout.HelpBox("尚无可用的运行时流程。创建继承 ProcedureBase 的具体流程后，可在此选择。", MessageType.Info);
                foreach (var type in _types)
                {
                    string name = type.AssemblyQualifiedName;
                    string previous = selected.FirstOrDefault(value => Matches(value, type));
                    bool enabled = previous != null;
                    bool next = EditorGUILayout.ToggleLeft(type.FullName, enabled);
                    if (next == enabled) continue;
                    if (next) selected.Add(name);
                    else
                    {
                        selected.RemoveAll(value => Matches(value, type));
                        if (Matches(_entrance.stringValue, type)) _entrance.stringValue = string.Empty;
                    }
                }

                // Preserve unresolved selections until the user explicitly removes them.
                foreach (var name in selected.ToArray())
                {
                    if (_types.Any(type => Matches(name, type))) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("不可用：" + name);
                        if (GUILayout.Button("移除", GUILayout.Width(50)))
                        {
                            selected.Remove(name);
                            if (_entrance.stringValue == name) _entrance.stringValue = string.Empty;
                        }
                    }
                }

                var choices = new List<string> { "请选择入口流程" };
                choices.AddRange(selected.Select(name => name.Split(',')[0]));
                int current = selected.FindIndex(name => name == _entrance.stringValue);
                int choice = EditorGUILayout.Popup("入口流程", current + 1, choices.ToArray());
                if (choice != current + 1) _entrance.stringValue = choice == 0 ? string.Empty : selected[choice - 1];
                _available.arraySize = selected.Count;
                for (int i = 0; i < selected.Count; i++) _available.GetArrayElementAtIndex(i).stringValue = selected[i];
            }
            serializedObject.ApplyModifiedProperties();
            var setting = (ProcedureSetting)target;
            if (!setting.TryValidate(out var error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("当前流程", setting.CurrentProcedure?.GetType().FullName ?? "未运行");
                EditorGUILayout.LabelField("持续时间", setting.CurrentProcedureTime.ToString("F2"));
            }
        }

        private static bool Matches(string name, Type type) => name == type.FullName || name == type.AssemblyQualifiedName;
    }
}
