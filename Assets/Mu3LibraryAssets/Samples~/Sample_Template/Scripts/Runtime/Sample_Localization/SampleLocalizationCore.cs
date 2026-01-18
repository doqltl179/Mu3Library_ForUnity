using System.Collections.Generic;
using Mu3Library.DI;
using Mu3Library.Sample.Template.Global;
using Mu3Library.Scene;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if TEMPLATE_LOCALIZATION_SUPPORT
using Mu3Library.Localization;
using UnityEngine.Localization;
#endif

namespace Mu3Library.Sample.Template.Localization
{
    public class SampleLocalizationCore : CoreBase
    {
#if TEMPLATE_LOCALIZATION_SUPPORT
        private ILocalizationManager _localizationManager;
#endif
        private ISceneLoader _sceneLoader;

        [Space(20)]
        [SerializeField] private TMP_Dropdown _localeDropdown;
        [SerializeField] private TextMeshProUGUI _textComponent;
        [SerializeField] private bool _getStringAsync = false;

        [Space(20)]
        [SerializeField] private Button _backButton;



        protected override void Start()
        {
            base.Start();

            _sceneLoader = GetClassFromOtherCore<SceneCore, ISceneLoader>();

            _localeDropdown.ClearOptions();

            _backButton.onClick.AddListener(OnBackButtonClicked);
#if TEMPLATE_LOCALIZATION_SUPPORT
            _localizationManager = GetClassFromOtherCore<LocalizationCore, ILocalizationManager>();
            _localizationManager.AddLocaleChangedEvent(OnLocaleChanged);
            _localeDropdown.onValueChanged.AddListener(OnLocaleDropdownValueChanged);

            _textComponent.text = "Localization Initializing...";
            _localizationManager.Initialize(OnLocalizationInitialized);
#else
            _textComponent.text = "Localization is not installed.";
            Debug.LogWarning("Localization support is disabled. Define TEMPLATE_LOCALIZATION_SUPPORT to enable this sample.");
#endif
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

#if TEMPLATE_LOCALIZATION_SUPPORT
            _localizationManager.RemoveLocaleChangedEvent(OnLocaleChanged);

            _localeDropdown.onValueChanged.RemoveListener(OnLocaleDropdownValueChanged);
#endif
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }

#if TEMPLATE_LOCALIZATION_SUPPORT
        private void OnLocaleChanged(Locale _)
        {
            SetTextToTestString();
        }

        private void OnLocalizationInitialized()
        {
            SetDropdownOptions();
            SetTextToTestString();
        }

        private void OnLocaleDropdownValueChanged(int index)
        {
            if (index < 0)
            {
                return;
            }

            var locales = _localizationManager.GetAvailableLocales();
            if (index >= locales.Count)
            {
                return;
            }

            _localizationManager.ChangeLocale(locales[index]);
        }
#endif

        private void OnBackButtonClicked()
        {
#if UNITY_EDITOR
            _sceneLoader.LoadSingleSceneWithAssetPath(SceneNames.GetSceneAssetPath(SceneNames.Main));
#else
            _sceneLoader.LoadSingleScene(SceneNames.Main);
#endif
        }

        private void SetTextToTestString()
        {
#if TEMPLATE_LOCALIZATION_SUPPORT
            if (_getStringAsync)
            {
                _textComponent.text = "Text Loading...";

            _localizationManager.GetStringAsync("TestStringTable", "hello", (result) =>
            {
                if(this == null || gameObject == null)
                {
                    return;
                }

                _textComponent.text = result;
            });
            }
            else
            {
                _textComponent.text = _localizationManager.GetString("TestStringTable", "hello");
            }
#else
            _textComponent.text = "Localization is not installed.";
#endif
        }

        private void SetDropdownOptions()
        {
#if TEMPLATE_LOCALIZATION_SUPPORT
            _localeDropdown.ClearOptions();

            var locales = _localizationManager.GetAvailableLocales();
            var options = new List<string>();

            int currentIndex = 0;
            for (int i = 0; i < locales.Count; i++)
            {
                options.Add(locales[i].Identifier.CultureInfo.EnglishName);
                if (locales[i] == _localizationManager.CurrentLocale)
                {
                    currentIndex = i;
                }
            }

            _localeDropdown.AddOptions(options);
            _localeDropdown.value = currentIndex;
#endif
        }
    }
}
