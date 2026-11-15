// Adapted from TEngine GameEntry (Copyright (c) 2022 ALEXTANG, MIT).
// See FrameWork/Runtime/Core/LICENSE.txt.
using EmberFrameWork;
using UnityEngine;

namespace EmberFrameWork.Game
{
    public class GameEntry : MonoBehaviour
    {
        // 阶段性连接：ResourceModuleDriver 尚未恢复，暂时沿用已绑定的资源配置。
        [SerializeField] private ResourceSetting resourceSetting;

        private void Awake()
        {
            var resource = ModuleSystem.GetModule<IResourceModule>();
            resource.Configure(resourceSetting);
            // 原版 IDebuggerModule 尚未迁移，本步不引入调试模块。
            ModuleSystem.GetModule<IFsmModule>();
            Settings.ProcedureSetting.StartProcedure().Forget();
            DontDestroyOnLoad(this);
        }
    }
}
