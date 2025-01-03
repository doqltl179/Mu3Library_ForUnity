using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Mu3Library.Editor.Window {
    public class Mu3Window<T> : EditorWindow where T : Mu3WindowProperty {
        protected const string PropertyObjectFolderPath = "Assets/22_Tool/Editor";

        private List<T> windowPropertyList;
        
        protected T currentWindowProperty = null;

        protected bool isRefreshed = false;

        protected Vector2 windowScreenPos;

        #region GUIStyle

        protected GUIStyle header1Style = null;
        protected GUIStyle header2Style = null;
        protected GUIStyle header3Style = null;
        protected GUIStyle normalMiddleLeftStyle = null;

        #endregion



        protected virtual void OnBecameVisible() {
            InitializeProperties();


        }

        protected virtual void OnBecameInvisible() {

        }

        private void OnStateInfoChanged(string title = "", string info = "", float progress = 0) {
            if(string.IsNullOrEmpty(title) && string.IsNullOrEmpty(info)) {
                ClearProgressBar();
            }
            else {
                DisplayProgressBar(title, info, progress);
            }
        }

        private void InitializeProperties() {
            if(currentWindowProperty == null || windowPropertyList == null || windowPropertyList.Count == 0) {
                isRefreshed = false;

                currentWindowProperty = null;

                //string typeString = typeof(T).Name;
                //string nameString = $"{typeString}Object";
                //windowPropertyList = FileManager.LoadAssetsWithTypeAndName<T>(typeString, nameString);

                //MonoScript.FromScriptableObject((ScriptableObject)this)

                //if(windowPropertyList.Count > 0) {
                //    currentWindowProperty = windowPropertyList[0];
                //}
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

        #region Util Style
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

        protected void DrawHeader1(string headText, bool insertSpaceOnUpSpaceOfHeader, string btnText, Action<bool> onClicked, params GUILayoutOption[] options) {
            if(insertSpaceOnUpSpaceOfHeader) GUILayout.Space(header1Style.fontSize);

            DrawHorizontal(() => {
                DrawVertical(() => {
                    onClicked?.Invoke(GUILayout.Button(btnText, options));
                }, 8, 0);

                GUILayout.Space(-20);
                GUILayout.Label(headText, header1Style, GUILayout.Width(100));

                GUILayout.FlexibleSpace();
            });

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

        protected void DrawVertical(Action content, float beginSpace = 0, float endSpace = 0) {
            GUILayout.BeginVertical();

            DrawLayoutStruct(content, beginSpace, endSpace);

            GUILayout.EndVertical();
        }

        protected void DrawHorizontal(Action content, float beginSpace = 0, float endSpace = 0) {
            GUILayout.BeginHorizontal();

            DrawLayoutStruct(content, beginSpace, endSpace);

            GUILayout.EndHorizontal();
        }

        private void DrawLayoutStruct(Action content, float beginSpace = 0, float endSpace = 0) {
            if(beginSpace != 0) GUILayout.Space(beginSpace);

            content?.Invoke();

            if(endSpace != 0) GUILayout.Space(endSpace);
        }

        protected void DrawLayoutStructWithState(Action contentTrue, Action contentFalse, Func<bool> stateFunc) {
            if(stateFunc == null) {
                GUILayout.Label("StateFunc is NULL..");

                return;
            }

            if(stateFunc()) {
                contentTrue?.Invoke();
            }
            else {
                contentFalse?.Invoke();
            }
        }

        protected void DrawAsReadOnlyField<UO>(UO obj, Func<bool> stateFunc = null) where UO : Object {
            DrawAsReadOnly(() => {
                EditorGUILayout.ObjectField(obj, typeof(UO), false);
            }, stateFunc);
        }

        protected void DrawAsReadOnlyField(SerializedProperty property, Func<bool> stateFunc = null) {
            DrawAsReadOnly(() => {
                EditorGUILayout.PropertyField(property);
            }, stateFunc);
        }

        /// <summary>
        /// If 'stateFunc() == null' or 'stateFunc() == true', content will be draw as readonly.
        /// </summary>
        protected void DrawAsReadOnly(Action content, Func<bool> stateFunc = null) {
            if(stateFunc == null || stateFunc()) {
                GUI.enabled = false;
                content?.Invoke();
                GUI.enabled = true;
            }
            else {
                content?.Invoke();
            }
        }

        protected bool DrawPropertyArea() {
            // 'DrawLayoutStructWithState'를 사용하려 했으나,
            // 'return'을 날려 'OnGUI'를 종료해야 하는 코드가 있기 때문에 사용하지 않음.
            if(currentWindowProperty == null) {
                EditorGUILayout.LabelField("CurrentWindowProperty is NULL...");

                return false;
            }
            else {
                DrawHorizontal(() => {
                    if(GUILayout.Button("Refresh")) {
                        currentWindowProperty = null;

                        InitializeProperties();
                    }

                    DrawAsReadOnlyField(currentWindowProperty);

                    GUILayout.FlexibleSpace();
                });

                return true;
            }
        }
        #endregion

        protected string GetPathOfGameObjectOnHierarchy(Transform go) {
            StringBuilder result = new StringBuilder();

            Transform root = go;
            while(root != null) {
                result.Insert(0, $"/{root.name}");

                root = root.parent;
            }

            return result.ToString();
        }
    }
}