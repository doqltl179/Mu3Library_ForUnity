using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Observable
{
    /// <summary>
    /// Draws the serialized rows of an <c>ObservableDictionary</c> as a list. A row whose key an
    /// earlier row already uses, or an object key left empty, is kept in the list but is not in
    /// the dictionary; the drawer names those rows in a warning under the list instead of
    /// resetting or merging them.
    /// </summary>
    [CustomPropertyDrawer(typeof(Mu3Library.Observable.ObservableDictionary<,>))]
    public class ObservableDictionaryDrawer : PropertyDrawer
    {
        private const string EntriesFieldName = "_entries";
        private const string KeyFieldName = "Key";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty entriesProp = property.FindPropertyRelative(EntriesFieldName);
            if (entriesProp == null)
            {
                return base.GetPropertyHeight(property, label);
            }

            float height = EditorGUI.GetPropertyHeight(entriesProp, label, true);

            string ignoredRows = DescribeIgnoredRows(entriesProp);
            if (ignoredRows != null)
            {
                height += EditorGUIUtility.standardVerticalSpacing + GetHelpBoxHeight(ignoredRows);
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty entriesProp = property.FindPropertyRelative(EntriesFieldName);
            if (entriesProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Unsupported type.");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            var observableLabel = new GUIContent($"[Observable] {label.text}", label.tooltip);
            Rect listRect = position;
            listRect.height = EditorGUI.GetPropertyHeight(entriesProp, observableLabel, true);

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(listRect, entriesProp, observableLabel, true);
            if (EditorGUI.EndChangeCheck())
            {
                entriesProp.serializedObject.ApplyModifiedProperties();
                ObservableDrawerUtility.InvokeOnTargets(property, "RefreshFromSerialized");
            }

            string ignoredRows = DescribeIgnoredRows(entriesProp);
            if (ignoredRows != null)
            {
                Rect helpRect = position;
                helpRect.y = listRect.yMax + EditorGUIUtility.standardVerticalSpacing;
                helpRect.height = GetHelpBoxHeight(ignoredRows);
                EditorGUI.HelpBox(helpRect, ignoredRows, MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Names the rows the dictionary leaves out, by the same rule it builds itself with: the
        /// first row of a key wins, and a row whose object key is empty has no key. Answers null
        /// when every row is in the dictionary.
        /// </summary>
        internal static string DescribeIgnoredRows(SerializedProperty entriesProp)
        {
            if (!entriesProp.isArray || entriesProp.arraySize == 0)
            {
                return null;
            }

            var activeKeys = new List<KeyValuePair<int, SerializedProperty>>(entriesProp.arraySize);
            StringBuilder message = null;

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                SerializedProperty keyProp = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative(KeyFieldName);
                if (keyProp == null)
                {
                    continue;
                }

                string problem = null;
                if (keyProp.propertyType == SerializedPropertyType.ObjectReference && keyProp.objectReferenceValue == null)
                {
                    problem = $"Element {i} has no key.";
                }
                else
                {
                    for (int j = 0; j < activeKeys.Count; j++)
                    {
                        if (SerializedProperty.DataEquals(keyProp, activeKeys[j].Value))
                        {
                            problem = $"Element {i} has the same key as element {activeKeys[j].Key}.";
                            break;
                        }
                    }
                }

                if (problem == null)
                {
                    activeKeys.Add(new KeyValuePair<int, SerializedProperty>(i, keyProp));
                    continue;
                }

                message ??= new StringBuilder();
                message.AppendLine(problem);
            }

            if (message == null)
            {
                return null;
            }

            message.Append("These rows stay in the list but are not in the dictionary.");
            return message.ToString();
        }

        private static float GetHelpBoxHeight(string message)
        {
            int lineCount = 1;
            for (int i = 0; i < message.Length; i++)
            {
                if (message[i] == '\n')
                {
                    lineCount++;
                }
            }

            // The warning icon needs about two lines even for a one-line message.
            return Mathf.Max(lineCount, 2) * EditorGUIUtility.singleLineHeight + 4f;
        }
    }
}
