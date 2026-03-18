using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    public abstract class ObservablePropertyDrawer<T> : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProp = property.FindPropertyRelative("_value");
            if (valueProp == null)
            {
                return base.GetPropertyHeight(property, label);
            }

            return EditorGUI.GetPropertyHeight(valueProp, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty valueProp = property.FindPropertyRelative("_value");

            if (valueProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Unsupported type.");
                return;
            }
            DrawField(position, valueProp, label);
        }

        protected abstract void DrawField(Rect position, SerializedProperty valueProp, GUIContent label);
    }
}
