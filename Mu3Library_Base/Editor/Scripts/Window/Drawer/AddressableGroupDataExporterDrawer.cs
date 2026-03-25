#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mu3Library.Editor.FileUtil;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class AddressableGroupDataExporterDrawer : Mu3WindowDrawer
    {
        public const string FileName = "AddressableGroupDataExporter";
        private const string ItemName = "Addressable Group Data Exporter";
        private const string MenuName = MenuRoot + "/" + ItemName;

        [SerializeField, HideInInspector] private DefaultAsset _scriptSaveFolder;
        [SerializeField, HideInInspector] private string _scriptNamespace = "";
        [SerializeField, HideInInspector] private string _scriptClassName = "";

        [SerializeField, HideInInspector] private bool _foldoutGroupPreview = false;

        private List<AddressableAssetGroup> _groups = new();
        private bool _isDataLoaded = false;

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

                DrawGroupPreview();
                GUILayout.Space(8);

                DrawValidationAndButton();

            }, 20, 20, 0, 0);
        }

        private void RefreshData()
        {
            _groups = new List<AddressableAssetGroup>();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                _groups = settings.groups
                    .Where(g => g != null)
                    .OrderBy(g => g.Name)
                    .ToList();
            }
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
                "Addressable Exporter: Namespace");
        }

        private void DrawClassNameField()
        {
            DrawWithUndo(
                () => EditorGUILayout.TextField("Class Name (optional)", _scriptClassName),
                v => _scriptClassName = v,
                "Addressable Exporter: Class Name");

            if (string.IsNullOrWhiteSpace(_scriptClassName))
            {
                EditorGUILayout.HelpBox("Default class name: AddressableGroups", MessageType.None);
            }
        }

        private void DrawGroupPreview()
        {
            if (!_isDataLoaded || _groups.Count == 0)
            {
                EditorGUILayout.HelpBox("No Addressable Groups found. Click Refresh.", MessageType.Info);
                return;
            }

            string countLabel = $"Addressable Groups Preview  ({_groups.Count} group(s))";
            DrawFoldoutHeader2(countLabel, ref _foldoutGroupPreview);

            if (!_foldoutGroupPreview) return;

            DrawStruct(() =>
            {
                foreach (AddressableAssetGroup group in _groups)
                {
                    var entries = group.entries?.OrderBy(e => e.address).ToList();
                    int entryCount = entries?.Count ?? 0;

                    EditorGUILayout.LabelField($"[Group]  {group.Name}  ({entryCount} asset(s))", EditorStyles.boldLabel);

                    if (entryCount > 0)
                    {
                        DrawStruct(() =>
                        {
                            foreach (AddressableAssetEntry entry in entries)
                            {
                                bool isFolder = AssetDatabase.IsValidFolder(entry.AssetPath);
                                string assetName = isFolder
                                    ? Path.GetFileName(entry.AssetPath)
                                    : Path.GetFileNameWithoutExtension(entry.AssetPath);
                                string labelStr = entry.labels.Count > 0
                                    ? string.Join(", ", entry.labels.OrderBy(l => l))
                                    : "(no labels)";
                                string prefix = isFolder ? "• [Folder]" : "•";

                                EditorGUILayout.LabelField(
                                    $"{prefix} {assetName}  |  Address: {entry.address}  |  Labels: {labelStr}",
                                    EditorStyles.miniLabel);

                                if (isFolder)
                                {
                                    var subEntries = new List<AddressableAssetEntry>();
                                    entry.GatherAllAssets(subEntries, false, true, false);
                                    DrawStruct(() =>
                                    {
                                        foreach (AddressableAssetEntry sub in subEntries.OrderBy(s => s.address))
                                        {
                                            string subName = Path.GetFileNameWithoutExtension(sub.AssetPath);
                                            string subLabelStr = sub.labels.Count > 0
                                                ? string.Join(", ", sub.labels.OrderBy(l => l))
                                                : "(no labels)";
                                            EditorGUILayout.LabelField(
                                                $"  \u21b3 {subName}  |  Address: {sub.address}  |  Labels: {subLabelStr}",
                                                EditorStyles.miniLabel);
                                        }
                                    }, 12);
                                }
                            }
                        }, 16);
                    }

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
            if (!_isDataLoaded || _groups.Count == 0)
                return "No Addressable Groups found. Click Refresh.";

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
                : "AddressableGroups";
            string scriptBody = BuildScriptBody(className);
            string filePath = Path.Combine(systemPath, $"{className}.cs");

            File.WriteAllText(filePath, scriptBody, new UTF8Encoding(true));

            AssetDatabase.Refresh();

            Debug.Log($"Addressable group name script generated. path: {filePath}");
        }

        private string BuildScriptBody(string className)
        {
            var lines = new List<object>();

            // Root-level Groups class: All array + const string per group referencing each group's Name
            var groupsContent = new List<object>();
            var groupIdentifiers = _groups.Select(g => SanitizeIdentifier(g.Name)).ToList();
            groupsContent.Add($"public static readonly string[] All = new string[] {{ {string.Join(", ", groupIdentifiers)} }};");
            groupsContent.Add("");
            foreach (AddressableAssetGroup g in _groups)
            {
                string gName = SanitizeIdentifier(g.Name);
                groupsContent.Add($"public const string {gName} = {className}.{gName}.Name;");
            }
            lines.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static class Groups",
                Content = groupsContent
            });
            lines.Add("");

            // Root-level Labels class: All array + const string per unique label
            var sortedGlobalLabels = CollectAllLabels(_groups);
            var labelsContent = new List<object>();
            var labelIdentifiers = sortedGlobalLabels.Select(l => SanitizeIdentifier(l)).ToList();
            labelsContent.Add($"public static readonly string[] All = new string[] {{ {string.Join(", ", labelIdentifiers)} }};");
            labelsContent.Add("");
            foreach (string label in sortedGlobalLabels)
            {
                labelsContent.Add($"public const string {SanitizeIdentifier(label)} = \"{label}\";");
            }
            lines.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static class Labels",
                Content = labelsContent
            });
            lines.Add("");

            foreach (AddressableAssetGroup group in _groups)
            {
                string groupClassName = SanitizeIdentifier(group.Name);
                var groupLines = new List<object>();

                groupLines.Add($"public const string Name = \"{group.Name}\";");
                groupLines.Add("");

                var entries = group.entries?.OrderBy(e => e.address).ToList();
                if (entries != null && entries.Count > 0)
                {
                    var allNames = entries
                        .Select(e => AssetDatabase.IsValidFolder(e.AssetPath)
                            ? Path.GetFileName(e.AssetPath)
                            : Path.GetFileNameWithoutExtension(e.AssetPath))
                        .ToList();
                    var allAddresses = entries.Select(e => e.address).ToList();

                    groupLines.Add(new ScriptBuilder.ArrayBlock { FieldName = "AllNames", Values = allNames });
                    groupLines.Add("");
                    groupLines.Add(new ScriptBuilder.ArrayBlock { FieldName = "AllAddresses", Values = allAddresses });
                    groupLines.Add("");

                    // Per-group Labels: unique labels across all entries in this group, referencing root Labels
                    var groupLabelsSeen = new HashSet<string>();
                    void CollectGroupLabels(AddressableAssetEntry e)
                    {
                        foreach (string l in e.labels) groupLabelsSeen.Add(l);
                        if (AssetDatabase.IsValidFolder(e.AssetPath))
                        {
                            var subs = new List<AddressableAssetEntry>();
                            e.GatherAllAssets(subs, false, true, false);
                            foreach (var sub in subs) CollectGroupLabels(sub);
                        }
                    }
                    foreach (AddressableAssetEntry e in entries) CollectGroupLabels(e);

                    var groupUniqueLabels = groupLabelsSeen.OrderBy(l => l).ToList();
                    if (groupUniqueLabels.Count > 0)
                    {
                        var groupLabelIdentifiers = groupUniqueLabels.Select(l => SanitizeIdentifier(l)).ToList();
                        var groupLabelsContent = new List<object>();
                        groupLabelsContent.Add($"public static readonly string[] All = new string[] {{ {string.Join(", ", groupLabelIdentifiers)} }};");
                        groupLabelsContent.Add("");
                        foreach (string label in groupUniqueLabels)
                            groupLabelsContent.Add($"public const string {SanitizeIdentifier(label)} = {className}.Labels.{SanitizeIdentifier(label)};");
                        groupLines.Add(new ScriptBuilder.CodeBlock
                        {
                            Header = "public static class Labels",
                            Content = groupLabelsContent
                        });
                        groupLines.Add("");
                    }

                    foreach (AddressableAssetEntry entry in entries)
                    {
                        groupLines.Add(BuildEntryBlock(entry, className));
                    }
                }

                lines.Add(new ScriptBuilder.CodeBlock
                {
                    Header = $"public static class {groupClassName}",
                    Content = groupLines
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

        private ScriptBuilder.CodeBlock BuildEntryBlock(AddressableAssetEntry entry, string rootClassName, string parentClassName = null)
        {
            bool isFolder = AssetDatabase.IsValidFolder(entry.AssetPath);
            string assetName = isFolder
                ? Path.GetFileName(entry.AssetPath)
                : Path.GetFileNameWithoutExtension(entry.AssetPath);
            string entryClassName = SanitizeIdentifier(entry.address);

            if (!string.IsNullOrEmpty(parentClassName) && entryClassName.StartsWith(parentClassName))
            {
                entryClassName = entryClassName.Substring(parentClassName.Length);
                if (string.IsNullOrEmpty(entryClassName))
                    entryClassName = "_";
                else if (char.IsDigit(entryClassName[0]))
                    entryClassName = "_" + entryClassName;
            }

            var entryLines = new List<object>();
            entryLines.Add($"public const string Name = \"{assetName}\";");
            entryLines.Add($"public const string Address = \"{entry.address}\";");

            if (entry.labels.Count > 0)
            {
                var sortedLabels = entry.labels.OrderBy(l => l).ToList();
                var labelLines = new List<object>();
                foreach (string label in sortedLabels)
                {
                    string labelFieldName = SanitizeIdentifier(label);
                    labelLines.Add($"public const string {labelFieldName} = {rootClassName}.Labels.{labelFieldName};");
                }
                labelLines.Add("");
                var localLabelNames = sortedLabels.Select(l => SanitizeIdentifier(l)).ToList();
                labelLines.Add($"public static readonly string[] All = new string[] {{ {string.Join(", ", localLabelNames)} }};");
                entryLines.Add(new ScriptBuilder.CodeBlock
                {
                    Header = "public static class Labels",
                    Content = labelLines
                });
            }

            if (isFolder)
            {
                var subEntries = new List<AddressableAssetEntry>();
                entry.GatherAllAssets(subEntries, false, true, false);
                if (subEntries.Count > 0)
                {
                    var orderedSubs = subEntries.OrderBy(s => s.address).ToList();

                    var subAllNames = orderedSubs
                        .Select(s => AssetDatabase.IsValidFolder(s.AssetPath)
                            ? Path.GetFileName(s.AssetPath)
                            : Path.GetFileNameWithoutExtension(s.AssetPath))
                        .ToList();
                    var subAllAddresses = orderedSubs.Select(s => s.address).ToList();

                    var assetsLines = new List<object>();
                    assetsLines.Add(new ScriptBuilder.ArrayBlock { FieldName = "AllNames", Values = subAllNames });
                    assetsLines.Add("");
                    assetsLines.Add(new ScriptBuilder.ArrayBlock { FieldName = "AllAddresses", Values = subAllAddresses });
                    assetsLines.Add("");
                    assetsLines.AddRange(orderedSubs.Select(s => (object)BuildEntryBlock(s, rootClassName, entryClassName)));

                    entryLines.Add(new ScriptBuilder.CodeBlock
                    {
                        Header = "public static class Assets",
                        Content = assetsLines
                    });
                }
            }

            return new ScriptBuilder.CodeBlock
            {
                Header = $"public static class {entryClassName}",
                Content = entryLines
            };
        }

        private static List<string> CollectAllLabels(IEnumerable<AddressableAssetGroup> groups)
        {
            var seen = new HashSet<string>();
            var labels = new List<string>();

            void Collect(AddressableAssetEntry entry)
            {
                foreach (string label in entry.labels)
                    if (seen.Add(label))
                        labels.Add(label);

                if (AssetDatabase.IsValidFolder(entry.AssetPath))
                {
                    var subs = new List<AddressableAssetEntry>();
                    entry.GatherAllAssets(subs, false, true, false);
                    foreach (var sub in subs.OrderBy(s => s.address))
                        Collect(sub);
                }
            }

            foreach (AddressableAssetGroup group in groups)
                foreach (AddressableAssetEntry entry in group.entries?.OrderBy(e => e.address) ?? Enumerable.Empty<AddressableAssetEntry>())
                    Collect(entry);

            return labels.OrderBy(l => l).ToList();
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
#endif
