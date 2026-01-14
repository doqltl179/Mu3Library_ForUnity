using System;
using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public abstract class PresenterBase : IPresenter
    {
        public abstract Type ViewType { get; }

        public abstract Type ModelType { get; }
        public abstract Type ArgumentType { get; }

        public abstract bool IsViewExist { get; }
        public abstract ViewState ViewState { get; }

        public abstract string ObjectLayerName { get; }
        public abstract string CanvasLayerName { get; }
        public abstract int SortingOrder { get; }

        public abstract bool Interactable { get; }

        public abstract void Initialize(View view, Arguments args);
        public abstract void Initialize(Arguments args);

        internal abstract void Load();
        internal abstract void Open();
        internal abstract void Close(bool forceClose = false);
        internal abstract void Unload();

        internal abstract RectTransform RectTransform { get; }
        internal abstract Canvas ViewCanvas { get; }
        internal abstract CanvasGroup CanvasGroup { get; }
        internal abstract float Alpha { get; set; }

        internal abstract void SetActiveView(bool active);
        internal abstract void OptimizeView();
        internal abstract void ForceDestroyView();
        internal abstract void SetSortingOrder(int sortingOrder);
    }

    public abstract class Presenter<TView, TModel, TArgs> : PresenterBase
        where TView : View
        where TModel : Model<TArgs>, new()
        where TArgs : Arguments
    {
        protected TView _view;
        public override Type ViewType => typeof(TView);

        protected TModel _model;
        public override Type ModelType => typeof(TModel);

        protected TArgs _args;
        public override Type ArgumentType => typeof(TArgs);

        public override bool IsViewExist => _view != null;
        public override ViewState ViewState => _view.ViewState;

        public override string ObjectLayerName => _view.ObjectLayerName;
        public override string CanvasLayerName => _view.CanvasLayerName;
        public override int SortingOrder => _view.SortingOrder;

        public override bool Interactable => _view.Interactable;

        internal sealed override RectTransform RectTransform => _view != null ? _view.RectTransform : null;
        internal sealed override Canvas ViewCanvas => _view != null ? _view.Canvas : null;
        internal sealed override CanvasGroup CanvasGroup => _view != null ? _view.CanvasGroup : null;
        internal sealed override float Alpha
        {
            get => _view != null ? _view.Alpha : 0.0f;
            set
            {
                if (_view != null)
                {
                    _view.Alpha = value;
                }
            }
        }



        public override void Initialize(Arguments args) => Initialize(_view, args);

        public override void Initialize(View view, Arguments args)
        {
            _view = view as TView;
            _args = args as TArgs;

            _model = new TModel();
            _model.Init(_args);
        }

        internal sealed override void Load()
        {
            if (_view == null)
            {
                return;
            }

            LoadFunc();
            _view.Load();
        }

        protected virtual void LoadFunc() { }

        internal sealed override void Open()
        {
            if (_view == null)
            {
                return;
            }

            OpenFunc();
            _view.Open();
        }

        protected virtual void OpenFunc() { }

        internal sealed override void Close(bool forceClose = false)
        {
            if (_view == null)
            {
                return;
            }

            CloseFunc();
            _view.Close(forceClose);
        }

        protected virtual void CloseFunc() { }

        internal sealed override void Unload()
        {
            if (_view == null)
            {
                return;
            }

            UnloadFunc();
            _view.Unload();
        }

        protected virtual void UnloadFunc() { }

        internal sealed override void SetActiveView(bool active)
        {
            if (_view == null)
            {
                return;
            }

            _view.SetActive(active);
        }

        internal sealed override void OptimizeView()
        {
            if (_view == null)
            {
                return;
            }

            _view.transform.SetAsLastSibling();
            _view.Stretch();
        }

        internal sealed override void ForceDestroyView()
        {
            if (_view == null)
            {
                return;
            }

            _view.DestroySelf();
            _view = null;
        }

        internal sealed override void SetSortingOrder(int sortingOrder)
        {
            if (_view == null)
            {
                return;
            }

            _view.SetSortingOrder(sortingOrder);
        }
    }
}
