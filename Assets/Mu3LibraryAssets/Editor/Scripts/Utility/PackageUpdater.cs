using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Mu3Library.Editor.Utility
{
    public class PackageUpdater : MonoBehaviour
    {
        private const string _packageIdentifier = "com.github.doqltl179.mu3libraryassets";
        private static AddRequest _updateRequest;

        private static bool _isUpdating = false;



        [MenuItem("Mu3Library/Update Package")]
        private static void UpdateMyPackage()
        {
            if(_isUpdating)
            {
                Debug.LogWarning($"Package updating now...");
                return;
            }

            _updateRequest = Client.Add(_packageIdentifier);
            EditorApplication.update += Progress;
        }

        private static void Progress()
        {
            if (_isUpdating || _updateRequest == null || !_updateRequest.IsCompleted)
            {
                return;
            }

            if (_updateRequest.Status == StatusCode.Success)
            {
                var result = _updateRequest.Result;
                Debug.Log($"Package update complated. {result.packageId} ({result.version})");
            }
            else
            {
                Debug.LogError($"Package update failed. {_updateRequest.Error.message}");
            }

            _updateRequest = null;

            EditorApplication.update -= Progress;
        }
    }
}