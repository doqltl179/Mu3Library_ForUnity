using Mu3Library.UI.Area;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.UI.Area
{
    /// <summary>
    /// Shared inspector drawing for <see cref="UIAreaGrid"/> and <see cref="UIAreaElement"/>.
    /// </summary>
    internal static class UIAreaEditorUtility
    {
        /// <summary>
        /// Name of the property that holds the script asset, which every MonoBehaviour inspector draws first.
        /// </summary>
        public const string ScriptPropertyName = "m_Script";

        private const string _minFieldName = "m_min";
        private const string _maxFieldName = "m_max";
        private const float _boundaryFieldWidth = 46.0f;

        private const string _leftLabel = "L";
        private const string _rightLabel = "R";
        private const string _bottomLabel = "B";
        private const string _topLabel = "T";
        private const string _middleLabel = "M";

        private static string[] _shortLabels;
        private static string[] _displayNames;
        private static GUIContent[] _selectionLabels;

        /// <summary>
        /// Short label of the area, such as "LT" or "M".
        /// </summary>
        public static string GetShortLabel(UIAreaType areaType)
        {
            EnsureLabels();

            return _shortLabels[areaType.GetAreaIndex()];
        }

        /// <summary>
        /// Readable name of the area, such as "Left Top".
        /// </summary>
        public static string GetDisplayName(UIAreaType areaType)
        {
            EnsureLabels();

            return _displayNames[areaType.GetAreaIndex()];
        }

        /// <summary>
        /// Labels of a 3x3 selection grid, ordered from the left top to the right bottom.
        /// </summary>
        public static GUIContent[] GetSelectionLabels()
        {
            EnsureLabels();

            return _selectionLabels;
        }

        /// <summary>
        /// Area at the given index of a 3x3 selection grid.
        /// </summary>
        public static UIAreaType GetAreaTypeBySelectionIndex(int selectionIndex)
        {
            int clampedIndex = Mathf.Clamp(selectionIndex, 0, UIAreaUtility.AreaCount - 1);

            UIAreaColumn column = (UIAreaColumn)(clampedIndex % UIAreaUtility.ColumnCount);
            UIAreaRow row = (UIAreaRow)(UIAreaUtility.RowCount - 1 - clampedIndex / UIAreaUtility.ColumnCount);

            return UIAreaUtility.GetAreaType(column, row);
        }

        /// <summary>
        /// Draws the areas as a 3x3 button grid and returns the picked one.
        /// An area another element already owns is disabled, so it cannot be claimed twice.
        /// </summary>
        public static UIAreaType DrawAreaSelection(GUIContent label, UIAreaType selected, UIAreaElement owner)
        {
            EditorGUILayout.LabelField(label);

            UIAreaType picked = selected;

            using (new EditorGUI.IndentLevelScope())
            {
                Rect gridRect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(
                    GUIContent.none,
                    GUIStyle.none,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight * UIAreaUtility.RowCount)));

                float cellWidth = gridRect.width / UIAreaUtility.ColumnCount;
                float cellHeight = gridRect.height / UIAreaUtility.RowCount;

                GUIContent[] labels = GetSelectionLabels();

                for (int i = 0; i < UIAreaUtility.AreaCount; i++)
                {
                    UIAreaType areaType = GetAreaTypeBySelectionIndex(i);

                    int column = i % UIAreaUtility.ColumnCount;
                    int row = i / UIAreaUtility.ColumnCount;

                    Rect cellRect = new Rect(
                        gridRect.x + cellWidth * column,
                        gridRect.y + cellHeight * row,
                        cellWidth,
                        cellHeight);

                    bool isSelected = areaType == selected;
                    bool isTaken = !isSelected && owner != null && owner.IsAreaTaken(areaType);

                    using (new EditorGUI.DisabledScope(isTaken))
                    {
                        if (GUI.Toggle(cellRect, isSelected, labels[i], GetSelectionCellStyle(column)) && !isSelected)
                        {
                            picked = areaType;
                        }
                    }
                }
            }

            return picked;
        }

        /// <summary>
        /// Draws a pair of cut lines as one min/max slider with two value fields.
        /// </summary>
        public static bool DrawBoundary(SerializedProperty boundaryProperty, GUIContent label)
        {
            SerializedProperty minProperty = boundaryProperty?.FindPropertyRelative(_minFieldName);
            SerializedProperty maxProperty = boundaryProperty?.FindPropertyRelative(_maxFieldName);

            if (minProperty == null || maxProperty == null)
            {
                EditorGUILayout.PropertyField(boundaryProperty, label, true);
                return false;
            }

            float min = minProperty.floatValue;
            float max = maxProperty.floatValue;

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.MinMaxSlider(label, ref min, ref max, 0.0f, 1.0f);
            min = EditorGUILayout.DelayedFloatField(min, GUILayout.Width(_boundaryFieldWidth));
            max = EditorGUILayout.DelayedFloatField(max, GUILayout.Width(_boundaryFieldWidth));

            bool changed = EditorGUI.EndChangeCheck();

            EditorGUILayout.EndHorizontal();

            if (changed)
            {
                // The struct owns what a valid pair of cut lines is, so the edited values go through it.
                UIAreaBoundary boundary = new UIAreaBoundary(min, max);

                minProperty.floatValue = boundary.Min;
                maxProperty.floatValue = boundary.Max;
            }

            return changed;
        }

        /// <summary>
        /// Draws the read only script field a custom inspector would otherwise drop.
        /// </summary>
        public static void DrawScript(SerializedProperty scriptProperty)
        {
            if (scriptProperty == null)
            {
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(scriptProperty);
            }

            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draws every property the inspector does not lay out by hand, such as the ones a derived class adds.
        /// </summary>
        public static void DrawRemainingProperties(SerializedObject serializedObject, string[] handledPropertyNames)
        {
            if (serializedObject == null)
            {
                return;
            }

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            bool spaceDrawn = false;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (handledPropertyNames != null && System.Array.IndexOf(handledPropertyNames, iterator.name) >= 0)
                {
                    continue;
                }

                if (!spaceDrawn)
                {
                    EditorGUILayout.Space();
                    spaceDrawn = true;
                }

                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        /// <summary>
        /// Marks the object as changed so that the scene and the prefab instance keep the edit.
        /// </summary>
        public static void MarkDirty(Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static void EnsureLabels()
        {
            if (_selectionLabels != null)
            {
                return;
            }

            _shortLabels = new string[UIAreaUtility.AreaCount];
            _displayNames = new string[UIAreaUtility.AreaCount];

            // Both labels are read off the area itself, so the enum stays the only place that names an area.
            for (int i = 0; i < UIAreaUtility.AreaCount; i++)
            {
                UIAreaType areaType = (UIAreaType)i;

                _shortLabels[i] = BuildShortLabel(areaType);
                _displayNames[i] = ObjectNames.NicifyVariableName(areaType.ToString());
            }

            _selectionLabels = new GUIContent[UIAreaUtility.AreaCount];

            for (int i = 0; i < _selectionLabels.Length; i++)
            {
                int areaIndex = GetAreaTypeBySelectionIndex(i).GetAreaIndex();

                _selectionLabels[i] = new GUIContent(_shortLabels[areaIndex], _displayNames[areaIndex]);
            }
        }

        private static string BuildShortLabel(UIAreaType areaType)
        {
            string label = GetColumnLabel(areaType.GetColumn()) + GetRowLabel(areaType.GetRow());

            // The center area sits on no side, so it takes the letter of the middle instead.
            return label.Length > 0 ? label : _middleLabel;
        }

        private static string GetColumnLabel(UIAreaColumn column)
        {
            if (column == UIAreaColumn.Left)
            {
                return _leftLabel;
            }

            return column == UIAreaColumn.Right ? _rightLabel : string.Empty;
        }

        private static string GetRowLabel(UIAreaRow row)
        {
            if (row == UIAreaRow.Bottom)
            {
                return _bottomLabel;
            }

            return row == UIAreaRow.Top ? _topLabel : string.Empty;
        }

        private static GUIStyle GetSelectionCellStyle(int column)
        {
            if (column == 0)
            {
                return EditorStyles.miniButtonLeft;
            }

            return column == UIAreaUtility.ColumnCount - 1
                ? EditorStyles.miniButtonRight
                : EditorStyles.miniButtonMid;
        }
    }
}
