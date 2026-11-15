using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using EmberFrameWork;
using EmberFrameWork.Game.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class UIExampleTools
{
    private const string Menu = "EmberFrameWork/UI Example/";
    private static bool _validating;

    [MenuItem(Menu + "Open or Refresh")]
    public static void Open() => GameModule.UI.ShowUIAsync<UIExampleWindow>();

    [MenuItem(Menu + "Hide")]
    public static void Hide() => GameModule.UI.HideUI<UIExampleWindow>();

    [MenuItem(Menu + "Close")]
    public static void Close() => GameModule.UI.CloseUI<UIExampleWindow>();

    [MenuItem(Menu + "Open or Refresh", true)]
    [MenuItem(Menu + "Hide", true)]
    [MenuItem(Menu + "Close", true)]
    [MenuItem(Menu + "Validate Lifecycle", true)]
    private static bool CanUse() => Application.isPlaying && UIModule.IsValid && !_validating;

    [MenuItem(Menu + "Validate Lifecycle")]
    private static void ValidateMenu() => RunValidation().Forget();

    private static async UniTaskVoid RunValidation()
    {
        try { Debug.Log(await ValidateAsync()); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    public static async UniTask<string> ValidateAsync()
    {
        if (!CanUse()) throw new InvalidOperationException("请运行 Launch 场景并等待 UI 初始化完成。");
        _validating = true;
        var ui = GameModule.UI;
        var resources = ModuleSystem.GetModule<IResourceModule>();
        ObjectInfo[] CachedInfo() => ModuleSystem.GetModule<IObjectPoolModule>().GetAllObjectPools()
            .Where(p => p.Name == "Asset Pool").SelectMany(p => p.GetAllObjectInfos())
            .Where(a => a.Name == UIExampleWindow.AssetLocation).ToArray();
        try
        {
            ui.CloseUI<UIExampleWindow>();
            await UniTask.DelayFrame(2);
            await resources.UnloadUnusedAssetsAsync();
            var first = await ui.ShowUIAsyncAwait<UIExampleWindow>();
            Require(first != null && first.IsPrepare, "窗口加载");
            first.FindChildComponent<Button>("Panel/CounterButton").onClick.Invoke();
            Require(first.Counter == 1 && first.EventNotifications == 1, "按钮与事件派发");
            var repeated = await ui.ShowUIAsyncAwait<UIExampleWindow>();
            Require(ReferenceEquals(first, repeated) && first.RefreshCount == 2, "重复打开复用窗口");
            Require(CachedInfo().Length == 1 && CachedInfo()[0].SpawnCount == 1, "重复打开不增加资源借用");

            first.FindChildComponent<Button>("Panel/HideButton").onClick.Invoke();
            Require(first.IsHide && ui.HasWindow<UIExampleWindow>(), "隐藏保留窗口");
            await ui.ShowUIAsyncAwait<UIExampleWindow>();
            Require(!first.IsHide && first.HideTimerId == 0, "再次显示取消原隐藏计时器");

            var oldInstance = first.gameObject;
            first.FindChildComponent<Button>("Panel/CloseButton").onClick.Invoke();
            await UniTask.DelayFrame(2);
            Require(!ui.HasWindow<UIExampleWindow>() && oldInstance == null, "关闭并销毁实例");
            var second = await ui.ShowUIAsyncAwait<UIExampleWindow>();
            Require(!ReferenceEquals(first, second), "关闭后创建新窗口");
            second.FindChildComponent<Button>("Panel/CounterButton").onClick.Invoke();
            Require(second.EventNotifications == 1 && first.EventNotifications == 1, "旧窗口事件已解绑");

            // 缩短示例窗口本次隐藏时间，不修改框架的计时实现或全局时间。
            second.HideTimeToClose = 1;
            ui.HideUI<UIExampleWindow>();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (ui.HasWindow<UIExampleWindow>())
            {
                Require(Time.realtimeSinceStartup < deadline, "隐藏后自动关闭（请保持 timeScale > 0）");
                await UniTask.Yield();
            }
            await UniTask.DelayFrame(2);
            await resources.UnloadUnusedAssetsAsync();
            Require(CachedInfo().Length == 0, "窗口全部关闭后释放资源缓存");
            return "PASS UI example: open, refresh, click, hide/reopen, close, unsubscribe, timed close, resource release.";
        }
        finally
        {
            try
            {
                ui.CloseUI<UIExampleWindow>();
                await UniTask.DelayFrame(2);
                if (resources.IsReady) await resources.UnloadUnusedAssetsAsync();
            }
            finally { _validating = false; }
        }
    }

    private static void Require(bool condition, string stage)
    {
        if (!condition) throw new InvalidOperationException("UI 示例验证失败：" + stage);
    }
}
