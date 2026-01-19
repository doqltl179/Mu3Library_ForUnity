using Mu3Library.DI;
using Mu3Library.Extensions;
using Mu3Library.Sample.Template.Global;
using Mu3Library.Scene;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
#if TEMPLATE_ADDRESSABLES_SUPPORT
using Mu3Library.Addressable;
using UnityEngine.AddressableAssets.ResourceLocators;
#endif

namespace Mu3Library.Sample.Template.Addressables
{
    public class SampleAddressablesCore : CoreBase
    {
#if TEMPLATE_ADDRESSABLES_SUPPORT
        private IAddressablesManager _addressablesManager;
#endif
        private ISceneLoader _sceneLoader;

        [Space(20)]
        [SerializeField] private Image _progressBar;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private TextMeshProUGUI _progressText;

        [Space(20)]
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _additiveSceneLoadButton;

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

            _sceneLoader = GetClassFromOtherCore<SceneCore, ISceneLoader>();

            SetProgress(0f);

            _backButton.onClick.AddListener(OnBackButtonClicked);
            _additiveSceneLoadButton.onClick.AddListener(OnAdditiveSceneLoadButtonClicked);

#if TEMPLATE_ADDRESSABLES_SUPPORT
            _addressablesManager = GetClassFromOtherCore<ResourceCore, IAddressablesManager>();

            _messageText.text = "Addressables Initializing...";
            _addressablesManager.Initialize(OnAddressablesInitialized);
#else
            _messageText.text = "Addressables is not installed.";
            Debug.LogWarning("Addressables support is disabled. Define TEMPLATE_ADDRESSABLES_SUPPORT to enable this sample.");
#endif
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _backButton.onClick.RemoveListener(OnBackButtonClicked);
            _additiveSceneLoadButton.onClick.RemoveListener(OnAdditiveSceneLoadButtonClicked);
        }

#if TEMPLATE_ADDRESSABLES_SUPPORT
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
#endif

        private void OnAdditiveSceneLoadButtonClicked()
        {
            _sceneLoader.LoadAdditiveSceneWithAddressables(SceneNames.SampleAddressablesAdditive);
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
