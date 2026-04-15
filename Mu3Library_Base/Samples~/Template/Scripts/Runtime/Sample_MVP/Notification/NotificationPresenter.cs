using Mu3Library.UI.MVP;

namespace Mu3Library.Sample.Template.MVP
{
    public class NotificationPresenter : Presenter<NotificationView, NotificationModel, NotificationArguments>
    {



        protected override void OpenFunc()
        {
            _view.SetMessage(_model.Message);
            _view.SetConfirmButton(_model.ConfirmText, _model.OnConfirm);
            if(_model.OnCancel != null)
            {
                _view.SetCancelButton(_model.CancelText, _model.OnCancel);
            }
        }
    }
}
