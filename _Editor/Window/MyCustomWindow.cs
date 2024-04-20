#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static Unity.VisualScripting.Member;

namespace Mu3Library.Editor.Window {
    public class MyCustomWindow : EditorWindow {
        private const string WindowsMenuName = "Mu3Library/Windows";

        private const string WindowName_MyCustomWindow = WindowsMenuName + "/My Custom Window";

        private int gameLevelClear;
        private int currentGameLevel;

        private string materialCopyDirectory;
        private string materialPasteDirectory;
        private Shader materialPasteShader;
        private int materialPropertyPairCount = 0;
        private MaterialPropertyPair[] materialPropertyPairs = new MaterialPropertyPair[0];

        private Transform materialChangeTransform;
        private string materialChangeDirectory;

        private GameObject compareAvatar;
        private GameObject editAvatar;

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

            #region Material
            EditorGUILayout.LabelField("Material", headerStyle);

            GUILayout.Space(25);
            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 1), EditorGUIUtility.whiteTexture);
            GUILayout.Space(10);

            if(GUILayout.Button("Add Material Property Pair")) {
                materialPropertyPairCount++;

                Array.Resize(ref materialPropertyPairs, materialPropertyPairCount);
                materialPropertyPairs[materialPropertyPairs.Length - 1] = new MaterialPropertyPair();
            }
            if(GUILayout.Button("Remove Material Property Pari")) {
                materialPropertyPairCount--;

                Array.Resize(ref materialPropertyPairs, materialPropertyPairCount);
            }
            if(materialPropertyPairs != null && materialPropertyPairs.Length > 0) {
                for(int i = 0; i < materialPropertyPairs.Length; i++) {
                    GUILayout.BeginHorizontal();
                    materialPropertyPairs[i].type = (MaterialPropertyType)EditorGUILayout.EnumPopup("Type", materialPropertyPairs[i].type);
                    materialPropertyPairs[i].original = EditorGUILayout.TextField("Original", materialPropertyPairs[i].original);
                    materialPropertyPairs[i].pair = EditorGUILayout.TextField("Pair", materialPropertyPairs[i].pair);
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                materialPasteShader = (Shader)EditorGUILayout.ObjectField("Change Shader", materialPasteShader, typeof(Shader), false);

                if(materialPasteShader != null && GUILayout.Button("Copy Material")) {
                    CopyAllMaterials();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(20);

            materialChangeTransform = EditorGUILayout.ObjectField("Change Transform", materialChangeTransform, typeof(Transform), true) as Transform;
            if(materialChangeTransform != null) {
                if(GUILayout.Button("Change Material With Same Name")) {
                    ChangeAllMaterialsWithSameName();
                }
            }

            GUILayout.Space(30);
            #endregion

            #region Avater
            EditorGUILayout.LabelField("Avatar", headerStyle);

            GUILayout.Space(25);
            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 1), EditorGUIUtility.whiteTexture);
            GUILayout.Space(10);

            compareAvatar = EditorGUILayout.ObjectField("Compare Avatar", compareAvatar, typeof(GameObject), false) as GameObject;
            editAvatar = EditorGUILayout.ObjectField("Edit Avatar", editAvatar, typeof(GameObject), false) as GameObject;
            if(compareAvatar != null && editAvatar != null) {
                if(GUILayout.Button("Copy Transform")) {
                    CopyAvatarTransform(compareAvatar, editAvatar);
                }
            }

            GUILayout.Space(30);
            #endregion

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

        private void ChangeAllMaterialsWithSameName() {
            materialChangeDirectory = EditorUtility.OpenFolderPanel("Change Directory",
                string.IsNullOrEmpty(materialChangeDirectory) ? Application.dataPath : materialChangeDirectory, "");
            if(string.IsNullOrEmpty(materialChangeDirectory)) return;

            string[] files = Directory.GetFiles(materialChangeDirectory, "*.mat", SearchOption.TopDirectoryOnly);
            Dictionary<string, string> materials = new Dictionary<string, string>();
            foreach(string file in files) {
                string fileName = Path.GetFileName(file).Replace(".mat", "");

                materials.Add(fileName, file);
            }

            if(materials.Count > 0) {
                MeshRenderer[] renderers = materialChangeTransform.GetComponentsInChildren<MeshRenderer>(true);
                foreach(MeshRenderer renderer in renderers) {
                    //for(int i = 0; i < renderer.sharedMaterials.Length; i++) {
                        string name = renderer.sharedMaterial.name.Replace(" (Instance)", "");
                        string assetPath = "";
                        if(materials.TryGetValue(name, out assetPath)) {
                            assetPath = assetPath.Replace(Application.dataPath, "Assets");
                            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                        }
                    //}
                }
            }
        }

        private void CopyAllMaterials() {
            materialCopyDirectory = EditorUtility.OpenFolderPanel("Copy Directory",
                string.IsNullOrEmpty(materialCopyDirectory) ? Application.dataPath : materialCopyDirectory, "");
            if(string.IsNullOrEmpty(materialCopyDirectory)) return;

            materialPasteDirectory = EditorUtility.OpenFolderPanel("Save Directory",
                string.IsNullOrEmpty(materialPasteDirectory) ? Application.dataPath : materialPasteDirectory, "");
            if(string.IsNullOrEmpty(materialPasteDirectory)) return;

            string[] files = Directory.GetFiles(materialCopyDirectory, "*.mat", SearchOption.TopDirectoryOnly);
            foreach(string file in files) {
                string assetPath = file.Replace(Application.dataPath, "Assets");
                Material originalMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if(originalMaterial != null) {
                    // Create a new material instance
                    Material newMaterial = new Material(originalMaterial);
                    newMaterial.shader = materialPasteShader;
                    foreach(MaterialPropertyPair pair in materialPropertyPairs) {
                        switch(pair.type) {
                            case MaterialPropertyType.Texture: {
                                    Texture original = originalMaterial.GetTexture(pair.original);
                                    newMaterial.SetTexture(pair.pair, original);
                                }
                                break;
                            case MaterialPropertyType.Color: {
                                    Color original = originalMaterial.GetColor(pair.original);
                                    newMaterial.SetColor(pair.pair, original);
                                }
                                break;
                        }
                    }

                    string saveDir = materialPasteDirectory.Replace(Application.dataPath, "Assets");
                    string newFilePath = Path.Combine(saveDir, Path.GetFileName(assetPath));
                    AssetDatabase.CreateAsset(newMaterial, newFilePath);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void CopyAvatarTransform(GameObject from, GameObject to) {
            void CopyProperty(Transform from, Transform to) {
                to.name = from.name;
                to.position = from.position;
                to.rotation = from.rotation;
                to.localScale = from.localScale;
            }

            void Copy(Transform from, Transform to) {
                CopyProperty(from, to);

                int childCount = Mathf.Min(from.childCount, to.childCount);
                if(childCount > 0) {
                    for(int i = 0; i < childCount; i++) {
                        Copy(from.GetChild(i), to.GetChild(i));
                    }
                }
            }

            Transform fromChild = from.transform.GetChild(0);
            Transform toChild = to.transform.GetChild(0);
            Copy(fromChild, toChild);
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

        [Serializable]
        private class MaterialPropertyPair {
            public MaterialPropertyType type;
            public string original;
            public string pair;
        }

        private enum MaterialPropertyType {
            Texture, 
            Color, 
            Float, 
            Vector, 
        }
    }
}
#endif