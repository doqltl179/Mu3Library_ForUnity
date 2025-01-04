#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.Attribute;
using Mu3Library.Editor.Window;
using Mu3Library.Editor.FileUtil;

namespace Mu3Library.Demo.UtilWindow {
    /// <summary>
    /// <br/> 해당 'ScriptableObject'는 에디터의 데이터를 저장하기 위해 사용한다.
    /// <br/> 원래는 'EditorPrefs'를 사용하려 했으나, 관리가 너무 불편함.
    /// </summary>
    [System.Serializable]
    [CreateAssetMenu(fileName = "UtilWindowPropertyObject", menuName = "Mu3 Library/Window/Util Window Property Object", order = 1)]
    public class UtilWindowProperty : Mu3WindowProperty {

        #region Scene List Properties
        public bool Foldout_SceneList {
            get => foldout_sceneList;
            set => foldout_sceneList = value;
        }
        [Title("Scene List Properties")]
        [SerializeField] private bool foldout_sceneList = true;

        public List<SceneCheckDirectoryStruct> SceneCheckDirectoryList {
            get => sceneCheckDirectoryList;
        }
        [SerializeField] private List<SceneCheckDirectoryStruct> sceneCheckDirectoryList = new List<SceneCheckDirectoryStruct>();
        #endregion

        #region Screen Capture Properties
        public bool CaptureToCustomSize {
            get => captureToCustomSize;
            set => captureToCustomSize = value;
        }
        [Title("Screen Capture Properties")]
        [SerializeField] private bool captureToCustomSize = false;
        public Vector2Int CaptureSize {
            get => captureSize;
            set => captureSize = value;
        }
        [SerializeField] private Vector2Int captureSize = new Vector2Int(1920, 1080);

        public bool ChangeCaptureColor {
            get => changeCaptureColor;
            set => changeCaptureColor = value;
        }
        [SerializeField] private bool changeCaptureColor;
        public Color TargetColor {
            get => targetColor;
            set => targetColor = value;
        }
        [SerializeField] private Color targetColor = Color.black;
        public Color ChangeColor {
            get => changeColor;
            set => changeColor = value;
        }
        [SerializeField] private Color changeColor = Color.white;
        public float ColorChangeStrength {
            get => colorChangeStrength;
            set => colorChangeStrength = value;
        }
        [SerializeField] private float colorChangeStrength = 1.0f;

        //public string CaptureSaveDirectory {
        //    get => captureSaveDirectory;
        //    set => captureSaveDirectory = value;
        //}
        //[SerializeField] private string captureSaveDirectory = "";
        //public string CaptureSaveFileName {
        //    get => captureSaveFileName;
        //    set => captureSaveFileName = value;
        //}
        //[SerializeField] private string captureSaveFileName = "ScreenShot";
        #endregion



        /// <summary>
        /// When called recompile.
        /// </summary>
        public override void Refresh() {
            if(sceneCheckDirectoryList != null) {
                for(int i = 0; i < sceneCheckDirectoryList.Count; i++) {
                    if(sceneCheckDirectoryList[i] == null) {
                        sceneCheckDirectoryList.RemoveAt(i);
                        i--;
                        continue;
                    }

                    sceneCheckDirectoryList[i].RefreshScenePaths();
                }
            }
        }

        #region Utility
        /// <summary>
        /// 
        /// </summary>
        /// <param name="directory"> Assets 폴더를 기준으로 한 상대 경로 </param>
        public void AddSceneCheckDirectory(string directory) {
            if(sceneCheckDirectoryList.Any(t => t.Directory == directory)) {
                Debug.LogWarning($"Directory already exist in check list. directory: {directory}");

                return;
            }

            sceneCheckDirectoryList.Add(new SceneCheckDirectoryStruct(directory));
        }
        #endregion
    }

    [System.Serializable]
    public class SceneCheckDirectoryStruct {
        /// <summary>
        /// Assets 폴더를 기준으로 한 상대 경로
        /// </summary>
        public string Directory {
            get => directory;
        }
        [SerializeField] private string directory;

        /// <summary>
        /// Assets 폴더를 기준으로 한 상대 경로
        /// </summary>
        public string[] ScenePaths {
            get => scenePaths;
        }
        [SerializeField] private string[] scenePaths;

        public bool Foldout {
            get => foldout;
            set => foldout = value;
        }
        [SerializeField] private bool foldout = true;



        public SceneCheckDirectoryStruct(string rootDirectory) {
            directory = rootDirectory;

            RefreshScenePaths();
        }

        #region Utility
        public void RefreshScenePaths() {
            string typeString = "Scene";
            string[] sceneFilePaths = FileFinder.GetAssetPaths(directory, "", typeString, "");
            if(sceneFilePaths.Length == 0) {
                Debug.LogWarning($"Scene Files not found. directory: {directory}");
            }

            scenePaths = sceneFilePaths;
        }
        #endregion
    }
}
#endif