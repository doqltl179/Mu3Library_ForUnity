using Mu3Library.Audio;
using Mu3Library.DI;
using Mu3Library.Sample.Template.Global;
using Mu3Library.Scene;
using Mu3Library.Attribute;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.Audio3D
{
    public class SampleAudio3DCore : CoreBase
    {
        [Inject(typeof(AudioCore))] private IAudioManager _audioManager;
        [Inject(typeof(SceneCore))] private ISceneLoader _sceneLoader;

        [Title("UI Elements")]
        [SerializeField] private Button _backButton;

        [Title("Sfx")]
        [SerializeField] private AudioClip _sfx;

        [Title("Etc")]
        [SerializeField] private MouseClickHandler _mouseClickHandler;
        [SerializeField] private AudioSourceSettings _audioSettings = AudioSourceSettings.Standard;



        protected override void OnEnable()
        {
            base.OnEnable();

            RegisterUiEvents();

            _mouseClickHandler.OnClick += OnMouseClick;
        }

        private void OnDisable()
        {
            UnregisterUiEvents();

            _mouseClickHandler.OnClick -= OnMouseClick;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _audioManager?.Stop();
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

        private void OnMouseClick(Vector3 mousePosition)
        {
            Ray ray = Camera.main.ScreenPointToRay(mousePosition);
            if (!Physics.Raycast(ray, out var hit))
            {
                return;
            }

            Debug.Log($"Hit Point: {hit.point}");

            PlaySfx(_sfx, hit.point);
        }

        private void PlaySfx(AudioClip clip, Vector3 position)
        {
            _audioManager?.PlaySfx(clip, _audioSettings, position);
        }
    }
}
