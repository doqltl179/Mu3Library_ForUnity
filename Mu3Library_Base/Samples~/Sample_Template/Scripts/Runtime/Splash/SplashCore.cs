using Mu3Library.DI;
using Mu3Library.Sample.Template.Global;
using Mu3Library.Scene;
using UnityEngine;

namespace Mu3Library.Sample.Template.Splash
{
    public class SplashCore : CoreBase
    {
        private ISceneLoader _sceneLoader;

        [Space(20)]
        [SerializeField] private SplashUIAnimation _splashAnimation;


        protected override void Start()
        {
            base.Start();

            _splashAnimation.OnAnimationEnd += OnSplashAnimationEnd;

            WaitForOtherCore<SceneCore>(OnSceneCoreAdded);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _splashAnimation.OnAnimationEnd -= OnSplashAnimationEnd;
        }

        private void OnSceneCoreAdded()
        {
            _sceneLoader = GetClassFromOtherCore<SceneCore, ISceneLoader>();

            LoadLoadingScene();
        }

        private void OnSplashAnimationEnd()
        {
            LoadLoadingScene();
        }

        private void LoadLoadingScene()
        {
            if(_sceneLoader == null ||
                _sceneLoader.IsLoading ||
                !_splashAnimation.IsAnimationEnded)
            {
                return;
            }

#if UNITY_EDITOR
            _sceneLoader.LoadSingleSceneWithAssetPath(SceneNames.GetSceneAssetPath(SceneNames.Main));
#else
            _sceneLoader.LoadSingleScene(SceneNames.Main);
#endif
        }
    }
}
