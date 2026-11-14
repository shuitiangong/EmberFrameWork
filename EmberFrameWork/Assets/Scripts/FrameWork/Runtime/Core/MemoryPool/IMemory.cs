// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../LICENSE.txt.
namespace EmberFrameWork
{
    /// <summary>
    /// 内存对象Interface。
    /// </summary>
    public interface IMemory
    {
        /// <summary>
        /// 清理内存对象回收入池。
        /// </summary>
        void Clear();
    }
}
