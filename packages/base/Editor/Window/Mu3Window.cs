using System;
using System.Collections.Generic;
using System.Linq;
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
        /// The project folder where a window whose script sits in a package creates its drawer list, one asset per window
        /// type. The list may be moved or renamed anywhere under <c>Assets</c> afterwards.
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
        /// <br/> A window whose script sits under <c>Assets</c> keeps its list next to the script. That list records
        /// the window type, so the user may move or rename it anywhere under <c>Assets</c>: the window reads the list
        /// next to its script first, then the list that records its type, the first in path order with a warning when
        /// several do, then a list in a folder below the script, and creates a new list next to the script only when
        /// none of these exists. A list next to the script that does not record the type yet, such as one imported
        /// with a sample before lists recorded it, is given it when the window loads it, so it can be moved from then
        /// on; a list read from a folder below the script keeps what it records, since it may belong to a window whose
        /// script sits there.
        /// <br/> A window whose script sits in a package reads the list next to the script while the package can be
        /// changed (embedded, or a local folder referenced with <c>file:</c>). Otherwise the list lives in
        /// <see cref="ProjectDrawerListFolder"/>, named after the window type, since a package installed from a Git URL,
        /// a registry, or a tarball is read-only; a list that such a package ships next to the script is copied there
        /// the first time. Only the list is copied: the drawers it names stay in the package, so their own settings
        /// are not saved in the project.
        /// <br/> The project list records the window type it belongs to, so the user may move or rename it anywhere
        /// under <c>Assets</c>. The list at <see cref="GetProjectDrawerListPath"/> comes first; when no list of the
        /// window is there, the window reads the list that records its type, the first in path order with a warning
        /// when several do, and creates a new list only when none does. A list at that path that does not record the
        /// type yet, such as one created before lists recorded it, is given it when the window loads it.
        /// <br/> A window never reads a list that records another window type. A list that records a name no window
        /// type has, such as the old name of a renamed window class, counts as a list that records nothing.
        /// <br/> A window never replaces an asset where it creates its list, such as another window's list that the
        /// user moved there and renamed: it creates the list under the next free name instead, for example
        /// <c>DrawerList 1.asset</c>, and logs a warning, so the other asset and its settings stay. The window keeps
        /// reading the list it created, as it records the window type.
        /// </summary>
        /// <param name="windowType"> The window type, which the list records and which names a project list </param>
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

            // The search covers the folders below the script too. It leaves out the lists of other windows: those
            // the project keeps for windows in packages in the project list folder, and those that record another
            // window type. A list that records this type comes before one that records nothing, then path order.
            List<DrawerListDrawer> scriptFolderLists = FileFinder.LoadAllAssetsAtPath<DrawerListDrawer>(directory)
                .Where(list => !AssetDatabase.GetAssetPath(list).StartsWith(ProjectDrawerListFolder + "/") &&
                    MayBelongTo(list, windowType))
                .OrderBy(list => list.WindowTypeName == windowType.FullName ? 0 : 1)
                .ThenBy(list => AssetDatabase.GetAssetPath(list), StringComparer.Ordinal)
                .ToList();
            DrawerListDrawer listNextToScript = scriptFolderLists
                .FirstOrDefault(list => GetFolder(AssetDatabase.GetAssetPath(list)) == directory);
            DrawerListDrawer listBelowScript = scriptFolderLists
                .FirstOrDefault(list => GetFolder(AssetDatabase.GetAssetPath(list)) != directory);

            if (FilePathConvertor.IsAssetPath(directory))
            {
                if (listNextToScript != null)
                {
                    return RecordWindowType(listNextToScript, windowType);
                }

                DrawerListDrawer movedList = FindListThatRecords(windowType);
                if (movedList != null)
                {
                    return movedList;
                }

                return listBelowScript != null ?
                    listBelowScript :
                    CreateList($"{directory}/{DrawerListDrawer.FileName}.asset", windowType);
            }

            DrawerListDrawer scriptFolderList = listNextToScript != null ? listNextToScript : listBelowScript;
            if (scriptFolderList != null && IsInWritablePackage(directory))
            {
                return scriptFolderList;
            }

            DrawerListDrawer projectList = FindProjectDrawerList(windowType);
            if (projectList != null)
            {
                return projectList;
            }

            CreateFolders(ProjectDrawerListFolder);

            string projectListPath = GetFreeListPath(GetProjectDrawerListPath(windowType), windowType);
            if (listNextToScript != null &&
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(listNextToScript), projectListPath))
            {
                return RecordWindowType(AssetDatabase.LoadAssetAtPath<DrawerListDrawer>(projectListPath), windowType);
            }

            return CreateList(projectListPath, windowType);
        }

        /// <summary>
        /// The asset path of the drawer list that the project keeps for a window whose script sits in a package.
        /// </summary>
        internal static string GetProjectDrawerListPath(Type windowType)
        {
            return $"{ProjectDrawerListFolder}/{windowType.FullName}.asset";
        }

        /// <summary>
        /// Finds the project list of a window type: the list at <see cref="GetProjectDrawerListPath"/> unless it
        /// belongs to another window, else a list under <c>Assets</c> that records the type, which the user moved or
        /// renamed.
        /// </summary>
        private static DrawerListDrawer FindProjectDrawerList(Type windowType)
        {
            DrawerListDrawer listAtPath =
                AssetDatabase.LoadAssetAtPath<DrawerListDrawer>(GetProjectDrawerListPath(windowType));
            if (listAtPath != null && MayBelongTo(listAtPath, windowType))
            {
                return RecordWindowType(listAtPath, windowType);
            }

            return FindListThatRecords(windowType);
        }

        /// <summary>
        /// Finds a list under <c>Assets</c> that records the window type: the first in path order, with a warning when
        /// several do.
        /// </summary>
        private static DrawerListDrawer FindListThatRecords(Type windowType)
        {
            List<string> listPaths = FileFinder.LoadAllAssetsAtPath<DrawerListDrawer>("Assets")
                .Where(list => list.WindowTypeName == windowType.FullName)
                .Select(list => AssetDatabase.GetAssetPath(list))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            if (listPaths.Count == 0)
            {
                return null;
            }

            if (listPaths.Count > 1)
            {
                Debug.LogWarning(
                    $"Several drawer lists belong to {windowType.FullName}: {string.Join(", ", listPaths)}. " +
                    $"The window uses {listPaths[0]}.");
            }

            return AssetDatabase.LoadAssetAtPath<DrawerListDrawer>(listPaths[0]);
        }

        /// <summary>
        /// Whether a list may be the window's: it records the window type, records nothing, or records a name that no
        /// window type has any more, such as the old name of a renamed window class.
        /// </summary>
        private static bool MayBelongTo(DrawerListDrawer list, Type windowType)
        {
            string recordedTypeName = list.WindowTypeName;
            if (string.IsNullOrEmpty(recordedTypeName) || recordedTypeName == windowType.FullName)
            {
                return true;
            }

            return !TypeCache.GetTypesDerivedFrom<Mu3Window>().Any(type => type.FullName == recordedTypeName);
        }

        /// <summary>
        /// Records the window type in a list, so the window still finds the list once it is moved or renamed.
        /// </summary>
        private static DrawerListDrawer RecordWindowType(DrawerListDrawer list, Type windowType)
        {
            if (list != null && list.WindowTypeName != windowType.FullName)
            {
                list.WindowTypeName = windowType.FullName;
                EditorUtility.SetDirty(list);
                AssetDatabase.SaveAssetIfDirty(list);
            }

            return list;
        }

        /// <summary>
        /// Creates a list that records the window type, at the path or, when an asset is already there, under the next
        /// free name.
        /// </summary>
        private static DrawerListDrawer CreateList(string assetPath, Type windowType)
        {
            DrawerListDrawer list = ScriptableObject.CreateInstance<DrawerListDrawer>();
            list.WindowTypeName = windowType.FullName;
            AssetDatabase.CreateAsset(list, GetFreeListPath(assetPath, windowType));

            return list;
        }

        /// <summary>
        /// The path itself when no asset is there, else the next free name next to it, such as
        /// <c>DrawerList 1.asset</c>, with a warning. <see cref="AssetDatabase.CreateAsset"/> replaces the asset at its
        /// path, which may be another window's list.
        /// </summary>
        private static string GetFreeListPath(string assetPath, Type windowType)
        {
            if (string.IsNullOrEmpty(
                AssetDatabase.AssetPathToGUID(assetPath, AssetPathToGUIDOptions.OnlyExistingAssets)))
            {
                return assetPath;
            }

            string freePath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            Debug.LogWarning(
                $"{assetPath} already holds another asset, so the drawer list of {windowType.FullName} is created " +
                $"at {freePath}.");

            return freePath;
        }

        private static string GetFolder(string assetPath)
        {
            int lastSlash = assetPath.LastIndexOf('/');

            return lastSlash > 0 ? assetPath.Substring(0, lastSlash) : "";
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