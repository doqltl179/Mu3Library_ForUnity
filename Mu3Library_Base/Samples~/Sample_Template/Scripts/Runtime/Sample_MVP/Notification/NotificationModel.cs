using System;
using Mu3Library.UI.MVP;

namespace Mu3Library.Sample.Template.MVP
{
    public class NotificationModel : Model<NotificationArguments>
    {
        private string _message = "This is Notification model.";
        public string Message => _message;

        private string _confirmText = "Confirm";
        public string ConfirmText => _confirmText;
        private string _cancelText = "Cancel";
        public string CancelText => _cancelText;

        public Action OnConfirm = null;
        public Action OnCancel = null;



        public override void Init(NotificationArguments args)
        {
            _message = args.Message;
            _confirmText = args.ConfirmText;
            _cancelText = args.CancelmText;

            OnConfirm = args.OnConfirm;
            OnCancel = args.OnCancel;
        }
    }
}