using System.Collections.Generic;
using Mu3Library.Editor.Attribute;
using Mu3Library.UI.Area;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.UI.Area
{
    [CustomEditor(typeof(UIAreaGrid), true)]
    public class UIAreaGridEditor : UnityEditor.Editor
    {
        private const string _horizontalEditModePropertyName = "m_horizontalEditMode";
        private const string _verticalEditModePropertyName = "m_verticalEditMode";
        private const string _horizontalPropertyName = "m_horizontal";
        private const string _verticalPropertyName = "m_vertical";
        private const string _rowHorizontalsPropertyName = "m_rowHorizontals";
        private const string _columnVerticalsPropertyName = "m_columnVerticals";
        private const string _createElementsAutomaticallyPropertyName = "m_createElementsAutomatically";
        private const string _drawAreaGizmoPropertyName = "m_drawAreaGizmo";
        private const string _areaGizmoColorPropertyName = "m_areaGizmoColor";

        private const string _applyUndoName = "Apply UI Area Grid";
        private const string _resetUndoName = "Reset UI Area Grid";
        private const string _elementUndoName = "Edit UI Area Element";

        private const float _elementNameWidth = 96.0f;
        private const float _elementToggleWidth = 44.0f;

        private static readonly string[] _handledPropertyNames =
        {
            UIAreaEditorUtility.ScriptPropertyName,
            _horizontalEditModePropertyName,
            _verticalEditModePropertyName,
            _horizontalPropertyName,
            _verticalPropertyName,
            _rowHorizontalsPropertyName,
            _columnVerticalsPropertyName,
            _createElementsAutomaticallyPropertyName,
            _drawAreaGizmoPropertyName,
            _areaGizmoColorPropertyName,
        };

        private static readonly GUIContent _horizontalSectionLabel = new GUIContent("Horizontal Cut Lines", "Left and right cut lines of the grid.");
        private static readonly GUIContent _verticalSectionLabel = new GUIContent("Vertical Cut Lines", "Bottom and top cut lines of the grid.");
        private static readonly GUIContent _elementsSectionLabel = new GUIContent("Elements", "Child objects anchored to the areas.");
        private static readonly GUIContent _gizmoSectionLabel = new GUIContent("Gizmo", "Area outlines drawn in the scene view.");
        private static readonly GUIContent _drawAreaGizmoLabel = new GUIContent("Draw Areas", "Draws the nine areas in the scene view.");
        private static readonly GUIContent _areaGizmoColorLabel = new GUIContent("Color", "Color of the area gizmo.");
        private static readonly GUIContent _horizontalEditModeLabel = new GUIContent("Edit Mode", "Uniform moves the cut lines of every row. Independent moves the cut lines of one row only.");
        private static readonly GUIContent _verticalEditModeLabel = new GUIContent("Edit Mode", "Uniform moves the cut lines of every column. Independent moves the cut lines of one column only.");
        private static readonly GUIContent _uniformHorizontalLabel = new GUIContent("All Rows", "Cut lines shared by the left, middle and right areas of every row.");
        private static readonly GUIContent _uniformVerticalLabel = new GUIContent("All Columns", "Cut lines shared by the bottom, middle and top areas of every column.");
        private static readonly GUIContent _createElementsAutomaticallyLabel = new GUIContent("Auto Create", "Creates a child element for every area that has none.");

        private static readonly GUIContent _areaHeaderLabel = new GUIContent("Area");
        private static readonly GUIContent _objectHeaderLabel = new GUIContent("Object");
        private static readonly GUIContent _activeHeaderLabel = new GUIContent("Active", "Active state of the element object.");
        private static readonly GUIContent _fillHeaderLabel = new GUIContent("Fill", "Clears the offsets so that the rect fills its area exactly.");
        private static readonly GUIContent _createElementLabel = new GUIContent("Create", "Creates the element of this area.");

        private static readonly GUIContent[] _rowLabels =
        {
            new GUIContent("Bottom Row"),
            new GUIContent("Middle Row"),
            new GUIContent("Top Row"),
        };

        private static readonly GUIContent[] _columnLabels =
        {
            new GUIContent("Left Column"),
            new GUIContent("Middle Column"),
            new GUIContent("Right Column"),
        };

        private readonly List<UIAreaElement> _elements = new List<UIAreaElement>();

        private SerializedProperty _scriptProperty;
        private SerializedProperty _horizontalEditModeProperty;
        private SerializedProperty _verticalEditModeProperty;
        private SerializedProperty _horizontalProperty;
        private SerializedProperty _verticalProperty;
        private SerializedProperty _rowHorizontalsProperty;
        private SerializedProperty _columnVerticalsProperty;
        private SerializedProperty _createElementsAutomaticallyProperty;
        private SerializedProperty _drawAreaGizmoProperty;
        private SerializedProperty _areaGizmoColorProperty;



        private void OnEnable()
        {
            _scriptProperty = serializedObject.FindProperty(UIAreaEditorUtility.ScriptPropertyName);
            _horizontalEditModeProperty = serializedObject.FindProperty(_horizontalEditModePropertyName);
            _verticalEditModeProperty = serializedObject.FindProperty(_verticalEditModePropertyName);
            _horizontalProperty = serializedObject.FindProperty(_horizontalPropertyName);
            _verticalProperty = serializedObject.FindProperty(_verticalPropertyName);
            _rowHorizontalsProperty = serializedObject.FindProperty(_rowHorizontalsPropertyName);
            _columnVerticalsProperty = serializedObject.FindProperty(_columnVerticalsPropertyName);
            _createElementsAutomaticallyProperty = serializedObject.FindProperty(_createElementsAutomaticallyPropertyName);
            _drawAreaGizmoProperty = serializedObject.FindProperty(_drawAreaGizmoPropertyName);
            _areaGizmoColorProperty = serializedObject.FindProperty(_areaGizmoColorPropertyName);
        }

        public override void OnInspectorGUI()
        {
            UIAreaGrid grid = target as UIAreaGrid;
            if (grid == null)
            {
                DrawDefaultInspector();
                return;
            }

            serializedObject.Update();

            ResizeBoundaryArray(_rowHorizontalsProperty, UIAreaUtility.RowCount);
            ResizeBoundaryArray(_columnVerticalsProperty, UIAreaUtility.ColumnCount);

            UIAreaEditorUtility.DrawScript(_scriptProperty);

            bool changed = DrawHorizontalSection();
            EditorGUILayout.Space();
            changed |= DrawVerticalSection();

            EditorGUILayout.Space();
            DrawGizmoSection();

            UIAreaEditorUtility.DrawRemainingProperties(serializedObject, _handledPropertyNames);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawElementsSection(grid);

            EditorGUILayout.Space();
            DrawButtons(grid);

            if (changed)
            {
                ApplyToElements(grid, _applyUndoName);
            }

            ButtonInvokeAttributeInspector.Draw(targets);
        }

        private bool DrawHorizontalSection()
        {
            EditorGUILayout.LabelField(_horizontalSectionLabel, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_horizontalEditModeProperty, _horizontalEditModeLabel);
            bool changed = EditorGUI.EndChangeCheck();

            using (new EditorGUI.IndentLevelScope())
            {
                if (_horizontalEditModeProperty.intValue == (int)UIAreaEditMode.Uniform)
                {
                    changed |= UIAreaEditorUtility.DrawBoundary(_horizontalProperty, _uniformHorizontalLabel);
                }
                else
                {
                    // Rows are listed from the top to the bottom so that they match the screen.
                    for (int i = UIAreaUtility.RowCount - 1; i >= 0; i--)
                    {
                        changed |= UIAreaEditorUtility.DrawBoundary(_rowHorizontalsProperty.GetArrayElementAtIndex(i), _rowLabels[i]);
                    }
                }
            }

            return changed;
        }

        private bool DrawVerticalSection()
        {
            EditorGUILayout.LabelField(_verticalSectionLabel, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_verticalEditModeProperty, _verticalEditModeLabel);
            bool changed = EditorGUI.EndChangeCheck();

            using (new EditorGUI.IndentLevelScope())
            {
                if (_verticalEditModeProperty.intValue == (int)UIAreaEditMode.Uniform)
                {
                    changed |= UIAreaEditorUtility.DrawBoundary(_verticalProperty, _uniformVerticalLabel);
                }
                else
                {
                    for (int i = 0; i < UIAreaUtility.ColumnCount; i++)
                    {
                        changed |= UIAreaEditorUtility.DrawBoundary(_columnVerticalsProperty.GetArrayElementAtIndex(i), _columnLabels[i]);
                    }
                }
            }

            return changed;
        }

        private void DrawGizmoSection()
        {
            if (_drawAreaGizmoProperty == null || _areaGizmoColorProperty == null)
            {
                return;
            }

            EditorGUILayout.LabelField(_gizmoSectionLabel, EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_drawAreaGizmoProperty, _drawAreaGizmoLabel);

            using (new EditorGUI.DisabledScope(!_drawAreaGizmoProperty.boolValue))
            {
                EditorGUILayout.PropertyField(_areaGizmoColorProperty, _areaGizmoColorLabel);
            }
        }

        private void DrawElementsSection(UIAreaGrid grid)
        {
            EditorGUILayout.LabelField(_elementsSectionLabel, EditorStyles.boldLabel);

            if (_createElementsAutomaticallyProperty != null)
            {
                // The section is drawn after the main block was applied, so this toggle applies on its own.
                EditorGUILayout.PropertyField(_createElementsAutomaticallyProperty, _createElementsAutomaticallyLabel);
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_areaHeaderLabel, EditorStyles.miniLabel, GUILayout.Width(_elementNameWidth));
            EditorGUILayout.LabelField(_objectHeaderLabel, EditorStyles.miniLabel);
            EditorGUILayout.LabelField(_activeHeaderLabel, EditorStyles.miniLabel, GUILayout.Width(_elementToggleWidth));
            EditorGUILayout.LabelField(_fillHeaderLabel, EditorStyles.miniLabel, GUILayout.Width(_elementToggleWidth));
            EditorGUILayout.EndHorizontal();

            // Areas are listed from the left top to the right bottom so that they match the screen.
            for (int i = 0; i < UIAreaUtility.AreaCount; i++)
            {
                DrawElementRow(grid, UIAreaEditorUtility.GetAreaTypeBySelectionIndex(i));
            }
        }

        private void DrawElementRow(UIAreaGrid grid, UIAreaType areaType)
        {
            UIAreaElement element = grid.GetElement(areaType);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(UIAreaEditorUtility.GetDisplayName(areaType), GUILayout.Width(_elementNameWidth));

            if (element == null)
            {
                if (GUILayout.Button(_createElementLabel))
                {
                    CreateElement(grid, areaType);
                }

                GUILayout.Space(_elementToggleWidth * 2.0f);
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(element, typeof(UIAreaElement), true);
                }

                DrawElementActiveToggle(element);
                DrawElementFillToggle(element);
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawElementActiveToggle(UIAreaElement element)
        {
            GameObject elementObject = element.gameObject;

            bool active = elementObject.activeSelf;
            bool changedActive = EditorGUILayout.Toggle(active, GUILayout.Width(_elementToggleWidth));

            if (changedActive == active)
            {
                return;
            }

            Undo.RecordObject(elementObject, _elementUndoName);

            elementObject.SetActive(changedActive);

            UIAreaEditorUtility.MarkDirty(elementObject);
        }

        private static void DrawElementFillToggle(UIAreaElement element)
        {
            bool fillArea = element.FillArea;
            bool changedFillArea = EditorGUILayout.Toggle(fillArea, GUILayout.Width(_elementToggleWidth));

            if (changedFillArea == fillArea)
            {
                return;
            }

            RectTransform rectTransform = element.RectTransform;

            Undo.RecordObject(element, _elementUndoName);
            Undo.RecordObject(rectTransform, _elementUndoName);

            element.FillArea = changedFillArea;

            UIAreaEditorUtility.MarkDirty(element);
            UIAreaEditorUtility.MarkDirty(rectTransform);
        }

        private void DrawButtons(UIAreaGrid grid)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Create Missing Elements"))
            {
                CreateMissingElements(grid);
            }

            if (GUILayout.Button("Apply To Children"))
            {
                ApplyToElements(grid, _applyUndoName);
            }

            if (GUILayout.Button("Reset Cut Lines"))
            {
                ResetBoundaries(grid);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void CreateMissingElements(UIAreaGrid grid)
        {
            grid.CreateMissingElements();

            EditorUtility.SetDirty(grid);
        }

        private void CreateElement(UIAreaGrid grid, UIAreaType areaType)
        {
            grid.CreateElement(areaType);

            EditorUtility.SetDirty(grid);
        }

        private void ResetBoundaries(UIAreaGrid grid)
        {
            RecordElementUndo(grid, _resetUndoName);
            Undo.RecordObject(grid, _resetUndoName);

            grid.ResetBoundaries();

            EditorUtility.SetDirty(grid);
            serializedObject.Update();

            MarkElementsDirty();
        }

        private void ApplyToElements(UIAreaGrid grid, string undoName)
        {
            RecordElementUndo(grid, undoName);

            grid.Apply();

            MarkElementsDirty();
        }

        private void RecordElementUndo(UIAreaGrid grid, string undoName)
        {
            grid.GetElements(_elements);

            for (int i = 0; i < _elements.Count; i++)
            {
                RectTransform rectTransform = _elements[i].RectTransform;
                if (rectTransform == null)
                {
                    continue;
                }

                Undo.RecordObject(rectTransform, undoName);
            }
        }

        private void MarkElementsDirty()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                UIAreaEditorUtility.MarkDirty(_elements[i].RectTransform);
            }

            _elements.Clear();
        }

        private static void ResizeBoundaryArray(SerializedProperty arrayProperty, int length)
        {
            if (arrayProperty == null || !arrayProperty.isArray || arrayProperty.arraySize == length)
            {
                return;
            }

            arrayProperty.arraySize = length;
        }
    }
}
