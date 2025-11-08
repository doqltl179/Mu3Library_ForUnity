using Mu3Library.UI.MVP;

namespace Mu3Library.Sample.MVP
{
    public class MainPresenter : Presenter<MainView, MainModel, MainArguments>
    {



        public override void Load()
        {
            base.Load();

            _view.ClearAllKeyDescriptions();
        }

        public override void Open()
        {
            base.Open();

            foreach(var pair in _model.KeyDescriptions)
            {
                _view.AddKeyDescription(pair.Key, pair.Value);
            }
        }
    }
}