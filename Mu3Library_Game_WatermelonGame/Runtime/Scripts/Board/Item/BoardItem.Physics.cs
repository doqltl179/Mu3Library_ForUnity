using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Item
{
    public partial class BoardItem
    {
        /// <summary>
        /// True from the moment the item is placed on the board until it lands
        /// on the board floor or on another item.
        /// </summary>
        protected bool _isFalling = false;
        public bool IsFalling => _isFalling;

        /// <summary>
        /// The board floor this item can land on.
        /// <br/> The side walls are never assigned here, they can neither end a fall nor hold an item up.
        /// </summary>
        protected Collider2D _floorCollider;

        private static readonly Collider2D[] ContactBuffer = new Collider2D[16];

        /// <summary>
        /// The collider radius measured in the parent(board) local space.
        /// </summary>
        public float BoardLocalRadius
        {
            get
            {
                Vector3 scale = transform.localScale;
                return _collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }
        }

        /// <summary>
        /// The collider center measured from the item position, in the parent(board) local space.
        /// <br/> The sprite center and the contact center are not the same place, the catalog entry
        /// <br/> decides how far they sit apart.
        /// </summary>
        public Vector2 BoardLocalColliderCenter
        {
            get
            {
                Vector3 scale = transform.localScale;
                Vector2 offset = _collider.offset;
                return new Vector2(offset.x * scale.x, offset.y * scale.y);
            }
        }

        /// <summary>
        /// The local Y of the item's top edge, in the parent(board) local space.
        /// </summary>
        public float BoardLocalTopY => GetBoardLocalTopY(transform.localPosition.y);

        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_isFalling)
            {
                return;
            }

            // Only the board floor and other items end a fall.
            // Sliding along a side wall is still falling, the wall cannot hold the item up.
            if (!IsLandingContact(collision.collider))
            {
                return;
            }

            _isFalling = false;
        }

        /// <summary>
        /// Marks the item as falling. Call it whenever the item is placed on the board,
        /// because every item is placed above the board line and has to drop first.
        /// <br/> The fall ends once the item touches <paramref name="floorCollider"/> or another item.
        /// </summary>
        /// <param name="floorCollider">The board floor the item can land on.</param>
        public void SetFallState(bool value, Collider2D floorCollider)
        {
            _floorCollider = floorCollider;
            _isFalling = value;
        }

        /// <summary>
        /// Gives the item the velocity it starts its fall with, in world units per second.
        /// <br/> A velocity is used instead of a force because the items differ in size, and therefore
        /// <br/> in mass, so the same force would push every item differently.
        /// </summary>
        public void SetDropVelocity(Vector2 worldVelocity)
        {
            _rb.linearVelocity = worldVelocity;
            _rb.angularVelocity = 0.0f;
        }

        /// <summary>
        /// Adds to the speed the item already carries, in world units per second, which is how a
        /// <br/> command pushes an item that is already resting on the board.
        /// <br/> Like <see cref="SetDropVelocity"/> it changes the speed and not the force, so the
        /// <br/> item size, and with it the item mass, does not decide how far it is pushed.
        /// <br/> An item that is not falling under its own weight, the one in the player's hand or
        /// <br/> one that is back in the pool, cannot be pushed.
        /// </summary>
        /// <param name="worldVelocity">The speed added to the item, in world units per second.</param>
        /// <param name="angularVelocity">The spin added to the item, in degrees per second.</param>
        public void AddVelocity(Vector2 worldVelocity, float angularVelocity = 0.0f)
        {
            if (_rb.bodyType != RigidbodyType2D.Dynamic)
            {
                return;
            }

            _rb.linearVelocity += worldVelocity;
            _rb.angularVelocity += angularVelocity;
        }

        /// <summary>
        /// <br/> Returns true when this item is stacked above the out line.
        /// <br/> An item counts as stacked only while it rests on the board floor
        /// <br/> or on other board items. The side walls are never a support, they cannot hold an item up.
        /// <br/> Every item is placed at the top of the board, so an item that has not landed yet
        /// <br/> is excluded through <see cref="IsFalling"/>.
        /// </summary>
        /// <param name="boardLocalOutLineY">The out line position in the board local space, the top edge of the board area.</param>
        internal bool IsStackedOverLine(float boardLocalOutLineY)
        {
            // A held item is kinematic and a merging item is about to disappear.
            if (_isFalling || _isMerging || BodyType != RigidbodyType2D.Dynamic || !_collider.enabled)
            {
                return false;
            }

            if (BoardLocalTopY <= boardLocalOutLineY)
            {
                return false;
            }

            return IsSupported();
        }

        private float GetBoardLocalTopY(float localY)
            => localY + BoardLocalColliderCenter.y + BoardLocalRadius;

        /// <summary>
        /// True while the item touches the board floor or another board item.
        /// </summary>
        private bool IsSupported()
        {
            int contactCount = _rb.GetContacts(ContactBuffer);

            for (int i = 0; i < contactCount; i++)
            {
                if (IsLandingContact(ContactBuffer[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when the contact can end a fall and hold the item up,
        /// which only the board floor and other board items can do.
        /// </summary>
        private bool IsLandingContact(Collider2D contact)
        {
            if (contact == null)
            {
                return false;
            }

            if (_floorCollider != null && contact == _floorCollider)
            {
                return true;
            }

            // The side walls are skipped here, only other items can stack this one up.
            return contact.TryGetComponent(out BoardItem _);
        }
    }
}
