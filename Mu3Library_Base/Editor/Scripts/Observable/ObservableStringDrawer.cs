using Mu3Library.Observable;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    [CustomPropertyDrawer(typeof(ObservableString))]
    public class ObservableStringDrawer : ObservablePropertyDrawer<string>
    {
        protected override void DrawField(Rect position, SerializedProperty valueProp, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, valueProp);
            EditorGUI.BeginChangeCheck();

            string newValue = EditorGUI.TextField(position, $"[Observable] {label}", valueProp.stringValue);

            if (EditorGUI.EndChangeCheck())
            {
                valueProp.stringValue = newValue;
                ObservableDrawerUtility.ApplyAndNotify(valueProp, newValue);
            }

            EditorGUI.EndProperty();
        }
    }
}

