using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Mu3Library.WebRequest
{
    public partial class WebRequestManager : IWebRequestManager
    {



        #region Utility
        public void GetDownloadSize(string url, Action<long> callback = null)
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

        public void Get<T>(string url, Action<T> callback = null)
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

        public void Post<TRequest, TResponse>(string url, TRequest body, Action<TResponse> callback = null, string contentType = "application/json")
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
