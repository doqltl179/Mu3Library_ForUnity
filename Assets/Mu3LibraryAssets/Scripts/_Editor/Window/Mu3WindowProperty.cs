using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Mu3Library.Editor.Window {
    /// <summary>
    /// <br/> 해당 'ScriptableObject'는 에디터의 데이터를 저장하기 위해 사용한다.
    /// <br/> 원래는 'EditorPrefs'를 사용하려 했으나, 관리가 너무 불편함.
    /// </summary>
    [System.Serializable]
    [CreateAssetMenu(fileName = "Mu3WindowPropertyObject", menuName = "Mu3 Library/Window/Mu3 Window Property Object", order = 1)]
    public class Mu3WindowProperty : ScriptableObject {
        #region Play Load Scene Properties
        public bool UsePlayLoadScene {
            get => usePlayLoadScene;
            set => usePlayLoadScene = value;
        }
        [Title("Play Load Scene")]
        [SerializeField, ReadOnly] private bool usePlayLoadScene = false;

        public string PlayLoadScene {
            get => playLoadScene;
            set => playLoadScene = value;
        }
        [SerializeField, ReadOnly] private string playLoadScene = "";

        public int PlayLoadSceneIndex {
            get => playLoadSceneIndex;
            set {
                playLoadScene = (sceneInBuildNameList == null || sceneInBuildNameList.Length == 0 || value >= sceneInBuildNameList.Length) ? "" : sceneInBuildNameList[value];

                playLoadSceneIndex = value;
            }
        }
        [SerializeField, ReadOnly] private int playLoadSceneIndex = 0;

        public string[] SceneInBuildNameList {
            get => sceneInBuildNameList;
        }
        private string[] sceneInBuildNameList;
        #endregion

        #region Move Scene Properties
        public int SceneStructCount {
            get => sceneStructs.Count;
        }
        public IEnumerable<SceneControlStruct> SceneStructs {
            get => sceneStructs;
        }
        [Title("Move Scene Properties")]
        [SerializeField, ReadOnly] private List<SceneControlStruct> sceneStructs;

        public ReorderableList SceneInBuildReorderableList {
            get => sceneInBuildReorderableList;
        }
        [SerializeField, ReadOnly] private ReorderableList sceneInBuildReorderableList;
        #endregion

        #region Screen Capture Properties
        public bool CaptureToCustomSize {
            get => captureToCustomSize;
            set => captureToCustomSize = value;
        }
        [Title("Screen Capture Properties")]
        [SerializeField, ReadOnly] private bool captureToCustomSize = false;
        public Vector2Int CaptureSize {
            get => captureSize;
            set => captureSize = value;
        }
        [SerializeField, ReadOnly] private Vector2Int captureSize = new Vector2Int(1920, 1080);

        public bool ChangeCaptureColor {
            get => changeCaptureColor;
            set => changeCaptureColor = value;
        }
        [SerializeField, ReadOnly] private bool changeCaptureColor;
        public Color TargetColor {
            get => targetColor;
            set => targetColor = value;
        }
        [SerializeField, ReadOnly] private Color targetColor = Color.black;
        public Color ChangeColor {
            get => changeColor;
            set => changeColor = value;
        }
        [SerializeField, ReadOnly] private Color changeColor = Color.white;
        public float ColorChangeStrength {
            get => colorChangeStrength;
            set => colorChangeStrength = value;
        }
        [SerializeField, ReadOnly] private float colorChangeStrength = 1.0f;

        //public string CaptureSaveDirectory {
        //    get => captureSaveDirectory;
        //    set => captureSaveDirectory = value;
        //}
        //[SerializeField, ReadOnly] private string captureSaveDirectory = "";
        //public string CaptureSaveFileName {
        //    get => captureSaveFileName;
        //    set => captureSaveFileName = value;
        //}
        //[SerializeField, ReadOnly] private string captureSaveFileName = "ScreenShot";
        #endregion



        /// <summary>
        /// When called recompile.
        /// </summary>
        private void OnEnable() {
            Refresh();
        }

        public void Refresh() {
            RefreshSceneStructs();
            RefreshBuildScenes();
        }

        private void RefreshBuildScenes() {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            List<SceneProperty> sceneInBuildControlList = new List<SceneProperty>();
            for(int i = 0; i < scenes.Count; i++) {
                SceneProperty s = GetSceneProperty(scenes[i].guid.ToString());

                if(s == null) {
                    Debug.LogWarning($"Build Scene not found in 'currentWindowProperty'. path: {scenes[i].path}, guid: {scenes[i].guid}");
                }
                else {
                    sceneInBuildControlList.Add(s);
                }
            }

            sceneInBuildReorderableList = new ReorderableList(sceneInBuildControlList, typeof(SceneProperty), true, true, false, false);
            sceneInBuildReorderableList.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, "Scenes In Build");
            };
            sceneInBuildReorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                if(index >= sceneInBuildControlList.Count) {
                    Debug.LogWarning($"Index out of range. listCount: {sceneInBuildControlList.Count}, index: {index}");

                    return;
                }

                var item = sceneInBuildControlList[index];

                EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width - 100, EditorGUIUtility.singleLineHeight), item.Name);
                if(GUI.Button(new Rect(rect.x + rect.width - 90, rect.y, 80, EditorGUIUtility.singleLineHeight), "Remove")) {
                    RemoveBuildScene(index);

                    RefreshBuildSceneList();
                }
            };
            sceneInBuildReorderableList.onReorderCallback = (ReorderableList list) => {
                RefreshBuildSceneList();

                PlayLoadSceneIndex = (sceneInBuildNameList == null || string.IsNullOrEmpty(playLoadScene)) ? 0 : System.Array.FindIndex(sceneInBuildNameList, t => t == playLoadScene);
            };
        }

        private void RefreshSceneStructs() {
            List<SceneControlStruct>  newStructs = new List<SceneControlStruct>();

            UtilFuncForEditor.ResetAssetsFindOptions();
            UtilFuncForEditor.TypeString = "Scene";
            string[] scenes = UtilFuncForEditor.FindAssetsPath();

            if(scenes != null && scenes.Length > 0) {
                foreach(string path in scenes) {
                    string directory = Path.GetDirectoryName(path);

                    SceneControlStruct currentST = newStructs.Where(t => t.Key == directory).FirstOrDefault();
                    if(currentST == null) {
                        SceneControlStruct st = new SceneControlStruct();
                        st.ChangeKey(directory);

                        newStructs.Add(st);
                        currentST = st;
                    }

                    SceneProperty newProperty = new SceneProperty();
                    newProperty.ChangeGUID(AssetDatabase.AssetPathToGUID(path));
                    newProperty.ChangePath(path);
                    newProperty.ChangeName(Path.GetFileNameWithoutExtension(path));

                    currentST.AddSceneProperty(newProperty);
                }
            }

            if(sceneStructs != null) {
                for(int i = 0; i < newStructs.Count; i++) {
                    SceneControlStruct scs = newStructs[i];
                    SceneControlStruct old = sceneStructs.Where(t => t.Key == scs.Key).FirstOrDefault();
                    if(old != null) {
                        scs.ChangeShowInInspector(old.ShowInInspector);

                        //for(int j = 0; j < scs.Properties.Count; j++) {
                        //    SceneProperty sp = scs.Properties[j];
                        //    SceneProperty op = old.Properties.Where(t => t.GUID == sp.GUID).FirstOrDefault();
                        //    if(op != null) {

                        //    }
                        //}
                    }
                }
            }

            sceneStructs = newStructs;
        }

        #region Utility
        public void AddAllScenesInBuild() {
            if(sceneStructs == null) return;

            for(int i = 0; i < sceneStructs.Count; i++) {
                AddBuildScenes(sceneStructs[i].Properties);
            }
        }

        public void AddBuildScenes(IEnumerable<SceneProperty> properties) {
            foreach(SceneProperty property in properties) { 
                AddBuildScene(property);
            }
        }

        public void AddBuildScene(SceneProperty property) {
            int changeSceneIndex = (sceneInBuildReorderableList.list as List<SceneProperty>).FindIndex(t => t.GUID == property.GUID);
            if(changeSceneIndex >= 0) {
                Debug.LogWarning($"Scene already included in build. {property.Name}");
            }
            else {
                sceneInBuildReorderableList.list.Add(property);
            }
        }

        public void RemoveAllScenesInBuild() {
            sceneInBuildReorderableList.list.Clear();
        }

        public void RemoveBuildScenes(IEnumerable<SceneProperty> properties) {
            foreach(SceneProperty property in properties) {
                RemoveBuildScene(property);
            }
        }

        public void RemoveBuildScene(SceneProperty property) {
            int changeSceneIndex = (sceneInBuildReorderableList.list as List<SceneProperty>).FindIndex(t => t.GUID == property.GUID);
            if(changeSceneIndex >= 0) {
                sceneInBuildReorderableList.list.RemoveAt(changeSceneIndex);
            }
            else {
                Debug.LogWarning($"Scene not found in build list. name: {property.Name}, path: {property.Path}, guid: {property.GUID}");
            }
        }

        public void RemoveBuildScene(int index) {
            if(index < sceneInBuildReorderableList.count) {
                sceneInBuildReorderableList.list.RemoveAt(index);
            }
            else {
                Debug.LogWarning($"Index out of range. index: {index}");
            }
        }

        public void RefreshBuildSceneList() {
            RefreshBuildSceneList(sceneInBuildReorderableList);

            sceneInBuildNameList = ReorderableListToGenericList<SceneProperty>(sceneInBuildReorderableList).Select(t => t.Name).ToArray();
        }

        public bool IsExistInBuildScenes(SceneProperty property) {
            if(sceneInBuildReorderableList == null || sceneInBuildReorderableList.count == 0) return false;

            return (sceneInBuildReorderableList.list as List<SceneProperty>).Any(t => t.GUID == property.GUID);
        }

        public SceneProperty GetSceneProperty(string guid) {
            if(sceneStructs == null) return null;

            SceneProperty result = null;
            for(int i = 0; i < sceneStructs.Count; i++) {
                result = sceneStructs[i].GetSceneProperty(guid);

                if(result != null) {
                    break;
                }
            }

            return result;
        }
        #endregion

        private void RefreshBuildSceneList(ReorderableList list) {
            List<SceneProperty> properties = ReorderableListToGenericList<SceneProperty>(list);

            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[properties.Count];
            for(int i = 0; i < scenes.Length; i++) {
                scenes[i] = new EditorBuildSettingsScene(properties[i].Path, true);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private List<T> ReorderableListToGenericList<T>(ReorderableList list) {
            List<T> result = new List<T>();

            if(list == null) {
                Debug.LogWarning("ReorderableList is NULL.");
            }
            else if(list.GetType().IsGenericType && list.GetType().GenericTypeArguments[0] != typeof(T)) {
                Debug.LogWarning($"Type is different. GenericType: {typeof(T)}, ReorderableType: {list.GetType().GenericTypeArguments[0]}");
            }
            else {
                result = list.list as List<T>;
            }

            return result;
        }
    }



    [System.Serializable]
    public class SceneControlStruct {
        public string Key {
            get => key;
        }
        [SerializeField, ReadOnly] private string key = "";
        public bool ShowInInspector {
            get => showInInspector;
        }
        [SerializeField, ReadOnly] private bool showInInspector = true;

        public IEnumerable<SceneProperty> Properties {
            get => properties;
        }
        [SerializeField, ReadOnly] private List<SceneProperty> properties = new List<SceneProperty>();



        public SceneControlStruct() {
            key = "";
            showInInspector = true;
            properties = new List<SceneProperty>();
        }

        #region Utility
        public void ChangeKey(string value) {
            if(value != key) {
                Debug.Log($"'key' changed. value: {value}");

                key = value;
            }
        }

        public void ChangeShowInInspector(bool value) {
            if(value != showInInspector) {
                Debug.Log($"'showInInspector' changed. value: {value}");

                showInInspector = value;
            }
        }

        public void AddSceneProperty(SceneProperty property) {
            properties.Add(property);
        }

        public void RemoveSceneProperty(SceneProperty property) {
            properties.Remove(property);
        }

        public SceneProperty GetSceneProperty(string guid) {
            return properties == null ? null : properties.Where(t => t.GUID == guid).FirstOrDefault();
        }
        #endregion
    }

    [System.Serializable]
    public class SceneProperty {
        public string Path {
            get => path;
        }
        [SerializeField, ReadOnly] private string path = "";
        public string GUID {
            get => guid;
        }
        [SerializeField, ReadOnly] private string guid = "";
        public string Name {
            get => name;
        }
        [SerializeField, ReadOnly] private string name = "";



        public SceneProperty() {
            path = "";
            guid = "";
            name = "";
        }

        #region Utility
        public void ChangePath(string value) {
            if(path != value) {
                Debug.Log($"'path' changed. value: {value}");

                path = value;
            }
        }

        public void ChangeGUID(string value) {
            if(guid != value) {
                Debug.Log($"'guid' changed. value: {value}");

                guid = value;
            }
        }

        public void ChangeName(string value) {
            if(name != value) {
                Debug.Log($"'name' changed. value: {value}");

                name = value;
            }
        }
        #endregion
    }
}