#if MU3LIBRARY_LOCALIZATION_SUPPORT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class LocalizationCharacterCollectorDrawer : Mu3WindowDrawer
    {
        public const string FileName = "LocalizationCharacterCollector";
        private const string ItemName = "Localization Character Collector";
        private const string MenuName = MenuRoot + "/" + ItemName;

        // Runtime data — not serialized (refreshed each session)
        private List<StringTableCollection> _tableCollections = new();
        private List<Locale> _locales = new();
        // Cached display lists — rebuilt in RefreshData, used in DrawSelectionOptions
        private List<string> _tableNames = new();
        private List<string> _localeCodes = new();

        // Persisted multi-selections — stored as stable string keys (name / locale code)
        [SerializeField, HideInInspector] private List<string> _selectedTableNames = new();
        [SerializeField, HideInInspector] private List<string> _selectedLocaleCodes = new();
        [SerializeField, HideInInspector] private bool _allTablesSelected = true;
        [SerializeField, HideInInspector] private bool _allLocalesSelected = true;
        [SerializeField, HideInInspector] private string _outputPath = string.Empty;
        [SerializeField, HideInInspector] private string _fileName = "characters";
        [SerializeField, HideInInspector] private string _customCharacters = string.Empty;

        // Runtime sets for O(1) lookup — kept in sync with the serialized lists
        private HashSet<string> _selectedTableNameSet = new();
        private HashSet<string> _selectedLocaleCodeSet = new();

        private Vector2 _customCharScrollPos;

        // Result state — List preserves encounter order; HashSet provides O(1) dedup
        private readonly Dictionary<string, List<char>> _collectedCharOrders = new();
        private bool _hasResult = false;
        private bool _isDataLoaded = false;



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
            DrawStruct(() =>
            {
                if (!_isDataLoaded)
                {
                    RefreshData();
                }

                DrawRefreshButton();

                GUILayout.Space(8);

                DrawSelectionOptions();

                GUILayout.Space(8);

                DrawCustomCharacters();

                GUILayout.Space(8);

                DrawOutputPath();

                GUILayout.Space(4);

                DrawFileName();

                GUILayout.Space(12);

                DrawCollectButton();

                if (_hasResult && _collectedCharOrders.Count > 0)
                {
                    GUILayout.Space(20);
                    DrawResults();
                }
            }, 20, 20, 0, 0);
        }

        private void RefreshData()
        {
            _tableCollections = LocalizationEditorSettings
                .GetStringTableCollections()
                .OfType<StringTableCollection>()
                .ToList();

            _locales = LocalizationEditorSettings.GetLocales()?.ToList() ?? new List<Locale>();

            _tableNames = _tableCollections.Select(t => t.TableCollectionName).ToList();
            _localeCodes = _locales.Select(l => l.Identifier.Code).ToList();

            // Rebuild runtime sets from serialized lists, pruning stale entries
            HashSet<string> validTableNames = new(_tableNames);
            HashSet<string> validLocaleCodes = new(_localeCodes);

            _selectedTableNames.RemoveAll(n => !validTableNames.Contains(n));
            _selectedLocaleCodes.RemoveAll(c => !validLocaleCodes.Contains(c));

            // If nothing was previously saved and not in all-mode, default to everything selected
            if (_selectedTableNames.Count == 0 && !_allTablesSelected)
            {
                _selectedTableNames.AddRange(validTableNames);
            }
            if (_selectedLocaleCodes.Count == 0 && !_allLocalesSelected)
            {
                _selectedLocaleCodes.AddRange(validLocaleCodes);
            }

            SyncSetsFromLists();

            _collectedCharOrders.Clear();
            _hasResult = false;
            _isDataLoaded = true;
        }

        private void SyncSetsFromLists()
        {
            _selectedTableNameSet = new HashSet<string>(_selectedTableNames);
            _selectedLocaleCodeSet = new HashSet<string>(_selectedLocaleCodes);
        }

        private void SyncListsFromSets()
        {
            _selectedTableNames = _selectedTableNameSet.ToList();
            _selectedLocaleCodes = _selectedLocaleCodeSet.ToList();
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

        private void DrawSelectionOptions()
        {
            if (_tableCollections.Count == 0)
            {
                EditorGUILayout.HelpBox("No String Table Collections found. Click Refresh.", MessageType.Warning);
                return;
            }

            if (_locales.Count == 0)
            {
                EditorGUILayout.HelpBox("No Locales found. Click Refresh.", MessageType.Warning);
                return;
            }

            string tableLabel = _allTablesSelected
                ? "String Tables  (All)"
                : $"String Tables  ({_selectedTableNameSet.Count} / {_tableCollections.Count} selected)";
            DrawMultiSelect(
                label: tableLabel,
                ref _allTablesSelected,
                items: _tableNames,
                selectedSet: _selectedTableNameSet,
                onChanged: SyncListsFromSets,
                undoActionName: "Localization Collector: Table Selection");

            GUILayout.Space(4);

            string localeLabel = _allLocalesSelected
                ? "Locales  (All)"
                : $"Locales  ({_selectedLocaleCodeSet.Count} / {_locales.Count} selected)";
            DrawMultiSelect(
                label: localeLabel,
                ref _allLocalesSelected,
                items: _localeCodes,
                selectedSet: _selectedLocaleCodeSet,
                onChanged: SyncListsFromSets,
                undoActionName: "Localization Collector: Locale Selection");
        }

        /// <summary>
        /// Label header with an "All" trigger toggle.
        /// When All is ON  → only the All toggle is shown (items hidden).
        /// When All is OFF → individual item toggles are shown.
        /// </summary>
        private void DrawMultiSelect(
            string label,
            ref bool allMode,
            List<string> items,
            HashSet<string> selectedSet,
            Action onChanged,
            string undoActionName)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            bool newAllMode = EditorGUILayout.ToggleLeft("All", allMode);
            if (newAllMode != allMode)
            {
                Undo.RecordObject(this, undoActionName);
                allMode = newAllMode;
                if (allMode)
                {
                    // Entering All mode — clear individual selection (not needed)
                    selectedSet.Clear();
                }
                else
                {
                    // Leaving All mode — default individual selection to everything
                    foreach (string item in items)
                    {
                        selectedSet.Add(item);
                    }
                }
                onChanged?.Invoke();
                EditorUtility.SetDirty(this);
            }

            if (!allMode)
            {
                // Draw items as a grid; column width is fixed so rows wrap automatically
                const float itemWidth = 140f;
                // Subtract DrawStruct left/right margins (20+20) and indent
                float availableWidth = EditorGUIUtility.currentViewWidth
                    - 40f
                    - EditorGUI.indentLevel * 15f;
                int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / itemWidth));

                int col = 0;
                EditorGUILayout.BeginHorizontal();
                foreach (string item in items)
                {
                    if (col == columns)
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        col = 0;
                    }

                    bool isSelected = selectedSet.Contains(item);
                    bool newIsSelected = EditorGUILayout.ToggleLeft(item, isSelected, GUILayout.Width(itemWidth));
                    if (newIsSelected != isSelected)
                    {
                        Undo.RecordObject(this, undoActionName);
                        if (newIsSelected) selectedSet.Add(item);
                        else selectedSet.Remove(item);
                        onChanged?.Invoke();
                        EditorUtility.SetDirty(this);
                    }
                    col++;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
        }

        private void DrawCustomCharacters()
        {
            EditorGUILayout.LabelField("Custom Characters", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Additional characters merged into every locale output:", EditorStyles.miniLabel);

            // Scrollable text area
            _customCharScrollPos = EditorGUILayout.BeginScrollView(
                _customCharScrollPos,
                GUILayout.Height(80));

            EditorGUI.BeginChangeCheck();
            string newCustom = EditorGUILayout.TextArea(
                _customCharacters,
                GUILayout.ExpandHeight(true));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Localization Collector: Custom Characters");
                _customCharacters = newCustom;
                EditorUtility.SetDirty(this);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"{new HashSet<char>(_customCharacters ?? string.Empty).Count} unique custom chars",
                EditorStyles.miniLabel);
            if (GUILayout.Button("Clear", GUILayout.Width(50), GUILayout.Height(18)))
            {
                Undo.RecordObject(this, "Localization Collector: Custom Characters");
                _customCharacters = string.Empty;
                EditorUtility.SetDirty(this);
                // Force IMGUI to flush the TextArea's internal edit buffer
                GUIUtility.keyboardControl = 0;
                GUIUtility.hotControl = 0;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        private void DrawOutputPath()
        {
            // Resolve current path to a folder asset for the ObjectField
            DefaultAsset folderAsset = string.IsNullOrEmpty(_outputPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<DefaultAsset>(_outputPath);

            EditorGUI.BeginChangeCheck();
            DefaultAsset newFolderAsset = EditorGUILayout.ObjectField(
                "Output Folder",
                folderAsset,
                typeof(DefaultAsset),
                allowSceneObjects: false) as DefaultAsset;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Localization Collector: Output Folder");
                if (newFolderAsset != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(newFolderAsset);
                    if (AssetDatabase.IsValidFolder(assetPath))
                    {
                        _outputPath = assetPath;
                    }
                    else
                    {
                        Debug.LogWarning("[LocalizationCharacterCollector] Please drop a folder, not a file.");
                    }
                }
                else
                {
                    _outputPath = string.Empty;
                }
                EditorUtility.SetDirty(this);
            }
        }

        private void DrawFileName()
        {
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField("File Name", _fileName);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Localization Collector: File Name");
                _fileName = newName;
                EditorUtility.SetDirty(this);
            }
        }

        private void DrawCollectButton()
        {
            if (string.IsNullOrWhiteSpace(_outputPath))
            {
                EditorGUILayout.HelpBox("Output folder is not set. Drop a folder into the field above.", MessageType.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_fileName))
            {
                EditorGUILayout.HelpBox("File name cannot be empty.", MessageType.Warning);
                return;
            }

            if (_tableCollections.Count == 0)
            {
                EditorGUILayout.HelpBox("No String Tables available. Click Refresh.", MessageType.Warning);
                return;
            }

            if (!_allTablesSelected && _selectedTableNameSet.Count == 0)
            {
                EditorGUILayout.HelpBox("Select at least one String Table.", MessageType.Warning);
                return;
            }

            if (_locales.Count == 0)
            {
                EditorGUILayout.HelpBox("No Locales available. Click Refresh.", MessageType.Warning);
                return;
            }

            if (!_allLocalesSelected && _selectedLocaleCodeSet.Count == 0)
            {
                EditorGUILayout.HelpBox("Select at least one Locale.", MessageType.Warning);
                return;
            }

            if (GUILayout.Button("Collect & Save", GUILayout.Height(30)))
            {
                CollectAndSave();
            }
        }

        private void DrawResults()
        {
            DrawHeader2("[ Results ]");

            foreach (KeyValuePair<string, List<char>> kvp in _collectedCharOrders)
            {
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(80));
                EditorGUILayout.LabelField($"{kvp.Value.Count} chars", GUILayout.Width(80));
                EditorGUILayout.LabelField($"→  {_outputPath}/{_fileName}_{kvp.Key}.txt");

                EditorGUILayout.EndHorizontal();
            }
        }

        private void CollectAndSave()
        {
            _collectedCharOrders.Clear();
            _hasResult = false;

            List<StringTableCollection> targetTables = _allTablesSelected
                ? _tableCollections
                : _tableCollections.Where(t => _selectedTableNameSet.Contains(t.TableCollectionName)).ToList();

            IEnumerable<Locale> targetLocales = _allLocalesSelected
                ? _locales
                : _locales.Where(l => _selectedLocaleCodeSet.Contains(l.Identifier.Code));

            // Per-collection dedup sets — local to this call only
            Dictionary<string, HashSet<char>> charSeen = new();

            foreach (Locale locale in targetLocales)
            {
                string localeCode = locale.Identifier.Code;
                if (!_collectedCharOrders.ContainsKey(localeCode))
                {
                    _collectedCharOrders[localeCode] = new List<char>();
                    charSeen[localeCode] = new HashSet<char>();
                }

                List<char> ordered = _collectedCharOrders[localeCode];
                HashSet<char> seen = charSeen[localeCode];

                // Merge custom characters first so they appear before table-derived chars
                if (!string.IsNullOrEmpty(_customCharacters))
                {
                    foreach (char c in _customCharacters)
                    {
                        if (seen.Add(c))
                        {
                            ordered.Add(c);
                        }
                    }
                }

                foreach (StringTableCollection collection in targetTables)
                {
                    StringTable table = collection.GetTable(locale.Identifier) as StringTable;
                    if (table == null)
                    {
                        continue;
                    }

                    foreach (StringTableEntry entry in table.Values)
                    {
                        if (string.IsNullOrEmpty(entry.LocalizedValue))
                        {
                            continue;
                        }

                        foreach (char c in entry.LocalizedValue)
                        {
                            if (seen.Add(c))
                            {
                                ordered.Add(c);
                            }
                        }
                    }
                }
            }

            SaveFiles();
        }

        private void SaveFiles()
        {
            try
            {
                string absoluteOutputPath = ResolveAbsolutePath(_outputPath);
                Directory.CreateDirectory(absoluteOutputPath);

                int savedCount = 0;
                foreach (KeyValuePair<string, List<char>> kvp in _collectedCharOrders)
                {
                    if (kvp.Value.Count == 0)
                    {
                        continue;
                    }

                    string content = new string(kvp.Value.ToArray());

                    string filePath = Path.Combine(absoluteOutputPath, $"{_fileName}_{kvp.Key}.txt");
                    File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    savedCount++;
                }

                AssetDatabase.Refresh();
                _hasResult = true;

                Debug.Log($"[LocalizationCharacterCollector] Saved {savedCount} locale file(s) to '{_outputPath}'.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LocalizationCharacterCollector] Failed to save files: {e.Message}");
            }
        }

        private static string ResolveAbsolutePath(string path)
        {
            if (path.StartsWith("Assets/") || path.StartsWith("Assets\\"))
            {
                return Path.GetFullPath(
                    Path.Combine(Application.dataPath, path.Substring("Assets/".Length)));
            }

            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }
    }
}
#endif
