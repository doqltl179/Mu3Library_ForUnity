using Mu3Library.DI;
using Mu3Library.UI.MVP;

namespace Mu3Library.Sample.Template.Global
{
    public class UICore : CoreBase
    {
        protected override void Awake()
        {
            base.Awake();

            RegisterClass<MVPManager>();
        }
    }
}
