using Mu3Library.Observable;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    [CustomPropertyDrawer(typeof(ObservableLong))]
    public class ObservableLongDrawer : ObservablePropertyDrawer<long>
    {
        protected override void DrawField(Rect position, SerializedProperty valueProp, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, valueProp);

            long oldValue = valueProp.longValue;
            long newValue = EditorGUI.LongField(position, $"[Observable] {label}", oldValue);

            if (oldValue != newValue)
            {
                valueProp.longValue = newValue;

                var target = fieldInfo.GetValue(valueProp.serializedObject.targetObject) as ObservableLong;
                target?.Set(newValue);
            }

            EditorGUI.EndProperty();
        }
    }
}

