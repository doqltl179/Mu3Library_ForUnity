using System;
using System.Collections.Generic;
using Mu3Library.Editor.FileUtil;
using Mu3Library.Editor.Window;
using Mu3Library.Editor.Window.Drawer;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.TestTools;

using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Where a <see cref="Mu3Window"/> finds its drawer list. A window under <c>Assets</c> and a window in a package
    /// that can be changed read the list next to their script; a window in a read-only package, such as one installed
    /// from a Git URL, keeps it under <see cref="Mu3Window.ProjectDrawerListFolder"/>. The tests that need Base to be a
    /// local or embedded package are ignored where it is installed read-only. Each test deletes the assets and folders
    /// it created; its temporary folders carry <see cref="TempFolderPrefix"/> and a unique suffix, the next run deletes
    /// any that an interrupted run left, and Git ignores those inside the Base package.
    /// </summary>
    public class Mu3WindowTests
    {
        private const string TempFolderPrefix = nameof(Mu3WindowTests) + "_";

        private readonly List<string> _createdPaths = new();

        [SetUp]
        public void SetUp()
        {
            DeleteTempFolders("Assets");

            PackageInfo basePackage = PackageInfo.FindForAssembly(typeof(Mu3Window).Assembly);
            if (IsWritable(basePackage))
            {
                DeleteTempFolders(basePackage.assetPath);
            }
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdPaths.Count - 1; i >= 0; i--)
            {
                AssetDatabase.DeleteAsset(_createdPaths[i]);
            }

            _createdPaths.Clear();
        }

        [Test]
        public void LoadDrawerList_ScriptUnderAssetsWithAList_ReadsTheListNextToTheScript()
        {
            string folder = CreateTempFolder("Assets");
            DrawerListDrawer list = CreateList($"{folder}/Existing.asset");

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreSame(list, loaded);
            AssertNoProjectList(typeof(Mu3WindowTests));
        }

        [Test]
        public void LoadDrawerList_ScriptUnderAssetsWithoutAList_CreatesTheListNextToTheScript()
        {
            string folder = CreateTempFolder("Assets");

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreEqual($"{folder}/{DrawerListDrawer.FileName}.asset", AssetDatabase.GetAssetPath(loaded));
            AssertNoProjectList(typeof(Mu3WindowTests));
        }

        [Test]
        public void LoadDrawerList_ScriptAboveTheProjectListFolder_LeavesOutTheListsKeptForPackageWindows()
        {
            string readOnlyScriptPath = GetReadOnlyPackageScriptPath();
            TrackProjectList(typeof(Mu3WindowTests));
            DrawerListDrawer packageWindowList = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), readOnlyScriptPath);
            string scriptFolder = Mu3Window.ProjectDrawerListFolder.Substring(
                0, Mu3Window.ProjectDrawerListFolder.LastIndexOf('/'));
            string expectedPath = $"{scriptFolder}/{DrawerListDrawer.FileName}.asset";
            if (AssetDatabase.LoadMainAssetAtPath(expectedPath) != null)
            {
                Assert.Ignore($"{expectedPath} already exists in this project.");
            }

            _createdPaths.Add(expectedPath);

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTestWindow), $"{scriptFolder}/Window.cs");

            Assert.AreNotSame(packageWindowList, loaded);
            Assert.AreEqual(expectedPath, AssetDatabase.GetAssetPath(loaded));
        }

        [Test]
        public void LoadDrawerList_ScriptInAWritablePackageWithAList_ReadsTheListNextToTheScript()
        {
            string folder = CreateTempFolder(GetWritableBasePackagePath());
            DrawerListDrawer list = CreateList($"{folder}/Existing.asset");

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreSame(list, loaded);
            AssertNoProjectList(typeof(Mu3WindowTests));
        }

        [Test]
        public void LoadDrawerList_ScriptInAWritablePackageWithoutAList_KeepsTheListInTheProject()
        {
            string folder = CreateTempFolder(GetWritableBasePackagePath());
            TrackProjectList(typeof(Mu3WindowTests));

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreEqual(Mu3Window.GetProjectDrawerListPath(typeof(Mu3WindowTests)), AssetDatabase.GetAssetPath(loaded));
            CollectionAssert.IsEmpty(FileFinder.LoadAllAssetsAtPath<DrawerListDrawer>(folder));
        }

        [Test]
        public void LoadDrawerList_ScriptInAReadOnlyPackage_KeepsTheListInTheProjectAndReadsItAgain()
        {
            string scriptPath = GetReadOnlyPackageScriptPath();
            TrackProjectList(typeof(Mu3WindowTests));

            DrawerListDrawer created = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);
            DrawerListDrawer reopened = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);

            Assert.AreEqual(Mu3Window.GetProjectDrawerListPath(typeof(Mu3WindowTests)), AssetDatabase.GetAssetPath(created));
            Assert.AreSame(created, reopened);
            CollectionAssert.IsEmpty(
                FileFinder.LoadAllAssetsAtPath<DrawerListDrawer>(scriptPath.Substring(0, scriptPath.LastIndexOf('/'))));
        }

        [Test]
        public void OnBecameVisible_WindowScriptInAPackage_LoadsOneListKeptInTheProject()
        {
            TrackProjectList(typeof(Mu3WindowTestWindow));
            Mu3WindowTestWindow first = ScriptableObject.CreateInstance<Mu3WindowTestWindow>();
            Mu3WindowTestWindow second = ScriptableObject.CreateInstance<Mu3WindowTestWindow>();
            try
            {
                first.SimulateBecameVisible();
                second.SimulateBecameVisible();

                Assert.IsNotNull(first.DrawerList);
                Assert.AreEqual(
                    Mu3Window.GetProjectDrawerListPath(typeof(Mu3WindowTestWindow)),
                    AssetDatabase.GetAssetPath(first.DrawerList));
                Assert.AreSame(first.DrawerList, second.DrawerList);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        private static bool IsWritable(PackageInfo package)
        {
            return package != null &&
                (package.source == PackageSource.Embedded || package.source == PackageSource.Local);
        }

        private static string GetWritableBasePackagePath()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(Mu3Window).Assembly);
            if (!IsWritable(package))
            {
                Assert.Ignore("Base is installed read-only in this project.");
            }

            return package.assetPath;
        }

        private static string GetReadOnlyPackageScriptPath()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(LogAssert).Assembly);
            if (package == null || IsWritable(package))
            {
                Assert.Ignore("The Test Framework is not a read-only package in this project.");
            }

            return $"{package.assetPath}/Window.cs";
        }

        private static DrawerListDrawer CreateList(string assetPath)
        {
            DrawerListDrawer list = ScriptableObject.CreateInstance<DrawerListDrawer>();
            AssetDatabase.CreateAsset(list, assetPath);

            return list;
        }

        private static void AssertNoProjectList(Type windowType)
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<DrawerListDrawer>(Mu3Window.GetProjectDrawerListPath(windowType)));
        }

        private static void DeleteTempFolders(string parentFolder)
        {
            foreach (string folder in AssetDatabase.GetSubFolders(parentFolder))
            {
                if (folder.Substring(folder.LastIndexOf('/') + 1).StartsWith(TempFolderPrefix))
                {
                    AssetDatabase.DeleteAsset(folder);
                }
            }
        }

        private string CreateTempFolder(string parentFolder)
        {
            string name = TempFolderPrefix + Guid.NewGuid().ToString("N").Substring(0, 8);
            string folder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(parentFolder, name));
            Assert.AreEqual($"{parentFolder}/{name}", folder);
            _createdPaths.Add(folder);

            return folder;
        }

        /// <summary>
        /// Marks the project list of a window type, and the folders above it, for deletion when the test created them.
        /// </summary>
        private void TrackProjectList(Type windowType)
        {
            string folder = "Assets";
            foreach (string name in Mu3Window.ProjectDrawerListFolder.Substring("Assets/".Length).Split('/'))
            {
                folder = $"{folder}/{name}";
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    _createdPaths.Add(folder);
                }
            }

            string listPath = Mu3Window.GetProjectDrawerListPath(windowType);
            if (AssetDatabase.LoadMainAssetAtPath(listPath) == null)
            {
                _createdPaths.Add(listPath);
            }
        }
    }
}
