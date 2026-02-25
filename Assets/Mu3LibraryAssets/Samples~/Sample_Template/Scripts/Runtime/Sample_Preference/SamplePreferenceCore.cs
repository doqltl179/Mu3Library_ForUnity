using Mu3Library.Attribute;
using Mu3Library.DI;
using Mu3Library.Preference;
using Mu3Library.Sample.Template.Global;
using Mu3Library.Scene;
using UnityEngine;
using UnityEngine.UI;

// TODO: Add Contents

namespace Mu3Library.Sample.Template.Preference
{
    public class SamplePreferenceCore : CoreBase
    {
        [Inject(typeof(PreferenceCore))] private IPlayerPrefsLoader _playerPrefsLoader;
        [Inject(typeof(SceneCore))] private ISceneLoader _sceneLoader;

        [Title("UI Elements")]
        [SerializeField] private Button _backButton;



        private void OnEnable()
        {
            RegisterUiEvents();
        }

        private void OnDisable()
        {
            UnregisterUiEvents();
        }

        private void RegisterUiEvents()
        {
            _backButton.onClick.AddListener(OnBackButtonClicked);
        }

        private void UnregisterUiEvents()
        {
            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }

        #region UI Event
        private void OnBackButtonClicked()
        {
#if UNITY_EDITOR
            _sceneLoader.LoadSingleSceneWithAssetPath(SceneNames.GetSceneAssetPath(SceneNames.Main));
#else
            _sceneLoader.LoadSingleScene(SceneNames.Main);
#endif
        }
        #endregion
    }
}
