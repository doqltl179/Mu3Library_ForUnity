using System;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Mu3Library.Editor.Window.Drawer
{
    [Serializable]
    public abstract class Mu3WindowDrawer : ScriptableObject
    {
        protected const string MenuRoot = "Mu3Library/Windows Drawer";

        [SerializeField] protected bool _foldout = true;
        public bool Foldout => _foldout;

        #region GUIStyle

        [Header("Base Styles")]

        [SerializeField] protected GUIStyle _header1Style = null;
        [SerializeField] protected GUIStyle _header2Style = null;
        [SerializeField] protected GUIStyle _header3Style = null;

        [SerializeField] protected GUIStyle _toggleIcon1Style = null;
        [SerializeField] protected GUIStyle _toggleIcon2Style = null;

        #endregion



        protected virtual void Reset()
        {
            InitializeBaseStyles(true);
        }

        public virtual void OnBecameVisible()
        {
            InitializeBaseStyles();
        }

        public virtual void OnBecameInvisible() { }

        public abstract void OnGUIHeader();
        public abstract void OnGUIBody();

        private void InitializeBaseStyles(bool force = false)
        {
            if (force || _header1Style == null)
            {
                _header1Style = new GUIStyle()
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(24, 24, 12, 12),
                    fixedHeight = 48,
                    normal = new GUIStyleState()
                    {
                        textColor = Color.white,
                    },
                };
            }
            if (force || _header2Style == null)
            {
                _header2Style = new GUIStyle()
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(16, 16, 8, 8),
                    fixedHeight = 32,
                    normal = new GUIStyleState()
                    {
                        textColor = Color.white,
                    },
                };
            }
            if (force || _header3Style == null)
            {
                _header3Style = new GUIStyle()
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(11, 11, 5, 5),
                    fixedHeight = 22,
                    normal = new GUIStyleState()
                    {
                        textColor = Color.white,
                    },
                };
            }

            if (force || _toggleIcon1Style == null)
            {
                _toggleIcon1Style = new GUIStyle()
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fixedHeight = 48,
                    normal = new GUIStyleState()
                    {
                        textColor = Color.white,
                    },
                };
            }
            if (force || _toggleIcon2Style == null)
            {
                _toggleIcon2Style = new GUIStyle()
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    fixedHeight = 32,
                    normal = new GUIStyleState()
                    {
                        textColor = Color.white,
                    },
                };
            }
        }

        #region Struct Util Style
        protected void DisplayProgressBar(string title, string info, float progress)
        {
            EditorUtility.DisplayProgressBar(title, info, progress);
        }

        protected void DisplayCancelableProgressBar(string title, string info, float progress)
        {
            EditorUtility.DisplayCancelableProgressBar(title, info, progress);
        }

        protected void ClearProgressBar()
        {
            EditorUtility.ClearProgressBar();
        }

        protected void DrawHeader1(string label, bool insertSpaceOnUpSpaceOfHeader = false, bool insertSpaceOnDownSpaceOfHeader = false)
        {
            if (insertSpaceOnUpSpaceOfHeader) GUILayout.Space(_header1Style.fontSize);
            GUILayout.Label(label, _header1Style, GUILayout.ExpandWidth(false));
            GUILayout.Space(_header1Style.fontSize);

            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 1), EditorGUIUtility.whiteTexture);
            if (insertSpaceOnDownSpaceOfHeader) GUILayout.Space(_header1Style.fontSize);GUILayout.Space(10);
        }

        protected void DrawFoldoutHeader1(string label, ref bool foldout, bool insertSpaceOnUpSpaceOfHeader = false, bool insertSpaceOnDownSpaceOfHeader = false)
        {
            if (insertSpaceOnUpSpaceOfHeader) GUILayout.Space(_header1Style.fontSize);

            GUILayout.BeginHorizontal();

            GUILayout.Space(_header1Style.fontSize);

            // 토글 마크 그리기
            _toggleIcon1Style.normal.textColor = foldout ? Color.green : Color.red;
            _toggleIcon1Style.fixedHeight = _header1Style.fixedHeight;
            foldout = GUILayout.Toggle(foldout, foldout ? "▼" : "▶", _toggleIcon1Style, GUILayout.ExpandWidth(false));

            // 토글 텍스트 작성
            foldout = GUILayout.Toggle(foldout, label, _header1Style, GUILayout.ExpandWidth(false));

            GUILayout.EndHorizontal();

            GUI.DrawTexture(EditorGUILayout.GetControlRect(false, 1), EditorGUIUtility.whiteTexture);
            if (insertSpaceOnDownSpaceOfHeader) GUILayout.Space(_header1Style.fontSize);
        }

        protected void DrawHeader2(string label, bool insertSpaceOnUpSpaceOfHeader = false, bool insertSpaceOnDownSpaceOfHeader = false)
        {
            if (insertSpaceOnUpSpaceOfHeader) GUILayout.Space(_header2Style.fontSize);
            GUILayout.Label(label, _header2Style, GUILayout.ExpandWidth(false));
            if (insertSpaceOnDownSpaceOfHeader) GUILayout.Space(_header2Style.fontSize);
        }

        protected void DrawFoldoutHeader2(string label, ref bool foldout, bool insertSpaceOnUpSpaceOfHeader = false, bool insertSpaceOnDownSpaceOfHeader = false)
        {
            if (insertSpaceOnUpSpaceOfHeader) GUILayout.Space(_header2Style.fontSize);

            GUILayout.BeginHorizontal();

            GUILayout.Space(_header2Style.fontSize);

            // 토글 마크 그리기
            _toggleIcon2Style.normal.textColor = foldout ? Color.green : Color.red;
            foldout = GUILayout.Toggle(foldout, foldout ? "▼" : "▶", _toggleIcon2Style, GUILayout.ExpandWidth(false));

            // 토글 텍스트 작성
            foldout = GUILayout.Toggle(foldout, label, _header2Style, GUILayout.ExpandWidth(false));

            GUILayout.EndHorizontal();

            if (insertSpaceOnDownSpaceOfHeader) GUILayout.Space(_header2Style.fontSize);
        }

        protected void DrawHeader3(string label, bool insertSpaceOnUpSpaceOfHeader = false, bool insertSpaceOnDownSpaceOfHeader = false)
        {
            if (insertSpaceOnUpSpaceOfHeader) GUILayout.Space(_header3Style.fontSize);
            GUILayout.Label(label, _header3Style, GUILayout.ExpandWidth(false));
            if (insertSpaceOnDownSpaceOfHeader) GUILayout.Space(_header3Style.fontSize);
        }

        protected void DrawToggleArea(string label, ref bool toggle)
        {
            toggle = EditorGUILayout.ToggleLeft(label, toggle);
        }

        protected void DrawObjectAreaForProjectObject<Obj>(ref Obj obj, float size) where Obj : Object
        {
            obj = EditorGUILayout.ObjectField(obj, typeof(Obj), false, GUILayout.Width(size), GUILayout.Height(size)) as Obj;
        }

        protected void DrawObjectAreaForProjectObject<Obj>(ref Obj obj) where Obj : Object
        {
            obj = EditorGUILayout.ObjectField(obj, typeof(Obj), false) as Obj;
        }

        protected Obj DrawObjectAreaForProjectObject<Obj>(Obj obj) where Obj : Object
        {
            return EditorGUILayout.ObjectField(obj, typeof(Obj), false) as Obj;
        }

        protected void DrawObjectAreaForHierarchyObject<Obj>(ref Obj obj) where Obj : Object
        {
            obj = EditorGUILayout.ObjectField(obj, typeof(Obj), true) as Obj;

            if (obj != null && AssetDatabase.Contains(obj))
            {
                Debug.LogError($"This Object not exist in Hierarchy.");

                obj = null;
            }
        }

        protected void DrawStruct(Action content, float leftSpace, float rightSpace, float upSpace, float downSpace)
        {
            if (content == null)
            {
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

        protected void DrawVertical(Action content, float beginSpace, float endSpace)
        {
            if (content == null)
            {
                return;
            }

            GUILayout.BeginVertical();

            GUILayout.Space(beginSpace);
            content?.Invoke();
            GUILayout.Space(endSpace);

            GUILayout.EndVertical();
        }

        protected void DrawHorizontal(Action content, float beginSpace, float endSpace)
        {
            if (content == null)
            {
                return;
            }

            GUILayout.BeginHorizontal();

            GUILayout.Space(beginSpace);
            content?.Invoke();
            GUILayout.Space(endSpace);

            GUILayout.EndHorizontal();
        }
        #endregion
    }
}