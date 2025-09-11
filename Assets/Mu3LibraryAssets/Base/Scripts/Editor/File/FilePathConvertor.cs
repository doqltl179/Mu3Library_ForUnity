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
        public static void SplitPathToDirectoryAndFileNameAndExtension(string path, out string directory, out string fileName, out string extension) {
            directory = "";
            fileName = "";
            extension = "";

            if(string.IsNullOrEmpty(path)) {
                Debug.LogWarning("Path is NULL.");

                return;
            }

            string replacedPath = path.Replace('\\', '/');

            // 'path'가 비어있지 않기 때문에 반드시 '1' 이상의 길이가 나온다.
            string[] splitPath = replacedPath.Split('/');

            // 파일명 혹은 폴더명
            string lastString = splitPath[splitPath.Length - 1];

            // 만약 길이가 '1' 보다 크다면 'path'는 파일 경로일 것이고, 
            // 그렇지 않다면 'path'는 폴더 경로일 것이다.
            string[] lastStringSplit = lastString.Split('.');

            // 'lastString' == 파일명
            if(lastStringSplit.Length > 1) {
                extension = lastStringSplit[lastStringSplit.Length - 1];
                fileName = lastString[0..(lastString.Length - (extension.Length + 1))];

                if(lastString.Length != replacedPath.Length) {
                    directory = replacedPath[0..(replacedPath.Length - (lastString.Length + 1))];
                }
            }
            // 'lastString' == 폴더명
            else {
                directory = replacedPath;
            }

            //Debug.Log($"Path Split.\r\npath: {path}\r\ndirectory: {directory}\r\nfileName: {fileName}\r\nextension: {extension}\r\n");
        }

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

            //Debug.Log($"SystemPath changed to AssetPath. systemPath: {systemPath}, assetPath: {result}");

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

            //Debug.Log($"AssetPath changed to SystemPath. assetPath: {assetPath}, systemPath: {result}");

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