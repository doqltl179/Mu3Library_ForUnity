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

        public void GetWithResult<T>(string url, Action<WebRequestResult<T>> callback, IDictionary<string, string> requestHeaders = null, int timeoutSeconds = 0, int retryCount = 0)
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
                });
        }

        public void PostWithResult<TRequest, TResponse>(string url, TRequest body, Action<WebRequestResult<TResponse>> callback, string contentType = "application/json", IDictionary<string, string> requestHeaders = null, int timeoutSeconds = 0, int retryCount = 0)
        {
            if (string.IsNullOrEmpty(url))
            {
                string error = "WebRequest POST failed. url is null or empty.";
                Debug.LogError(error);
                callback?.Invoke(WebRequestResult<TResponse>.Failure(-1, error, null));
                return;
            }

            string payload = SerializeBody(body);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(payload ?? string.Empty);

            ExecuteWithRetry(
                method: UnityWebRequest.kHttpVerbPOST,
                retryCount: retryCount,
                createRequest: () =>
                {
                    UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = CreateDownloadHandler<TResponse>();
                    request.SetRequestHeader("Content-Type", contentType);
                    ApplyRequestOptions(request, requestHeaders, timeoutSeconds);
                    return request;
                },
                onComplete: request =>
                {
                    WebRequestResult<TResponse> result = ParseResult<TResponse>(url, request, "POST");
                    callback?.Invoke(result);
                });
        }

        public void GetDownloadSizeWithResult(string url, Action<WebRequestResult<long>> callback, IDictionary<string, string> requestHeaders = null, int timeoutSeconds = 0, int retryCount = 0)
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
                createRequest: () =>
                {
                    UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbHEAD);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    ApplyRequestOptions(request, requestHeaders, timeoutSeconds);
                    return request;
                },
                onComplete: request =>
                {
                    WebRequestResult<long> result = ParseDownloadSizeResult(url, request);
                    callback?.Invoke(result);
                });
        }
        #endregion

        private void ExecuteWithRetry(
            string method,
            int retryCount,
            Func<UnityWebRequest> createRequest,
            Action<UnityWebRequest> onComplete)
        {
            int maxAttempts = Mathf.Max(1, retryCount + 1);

            void SendAttempt(int attempt)
            {
                UnityWebRequest request = createRequest();
                request.SendWebRequest().completed += _ =>
                {
                    bool canRetry = request.result != UnityWebRequest.Result.Success && attempt + 1 < maxAttempts;
                    if (canRetry)
                    {
                        using (request)
                        {
                            Debug.LogWarning($"WebRequest {method} retry. attempt: {attempt + 2}/{maxAttempts}");
                        }

                        SendAttempt(attempt + 1);
                        return;
                    }

                    onComplete?.Invoke(request);
                    request.Dispose();
                };
            }

            SendAttempt(0);
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
                Debug.LogError(error);
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
