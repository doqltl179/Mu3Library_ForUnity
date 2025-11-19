namespace Mu3Library.UI.MVP
{
    public interface ILifecycle
    {
        public void Load();
        public void Open();
        public void Close(bool forceClose = false);
        public void Unload();
    }
}
