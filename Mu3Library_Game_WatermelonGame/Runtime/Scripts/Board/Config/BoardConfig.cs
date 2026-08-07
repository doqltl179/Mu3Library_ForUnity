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



        private void OnValidate()
        {
            _itemConfig?.EnsureItemInfoCount();
        }
    }
}
