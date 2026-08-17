using UnityEngine.Audio;
using Mu3Library.Preference;

namespace Mu3Library.Audio
{
    public partial interface IAudioManager
    {
        public void Stop();
        public void Pause();
        public void UnPause();

        public void SetMixerGroups(AudioMixerGroup bgmGroup, AudioMixerGroup sfxGroup, AudioMixerGroup environmentGroup);

        public void SaveVolumes(IPlayerPrefsLoader prefs, string keyPrefix = "Mu3.Audio", bool saveImmediately = true);
        public void LoadVolumes(IPlayerPrefsLoader prefs, string keyPrefix = "Mu3.Audio");
    }
}
