#if UNITY_EDITOR
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
    using UnityEngine;

namespace Mu3Library.Editor.Window {
    public class MyCustomWindow : EditorWindow {
        private const string WindowsMenuName = MyCustomMenu.MenuName + "/Windows";

        private const string WindowName_MyCustomWindow = WindowsMenuName + "/My Custom Window";

        private int gameLevelClear;
        private int currentGameLevel;

        private AnimationClip selectedClip;
        private string animationClipSavePath;

        private int clipSampleRateChangeTo;

        private Vector2Int captureSize;
        private bool changeCaptureColor;
        private Color targetColor;
        private Color changeColor;
        private string captureSavePath;



        [MenuItem(WindowName_MyCustomWindow)]
        public static void ShowWindow() {
            // 윈도우 인스턴스를 가져오거나 생성합니다.
            GetWindow(typeof(MyCustomWindow), false, "My Custom Window");
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


            #region Edit Animation Clip
            EditorGUILayout.LabelField("Edit Animation Clip", headerStyle);

            GUILayout.Space(25);
            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 1), EditorGUIUtility.whiteTexture);
            GUILayout.Space(10);

            selectedClip = EditorGUILayout.ObjectField("Animation Clip", selectedClip, typeof(AnimationClip), false) as AnimationClip;
            if(selectedClip != null) {
                if(GUILayout.Button("Create Reverse Clip")) {
                    string path = AssetDatabase.GetAssetPath(selectedClip);
                    string directory = Path.GetDirectoryName(path);
                    string fileName = Path.GetFileName(path);
                    string[] nameSplit = fileName.Split('.');
                    fileName = nameSplit[0] + "_Reverse";
                    if(nameSplit.Length > 1) {
                        for(int i = 1; i < nameSplit.Length; i++) {
                            fileName += '.';
                            fileName += nameSplit[i];
                        }
                    }
                    path = Path.Combine(directory, fileName);

                    if(!string.IsNullOrEmpty(path)) {
                        animationClipSavePath = path;

                        AnimationClip clip = GetReverseClip(selectedClip);

                        AssetDatabase.CreateAsset(clip, animationClipSavePath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        Debug.Log($"AnimationClip saved. path: {animationClipSavePath}");
                    }
                    else {
                        Debug.Log("AnimationClip path is NULL.");
                    }
                }

                GUILayout.BeginHorizontal();

                GUILayout.Label("Sample Rate");
                clipSampleRateChangeTo = EditorGUILayout.IntField(clipSampleRateChangeTo);
                if(GUILayout.Button("Change Sample Rate")) {
                    if(clipSampleRateChangeTo <= 0) {
                        Debug.Log($"Can not change. Sample Rate: {clipSampleRateChangeTo}");

                        return;
                    }

                    string path = AssetDatabase.GetAssetPath(selectedClip);
                    string directory = Path.GetDirectoryName(path);
                    string fileName = Path.GetFileName(path);
                    string[] nameSplit = fileName.Split('.');
                    fileName = nameSplit[0] + $"_{clipSampleRateChangeTo}";
                    if(nameSplit.Length > 1) {
                        for(int i = 1; i < nameSplit.Length; i++) {
                            fileName += '.';
                            fileName += nameSplit[i];
                        }
                    }
                    path = Path.Combine(directory, fileName);

                    if(!string.IsNullOrEmpty(path)) {
                        animationClipSavePath = path;

                        AnimationClip clip = GetSampleRateChangedClip(selectedClip, clipSampleRateChangeTo);

                        AssetDatabase.CreateAsset(clip, animationClipSavePath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();

                        Debug.Log($"AnimationClip saved. path: {animationClipSavePath}");
                    }
                    else {
                        Debug.Log("AnimationClip path is NULL.");
                    }
                }

                GUILayout.EndHorizontal();
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

        private AnimationClip GetSampleRateChangedClip(AnimationClip originalClip, int sampleRate) {
            AnimationClip clip = new AnimationClip();
            EditorUtility.CopySerialized(originalClip, clip);
            clip.frameRate = sampleRate;

            EditorCurveBinding[] curves = AnimationUtility.GetCurveBindings(originalClip);
            AnimationCurve tempCurve;
            foreach(EditorCurveBinding curve in curves) {
                tempCurve = AnimationUtility.GetEditorCurve(originalClip, curve);
                clip.SetCurve(curve.path, curve.type, curve.propertyName, tempCurve);
            }

            return clip;
        }

        private AnimationClip GetReverseClip(AnimationClip originalClip) {
            AnimationClip clip = new AnimationClip();
            clip.frameRate = originalClip.frameRate;

            EditorCurveBinding[] curves = AnimationUtility.GetCurveBindings(originalClip);
            AnimationCurve tempCurve;
            foreach(EditorCurveBinding curve in curves) {
                tempCurve = AnimationUtility.GetEditorCurve(originalClip, curve);
                AnimationCurve reverseCurve = new AnimationCurve();
                foreach(Keyframe key in tempCurve.keys) {
                    Keyframe reverseKey = new Keyframe(originalClip.length - key.time, key.value, -key.inTangent, -key.outTangent);
                    reverseCurve.AddKey(reverseKey);
                }

                AnimationUtility.SetEditorCurve(clip, curve, reverseCurve);
            }

            return clip;
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
                Color[] colors = tex.GetPixels();
                for(int i = 0; i < colors.Length; i++) {
                    //if(IsSameColor(targetColor, colors[i])) {
                    if(IsSameColor(targetColor, colors[i])) {
                        colors[i] = changeColor;
                    }
                }

                tex.SetPixels(colors);
                tex.Apply();
            }

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);
        }

        private bool IsSameColor(Color c1, Color c2) => c1.r == c2.r && c1.g == c2.g && c1.b == c2.b;
        private bool IsSameColorWithAlpha(Color c1, Color c2) => c1.r == c2.r && c1.g == c2.g && c1.b == c2.b && c1.a == c2.a;
    }
}
#endif