using System.Collections.Generic;
using System.Linq;
using Mu3Library.Editor.FileUtil;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class DependencyCheckerDrawer : Mu3WindowDrawer
    {
        public const string FileName = "DependencyChecker";
        private const string ItemName = "Dependency Checker";
        private const string MenuName = MenuRoot + "/" + ItemName;

        [SerializeField, HideInInspector] private List<Object> _objectList = new();
        private List<string> _dependencies = new();

        [SerializeField, HideInInspector] private bool _excludePackages = true;
        /// <summary>
        /// 재귀적으로 모든 하위 오브젝트를 탐색하기 위한 프로퍼티
        /// </summary>
        [SerializeField, HideInInspector] private bool _recursive = true;

        [SerializeField, HideInInspector] private bool _searchWithExtensions = false;
        [SerializeField, HideInInspector] private List<string> _searchExtensions = new();

        #region Serialized Options

        private SerializedObject m_serializedObject;
        private SerializedObject _serializedObject
        {
            get
            {
                if (m_serializedObject == null)
                {
                    m_serializedObject = new SerializedObject(this);
                }

                return m_serializedObject;
            }
        }

        private SerializedProperty m_objectListProperties;
        private SerializedProperty _objectListProperties
        {
            get
            {
                if (m_objectListProperties == null)
                {
                    m_objectListProperties = _serializedObject.FindProperty(nameof(_objectList));
                }

                return m_objectListProperties;
            }
        }

        private SerializedProperty m_searchExtensionsProperties;
        private SerializedProperty _searchExtensionsProperties
        {
            get
            {
                if (m_searchExtensionsProperties == null)
                {
                    m_searchExtensionsProperties = _serializedObject.FindProperty(nameof(_searchExtensions));
                }

                return m_searchExtensionsProperties;
            }
        }

        #endregion



        public override void OnGUIHeader()
        {
            DrawFoldoutHeader1(ItemName, ref _foldout);
        }

        public override void OnGUIBody()
        {
            DrawStruct(() =>
            {
                DrawObjectListField();

                GUILayout.Space(8);

                DrawSearchOptions();

                DrawButtons();

                if (_dependencies.Count == 0)
                {
                    return;
                }

                GUILayout.Space(20);

                DrawDependencies();
            }, 20, 20, 0, 0);
        }

        private void DrawDependencies()
        {
            DrawHeader2("[ Dependencies ]");

            foreach (string assetPath in _dependencies)
            {
                DrawDependency(assetPath);
            }
        }
        
        private void DrawDependency(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Select", GUILayout.Width(60), GUILayout.Height(20)))
            {
                Object asset = FileFinder.LoadAssetAtPath(assetPath);
                if (asset != null)
                {
                    FileFinder.PingObject(asset, true);
                }
            }
            if (GUILayout.Button("Ping", GUILayout.Width(60), GUILayout.Height(20)))
            {
                Object asset = FileFinder.LoadAssetAtPath(assetPath);
                if (asset != null)
                {
                    FileFinder.PingObject(asset);
                }
            }

            GUILayout.Label(assetPath);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawButtons()
        {
            if (GUILayout.Button("Search", GUILayout.Height(30)))
            {
                IEnumerable<string> dependencies = null;

                if (_objectList.Count == 1)
                {
                    dependencies = FileFinder.GetDependencies(_objectList[0], _recursive);
                }
                else if (_objectList.Count > 1)
                {
                    dependencies = FileFinder.GetDependencies(_objectList, _recursive);
                }

                if(dependencies == null)
                {
                    return;
                }

                if (_excludePackages)
                {
                    dependencies = dependencies
                        .Where(t => !t.StartsWith("Packages/"));
                }
                
                if(_searchWithExtensions)
                {
                    dependencies = dependencies
                        .Where(t =>
                        {
                            string extension = System.IO.Path.GetExtension(t);
                            if (string.IsNullOrEmpty(extension) || extension.Length <= 1)
                            {
                                return false;
                            }

                            extension = extension[1..extension.Length];
                            return _searchExtensions.Contains(extension);
                        });
                }

                _dependencies = dependencies.ToList();
                _dependencies.Sort();
            }
        }
        
        private void DrawSearchOptions()
        {
            _excludePackages = EditorGUILayout.Toggle("Exclude Packages", _excludePackages);
            _recursive = EditorGUILayout.Toggle("Recursive", _recursive);

            _searchWithExtensions = EditorGUILayout.Toggle("With Extensions", _searchWithExtensions);

            if(_searchWithExtensions)
            {
                _serializedObject.Update();

                EditorGUILayout.PropertyField(_searchExtensionsProperties);

                if (!_serializedObject.ApplyModifiedProperties())
                {
                    return;
                }


            }
        }
        
        private void DrawObjectListField()
        {
            _serializedObject.Update();

            EditorGUILayout.PropertyField(_objectListProperties);

            if (!_serializedObject.ApplyModifiedProperties())
            {
                return;
            }

            
        }
    }
}