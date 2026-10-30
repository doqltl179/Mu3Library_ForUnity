using Mu3Library.Observable;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    [CustomPropertyDrawer(typeof(ObservableInt))]
    public class ObservableIntDrawer : ObservablePropertyDrawer<int>
    {
        protected override void DrawField(Rect position, SerializedProperty valueProp, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, valueProp);
            EditorGUI.BeginChangeCheck();

            int newValue = EditorGUI.IntField(position, $"[Observable] {label}", valueProp.intValue);

            if (EditorGUI.EndChangeCheck())
            {
                valueProp.intValue = newValue;
                ObservableDrawerUtility.ApplyAndNotify(valueProp, newValue);
            }

            EditorGUI.EndProperty();
        }
    }
}

