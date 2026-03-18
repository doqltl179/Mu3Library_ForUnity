using Mu3Library.Attribute;
using UnityEngine;

namespace Mu3Library.Audio
{
    [System.Serializable]
    public struct AudioSourceSettings
    {
        [SerializeField, Range(0.0f, 1.0f)] private float _volume;
        [SerializeField] private AudioBaseSettings _baseSettings;
        [SerializeField] private Audio3dSoundSettings _soundSettings;

        [Space(10)]
        [Tooltip("Number of times to play the clip.\n0 or less = infinite loop.\n1 = play once (no loop).")]
        [SerializeField] private int _loopCount;

        [Tooltip("Seconds to wait between each loop cycle. Ignored when loopCount is 1.")]
        [SerializeField, Min(0.0f)] private float _loopInterval;

        public float Volume
        {
            get => _volume;
            set => _volume = value;
        }
        public AudioBaseSettings BaseSettings
        {
            get => _baseSettings;
            set => _baseSettings = value;
        }
        public Audio3dSoundSettings SoundSettings
        {
            get => _soundSettings;
            set => _soundSettings = value;
        }
        /// <summary>
        /// Number of times to play the clip.
        /// 0 or less = infinite loop. 1 = play once (no loop).
        /// </summary>
        public int LoopCount
        {
            get => _loopCount;
            set => _loopCount = value;
        }
        /// <summary>
        /// Seconds to wait between each loop cycle. Ignored when LoopCount is 1.
        /// </summary>
        public float LoopInterval
        {
            get => _loopInterval;
            set => _loopInterval = Mathf.Max(0.0f, value);
        }

        /// <summary>
        /// Default settings. Infinite loop, no interval.
        /// </summary>
        public static readonly AudioSourceSettings Standard = new()
        {
            _volume = 1.0f,
            _baseSettings = AudioBaseSettings.Standard,
            _soundSettings = Audio3dSoundSettings.Standard,
            _loopCount = -1,
            _loopInterval = 0.0f,
        };

        /// <summary>
        /// One-shot settings. Plays once with no looping.
        /// </summary>
        public static readonly AudioSourceSettings OneShot = new()
        {
            _volume = 1.0f,
            _baseSettings = AudioBaseSettings.Standard,
            _soundSettings = Audio3dSoundSettings.Standard,
            _loopCount = 1,
            _loopInterval = 0.0f,
        };

        /// <summary>
        /// BGM: infinite loop, 2D (spatialBlend = 0).
        /// </summary>
        public static readonly AudioSourceSettings BgmStandard = new()
        {
            _volume = 1.0f,
            _baseSettings = AudioBaseSettings.Standard,
            _soundSettings = Audio3dSoundSettings.Standard,
            _loopCount = -1,
            _loopInterval = 0.0f,
        };

        /// <summary>
        /// BGM: infinite loop, 3D (spatialBlend = 1).
        /// </summary>
        public static readonly AudioSourceSettings BgmStandard3D = new()
        {
            _volume = 1.0f,
            _baseSettings = AudioBaseSettings.Standard,
            _soundSettings = Audio3dSoundSettings.Standard3D,
            _loopCount = -1,
            _loopInterval = 0.0f,
        };

        /// <summary>
        /// SFX: one-shot (plays once), 2D (spatialBlend = 0).
        /// </summary>
        public static readonly AudioSourceSettings SfxStandard = new()
        {
            _volume = 1.0f,
            _baseSettings = AudioBaseSettings.Standard,
            _soundSettings = Audio3dSoundSettings.Standard,
            _loopCount = 1,
            _loopInterval = 0.0f,
        };

        /// <summary>
        /// SFX: one-shot (plays once), 3D (spatialBlend = 1).
        /// </summary>
        public static readonly AudioSourceSettings SfxStandard3D = new()
        {
            _volume = 1.0f,
            _baseSettings = AudioBaseSettings.Standard,
            _soundSettings = Audio3dSoundSettings.Standard3D,
            _loopCount = 1,
            _loopInterval = 0.0f,
        };

        /// <summary>
        /// Environment: infinite loop, 2D (spatialBlend = 0).
        /// </summary>
        public static readonly AudioSourceSettings EnvironmentStandard = new()
        {
            _volume = 1.0f,
            _baseSettings = AudioBaseSettings.Standard,
            _soundSettings = Audio3dSoundSettings.Standard,
            _loopCount = -1,
            _loopInterval = 0.0f,
        };

        /// <summary>
        /// Environment: infinite loop, 3D (spatialBlend = 1).
        /// </summary>
        public static readonly AudioSourceSettings EnvironmentStandard3D = new()
        {
            _volume = 1.0f,
            _baseSettings = AudioBaseSettings.Standard,
            _soundSettings = Audio3dSoundSettings.Standard3D,
            _loopCount = -1,
            _loopInterval = 0.0f,
        };

        public void ReadFromSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            _volume = source.volume;
            _baseSettings.ReadFromSource(source);
            _soundSettings.ReadFromSource(source);
        }

