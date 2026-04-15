using Mu3Library.URP.ScreenEffect;
using UnityEngine;

namespace Mu3Library.URP.Sample.ScreenEffect.VolumeHandle
{
    public abstract class PostVolumeHandler<TEffect> : MonoBehaviour where TEffect : IPassInjector
    {
        protected TEffect _effect;



        public void Init(TEffect effect)
        {
            _effect = effect;
        }

        #region UI Event
        public void SetActiveVolume(bool active)
        {
            OnSetActive(active);
        }
        #endregion

        protected abstract void OnSetActive(bool active);
    }
}