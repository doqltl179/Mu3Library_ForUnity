using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public interface IView
    {
        public ViewState ViewState { get; }

        public RectTransform RectTransform { get; }
        public Canvas Canvas { get; }
        public string LayerName { get; }
        public int SortingOrder { get; }

        public CanvasGroup CanvasGroup { get; }
        public bool Interactable { get; }
        public float Alpha { get; set; }

        

        public void SetSortingLayer(string layerName);
        public void SetSortingOrder(int sortingOrder);
        public void OverwriteInto(Canvas target);
    }
}
