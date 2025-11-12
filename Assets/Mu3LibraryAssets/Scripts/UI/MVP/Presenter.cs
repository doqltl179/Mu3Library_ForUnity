using System;
using Mu3Library.Extensions;
using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public abstract class Presenter<TView, TModel, TArgs> : IPresenter, IPresenterInitialize, ILifecycle
        where TView : View
        where TModel : Model<TArgs>, new()
        where TArgs : Arguments
    {
        protected TView _view;
        public IView View => _view;
        public Type ViewType => typeof(TView);

        protected TModel _model;
        public Type ModelType => typeof(TModel);

        protected TArgs _args;
        public Type ArgumentType => typeof(TArgs);

        public bool IsViewExist => _view != null;
        public ViewState ViewState => _view.ViewState;

        public RectTransform RectTransform => _view.RectTransform;
        public Canvas ViewCanvas => _view.Canvas;
        public string LayerName => _view.LayerName;
        public int SortingOrder => _view.SortingOrder;



        public void Init(Arguments args) => Init(_view, args);

        public virtual void Init(View view, Arguments args)
        {
            _view = view as TView;
            _args = args as TArgs;

            _model = new TModel();
            _model.Init(_args);
        }

        public virtual void Load() => _view.Load();
        public virtual void Open() => _view.Open();
        public virtual void Close() => _view.Close();
        public virtual void Unload() => _view.Unload();

        public void SetActiveView(bool active)
        {
            if (_view == null)
            {
                return;
            }

            _view.SetActive(active);
        }

        public void OptimizeView()
        {
            if (_view == null)
            {
                return;
            }

            _view.transform.SetAsLastSibling();
            _view.transform.LocalToOrigin();
            _view.Stretch();
        }

        public void ForceDestroyView()
        {
            if (_view == null)
            {
                return;
            }

            _view.DestroySelf();
            _view = null;
        }
    }
}