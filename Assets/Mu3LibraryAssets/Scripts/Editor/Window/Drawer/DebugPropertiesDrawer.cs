using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Window.Drawer
{
    [CreateAssetMenu(fileName = FileName, menuName = MenuName, order = 0)]
    public class DebugPropertiesDrawer : Mu3WindowDrawer
    {
        public const string FileName = "DebugProperties";
        private const string ItemName = "Debug Properties";
        private const string MenuName = MenuRoot + "/" + ItemName;



        public override void OnGUIHeader()
        {
            DrawFoldoutHeader1(ItemName, ref _foldout);
        }

        public override void OnGUIBody()
        {
            DrawStruct(() =>
            {
                DrawSceneViewProperties();

                GUILayout.Space(8);

                DrawGameViewProperties();
            }, 20, 20, 0, 0);
        }

        private void DrawGameViewProperties()
        {
            DrawHeader3("[ Game View Properties ]");

            if (Camera.main != null)
            {
                EditorGUILayout.LabelField($"Camera Pixel Size -> {Camera.main.pixelWidth}x{Camera.main.pixelHeight}");
                EditorGUILayout.LabelField($"Camera ScaledPixel Size -> {Camera.main.scaledPixelWidth}x{Camera.main.scaledPixelHeight}");
            }
            else
            {
                EditorGUILayout.LabelField($"Main Camera not found...");
            }
        }

        private void DrawSceneViewProperties()
        {
            DrawHeader3("[ Scene View Properties ]");

            SceneView sceneView = SceneView.lastActiveSceneView;

            if (sceneView != null && sceneView.camera != null)
            {
                Camera sceneCam = sceneView.camera;

                EditorGUILayout.LabelField($"SceneView Pixel Size -> {sceneCam.pixelWidth}x{sceneCam.pixelHeight}");
                EditorGUILayout.LabelField($"SceneView ScaledPixel Size -> {sceneCam.scaledPixelWidth}x{sceneCam.scaledPixelHeight}");
            }
            else
            {
                EditorGUILayout.LabelField("SceneView or Scene Camera not found...");
            }
        }

    }
}