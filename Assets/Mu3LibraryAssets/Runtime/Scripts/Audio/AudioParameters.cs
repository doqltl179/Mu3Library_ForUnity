using UnityEngine;

namespace Mu3Library.Audio
{
    [System.Serializable]
    public struct AudioParameters
    {
        public float Volume;
        public AudioBaseParameters Base;
        public Audio3dSoundSettings SoundSettings;
    }

    [System.Serializable]
    public struct AudioBaseParameters
    {
        public int Priority;
        public float Pitch;
        public float StereoPan;
        public float SpatialBlend;
        public float ReverbZoneMix;
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
    }
}