using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public interface IView
    {
        public ViewState ViewState { get; }

        public string ObjectLayerName { get; }
        public string CanvasLayerName { get; }
        public int SortingOrder { get; }

        public bool Interactable { get; }
        public float Alpha { get; set; }

        public Vector2 AnchoredPosition { get; set; }
        public Vector3 LocalScale { get; set; }

        public void SetSortingLayer(string layerName);
        public void SetSortingOrder(int sortingOrder);
    }

}
