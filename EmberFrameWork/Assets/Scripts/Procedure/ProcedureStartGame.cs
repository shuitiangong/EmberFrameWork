// Adapted from TEngine's ProcedureStartGame (Copyright (c) 2022 ALEXTANG, MIT).
// See ../FrameWork/Runtime/Core/LICENSE.txt.
using EmberFrameWork;
using UnityEngine;
using UnityEngine.Scripting;

namespace Procedure
{
    /// <summary>基础启动完成，后续游戏逻辑和场景入口在此接入。</summary>
    [Preserve]
    public sealed class ProcedureStartGame : ProcedureBase
    {
        [Preserve] public ProcedureStartGame() { }

        protected override void OnEnter(IFsm<IProcedureModule> procedureOwner)
        {
            if (!ModuleSystem.GetModule<IResourceModule>().IsReady)
                throw new System.InvalidOperationException("资源包尚未就绪，不能进入游戏流程。");
            // 对应 TEngine GameApp.Entrance/Release 的 UI 单例连接；当前项目没有热更新 GameApp。
            ModuleSystem.GetModule<IUpdateDriver>().AddDestroyListener(ReleaseUI);
            UIModule.Instance.Active();
            // 业务示例入口；后续接入正式主界面时替换此调用。
            GameModule.UI.ShowUIAsync<EmberFrameWork.Game.UI.UIExampleWindow>();
            Log.Info("[Startup] ProcedureStartGame：启动完成，等待游戏逻辑接入。");
        }

        private static void ReleaseUI()
        {
            SingletonSystem.Release();
        }
    }
}
