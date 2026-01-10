using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public interface IMVPManager
    {
        public Camera RenderCamera { get; }
        public MVPCanvasSettings CanvasSettings { get; set; }

        public event Action<IPresenter> OnWindowLoaded;
        public event Action<IPresenter> OnWindowOpened;
        public event Action<IPresenter> OnWindowClosed;
        public event Action<IPresenter> OnWindowUnloaded;

        public void CloseAllWithoutDefault(bool forceClose = false);
        public void CloseAll(bool forceClose = false);
        public void CloseFocused(bool forceClose = false);
        public bool Close(IPresenter presenter, bool forceClose = false);

        public IPresenter Open<TPresenter>() where TPresenter : class, IPresenter, new();
        public IPresenter Open<TPresenter>(Arguments args) where TPresenter : class, IPresenter, new();
        public IPresenter Open<TPresenter>(OutPanelSettings settings) where TPresenter : class, IPresenter, new();
        public IPresenter Open<TPresenter>(Arguments args, OutPanelSettings settings) where TPresenter : class, IPresenter, new();

        public void RemoveCullingMask(string layerName);
        public void AddCullingMask(string layerName);
        public void SetCullingMask(params string[] layerNames);

        public void RegisterViewResource(View viewResource);
        public void RegisterViewResources(IEnumerable<View> viewResources);
    }
}
