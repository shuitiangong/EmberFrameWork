using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace EmberFrameWork.Tests
{
    public class UIModuleTests
    {
        [Window(UILayer.UI, location: "test-window", hideTimeToClose: 1)]
        public sealed class TestWindow : UIWindow
        {
            public int Creates, Refreshes, Destroys, Events;
            protected override void OnCreate() { Creates++; }
            protected override void OnRefresh() { Refreshes++; }
            protected override void OnDestroy() { Destroys++; }
            protected override void RegisterEvent() { AddUIEvent(900001, () => Events++); }
        }

        private sealed class TestLoader : IUIResourceLoader
        {
            public int Loads;
            public GameObject LoadGameObject(string location, Transform parent = null, string packageName = "")
            {
                Assert.AreEqual("test-window", location);
                Loads++;
                var panel = new GameObject("TestPanel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                panel.transform.SetParent(parent, false);
                return panel;
            }
            public UniTask<GameObject> LoadGameObjectAsync(string location, Transform parent = null,
                CancellationToken cancellationToken = default, string packageName = "")
                => UniTask.FromResult(LoadGameObject(location, parent, packageName));
        }

        [UnityTest]
        public IEnumerator OriginalWindowLifecycleTimerAndEventBinding()
        {
            // 独立 PlayMode 测试，不与正在运行的业务 UI 混用。
            Assert.IsFalse(UIModule.IsValid);
            Assert.IsNull(GameObject.Find("UIRoot"));
            var root = new GameObject("UIRoot");
            var canvas = new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(root.transform, false);
            var camera = new GameObject("UICamera", typeof(Camera));
            camera.transform.SetParent(root.transform, false);
            var ui = UIModule.Instance;
            var loader = new TestLoader();
            UIModule.Resource = loader;
            try
            {
                Assert.AreSame(ui, GameModule.UI);
                TestWindow window = null;
                yield return ui.ShowUIAsyncAwait<TestWindow>("first").ToCoroutine(resultHandler: value => window = value);
                Assert.IsNotNull(window);
                Assert.AreEqual(1, window.Creates);
                Assert.AreEqual("first", window.UserData);
                Assert.AreEqual(typeof(TestWindow).FullName, ui.GetTopWindow());
                GameEvent.Send(900001);
                Assert.AreEqual(1, window.Events);

                ui.ShowUI<TestWindow>("second");
                Assert.AreEqual(1, loader.Loads);
                Assert.AreEqual(1, window.Creates);
                Assert.AreEqual(2, window.Refreshes);
                Assert.AreEqual("second", window.UserData);

                ui.HideUI<TestWindow>();
                Assert.IsTrue(window.IsHide);
                ui.ShowUI<TestWindow>();
                ModuleSystem.Update(2f, 2f);
                Assert.IsTrue(ui.HasWindow<TestWindow>(), "重新显示时应取消隐藏关闭计时器。");

                ui.HideUI<TestWindow>();
                ModuleSystem.Update(2f, 2f);
                Assert.IsFalse(ui.HasWindow<TestWindow>());
                Assert.AreEqual(1, window.Destroys);
                GameEvent.Send(900001);
                Assert.AreEqual(1, window.Events);
                yield return null;

                ui.ShowUI<TestWindow>();
                Assert.AreEqual(2, loader.Loads);
                ui.CloseAll();
                Assert.IsFalse(ui.HasWindow<TestWindow>());
            }
            finally
            {
                if (UIModule.IsValid) UIModule.Instance.Release();
                GameModule.Shutdown();
            }
            yield return null;
            Assert.IsTrue(root == null);
        }
    }
}
