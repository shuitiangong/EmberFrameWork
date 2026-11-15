// Adapted from TEngine's ProcedureStartGame (Copyright (c) 2022 ALEXTANG, MIT).
// See ../FrameWork/Runtime/Core/LICENSE.txt.
using Cysharp.Threading.Tasks;
using EmberFrameWork;
using UnityEngine.Scripting;

namespace Procedure
{
    /// <summary>资源就绪后启动战斗 Demo。</summary>
    [Preserve]
    public sealed class ProcedureStartGame : ProcedureBase
    {
        [Preserve] public ProcedureStartGame() { }

        protected override void OnEnter(IFsm<IProcedureModule> procedureOwner)
        {
            if (!ModuleSystem.GetModule<IResourceModule>().IsReady)
                throw new System.InvalidOperationException("资源包尚未就绪，不能进入游戏流程。");
            ModuleSystem.GetModule<IUpdateDriver>().AddDestroyListener(SingletonSystem.Release);
            UIModule.Instance.Active();
            BattleSystem.Instance.LoadBattleAsync().Forget();
        }
    }
}
