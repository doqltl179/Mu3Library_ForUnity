using Mu3Library.Attribute;
using Mu3Library.DI;
using Mu3Library.Resource;
using Mu3Library.Sample.Template.Global;
using Mu3Library.Scene;
using UnityEngine;
using UnityEngine.UI;

#if TEMPLATE_INPUTSYSTEM_SUPPORT
using Mu3Library.IS;
using UnityEngine.InputSystem;
#endif

namespace Mu3Library.Sample.Template.IS
{
    public class SampleISCore : CoreBase
    {
#if TEMPLATE_INPUTSYSTEM_SUPPORT
        [Inject(typeof(ISCore))] private IInputSystemManager _inputSystemManager;
#endif

        [Inject(typeof(SceneCore))] private ISceneLoader _sceneLoader;
        [Inject(typeof(ResourceCore))] private IResourceLoader _resourceLoader;

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

        protected override void Start()
        {
            base.Start();

#if TEMPLATE_INPUTSYSTEM_SUPPORT
            _inputSystemManager.AddInputActionAsset(_resourceLoader.Load<InputActionAsset>("Sample_IS/InputSystemActions"));
            _inputSystemManager.SetInputActionAssetEnable(true);
#endif
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

#if TEMPLATE_INPUTSYSTEM_SUPPORT
            _inputSystemManager.SetInputActionAssetEnable(false);
#endif
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
