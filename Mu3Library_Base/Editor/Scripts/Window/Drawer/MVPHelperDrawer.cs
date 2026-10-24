using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Mu3Library.Editor.FileUtil;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class MVPHelperDrawer : Mu3WindowDrawer
    {
        public const string FileName = "MVPHelper";
        private const string ItemName = "MVP Helper";
        private const string MenuName = MenuRoot + "/" + ItemName;

        [SerializeField, HideInInspector] private DefaultAsset _scriptSaveFolder;
        [SerializeField, HideInInspector] private string _scriptNamespace = "";
        [SerializeField, HideInInspector] private string _scriptName = "";
        [SerializeField, HideInInspector] private int _scriptSpaces = 4;
        [SerializeField, HideInInspector] private bool _ignoreTypeOverlap = false;
        [SerializeField, HideInInspector] private bool _applyAnimationView = false;

        private SerializedObject m_scriptSaveFolderObject;
        private SerializedObject _scriptSaveFolderObject
        {
            get
            {
                if (m_scriptSaveFolderObject == null)
                {
                    m_scriptSaveFolderObject = new SerializedObject(this);
                }

                return m_scriptSaveFolderObject;
            }
        }

        private SerializedProperty m_scriptSaveFolderProperties;
        private SerializedProperty _scriptSaveFolderProperties
        {
            get
            {
                if (m_scriptSaveFolderProperties == null)
                {
                    m_scriptSaveFolderProperties = _scriptSaveFolderObject.FindProperty(nameof(_scriptSaveFolder));
                }

                return m_scriptSaveFolderProperties;
            }
        }



        public override void OnGUIHeader()
        {
            DrawFoldoutHeader1(ItemName, ref _foldout);
        }

        public override void OnGUIBody()
        {
            DrawStruct(() =>
            {
                DrawScriptSaveFolderField();

                if (_scriptSaveFolder == null)
                {
                    return;
                }

                DrawScriptNameField();

                if (string.IsNullOrEmpty(_scriptName))
                {
                    return;
                }

                DrawScriptSavePropertiesField();

                DrawScriptCreateButton();
            }, 20, 20, 0, 0);
        }

        private void DrawScriptCreateButton()
        {
            string scriptNamespace = _scriptNamespace?.Trim() ?? string.Empty;
            string scriptName = _scriptName.Trim();
            string warning = GetInputWarning(scriptNamespace, scriptName);

            if (warning != null)
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(warning != null))
            {
                if (!GUILayout.Button("Create MVP Components", GUILayout.Height(30)))
                {
                    return;
                }
            }

            string assetPath = FileFinder.GetAssetPath(_scriptSaveFolder);
            string systemPath = FilePathConvertor.AssetPathToSystemPath(assetPath);

            if (!System.IO.Directory.Exists(systemPath))
            {
                Debug.LogWarning($"Folder not found. name: {_scriptSaveFolder.name}");

                _scriptSaveFolder = null;

                _scriptSaveFolderObject.ApplyModifiedProperties();

                return;
            }

            CreateMVPComponents(
                systemPath,
                scriptNamespace,
                scriptName,
                _scriptSpaces,
                _ignoreTypeOverlap,
                _applyAnimationView);
        }

        /// <summary>
        /// Returns why the inputs cannot produce compilable scripts, or null when they can.
        /// An empty namespace is valid and generates the scripts without a namespace block.
        /// </summary>
        private static string GetInputWarning(string scriptNamespace, string scriptName)
        {
            if (!ScriptIdentifier.IsValidIdentifier(scriptName))
            {
                return $"Script Name '{scriptName}' is not a valid C# identifier. " +
                    "Use letters, digits, and '_' only, do not start with a digit, and do not use a C# keyword.";
            }

            if (scriptNamespace.Length > 0 && !ScriptIdentifier.IsValidNamespace(scriptNamespace))
            {
                return $"Script Namespace '{scriptNamespace}' is not a valid namespace. " +
                    "Join C# identifiers with '.', or leave it empty to generate the scripts without a namespace.";
            }

            return null;
        }

        private void DrawScriptSavePropertiesField()
        {
            DrawWithUndo(
                () => EditorGUILayout.IntSlider("Script Spaces", _scriptSpaces, 2, 8),
                v => _scriptSpaces = v,
                "MVP Helper: Script Spaces");

            DrawWithUndo(
                () => EditorGUILayout.Toggle("Ignore Type Overlap", _ignoreTypeOverlap),
                v => _ignoreTypeOverlap = v,
                "MVP Helper: Ignore Type Overlap");

            DrawWithUndo(
                () => EditorGUILayout.Toggle("Apply AnimationView", _applyAnimationView),
                v => _applyAnimationView = v,
                "MVP Helper: Apply AnimationView");
        }

        private void DrawScriptNameField()
        {
            DrawWithUndo(
                () => EditorGUILayout.TextField("Script Namespace", _scriptNamespace),
                v => _scriptNamespace = v,
                "MVP Helper: Script Namespace");

            DrawWithUndo(
                () => EditorGUILayout.TextField("Script Name", _scriptName),
                v => _scriptName = v,
                "MVP Helper: Script Name");
        }

        private void DrawScriptSaveFolderField()
            => DrawAssetsFolderField(_scriptSaveFolderObject, _scriptSaveFolderProperties);

        private void CreateMVPComponents(string folderSystemPath, string scriptNamespace, string scriptName, int spaces, bool ignoreTypeOverlap, bool applyAnimationView)
        {
            if (string.IsNullOrEmpty(folderSystemPath) || string.IsNullOrEmpty(scriptName))
            {
                Debug.LogWarning("Property is null.");

                return;
            }

            string argumentsClassName = $"{scriptName}Arguments";
            string modelClassName = $"{scriptName}Model";
            string viewClassName = $"{scriptName}View";
            string presenterClassName = $"{scriptName}Presenter";

            var components = new List<(string ClassName, string UsingLine, ScriptBuilder.CodeBlock ClassBlock)>
            {
                (argumentsClassName, "using Mu3Library.UI.MVP;", GetArgumentsBlock(argumentsClassName)),
                (modelClassName, "using Mu3Library.UI.MVP;", GetModelBlock(modelClassName, argumentsClassName)),
                (viewClassName, applyAnimationView ? "using Mu3Library.UI.MVP.Animation;" : "using Mu3Library.UI.MVP;", GetViewBlock(viewClassName, applyAnimationView)),
                (presenterClassName, "using Mu3Library.UI.MVP;", GetPresenterBlock(presenterClassName, viewClassName, modelClassName, argumentsClassName)),
            };

            var scripts = new List<(string ClassName, string Body)>();
            foreach (var component in components)
            {
                string fullName = scriptNamespace.Length > 0 ? $"{scriptNamespace}.{component.ClassName}" : component.ClassName;

                if (!ignoreTypeOverlap && IsTypeExist(fullName))
                {
                    Debug.LogWarning($"Type already exist. type: {fullName}");

                    continue;
                }

                if (!TryBuildScript(spaces, scriptNamespace, component.UsingLine, component.ClassBlock, out string body, out string error))
                {
                    Debug.LogError($"MVP components were not created. {error}");

                    return;
                }

                scripts.Add((component.ClassName, body));
            }

            if (scripts.Count == 0 || !ConfirmOverwrite(folderSystemPath, scripts))
            {
                return;
            }

            foreach (var script in scripts)
            {
                WriteComponent(folderSystemPath, script.ClassName, script.Body);
            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Asks before replacing script files that already exist in the save folder.
        /// Returns true when none exists or the user agrees to overwrite them.
        /// </summary>
        private static bool ConfirmOverwrite(string folderSystemPath, List<(string ClassName, string Body)> scripts)
        {
            var existingFiles = new List<string>();
            foreach (var script in scripts)
            {
                string fileName = $"{script.ClassName}.cs";
                if (File.Exists(Path.Combine(folderSystemPath, fileName)))
                {
                    existingFiles.Add(fileName);
                }
            }

            if (existingFiles.Count == 0)
            {
                return true;
            }

            return EditorUtility.DisplayDialog(
                ItemName,
                "These files already exist in the save folder and will be replaced:\n\n" +
                string.Join("\n", existingFiles) +
                "\n\nOverwrite them?",
                "Overwrite",
                "Cancel");
        }

        private static bool TryBuildScript(int spaces, string scriptNamespace, string usingLine, ScriptBuilder.CodeBlock classBlock, out string body, out string error)
        {
            ScriptBuilder.CodeBlock usingBlock = new ScriptBuilder.CodeBlock()
            {
                Content = new List<object> { usingLine, "" },
            };

            ScriptBuilder.CodeBlock rootBlock = classBlock;
            if (scriptNamespace.Length > 0)
            {
                rootBlock = new ScriptBuilder.CodeBlock()
                {
                    Header = $"namespace {scriptNamespace}",
                    Content = new List<object> { classBlock },
                };
            }

            return ScriptBuilder.TryBuild(spaces, out body, out error, usingBlock, rootBlock);
        }

        private static ScriptBuilder.CodeBlock GetPresenterBlock(string className, string viewClassName, string modelClassName, string argumentsClassName)
        {
            return new ScriptBuilder.CodeBlock()
            {
                Header = $"public class {className} : Presenter<{viewClassName}, {modelClassName}, {argumentsClassName}>",
                Content = new List<object>
                {
                    new ScriptBuilder.CodeBlock()
                    {
                        Header = $"protected override void LoadFunc()",
                        Content = new List<object> { "" }
                    },
                    new ScriptBuilder.CodeBlock()
                    {
                        Header = $"protected override void OpenFunc()",
                        Content = new List<object> { "" }
                    },
                    new ScriptBuilder.CodeBlock()
                    {
                        Header = $"protected override void CloseFunc()",
                        Content = new List<object> { "" }
                    },
                    new ScriptBuilder.CodeBlock()
                    {
                        Header = $"protected override void UnloadFunc()",
                        Content = new List<object> { "" }
                    },
                }
            };
        }

        private static ScriptBuilder.CodeBlock GetViewBlock(string className, bool applyAnimationView)
        {
            string parentViewString = applyAnimationView ?
                "AnimationView" :
                "View";

            List<object> openStartContent = new List<object>();
            if (applyAnimationView) openStartContent.Add("base.OpenStart();\r\n");
            openStartContent.Add("");

            List<object> closeStartContent = new List<object>();
            if (applyAnimationView) closeStartContent.Add("base.CloseStart();\r\n");
            closeStartContent.Add("");

            ScriptBuilder.CodeBlock classBlock = new ScriptBuilder.CodeBlock()
            {
                Header = $"public class {className} : {parentViewString}",
                Content = new List<object>()
            };
            classBlock.Content.Add(new ScriptBuilder.CodeBlock()
            {
                Header = $"protected override void LoadFunc()",
                Content = new List<object> { "" }
            });
            classBlock.Content.Add(new ScriptBuilder.CodeBlock()
            {
                Header = $"protected override void OpenStart()",
                Content = openStartContent
            });
            classBlock.Content.Add(new ScriptBuilder.CodeBlock()
            {
                Header = $"protected override void CloseStart()",
                Content = closeStartContent
            });
            classBlock.Content.Add(new ScriptBuilder.CodeBlock()
            {
                Header = $"protected override void UnloadFunc()",
                Content = new List<object> { "" }
            });

            return classBlock;
        }

        private static ScriptBuilder.CodeBlock GetModelBlock(string className, string argumentsClassName)
        {
            return new ScriptBuilder.CodeBlock()
            {
                Header = $"public class {className} : Model<{argumentsClassName}>",
                Content = new List<object>
                {
                    "// You can use this function for create Model.",
                    new ScriptBuilder.CodeBlock()
                    {
                        Header = $"public override void Init({argumentsClassName} args)",
                        Content = new List<object> { "" }
                    },
                }
            };
        }

        private static ScriptBuilder.CodeBlock GetArgumentsBlock(string className)
        {
            return new ScriptBuilder.CodeBlock()
            {
                Header = $"public class {className} : Arguments",
                Content = new List<object> { "" }
            };
        }

        private void AppendLine(StringBuilder sb, string code = "", int spaceCount = 0)
        {
            if (spaceCount > 0)
            {
                sb.Append(' ', spaceCount).AppendLine(code);
            }
            else
            {
                sb.AppendLine(code);
            }
        }

        private bool IsTypeExist(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return false;
            }

#if UNITY_6000_5_OR_NEWER
            var assemblies = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
#else
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
#endif
            foreach (var asm in assemblies)
            {
                try
                {
                    if (asm.GetType(fullName, false) != null)
                    {
                        return true;
                    }
                }
                catch
                {
                    // ...
                }
            }

            return false;
        }

        private void WriteComponent(string folder, string fileName, string body)
        {
            FileCreator.WriteScript(folder, fileName, body);
        }
    }
}
