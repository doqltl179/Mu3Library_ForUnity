using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Scene {
    public class SceneController : MonoBehaviour {
        public SceneType Type => type;
        [SerializeField] protected SceneType type;

        /// <summary>
        /// <br/>If [False] => SceneLoader.LoadScene not end.
        /// <br/>If you want end SceneLoader.LoadScene, Change this property to [True].
        /// </summary>
        public bool SceneLoadedCompletely { get; protected set; }



        public virtual void OnSceneLoad() {
            SceneLoadedCompletely = true;
        }

        public virtual void OnSceneUnload() {
            SceneLoadedCompletely = false;
        }
    }
}