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

        [Space(20)]
        [SerializeField] protected BoardItemsConfig _itemConfig;
        public BoardItemsConfig ItemConfig => _itemConfig;



        private void OnValidate()
        {
            _itemConfig?.EnsureItemInfoCount();
        }
    }
}
