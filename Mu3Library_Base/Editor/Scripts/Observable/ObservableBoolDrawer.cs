using Mu3Library.Observable;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    [CustomPropertyDrawer(typeof(ObservableBool))]
    public class ObservableBoolDrawer : ObservablePropertyDrawer<bool>
    {
        protected override void DrawField(Rect position, SerializedProperty valueProp, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, valueProp);
            EditorGUI.BeginChangeCheck();

            bool newValue = EditorGUI.Toggle(position, $"[Observable] {label}", valueProp.boolValue);

            if (EditorGUI.EndChangeCheck())
            {
                valueProp.boolValue = newValue;
                ObservableDrawerUtility.ApplyAndNotify(valueProp, newValue);
            }

            EditorGUI.EndProperty();
        }
    }
}
