using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Audio
{
    public partial interface IAudioManager
    {
        public void RegisterAudioResource(string key, AudioClip clip);
        public void RegisterAudioResources(Dictionary<string, AudioClip> resources);
    }
}
