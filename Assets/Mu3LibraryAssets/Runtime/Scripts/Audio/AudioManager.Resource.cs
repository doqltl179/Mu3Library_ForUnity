using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Audio
{
    public partial class AudioManager
    {
        private readonly Dictionary<string, AudioClip> _audioResourceMap = new();



        public void RegisterAudioResource(string key, AudioClip clip)
        {
            if (_audioResourceMap.ContainsKey(key))
            {
                Debug.LogWarning($"Audio resource with key '{key}' is already registered. Overwriting the existing resource.");
            }
            _audioResourceMap[key] = clip;
        }

        public void RegisterAudioResources(Dictionary<string, AudioClip> resources)
        {
            foreach (var kvp in resources)
            {
                RegisterAudioResource(kvp.Key, kvp.Value);
            }
        }

        private bool TryGetCachedAudioResource(string key, out AudioClip clip)
        {
            if (_audioResourceMap.TryGetValue(key, out clip))
            {
                return true;
            }
            Debug.LogWarning($"Audio resource with key '{key}' is not registered.");
            return false;
        }
    }
}