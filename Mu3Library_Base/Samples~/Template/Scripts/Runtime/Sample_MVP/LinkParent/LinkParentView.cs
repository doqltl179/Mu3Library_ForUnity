using System;
using Mu3Library.UI.MVP;
using UnityEngine;

namespace Mu3Library.Sample.Template.MVP
{
    public class LinkParentView : View
    {
        [SerializeField] private RectTransform _childHost;

        public event Action OnInstantiateChildButtonClicked;
        public RectTransform ChildHost => _childHost != null ? _childHost : _rectTransform;


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
