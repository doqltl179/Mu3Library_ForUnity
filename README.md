# Mu3Library_ForUnity
Unity용 Mu3Library 패키지와 샘플 프로젝트 모음입니다.

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
- 샘플(프로젝트용): `Assets/Mu3LibrarySamples`

## Optional Packages / Defines
Mu3Library.asmdef의 Version Define 기준으로 아래 패키지가 있으면 기능이 자동 활성화됩니다.
- com.cysharp.unitask → MU3LIBRARY_UNITASK_SUPPORT (UniTask 기반 비동기 API)
- com.unity.inputsystem → MU3LIBRARY_INPUTSYSTEM_SUPPORT (UI/MVP 입력 시스템 연동)
- com.unity.localization → MU3LIBRARY_LOCALIZATION_SUPPORT (Localization 모듈)
- com.unity.addressables → MU3LIBRARY_ADDRESSABLES_SUPPORT (Addressables 모듈)

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

Sample_Template 주요 구성:
- Scenes: Main, Sample_MVP, Sample_Addressables, Sample_Localization, Sample_WebRequest, Sample_Audio, Splash
- Fonts: NotoSans KR/JP/EN Bold (SDF 포함)
- Localization: Locales(KO/JA/EN), String Table 샘플
- Resources: MVP 샘플용 Prefab/AnimationConfig

## Recent Updates (0.1.6)
Runtime
- ScreenShot 런타임 헬퍼 정리 (ScreenCaptureHelper/ScreenCaptureObject 제거)

Editor
- Util Window에 Screen Capture Drawer 추가 (Game/Scene View 캡처, 파일명/폴더/타임스탬프 옵션)
- Sample_UtilWindow Drawer 프로퍼티 정리

Samples / Assets
- Sample_Template에 Optional Packages용 Version Define 추가 (TEMPLATE_ADDRESSABLES_SUPPORT/TEMPLATE_LOCALIZATION_SUPPORT 등)
- Sample_Template 씬 썸네일 이미지 추가
- Utility용 원형 InnerShadow 텍스처 추가

## Metadata
- Package name: `com.github.doqltl179.mu3libraryassets.base`
- Version: `0.1.6`
- Author: Mu3 (https://github.com/doqltl179)
