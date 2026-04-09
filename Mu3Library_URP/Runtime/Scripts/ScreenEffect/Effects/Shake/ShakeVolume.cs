using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mu3Library.URP.ScreenEffect
{
    [Serializable, VolumeComponentMenu("Mu3Library/Shake")]
    public class ShakeVolume : VolumeComponent
    {
        [SerializeField] private ClampedFloatParameter _weight = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter Weight => _weight;

        /// <summary>
        /// <br/> max: 화면의 최대 10% 까지만 흔들리도록 설정
        /// </summary>
        [SerializeField] private ClampedFloatParameter _amplitude = new ClampedFloatParameter(0.1f, 0f, 0.1f);
        public ClampedFloatParameter Amplitude => _amplitude;

        public bool IsActive => active && _weight.value > 0 && _amplitude.value > 0;
    }
}
