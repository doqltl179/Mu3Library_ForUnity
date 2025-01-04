#if UNITY_EDITOR
using Mu3Library.Editor;
using Mu3Library.Editor.FileUtil;
using Mu3Library.Editor.Window;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Mu3Library.Demo.UtilWindow {
    public class UtilWindow : Mu3Window<UtilWindowProperty> {
        private const string WindowsMenuName = "Mu3Library/Windows";

        private const string WindowName_MyCustomWindow = WindowsMenuName + "/Util Window";

        #region Move Scene Properties



        #endregion

        #region Screen Capture Properties

        private string captureSaveDirectory = "";
        private string captureSaveFileName = "ScreenShot";

        #endregion



        [MenuItem(WindowName_MyCustomWindow)]
        public static void ShowWindow() {
            GetWindow(typeof(UtilWindow), false, "Util Window");
        }

        protected override void OnGUIFunc() {
            //DrawPropertiesForDebug();

            GUILayoutOption normalButtonHeight = GUILayout.Height(30);

            SceneListDrawFunc();

            #region Screen Capture
            DrawHeader1("Screen Capture", true);

            DrawHorizontal(() => {
                bool useCustomSizeWhenScreenCapture = GUILayout.Toggle(currentWindowProperty.CaptureToCustomSize, "Capture Size To Custom Size");
                if(useCustomSizeWhenScreenCapture != currentWindowProperty.CaptureToCustomSize) {
                    currentWindowProperty.CaptureToCustomSize = useCustomSizeWhenScreenCapture;
                }

                if(useCustomSizeWhenScreenCapture) {
                    GUILayout.Space(15);

                    currentWindowProperty.CaptureSize = EditorGUILayout.Vector2IntField("", currentWindowProperty.CaptureSize);
                }

                GUILayout.FlexibleSpace();
            }, 0, 0);

            currentWindowProperty.ChangeCaptureColor = GUILayout.Toggle(currentWindowProperty.ChangeCaptureColor, "Change Color");
            if(currentWindowProperty.ChangeCaptureColor) {
                currentWindowProperty.TargetColor = EditorGUILayout.ColorField("Target", currentWindowProperty.TargetColor);
                currentWindowProperty.ChangeColor = EditorGUILayout.ColorField("Change To", currentWindowProperty.ChangeColor);
                currentWindowProperty.ColorChangeStrength = EditorGUILayout.Slider("Color Change Strength", currentWindowProperty.ColorChangeStrength, 0.0f, 16.0f);
            }

            DrawHorizontal(() => {
                if(GUILayout.Button("Screen Capture", normalButtonHeight)) {
                    string path = EditorUtility.SaveFilePanel(
                        "Save ScreenShot",
                        string.IsNullOrEmpty(captureSaveDirectory) ? Application.dataPath : captureSaveDirectory,
                        captureSaveFileName + ".png",
                        "png");
                    if(!string.IsNullOrEmpty(path)) {
                        captureSaveDirectory = Path.GetDirectoryName(path);
                        captureSaveFileName = Path.GetFileNameWithoutExtension(path);

                        if(currentWindowProperty.CaptureToCustomSize) {
                            ScreenCapture(currentWindowProperty.CaptureSize, path);
                        }
                        else {
                            ScreenCapture(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height), path);
                        }

                        Debug.Log($"ScreenShot saved. path: {path}");
                    }
                    else {
                        Debug.Log("ScreenShot path is NULL.");
                    }
                }
            }, 0, 0);
            #endregion
        }

        #region Draw Func

        #region Scene List
        private void SceneListDrawFunc() {
            GUILayoutOption normalButtonHeight = GUILayout.Height(30);

            bool foldout_sceneList = currentWindowProperty.Foldout_SceneList;
            DrawFoldoutHeader1("Scene List", ref foldout_sceneList);

            if(foldout_sceneList) {
                DrawHorizontal(() => {
                    if(GUILayout.Button("Add Scene Directory", GUILayout.Width(136), normalButtonHeight)) {
                        // 폴더의 절대 경로
                        string directory = EditorUtility.OpenFolderPanel("Find Scene Directory", Application.dataPath, "Scenes");
                        // 폴더의 상대 경로
                        string relativeDirectory = FilePathConvertor.SystemPathToAssetPath(directory);

                        currentWindowProperty.AddSceneCheckDirectory(relativeDirectory);
                    }

                    GUILayout.Space(4);

                    if(GUILayout.Button("Remove All", GUILayout.Width(96), normalButtonHeight)) {
                        currentWindowProperty.SceneCheckDirectoryList.Clear();
                    }
                }, 20, 20);

                GUILayout.Space(4);

                DrawStruct(() => {
                    for(int i = 0; i < currentWindowProperty.SceneCheckDirectoryList.Count; i++) {
                        GUILayout.Space(4);

                        SceneCheckDirectoryStruct sceneStruct = currentWindowProperty.SceneCheckDirectoryList[i];
                        bool isRemoved = false;

                        DrawHorizontal(() => {
                            bool foldout_sceneStruct = sceneStruct.Foldout;
                            DrawFoldoutHeader2($"{sceneStruct.Directory} ({sceneStruct.ScenePaths.Length})", ref foldout_sceneStruct);

                            if(GUILayout.Button("Remove", GUILayout.Width(60), normalButtonHeight)) {
                                currentWindowProperty.SceneCheckDirectoryList.RemoveAt(i);
                                i--;
                                isRemoved = true;
                            }

                            sceneStruct.Foldout = foldout_sceneStruct;
                        }, 20, 20);

                        if(isRemoved) {
                            continue;
                        }

                        if(sceneStruct.Foldout) {
                            DrawStruct(() => {
                                foreach(string scenePath in sceneStruct.ScenePaths) {
                                    DrawHorizontal(() => {
                                        if(GUILayout.Button("Select", GUILayout.Width(60), normalButtonHeight)) {
                                            Selection.activeObject = FileFinder.LoadAssetAtPath<Object>(scenePath);
                                            EditorGUIUtility.PingObject(Selection.activeObject);
                                        }

                                        GUILayout.Space(4);

                                        string directory = "";
                                        string fileName = "";
                                        string extension = "";
                                        FilePathConvertor.SplitPathToDirectoryAndFileNameAndExtension(scenePath, out directory, out fileName, out extension);

                                        string sceneButtonName = $"{directory.Replace(sceneStruct.Directory, "")}/{fileName}";
                                        if(GUILayout.Button($"{sceneButtonName}", normalButtonHeight)) {
                                            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                                                EditorSceneManager.OpenScene(scenePath);
                                            }
                                        }
                                    }, 0, 0);
                                }
                            }, 20, 20, 0, 0);
                        }
                    }
                }, 20, 20, 0, 0);
            }

            currentWindowProperty.Foldout_SceneList = foldout_sceneList;
        }
        #endregion

        #region Screen Capture
        private void ScreenCaptureDrawFunc() {

        }
        #endregion

        #endregion

        private void ScreenCapture(Vector2Int captureSize, string path) {
            int width = captureSize.x;
            int height = captureSize.y;
            Debug.Log($"Capture Size: {width}x{height}");

            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);

            Camera.main.targetTexture = rt;
            Camera.main.Render();
            RenderTexture.active = rt;

            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            Camera.main.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(rt);

            if(currentWindowProperty.ChangeCaptureColor) {
                Vector3 targetVec = UtilFuncForEditor.ColToVec3(currentWindowProperty.TargetColor);
                Vector3 changeVec = UtilFuncForEditor.ColToVec3(currentWindowProperty.ChangeColor);
                float changeDistance = Vector3.Distance(targetVec, changeVec);

                Color[] colors = tex.GetPixels();
                Vector3 currentVec;
                float dist;
                for(int i = 0; i < colors.Length; i++) {
                    currentVec = UtilFuncForEditor.ColToVec3(colors[i]);
                    dist = Vector3.Distance(currentVec, targetVec);

                    colors[i] = Color.Lerp(colors[i], currentWindowProperty.ChangeColor, Mathf.Pow(Mathf.Clamp01(1.0f - dist / changeDistance), currentWindowProperty.ColorChangeStrength));
                }

                tex.SetPixels(colors);
                tex.Apply();
            }

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }
    }
}
#endif