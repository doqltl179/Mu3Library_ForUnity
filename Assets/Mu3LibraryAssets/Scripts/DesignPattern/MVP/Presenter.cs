using UnityEngine;

namespace Mu3Library.UI.DesignPattern.MPV
{
    public abstract class Presenter<TView> : IPresenter where TView : View
    {
        protected TView _view = null;
        public IView View { get => _view; }

        public ViewState ViewState { get => _view == null ? ViewState.None : _view.ViewState; }
        public string ViewSortingLayerName { get => _view == null ? "" : _view.SortingLayerName; }
        public int ViewSortingOrder { get => _view == null ? -1 : _view.SortingOrder; }

        private TView _viewResource = null;

        public bool IsInstanceExist { get => _view != null; }
        public bool IsViewOpeningOrClosing { get => false; }



        public void OnLoad<TModel>(TModel model) where TModel : Model
        {
            if (_view == null)
            {
                return;
            }
            OnLoadFunc(model);
        }
        protected virtual void OnLoadFunc<TModel>(TModel model) where TModel : Model
        {
            _view.SetActive(true);
            _view.OnLoad(model);
        }

        public void OnUnload()
        {
            if (_view == null)
            {
                return;
            }
            OnUnloadFunc();
        }
        protected virtual void OnUnloadFunc()
        {
            _view.OnUnload();
            _view.SetActive(false);
        }

        public void Open()
        {
            if (_view == null)
            {
                return;
            }
            _view.Open();
        }

        public void Close()
        {
            if (_view == null)
            {
                return;
            }
            _view.Close();
        }

        public void CloseImmediately()
        {
            if (_view == null)
            {
                return;
            }
            _view.CloseImmediately();
        }

        public void CreateView(IView viewResource, Transform parent = null)
        {
            if (_view != null)
            {
                return;
            }

            if (viewResource == null)
            {
                return;
            }
            else if (viewResource.GetType() != typeof(TView))
            {
                Debug.LogError($"View Type Error. definedType: {typeof(TView)}, viewResourceType: {viewResource.GetType()}");
                return;
            }

            TView resource = viewResource as TView;
            if (resource == null)
            {
                Debug.LogError($"Resource is NULL. viewResourceType: {viewResource.GetType()}");
                return;
            }

            TView view = Object.Instantiate(resource, parent);
            view.SetActive(true);

            _view = view;
            _viewResource = resource;
        }

        public void DestroyView()
        {
            if (_view == null)
            {
                return;
            }
            _view.CloseImmediately();

            Object.Destroy(_view.gameObject);

            _view = null;
            _viewResource = null;
        }

        public void ChangeViewSortingOrder(int value)
        {
            if (_view == null)
            {
                return;
            }
            _view.ChangeSortingOrder(value);
        }
    }
}