#if MU3LIBRARY_INPUTSYSTEM_SUPPORT
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mu3Library.Editor.FileUtil;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class InputSystemNameExporterDrawer : Mu3WindowDrawer
    {
        public const string FileName = "InputSystemNameExporter";
        private const string ItemName = "InputSystem Name Exporter";
        private const string MenuName = MenuRoot + "/" + ItemName;

        [SerializeField, HideInInspector] private InputActionAsset _inputActionAsset;
        [SerializeField, HideInInspector] private DefaultAsset _scriptSaveFolder;
        [SerializeField, HideInInspector] private string _assetId = "";
        [SerializeField, HideInInspector] private string _scriptNamespace = "";
        [SerializeField, HideInInspector] private string _scriptClassName = "";

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

        private SerializedProperty m_serializedPropInputActionAsset;
        private SerializedProperty _serializedPropInputActionAsset
        {
            get
            {
                if (m_serializedPropInputActionAsset == null)
                    m_serializedPropInputActionAsset = _serializedObject.FindProperty(nameof(_inputActionAsset));
                return m_serializedPropInputActionAsset;
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

        [SerializeField, HideInInspector] private bool _foldoutActionMaps = false;



        public override void OnGUIHeader()
        {
            DrawFoldoutHeader1(ItemName, ref _foldout);
        }

        public override void OnGUIBody()
        {
            if (!_foldout) return;

            DrawStruct(() =>
            {
                DrawInputActionAssetField();
                GUILayout.Space(4);

                DrawScriptSaveFolderField();
                GUILayout.Space(4);

                DrawAssetIdField();
                GUILayout.Space(4);

                DrawNamespaceField();
                GUILayout.Space(4);

                DrawClassNameField();
                GUILayout.Space(8);

                DrawActionMapPreview();
                GUILayout.Space(8);

                DrawValidationAndButton();

            }, 20, 20, 0, 0);
        }

        private void DrawInputActionAssetField()
        {
            _serializedObject.Update();
            EditorGUILayout.PropertyField(_serializedPropInputActionAsset, new GUIContent("Input Action Asset"));
            _serializedObject.ApplyModifiedProperties();
        }

        private void DrawAssetIdField()
        {
            DrawWithUndo(
                () => EditorGUILayout.TextField("Asset ID", _assetId),
                v => _assetId = v,
                "InputSystem Exporter: Asset ID");
        }

        private void DrawNamespaceField()
        {
            DrawWithUndo(
                () => EditorGUILayout.TextField("Namespace (optional)", _scriptNamespace),
                v => _scriptNamespace = v,
                "InputSystem Exporter: Namespace");
        }

        private void DrawClassNameField()
        {
            DrawWithUndo(
                () => EditorGUILayout.TextField("Class Name (optional)", _scriptClassName),
                v => _scriptClassName = v,
                "InputSystem Exporter: Class Name");

            if (string.IsNullOrWhiteSpace(_scriptClassName) && _inputActionAsset != null)
            {
                string placeholder = SanitizeIdentifier(_inputActionAsset.name);
                EditorGUILayout.HelpBox($"Default class name: {placeholder}", MessageType.None);
            }
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

        private void DrawActionMapPreview()
        {
            if (_inputActionAsset == null) return;

            DrawFoldoutHeader2("Action Maps & Actions Preview", ref _foldoutActionMaps);

            if (!_foldoutActionMaps) return;

            DrawStruct(() =>
            {
                foreach (InputActionMap map in _inputActionAsset.actionMaps)
                {
                    EditorGUILayout.LabelField($"[ActionMap]  {map.name}", EditorStyles.boldLabel);

                    DrawStruct(() =>
                    {
                        var actions = map.actions;
                        if (actions.Count == 0) return;

                        float availWidth = EditorGUILayout.GetControlRect(false, 0).width;
                        float colWidth = Mathf.Floor(availWidth / GridColumns);
                        float lineHeight = EditorGUIUtility.singleLineHeight;

                        for (int i = 0; i < actions.Count; i += GridColumns)
                        {
                            Rect rowRect = EditorGUILayout.GetControlRect(false, lineHeight);
                            for (int col = 0; col < GridColumns; col++)
                            {
                                int idx = i + col;
                                if (idx >= actions.Count) break;
                                Rect cellRect = new Rect(
                                    rowRect.x + col * colWidth,
                                    rowRect.y,
                                    colWidth,
                                    rowRect.height);
                                EditorGUI.LabelField(cellRect, $"• {actions[idx].name}");
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
            if (_inputActionAsset == null)
                return "Input Action Asset is not assigned.";

            if (_scriptSaveFolder == null)
                return "Script Save Folder is not set. Drag & drop a project folder.";

            if (string.IsNullOrWhiteSpace(_assetId))
                return "Asset ID is empty. Please enter an ID.";

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
                : SanitizeIdentifier(_inputActionAsset.name);
            string scriptBody = BuildScriptBody(className);
            string filePath = Path.Combine(systemPath, $"{className}.cs");

            File.WriteAllText(filePath, scriptBody, new UTF8Encoding(true));

            AssetDatabase.Refresh();

            Debug.Log($"InputSystem name script generated. path: {filePath}");
        }

        private string BuildScriptBody(string className)
        {
            var lines = new List<object>();

            // Asset ID constant
            lines.Add($"public const string Name = \"{_inputActionAsset.name}\";");
            lines.Add($"public const string Id = \"{_assetId}\";");
            lines.Add("");

            foreach (InputActionMap map in _inputActionAsset.actionMaps)
            {
                string mapClassName = SanitizeIdentifier(map.name);
                var mapLines = new List<object>();

                mapLines.Add($"public const string Name = \"{map.name}\";");
                mapLines.Add($"public const string Id = \"{map.id}\";");
                mapLines.Add("");

                foreach (InputAction action in map.actions)
                {
                    string actionClassName = SanitizeIdentifier(action.name);
                    mapLines.Add(new ScriptBuilder.CodeBlock
                    {
                        Header = $"public static class {actionClassName}",
                        Content = new List<object>
                        {
                            $"public const string Name = \"{action.name}\";",
                            $"public const string Id = \"{action.id}\";"
                        }
                    });
                }

                lines.Add(new ScriptBuilder.CodeBlock
                {
                    Header = $"public static class {mapClassName}",
                    Content = mapLines
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
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else
                    sb.Append('_');
            }

            // Identifier must not start with a digit
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
