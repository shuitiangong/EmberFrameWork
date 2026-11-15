// Selected from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../Core/LICENSE.txt.
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace EmberFrameWork
{
    /// <summary>主场景加载模块，须从 Unity 主线程调用。</summary>
    public interface ISceneModule
    {
        /// <summary>从已就绪的默认资源包异步切换主场景。再次加载同一地址会重新加载场景。</summary>
        /// <param name="location">YooAsset 清单中的场景资源定位地址。</param>
        /// <exception cref="System.ArgumentException">定位地址为空或指向非场景资源。</exception>
        /// <exception cref="System.InvalidOperationException">资源包未就绪、已有主场景正在加载或加载失败。</exception>
        UniTask<Scene> LoadSceneAsync(string location);
    }
}
