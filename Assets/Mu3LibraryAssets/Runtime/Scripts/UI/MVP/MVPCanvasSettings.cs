using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.UI.MVP
{
    public struct MVPCanvasSettings
    {
        public RenderMode RenderMode;
        public string SortingLayerName;
        public int SortingOrder;
        public float PlaneDistance;
        public CanvasScaler.ScaleMode UIScaleMode;
        public Vector2 Resolution;
        public CanvasScaler.ScreenMatchMode ScreenMatchMode;
        public float MatchWidthOrHeight;
        public CanvasScaler.Unit PhysicalUnit;
        public float FallbackScreenDPI;
        public float SpriteDPI;
        public float ScaleFactor;

        public static readonly MVPCanvasSettings Standard = new()
        {
            RenderMode = RenderMode.ScreenSpaceCamera,
            SortingLayerName = "Default",
            SortingOrder = 0,
            PlaneDistance = 10.0f,
            UIScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize,
            Resolution = new Vector2(1920, 1080),
            ScreenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
            MatchWidthOrHeight = 1.0f,
            PhysicalUnit = CanvasScaler.Unit.Points,
            FallbackScreenDPI = 96.0f,
            SpriteDPI = 96.0f,
            ScaleFactor = 1.0f,
        };
    }
}
