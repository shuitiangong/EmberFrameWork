using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using EmberFrameWork;
using UnityEngine.Scripting;

namespace Procedure
{
    /// <summary>等待默认资源包就绪。失败停留在本流程，通过 Retry 显式重试。</summary>
    [Preserve]
    public sealed class ProcedureInitResources : ProcedureBase
    {
        private IResourceModule _resource;
        private Task<string> _pending;
        private bool _active;
        public string LastError { get; private set; }
        public bool IsLoading => _active && _pending != null;
        public bool CanRetry => _active && _pending == null && LastError != null;
        [Preserve] public ProcedureInitResources() { }

        protected override void OnEnter(IFsm<IProcedureModule> owner)
        {
            _active = true;
            _resource = ModuleSystem.GetModule<IResourceModule>();
            StartAttempt();
        }

        private void StartAttempt()
        {
            LastError = null;
            try
            {
                _pending = _resource.InitPackage().AsTask();
                Log.Info("[Startup] 正在初始化资源包 {0}。", _resource.DefaultPackageName);
            }
            catch (Exception exception) { SetFailure(exception); }
        }

        public bool Retry()
        {
            if (!CanRetry) return false;
            StartAttempt();
            return true;
        }

        protected override void OnUpdate(IFsm<IProcedureModule> owner, float elapseSeconds, float realElapseSeconds)
        {
            if (!_active || _pending == null || !_pending.IsCompleted) return;
            var completed = _pending;
            _pending = null;
            try
            {
                string version = completed.GetAwaiter().GetResult();
                if (!_resource.IsReady) throw new InvalidOperationException("初始化结束，但资源包尚未就绪。");
                Log.Info("[Startup] 资源包就绪，版本 {0}。", version);
            }
            catch (Exception exception) { SetFailure(exception); return; }
            // 状态切换只发生在流程 Update，异步完成回调不会直接修改 FSM。
            ChangeState<ProcedureStartGame>(owner);
        }

        private void SetFailure(Exception exception)
        {
            _pending = null;
            LastError = _resource?.LastError ?? exception.Message;
            Log.Error("[Startup] 资源初始化失败：{0}。修复包体或收集配置后可重试；修改运行模式等参数后需重新启动。", LastError);
        }

        protected override void OnLeave(IFsm<IProcedureModule> owner, bool isShutdown)
        {
            _active = false;
            if (_pending != null)
            {
                // 离开后只观察潜在异常，不再推进流程。框架关闭时由资源模块取消底层任务。
                _pending.ContinueWith(task => { var ignored = task.Exception; }, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                _pending = null;
            }
            _resource = null;
        }
    }
}
