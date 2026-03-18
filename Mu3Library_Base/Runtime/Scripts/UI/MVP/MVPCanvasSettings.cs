using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.UI.MVP
{
    [System.Serializable]
    public struct MVPCanvasSettings
    {
        [SerializeField] private CanvasSettings _canvasSettings;
        [SerializeField] private CanvasScalerSettings _canvasScalerSettings;
        [SerializeField] private GraphicRaycasterSettings _graphicRaycasterSettings;

        public CanvasSettings CanvasSettings
        {
            get => _canvasSettings;
            set => _canvasSettings = value;
        }
        public CanvasScalerSettings CanvasScalerSettings
        {
            get => _canvasScalerSettings;
            set => _canvasScalerSettings = value;
        }
        public GraphicRaycasterSettings GraphicRaycasterSettings
        {
            get => _graphicRaycasterSettings;
            set => _graphicRaycasterSettings = value;
        }

        public static readonly MVPCanvasSettings Standard = new()
        {
            _canvasSettings = CanvasSettings.Standard,
            _canvasScalerSettings = CanvasScalerSettings.Standard,
            _graphicRaycasterSettings = GraphicRaycasterSettings.Standard,
        };

        public void ReadFromSource(Canvas canvas) => ReadFromSource(canvas, null, null);
        public void ReadFromSource(CanvasScaler canvasScaler) => ReadFromSource(null, canvasScaler, null);
        public void ReadFromSource(GraphicRaycaster graphicRaycaster) => ReadFromSource(null, null, graphicRaycaster);
        public void ReadFromSource(Canvas canvas, CanvasScaler canvasScaler) => ReadFromSource(canvas, canvasScaler, null);
        public void ReadFromSource(CanvasScaler canvasScaler, GraphicRaycaster graphicRaycaster) => ReadFromSource(null, canvasScaler, graphicRaycaster);
        public void ReadFromSource(GraphicRaycaster graphicRaycaster, Canvas canvas) => ReadFromSource(canvas, null, graphicRaycaster);
        public void ReadFromSource(Canvas canvas, CanvasScaler canvasScaler, GraphicRaycaster graphicRaycaster)
        {
            _canvasSettings.ReadFromSource(canvas);
            _canvasScalerSettings.ReadFromSource(canvasScaler);
            _graphicRaycasterSettings.ReadFromSource(graphicRaycaster);
        }

        public void WriteToTarget(Canvas canvas) => WriteToTarget(canvas, null, null);
        public void WriteToTarget(CanvasScaler canvasScaler) => WriteToTarget(null, canvasScaler, null);
        public void WriteToTarget(GraphicRaycaster graphicRaycaster) => WriteToTarget(null, null, graphicRaycaster);
        public void WriteToTarget(Canvas canvas, CanvasScaler canvasScaler) => WriteToTarget(canvas, canvasScaler, null);
        public void WriteToTarget(CanvasScaler canvasScaler, GraphicRaycaster graphicRaycaster) => WriteToTarget(null, canvasScaler, graphicRaycaster);
        public void WriteToTarget(GraphicRaycaster graphicRaycaster, Canvas canvas) => WriteToTarget(canvas, null, graphicRaycaster);
        public void WriteToTarget(Canvas canvas, CanvasScaler canvasScaler, GraphicRaycaster graphicRaycaster)
        {
            _canvasSettings.WriteToTarget(canvas);
            _canvasScalerSettings.WriteToTarget(canvasScaler);
            _graphicRaycasterSettings.WriteToTarget(graphicRaycaster);
        }
    }

    [System.Serializable]
    public struct CanvasSettings
    {
        [SerializeField] private RenderMode _renderMode;
        [SerializeField] private string _sortingLayerName;
        [SerializeField] private int _sortingOrder;
        [SerializeField] private float _planeDistance;

        public RenderMode RenderMode
        {
            get => _renderMode;
            set => _renderMode = value;
        }
        public string SortingLayerName
        {
            get => _sortingLayerName;
            set => _sortingLayerName = value;
        }
        public int SortingOrder
        {
            get => _sortingOrder;
            set => _sortingOrder = value;
        }
        public float PlaneDistance
        {
            get => _planeDistance;
            set => _planeDistance = value;
        }

        public static readonly CanvasSettings Standard = new()
        {
            _renderMode = RenderMode.ScreenSpaceCamera,
            _sortingLayerName = "Default",
            _sortingOrder = 0,
            _planeDistance = 10.0f,
        };

        public void ReadFromSource(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            _renderMode = canvas.renderMode;
            _sortingLayerName = canvas.sortingLayerName;
            _sortingOrder = canvas.sortingOrder;
            _planeDistance = canvas.planeDistance;
        }

        public void WriteToTarget(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.renderMode = _renderMode;
            canvas.sortingLayerName = _sortingLayerName;
            canvas.sortingOrder = _sortingOrder;
            canvas.planeDistance = _planeDistance;
        }
    }

    [System.Serializable]
    public struct CanvasScalerSettings
    {
        [SerializeField] private CanvasScaler.ScaleMode _uiScaleMode;
        [SerializeField] private Vector2 _resolution;
        [SerializeField] private CanvasScaler.ScreenMatchMode _screenMatchMode;
        [SerializeField, Range(0.0f, 1.0f)] private float _matchWidthOrHeight;
        [SerializeField] private CanvasScaler.Unit _physicalUnit;
        [SerializeField] private float _fallbackScreenDPI;
        [SerializeField] private float _spriteDPI;
        [SerializeField] private float _scaleFactor;

        public CanvasScaler.ScaleMode UIScaleMode
        {
            get => _uiScaleMode;
            set => _uiScaleMode = value;
        }
        public Vector2 Resolution
        {
            get => _resolution;
            set => _resolution = value;
        }
        public CanvasScaler.ScreenMatchMode ScreenMatchMode
        {
            get => _screenMatchMode;
            set => _screenMatchMode = value;
        }
        public float MatchWidthOrHeight
        {
            get => _matchWidthOrHeight;
            set => _matchWidthOrHeight = value;
        }
        public CanvasScaler.Unit PhysicalUnit
        {
            get => _physicalUnit;
            set => _physicalUnit = value;
        }
        public float FallbackScreenDPI
        {
            get => _fallbackScreenDPI;
            set => _fallbackScreenDPI = value;
        }
        public float SpriteDPI
        {
            get => _spriteDPI;
            set => _spriteDPI = value;
        }
        public float ScaleFactor
        {
            get => _scaleFactor;
            set => _scaleFactor = value;
        }

        public static readonly CanvasScalerSettings Standard = new()
        {
            _uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize,
            _resolution = new Vector2(1920, 1080),
            _screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
            _matchWidthOrHeight = 1.0f,
            _physicalUnit = CanvasScaler.Unit.Points,
            _fallbackScreenDPI = 96.0f,
            _spriteDPI = 96.0f,
            _scaleFactor = 1.0f,
        };

        public void ReadFromSource(CanvasScaler source)
        {
            if (source == null)
            {
                return;
            }

            _uiScaleMode = source.uiScaleMode;
            _resolution = source.referenceResolution;
            _screenMatchMode = source.screenMatchMode;
            _matchWidthOrHeight = source.matchWidthOrHeight;
            _physicalUnit = source.physicalUnit;
            _fallbackScreenDPI = source.fallbackScreenDPI;
            _spriteDPI = source.defaultSpriteDPI;
            _scaleFactor = source.scaleFactor;
        }

        public void WriteToTarget(CanvasScaler target)
        {
            if (target == null)
            {
                return;
            }

            target.uiScaleMode = _uiScaleMode;
            target.referenceResolution = _resolution;
            target.screenMatchMode = _screenMatchMode;
            target.matchWidthOrHeight = _matchWidthOrHeight;
            target.physicalUnit = _physicalUnit;
            target.fallbackScreenDPI = _fallbackScreenDPI;
            target.defaultSpriteDPI = _spriteDPI;
            target.scaleFactor = _scaleFactor;
        }
    }

    [System.Serializable]
    public struct GraphicRaycasterSettings
    {
        [SerializeField] private bool _ignoreReversedGraphics;
        [SerializeField] private GraphicRaycaster.BlockingObjects _blockingObjects;
        [SerializeField] private LayerMask _blockingMask;

        public bool IgnoreReversedGraphics
        {
            get => _ignoreReversedGraphics;
            set => _ignoreReversedGraphics = value;
        }
        public GraphicRaycaster.BlockingObjects BlockingObjects
        {
            get => _blockingObjects;
            set => _blockingObjects = value;
        }
        public LayerMask BlockingMask
        {
            get => _blockingMask;
            set => _blockingMask = value;
        }

        public static readonly GraphicRaycasterSettings Standard = new()
        {
            _ignoreReversedGraphics = true,
            _blockingObjects = GraphicRaycaster.BlockingObjects.None,
            _blockingMask = ~0,
        };

        public void ReadFromSource(GraphicRaycaster source)
        {
            if (source == null)
            {
                return;
            }

            _ignoreReversedGraphics = source.ignoreReversedGraphics;
            _blockingObjects = source.blockingObjects;
            _blockingMask = source.blockingMask;
        }

        public void WriteToTarget(GraphicRaycaster target)
        {
            if (target == null)
            {
                return;
            }

            target.ignoreReversedGraphics = _ignoreReversedGraphics;
            target.blockingObjects = _blockingObjects;
            target.blockingMask = _blockingMask;
        }
    }
}
