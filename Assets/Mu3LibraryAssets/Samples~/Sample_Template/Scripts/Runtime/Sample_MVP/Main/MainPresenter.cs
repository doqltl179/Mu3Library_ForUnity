using Mu3Library.Sample.Template.Common;
using Mu3Library.UI.MVP;

namespace Mu3Library.Sample.Template.MVP
{
    public class MainPresenter : Presenter<MainView, MainModel, MainArguments>
    {



        public override void Load()
        {
            base.Load();

            _view.ClearAllKeyDescriptions();

            _view.OnBackButtonClick += OnBackButtonClick;
        }

        public override void Unload()
        {
            base.Unload();

            _view.OnBackButtonClick -= OnBackButtonClick;
        }

        public override void Open()
        {
            base.Open();

            foreach (var pair in _model.KeyDescriptions)
            {
                _view.AddKeyDescription(pair.Key, pair.Value);
            }
        }

        private void OnBackButtonClick()
        {
#if UNITY_EDITOR
            _model.SceneLoader.LoadSingleSceneWithAssetPath(SceneNames.GetSceneAssetPath(SceneNames.Main));
#else
            _model.SceneLoader.LoadSingleScene(SceneNames.Main);
#endif
        }
    }
}