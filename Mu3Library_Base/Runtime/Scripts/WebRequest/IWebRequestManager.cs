using System;
using System.Collections.Generic;

namespace Mu3Library.WebRequest
{
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
        public void GetDownloadSize(string url, Action<long> callback);

        public void GetWithResult<T>(
            string url,
            Action<WebRequestResult<T>> callback,
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0);

        public void PostWithResult<TRequest, TResponse>(
            string url,
            TRequest body,
            Action<WebRequestResult<TResponse>> callback,
            string contentType = "application/json",
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0);

        public void GetDownloadSizeWithResult(
            string url,
            Action<WebRequestResult<long>> callback,
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0);
    }
}
