using System.Collections.Generic;
using System.Linq;
using Mu3Library.DI;
using Mu3Library.Sample.Template.Common;
using Mu3Library.Scene;
using UnityEngine;

namespace Mu3Library.Sample.Template.Main
{
    public class MainCore : CoreBase
    {
        private ISceneLoader _sceneLoader;

        [Space(20)]
        [SerializeField] private SampleSceneEntry _sceneEntryResource;
        private readonly List<SampleSceneEntry> _sceneEntries = new();

        [System.Serializable]
        private class SampleSceneInfo
        {
            public string SceneName;
            public string Title;
        }
        [SerializeField] private List<SampleSceneInfo> _sampleSceneInfos = new();



        protected override void Start()
        {
            base.Start();

            _sceneLoader = GetFromCore<CommonCore, ISceneLoader>();

            _sceneEntryResource.gameObject.SetActive(false);

            CreateSceneEntries();
        }

        private void CreateSceneEntries()
        {
            if (_sceneEntries.Count > 0)
            {
                return;
            }

            foreach (var info in _sampleSceneInfos)
            {
                CreateSceneEntry(info);
            }
        }

        private void CreateSceneEntry(SampleSceneInfo info)
        {
            var entry = Instantiate(_sceneEntryResource, _sceneEntryResource.transform.parent);
            entry.gameObject.SetActive(true);

            string title = SceneNames.SampleSceneNames.Contains(info.SceneName)
                ? info.Title
                : info.SceneName;
            entry.SetTitle(title);

            entry.ApplyOnClickListener(() =>
            {
#if UNITY_EDITOR
                _sceneLoader.LoadSingleSceneWithAssetPath(SceneNames.GetSceneAssetPath(info.SceneName));
#else
                _sceneLoader.LoadSingleScene(info.SceneName);
#endif
            });

            _sceneEntries.Add(entry);
        }
    }
}
