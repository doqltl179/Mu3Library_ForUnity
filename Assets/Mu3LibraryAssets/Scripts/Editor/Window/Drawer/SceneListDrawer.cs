using System.Collections.Generic;
using System.Linq;
using Mu3Library.Editor.FileUtil;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class SceneListDrawer : Mu3WindowDrawer
    {
        public const string FileName = "SceneList";
        private const string ItemName = "Scene List";
        private const string MenuName = MenuRoot + "/" + ItemName;

        [SerializeField, HideInInspector] private List<DefaultAsset> _sceneFolders = new();

        [System.Serializable]
        private class SceneAssetStruct
        {
            [HideInInspector] public SceneAsset SceneAsset;
            [HideInInspector] public string AssetPath;
        }
        [System.Serializable]
        private class SceneFolderStruct
        {
            [HideInInspector] public DefaultAsset Folder;
            [HideInInspector] public string FolderPath;
            [HideInInspector] public bool Foldout;
            [HideInInspector] public List<SceneAssetStruct> SceneAssets;
        }
        // Dictionary를 SerializeField로 사용할 수 없어서 List를 사용함
        [SerializeField, HideInInspector] private List<SceneFolderStruct> _scenes = new();

        private SerializedObject _serializedObject = null;
        private SerializedProperty _sceneFoldersProperties = null;



        private void OnValidate()
        {
            if(_serializedObject != null && _sceneFoldersProperties != null)
            {
                RefreshSceneProperties();
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
                DrawSceneFolderList();

                GUILayout.Space(20);

                DrawSceneList();
            }, 20, 20, 0, 0);
        }

        private void DrawSceneList()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Sync", GUILayout.Width(50), GUILayout.Height(40)))
            {
                SyncSceneAssets();
            }

            if (GUILayout.Button("Foldout All", GUILayout.Width(80), GUILayout.Height(40)))
            {
                foreach(SceneFolderStruct sceneFolderStruct in _scenes)
                {
                    sceneFolderStruct.Foldout = true;
                }
            }

            if (GUILayout.Button("Fold All", GUILayout.Width(80), GUILayout.Height(40)))
            {
                foreach(SceneFolderStruct sceneFolderStruct in _scenes)
                {
                    sceneFolderStruct.Foldout = false;
                }
            }

            GUILayout.EndHorizontal();

            foreach (SceneFolderStruct sceneFolderStruct in _scenes)
            {
                DrawFoldoutHeader2(sceneFolderStruct.FolderPath, ref sceneFolderStruct.Foldout);

                if (!sceneFolderStruct.Foldout)
                {
                    continue;
                }

                foreach (SceneAssetStruct sceneAssetStruct in sceneFolderStruct.SceneAssets)
                {
                    if(sceneAssetStruct.SceneAsset == null)
                    {
                        continue;
                    }

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("Find", GUILayout.Width(60), GUILayout.Height(30)))
                    {
                        FileFinder.PingObject(sceneAssetStruct.SceneAsset);
                    }

                    int buildSceneIndex = System.Array.FindIndex(EditorBuildSettings.scenes,
                        t => t.path == sceneAssetStruct.AssetPath);
                    if (GUILayout.Button(buildSceneIndex >= 0 ?
                        "Remove In Build Settings" :
                        "Add To Build Settings",
                        GUILayout.Width(160), GUILayout.Height(30)))
                    {
                        List<EditorBuildSettingsScene> copyScenes = EditorBuildSettings.scenes.ToList();

                        if (buildSceneIndex >= 0)
                        {
                            copyScenes.RemoveAt(buildSceneIndex);
                        }
                        else
                        {
                            copyScenes.Add(new EditorBuildSettingsScene(sceneAssetStruct.AssetPath, true));
                        }

                        EditorBuildSettings.scenes = copyScenes.ToArray();
                    }

                    if (GUILayout.Button($"Open [ {sceneAssetStruct.SceneAsset.name} ]", GUILayout.Height(30)))
                    {
                        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        {
                            string scenePath = FileFinder.GetAssetPath(sceneAssetStruct.SceneAsset);
                            EditorSceneManager.OpenScene(scenePath);
                        }
                    }

                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawSceneFolderList()
        {
            if (_serializedObject == null)
            {
                _serializedObject = new SerializedObject(this);
            }
            if (_sceneFoldersProperties == null && _serializedObject != null)
            {
                _sceneFoldersProperties = _serializedObject.FindProperty(nameof(_sceneFolders));
            }

            if (_serializedObject != null && _sceneFoldersProperties != null)
            {
                EditorGUILayout.PropertyField(_sceneFoldersProperties, true);
                _serializedObject.ApplyModifiedProperties();

                RefreshSceneProperties();
            }

            for (int i = 0; i < _sceneFolders.Count; i++)
            {
                DefaultAsset asset = _sceneFolders[i];

                if (asset == null)
                {
                    _sceneFolders.RemoveAt(i);
                    i--;
                    continue;
                }

                if (!FileFinder.IsValidFolder(asset))
                {
                    Debug.LogWarning($"This asset is not a folder. name: {asset.name}");

                    _sceneFolders.RemoveAt(i);
                    i--;
                }
            }
        }

        private void RefreshSceneProperties()
        {
            bool initiate = RemoveUnusableFolders();

            if (!initiate)
            {
                initiate = _sceneFolders.Count != _sceneFoldersProperties.arraySize ||
                    _sceneFolders.Count != _scenes.Count;
            }
            
            if (!initiate)
            {
                for(int i = 0; i < _sceneFolders.Count; i++)
                {
                    if(_sceneFolders[i] != _scenes[i].Folder)
                    {
                        initiate = true;
                        break;
                    }
                }
            }

            if(!initiate)
            {
                return;
            }

            _serializedObject = new SerializedObject(this);
            _sceneFoldersProperties = _serializedObject.FindProperty(nameof(_sceneFolders));

            SyncSceneAssets();
        }

        private void SyncSceneAssets()
        {
            _scenes.Clear();

            foreach (DefaultAsset folder in _sceneFolders)
            {
                string path = FileFinder.GetAssetPath(folder);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                List<SceneAsset> sceneAssets = FileFinder.LoadAllAssetsAtPath<SceneAsset>(path);
                List<SceneAssetStruct> sceneAssetStructs = sceneAssets.Select(t =>
                {
                    string sceneAssetPath = FileFinder.GetAssetPath(t);
                    return new SceneAssetStruct()
                    {
                        SceneAsset = t,
                        AssetPath = sceneAssetPath,
                    };
                }).ToList();

                SceneFolderStruct sceneAssetStruct = new SceneFolderStruct()
                {
                    Folder = folder,
                    FolderPath = path,
                    Foldout = true,
                    SceneAssets = sceneAssetStructs,
                };
                _scenes.Add(sceneAssetStruct);
            }
        }

        private bool RemoveUnusableFolders()
        {
            bool isChanged = false;

            for (int i = 0; i < _sceneFolders.Count - 1; i++)
            {
                DefaultAsset currentFolder = _sceneFolders[i];

                // Remove empty
                if (currentFolder == null)
                {
                    _sceneFolders.RemoveAt(i);
                    i--;
                    isChanged = true;
                    continue;
                }

                // Remove overlap
                for (int j = i + 1; j < _sceneFolders.Count; j++)
                {
                    DefaultAsset compareFolder = _sceneFolders[j];
                    if (currentFolder == compareFolder)
                    {
                        string folderPath = FileFinder.GetAssetPath(compareFolder);
                        Debug.LogWarning($"Folder overlapped. path: {folderPath}");

                        FileFinder.PingObject(compareFolder);
                        _sceneFolders.RemoveAt(j);
                        j--;
                        isChanged = true;
                    }
                }
            }

            return isChanged;
        }
    }
}