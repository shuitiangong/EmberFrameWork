// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../LICENSE.txt.
using System;
using UnityEngine;

namespace EmberFrameWork
{
    /// <summary>默认输出到 Unity Console；Fatal 保留 TEngine 的抛异常语义。</summary>
    public sealed class DefaultLogHelper : GameFrameworkLog.ILogHelper
    {
        public void Log(GameFrameworkLogLevel level, object message)
        {
            // 每次调用独立格式化，避免共享 StringBuilder 的重入及线程竞争。
            string text = message?.ToString() ?? "null";
            string label;
            string color = null;
            switch (level)
            {
                case GameFrameworkLogLevel.Debug: label = "Debug"; color = "888888"; break;
                case GameFrameworkLogLevel.Info: label = "INFO"; color = "CFCFCF"; break;
                case GameFrameworkLogLevel.Warning: label = "WARNING"; color = "FF9400"; break;
                case GameFrameworkLogLevel.Error: label = "ERROR"; color = "FF0000"; break;
                case GameFrameworkLogLevel.Fatal: label = "FATAL"; color = "FF0000"; break;
                default: throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown log level.");
            }

            string output = Format(label, text, color);
            switch (level)
            {
                case GameFrameworkLogLevel.Debug:
                case GameFrameworkLogLevel.Info: UnityEngine.Debug.Log(output); break;
                case GameFrameworkLogLevel.Warning: UnityEngine.Debug.LogWarning(output); break;
                case GameFrameworkLogLevel.Error: UnityEngine.Debug.LogError(output); break;
                case GameFrameworkLogLevel.Fatal: throw new Exception(output, message as Exception);
            }
        }

        private static string Format(string label, string text, string color)
        {
#if UNITY_EDITOR
            return $"<color=#{color}>[{label}]</color> {text}";
#else
            return $"[{label}] {text}";
#endif
        }
    }
}
