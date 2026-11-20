using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    /// <summary>
    /// A saved board, holding everything that is needed to bring it back as it was.
    /// <br/> <see cref="BoardController.ExportSnapshot"/> writes it and <see cref="BoardController.Prepare"/> reads it.
    /// </summary>
    [System.Serializable]
    public class BoardSnapshot
    {
        public int Score;
        public int SpawnCount;
        public int MergeCount;

        /// <summary>
        /// The item the player was holding, -1 when the board was saved with an empty hand.
        /// </summary>
        public int HoldingItemIndex = -1;

        /// <summary>
        /// The item the preview was showing, -1 when nothing had been drawn yet.
        /// <br/> The spawn rule draws at random, so the items the player was already promised
        /// <br/> are saved instead of being drawn again on the restored board.
        /// </summary>
        public int NextItemIndex = -1;

        public List<BoardItemSnapshot> BoardItemsSnapshot = new();

        /// <summary>
        /// Writes the snapshot as JSON, which is what gets stored or sent.
        /// </summary>
        public string ToJson(bool prettyPrint = false)
            => JsonUtility.ToJson(this, prettyPrint);

        /// <summary>
        /// Reads a snapshot written by <see cref="ToJson"/>, null when the text is no snapshot.
        /// </summary>
        public static BoardSnapshot FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<BoardSnapshot>(json);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }
    }

    [System.Serializable]
    public class BoardItemSnapshot
    {
        public int Index;

        /// <summary>
        /// Where the item sits as a fraction of the board area, so a board saved on one screen
        /// <br/> resolution is restored on the same spot on every other one.
        /// </summary>
        public Vector2 NormalizedLocalPosition;

        /// <summary>
        /// How far the item is turned in the board local space, in degrees around Z.
        /// <br/> The items roll while they settle, so the board is saved with them lying
        /// <br/> exactly as the player left them.
        /// </summary>
        public float LocalRotation;
    }
}
