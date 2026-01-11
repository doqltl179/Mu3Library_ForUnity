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
        public string ObjectLayerName { get; }
        public string CanvasLayerName { get; }
        public int SortingOrder { get; }

        public CanvasGroup CanvasGroup { get; }
        public bool Interactable { get; }



        public void SetActiveView(bool active);
        public void OptimizeView();
        public void ForceDestroyView();
    }
}
