// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See LICENSE.txt.
namespace EmberFrameWork
{
    /// <summary>声明模块需要由框架轮询。</summary>
    public interface IUpdateModule
    {
        void Update(float elapseSeconds, float realElapseSeconds);
    }
}
