using System.Collections.Generic;
using Mu3Library.Addressable.Data;

namespace Mu3Library.Sample.Template.Addressables
{
    public static class AddressableGroupKeys
    {
        public static class Labels
        {
            public static readonly LabelData BaseDownload = new LabelData("base-download");
            public static readonly LabelData DownloadAll = new LabelData("download-all");
            public static readonly LabelData Locale = new LabelData("Locale");
            public static readonly LabelData LocaleEn = new LabelData("Locale-en");
            public static readonly LabelData LocaleJa = new LabelData("Locale-ja");
            public static readonly LabelData LocaleKo = new LabelData("Locale-ko");
            public static readonly LabelData TestImage = new LabelData("test-image");
            public static readonly LabelData TestLabel = new LabelData("test-label");
            public static readonly LabelData TestScene = new LabelData("test-scene");

            public static readonly LabelData[] All = new LabelData[]
            {
                BaseDownload,
                DownloadAll,
                Locale,
                LocaleEn,
                LocaleJa,
                LocaleKo,
                TestImage,
                TestLabel,
                TestScene,
            };
        }

        public static class Groups
        {
            public static readonly DefaultLocalGroupData DefaultLocalGroup = new DefaultLocalGroupData();
            public static readonly LocalizationAssetsSharedData LocalizationAssetsShared = new LocalizationAssetsSharedData();
            public static readonly LocalizationLocalesData LocalizationLocales = new LocalizationLocalesData();
            public static readonly LocalizationStringTablesEnglishEnData LocalizationStringTablesEnglishEn = new LocalizationStringTablesEnglishEnData();
            public static readonly LocalizationStringTablesJapaneseJaData LocalizationStringTablesJapaneseJa = new LocalizationStringTablesJapaneseJaData();
            public static readonly LocalizationStringTablesKoreanKoData LocalizationStringTablesKoreanKo = new LocalizationStringTablesKoreanKoData();
            public static readonly TestPackData TestPack = new TestPackData();
            public static readonly TestPack02Data TestPack02 = new TestPack02Data();
            public static readonly TestSceneData TestScene = new TestSceneData();

            public static readonly IReadOnlyDictionary<string, GroupData> All = new Dictionary<string, GroupData>
            {
                { DefaultLocalGroup.Name, DefaultLocalGroup },
                { LocalizationAssetsShared.Name, LocalizationAssetsShared },
                { LocalizationLocales.Name, LocalizationLocales },
                { LocalizationStringTablesEnglishEn.Name, LocalizationStringTablesEnglishEn },
                { LocalizationStringTablesJapaneseJa.Name, LocalizationStringTablesJapaneseJa },
                { LocalizationStringTablesKoreanKo.Name, LocalizationStringTablesKoreanKo },
                { TestPack.Name, TestPack },
                { TestPack02.Name, TestPack02 },
                { TestScene.Name, TestScene },
            };

            public sealed class DefaultLocalGroupData : GroupData
            {
                internal DefaultLocalGroupData() : base(
                    "Default Local Group",
                    new Dictionary<string, EntryData> { },
                    new Dictionary<string, LabelData> { })
                { }
            }

            public sealed class LocalizationAssetsSharedData : GroupData
            {
                public new static class Labels
                {
                    public static readonly LabelData BaseDownload = AddressableGroupKeys.Labels.BaseDownload;

                    public static readonly LabelData[] All = new LabelData[]
                    {
                        BaseDownload,
                    };
                }

                private static readonly EntryData _AssetsMu3LibraryAssetsSamplesSampleLocalizationLocalizationTablesTestStringTableSharedDataAsset = new EntryData("Localization-Assets-Shared", "TestStringTable Shared Data", "Assets/Mu3LibraryAssets/Samples~/Sample_Localization/Localization/Tables/TestStringTable Shared Data.asset");
                public readonly EntryData AssetsMu3LibraryAssetsSamplesSampleLocalizationLocalizationTablesTestStringTableSharedDataAsset = _AssetsMu3LibraryAssetsSamplesSampleLocalizationLocalizationTablesTestStringTableSharedDataAsset;

