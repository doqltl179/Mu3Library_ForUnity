#if UNITY_EDITOR
using Mu3Library;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class DefaultSceneLoader {



    static DefaultSceneLoader() {
        EditorApplication.playModeStateChanged += LoadDefaultScene;
    }

    static void LoadDefaultScene(PlayModeStateChange state) {
        if(state == PlayModeStateChange.ExitingEditMode) {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        if(EditorUtilPrefs.UsePlayLoadScene) {
            if(state == PlayModeStateChange.EnteredPlayMode) {
                string loadSceneName = EditorUtilPrefs.PlayLoadSceneName;
                string compareSceneName = "";
                int loadSceneIndex = 0;
                for(int i = 0; i < EditorBuildSettings.scenes.Length; i++) {
                    compareSceneName = System.IO.Path.GetFileNameWithoutExtension(EditorBuildSettings.scenes[i].path);
                    if(compareSceneName == loadSceneName) {
                        loadSceneIndex = i;

                        break;
                    }
                }

                EditorSceneManager.LoadScene(loadSceneIndex);
            }
        }
    }
}
#endif