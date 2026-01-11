using System.Collections.Generic;
using Mu3Library.Addressable;
using Mu3Library.DI;
using Mu3Library.Extensions;
using Mu3Library.Sample.Template.Common;
using Mu3Library.Scene;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.Addressables
{
    public class SampleAddressablesCore : CoreBase
    {
        private IAddressablesManager _addressablesManager;
        private ISceneLoader _sceneLoader;

        [Space(20)]
        [SerializeField] private Image _progressBar;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private TextMeshProUGUI _progressText;

        [Space(20)]
        [SerializeField] private Button _backButton;

        private readonly string[] _allLabels = new string[]
        {
            "download-all",
            "test-image",
            "test-label",
            ""              // Test for none included label
        };



        protected override void Start()
        {
            base.Start();

            _addressablesManager = GetFromCore<CommonCore, IAddressablesManager>();
            _sceneLoader = GetFromCore<CommonCore, ISceneLoader>();

            SetProgress(0f);

            _backButton.onClick.AddListener(OnBackButtonClicked);

            _messageText.text = "Addressables Initializing...";
            _addressablesManager.Initialize(OnAddressablesInitialized);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }

        private void OnAddressablesInitialized()
        {
            _messageText.text = "Addressables Initialized. Checking for Catalog Updates...";
            _addressablesManager.CheckForCatalogUpdates(OnCheckForCatalogUpdatesCompleted);
        }

        private void OnCheckForCatalogUpdatesCompleted(IList<string> updatedCatalogs)
        {
            if (updatedCatalogs != null && updatedCatalogs.Count > 0)
            {
                _messageText.text = "Catalog Updates Found. Updating Catalogs...";
                _addressablesManager.UpdateCatalogs(OnUpdateCatalogsCompleted);
            }
            else
            {
                _messageText.text = "No Catalog Updates Found. Getting Download Size of Dependencies...";
                _addressablesManager.GetDownloadSizeAsync(_allLabels, OnGetDownloadSizeAsyncCompleted);
            }
        }

        private void OnUpdateCatalogsCompleted(IList<IResourceLocator> locators)
        {
            if (locators != null && locators.Count > 0)
            {
                _messageText.text = "Catalogs Updated. Getting Download Size of Dependencies...";
            }
            else
            {
                _messageText.text = "No Catalogs Updated. Getting Download Size of Dependencies...";
            }

            _addressablesManager.GetDownloadSizeAsync(_allLabels, OnGetDownloadSizeAsyncCompleted);
        }

        private void OnGetDownloadSizeAsyncCompleted(long size)
        {
            if (size > 0)
            {
                _messageText.text = $"Downloading {size.BytesToKB():F3}KB of dependencies...";

                _addressablesManager.DownloadDependenciesAsync(_allLabels, OnDownloadDependenciesCompleted, SetProgress);
            }
            else
            {
                _messageText.text = "No Dependencies to Download.";
                SetProgress(1f);
            }
        }

        private void OnDownloadDependenciesCompleted()
        {
            _messageText.text = "All Dependencies Downloaded.";
            SetProgress(1f);
        }

        private void OnBackButtonClicked()
        {
#if UNITY_EDITOR
            _sceneLoader.LoadSingleSceneWithAssetPath(SceneNames.GetSceneAssetPath(SceneNames.Main));
#else
            _sceneLoader.LoadSingleScene(SceneNames.Main);
#endif
        }

        private void SetProgress(float progress)
        {
            _progressBar.fillAmount = progress;
            _progressText.text = $"{(progress * 100f):F2}%";
        }
    }
}
