using System;
using NUnit.Framework;

namespace EmberFrameWork.Tests
{
    public class AudioSceneMigrationTests
    {
        [TearDown] public void TearDown() => ModuleSystem.Shutdown();

        [Test] public void FailedSceneLoadDoesNotLeaveLocationBusy()
        {
            var scene = new SceneModule();
            Assert.Throws<InvalidOperationException>(() => scene.LoadSceneAsync("uninitialized").GetAwaiter().GetResult());
            Assert.Throws<InvalidOperationException>(() => scene.LoadSceneAsync("uninitialized").GetAwaiter().GetResult());
            Assert.IsFalse(scene.IsContainScene("uninitialized"));
        }

        [Test] public void ShutdownRejectsNewSceneRequests()
        {
            var scene = new SceneModule();
            scene.Shutdown();
            Assert.DoesNotThrow(() => scene.Shutdown());
            Assert.Throws<ObjectDisposedException>(() => scene.LoadSceneAsync("stopped").GetAwaiter().GetResult());
        }

        [Test] public void AudioDataCanRecycleEmptyOrAlreadyReleasedState()
        {
            var data = new AudioData();
            Assert.DoesNotThrow(() => data.RecycleToPool());
            Assert.DoesNotThrow(() => data.RecycleToPool());
            Assert.IsNull(data.AssetHandle);
            Assert.IsFalse(data.InPool);
        }
    }
}
