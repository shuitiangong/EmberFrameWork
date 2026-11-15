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
            public int Sorts;
            public bool Injected, BoundAfterInjection, SortedBeforeRefresh;
            protected override void ScriptGenerator() { BoundAfterInjection = Injected; }
            protected override void OnSortDepth()
            {
                Sorts++;
                SortedBeforeRefresh |= Refreshes == 0;
            }
            protected override void OnCreate() { Creates++; }
            protected override void OnRefresh() { Refreshes++; }
            protected override void OnDestroy() { Destroys++; }
            protected override void RegisterEvent() { AddUIEvent(900001, () => Events++); }
        }

        public sealed class TestWidget : UIWidget
        {
            public bool Injected, BoundAfterInjection;
            public int Sorts;
            protected override void ScriptGenerator() { BoundAfterInjection = Injected; }
            protected override void OnSortDepth() { Sorts++; }
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
                foreach (int order in new[] { 7, 23 })
                {
                    var child = new GameObject("Child" + order, typeof(RectTransform), typeof(Canvas));
                    child.transform.SetParent(panel.transform, false);
                    child.GetComponent<Canvas>().overrideSorting = true;
                    child.GetComponent<Canvas>().sortingOrder = order;
                }
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
            var previousInjector = UIBase.Injector;
            try
            {
                UIBase.Injector = view =>
                {
                    if (view is TestWindow testWindow) testWindow.Injected = true;
                    if (view is TestWidget testWidget) testWidget.Injected = true;
                };
                Assert.AreSame(ui, GameModule.UI);
                TestWindow window = null;
                yield return ui.ShowUIAsyncAwait<TestWindow>("first").ToCoroutine(resultHandler: value => window = value);
                Assert.IsNotNull(window);
                Assert.AreEqual(1, window.Creates);
                Assert.IsTrue(window.BoundAfterInjection);
                Assert.IsFalse(window.SortedBeforeRefresh);
                Assert.Greater(window.Sorts, 0);
                var child7 = window.transform.Find("Child7").GetComponent<Canvas>();
                var child23 = window.transform.Find("Child23").GetComponent<Canvas>();
                Assert.AreEqual(window.Depth + 7, child7.sortingOrder);
                Assert.AreEqual(window.Depth + 23, child23.sortingOrder);

                var widgetRoot = new GameObject("Widget", typeof(RectTransform));
                widgetRoot.transform.SetParent(window.transform, false);
                var widget = new TestWidget();
                Assert.IsTrue(widget.Create(window, widgetRoot));
                Assert.IsTrue(widget.BoundAfterInjection);
                window.Depth += 100;
                Assert.AreEqual(window.Depth + 7, child7.sortingOrder);
                Assert.AreEqual(window.Depth + 23, child23.sortingOrder);
                Assert.AreEqual(1, widget.Sorts);

                window.Visible = false;
                int sorts = window.Sorts;
                window.Depth += 100;
                Assert.AreEqual(sorts, window.Sorts, "隐藏时暂缓排序回调。");
                window.Visible = true;
                Assert.AreEqual(sorts + 1, window.Sorts);
                Assert.AreEqual(2, widget.Sorts);
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
                ModuleSystem.Update(0.6f, 0.6f);
                ui.HideUI<TestWindow>();
                ModuleSystem.Update(0.6f, 0.6f);
                Assert.IsTrue(ui.HasWindow<TestWindow>(), "重复隐藏应取消旧计时器并重新计时。");
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
                UIBase.Injector = previousInjector;
                if (UIModule.IsValid) UIModule.Instance.Release();
                GameModule.Shutdown();
            }
            yield return null;
            Assert.IsTrue(root == null);
        }

        [Test]
        public void SafeFitHelperCompensatesAgainstSelectedRect()
        {
            var fit = new GameObject("Fit", typeof(RectTransform));
            var child = new GameObject("Child", typeof(RectTransform));
            try
            {
                var fitRect = (RectTransform)fit.transform;
                var childRect = (RectTransform)child.transform;
                childRect.SetParent(fitRect, false);
                fitRect.offsetMax = new Vector2(0, -40);
                childRect.localPosition = new Vector3(3, 10, 5);
                var helper = new SetUISafeFitHelper(fitRect);
                helper.SetUINotFit(childRect);
                Assert.AreEqual(new Vector3(3, 50, 5), childRect.localPosition);

                childRect.localPosition = new Vector3(3, 10, 5);
                new TestWindow().SetUINotFit(childRect, fitRect);
                Assert.AreEqual(new Vector3(3, 50, 5), childRect.localPosition);

                new TestWindow().SetUIFit(fitRect, liuHaiFit: false, bottomFit: false);
                Assert.AreEqual(Vector2.zero, fitRect.offsetMax);
                Assert.AreEqual(Vector2.zero, fitRect.offsetMin);
            }
            finally
            {
                Object.DestroyImmediate(child);
                Object.DestroyImmediate(fit);
            }
        }
    }
}
