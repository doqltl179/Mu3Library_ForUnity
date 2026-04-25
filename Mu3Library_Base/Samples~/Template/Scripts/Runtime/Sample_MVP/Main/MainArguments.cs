using System.Collections.Generic;
using Mu3Library.Scene;
using Mu3Library.UI.MVP;
using UnityEngine;

namespace Mu3Library.Sample.Template.MVP
{
    public class MainArguments : Arguments
    {
        public ISceneLoader SceneLoader;
        public Dictionary<KeyCode, string> KeyDescriptions;
    }
}