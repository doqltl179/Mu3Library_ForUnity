using Mu3Library.DI;
using Mu3Library.WebRequest;

namespace Mu3Library.Sample.Template.Global
{
    public class NetworkCore : CoreBase
    {
        protected override void Awake()
        {
            base.Awake();

            _container.Register<WebRequestManager>();
        }
    }
}
