using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public sealed class HostOptions
    {
        public RectTransform Host { get; set; }
        public System.Action<RectTransform> ApplyLayout { get; set; }
    }
}
