using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mu3Library.Editor.FileUtil;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class ResourcesPathExporterDrawer : Mu3WindowDrawer
    {
        public const string FileName = "ResourcesPathExporter";
        private const string ItemName = "Resources Path Exporter";
        private const string MenuName = MenuRoot + "/" + ItemName;

        [SerializeField, HideInInspector] private DefaultAsset _scriptSaveFolder;
        [SerializeField, HideInInspector] private string _scriptNamespace = "";
        [SerializeField, HideInInspector] private string _scriptClassName = "";

        [SerializeField, HideInInspector] private bool _foldoutPreview = false;

        private List<ResourceEntry> _entries = new();
        private bool _isDataLoaded = false;

        private const int GridColumns = 4;

        private class ResourceEntry
        {
            public string ResourcesRoot;
            public string ResourcePath;
            public string Name;
        }

        private class FolderNode
        {
            public string Name;
            public SortedDictionary<string, FolderNode> Subfolders = new();
            public SortedList<string, ResourceEntry> Assets = new();
        }

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

                DrawResourcesPreview();
                GUILayout.Space(8);

                DrawValidationAndButton();

            }, 20, 20, 0, 0);
        }

        private void RefreshData()
        {
            _entries = CollectResourceEntries();
            _isDataLoaded = true;
        }

        private static List<ResourceEntry> CollectResourceEntries()
        {
            const string resourcesFolder = "/Resources/";
            return AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/") && p.Contains(resourcesFolder))
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Select(p =>
                {
                    int idx = p.LastIndexOf(resourcesFolder);
                    string resourcePath = p.Substring(idx + resourcesFolder.Length);
                    // Strip extension
                    string noExt = resourcePath.Contains('.')
                        ? resourcePath.Substring(0, resourcePath.LastIndexOf('.'))
                        : resourcePath;
                    string name = Path.GetFileNameWithoutExtension(p);
                    return new ResourceEntry
                    {
                        ResourcesRoot = p.Substring(0, idx + resourcesFolder.Length - 1),
                        ResourcePath = noExt,
                        Name = name,
                    };
                })
                .Where(e => !string.IsNullOrEmpty(e.ResourcePath))
                .OrderBy(e => e.ResourcePath)
                .ToList();
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
                "Resources Exporter: Namespace");
        }

        private void DrawClassNameField()
        {
            DrawWithUndo(
                () => EditorGUILayout.TextField("Class Name (optional)", _scriptClassName),
                v => _scriptClassName = v,
                "Resources Exporter: Class Name");

            if (string.IsNullOrWhiteSpace(_scriptClassName))
            {
                EditorGUILayout.HelpBox("Default class name: ResourcePaths", MessageType.None);
            }
        }

        private void DrawResourcesPreview()
        {
            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No Resources assets found. Click Refresh.", MessageType.Info);
                return;
            }

            string countLabel = $"Resources Preview  ({_entries.Count} asset(s))";
            DrawFoldoutHeader2(countLabel, ref _foldoutPreview);

            if (!_foldoutPreview) return;

            DrawStruct(() =>
            {
                var byRoot = _entries.GroupBy(e => e.ResourcesRoot).OrderBy(g => g.Key);
                foreach (var group in byRoot)
                {
                    EditorGUILayout.LabelField($"[{group.Key}]", EditorStyles.boldLabel);

                    DrawStruct(() =>
                    {
                        float availWidth = EditorGUILayout.GetControlRect(false, 0).width;
                        float colWidth = Mathf.Floor(availWidth / GridColumns);
                        float lineHeight = EditorGUIUtility.singleLineHeight;

                        var sortedEntries = group.OrderBy(e => e.ResourcePath).ToList();
                        for (int i = 0; i < sortedEntries.Count; i += GridColumns)
                        {
                            Rect rowRect = EditorGUILayout.GetControlRect(false, lineHeight);
                            for (int col = 0; col < GridColumns; col++)
                            {
                                int idx = i + col;
                                if (idx >= sortedEntries.Count) break;
                                Rect cellRect = new Rect(
                                    rowRect.x + col * colWidth,
                                    rowRect.y,
                                    colWidth,
                                    rowRect.height);
                                EditorGUI.LabelField(cellRect, $"• {sortedEntries[idx].ResourcePath}");
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
            if (_entries.Count == 0)
                return "No Resources assets found. Click Refresh.";

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
                : "ResourcePaths";
            string scriptBody = BuildScriptBody(className);
            string filePath = Path.Combine(systemPath, $"{className}.cs");

            File.WriteAllText(filePath, scriptBody, new UTF8Encoding(true));

            AssetDatabase.Refresh();

            Debug.Log($"Resources path script generated. path: {filePath}");
        }

        private string BuildScriptBody(string className)
        {
            FolderNode root = BuildTree(_entries);
            var classContent = BuildFolderContent(root);

            var classBlock = new ScriptBuilder.CodeBlock
            {
                Header = $"public static class {className}",
                Content = classContent
            };

            string usingStatement = "using Mu3Library.Resource.Data;" + System.Environment.NewLine + System.Environment.NewLine;

            if (!string.IsNullOrWhiteSpace(_scriptNamespace))
            {
                var namespaceBlock = new ScriptBuilder.CodeBlock
                {
                    Header = $"namespace {_scriptNamespace.Trim()}",
                    Content = new List<object> { classBlock }
                };
                return usingStatement + ScriptBuilder.Build(4, namespaceBlock);
            }

            return usingStatement + ScriptBuilder.Build(4, classBlock);
        }

        private static FolderNode BuildTree(IEnumerable<ResourceEntry> entries)
        {
            var root = new FolderNode { Name = "" };
            foreach (ResourceEntry entry in entries)
            {
                string[] parts = entry.ResourcePath.Split('/');
                FolderNode current = root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (!current.Subfolders.TryGetValue(parts[i], out FolderNode child))
                    {
                        child = new FolderNode { Name = parts[i] };
                        current.Subfolders[parts[i]] = child;
                    }
                    current = child;
                }
                string assetName = parts[parts.Length - 1];
                if (!current.Assets.ContainsKey(assetName))
                    current.Assets.Add(assetName, entry);
            }
            return root;
        }

        private static List<object> BuildFolderContent(FolderNode node)
        {
            var content = new List<object>();

            foreach (var kvp in node.Assets)
            {
                string identifier = SanitizeIdentifier(kvp.Key);
                string resPath = kvp.Value.ResourcePath;
                string assetName = kvp.Value.Name;
                content.Add($"public static readonly ResourcePathData {identifier} = new ResourcePathData(\"{resPath}\", \"{assetName}\");");
            }

            if (node.Assets.Count > 0 && node.Subfolders.Count > 0)
                content.Add("");

            foreach (var kvp in node.Subfolders)
            {
                List<object> childContent = BuildFolderContent(kvp.Value);
                content.Add(new ScriptBuilder.CodeBlock
                {
                    Header = $"public static class {SanitizeIdentifier(kvp.Key)}",
                    Content = childContent
                });
            }

            return content;
        }

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_";

            var sb = new StringBuilder();
            bool capitalizeNext = false;
            bool isFirst = true;
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (isFirst)
                    {
                        sb.Append(char.ToUpperInvariant(c));
                        isFirst = false;
                    }
                    else if (capitalizeNext && char.IsLetter(c))
                    {
                        sb.Append(char.ToUpperInvariant(c));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    capitalizeNext = false;
                }
                else
                {
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
