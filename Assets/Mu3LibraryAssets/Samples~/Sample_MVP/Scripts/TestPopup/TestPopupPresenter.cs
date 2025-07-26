using Mu3Library.UI.DesignPattern.MPV;

namespace Mu3Library.Sample.MVP
{
    public class TestPopupPresenter : Presenter<TestPopupView>
    {


        
        protected override void OnLoadFunc<TModel>(TModel model)
        {
            base.OnLoadFunc(model);

            _view.AddConfirmEvent(OnConfirm);
            _view.AddCancelEvent(OnCancel);
        }

        protected override void OnUnloadFunc()
        {
            base.OnUnloadFunc();

            _view.RemoveConfirmEvent(OnConfirm);
            _view.RemoveCancelEvent(OnCancel);
        }

        private void OnConfirm()
        {
            MVPManager.Instance.Close(this);
        }

        private void OnCancel()
        {
            MVPManager.Instance.Close(this);
        }
    }
}