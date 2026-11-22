using System;
using Mu3Library.DI;
using UnityEngine;

namespace Mu3Library.Multiplayer.Sync
{
    /// <summary>
    /// Syncs a transform's position and rotation: its owner sends them quantized, and every other
    /// player moves its own transform smoothly between the updates it receives,
    /// <see cref="InterpolationDelaySeconds"/> behind them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A position travels as whole steps of <see cref="PositionPrecision"/> on each axis, each a
    /// variable-length number of one to five bytes, about three for a kilometre at the default
    /// millimetre, and a rotation as its three smallest components of ten bits each with the index
    /// of the largest, four bytes, about 0.16 degrees apart. A change smaller than a step sends
    /// nothing, and an update that changed only the position or only the rotation sends that
    /// one alone. Register it as <see cref="Data.DeliveryMode.Unreliable"/>, the default, so
    /// the whole state repeats every <see cref="StateSyncConfig.FullStateIntervalSeconds"/>.
    /// </para>
    /// <para>
    /// Its owner reads the target as the manager asks whether it changed, once a tick. Once a copy
    /// received an update it follows the updates and no longer reads the target; call
    /// <see cref="Update"/> every frame to move the target there. On the host, a copy of a
    /// client's transform sends the latest update it received on to the other players, not the
    /// position it shows. Give it the clock the <see cref="StateSyncManager"/> uses.
    /// </para>
    /// </remarks>
    public sealed class TransformSync : ISyncState, IUpdatable
    {
        public const float DefaultPositionPrecision = 0.001f;
        public const float DefaultInterpolationDelaySeconds = 0.1f;

        private const int SnapshotCapacity = 32;
        private const byte PositionBit = 1;
        private const byte RotationBit = 2;
        private const uint ComponentMask = (1u << 10) - 1u;
        private const float ComponentSteps = ComponentMask;
        private const float Sqrt2 = 1.41421356f;
        private const float InverseSqrt2 = 0.70710678f;

        // How far the estimate of the sender's clock moves toward a slower update, each update; a
        // faster one moves it at once.
        private const double OffsetDrift = 0.02;

        private struct Snapshot
        {
            public double Time;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private readonly Transform _target;
        private readonly float _precision;
        private readonly float _delay;
        private readonly bool _useLocalSpace;
        private readonly Func<double> _clock;

        // What this state sends: read from the target on its owner, the latest update on a copy.
        private int _x;
        private int _y;
        private int _z;
        private uint _rotation;

        // What the last tick took, which the next change is measured from.
        private bool _hasSent = false;
        private int _sentX;
        private int _sentY;
        private int _sentZ;
        private uint _sentRotation;

        private readonly Snapshot[] _snapshots = new Snapshot[SnapshotCapacity];
        private int _first = 0;
        private int _count = 0;

        // The receive time minus the sender's tick time, at its smallest, which turns a tick into this player's clock.
        private bool _hasOffset = false;
        private double _offset;

        public uint Id { get; }
        public Transform Target => _target;
        public float PositionPrecision => _precision;
        public float InterpolationDelaySeconds => _delay;
        public bool UseLocalSpace => _useLocalSpace;

        /// <summary>True once this state received an update: it follows another player's transform.</summary>
        public bool IsCopy { get; private set; } = false;

        private double Now => _clock != null ? _clock() : Time.realtimeSinceStartupAsDouble;



        /// <param name="positionPrecision">The step a position travels in, above 0.</param>
        /// <param name="interpolationDelaySeconds">How far behind the updates a copy shows the transform, 0 or more, so late updates still arrive in time.</param>
        /// <param name="useLocalSpace">Sync the local position and rotation rather than the world ones.</param>
        /// <param name="clock">The clock of the <see cref="StateSyncManager"/>; null uses <c>Time.realtimeSinceStartupAsDouble</c>.</param>
        public TransformSync(uint id, Transform target, float positionPrecision = DefaultPositionPrecision,
            float interpolationDelaySeconds = DefaultInterpolationDelaySeconds, bool useLocalSpace = false, Func<double> clock = null)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!(positionPrecision > 0f) || float.IsInfinity(positionPrecision))
            {
                throw new ArgumentOutOfRangeException(nameof(positionPrecision), positionPrecision, "The precision must be a finite number above 0.");
            }

            if (!(interpolationDelaySeconds >= 0f) || float.IsInfinity(interpolationDelaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(interpolationDelaySeconds), interpolationDelaySeconds, "The delay must be a finite number of seconds, 0 or more.");
            }

            Id = id;
            _target = target;
            _precision = positionPrecision;
            _delay = interpolationDelaySeconds;
            _useLocalSpace = useLocalSpace;
            _clock = clock;
        }

        #region Sending
        public bool IsDirty
        {
            get
            {
                if (IsCopy)
                {
                    return false;
                }

                Capture();
                return !_hasSent || PositionChanged || RotationChanged;
            }
        }

