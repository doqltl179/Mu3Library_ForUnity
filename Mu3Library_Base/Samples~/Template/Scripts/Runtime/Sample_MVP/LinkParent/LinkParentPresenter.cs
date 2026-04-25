using Mu3Library.UI.MVP;
using UnityEngine;

namespace Mu3Library.Sample.Template.MVP
{
    public class LinkParentPresenter : Presenter<LinkParentView, LinkParentModel, LinkParentArguments>
    {
        protected override void LoadFunc()
        {

        }

        protected override void OpenFunc()
        {
            _view.OnInstantiateChildButtonClicked += OnInstantiateChildButtonClicked;
        }

        protected override void CloseFunc()
        {
            _view.OnInstantiateChildButtonClicked -= OnInstantiateChildButtonClicked;
        }

        protected override void UnloadFunc()
        {

        }

        private void OnInstantiateChildButtonClicked()
        {
            var child = OpenAsChild<LinkChildPresenter>();
            child.AnchoredPosition = new Vector2(Random.Range(-800, 800), Random.Range(-400, 400));
        }
    }
}
