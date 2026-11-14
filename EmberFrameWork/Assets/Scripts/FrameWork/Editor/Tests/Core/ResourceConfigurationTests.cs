using System;
using NUnit.Framework;
using YooAsset;

namespace EmberFrameWork.Tests
{
    public class ResourceConfigurationTests
    {
        [SetUp] public void SetUp() => ModuleSystem.Shutdown();
        [TearDown] public void TearDown() => ModuleSystem.Shutdown();

        [Test] public void DefaultsWorkForEditorAndNativePlayer()
        {
            var options = new ResourceOptions();
            var editor = options.CreateValidatedCopy(true, false);
            var player = options.CreateValidatedCopy(false, false);
            Assert.AreEqual(EPlayMode.EditorSimulateMode, editor.EditorPlayMode);
            Assert.AreEqual(EPlayMode.OfflinePlayMode, player.PlayMode);
            Assert.AreEqual("DefaultPackage", player.DefaultPackageName);
        }

        [Test] public void HostModeRequiresUrlAndNormalizesFallback()
        {
            var options = new ResourceOptions { PlayMode = EPlayMode.HostPlayMode };
            Assert.Throws<ArgumentException>(() => options.CreateValidatedCopy(false, false));
            options.HostServerURL = "https://cdn.example/game/Windows64/";
            var copy = options.CreateValidatedCopy(false, false);
            Assert.AreEqual("https://cdn.example/game/Windows64", copy.HostServerURL);
            Assert.AreEqual(copy.HostServerURL, copy.FallbackHostServerURL);
            Assert.AreEqual("", options.FallbackHostServerURL);
        }

        [Test] public void RejectsInvalidPackageNamesAndModes()
        {
            foreach (string name in new[] { "", " ", "../data", "bad\\name", " DefaultPackage", "..", "bad\nname" })
                Assert.Throws<ArgumentException>(() => new ResourceOptions { DefaultPackageName = name }.CreateValidatedCopy(true, false));
            foreach (var mode in new[] { EPlayMode.None, EPlayMode.CustomPlayMode, EPlayMode.EditorSimulateMode, (EPlayMode)999 })
                Assert.Throws<ArgumentException>(() => new ResourceOptions { PlayMode = mode }.CreateValidatedCopy(false, false));
        }

        [Test] public void RejectsInvalidLimitsAndUrls()
        {
            var invalid = new[] {
                new ResourceOptions { DownloadingMaxNum = 0 }, new ResourceOptions { FailedTryAgain = -1 },
                new ResourceOptions { Milliseconds = 0 }, new ResourceOptions { AssetCapacity = -1 },
                new ResourceOptions { AssetExpireTime = float.NaN }, new ResourceOptions { AssetAutoReleaseInterval = float.PositiveInfinity }
            };
            foreach (var options in invalid) Assert.Catch<ArgumentException>(() => options.CreateValidatedCopy(true, false));
            foreach (string url in new[] { "not a url", "file:///tmp", "https://cdn.example/game?version=1", "https://cdn.example/game#fragment" })
                Assert.Throws<ArgumentException>(() => new ResourceOptions { HostServerURL = url }.CreateValidatedCopy(true, false));
        }

        [Test] public void WebGLPlayerRequiresWebMode()
        {
            Assert.Throws<ArgumentException>(() => new ResourceOptions().CreateValidatedCopy(false, true));
            var options = new ResourceOptions { PlayMode = EPlayMode.WebPlayMode, HostServerURL = "https://cdn.example/game" };
            Assert.DoesNotThrow(() => options.CreateValidatedCopy(false, true));
            Assert.Throws<ArgumentException>(() => options.CreateValidatedCopy(false, false));
        }

        [Test] public void ModuleConfigurationIsCopiedAndSingleAssignment()
        {
            var module = ModuleSystem.GetModule<IResourceModule>();
            Assert.IsFalse(module.IsConfigured);
            Assert.Throws<InvalidOperationException>(() => { var ignored = module.Options; });
            var input = new ResourceOptions();
            module.Configure(input);
            input.DefaultPackageName = "changed";
            module.Options.DefaultPackageName = "also changed";
            Assert.AreEqual("DefaultPackage", module.DefaultPackageName);
#if UNITY_EDITOR
            Assert.AreEqual(EPlayMode.EditorSimulateMode, module.PlayMode);
#else
            Assert.AreEqual(EPlayMode.OfflinePlayMode, module.PlayMode);
#endif
            Assert.AreEqual(4, ((Module)module).Priority);
            Assert.Throws<InvalidOperationException>(() => module.Configure(new ResourceOptions()));
        }

        [Test] public void FailedConfigurationCanBeRetried()
        {
            var module = ModuleSystem.GetModule<IResourceModule>();
            Assert.Throws<ArgumentException>(() => module.Configure(new ResourceOptions { DefaultPackageName = "" }));
            Assert.IsFalse(module.IsConfigured);
            module.Configure(new ResourceOptions());
            Assert.IsTrue(module.IsConfigured);
        }

        [Test] public void ShutdownInvalidatesOldModuleAndNewSessionCanConfigure()
        {
            var module = ModuleSystem.GetModule<IResourceModule>();
            module.Configure(new ResourceOptions());
            ModuleSystem.Shutdown();
            Assert.IsFalse(module.IsConfigured);
            Assert.Throws<ObjectDisposedException>(() => module.Configure(new ResourceOptions()));
            var next = ModuleSystem.GetModule<IResourceModule>();
            Assert.AreNotSame(module, next);
            next.Configure(new ResourceOptions());
        }
    }
}
