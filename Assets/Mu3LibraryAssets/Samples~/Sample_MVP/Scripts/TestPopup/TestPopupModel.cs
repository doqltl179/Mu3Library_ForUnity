using Mu3Library.UI.DesignPattern.MPV;
using UnityEngine.Events;

namespace Mu3Library.Sample.MVP
{
    public class TestPopupModel : Model
    {
        public string Message = "";
        public UnityAction OnClickConfirm = null;
        public UnityAction OnClickCancel = null;
    }
}