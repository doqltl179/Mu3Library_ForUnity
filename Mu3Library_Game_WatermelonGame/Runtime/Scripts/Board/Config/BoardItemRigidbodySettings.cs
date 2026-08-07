using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Config
{
    /// <summary>
    /// The rigidbody values handed to every board item, so all of them move the same way
    /// <br/> without the item prefab having to carry the settings.
    /// <br/> The gravity is not part of it, the board scales that one with its own size.
    /// </summary>
    [System.Serializable]
    public class BoardItemRigidbodySettings
    {
        /// <summary>
        /// The Unity default is 0, a little damping is added so a dropped item stops sliding
        /// <br/> instead of drifting on until it reaches a side wall.
        /// </summary>
        public const float DefaultLinearDamping = 0.05f;

        /// <summary>
        /// The Unity default is 0.05, which lets an item roll across the whole floor.
        /// <br/> The spin is taken out of it faster, so it settles near where it landed.
        /// </summary>
        public const float DefaultAngularDamping = 0.5f;

        [Tooltip("How quickly an item loses its speed while it falls and slides.\nUnity default: 0")]
        [SerializeField, Min(0.0f)] protected float _linearDamping = DefaultLinearDamping;
        public float LinearDamping => Mathf.Max(0.0f, _linearDamping);

        [Tooltip("How quickly an item loses its spin, which is what decides how far it rolls before it settles.\nUnity default: 0.05")]
        [SerializeField, Min(0.0f)] protected float _angularDamping = DefaultAngularDamping;
        public float AngularDamping => Mathf.Max(0.0f, _angularDamping);
    }
}
