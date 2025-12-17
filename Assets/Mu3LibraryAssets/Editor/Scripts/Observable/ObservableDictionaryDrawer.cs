using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    [CustomPropertyDrawer(typeof(Mu3Library.Observable.ObservableDictionary<,>))]
    public class ObservableDictionaryDrawer : PropertyDrawer
    {
        private static readonly Dictionary<string, int> _lastEntrySizesByPath = new Dictionary<string, int>();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty entriesProp = property.FindPropertyRelative("_entries");
            if (entriesProp == null)
            {
                return base.GetPropertyHeight(property, label);
            }

            return EditorGUI.GetPropertyHeight(entriesProp, label, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty entriesProp = property.FindPropertyRelative("_entries");
            if (entriesProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Unsupported type.");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            var observableLabel = new GUIContent($"[Observable] {label.text}", label.tooltip);
            EditorGUI.PropertyField(position, entriesProp, observableLabel, true);

            string sizeKey = $"{property.serializedObject.targetObject.GetInstanceID()}:{property.propertyPath}";
            if (!_lastEntrySizesByPath.TryGetValue(sizeKey, out int lastSize))
            {
                lastSize = entriesProp.arraySize;
            }

            if (entriesProp.arraySize > lastSize)
            {
                for (int i = lastSize; i < entriesProp.arraySize; i++)
                {
                    SerializedProperty element = entriesProp.GetArrayElementAtIndex(i);
                    SerializedProperty keyProp = element.FindPropertyRelative("Key");
                    SerializedProperty valueProp = element.FindPropertyRelative("Value");

                    ResetProperty(keyProp);
                    ResetProperty(valueProp);
                }
            }

            _lastEntrySizesByPath[sizeKey] = entriesProp.arraySize;

            if (EditorGUI.EndChangeCheck())
            {
                entriesProp.serializedObject.ApplyModifiedProperties();
                InvokeRefreshFromSerialized(property.serializedObject.targetObject);
            }

            EditorGUI.EndProperty();
        }

        private void InvokeRefreshFromSerialized(object targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            object observableInstance = fieldInfo?.GetValue(targetObject);
            if (observableInstance == null)
            {
                return;
            }

            MethodInfo refreshMethod = observableInstance.GetType()
                .GetMethod("RefreshFromSerialized", BindingFlags.Instance | BindingFlags.Public);
            refreshMethod?.Invoke(observableInstance, null);
        }

        private static void ResetProperty(SerializedProperty prop)
        {
            if (prop == null)
            {
                return;
            }

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.longValue = 0;
                    return;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = false;
                    return;
                case SerializedPropertyType.Float:
                    prop.doubleValue = 0d;
                    return;
                case SerializedPropertyType.String:
                    prop.stringValue = string.Empty;
                    return;
                case SerializedPropertyType.Color:
                    prop.colorValue = default;
                    return;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = null;
                    return;
                case SerializedPropertyType.LayerMask:
                    prop.intValue = 0;
                    return;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = 0;
                    return;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = default;
                    return;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = default;
                    return;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = default;
                    return;
                case SerializedPropertyType.Rect:
                    prop.rectValue = default;
                    return;
                case SerializedPropertyType.Bounds:
                    prop.boundsValue = default;
                    return;
                case SerializedPropertyType.Quaternion:
                    prop.quaternionValue = Quaternion.identity;
                    return;
                case SerializedPropertyType.Vector2Int:
                    prop.vector2IntValue = default;
                    return;
                case SerializedPropertyType.Vector3Int:
                    prop.vector3IntValue = default;
                    return;
                case SerializedPropertyType.RectInt:
                    prop.rectIntValue = default;
                    return;
                case SerializedPropertyType.BoundsInt:
                    prop.boundsIntValue = default;
                    return;
                case SerializedPropertyType.ManagedReference:
                    prop.managedReferenceValue = null;
                    return;
                case SerializedPropertyType.Generic:
                    ResetGenericProperty(prop);
                    return;
                default:
                    return;
            }
        }

        private static void ResetGenericProperty(SerializedProperty prop)
        {
            SerializedProperty iterator = prop.Copy();
            SerializedProperty end = iterator.GetEndProperty();

            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                ResetProperty(iterator);
            }
        }
    }
}
