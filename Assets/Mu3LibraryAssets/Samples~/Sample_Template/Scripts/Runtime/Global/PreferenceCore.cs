using Mu3Library.DI;
using Mu3Library.Preference;

namespace Mu3Library.Sample.Template.Global
{
    public class PreferenceCore : CoreBase
    {
        protected override void Awake()
        {
            base.Awake();

            _container.Register<PlayerPrefsLoader>();
        }
    }
}
