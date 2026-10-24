using Mu3Library.Game.WatermelonGame.Board.Item.Rule;
using NUnit.Framework;

namespace Mu3Library.Game.WatermelonGame.Tests
{
    public class BoardItemSpawnRuleTests
    {
        /// <summary>
        /// Every bag holds a hundred indices, so drawing a whole bag yields exactly what it holds.
        /// </summary>
        private const int BagSize = 100;

        /// <summary>
        /// The first stage only holds the three smallest items.
        /// </summary>
        private const int FirstStageMaxIndex = 2;

        [Test]
        public void GetNextIndex_CountJumpsIntoTheLastStage_DrawsFromTheLastStageBag()
        {
            BoardItemSpawnRule rule = new();
            rule.GetNextIndex(0);

            // A board restored at 60 draws on from there, without the first stage's bag finishing first.
            int laterStageIndexCount = 0;
            for (int draw = 0; draw < BagSize - 1; draw++)
            {
                if (rule.GetNextIndex(60 + draw) > FirstStageMaxIndex)
                {
                    laterStageIndexCount++;
                }
            }

            Assert.Greater(laterStageIndexCount, 0);
        }

        [Test]
        public void GetNextIndex_CountJumpsBackIntoTheFirstStage_DrawsFromTheFirstStageBag()
        {
            BoardItemSpawnRule rule = new();
            rule.GetNextIndex(40);

            // The same count is asked a whole bag long, so the first stage never ends on its own.
            for (int draw = 0; draw < BagSize; draw++)
            {
                Assert.LessOrEqual(rule.GetNextIndex(5), FirstStageMaxIndex, $"Draw {draw}");
            }
        }

        [Test]
        public void GetNextIndex_CountsThroughTheFirstStage_DrawsOnlyTheSmallestItems()
        {
            BoardItemSpawnRule rule = new();

            for (int spawnCount = 0; spawnCount <= 16; spawnCount++)
            {
                Assert.LessOrEqual(rule.GetNextIndex(spawnCount), FirstStageMaxIndex, $"Count {spawnCount}");
            }
        }
    }
}
