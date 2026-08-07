using Mu3Library.ObjectPool;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Item
{
    [System.Serializable]
    public class BoardItemInfo : CreateArguments
    {
        [SerializeField] private Sprite _sprite;
        public Sprite Sprite => _sprite;
    }
}
