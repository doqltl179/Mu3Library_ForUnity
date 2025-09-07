#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.Base.Attribute;
using Mu3Library.Base.Editor.Window;
using Mu3Library.Base.Editor.FileUtil;

namespace Mu3Library.Base.Sample.UtilWindow {
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
        public bool Foldout_ScreenCapture {
            get => foldout_screenCapture;
            set => foldout_screenCapture = value;
        }
        [Title("Screen Capture Properties")]
        [SerializeField] private bool foldout_screenCapture = true;
        #endregion

        #region Image to Base64
        public bool Foldout_ImageToBase64 {
            get => foldout_imageToBase64;
            set => foldout_imageToBase64 = value;
        }
        [Title("Image to Base64 Properties")]
        [SerializeField] private bool foldout_imageToBase64 = true;
        #endregion



        /// <summary>
        /// When called recompile.
        /// </summary>
        public override void Refresh() {
            if(sceneCheckDirectoryList != null) {
                for(int i = 0; i < sceneCheckDirectoryList.Count; i++) {
                    // 'NULL'이면 제거
                    if(sceneCheckDirectoryList[i] == null) {
                        sceneCheckDirectoryList.RemoveAt(i);
                        i--;
                        continue;
                    }

                    // 디렉토리가 존재하지 않으면 제거
                    string assetDirectory = sceneCheckDirectoryList[i].Directory;
                    string systemDirectory = FilePathConvertor.AssetPathToSystemPath(assetDirectory);
                    if(!System.IO.Directory.Exists(systemDirectory)) {
                        Debug.Log($"Directory not found. AssetPath: {assetDirectory}");
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
            string[] sceneFilePaths = FileFinder.GetAssetsPath(directory, "", typeString, "");
            if(sceneFilePaths.Length == 0) {
                Debug.LogWarning($"Scene Files not found. directory: {directory}");
            }

            scenePaths = sceneFilePaths;
        }
        #endregion
    }
}
#endif