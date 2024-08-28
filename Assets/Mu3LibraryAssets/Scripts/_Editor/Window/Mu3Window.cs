#if UNITY_EDITOR
using Mu3Library.Utility;
using Mu3Library.Utility.CustomClass;
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

        #endregion

        private Vector2 windowScreenPos;

        #region Play Load Scene Properties
        private bool usePlayLoadScene = false;
        private SceneType playLoadScene = SceneType.Splash;
        #endregion

        #region Move Scene Properties
        /// <summary>
        /// (Directory, Paths)
        /// </summary>
        private SerializableDictionary<SceneControlStruct> sceneStructs;
        private Vector2 sceneListScrollPos;
        #endregion

        #region Screen Capture Properties
        private bool captureToCustomSize = false;
        private Vector2Int captureSize = new Vector2Int(1920, 1080);

        private bool changeCaptureColor;
        private Color targetColor = Color.black;
        private Color changeColor = Color.white;
        private float colorChangeStrength = 1.0f;

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
                SetProperties(currentWindowProperty);

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
                    normal = new GUIStyleState() {
                        textColor = Color.white,
                    },
                };

                isRefreshed = true;
            }
        }

        private void SetProperties(Mu3WindowProperty property) {
            usePlayLoadScene = property.UsePlayLoadScene;
            playLoadScene = property.PlayLoadScene;

            sceneStructs = property.SceneStructs;

            captureToCustomSize = property.CaptureToCustomSize;
            captureSize = property.CaptureSize;

            changeCaptureColor = property.ChangeCaptureColor;
            targetColor = property.TargetColor;
            changeColor = property.ChangeColor;
            colorChangeStrength = property.ColorChangeStrength;

            captureSaveDirectory = property.CaptureSaveDirectory;
            captureSaveFileName = property.CaptureSaveFileName;
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



            DrawHeader1("User Settings");

            #region User Settings
            DrawHeader2("Play Load Scene");

            bool usePlayScene = GUILayout.Toggle(usePlayLoadScene, "Use Play Load Scene");
            if(usePlayScene != usePlayLoadScene) {
                usePlayLoadScene = usePlayScene;
                currentWindowProperty.UsePlayLoadScene = usePlayScene;
            }
            if(usePlayScene) {
                SceneType playScene = (SceneType)EditorGUILayout.EnumPopup("Play Load Scene", playLoadScene);
                if(playScene != playLoadScene) {
                    Debug.Log($"playLoadScene changed to [{playScene}]");

                    EditorUtilPrefs.PlayLoadScene = playScene;
                    playLoadScene = playScene;
                }
            }

            if(usePlayScene != usePlayLoadScene) {
                Debug.Log($"usePlayLoadScene changed to [{usePlayScene}]");

                EditorUtilPrefs.UsePlayLoadScene = usePlayScene;
                usePlayLoadScene = usePlayScene;
            }

            DrawHeader2("Move Scene", true);

            if(EditorApplication.isPlayingOrWillChangePlaymode) {
                GUILayout.Label("Now editor is playing.");
            }
            else if(sceneStructs == null || sceneStructs.Count == 0) {
                GUILayout.Label("Scenes not found.");
            }
            else {
                const float buttonHeight = 30;

                foreach(var st in sceneStructs) {
                    //GUILayout.BeginHorizontal(GUILayout.Height(header3Style.fixedHeight));
                    GUILayout.BeginHorizontal();

                    bool showInInspector = GUILayout.Toggle(st.Value.ShowInInspector, "Show In Inspector");

                    DrawHeader3(st.Key);

                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    if(showInInspector) {
                        foreach(var path in st.Value.Paths) {
                            if(GUILayout.Button(Path.GetFileNameWithoutExtension(path), GUILayout.Height(buttonHeight))) {
                                if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                                    EditorSceneManager.OpenScene(path);
                                }
                            }
                        }
                    }

                    st.Value.ShowInInspector = showInInspector;
                }
            }
            #endregion

            DrawHeader1("Screen Capture", true);

            #region Screen Capture
            EditorGUILayout.BeginHorizontal();

            bool useCustomSizeWhenScreenCapture = GUILayout.Toggle(captureToCustomSize, "Capture Size To Custom Size");
            if(useCustomSizeWhenScreenCapture != captureToCustomSize) {


                captureToCustomSize = useCustomSizeWhenScreenCapture;
            }

            if(captureToCustomSize) {
                GUILayout.Space(15);

                captureSize = EditorGUILayout.Vector2IntField("", captureSize);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            changeCaptureColor = GUILayout.Toggle(changeCaptureColor, "Change Color");
            if(changeCaptureColor) {
                targetColor = EditorGUILayout.ColorField("Target", targetColor);
                changeColor = EditorGUILayout.ColorField("Change To", changeColor);
                colorChangeStrength = EditorGUILayout.Slider("Color Change Strength", colorChangeStrength, 0.0f, 16.0f);
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

                    if(captureToCustomSize) {
                        Capture(captureSize, path);
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

            if(changeCaptureColor) {
                Vector3 targetVec = UtilFunc.ColToVec(targetColor);
                Vector3 changeVec = UtilFunc.ColToVec(changeColor);
                float changeDistance = Vector3.Distance(targetVec, changeVec);

                Color[] colors = tex.GetPixels();
                Vector3 currentVec;
                float dist;
                for(int i = 0; i < colors.Length; i++) {
                    currentVec = UtilFunc.ColToVec(colors[i]);
                    dist = Vector3.Distance(currentVec, targetVec);

                    colors[i] = Color.Lerp(colors[i], changeColor, Mathf.Pow(Mathf.Clamp01(1.0f - dist / changeDistance), colorChangeStrength));
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