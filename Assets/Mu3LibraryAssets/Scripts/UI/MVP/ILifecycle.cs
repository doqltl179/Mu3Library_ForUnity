namespace Mu3Library.UI.MVP
{
    public interface ILifecycle
    {
        public void Load();
        public void Open();
        public void Close();
        public void Unload();
    }
}