                public static readonly string[] AllNames = new string[]
                {
                    "TestStringTable Shared Data",
                };

                public static readonly string[] AllAddresses = new string[]
                {
                    "Assets/Mu3LibraryAssets/Samples~/Sample_Localization/Localization/Tables/TestStringTable Shared Data.asset",
                };

                internal LocalizationAssetsSharedData() : base(
                    "Localization-Assets-Shared",
                    new Dictionary<string, EntryData>
                    {
                        { _AssetsMu3LibraryAssetsSamplesSampleLocalizationLocalizationTablesTestStringTableSharedDataAsset.Name, _AssetsMu3LibraryAssetsSamplesSampleLocalizationLocalizationTablesTestStringTableSharedDataAsset },
                    },
                    new Dictionary<string, LabelData>
                    {
                        { Labels.BaseDownload.Value, Labels.BaseDownload },
                    })
                { }
            }

            public sealed class LocalizationLocalesData : GroupData
            {
                public new static class Labels
                {
                    public static readonly LabelData BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                    public static readonly LabelData Locale = AddressableGroupKeys.Labels.Locale;

                    public static readonly LabelData[] All = new LabelData[]
                    {
                        BaseDownload,
                        Locale,
                    };
                }

                private static readonly EntryData _EnglishEn = new EntryData("Localization-Locales", "English (en)", "English (en)");
                public readonly EntryData EnglishEn = _EnglishEn;
                private static readonly EntryData _JapaneseJa = new EntryData("Localization-Locales", "Japanese (ja)", "Japanese (ja)");
                public readonly EntryData JapaneseJa = _JapaneseJa;
                private static readonly EntryData _KoreanKo = new EntryData("Localization-Locales", "Korean (ko)", "Korean (ko)");
                public readonly EntryData KoreanKo = _KoreanKo;

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

                internal LocalizationLocalesData() : base(
                    "Localization-Locales",
                    new Dictionary<string, EntryData>
                    {
                        { _EnglishEn.Name, _EnglishEn },
                        { _JapaneseJa.Name, _JapaneseJa },
                        { _KoreanKo.Name, _KoreanKo },
                    },
                    new Dictionary<string, LabelData>
                    {
                        { Labels.BaseDownload.Value, Labels.BaseDownload },
                        { Labels.Locale.Value, Labels.Locale },
                    })
                { }
            }

            public sealed class LocalizationStringTablesEnglishEnData : GroupData
            {
                public new static class Labels
                {
                    public static readonly LabelData BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                    public static readonly LabelData LocaleEn = AddressableGroupKeys.Labels.LocaleEn;

                    public static readonly LabelData[] All = new LabelData[]
                    {
                        BaseDownload,
                        LocaleEn,
                    };
                }

                private static readonly EntryData _TestStringTableEn = new EntryData("Localization-String-Tables-English (en)", "TestStringTable_en", "TestStringTable_en");
                public readonly EntryData TestStringTableEn = _TestStringTableEn;

                public static readonly string[] AllNames = new string[]
                {
                    "TestStringTable_en",
                };

                public static readonly string[] AllAddresses = new string[]
                {
                    "TestStringTable_en",
                };

                internal LocalizationStringTablesEnglishEnData() : base(
                    "Localization-String-Tables-English (en)",
                    new Dictionary<string, EntryData>
                    {
                        { _TestStringTableEn.Name, _TestStringTableEn },
                    },
                    new Dictionary<string, LabelData>
                    {
                        { Labels.BaseDownload.Value, Labels.BaseDownload },
                        { Labels.LocaleEn.Value, Labels.LocaleEn },
                    })
                { }
            }

            public sealed class LocalizationStringTablesJapaneseJaData : GroupData
            {
                public new static class Labels
                {
                    public static readonly LabelData BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                    public static readonly LabelData LocaleJa = AddressableGroupKeys.Labels.LocaleJa;

                    public static readonly LabelData[] All = new LabelData[]
                    {
                        BaseDownload,
                        LocaleJa,
                    };
                }

