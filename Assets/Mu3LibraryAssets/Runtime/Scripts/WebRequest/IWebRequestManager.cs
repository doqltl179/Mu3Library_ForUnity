using System;

namespace Mu3Library.WebRequest
{
    public partial interface IWebRequestManager
    {
        public void Get<T>(string url, Action<T> callback = null);
        public void Post<TRequest, TResponse>(string url, TRequest body, Action<TResponse> callback = null, string contentType = "application/json");
        public void GetDownloadSize(string url, Action<long> callback = null);
    }
}
