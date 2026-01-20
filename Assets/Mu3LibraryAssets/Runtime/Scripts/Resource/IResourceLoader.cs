using System;

using Object = UnityEngine.Object;

namespace Mu3Library.Resource
{
    public partial interface IResourceLoader
    {
        public bool IsCached<T>(string path) where T : Object;

        public T Load<T>(string path) where T : Object;
        public T[] LoadAll<T>(string path) where T : Object;
        public void LoadAsync<T>(string path, Action<T> onLoaded) where T : Object;
        public bool TryLoad<T>(string path, out T asset) where T : Object;

        public bool Release<T>(string path) where T : Object;
        public void ReleaseAll(string path);
        public void ReleaseAll();
    }
}
