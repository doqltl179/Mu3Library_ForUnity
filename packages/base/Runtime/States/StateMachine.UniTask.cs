#if MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mu3Library.States
{
    public sealed partial class StateMachine<TKey>
    {
        private CancellationTokenSource _cancellation = new CancellationTokenSource();
        private bool _hasTask = false;
        private UniTask _task;

        /// <summary>An <see cref="IAsyncState"/> enters and exits by its async methods, which the machine awaits instead of the routines.</summary>
        partial void TryStartAsync(IState state, bool entering, ref bool isAsync)
        {
            if (!(state is IAsyncState asyncState))
            {
                return;
            }

            isAsync = true;
            try
            {
                _task = entering ? asyncState.EnterAsync(_cancellation.Token) : asyncState.ExitAsync(_cancellation.Token);
                _hasTask = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }

            StepAsync();
        }

        partial void IsAsyncPending(ref bool isPending)
        {
            isPending |= _hasTask;
        }

        /// <summary>Takes the task's end once it ended, logging a failure, so the transition goes on.</summary>
        partial void StepAsync()
        {
            if (!_hasTask || _task.Status == UniTaskStatus.Pending)
            {
                return;
            }

            _hasTask = false;
            try
            {
                _task.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        partial void CancelAsync()
        {
            _hasTask = false;
            _cancellation.Cancel();
            _cancellation.Dispose();
        }
    }
}
#endif
