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
            EditorGUI.BeginChangeCheck();

            long newValue = EditorGUI.LongField(position, $"[Observable] {label}", valueProp.longValue);

            if (EditorGUI.EndChangeCheck())
            {
                valueProp.longValue = newValue;
                ObservableDrawerUtility.ApplyAndNotify(valueProp, newValue);
            }

            EditorGUI.EndProperty();
        }
    }
}

