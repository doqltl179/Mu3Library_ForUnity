using System.Collections;
using UnityEngine;

namespace Mu3Library.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioController : MonoBehaviour
    {
        private AudioSource _source;
        public AudioSource Source => _source;

        private float _clipVolume = 1.0f;
        public float ClipVolume => _clipVolume;

        public bool IsPlaying => _source.isPlaying;

        public float NormalizedTime => _source.time / _source.clip.length;

        private IEnumerator _fadeCoroutine = null;



        private void Awake()
        {
            if (_source == null)
            {
                _source = GetComponent<AudioSource>();
            }
        }

        #region Utility
        public void FadeOut(float fadeTime = 1.0f)
        {

        }

        public void FadeIn(float fadeTime = 1.0f)
        {

        }

        public void SetVolume(float clipVolume, float offset)
        {
            _source.volume = clipVolume * offset;

            _clipVolume = clipVolume;
        }

        public void RecalculateVolume(float offset)
        {
            _source.volume = _clipVolume * offset;
        }

        public void SetClip(AudioClip clip)
        {
            _source.clip = clip;
        }

        public void Play() => _source.Play();
        public void Stop() => _source.Stop();
        public void Pause() => _source.Pause();
        public void UnPause() => _source.UnPause();

        public void SetAudioParameters(AudioBaseParameters parameters)
        {
            _source.priority = parameters.Priority;
            _source.pitch = parameters.Pitch;
            _source.panStereo = parameters.StereoPan;
            _source.spatialBlend = parameters.SpatialBlend;
            _source.reverbZoneMix = parameters.ReverbZoneMix;
        }

        public void SetAudioParameters(Audio3dSoundSettings parameters)
        {
            _source.dopplerLevel = parameters.DopplerLevel;
            _source.spread = parameters.Spread;

            if (parameters.AudioSourceCurveType != null && parameters.AudioSourceCurve != null)
            {
                _source.SetCustomCurve(parameters.AudioSourceCurveType.Value, parameters.AudioSourceCurve);
            }
            else
            {
                if (parameters.AudioSourceCurve != null)
                {
                    _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, parameters.AudioSourceCurve);
                }
                else
                {
                    _source.rolloffMode = AudioRolloffMode.Linear;
                }
            }

            _source.minDistance = parameters.MinDistance;
            _source.maxDistance = parameters.MaxDistance;
        }

        public void SetActive(bool value)
        {
            _source.gameObject.SetActive(value);
        }
        #endregion
    }
}