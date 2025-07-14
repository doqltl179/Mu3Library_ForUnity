using Mu3Library.UI.DesignPattern.MPV;

namespace Mu3Library.Sample.MVP
{
    public class TestPopupPresenter : Presenter
    {



        public override void Open()
        {
            base.Open();

            _view.AddConfirmEvent(OnClickConfirm);
            _view.AddCancelEvent(OnClickCancel);
        }

        public override void Close()
        {
            base.Close();

            _view.RemoveConfirmEvent(OnClickConfirm);
            _view.RemoveCancelEvent(OnClickCancel);
        }

        private void OnClickConfirm()
        {
            MVPManager.Instance.Close(_view);
        }

        private void OnClickCancel()
        {
            MVPManager.Instance.Close(_view);
        }
    }
}