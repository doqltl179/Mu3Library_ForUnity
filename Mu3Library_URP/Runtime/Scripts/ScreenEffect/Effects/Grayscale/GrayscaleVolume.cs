using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.URP.ScreenEffect
{
    [Serializable, VolumeComponentMenu("Mu3Library/Grayscale")]
    public class GrayscaleVolume : VolumeComponent
    {
        [SerializeField] private ClampedFloatParameter _weight = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter Weight => _weight;

        public bool IsActive => active && _weight.value > 0;
    }
}