        private bool PositionChanged => _x != _sentX || _y != _sentY || _z != _sentZ;
        private bool RotationChanged => _rotation != _sentRotation;

        public void WriteFull(MessageWriter writer)
        {
            if (!IsCopy)
            {
                Capture();
            }

            WritePosition(writer);
            writer.WriteUInt(_rotation);
        }

        public void WriteDelta(MessageWriter writer)
        {
            if (!IsCopy)
            {
                Capture();
            }

            byte changes = 0;
            if (!_hasSent || PositionChanged)
            {
                changes |= PositionBit;
            }

            if (!_hasSent || RotationChanged)
            {
                changes |= RotationBit;
            }

            writer.WriteByte(changes);
            if ((changes & PositionBit) != 0)
            {
                WritePosition(writer);
            }

            if ((changes & RotationBit) != 0)
            {
                writer.WriteUInt(_rotation);
            }
        }

        public void ClearDirty()
        {
            _hasSent = true;
            _sentX = _x;
            _sentY = _y;
            _sentZ = _z;
            _sentRotation = _rotation;
        }

        private void Capture()
        {
            Vector3 position = _useLocalSpace ? _target.localPosition : _target.position;
            Quaternion rotation = _useLocalSpace ? _target.localRotation : _target.rotation;
            _x = Quantize(position.x);
            _y = Quantize(position.y);
            _z = Quantize(position.z);
            _rotation = PackRotation(rotation);
        }

        private void WritePosition(MessageWriter writer)
        {
            WriteVarInt(writer, _x);
            WriteVarInt(writer, _y);
            WriteVarInt(writer, _z);
        }
        #endregion

        #region Receiving
        public void ReadFull(ref MessageReader reader, in SyncReadContext context)
        {
            int x = ReadVarInt(ref reader);
            int y = ReadVarInt(ref reader);
            int z = ReadVarInt(ref reader);
            uint rotation = reader.ReadUInt();
            Accept(x, y, z, rotation, context);
        }

        public void ReadDelta(ref MessageReader reader, in SyncReadContext context)
        {
            byte changes = reader.ReadByte();
            if ((changes & ~(PositionBit | RotationBit)) != 0)
            {
                throw new PayloadFormatException($"A transform update holds unknown changes {changes}.");
            }

            int x = _x;
            int y = _y;
            int z = _z;
            uint rotation = _rotation;
            if ((changes & PositionBit) != 0)
            {
                x = ReadVarInt(ref reader);
                y = ReadVarInt(ref reader);
                z = ReadVarInt(ref reader);
            }

            if ((changes & RotationBit) != 0)
            {
                rotation = reader.ReadUInt();
            }

            Accept(x, y, z, rotation, context);
        }

        /// <summary>Takes an update read whole, so a malformed one changes nothing.</summary>
        private void Accept(int x, int y, int z, uint rotation, in SyncReadContext context)
        {
            IsCopy = true;
            _x = x;
            _y = y;
            _z = z;
            _rotation = rotation;

            Snapshot snapshot = new Snapshot
            {
                Time = context.Tick * (double)context.TickIntervalSeconds,
                Position = new Vector3(x * _precision, y * _precision, z * _precision),
                Rotation = UnpackRotation(rotation),
            };

            if (_count > 0)
            {
                ref Snapshot newest = ref _snapshots[(_first + _count - 1) % SnapshotCapacity];
                if (snapshot.Time < newest.Time)
                {
                    return;
                }

                if (snapshot.Time == newest.Time)
                {
                    newest = snapshot;
                    return;
                }
            }

            if (_count == SnapshotCapacity)
            {
                _first = (_first + 1) % SnapshotCapacity;
                _count--;
            }

            _snapshots[(_first + _count) % SnapshotCapacity] = snapshot;
            _count++;

            double sample = context.ReceivedAt - snapshot.Time;
            if (!_hasOffset || sample < _offset)
            {
                _offset = sample;
                _hasOffset = true;
            }
            else
            {
                _offset += (sample - _offset) * OffsetDrift;
            }

            if (_count == 1)
            {
                Apply(snapshot.Position, snapshot.Rotation);
            }
        }

        /// <summary>
        /// On a copy, moves the target to where the updates put it <see cref="InterpolationDelaySeconds"/>
        /// ago, between the two updates around that time, or to the newest one once the updates
        /// stop. On the owner, does nothing.
        /// </summary>
        public void Update()
        {
            if (!IsCopy || _count == 0)
            {
                return;
            }

            double time = Now - _offset - _delay;
            ref Snapshot oldest = ref _snapshots[_first];
            ref Snapshot newest = ref _snapshots[(_first + _count - 1) % SnapshotCapacity];
            if (time >= newest.Time)
            {
                Apply(newest.Position, newest.Rotation);
                return;
            }

            if (time <= oldest.Time)
            {
                Apply(oldest.Position, oldest.Rotation);
                return;
            }

            for (int i = 0; i < _count - 1; i++)
            {
                ref Snapshot from = ref _snapshots[(_first + i) % SnapshotCapacity];
                ref Snapshot to = ref _snapshots[(_first + i + 1) % SnapshotCapacity];
                if (time < to.Time)
                {
                    float t = (float)((time - from.Time) / (to.Time - from.Time));
                    Apply(Vector3.Lerp(from.Position, to.Position, t), Quaternion.Slerp(from.Rotation, to.Rotation, t));

                    // The updates before the one it starts from are behind it for good.
                    _first = (_first + i) % SnapshotCapacity;
                    _count -= i;
                    return;
                }
            }
        }

