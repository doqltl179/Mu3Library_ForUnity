using Mu3Library.Base.UI.DesignPattern.MPV;
using UnityEngine;

namespace Mu3Library.Base.Sample.MVP
{
    public enum BackgroundInteractType
    {
        None = 0,
        Confirm = 1,
        Cancel = 2,
    }

    /// <summary>
    /// 예시 Paramerter로, 필요에 맞게 수정하여 사용하자.
    /// </summary>
    public class WindowParams
    {
        public Model Model;

        public Color BackgroundColor = new Color(0, 0, 0, 180 / 255f);
        public BackgroundInteractType BackgroundInteractType = BackgroundInteractType.None;
    }
    
    public class MVPScheduleData
    {
        public IPresenter Presenter;
        public WindowParams Param;
    }
}