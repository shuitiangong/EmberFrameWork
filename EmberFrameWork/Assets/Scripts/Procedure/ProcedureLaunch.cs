// Adapted from TEngine's ProcedureLaunch (Copyright (c) 2022 ALEXTANG, MIT).
// See ../FrameWork/Runtime/Core/LICENSE.txt.
using EmberFrameWork;
using UnityEngine;
using UnityEngine.Scripting;

namespace Procedure
{
    /// <summary>基础启动流程。后续业务模块的初始化可在此接入。</summary>
    [Preserve]
    public sealed class ProcedureLaunch : ProcedureBase
    {
        [Preserve] public ProcedureLaunch() { }

        protected override void OnEnter(IFsm<IProcedureModule> procedureOwner)
        {
            Debug.Log("[Startup] ProcedureLaunch：基础模块初始化完成。");
        }

        protected override void OnUpdate(IFsm<IProcedureModule> procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            ChangeState<ProcedureStartGame>(procedureOwner);
        }
    }
}
