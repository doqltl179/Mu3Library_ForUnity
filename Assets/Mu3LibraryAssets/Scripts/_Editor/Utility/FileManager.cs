#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace Mu3Library.Editor {
    public static class FileManager {



        private static DirectoryInfo tempDirectoryInfo;
        private static StringBuilder tempStringBuilder = new StringBuilder();



        #region Utility
        public static T GetImporter<T>(string path) where T : AssetImporter {
            T importer = AssetImporter.GetAtPath(path) as T;
            if(importer == null) {
                Debug.LogWarning($"Importer not found. assetPath: {path}");
            }

            return importer;
        }

        public static string[] FindAssetGUIDs(string typeName, string name, string label, string[] folders = null) {
            return GetAssetGUIDs(typeName, name, label, folders);
        }

        public static string[] FindAssetPaths(string typeName, string name, string label, string[] folders = null) {
            string[] guids = GetAssetGUIDs(typeName, name, label, folders);
            return guids.Select(t => AssetDatabase.GUIDToAssetPath(t)).ToArray();
        }

        public static List<T> LoadAssets<T>(string typeName, string name, string label, string[] folders = null) where T : Object {
            string[] guids = GetAssetGUIDs(typeName, name, label, folders);
            string[] paths = guids.Select(t => AssetDatabase.GUIDToAssetPath(t)).ToArray();

            List<T> values = new List<T>();
            for(int i = 0; i < paths.Length; i++) {
                T obj = AssetDatabase.LoadAssetAtPath<T>(paths[i]);

                if(!values.Contains(obj)) {
                    values.Add(obj);
                }
            }

            return values;
        }
        #endregion

        private static string[] GetAssetGUIDs(string typeName, string name, string label, string[] folders = null) {
            string optionString = GetFindAssetsOptions(typeName, name, label);
            if(string.IsNullOrEmpty(optionString)) {
                Debug.Log("Please set options.");

                return new string[0];
            }

            string[] newFolders = GetExistFolders(folders);
            if(newFolders.Length > 0) {
                return AssetDatabase.FindAssets(optionString, newFolders.ToArray());
            }
            else {
                return AssetDatabase.FindAssets(optionString);
            }
        }

        //Asset Type          ex) 't:Prefab', 't:Texture2D', 't:AudioClip'
        //Asset Name          ex) 'name:Player'
        //Asset Label         ex) 'l:Important', 'l:Audio'
        private static string GetFindAssetsOptions(string typeName, string name, string label) {
            tempStringBuilder.Clear();

            if(!string.IsNullOrEmpty(name)) tempStringBuilder.Append($"{name}");
            if(!string.IsNullOrEmpty(typeName)) tempStringBuilder.Append($" t:{typeName}");
            if(!string.IsNullOrEmpty(label)) tempStringBuilder.Append($" l:{label}");

            return tempStringBuilder.ToString().Trim();
        }

        private static string[] GetExistFolders(string[] folders) {
            List<string> newFolders = new List<string>();
            if(folders != null) {
                for(int i = 0; i < folders.Length; i++) {
                    if(IsFolderExist(folders[i])) {
                        newFolders.Add(folders[i]);
                    }
                    else {
                        Debug.Log($"Folder not exist. folder: {folders[i]}");
                    }
                }
            }
            return newFolders.ToArray();
        }

        /// <summary>
        /// 'Assets' 폴더가 root인 경로를 넣으면 신기하게도 "FullName"이 system path로 출력됨.
        /// </summary>
        private static bool IsFolderExist(string folder) {
            tempDirectoryInfo = new DirectoryInfo(folder);
            return tempDirectoryInfo.Exists;
        }

        private static string GetProjectPath() {
            tempDirectoryInfo = new DirectoryInfo(Application.dataPath);
            return tempDirectoryInfo.Parent.FullName;
        }
    }
}
#endif