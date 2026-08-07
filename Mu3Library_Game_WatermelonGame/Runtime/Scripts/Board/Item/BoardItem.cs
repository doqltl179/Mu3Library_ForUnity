using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Item
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class BoardItem : MonoBehaviour
    {
        private SpriteRenderer m_renderer;
        protected SpriteRenderer _renderer => m_renderer ??= GetComponent<SpriteRenderer>();

        private CircleCollider2D m_collider;
        protected CircleCollider2D _collider => m_collider ??= GetComponent<CircleCollider2D>();
        public CircleCollider2D Collider => _collider;

        private Rigidbody2D m_rb;
        protected Rigidbody2D _rb => m_rb ??= GetComponent<Rigidbody2D>();

        public RigidbodyType2D BodyType
        {
            get => _rb.bodyType;
            set
            {
                _rb.bodyType = value;

                if (value == RigidbodyType2D.Dynamic)
                {
                    _rb.WakeUp();
                }
            }
        }

        public bool ColliderEnabled
        {
            get => _collider.enabled;
            set => _collider.enabled = value;
        }

        /// <summary>
        /// The multiplier applied to the project gravity while this item falls.
        /// <br/> The board scales it with the board size, so the item keeps falling the same way
        /// <br/> on every screen resolution, and the project gravity itself stays untouched.
        /// </summary>
        public float GravityScale
        {
            get => _rb.gravityScale;
            set => _rb.gravityScale = value;
        }

        /// <summary>
        /// How quickly the item loses its speed while it falls and slides.
        /// </summary>
        public float LinearDamping
        {
            get => _rb.linearDamping;
            set => _rb.linearDamping = value;
        }

        /// <summary>
        /// How quickly the item loses its spin, which is what decides how far it rolls
        /// <br/> on the board floor before it comes to rest.
        /// </summary>
        public float AngularDamping
        {
            get => _rb.angularDamping;
            set => _rb.angularDamping = value;
        }

        /// <summary>
        /// The material the item rubs and bounces with.
        /// <br/> Null hands the collider back to the material of the rigidbody or the project.
        /// </summary>
        public PhysicsMaterial2D PhysicsMaterial
        {
            get => _collider.sharedMaterial;
            set => _collider.sharedMaterial = value;
        }

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
        /// The local Y of the item's top edge, in the parent(board) local space.
        /// </summary>
        public float BoardLocalTopY => GetBoardLocalTopY(transform.localPosition.y);

        protected BoardItemInfo _info;
        public BoardItemInfo Info => _info;

        protected int _index = -1;
        public int Index => _index;

        protected bool _isMerging = false;
        public bool IsMerging => _isMerging;

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



        protected virtual void OnDisable()
        {
            _rb.bodyType = RigidbodyType2D.Kinematic;

            // A pooled item is always dropped again before it can be judged.
            _isFalling = true;
        }

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

        #region Utility
        public virtual bool CanMerge(BoardItem hitItem)
        {
            if (this == null ||
                this == hitItem ||
                hitItem == null ||
                _index != hitItem.Index ||
                _info == null ||
                hitItem.Info == null ||
                !IsTouching(hitItem) ||
                _isMerging ||
                hitItem.IsMerging)
            {
                return false;
            }

            return true;
        }

        public bool IsTouching(BoardItem hitItem)
        {
            if (this == null || hitItem == null)
            {
                return false;
            }

            return _collider.IsTouching(hitItem.Collider);
        }

        public void SetMergeState(bool value) => _isMerging = value;

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

        public virtual void Init(int index, BoardItemInfo info)
        {
            _index = index;
            _isMerging = false;

            if (info == null)
            {
                _info = null;
                return;
            }

            _renderer.sprite = info.Sprite;

            if (info.Sprite != null)
            {
                // The sprite usually carries transparent padding, so the collider is shrunk
                // to the part of it that should actually touch the other items.
                Vector2 spriteSize = info.Sprite.bounds.size;
                _collider.radius = Mathf.Max(spriteSize.x, spriteSize.y) * 0.5f * info.ColliderScale;
            }

            _info = info;
        }

        public virtual void Init(BoardItemInfo info) => Init(-1, info);
        #endregion

        private float GetBoardLocalTopY(float localY)
        {
            Vector3 scale = transform.localScale;
            return localY + _collider.offset.y * scale.y + BoardLocalRadius;
        }

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
