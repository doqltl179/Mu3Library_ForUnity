using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Area
{
    /// <summary>
    /// The board rectangle expressed in every coordinate space at once.
    /// <br/> An instance is only produced by <see cref="BoardAreaBoundsCalculator"/>, a default
    /// <br/> instance means the board has not been calculated yet(<see cref="IsValid"/>).
    /// </summary>
    public readonly struct BoardAreaBounds
    {
        public BoardAreaBounds(CoordinateBounds local, CoordinateBounds screen, CoordinateBounds world)
        {
            Local = local;
            Screen = screen;
            World = world;
            IsValid = true;
        }

        /// <summary>
        /// The board rectangle in the board's local XY space.
        /// </summary>
        public CoordinateBounds Local { get; }

        /// <summary>
        /// The board rectangle in screen pixels.
        /// </summary>
        public CoordinateBounds Screen { get; }

        /// <summary>
        /// The board rectangle in world XY space.
        /// </summary>
        public CoordinateBounds World { get; }

        /// <summary>
        /// False until the board has been calculated from a camera.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Returns the local rectangle shrunk by a normalized padding.
        /// </summary>
        /// <param name="padding">x: Left, y: Right, z: Top, w: Bottom.</param>
        public CoordinateBounds GetPaddedLocal(Vector4 padding)
            => new CoordinateBounds(
                Local.Lerp(new Vector2(padding.x, padding.w)),
                Local.Lerp(new Vector2(1.0f - padding.y, 1.0f - padding.z)));
    }
}
