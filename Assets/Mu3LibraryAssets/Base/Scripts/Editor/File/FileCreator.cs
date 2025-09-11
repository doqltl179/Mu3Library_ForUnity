#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.FileUtil {
    public static class FileCreator {



        #region Utility
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="directory"> Assets 폴더를 기준으로 한 상대 경로 </param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static T CreateScriptableObject<T>(string directory, string fileName) where T : ScriptableObject {
            if(string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) {
                Debug.LogError($"Property not enough. directory: {directory}, fileName: {fileName}");

                return null;
            }

            string systemDirectory = FilePathConvertor.AssetPathToSystemPath(directory);
            DirectoryInfo di = new DirectoryInfo(systemDirectory);
            // 만약 폴더가 존재하지 않으면 생성한다.
            if(!di.Exists) {
                di.Create();
            }

            string relativeFilePath = $"{directory}/{fileName}.asset";

            T instance = ScriptableObject.CreateInstance<T>();

            // ScriptableObject를 에셋으로 저장
            AssetDatabase.CreateAsset(instance, relativeFilePath);
            AssetDatabase.SaveAssets();

            //Debug.Log($"ScriptableObject Created. path: {relativeFilePath}");

            return instance;
        }
        #endregion
    }
}

#endif