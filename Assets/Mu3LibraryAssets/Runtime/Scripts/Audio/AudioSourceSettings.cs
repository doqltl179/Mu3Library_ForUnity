using UnityEngine;

namespace Mu3Library.Audio
{
    [System.Serializable]
    public struct AudioSourceSettings
    {
        public float Volume;
        public AudioBaseSettings BaseSettings;
        public Audio3dSoundSettings SoundSettings;

        public static readonly AudioSourceSettings Standard = new()
        {
            Volume = 1.0f,
            BaseSettings = AudioBaseSettings.Standard,
            SoundSettings = Audio3dSoundSettings.Standard,
        };

        public void ReadFromSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            Volume = source.volume;
            BaseSettings.ReadFromSource(source);
            SoundSettings.ReadFromSource(source);
        }
    }

    [System.Serializable]
    public struct AudioBaseSettings
    {
        public int Priority;
        public float Pitch;
        public float StereoPan;
        public float SpatialBlend;
        public float ReverbZoneMix;

        public static readonly AudioBaseSettings Standard = new()
        {
            Priority = 128,
            Pitch = 1.0f,
            StereoPan = 0.0f,
            SpatialBlend = 0.0f,
            ReverbZoneMix = 1.0f,
        };

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

        public static readonly Audio3dSoundSettings Standard = new()
        {
            DopplerLevel = 1.0f,
            Spread = 0.0f,
            AudioRolloffMode = AudioRolloffMode.Linear,
            MinDistance = 1.0f,
            MaxDistance = 500.0f,
        };

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

