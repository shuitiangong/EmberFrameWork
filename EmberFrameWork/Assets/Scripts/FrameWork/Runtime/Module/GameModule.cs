// Selected from TEngine GameModule (Copyright (c) 2022 ALEXTANG, MIT). See ../Core/LICENSE.txt.
namespace EmberFrameWork
{
    /// <summary>游戏模块访问入口；只迁入当前已选中且需要的入口。</summary>
    public class GameModule
    {
        /// <summary>获取计时器模块。</summary>
        public static ITimerModule Timer => _timer ??= Get<ITimerModule>();

        private static ITimerModule _timer;

        /// <summary>获取 UI 模块，保留 TEngine 原单例入口。</summary>
        public static UIModule UI => _ui ??= UIModule.Instance;

        private static UIModule _ui;

        private static T Get<T>() where T : class
        {
            T module = ModuleSystem.GetModule<T>();

            Log.Assert(condition: module != null, $"{typeof(T)} is null");

            return module;
        }

        /// <summary>与原版一致：仅清空入口缓存，模块关闭由 ModuleSystem.Shutdown 负责。</summary>
        public static void Shutdown()
        {
            Log.Info("GameModule Shutdown");

            _timer = null;
            _ui = null;
        }
    }
}
