using System;
using UnityEngine;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace Mu3Library.URP.ScreenEffect
{
    public abstract class VolumeHandler : IDisposable
    {
        public abstract bool Active { get; set; }



        public abstract void Dispose();
    }

    public class VolumeHandler<T> : VolumeHandler where T : VolumeComponent
    {
        private readonly Volume _volume;
        private readonly bool _owned;

        private T _component;
        public T Component => _component;

        private bool _active
        {
            get => _component.active;
            set => _component.active = value;
        }
        public override bool Active
        {
            get => _active;
            set => _active = value;
        }



        internal VolumeHandler(Volume volume, T component, bool owned)
        {
            _volume = volume;
            _component = component;
            _owned = owned;
        }

        public override void Dispose()
        {
            if (_owned)
            {
                Object.Destroy(_volume.gameObject);
            }
            else
            {
                Active = false;
            }
        }
    }
}