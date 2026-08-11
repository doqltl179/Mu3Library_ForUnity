using Mu3Library.Editor.Attribute;
using Mu3Library.UI.Area;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.UI.Area
{
    [CustomEditor(typeof(UIAreaElement), true)]
    public class UIAreaElementEditor : UnityEditor.Editor
    {
        private const string _areaTypePropertyName = "m_areaType";
        private const string _fillAreaPropertyName = "m_fillArea";

        private const string _applyUndoName = "Apply UI Area";
        private const string _missingGridMessage = "The parent has no UIAreaGrid, so this element is not anchored. Add a UIAreaGrid to the parent object.";
        private const string _drivenMessage = "The grid drives the anchors, so they are read only. Turn off Fill Area to place and size the rect inside its area by hand. The pivot, the rotation and the scale are never driven.";

        private static readonly string[] _handledPropertyNames =
        {
            UIAreaEditorUtility.ScriptPropertyName,
            _areaTypePropertyName,
            _fillAreaPropertyName,
        };

        private static readonly GUIContent _areaTypeLabel = new GUIContent("Area", "Area this element is anchored to.");
        private static readonly GUIContent _fillAreaLabel = new GUIContent("Fill Area", "Clears the offsets so that the rect fills its area exactly.");

        private SerializedProperty _scriptProperty;
        private SerializedProperty _areaTypeProperty;
        private SerializedProperty _fillAreaProperty;



        private void OnEnable()
        {
            _scriptProperty = serializedObject.FindProperty(UIAreaEditorUtility.ScriptPropertyName);
            _areaTypeProperty = serializedObject.FindProperty(_areaTypePropertyName);
            _fillAreaProperty = serializedObject.FindProperty(_fillAreaPropertyName);
        }

        public override void OnInspectorGUI()
        {
            UIAreaElement element = target as UIAreaElement;
            if (element == null)
            {
                DrawDefaultInspector();
                return;
            }

            serializedObject.Update();

            UIAreaEditorUtility.DrawScript(_scriptProperty);

            UIAreaGrid grid = element.Grid;

            EditorGUI.BeginChangeCheck();

            DrawAreaSelection(element);
            EditorGUILayout.PropertyField(_fillAreaProperty, _fillAreaLabel);

            bool changed = EditorGUI.EndChangeCheck();

            UIAreaEditorUtility.DrawRemainingProperties(serializedObject, _handledPropertyNames);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();

            if (grid == null)
            {
                EditorGUILayout.HelpBox(_missingGridMessage, MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(_drivenMessage, MessageType.Info);
                EditorGUILayout.Space();

                if (GUILayout.Button("Apply"))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                ApplyElement(element);
            }

            ButtonInvokeAttributeInspector.Draw(targets);
        }

        private void DrawAreaSelection(UIAreaElement element)
        {
            UIAreaType areaType = (UIAreaType)_areaTypeProperty.intValue;
            UIAreaType picked = UIAreaEditorUtility.DrawAreaSelection(_areaTypeLabel, areaType, element);

            if (picked != areaType)
            {
                _areaTypeProperty.intValue = (int)picked;
            }
        }

        private static void ApplyElement(UIAreaElement element)
        {
            RectTransform rectTransform = element.RectTransform;
            if (rectTransform == null)
            {
                return;
            }

            Undo.RecordObject(rectTransform, _applyUndoName);

            element.Apply();

            UIAreaEditorUtility.MarkDirty(rectTransform);
        }
    }
}
