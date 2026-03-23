namespace Mu3Library.Sample.Template.Addressables
{
    public static class AddressableGroupKeys
    {
        public static class Groups
        {
            public static readonly string[] All = new string[] { DefaultLocalGroup, LocalizationAssetsShared, LocalizationLocales, LocalizationStringTablesEnglishEn, LocalizationStringTablesJapaneseJa, LocalizationStringTablesKoreanKo, TestPack, TestPack02, TestScene };
            
            public const string DefaultLocalGroup = AddressableGroupKeys.DefaultLocalGroup.Name;
            public const string LocalizationAssetsShared = AddressableGroupKeys.LocalizationAssetsShared.Name;
            public const string LocalizationLocales = AddressableGroupKeys.LocalizationLocales.Name;
            public const string LocalizationStringTablesEnglishEn = AddressableGroupKeys.LocalizationStringTablesEnglishEn.Name;
            public const string LocalizationStringTablesJapaneseJa = AddressableGroupKeys.LocalizationStringTablesJapaneseJa.Name;
            public const string LocalizationStringTablesKoreanKo = AddressableGroupKeys.LocalizationStringTablesKoreanKo.Name;
            public const string TestPack = AddressableGroupKeys.TestPack.Name;
            public const string TestPack02 = AddressableGroupKeys.TestPack02.Name;
            public const string TestScene = AddressableGroupKeys.TestScene.Name;
        }
        

        public static class Labels
        {
            public static readonly string[] All = new string[] { BaseDownload, DownloadAll, Locale, LocaleEn, LocaleJa, LocaleKo, TestImage, TestLabel, TestScene };
            
            public const string BaseDownload = "base-download";
            public const string DownloadAll = "download-all";
            public const string Locale = "Locale";
            public const string LocaleEn = "Locale-en";
            public const string LocaleJa = "Locale-ja";
            public const string LocaleKo = "Locale-ko";
            public const string TestImage = "test-image";
            public const string TestLabel = "test-label";
            public const string TestScene = "test-scene";
        }
        

        public static class DefaultLocalGroup
        {
            public const string Name = "Default Local Group";
            
        }

        public static class LocalizationAssetsShared
        {
            public const string Name = "Localization-Assets-Shared";
            
            public static readonly string[] AllNames = new string[]
            {
                "TestStringTable Shared Data",
            };
            
            public static readonly string[] AllAddresses = new string[]
            {
                "Assets/Mu3LibraryAssets/Samples~/Sample_Localization/Localization/Tables/TestStringTable Shared Data.asset",
            };
            
            public static class Labels
            {
                public static readonly string[] All = new string[] { BaseDownload };
                
                public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
            }
            

            public static class AssetsMu3LibraryAssetsSamplesSampleLocalizationLocalizationTablesTestStringTableSharedDataAsset
            {
                public const string Name = "TestStringTable Shared Data";
                public const string Address = "Assets/Mu3LibraryAssets/Samples~/Sample_Localization/Localization/Tables/TestStringTable Shared Data.asset";
                public static class Labels
                {
                    public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                    
                    public static readonly string[] All = new string[] { BaseDownload };
                }
            }
        }

        public static class LocalizationLocales
        {
            public const string Name = "Localization-Locales";
            
            public static readonly string[] AllNames = new string[]
            {
                "English (en)",
                "Japanese (ja)",
                "Korean (ko)",
            };
            
            public static readonly string[] AllAddresses = new string[]
            {
                "English (en)",
                "Japanese (ja)",
                "Korean (ko)",
            };
            
            public static class Labels
            {
                public static readonly string[] All = new string[] { BaseDownload, Locale };
                
                public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                public const string Locale = AddressableGroupKeys.Labels.Locale;
            }
            

            public static class EnglishEn
            {
                public const string Name = "English (en)";
                public const string Address = "English (en)";
                public static class Labels
                {
                    public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                    public const string Locale = AddressableGroupKeys.Labels.Locale;
                    
                    public static readonly string[] All = new string[] { BaseDownload, Locale };
                }
            }

            public static class JapaneseJa
            {
                public const string Name = "Japanese (ja)";
                public const string Address = "Japanese (ja)";
                public static class Labels
                {
                    public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                    public const string Locale = AddressableGroupKeys.Labels.Locale;
                    
                    public static readonly string[] All = new string[] { BaseDownload, Locale };
                }
            }

            public static class KoreanKo
            {
                public const string Name = "Korean (ko)";
                public const string Address = "Korean (ko)";
                public static class Labels
                {
                    public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                    public const string Locale = AddressableGroupKeys.Labels.Locale;
                    
                    public static readonly string[] All = new string[] { BaseDownload, Locale };
                }
            }
        }

        public static class LocalizationStringTablesEnglishEn
        {
            public const string Name = "Localization-String-Tables-English (en)";
            
            public static readonly string[] AllNames = new string[]
            {
                "TestStringTable_en",
            };
            
            public static readonly string[] AllAddresses = new string[]
            {
                "TestStringTable_en",
            };
            
            public static class Labels
            {
                public static readonly string[] All = new string[] { BaseDownload, LocaleEn };
                
                public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                public const string LocaleEn = AddressableGroupKeys.Labels.LocaleEn;
            }
            

            public static class TestStringTableEn
            {
                public const string Name = "TestStringTable_en";
                public const string Address = "TestStringTable_en";
                public static class Labels
                {
                    public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                    public const string LocaleEn = AddressableGroupKeys.Labels.LocaleEn;
                    
                    public static readonly string[] All = new string[] { BaseDownload, LocaleEn };
                }
            }
        }

        public static class LocalizationStringTablesJapaneseJa
        {
            public const string Name = "Localization-String-Tables-Japanese (ja)";
            
            public static readonly string[] AllNames = new string[]
            {
                "TestStringTable_ja",
            };
            
            public static readonly string[] AllAddresses = new string[]
            {
                "TestStringTable_ja",
            };
            
            public static class Labels
            {
                public static readonly string[] All = new string[] { BaseDownload, LocaleJa };
                
                public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                public const string LocaleJa = AddressableGroupKeys.Labels.LocaleJa;
            }
            

            public static class TestStringTableJa
            {
                public const string Name = "TestStringTable_ja";
                public const string Address = "TestStringTable_ja";
                public static class Labels
                {
                    public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                    public const string LocaleJa = AddressableGroupKeys.Labels.LocaleJa;
                    
                    public static readonly string[] All = new string[] { BaseDownload, LocaleJa };
                }
            }
        }

        public static class LocalizationStringTablesKoreanKo
        {
            public const string Name = "Localization-String-Tables-Korean (ko)";
            
            public static readonly string[] AllNames = new string[]
            {
                "TestStringTable_ko",
            };
            
            public static readonly string[] AllAddresses = new string[]
            {
                "TestStringTable_ko",
            };
            
            public static class Labels
            {
                public static readonly string[] All = new string[] { BaseDownload, LocaleKo };
                
                public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                public const string LocaleKo = AddressableGroupKeys.Labels.LocaleKo;
            }
            

            public static class TestStringTableKo
            {
                public const string Name = "TestStringTable_ko";
                public const string Address = "TestStringTable_ko";
                public static class Labels
                {
                    public const string BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                    public const string LocaleKo = AddressableGroupKeys.Labels.LocaleKo;
                    
                    public static readonly string[] All = new string[] { BaseDownload, LocaleKo };
                }
            }
        }

        public static class TestPack
        {
            public const string Name = "TestPack";
            
            public static readonly string[] AllNames = new string[]
            {
                "SceneThumbnails",
                "TestImage03",
            };
            
            public static readonly string[] AllAddresses = new string[]
            {
                "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails",
                "TestImage03",
            };
            
            public static class Labels
            {
                public static readonly string[] All = new string[] { DownloadAll, TestImage };
                
                public const string DownloadAll = AddressableGroupKeys.Labels.DownloadAll;
                public const string TestImage = AddressableGroupKeys.Labels.TestImage;
            }
            

            public static class AssetsMu3LibrarySamplesSampleTemplateImagesSceneThumbnails
            {
                public const string Name = "SceneThumbnails";
                public const string Address = "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails";
                public static class Labels
                {
                    public const string DownloadAll = AddressableGroupKeys.Labels.DownloadAll;
                    
                    public static readonly string[] All = new string[] { DownloadAll };
                }

                public static class Assets
                {
                    public static readonly string[] AllNames = new string[]
                    {
                        "Thumbnail_AudioManager",
                        "Thumbnail_AudioManagerFor3DSFX",
                        "Thumbnail_InputSystemManager",
                        "Thumbnail_Localization",
                    };
                    
                    public static readonly string[] AllAddresses = new string[]
                    {
                        "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManager.png",
                        "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManagerFor3DSFX.png",
                        "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_InputSystemManager.png",
                        "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_Localization.png",
                    };
                    
                    public static class ThumbnailAudioManagerPng
                    {
                        public const string Name = "Thumbnail_AudioManager";
                        public const string Address = "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManager.png";
                        public static class Labels
                        {
                            public const string DownloadAll = AddressableGroupKeys.Labels.DownloadAll;
                            
                            public static readonly string[] All = new string[] { DownloadAll };
                        }
                    }

                    public static class ThumbnailAudioManagerFor3DSFXPng
                    {
                        public const string Name = "Thumbnail_AudioManagerFor3DSFX";
                        public const string Address = "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManagerFor3DSFX.png";
                        public static class Labels
                        {
                            public const string DownloadAll = AddressableGroupKeys.Labels.DownloadAll;
                            
                            public static readonly string[] All = new string[] { DownloadAll };
                        }
                    }

                    public static class ThumbnailInputSystemManagerPng
                    {
                        public const string Name = "Thumbnail_InputSystemManager";
                        public const string Address = "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_InputSystemManager.png";
                        public static class Labels
                        {
                            public const string DownloadAll = AddressableGroupKeys.Labels.DownloadAll;
                            
                            public static readonly string[] All = new string[] { DownloadAll };
                        }
                    }

                    public static class ThumbnailLocalizationPng
                    {
                        public const string Name = "Thumbnail_Localization";
                        public const string Address = "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_Localization.png";
                        public static class Labels
                        {
                            public const string DownloadAll = AddressableGroupKeys.Labels.DownloadAll;
                            
                            public static readonly string[] All = new string[] { DownloadAll };
                        }
                    }
                }
            }

            public static class TestImage03
            {
                public const string Name = "TestImage03";
                public const string Address = "TestImage03";
                public static class Labels
                {
                    public const string TestImage = AddressableGroupKeys.Labels.TestImage;
                    
                    public static readonly string[] All = new string[] { TestImage };
                }
            }
        }

        public static class TestPack02
        {
            public const string Name = "TestPack02";
            
            public static readonly string[] AllNames = new string[]
            {
                "TestImage",
                "TestImage02",
            };
            
            public static readonly string[] AllAddresses = new string[]
            {
                "TestImage",
                "TestImage02",
            };
            
            public static class Labels
            {
                public static readonly string[] All = new string[] { DownloadAll, TestImage, TestLabel };
                
                public const string DownloadAll = AddressableGroupKeys.Labels.DownloadAll;
                public const string TestImage = AddressableGroupKeys.Labels.TestImage;
                public const string TestLabel = AddressableGroupKeys.Labels.TestLabel;
            }
            

            public static class TestImage
            {
                public const string Name = "TestImage";
                public const string Address = "TestImage";
                public static class Labels
                {
                    public const string TestImage = AddressableGroupKeys.Labels.TestImage;
                    public const string TestLabel = AddressableGroupKeys.Labels.TestLabel;
                    
                    public static readonly string[] All = new string[] { TestImage, TestLabel };
                }
            }

            public static class TestImage02
            {
                public const string Name = "TestImage02";
                public const string Address = "TestImage02";
                public static class Labels
                {
                    public const string DownloadAll = AddressableGroupKeys.Labels.DownloadAll;
                    public const string TestImage = AddressableGroupKeys.Labels.TestImage;
                    
                    public static readonly string[] All = new string[] { DownloadAll, TestImage };
                }
            }
        }

        public static class TestScene
        {
            public const string Name = "TestScene";
            
            public static readonly string[] AllNames = new string[]
            {
                "Sample_Addressables",
                "Sample_AddressablesAdditive",
            };
            
            public static readonly string[] AllAddresses = new string[]
            {
                "Sample_Addressables",
                "Sample_AddressablesAdditive",
            };
            
            public static class Labels
            {
                public static readonly string[] All = new string[] { TestScene };
                
                public const string TestScene = AddressableGroupKeys.Labels.TestScene;
            }
            

            public static class SampleAddressables
            {
                public const string Name = "Sample_Addressables";
                public const string Address = "Sample_Addressables";
                public static class Labels
                {
                    public const string TestScene = AddressableGroupKeys.Labels.TestScene;
                    
                    public static readonly string[] All = new string[] { TestScene };
                }
            }

            public static class SampleAddressablesAdditive
            {
                public const string Name = "Sample_AddressablesAdditive";
                public const string Address = "Sample_AddressablesAdditive";
                public static class Labels
                {
                    public const string TestScene = AddressableGroupKeys.Labels.TestScene;
                    
                    public static readonly string[] All = new string[] { TestScene };
                }
            }
        }
    }
}
