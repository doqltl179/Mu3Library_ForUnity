using System;
using UnityEngine;
using UnityEngine.Events;

namespace Mu3Library.UI.DesignPattern.MPV
{
    public interface IView
    {
        public ViewState ViewState { get; }

        public RenderMode RenderMode { get; }
        public Camera RenderCamera { get; }
        public string SortingLayerName { get; }
        public int SortingOrder { get; }

        public void OnLoad<TModel>(TModel model) where TModel : Model;
        public void Open();
        public void Close();
        public void CloseImmediately();
        public void OnUnload();

        public void SetActive(bool value);
        public void ChangeSortingOrder(int value);

        public void ForceConfirm();
        public void ForceCancel();

        public void AddConfirmEvent(UnityAction onClick);
        public void RemoveConfirmEvent(UnityAction onClick);

        public void AddCancelEvent(UnityAction onClick);
        public void RemoveCancelEvent(UnityAction onClick);

        public void SetRenderModeToDefault(RenderMode mode);
        public void SetRenderModeToWorld(string sortingLayerName = "Default", int sortingOrder = 0, Camera eventCamera = null);
        public void SetRenderModeToCamera(string sortingLayerName = "Default", int sortingOrder = 0, Camera eventCamera = null, float cameraDistance = 100f);
        public void SetRenderModeToOverlay(string sortingLayerName = "Default", int sortingOrder = 0);
    }
}
