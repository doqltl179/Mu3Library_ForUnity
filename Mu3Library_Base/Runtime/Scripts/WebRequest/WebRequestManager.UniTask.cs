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

            int maxAttempts = Mathf.Max(1, retryCount + 1);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                using (UnityWebRequest request = CreateGetRequest<T>(url))
                {
                    ApplyRequestOptions(request, requestHeaders, timeoutSeconds);

                    await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        return ParseResult<T>(url, request, "GET");
                    }

                    bool canRetry = attempt + 1 < maxAttempts;
                    if (!canRetry)
                    {
                        return ParseResult<T>(url, request, "GET");
                    }

                    Debug.LogWarning($"WebRequest GET retry. attempt: {attempt + 2}/{maxAttempts}");
                    if (retryDelaySeconds > 0f)
                    {
                        int delayMs = Mathf.Max(1, (int)(retryDelaySeconds * 1000f));
                        await UniTask.Delay(delayMs, cancellationToken: cancellationToken);
                    }
                }
            }

            return WebRequestResult<T>.Failure(-1, "WebRequest GET failed due to unknown retry state.", null);
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

            string payload = SerializeBody(body);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payload ?? string.Empty);

            int maxAttempts = Mathf.Max(1, retryCount + 1);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
                {
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = CreateDownloadHandler<TResponse>();
                    request.SetRequestHeader("Content-Type", contentType);
                    ApplyRequestOptions(request, requestHeaders, timeoutSeconds);

                    await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        return ParseResult<TResponse>(url, request, "POST");
                    }

                    bool canRetry = attempt + 1 < maxAttempts;
                    if (!canRetry)
                    {
                        return ParseResult<TResponse>(url, request, "POST");
                    }

                    Debug.LogWarning($"WebRequest POST retry. attempt: {attempt + 2}/{maxAttempts}");
                    if (retryDelaySeconds > 0f)
                    {
                        int delayMs = Mathf.Max(1, (int)(retryDelaySeconds * 1000f));
                        await UniTask.Delay(delayMs, cancellationToken: cancellationToken);
                    }
                }
            }

            return WebRequestResult<TResponse>.Failure(-1, "WebRequest POST failed due to unknown retry state.", null);
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

            int maxAttempts = Mathf.Max(1, retryCount + 1);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbHEAD))
                {
                    request.downloadHandler = new DownloadHandlerBuffer();
                    ApplyRequestOptions(request, requestHeaders, timeoutSeconds);

                    await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        return ParseDownloadSizeResult(url, request);
                    }

                    bool canRetry = attempt + 1 < maxAttempts;
                    if (!canRetry)
                    {
                        return ParseDownloadSizeResult(url, request);
                    }

                    Debug.LogWarning($"WebRequest HEAD retry. attempt: {attempt + 2}/{maxAttempts}");
                    if (retryDelaySeconds > 0f)
                    {
                        int delayMs = Mathf.Max(1, (int)(retryDelaySeconds * 1000f));
                        await UniTask.Delay(delayMs, cancellationToken: cancellationToken);
                    }
                }
            }

            return WebRequestResult<long>.Failure(-1, "WebRequest HEAD failed due to unknown retry state.", null);
        }
    }
}
#endif
