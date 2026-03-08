#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mu3Library.Editor.FileUtil;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class LocalizationNameExporterDrawer : Mu3WindowDrawer
    {
        public const string FileName = "LocalizationNameExporter";
        private const string ItemName = "Localization Name Exporter";
        private const string MenuName = MenuRoot + "/" + ItemName;

        [SerializeField, HideInInspector] private DefaultAsset _scriptSaveFolder;
        [SerializeField, HideInInspector] private string _scriptNamespace = "";
        [SerializeField, HideInInspector] private string _scriptClassName = "";

        [SerializeField, HideInInspector] private bool _foldoutTablePreview = false;

        private List<StringTableCollection> _tableCollections = new();
        private bool _isDataLoaded = false;

        private const int GridColumns = 4;

        private SerializedObject m_serializedObject;
        private SerializedObject _serializedObject
        {
            get
            {
                if (m_serializedObject == null)
                    m_serializedObject = new SerializedObject(this);
                return m_serializedObject;
            }
        }

        private SerializedProperty m_serializedPropScriptSaveFolder;
        private SerializedProperty _serializedPropScriptSaveFolder
        {
            get
            {
                if (m_serializedPropScriptSaveFolder == null)
                    m_serializedPropScriptSaveFolder = _serializedObject.FindProperty(nameof(_scriptSaveFolder));
                return m_serializedPropScriptSaveFolder;
            }
        }



        public override void OnBecameVisible()
        {
            base.OnBecameVisible();
            RefreshData();
        }

        public override void OnGUIHeader()
        {
            DrawFoldoutHeader1(ItemName, ref _foldout);
        }

        public override void OnGUIBody()
        {
            if (!_foldout) return;

            DrawStruct(() =>
            {
                if (!_isDataLoaded)
                    RefreshData();

                DrawRefreshButton();
                GUILayout.Space(4);

                DrawScriptSaveFolderField();
                GUILayout.Space(4);

                DrawNamespaceField();
                GUILayout.Space(4);

                DrawClassNameField();
                GUILayout.Space(8);

                DrawTablePreview();
                GUILayout.Space(8);

                DrawValidationAndButton();

            }, 20, 20, 0, 0);
        }

        private void RefreshData()
        {
            _tableCollections = LocalizationEditorSettings
                .GetStringTableCollections()
                .OfType<StringTableCollection>()
                .ToList();

            _isDataLoaded = true;
        }

        private void DrawRefreshButton()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(80), GUILayout.Height(24)))
            {
                RefreshData();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawScriptSaveFolderField()
        {
            _serializedObject.Update();
            EditorGUILayout.PropertyField(_serializedPropScriptSaveFolder, new GUIContent("Script Save Folder"));
            if (_serializedObject.ApplyModifiedProperties() && _scriptSaveFolder != null)
            {
                if (!IsAssetsFolder(_scriptSaveFolder))
                {
                    Debug.LogWarning("Selected folder is not inside the Assets folder.");
                    _scriptSaveFolder = null;
                    _serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private void DrawNamespaceField()
        {
            DrawWithUndo(
                () => EditorGUILayout.TextField("Namespace (optional)", _scriptNamespace),
                v => _scriptNamespace = v,
                "Localization Exporter: Namespace");
        }

        private void DrawClassNameField()
        {
            DrawWithUndo(
                () => EditorGUILayout.TextField("Class Name (optional)", _scriptClassName),
                v => _scriptClassName = v,
                "Localization Exporter: Class Name");

            if (string.IsNullOrWhiteSpace(_scriptClassName))
            {
                EditorGUILayout.HelpBox("Default class name: LocalizationKeys", MessageType.None);
            }
        }

        private void DrawTablePreview()
        {
            if (_tableCollections.Count == 0)
            {
                EditorGUILayout.HelpBox("No String Table Collections found. Click Refresh.", MessageType.Info);
                return;
            }

            string countLabel = $"String Tables Preview  ({_tableCollections.Count} table(s))";
            DrawFoldoutHeader2(countLabel, ref _foldoutTablePreview);

            if (!_foldoutTablePreview) return;

            DrawStruct(() =>
            {
                foreach (StringTableCollection collection in _tableCollections)
                {
                    EditorGUILayout.LabelField($"[Table]  {collection.TableCollectionName}", EditorStyles.boldLabel);

                    DrawStruct(() =>
                    {
                        var entries = collection.SharedData?.Entries;
                        if (entries == null || entries.Count == 0)
                        {
                            EditorGUILayout.LabelField("(No keys)", EditorStyles.miniLabel);
                            return;
                        }

                        float availWidth = EditorGUILayout.GetControlRect(false, 0).width;
                        float colWidth = Mathf.Floor(availWidth / GridColumns);
                        float lineHeight = EditorGUIUtility.singleLineHeight;

                        for (int i = 0; i < entries.Count; i += GridColumns)
                        {
                            Rect rowRect = EditorGUILayout.GetControlRect(false, lineHeight);
                            for (int col = 0; col < GridColumns; col++)
                            {
                                int idx = i + col;
                                if (idx >= entries.Count) break;
                                Rect cellRect = new Rect(
                                    rowRect.x + col * colWidth,
                                    rowRect.y,
                                    colWidth,
                                    rowRect.height);
                                EditorGUI.LabelField(cellRect, $"• {entries[idx].Key}");
                            }
                        }
                    }, 16);

                    GUILayout.Space(4);
                }
            }, 8);
        }

        private void DrawValidationAndButton()
        {
            string firstWarning = GetFirstWarning();

            if (firstWarning != null)
            {
                EditorGUILayout.HelpBox(firstWarning, MessageType.Warning);
                return;
            }

            if (GUILayout.Button("Generate C# Script", GUILayout.Height(30)))
            {
                GenerateScript();
            }
        }

        private string GetFirstWarning()
        {
            if (_tableCollections.Count == 0)
                return "No String Table Collections found. Click Refresh.";

            if (_scriptSaveFolder == null)
                return "Script Save Folder is not set. Drag & drop a project folder.";

            return null;
        }

        private void GenerateScript()
        {
            string assetPath = FileFinder.GetAssetPath(_scriptSaveFolder);
            string systemPath = FilePathConvertor.AssetPathToSystemPath(assetPath);

            if (!Directory.Exists(systemPath))
            {
                Debug.LogWarning($"Folder not found. name: {_scriptSaveFolder.name}");
                _scriptSaveFolder = null;
                _serializedObject.ApplyModifiedProperties();
                return;
            }

            string className = !string.IsNullOrWhiteSpace(_scriptClassName)
                ? SanitizeIdentifier(_scriptClassName.Trim())
                : "LocalizationKeys";
            string scriptBody = BuildScriptBody(className);
            string filePath = Path.Combine(systemPath, $"{className}.cs");

            File.WriteAllText(filePath, scriptBody, new UTF8Encoding(true));

            AssetDatabase.Refresh();

            Debug.Log($"Localization name script generated. path: {filePath}");
        }

        private string BuildScriptBody(string className)
        {
            var lines = new List<object>();

            foreach (StringTableCollection collection in _tableCollections)
            {
                string tableClassName = SanitizeIdentifier(collection.TableCollectionName);
                var tableLines = new List<object>();

                tableLines.Add($"public const string Name = \"{collection.TableCollectionName}\";");
                tableLines.Add("");

                var entries = collection.SharedData?.Entries;
                if (entries != null)
                {
                    foreach (SharedTableData.SharedTableEntry entry in entries)
                    {
                        string keyClassName = SanitizeIdentifier(entry.Key);
                        tableLines.Add(new ScriptBuilder.CodeBlock
                        {
                            Header = $"public static class {keyClassName}",
                            Content = new List<object>
                            {
                                $"public const string Key = \"{entry.Key}\";",
                                $"public const string Id = \"{entry.Id}\";"
                            }
                        });
                    }
                }

                lines.Add(new ScriptBuilder.CodeBlock
                {
                    Header = $"public static class {tableClassName}",
                    Content = tableLines
                });
            }

            var classBlock = new ScriptBuilder.CodeBlock
            {
                Header = $"public static class {className}",
                Content = lines
            };

            if (!string.IsNullOrWhiteSpace(_scriptNamespace))
            {
                var namespaceBlock = new ScriptBuilder.CodeBlock
                {
                    Header = $"namespace {_scriptNamespace.Trim()}",
                    Content = new List<object> { classBlock }
                };
                return ScriptBuilder.Build(4, namespaceBlock);
            }

            return ScriptBuilder.Build(4, classBlock);
        }

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";

            var sb = new StringBuilder();
            bool capitalizeNext = true;
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (capitalizeNext && char.IsLetter(c))
                    {
                        sb.Append(char.ToUpperInvariant(c));
                        capitalizeNext = false;
                    }
                    else
                    {
                        sb.Append(c);
                        capitalizeNext = false;
                    }
                }
                else if (c == '_')
                {
                    sb.Append('_');
                    capitalizeNext = true;
                }
                else
                {
                    // '-' and other non-identifier chars act as PascalCase word boundary
                    capitalizeNext = true;
                }
            }

            if (sb.Length > 0 && char.IsDigit(sb[0]))
                sb.Insert(0, '_');

            return sb.ToString();
        }

        private static bool IsAssetsFolder(DefaultAsset folder)
        {
            string path = FileFinder.GetAssetPath(folder);
            return !string.IsNullOrEmpty(path) && (path == "Assets" || path.StartsWith("Assets/"));
        }
    }
}
#endif
