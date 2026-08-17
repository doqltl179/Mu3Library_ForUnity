using System;
using System.Collections.Generic;
using UnityEngine.Networking;

namespace Mu3Library.WebRequest
{
    /// <summary>
    /// Cancellation token for a running web request. Handed into a request call, it lets the
    /// caller abort the request in flight; the callback then answers with a failure result and
    /// no further retry runs.
    /// </summary>
    public sealed class WebRequestCancellation
    {
        private UnityWebRequest _activeRequest;

        public bool IsCancellationRequested { get; private set; }

        public void Cancel()
        {
            IsCancellationRequested = true;
            _activeRequest?.Abort();
        }

        internal void SetActiveRequest(UnityWebRequest request)
        {
            _activeRequest = request;
        }

        internal void ClearActiveRequest(UnityWebRequest request)
        {
            if (ReferenceEquals(_activeRequest, request))
            {
                _activeRequest = null;
            }
        }
    }

    public readonly struct WebRequestResult<T>
    {
        public bool IsSuccess { get; }
        public long StatusCode { get; }
        public T Data { get; }
        public string ErrorMessage { get; }
        public IReadOnlyDictionary<string, string> ResponseHeaders { get; }

        private WebRequestResult(
            bool isSuccess,
            long statusCode,
            T data,
            string errorMessage,
            IReadOnlyDictionary<string, string> responseHeaders)
        {
            IsSuccess = isSuccess;
            StatusCode = statusCode;
            Data = data;
            ErrorMessage = errorMessage;
            ResponseHeaders = responseHeaders;
        }

        public static WebRequestResult<T> Success(long statusCode, T data, IReadOnlyDictionary<string, string> responseHeaders)
            => new WebRequestResult<T>(true, statusCode, data, string.Empty, responseHeaders);

        public static WebRequestResult<T> Failure(long statusCode, string errorMessage, IReadOnlyDictionary<string, string> responseHeaders)
            => new WebRequestResult<T>(false, statusCode, default, errorMessage, responseHeaders);
    }

    public partial interface IWebRequestManager
    {
        public void Get<T>(string url, Action<T> callback);
        public void Post<TRequest, TResponse>(string url, TRequest body, Action<TResponse> callback, string contentType = "application/json");
        public void Put<TRequest, TResponse>(string url, TRequest body, Action<TResponse> callback, string contentType = "application/json");
        public void Patch<TRequest, TResponse>(string url, TRequest body, Action<TResponse> callback, string contentType = "application/json");
        public void Delete<TResponse>(string url, Action<TResponse> callback);
        public void GetDownloadSize(string url, Action<long> callback);

        public void GetWithResult<T>(
            string url,
            Action<WebRequestResult<T>> callback,
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 1.0f,
            Action<float> onDownloadProgress = null,
            WebRequestCancellation cancellation = null);

        public void PostWithResult<TRequest, TResponse>(
            string url,
            TRequest body,
            Action<WebRequestResult<TResponse>> callback,
            string contentType = "application/json",
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 1.0f,
            Action<float> onDownloadProgress = null,
            WebRequestCancellation cancellation = null);

        public void PutWithResult<TRequest, TResponse>(
            string url,
            TRequest body,
            Action<WebRequestResult<TResponse>> callback,
            string contentType = "application/json",
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 1.0f,
            Action<float> onDownloadProgress = null,
            WebRequestCancellation cancellation = null);

        public void PatchWithResult<TRequest, TResponse>(
            string url,
            TRequest body,
            Action<WebRequestResult<TResponse>> callback,
            string contentType = "application/json",
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 1.0f,
            Action<float> onDownloadProgress = null,
            WebRequestCancellation cancellation = null);

        public void DeleteWithResult<TResponse>(
            string url,
            Action<WebRequestResult<TResponse>> callback,
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 1.0f,
            WebRequestCancellation cancellation = null);

        public void GetDownloadSizeWithResult(
            string url,
            Action<WebRequestResult<long>> callback,
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 1.0f,
            WebRequestCancellation cancellation = null);
    }
}
