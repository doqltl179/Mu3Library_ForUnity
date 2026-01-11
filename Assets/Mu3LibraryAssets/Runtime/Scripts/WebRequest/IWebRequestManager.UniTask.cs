#if MU3LIBRARY_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Mu3Library.WebRequest
{
    public partial interface IWebRequestManager
    {
        public UniTask<T> GetAsync<T>(string url, CancellationToken cancellationToken = default);
        public UniTask<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest body, string contentType = "application/json", CancellationToken cancellationToken = default);
        public UniTask<long> GetDownloadSizeAsync(string url, CancellationToken cancellationToken = default);
    }
}
#endif
