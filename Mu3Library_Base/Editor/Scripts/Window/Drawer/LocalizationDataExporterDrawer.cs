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

        [SerializeField, HideInInspector] private DefaultAsset _scriptSaveFolder;
        [SerializeField, HideInInspector] private string _scriptNamespace = "";
        [SerializeField, HideInInspector] private string _scriptClassName = "";

        [SerializeField, HideInInspector] private bool _splitByTable = false;
        [SerializeField, HideInInspector] private bool _foldoutTablePreview = false;

        private List<StringTableCollection> _tableCollections = new();
        private List<Locale> _locales = new();
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
                GUILayout.Space(4);

                DrawSplitByTableField();
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

        private void DrawSplitByTableField()
        {
            DrawWithUndo(
                () => EditorGUILayout.Toggle("Split by Table", _splitByTable),
                v => _splitByTable = v,
                "Localization Exporter: Split by Table");
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

            if (_splitByTable)
            {
                // Write per-table files
                foreach (StringTableCollection collection in _tableCollections)
                {
                    string tableName = SanitizeIdentifier(collection.TableCollectionName);
                    string tableFileName = className + tableName;
                    string tableBody = BuildTableScriptBody(className, collection);
                    string tableFilePath = Path.Combine(systemPath, $"{tableFileName}.cs");
                    File.WriteAllText(tableFilePath, tableBody, new UTF8Encoding(true));
                    Debug.Log($"Localization table script generated. path: {tableFilePath}");
                }
                // Write root aggregation file
                string rootBody = BuildSplitRootScriptBody(className);
                string rootFilePath = Path.Combine(systemPath, $"{className}.cs");
                File.WriteAllText(rootFilePath, rootBody, new UTF8Encoding(true));
                Debug.Log($"Localization root script generated. path: {rootFilePath}");
            }
            else
            {
                string scriptBody = BuildScriptBody(className);
                string filePath = Path.Combine(systemPath, $"{className}.cs");
                File.WriteAllText(filePath, scriptBody, new UTF8Encoding(true));
                Debug.Log($"Localization name script generated. path: {filePath}");
            }

            AssetDatabase.Refresh();
        }

        private string BuildSplitRootScriptBody(string baseClassName)
        {
            var lines = new List<object>();

            // Locales: properties delegate to per-table classes
            var globalLocalesSeen = new HashSet<string>();
            var globalLocales = new List<Locale>();
            foreach (StringTableCollection collection in _tableCollections)
                foreach (Locale locale in _locales)
                {
                    StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                    if (table != null && globalLocalesSeen.Add(locale.Identifier.Code))
                        globalLocales.Add(locale);
                }

            if (globalLocales.Count > 0)
            {
                var localesContent = new List<object>();
                // Determine which table each locale first appears in
                var localeSourceTable = new Dictionary<string, string>();
                foreach (StringTableCollection collection in _tableCollections)
                {
                    string tClass = baseClassName + SanitizeIdentifier(collection.TableCollectionName);
                    foreach (Locale locale in _locales)
                    {
                        StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                        if (table != null)
                        {
                            string locId = SanitizeIdentifier(locale.Identifier.Code);
                            if (!localeSourceTable.ContainsKey(locId))
                                localeSourceTable[locId] = tClass;
                        }
                    }
                }
                foreach (Locale locale in globalLocales)
                {
                    string locId = SanitizeIdentifier(locale.Identifier.Code);
                    string srcClass = localeSourceTable[locId];
                    localesContent.Add($"public static LocaleData {locId} => {srcClass}.Locales.{locId};");
                }
                localesContent.Add("");
                var allLocaleEntries = globalLocales.Select(l =>
                {
                    string n = SanitizeIdentifier(l.Identifier.Code);
                    return (object)$"{{ {n}.Code, {n} }},";
                }).ToList();
                localesContent.Add(new ScriptBuilder.CodeBlock
                {
                    Header = "public static readonly IReadOnlyDictionary<string, LocaleData> All = new Dictionary<string, LocaleData>",
                    Content = allLocaleEntries,
                    Suffix = ";"
                });
                lines.Add(new ScriptBuilder.CodeBlock { Header = "public static class Locales", Content = localesContent });
                lines.Add("");
            }

            // Tables: properties delegate to per-table class Data fields
            var tablesContent = new List<object>();
            foreach (StringTableCollection col in _tableCollections)
            {
                string n = SanitizeIdentifier(col.TableCollectionName);
                string tClass = baseClassName + n;
                tablesContent.Add($"public static TableData {n} => {tClass}.Data;");
            }
            tablesContent.Add("");
            var allTableEntries = _tableCollections.Select(col =>
            {
                string n = SanitizeIdentifier(col.TableCollectionName);
                return (object)$"{{ {n}.Name, {n} }},";
            }).ToList();
            tablesContent.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static readonly IReadOnlyDictionary<string, TableData> All = new Dictionary<string, TableData>",
                Content = allTableEntries,
                Suffix = ";"
            });
            lines.Add(new ScriptBuilder.CodeBlock { Header = "public static class Tables", Content = tablesContent });

            var classBlock = new ScriptBuilder.CodeBlock
            {
                Header = $"public static class {baseClassName}",
                Content = lines
            };

            string usingStatement = "using System.Collections.Generic;" + System.Environment.NewLine
                + "using Mu3Library.Localization.Data;" + System.Environment.NewLine + System.Environment.NewLine;

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

        private string BuildTableScriptBody(string baseClassName, StringTableCollection collection)
        {
            string tableClassName = SanitizeIdentifier(collection.TableCollectionName);
            string className = baseClassName + tableClassName;
            var classLines = new List<object>();
            var tableLines = new List<object>();

            var tableLocaleIdentifiers = new List<string>();
            foreach (Locale locale in _locales)
            {
                StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                if (table != null)
                    tableLocaleIdentifiers.Add(SanitizeIdentifier(locale.Identifier.Code));
            }

            var entries = collection.SharedData?.Entries;

            // Locales: top-level LocaleData fields
            var outerLocalesContent = new List<object>();
            foreach (Locale locale in _locales)
            {
                StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                if (table != null)
                {
                    var ci = locale.Identifier.CultureInfo;
                    string englishName = ci?.EnglishName ?? locale.Identifier.Code;
                    string nativeName = ci?.NativeName ?? locale.Identifier.Code;
                    string locId = SanitizeIdentifier(locale.Identifier.Code);
                    outerLocalesContent.Add($"public static readonly LocaleData {locId} = new LocaleData(\"{locale.Identifier.Code}\", \"{englishName}\", \"{nativeName}\");");
                }
            }
            outerLocalesContent.Add("");
            var allLocaleEntries = tableLocaleIdentifiers.Select(n => (object)$"{{ {n}.Code, {n} }},").ToList();
            outerLocalesContent.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static readonly IReadOnlyDictionary<string, LocaleData> All = new Dictionary<string, LocaleData>",
                Content = allLocaleEntries,
                Suffix = ";"
            });
            classLines.Add(new ScriptBuilder.CodeBlock { Header = "public static class Locales", Content = outerLocalesContent });
            classLines.Add("");

            // Inner Locales on TableData — reference outer
            var innerLocalesContent = new List<object>();
            foreach (string locId in tableLocaleIdentifiers)
                innerLocalesContent.Add($"public static readonly LocaleData {locId} = {className}.Locales.{locId};");
            tableLines.Add(new ScriptBuilder.CodeBlock { Header = "public new static class Locales", Content = innerLocalesContent });
            tableLines.Add("");

            // Entry fields
            if (entries != null)
                foreach (SharedTableData.SharedTableEntry entry in entries)
                {
                    string keyId = SanitizeIdentifier(entry.Key);
                    tableLines.Add($"public static readonly EntryData {keyId} = new EntryData(\"{collection.TableCollectionName}\", \"{entry.Key}\", \"{entry.Id}\");");
                }
            tableLines.Add("");

            // Constructor
            tableLines.Add($"internal {tableClassName}Data() : base(");
            tableLines.Add($"    \"{collection.TableCollectionName}\",");
            tableLines.Add("    new Dictionary<string, LocaleData>");
            tableLines.Add("    {");
            foreach (string locId in tableLocaleIdentifiers)
                tableLines.Add($"        {{ Locales.{locId}.Code, Locales.{locId} }},");
            tableLines.Add("    },");
            tableLines.Add("    new Dictionary<string, EntryData>");
            tableLines.Add("    {");
            if (entries != null)
                foreach (SharedTableData.SharedTableEntry entry in entries)
                {
                    string keyId = SanitizeIdentifier(entry.Key);
                    tableLines.Add($"        {{ {keyId}.Key, {keyId} }},");
                }
            tableLines.Add("    })");
            tableLines.Add("{ }");

            classLines.Add(new ScriptBuilder.CodeBlock
            {
                Header = $"public sealed class {tableClassName}Data : TableData",
                Content = tableLines
            });
            classLines.Add("");
            classLines.Add($"public static readonly {tableClassName}Data Data = new {tableClassName}Data();");

            var classBlock = new ScriptBuilder.CodeBlock
            {
                Header = $"public static class {className}",
                Content = classLines
            };

            string usingStatement = "using System.Collections.Generic;" + System.Environment.NewLine
                + "using Mu3Library.Localization.Data;" + System.Environment.NewLine + System.Environment.NewLine;

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

        private string BuildScriptBody(string className)
        {
            var lines = new List<object>();

            // Root-level Locales class: all unique locales available across all tables
            var globalLocalesSeen = new HashSet<string>();
            var globalLocales = new List<Locale>();
            foreach (StringTableCollection collection in _tableCollections)
            {
                foreach (Locale locale in _locales)
                {
                    StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                    if (table != null && globalLocalesSeen.Add(locale.Identifier.Code))
                        globalLocales.Add(locale);
                }
            }

            var globalLocalesContent = new List<object>();
            // Locale fields declared first — All initializer references them by name
            foreach (Locale locale in globalLocales)
            {
                var cultureInfo = locale.Identifier.CultureInfo;
                string englishName = cultureInfo?.EnglishName ?? locale.Identifier.Code;
                string nativeName = cultureInfo?.NativeName ?? locale.Identifier.Code;
                string n = SanitizeIdentifier(locale.Identifier.Code);
                globalLocalesContent.Add($"public static readonly LocaleData {n} = new LocaleData(\"{locale.Identifier.Code}\", \"{englishName}\", \"{nativeName}\");");
            }
            globalLocalesContent.Add("");
            // All: references locale fields directly
            var globalLocalesAllEntries = globalLocales.Select(l =>
            {
                string n = SanitizeIdentifier(l.Identifier.Code);
                return (object)$"{{ {n}.Code, {n} }},";
            }).ToList();
            globalLocalesContent.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static readonly IReadOnlyDictionary<string, LocaleData> All = new Dictionary<string, LocaleData>",
                Content = globalLocalesAllEntries,
                Suffix = ";"
            });

            lines.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static class Locales",
                Content = globalLocalesContent
            });
            lines.Add("");

            // Tables class: each table is a sealed class inheriting TableData;
            // All holds direct references — no wrapper object, no duplication.
            var tablesContent = new List<object>();

            // Typed static fields declared before All so field initializers run in order
            foreach (StringTableCollection col in _tableCollections)
            {
                string n = SanitizeIdentifier(col.TableCollectionName);
                tablesContent.Add($"public static readonly {n}Data {n} = new {n}Data();");
            }
            tablesContent.Add("");

            // All: direct references to the typed instances above
            var allDictEntries = _tableCollections.Select(col =>
            {
                string n = SanitizeIdentifier(col.TableCollectionName);
                return (object)$"{{ {n}.Name, {n} }},";
            }).ToList();
            tablesContent.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static readonly IReadOnlyDictionary<string, TableData> All = new Dictionary<string, TableData>",
                Content = allDictEntries,
                Suffix = ";"
            });

            foreach (StringTableCollection collection in _tableCollections)
            {
                string tableClassName = SanitizeIdentifier(collection.TableCollectionName);
                var tableLines = new List<object>();

                var tableLocaleIdentifiers = new List<string>();
                foreach (Locale locale in _locales)
                {
                    StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                    if (table != null)
                        tableLocaleIdentifiers.Add(SanitizeIdentifier(locale.Identifier.Code));
                }

                var entries = collection.SharedData?.Entries;

                // Locales: typed LocaleData fields — direct references to root instances (declared first)
                var tableLocalesContent = new List<object>();
                foreach (Locale locale in _locales)
                {
                    StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                    if (table != null)
                    {
                        string locId = SanitizeIdentifier(locale.Identifier.Code);
                        tableLocalesContent.Add($"public static readonly LocaleData {locId} = {className}.Locales.{locId};");
                    }
                }
                tableLines.Add(new ScriptBuilder.CodeBlock
                {
                    Header = "public new static class Locales",
                    Content = tableLocalesContent
                });
                tableLines.Add("");

                // Entry fields — direct EntryData instances (declared before constructor)
                if (entries != null)
                    foreach (SharedTableData.SharedTableEntry entry in entries)
                    {
                        string keyClassName = SanitizeIdentifier(entry.Key);
                        tableLines.Add($"public static readonly EntryData {keyClassName} = new EntryData(\"{collection.TableCollectionName}\", \"{entry.Key}\", \"{entry.Id}\");");
                    }
                tableLines.Add("");

                // Constructor — references Locales and entry fields declared above
                tableLines.Add($"internal {tableClassName}Data() : base(");
                tableLines.Add($"    \"{collection.TableCollectionName}\",");
                tableLines.Add("    new Dictionary<string, LocaleData>");
                tableLines.Add("    {");
                foreach (string locId in tableLocaleIdentifiers)
                    tableLines.Add($"        {{ Locales.{locId}.Code, Locales.{locId} }},");
                tableLines.Add("    },");
                tableLines.Add("    new Dictionary<string, EntryData>");
                tableLines.Add("    {");
                if (entries != null)
                    foreach (SharedTableData.SharedTableEntry entry in entries)
                    {
                        string keyId = SanitizeIdentifier(entry.Key);
                        tableLines.Add($"        {{ {keyId}.Key, {keyId} }},");
                    }
                tableLines.Add("    })");
                tableLines.Add("{ }");

                tablesContent.Add(new ScriptBuilder.CodeBlock
                {
                    Header = $"public sealed class {tableClassName}Data : TableData",
                    Content = tableLines
                });
            }

            lines.Add(new ScriptBuilder.CodeBlock
            {
                Header = "public static class Tables",
                Content = tablesContent
            });

            var classBlock = new ScriptBuilder.CodeBlock
            {
                Header = $"public static class {className}",
                Content = lines
            };

            string usingStatement = "using System.Collections.Generic;" + System.Environment.NewLine + "using Mu3Library.Localization.Data;" + System.Environment.NewLine + System.Environment.NewLine;

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
                    // '_', '-', and all other non-identifier chars act as camelCase word boundary
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
