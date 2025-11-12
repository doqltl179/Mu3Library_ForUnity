using System;
using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public interface IPresenter
    {
        public IView View { get; }
        public Type ViewType { get; }

        public Type ModelType { get; }
        public Type ArgumentType{ get; }

        public bool IsViewExist { get; }
        public ViewState ViewState { get; }
        
        public RectTransform RectTransform { get; }
        public Canvas ViewCanvas { get; }
        public string LayerName { get; }
        public int SortingOrder { get; }



        public void SetActiveView(bool active);
        public void OptimizeView();
        public void ForceDestroyView();
    }
}
