// Adapted from TEngine's LubanTools (Copyright (c) 2022 ALEXTANG, MIT).
// See LICENSE.txt in this directory.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EmberFrameWork.Editor
{
    public static class LubanTools
    {
        private const string JsonMenu = "EmberFrameWork/Luban/导出 JSON &x";
        private const string BinMenu = "EmberFrameWork/Luban/导出二进制 &#x";
        private static Process _process;
        private static Task<string> _output;
        private static Task<string> _error;
        private static string _format;
        private static bool _reloadLocked;

        private static string LubanRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Luban"));

        [MenuItem(JsonMenu, priority = -100)]
        public static void GenerateJson() => Generate("Json");

        [MenuItem(BinMenu, priority = -99)]
        public static void GenerateBin() => Generate("Bin");

        private static void Generate(string format)
        {
            if (!CanGenerate())
            {
                Debug.LogWarning("[Luban] 请等待当前转表、编译或播放结束后重试。");
                return;
            }

            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                Debug.LogError("[Luban] 当前 Generate.ps1 使用 Windows tar.exe，仅支持 Windows 编辑器。");
                return;
            }

            string script = Path.Combine(LubanRoot, "Generate.ps1");
            if (!File.Exists(script))
            {
                Debug.LogError($"[Luban] 找不到转表脚本：{script}");
                return;
            }

            // Keep this callback alive while generated C# files are being imported.
            EditorApplication.LockReloadAssemblies();
            _reloadLocked = true;
            try
            {
                _format = format;
                _process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{script}\" -Format {format}",
                        WorkingDirectory = LubanRoot,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };
                _process.Start();
                // Drain both streams concurrently; Unity logging stays on the editor thread.
                _output = _process.StandardOutput.ReadToEndAsync();
                _error = _process.StandardError.ReadToEndAsync();
                EditorApplication.update += PollGeneration;
                Debug.Log($"[Luban] 开始导出 {format}：{script}");
            }
            catch (Exception exception)
            {
                ReleaseProcess();
                Debug.LogException(exception);
            }
        }

        [MenuItem(JsonMenu, validate = true)]
        [MenuItem(BinMenu, validate = true)]
        private static bool CanGenerate()
        {
            return _process == null && !EditorApplication.isCompiling &&
                   !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void PollGeneration()
        {
            try
            {
                if (!_process.HasExited || !_output.IsCompleted || !_error.IsCompleted)
                    return;

                int exitCode = _process.ExitCode;
                string output = _output.GetAwaiter().GetResult();
                string error = _error.GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(output))
                    Debug.Log($"[Luban]\n{output}");
                if (!string.IsNullOrWhiteSpace(error))
                {
                    if (exitCode == 0)
                        Debug.LogWarning($"[Luban]\n{error}");
                    else
                        Debug.LogError($"[Luban]\n{error}");
                }

                ReleaseProcess();
                // Also import any files written before a failed synchronization.
                AssetDatabase.Refresh();
                if (exitCode == 0)
                    Debug.Log($"[Luban] {_format} 导出完成，已刷新生成代码和配置数据。");
                else
                    Debug.LogError($"[Luban] {_format} 导出失败，退出码：{exitCode}。请检查上方日志。");
            }
            catch (Exception exception)
            {
                ReleaseProcess();
                Debug.LogException(exception);
            }
        }

        private static void ReleaseProcess()
        {
            EditorApplication.update -= PollGeneration;
            _process?.Dispose();
            _process = null;
            _output = null;
            _error = null;
            if (_reloadLocked)
            {
                _reloadLocked = false;
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        [MenuItem("EmberFrameWork/Luban/打开配置表目录", priority = -80)]
        private static void OpenTables()
        {
            EditorUtility.RevealInFinder(Path.Combine(LubanRoot, "Config/Data"));
        }

        [MenuItem("EmberFrameWork/Luban/打开工具目录", priority = -79)]
        private static void OpenTools()
        {
            EditorUtility.RevealInFinder(LubanRoot);
        }
    }
}
