using System;
using System.Collections.Generic;
using Mu3Library.Editor.FileUtil;
using Mu3Library.Editor.Window.Drawer;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Mu3Library.Editor.Window
{
    public abstract class Mu3Window : EditorWindow
    {
        protected const string MenuRoot = "Mu3Library/Windows";

        /// <summary>
        /// The project folder that keeps the drawer lists of windows whose scripts sit in packages, one asset per window type.
        /// </summary>
        internal const string ProjectDrawerListFolder = "Assets/Mu3Library/Editor/DrawerLists";

        #region Window Properties

        protected Rect _windowRect;
        protected float _windowWidth;
        protected float _windowHeight;

        protected Vector2 _windowScrollPos;

        #endregion

        [SerializeField] private bool _foldout_debug;

        [SerializeField] private DrawerListDrawer _drawerListDrawer = null;
        internal DrawerListDrawer DrawerList => _drawerListDrawer;



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
            _drawerListDrawer = LoadDrawerList(GetType(), scriptAssetPath);
        }

        /// <summary>
        /// Finds the drawer list of a window, or creates it.
        /// <br/> A window whose script sits under <c>Assets</c> keeps its list next to the script.
        /// <br/> A window whose script sits in a package reads the list next to the script while the package can be
        /// changed (embedded, or a local folder referenced with <c>file:</c>). Otherwise the list lives in
        /// <see cref="ProjectDrawerListFolder"/>, named after the window type, since a package installed from a Git URL,
        /// a registry, or a tarball is read-only; a list that such a package ships is copied there the first time.
        /// </summary>
        /// <param name="windowType"> The window type, which names the list kept in the project </param>
        /// <param name="scriptAssetPath"> The asset path of the window script </param>
        /// <returns> The drawer list, or null when the path is not a script </returns>
        internal static DrawerListDrawer LoadDrawerList(Type windowType, string scriptAssetPath)
        {
            if (windowType == null || string.IsNullOrEmpty(scriptAssetPath))
            {
                return null;
            }

            FilePathConvertor.SplitPath(scriptAssetPath, out string directory, out _, out string extension);
            if (string.IsNullOrEmpty(directory) || extension != "cs")
            {
                return null;
            }

            List<DrawerListDrawer> scriptFolderLists = FileFinder.LoadAllAssetsAtPath<DrawerListDrawer>(directory);
            DrawerListDrawer scriptFolderList = scriptFolderLists.Count > 0 ? scriptFolderLists[0] : null;

            if (IsInAssetsFolder(directory))
            {
                return scriptFolderList != null ?
                    scriptFolderList :
                    FileCreator.CreateScriptableObject<DrawerListDrawer>(directory, DrawerListDrawer.FileName);
            }

            if (scriptFolderList != null && IsInWritablePackage(directory))
            {
                return scriptFolderList;
            }

            string projectListPath = GetProjectDrawerListPath(windowType);
            DrawerListDrawer projectList = AssetDatabase.LoadAssetAtPath<DrawerListDrawer>(projectListPath);
            if (projectList != null)
            {
                return projectList;
            }

            CreateFolders(ProjectDrawerListFolder);

            if (scriptFolderList != null &&
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(scriptFolderList), projectListPath))
            {
                return AssetDatabase.LoadAssetAtPath<DrawerListDrawer>(projectListPath);
            }

            return FileCreator.CreateScriptableObject<DrawerListDrawer>(ProjectDrawerListFolder, windowType.FullName);
        }

        /// <summary>
        /// The asset path of the drawer list that the project keeps for a window whose script sits in a package.
        /// </summary>
        internal static string GetProjectDrawerListPath(Type windowType)
        {
            return $"{ProjectDrawerListFolder}/{windowType.FullName}.asset";
        }

        private static bool IsInAssetsFolder(string assetPath)
        {
            return assetPath == "Assets" || assetPath.StartsWith("Assets/");
        }

        private static bool IsInWritablePackage(string assetPath)
        {
            PackageInfo package = PackageInfo.FindForAssetPath(assetPath);

            return package != null &&
                (package.source == PackageSource.Embedded || package.source == PackageSource.Local);
        }

        private static void CreateFolders(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            int lastSlash = folderPath.LastIndexOf('/');
            string parentFolder = folderPath.Substring(0, lastSlash);
            CreateFolders(parentFolder);
            AssetDatabase.CreateFolder(parentFolder, folderPath.Substring(lastSlash + 1));
        }
    }
}