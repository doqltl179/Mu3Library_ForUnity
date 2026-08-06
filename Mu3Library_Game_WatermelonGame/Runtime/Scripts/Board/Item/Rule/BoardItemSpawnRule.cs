using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Item.Rule
{
    public class BoardItemSpawnRule
    {
        private readonly List<int> _indexBag = new();



        public virtual int GetNextIndex(int spawnCount)
        {
            const int stage01MaxCount = 16;
            const int stage02MaxCount = 32;

            int stage = spawnCount <= stage01MaxCount
                ? 0
                : spawnCount <= stage02MaxCount
                    ? 1
                    : 2;

            bool isStageStart = spawnCount == 0 ||
                spawnCount == stage01MaxCount + 1 ||
                spawnCount == stage02MaxCount + 1;
            if (isStageStart || _indexBag.Count == 0)
            {
                RebuildIndexBag(stage);
            }

            int randomIndex = Random.Range(0, _indexBag.Count);
            int lastIndex = _indexBag.Count - 1;
            int nextIndex = _indexBag[randomIndex];
            _indexBag[randomIndex] = _indexBag[lastIndex];
            _indexBag.RemoveAt(lastIndex);

            return nextIndex;
        }

        private void RebuildIndexBag(int stage)
        {
            _indexBag.Clear();

            switch (stage)
            {
                case 0:
                    AddWeightedIndex(0, 50);
                    AddWeightedIndex(1, 30);
                    AddWeightedIndex(2, 20);
                    break;
                case 1:
                    AddWeightedIndex(0, 40);
                    AddWeightedIndex(1, 30);
                    AddWeightedIndex(2, 20);
                    AddWeightedIndex(3, 10);
                    break;
                default:
                    AddWeightedIndex(0, 30);
                    AddWeightedIndex(1, 25);
                    AddWeightedIndex(2, 20);
                    AddWeightedIndex(3, 15);
                    AddWeightedIndex(4, 10);
                    break;
            }

            ShuffleIndexBag();
        }

        private void AddWeightedIndex(int index, int weight)
        {
            for (int i = 0; i < weight; i++)
            {
                _indexBag.Add(index);
            }
        }

        private void ShuffleIndexBag()
        {
            for (int i = _indexBag.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                int value = _indexBag[i];
                _indexBag[i] = _indexBag[randomIndex];
                _indexBag[randomIndex] = value;
            }
        }
    }
}
