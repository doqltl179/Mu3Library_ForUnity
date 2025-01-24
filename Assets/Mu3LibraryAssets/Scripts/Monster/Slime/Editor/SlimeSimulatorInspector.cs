#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;


namespace Mu3Library.Monster {
    [CustomEditor(typeof(SlimeSimulator))]
    public class SlimeSimulatorInspector : Editor {
        private GUIStyle header1Style = null;
        private GUIStyle header2Style = null;
        private GUIStyle header3Style = null;

        private int bodyDensityWidth = 3;
        private int bodyDensityHeight = 3;
        private float bodyRadius = 0.5f;
        private string bodyGenerateDirectory = Application.dataPath;
        private string bodyGenerateFileName = "SlimeBody";



        private void OnEnable() {
            InitializeProperties();
        }

        public override void OnInspectorGUI() {
            // 기본 Inspector 표시
            DrawDefaultInspector();

            GUILayout.Space(10);

            GenerateSlimeBody();
        }

        private void InitializeProperties() {
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
        }

        #region Draw Func

        #region Generate Slime Body
        private void GenerateSlimeBody() {
            GUILayoutOption normalButtonHeight = GUILayout.Height(30);

            DrawHeader1("Generate Slime Body");

            bodyDensityWidth = EditorGUILayout.IntSlider("Body Density Width", bodyDensityWidth, 3, 30);
            bodyDensityHeight = EditorGUILayout.IntSlider("Body Density Height", bodyDensityHeight, 3, 30);
            bodyRadius = EditorGUILayout.Slider("Body Radius", bodyRadius, 0.1f, 5.0f);

            if(GUILayout.Button("Generate Body", normalButtonHeight)) {
                // 타겟 스크립트 가져오기
                SlimeSimulator script = (SlimeSimulator)target;

                MeshFilter meshFilter = script.GetComponent<MeshFilter>();
                if(meshFilter == null) {
                    Debug.LogError("MeshFilter component not found.");

                    return;
                }

                Mesh bodyMesh = script.GenerateBodyMesh(bodyDensityWidth, bodyDensityHeight, bodyRadius);
                if(bodyMesh != null) {
                    // Scene에 깡으로 저장한다.
                    meshFilter.sharedMesh = bodyMesh;

                    /*
                    FBX로 저장하려고 했으나, FBX Exporter를 사용해야 하기 때문에 package 의존성 문제로 보류.
                    */
                    //string panelTitle = "ScreenShot Save Folder";
                    //string directory = bodyGenerateDirectory;
                    //string defaultFolderName = bodyGenerateFileName;
                    //string saveFolderDirectory = EditorUtility.SaveFolderPanel(panelTitle, directory, defaultFolderName);
                    //if(!string.IsNullOrEmpty(saveFolderDirectory)) {
                    //    string extension = "";
                    //    FilePathConvertor.SplitPathToDirectoryAndFileNameAndExtension(saveFolderDirectory, out bodyGenerateDirectory, out bodyGenerateFileName, out extension);


                    //}
                }
            }
        }
        #endregion

        #endregion

        #region Struct Util Style
        protected void DrawHeader1(string label, bool insertSpaceOnUpSpaceOfHeader = false) {
            if(insertSpaceOnUpSpaceOfHeader) GUILayout.Space(header1Style.fontSize);
            EditorGUILayout.LabelField(label, header1Style);
            GUILayout.Space(header1Style.fontSize);

            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 1), EditorGUIUtility.whiteTexture);
            GUILayout.Space(10);
        }

        protected void DrawHeader2(string label, bool insertSpaceOnUpSpaceOfHeader = false, bool insertSpaceOnDownSpaceOfHeader = false) {
            if(insertSpaceOnUpSpaceOfHeader) GUILayout.Space(header2Style.fontSize);
            EditorGUILayout.LabelField(label, header2Style);
            if(insertSpaceOnDownSpaceOfHeader) GUILayout.Space(header2Style.fontSize);
        }

        protected void DrawHeader3(string label, bool insertSpaceOnUpSpaceOfHeader = false, bool insertSpaceOnDownSpaceOfHeader = false) {
            if(insertSpaceOnUpSpaceOfHeader) GUILayout.Space(header3Style.fontSize);
            EditorGUILayout.LabelField(label, header3Style);
            if(insertSpaceOnDownSpaceOfHeader) GUILayout.Space(header3Style.fontSize);
        }
        #endregion
    }
}
#endif