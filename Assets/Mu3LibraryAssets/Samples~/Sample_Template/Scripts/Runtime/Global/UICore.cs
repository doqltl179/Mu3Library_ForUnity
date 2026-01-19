using Mu3Library.DI;
using Mu3Library.UI.MVP;

namespace Mu3Library.Sample.Template.Global
{
    public class UICore : CoreBase
    {
        protected override void ConfigureContainer(ContainerScope scope)
        {
            scope.Register<MVPManager>();
        }
    }
}
