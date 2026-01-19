using System.Collections.Generic;

namespace Mu3Library.Sample.Template.Global
{
    public static class SceneNames
    {
        public const string Splash = "Splash";
        public const string Main = "Main";

        public const string SampleAudio = "Sample_Audio";
        public const string SampleMVP = "Sample_MVP";
        public const string SampleLocalization = "Sample_Localization";
        public const string SampleAddressables = "Sample_Addressables";
        public const string SampleAddressablesAdditive = "Sample_AddressablesAdditive";
        public const string SampleWebRequest = "Sample_WebRequest";

        private readonly static string[] _sampleSceneNames =
        {
            SampleAudio,
            SampleMVP,
            SampleLocalization,
            SampleAddressables,
            SampleWebRequest,
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
