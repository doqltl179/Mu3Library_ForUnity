using UnityEngine;
using UnityEngine.Events;

namespace Mu3Library.UI.DesignPattern.MPV
{
    public interface IView
    {
        public bool IsOpened { get; }
        public bool IsOpeningOrClosing { get; }

        public RenderMode RenderMode { get; }
        public Camera RenderCamera { get; }
        public string SortingLayerName { get; }
        public int SortingOrder { get; }

        public void OnLoad<TModel>(TModel model) where TModel : Model;
        public void Open();
        public void Close();
        public void OnUnload();

        public void SetActive(bool value);
        public void ChangeSortingOrder(int value);

        public void ForceConfirm();
        public void ForceCancel();

        public void AddConfirmEvent(UnityAction onClick);
        public void RemoveConfirmEvent(UnityAction onClick);

        public void AddCancelEvent(UnityAction onClick);
        public void RemoveCancelEvent(UnityAction onClick);
    }
}
