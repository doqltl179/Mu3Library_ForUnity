using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using static Mu3Library.Scene.SceneLoader;

namespace Mu3Library.Scene {
    /// <summary>
    /// Create once at each scenes.
    /// </summary>
    public class SceneController : MonoBehaviour {
        public SceneType Type => type;
        [SerializeField] protected SceneType type;

        [Space(20)]
        [SerializeField] protected SceneUI sceneUi;

        /// <summary>
        /// <br/>If [False] => SceneLoader.LoadScene not end.
        /// <br/>If you want end SceneLoader.LoadScene, Change this property to [True].
        /// </summary>
        public bool SceneLoadedCompletely { get; protected set; }



        public virtual void OnSceneLoad() {
            if(sceneUi != null) sceneUi.OnFirstActivate();

            SceneLoadedCompletely = true;
        }

        public virtual void OnSceneUnload() {
            SceneLoadedCompletely = false;
        }
    }
}