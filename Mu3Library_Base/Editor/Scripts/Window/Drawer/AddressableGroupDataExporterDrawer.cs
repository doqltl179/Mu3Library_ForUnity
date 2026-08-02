#if MU3LIBRARY_ADDRESSABLES_SUPPORT
using System;
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
        private const string DefaultClassName = "AddressableGroups";

        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while"
        };

        [SerializeField, HideInInspector] private DefaultAsset _scriptSaveFolder;
        [SerializeField, HideInInspector] private string _scriptNamespace = "";
        [SerializeField, HideInInspector] private string _scriptClassName = "";
        [SerializeField, HideInInspector] private bool _foldoutGroupPreview = false;

        private List<AddressableAssetGroup> _groups = new List<AddressableAssetGroup>();
        private bool _isDataLoaded;

        private SerializedObject _serializedObjectValue;
        private SerializedObject _serializedObject
        {
            get
            {
                if (_serializedObjectValue == null)
                    _serializedObjectValue = new SerializedObject(this);
                return _serializedObjectValue;
            }
        }

        private SerializedProperty _serializedPropScriptSaveFolderValue;
        private SerializedProperty _serializedPropScriptSaveFolder
        {
            get
            {
                if (_serializedPropScriptSaveFolderValue == null)
                    _serializedPropScriptSaveFolderValue = _serializedObject.FindProperty(nameof(_scriptSaveFolder));
                return _serializedPropScriptSaveFolderValue;
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
            if (!_foldout)
                return;

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
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                _groups = settings.groups
                    .Where(group => group != null)
                    .OrderBy(group => group.Name)
                    .ToList();
            }

            _isDataLoaded = true;
        }

        private void DrawRefreshButton()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(80), GUILayout.Height(24)))
                RefreshData();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawScriptSaveFolderField()
        {
            _serializedObject.Update();
            EditorGUILayout.PropertyField(_serializedPropScriptSaveFolder, new GUIContent("Script Save Folder"));
            if (_serializedObject.ApplyModifiedProperties() && _scriptSaveFolder != null && !IsAssetsFolder(_scriptSaveFolder))
            {
                Debug.LogWarning("Selected folder is not inside the Assets folder.");
                _scriptSaveFolder = null;
                _serializedObject.ApplyModifiedProperties();
            }
        }

        private void DrawNamespaceField()
        {
            DrawWithUndo(
                () => EditorGUILayout.TextField("Namespace (optional)", _scriptNamespace),
                value => _scriptNamespace = value,
                "Addressable Exporter: Namespace");
        }

        private void DrawClassNameField()
        {
            DrawWithUndo(
                () => EditorGUILayout.TextField("Class Name (optional)", _scriptClassName),
                value => _scriptClassName = value,
                "Addressable Exporter: Class Name");

            if (string.IsNullOrWhiteSpace(_scriptClassName))
                EditorGUILayout.HelpBox($"Default class name: {DefaultClassName}", MessageType.None);
        }

        private void DrawGroupPreview()
        {
            if (!_isDataLoaded || _groups.Count == 0)
            {
                EditorGUILayout.HelpBox("No Addressable Groups found. Click Refresh.", MessageType.Info);
                return;
            }

            DrawFoldoutHeader2($"Addressable Groups Preview  ({_groups.Count} group(s))", ref _foldoutGroupPreview);
            if (!_foldoutGroupPreview)
                return;

            DrawStruct(() =>
            {
                foreach (AddressableAssetGroup group in _groups)
                {
                    List<AddressableAssetEntry> entries = GetGroupEntries(group);
                    EditorGUILayout.LabelField($"[Group]  {group.Name}  ({entries.Count} asset(s))", EditorStyles.boldLabel);
                    if (entries.Count == 0)
                    {
                        GUILayout.Space(4);
                        continue;
                    }

                    DrawStruct(() =>
                    {
                        foreach (AddressableAssetEntry entry in entries)
                            DrawEntryPreview(entry, 0);
                    }, 16);
                    GUILayout.Space(4);
                }
            }, 8);
        }

        private void DrawEntryPreview(AddressableAssetEntry entry, int depth)
        {
            bool isFolder = AssetDatabase.IsValidFolder(entry.AssetPath);
            string labelText = entry.labels != null && entry.labels.Count > 0
                ? string.Join(", ", entry.labels.OrderBy(label => label))
                : "(no labels)";
            string prefix = isFolder ? "• [Folder]" : "•";
            EditorGUILayout.LabelField(
                $"{new string(' ', depth * 2)}{prefix} {GetEntryName(entry)}  |  Address: {entry.address}  |  Labels: {labelText}",
                EditorStyles.miniLabel);

            foreach (AddressableAssetEntry child in GetEntryChildren(entry))
                DrawEntryPreview(child, depth + 1);
        }

        private void DrawValidationAndButton()
        {
            string warning = GetFirstWarning();
            if (warning != null)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
                return;
            }

            if (GUILayout.Button("Generate C# Scripts", GUILayout.Height(30)))
                GenerateScripts();
        }

        private string GetFirstWarning()
        {
            if (!_isDataLoaded || _groups.Count == 0)
                return "No Addressable Groups found. Click Refresh.";
            if (_scriptSaveFolder == null)
                return "Script Save Folder is not set. Drag & drop a project folder.";
            return null;
        }

        private void GenerateScripts()
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

            string className = GetClassName();
            List<GeneratedGroup> generatedGroups = BuildGeneratedGroups();
            Dictionary<string, string> labelIdentifiers = BuildIdentifierMap(
                generatedGroups.SelectMany(group => group.Labels));
            string labelsClassName = GetPascalCaseIdentifier(className + "Labels");

            WriteGeneratedScript(
                systemPath,
                labelsClassName,
                BuildLabelsScriptBody(labelsClassName, labelIdentifiers));

            foreach (GeneratedGroup group in generatedGroups)
            {
                string groupClassName = GetPascalCaseIdentifier(className + group.Identifier);
                WriteGeneratedScript(
                    systemPath,
                    groupClassName,
                    BuildGroupScriptBody(groupClassName, group, labelsClassName, labelIdentifiers));
            }

            WriteGeneratedScript(systemPath, className, BuildRootScriptBody(className, generatedGroups));
            AssetDatabase.Refresh();
            Debug.Log($"Addressable scripts generated. folder: {systemPath}");
        }

        private void WriteGeneratedScript(string systemPath, string fileName, string body)
        {
            string filePath = Path.Combine(systemPath, $"{fileName}.cs");
            File.WriteAllText(filePath, body, new UTF8Encoding(true));
            Debug.Log($"Addressable script generated. path: {filePath}");
        }

        private List<GeneratedGroup> BuildGeneratedGroups()
        {
            var generatedGroups = new List<GeneratedGroup>();
            var usedIdentifiers = new HashSet<string>();
            foreach (AddressableAssetGroup group in _groups)
            {
                string identifier = MakeUniqueIdentifier(group.Name, usedIdentifiers);
                var generatedEntries = new List<GeneratedEntry>();
                var ancestorIdentifiers = new HashSet<string> { identifier };
                var siblingIdentifiers = new HashSet<string>(ancestorIdentifiers);
                var ancestorTypeIdentifiers = new HashSet<string> { identifier };
                var siblingTypeIdentifiers = new HashSet<string>(ancestorTypeIdentifiers);
                foreach (AddressableAssetEntry entry in GetGroupEntries(group))
                    generatedEntries.Add(BuildGeneratedEntry(
                        entry,
                        ancestorIdentifiers,
                        siblingIdentifiers,
                        ancestorTypeIdentifiers,
                        siblingTypeIdentifiers));

                var labels = generatedEntries
                    .SelectMany(GetAllEntryLabels)
                    .Distinct()
                    .OrderBy(label => label)
                    .ToList();
                generatedGroups.Add(new GeneratedGroup(group.Name, identifier, generatedEntries, labels));
            }

            var usedRootIdentifiers = new HashSet<string> { "All" };
            foreach (GeneratedGroup group in generatedGroups)
                group.RootIdentifier = MakeUniqueIdentifier(group.Name, usedRootIdentifiers);
            return generatedGroups;
        }

        private GeneratedEntry BuildGeneratedEntry(
            AddressableAssetEntry entry,
            ISet<string> ancestorIdentifiers,
            ISet<string> siblingIdentifiers,
            ISet<string> ancestorTypeIdentifiers,
            ISet<string> siblingTypeIdentifiers)
        {
            string name = GetEntryName(entry);
            string assetTypeName = GetEntryAssetTypeName(entry);
            string memberIdentifier = MakeUniqueIdentifier(
                name + assetTypeName,
                siblingIdentifiers);
            string typeIdentifier = MakeUniqueIdentifier(
                name + assetTypeName,
                siblingTypeIdentifiers);
            var childAncestors = new HashSet<string>(ancestorIdentifiers) { memberIdentifier };
            var childSiblingIdentifiers = new HashSet<string>(childAncestors);
            var childTypeAncestors = new HashSet<string>(ancestorTypeIdentifiers) { typeIdentifier };
            var childTypeSiblingIdentifiers = new HashSet<string>(childTypeAncestors);
            var children = new List<GeneratedEntry>();
            foreach (AddressableAssetEntry child in GetEntryChildren(entry))
                children.Add(BuildGeneratedEntry(
                    child,
                    childAncestors,
                    childSiblingIdentifiers,
                    childTypeAncestors,
                    childTypeSiblingIdentifiers));

            var labels = entry.labels == null
                ? new List<string>()
                : entry.labels.Where(label => !string.IsNullOrEmpty(label)).Distinct().OrderBy(label => label).ToList();
            return new GeneratedEntry(
                name,
                entry.address ?? string.Empty,
                memberIdentifier,
                typeIdentifier,
                labels,
                children);
        }

        private static IEnumerable<string> GetAllEntryLabels(GeneratedEntry entry)
        {
            foreach (string label in entry.Labels)
                yield return label;
            foreach (GeneratedEntry child in entry.Children)
                foreach (string label in GetAllEntryLabels(child))
                    yield return label;
        }

        private static List<AddressableAssetEntry> GetGroupEntries(AddressableAssetGroup group)
        {
            return group.entries == null
                ? new List<AddressableAssetEntry>()
                : group.entries.Where(entry => entry != null).OrderBy(entry => entry.address).ToList();
        }

        private static List<AddressableAssetEntry> GetEntryChildren(AddressableAssetEntry entry)
        {
            var children = new List<AddressableAssetEntry>();
            if (AssetDatabase.IsValidFolder(entry.AssetPath))
                entry.GatherAllAssets(children, false, true, false);
            else
                entry.GatherAllAssets(children, false, false, true);
            return children.Where(child => child != null).OrderBy(child => child.address).ToList();
        }

        private static string GetEntryName(AddressableAssetEntry entry)
        {
            string subObjectName = GetSubObjectName(entry.address);
            if (subObjectName != null)
                return subObjectName;
            if (AssetDatabase.IsValidFolder(entry.AssetPath))
                return Path.GetFileName(entry.AssetPath);
            return Path.GetFileNameWithoutExtension(entry.AssetPath);
        }

        private static string GetSubObjectName(string address)
        {
            if (string.IsNullOrEmpty(address))
                return null;
            int start = address.LastIndexOf('[');
            int end = address.LastIndexOf(']');
            return start >= 0 && end > start
                ? address.Substring(start + 1, end - start - 1)
                : null;
        }

        private static string GetEntryAssetTypeName(AddressableAssetEntry entry)
        {
            return entry.MainAssetType == null
                ? "Object"
                : entry.MainAssetType.Name;
        }

        private string BuildLabelsScriptBody(string labelsClassName, IReadOnlyDictionary<string, string> labelIdentifiers)
        {
            var body = new StringBuilder();
            AppendGeneratedHeader(body);
            AppendOuterLine(body, 0, "using System.Collections.Generic;");
            AppendOuterLine(body, 0, "");
            AppendNamespaceStart(body);
            AppendLine(body, 0, $"public static class {labelsClassName}");
            AppendLine(body, 0, "{");

            foreach (KeyValuePair<string, string> label in labelIdentifiers)
                AppendLine(body, 1, $"public static readonly string {label.Value} = {Quote(label.Key)};");

            AppendLine(body, 1, "");
            AppendLine(body, 1, "public static readonly IReadOnlyList<string> All = new string[]");
            AppendLine(body, 1, "{");
            foreach (string identifier in labelIdentifiers.Values)
                AppendLine(body, 2, $"{identifier},");
            AppendLine(body, 1, "};");
            AppendLine(body, 0, "}");
            AppendNamespaceEnd(body);
            return body.ToString();
        }

        private string BuildGroupScriptBody(
            string groupClassName,
            GeneratedGroup group,
            string labelsClassName,
            IReadOnlyDictionary<string, string> labelIdentifiers)
        {
            var body = new StringBuilder();
            AppendGeneratedHeader(body);
            AppendOuterLine(body, 0, "using System.Collections.Generic;");
            AppendOuterLine(body, 0, "using Mu3Library.Addressable.Data;");
            AppendOuterLine(body, 0, "");
            AppendNamespaceStart(body);
            AppendLine(body, 0, $"public sealed class {groupClassName} : GroupData");
            AppendLine(body, 0, "{");

            foreach (GeneratedEntry entry in group.Entries)
                AppendEntryType(body, entry, 1, labelsClassName, labelIdentifiers);

            if (group.Entries.Count > 0)
                AppendLine(body, 1, "");
            foreach (GeneratedEntry entry in group.Entries)
            {
                string typeName = GetEntryTypeName(entry);
                string privateIdentifier = GetPrivateFieldIdentifier(entry.MemberIdentifier);
                AppendLine(body, 1, $"private static readonly {typeName} {privateIdentifier} = new {typeName}();");
                AppendLine(body, 1, $"public readonly {typeName} {entry.MemberIdentifier} = {privateIdentifier};");
            }

            AppendLine(body, 1, "");
            AppendLine(body, 1, "private static readonly IReadOnlyList<EntryData> _groupEntries = new EntryData[]");
            AppendLine(body, 1, "{");
            foreach (GeneratedEntry entry in group.Entries)
                AppendLine(body, 2, $"{GetPrivateFieldIdentifier(entry.MemberIdentifier)},");
            AppendLine(body, 1, "};");
            AppendLine(body, 1, "");
            AppendLine(body, 1, "private static readonly IReadOnlyList<string> _groupLabels = new string[]");
            AppendLine(body, 1, "{");
            foreach (string label in group.Labels)
                AppendLine(body, 2, $"{labelsClassName}.{labelIdentifiers[label]},");
            AppendLine(body, 1, "};");
            AppendLine(body, 1, "");
            AppendLine(body, 1, $"public static readonly {groupClassName} Instance = new {groupClassName}();");
            AppendLine(body, 1, "");
            AppendLine(body, 1, $"internal {groupClassName}() : base(");
            AppendLine(body, 2, Quote(group.Name) + ",");
            AppendLine(body, 2, "_groupEntries,");
            AppendLine(body, 2, "_groupLabels)");
            AppendLine(body, 1, "{");
            AppendLine(body, 1, "}");
            AppendLine(body, 0, "}");
            AppendNamespaceEnd(body);
            return body.ToString();
        }

        private void AppendEntryType(
            StringBuilder body,
            GeneratedEntry entry,
            int indent,
            string labelsClassName,
            IReadOnlyDictionary<string, string> labelIdentifiers)
        {
            string typeName = GetEntryTypeName(entry);
            AppendLine(body, indent, $"public sealed class {typeName} : EntryData");
            AppendLine(body, indent, "{");

            foreach (GeneratedEntry child in entry.Children)
                AppendEntryType(body, child, indent + 1, labelsClassName, labelIdentifiers);

            if (entry.Children.Count > 0)
                AppendLine(body, indent + 1, "");
            foreach (GeneratedEntry child in entry.Children)
            {
                string childTypeName = GetEntryTypeName(child);
                string privateIdentifier = GetPrivateFieldIdentifier(child.MemberIdentifier);
                AppendLine(body, indent + 1, $"private static readonly {childTypeName} {privateIdentifier} = new {childTypeName}();");
                AppendLine(body, indent + 1, $"public readonly {childTypeName} {child.MemberIdentifier} = {privateIdentifier};");
            }

            if (entry.Children.Count > 0)
            {
                AppendLine(body, indent + 1, "");
                AppendLine(body, indent + 1, "private static readonly IReadOnlyList<EntryData> _entryChildren = new EntryData[]");
                AppendLine(body, indent + 1, "{");
                foreach (GeneratedEntry child in entry.Children)
                    AppendLine(body, indent + 2, $"{GetPrivateFieldIdentifier(child.MemberIdentifier)},");
                AppendLine(body, indent + 1, "};");
            }

            AppendLine(body, indent + 1, "");
            AppendLine(body, indent + 1, $"public {typeName}() : base(");
            AppendLine(body, indent + 2, Quote(entry.Name) + ",");
            AppendLine(body, indent + 2, Quote(entry.Address) + ",");
            AppendLine(body, indent + 2, BuildLabelsExpression(entry.Labels, labelsClassName, labelIdentifiers)
                + (entry.Children.Count > 0 ? "," : ""));
            if (entry.Children.Count > 0)
                AppendLine(body, indent + 2, "_entryChildren");
            AppendLine(body, indent + 1, ")");
            AppendLine(body, indent + 1, "{");
            AppendLine(body, indent + 1, "}");
            AppendLine(body, indent, "}");
            AppendLine(body, indent, "");
        }

        private static string BuildLabelsExpression(
            IReadOnlyList<string> labels,
            string labelsClassName,
            IReadOnlyDictionary<string, string> labelIdentifiers)
        {
            if (labels.Count == 0)
                return "new string[] { }";
            return "new string[] { " + string.Join(", ", labels.Select(label => $"{labelsClassName}.{labelIdentifiers[label]}")) + " }";
        }

        private string BuildRootScriptBody(string className, IReadOnlyList<GeneratedGroup> groups)
        {
            var body = new StringBuilder();
            AppendGeneratedHeader(body);
            AppendOuterLine(body, 0, "using System.Collections.Generic;");
            AppendOuterLine(body, 0, "using Mu3Library.Addressable.Data;");
            AppendOuterLine(body, 0, "");
            AppendNamespaceStart(body);
            AppendLine(body, 0, $"public static class {className}");
            AppendLine(body, 0, "{");

            foreach (GeneratedGroup group in groups)
            {
                string groupClassName = GetPascalCaseIdentifier(className + group.Identifier);
                AppendLine(body, 1, $"public static readonly {groupClassName} {group.RootIdentifier} = {groupClassName}.Instance;");
            }

            if (groups.Count > 0)
                AppendLine(body, 1, "");
            AppendLine(body, 1, "public static readonly IReadOnlyList<GroupData> All = new GroupData[]");
            AppendLine(body, 1, "{");
            foreach (GeneratedGroup group in groups)
                AppendLine(body, 2, $"{group.RootIdentifier},");
            AppendLine(body, 1, "};");
            AppendLine(body, 0, "}");
            AppendNamespaceEnd(body);
            return body.ToString();
        }

        private string GetClassName()
        {
            return string.IsNullOrWhiteSpace(_scriptClassName)
                ? DefaultClassName
                : GetPublicMemberIdentifier(SanitizeIdentifier(_scriptClassName.Trim()));
        }

        private Dictionary<string, string> BuildIdentifierMap(IEnumerable<string> values)
        {
            var map = new Dictionary<string, string>();
            var usedIdentifiers = new HashSet<string>();
            foreach (string value in values.Where(value => !string.IsNullOrEmpty(value)).Distinct().OrderBy(value => value))
                map[value] = MakeUniqueIdentifier(value, usedIdentifiers);
            return map;
        }

        private static string MakeUniqueIdentifier(string value, ISet<string> usedIdentifiers)
        {
            string baseIdentifier = GetPublicMemberIdentifier(SanitizeIdentifier(value));
            string identifier = baseIdentifier;
            int suffix = 2;
            while (!usedIdentifiers.Add(identifier))
                identifier = baseIdentifier + suffix++;
            return identifier;
        }

        private static string GetEntryTypeName(GeneratedEntry entry)
        {
            return GetPascalCaseIdentifier(entry.TypeIdentifier + "Entry");
        }

        private static string GetPascalCaseIdentifier(string identifier)
        {
            return GetPublicMemberIdentifier(identifier);
        }

        private static string GetPublicMemberIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return "Item";

            var builder = new StringBuilder();
            bool capitalizeNext = true;
            foreach (char character in identifier)
            {
                if (!char.IsLetterOrDigit(character))
                {
                    capitalizeNext = true;
                    continue;
                }

                if (builder.Length == 0 && char.IsDigit(character))
                    builder.Append("Item");

                if (capitalizeNext && char.IsLetter(character))
                    builder.Append(char.ToUpperInvariant(character));
                else
                    builder.Append(character);
                capitalizeNext = false;
            }

            return builder.Length == 0 ? "Item" : builder.ToString();
        }

        private static string GetPrivateFieldIdentifier(string identifier)
        {
            string publicIdentifier = GetPublicMemberIdentifier(identifier);
            return "_" + char.ToLowerInvariant(publicIdentifier[0]) + publicIdentifier.Substring(1);
        }

        private static void AppendGeneratedHeader(StringBuilder body)
        {
            body.AppendLine("// <auto-generated />");
            body.AppendLine("// Generated by AddressableGroupDataExporterDrawer. Do not edit manually.");
        }

        private void AppendNamespaceStart(StringBuilder body)
        {
            if (string.IsNullOrWhiteSpace(_scriptNamespace))
                return;
            AppendOuterLine(body, 0, $"namespace {_scriptNamespace.Trim()}");
            AppendOuterLine(body, 0, "{");
        }

        private void AppendNamespaceEnd(StringBuilder body)
        {
            if (!string.IsNullOrWhiteSpace(_scriptNamespace))
                AppendOuterLine(body, 0, "}");
        }

        private void AppendLine(StringBuilder body, int indent, string line)
        {
            int namespaceIndent = string.IsNullOrWhiteSpace(_scriptNamespace) ? 0 : 1;
            AppendOuterLine(body, indent + namespaceIndent, line);
        }

        private static void AppendOuterLine(StringBuilder body, int indent, string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                body.AppendLine();
                return;
            }

            body.Append(' ', indent * 4).AppendLine(line);
        }

        private static string Quote(string value)
        {
            if (value == null)
                value = string.Empty;
            return "\"" + value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t")
                .Replace("\0", "\\0") + "\"";
        }

        private static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "_";

            var builder = new StringBuilder();
            bool capitalizeNext = false;
            foreach (char character in name)
            {
                if (char.IsLetterOrDigit(character) || character == '_')
                {
                    if (builder.Length == 0 && char.IsDigit(character))
                        builder.Append('_');
                    if (capitalizeNext && char.IsLetter(character))
                        builder.Append(char.ToUpperInvariant(character));
                    else
                        builder.Append(character);
                    capitalizeNext = false;
                }
                else
                {
                    capitalizeNext = true;
                }
            }

            if (builder.Length == 0)
                builder.Append('_');
            if (CSharpKeywords.Contains(builder.ToString()))
                builder.Insert(0, '_');
            return builder.ToString();
        }

        private static bool IsAssetsFolder(DefaultAsset folder)
        {
            string path = FileFinder.GetAssetPath(folder);
            return !string.IsNullOrEmpty(path) && (path == "Assets" || path.StartsWith("Assets/"));
        }

        private sealed class GeneratedGroup
        {
            public readonly string Name;
            public readonly string Identifier;
            public readonly List<GeneratedEntry> Entries;
            public readonly List<string> Labels;
            public string RootIdentifier;

            public GeneratedGroup(string name, string identifier, List<GeneratedEntry> entries, List<string> labels)
            {
                Name = name;
                Identifier = identifier;
                Entries = entries;
                Labels = labels;
            }
        }

        private sealed class GeneratedEntry
        {
            public readonly string Name;
            public readonly string Address;
            public readonly string MemberIdentifier;
            public readonly string TypeIdentifier;
            public readonly List<string> Labels;
            public readonly List<GeneratedEntry> Children;

            public GeneratedEntry(
                string name,
                string address,
                string memberIdentifier,
                string typeIdentifier,
                List<string> labels,
                List<GeneratedEntry> children)
            {
                Name = name;
                Address = address;
                MemberIdentifier = memberIdentifier;
                TypeIdentifier = typeIdentifier;
                Labels = labels;
                Children = children;
            }
        }
    }
}
#endif
