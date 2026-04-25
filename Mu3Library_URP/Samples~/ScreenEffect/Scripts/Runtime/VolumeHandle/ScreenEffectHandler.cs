using Mu3Library.URP.ScreenEffect;
using UnityEngine;

namespace Mu3Library.URP.Sample.ScreenEffect.VolumeHandle
{
    public abstract class ScreenEffectHandler<TEffect> : MonoBehaviour where TEffect : IScreenEffect, new()
    {
        protected IScreenEffectManager _screenEffectManager;
        protected Camera _camera;

        protected TEffect _effect;



        public void Context(IScreenEffectManager screenEffectManager, Camera camera)
        {
            _screenEffectManager = screenEffectManager;
            _camera = camera;
        }

        public void Init()
        {
            if (_effect == null)
            {
                var effect = new TEffect();

                _screenEffectManager.RegisterEffect(effect, CameraFilter);
                effect.SetActive(false);

                _effect = effect;
            }

            OnInit();
        }

        protected virtual void OnInit()
        {

        }

        public void RegisterEffect()
        {
            if (_screenEffectManager == null)
            {
                return;
            }

            if (_effect != null && !_effect.IsDisposed)
            {
                return;
            }

            var effect = new TEffect();

            _screenEffectManager.RegisterEffect(effect, CameraFilter);
            effect.SetActive(false);

            _effect = effect;
        }

        public void UnregisterEffect()
            => UnregisterEffect(false);

        public void UnregisterEffect(bool dispose)
        {
            if (_effect == null || _effect.IsDisposed)
            {
                return;
            }

            _screenEffectManager.UnregisterEffect(_effect, dispose);
        }

        #region UI Event
        public void SetActive(bool active) => _effect?.SetActive(active);
        #endregion

        protected virtual bool CameraFilter(Camera camera)
        {
            return _camera == null || camera == _camera;
        }
    }
}