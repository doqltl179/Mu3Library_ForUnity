using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    [System.Serializable]
    public class BoardSnapshot
    {
        public int Score;

        public List<BoardItemSnapshot> BoardItemsSnapshot = new();
    }

    [System.Serializable]
    public class BoardItemSnapshot
    {
        public int Index;
        public Vector2 NormalizedLocalPosition;
    }
}
