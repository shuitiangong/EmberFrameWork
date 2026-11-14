// Adapted from TEngine ResourceModuleDriver (Copyright (c) 2022 ALEXTANG, MIT).
// See ../../Core/LICENSE.txt.
using System;
using UnityEngine;
using YooAsset;

namespace EmberFrameWork
{
    /// <summary>资源配置数据。模块接收、返回配置时均复制，避免外部修改运行时配置。</summary>
    [Serializable]
    public sealed class ResourceOptions
    {
        [Tooltip("必须与 YooAsset 收集器中的包名一致。")]
        public string DefaultPackageName = "DefaultPackage";
        public EPlayMode EditorPlayMode = EPlayMode.EditorSimulateMode;
        public EPlayMode PlayMode = EPlayMode.OfflinePlayMode;

        [Tooltip("完整资源包根 URL，需包含部署所需的平台和目录。")]
        public string HostServerURL = "";
        [Tooltip("留空时使用主地址。")]
        public string FallbackHostServerURL = "";
        public bool UpdatableWhilePlaying;
        [Min(1)] public int DownloadingMaxNum = 10;
        [Min(0)] public int FailedTryAgain = 3;
        [Min(1)] public long Milliseconds = 30;

        [Min(0)] public float AssetAutoReleaseInterval = 60f;
        [Min(0)] public int AssetCapacity = 64;
        [Min(0)] public float AssetExpireTime = 60f;
        public int AssetPriority;

        public ResourceOptions Clone() => (ResourceOptions)MemberwiseClone();

        /// <summary>验证配置并生成规范化副本；不会修改原对象或初始化 YooAsset。</summary>
        public ResourceOptions CreateValidatedCopy(bool isEditor, bool isWebGL)
        {
            var copy = Clone();
            if (string.IsNullOrWhiteSpace(copy.DefaultPackageName) || copy.DefaultPackageName != copy.DefaultPackageName.Trim() ||
                copy.DefaultPackageName.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) >= 0 ||
                copy.DefaultPackageName == "." || copy.DefaultPackageName == "..")
                throw new ArgumentException("默认资源包名称不能为空、包含路径字符或首尾空格。");
            foreach (char c in copy.DefaultPackageName) if (char.IsControl(c)) throw new ArgumentException("资源包名称不能包含控制字符。");
            ValidateMode(copy.EditorPlayMode, true);
            ValidateMode(copy.PlayMode, false);
            if (copy.DownloadingMaxNum <= 0 || copy.FailedTryAgain < 0 || copy.Milliseconds <= 0 || copy.AssetCapacity < 0)
                throw new ArgumentException("下载并发数和时间片必须大于零，重试次数和缓存容量不能为负数。");
            ValidateSeconds(copy.AssetAutoReleaseInterval, nameof(AssetAutoReleaseInterval));
            ValidateSeconds(copy.AssetExpireTime, nameof(AssetExpireTime));

            var mode = isEditor ? copy.EditorPlayMode : copy.PlayMode;
            if (!isEditor && isWebGL && mode != EPlayMode.WebPlayMode)
                throw new ArgumentException("WebGL 发布配置必须使用 WebPlayMode。");
            if (!isEditor && !isWebGL && mode == EPlayMode.WebPlayMode)
                throw new ArgumentException("非 WebGL 发布配置不能使用 WebPlayMode。");
            copy.HostServerURL = NormalizeUrl(copy.HostServerURL);
            copy.FallbackHostServerURL = NormalizeUrl(copy.FallbackHostServerURL);
            if ((mode == EPlayMode.HostPlayMode || mode == EPlayMode.WebPlayMode) && copy.HostServerURL.Length == 0)
                throw new ArgumentException("联机或 Web 模式必须配置资源主地址。");
            if (copy.FallbackHostServerURL.Length == 0) copy.FallbackHostServerURL = copy.HostServerURL;
            return copy;
        }

        private static void ValidateMode(EPlayMode mode, bool editor)
        {
            if (mode == EPlayMode.OfflinePlayMode || mode == EPlayMode.HostPlayMode || mode == EPlayMode.WebPlayMode ||
                editor && mode == EPlayMode.EditorSimulateMode) return;
            throw new ArgumentException("运行模式无效；发布配置不能使用编辑器模拟模式，当前不支持自定义文件系统模式。");
        }

        private static void ValidateSeconds(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0) throw new ArgumentOutOfRangeException(name);
        }

        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            url = url.Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                uri.Host.Length == 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
                throw new ArgumentException("资源地址必须是无查询参数、片段和凭据的 HTTP(S) 目录地址。");
            return url.TrimEnd('/');
        }
    }
}
