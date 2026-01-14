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
- Scenes: Main, Sample_MVP, Sample_Addressables, Sample_Localization, Sample_WebRequest, Sample_Audio, Splash
- Fonts: NotoSans KR/JP/EN Bold (SDF 포함)
- Localization: Locales(KO/JA/EN), String Table 샘플
- Resources: MVP 샘플용 Prefab/AnimationConfig

## Recent Updates (0.1.7)
Runtime
- DI 초기화 관련 오탈자 수정 (Initialize/InitializeAsync로 정리)

Samples / Assets
- 등록 목록에서 존재하지 않는 Sample_MVP 제거

Docs
- README 인코딩 정리 및 문구 개선

## Metadata
- Package name: `com.github.doqltl179.mu3libraryassets.base`
- Version: `0.1.7`
- Author: Mu3 (https://github.com/doqltl179)
