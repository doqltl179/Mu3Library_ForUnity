#if UNITY_EDITOR

using UnityEngine;

namespace Mu3Library.Editor.FileUtil {
    public static class FilePathConvertor {
        /// <summary>
        /// "Assets" 폴더의 상대 경로
        /// </summary>
        private static readonly string relativePathOfAssetsFolder = "Assets";

        /// <summary>
        /// "Assets" 폴더의 절대 경로
        /// </summary>
        private static readonly string absolutePathOfAssetsFolder = Application.dataPath;

        /// <summary>
        /// "Assets" 폴더의 상위 폴더이며, 유니티 프로젝트의 절대 경로
        /// </summary>
        private static readonly string absolutePathOfRootFolder = absolutePathOfAssetsFolder[0..(absolutePathOfAssetsFolder.Length - relativePathOfAssetsFolder.Length - 1)];



        #region Utility
        public static string SystemPathToAssetPath(string systemPath) {
            if(string.IsNullOrEmpty(systemPath)) {
                Debug.LogError("SystemPath is NULL.");

                return "";
            }

            string replacedPath = systemPath.Replace('\\', '/');

            if(!IsSystemPath(replacedPath)) {
                Debug.LogError($"This path is not SystemPath. path: {systemPath}");

                return "";
            }

            // (유니티 프로젝트의 절대 경로 + "/") 만큼 제외한 string 값을 반환한다.
            string result = replacedPath[(absolutePathOfRootFolder.Length + 1)..replacedPath.Length];

            Debug.Log($"SystemPath changed to AssetPath. systemPath: {systemPath}, assetPath: {result}");

            return result;
        }

        public static string AssetPathToSystemPath(string assetPath) {
            if(string.IsNullOrEmpty(assetPath)) {
                Debug.LogError("AssetPath is NULL.");

                return "";
            }

            string replacedPath = assetPath.Replace('\\', '/');

            if(!IsAssetPath(replacedPath)) {
                Debug.LogError($"This path is not AssetPath. path: {assetPath}");

                return "";
            }

            string result = absolutePathOfRootFolder + "/" + replacedPath;

            Debug.Log($"AssetPath changed to SystemPath. assetPath: {assetPath}, systemPath: {result}");

            return result;
        }
        #endregion

        private static bool IsSystemPath(string systemPath) {
            bool result = false;

            if(systemPath.StartsWith(absolutePathOfAssetsFolder)) {
                result = true;
            }

            return result;
        }

        private static bool IsAssetPath(string assetPath) {
            bool result = false;

            if(assetPath.StartsWith(relativePathOfAssetsFolder)) {
                result = true;
            }

            return result;
        }
    }
}

#endif