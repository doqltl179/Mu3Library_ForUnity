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
                ObservableDrawerUtility.InvokeOnTargets(
                    property,
                    nameof(Mu3Library.Observable.ObservableDictionary<int, int>.RefreshFromSerialized));
            }

            string ignoredRows = DescribeIgnoredRows(entriesProp);
            if (ignoredRows != null)
            {
                Rect helpRect = position;
                helpRect.y = listRect.yMax + EditorGUIUtility.standardVerticalSpacing;
                helpRect.height = Mathf.Max(position.yMax - helpRect.y, GetHelpBoxHeight(ignoredRows));
                EditorGUI.HelpBox(helpRect, ignoredRows, MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Names the rows the dictionary leaves out, by the same rule it builds itself with: the
        /// first row of a key wins, and a row whose object key is empty has no key. Keys are
        /// compared by their serialized data, which matches the runtime for keys whose equality
        /// is their data. Answers null when every row is in the dictionary.
        /// </summary>
        internal static string DescribeIgnoredRows(SerializedProperty entriesProp)
        {
            if (!entriesProp.isArray || entriesProp.arraySize == 0)
            {
                return null;
            }

            // Rows are grouped by the hash of their key data so that only keys in the same group
            // are compared, which keeps a long list linear.
            var activeKeysByHash = new Dictionary<uint, List<KeyValuePair<int, SerializedProperty>>>();
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
                    uint hash = keyProp.contentHash;
                    if (!activeKeysByHash.TryGetValue(hash, out List<KeyValuePair<int, SerializedProperty>> sameHash))
                    {
                        sameHash = new List<KeyValuePair<int, SerializedProperty>>(1);
                        activeKeysByHash.Add(hash, sameHash);
                    }

                    for (int j = 0; j < sameHash.Count; j++)
                    {
                        if (SerializedProperty.DataEquals(keyProp, sameHash[j].Value))
                        {
                            problem = $"Element {i} has the same key as element {sameHash[j].Key}.";
                            break;
                        }
                    }

                    if (problem == null)
                    {
                        sameHash.Add(new KeyValuePair<int, SerializedProperty>(i, keyProp));
                        continue;
                    }
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

        /// <summary>
        /// The help box height for the inspector's current width, so a message that wraps in a
        /// narrow inspector is not cut off. The warning icon takes about 40 pixels of the width
        /// and needs two lines even for a one-line message.
        /// </summary>
        private static float GetHelpBoxHeight(string message)
        {
            float textWidth = Mathf.Max(EditorGUIUtility.currentViewWidth - EditorGUI.indentLevel * 15f - 70f, 100f);
            float textHeight = EditorStyles.helpBox.CalcHeight(new GUIContent(message), textWidth);
            return Mathf.Max(textHeight, 2f * EditorGUIUtility.singleLineHeight + 4f);
        }
    }
}
