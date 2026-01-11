#if MU3LIBRARY_UNITASK_SUPPORT
using System;
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
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("WebRequest HEAD failed. url is null or empty.");
                return -1;
            }

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbHEAD))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"WebRequest HEAD failed. url: {url}\r\n{request.error}");
                    return -1;
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

                return size;
            }
        }

        public async UniTask<T> GetAsync<T>(string url, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("WebRequest GET failed. url is null or empty.");
                return default;
            }

            using (UnityWebRequest request = CreateGetRequest<T>(url))
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"WebRequest GET failed. url: {url}\r\n{request.error}");
                    return default;
                }

                try
                {
                    return ParseResponse<T>(request);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"WebRequest GET parse failed. url: {url}\r\n{ex.Message}");
                    return default;
                }
            }
        }

        public async UniTask<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest body, string contentType = "application/json", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("WebRequest POST failed. url is null or empty.");
                return default;
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

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = CreateDownloadHandler<TResponse>();
                request.SetRequestHeader("Content-Type", contentType);

                await request.SendWebRequest().ToUniTask(cancellationToken: cancellationToken);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"WebRequest POST failed. url: {url}\r\n{request.error}");
                    return default;
                }

                try
                {
                    return ParseResponse<TResponse>(request);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"WebRequest POST parse failed. url: {url}\r\n{ex.Message}");
                    return default;
                }
            }
        }
    }
}
#endif
