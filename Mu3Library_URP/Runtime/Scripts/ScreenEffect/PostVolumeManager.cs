using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.URP.ScreenEffect
{
    public class PostVolumeManager : IPostVolumeManager
    {




        public VolumeHandler<T> Wrap<T>(Volume volume) where T : VolumeComponent
        {
            if (!volume.profile.TryGet<T>(out var component))
            {
                component = volume.profile.Add<T>();
            }

            return new VolumeHandler<T>(volume, component, owned: false);
        }

        public VolumeHandler<T> Create<T>() where T : VolumeComponent, new()
            => Create<T>(0, true, null);

        public VolumeHandler<T> Create<T>(float priority) where T : VolumeComponent, new()
            => Create<T>(priority, true, null);

        public VolumeHandler<T> Create<T>(bool isGlobal) where T : VolumeComponent, new()
            => Create<T>(0, isGlobal, null);

        public VolumeHandler<T> Create<T>(Transform parent) where T : VolumeComponent, new()
            => Create<T>(0, true, parent);

        public VolumeHandler<T> Create<T>(float priority, bool isGlobal) where T : VolumeComponent, new()
            => Create<T>(priority, isGlobal, null);

        public VolumeHandler<T> Create<T>(float priority, Transform parent) where T : VolumeComponent, new()
            => Create<T>(priority, true, parent);

        public VolumeHandler<T> Create<T>(bool isGlobal, Transform parent) where T : VolumeComponent, new()
            => Create<T>(0, isGlobal, parent);

        public VolumeHandler<T> Create<T>(float priority, bool isGlobal, Transform parent) where T : VolumeComponent, new()
        {
            var go = new GameObject(typeof(T).Name);
            if (parent != null)
            {
                go.transform.SetParent(parent);
            }

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = isGlobal;
            volume.priority = priority;

            var component = volume.profile.Add<T>();
            return new VolumeHandler<T>(volume, component, owned: true);
        }
    }
}
