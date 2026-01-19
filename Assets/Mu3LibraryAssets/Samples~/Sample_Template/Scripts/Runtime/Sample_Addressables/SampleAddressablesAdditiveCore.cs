using Mu3Library.DI;
using Mu3Library.Sample.Template.Global;
using Mu3Library.Scene;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.Addressables
{
    public class SampleAddressablesAdditiveCore : CoreBase
    {
        [Inject(typeof(SceneCore))] private ISceneLoader _sceneLoader;

        [Space(20)]
        [SerializeField] private Button _backButton;



        protected override void Start()
        {
            base.Start();

            _backButton.onClick.AddListener(OnBackButtonClicked);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }

        private void OnBackButtonClicked()
        {
            _sceneLoader.UnloadAdditiveSceneWithAddressables(SceneNames.SampleAddressablesAdditive);
        }
    }
}
