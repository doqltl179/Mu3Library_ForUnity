#if UNITY_EDITOR
using Mu3Library.Editor.FileUtil;
using Mu3Library.Editor.Window;
using Mu3Library.ScreenShot;
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Mu3Library.Sample.UtilWindow {
    public class UtilWindow : Mu3Window<UtilWindowProperty> {
        private const string WindowsMenuName = "Mu3Library/Windows";

        private const string WindowName_MyCustomWindow = WindowsMenuName + "/Util Window";

        private int screenCaptureSuperSize = 1;
        private string captureSaveDirectory = "";

        /// <summary>
        /// Input
        /// </summary>
        private Texture2D imageToBase64Texture = null;
        /// <summary>
        /// 'imageToBase64Texture'를 변환하여 얻은 base64로 다시 만든 Texture
        /// </summary>
        private Texture2D base64ToImageTexture = null;
        /// <summary>
        /// Output Material
        /// </summary>
        private Material base64ToImageMaterial = null;
        /// <summary>
        /// If you want to check base64 converted correctly.
        /// </summary>
        private bool showBase64Image = false;



        [MenuItem(WindowName_MyCustomWindow)]
        public static void ShowWindow() {
            GetWindow(typeof(UtilWindow), false, "Util Window");
        }

        protected override void OnGUIFunc() {
            DrawPropertiesForDebug();

            GUILayoutOption normalButtonHeight = GUILayout.Height(30);

            SceneListDrawFunc();
            ImageToBase64DrawFunc();
            ScreenCaptureDrawFunc();
        }

        #region Draw Func

        #region Scene List
        private void SceneListDrawFunc() {
            GUILayoutOption normalButtonHeight = GUILayout.Height(30);

            bool foldout_sceneList = currentWindowProperty.Foldout_SceneList;
            DrawFoldoutHeader1("Scene List", ref foldout_sceneList);
            currentWindowProperty.Foldout_SceneList = foldout_sceneList;

            if(foldout_sceneList) {
                // 플레이 중에는 Scene 이동을 막는다.
                if(EditorApplication.isPlaying) {
                    GUILayout.Label("When editor is playing, you can't move to other scene.");

                    return;
                }

                DrawHorizontal(() => {
                    if(GUILayout.Button("Add Scene Directory", GUILayout.Width(136), normalButtonHeight)) {
                        // 폴더의 절대 경로
                        string directory = EditorUtility.OpenFolderPanel("Find Scene Directory", Application.dataPath, "Scenes");
                        // 폴더의 상대 경로
                        string relativeDirectory = FilePathConvertor.SystemPathToAssetPath(directory);

                        currentWindowProperty.AddSceneCheckDirectory(relativeDirectory);

                        // Unity에 변경 사항이 있음을 알림
                        EditorUtility.SetDirty(currentWindowProperty);
                        // 변경 사항 저장
                        AssetDatabase.SaveAssets();
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

                                EditorUtility.SetDirty(currentWindowProperty);
                                AssetDatabase.SaveAssets();
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
                                        FilePathConvertor.SplitPath(scenePath, out directory, out fileName, out extension);

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
        }
        #endregion

        #region Screen Capture
        private void ScreenCaptureDrawFunc() {
            GUILayoutOption normalButtonHeight = GUILayout.Height(30);

            bool foldout_screenCapture = currentWindowProperty.Foldout_ScreenCapture;
            DrawFoldoutHeader1("Screen Capture", ref foldout_screenCapture);
            currentWindowProperty.Foldout_ScreenCapture = foldout_screenCapture;

            if(foldout_screenCapture) {
                DrawStruct(() => {
                    DrawHorizontal(() => {
                        if(Camera.main != null) {
                            int superSizeWidth = Camera.main.scaledPixelWidth * screenCaptureSuperSize;
                            int superSizeHeight = Camera.main.scaledPixelHeight * screenCaptureSuperSize;
                            GUILayout.Label($"Super Size ({superSizeWidth}x{superSizeHeight})", GUILayout.Width(240));
                        }
                        else {
                            GUILayout.Label("Super Size (Main Camera not found...)", GUILayout.Width(240));
                        }

                        GUILayout.Space(4);

                        screenCaptureSuperSize = EditorGUILayout.IntSlider("", screenCaptureSuperSize, 1, 10);
                    }, 0, 0);

                    GUILayout.Space(4);

                    if(GUILayout.Button("Screen Capture", normalButtonHeight)) {
                        string panelTitle = "ScreenShot Save Folder";
                        string directory = 
                            string.IsNullOrEmpty(captureSaveDirectory) ? 
                            Environment.GetFolderPath(Environment.SpecialFolder.Desktop) :
                            captureSaveDirectory;
                        string defaultFolderName = "";
                        string saveFolderDirectory = EditorUtility.SaveFolderPanel(panelTitle, directory, defaultFolderName);

                        if(!string.IsNullOrEmpty(saveFolderDirectory)) {
                            captureSaveDirectory = saveFolderDirectory;

                            if(Application.isPlaying) {
                                ScreenCaptureHelper.ScreenShot(saveFolderDirectory, screenCaptureSuperSize, (tex) => {
                                    if(tex != null) {
                                        MonoBehaviour.DestroyImmediate(tex);
                                    }
                                });
                            }
                            else {
                                ScreenCaptureHelper.ScreenShot(saveFolderDirectory, screenCaptureSuperSize);
                            }

                            // 간혹, 캡처 후 에러 메시지가 나오지 않음에도 이미지 파일이 보이지 않는 문제가 있는데
                            // Editor에서 ScreenCapture를 사용할 경우 EditMode 혹은 PlayMode에 상관 없이
                            // GameView에 포커스가 잡혀있어야 캡처가 진행된다.
                            EditorApplication.ExecuteMenuItem("Window/General/Game");
                        }
                    }
                }, 20, 20, 0, 0);
            }
        }

        private void SaveImageAsPNG(Texture2D tex, string directory) {
            if(tex == null) {
                Debug.LogError($"Failed save image. Image is NULL.");

                return;
            }
            if(string.IsNullOrEmpty(directory)) {
                Debug.LogError($"Failed save image. Directory is empty.");

                return;
            }

            byte[] bytes = tex.EncodeToPNG();
            //string fileName = $"ScreenCapture_{tex.width}x{tex.height}_{DateTime.Now.ToString("yyyyMMdd_HHmmss_fffffff")}";
            string fileName = $"ScreenCapture_{DateTime.Now.ToString("yyyyMMdd_HHmmss_fffffff")}";
            string filePath = $"{directory}/{fileName}.png";
            File.WriteAllBytes(filePath, bytes);
            MonoBehaviour.DestroyImmediate(tex);
        }
        #endregion

        #region Image to Base64
        private void ImageToBase64DrawFunc() {
            GUILayoutOption normalButtonHeight = GUILayout.Height(30);

            bool foldout_imageToBase64 = currentWindowProperty.Foldout_ImageToBase64;
            DrawFoldoutHeader1("Image to Base64", ref foldout_imageToBase64);
            currentWindowProperty.Foldout_ImageToBase64 = foldout_imageToBase64;

            if(foldout_imageToBase64) {
                DrawHorizontal(() => {
                    DrawObjectAreaForProjectObject(ref imageToBase64Texture, 80);
                    if(imageToBase64Texture != null) {
                        if(!imageToBase64Texture.isReadable) {
                            Selection.activeObject = imageToBase64Texture;
                            EditorGUIUtility.PingObject(Selection.activeObject);

                            imageToBase64Texture = null;

                            Debug.LogError($"This Texture is not readable. Please check 'Read/Write' in this Texture.");

                            return;
                        }

                        DrawVertical(() => {
                            showBase64Image = GUILayout.Toggle(showBase64Image, "Show Copied Base64 Image (To Right Side)");

                            if(GUILayout.Button("To Base64 and Copy", GUILayout.Width(160), normalButtonHeight)) {
                                byte[] data = null;

                                string texPath = FileFinder.GetAssetPath(imageToBase64Texture);
                                string extension = texPath.Split('.').Last();
                                if(!EncodeTexture(imageToBase64Texture, extension, ref data)) {
                                    imageToBase64Texture = null;

                                    return;
                                }

                                // 이미지 파일의 확장자는 정의되어 있으나, byte[]로 변환하지 못하는 경우가 있다.
                                if(data == null) {
                                    Texture2D copyTex = new Texture2D(imageToBase64Texture.width, imageToBase64Texture.height, TextureFormat.RGBA32, false);
                                    copyTex.SetPixels(imageToBase64Texture.GetPixels());
                                    copyTex.Apply();

                                    if(!EncodeTexture(copyTex, extension, ref data)) {
                                        Debug.LogError("Encode Failed.");
                                    }
                                    DestroyImmediate(copyTex);
                                }

                                if(data != null) {
                                    string base64String = Convert.ToBase64String(data);
                                    GUIUtility.systemCopyBuffer = base64String;
                                    Debug.Log($"Copy base64String.");

                                    if(base64ToImageTexture != null) {
                                        DestroyImmediate(base64ToImageTexture);
                                    }

                                    if(showBase64Image) {
                                        base64ToImageTexture = Base64ToImage(base64String);
                                    }

                                    // 변환된 Texture를 별도의 프로퍼티 창에서 보여준다.
                                    //EditorUtility.OpenPropertyEditor(base64ToImageTexture);
                                }
                            }
                        }, 0, 0);
                    }

                    GUILayout.FlexibleSpace();

                    if(base64ToImageTexture != null) {
                        if(base64ToImageMaterial == null) {
                            const string shaderPath = "Unlit/Transparent";
                            Shader shader = Shader.Find(shaderPath);
                            if(shader != null) {
                                base64ToImageMaterial = new Material(shader);
                            }
                            else {
                                Debug.LogWarning($"Shader not Found. path: {shaderPath}");
                            }
                        }

                        if(base64ToImageMaterial != null) {
                            Rect texRect = GUILayoutUtility.GetRect(80, 80);
                            EditorGUI.DrawPreviewTexture(texRect, base64ToImageTexture, base64ToImageMaterial);
                        }

                        // 읽기 전용
                        //GUI.enabled = false;
                        //DrawObjectAreaForProjectObject(ref base64ToImageTexture, 80);
                        //GUI.enabled = true;
                    }
                }, 20, 20);
            }
        }

        private Texture2D Base64ToImage(string base64String) {
            byte[] data = Convert.FromBase64String(base64String);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(data);

            tex.alphaIsTransparency = true;
            tex.Apply();

            return tex;
        }

        private bool EncodeTexture(Texture2D tex, string extension, ref byte[] data) {
            data = null;

            if(extension == "jpg" || extension == "jpeg") {
                data = tex.EncodeToJPG();
            }
            else if(extension == "png") {
                data = tex.EncodeToPNG();
            }
            else if(extension == "exr") {
                data = tex.EncodeToEXR();
            }
            else if(extension == "tga") {
                data = tex.EncodeToTGA();
            }
            else {
                Debug.LogError($"Not defined extension. extension: {extension}");

                return false;
            }

            return true;
        }
        #endregion

        #endregion
    }
}
#endif