namespace Mu3Library.UI.MVP
{
    public interface IPresenterInitialize
    {
        public void Init(Arguments args);
        public void Init(View view, Arguments args);
    }
}
