using System;
using UnityEngine;

namespace Mu3Library.UI.MVP
{
    public interface IPresenter
    {
        public IView View { get; }
        public Type ViewType { get; }

        public Type ModelType { get; }
        public Type ArgumentType{ get; }

        public bool IsViewExist { get; }
        public ViewState ViewState { get; }
        
        public Canvas ViewCanvas { get; }
        public string LayerName { get; }
        public int SortingOrder { get; }


        public void Init(Arguments args);
        public void Init(View view, Arguments args);
        public void Load();
        public void Open();
        public void Close();
        public void Unload();

        public void SetActiveView(bool active);
        public void OptimizeView();
        public void ForceDestroyView();
    }
}