        private void Apply(Vector3 position, Quaternion rotation)
        {
            if (_useLocalSpace)
            {
                _target.localPosition = position;
                _target.localRotation = rotation;
            }
            else
            {
                _target.SetPositionAndRotation(position, rotation);
            }
        }
        #endregion

        #region Quantization
        private int Quantize(float value)
        {
            double steps = Math.Round(value / (double)_precision);
            if (double.IsNaN(steps))
            {
                return 0;
            }

            return steps >= int.MaxValue ? int.MaxValue : steps <= int.MinValue ? int.MinValue : (int)steps;
        }

        /// <summary>The three smallest components of the unit rotation, ten bits each, below the index of the largest.</summary>
        internal static uint PackRotation(Quaternion rotation)
        {
            float x = rotation.x;
            float y = rotation.y;
            float z = rotation.z;
            float w = rotation.w;
            float length = Mathf.Sqrt(x * x + y * y + z * z + w * w);
            if (!(length > 0f) || float.IsInfinity(length))
            {
                x = 0f;
                y = 0f;
                z = 0f;
                w = 1f;
                length = 1f;
            }

            x /= length;
            y /= length;
            z /= length;
            w /= length;

            uint largest = 0;
            float max = Mathf.Abs(x);
            if (Mathf.Abs(y) > max)
            {
                largest = 1;
                max = Mathf.Abs(y);
            }

            if (Mathf.Abs(z) > max)
            {
                largest = 2;
                max = Mathf.Abs(z);
            }

            if (Mathf.Abs(w) > max)
            {
                largest = 3;
            }

            float a;
            float b;
            float c;
            float sign;
            switch (largest)
            {
                case 0: a = y; b = z; c = w; sign = x; break;
                case 1: a = x; b = z; c = w; sign = y; break;
                case 2: a = x; b = y; c = w; sign = z; break;
                default: a = x; b = y; c = z; sign = w; break;
            }

            // q and -q are the same rotation, so the largest component travels as positive.
            if (sign < 0f)
            {
                a = -a;
                b = -b;
                c = -c;
            }

            return (largest << 30) | (EncodeComponent(a) << 20) | (EncodeComponent(b) << 10) | EncodeComponent(c);
        }

        internal static Quaternion UnpackRotation(uint packed)
        {
            float a = DecodeComponent((packed >> 20) & ComponentMask);
            float b = DecodeComponent((packed >> 10) & ComponentMask);
            float c = DecodeComponent(packed & ComponentMask);
            float largest = Mathf.Sqrt(Mathf.Max(0f, 1f - a * a - b * b - c * c));
            Quaternion rotation;
            switch (packed >> 30)
            {
                case 0: rotation = new Quaternion(largest, a, b, c); break;
                case 1: rotation = new Quaternion(a, largest, b, c); break;
                case 2: rotation = new Quaternion(a, b, largest, c); break;
                default: rotation = new Quaternion(a, b, c, largest); break;
            }

            return Quaternion.Normalize(rotation);
        }

        // The smaller three components of a unit quaternion lie within plus and minus 1 / sqrt(2).
        private static uint EncodeComponent(float value)
        {
            float normalized = Mathf.Clamp01((value * Sqrt2 + 1f) * 0.5f);
            return (uint)Mathf.RoundToInt(normalized * ComponentSteps);
        }

        private static float DecodeComponent(uint bits)
        {
            return (bits / ComponentSteps * 2f - 1f) * InverseSqrt2;
        }

        private static void WriteVarInt(MessageWriter writer, int value)
        {
            uint zigzag = unchecked((uint)((value << 1) ^ (value >> 31)));
            while (zigzag >= 0x80u)
            {
                writer.WriteByte((byte)(zigzag | 0x80u));
                zigzag >>= 7;
            }

            writer.WriteByte((byte)zigzag);
        }

        private static int ReadVarInt(ref MessageReader reader)
        {
            uint value = 0;
            for (int shift = 0; shift <= 28; shift += 7)
            {
                byte next = reader.ReadByte();
                if (shift == 28 && (next & 0xF0) != 0)
                {
                    throw new PayloadFormatException("A transform update holds a number past 32 bits.");
                }

                value |= (uint)(next & 0x7F) << shift;
                if ((next & 0x80) == 0)
                {
                    return unchecked((int)(value >> 1) ^ -(int)(value & 1u));
                }
            }

            throw new PayloadFormatException("A transform update holds a number past 32 bits.");
        }
        #endregion
    }
}
