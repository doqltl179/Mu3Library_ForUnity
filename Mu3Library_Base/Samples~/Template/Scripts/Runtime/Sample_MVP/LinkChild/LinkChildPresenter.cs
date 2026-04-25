using Mu3Library.UI.MVP;

namespace Mu3Library.Sample.Template.MVP
{
    public class LinkChildPresenter : Presenter<LinkChildView, LinkChildModel, LinkChildArguments>
    {
        protected override void LoadFunc()
        {

        }

        protected override void OpenFunc()
        {
            _view.OnCloseButtonClicked += OnCloseButtonClicked;
        }

        protected override void CloseFunc()
        {
            _view.OnCloseButtonClicked -= OnCloseButtonClicked;
        }

        protected override void UnloadFunc()
        {

        }

        private void OnCloseButtonClicked()
        {
            CloseSelf();
        }
    }
}
