using System.Collections.Generic;
using System.Linq;
using Mu3Library.DI;
using Mu3Library.Sample.Template.Global;
using Mu3Library.Scene;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.Main
{
    public class MainCore : CoreBase
    {
        private ISceneLoader _sceneLoader;

        [Space(20)]
        [SerializeField] private SampleSceneEntry _sceneEntryResource;
        private readonly List<SampleSceneEntry> _sceneEntries = new();

        [SerializeField] private Image _thumbnailImage;
        [SerializeField] private TextMeshProUGUI _descriptionText;

        private SampleSceneEntry _currentHighlightedEntry;

        [System.Serializable]
        private class SampleSceneInfo
        {
            public string SceneName;
            public string Title;

            public Sprite Thumbnail;
            [TextArea] public string Description;
        }
        [Space(20)]
        [SerializeField] private List<SampleSceneInfo> _sampleSceneInfos = new();



        protected override void Start()
        {
            base.Start();

            _sceneLoader = GetClassFromOtherCore<SceneCore, ISceneLoader>();

            _sceneEntryResource.gameObject.SetActive(false);

            UpdateInfoPanel();
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
            entry.SetSceneName(info.SceneName);
            entry.SetActiveHighlight(false);

            entry.ApplyOnClickListener(OnClickListener);
            entry.ApplyOnPointerEntered(OnPointerEntered);
            entry.ApplyOnPointerExited(OnPointerExited);

            _sceneEntries.Add(entry);
        }

        private void OnClickListener(SampleSceneEntry entry)
        {
            if (entry.SceneName == SceneNames.SampleAddressables)
            {
                _sceneLoader.LoadSingleSceneWithAddressables(entry.SceneName);
            }
            else
            {
#if UNITY_EDITOR
                _sceneLoader.LoadSingleSceneWithAssetPath(SceneNames.GetSceneAssetPath(entry.SceneName));
#else
                _sceneLoader.LoadSingleScene(entry.SceneName);
#endif
            }
        }

        private void OnPointerExited(SampleSceneEntry exitedEntry)
        {
            if (_currentHighlightedEntry == exitedEntry)
            {
                _currentHighlightedEntry.SetActiveHighlight(false);
                _currentHighlightedEntry = null;

                UpdateInfoPanel();
            }
        }

        private void OnPointerEntered(SampleSceneEntry newEntry)
        {
            if (_currentHighlightedEntry != null)
            {
                _currentHighlightedEntry.SetActiveHighlight(false);
            }

            _currentHighlightedEntry = newEntry;

            if (_currentHighlightedEntry != null)
            {
                _currentHighlightedEntry.SetActiveHighlight(true);
            }

            UpdateInfoPanel();
        }

        private void UpdateInfoPanel()
        {
            SampleSceneInfo info = _currentHighlightedEntry == null ?
                null :
                _sampleSceneInfos.Where(t => t.SceneName == _currentHighlightedEntry.SceneName).FirstOrDefault();

            if (info == null)
            {
                _thumbnailImage.sprite = null;
                _descriptionText.text = string.Empty;
            }
            else
            {
                _thumbnailImage.sprite = info.Thumbnail;
                _descriptionText.text = info.Description;
            }

            _thumbnailImage.gameObject.SetActive(_thumbnailImage.sprite != null);
        }
    }
}
