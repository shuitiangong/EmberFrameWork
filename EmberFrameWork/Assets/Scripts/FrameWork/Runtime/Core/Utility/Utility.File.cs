// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../LICENSE.txt.
using System;

namespace EmberFrameWork
{
    public static partial class Utility
    {
        /// <summary>资源更新界面使用的文件大小显示工具。</summary>
        public static class File
        {
            /// <summary>按 1024 换算 Bytes、KB、MB、GB；用于下载大小和速度显示。</summary>
            public static string GetLengthString(long length)
            {
                if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
                if (length < 1024L) return $"{length} Bytes";
                if (length < 1024L * 1024) return $"{length / 1024d:F2} KB";
                if (length < 1024L * 1024 * 1024) return $"{length / (1024d * 1024):F2} MB";
                return $"{length / (1024d * 1024 * 1024):F2} GB";
            }
        }
    }
}
