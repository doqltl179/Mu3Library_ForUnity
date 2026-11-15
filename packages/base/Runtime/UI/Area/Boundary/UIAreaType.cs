using UnityEngine;

namespace Mu3Library.UI.Area
{
    /// <summary>
    /// Column of the area grid, ordered along the x axis.
    /// </summary>
    public enum UIAreaColumn
    {
        Left = 0,
        Middle = 1,
        Right = 2,
    }

    /// <summary>
    /// Row of the area grid, ordered along the y axis so that it matches the anchor space.
    /// </summary>
    public enum UIAreaRow
    {
        Bottom = 0,
        Middle = 1,
        Top = 2,
    }

    /// <summary>
    /// One of the nine areas. The value is packed as (column * <see cref="UIAreaUtility.RowCount"/> + row).
    /// </summary>
    public enum UIAreaType
    {
        LeftBottom = 0,
        Left = 1,
        LeftTop = 2,
        Bottom = 3,
        Middle = 4,
        Top = 5,
        RightBottom = 6,
        Right = 7,
        RightTop = 8,
    }

    public static class UIAreaUtility
    {
        public const int ColumnCount = 3;
        public const int RowCount = 3;
        public const int AreaCount = ColumnCount * RowCount;

        public static UIAreaColumn GetColumn(this UIAreaType areaType) => (UIAreaColumn)(GetAreaIndex(areaType) / RowCount);

        public static UIAreaRow GetRow(this UIAreaType areaType) => (UIAreaRow)(GetAreaIndex(areaType) % RowCount);

        public static int GetColumnIndex(this UIAreaColumn column) => Mathf.Clamp((int)column, 0, ColumnCount - 1);

        public static int GetRowIndex(this UIAreaRow row) => Mathf.Clamp((int)row, 0, RowCount - 1);

        public static int GetAreaIndex(this UIAreaType areaType) => Mathf.Clamp((int)areaType, 0, AreaCount - 1);

        public static UIAreaType GetAreaType(UIAreaColumn column, UIAreaRow row)
        {
            return (UIAreaType)(column.GetColumnIndex() * RowCount + row.GetRowIndex());
        }
    }
}
