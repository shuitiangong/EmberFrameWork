using EmberFrameWork;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace EmberFrameWork.Game.UI
{
    /// <summary>业务示例：按 TEngine 的窗口生命周期绑定控件和事件。</summary>
    [Preserve]
    [Window(UILayer.UI, location: AssetLocation, hideTimeToClose: 10)]
    public sealed class UIExampleWindow : UIWindow
    {
        public const string AssetLocation = "Assets/RawAssets/UI/UIExampleWindow.prefab";
        private static readonly int CounterChanged = RuntimeId.ToRuntimeId("UIExampleWindow.CounterChanged");
        private Text _status;
        public int Counter { get; private set; }
        public int RefreshCount { get; private set; }
        public int EventNotifications { get; private set; }
        [Preserve] public UIExampleWindow() { }

        protected override void ScriptGenerator()
        {
            _status = FindChildComponent<Text>("Panel/Status");
            FindChildComponent<Button>("Panel/CounterButton").onClick.AddListener(OnCounterClick);
            FindChildComponent<Button>("Panel/HideButton").onClick.AddListener(Hide);
            FindChildComponent<Button>("Panel/CloseButton").onClick.AddListener(Close);
        }

        protected override void RegisterEvent()
        {
            AddUIEvent<int>(CounterChanged, OnCounterChanged);
        }

        protected override void OnRefresh()
        {
            RefreshCount++;
            RefreshStatus();
        }

        private void OnCounterClick() => GameEvent.Send(CounterChanged, Counter + 1);

        private void OnCounterChanged(int value)
        {
            Counter = value;
            EventNotifications++;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            _status.text = $"Count: {Counter}\nRefresh: {RefreshCount}";
        }
    }
}
