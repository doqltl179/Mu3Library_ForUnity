using Mu3Library.Scene;
using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Demo.SceneLoadTemplate {
    public class SceneController_Splash : SceneController {



        private void Start() {
            CameraManager.Instance.SetCameraToMainCamera();
        }

        private void Update() {
            if(Input.GetKeyDown(KeyCode.Space)) {
                if(!SceneLoader.Instance.IsLoading) SceneLoader.Instance.LoadScene(SceneLoader.SceneType.StandardSceneTemplate_01_Main);
            }
        }
    }
}