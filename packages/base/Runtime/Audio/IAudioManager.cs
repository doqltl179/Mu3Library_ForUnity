using UnityEngine.Audio;
using Mu3Library.Preference;
using Mu3Library.Utility;

namespace Mu3Library.Audio
{
    public partial interface IAudioManager
    {
        /// <summary>
        /// The clock that fades, ducks, loop intervals, and the gap between playlist tracks advance
        /// on. Defaults to <see cref="TimeMode.Unscaled"/>, which keeps them running while
        /// <c>Time.timeScale</c> is 0.
        /// </summary>
        public TimeMode TimeMode { get; set; }

        public void Stop();
        public void Pause();
        public void UnPause();

        public void SetMixerGroups(AudioMixerGroup bgmGroup, AudioMixerGroup sfxGroup, AudioMixerGroup environmentGroup);

        public void SaveVolumes(IPlayerPrefsLoader prefs, string keyPrefix = "Mu3.Audio", bool saveImmediately = true);
        public void LoadVolumes(IPlayerPrefsLoader prefs, string keyPrefix = "Mu3.Audio");
    }
}
