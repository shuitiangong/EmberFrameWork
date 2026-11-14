using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace EmberFrameWork
{
    internal interface IResourceAssetBackend
    {
        IResourceAssetHandle Load(string location, Type type, bool synchronous);
        UniTask UnloadUnusedAsync(CancellationToken token);
    }

    internal interface IResourceAssetHandle
    {
        // 仅在成功完成后读取；失败时抛出带后端错误信息的异常。
        UnityEngine.Object Asset { get; }
        UniTask WaitAsync(CancellationToken token);
        // 幂等释放，允许关闭流程与请求 finally 同时尝试释放。
        void Release();
    }
}
