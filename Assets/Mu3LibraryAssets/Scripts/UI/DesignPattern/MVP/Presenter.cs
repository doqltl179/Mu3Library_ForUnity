namespace Mu3Library.UI.DesignPattern.MPV
{
    public abstract class Presenter : IPresenter
    {
        protected IView _view = null;
        public IView View { get => _view; }

        public bool IsViewOpeningOrClosing { get => _view.IsOpeningOrClosing; }



        public virtual void OnLoad<TModel, TView>(TModel model, TView view)
            where TModel : Model
            where TView : View
        {
            view.OnLoad(model);

            _view = view;
        }

        public virtual void OnUnload()
        {
            _view.OnUnload();
        }

        public virtual void Open()
        {
            _view.Open();
        }

        public virtual void Close()
        {
            _view.Close();
        }
    }
}
