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
        [SerializeField, HideInInspector] private bool _splitByGroup = false;

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
                GUILayout.Space(4);

                DrawSplitByGroupField();
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

        private void DrawSplitByGroupField()
        {
            DrawWithUndo(
                () => EditorGUILayout.Toggle("Split by Group", _splitByGroup),
                v => _splitByGroup = v,
                "Addressable Exporter: Split by Group");

            if (_splitByGroup)
            {
                string baseClass = !string.IsNullOrWhiteSpace(_scriptClassName)
                    ? SanitizeIdentifier(_scriptClassName.Trim())
                    : "AddressableGroups";
                EditorGUILayout.HelpBox(
                    $"Generates one file per group.  Class name: {baseClass}{{GroupName}}",
                    MessageType.None);
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

            if (_splitByGroup)
            {
                foreach (AddressableAssetGroup group in _groups)
                {
                    string groupClassName = className + SanitizeIdentifier(group.Name);
                    string groupScriptBody = BuildGroupScriptBody(className, group);
                    string groupFilePath = Path.Combine(systemPath, $"{groupClassName}.cs");
                    File.WriteAllText(groupFilePath, groupScriptBody, new UTF8Encoding(true));
                    Debug.Log($"Addressable group script generated. path: {groupFilePath}");
                }

                string rootScriptBody = BuildSplitRootScriptBody(className);
                string rootFilePath = Path.Combine(systemPath, $"{className}.cs");
                File.WriteAllText(rootFilePath, rootScriptBody, new UTF8Encoding(true));
                Debug.Log($"Addressable root script generated. path: {rootFilePath}");

                AssetDatabase.Refresh();
            }
            else
            {
                string scriptBody = BuildScriptBody(className);
                string filePath = Path.Combine(systemPath, $"{className}.cs");
                File.WriteAllText(filePath, scriptBody, new UTF8Encoding(true));
                AssetDatabase.Refresh();
                Debug.Log($"Addressable group name script generated. path: {filePath}");
            }
        }

        private string BuildScriptBody(string className)
        {
            var lines = new List<object>();

            // Root-level Labels class: individual LabelData first, then All
            var sortedGlobalLabels = CollectAllLabels(_groups);
            var labelsContent = new List<object>();
            foreach (string label in sortedGlobalLabels)
                labelsContent.Add($"public static readonly LabelData {SanitizeIdentifier(label)} = new LabelData(\"{label}\");");
            labelsContent.Add("");
            var allLabelFields = sortedGlobalLabels.Select(l => $"{SanitizeIdentifier(l)},").Cast<object>().ToList();
            labelsContent.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static readonly IReadOnlyList<LabelData> All = new LabelData[]",
                Content = allLabelFields,
                Suffix = ";"
            });
            lines.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static class Labels",
                Content = labelsContent
            });
            lines.Add("");

            // Root-level Groups class: instances first, All dict, then sealed class definitions
            var groupsContent = new List<object>();

            // Instance fields declared before All so static init order is correct
            foreach (AddressableAssetGroup g in _groups)
            {
                string gName = SanitizeIdentifier(g.Name);
                groupsContent.Add($"public static readonly {gName}Data {gName} = new {gName}Data();");
            }
            groupsContent.Add("");

            // All: IReadOnlyList<GroupData>
            var allGroupEntries = _groups.Select(g =>
            {
                string gName = SanitizeIdentifier(g.Name);
                return (object)$"{gName},";
            }).ToList();
            groupsContent.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static readonly IReadOnlyList<GroupData> All = new GroupData[]",
                Content = allGroupEntries,
                Suffix = ";"
            });

            // Sealed class definitions for each group
            foreach (AddressableAssetGroup group in _groups)
            {
                string groupClassName = SanitizeIdentifier(group.Name);
                var groupLines = new List<object>();

                var entries = group.entries?.OrderBy(e => e.address).ToList();

                // Collect per-group unique labels (including sub-entries of folders)
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
                if (entries != null)
                    foreach (AddressableAssetEntry e in entries) CollectGroupLabels(e);

                var groupUniqueLabels = groupLabelsSeen.OrderBy(l => l).ToList();

                // Per-group Labels inner class: LabelData refs then All
                if (groupUniqueLabels.Count > 0)
                {
                    var groupLabelsContent = new List<object>();
                    foreach (string label in groupUniqueLabels)
                        groupLabelsContent.Add($"public static readonly LabelData {SanitizeIdentifier(label)} = {className}.Labels.{SanitizeIdentifier(label)};");
                    groupLabelsContent.Add("");
                    var allGroupLabelFields = groupUniqueLabels.Select(l => $"{SanitizeIdentifier(l)},").Cast<object>().ToList();
                    groupLabelsContent.Add(new ScriptBuilder.CodeBlock
                    {
                        Header = "public static readonly LabelData[] All = new LabelData[]",
                        Content = allGroupLabelFields,
                        Suffix = ";"
                    });
                    groupLines.Add(new ScriptBuilder.CodeBlock
                    {
                        Header = "public new static class Labels",
                        Content = groupLabelsContent
                    });
                    groupLines.Add("");
                }

                // Entries: top-level entries become instance fields for instance access
                if (entries != null && entries.Count > 0)
                {
                    foreach (AddressableAssetEntry entry in entries)
                        groupLines.AddRange(BuildTopLevelEntryLines(entry, group.Name, className));
                    groupLines.Add("");

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
                }

                // Constructor
                groupLines.Add($"internal {groupClassName}Data() : base(");
                groupLines.Add($"    \"{group.Name}\",");
                groupLines.Add("    new Dictionary<string, EntryData>");
                groupLines.Add("    {");
                if (entries != null)
                {
                    foreach (AddressableAssetEntry entry in entries)
                    {
                        bool isFolder = AssetDatabase.IsValidFolder(entry.AssetPath);
                        string entryClassName = SanitizeIdentifier(entry.address);
                        if (isFolder)
                        {
                            string shortName = SanitizeIdentifier(Path.GetFileName(entry.AssetPath));
                            groupLines.Add($"        {{ _{shortName}.Name, _{shortName} }},");
                        }
                        else
                        {
                            groupLines.Add($"        {{ _{entryClassName}.Name, _{entryClassName} }},");
                        }
                    }
                }
                groupLines.Add("    },");
                groupLines.Add("    new Dictionary<string, LabelData>");
                groupLines.Add("    {");
                foreach (string label in groupUniqueLabels)
                    groupLines.Add($"        {{ Labels.{SanitizeIdentifier(label)}.Value, Labels.{SanitizeIdentifier(label)} }},");
                groupLines.Add("    })");
                groupLines.Add("{ }");

                groupsContent.Add(new ScriptBuilder.CodeBlock
                {
                    Header = $"public sealed class {groupClassName}Data : GroupData",
                    Content = groupLines
                });
            }

            lines.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static class Groups",
                Content = groupsContent
            });

            var classBlock = new ScriptBuilder.CodeBlock
            {
                Header = $"public static class {className}",
                Content = lines
            };

            string usingStatement = "using System.Collections.Generic;" + System.Environment.NewLine + "using Mu3Library.Addressable.Data;" + System.Environment.NewLine + System.Environment.NewLine;

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

        /// <summary>
        /// Generates the entry lines for a top-level group entry.
        /// Non-folder entries become instance fields (accessible via group instance).
        /// Folder entries keep their static class structure but also get an instance field.
        /// </summary>
        private List<object> BuildTopLevelEntryLines(AddressableAssetEntry entry, string groupName, string rootClassName)
        {
            bool isFolder = AssetDatabase.IsValidFolder(entry.AssetPath);
            string assetName = isFolder
                ? Path.GetFileName(entry.AssetPath)
                : Path.GetFileNameWithoutExtension(entry.AssetPath);
            string entryClassName = SanitizeIdentifier(entry.address);
            var result = new List<object>();

            if (!isFolder)
            {
                var subAssets = new List<AddressableAssetEntry>();
                entry.GatherAllAssets(subAssets, false, false, true);
                subAssets = subAssets.OrderBy(s => s.address).ToList();

                if (subAssets.Count == 0)
                {
                    // Simple entry: private static backing + public instance field
                    result.Add($"private static readonly EntryData _{entryClassName} = new EntryData(\"{groupName}\", \"{assetName}\", \"{entry.address}\");");
                    result.Add($"public readonly EntryData {entryClassName} = _{entryClassName};");
                }
                else
                {
                    // With sub-objects: XSubAssets class + private static backing + public instance field
                    string subAssetsClassName = entryClassName + "SubAssets";
                    var subObjectFields = subAssets
                        .Select(s => (s, subName: GetSubObjectName(s.address), fieldName: SanitizeIdentifier(GetSubObjectName(s.address))))
                        .ToList();

                    var subAssetsContent = new List<object>();
                    foreach (var (s, subName, fieldName) in subObjectFields)
                        subAssetsContent.Add($"public static readonly EntryData {fieldName} = new EntryData(\"{groupName}\", \"{subName}\", \"{s.address}\");");
                    subAssetsContent.Add("");
                    var allFieldRefs = subObjectFields.Select(t => (object)$"{t.fieldName},").ToList();
                    subAssetsContent.Add(new ScriptBuilder.CodeBlock
                    {
                        Header = "public static readonly EntryData[] All = new EntryData[]",
                        Content = allFieldRefs,
                        Suffix = ";"
                    });
                    result.Add(new ScriptBuilder.CodeBlock
                    {
                        Header = $"public static class {subAssetsClassName}",
                        Content = subAssetsContent
                    });
                    result.Add("");

                    var backingLines = new List<string>();
                    backingLines.Add($"private static readonly EntryData _{entryClassName} = new EntryData(");
                    backingLines.Add($"    \"{groupName}\",");
                    backingLines.Add($"    \"{assetName}\",");
                    backingLines.Add($"    \"{entry.address}\",");
                    backingLines.Add($"    {subAssetsClassName}.All);");
                    result.Add(new ScriptBuilder.RawBlock { Lines = backingLines });
                    result.Add($"public readonly EntryData {entryClassName} = _{entryClassName};");
                }
            }
            else
            {
                // Folder: static class structure (Data + Labels + Assets) + instance field using short name
                var folderElement = BuildEntryElement(entry, groupName, rootClassName, null);
                result.Add(folderElement);
                result.Add("");

                string shortName = SanitizeIdentifier(assetName);
                result.Add($"private static readonly EntryData _{shortName} = {entryClassName}.Data;");
                result.Add($"public readonly EntryData {shortName} = _{shortName};");
            }

            return result;
        }

        private object BuildEntryElement(AddressableAssetEntry entry, string groupName, string rootClassName, string parentClassName)
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

            if (!isFolder)
            {
                // Collect sub-objects (e.g. sprites inside a texture) — excludes self
                var subAssets = new List<AddressableAssetEntry>();
                entry.GatherAllAssets(subAssets, false, false, true);
                subAssets = subAssets.OrderBy(s => s.address).ToList();

                if (subAssets.Count == 0)
                {
                    return $"public static readonly EntryData {entryClassName} = new EntryData(\"{groupName}\", \"{assetName}\", \"{entry.address}\");";
                }

                // Has sub-objects: promote to static class with Data + SubAssets
                var subObjectFields = subAssets
                    .Select(s => (s, subName: GetSubObjectName(s.address), fieldName: SanitizeIdentifier(GetSubObjectName(s.address))))
                    .ToList();

                // SubAssets class content
                var subAssetsContent = new List<object>();
                foreach (var (s, subName, fieldName) in subObjectFields)
                    subAssetsContent.Add($"public static readonly EntryData {fieldName} = new EntryData(\"{groupName}\", \"{subName}\", \"{s.address}\");");
                subAssetsContent.Add("");
                var allFieldRefs = subObjectFields.Select(t => (object)$"{t.fieldName},").ToList();
                subAssetsContent.Add(new ScriptBuilder.CodeBlock
                {
                    Header = "public static readonly EntryData[] All = new EntryData[]",
                    Content = allFieldRefs,
                    Suffix = ";"
                });

                // Data field referencing SubAssets fields
                var dataLines = new List<string>();
                dataLines.Add($"public static readonly EntryData Data = new EntryData(");
                dataLines.Add($"    \"{groupName}\",");
                dataLines.Add($"    \"{assetName}\",");
                dataLines.Add($"    \"{entry.address}\",");
                dataLines.Add("    new EntryData[]");
                dataLines.Add("    {");
                foreach (var (_, _, fieldName) in subObjectFields)
                    dataLines.Add($"        SubAssets.{fieldName},");
                dataLines.Add("    });");

                var classContent = new List<object>();
                classContent.Add(new ScriptBuilder.RawBlock { Lines = dataLines });
                classContent.Add("");
                classContent.Add(new ScriptBuilder.CodeBlock
                {
                    Header = "public static class SubAssets",
                    Content = subAssetsContent
                });

                return new ScriptBuilder.CodeBlock
                {
                    Header = $"public static class {entryClassName}",
                    Content = classContent
                };
            }

            var entryLines = new List<object>();

            // Collect folder sub-entries
            var subEntries = new List<AddressableAssetEntry>();
            entry.GatherAllAssets(subEntries, false, true, false);
            var orderedSubs = subEntries.OrderBy(s => s.address).ToList();

            // Data field for the folder entry itself, with recursively nested SubEntries
            if (orderedSubs.Count > 0)
            {
                var dataLines = new List<string>();
                dataLines.Add($"public static readonly EntryData Data = new EntryData(");
                dataLines.Add($"    \"{groupName}\",");
                dataLines.Add($"    \"{assetName}\",");
                dataLines.Add($"    \"{entry.address}\",");
                dataLines.Add("    new EntryData[]");
                dataLines.Add("    {");
                foreach (var sub in orderedSubs)
                {
                    var subLines = BuildEntryDataLines(sub, groupName, "        ");
                    dataLines.AddRange(subLines.Take(subLines.Count - 1));
                    dataLines.Add(subLines[subLines.Count - 1] + ",");
                }
                dataLines.Add("    });");
                entryLines.Add(new ScriptBuilder.RawBlock { Lines = dataLines });
            }
            else
            {
                entryLines.Add($"public static readonly EntryData Data = new EntryData(\"{groupName}\", \"{assetName}\", \"{entry.address}\");");
            }

            // Per-folder Labels (only if this folder entry has labels)
            if (entry.labels.Count > 0)
            {
                var sortedLabels = entry.labels.OrderBy(l => l).ToList();
                var labelLines = new List<object>();
                foreach (string label in sortedLabels)
                    labelLines.Add($"public static readonly LabelData {SanitizeIdentifier(label)} = {rootClassName}.Labels.{SanitizeIdentifier(label)};");
                labelLines.Add("");
                var allFolderLabelFields = sortedLabels.Select(l => $"{SanitizeIdentifier(l)},").Cast<object>().ToList();
                labelLines.Add(new ScriptBuilder.CodeBlock
                {
                    Header = "public static readonly LabelData[] All = new LabelData[]",
                    Content = allFolderLabelFields,
                    Suffix = ";"
                });
                entryLines.Add(new ScriptBuilder.CodeBlock
                {
                    Header = "public static class Labels",
                    Content = labelLines
                });
            }

            // Assets inner class for sub-entries
            if (orderedSubs.Count > 0)
            {
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
                foreach (var sub in orderedSubs)
                    assetsLines.Add(BuildEntryElement(sub, groupName, rootClassName, entryClassName));

                entryLines.Add(new ScriptBuilder.CodeBlock
                {
                    Header = "public static class Assets",
                    Content = assetsLines
                });
            }

            return new ScriptBuilder.CodeBlock
            {
                Header = $"public static class {entryClassName}",
                Content = entryLines
            };
        }

        /// <summary>
        /// Recursively generates lines for a "new EntryData(...)" expression.
        /// Sub-objects are included without self-reference (includeSelf = false).
        /// The returned list's last line does NOT include a trailing comma or semicolon.
        /// </summary>
        private static List<string> BuildEntryDataLines(AddressableAssetEntry entry, string groupName, string indent)
        {
            bool isFolder = AssetDatabase.IsValidFolder(entry.AssetPath);
            string name = isFolder
                ? Path.GetFileName(entry.AssetPath)
                : Path.GetFileNameWithoutExtension(entry.AssetPath);

            var subList = new List<AddressableAssetEntry>();
            if (isFolder)
                entry.GatherAllAssets(subList, false, true, false);
            else
                entry.GatherAllAssets(subList, false, false, true);  // no self, just sub-objects
            subList = subList.OrderBy(s => s.address).ToList();

            // No deeper hierarchy: single-line expression
            if (subList.Count == 0)
                return new List<string> { $"{indent}new EntryData(\"{groupName}\", \"{name}\", \"{entry.address}\")" };

            string innerIndent = indent + "    ";
            string itemIndent = indent + "        ";
            var lines = new List<string>();
            lines.Add($"{indent}new EntryData(");
            lines.Add($"{innerIndent}\"{groupName}\",");
            lines.Add($"{innerIndent}\"{name}\",");
            lines.Add($"{innerIndent}\"{entry.address}\",");
            lines.Add($"{innerIndent}new EntryData[]");
            lines.Add($"{innerIndent}{{");
            foreach (var sub in subList)
            {
                var subLines = BuildEntryDataLines(sub, groupName, itemIndent);
                lines.AddRange(subLines.Take(subLines.Count - 1));
                lines.Add(subLines[subLines.Count - 1] + ",");
            }
            lines.Add($"{innerIndent}}}");
            lines.Add($"{indent})");
            return lines;
        }

        /// <summary>
        /// Extracts the sub-object name from an address of the form "Texture[SpriteName]" → "SpriteName".
        /// </summary>
        private static string GetSubObjectName(string address)
        {
            int start = address.LastIndexOf('[');
            int end = address.LastIndexOf(']');
            if (start >= 0 && end > start)
                return address.Substring(start + 1, end - start - 1);
            return Path.GetFileNameWithoutExtension(address);
        }

        private string BuildSplitRootScriptBody(string baseClassName)
        {
            var classLines = new List<object>();
            var allLabels = CollectAllLabels(_groups);
            if (allLabels.Count > 0)
            {
                var labelToSourceClass = new Dictionary<string, string>();
                foreach (AddressableAssetGroup group in _groups)
                {
                    string groupScriptClass = baseClassName + SanitizeIdentifier(group.Name);
                    foreach (string label in CollectGroupLabelsFlat(group))
                        if (!labelToSourceClass.ContainsKey(label))
                            labelToSourceClass[label] = groupScriptClass;
                }

                var labelsContent = new List<object>();
                foreach (string label in allLabels)
                {
                    string sourceClass = labelToSourceClass[label];
                    labelsContent.Add($"public static LabelData {SanitizeIdentifier(label)} => {sourceClass}.Labels.{SanitizeIdentifier(label)};");
                }
                labelsContent.Add("");
                var allLabelFields = allLabels.Select(l => $"{SanitizeIdentifier(l)},").Cast<object>().ToList();
                labelsContent.Add(new ScriptBuilder.CodeBlock { Header = "public static readonly IReadOnlyList<LabelData> All = new LabelData[]", Content = allLabelFields, Suffix = ";" });
                classLines.Add(new ScriptBuilder.CodeBlock { Header = "public static class Labels", Content = labelsContent });
                classLines.Add("");
            }

            var groupsContent = new List<object>();
            foreach (AddressableAssetGroup g in _groups)
            {
                string gName = SanitizeIdentifier(g.Name);
                string gScriptClass = baseClassName + gName;
                groupsContent.Add($"public static GroupData {gName} => {gScriptClass}.Data;");
            }
            groupsContent.Add("");
            var allGroupEntries = _groups.Select(g => { string gName = SanitizeIdentifier(g.Name); return (object)$"{gName},"; }).ToList();
            groupsContent.Add(new ScriptBuilder.CodeBlock { Header = "public static readonly IReadOnlyList<GroupData> All = new GroupData[]", Content = allGroupEntries, Suffix = ";" });
            classLines.Add(new ScriptBuilder.CodeBlock { Header = "public static class Groups", Content = groupsContent });
            classLines.Add("");

            if (_groups.Count == 1)
            {
                string onlyGroup = baseClassName + SanitizeIdentifier(_groups[0].Name);
                classLines.Add($"public static readonly IReadOnlyList<EntryData> AllEntries = {onlyGroup}.Data.Entries.Values.ToList();");
            }
            else if (_groups.Count > 1)
            {
                string first = baseClassName + SanitizeIdentifier(_groups[0].Name);
                var concatLines = new List<string>
                {
                    $"public static readonly IReadOnlyList<EntryData> AllEntries =",
                    $"    {first}.Data.Entries.Values"
                };
                for (int i = 1; i < _groups.Count; i++)
                {
                    string gClass = baseClassName + SanitizeIdentifier(_groups[i].Name);
                    bool isLast = i == _groups.Count - 1;
                    concatLines.Add($"    .Concat({gClass}.Data.Entries.Values){(isLast ? ".ToList();" : "")}");
                }
                classLines.Add(new ScriptBuilder.RawBlock { Lines = concatLines });
            }

            var classBlock = new ScriptBuilder.CodeBlock { Header = $"public static class {baseClassName}", Content = classLines };
            string usingStatement = "using System.Collections.Generic;" + System.Environment.NewLine
                + "using System.Linq;" + System.Environment.NewLine
                + "using Mu3Library.Addressable.Data;" + System.Environment.NewLine + System.Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(_scriptNamespace))
            {
                var namespaceBlock = new ScriptBuilder.CodeBlock { Header = $"namespace {_scriptNamespace.Trim()}", Content = new List<object> { classBlock } };
                return usingStatement + ScriptBuilder.Build(4, namespaceBlock);
            }
            return usingStatement + ScriptBuilder.Build(4, classBlock);
        }

        private string BuildGroupScriptBody(string baseClassName, AddressableAssetGroup group)
        {
            string groupSanitizedName = SanitizeIdentifier(group.Name);
            string className = baseClassName + groupSanitizedName;
            var classLines = new List<object>();
            var groupLines = new List<object>();

            var entries = group.entries?.OrderBy(e => e.address).ToList();
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
            if (entries != null)
                foreach (AddressableAssetEntry e in entries) CollectGroupLabels(e);

            var groupUniqueLabels = groupLabelsSeen.OrderBy(l => l).ToList();

            if (groupUniqueLabels.Count > 0)
            {
                // Outer Labels class (on className)
                var outerLabelsContent = new List<object>();
                foreach (string label in groupUniqueLabels)
                    outerLabelsContent.Add($"public static readonly LabelData {SanitizeIdentifier(label)} = new LabelData(\"{label}\");");
                outerLabelsContent.Add("");
                var allOuterFields = groupUniqueLabels.Select(l => $"{SanitizeIdentifier(l)},").Cast<object>().ToList();
                outerLabelsContent.Add(new ScriptBuilder.CodeBlock { Header = "public static readonly IReadOnlyList<LabelData> All = new LabelData[]", Content = allOuterFields, Suffix = ";" });
                classLines.Add(new ScriptBuilder.CodeBlock { Header = "public static class Labels", Content = outerLabelsContent });
                classLines.Add("");

                // Inner Labels class (on GroupData, references outer)
                var innerLabelsContent = new List<object>();
                foreach (string label in groupUniqueLabels)
                    innerLabelsContent.Add($"public static readonly LabelData {SanitizeIdentifier(label)} = {className}.Labels.{SanitizeIdentifier(label)};");
                innerLabelsContent.Add("");
                var allInnerFields = groupUniqueLabels.Select(l => $"{SanitizeIdentifier(l)},").Cast<object>().ToList();
                innerLabelsContent.Add(new ScriptBuilder.CodeBlock { Header = "public static readonly IReadOnlyList<LabelData> All = new LabelData[]", Content = allInnerFields, Suffix = ";" });
                groupLines.Add(new ScriptBuilder.CodeBlock { Header = "public new static class Labels", Content = innerLabelsContent });
                groupLines.Add("");
            }

            if (entries != null && entries.Count > 0)
            {
                foreach (AddressableAssetEntry entry in entries)
                    groupLines.AddRange(BuildTopLevelEntryLines(entry, group.Name, className));
                groupLines.Add("");

                var allNames = entries.Select(e => AssetDatabase.IsValidFolder(e.AssetPath)
                    ? Path.GetFileName(e.AssetPath)
                    : Path.GetFileNameWithoutExtension(e.AssetPath)).ToList();
                var allAddresses = entries.Select(e => e.address).ToList();
                groupLines.Add(new ScriptBuilder.ArrayBlock { FieldName = "AllNames", Values = allNames });
                groupLines.Add("");
                groupLines.Add(new ScriptBuilder.ArrayBlock { FieldName = "AllAddresses", Values = allAddresses });
                groupLines.Add("");
            }

            groupLines.Add($"internal {groupSanitizedName}Data() : base(");
            groupLines.Add($"    \"{group.Name}\",");
            groupLines.Add("    new Dictionary<string, EntryData>");
            groupLines.Add("    {");
            if (entries != null)
                foreach (AddressableAssetEntry entry in entries)
                {
                    bool isFolder = AssetDatabase.IsValidFolder(entry.AssetPath);
                    string entryClassName = SanitizeIdentifier(entry.address);
                    if (isFolder)
                    {
                        string shortName = SanitizeIdentifier(Path.GetFileName(entry.AssetPath));
                        groupLines.Add($"        {{ _{shortName}.Name, _{shortName} }},");
                    }
                    else
                    {
                        groupLines.Add($"        {{ _{entryClassName}.Name, _{entryClassName} }},");
                    }
                }
            groupLines.Add("    },");
            groupLines.Add("    new Dictionary<string, LabelData>");
            groupLines.Add("    {");
            foreach (string label in groupUniqueLabels)
                groupLines.Add($"        {{ Labels.{SanitizeIdentifier(label)}.Value, Labels.{SanitizeIdentifier(label)} }},");
            groupLines.Add("    })");
            groupLines.Add("{ }");

            classLines.Add(new ScriptBuilder.CodeBlock { Header = $"public sealed class {groupSanitizedName}Data : GroupData", Content = groupLines });
            classLines.Add("");
            classLines.Add($"public static readonly {groupSanitizedName}Data Data = new {groupSanitizedName}Data();");

            var classBlock = new ScriptBuilder.CodeBlock { Header = $"public static class {className}", Content = classLines };
            string usingStatement = "using System.Collections.Generic;" + System.Environment.NewLine
                + "using Mu3Library.Addressable.Data;" + System.Environment.NewLine + System.Environment.NewLine;
            if (!string.IsNullOrWhiteSpace(_scriptNamespace))
            {
                var namespaceBlock = new ScriptBuilder.CodeBlock { Header = $"namespace {_scriptNamespace.Trim()}", Content = new List<object> { classBlock } };
                return usingStatement + ScriptBuilder.Build(4, namespaceBlock);
            }
            return usingStatement + ScriptBuilder.Build(4, classBlock);
        }

        private static List<string> CollectGroupLabelsFlat(AddressableAssetGroup group)
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
                    foreach (var sub in subs.OrderBy(s => s.address)) Collect(sub);
                }
            }
            foreach (AddressableAssetEntry entry in group.entries?.OrderBy(e => e.address) ?? Enumerable.Empty<AddressableAssetEntry>())
                Collect(entry);
            return labels;
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
