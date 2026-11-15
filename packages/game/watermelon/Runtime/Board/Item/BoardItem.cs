using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Item
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public partial class BoardItem : MonoBehaviour
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

        protected BoardItemInfo _info;
        public BoardItemInfo Info => _info;

        protected int _index = -1;
        public int Index => _index;
    }
}
