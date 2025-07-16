namespace Mu3Library.UI.DesignPattern.MPV
{
    public interface IPresenter
    {
        public IView View { get; }

        public bool IsViewOpeningOrClosing { get; }

        public void OnLoad<TModel, TView>(TModel model, TView view)
            where TModel : Model
            where TView : View;
        public void Open();
        public void Close();
        public void OnUnload();
    }
}
