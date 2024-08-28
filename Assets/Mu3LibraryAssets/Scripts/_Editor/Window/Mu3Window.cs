#if UNITY_EDITOR
using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using static Mu3Library.Scene.SceneLoader;

namespace Mu3Library.Editor.Window {
    public class Mu3Window : EditorWindow {
        private const string WindowsMenuName = "Mu3Library/Windows";

        private const string WindowName_MyCustomWindow = WindowsMenuName + "/Mu3 Window";

        private List<Mu3WindowProperty> windowPropertyList;
        private Mu3WindowProperty currentWindowProperty = null;
        private bool isRefreshed = false;

        #region GUIStyle

        private GUIStyle header1Style = null;
        private GUIStyle header2Style = null;
        private GUIStyle header3Style = null;
        private GUIStyle normalMiddleLeftStyle = null;

        #endregion

        private Vector2 windowScreenPos;

        #region Screen Capture Properties

        private string captureSaveDirectory = "";
        private string captureSaveFileName = "ScreenShot";

        #endregion



        [MenuItem(WindowName_MyCustomWindow)]
        public static void ShowWindow() {
            // 윈도우 인스턴스를 가져오거나 생성합니다.
            GetWindow(typeof(Mu3Window), false, "Mu3 Window");
        }

        private void OnBecameVisible() {
            InitializeProperties();
        }

        private void InitializeProperties() {
            if(currentWindowProperty == null || windowPropertyList == null || windowPropertyList.Count == 0) {
                isRefreshed = false;

                string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
                currentWindowProperty = null;

                windowPropertyList = new List<Mu3WindowProperty>();
                foreach(string guid in guids) {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    ScriptableObject obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                    if(obj.GetType() == typeof(Mu3WindowProperty)) {
                        windowPropertyList.Add((Mu3WindowProperty)obj);
                    }
                }

                if(windowPropertyList.Count > 0) {
                    currentWindowProperty = windowPropertyList[0];
                }
            }

            if(currentWindowProperty != null && !isRefreshed) {
                currentWindowProperty.Refresh();

                header1Style = new GUIStyle() {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(24, 24, 12, 12),
                    fixedHeight = 48,
                    normal = new GUIStyleState() {
                        textColor = Color.white,
                    },
                };
                header2Style = new GUIStyle() {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(16, 16, 8, 8),
                    fixedHeight = 32,
                    normal = new GUIStyleState() {
                        textColor = Color.white,
                    },
                };
                header3Style = new GUIStyle() {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(11, 11, 5, 5),
                    fixedHeight = 22,
                    normal = new GUIStyleState() {
                        textColor = Color.white,
                    },
                };

                isRefreshed = true;
            }
        }

        void OnGUI() {
            if(currentWindowProperty == null) {
                EditorGUILayout.LabelField("CurrentWindowProperty is NULL...");

                return;
            }
            else {
                GUILayout.BeginHorizontal();

                if(GUILayout.Button("Refresh")) {
                    isRefreshed = false;

                    InitializeProperties();
                }

                DrawAsReadOnlyField(currentWindowProperty);

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }



            windowScreenPos = EditorGUILayout.BeginScrollView(windowScreenPos);



            #region User Settings
            DrawHeader1("User Settings");

            DrawHeader2("Play Load Scene");

            bool usePlayScene = GUILayout.Toggle(currentWindowProperty.UsePlayLoadScene, "Use Play Load Scene");
            if(usePlayScene != currentWindowProperty.UsePlayLoadScene) {
                currentWindowProperty.UsePlayLoadScene = usePlayScene;
            }
            if(usePlayScene) {
                SceneType playScene = (SceneType)EditorGUILayout.EnumPopup("Play Load Scene", currentWindowProperty.PlayLoadScene);
                if(playScene != currentWindowProperty.PlayLoadScene) {
                    currentWindowProperty.PlayLoadScene = playScene;
                }
            }

            DrawHeader2("Move Scene", true);

            if(EditorApplication.isPlayingOrWillChangePlaymode) {
                GUILayout.Label("Now editor is playing.");
            }
            else if(currentWindowProperty.SceneStructs == null || currentWindowProperty.SceneStructs.Count == 0) {
                GUILayout.Label("Scenes not found.");
            }
            else {
                const float buttonHeight = 30;

                foreach(var st in currentWindowProperty.SceneStructs) {
                    //GUILayout.BeginHorizontal(GUILayout.Height(header3Style.fixedHeight));
                    GUILayout.BeginHorizontal();

                    bool showInInspector = GUILayout.Toggle(st.ShowInInspector, "Show In Inspector");

                    DrawHeader3(st.Key);

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    if(showInInspector) {
                        foreach(var property in st.Properties) {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(20);

                            bool includeInBuild = GUILayout.Toggle(property.IncludeInBuild, "Include In Build", GUILayout.Width(120));
                            if(includeInBuild != property.IncludeInBuild) {


                                property.IncludeInBuild = includeInBuild;
                            }

                            if(GUILayout.Button(Path.GetFileNameWithoutExtension(property.Path), GUILayout.Height(buttonHeight))) {
                                if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                                    EditorSceneManager.OpenScene(property.Path);
                                }
                            }

                            GUILayout.Space(20);
                            GUILayout.EndHorizontal();
                        }

                        GUILayout.Space(5);
                    }

                    st.ShowInInspector = showInInspector;
                }
            }
            #endregion

