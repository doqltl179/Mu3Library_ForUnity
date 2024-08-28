using Mu3Library.Utility.CustomClass;
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
        public SerializableDictionary<SceneControlStruct> SceneStructs {
            get => sceneStructs;
        }
        [Title("Move Scene Properties")]
        [SerializeField, ReadOnly] private SerializableDictionary<SceneControlStruct> sceneStructs;
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

        public string CaptureSaveDirectory {
            get => captureSaveDirectory;
            set => captureSaveDirectory = value;
        }
        [SerializeField, ReadOnly] private string captureSaveDirectory = "";
        public string CaptureSaveFileName {
            get => captureSaveFileName;
            set => captureSaveFileName = value;
        }
        [SerializeField, ReadOnly] private string captureSaveFileName = "ScreenShot";
        #endregion



        public void Refresh() {
            sceneStructs = new SerializableDictionary<SceneControlStruct>();
            string[] scenes = AssetDatabase.FindAssets("t:Scene").Select(AssetDatabase.GUIDToAssetPath).ToArray();
            if(scenes != null && scenes.Length > 0) {
                foreach(string s in scenes) {
                    string directory = Path.GetDirectoryName(s);
                    if(!sceneStructs.ContainsKey(directory)) {
                        SceneControlStruct st = new SceneControlStruct() {
                            ShowInInspector = true,
                            Paths = new List<string>(),
                        };

                        sceneStructs.Add(directory, st);
                    }

                    ((SceneControlStruct)sceneStructs[directory]).Paths.Add(s);
                }
            }
        }
    }

    [System.Serializable]
    public class SceneControlStruct {
        public bool ShowInInspector;
        public List<string> Paths;
    }
}