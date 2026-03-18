using System.Collections.Generic;

namespace Mu3Library.Sample.Template.Global
{
    public static class SceneNames
    {
        public const string Splash = "Splash";
        public const string Main = "Main";

        public const string SampleAudio = "Sample_Audio";
        public const string SampleAudio3D = "Sample_Audio3D";
        public const string SampleMVP = "Sample_MVP";
        public const string SampleLocalization = "Sample_Localization";
        public const string SampleAddressables = "Sample_Addressables";
        public const string SampleAddressablesAdditive = "Sample_AddressablesAdditive";
        public const string SampleWebRequest = "Sample_WebRequest";
        public const string SampleIS = "Sample_IS";
        public const string SamplePreference = "Sample_Preference";

        private readonly static string[] _sampleSceneNames =
        {
            SampleAudio,
            SampleAudio3D,
            SampleMVP,
            SampleLocalization,
            SampleAddressables,
            SampleWebRequest,
            SampleIS,
            SamplePreference,
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
