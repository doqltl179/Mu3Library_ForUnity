using Mu3Library.DI;
using Mu3Library.Preference;

namespace Mu3Library.Sample.Template.Global
{
    public class PreferenceCore : CoreBase
    {
        protected override void ConfigureContainer(ContainerScope scope)
        {
            scope.Register<PlayerPrefsLoader>();
        }
    }
}
