// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See LICENSE.txt.
namespace EmberFrameWork
{
    /// <summary>框架模块。优先级在注册时读取，高优先级先更新、后关闭。</summary>
    public abstract class Module
    {
        public virtual int Priority => 0;
        public abstract void OnInit();
        public abstract void Shutdown();
    }
}
