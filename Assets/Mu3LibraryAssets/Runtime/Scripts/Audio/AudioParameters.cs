using UnityEngine;

namespace Mu3Library.Audio
{
    [System.Serializable]
    public struct AudioParameters
    {
        public float Volume;
        public AudioBaseParameters Base;
        public Audio3dSoundSettings SoundSettings;

        public void ReadFromSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            Volume = source.volume;
            Base.ReadFromSource(source);
            SoundSettings.ReadFromSource(source);
        }
    }

    [System.Serializable]
    public struct AudioBaseParameters
    {
        public int Priority;
        public float Pitch;
        public float StereoPan;
        public float SpatialBlend;
        public float ReverbZoneMix;

        public void ReadFromSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            Priority = source.priority;
            Pitch = source.pitch;
            StereoPan = source.panStereo;
            SpatialBlend = source.spatialBlend;
            ReverbZoneMix = source.reverbZoneMix;
        }
    }

    [System.Serializable]
    public struct Audio3dSoundSettings
    {
        public float DopplerLevel;
        public float Spread;
        public AudioRolloffMode AudioRolloffMode;
        public AudioSourceCurveType? AudioSourceCurveType;
        public AnimationCurve AudioSourceCurve;
        public float MinDistance;
        public float MaxDistance;

        public void ReadFromSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            DopplerLevel = source.dopplerLevel;
            Spread = source.spread;
            AudioRolloffMode = source.rolloffMode;

            if (source.rolloffMode == AudioRolloffMode.Custom)
            {
                AudioSourceCurveType = UnityEngine.AudioSourceCurveType.CustomRolloff;
                AudioSourceCurve = source.GetCustomCurve(UnityEngine.AudioSourceCurveType.CustomRolloff);
            }
            else
            {
                AudioSourceCurveType = null;
                AudioSourceCurve = null;
            }

            MinDistance = source.minDistance;
            MaxDistance = source.maxDistance;
        }
    }
}

