using System.Collections.Generic;
using Mu3Library.DI;
using Mu3Library.Localization;
using Mu3Library.Sample.Template.Common;
using Mu3Library.Scene;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template
{
    public class SampleLocalizationCore : CoreBase
    {
        private ILocalizationManager _localizationManager;
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

            _localizationManager = GetFromCore<CommonCore, ILocalizationManager>();
            _sceneLoader = GetFromCore<CommonCore, ISceneLoader>();

            _localeDropdown.ClearOptions();

            _localizationManager.AddLocaleChangedEvent(OnLocaleChanged);

            _backButton.onClick.AddListener(OnBackButtonClicked);
            _localeDropdown.onValueChanged.AddListener(OnLocaleDropdownValueChanged);

            _textComponent.text = "Localization Initializing...";
            _localizationManager.Initialize(OnLocalizationInitialized);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _localizationManager.RemoveLocaleChangedEvent(OnLocaleChanged);

            _backButton.onClick.RemoveListener(OnBackButtonClicked);
            _localeDropdown.onValueChanged.RemoveListener(OnLocaleDropdownValueChanged);
        }

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
        }

        private void SetDropdownOptions()
        {
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
        }
    }
}
