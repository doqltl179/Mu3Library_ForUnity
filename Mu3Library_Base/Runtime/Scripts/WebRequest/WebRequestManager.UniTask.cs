#if MU3LIBRARY_UNITASK_SUPPORT
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Mu3Library.WebRequest
{
    public partial class WebRequestManager
    {
        public async UniTask<long> GetDownloadSizeAsync(string url, CancellationToken cancellationToken = default)
        {
            WebRequestResult<long> result = await GetDownloadSizeResultAsync(url, cancellationToken: cancellationToken);
            return result.IsSuccess ? result.Data : -1;
        }

        public async UniTask<T> GetAsync<T>(string url, CancellationToken cancellationToken = default)
        {
            WebRequestResult<T> result = await GetResultAsync<T>(url, cancellationToken: cancellationToken);
            return result.Data;
        }

        public async UniTask<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest body, string contentType = "application/json", CancellationToken cancellationToken = default)
        {
            WebRequestResult<TResponse> result = await PostResultAsync<TRequest, TResponse>(
                url,
                body,
                contentType: contentType,
                cancellationToken: cancellationToken);
            return result.Data;
        }

        public async UniTask<WebRequestResult<T>> GetResultAsync<T>(
            string url,
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 0.5f,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                string error = "WebRequest GET failed. url is null or empty.";
                Debug.LogError(error);
                return WebRequestResult<T>.Failure(-1, error, null);
            }

            return await ExecuteWithRetryAsync(
                method: "GET",
                url: url,
                createRequest: () =>
                {
                    UnityWebRequest request = CreateGetRequest<T>(url);
                    ApplyRequestOptions(request, requestHeaders, timeoutSeconds);
                    return request;
                },
                parseResult: request => ParseResult<T>(url, request, "GET"),
                retryCount: retryCount,
                retryDelaySeconds: retryDelaySeconds,
                cancellationToken: cancellationToken);
        }

        public async UniTask<WebRequestResult<TResponse>> PostResultAsync<TRequest, TResponse>(
            string url,
            TRequest body,
            string contentType = "application/json",
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 0.5f,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                string error = "WebRequest POST failed. url is null or empty.";
                Debug.LogError(error);
                return WebRequestResult<TResponse>.Failure(-1, error, null);
            }

            return await ExecuteWithRetryAsync(
                method: "POST",
                url: url,
                createRequest: () =>
                {
                    string payload = SerializeBody(body);
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(payload ?? string.Empty);

                    UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = CreateDownloadHandler<TResponse>();
                    request.SetRequestHeader("Content-Type", contentType);
                    ApplyRequestOptions(request, requestHeaders, timeoutSeconds);
                    return request;
                },
                parseResult: request => ParseResult<TResponse>(url, request, "POST"),
                retryCount: retryCount,
                retryDelaySeconds: retryDelaySeconds,
                cancellationToken: cancellationToken);
        }

        public async UniTask<WebRequestResult<long>> GetDownloadSizeResultAsync(
            string url,
            IDictionary<string, string> requestHeaders = null,
            int timeoutSeconds = 0,
            int retryCount = 0,
            float retryDelaySeconds = 0.5f,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                string error = "WebRequest HEAD failed. url is null or empty.";
                Debug.LogError(error);
                return WebRequestResult<long>.Failure(-1, error, null);
            }

            return await ExecuteWithRetryAsync(
                method: "HEAD",
                url: url,
                createRequest: () =>
                {
                    UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbHEAD);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    ApplyRequestOptions(request, requestHeaders, timeoutSeconds);
                    return request;
                },
                parseResult: request => ParseDownloadSizeResult(url, request),
                retryCount: retryCount,
                retryDelaySeconds: retryDelaySeconds,
                cancellationToken: cancellationToken);
        }

        private async UniTask<WebRequestResult<T>> ExecuteWithRetryAsync<T>(
            string method,
            string url,
            Func<UnityWebRequest> createRequest,
            Func<UnityWebRequest, WebRequestResult<T>> parseResult,
            int retryCount,
            float retryDelaySeconds,
            CancellationToken cancellationToken)
        {
            int maxAttempts = Mathf.Max(1, retryCount + 1);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    using (UnityWebRequest request = createRequest())
                    {
                        await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            return parseResult(request);
                        }

                        bool canRetry = attempt + 1 < maxAttempts;
                        if (!canRetry)
                        {
                            return parseResult(request);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    bool canRetry = attempt + 1 < maxAttempts;
                    if (!canRetry)
                    {
                        return CreateUnexpectedFailure<T>(method, url, ex);
                    }
                }

                Debug.LogWarning($"WebRequest {method} retry. attempt: {attempt + 2}/{maxAttempts}");
                if (retryDelaySeconds > 0f)
                {
                    int delayMs = Mathf.Max(1, (int)(retryDelaySeconds * 1000f));
                    await UniTask.Delay(delayMs, cancellationToken: cancellationToken);
                }
            }

            return WebRequestResult<T>.Failure(-1, $"WebRequest {method} failed due to unknown retry state.", null);
        }

        private WebRequestResult<T> CreateUnexpectedFailure<T>(string method, string url, Exception exception)
        {
            string error = $"WebRequest {method} failed with exception. url: {url}\r\n{exception.GetType().Name}: {exception.Message}";
            Debug.LogError(error);
            return WebRequestResult<T>.Failure(-1, error, null);
        }
    }
}
#endif
