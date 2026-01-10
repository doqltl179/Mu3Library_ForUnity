using System.Collections.Generic;

namespace Mu3Library.Sample.Template.Common
{
    public static class SceneNames
    {
        public const string Splash = "Splash";
        public const string Main = "Main";

        public const string SampleAudio = "Sample_Audio";

        private readonly static string[] _sampleSceneNames =
        {
            SampleAudio,
        };
        public static IEnumerable<string> SampleSceneNames => _sampleSceneNames;

#if UNITY_EDITOR
        public const string SceneFolderAssetPath = "Assets/Mu3LibrarySamples/Sample_Template/Scenes/";

        public static string GetSceneAssetPath(string sceneName)
        {
            return SceneFolderAssetPath + sceneName + ".unity";
        }
#endif
    }
}