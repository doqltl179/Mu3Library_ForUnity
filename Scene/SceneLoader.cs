
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mu3Library.Singleton;

namespace Mu3Library.Scene {
    public class SceneLoader : GenericSingleton<SceneLoader> {
        public object[] Param { get; private set; } = null;

        public bool IsLoading { get { return loadSceneCoroutine != null; } }
        public SceneType CurrentLoadedScene { get; private set; }

        public Action<SceneType> OnSceneChanged;

        private IEnumerator loadSceneCoroutine = null;



        #region Utility
        public void LoadScene(SceneType scene, object[] param = null) {
            if(loadSceneCoroutine == null) {
                Param = param;

                loadSceneCoroutine = LoadSceneCoroutine(scene);
                StartCoroutine(loadSceneCoroutine);
            }
        }

        /// <summary>
        /// 컴포넌트에 선언된 "CurrentLoadedScene"의 값만 바꾸고 실제로 Scene을 이동하지는 않음
        /// </summary>
        public void ChangeCurrentLoadedSceneImmediately(SceneType type) {
            CurrentLoadedScene = type;
        }
        #endregion

        private IEnumerator LoadSceneCoroutine(SceneType scene) {
            // 다른 작업에 먼저 하기 위함.
            yield return null;



            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(scene.ToString());
            while(!asyncLoad.isDone) {


                yield return null;
            }

            CurrentLoadedScene = scene;
            OnSceneChanged?.Invoke(scene);

            yield return new WaitForSeconds(1.0f); //Fake Wait

            loadSceneCoroutine = null;
        }
    }
}