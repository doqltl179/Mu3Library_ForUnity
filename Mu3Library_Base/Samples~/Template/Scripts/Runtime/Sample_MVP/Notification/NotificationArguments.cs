using System;
using Mu3Library.UI.MVP;

namespace Mu3Library.Sample.Template.MVP
{
    public class NotificationArguments : Arguments
    {
        public string Message = "This is Notification model.";

        public string ConfirmText = "Confirm";
        public Action OnConfirm = null;

        public string CancelmText = "Cancel";
        public Action OnCancel = null;
    }
}