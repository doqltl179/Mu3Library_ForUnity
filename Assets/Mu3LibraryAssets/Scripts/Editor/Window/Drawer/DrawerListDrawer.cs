using System.Collections.Generic;
using System.Linq;
using Mu3Library.Editor.FileUtil;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class DrawerListDrawer : Mu3WindowDrawer
    {
        public const string FileName = "DrawerList";
        private const string ItemName = "Drawer List";
        private const string MenuName = MenuRoot + "/" + ItemName;

        [SerializeField, HideInInspector] private List<Mu3WindowDrawer> _drawers = new();
        public List<Mu3WindowDrawer> Drawers => _drawers;

        private SerializedObject _serializedObject = null;
        private SerializedProperty _drawersProperties = null;



        public override void OnGUIHeader()
        {

        }

        public override void OnGUIBody()
        {
            DrawStruct(() =>
            {
                DrawDrawerList();
            }, 20, 20, 10, 10);
        }

        private void DrawDrawerList()
        {
            if (_serializedObject == null)
            {
                _serializedObject = new SerializedObject(this);
            }
            if (_drawersProperties == null && _serializedObject != null)
            {
                _drawersProperties = _serializedObject.FindProperty(nameof(_drawers));
            }

            if (_serializedObject != null && _drawersProperties != null)
            {
                EditorGUILayout.PropertyField(_drawersProperties, true);
                _serializedObject.ApplyModifiedProperties();

                RefreshDrawerProperties();
            }
        }

        private void RefreshDrawerProperties()
        {
            bool initiate = RemoveUnusableDrawers();
            
            if (!initiate)
            {
                initiate = _drawers.Count != _drawersProperties.arraySize;
            }

            if(!initiate)
            {
                return;
            }

            _serializedObject = new SerializedObject(this);
            _drawersProperties = _serializedObject.FindProperty(nameof(_drawers));
        }

        private bool RemoveUnusableDrawers()
        {
            bool isChanged = false;

            for (int i = 0; i < _drawers.Count - 1; i++)
            {
                Mu3WindowDrawer currentDrawer = _drawers[i];

                // Remove empty
                if (_drawers[i] == null)
                {
                    _drawers.RemoveAt(i);
                    i--;
                    isChanged = true;
                    continue;
                }

                // Remove overlap
                for (int j = i + 1; j < _drawers.Count; j++)
                {
                    Mu3WindowDrawer compareDrawer = _drawers[j];
                    if (currentDrawer == compareDrawer)
                    {
                        string folderPath = FileFinder.GetAssetPath(compareDrawer);
                        Debug.LogWarning($"Drawer overlapped. path: {folderPath}");

                        FileFinder.PingObject(compareDrawer);
                        _drawers.RemoveAt(j);
                        j--;
                        isChanged = true;
                    }
                }
            }

            return isChanged;
        }
    }
}