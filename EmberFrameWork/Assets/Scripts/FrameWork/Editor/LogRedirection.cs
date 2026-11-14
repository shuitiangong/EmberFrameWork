// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../Runtime/Core/LICENSE.txt.
using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditorInternal;
using UnityEngine;

namespace EmberFrameWork.Editor
{
    /// <summary>双击框架日志时跳过日志包装层，定位到业务调用行。</summary>
    internal static class LogRedirection
    {
        private const string LogFolder = "Assets/Scripts/FrameWork/Runtime/Core/Log/";

        [OnOpenAsset(0)]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            if (line <= 0 || !AssetDatabase.GetAssetPath(instanceId).StartsWith(LogFolder, StringComparison.Ordinal))
                return false;

            // Unity 的私有 Console API 可能随版本变化；失效时交还默认打开行为。
            Type consoleType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
            object console = consoleType?.GetField("ms_ConsoleWindow", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            if (console == null || !ReferenceEquals(EditorWindow.focusedWindow, console)) return false;
            string trace = consoleType.GetField("m_ActiveText", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(console) as string;
            if (string.IsNullOrEmpty(trace)) return false;

            foreach (Match match in Regex.Matches(trace, @"\(at (.+):(\d+)\)"))
            {
                string path = match.Groups[1].Value.Replace('\\', '/');
                if (path.Contains(LogFolder) || !int.TryParse(match.Groups[2].Value, out int targetLine)) continue;
                if (!Path.IsPathRooted(path)) path = Path.Combine(Path.GetDirectoryName(Application.dataPath), path);
                if (!File.Exists(path)) continue;
                InternalEditorUtility.OpenFileAtLineExternal(path, targetLine);
                return true;
            }
            return false;
        }
    }
}
