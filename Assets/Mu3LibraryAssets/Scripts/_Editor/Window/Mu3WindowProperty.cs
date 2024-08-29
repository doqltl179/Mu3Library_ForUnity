using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

using static Mu3Library.Scene.SceneLoader;

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

        public SceneType PlayLoadScene {
            get => playLoadScene;
            set => playLoadScene = value;
        }
        [SerializeField, ReadOnly] private SceneType playLoadScene = SceneType.Splash;
        #endregion

        #region Move Scene Properties
        public List<SceneControlStruct> SceneStructs {
            get => sceneStructs;
        }
        [Title("Move Scene Properties")]
        [SerializeField, ReadOnly] private List<SceneControlStruct> sceneStructs;
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



        private void OnEnable() {
            Refresh();
        }

        public void Refresh() {
            RefreshSceneStructs();
        }

        private void RefreshSceneStructs() {
            List<SceneControlStruct>  newStructs = new List<SceneControlStruct>();

            string[] scenes = AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath).ToArray();
            if(scenes != null && scenes.Length > 0) {
                foreach(string path in scenes) {
                    string directory = Path.GetDirectoryName(path);

                    SceneControlStruct currentST = newStructs.Where(t => t.Key == directory).FirstOrDefault();
                    if(currentST == null) {
                        SceneControlStruct st = new SceneControlStruct() {
                            Key = directory,
                            ShowInInspector = true,
                            Properties = new List<SceneProperty>(),
                        };

                        newStructs.Add(st);
                        currentST = st;
                    }

                    SceneProperty newProperty = new SceneProperty();
                    newProperty.IncludeInBuild = false;
                    newProperty.GUID = AssetDatabase.AssetPathToGUID(path);
                    newProperty.Path = path;
                    newProperty.Name = Path.GetFileNameWithoutExtension(path);

                    currentST.Properties.Add(newProperty);
                }
            }

            if(sceneStructs != null) {
                for(int i = 0; i < newStructs.Count; i++) {
                    SceneControlStruct scs = newStructs[i];
                    SceneControlStruct old = sceneStructs.Where(t => t.Key == scs.Key).FirstOrDefault();
                    if(old != null) {
                        scs.ShowInInspector = old.ShowInInspector;

                        for(int j = 0; j < scs.Properties.Count; j++) {
                            SceneProperty sp = scs.Properties[j];
                            SceneProperty op = old.Properties.Where(t => t.GUID == sp.GUID).FirstOrDefault();
                            if(op != null) {
                                sp.IncludeInBuild = op.IncludeInBuild;
                            }
                        }
                    }
                }
            }

            sceneStructs = newStructs;
        }

        #region Utility
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
    }



    [System.Serializable]
    public class SceneControlStruct {
        public string Key;
        public bool ShowInInspector;

        public List<SceneProperty> Properties;



        #region Utility
        public SceneProperty GetSceneProperty(string guid) {
            return Properties == null ? null : Properties.Where(t => t.GUID == guid).FirstOrDefault();
        }
        #endregion
    }

    [System.Serializable]
    public class SceneProperty {
        public bool IncludeInBuild;

        public string GUID;
        public string Path;
        public string Name;
    }
}