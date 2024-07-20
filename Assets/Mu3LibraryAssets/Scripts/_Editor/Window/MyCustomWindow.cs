#if UNITY_EDITOR
using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

using static Mu3Library.Scene.SceneLoader;

namespace Mu3Library.Editor.Window {
    public class MyCustomWindow : EditorWindow {
        private const string WindowsMenuName = "Mu3Library/Windows";

        private const string WindowName_MyCustomWindow = WindowsMenuName + "/My Custom Window";

        private bool usePlayLoadScene = true;
        private SceneType playLoadScene = SceneType.Splash;

        private Vector2Int captureSize = new Vector2Int(1920, 1080);
        private bool changeCaptureColor;
        private Color targetColor = Color.black;
        private Color changeColor = Color.white;
        private float colorChangeStrength = 1.0f;
        private string captureSavePath;



        [MenuItem(WindowName_MyCustomWindow)]
        public static void ShowWindow() {
            // 윈도우 인스턴스를 가져오거나 생성합니다.
            GetWindow(typeof(MyCustomWindow), false, "My Custom Window");
        }

        private void OnBecameVisible() {
            usePlayLoadScene = EditorUtilPrefs.UsePlayLoadScene;
            UtilFunc.StringToEnum(EditorUtilPrefs.PlayLoadSceneName, ref playLoadScene);
        }

        void OnGUI() {
            GUIStyle headerStyle = new GUIStyle() {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(20, 20, 12, 12),
                fixedHeight = 40,
                normal = new GUIStyleState() {
                    textColor = Color.white,
                },
            };
            GUIStyle header2Style = new GUIStyle() {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(20, 20, 12, 12),
                fixedHeight = 40,
                normal = new GUIStyleState() {
                    textColor = Color.white,
                },
            };



            EditorGUILayout.LabelField("User Settings", headerStyle);

            GUILayout.Space(25);
            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 1), EditorGUIUtility.whiteTexture);
            GUILayout.Space(10);

            #region User Settings
            bool usePlayScene = GUILayout.Toggle(usePlayLoadScene, "Use Play Load Scene");
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
            #endregion

            GUILayout.Space(30);
            EditorGUILayout.LabelField("Screen Capture", headerStyle);

            GUILayout.Space(25);
            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 1), EditorGUIUtility.whiteTexture);
            GUILayout.Space(10);

            #region Screen Capture
            captureSize = EditorGUILayout.Vector2IntField("Capture Size", captureSize);

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
                    string.IsNullOrEmpty(captureSavePath) ? Application.dataPath : captureSavePath,
                    "ScreenShot" + ".png",
                    "png");
                if(!string.IsNullOrEmpty(path)) {
                    captureSavePath = path;
                    //ScreenCapture.CaptureScreenshot(captureSavePath);
                    Capture(captureSize, captureSavePath);

                    Debug.Log($"ScreenShot saved. path: {captureSavePath}");
                }
                else {
                    Debug.Log("ScreenShot path is NULL.");
                }
            }

            GUILayout.EndHorizontal();
            #endregion
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
    }
}
#endif