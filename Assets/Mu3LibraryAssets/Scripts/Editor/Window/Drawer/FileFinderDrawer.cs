using System.Collections.Generic;
using System.Linq;
using Mu3Library.Editor.FileUtil;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class FileFinderDrawer : Mu3WindowDrawer
    {
        public const string FileName = "FileFinder";
        private const string ItemName = "File Finder";
        private const string MenuName = MenuRoot + "/" + ItemName;

        [SerializeField, HideInInspector] private string _guid;



        public override void OnGUIHeader()
        {
            DrawFoldoutHeader1(ItemName, ref _foldout);
        }

        public override void OnGUIBody()
        {
            DrawStruct(() =>
            {
                DrawPropertyField();
                
                if(string.IsNullOrEmpty(_guid))
                {
                    return;
                }

                GUILayout.Space(8);

                DrawButtons();
            }, 20, 20, 0, 0);
        }

        private void DrawPropertyField()
        {
            _guid = EditorGUILayout.TextField("GUID", _guid);
        }

        private void DrawButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Select", GUILayout.Width(60), GUILayout.Height(30)))
            {
                Object obj = FileFinder.LoadAssetAtGUID(_guid);
                PingObject(obj, true);
            }

            if (GUILayout.Button("Ping", GUILayout.Width(60), GUILayout.Height(30)))
            {
                Object obj = FileFinder.LoadAssetAtGUID(_guid);
                PingObject(obj);
            }

            EditorGUILayout.EndHorizontal();
        }
        
        private void PingObject(Object obj, bool selectObject = false)
        {
            if (obj == null)
            {
                Debug.LogError($"Asset not found. guid: {_guid}");
                return;
            }

            FileFinder.PingObject(obj, selectObject);
        }
    }
}