                private static readonly EntryData _TestStringTableJa = new EntryData("Localization-String-Tables-Japanese (ja)", "TestStringTable_ja", "TestStringTable_ja");
                public readonly EntryData TestStringTableJa = _TestStringTableJa;

                public static readonly string[] AllNames = new string[]
                {
                    "TestStringTable_ja",
                };

                public static readonly string[] AllAddresses = new string[]
                {
                    "TestStringTable_ja",
                };

                internal LocalizationStringTablesJapaneseJaData() : base(
                    "Localization-String-Tables-Japanese (ja)",
                    new Dictionary<string, EntryData>
                    {
                        { _TestStringTableJa.Name, _TestStringTableJa },
                    },
                    new Dictionary<string, LabelData>
                    {
                        { Labels.BaseDownload.Value, Labels.BaseDownload },
                        { Labels.LocaleJa.Value, Labels.LocaleJa },
                    })
                { }
            }

            public sealed class LocalizationStringTablesKoreanKoData : GroupData
            {
                public new static class Labels
                {
                    public static readonly LabelData BaseDownload = AddressableGroupKeys.Labels.BaseDownload;
                    public static readonly LabelData LocaleKo = AddressableGroupKeys.Labels.LocaleKo;

                    public static readonly LabelData[] All = new LabelData[]
                    {
                        BaseDownload,
                        LocaleKo,
                    };
                }

                private static readonly EntryData _TestStringTableKo = new EntryData("Localization-String-Tables-Korean (ko)", "TestStringTable_ko", "TestStringTable_ko");
                public readonly EntryData TestStringTableKo = _TestStringTableKo;

                public static readonly string[] AllNames = new string[]
                {
                    "TestStringTable_ko",
                };

                public static readonly string[] AllAddresses = new string[]
                {
                    "TestStringTable_ko",
                };

                internal LocalizationStringTablesKoreanKoData() : base(
                    "Localization-String-Tables-Korean (ko)",
                    new Dictionary<string, EntryData>
                    {
                        { _TestStringTableKo.Name, _TestStringTableKo },
                    },
                    new Dictionary<string, LabelData>
                    {
                        { Labels.BaseDownload.Value, Labels.BaseDownload },
                        { Labels.LocaleKo.Value, Labels.LocaleKo },
                    })
                { }
            }

            public sealed class TestPackData : GroupData
            {
                public new static class Labels
                {
                    public static readonly LabelData DownloadAll = AddressableGroupKeys.Labels.DownloadAll;
                    public static readonly LabelData TestImage = AddressableGroupKeys.Labels.TestImage;

                    public static readonly LabelData[] All = new LabelData[]
                    {
                        DownloadAll,
                        TestImage,
                    };
                }

                public static class AssetsMu3LibrarySamplesSampleTemplateImagesSceneThumbnails
                {
                    public static readonly EntryData Data = new EntryData(
                        "TestPack",
                        "SceneThumbnails",
                        "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails",
                        new EntryData[]
                        {
                            new EntryData(
                                "TestPack",
                                "Thumbnail_AudioManager",
                                "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManager.png",
                                new EntryData[]
                                {
                                    new EntryData("TestPack", "", "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManager.png[Thumbnail_AudioManager]"),
                                }),
                            new EntryData(
                                "TestPack",
                                "Thumbnail_AudioManagerFor3DSFX",
                                "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManagerFor3DSFX.png",
                                new EntryData[]
                                {
                                    new EntryData("TestPack", "", "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManagerFor3DSFX.png[Thumbnail_AudioManagerFor3DSFX]"),
                                }),
                            new EntryData(
                                "TestPack",
                                "Thumbnail_InputSystemManager",
                                "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_InputSystemManager.png",
                                new EntryData[]
                                {
                                    new EntryData("TestPack", "", "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_InputSystemManager.png[Thumbnail_InputSystemManager]"),
                                }),
                            new EntryData(
                                "TestPack",
                                "Thumbnail_Localization",
                                "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_Localization.png",
                                new EntryData[]
                                {
                                    new EntryData("TestPack", "", "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_Localization.png[Thumbnail_Localization]"),
                                }),
                        });

