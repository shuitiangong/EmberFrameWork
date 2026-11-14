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
            Debug.Log("[Startup] ProcedureStartGame：启动完成，等待游戏逻辑接入。");
        }
    }
}
