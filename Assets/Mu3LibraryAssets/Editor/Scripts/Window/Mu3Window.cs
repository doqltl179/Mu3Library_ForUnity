using System.Collections.Generic;
using Mu3Library.Editor.FileUtil;
using Mu3Library.Editor.Window.Drawer;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Window
{
    public abstract class Mu3Window : EditorWindow
    {
        protected const string MenuRoot = "Mu3Library/Windows";

        #region Window Propertyies

        protected Rect _windowRect;
        protected float _windowWidth;
        protected float _windowHeight;

        protected Vector2 _windowScrollPos;

        #endregion

        [SerializeField] private bool _foldout_debug;

        [SerializeField] private DrawerListDrawer _drawerListDrawer = null;



        protected virtual void OnBecameVisible()
        {
            if (_drawerListDrawer == null)
            {
                LoadDrawerListDrawer();
                if (_drawerListDrawer == null)
                {
                    return;
                }
            }
            _drawerListDrawer.OnBecameVisible();

            foreach(Mu3WindowDrawer drawer in _drawerListDrawer.Drawers)
            {
                if (drawer == null)
                {
                    continue;
                }
                
                drawer.OnBecameVisible();
            }
        }

        protected virtual void OnBecameInvisible()
        {
            if (_drawerListDrawer == null)
            {
                return;
            }
            _drawerListDrawer.OnBecameInvisible();

            foreach(Mu3WindowDrawer drawer in _drawerListDrawer.Drawers)
            {
                if (drawer == null)
                {
                    continue;
                }
                
                drawer.OnBecameInvisible();
            }
        }

        private void OnGUI()
        {
            if (_drawerListDrawer == null)
            {
                return;
            }

            _windowRect = position;
            _windowWidth = _windowRect.width;
            _windowHeight = _windowRect.height;

            _windowScrollPos = EditorGUILayout.BeginScrollView(_windowScrollPos);

            _drawerListDrawer.OnGUIHeader();
            if (_drawerListDrawer.Foldout)
            {
                _drawerListDrawer.OnGUIBody();
            }

            foreach (Mu3WindowDrawer drawer in _drawerListDrawer.Drawers)
            {
                if (drawer == null)
                {
                    continue;
                }
                
                drawer.OnGUIHeader();
                if (drawer.Foldout)
                {
                    GUILayout.Space(10);
                    drawer.OnGUIBody();
                    GUILayout.Space(10);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void LoadDrawerListDrawer()
        {
            string scriptAssetPath = FileFinder.GetAssetPathFromScriptableObject(this);
            if (string.IsNullOrEmpty(scriptAssetPath))
            {
                return;
            }

            string directory = "";
            string extension = "";
            FilePathConvertor.SplitPath(scriptAssetPath, out directory, out _, out extension);
            if (string.IsNullOrEmpty(directory) || extension != "cs")
            {
                return;
            }

            List<DrawerListDrawer> ds = FileFinder.LoadAllAssetsAtPath<DrawerListDrawer>(directory);
            _drawerListDrawer = ds.Count > 0 ?
                ds[0] :
                CreateWindowDrawerObject<DrawerListDrawer>(directory, DrawerListDrawer.FileName);
        }
        
        private T CreateWindowDrawerObject<T>(string directory, string fileName) where T : Mu3WindowDrawer
        {
            return FileCreator.CreateScriptableObject<T>(directory, fileName);
        }
    }
}