using System.Collections.Generic;
using System.Linq;
using Mu3Library.Editor.FileUtil;
using Mu3Library.Scene;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Sample.Game
{
    public class GameSceneController : MonoBehaviour
    {



        private void Start()
        {
            SceneLoader.Instance.AddSceneLoadStartListener(StaticDatas.SCENE_NAME_MAIN, TestStart);
            SceneLoader.Instance.AddSceneLoadEndListener(StaticDatas.SCENE_NAME_MAIN, TestEnd);
            SceneLoader.Instance.AddSceneLoadProgressListener(StaticDatas.SCENE_NAME_MAIN, TestProgress);
        }

        private void OnDestroy()
        {
            SceneLoader.Instance.RemoveSceneLoadStartListener(StaticDatas.SCENE_NAME_MAIN, TestStart);
            SceneLoader.Instance.RemoveSceneLoadEndListener(StaticDatas.SCENE_NAME_MAIN, TestEnd);
            SceneLoader.Instance.RemoveSceneLoadProgressListener(StaticDatas.SCENE_NAME_MAIN, TestProgress);
        }

        private void TestStart()
        {
            Debug.Log("Scene Load Start");
        }

        private void TestEnd()
        {
            Debug.Log("Scene Load End");
        }

        private void TestProgress(float progress)
        {
            Debug.Log($"Scene Load Progress: {progress:F2}");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Q))
            {
                RequestLoadSingleSceneWithAssetPath(StaticDatas.SCENE_NAME_TITLE);
            }
            else if (Input.GetKeyDown(KeyCode.W))
            {
                RequestLoadSingleSceneWithAssetPath(StaticDatas.SCENE_NAME_MAIN);
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                RequestLoadSingleSceneWithAssetPath(StaticDatas.SCENE_NAME_GAME);
            }
            else if (Input.GetKeyDown(KeyCode.A))
            {
                RequestLoadAdditiveSceneWithAssetPath(StaticDatas.SCENE_NAME_ADDITIVE01);
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                RequestLoadAdditiveSceneWithAssetPath(StaticDatas.SCENE_NAME_ADDITIVE02);
            }
            else if (Input.GetKeyDown(KeyCode.D))
            {
                RequestLoadAdditiveSceneWithAssetPath(StaticDatas.SCENE_NAME_ADDITIVE03);
            }
            else if (Input.GetKeyDown(KeyCode.Z))
            {
                RequestUnloadAdditiveSceneWithAssetPath(StaticDatas.SCENE_NAME_ADDITIVE01);
            }
            else if (Input.GetKeyDown(KeyCode.X))
            {
                RequestUnloadAdditiveSceneWithAssetPath(StaticDatas.SCENE_NAME_ADDITIVE02);
            }
            else if (Input.GetKeyDown(KeyCode.C))
            {
                RequestUnloadAdditiveSceneWithAssetPath(StaticDatas.SCENE_NAME_ADDITIVE03);
            }
        }

        private void RequestLoadSingleSceneWithAssetPath(string sceneName)
        {
            string sceneAssetPath = GetScenePath(sceneName);

            if (string.IsNullOrEmpty(sceneAssetPath))
            {
                Debug.LogError($"Scene file not found. sceneName: {sceneName}, folder: {StaticDatas.SAMPLE_FOLDER_NAME}");
                return;
            }

            SceneLoader.Instance.LoadSingleSceneWithAssetPath(sceneAssetPath);
        }

        private void RequestLoadAdditiveSceneWithAssetPath(string sceneName)
        {
            string sceneAssetPath = GetScenePath(sceneName);

            if (string.IsNullOrEmpty(sceneAssetPath))
            {
                Debug.LogError($"Scene file not found. sceneName: {sceneName}, folder: {StaticDatas.SAMPLE_FOLDER_NAME}");
                return;
            }

            SceneLoader.Instance.LoadAdditiveSceneWithAssetPath(sceneAssetPath);
        }

        private void RequestUnloadAdditiveSceneWithAssetPath(string sceneName)
        {
            string sceneAssetPath = GetScenePath(sceneName);

            if (string.IsNullOrEmpty(sceneAssetPath))
            {
                Debug.LogError($"Scene file not found. sceneName: {sceneName}, folder: {StaticDatas.SAMPLE_FOLDER_NAME}");
                return;
            }

            SceneLoader.Instance.UnloadAdditiveSceneWithAssetPath(sceneAssetPath);
        }

        private string GetScenePath(string sceneName)
        {
            string sceneAssetPath = "";

            List<string> folders = FileFinder.FindAllFolders(name: StaticDatas.SAMPLE_FOLDER_NAME);
            foreach (string folder in folders)
            {
                var sceneAssets = FileFinder.LoadAllAssetsAtPath<SceneAsset>(folder);
                var sceneAsset = sceneAssets.Where(t => t.name == sceneName).FirstOrDefault();
                if (sceneAsset == null)
                {
                    continue;
                }

                sceneAssetPath = FileFinder.GetAssetPath(sceneAsset);
                if (!string.IsNullOrEmpty(sceneAssetPath))
                {
                    break;
                }
            }

            return sceneAssetPath;
        }
    }
}