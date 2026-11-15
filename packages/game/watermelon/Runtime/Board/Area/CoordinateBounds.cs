using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Area
{
    /// <summary>
    /// An axis aligned XY rectangle described by its min and max corner.
    /// <br/> Every board coordinate space(local, screen, world) is stored in this form.
    /// </summary>
    public readonly struct CoordinateBounds
    {
        public CoordinateBounds(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
        }

        public Vector2 Min { get; }
        public Vector2 Max { get; }
        public Vector2 Size => Max - Min;
        public Vector2 Center => (Min + Max) * 0.5f;
        public Rect Rect => Rect.MinMaxRect(Min.x, Min.y, Max.x, Max.y);

        public Vector2 Lerp(Vector2 normalizedPosition)
            => new Vector2(Mathf.Lerp(Min.x, Max.x, normalizedPosition.x), Mathf.Lerp(Min.y, Max.y, normalizedPosition.y));

        public Vector2 Normalize(Vector2 position)
            => new Vector2(SafeInverseLerp(Min.x, Max.x, position.x), SafeInverseLerp(Min.y, Max.y, position.y));

        public Vector3 Clamp(Vector3 position)
            => new Vector3(Mathf.Clamp(position.x, Min.x, Max.x), Mathf.Clamp(position.y, Min.y, Max.y), position.z);

        public bool Contains(Vector2 position, bool includeBoundary)
            => includeBoundary
                ? Min.x <= position.x && position.x <= Max.x && Min.y <= position.y && position.y <= Max.y
                : Min.x < position.x && position.x < Max.x && Min.y < position.y && position.y < Max.y;

        private static float SafeInverseLerp(float min, float max, float value)
            => Mathf.Abs(max - min) <= Mathf.Epsilon ? 0.0f : Mathf.InverseLerp(min, max, value);
    }
}
