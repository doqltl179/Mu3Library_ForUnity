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

            float oldValue = valueProp.floatValue;
            float newValue = EditorGUI.FloatField(position, $"[Observable] {label}", oldValue);

            if (oldValue != newValue)
            {
                valueProp.floatValue = newValue;

                var target = fieldInfo.GetValue(valueProp.serializedObject.targetObject) as ObservableFloat;
                target?.Set(newValue);
            }

            EditorGUI.EndProperty();
        }
    }
}