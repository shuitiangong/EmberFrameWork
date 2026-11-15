// Adapted from TEngine GameEntry (MIT). See FrameWork/Runtime/Core/LICENSE.txt.
using EmberFrameWork;
using UnityEngine;
namespace EmberFrameWork.Game
{
    [RequireComponent(typeof(ResourceModuleDriver))]
    public class GameEntry : MonoBehaviour
    {
        private void Awake()
        {
            ModuleSystem.GetModule<IUpdateDriver>();
            ModuleSystem.GetModule<IFsmModule>();
            DontDestroyOnLoad(gameObject);
        }
        private void Start()
        {
            // 显式保证初始化先于流程启动，不依赖同帧各组件 Start 的排列。
            GetComponent<ResourceModuleDriver>().Initialize();
            Settings.ProcedureSetting.StartProcedure().Forget();
        }
        private void OnDestroy()
        {
            try { SingletonSystem.Release(); }
            finally
            {
                GameModule.Shutdown();
                ModuleSystem.Shutdown();
            }
        }
    }
}
