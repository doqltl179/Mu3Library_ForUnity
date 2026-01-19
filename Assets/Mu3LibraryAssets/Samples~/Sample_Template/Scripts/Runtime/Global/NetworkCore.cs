using Mu3Library.DI;
using Mu3Library.WebRequest;

namespace Mu3Library.Sample.Template.Global
{
    public class NetworkCore : CoreBase
    {
        protected override void ConfigureContainer(ContainerScope scope)
        {
            scope.Register<WebRequestManager>();
        }
    }
}
