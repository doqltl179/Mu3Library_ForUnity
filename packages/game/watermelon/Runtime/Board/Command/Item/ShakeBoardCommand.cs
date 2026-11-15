using System;
using System.Collections.Generic;
using Mu3Library.Game.WatermelonGame.Board.Item;
using UnityEngine;

using Random = UnityEngine.Random;

namespace Mu3Library.Game.WatermelonGame.Board.Command.Item
{
    /// <summary>
    /// Pushes every item on the board around for a while, the way a player shaking the cabinet
    /// <br/> would, so a stack that got stuck can settle differently.
    /// <br/> It is the example of a command that keeps working over several frames: the board
    /// <br/> advances it every frame until its time is up, and cancelling it stops the pushing
    /// <br/> at once.
    /// <br/> The push is measured against the board height, which follows the screen resolution,
    /// <br/> so the same shake moves the stack the same way on every device.
    /// </summary>
    public class ShakeBoardCommand : BoardCommand
    {
        protected readonly float _duration;
        /// <summary>
        /// The seconds the board is shaken for.
        /// </summary>
        public float Duration => _duration;

        protected readonly float _strengthBoardHeightRatio;
        /// <summary>
        /// How hard the items are pushed, as a fraction of the board area height per second squared.
        /// </summary>
        public float StrengthBoardHeightRatio => _strengthBoardHeightRatio;

        protected readonly float _spinDegrees;
        /// <summary>
        /// How much spin the items are given, in degrees per second squared.
        /// </summary>
        public float SpinDegrees => _spinDegrees;

        protected float _elapsed;
        public float Elapsed => _elapsed;

        private readonly IBoardCommandContext _context;
        private readonly Action _onComplete;

        // The board list changes while the items are pushed, a merge can complete in between,
        // so the items of one frame are taken over first.
        private readonly List<BoardItem> _shakenItems = new();



        /// <param name="context">The board, taken from <see cref="BoardController.CommandContext"/>.</param>
        /// <param name="seconds">The seconds to shake for.</param>
        /// <param name="strengthBoardHeightRatio">How hard to push, as a fraction of the board area height per second squared.</param>
        /// <param name="spinDegrees">How much spin to add, in degrees per second squared.</param>
        /// <param name="onComplete">Called once the shake is over, never on a canceled command.</param>
        public ShakeBoardCommand(
            IBoardCommandContext context,
            float seconds = 0.4f,
            float strengthBoardHeightRatio = 1.0f,
            float spinDegrees = 90.0f,
            Action onComplete = null)
        {
            _context = context;
            _duration = Mathf.Max(0.0f, seconds);
            _strengthBoardHeightRatio = Mathf.Max(0.0f, strengthBoardHeightRatio);
            _spinDegrees = spinDegrees;
            _onComplete = onComplete;
        }

        protected override void OnRun()
        {
            if (_context == null)
            {
                Debug.LogWarning("The board cannot be shaken without a board.");
                Cancel();
                return;
            }

            if (_duration <= 0.0f)
            {
                Complete();
            }
        }

        protected override void OnUpdate(float deltaTime)
        {
            deltaTime = Mathf.Max(0.0f, deltaTime);
            _elapsed += deltaTime;

            Shake(deltaTime);

            if (_elapsed >= _duration)
            {
                Complete();
            }
        }

        protected override void OnComplete()
        {
            _shakenItems.Clear();

            _onComplete?.Invoke();
        }

        protected override void OnCancel()
        {
            _shakenItems.Clear();
        }

        /// <summary>
        /// Gives every item one frame worth of the push.
        /// </summary>
        protected virtual void Shake(float deltaTime)
        {
            BoardArea area = _context.Area;
            IReadOnlyList<BoardItem> items = _context.Items;
            if (area == null || items == null || deltaTime <= 0.0f)
            {
                return;
            }

            // The strength is an acceleration, so one frame of it is what the items are given.
            float velocity = area.WorldSize.y * _strengthBoardHeightRatio * deltaTime;
            float spin = _spinDegrees * deltaTime;

            _shakenItems.Clear();
            for (int index = 0; index < items.Count; index++)
            {
                _shakenItems.Add(items[index]);
            }

            for (int index = 0; index < _shakenItems.Count; index++)
            {
                BoardItem item = _shakenItems[index];

                // An item that is about to disappear into a merge is left where it is.
                if (item == null || item.IsMerging)
                {
                    continue;
                }

                // Sideways more than upward, a shake rattles the stack instead of tossing it
                // over the top edge of the board.
                Vector2 direction = new Vector2(Random.Range(-1.0f, 1.0f), Random.Range(-0.25f, 0.5f));
                item.AddVelocity(direction.normalized * velocity, Random.Range(-1.0f, 1.0f) * spin);
            }

            _shakenItems.Clear();
        }
    }
}
