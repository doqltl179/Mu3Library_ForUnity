using System;
using System.IO;
using System.Text;
using Mu3Library.Editor.FileUtil;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class ScreenCaptureDrawer : Mu3WindowDrawer
    {
        public const string FileName = "ScreenCaptureDrawer";
        private const string ItemName = "Screen Capture";
        private const string MenuName = MenuRoot + "/" + ItemName;

        [SerializeField, HideInInspector] private DefaultAsset _saveFolder;
        [SerializeField, HideInInspector] private string _fileName = "Screenshot";
        [SerializeField, HideInInspector] private bool _appendTimestamp = true;
        [SerializeField, HideInInspector] private bool _openFolderAfterCapture = true;
        [SerializeField, HideInInspector] private bool _captureGameView = true;
        [SerializeField, HideInInspector] private bool _captureSceneView = false;
        [SerializeField, HideInInspector] private int _gameViewSuperSize = 1;
        [SerializeField, HideInInspector] private int _sceneViewWidth = 0;
        [SerializeField, HideInInspector] private int _sceneViewHeight = 0;



        public override void OnGUIHeader()
        {
            DrawFoldoutHeader1(ItemName, ref _foldout);
        }

        public override void OnGUIBody()
        {
            DrawStruct(() =>
            {
                DrawSaveFolderField();

                _fileName = EditorGUILayout.TextField("File Name", _fileName);
                _appendTimestamp = EditorGUILayout.Toggle("Append Timestamp", _appendTimestamp);
                _openFolderAfterCapture = EditorGUILayout.Toggle("Reveal After Capture", _openFolderAfterCapture);

                GUILayout.Space(8);

                _captureGameView = EditorGUILayout.Toggle("Capture Game View", _captureGameView);
                if (_captureGameView)
                {
                    _gameViewSuperSize = Mathf.Max(1, EditorGUILayout.IntField("Game View Super Size", _gameViewSuperSize));
                }

                _captureSceneView = EditorGUILayout.Toggle("Capture Scene View", _captureSceneView);
                if (_captureSceneView)
                {
                    _sceneViewWidth = Mathf.Max(0, EditorGUILayout.IntField("Scene View Width", _sceneViewWidth));
                    _sceneViewHeight = Mathf.Max(0, EditorGUILayout.IntField("Scene View Height", _sceneViewHeight));
                }

                GUILayout.Space(8);

                if (GUILayout.Button("Capture", GUILayout.Height(30)))
                {
                    CaptureScreens();
                }

            }, 20, 20, 0, 0);
        }

        private void DrawSaveFolderField()
        {
            _saveFolder = EditorGUILayout.ObjectField("Save Folder", _saveFolder, typeof(DefaultAsset), false) as DefaultAsset;

            if (_saveFolder == null)
            {
                return;
            }

            if (!FileFinder.IsValidFolder(_saveFolder))
            {
                Debug.LogWarning("Selected object is not a valid folder.");
                _saveFolder = null;
            }
        }

        private void CaptureScreens()
        {
            if (!_captureGameView && !_captureSceneView)
            {
                Debug.LogWarning("No capture target selected.");
                return;
            }

            string saveFolderAssetPath = GetSaveFolderAssetPath();
            if (string.IsNullOrEmpty(saveFolderAssetPath))
            {
                Debug.LogWarning("Save folder is invalid.");
                return;
            }

            string saveFolderSystemPath = FilePathConvertor.AssetPathToSystemPath(saveFolderAssetPath);
            if (!Directory.Exists(saveFolderSystemPath))
            {
                Directory.CreateDirectory(saveFolderSystemPath);
            }

            string baseName = BuildBaseFileName();
            bool capturedSceneView = false;

            if (_captureSceneView)
            {
                string sceneViewPath = BuildCapturePath(saveFolderSystemPath, baseName, _captureGameView ? "SceneView" : "");
                capturedSceneView = CaptureSceneView(sceneViewPath);
            }

            if (capturedSceneView)
            {
                AssetDatabase.Refresh();
            }

            if (_captureGameView)
            {
                string gameViewPath = BuildCapturePath(saveFolderSystemPath, baseName, _captureSceneView ? "GameView" : "");
                EditorApplication.CallbackFunction captureGameView = () =>
                {
                    ScreenCapture.CaptureScreenshot(gameViewPath, Mathf.Max(1, _gameViewSuperSize));
                    EditorApplication.delayCall += AssetDatabase.Refresh;
                    if (_openFolderAfterCapture)
                    {
                        EditorUtility.RevealInFinder(saveFolderSystemPath);
                    }
                };

                if (EnsureGameViewOpenAndFocus())
                {
                    EditorApplication.delayCall += captureGameView;
                }
                else
                {
                    captureGameView.Invoke();
                }
            }
            else if (_openFolderAfterCapture && capturedSceneView)
            {
                EditorUtility.RevealInFinder(saveFolderSystemPath);
            }
        }

        private bool EnsureGameViewOpenAndFocus()
        {
            Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameViewType == null)
            {
                Debug.LogWarning("GameView type not found.");
                return false;
            }

            EditorWindow gameView = EditorWindow.GetWindow(gameViewType, false, "GameView", true);
            if (gameView == null)
            {
                Debug.LogWarning("GameView window not available.");
                return false;
            }

            gameView.Show();
            gameView.Focus();
            return true;
        }

        private string GetSaveFolderAssetPath()
        {
            if (_saveFolder == null)
            {
                return "Assets";
            }

            string assetPath = FileFinder.GetAssetPath(_saveFolder);
            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.IsValidFolder(assetPath))
            {
                return assetPath;
            }

            return "";
        }

        private string BuildBaseFileName()
        {
            string baseName = string.IsNullOrEmpty(_fileName) ? "Screenshot" : _fileName;
            baseName = SanitizeFileName(baseName);

            if (_appendTimestamp)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                baseName = $"{baseName}_{timestamp}";
            }

            return baseName;
        }

        private string BuildCapturePath(string folderSystemPath, string baseName, string suffix)
        {
            string finalName = string.IsNullOrEmpty(suffix) ? baseName : $"{baseName}_{suffix}";
            return Path.Combine(folderSystemPath, $"{finalName}.png");
        }

        private bool CaptureSceneView(string path)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || sceneView.camera == null)
            {
                Debug.LogWarning("SceneView camera not found.");
                return false;
            }

            int width = _sceneViewWidth > 0 ? _sceneViewWidth : sceneView.camera.pixelWidth;
            int height = _sceneViewHeight > 0 ? _sceneViewHeight : sceneView.camera.pixelHeight;

            if (width <= 0 || height <= 0)
            {
                Debug.LogWarning("SceneView size is invalid.");
                return false;
            }

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24);
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = sceneView.camera.targetTexture;

            try
            {
                sceneView.camera.targetTexture = rt;
                sceneView.camera.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                byte[] bytes = tex.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
            }
            finally
            {
                sceneView.camera.targetTexture = previous;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);
                DestroyImmediate(tex);
            }

            return true;
        }

        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Screenshot";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(name.Length);

            foreach (char ch in name)
            {
                bool isInvalid = false;
                for (int i = 0; i < invalidChars.Length; i++)
                {
                    if (ch == invalidChars[i])
                    {
                        isInvalid = true;
                        break;
                    }
                }

                if (!isInvalid)
                {
                    builder.Append(ch);
                }
            }

            string result = builder.ToString();
            return string.IsNullOrEmpty(result) ? "Screenshot" : result;
        }
    }
}
