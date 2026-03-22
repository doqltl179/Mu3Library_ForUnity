using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Mu3Library.Editor.Utility
{
    public static class PackageUpdater
    {
        private const string _packageNameRoot = "com.github.doqltl179.mu3library";
        private const string _packageNameBase = "com.github.doqltl179.mu3library.base";
        private const string _packageNameURP = "com.github.doqltl179.mu3library.urp";

        private static ListRequest _listRequest;
        private static AddRequest _updateRequest;
        private static Queue<string> _pendingPackages = new Queue<string>();
        private static string _targetFilter;

        private static bool _isUpdating = false;



        [MenuItem("Mu3Library/Update Package/All")]
        private static void UpdateAllPackages() => BeginUpdate(_packageNameRoot);

        [MenuItem("Mu3Library/Update Package/Base")]
        private static void UpdateBasePackage() => BeginUpdate(_packageNameBase);

        [MenuItem("Mu3Library/Update Package/URP")]
        private static void UpdateURPPackage() => BeginUpdate(_packageNameURP);

        private static void BeginUpdate(string filter)
        {
            if (_isUpdating)
            {
                Debug.LogWarning($"Package updating now...");
                return;
            }

            _isUpdating = true;
            _targetFilter = filter;

            _listRequest = Client.List(true, false);
            EditorApplication.update += ListProgress;
        }

        private static void ListProgress()
        {
            if (_listRequest == null || !_listRequest.IsCompleted)
            {
                return;
            }

            if (_listRequest.Status == StatusCode.Success)
            {
                var result = _listRequest.Result;
                var packageNames = result
                    .Select(t => t.packageId)
                    .Where(t => t.Contains(_targetFilter))
                    .ToList();

                if (packageNames.Count > 0)
                {
                    _pendingPackages = new Queue<string>(packageNames);
                    StartNextUpdate();
                }
                else
                {
                    Debug.LogError("Package not found");
                    _isUpdating = false;
                }
            }
            else
            {
                Debug.LogError("Package list not found");
                _isUpdating = false;
            }

            _listRequest = null;

            EditorApplication.update -= ListProgress;
        }

        private static void StartNextUpdate()
        {
            if (_pendingPackages.Count == 0)
            {
                _isUpdating = false;
                return;
            }

            string packageId = _pendingPackages.Dequeue();
            _updateRequest = Client.Add(packageId);
            EditorApplication.update += UpdateProgress;
        }

        private static void UpdateProgress()
        {
            if (_updateRequest == null || !_updateRequest.IsCompleted)
            {
                return;
            }

            if (_updateRequest.Status == StatusCode.Success)
            {
                PackageInfo info = _updateRequest.Result;
                Debug.Log($"Package update completed. {info.packageId} ({info.version})");
            }
            else
            {
                Debug.LogError($"Package update failed. {_updateRequest.Error.message}");
            }

            _updateRequest = null;

            EditorApplication.update -= UpdateProgress;

            StartNextUpdate();
        }
    }
}