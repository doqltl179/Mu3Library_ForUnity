using System;
using System.Collections.Generic;
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
            if (!GUILayout.Button("Create MVP Components", GUILayout.Height(30)))
            {
                return;
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
                _scriptNamespace,
                _scriptName,
                _scriptSpaces,
                _ignoreTypeOverlap,
                _applyAnimationView);
        }

        private void DrawScriptSavePropertiesField()
        {
            _scriptSpaces = EditorGUILayout.IntSlider("Script Spaces", _scriptSpaces, 2, 8);

            _ignoreTypeOverlap = EditorGUILayout.Toggle("Ignore Type Overlap", _ignoreTypeOverlap);

            _applyAnimationView = EditorGUILayout.Toggle("Apply AnimationView", _applyAnimationView);
        }

        private void DrawScriptNameField()
        {
            _scriptNamespace = EditorGUILayout.TextField("Script Namespace", _scriptNamespace);
            _scriptName = EditorGUILayout.TextField("Script Name", _scriptName);
        }

        private void DrawScriptSaveFolderField()
        {
            _scriptSaveFolderObject.Update();

            EditorGUILayout.PropertyField(_scriptSaveFolderProperties);

            if (!_scriptSaveFolderObject.ApplyModifiedProperties() || _scriptSaveFolder == null)
            {
                return;
            }

            if (!IsAssetsFolder(_scriptSaveFolder))
            {
                _scriptSaveFolder = null;

                _scriptSaveFolderObject.ApplyModifiedProperties();
            }
        }

        private void CreateMVPComponents(string folderSystemPath, string scriptNamespace, string scriptName, int spaces, bool ignoreTypeOverlap, bool applyAnimationView)
        {
            if (string.IsNullOrEmpty(folderSystemPath) || string.IsNullOrEmpty(scriptName))
            {
                Debug.LogWarning("Property is null.");

                return;
            }

            string argumentsClassName = $"{scriptName}Arguments";
            string argumentsBody = GetArgumentsBody(scriptNamespace, argumentsClassName, spaces, ignoreTypeOverlap);
            if (!string.IsNullOrEmpty(argumentsBody))
            {
                WriteComponent(folderSystemPath, argumentsClassName, argumentsBody);
            }

            string modelClassName = $"{scriptName}Model";
            string modelBody = GetModelBody(scriptNamespace, modelClassName, argumentsClassName, spaces, ignoreTypeOverlap);
            if (!string.IsNullOrEmpty(modelBody))
            {
                WriteComponent(folderSystemPath, modelClassName, modelBody);
            }

            string viewClassName = $"{scriptName}View";
            string viewBody = GetViewBody(scriptNamespace, viewClassName, spaces, ignoreTypeOverlap, applyAnimationView);
            if (!string.IsNullOrEmpty(viewBody))
            {
                WriteComponent(folderSystemPath, viewClassName, viewBody);
            }

            string presenterClassName = $"{scriptName}Presenter";
            string presenterBody = GetPresenterBody(scriptNamespace, presenterClassName, viewClassName, modelClassName, argumentsClassName, spaces, ignoreTypeOverlap);
            if (!string.IsNullOrEmpty(presenterBody))
            {
                WriteComponent(folderSystemPath, presenterClassName, presenterBody);
            }

            AssetDatabase.Refresh();
        }

        private bool IsAssetsFolder(DefaultAsset folder)
        {
            string path = FileFinder.GetAssetPath(folder);

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return path == "Assets" || path.StartsWith("Assets/");
        }

        private string GetPresenterBody(string scriptNamespace, string className, string viewClassName, string modelClassName, string argumentsClassName, int spaces, bool ignoreTypeOverlap)
        {
            string fullName = $"{scriptNamespace}.{className}";

            if (!ignoreTypeOverlap && IsTypeExist(fullName))
            {
                Debug.LogWarning($"Type already exist. type: {fullName}");

                return "";
            }

            ScriptBuilder.CodeBlock classBlock = new ScriptBuilder.CodeBlock()
            {
                Header = $"public class {className} : Presenter<{viewClassName}, {modelClassName}, {argumentsClassName}>",
                Content = new List<object>
                {
                    new ScriptBuilder.CodeBlock()
                    {
                        Header = $"public override void Load()",
                        Content = new List<object> { "base.Load();\r\n\r\n" }
                    },
                    new ScriptBuilder.CodeBlock()
                    {
                        Header = $"public override void Open()",
                        Content = new List<object> { "base.Open();\r\n\r\n" }
                    },
                    new ScriptBuilder.CodeBlock()
                    {
                        Header = $"public override void Close(bool forceClose = false)",
                        Content = new List<object> { "base.Close(forceClose);\r\n\r\n" }
                    },
                    new ScriptBuilder.CodeBlock()
                    {
                        Header = $"public override void Unload()",
                        Content = new List<object> { "base.Unload();\r\n\r\n" }
                    },
                }
            };
            ScriptBuilder.CodeBlock namespaceBlock = new ScriptBuilder.CodeBlock()
            {
                Header = $"namespace {scriptNamespace}",
                Content = new List<object> { classBlock },
            };
            ScriptBuilder.CodeBlock usingBlock = new ScriptBuilder.CodeBlock()
            {
                Content = new List<object> { "using Mu3Library.UI.MVP;", "" },
            };

            return ScriptBuilder.Build(spaces, new ScriptBuilder.CodeBlock[]
            {
                usingBlock,
                namespaceBlock
            });
        }

        private string GetViewBody(string scriptNamespace, string className, int spaces, bool ignoreTypeOverlap, bool applyAnimationView)
        {
            string fullName = $"{scriptNamespace}.{className}";

            if (!ignoreTypeOverlap && IsTypeExist(fullName))
            {
                Debug.LogWarning($"Type already exist. type: {fullName}");

                return "";
            }

            string usingString = applyAnimationView ?
                "using Mu3Library.UI.MVP.Animation;" :
                "using Mu3Library.UI.MVP;";
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
            ScriptBuilder.CodeBlock namespaceBlock = new ScriptBuilder.CodeBlock()
            {
                Header = $"namespace {scriptNamespace}",
                Content = new List<object> { classBlock },
            };
            ScriptBuilder.CodeBlock usingBlock = new ScriptBuilder.CodeBlock()
            {
                Content = new List<object> { $"{usingString}", "" },
            };

            return ScriptBuilder.Build(spaces, new ScriptBuilder.CodeBlock[]
            {
                usingBlock,
                namespaceBlock
            });
        }

        private string GetModelBody(string scriptNamespace, string className, string argumentsClassName, int spaces, bool ignoreTypeOverlap)
        {
            string fullName = $"{scriptNamespace}.{className}";

            if (!ignoreTypeOverlap && IsTypeExist(fullName))
            {
                Debug.LogWarning($"Type already exist. type: {fullName}");

                return "";
            }

            ScriptBuilder.CodeBlock classBlock = new ScriptBuilder.CodeBlock()
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
            ScriptBuilder.CodeBlock namespaceBlock = new ScriptBuilder.CodeBlock()
            {
                Header = $"namespace {scriptNamespace}",
                Content = new List<object> { classBlock },
            };
            ScriptBuilder.CodeBlock usingBlock = new ScriptBuilder.CodeBlock()
            {
                Content = new List<object> { "using Mu3Library.UI.MVP;", "" },
            };

            return ScriptBuilder.Build(spaces, new ScriptBuilder.CodeBlock[]
            {
                usingBlock,
                namespaceBlock
            });
        }

        private string GetArgumentsBody(string scriptNamespace, string className, int spaces, bool ignoreTypeOverlap)
        {
            string fullName = $"{scriptNamespace}.{className}";

            if (!ignoreTypeOverlap && IsTypeExist(fullName))
            {
                Debug.LogWarning($"Type already exist. type: {fullName}");

                return "";
            }

            ScriptBuilder.CodeBlock classBlock = new ScriptBuilder.CodeBlock()
            {
                Header = $"public class {className} : Arguments",
                Content = new List<object> { "" }
            };
            ScriptBuilder.CodeBlock namespaceBlock = new ScriptBuilder.CodeBlock()
            {
                Header = $"namespace {scriptNamespace}",
                Content = new List<object> { classBlock },
            };
            ScriptBuilder.CodeBlock usingBlock = new ScriptBuilder.CodeBlock()
            {
                Content = new List<object> { "using Mu3Library.UI.MVP;", "" },
            };

            return ScriptBuilder.Build(spaces, new ScriptBuilder.CodeBlock[]
            {
                usingBlock,
                namespaceBlock
            });
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

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
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
            string path = $"{folder}/{fileName}.cs";
            System.IO.File.WriteAllText(path, body);
        }
    }
}