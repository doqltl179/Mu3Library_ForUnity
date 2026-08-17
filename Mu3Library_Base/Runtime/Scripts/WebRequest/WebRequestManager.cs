using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Mu3Library.WebRequest
{
    /// <summary>
    /// Manages HTTP requests using UnityWebRequest.
    /// Supports GET, POST operations with type-safe response parsing.
    /// </summary>
    public partial class WebRequestManager : IWebRequestManager
    {
        #region Utility
        /// <summary>
        /// Gets the download size of a resource at the specified URL using a HEAD request.
        /// </summary>
        /// <param name="url">The URL to query.</param>
        /// <param name="callback">Callback with the size in bytes, or -1 if failed.</param>
        public void GetDownloadSize(string url, Action<long> callback)
        {
            GetDownloadSizeWithResult(url, result => callback?.Invoke(result.IsSuccess ? result.Data : -1));
        }

        /// <summary>
        /// Sends a GET request to the specified URL.
        /// </summary>
        /// <typeparam name="T">Response type (string, byte[], Texture2D, or JSON-serializable type).</typeparam>
        /// <param name="url">The URL to request.</param>
        /// <param name="callback">Callback with the parsed response.</param>
        public void Get<T>(string url, Action<T> callback)
        {
            GetWithResult<T>(url, result => callback?.Invoke(result.Data));
        }

        /// <summary>
        /// Sends a POST request with a JSON body to the specified URL.
        /// </summary>
        /// <typeparam name="TRequest">Request body type (will be JSON-serialized).</typeparam>
        /// <typeparam name="TResponse">Response type.</typeparam>
        /// <param name="url">The URL to post to.</param>
        /// <param name="body">The request body.</param>
        /// <param name="callback">Callback with the parsed response.</param>
        /// <param name="contentType">Content-Type header (default: application/json).</param>
        public void Post<TRequest, TResponse>(string url, TRequest body, Action<TResponse> callback, string contentType = "application/json")
        {
            PostWithResult<TRequest, TResponse>(url, body, result => callback?.Invoke(result.Data), contentType);
        }

        /// <summary>
        /// Sends a PUT request with a JSON body to the specified URL.
        /// </summary>
        public void Put<TRequest, TResponse>(string url, TRequest body, Action<TResponse> callback, string contentType = "application/json")
        {
            PutWithResult<TRequest, TResponse>(url, body, result => callback?.Invoke(result.Data), contentType);
        }

        /// <summary>
        /// Sends a PATCH request with a JSON body to the specified URL.
        /// </summary>
        public void Patch<TRequest, TResponse>(string url, TRequest body, Action<TResponse> callback, string contentType = "application/json")
        {
            PatchWithResult<TRequest, TResponse>(url, body, result => callback?.Invoke(result.Data), contentType);
        }

        /// <summary>
        /// Sends a DELETE request to the specified URL.
        /// </summary>
        public void Delete<TResponse>(string url, Action<TResponse> callback)
        {
            DeleteWithResult<TResponse>(url, result => callback?.Invoke(result.Data));
        }

        public void GetWithResult<T>(string url, Action<WebRequestResult<T>> callback, IDictionary<string, string> requestHeaders = null, int timeoutSeconds = 0, int retryCount = 0, float retryDelaySeconds = 1.0f, Action<float> onDownloadProgress = null, WebRequestCancellation cancellation = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                string error = "WebRequest GET failed. url is null or empty.";
                Debug.LogError(error);
                callback?.Invoke(WebRequestResult<T>.Failure(-1, error, null));
                return;
            }

            ExecuteWithRetry(
                method: UnityWebRequest.kHttpVerbGET,
                retryCount: retryCount,
                retryDelaySeconds: retryDelaySeconds,
                cancellation: cancellation,
                onDownloadProgress: onDownloadProgress,
                createRequest: () =>
                {
                    UnityWebRequest request = CreateGetRequest<T>(url);
                    ApplyRequestOptions(request, requestHeaders, timeoutSeconds);
                    return request;
                },
                onComplete: request =>
                {
                    WebRequestResult<T> result = ParseResult<T>(url, request, "GET");
                    callback?.Invoke(result);
                },
                onUnexpectedFailure: ex => callback?.Invoke(CreateUnexpectedFailureResult<T>("GET", url, ex)),
                onCanceled: () => callback?.Invoke(CreateCanceledResult<T>("GET", url)));
        }

        public void PostWithResult<TRequest, TResponse>(string url, TRequest body, Action<WebRequestResult<TResponse>> callback, string contentType = "application/json", IDictionary<string, string> requestHeaders = null, int timeoutSeconds = 0, int retryCount = 0, float retryDelaySeconds = 1.0f, Action<float> onDownloadProgress = null, WebRequestCancellation cancellation = null)
            => SendBodyRequestWithResult(UnityWebRequest.kHttpVerbPOST, url, body, callback, contentType, requestHeaders, timeoutSeconds, retryCount, retryDelaySeconds, onDownloadProgress, cancellation);

        public void PutWithResult<TRequest, TResponse>(string url, TRequest body, Action<WebRequestResult<TResponse>> callback, string contentType = "application/json", IDictionary<string, string> requestHeaders = null, int timeoutSeconds = 0, int retryCount = 0, float retryDelaySeconds = 1.0f, Action<float> onDownloadProgress = null, WebRequestCancellation cancellation = null)
            => SendBodyRequestWithResult(UnityWebRequest.kHttpVerbPUT, url, body, callback, contentType, requestHeaders, timeoutSeconds, retryCount, retryDelaySeconds, onDownloadProgress, cancellation);

        public void PatchWithResult<TRequest, TResponse>(string url, TRequest body, Action<WebRequestResult<TResponse>> callback, string contentType = "application/json", IDictionary<string, string> requestHeaders = null, int timeoutSeconds = 0, int retryCount = 0, float retryDelaySeconds = 1.0f, Action<float> onDownloadProgress = null, WebRequestCancellation cancellation = null)
            => SendBodyRequestWithResult("PATCH", url, body, callback, contentType, requestHeaders, timeoutSeconds, retryCount, retryDelaySeconds, onDownloadProgress, cancellation);

        public void DeleteWithResult<TResponse>(string url, Action<WebRequestResult<TResponse>> callback, IDictionary<string, string> requestHeaders = null, int timeoutSeconds = 0, int retryCount = 0, float retryDelaySeconds = 1.0f, WebRequestCancellation cancellation = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                string error = "WebRequest DELETE failed. url is null or empty.";
                Debug.LogError(error);
                callback?.Invoke(WebRequestResult<TResponse>.Failure(-1, error, null));
                return;
            }

            ExecuteWithRetry(
                method: UnityWebRequest.kHttpVerbDELETE,
                retryCount: retryCount,
                retryDelaySeconds: retryDelaySeconds,
                cancellation: cancellation,
                onDownloadProgress: null,
                createRequest: () =>
                {
                    UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbDELETE);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    ApplyRequestOptions(request, requestHeaders, timeoutSeconds);
                    return request;
                },
                onComplete: request =>
                {
                    WebRequestResult<TResponse> result = ParseResult<TResponse>(url, request, "DELETE");
                    callback?.Invoke(result);
                },
                onUnexpectedFailure: ex => callback?.Invoke(CreateUnexpectedFailureResult<TResponse>("DELETE", url, ex)),
                onCanceled: () => callback?.Invoke(CreateCanceledResult<TResponse>("DELETE", url)));
        }

        public void GetDownloadSizeWithResult(string url, Action<WebRequestResult<long>> callback, IDictionary<string, string> requestHeaders = null, int timeoutSeconds = 0, int retryCount = 0, float retryDelaySeconds = 1.0f, WebRequestCancellation cancellation = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                string error = "WebRequest HEAD failed. url is null or empty.";
                Debug.LogError(error);
                callback?.Invoke(WebRequestResult<long>.Failure(-1, error, null));
                return;
            }

            ExecuteWithRetry(
                method: UnityWebRequest.kHttpVerbHEAD,
                retryCount: retryCount,
                retryDelaySeconds: retryDelaySeconds,
                cancellation: cancellation,
                onDownloadProgress: null,
                createRequest: () => CreateHeadRequest(url, requestHeaders, timeoutSeconds),
                onComplete: request =>
                {
                    WebRequestResult<long> result = ParseDownloadSizeResult(url, request);
                    callback?.Invoke(result);
                },
                onUnexpectedFailure: ex => callback?.Invoke(CreateUnexpectedFailureResult<long>("HEAD", url, ex)),
                onCanceled: () => callback?.Invoke(CreateCanceledResult<long>("HEAD", url)));
        }
        #endregion

        private void SendBodyRequestWithResult<TRequest, TResponse>(
            string method,
            string url,
            TRequest body,
            Action<WebRequestResult<TResponse>> callback,
            string contentType,
            IDictionary<string, string> requestHeaders,
            int timeoutSeconds,
            int retryCount,
            float retryDelaySeconds,
            Action<float> onDownloadProgress,
            WebRequestCancellation cancellation)
        {
            if (string.IsNullOrEmpty(url))
            {
                string error = $"WebRequest {method} failed. url is null or empty.";
                Debug.LogError(error);
                callback?.Invoke(WebRequestResult<TResponse>.Failure(-1, error, null));
                return;
            }

            string payload = SerializeBody(body);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payload ?? string.Empty);

            ExecuteWithRetry(
                method: method,
                retryCount: retryCount,
                retryDelaySeconds: retryDelaySeconds,
                cancellation: cancellation,
                onDownloadProgress: onDownloadProgress,
                createRequest: () => CreateBodyRequest<TResponse>(url, method, bodyRaw, contentType, requestHeaders, timeoutSeconds),
                onComplete: request =>
                {
                    WebRequestResult<TResponse> result = ParseResult<TResponse>(url, request, method);
                    callback?.Invoke(result);
                },
                onUnexpectedFailure: ex => callback?.Invoke(CreateUnexpectedFailureResult<TResponse>(method, url, ex)),
                onCanceled: () => callback?.Invoke(CreateCanceledResult<TResponse>(method, url)));
        }

        private void ExecuteWithRetry(
            string method,
            int retryCount,
            float retryDelaySeconds,
            WebRequestCancellation cancellation,
            Action<float> onDownloadProgress,
            Func<UnityWebRequest> createRequest,
            Action<UnityWebRequest> onComplete,
            Action<Exception> onUnexpectedFailure,
            Action onCanceled)
        {
            int maxAttempts = Mathf.Max(1, retryCount + 1);

            void RetryAfterBackoff(int nextAttempt)
            {
                Debug.LogWarning($"WebRequest {method} retry. attempt: {nextAttempt + 1}/{maxAttempts}");

                // Exponential backoff: the first retry waits the base delay, each further
                // retry doubles it, so a struggling server is not hammered in a tight loop.
                float delay = retryDelaySeconds * Mathf.Pow(2.0f, nextAttempt - 1);
                if (delay > 0.0f)
                {
                    InvokeAfterDelay(delay, () => SendAttempt(nextAttempt));
                }
                else
                {
                    SendAttempt(nextAttempt);
                }
            }

            void SendAttempt(int attempt)
            {
                if (cancellation != null && cancellation.IsCancellationRequested)
                {
                    onCanceled?.Invoke();
                    return;
                }

                UnityWebRequest request = null;

                try
                {
                    request = createRequest();
                    cancellation?.SetActiveRequest(request);

                    request.SendWebRequest().completed += _ =>
                    {
                        cancellation?.ClearActiveRequest(request);

                        bool isCanceled = cancellation != null && cancellation.IsCancellationRequested;
                        bool canRetry = !isCanceled && request.result != UnityWebRequest.Result.Success && attempt + 1 < maxAttempts;
                        if (canRetry)
                        {
                            request.Dispose();
                            RetryAfterBackoff(attempt + 1);
                            return;
                        }

                        onComplete?.Invoke(request);
                        request.Dispose();
                    };

                    if (onDownloadProgress != null)
                    {
                        PollDownloadProgress(request, onDownloadProgress);
                    }
                }
                catch (Exception ex)
                {
                    cancellation?.ClearActiveRequest(request);
                    request?.Dispose();

                    bool isCanceled = cancellation != null && cancellation.IsCancellationRequested;
                    bool canRetry = !isCanceled && attempt + 1 < maxAttempts;
                    if (canRetry)
                    {
                        RetryAfterBackoff(attempt + 1);
                        return;
                    }

                    if (isCanceled)
                    {
                        onCanceled?.Invoke();
                        return;
                    }

                    onUnexpectedFailure?.Invoke(ex);
                }
            }

            SendAttempt(0);
        }

        /// <summary>
        /// Runs an action after a delay on the main thread. Unity's synchronization context
        /// brings the continuation back, so the action lands where web request callbacks land.
        /// </summary>
        private static async void InvokeAfterDelay(float seconds, Action action)
        {
            try
            {
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(seconds));
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Reports the download progress of a request until it finishes. The request may be
        /// disposed while the poll sleeps, which simply ends the poll.
        /// </summary>
        private static async void PollDownloadProgress(UnityWebRequest request, Action<float> onDownloadProgress)
        {
            float lastProgress = -1.0f;

            try
            {
                while (!request.isDone)
                {
                    float progress = request.downloadProgress;
                    if (!Mathf.Approximately(progress, lastProgress))
                    {
                        lastProgress = progress;
                        onDownloadProgress(progress);
                    }

                    await System.Threading.Tasks.Task.Yield();
                }

                onDownloadProgress(1.0f);
            }
            catch (Exception)
            {
                // The request was disposed while the poll slept; the download is over either way.
            }
        }

        private WebRequestResult<T> CreateCanceledResult<T>(string method, string url)
        {
            string error = $"WebRequest {method} canceled. url: {url}";
            Debug.LogWarning(error);
            return WebRequestResult<T>.Failure(-1, error, null);
        }

        private WebRequestResult<T> CreateUnexpectedFailureResult<T>(string method, string url, Exception exception)
        {
            string error = $"WebRequest {method} failed with exception. url: {url}\r\n{exception.GetType().Name}: {exception.Message}";
            Debug.LogException(exception);
            return WebRequestResult<T>.Failure(-1, error, null);
        }

        private WebRequestResult<T> ParseResult<T>(string url, UnityWebRequest request, string method)
        {
            long statusCode = request.responseCode;
            IReadOnlyDictionary<string, string> headers = request.GetResponseHeaders();
            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"WebRequest {method} failed. url: {url}\r\n{request.error}";
                Debug.LogError(error);
                return WebRequestResult<T>.Failure(statusCode, error, headers);
            }

            try
            {
                T result = ParseResponse<T>(request);
                return WebRequestResult<T>.Success(statusCode, result, headers);
            }
            catch (Exception ex)
            {
                string error = $"WebRequest {method} parse failed. url: {url}\r\n{ex.Message}";
                Debug.LogException(ex);
                return WebRequestResult<T>.Failure(statusCode, error, headers);
            }
        }

        private T ParseResponse<T>(UnityWebRequest request)
        {
            if (typeof(T) == typeof(string))
            {
                return (T)(object)request.downloadHandler.text;
            }
            if (typeof(T) == typeof(byte[]))
            {
                return (T)(object)request.downloadHandler.data;
            }
            if (typeof(T) == typeof(Texture2D))
            {
                return (T)(object)DownloadHandlerTexture.GetContent(request);
            }

            return JsonUtility.FromJson<T>(request.downloadHandler.text);
        }

        private WebRequestResult<long> ParseDownloadSizeResult(string url, UnityWebRequest request)
        {
            long statusCode = request.responseCode;
            IReadOnlyDictionary<string, string> headers = request.GetResponseHeaders();
            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"WebRequest HEAD failed. url: {url}\r\n{request.error}";
                Debug.LogError(error);
                return WebRequestResult<long>.Failure(statusCode, error, headers);
            }

            long size = -1;
            if (headers != null && headers.TryGetValue("Content-Length", out string lengthValue))
            {
                if (!long.TryParse(lengthValue, out size))
                {
                    size = -1;
                }
            }

            return WebRequestResult<long>.Success(statusCode, size, headers);
        }

        private static string SerializeBody<TRequest>(TRequest body)
        {
            if (body == null)
            {
                return string.Empty;
            }

            if (body is string bodyString)
            {
                return bodyString;
            }

            return JsonUtility.ToJson(body);
        }

        private static void ApplyRequestOptions(UnityWebRequest request, IDictionary<string, string> requestHeaders, int timeoutSeconds)
        {
            if (request == null)
            {
                return;
            }

            if (timeoutSeconds > 0)
            {
                request.timeout = timeoutSeconds;
            }

            if (requestHeaders == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> header in requestHeaders)
            {
                if (string.IsNullOrEmpty(header.Key))
                {
                    continue;
                }

                request.SetRequestHeader(header.Key, header.Value ?? string.Empty);
            }
        }

        private static UnityWebRequest CreateHeadRequest(
            string url,
            IDictionary<string, string> requestHeaders,
            int timeoutSeconds)
        {
            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbHEAD);
            request.downloadHandler = new DownloadHandlerBuffer();
            ApplyRequestOptions(request, requestHeaders, timeoutSeconds);
            return request;
        }

        private UnityWebRequest CreateBodyRequest<TResponse>(
            string url,
            string method,
            byte[] bodyRaw,
            string contentType,
            IDictionary<string, string> requestHeaders,
            int timeoutSeconds)
        {
            UnityWebRequest request = new UnityWebRequest(url, method);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = CreateDownloadHandler<TResponse>();
            request.SetRequestHeader("Content-Type", contentType);
            ApplyRequestOptions(request, requestHeaders, timeoutSeconds);
            return request;
        }

        private UnityWebRequest CreateGetRequest<T>(string url)
        {
            if (typeof(T) == typeof(Texture2D))
            {
                return UnityWebRequestTexture.GetTexture(url);
            }

            return UnityWebRequest.Get(url);
        }

        private DownloadHandler CreateDownloadHandler<T>()
        {
            if (typeof(T) == typeof(Texture2D))
            {
                return new DownloadHandlerTexture(true);
            }

            return new DownloadHandlerBuffer();
        }
    }
}
