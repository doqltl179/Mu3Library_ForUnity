using Mu3Library.ObjectPool;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Item
{
    [System.Serializable]
    public class BoardItemInfo : CreateArguments
    {
        [SerializeField] private Sprite _sprite;
        public Sprite Sprite => _sprite;

        [Tooltip("The collider diameter as a fraction of the sprite size. The board sizes the collider, so a smaller value draws the sprite larger instead of shrinking what the item touches with.")]
        [SerializeField, Range(0.01f, 1.0f)] private float _colliderScale = 0.97f;
        /// <summary>
        /// The collider diameter as a fraction of the sprite size.
        /// <br/> The board gives the collider the size an item index asks for, so this only decides
        /// <br/> how much of the sprite the contact area covers: the smaller it is, the larger the
        /// <br/> sprite is drawn around an unchanged contact area.
        /// <br/> An entry serialized before this field existed carries 0, which the range never
        /// <br/> allows, so it is read as the full sprite size instead of a collider without one.
        /// </summary>
        public float ColliderScale => _colliderScale > 0.0f ? _colliderScale : 1.0f;

        [Tooltip("The collider center as a fraction of the collider diameter. The collider diameter is the same for an item index on every screen resolution, so this offset is too.")]
        [SerializeField] private Vector2 _colliderOffset = new Vector2(0.0f, -0.03f);
        /// <summary>
        /// The collider center as a fraction of the collider diameter, the sprite center being zero.
        /// <br/> The board keeps the collider diameter of an item index the same on every screen
        /// <br/> resolution, so an offset written here moves the contact area by the same fraction
        /// <br/> of it everywhere.
        /// <br/> An entry serialized before this field existed carries <see cref="Vector2.zero"/>,
        /// <br/> which centers the collider on the sprite.
        /// </summary>
        public Vector2 ColliderOffset => _colliderOffset;
    }
}
