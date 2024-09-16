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



        public static string[] FindAssetGUIDsWithType(string type, string[] folders = null) {
            return FindAssetGUIDs(type, "", "", folders);
        }

        public static string[] FindAssetGUIDsWithName(string name, string[] folders = null) {
            return FindAssetGUIDs("", name, "", folders);
        }

        public static string[] FindAssetGUIDsWithLabel(string label, string[] folders = null) {
            return FindAssetGUIDs("", "", label, folders);
        }

        public static string[] FindAssetGUIDsWithTypeAndName(string type, string name, string[] folders = null) {
            return FindAssetGUIDs(type, name, "", folders);
        }
        
        public static string[] FindAssetGUIDsWithTypeAndLabel(string type, string label, string[] folders = null) {
            return FindAssetGUIDs(type, "", label, folders);
        }

        public static string[] FindAssetGUIDsWithTypeAndNameAndLabel(string type, string name, string label, string[] folders = null) {
            return FindAssetGUIDs(type, name, label, folders);
        }



        public static string[] FindAssetPathsWithType(string type, string[] folders = null) {
            return FindAssetPaths(type, "", "", folders);
        }

        public static string[] FindAssetPathsWithName(string name, string[] folders = null) {
            return FindAssetPaths("", name, "", folders);
        }

        public static string[] FindAssetPathsWithLabel(string label, string[] folders = null) {
            return FindAssetPaths("", "", label, folders);
        }

        public static string[] FindAssetPathsWithTypeAndName(string type, string name, string[] folders = null) {
            return FindAssetPaths(type, name, "", folders);
        }

        public static string[] FindAssetPathsWithTypeAndLabel(string type, string label, string[] folders = null) {
            return FindAssetPaths(type, "", label, folders);
        }

        public static string[] FindAssetPathsWithTypeAndNameAndLabel(string type, string name, string label, string[] folders = null) {
            return FindAssetPaths(type, name, label, folders);
        }



        public static List<T> LoadAssetsWithType<T>(string type, string[] folders = null) where T : Object {
            return LoadAssets<T>(type, "", "", folders);
        }

        public static List<T> LoadAssetsWithName<T>(string name, string[] folders = null) where T : Object {
            return LoadAssets<T>("", name, "", folders);
        }

        public static List<T> LoadAssetsWithLabel<T>(string label, string[] folders = null) where T : Object {
            return LoadAssets<T>("", "", label, folders);
        }

        public static List<T> LoadAssetsWithTypeAndName<T>(string type, string name, string[] folders = null) where T : Object {
            return LoadAssets<T>(type, name, "", folders);
        }

        public static List<T> LoadAssetsWithTypeAndLabel<T>(string type, string label, string[] folders = null) where T : Object {
            return LoadAssets<T>(type, "", label, folders);
        }

        public static List<T> LoadAssetsWithTypeAndNameAndLabel<T>(string type, string name, string label, string[] folders = null) where T : Object {
            return LoadAssets<T>(type, name, label, folders);
        }
        #endregion

        private static List<T> LoadAssets<T>(string type, string name, string label, string[] folders = null) where T : Object {
            string[] paths = FindAssetPaths(type, name, label, folders);

            List<T> values = new List<T>();
            for(int i = 0; i < paths.Length; i++) {
                T obj = AssetDatabase.LoadAssetAtPath<T>(paths[i]);

                if(!values.Contains(obj)) {
                    values.Add(obj);
                }
            }

            return values;
        }

        private static string[] FindAssetPaths(string type, string name, string label, string[] folders = null) {
            string[] guids = FindAssetGUIDs(type, name, label, folders);
            return guids.Select(t => AssetDatabase.GUIDToAssetPath(t)).ToArray();
        }

        private static string[] FindAssetGUIDs(string type, string name, string label, string[] folders = null) {
            string optionString = GetFindAssetsOptions(type, name, label);
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