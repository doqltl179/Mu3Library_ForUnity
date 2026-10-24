using Mu3Library.Observable;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    [CustomPropertyDrawer(typeof(ObservableFloat))]
    public class ObservableFloatDrawer : ObservablePropertyDrawer<float>
    {
        protected override void DrawField(Rect position, SerializedProperty valueProp, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, valueProp);
            EditorGUI.BeginChangeCheck();

            float newValue = EditorGUI.FloatField(position, $"[Observable] {label}", valueProp.floatValue);

            if (EditorGUI.EndChangeCheck())
            {
                valueProp.floatValue = newValue;
                ObservableDrawerUtility.ApplyAndNotify(valueProp, newValue);
            }

            EditorGUI.EndProperty();
        }
    }
}