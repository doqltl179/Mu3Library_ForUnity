using System;
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
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("WebRequest HEAD failed. url is null or empty.");
                callback?.Invoke(-1);
                return;
            }

            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbHEAD);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SendWebRequest().completed += _ => HandleDownloadSize(url, request, callback);
        }

        /// <summary>
        /// Sends a GET request to the specified URL.
        /// </summary>
        /// <typeparam name="T">Response type (string, byte[], Texture2D, or JSON-serializable type).</typeparam>
        /// <param name="url">The URL to request.</param>
        /// <param name="callback">Callback with the parsed response.</param>
        public void Get<T>(string url, Action<T> callback)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("WebRequest GET failed. url is null or empty.");
                callback?.Invoke(default);
                return;
            }

            UnityWebRequest request = CreateGetRequest<T>(url);
            request.SendWebRequest().completed += _ => HandleResponse(url, request, callback, "GET");
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
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("WebRequest POST failed. url is null or empty.");
                callback?.Invoke(default);
                return;
            }

            string payload;
            if (body == null)
            {
                payload = string.Empty;
            }
            else if (body is string bodyString)
            {
                payload = bodyString;
            }
            else
            {
                payload = JsonUtility.ToJson(body);
            }

            byte[] bodyRaw = Encoding.UTF8.GetBytes(payload ?? string.Empty);

            UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = CreateDownloadHandler<TResponse>();
            request.SetRequestHeader("Content-Type", contentType);
            request.SendWebRequest().completed += _ => HandleResponse(url, request, callback, "POST");
        }
        #endregion

        private void HandleResponse<T>(string url, UnityWebRequest request, Action<T> callback, string method)
        {
            using (request)
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"WebRequest {method} failed. url: {url}\r\n{request.error}");
                    callback?.Invoke(default);
                    return;
                }

                try
                {
                    T result = ParseResponse<T>(request);
                    callback?.Invoke(result);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"WebRequest {method} parse failed. url: {url}\r\n{ex.Message}");
                    callback?.Invoke(default);
                }
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

        private void HandleDownloadSize(string url, UnityWebRequest request, Action<long> callback)
        {
            using (request)
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"WebRequest HEAD failed. url: {url}\r\n{request.error}");
                    callback?.Invoke(-1);
                    return;
                }

                long size = -1;
                var headers = request.GetResponseHeaders();
                if (headers != null && headers.TryGetValue("Content-Length", out string lengthValue))
                {
                    if (!long.TryParse(lengthValue, out size))
                    {
                        size = -1;
                    }
                }

                callback?.Invoke(size);
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
