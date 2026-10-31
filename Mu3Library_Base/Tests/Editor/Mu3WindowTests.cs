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
    /// local or embedded package are inconclusive where it is installed read-only. Each test deletes the assets and
    /// folders it created.
    /// </summary>
    public class Mu3WindowTests
    {
        private readonly List<string> _createdPaths = new();

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
            string folder = CreateFolder("Assets", nameof(Mu3WindowTests));
            DrawerListDrawer list = CreateList($"{folder}/Existing.asset");

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreSame(list, loaded);
            AssertNoProjectList(typeof(Mu3WindowTests));
        }

        [Test]
        public void LoadDrawerList_ScriptUnderAssetsWithoutAList_CreatesTheListNextToTheScript()
        {
            string folder = CreateFolder("Assets", nameof(Mu3WindowTests));

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreEqual($"{folder}/{DrawerListDrawer.FileName}.asset", AssetDatabase.GetAssetPath(loaded));
            AssertNoProjectList(typeof(Mu3WindowTests));
        }

        [Test]
        public void LoadDrawerList_ScriptInAWritablePackageWithAList_ReadsTheListNextToTheScript()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(Mu3Window).Assembly);
            Assume.That(IsWritable(package), Is.True, "Base is installed read-only in this project.");
            string folder = CreateFolder(package.assetPath, nameof(Mu3WindowTests));
            DrawerListDrawer list = CreateList($"{folder}/Existing.asset");

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreSame(list, loaded);
            AssertNoProjectList(typeof(Mu3WindowTests));
        }

        [Test]
        public void LoadDrawerList_ScriptInAWritablePackageWithoutAList_KeepsTheListInTheProject()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(Mu3Window).Assembly);
            Assume.That(IsWritable(package), Is.True, "Base is installed read-only in this project.");
            string folder = CreateFolder(package.assetPath, nameof(Mu3WindowTests));
            TrackProjectList(typeof(Mu3WindowTests));

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreEqual(Mu3Window.GetProjectDrawerListPath(typeof(Mu3WindowTests)), AssetDatabase.GetAssetPath(loaded));
            CollectionAssert.IsEmpty(FileFinder.LoadAllAssetsAtPath<DrawerListDrawer>(folder));
        }

        [Test]
        public void LoadDrawerList_ScriptInAReadOnlyPackage_KeepsTheListInTheProjectAndReadsItAgain()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(LogAssert).Assembly);
            Assume.That(package, Is.Not.Null, "The Test Framework is not a package in this project.");
            Assume.That(IsWritable(package), Is.False, "The Test Framework can be changed in this project.");
            string scriptPath = $"{package.assetPath}/Window.cs";
            TrackProjectList(typeof(Mu3WindowTests));

            DrawerListDrawer created = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);
            DrawerListDrawer reopened = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);

            Assert.AreEqual(Mu3Window.GetProjectDrawerListPath(typeof(Mu3WindowTests)), AssetDatabase.GetAssetPath(created));
            Assert.AreSame(created, reopened);
            CollectionAssert.IsEmpty(FileFinder.LoadAllAssetsAtPath<DrawerListDrawer>(package.assetPath));
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

        private string CreateFolder(string parentFolder, string name)
        {
            Assert.IsFalse(
                AssetDatabase.IsValidFolder($"{parentFolder}/{name}"),
                $"{parentFolder}/{name} is left over from an earlier run.");
            string folder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(parentFolder, name));
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
