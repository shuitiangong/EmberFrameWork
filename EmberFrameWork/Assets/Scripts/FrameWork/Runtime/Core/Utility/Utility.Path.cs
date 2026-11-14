// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../LICENSE.txt.
namespace EmberFrameWork
{
    public static partial class Utility
    {
        public static class Path
        {
            /// <summary>统一资源构建路径的分隔符；null 保持为 null。</summary>
            public static string GetRegularPath(string path) => path?.Replace('\\', '/');
        }
    }
}
