using System;
using System.Collections;

namespace Mu3Library.Coroutine.Foundation
{
    /// <summary>
    /// Runs an IEnumerator while forwarding exceptions from the iterator and nested iterators to a callback.
    /// </summary>
    public static class CoroutineSafeRunner
    {
        /// <summary>
        /// Run a coroutine safely.
        /// </summary>
        /// <param name="routine">The coroutine to run.</param>
        /// <param name="onError">Called once when the routine or a nested routine throws.</param>
        public static IEnumerator Run(IEnumerator routine, Action<Exception> onError = null)
        {
            if (routine == null)
            {
                onError?.Invoke(new ArgumentNullException(nameof(routine)));
                yield break;
            }

            Exception error = null;
            yield return RunInternal(routine, exception => error = exception);

            if (error != null)
            {
                onError?.Invoke(error);
            }
        }

        private static IEnumerator RunInternal(IEnumerator routine, Action<Exception> onError)
        {
            Exception error = null;

            try
            {
                while (error == null)
                {
                    bool hasNext;
                    object current = null;

                    try
                    {
                        hasNext = routine.MoveNext();

                        if (hasNext)
                        {
                            current = routine.Current;
                        }
                    }
                    catch (Exception exception)
                    {
                        error = exception;
                        break;
                    }

                    if (!hasNext)
                    {
                        break;
                    }

                    if (current is IEnumerator nestedRoutine)
                    {
                        Exception nestedError = null;

                        yield return RunInternal(
                            nestedRoutine,
                            exception => nestedError = exception
                        );

                        if (nestedError != null)
                        {
                            error = nestedError;
                        }
                    }
                    else
                    {
                        yield return current;
                    }
                }
            }
            finally
            {
                if (routine is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception exception)
                    {
                        error ??= exception;
                    }
                }
            }

            if (error != null)
            {
                onError?.Invoke(error);
            }
        }
    }
}
