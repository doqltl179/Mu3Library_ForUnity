#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

using Mu3Library.Editor;
using Mu3Library.Editor.Window;

[InitializeOnLoad]
public static class DefaultSceneLoader {




    static DefaultSceneLoader() {
        EditorApplication.playModeStateChanged += LoadDefaultScene;
    }

    static void LoadDefaultScene(PlayModeStateChange state) {
        Mu3Window win = EditorWindow.GetWindow<Mu3Window>();
        if(win == null) {
            // Log
        }
        else if(win.CurrentWindowProperty == null) {
            // Log
        }
        else {
            if(win.CurrentWindowProperty.UsePlayLoadScene && !string.IsNullOrEmpty(win.CurrentWindowProperty.PlayLoadScene)) {
                if(state == PlayModeStateChange.ExitingEditMode) {
                    EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
                }

                if(state == PlayModeStateChange.EnteredPlayMode) {
                    if(UtilFuncForEditor.IsExistInBuildScenes(win.CurrentWindowProperty.PlayLoadScene)) {
                        EditorSceneManager.LoadScene(win.CurrentWindowProperty.PlayLoadScene);
                    }
                }
            }
        }
    }
}
#endif