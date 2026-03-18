using System;
using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public interface IPresenter
    {
        public Type ViewType { get; }

        public Type ModelType { get; }
        public Type ArgumentType{ get; }

        public bool IsViewExist { get; }
        public ViewState ViewState { get; }
        
        public string ObjectLayerName { get; }
        public string CanvasLayerName { get; }
        public int SortingOrder { get; }

        public bool Interactable { get; }
    }
}
