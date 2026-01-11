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
  - ScreenShot
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

## Recent Updates (0.1.5)
Runtime
- AddressablesManager / IAddressablesManager 추가 (캐시, 다운로드 사이즈/의존성, 카탈로그 업데이트, 진행률 이벤트)
- LocalizationManager / ILocalizationManager 추가 (초기화, 문자열 조회, 로케일 변경)
- WebRequestManager / IWebRequestManager 추가 (GET/POST/HEAD, JSON/텍스처/바이너리 지원, UniTask API)
- ResourceLoader / IResourceLoader 추가 및 경로 이동 (Utility → Resource)
- SceneLoader 인터페이스 분리: ISceneLoader.Editor (에디터 전용 로드/언로드 API)
- UI/MVP 리팩토링: IMVPManager 분리, MVPCanvasSettings/OutPanelSettings 추가, MVPCanvasUtil로 정리
- Audio 구조 개선: AudioSourceSettings 통합 및 AudioManager 루트 오브젝트 관리
- ObservableBool 및 전용 Drawer 추가

Editor
- ConditionalHidePropertyDrawer가 Generic(Observable 등) 필드의 _value를 인식하도록 개선

Samples / Assets
- Sample_MVP 샘플을 Sample_Template로 이관 및 정리
- Addressables/Localization/WebRequest 샘플 씬 추가
- 샘플용 폰트/이미지/로컬라이제이션 테이블/프리팹 추가
- 기본 텍스처 네이밍 정리 및 1px 텍스처 추가

## Metadata
- Package name: `com.github.doqltl179.mu3libraryassets.base`
- Version: `0.1.5`
- Author: Mu3 (https://github.com/doqltl179)
