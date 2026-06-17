namespace Mu3Library.UI.MVP
{
    public sealed class OpenOptions
    {
        public IPresenter Owner { get; set; }
        public Arguments Arguments { get; set; }
        public OutPanelSettings OutPanelSettings { get; set; }
        public HostOptions HostOptions { get; set; }

        public OpenOptions()
        {
            OutPanelSettings = OutPanelSettings.Disabled;
        }
    }
}
