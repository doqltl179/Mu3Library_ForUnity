#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mu3Library.Editor.FileUtil;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class LocalizationDataExporterDrawer : Mu3WindowDrawer
    {
        public const string FileName = "LocalizationDataExporter";
        private const string ItemName = "Localization Data Exporter";
        private const string MenuName = MenuRoot + "/" + ItemName;
        private const string DefaultClassName = "LocalizationKeys";

        [SerializeField, HideInInspector] private DefaultAsset _scriptSaveFolder;
        [SerializeField, HideInInspector] private string _scriptNamespace = "";
        [SerializeField, HideInInspector] private string _scriptClassName = "";
        [SerializeField, HideInInspector] private bool _foldoutTablePreview;

        private List<StringTableCollection> _tableCollections = new List<StringTableCollection>();
        private List<Locale> _locales = new List<Locale>();
        private bool _isDataLoaded;

        private const int GridColumns = 4;

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

                DrawRefreshButton(RefreshData);
                GUILayout.Space(4);
                DrawAssetsFolderField(_serializedObject, _serializedPropScriptSaveFolder);
                GUILayout.Space(4);
                DrawNamespaceField(_scriptNamespace, value => _scriptNamespace = value, "Localization Exporter: Namespace");
                GUILayout.Space(4);
                DrawClassNameField(_scriptClassName, value => _scriptClassName = value, "Localization Exporter: Class Name", DefaultClassName);
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
            _locales = LocalizationEditorSettings.GetLocales()?.ToList() ?? new List<Locale>();
            _isDataLoaded = true;
        }

        private void DrawTablePreview()
        {
            if (_tableCollections.Count == 0)
            {
                EditorGUILayout.HelpBox("No String Table Collections found. Click Refresh.", MessageType.Info);
                return;
            }

            DrawFoldoutHeader2($"String Tables Preview  ({_tableCollections.Count} table(s))", ref _foldoutTablePreview);
            if (!_foldoutTablePreview)
                return;

            DrawStruct(() =>
            {
                foreach (StringTableCollection collection in _tableCollections)
                {
                    EditorGUILayout.LabelField($"[Table]  {collection.TableCollectionName}", EditorStyles.boldLabel);

                    DrawStruct(() =>
                    {
                        IList<SharedTableData.SharedTableEntry> entries = collection.SharedData?.Entries;
                        if (entries == null || entries.Count == 0)
                        {
                            EditorGUILayout.LabelField("(No keys)", EditorStyles.miniLabel);
                            return;
                        }

                        float availableWidth = EditorGUILayout.GetControlRect(false, 0).width;
                        float columnWidth = Mathf.Floor(availableWidth / GridColumns);
                        float lineHeight = EditorGUIUtility.singleLineHeight;
                        for (int index = 0; index < entries.Count; index += GridColumns)
                        {
                            Rect rowRect = EditorGUILayout.GetControlRect(false, lineHeight);
                            for (int column = 0; column < GridColumns; column++)
                            {
                                int entryIndex = index + column;
                                if (entryIndex >= entries.Count)
                                    break;

                                Rect cellRect = new Rect(
                                    rowRect.x + column * columnWidth,
                                    rowRect.y,
                                    columnWidth,
                                    rowRect.height);
                                EditorGUI.LabelField(cellRect, $"• {entries[entryIndex].Key}");
                            }
                        }
                    }, 16);

                    GUILayout.Space(4);
                }
            }, 8);
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
            if (_tableCollections.Count == 0)
                return "No String Table Collections found. Click Refresh.";
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
            List<GeneratedLocale> generatedLocales = BuildGeneratedLocales();
            var localesByCode = new Dictionary<string, GeneratedLocale>();
            foreach (GeneratedLocale locale in generatedLocales)
                localesByCode.Add(locale.Code, locale);

            List<GeneratedTable> generatedTables = BuildGeneratedTables(localesByCode);
            string localesClassName = GetPascalCaseIdentifier(className + "Locales");

            WriteGeneratedScript(
                systemPath,
                localesClassName,
                BuildLocalesScriptBody(localesClassName, generatedLocales));

            foreach (GeneratedTable table in generatedTables)
            {
                string tableClassName = GetPascalCaseIdentifier(className + table.Identifier);
                WriteGeneratedScript(
                    systemPath,
                    tableClassName,
                    BuildTableScriptBody(tableClassName, table, localesClassName));
            }

            WriteGeneratedScript(systemPath, className, BuildRootScriptBody(className, generatedTables));
            AssetDatabase.Refresh();
            Debug.Log($"Localization scripts generated. folder: {systemPath}");
        }

        private void WriteGeneratedScript(string systemPath, string fileName, string body)
        {
            string filePath = FileCreator.WriteScript(systemPath, fileName, body);
            Debug.Log($"Localization script generated. path: {filePath}");
        }

        private List<GeneratedLocale> BuildGeneratedLocales()
        {
            var generatedLocales = new List<GeneratedLocale>();
            var usedCodes = new HashSet<string>();
            var usedIdentifiers = new HashSet<string> { "All" };

            foreach (StringTableCollection collection in _tableCollections)
            {
                if (collection == null)
                    continue;

                foreach (Locale locale in _locales)
                {
                    if (locale == null || collection.GetTable(locale.Identifier) as StringTable == null)
                        continue;

                    string code = locale.Identifier.Code ?? string.Empty;
                    if (!usedCodes.Add(code))
                        continue;

                    var cultureInfo = locale.Identifier.CultureInfo;
                    string englishName = cultureInfo?.EnglishName ?? code;
                    string nativeName = cultureInfo?.NativeName ?? code;
                    generatedLocales.Add(new GeneratedLocale(
                        code,
                        englishName,
                        nativeName,
                        MakeUniqueIdentifier(code, usedIdentifiers)));
                }
            }

            return generatedLocales;
        }

        private List<GeneratedTable> BuildGeneratedTables(
            IReadOnlyDictionary<string, GeneratedLocale> localesByCode)
        {
            var generatedTables = new List<GeneratedTable>();
            var usedTableIdentifiers = new HashSet<string> { "Locales" };
            var usedRootIdentifiers = new HashSet<string> { "All" };

            foreach (StringTableCollection collection in _tableCollections)
            {
                if (collection == null)
                    continue;

                string tableName = collection.TableCollectionName ?? string.Empty;
                var tableLocales = new List<GeneratedLocale>();
                var usedLocaleCodes = new HashSet<string>();
                foreach (Locale locale in _locales)
                {
                    if (locale == null || collection.GetTable(locale.Identifier) as StringTable == null)
                        continue;

                    string code = locale.Identifier.Code ?? string.Empty;
                    GeneratedLocale generatedLocale;
                    if (usedLocaleCodes.Add(code) && localesByCode.TryGetValue(code, out generatedLocale))
                        tableLocales.Add(generatedLocale);
                }

                var generatedEntries = new List<GeneratedEntry>();
                var usedEntryIdentifiers = new HashSet<string>
                {
                    "All", "Entries", "Instance", "Locales", "Name", "TableName"
                };
                IList<SharedTableData.SharedTableEntry> entries = collection.SharedData?.Entries;
                if (entries != null)
                {
                    foreach (SharedTableData.SharedTableEntry entry in entries)
                    {
                        string key = entry.Key ?? string.Empty;
                        generatedEntries.Add(new GeneratedEntry(
                            key,
                            entry.Id.ToString(),
                            MakeUniqueIdentifier(key, usedEntryIdentifiers)));
                    }
                }

                generatedTables.Add(new GeneratedTable(
                    tableName,
                    MakeUniqueIdentifier(tableName, usedTableIdentifiers),
                    MakeUniqueIdentifier(tableName, usedRootIdentifiers),
                    tableLocales,
                    generatedEntries));
            }

            return generatedTables;
        }

        private string BuildLocalesScriptBody(string localesClassName, IReadOnlyList<GeneratedLocale> locales)
        {
            var body = new StringBuilder();
            AppendGeneratedHeader(body);
            AppendOuterLine(body, 0, "using System.Collections.Generic;");
            AppendOuterLine(body, 0, "using Mu3Library.Localization.Data;");
            AppendOuterLine(body, 0, "");
            AppendNamespaceStart(body);
            AppendLine(body, 0, $"public static class {localesClassName}");
            AppendLine(body, 0, "{");

            foreach (GeneratedLocale locale in locales)
            {
                AppendLine(
                    body,
                    1,
                    $"public static readonly LocaleData {locale.Identifier} = new LocaleData({Quote(locale.Code)}, {Quote(locale.EnglishName)}, {Quote(locale.NativeName)});");
            }

            AppendLine(body, 1, "");
            AppendLine(body, 1, "public static readonly IReadOnlyDictionary<string, LocaleData> All = new Dictionary<string, LocaleData>");
            AppendLine(body, 1, "{");
            foreach (GeneratedLocale locale in locales)
                AppendLine(body, 2, $"{{ {locale.Identifier}.Code, {locale.Identifier} }},");
            AppendLine(body, 1, "};");
            AppendLine(body, 0, "}");
            AppendNamespaceEnd(body);
            return body.ToString();
        }

        private string BuildTableScriptBody(
            string tableClassName,
            GeneratedTable table,
            string localesClassName)
        {
            var body = new StringBuilder();
            AppendGeneratedHeader(body);
            AppendOuterLine(body, 0, "using System.Collections.Generic;");
            AppendOuterLine(body, 0, "using Mu3Library.Localization.Data;");
            AppendOuterLine(body, 0, "");
            AppendNamespaceStart(body);
            AppendLine(body, 0, $"public sealed class {tableClassName} : TableData");
            AppendLine(body, 0, "{");
            AppendLine(body, 1, $"private const string TableName = {Quote(table.Name)};");
            AppendLine(body, 1, "");

            foreach (GeneratedEntry entry in table.Entries)
            {
                string privateIdentifier = GetPrivateFieldIdentifier(entry.Identifier);
                AppendLine(
                    body,
                    1,
                    $"private static readonly EntryData {privateIdentifier} = new EntryData(TableName, {Quote(entry.Key)}, {Quote(entry.Id)});");
                AppendLine(body, 1, $"public readonly EntryData {entry.Identifier} = {privateIdentifier};");
            }

            if (table.Entries.Count > 0)
                AppendLine(body, 1, "");
            AppendLine(body, 1, "private static readonly IReadOnlyDictionary<string, LocaleData> _tableLocales = new Dictionary<string, LocaleData>");
            AppendLine(body, 1, "{");
            foreach (GeneratedLocale locale in table.Locales)
            {
                AppendLine(
                    body,
                    2,
                    $"{{ {localesClassName}.{locale.Identifier}.Code, {localesClassName}.{locale.Identifier} }},");
            }
            AppendLine(body, 1, "};");
            AppendLine(body, 1, "");
            AppendLine(body, 1, "private static readonly IReadOnlyDictionary<string, EntryData> _tableEntries = new Dictionary<string, EntryData>");
            AppendLine(body, 1, "{");
            foreach (GeneratedEntry entry in table.Entries)
            {
                string privateIdentifier = GetPrivateFieldIdentifier(entry.Identifier);
                AppendLine(body, 2, $"{{ {privateIdentifier}.Key, {privateIdentifier} }},");
            }
            AppendLine(body, 1, "};");
            AppendLine(body, 1, "");
            AppendLine(body, 1, $"public static readonly {tableClassName} Instance = new {tableClassName}();");
            AppendLine(body, 1, "");
            AppendLine(body, 1, $"internal {tableClassName}() : base(");
            AppendLine(body, 2, "TableName,");
            AppendLine(body, 2, "_tableLocales,");
            AppendLine(body, 2, "_tableEntries)");
            AppendLine(body, 1, "{");
            AppendLine(body, 1, "}");
            AppendLine(body, 0, "}");
            AppendNamespaceEnd(body);
            return body.ToString();
        }

        private string BuildRootScriptBody(string className, IReadOnlyList<GeneratedTable> tables)
        {
            var body = new StringBuilder();
            AppendGeneratedHeader(body);
            AppendOuterLine(body, 0, "using System.Collections.Generic;");
            AppendOuterLine(body, 0, "using Mu3Library.Localization.Data;");
            AppendOuterLine(body, 0, "");
            AppendNamespaceStart(body);
            AppendLine(body, 0, $"public static class {className}");
            AppendLine(body, 0, "{");

            foreach (GeneratedTable table in tables)
            {
                string tableClassName = GetPascalCaseIdentifier(className + table.Identifier);
                AppendLine(
                    body,
                    1,
                    $"public static readonly {tableClassName} {table.RootIdentifier} = {tableClassName}.Instance;");
            }

            if (tables.Count > 0)
                AppendLine(body, 1, "");
            AppendLine(body, 1, "public static readonly IReadOnlyDictionary<string, TableData> All = new Dictionary<string, TableData>");
            AppendLine(body, 1, "{");
            foreach (GeneratedTable table in tables)
                AppendLine(body, 2, $"{{ {table.RootIdentifier}.Name, {table.RootIdentifier} }},");
            AppendLine(body, 1, "};");
            AppendLine(body, 0, "}");
            AppendNamespaceEnd(body);
            return body.ToString();
        }

        private string GetClassName()
        {
            return string.IsNullOrWhiteSpace(_scriptClassName)
                ? DefaultClassName
                : ScriptIdentifier.ToPublicMember(ScriptIdentifier.Sanitize(_scriptClassName.Trim()));
        }

        private static string MakeUniqueIdentifier(string value, ISet<string> usedIdentifiers)
        {
            string baseIdentifier = ScriptIdentifier.ToPublicMember(ScriptIdentifier.Sanitize(value));
            string identifier = baseIdentifier;
            int suffix = 2;
            while (!usedIdentifiers.Add(identifier))
                identifier = baseIdentifier + suffix++;
            return identifier;
        }

        private static string GetPascalCaseIdentifier(string identifier)
        {
            return ScriptIdentifier.ToPublicMember(identifier);
        }

        private static string GetPrivateFieldIdentifier(string identifier)
        {
            string publicIdentifier = ScriptIdentifier.ToPublicMember(identifier);
            return "_" + char.ToLowerInvariant(publicIdentifier[0]) + publicIdentifier.Substring(1);
        }

        private static void AppendGeneratedHeader(StringBuilder body)
        {
            body.AppendLine("// <auto-generated />");
            body.AppendLine("// Generated by LocalizationDataExporterDrawer. Do not edit manually.");
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

        private sealed class GeneratedLocale
        {
            public readonly string Code;
            public readonly string EnglishName;
            public readonly string NativeName;
            public readonly string Identifier;

            public GeneratedLocale(string code, string englishName, string nativeName, string identifier)
            {
                Code = code;
                EnglishName = englishName;
                NativeName = nativeName;
                Identifier = identifier;
            }
        }

        private sealed class GeneratedTable
        {
            public readonly string Name;
            public readonly string Identifier;
            public readonly string RootIdentifier;
            public readonly List<GeneratedLocale> Locales;
            public readonly List<GeneratedEntry> Entries;

            public GeneratedTable(
                string name,
                string identifier,
                string rootIdentifier,
                List<GeneratedLocale> locales,
                List<GeneratedEntry> entries)
            {
                Name = name;
                Identifier = identifier;
                RootIdentifier = rootIdentifier;
                Locales = locales;
                Entries = entries;
            }
        }

        private sealed class GeneratedEntry
        {
            public readonly string Key;
            public readonly string Id;
            public readonly string Identifier;

            public GeneratedEntry(string key, string id, string identifier)
            {
                Key = key;
                Id = id;
                Identifier = identifier;
            }
        }
    }
}
#endif
