using Mu3Library.ObjectPool;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Item
{
    [System.Serializable]
    public class BoardItemInfo : CreateArguments
    {
        [SerializeField] private Sprite _sprite;
        public Sprite Sprite => _sprite;

        [Tooltip("The collider diameter as a fraction of the sprite size.")]
        [SerializeField, Range(0.01f, 1.0f)] private float _colliderScale = 0.96f;
        /// <summary>
        /// The collider diameter as a fraction of the sprite size.
        /// <br/> An entry serialized before this field existed carries 0, which the range never
        /// <br/> allows, so it is read as the full sprite size instead of a collider without one.
        /// </summary>
        public float ColliderScale => _colliderScale > 0.0f ? _colliderScale : 1.0f;
    }
}
