namespace Mu3Library.Game.WatermelonGame.Board.Item.Rule
{
    public class BoardItemScoreRule
    {
        /// <summary>
        /// Returns the score for the item at the specified index.
        /// </summary>
        /// <remarks>
        /// The default score follows the Watermelon Game's triangular score progression:
        /// 1, 3, 6, 10, and so on.
        /// </remarks>
        public virtual int GetScore(int index)
        {
            if (index < 0)
            {
                return 0;
            }

            return (index + 1) * (index + 2) / 2;
        }
    }
}
