#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor {
    public static class UtilFuncForEditor {
        #region Find Assets
        //Asset Type          ex) 't:Prefab', 't:Texture2D', 't:AudioClip'
        public static string TypeString {
            get => typeString;
            set => typeString = value;
        }
        private static string typeString = null;

        //Asset Label         ex) 'l:Important', 'l:Audio'
        public static string LabelString {
            get => labelString;
            set => labelString = value;
        }
        private static string labelString = "";

        //Asset Name          ex) 'name:Player'
        public static string NameString {
            get => nameString;
            set => nameString = value;
        }
        private static string nameString = "";

        //Asset Extention     ex) 'ext:png', 'ext:mp3'
        public static string ExtentionString {
            get => extentionString;
            set => extentionString = value;
        }
        private static string extentionString = "";

        //Asset Size          ex) 'size>1024', 'size<=4096'
        public static string SizeString {
            get => sizeString;
            set => sizeString = value;
        }
        private static string sizeString = "";

        //Directory           ex) 'a:Assets', 'a:Assets/Mu3LibraryAssets/Scripts'
        public static string DirectoryString {
            get => directoryString;
            set => directoryString = value;
        }
        private static string directoryString = "";

        public static bool FindAssetsSizeLower {
            get => findAssetsSizeLower;
            set => findAssetsSizeLower = value;
        }
        private static bool findAssetsSizeLower = true;

        private static StringBuilder findAssetsOptionBuilder = new StringBuilder();
        #endregion



        #region Utility

        #region Assets
        public static void ResetAssetsFindOptions() {
            typeString = "";
            labelString = "";
            nameString = "";
            extentionString = "";
            sizeString = "";
            directoryString = "";

            findAssetsSizeLower = true;
        }

        /// <summary>
        /// This will be return assets GUIDs
        /// </summary>
        public static string[] FindAssets() {
            SetFindAssetsOptions();

            return FindAssets(findAssetsOptionBuilder.ToString());
        }

        /// <summary>
        /// <br/> This will be return assets path.
        /// <br/> Root directory is 'Assets'.
        /// </summary>
        public static string[] FindAssetsPath() {
            SetFindAssetsOptions();

            string[] guids = FindAssets(findAssetsOptionBuilder.ToString());
            return FindAssetsPathWithGUIDs(guids);
        }

        public static List<T> LoadAssets<T>() where T : UnityEngine.Object {
            SetFindAssetsOptions();

            string[] guids = FindAssets(findAssetsOptionBuilder.ToString());
            string[] paths = FindAssetsPathWithGUIDs(guids);

            List<T> values = new List<T>();
            for(int i = 0; i < paths.Length; i++) {
                T obj = AssetDatabase.LoadAssetAtPath<T>(paths[i]);

                values.Add(obj);
            }

            return values;
        }

        private static string[] FindAssetsPathWithGUIDs(string[] guids) {
            if(guids != null && guids.Length > 0) {
                return guids.Select(AssetDatabase.GUIDToAssetPath).ToArray();
            }
            else {
                Debug.LogWarning("GUIDs not found.");
                return new string[0];
            }
        }

        private static string[] FindAssets(string optionString) {
            if(!string.IsNullOrEmpty(optionString)) {
                Debug.Log($"Find Assets. option: {optionString}");
                return AssetDatabase.FindAssets(optionString);
            }
            else {
                Debug.LogWarning("Find options not found.");
                return new string[0];
            }
        }

        private static void SetFindAssetsOptions() {
            findAssetsOptionBuilder.Clear();

            if(!string.IsNullOrEmpty(typeString)) findAssetsOptionBuilder.Append($"t:{typeString}");
            if(!string.IsNullOrEmpty(labelString)) findAssetsOptionBuilder.Append($"t:{labelString}");
            if(!string.IsNullOrEmpty(nameString)) findAssetsOptionBuilder.Append($"t:{nameString}");
            if(!string.IsNullOrEmpty(extentionString)) findAssetsOptionBuilder.Append($"t:{extentionString}");
            if(!string.IsNullOrEmpty(sizeString)) findAssetsOptionBuilder.Append($"t{(findAssetsSizeLower ? "<=" : ">=")}{sizeString}");
            if(!string.IsNullOrEmpty(directoryString)) findAssetsOptionBuilder.Append($"t:{directoryString}");
        }
        #endregion

        public static bool IsExistInBuildScenes(string sceneName) {
            return EditorBuildSettings.scenes.Any(t => System.IO.Path.GetFileNameWithoutExtension(t.path) == sceneName);
        }

        #endregion
    }
}
#endif