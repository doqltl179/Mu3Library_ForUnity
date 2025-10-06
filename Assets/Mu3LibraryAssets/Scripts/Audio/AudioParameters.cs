using UnityEngine;

namespace Mu3Library.Audio
{
    public struct AudioParameters
    {
        public float Volume;
        public AudioBaseParameters Base;
        public Audio3dSoundSettings SoundSettings;
    }

    public struct AudioBaseParameters
    {
        public int Priority;
        public float Pitch;
        public float StereoPan;
        public float SpatialBlend;
        public float ReverbZoneMix;
    }

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