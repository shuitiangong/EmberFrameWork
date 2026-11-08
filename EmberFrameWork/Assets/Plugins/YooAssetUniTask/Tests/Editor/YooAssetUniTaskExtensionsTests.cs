using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using YooAsset;

public class YooAssetUniTaskExtensionsTests
{
    [Test]
    public void CompletedFailurePreservesYooAssetStatusAndError()
    {
        var operation = new ManualOperation();
        operation.Start();
        operation.Fail("load failed");

        var progress = new RecordingProgress();
        var wait = operation.ToUniTask(progress);

        Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        wait.GetAwaiter().GetResult();
        Assert.That(operation.Status, Is.EqualTo(EOperationStatus.Failed));
        Assert.That(operation.Error, Is.EqualTo("load failed"));
        Assert.That(progress.Values, Is.EqualTo(new[] { 1f }));
    }

    [Test]
    public void NullOperationsAndHandlesAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ((AsyncOperationBase)null).ToUniTask());
        Assert.Throws<ArgumentNullException>(() => ((HandleBase)null).ToUniTask());
    }

    [Test]
    public void AlreadyCanceledWaitDoesNotStopOperation()
    {
        var operation = new ManualOperation();
        operation.Start();
        using (var source = new CancellationTokenSource())
        {
            source.Cancel();
            var wait = operation.WithCancellation(source.Token);

            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.Catch<OperationCanceledException>(() => wait.GetAwaiter().GetResult());
            Assert.That(operation.Status, Is.EqualTo(EOperationStatus.Processing));
        }
    }

    [UnityTest]
    public IEnumerator ProgressAndCompletionAreObservedOnThePlayerLoop()
    {
        var operation = new ManualOperation();
        operation.Start();
        operation.SetProgress(0.25f);
        var progress = new RecordingProgress();
        var wait = operation.ToUniTask(progress);

        for (var frame = 0; frame < 120 && progress.Values.Count == 0; frame++)
            yield return null;

        Assert.That(progress.Values, Does.Contain(0.25f));
        Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Pending));

        operation.Succeed();
        for (var frame = 0; frame < 120 && wait.Status == UniTaskStatus.Pending; frame++)
            yield return null;

        Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Succeeded));
        wait.GetAwaiter().GetResult();
        Assert.That(progress.Values[progress.Values.Count - 1], Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator CancelingOneWaitDoesNotCancelTheOperationOrAnotherWait()
    {
        var operation = new ManualOperation();
        operation.Start();
        using (var source = new CancellationTokenSource())
        {
            var canceledWait = operation.WithCancellation(source.Token);
            var otherWait = operation.ToUniTask();
            source.Cancel();

            for (var frame = 0; frame < 120 && canceledWait.Status == UniTaskStatus.Pending; frame++)
                yield return null;

            Assert.That(canceledWait.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.Catch<OperationCanceledException>(() => canceledWait.GetAwaiter().GetResult());
            Assert.That(operation.Status, Is.EqualTo(EOperationStatus.Processing));
            Assert.That(otherWait.Status, Is.EqualTo(UniTaskStatus.Pending));

            operation.Succeed();
            for (var frame = 0; frame < 120 && otherWait.Status == UniTaskStatus.Pending; frame++)
                yield return null;

            Assert.That(otherWait.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            otherWait.GetAwaiter().GetResult();
        }
    }

    [UnityTest]
    public IEnumerator ImmediateCancellationDoesNotCompleteAnUnrelatedWait()
    {
        var first = new ManualOperation();
        var second = new ManualOperation();
        first.Start();
        second.Start();

        using (var source = new CancellationTokenSource())
        {
            var canceledWait = first.WithCancellation(source.Token, cancelImmediately: true);
            source.Cancel();
            Assert.That(canceledWait.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.Catch<OperationCanceledException>(() => canceledWait.GetAwaiter().GetResult());

            var otherWait = second.ToUniTask();
            first.Succeed();
            yield return null;

            Assert.That(otherWait.Status, Is.EqualTo(UniTaskStatus.Pending));
            second.Succeed();
            for (var frame = 0; frame < 120 && otherWait.Status == UniTaskStatus.Pending; frame++)
                yield return null;

            Assert.That(otherWait.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            otherWait.GetAwaiter().GetResult();
        }
    }

    [UnityTest]
    public IEnumerator HandleCompletionPreservesFailureAndCallerOwnership()
    {
        using (var fixture = new HandleFixture())
        {
            var progress = new RecordingProgress();
            var wait = fixture.Handle.ToUniTask(progress);
            fixture.Fail("asset failed");

            for (var frame = 0; frame < 120 && wait.Status == UniTaskStatus.Pending; frame++)
                yield return null;

            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            wait.GetAwaiter().GetResult();
            Assert.That(fixture.Handle.Status, Is.EqualTo(EOperationStatus.Failed));
            Assert.That(fixture.Handle.Error, Is.EqualTo("asset failed"));
            Assert.That(fixture.Handle.IsValid, Is.True);
            Assert.That(progress.Values[progress.Values.Count - 1], Is.EqualTo(1f));
        }
    }

    [Test]
    public void CancelingHandleWaitDoesNotReleaseTheHandle()
    {
        using (var fixture = new HandleFixture())
        using (var source = new CancellationTokenSource())
        {
            var wait = fixture.Handle.WithCancellation(source.Token, cancelImmediately: true);
            source.Cancel();

            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Canceled));
            Assert.Catch<OperationCanceledException>(() => wait.GetAwaiter().GetResult());
            Assert.That(fixture.Handle.IsValid, Is.True);

            fixture.Fail("asset failed");
            Assert.That(fixture.Handle.Status, Is.EqualTo(EOperationStatus.Failed));
            Assert.That(fixture.Handle.IsValid, Is.True);
        }
    }

    [UnityTest]
    public IEnumerator ReleasingHandleDuringWaitFaultsTheWait()
    {
        using (var fixture = new HandleFixture())
        {
            var wait = fixture.Handle.ToUniTask();
            fixture.Handle.Release();

            for (var frame = 0; frame < 120 && wait.Status == UniTaskStatus.Pending; frame++)
                yield return null;

            Assert.That(wait.Status, Is.EqualTo(UniTaskStatus.Faulted));
            Assert.Throws<InvalidOperationException>(() => wait.GetAwaiter().GetResult());
        }
    }

    private sealed class RecordingProgress : IProgress<float>
    {
        public readonly List<float> Values = new List<float>();
        public void Report(float value) => Values.Add(value);
    }

    private sealed class ManualOperation : CustomAsyncOperation
    {
        protected override void InternalStart() { }
        protected override void InternalUpdate() { }

        public void SetProgress(float value) => Progress = value;

        public void Succeed()
        {
            SetResult();
            Update();
        }

        public void Fail(string error)
        {
            SetError(error);
            Update();
        }
    }

    private sealed class HandleFixture : IDisposable
    {
        private readonly object provider;
        private readonly MethodInfo fail;
        public readonly AssetHandle Handle;

        public HandleFixture()
        {
            // YooAsset keeps provider construction internal. Use its real error provider to
            // exercise handle ownership without creating a package or changing project assets.
            var assembly = typeof(AssetHandle).Assembly;
            var managerType = assembly.GetType("YooAsset.ResourceManager", true);
            var providerType = assembly.GetType("YooAsset.ErrorProvider", true);
            var manager = Activator.CreateInstance(managerType, "AdapterTest");
            var assetInfo = Activator.CreateInstance(
                typeof(AssetInfo),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[] { "AdapterTest", "fixture" },
                null);
            provider = Activator.CreateInstance(providerType, manager, assetInfo);
            fail = providerType.GetMethod("SetCompletedWithError");
            var createHandle = providerType.BaseType.GetMethod("CreateHandle").MakeGenericMethod(typeof(AssetHandle));
            Handle = (AssetHandle)createHandle.Invoke(provider, null);
        }

        public void Fail(string error) => fail.Invoke(provider, new object[] { error });

        public void Dispose()
        {
            if (Handle.IsValid)
                Handle.Release();
        }
    }
}
