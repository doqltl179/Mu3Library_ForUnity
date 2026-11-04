using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// from a Git URL, keeps it under <see cref="Mu3Window.ProjectDrawerListFolder"/>. The window under <c>Assets</c>
    /// and the window in a read-only package still find their list after it is moved or renamed under <c>Assets</c>,
    /// never read the list of another window, and never replace an asset where they create their list. The tests that
    /// need Base to be a local or embedded package are ignored where it is installed read-only. Each test deletes the
    /// assets and folders it created; its temporary folders carry <see cref="TempFolderPrefix"/> and a unique suffix,
    /// the next run deletes any that an interrupted run left, and Git ignores those inside the Base package. An error
    /// that Unity logs because it could not write the .meta file of an asset the test created, such as the one in
    /// #341, does not fail the test: the window never writes .meta files itself, and the test still checks what the
    /// window did. Every other log that the Test Framework counts as failing still fails the test. Each test turns this
    /// on with the first helper it calls, <see cref="CreateTempFolder"/> or <see cref="TrackProjectList"/>.
    /// </summary>
    public class Mu3WindowTests
    {
        private const string TempFolderPrefix = nameof(Mu3WindowTests) + "_";

        /// <summary>
        /// The errors Unity logs when it cannot write the .meta file of an asset, which name the asset path.
        /// </summary>
        private static readonly Regex MetaWriteFailure = new(
            @"^(?:Cannot open file '(?<path>[^']+)\.meta' for write\.|Failed to write meta file '(?<path>[^']+)\.meta')");

        private readonly List<string> _createdPaths = new();
        private readonly List<(LogType Type, string Message, string StackTrace)> _failingLogs = new();
        private volatile bool _toleratingMetaWriteFailures;

        [SetUp]
        public void SetUp()
        {
            DeleteTempFolders("Assets");

            PackageInfo basePackage = PackageInfo.FindForAssembly(typeof(Mu3Window).Assembly);
            if (IsWritable(basePackage))
            {
                DeleteTempFolders(basePackage.assetPath);
            }

            // A log that reached the callback of the previous test after its TearDown took the logs.
            lock (_failingLogs)
            {
                _failingLogs.Clear();
            }

            Application.logMessageReceivedThreaded += RecordFailingLog;
        }

        [TearDown]
        public void TearDown()
        {
            // The TearDown checks its logs in a scope of its own.
            bool tolerating = _toleratingMetaWriteFailures;
            if (tolerating)
            {
                LogAssert.ignoreFailingMessages = true;
            }

            List<(LogType Type, string Message, string StackTrace)> unexpectedLogs;
            try
            {
                // DeleteAsset also deletes a folder whose .meta file Unity could not write.
                for (int i = _createdPaths.Count - 1; i >= 0; i--)
                {
                    AssetDatabase.DeleteAsset(_createdPaths[i]);
                }
            }
            finally
            {
                // The Test Framework counts failing logs again before the callback stops recording them.
                if (tolerating)
                {
                    LogAssert.ignoreFailingMessages = false;
                }

                Application.logMessageReceivedThreaded -= RecordFailingLog;
                _toleratingMetaWriteFailures = false;
                unexpectedLogs = TakeUnexpectedLogs();
                _createdPaths.Clear();
            }

            if (unexpectedLogs.Count > 0)
            {
                string messages = string.Join(" | ", unexpectedLogs.Select(log => $"[{log.Type}] {log.Message}"));
                Assert.Fail($"Unhandled log message: {messages}\nStack trace of the first:\n{unexpectedLogs[0].StackTrace}");
            }
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

            // A window under Assets records its type in the list next to its script, so the test leaves this folder of
            // the project alone when it holds a list of its own.
            if (FileFinder.LoadAllAssetsAtPath<DrawerListDrawer>(scriptFolder)
                .Any(list => !AssetDatabase.GetAssetPath(list).StartsWith(Mu3Window.ProjectDrawerListFolder + "/")))
            {
                Assert.Ignore($"{scriptFolder} already holds a drawer list in this project.");
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
        public void LoadDrawerList_ProjectListMovedAndRenamed_ReadsTheMovedList()
        {
            string scriptPath = GetReadOnlyPackageScriptPath();
            TrackProjectList(typeof(Mu3WindowTests));
            DrawerListDrawer created = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);
            string movedPath = $"{CreateTempFolder("Assets")}/Renamed.asset";
            AssertCreatedByTest(created);
            Assert.IsEmpty(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(created), movedPath));

            DrawerListDrawer reopened = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);

            Assert.AreSame(created, reopened);
            Assert.AreEqual(movedPath, AssetDatabase.GetAssetPath(reopened));
            AssertNoProjectList(typeof(Mu3WindowTests));
        }

        [Test]
        public void LoadDrawerList_ProjectListThatDoesNotRecordItsWindow_RecordsItSoTheMovedListIsFound()
        {
            string scriptPath = GetReadOnlyPackageScriptPath();
            TrackProjectList(typeof(Mu3WindowTests));
            DrawerListDrawer created = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);
            AssertCreatedByTest(created);
            // A list created before lists recorded their window type.
            created.WindowTypeName = "";
            EditorUtility.SetDirty(created);
            AssetDatabase.SaveAssetIfDirty(created);

            Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);
            string movedPath = $"{CreateTempFolder("Assets")}/Renamed.asset";
            AssertCreatedByTest(created);
            Assert.IsEmpty(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(created), movedPath));
            DrawerListDrawer reopened = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);

            Assert.AreSame(created, reopened);
            Assert.AreEqual(typeof(Mu3WindowTests).FullName, reopened.WindowTypeName);
        }

        [Test]
        public void LoadDrawerList_SeveralMovedProjectLists_ReadsTheFirstInPathOrderAndWarns()
        {
            string scriptPath = GetReadOnlyPackageScriptPath();
            IgnoreIfProjectListExists(typeof(Mu3WindowTests));
            TrackProjectList(typeof(Mu3WindowTests));
            string folder = CreateTempFolder("Assets");
            DrawerListDrawer first = CreateList($"{folder}/A.asset", typeof(Mu3WindowTests));
            CreateList($"{folder}/B.asset", typeof(Mu3WindowTests));
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape($"The window uses {folder}/A.asset")));

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);

            Assert.AreSame(first, loaded);
            AssertNoProjectList(typeof(Mu3WindowTests));
        }

        [Test]
        public void LoadDrawerList_ScriptUnderAssetsAboveAMovedProjectList_LeavesOutTheListOfTheOtherWindow()
        {
            string folder = CreateTempFolder("Assets");
            DrawerListDrawer movedList = CreateList($"{folder}/Moved.asset", typeof(Mu3WindowTestWindow));

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreNotSame(movedList, loaded);
            Assert.AreEqual($"{folder}/{DrawerListDrawer.FileName}.asset", AssetDatabase.GetAssetPath(loaded));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LoadDrawerList_ScriptUnderAssetsWithListsOfTwoWindows_LeavesOutTheListOfTheOtherWindow(
            bool otherWindowListFirst)
        {
            string folder = CreateTempFolder("Assets");
            DrawerListDrawer otherWindowList =
                CreateList($"{folder}/{(otherWindowListFirst ? "A" : "B")}.asset", typeof(Mu3WindowTestWindow));
            DrawerListDrawer ownList = CreateList($"{folder}/{(otherWindowListFirst ? "B" : "A")}.asset");

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreSame(ownList, loaded);
            Assert.AreEqual(typeof(Mu3WindowTestWindow).FullName, otherWindowList.WindowTypeName);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LoadDrawerList_ScriptInAReadOnlyPackageWithMovedListsOfTwoWindows_ReadsTheListThatRecordsItsWindow(
            bool otherWindowListFirst)
        {
            string scriptPath = GetReadOnlyPackageScriptPath();
            IgnoreIfProjectListExists(typeof(Mu3WindowTests));
            TrackProjectList(typeof(Mu3WindowTests));
            string folder = CreateTempFolder("Assets");
            DrawerListDrawer otherWindowList =
                CreateList($"{folder}/{(otherWindowListFirst ? "A" : "B")}.asset", typeof(Mu3WindowTestWindow));
            DrawerListDrawer ownList =
                CreateList($"{folder}/{(otherWindowListFirst ? "B" : "A")}.asset", typeof(Mu3WindowTests));

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);

            Assert.AreSame(ownList, loaded);
            Assert.AreEqual(typeof(Mu3WindowTestWindow).FullName, otherWindowList.WindowTypeName);
            AssertNoProjectList(typeof(Mu3WindowTests));
        }

        [Test]
        public void LoadDrawerList_ProjectListAndACopyFirstInPathOrder_ReadsTheProjectList()
        {
            string scriptPath = GetReadOnlyPackageScriptPath();
            TrackProjectList(typeof(Mu3WindowTests));
            DrawerListDrawer projectList = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);
            string projectListPath = Mu3Window.GetProjectDrawerListPath(typeof(Mu3WindowTests));
            // A copy duplicated in the Project window records the same window type and comes first in path order.
            string copyPath = $"{Mu3Window.ProjectDrawerListFolder}/{typeof(Mu3WindowTests).FullName} 1.asset";
            if (AssetDatabase.LoadMainAssetAtPath(copyPath) != null)
            {
                Assert.Ignore($"{copyPath} already exists in this project.");
            }

            _createdPaths.Add(copyPath);
            Assert.IsTrue(AssetDatabase.CopyAsset(projectListPath, copyPath));
            Assert.Less(string.CompareOrdinal(copyPath, projectListPath), 0);
            Assert.AreEqual(
                typeof(Mu3WindowTests).FullName,
                AssetDatabase.LoadAssetAtPath<DrawerListDrawer>(copyPath).WindowTypeName);

            DrawerListDrawer reopened = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);

            Assert.AreEqual(projectListPath, AssetDatabase.GetAssetPath(projectList));
            Assert.AreSame(projectList, reopened);
        }

        [Test]
        public void LoadDrawerList_ScriptUnderAssetsWhereTheListOfAnotherWindowTakesTheName_KeepsItAndCreatesAnother()
        {
            string folder = CreateTempFolder("Assets");
            string takenPath = $"{folder}/{DrawerListDrawer.FileName}.asset";
            // The list of a window in a package, which the user moved next to this window's script and renamed.
            DrawerListDrawer otherWindowList = CreateList(takenPath, typeof(Mu3WindowTestWindow));
            string freePath = AssetDatabase.GenerateUniqueAssetPath(takenPath);
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape($"is created at {freePath}")));

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");
            DrawerListDrawer reopened = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreSame(otherWindowList, AssetDatabase.LoadAssetAtPath<DrawerListDrawer>(takenPath));
            Assert.AreEqual(typeof(Mu3WindowTestWindow).FullName, otherWindowList.WindowTypeName);
            Assert.AreNotEqual(takenPath, freePath);
            Assert.AreEqual(freePath, AssetDatabase.GetAssetPath(loaded));
            Assert.AreEqual(typeof(Mu3WindowTests).FullName, loaded.WindowTypeName);
            Assert.AreSame(loaded, reopened);
        }

        [Test]
        public void LoadDrawerList_ScriptInAReadOnlyPackageWhereTheListOfAnotherWindowTakesTheName_KeepsItAndCreatesAnother()
        {
            string scriptPath = GetReadOnlyPackageScriptPath();
            string takenPath = Mu3Window.GetProjectDrawerListPath(typeof(Mu3WindowTests));
            IgnoreIfProjectListExists(typeof(Mu3WindowTests));
            IgnoreIfProjectListExists(typeof(Mu3WindowTestWindow));
            TrackProjectList(typeof(Mu3WindowTestWindow));
            TrackProjectList(typeof(Mu3WindowTests));
            // Another window's project list, which the user renamed to this window's name.
            DrawerListDrawer otherWindowList = Mu3Window.LoadDrawerList(typeof(Mu3WindowTestWindow), scriptPath);
            AssertCreatedByTest(otherWindowList);
            Assert.IsEmpty(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(otherWindowList), takenPath));
            string freePath = AssetDatabase.GenerateUniqueAssetPath(takenPath);
            _createdPaths.Add(freePath);
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape($"is created at {freePath}")));

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);
            DrawerListDrawer reopened = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), scriptPath);

            Assert.AreSame(otherWindowList, AssetDatabase.LoadAssetAtPath<DrawerListDrawer>(takenPath));
            Assert.AreEqual(typeof(Mu3WindowTestWindow).FullName, otherWindowList.WindowTypeName);
            Assert.AreNotEqual(takenPath, freePath);
            Assert.AreEqual(freePath, AssetDatabase.GetAssetPath(loaded));
            Assert.AreEqual(typeof(Mu3WindowTests).FullName, loaded.WindowTypeName);
            Assert.AreSame(loaded, reopened);
        }

        [Test]
        public void LoadDrawerList_ScriptUnderAssetsListMovedOutOfTheScriptFolder_ReadsTheMovedList()
        {
            string scriptFolder = CreateTempFolder("Assets");
            DrawerListDrawer created = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{scriptFolder}/Window.cs");
            string movedPath = $"{CreateTempFolder("Assets")}/Renamed.asset";
            AssertCreatedByTest(created);
            Assert.IsEmpty(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(created), movedPath));

            DrawerListDrawer reopened = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{scriptFolder}/Window.cs");

            Assert.AreSame(created, reopened);
            Assert.AreEqual(movedPath, AssetDatabase.GetAssetPath(reopened));
            CollectionAssert.IsEmpty(FileFinder.LoadAllAssetsAtPath<DrawerListDrawer>(scriptFolder));
        }

        [Test]
        public void LoadDrawerList_ScriptUnderAssetsWithAListThatDoesNotRecordItsWindow_RecordsItSoTheMovedListIsFound()
        {
            string scriptFolder = CreateTempFolder("Assets");
            // A list imported with the UtilWindow sample before lists recorded their window type.
            DrawerListDrawer imported = CreateList($"{scriptFolder}/{DrawerListDrawer.FileName}.asset");

            Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{scriptFolder}/Window.cs");
            string movedPath = $"{CreateTempFolder("Assets")}/{DrawerListDrawer.FileName}.asset";
            AssertCreatedByTest(imported);
            Assert.IsEmpty(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(imported), movedPath));
            DrawerListDrawer reopened = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{scriptFolder}/Window.cs");

            Assert.AreSame(imported, reopened);
            Assert.AreEqual(typeof(Mu3WindowTests).FullName, reopened.WindowTypeName);
            CollectionAssert.IsEmpty(FileFinder.LoadAllAssetsAtPath<DrawerListDrawer>(scriptFolder));
        }

        [Test]
        public void LoadDrawerList_ScriptUnderAssetsWithTheListOfARenamedWindowClass_ReadsItAndRecordsTheNewName()
        {
            string folder = CreateTempFolder("Assets");
            // No type has the name the window class had before it was renamed.
            DrawerListDrawer list =
                CreateList($"{folder}/{DrawerListDrawer.FileName}.asset", "Mu3Library.Tests.RenamedMu3Window");

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreSame(list, loaded);
            Assert.AreEqual(typeof(Mu3WindowTests).FullName, loaded.WindowTypeName);
        }

        [Test]
        public void LoadDrawerList_ScriptUnderAssetsWithAListOnlyBelowTheScript_ReadsItWithoutRecordingTheWindow()
        {
            string folder = CreateTempFolder("Assets");
            string subfolder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(folder, "Sub"));
            // It may be the list of a window whose script sits in the folder below.
            DrawerListDrawer listBelow = CreateList($"{subfolder}/{DrawerListDrawer.FileName}.asset");

            DrawerListDrawer loaded = Mu3Window.LoadDrawerList(typeof(Mu3WindowTests), $"{folder}/Window.cs");

            Assert.AreSame(listBelow, loaded);
            Assert.IsEmpty(listBelow.WindowTypeName);
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

        private static DrawerListDrawer CreateList(string assetPath, Type windowType = null)
        {
            return CreateList(assetPath, windowType != null ? windowType.FullName : "");
        }

        private static DrawerListDrawer CreateList(string assetPath, string windowTypeName)
        {
            DrawerListDrawer list = ScriptableObject.CreateInstance<DrawerListDrawer>();
            list.WindowTypeName = windowTypeName;
            AssetDatabase.CreateAsset(list, assetPath);

            return list;
        }

        private static void IgnoreIfProjectListExists(Type windowType)
        {
            string projectListPath = Mu3Window.GetProjectDrawerListPath(windowType);
            if (AssetDatabase.LoadMainAssetAtPath(projectListPath) != null)
            {
                Assert.Ignore($"{projectListPath} already exists in this project.");
            }
        }

        private static void AssertNoProjectList(Type windowType)
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<DrawerListDrawer>(Mu3Window.GetProjectDrawerListPath(windowType)));
        }

        /// <summary>
        /// Fails unless the asset sits at a path this test created, or below one. A test checks this before it moves or
        /// changes an asset, so a search that returns another asset, such as a list of the project or of a linked
        /// sample, never makes the test move or change it, and the TearDown never deletes it.
        /// </summary>
        private void AssertCreatedByTest(Object asset)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            Assert.IsTrue(IsCreatedByTest(assetPath), $"{assetPath} is not an asset this test created.");
        }

        private bool IsCreatedByTest(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                _createdPaths.Any(path =>
                    assetPath == path || assetPath.StartsWith(path + "/", StringComparison.Ordinal));
        }

        /// <summary>
        /// From here to the end of the test, an error that Unity logs because it could not write the .meta file of an
        /// asset this test created does not fail the test. The Test Framework then lets every failing log pass, so the
        /// TearDown fails the test on any other. The test body calls this, since the Test Framework checks the logs of
        /// the body apart from those of the SetUp and the TearDown, and ignores the setting the SetUp makes. An error
        /// that a test expects with <c>LogAssert.Expect</c> after this call still fails it, since the TearDown sees
        /// every failing log.
        /// </summary>
        private void TolerateMetaWriteFailures()
        {
            _toleratingMetaWriteFailures = true;
            LogAssert.ignoreFailingMessages = true;
        }

        private void RecordFailingLog(string message, string stackTrace, LogType type)
        {
            // The logs the Test Framework counts as failing (LogScope.AddLog).
            bool failing = type == LogType.Error || type == LogType.Assert ||
                (type == LogType.Exception && !CarriesNUnitResult(message, stackTrace));
            if (failing && _toleratingMetaWriteFailures)
            {
                lock (_failingLogs)
                {
                    _failingLogs.Add((type, message, stackTrace));
                }
            }
        }

        /// <summary>
        /// Whether an exception log carries an NUnit success, inconclusive, or ignore result, the only exceptions the
        /// Test Framework does not count as failing.
        /// </summary>
        private static bool CarriesNUnitResult(string message, string stackTrace)
        {
            return (string.IsNullOrEmpty(stackTrace) ||
                    stackTrace.StartsWith("NUnit.Framework.Assert.", StringComparison.Ordinal)) &&
                (message.StartsWith("SuccessException: ", StringComparison.Ordinal) ||
                    message.StartsWith("InconclusiveException: ", StringComparison.Ordinal) ||
                    message.StartsWith("IgnoreException: ", StringComparison.Ordinal));
        }

        /// <summary>
        /// Takes the failing logs recorded while the test tolerated .meta write failures, logs a warning for each of
        /// those failures that names an asset this test created, and returns the others.
        /// </summary>
        private List<(LogType Type, string Message, string StackTrace)> TakeUnexpectedLogs()
        {
            List<(LogType Type, string Message, string StackTrace)> logs;
            lock (_failingLogs)
            {
                logs = new List<(LogType Type, string Message, string StackTrace)>(_failingLogs);
                _failingLogs.Clear();
            }

            List<(LogType Type, string Message, string StackTrace)> unexpectedLogs = new();
            foreach ((LogType Type, string Message, string StackTrace) log in logs)
            {
                Match match = MetaWriteFailure.Match(log.Message);
                if (log.Type == LogType.Error && match.Success && IsCreatedByTest(match.Groups["path"].Value))
                {
                    Debug.LogWarning(
                        $"{nameof(Mu3WindowTests)} tolerates this error, since the test created " +
                        $"{match.Groups["path"].Value}: {log.Message}");
                }
                else
                {
                    unexpectedLogs.Add(log);
                }
            }

            return unexpectedLogs;
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
            TolerateMetaWriteFailures();
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
            TolerateMetaWriteFailures();
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