                    public static class Labels
                    {
                        public static readonly LabelData DownloadAll = AddressableGroupKeys.Labels.DownloadAll;

                        public static readonly LabelData[] All = new LabelData[]
                        {
                            DownloadAll,
                        };
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
                            public static readonly EntryData Data = new EntryData(
                                "TestPack",
                                "Thumbnail_AudioManager",
                                "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManager.png",
                                new EntryData[]
                                {
                                    SubAssets.ThumbnailAudioManager,
                                });

                            public static class SubAssets
                            {
                                public static readonly EntryData ThumbnailAudioManager = new EntryData("TestPack", "Thumbnail_AudioManager", "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManager.png[Thumbnail_AudioManager]");

                                public static readonly EntryData[] All = new EntryData[]
                                {
                                    ThumbnailAudioManager,
                                };
                            }
                        }
                        public static class ThumbnailAudioManagerFor3DSFXPng
                        {
                            public static readonly EntryData Data = new EntryData(
                                "TestPack",
                                "Thumbnail_AudioManagerFor3DSFX",
                                "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManagerFor3DSFX.png",
                                new EntryData[]
                                {
                                    SubAssets.ThumbnailAudioManagerFor3DSFX,
                                });

                            public static class SubAssets
                            {
                                public static readonly EntryData ThumbnailAudioManagerFor3DSFX = new EntryData("TestPack", "Thumbnail_AudioManagerFor3DSFX", "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_AudioManagerFor3DSFX.png[Thumbnail_AudioManagerFor3DSFX]");

                                public static readonly EntryData[] All = new EntryData[]
                                {
                                    ThumbnailAudioManagerFor3DSFX,
                                };
                            }
                        }
                        public static class ThumbnailInputSystemManagerPng
                        {
                            public static readonly EntryData Data = new EntryData(
                                "TestPack",
                                "Thumbnail_InputSystemManager",
                                "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_InputSystemManager.png",
                                new EntryData[]
                                {
                                    SubAssets.ThumbnailInputSystemManager,
                                });

                            public static class SubAssets
                            {
                                public static readonly EntryData ThumbnailInputSystemManager = new EntryData("TestPack", "Thumbnail_InputSystemManager", "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_InputSystemManager.png[Thumbnail_InputSystemManager]");

                                public static readonly EntryData[] All = new EntryData[]
                                {
                                    ThumbnailInputSystemManager,
                                };
                            }
                        }
                        public static class ThumbnailLocalizationPng
                        {
                            public static readonly EntryData Data = new EntryData(
                                "TestPack",
                                "Thumbnail_Localization",
                                "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_Localization.png",
                                new EntryData[]
                                {
                                    SubAssets.ThumbnailLocalization,
                                });

                            public static class SubAssets
                            {
                                public static readonly EntryData ThumbnailLocalization = new EntryData("TestPack", "Thumbnail_Localization", "Assets/Mu3LibrarySamples/Sample_Template/Images/SceneThumbnails/Thumbnail_Localization.png[Thumbnail_Localization]");

                                public static readonly EntryData[] All = new EntryData[]
                                {
                                    ThumbnailLocalization,
                                };
                            }
                        }
                    }
                }

                public static class TestImage03SubAssets
                {
                    public static readonly EntryData TestImage03 = new EntryData("TestPack", "TestImage03", "TestImage03[TestImage03]");

                    public static readonly EntryData[] All = new EntryData[]
                    {
                        TestImage03,
                    };
                }

                private static readonly EntryData _TestImage03 = new EntryData(
                    "TestPack",
                    "TestImage03",
                    "TestImage03",
                    TestImage03SubAssets.All);
                public readonly EntryData TestImage03 = _TestImage03;

