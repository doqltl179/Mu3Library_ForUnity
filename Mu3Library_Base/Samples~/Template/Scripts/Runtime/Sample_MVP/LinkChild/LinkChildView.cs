using System;
using Mu3Library.UI.MVP;

namespace Mu3Library.Sample.Template.MVP
{
    public class LinkChildView : View
    {
        public event Action OnCloseButtonClicked;



        protected override void LoadFunc()
        {

        }

        protected override void OpenStart()
        {

        }

        protected override void CloseStart()
        {

        }

        protected override void UnloadFunc()
        {

        }

        #region UI Event
        public void OnClickCloseButton()
        {
            OnCloseButtonClicked?.Invoke();
        }
        #endregion
    }
}
