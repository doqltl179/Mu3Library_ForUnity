using Mu3Library.Editor.FileUtil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Window {
    public abstract class Mu3Window<T> : EditorWindow where T : Mu3WindowProperty {
        protected T currentWindowProperty = null;

        protected bool isRefreshed = false;

        protected Vector2 windowScrollPos;

        #region Window Propertyies

        protected Rect windowRect;
        protected float windowWidth;
        protected float windowHeight;

        #endregion

        #region GUIStyle

        protected GUIStyle header1Style = null;
        protected GUIStyle header2Style = null;
        protected GUIStyle header3Style = null;

        protected GUIStyle toggleIcon1Style = null;
        protected GUIStyle toggleIcon2Style = null;

        #endregion



        protected virtual void OnBecameVisible() {
            InitializeProperties();


        }

        protected virtual void OnBecameInvisible() {

        }

        protected virtual void OnGUI() {
            DrawPropertyObjectArea();

            // PropertyObject가 존재하지 않는다면 GUI를 그리지 않는다.
            if(currentWindowProperty == null) {
                return;
            }

            windowRect = position;
            windowWidth = windowRect.width;
            windowHeight = windowRect.height;

            windowScrollPos = EditorGUILayout.BeginScrollView(windowScrollPos);

            OnGUIFunc();

            EditorGUILayout.EndScrollView();
        }

        protected abstract void OnGUIFunc();

        private void InitializeProperties() {
            if(currentWindowProperty == null) {
                isRefreshed = false;

                string windowScriptAssetPath = FileFinder.GetAssetPathFromScriptableObject(this);
                if(!string.IsNullOrEmpty(windowScriptAssetPath)) {
                    string directory = "";
                    string fileName = "";
                    string extension = "";
                    FilePathConvertor.SplitPathToDirectoryAndFileNameAndExtension(windowScriptAssetPath, out directory, out fileName, out extension);

                    // cs 파일 확인
                    if(!string.IsNullOrEmpty(directory) && extension == "cs") {
                        string propertyTypeString = typeof(T).Name;

                        List<T> propertyObjs = FileFinder.LoadAllAssetsAtPath<T>(directory, "", propertyTypeString, "");
                        // PropertyObject가 존재하지 않으면 생성한다.
                        if(propertyObjs.Count == 0) {
                            //Debug.Log($"Property Object not found. directory: {directory}");

                            string propertyObjName = $"{this.GetType().Name}PropertyObject";

                            currentWindowProperty = FileCreator.CreateScriptableObject<T>(directory, propertyObjName);
                        }
                        else if(propertyObjs.Count == 1) {
                            currentWindowProperty = propertyObjs[0];
                        }
                        else {
                            Debug.LogWarning($"PropertyObject found. But, there are exist more than two PropertyObjects.");

                            currentWindowProperty = propertyObjs[0];
                        }
                    }
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

                toggleIcon1Style = new GUIStyle() {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(12, 12, 8, 8),
                    fixedHeight = 32,
                    normal = new GUIStyleState() {
                        textColor = Color.white,
                    },
                };
                toggleIcon2Style = new GUIStyle() {
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

        #region Struct Util Style
        protected void DisplayProgressBar(string title, string info, float progress) {
            EditorUtility.DisplayProgressBar(title, info, progress);
        }

        protected void DisplayCancelableProgressBar(string title, string info, float progress) {
            EditorUtility.DisplayCancelableProgressBar(title, info, progress);
        }

        protected void ClearProgressBar() {
            EditorUtility.ClearProgressBar();
        }

        protected void DrawHeader1(string label, bool insertSpaceOnUpSpaceOfHeader = false) {
            if(insertSpaceOnUpSpaceOfHeader) GUILayout.Space(header1Style.fontSize);
            EditorGUILayout.LabelField(label, header1Style);
            GUILayout.Space(header1Style.fontSize);

            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 1), EditorGUIUtility.whiteTexture);
            GUILayout.Space(10);
        }

        protected void DrawFoldoutHeader1(string label, ref bool foldout, bool insertSpaceOnUpSpaceOfHeader = false) {
            if(insertSpaceOnUpSpaceOfHeader) GUILayout.Space(header1Style.fontSize);

            GUILayout.BeginHorizontal();

            GUILayout.Space(header1Style.fontSize);

            // 토글 마크 그리기
            toggleIcon1Style.normal.textColor = foldout ? Color.green : Color.red;
            toggleIcon1Style.fixedHeight = header1Style.fixedHeight;
            EditorGUILayout.LabelField(foldout ? "▼" : "▶", toggleIcon1Style, GUILayout.Width(16), GUILayout.Height(header1Style.fixedHeight));

            // 토글 텍스트 작성
            foldout = GUILayout.Toggle(foldout, label, header1Style);

            GUILayout.EndHorizontal();

            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 1), EditorGUIUtility.whiteTexture);
            GUILayout.Space(10);
        }

        protected void DrawHeader2(string label, bool insertSpaceOnUpSpaceOfHeader = false, bool insertSpaceOnDownSpaceOfHeader = false) {
            if(insertSpaceOnUpSpaceOfHeader) GUILayout.Space(header2Style.fontSize);
            EditorGUILayout.LabelField(label, header2Style);
            if(insertSpaceOnDownSpaceOfHeader) GUILayout.Space(header2Style.fontSize);
        }

        protected void DrawFoldoutHeader2(string label, ref bool foldout, bool insertSpaceOnUpSpaceOfHeader = false, bool insertSpaceOnDownSpaceOfHeader = false) {
            if(insertSpaceOnUpSpaceOfHeader) GUILayout.Space(header2Style.fontSize);

            GUILayout.BeginHorizontal();

            GUILayout.Space(header2Style.fontSize);

            // 토글 마크 그리기
            toggleIcon2Style.normal.textColor = foldout ? Color.green : Color.red;
            toggleIcon2Style.fixedHeight = header2Style.fixedHeight;
            EditorGUILayout.LabelField(foldout ? "▼" : "▶", toggleIcon2Style, GUILayout.Width(16), GUILayout.Height(header2Style.fixedHeight));

            // 토글 텍스트 작성
            foldout = GUILayout.Toggle(foldout, label, header2Style);

            GUILayout.EndHorizontal();

            if(insertSpaceOnDownSpaceOfHeader) GUILayout.Space(header2Style.fontSize);
        }

        protected void DrawHeader3(string label, bool insertSpaceOnUpSpaceOfHeader = false, bool insertSpaceOnDownSpaceOfHeader = false) {
            if(insertSpaceOnUpSpaceOfHeader) GUILayout.Space(header3Style.fontSize);
            EditorGUILayout.LabelField(label, header3Style);
            if(insertSpaceOnDownSpaceOfHeader) GUILayout.Space(header3Style.fontSize);
        }

        protected void DrawGrid(List<Action> contents, int columnCount, float leftSpace, float rightSpace, float upSpace, float downSpace) {
            if(contents == null || contents.Count == 0) {
                return;
            }

            GUILayout.BeginVertical();

            GUILayout.Space(upSpace);

            float cellWidth = (windowWidth - (leftSpace + rightSpace)) / columnCount;

            int rowCount = Mathf.CeilToInt(contents.Count / (float)columnCount);
            for(int r = 0; r < rowCount; r++) {
                GUILayout.BeginHorizontal();

                GUILayout.Space(leftSpace);

                for(int c = 0; c < columnCount; c++) {
                    int contentIdx = r * columnCount + c;
                    // Action 개수 부족
                    if(contentIdx >= contents.Count) {
                        GUILayout.FlexibleSpace();
                    }
                    else {
                        GUILayout.BeginVertical(GUILayout.Width(cellWidth));
                        contents[contentIdx]?.Invoke();
                        GUILayout.EndVertical();
                    }
                }

                GUILayout.Space(rightSpace);

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(downSpace);

            GUILayout.EndVertical();
        }

        protected void DrawStruct(Action content, float leftSpace, float rightSpace, float upSpace, float downSpace) {
            if(content == null) {
                return;
            }

            GUILayout.BeginHorizontal();

            GUILayout.Space(leftSpace);

            GUILayout.BeginVertical();

            GUILayout.Space(upSpace);
            content?.Invoke();
            GUILayout.Space(downSpace);

            GUILayout.EndVertical();

            GUILayout.Space(rightSpace);

            GUILayout.EndHorizontal();
        }

        protected void DrawVertical(Action content, float beginSpace, float endSpace) {
            if(content == null) {
                return;
            }

            GUILayout.BeginVertical();

            GUILayout.Space(beginSpace);
            content?.Invoke();
            GUILayout.Space(endSpace);

            GUILayout.EndVertical();
        }

        protected void DrawHorizontal(Action content, float beginSpace, float endSpace) {
            if(content == null) {
                return;
            }

            GUILayout.BeginHorizontal();

            GUILayout.Space(beginSpace);
            content?.Invoke();
            GUILayout.Space(endSpace);

            GUILayout.EndHorizontal();
        }

        protected void DrawPropertyObjectArea() {
            GUILayout.BeginHorizontal();

            if(GUILayout.Button("Refresh")) {
                currentWindowProperty = null;

                InitializeProperties();
            }

            if(currentWindowProperty == null) {
                EditorGUILayout.LabelField("CurrentWindowProperty is NULL...");
            }
            else {
                GUI.enabled = false;
                EditorGUILayout.ObjectField(currentWindowProperty, typeof(T), false);
                GUI.enabled = true;
            }

            GUILayout.FlexibleSpace();

            GUILayout.EndHorizontal();
        }

        protected void DrawPropertiesForDebug() {
            bool foldout_debug = currentWindowProperty.Foldout_Debug;
            DrawFoldoutHeader1("Debug Properties", ref foldout_debug);

            if(foldout_debug) {
                EditorGUILayout.LabelField($"windowScrollPos -> ({windowScrollPos.x:F2}, {windowScrollPos.y:F2})");

                EditorGUILayout.LabelField($"windowRect -> pos: ({windowRect.x:F2}, {windowRect.y:F2}), size: ({windowRect.width:F2}, {windowRect.height:F2})");
            }

            GUILayout.Space(20);

            currentWindowProperty.Foldout_Debug = foldout_debug;
        }
        #endregion

        protected string GetPathOfGameObjectOnHierarchy(Transform go) {
            StringBuilder result = new StringBuilder();

            Transform root = go;
            while(root != null) {
                if(root.parent != null) {
                    result.Insert(0, $"/{root.name}");
                }
                else {
                    result.Insert(0, $"{root.name}");
                }

                root = root.parent;
            }

            return result.ToString();
        }
    }
}