#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.FileUtil {
    public static class FileFinder {



        #region Utility
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filePath"> Assets 폴더를 기준으로 한 상대 경로 </param>
        /// <returns></returns>
        public static T LoadAssetAtPath<T>(string filePath) where T : Object {
            return AssetDatabase.LoadAssetAtPath<T>(filePath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="directory"> Assets 폴더를 기준으로 한 상대 경로 </param>
        /// <param name="name"></param>
        /// <param name="typeString"></param>
        /// <param name="assetlabel"></param>
        /// <returns></returns>
        public static List<T> LoadAllAssetsAtPath<T>(string directory = "", string name = "", string typeString = "", string assetlabel = "") where T : Object {
            string[] guids = FindAssets(directory, name, typeString, assetlabel);
            string[] relativePaths = guids.Select(g => AssetDatabase.GUIDToAssetPath(g)).ToArray();

            List<T> objs = new List<T>();
            foreach(string path in relativePaths) {
                T obj = AssetDatabase.LoadAssetAtPath<T>(path);
                if(obj != null) {
                    objs.Add(obj);
                }
                else {
                    Debug.LogWarning($"Object not found. path: {path}");
                }
            }

            return objs;
        }

        /// <summary>
        /// 에셋 파일의 상대 경로를 반환한다.
        /// </summary>
        public static string GetAssetPathFromMonoBehaviour<T>(T obj) where T : MonoBehaviour {
            if(obj == null) {
                Debug.LogError("MonoBehaviour is NULL.");

                return "";
            }

            MonoScript scriptObj = MonoScript.FromMonoBehaviour(obj);
            if(scriptObj == null) {
                Debug.LogError($"MonoScript not found. type: {typeof(T).Name}");

                return "";
            }

            // 상대 경로 반환
            string assetPath = AssetDatabase.GetAssetPath(scriptObj);

            return assetPath;
        }

        /// <summary>
        /// 에셋 파일의 상대 경로를 반환한다.
        /// </summary>
        public static string GetAssetPathFromScriptableObject<T>(T obj) where T : ScriptableObject {
            if(obj == null) {
                Debug.LogError("ScriptableObject is NULL.");

                return "";
            }

            MonoScript scriptObj = MonoScript.FromScriptableObject(obj);
            if(scriptObj == null) {
                Debug.LogError($"MonoScript not found. type: {typeof(T).Name}");

                return "";
            }

            // 상대 경로 반환
            string assetPath = AssetDatabase.GetAssetPath(scriptObj);

            return assetPath;
        }

        /// <summary>
        /// 에셋 파일의 상대 경로를 반환한다.
        /// </summary>
        public static string[] GetAssetPaths(string directory = "", string name = "", string typeString = "", string assetlabel = "") {
            string[] guids = FindAssets(directory, name, typeString, assetlabel);
            string[] relativePaths = guids.Select(g => AssetDatabase.GUIDToAssetPath(g)).ToArray();

            return relativePaths;
        }

        public static string[] GetAssetGuids(string directory = "", string name = "", string typeString = "", string assetlabel = "") {
            string[] guids = FindAssets(directory, name, typeString, assetlabel);

            return guids;
        }
        #endregion

        /// <summary>
        /// 에셋 파일의 Guid를 반환한다.
        /// </summary>
        private static string[] FindAssets(string directory = "", string name = "", string typeString = "", string assetlabel = "") {
            string optionString = GetOptionString(name, typeString, assetlabel);

            if(string.IsNullOrEmpty(directory)) {
                return AssetDatabase.FindAssets(optionString);
            }
            else {
                return AssetDatabase.FindAssets(optionString, new string[] { directory });
            }
        }

        /// <summary>
        /// <br/> Unity C# reference source
        /// <br/> Copyright (c) Unity Technologies. For terms of use, see
        /// <br/> https://unity3d.com/legal/licenses/Unity_Reference_Only_License
        /// <br/> 
        /// <br/> Supports the following syntax:
        /// <br/> 't:type' syntax (e.g 't:Texture2D' will show Texture2D objects)
        /// <br/> 'l:assetlabel' syntax (e.g 'l:architecture' will show assets with AssetLabel 'architecture')
        /// <br/> 'ref[:id]:path' syntax (e.g 'ref:1234' will show objects that references the object with instanceID 1234)
        /// <br/> 'v:versionState' syntax (e.g 'v:modified' will show objects that are modified locally)
        /// <br/> 's:softLockState' syntax (e.g 's:inprogress' will show objects that are modified by anyone (except you))
        /// <br/> 'a:area' syntax (e.g 'a:all' will s search in all assets, 'a:assets' will s search in assets folder only and 'a:packages' will s search in packages folder only)
        /// <br/> 'glob:path' syntax (e.g 'glob:Assets/**/*.{png|PNG}' will show objects in any subfolder with name ending by .png or .PNG)
        /// <br/> 
        /// <br/> 편의상 't:type'와 'l:assetlabel'만을 사용한다.
        /// </summary>
        /// <returns></returns>
        private static string GetOptionString(string name, string typeString, string assetlabel) {
            string result = "";

            if(!string.IsNullOrEmpty(name)) {
                result += $"{name}";
            }
            // 첫 시작 띄어쓰기
            if(!string.IsNullOrEmpty(typeString)) {
                result += $" t:{typeString}";
            }
            // 첫 시작 띄어쓰기
            if(!string.IsNullOrEmpty(assetlabel)) {
                result += $" l:{assetlabel}";
            }

            // filter의 시작이 공백이라면 해당 공백을 제거한다.
            if(!string.IsNullOrEmpty(result) && result[0] == ' ') {
                result = result.TrimStart();
            }

            return result;
        }
    }
}

#endif