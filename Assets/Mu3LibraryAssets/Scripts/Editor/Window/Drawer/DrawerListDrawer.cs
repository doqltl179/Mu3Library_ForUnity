using System.Collections.Generic;
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

        private SerializedObject m_serializedObject;
        private SerializedObject _serializedObject
        {
            get
            {
                if (m_serializedObject == null)
                {
                    m_serializedObject = new SerializedObject(this);
                }

                return m_serializedObject;
            }
        }

        private SerializedProperty m_serializedProperties;
        private SerializedProperty _serializedProperties
        {
            get
            {
                if (m_serializedProperties == null)
                {
                    m_serializedProperties = _serializedObject.FindProperty(nameof(_drawers));
                }

                return m_serializedProperties;
            }
        }



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
            _serializedObject.Update();

            EditorGUILayout.PropertyField(_serializedProperties, true);

            if (!_serializedObject.ApplyModifiedProperties())
            {
                return;
            }

            RemoveOverlappedDrawers();
        }

        private void RemoveOverlappedDrawers()
        {
            bool drawerPropertyChanged = false;

            HashSet<Mu3WindowDrawer> drawers = new HashSet<Mu3WindowDrawer>();
            for (int i = 0; i < _drawers.Count; i++)
            {
                Mu3WindowDrawer drawer = _drawers[i];

                if (drawer == null)
                {
                    continue;
                }

                if (drawers.Contains(drawer))
                {
                    _drawers[i] = null;

                    drawerPropertyChanged = true;
                }
                else
                {
                    drawers.Add(drawer);
                }
            }

            if (drawerPropertyChanged)
            {
                _serializedObject.ApplyModifiedProperties();
            }
        }
    }
}