using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Config
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "BoardConfig", menuName = "Mu3Library/Game/Watermelon Game/Board Config", order = 0)]
    public class BoardConfig : ScriptableObject
    {
        [SerializeField] protected string _configId = "default";
        public string ConfigId => _configId;

        [Space(20)]
        [SerializeField] protected Sprite _boardSprite;
        public Sprite BoardSprite => _boardSprite;

        [SerializeField] protected Sprite _boardLineSprite;
        public Sprite BoardLineSprite => _boardLineSprite;

        [Tooltip("The image hung on the top edge of the board area, following the item the player is holding.\nIts pivot has to be (0.5, 0.0), so that it is drawn straight above the edge.")]
        [SerializeField] protected Sprite _boardSpawnSprite;
        public Sprite BoardSpawnSprite => _boardSpawnSprite;

        [Space(20)]
        [SerializeField] protected BoardItemsConfig _itemConfig;
        public BoardItemsConfig ItemConfig => _itemConfig;

        [Space(20)]
        [Tooltip("Optional. Applied to the collider of every board item.\nNone leaves the item prefab with the material it already carries.")]
        [SerializeField] protected PhysicsMaterial2D _itemPhysicsMaterial;
        /// <summary>
        /// The material every board item rubs and bounces with, null to keep the one on the item prefab.
        /// </summary>
        public PhysicsMaterial2D ItemPhysicsMaterial => _itemPhysicsMaterial;

        [Tooltip("The rigidbody values applied to every board item.")]
        [SerializeField] protected BoardItemRigidbodySettings _itemRigidbodySettings = new();
        [System.NonSerialized] private BoardItemRigidbodySettings m_runtimeItemRigidbodySettings;
        /// <summary>
        /// The rigidbody values every board item is given, never null.
        /// </summary>
        public BoardItemRigidbodySettings ItemRigidbodySettings =>
            _itemRigidbodySettings ?? (m_runtimeItemRigidbodySettings ??= new BoardItemRigidbodySettings());


        private void OnValidate()
        {
            _itemConfig?.EnsureItemInfoCount();
        }
    }
}
