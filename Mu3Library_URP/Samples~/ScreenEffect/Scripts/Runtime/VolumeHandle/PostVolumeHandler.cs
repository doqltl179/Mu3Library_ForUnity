using Mu3Library.URP.ScreenEffect;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.URP.Sample.ScreenEffect.VolumeHandle
{
    public abstract class PostVolumeHandler<T> : MonoBehaviour where T : VolumeComponent
    {
        protected IPostVolumeManager _postVolumeManager;

        protected Volume _volume;
        protected VolumeHandler<T> _handler;



        #region Utility
        public void Context(IPostVolumeManager postVolumeManager, Volume volume)
        {
            var handler = postVolumeManager.Wrap<T>(volume);
            handler.Active = false;

            _handler = handler;
            _volume = volume;
            _postVolumeManager = postVolumeManager;
        }
        #endregion

        #region UI Event
        public void SetActiveVolume(bool active)
        {
            if (_handler == null)
            {
                return;
            }

            _handler.Active = active;
        }
        #endregion
    }
}