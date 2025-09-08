using UnityEngine;

namespace Mu3Library.Base.UI.DesignPattern.MPV
{
    public interface IPresenter
    {
        public IView View { get; }
        public ViewState ViewState { get; }

        public string ViewSortingLayerName { get; }
        public int ViewSortingOrder { get; }

        public void OnLoad<TModel>(TModel model) where TModel : Model;
        public void Open();
        public void Close();
        public void CloseImmediately();
        public void OnUnload();

        public void CreateView(IView viewResource, Transform parent = null);
        public void DestroyView();
        public void ChangeViewSortingOrder(int value);
    }
}
