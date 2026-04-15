using Mu3Library.Resource.Data;

namespace Mu3Library.Sample.Template.Resource
{
    public static class ResourceDataKeys
    {
        public static readonly ResourcePathData AIGameDeveloperConfig = new ResourcePathData("AI-Game-Developer-Config", "AI-Game-Developer-Config");
        public static readonly ResourcePathData LineBreakingFollowingCharacters = new ResourcePathData("LineBreaking Following Characters", "LineBreaking Following Characters");
        public static readonly ResourcePathData LineBreakingLeadingCharacters = new ResourcePathData("LineBreaking Leading Characters", "LineBreaking Leading Characters");
        public static readonly ResourcePathData TMPSettings = new ResourcePathData("TMP Settings", "TMP Settings");
        
        public static class FontsMaterials
        {
            public static readonly ResourcePathData LiberationSansSDF = new ResourcePathData("Fonts & Materials/LiberationSans SDF", "LiberationSans SDF");
            public static readonly ResourcePathData LiberationSansSDFFallback = new ResourcePathData("Fonts & Materials/LiberationSans SDF - Fallback", "LiberationSans SDF - Fallback");
        }

        public static class SampleMVP
        {
            public static class AnimationConfig
            {
                public static readonly ResourcePathData BottomToMiddleAnimation = new ResourcePathData("Sample_MVP/AnimationConfig/BottomToMiddleAnimation", "BottomToMiddleAnimation");
                public static readonly ResourcePathData OneCurveScaleAnimation = new ResourcePathData("Sample_MVP/AnimationConfig/OneCurveScaleAnimation", "OneCurveScaleAnimation");
                public static readonly ResourcePathData TwoCurveScaleAnimation = new ResourcePathData("Sample_MVP/AnimationConfig/TwoCurveScaleAnimation", "TwoCurveScaleAnimation");
            }

            public static class UIView
            {
                public static readonly ResourcePathData BottomToMiddleAnimationPopup = new ResourcePathData("Sample_MVP/UIView/BottomToMiddleAnimationPopup", "BottomToMiddleAnimationPopup");
                public static readonly ResourcePathData LoadingScreen = new ResourcePathData("Sample_MVP/UIView/LoadingScreen", "LoadingScreen");
                public static readonly ResourcePathData MainView = new ResourcePathData("Sample_MVP/UIView/MainView", "MainView");
                public static readonly ResourcePathData NotificationView = new ResourcePathData("Sample_MVP/UIView/NotificationView", "NotificationView");
                public static readonly ResourcePathData OneCurveAnimationPopup = new ResourcePathData("Sample_MVP/UIView/OneCurveAnimationPopup", "OneCurveAnimationPopup");
                public static readonly ResourcePathData TwoCurveAnimationPopup = new ResourcePathData("Sample_MVP/UIView/TwoCurveAnimationPopup", "TwoCurveAnimationPopup");
            }
        }

        public static class SpriteAssets
        {
            public static readonly ResourcePathData EmojiOne = new ResourcePathData("Sprite Assets/EmojiOne", "EmojiOne");
        }

        public static class StyleSheets
        {
            public static readonly ResourcePathData DefaultStyleSheet = new ResourcePathData("Style Sheets/Default Style Sheet", "Default Style Sheet");
        }
    }
}
