#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mu3Library.Editor {
    public static class UtilFuncForEditor {



        #region Utility

        public static bool IsExistInBuildScenes(string sceneName) {
            return EditorBuildSettings.scenes.Any(t => System.IO.Path.GetFileNameWithoutExtension(t.path) == sceneName);
        }

        public static string GetCurrentSceneName() {
            return SceneManager.GetActiveScene().name;
        }

        #endregion
    }
}
#endif