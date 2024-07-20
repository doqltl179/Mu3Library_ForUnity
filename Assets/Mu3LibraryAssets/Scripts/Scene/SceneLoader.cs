using Mu3Library.UI;
using Mu3Library.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mu3Library.Scene {
    public class SceneLoader : GenericSingleton<SceneLoader> {
        public SceneController CurrentSceneController { get; private set; }

        public SceneType CurrentSceneType {
            get => currentSceneType;
            private set {
                if(currentSceneType != value) {
                    OnSceneChanged?.Invoke(currentSceneType, value);

                    currentSceneType = value;
                }
            }
        }
        private SceneType currentSceneType = SceneType.None;
        public Action<SceneType, SceneType> OnSceneChanged;

        private List<SceneType> addedSceneTypeList = new List<SceneType>();

        public object[] Param { get; private set; } = null;
        public bool IsLoading => loadSceneCoroutine != null;
        public float ProgressNum { get; private set; } = -1;

        private IEnumerator loadSceneCoroutine = null;



        #region Utility
        public void LoadScene(SceneType scene, object[] param = null) {
            if(loadSceneCoroutine == null) {
                Param = param;

                loadSceneCoroutine = LoadSceneCoroutine(scene);
                StartCoroutine(loadSceneCoroutine);
            }
        }

        private IEnumerator LoadSceneCoroutine(SceneType scene) {
            // CurrentSceneController unload action
            if(CurrentSceneController != null) CurrentSceneController.OnSceneUnload();
            CurrentSceneController = null;

            // Init properties
            ProgressNum = 0.0f;

            // Wait one frame
            yield return null;

            // Declare properties
            const float loadingPanelFadeTime = 0.5f;
            WaitForSeconds wait = new WaitForSeconds(loadingPanelFadeTime);

            // Activate loading panel
            LoadingPanel.Instance.SetActive(true, loadingPanelFadeTime);
            LoadingPanel.Instance.UpdateProgress();
            yield return wait;

            // Load scene ==> not activate
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(scene.ToString());
            asyncLoad.allowSceneActivation = false;
            while(asyncLoad.progress < 0.9f) {
                ProgressNum = asyncLoad.progress;

                yield return null;
            }

            // Load scene ==> activate
            asyncLoad.allowSceneActivation = true;
            while(!asyncLoad.isDone) {
                yield return null;
            }

            // Find SceneController in loaded scene
            SceneController[] sceneControllers = FindObjectsOfType<SceneController>();
            SceneController newSceneController = sceneControllers.Where(t => t.Type == scene).FirstOrDefault();
            if(newSceneController == null) {
                Debug.LogWarning($"SceneController not found in loaded scene. scene: {scene}");
            }

            // Change CurrentSceneController
            CurrentSceneController = newSceneController;
            // CurrentSceneController load action
            if(CurrentSceneController != null) {
                CurrentSceneController.OnSceneLoad();

                // Wait end CurrentSceneController.OnSceneLoad
                while(!CurrentSceneController.SceneLoadedCompletely) {
                    yield return null;
                }
            }

            CurrentSceneType = scene;

            //Fake Wait
            const float fakeWaitTime = 1.0f;
            float timer = 0.0f;
            while(timer < fakeWaitTime) {
                timer += Time.deltaTime;

                ProgressNum = Mathf.Lerp(0.9f, 1.0f, timer / fakeWaitTime);

                yield return null;
            }
            ProgressNum = 1.0f;

            // Deactivate loading panel
            LoadingPanel.Instance.SetActive(false, 0.5f);
            LoadingPanel.Instance.StopProgressUpdate();
            yield return wait;

            loadSceneCoroutine = null;
        }
        #endregion

        public enum SceneType {
            None,

            Splash,

            Main,
            Lobby,
            Game,

            Credits,



            /***** Demo Template *****/
            StandardSceneTemplate_00_Splash,
            StandardSceneTemplate_01_Main, 
            StandardSceneTemplate_02_Game,
        }
    }
}