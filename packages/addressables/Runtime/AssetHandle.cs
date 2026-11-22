using System;

namespace Mu3Library.Addressable
{
    /// <summary>
    /// One caller's share of an asset that <see cref="IAddressablesManager.LoadAssetHandle{T}"/>
    /// loaded. The manager counts the handles of each key and type and releases the cached asset when
    /// the last one is disposed, so callers that load the same asset keep it for one another. Dispose
    /// a handle once the caller no longer uses the asset; disposing it again counts nothing.
    /// </summary>
    /// <remarks>
    /// A handle that <see cref="IAddressablesManager.LoadAssetHandle{T}"/> hands for a load that failed
    /// holds no asset and is not valid; the UniTask form throws for such a load instead. <see cref="IAddressablesManager.ReleaseCachedAsset"/>
    /// and <see cref="IAddressablesManager.ClearCache"/> release the asset whatever handles remain,
    /// which then read not valid, and so does disposing the manager.
    /// </remarks>
    public sealed class AssetHandle<T> : IDisposable where T : class
    {
        private readonly AddressablesManager _manager;
        private readonly object _cacheKey;
        private readonly int _generation;
        private readonly T _asset;
        private bool _isDisposed;

        /// <summary>The key the asset was loaded with.</summary>
        public object Key { get; }

        /// <summary>
        /// True until this handle is disposed or the manager releases the asset another way; false
        /// for a load that failed.
        /// </summary>
        public bool IsValid => !_isDisposed && _manager != null && _manager.IsHandleCurrent(_cacheKey, _generation);

        /// <summary>The loaded asset while <see cref="IsValid"/>, otherwise null.</summary>
        public T Asset => IsValid ? _asset : null;



        internal AssetHandle(object key)
        {
            Key = key;
        }

        internal AssetHandle(AddressablesManager manager, object key, object cacheKey, int generation, T asset)
        {
            _manager = manager;
            Key = key;
            _cacheKey = cacheKey;
            _generation = generation;
            _asset = asset;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _manager?.ReleaseHandle(Key, _cacheKey, _generation);
        }
    }
}
