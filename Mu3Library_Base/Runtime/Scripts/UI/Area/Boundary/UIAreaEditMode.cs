namespace Mu3Library.UI.Area
{
    /// <summary>
    /// Decides whether the cut lines of one axis are shared by the whole grid or kept per row/column.
    /// </summary>
    public enum UIAreaEditMode
    {
        /// <summary>
        /// One shared pair of cut lines. Moving a cut line resizes the whole line of areas.
        /// </summary>
        Uniform = 0,

        /// <summary>
        /// One pair of cut lines per row (horizontal) or per column (vertical).
        /// Moving a cut line resizes only the areas of that row/column.
        /// </summary>
        Independent = 1,
    }
}
