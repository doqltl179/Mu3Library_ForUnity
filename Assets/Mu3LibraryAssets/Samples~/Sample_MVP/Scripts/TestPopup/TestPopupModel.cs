using Mu3Library.Base.UI.DesignPattern.MPV;
using UnityEngine.Events;

namespace Mu3Library.Base.Sample.MVP
{
    public class TestPopupModel : Model
    {
        public string Message = "";
        public UnityAction OnClickConfirm = null;
        public UnityAction OnClickCancel = null;
    }
}