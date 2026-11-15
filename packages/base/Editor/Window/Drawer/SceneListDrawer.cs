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
            }, 20, 20);
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

            DrawStruct(() =>
            {
                foreach (SceneAssetStruct sceneAssetStruct in sceneFolderStruct.SceneAssets)
                {
                    DrawSceneAssetStruct(sceneAssetStruct);
                }
            }, 20, 20);
        }

        private void DrawSceneAssetStruct(SceneAssetStruct sceneAssetStruct)
        {
            if (sceneAssetStruct.SceneAsset == null)
            {
                return;
            }

            Color prevContentColor = GUI.contentColor;

            GUILayout.Label($"{sceneAssetStruct.AssetPath}");

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Find", GUILayout.Width(60), GUILayout.Height(30)))
            {
                FileFinder.PingObject(sceneAssetStruct.SceneAsset);
            }

            int buildSceneIndex = System.Array.FindIndex(EditorBuildSettings.scenes,
                t => t.path == sceneAssetStruct.AssetPath);
            bool buildSettingsButton = buildSceneIndex >= 0 ?
                GUILayout.Button("Remove From Build Settings", GUILayout.Width(190), GUILayout.Height(30)) :
                GUILayout.Button("Add To Build Settings", GUILayout.Width(160), GUILayout.Height(30));
            if (buildSettingsButton)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.LogWarning($"Action unavailable while Editor is in Play Mode.");
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

            bool openSingleSceneButton = GUILayout.Button($"Open [{sceneAssetStruct.SceneAsset.name}]", GUILayout.Height(30));
            if (openSingleSceneButton)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.LogWarning($"Action unavailable while Editor is in Play Mode.");
                }
                else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    string scenePath = sceneAssetStruct.AssetPath;
                    EditorSceneManager.OpenScene(scenePath);
                }
            }

            bool isMainScene = EditorSceneManager.GetActiveScene().path == sceneAssetStruct.AssetPath;
            var currentScene = EditorSceneManager.GetSceneByPath(sceneAssetStruct.AssetPath);
            bool isOpenedAsAdditiveScene = currentScene.IsValid();

            GUI.contentColor = isMainScene ?
                Color.gray :
                isOpenedAsAdditiveScene ?
                    Color.red :
                    Color.green;

            bool openAdditiveSceneButton = isOpenedAsAdditiveScene ?
                GUILayout.Button($"Remove", GUILayout.Width(80), GUILayout.Height(30)) :
                GUILayout.Button($"Add", GUILayout.Width(60), GUILayout.Height(30));
            if (openAdditiveSceneButton)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Debug.LogWarning($"Action canceled. Editor is in Play Mode.");
                }
                else if (isMainScene)
                {
                    Debug.LogWarning($"Can't change scene state for '{sceneAssetStruct.SceneAsset.name}'. It's already the main scene.");
                }
                else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    if (isOpenedAsAdditiveScene)
                    {
                        EditorSceneManager.CloseScene(currentScene, true);
                    }
                    else
                    {
                        string scenePath = sceneAssetStruct.AssetPath;
                        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    }
                }
            }

            GUI.contentColor = prevContentColor;

            bool isPlayModeSceneScene = _playModeStartScene == sceneAssetStruct.SceneAsset;

            GUI.contentColor = isPlayModeSceneScene ? Color.green : prevContentColor;

            bool playModeSceneButton = isPlayModeSceneScene ?
                GUILayout.Button("Unset From Play Mode Start Scene", GUILayout.Width(220), GUILayout.Height(30)) :
                GUILayout.Button("Set To Play Mode Start Scene", GUILayout.Width(200), GUILayout.Height(30));
            if (playModeSceneButton)
            {
                _playModeStartScene = isPlayModeSceneScene ?
                    null :
                    sceneAssetStruct.SceneAsset;
            }

            GUI.contentColor = prevContentColor;

            GUILayout.EndHorizontal();
        }

        private void SyncSceneAssets()
        {
            var scenesCopy = _scenes.ToList();
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

                var oldData = scenesCopy.Where(t => t.Folder == folder).FirstOrDefault();

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

                SceneFolderStruct sceneFolderStruct = new SceneFolderStruct()
                {
                    Folder = folder,
                    FolderPath = path,
                    Foldout = oldData != null ? oldData.Foldout : true,
                    SceneAssets = sceneAssetStructs,
                };
                _scenes.Add(sceneFolderStruct);
            }
        }
    }
}
