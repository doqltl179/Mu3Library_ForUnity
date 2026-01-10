using System;

namespace Mu3Library.Resource
{
    public partial interface IResourceLoader
    {
        public bool IsCached<T>(string path) where T : class;

        public T Load<T>(string path) where T : class;
        public T[] LoadAll<T>(string path) where T : class;
        public void LoadAsync<T>(string path, Action<T> onLoaded) where T : class;
        public bool TryLoad<T>(string path, out T asset) where T : class;

        public bool Release<T>(string path) where T : class;
        public void ReleaseAll(string path);
        public void ReleaseAll();
    }
}