                private static readonly EntryData _SceneThumbnails = AssetsMu3LibrarySamplesSampleTemplateImagesSceneThumbnails.Data;
                public readonly EntryData SceneThumbnails = _SceneThumbnails;

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

                internal TestPackData() : base(
                    "TestPack",
                    new Dictionary<string, EntryData>
                    {
                        { _SceneThumbnails.Name, _SceneThumbnails },
                        { _TestImage03.Name, _TestImage03 },
                    },
                    new Dictionary<string, LabelData>
                    {
                        { Labels.DownloadAll.Value, Labels.DownloadAll },
                        { Labels.TestImage.Value, Labels.TestImage },
                    })
                { }
            }

            public sealed class TestPack02Data : GroupData
            {
                public new static class Labels
                {
                    public static readonly LabelData DownloadAll = AddressableGroupKeys.Labels.DownloadAll;
                    public static readonly LabelData TestImage = AddressableGroupKeys.Labels.TestImage;
                    public static readonly LabelData TestLabel = AddressableGroupKeys.Labels.TestLabel;

                    public static readonly LabelData[] All = new LabelData[]
                    {
                        DownloadAll,
                        TestImage,
                        TestLabel,
                    };
                }

                public static class TestImageSubAssets
                {
                    public static readonly EntryData TestImage = new EntryData("TestPack02", "TestImage", "TestImage[TestImage]");

                    public static readonly EntryData[] All = new EntryData[]
                    {
                        TestImage,
                    };
                }

                private static readonly EntryData _TestImage = new EntryData(
                    "TestPack02",
                    "TestImage",
                    "TestImage",
                    TestImageSubAssets.All);
                public readonly EntryData TestImage = _TestImage;

                public static class TestImage02SubAssets
                {
                    public static readonly EntryData TestImage02 = new EntryData("TestPack02", "TestImage02", "TestImage02[TestImage02]");

                    public static readonly EntryData[] All = new EntryData[]
                    {
                        TestImage02,
                    };
                }

                private static readonly EntryData _TestImage02 = new EntryData(
                    "TestPack02",
                    "TestImage02",
                    "TestImage02",
                    TestImage02SubAssets.All);
                public readonly EntryData TestImage02 = _TestImage02;

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

                internal TestPack02Data() : base(
                    "TestPack02",
                    new Dictionary<string, EntryData>
                    {
                        { _TestImage.Name, _TestImage },
                        { _TestImage02.Name, _TestImage02 },
                    },
                    new Dictionary<string, LabelData>
                    {
                        { Labels.DownloadAll.Value, Labels.DownloadAll },
                        { Labels.TestImage.Value, Labels.TestImage },
                        { Labels.TestLabel.Value, Labels.TestLabel },
                    })
                { }
            }

            public sealed class TestSceneData : GroupData
            {
                public new static class Labels
                {
                    public static readonly LabelData TestScene = AddressableGroupKeys.Labels.TestScene;

                    public static readonly LabelData[] All = new LabelData[]
                    {
                        TestScene,
                    };
                }

                private static readonly EntryData _SampleAddressables = new EntryData("TestScene", "Sample_Addressables", "Sample_Addressables");
                public readonly EntryData SampleAddressables = _SampleAddressables;
                private static readonly EntryData _SampleAddressablesAdditive = new EntryData("TestScene", "Sample_AddressablesAdditive", "Sample_AddressablesAdditive");
                public readonly EntryData SampleAddressablesAdditive = _SampleAddressablesAdditive;

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

                internal TestSceneData() : base(
                    "TestScene",
                    new Dictionary<string, EntryData>
                    {
                        { _SampleAddressables.Name, _SampleAddressables },
                        { _SampleAddressablesAdditive.Name, _SampleAddressablesAdditive },
                    },
                    new Dictionary<string, LabelData>
                    {
                        { Labels.TestScene.Value, Labels.TestScene },
                    })
                { }
            }
        }
    }
}