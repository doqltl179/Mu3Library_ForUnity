using System.Collections.Generic;

namespace Mu3Library.UI.MVP
{
    public partial interface IMVPManager
    {
        public void SetLayerCanvasSettings(string layerName, MVPCanvasSettings settings);

        public void RegisterViewResource(View viewResource);
        public void RegisterViewResources(IEnumerable<View> viewResources);
    }
}