        public void WriteToTarget(AudioSource target)
        {
            if (target == null)
            {
                return;
            }

            target.volume = _volume;
            _baseSettings.WriteToTarget(target);
            _soundSettings.WriteToTarget(target);
        }
    }

    [System.Serializable]
    public struct AudioBaseSettings
    {
        [SerializeField, Range(0, 256)] private int _priority;
        [SerializeField, Range(-3.0f, 3.0f)] private float _pitch;
        [SerializeField, Range(-1.0f, 1.0f)] private float _stereoPan;

        public int Priority
        {
            get => _priority;
            set => _priority = value;
        }
        public float Pitch
        {
            get => _pitch;
            set => _pitch = value;
        }
        public float StereoPan
        {
            get => _stereoPan;
            set => _stereoPan = value;
        }

        public static readonly AudioBaseSettings Standard = new()
        {
            _priority = 128,
            _pitch = 1.0f,
            _stereoPan = 0.0f,
        };

        public void ReadFromSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            _priority = source.priority;
            _pitch = source.pitch;
            _stereoPan = source.panStereo;
        }

        public void WriteToTarget(AudioSource target)
        {
            if (target == null)
            {
                return;
            }

            target.priority = _priority;
            target.pitch = _pitch;
            target.panStereo = _stereoPan;
        }
    }

    [System.Serializable]
    public struct Audio3dSoundSettings
    {
        [SerializeField, Range(0.0f, 5.0f)] private float _dopplerLevel;
        [SerializeField, Range(0.0f, 1.0f)] private float _spatialBlend;
        [SerializeField, Range(0.0f, 360.0f)] private float _spread;
        [SerializeField, Range(0.0f, 1.1f)] private float _reverbZoneMix;

        [Space(20)]
        [SerializeField, Min(0.0f)] private float _minDistance;
        [SerializeField, Min(0.0f)] private float _maxDistance;
        [SerializeField] private AudioRolloffMode _audioRolloffMode;

        [ConditionalHide(nameof(_audioRolloffMode), 2, true)]
        [SerializeField] private AnimationCurve _volumeCurve;

        public float DopplerLevel
        {
            get => _dopplerLevel;
            set => _dopplerLevel = value;
        }
        public float SpatialBlend
        {
            get => _spatialBlend;
            set => _spatialBlend = value;
        }
        public float Spread
        {
            get => _spread;
            set => _spread = value;
        }
        public float ReverbZoneMix
        {
            get => _reverbZoneMix;
            set => _reverbZoneMix = value;
        }
        public AudioRolloffMode AudioRolloffMode
        {
            get => _audioRolloffMode;
            set => _audioRolloffMode = value;
        }
        public AnimationCurve VolumeCurve
        {
            get => _volumeCurve;
            set => _volumeCurve = value;
        }
        public float MinDistance
        {
            get => _minDistance;
            set => _minDistance = value;
        }
        public float MaxDistance
        {
            get => _maxDistance;
            set => _maxDistance = value;
        }

        public static readonly Audio3dSoundSettings Standard = new()
        {
            _dopplerLevel = 1.0f,
            _spatialBlend = 0.0f,
            _spread = 0.0f,
            _reverbZoneMix = 1.0f,

            _minDistance = 1.0f,
            _maxDistance = 500.0f,
            _audioRolloffMode = AudioRolloffMode.Linear,

            _volumeCurve = new AnimationCurve(
                new Keyframe(0.0f, 1.0f),
                new Keyframe(1.0f, 0.0f)
            ),
        };

        /// <summary>
        /// Fully 3D spatial blend (spatialBlend = 1). Other values match <see cref="Standard"/>.
        /// </summary>
        public static readonly Audio3dSoundSettings Standard3D = new()
        {
            _dopplerLevel = 1.0f,
            _spatialBlend = 1.0f,
            _spread = 0.0f,
            _reverbZoneMix = 1.0f,

            _minDistance = 1.0f,
            _maxDistance = 500.0f,
            _audioRolloffMode = AudioRolloffMode.Linear,

            _volumeCurve = new AnimationCurve(
                new Keyframe(0.0f, 1.0f),
                new Keyframe(1.0f, 0.0f)
            ),
        };

        public void ReadFromSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            _dopplerLevel = source.dopplerLevel;
            _spatialBlend = source.spatialBlend;
            _spread = source.spread;
            _reverbZoneMix = source.reverbZoneMix;

            _minDistance = source.minDistance;
            _maxDistance = source.maxDistance;
            _audioRolloffMode = source.rolloffMode;

            _volumeCurve = source.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
        }

        public void WriteToTarget(AudioSource target)
        {
            if (target == null)
            {
                return;
            }

            target.dopplerLevel = _dopplerLevel;
            target.spatialBlend = _spatialBlend;
            target.spread = _spread;
            target.reverbZoneMix = _reverbZoneMix;

            target.minDistance = _minDistance;
            target.maxDistance = _maxDistance;
            target.rolloffMode = _audioRolloffMode;

            if (_audioRolloffMode == AudioRolloffMode.Custom)
            {
                target.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _volumeCurve);
            }
        }
    }
}
