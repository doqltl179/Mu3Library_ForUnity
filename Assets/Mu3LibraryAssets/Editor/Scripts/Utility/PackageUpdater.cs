using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using System.Linq;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Mu3Library.Editor.Utility
{
    public class PackageUpdater : MonoBehaviour
    {
        private const string _packageNameRoot = "com.github.doqltl179.mu3libraryassets";

        private static ListRequest _listRequest;
        private static AddRequest _updateRequest;
        private static PackageInfo _lastUpdatedPackageInfo;

        private static bool _isUpdating = false;



        [MenuItem("Mu3Library/Update Package")]
        private static void UpdateMyPackage()
        {
            if (_isUpdating)
            {
                Debug.LogWarning($"Package updating now...");
                return;
            }

            _isUpdating = true;

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
                string packageName = result
                    .Select(t => t.packageId)
                    .Where(t => t.Contains(_packageNameRoot))
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(packageName))
                {
                    _updateRequest = Client.Add(packageName);
                    EditorApplication.update += UpdateProgress;
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

        private static void UpdateProgress()
        {
            if (_updateRequest == null || !_updateRequest.IsCompleted)
            {
                return;
            }

            if (_updateRequest.Status == StatusCode.Success)
            {
                _lastUpdatedPackageInfo = _updateRequest.Result;

                EditorApplication.update += PackageUpdateCompleteLog;
            }
            else
            {
                Debug.LogError($"Package update failed. {_updateRequest.Error.message}");
            }

            _updateRequest = null;
            _isUpdating = false;

            EditorApplication.update -= UpdateProgress;
        }

        private static void PackageUpdateCompleteLog()
        {
            if (_lastUpdatedPackageInfo == null)
            {
                return;
            }

            Debug.Log($"Package update complated. {_lastUpdatedPackageInfo.packageId} ({_lastUpdatedPackageInfo.version})");
            
            EditorApplication.update -= PackageUpdateCompleteLog;
        }
    }
}