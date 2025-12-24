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
        private SceneAsset _playModeStartScene
        {
            get => EditorSceneManager.playModeStartScene;
            set => EditorSceneManager.playModeStartScene = value;
        }

        private SerializedObject m_sceneFoldersObject;
        private SerializedObject _sceneFoldersObject
        {
            get
            {
                if (m_sceneFoldersObject == null)
                {
                    m_sceneFoldersObject = new SerializedObject(this);
                }

                return m_sceneFoldersObject;
            }
        }

        private SerializedProperty m_sceneFoldersProperties;
        private SerializedProperty _sceneFoldersProperties
        {
            get
            {
                if (m_sceneFoldersProperties == null)
                {
                    m_sceneFoldersProperties = _sceneFoldersObject.FindProperty(nameof(_sceneFolders));
                }

                return m_sceneFoldersProperties;
            }
        }



        private void OnEnable()
        {
            SyncSceneAssets();
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

        private void DrawSceneFolderList()
        {
            _sceneFoldersObject.Update();

            EditorGUILayout.PropertyField(_sceneFoldersProperties, true);

            if (!_sceneFoldersObject.ApplyModifiedProperties())
            {
                return;
            }

            bool folderPropertyChanged = false;

            HashSet<DefaultAsset> folders = new HashSet<DefaultAsset>();
            for (int i = 0; i < _sceneFolders.Count; i++)
            {
                DefaultAsset checkFolder = _sceneFolders[i];

                if (checkFolder == null)
                {
                    continue;
                }

                if (folders.Contains(checkFolder))
                {
                    _sceneFolders[i] = null;

                    folderPropertyChanged = true;
                }
                else
                {
                    folders.Add(checkFolder);
                }
            }

            if (folderPropertyChanged)
            {
                _sceneFoldersObject.ApplyModifiedProperties();
            }

            SyncSceneAssets();
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
                foreach (SceneFolderStruct sceneFolderStruct in _scenes)
                {
                    sceneFolderStruct.Foldout = true;
                }
            }

            if (GUILayout.Button("Fold All", GUILayout.Width(80), GUILayout.Height(40)))
            {
                foreach (SceneFolderStruct sceneFolderStruct in _scenes)
                {
                    sceneFolderStruct.Foldout = false;
                }
            }

            GUILayout.EndHorizontal();

            foreach (SceneFolderStruct sceneFolderStruct in _scenes)
            {
                DrawSceneFolderStruct(sceneFolderStruct);
            }
        }

        private void DrawSceneFolderStruct(SceneFolderStruct sceneFolderStruct)
        {
            DrawFoldoutHeader2(sceneFolderStruct.FolderPath, ref sceneFolderStruct.Foldout);

            if (!sceneFolderStruct.Foldout)
            {
                return;
            }

            foreach (SceneAssetStruct sceneAssetStruct in sceneFolderStruct.SceneAssets)
            {
                DrawSceneAssetStruct(sceneAssetStruct);
            }
        }

        private void DrawSceneAssetStruct(SceneAssetStruct sceneAssetStruct)
        {
            if (sceneAssetStruct.SceneAsset == null)
            {
                return;
            }

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Find", GUILayout.Width(60), GUILayout.Height(30)))
            {
                FileFinder.PingObject(sceneAssetStruct.SceneAsset);
            }

            int buildSceneIndex = System.Array.FindIndex(EditorBuildSettings.scenes,
                t => t.path == sceneAssetStruct.AssetPath);
            if (GUILayout.Button(
                buildSceneIndex >= 0 ?
                    "Remove In Build Settings" :
                    "Add To Build Settings",
                GUILayout.Width(160),
                GUILayout.Height(30)))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.LogWarning($"Can't edit editor during Playmode.");
                }
                else
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
            }

            if (GUILayout.Button($"Open [ {sceneAssetStruct.SceneAsset.name} ]", GUILayout.Height(30)))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.LogWarning($"Can't open editor during Playmode.");
                }
                else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    string scenePath = FileFinder.GetAssetPath(sceneAssetStruct.SceneAsset);
                    EditorSceneManager.OpenScene(scenePath);
                }
            }

            bool isSameScene = _playModeStartScene == sceneAssetStruct.SceneAsset;
            Color prevContentColor = GUI.contentColor;

            if(isSameScene)
            {
                GUI.contentColor = Color.green;
            }

            if (GUILayout.Button(
                isSameScene ?
                    "Unset From Play Mode Start Scene" :
                    "Set To Play Mode Start Scene",
                GUILayout.Width(220),
                GUILayout.Height(30)))
            {
                if (isSameScene)
                {
                    _playModeStartScene = null;
                }
                else
                {
                    _playModeStartScene = sceneAssetStruct.SceneAsset;
                }
            }

            GUI.contentColor = prevContentColor;

            GUILayout.EndHorizontal();
        }

        private void SyncSceneAssets()
        {
            _scenes.Clear();

            foreach (DefaultAsset folder in _sceneFolders)
            {
                if (folder == null)
                {
                    continue;
                }

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
    }
}