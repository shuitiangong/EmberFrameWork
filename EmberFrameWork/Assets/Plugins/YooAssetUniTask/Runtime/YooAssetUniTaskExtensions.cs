using System;
using System.Threading;
using YooAsset;

namespace Cysharp.Threading.Tasks
{
    /// <summary>Converts YooAsset operations and resource handles to UniTask.</summary>
    /// <remarks>
    /// Call from the Unity main thread. With immediate cancellation, a continuation may run on
    /// the thread that cancels the token; return to the main thread before accessing YooAsset.
    /// </remarks>
    public static class YooAssetUniTaskExtensions
    {
        /// <summary>
        /// Waits for an operation without starting or canceling it. YooAsset failures remain
        /// available through <see cref="AsyncOperationBase.Status"/> and <see cref="AsyncOperationBase.Error"/>.
        /// Cancellation stops only this wait.
        /// </summary>
        public static UniTask ToUniTask(
            this AsyncOperationBase operation,
            IProgress<float> progress = null,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default,
            bool cancelImmediately = false)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromCanceled(cancellationToken);
            if (operation.IsDone)
            {
                progress?.Report(operation.Progress);
                return UniTask.CompletedTask;
            }

            return UniTask.WaitUntil(
                new OperationState(operation, progress),
                IsOperationComplete,
                timing,
                cancellationToken,
                cancelImmediately);
        }

        /// <summary>Stops waiting when the token is canceled; the operation continues.</summary>
        public static UniTask WithCancellation(
            this AsyncOperationBase operation,
            CancellationToken cancellationToken,
            bool cancelImmediately = false)
        {
            return operation.ToUniTask(cancellationToken: cancellationToken, cancelImmediately: cancelImmediately);
        }

        /// <summary>
        /// Waits for a resource handle. YooAsset failures remain available through
        /// <see cref="HandleBase.Status"/> and <see cref="HandleBase.Error"/>. Cancellation
        /// stops only this wait; the caller retains ownership and must release the handle.
        /// </summary>
        public static UniTask ToUniTask(
            this HandleBase handle,
            IProgress<float> progress = null,
            PlayerLoopTiming timing = PlayerLoopTiming.Update,
            CancellationToken cancellationToken = default,
            bool cancelImmediately = false)
        {
            if (handle == null)
                throw new ArgumentNullException(nameof(handle));
            if (cancellationToken.IsCancellationRequested)
                return UniTask.FromCanceled(cancellationToken);
            EnsureValid(handle);
            if (handle.IsDone)
            {
                progress?.Report(handle.Progress);
                return UniTask.CompletedTask;
            }

            return UniTask.WaitUntil(
                new HandleState(handle, progress),
                IsHandleComplete,
                timing,
                cancellationToken,
                cancelImmediately);
        }

        /// <summary>Stops waiting when the token is canceled; the caller still owns the handle.</summary>
        public static UniTask WithCancellation(
            this HandleBase handle,
            CancellationToken cancellationToken,
            bool cancelImmediately = false)
        {
            return handle.ToUniTask(cancellationToken: cancellationToken, cancelImmediately: cancelImmediately);
        }

        private static bool IsOperationComplete(OperationState state)
        {
            state.Progress?.Report(state.Operation.Progress);
            return state.Operation.IsDone;
        }

        private static bool IsHandleComplete(HandleState state)
        {
            EnsureValid(state.Handle);
            var isDone = state.Handle.IsDone;
            state.Progress?.Report(state.Handle.Progress);
            return isDone;
        }

        private static void EnsureValid(HandleBase handle)
        {
            if (!handle.IsValid)
                throw new InvalidOperationException("The YooAsset handle is no longer valid.");
        }

        private readonly struct OperationState
        {
            public readonly AsyncOperationBase Operation;
            public readonly IProgress<float> Progress;

            public OperationState(AsyncOperationBase operation, IProgress<float> progress)
            {
                Operation = operation;
                Progress = progress;
            }
        }

        private readonly struct HandleState
        {
            public readonly HandleBase Handle;
            public readonly IProgress<float> Progress;

            public HandleState(HandleBase handle, IProgress<float> progress)
            {
                Handle = handle;
                Progress = progress;
            }
        }
    }
}
