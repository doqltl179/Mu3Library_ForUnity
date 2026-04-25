using System;
using Mu3Library.UI.MVP;

namespace Mu3Library.Sample.Template.MVP
{
    public class LinkParentView : View
    {
        public event Action OnInstantiateChildButtonClicked;


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
        public void OnClickInstantiateChildButton()
        {
            OnInstantiateChildButtonClicked?.Invoke();
        }
        #endregion
    }
}
