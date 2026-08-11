using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const string DefaultClassName = "ResourcePaths";

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

                DrawRefreshButton(RefreshData);
                GUILayout.Space(4);

                DrawAssetsFolderField(_serializedObject, _serializedPropScriptSaveFolder);
                GUILayout.Space(4);

                DrawNamespaceField(_scriptNamespace, v => _scriptNamespace = v, "Resources Exporter: Namespace");
                GUILayout.Space(4);

                DrawClassNameField(_scriptClassName, v => _scriptClassName = v, "Resources Exporter: Class Name", DefaultClassName);
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
                : DefaultClassName;
            string scriptBody = BuildScriptBody(className);
            string filePath = FileCreator.WriteScript(systemPath, className, scriptBody);

            AssetDatabase.Refresh();

            Debug.Log($"Resources path script generated. path: {filePath}");
        }

        private string BuildScriptBody(string className)
        {
            var byRoot = _entries.GroupBy(e => e.ResourcesRoot).OrderBy(g => g.Key).ToList();

            List<object> classContent;
            if (byRoot.Count == 1)
            {
                classContent = BuildFolderContent(BuildTree(byRoot[0]));
            }
            else
            {
                classContent = new List<object>();
                foreach (var group in byRoot)
                {
                    string parentDir = Path.GetFileName(Path.GetDirectoryName(group.Key)) ?? "Root";
                    string rootClassName = SanitizeIdentifier(parentDir);
                    classContent.Add(new ScriptBuilder.CodeBlock
                    {
                        Header = $"public static class {rootClassName}",
                        Content = BuildFolderContent(BuildTree(group))
                    });
                }
            }

            var classBlock = new ScriptBuilder.CodeBlock
            {
                Header = $"public static class {className}",
                Content = classContent
            };

            string usingStatement = "using System.Collections.Generic;" + System.Environment.NewLine
                + "using Mu3Library.Resource.Data;" + System.Environment.NewLine + System.Environment.NewLine;

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

        private static List<object> BuildFolderContent(FolderNode node, string folderPath = "")
        {
            var content = new List<object>();
            var fieldNames = new List<string>();

            if (!string.IsNullOrEmpty(folderPath))
            {
                content.Add($"public const string FolderPath = \"{folderPath}\";");
                content.Add("");
            }

            foreach (var kvp in node.Assets)
            {
                string identifier = SanitizeIdentifier(kvp.Value.ResourcePath);
                string resPath = kvp.Value.ResourcePath;
                string assetName = kvp.Value.Name;
                content.Add($"public static readonly ResourcePathData {identifier} = new ResourcePathData(\"{resPath}\", \"{assetName}\");");
                fieldNames.Add(identifier);
            }

            if (fieldNames.Count > 0)
            {
                content.Add("");
                var allEntries = fieldNames.Select(n => (object)$"{n},").ToList();
                content.Add(new ScriptBuilder.CodeBlock
                {
                    Header = "public static readonly IReadOnlyList<ResourcePathData> All = new ResourcePathData[]",
                    Content = allEntries,
                    Suffix = ";"
                });
            }

            if (node.Assets.Count > 0 && node.Subfolders.Count > 0)
                content.Add("");

            foreach (var kvp in node.Subfolders)
            {
                string childClassName = SanitizeIdentifier(kvp.Key);
                string childPath = string.IsNullOrEmpty(folderPath) ? kvp.Key : $"{folderPath}/{kvp.Key}";
                List<object> childContent = BuildFolderContent(kvp.Value, childPath);
                content.Add(new ScriptBuilder.CodeBlock
                {
                    Header = $"public static class {childClassName}",
                    Content = childContent
                });
            }

            return content;
        }

        private static string SanitizeIdentifier(string name)
            => ScriptIdentifier.SanitizePascal(name);
    }
}
