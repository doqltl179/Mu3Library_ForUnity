using System;
using UnityEngine;

namespace Mu3Library.URP.ScreenEffect
{
    public interface IScreenEffectManager
    {
        public IScreenEffect RegisterEffect<TEffect>(Func<Camera, bool> cameraFilter = null) where TEffect : IScreenEffect, new();
        public void RegisterEffect(IScreenEffect effect, Func<Camera, bool> cameraFilter = null);

        public void UnregisterEffect(IScreenEffect effect);
        public void UnregisterEffect(IScreenEffect effect, bool dispose);

        public void UnregisterEffectAll<TEffect>() where TEffect : IScreenEffect;
        public void UnregisterEffectAll<TEffect>(bool dispose) where TEffect : IScreenEffect;
        public void UnregisterEffectAll(Type effectType);
        public void UnregisterEffectAll(Type effectType, bool dispose);
    }
}
