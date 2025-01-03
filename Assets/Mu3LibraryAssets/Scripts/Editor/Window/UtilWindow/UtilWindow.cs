#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Mu3Library.Editor.Window {
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

        void OnGUI() {
            if(!DrawPropertyArea()) {
                return;
            }



            windowScreenPos = EditorGUILayout.BeginScrollView(windowScreenPos);
            const float buttonHeight = 30;

            #region User Settings
            DrawHeader1("User Settings");

            DrawHeader2("Play Load Scene", false, true);

            DrawHorizontal(() => {
                bool usePlayScene = EditorGUILayout.ToggleLeft("Use Play Load Scene", currentWindowProperty.UsePlayLoadScene);
                if(usePlayScene != currentWindowProperty.UsePlayLoadScene) {
                    currentWindowProperty.UsePlayLoadScene = usePlayScene;
                }
                if(usePlayScene) {
                    GUILayout.Space(5);

                    DrawLayoutStructWithState(() => {
                        if(!string.IsNullOrEmpty(currentWindowProperty.PlayLoadScene)) {
                            currentWindowProperty.PlayLoadScene = "";
                        }

                        GUILayout.Label("Build Scenes not found.");
                    }, () => {
                        if(currentWindowProperty.PlayLoadSceneIndex < 0) {
                            currentWindowProperty.PlayLoadSceneIndex = 0;
                        }
                        else if(currentWindowProperty.PlayLoadSceneIndex >= currentWindowProperty.SceneInBuildNameList.Length) {
                            currentWindowProperty.PlayLoadSceneIndex = currentWindowProperty.SceneInBuildNameList.Length - 1;
                        }

                        int playSceneIndex = EditorGUILayout.Popup(currentWindowProperty.PlayLoadSceneIndex, currentWindowProperty.SceneInBuildNameList, EditorStyles.popup);
                        if(playSceneIndex != currentWindowProperty.PlayLoadSceneIndex) {


                            currentWindowProperty.PlayLoadSceneIndex = playSceneIndex;
                        }
                    }, () => {
                        return currentWindowProperty.SceneInBuildNameList == null || currentWindowProperty.SceneInBuildNameList.Length == 0;
                    });
                }
            });

            DrawHeader2("Scene Control", true, true);

            DrawLayoutStructWithState(() => {
                GUILayout.Label("Now editor is playing.");
            }, () => {
                DrawHorizontal(() => {
                    if(GUILayout.Button("Add All", GUILayout.Width(60), GUILayout.Height(buttonHeight))) {
                        currentWindowProperty.AddAllScenesInBuild();
                        currentWindowProperty.RefreshBuildSceneList();
                    }

                    GUILayout.Space(5);

                    if(GUILayout.Button("Remove All", GUILayout.Width(80), GUILayout.Height(buttonHeight))) {
                        currentWindowProperty.RemoveAllScenesInBuild();
                        currentWindowProperty.RefreshBuildSceneList();
                    }
                });

                DrawHorizontal(() => {
                    if(currentWindowProperty.SceneInBuildReorderableList != null) {
                        currentWindowProperty.SceneInBuildReorderableList.DoLayoutList();
                    }
                    else {
                        GUILayout.Label("'sceneInBuildReorderableList' is NULL.");
                    }
                }, 20, 20);

                DrawLayoutStructWithState(() => {
                    GUILayout.Label("Scenes not found.");
                }, () => {
                    foreach(var st in currentWindowProperty.SceneStructs) {
                        DrawHorizontal(() => {
                            if(GUILayout.Button("Add All", GUILayout.Width(60), GUILayout.Height(buttonHeight))) {
                                currentWindowProperty.AddBuildScenes(st.Properties);
                                currentWindowProperty.RefreshBuildSceneList();
                            }
                            GUILayout.Space(5);

                            if(GUILayout.Button("Remove All", GUILayout.Width(80), GUILayout.Height(buttonHeight))) {
                                currentWindowProperty.RemoveBuildScenes(st.Properties);
                                currentWindowProperty.RefreshBuildSceneList();
                            }
                            GUILayout.Space(5);

                            Rect lastRect = GUILayoutUtility.GetLastRect(); //이전 요소의 Rect 가져오기
                            Rect foldoutRect = new Rect(lastRect.x + 5, lastRect.y + lastRect.height + 2, 20, EditorGUIUtility.singleLineHeight); //이전 Rect를 기반으로 Foldout 그리기
                            st.ChangeShowInInspector(EditorGUI.Foldout(foldoutRect, st.ShowInInspector, ""));

                            GUILayout.Space(5);

                            DrawHeader3(st.Key);
                        }, 5);

                        if(st.ShowInInspector) {
                            foreach(var property in st.Properties) {
                                DrawHorizontal(() => {
                                    DrawAsReadOnly(() => {
                                        if(GUILayout.Button("Add In Build", GUILayout.Height(buttonHeight), GUILayout.Width(90))) {
                                            currentWindowProperty.AddBuildScene(property);
                                            currentWindowProperty.RefreshBuildSceneList();
                                        }
                                    }, () => currentWindowProperty.IsExistInBuildScenes(property));

                                    if(GUILayout.Button("Select", GUILayout.Height(buttonHeight), GUILayout.Width(60))) {
                                        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(property.Path);
                                        EditorGUIUtility.PingObject(Selection.activeObject);
                                    }

                                    if(GUILayout.Button(Path.GetFileNameWithoutExtension(property.Path), GUILayout.Height(buttonHeight))) {
                                        if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                                            EditorSceneManager.OpenScene(property.Path);
                                        }
                                    }
                                }, 20, 20);
                            }

                            GUILayout.Space(5);
                        }
                    }
                }, () => {
                    return currentWindowProperty.SceneStructs == null || currentWindowProperty.SceneStructCount == 0;
                });
            }, () => {
                return EditorApplication.isPlayingOrWillChangePlaymode;
            });
            #endregion

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
            });

            currentWindowProperty.ChangeCaptureColor = GUILayout.Toggle(currentWindowProperty.ChangeCaptureColor, "Change Color");
            if(currentWindowProperty.ChangeCaptureColor) {
                currentWindowProperty.TargetColor = EditorGUILayout.ColorField("Target", currentWindowProperty.TargetColor);
                currentWindowProperty.ChangeColor = EditorGUILayout.ColorField("Change To", currentWindowProperty.ChangeColor);
                currentWindowProperty.ColorChangeStrength = EditorGUILayout.Slider("Color Change Strength", currentWindowProperty.ColorChangeStrength, 0.0f, 16.0f);
            }

            DrawHorizontal(() => {
                if(GUILayout.Button("Screen Capture", GUILayout.Height(buttonHeight))) {
                    string path = EditorUtility.SaveFilePanel(
                        "Save ScreenShot",
                        string.IsNullOrEmpty(captureSaveDirectory) ? Application.dataPath : captureSaveDirectory,
                        captureSaveFileName + ".png",
                        "png");
                    if(!string.IsNullOrEmpty(path)) {
                        captureSaveDirectory = Path.GetDirectoryName(path);
                        captureSaveFileName = Path.GetFileNameWithoutExtension(path);

                        if(currentWindowProperty.CaptureToCustomSize) {
                            Capture(currentWindowProperty.CaptureSize, path);
                        }
                        else {
                            Capture(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height), path);
                        }

                        Debug.Log($"ScreenShot saved. path: {path}");
                    }
                    else {
                        Debug.Log("ScreenShot path is NULL.");
                    }
                }
            });
            #endregion



            EditorGUILayout.EndScrollView();
        }

        private void Capture(Vector2Int captureSize, string path) {
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