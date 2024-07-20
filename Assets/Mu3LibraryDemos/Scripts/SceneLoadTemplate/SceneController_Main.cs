using Mu3Library.Scene;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Demo.SceneLoadTemplate {
    public class SceneController_Main : SceneController {




        private void Update() {
            if(Input.GetKeyDown(KeyCode.Space)) {
                if(!SceneLoader.Instance.IsLoading) SceneLoader.Instance.LoadScene(SceneLoader.SceneType.StandardSceneTemplate_02_Game);
            }
        }
    }
}