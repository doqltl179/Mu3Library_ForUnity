# Mu3Library_ForUnity
Unity용 Mu3Library 패키지와 샘플 프로젝트 모음입니다.

## Requirements
- Unity 6 (6000.0+)

## Package Overview
- UPM 패키지: `Assets/Mu3LibraryAssets`
- Runtime 모듈:
  - Attribute
  - Audio
  - DI
  - Extensions
  - ObjectPool
  - Observable
  - Scene
  - ScreenShot
  - UI
  - Utility
- Editor 확장: `Assets/Mu3LibraryAssets/Editor`
- 샘플(UPM 등록): `Assets/Mu3LibraryAssets/Samples~`
- 샘플(프로젝트용): `Assets/Mu3LibrarySamples`

## Install
### Package Manager (Git URL)
1) Unity Editor에서 Package Manager를 열어주세요.
2) `+` 버튼 → `Add package from git URL...`
3) 다음 URL 입력 후 `Add`를 눌러주세요.
   `https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Assets/Mu3LibraryAssets`

### Package Manager (Local Disk)
1) `+` 버튼 → `Add package from disk...`
2) `Assets/Mu3LibraryAssets/package.json`을 선택해주세요.

## Samples
Package Manager의 Samples 탭에서 아래 샘플을 가져올 수 있습니다.
- Sample_Template
- Sample_Attribute
- Sample_UtilWindow
- Sample_MVP

추가로 아래 샘플은 아직 미완성이라 별도 안내 전까지는 사용을 권장하지 않습니다.
- Sample_Addressables
- Sample_Localization

## Notes
현재 GenericSingleton, Singleton 기반 코드는 향후 DI 방식으로 전환될 예정이라, 관련 코드는 순차적으로 수정할 계획입니다.

## Metadata
- Package name: `com.github.doqltl179.mu3libraryassets.base`
- Version: `0.1.4`
- Author: Mu3 (https://github.com/doqltl179)
