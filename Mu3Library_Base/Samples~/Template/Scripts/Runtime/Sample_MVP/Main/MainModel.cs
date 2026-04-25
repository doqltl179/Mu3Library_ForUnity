using System.Collections.Generic;
using Mu3Library.Scene;
using Mu3Library.UI.MVP;
using UnityEngine;

namespace Mu3Library.Sample.Template.MVP
{
    public class MainModel : Model<MainArguments>
    {
        private ISceneLoader _sceneLoader;
        public ISceneLoader SceneLoader => _sceneLoader;

        public readonly Dictionary<KeyCode, string> _keyDescriptions = new();
        public Dictionary<KeyCode, string> KeyDescriptions => _keyDescriptions;



        public override void Init(MainArguments args)
        {
            _keyDescriptions.Clear();

            if (args == null)
            {
                return;
            }

            _sceneLoader = args.SceneLoader;

            foreach(var pair in args.KeyDescriptions)
            {
                _keyDescriptions.Add(pair.Key, pair.Value);
            }
        }
    }
}