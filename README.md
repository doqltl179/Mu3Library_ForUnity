# Mu3Library_ForUnity
Unity용 Mu3Library 패키지 샘플 프로젝트 모음입니다.

## Requirements
- Unity 6 (6000.0+)

## Package Overview
- UPM 패키지: `Assets/Mu3LibraryAssets`
- Runtime 모듈:
  - Addressable
  - Attribute
  - Audio
  - DI
  - Extensions
  - Localization
  - ObjectPool
  - Observable
  - Resource
  - Scene
  - UI
  - Utility
  - WebRequest
- Editor 확장: `Assets/Mu3LibraryAssets/Editor`
- 샘플(UPM 등록): `Assets/Mu3LibraryAssets/Samples~`
- 샘플(프로젝트): `Assets/Mu3LibrarySamples`

## Optional Packages / Defines
Mu3Library.asmdef의 Version Define 기준으로 아래 패키지가 있으면 기능이 활성화됩니다.
- com.cysharp.unitask -> MU3LIBRARY_UNITASK_SUPPORT (UniTask 기반 비동기 API)
- com.unity.inputsystem -> MU3LIBRARY_INPUTSYSTEM_SUPPORT (UI/MVP 입력 시스템 연동)
- com.unity.localization -> MU3LIBRARY_LOCALIZATION_SUPPORT (Localization 모듈)
- com.unity.addressables -> MU3LIBRARY_ADDRESSABLES_SUPPORT (Addressables 모듈)

## Install
### Package Manager (Git URL)
1) Unity Editor에서 Package Manager를 엽니다.
2) `+` 버튼 -> `Add package from git URL...`
3) 아래 URL 입력 후 `Add` 클릭:
   `https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Assets/Mu3LibraryAssets`

### Package Manager (Local Disk)
1) `+` 버튼 -> `Add package from disk...`
2) `Assets/Mu3LibraryAssets/package.json` 선택.

## Samples
Package Manager의 Samples 탭에서 아래 샘플을 가져올 수 있습니다.
- Sample_Template
- Sample_Attribute
- Sample_UtilWindow

Sample_Template 주요 구성:
- Scenes: Main, Sample_MVP, Sample_Addressables, Sample_AddressablesAdditive, Sample_Localization, Sample_WebRequest, Sample_Audio, Splash
- Fonts: NotoSans KR/JP/EN Bold (SDF 포함)
- Localization: Locales(KO/JA/EN), String Table 샘플
- Resources: MVP 샘플용 Prefab/AnimationConfig

## Recent Updates (0.1.8)
Runtime
- DI 컨테이너 구조 개편 (스코프 기반 라이프사이클, Inject 기반 멤버 주입, 등록 API 확장)
- Scene 모듈에 Addressables 기반 씬 로드/언로드 지원 추가

Samples / Assets
- Addressables Additive Load/Unload 샘플 추가 및 Sample_Addressables 데모 확장

## Metadata
- Package name: `com.github.doqltl179.mu3libraryassets.base`
- Version: `0.1.8`
- Author: Mu3 (https://github.com/doqltl179)
