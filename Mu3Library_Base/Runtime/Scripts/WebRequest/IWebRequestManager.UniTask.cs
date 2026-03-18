#if MU3LIBRARY_UNITASK_SUPPORT
using System.Threading;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Mu3Library.WebRequest
{
    public partial interface IWebRequestManager
    {
        public UniTask<T> GetAsync<T>(string url, CancellationToken cancellationToken = default);
        public UniTask<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest body, string contentType = "application/json", CancellationToken cancellationToken = default);
        public UniTask<long> GetDownloadSizeAsync(string url, CancellationToken cancellationToken = default);

        public UniTask<WebRequestResult<T>> GetResultAsync<T>(
            string url,
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 0.5f,
            CancellationToken cancellationToken = default);

        public UniTask<WebRequestResult<TResponse>> PostResultAsync<TRequest, TResponse>(
            string url,
            TRequest body,
            string contentType = "application/json",
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 0.5f,
            CancellationToken cancellationToken = default);

        public UniTask<WebRequestResult<long>> GetDownloadSizeResultAsync(
            string url,
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 0.5f,
            CancellationToken cancellationToken = default);
    }
}
#endif