            #region Screen Capture
            DrawHeader1("Screen Capture", true);

            EditorGUILayout.BeginHorizontal();

            bool useCustomSizeWhenScreenCapture = GUILayout.Toggle(currentWindowProperty.CaptureToCustomSize, "Capture Size To Custom Size");
            if(useCustomSizeWhenScreenCapture != currentWindowProperty.CaptureToCustomSize) {
                currentWindowProperty.CaptureToCustomSize = useCustomSizeWhenScreenCapture;
            }

            if(useCustomSizeWhenScreenCapture) {
                GUILayout.Space(15);

                currentWindowProperty.CaptureSize = EditorGUILayout.Vector2IntField("", currentWindowProperty.CaptureSize);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            currentWindowProperty.ChangeCaptureColor = GUILayout.Toggle(currentWindowProperty.ChangeCaptureColor, "Change Color");
            if(currentWindowProperty.ChangeCaptureColor) {
                currentWindowProperty.TargetColor = EditorGUILayout.ColorField("Target", currentWindowProperty.TargetColor);
                currentWindowProperty.ChangeColor = EditorGUILayout.ColorField("Change To", currentWindowProperty.ChangeColor);
                currentWindowProperty.ColorChangeStrength = EditorGUILayout.Slider("Color Change Strength", currentWindowProperty.ColorChangeStrength, 0.0f, 16.0f);
            }

            GUILayout.BeginHorizontal();

            if(GUILayout.Button("Screen Capture")) {
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

            GUILayout.EndHorizontal();
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
                Vector3 targetVec = UtilFunc.ColToVec(currentWindowProperty.TargetColor);
                Vector3 changeVec = UtilFunc.ColToVec(currentWindowProperty.ChangeColor);
                float changeDistance = Vector3.Distance(targetVec, changeVec);

                Color[] colors = tex.GetPixels();
                Vector3 currentVec;
                float dist;
                for(int i = 0; i < colors.Length; i++) {
                    currentVec = UtilFunc.ColToVec(colors[i]);
                    dist = Vector3.Distance(currentVec, targetVec);

                    colors[i] = Color.Lerp(colors[i], currentWindowProperty.ChangeColor, Mathf.Pow(Mathf.Clamp01(1.0f - dist / changeDistance), currentWindowProperty.ColorChangeStrength));
                }

                tex.SetPixels(colors);
                tex.Apply();
            }

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }

        #region Util Style
        private void DrawHeader1(string label, bool insertSpaceOnUpSpaceOfHeader = false) {
            if(insertSpaceOnUpSpaceOfHeader) GUILayout.Space(25);
            EditorGUILayout.LabelField(label, header1Style);
            GUILayout.Space(25);

            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 1), EditorGUIUtility.whiteTexture);
            GUILayout.Space(10);
        }

        private void DrawHeader2(string label, bool insertSpaceOnUpSpaceOfHeader = false) {
            if(insertSpaceOnUpSpaceOfHeader) GUILayout.Space(25);
            EditorGUILayout.LabelField(label, header2Style);
            GUILayout.Space(15);
        }

        private void DrawHeader3(string label) {
            EditorGUILayout.LabelField(label, header3Style);
        }

        private void DrawAsReadOnlyField<T>(T obj) where T : Object {
            GUI.enabled = false;
            EditorGUILayout.ObjectField(obj, typeof(T), false);
            GUI.enabled = true;
        }

        private void DrawAsReadOnlyField(SerializedProperty property) {
            GUI.enabled = false;
            EditorGUILayout.PropertyField(property);
            GUI.enabled = true;
        }
        #endregion
    }
}
#endif