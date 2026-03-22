# 변경 이력 (Changelog)
<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](../../CHANGELOG.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](CHANGELOG.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](CHANGELOG.ja.md)

</div>
Mu3Library For Unity의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 문서의 형식은 [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](https://semver.org/spec/v2.0.0.html)을 준수합니다.

## [Unreleased]

### 추가됨
- `MVPManager` / `IMVPManager`: `FocusIgnoredLayers` 프로퍼티와 `SetFocusIgnoredLayer(string layerName, bool ignored)` 메서드 추가.
  - 무시(ignored) 레이어의 Presenter는 포커스 및 `OutPanel` 업데이트 계산에서 제외됨.
  - 무시 레이어는 런타임에 토글 가능하며, 변경 즉시 `UpdateFocus()`가 호출됨.

## [0.5.0] - 2026-03-18

### 변경됨
- 리포지토리를 모노레포 구조로 개편: `Mu3Library_Base/`와 `Mu3Library_URP/`는 독립 UPM 패키지로, `UnityProject_BuiltIn/`과 `UnityProject_URP/`는 별도 개발 프로젝트로 분리됨.
- `.gitignore`의 무시 패턴에 `**/` 접두어를 추가하여 모노레포 하위 프로젝트 전체를 포함하도록 개선.

### 수정됨
- `CoreBase.WaitForOtherCore`: `CoreRoot.Instance`가 null일 때(예: 앱 종료 시점)에 발생하던 `NullReferenceException` 수정.
- `CoreBase.GetClassFromOtherCore`: 동일한 null 안전 처리 적용.
- `ContainerScope.ResolveFromCore`: 동일한 null 안전 처리 적용.
- 문서: 전체 README의 `ConfigureContainer()` 코드 예제 수정 — 잘못된 `ContainerScope scope` 파라미터를 제거하고 서비스 등록에 `RegisterClass<T>()`를 사용하도록 수정.

## [0.4.7] - 2026-03-15

### 추가됨
- `ScriptBuilder`: `ArrayBlock` 구조체(`FieldName`, `Values`)와 `AppendArrayBlock` 메서드 추가.
  - `ArrayBlock`을 `CodeBlock.Content` 목록에 `string`, `CodeBlock`과 함께 배치할 수 있음.
  - 들여쓰기는 `ScriptBuilder`가 자동으로 처리하며 `CodeBlock` 출력과 일관성 유지.

### 변경됨
- `AddressableGroupNameExporterDrawer`: `BuildArrayLines` 헬퍼를 `ScriptBuilder.ArrayBlock`으로 교체.
  - `AllNames`, `AllAddresses`, `Labels.All` 배열 선언이 `foreach` 루프 대신 단일 `.Add()` 호출로 축약됨.

## [0.4.6] - 2026-03-15

### 추가됨
- `AudioManager.Resource`: 키 기반 `AudioClip` 등록 시스템 추가.
  - `RegisterAudioResource(string key, AudioClip clip)`: 단일 클립을 키에 등록.
  - `RegisterAudioResources(Dictionary<string, AudioClip> resources)`: 여러 클립을 일괄 등록.
- `IAudioManager` / `AudioManager`: 등록된 키로 오디오를 재생하는 `WithKey` 오버로드를 전 채널 타입에 추가.
  - BGM: `PlayBgmWithKey`, `PlayBgmForceWithKey`, `TransitionBgmWithKey`
  - SFX: `PlaySfxWithKey`, `StopFirstSfxWithKey`, `FadeInSfxWithKey`, `FadeOutFirstSfxWithKey`
  - Environment: `PlayEnvironmentWithKey`, `StopFirstEnvironmentWithKey`, `FadeInEnvironmentWithKey`, `FadeOutFirstEnvironmentWithKey`

### 변경됨
- `IAudioManager.Bgm`, `IAudioManager.Sfx`, `IAudioManager.Environment`: 인터페이스 선언을 알파벳 순으로 정렬하고 동작 유형별로 그룹화하여 가독성 향상.
- `AudioManager.Bgm`, `AudioManager.Sfx`, `AudioManager.Environment`: public 메서드를 알파벳 순으로 정렬.
- `WithKey` 오버로드는 위임 패턴을 사용 — 짧은 오버로드는 전체 인자를 받는 오버로드에 위임하고, `TryGetCachedAudioResource`는 해당 오버로드 안에서 한 번만 호출.

## [0.4.5] - 2026-03-14

### 변경됨
- `AddressableGroupNameExporterDrawer`: 하위 에셋 클래스명이 상위 클래스명으로 시작하는 경우 해당 접두어를 제거하도록 변경.
  - 예: 상위 `Views`, 하위 `ViewsDialoguePanelPrefab` → `DialoguePanelPrefab`으로 출력.
  - 중첩된 폴더 계층에도 재귀적으로 적용.

## [0.4.4] - 2026-03-14

### 변경됨
- `AddressableGroupNameExporterDrawer`: 폴더 에셋 지원 추가.
  - `AssetDatabase.IsValidFolder()`로 그룹에 폴더로 등록된 에셋을 감지.
  - 폴더 에셋의 경우 `GatherAllAssets()`로 하위 에셋을 수집하여 `Assets` inner static class에 중첩 출력.
  - 에디터 프리뷰에서 폴더 진입점은 `[Folder]` 접두어로 표시되고, 하위 에셋은 들여쓰기하여 표시.

## [0.4.3] - 2026-03-14

### 추가됨
- `AddressableGroupNameExporterDrawer`: 에디터 시점에 모든 Addressable 그룹을 읽어 그룹 이름, 에셋 이름, address(key), 레이블을 중첩 C# static 클래스로 내보내는 에디터 Drawer를 추가 (`MU3LIBRARY_ADDRESSABLES_SUPPORT` 조건 컴파일).
  - `Labels` 내부 클래스에 레이블별 `const string` 필드와 함께 모든 레이블 값을 담은 `static readonly string[] All`을 제공.
- `Sample_UtilWindow`: `AddressableGroupNameExporter` 샘플 에셋을 유틸리티 윈도우 Drawer 목록에 추가.
- `Sample_Template`: Addressable 그룹/어드레스 상수 생성 예제로 `AddressableGroupKeys`를 추가.
- `Mu3Library.Editor.asmdef`: `Unity.Addressables` 및 `Unity.Addressables.Editor` 선택적 참조와 `MU3LIBRARY_ADDRESSABLES_SUPPORT` 버전 정의를 추가.

## [0.4.2] - 2026-03-08

### 추가됨
- `LocalizationNameExporterDrawer`: Localization 스트링 테이블 이름과 엔트리 키를 C# 상수로 내보내 미리 선언된 조회에 사용할 수 있는 에디터 Drawer를 추가.
- `Sample_UtilWindow`: 유틸리티 윈도우 Drawer 목록에 `LocalizationNameExporter` 샘플 에셋을 추가.
- `Sample_Template`: Localization 테이블/키 상수 예제로 생성된 `LocalizationTableKeys`를 추가.

### 변경됨
- `InputSystemNameExporterDrawer` 및 `LocalizationNameExporterDrawer`: 동작 변경 없이 backing field와 캐시된 accessor를 더 쉽게 구분할 수 있도록 private serialized helper 멤버 이름을 정리.

### 수정됨
- `LocalizationNameExporterDrawer`: 엔트리 키에서 올바른 PascalCase 클래스명을 생성하도록 `SanitizeIdentifier`를 수정. `-` 등 비식별자 문자는 단어 경계로 처리되어 생략되고 다음 문자를 대문자화. `_`는 그대로 출력되며 다음 문자를 대문자화 (예: `my-key_name` → `MyKey_Name`).

## [0.4.0] - 2026-03-08

### 추가됨
- `AudioSourceSettings`: 루프 동작을 설정 인스턴스별로 제어할 수 있는 `LoopCount` 및 `LoopInterval` 프로퍼티를 추가.
  - `LoopCount`: 재생 횟수 (`≤0` = 무한 반복, `1` = 1회 재생).
  - `LoopInterval`: 루프 사이클 사이의 대기 시간(초).
- `AudioSourceSettings`: 자주 사용되는 설정을 위한 명명된 프리셋 인스턴스를 추가.
  - `Standard`(무한 루프, 2D), `OneShot`(1회 재생, 2D)
  - `BgmStandard`, `BgmStandard3D`
  - `SfxStandard`, `SfxStandard3D`
  - `EnvironmentStandard`, `EnvironmentStandard3D`
- `Audio3dSoundSettings.Standard3D`: 완전한 3D 공간 블렌드(`spatialBlend = 1`)를 갖는 새 프리셋 추가.
- `AudioController`: `AudioSourceSettings`의 `LoopCount` 및 `LoopInterval`에 의해 구동되는 인터벌 포함 루프 재생 기능 추가.
- `AudioController`: 완료 콜백을 지원하는 `FadeIn` / `FadeOut` 코루틴 API 추가.

### 변경됨
- `FadeInFirstSfx(AudioClip, float)`가 `FadeInSfx(AudioClip, float)`로 이름이 변경되고 동작이 수정됨: 이미 재생 중인 인스턴스를 대상으로 하는 대신, **새 SFX 인스턴스를** 볼륨 `0`에서 재생하며 페이드 인.
- `FadeInFirstEnvironment(AudioClip, float)`가 `FadeInEnvironment(AudioClip, float)`로 이름이 변경되고 동일한 동작 변경 적용.
- `IAudioManager`: `SourceSettings`, `BaseSettings`, `SoundSettings` 프로퍼티를 제거 (호출별 `AudioSourceSettings` 파라미터로 대체됨).
- `AudioManager` 및 `IAudioManager`를 카테고리(`Bgm`, `Sfx`, `Environment`)별 partial 클래스 파일로 분리. 공개 API 변경 없음.

## [0.3.3] - 2026-03-02

### 추가됨
- `AudioManager`: 환경음 재생을 위한 `EnvironmentController` 기능 추가.
  - 새 `EnvironmentController` 클래스: `EnvironmentVolume`을 카테고리 볼륨으로 사용하여 오디오를 재생.
  - `EnvironmentInstanceCountMax` 프로퍼티 추가 (기본값: `3`, 최대: `5`).
  - `EnvironmentVolume`, `CalculatedEnvironmentVolume`, `ResetEnvironmentVolume()` 추가 (`AudioManager` 및 `IVolumeSettings`).
  - `PlayEnvironment`, `StopFirstEnvironment`, `StopEnvironmentAll`, `PauseEnvironmentAll`, `UnPauseEnvironmentAll` 메서드 추가 (`AudioManager` 및 `IAudioManager`).
  - `OnEnvironmentVolumeChanged` 이벤트 추가 (`IAudioManagerEventBus`).
  - `Stop()`, `Pause()`, `UnPause()`가 환경음도 포함하도록 갱신.

## [0.3.2] - 2026-03-02

### 수정됨
- `Mu3WindowDrawer`: 반복적인 `BeginChangeCheck` / `RecordObject` / `SetDirty` 보일러플레이트를 제거하기 위해 기반 클래스에 `DrawWithUndo<T>(Func<T>, Action<T>, string)` 헬퍼를 추가.
- `Mu3WindowDrawer`: `DrawFoldoutHeader1` 및 `DrawFoldoutHeader2`가 명시적인 `!=` 비교 대신 `EditorGUI.BeginChangeCheck` / `EndChangeCheck` 방식으로 통일됨.
- `DependencyCheckerDrawer`, `FileFinderDrawer`, `InputSystemNameExporterDrawer`, `MVPHelperDrawer`, `ScreenCaptureDrawer`: 모든 인터랙티브 필드가 새로운 `DrawWithUndo<T>` 헬퍼를 통해 undo/redo 상태를 올바르게 기록하도록 수정.

## [0.3.1] - 2026-03-02

### 수정됨
- `MVPManager`: View가 Load 중 기본 상태(예: alpha 1)로 한 프레임 렌더링되는 싱크 문제를 수정.
  - `Open()` 호출 시 즉시 `SetActiveView(true)` 하던 처리를 `SetActiveView(false)`로 변경하고,
    Load 완료 후 `Open()` 시작 직전에 `SetActiveView(true)`를 호출하도록 수정.
  - 이로 인해 애니메이션(예: alpha 0→1)이 View 초기 상태와 동기화된 후 시작됩니다.

## [0.3.0] - 2026-03-01

### 추가됨
- `InputSystemManager`: 새로운 Input System 모듈 추가 (`MU3LIBRARY_INPUTSYSTEM_SUPPORT` 필요):
  - `InputActionAsset`을 커스텀 ID로 등록; GUID 기반 및 이름 기반 액션/맵 조회 지원.
  - `StartInteractiveRebind(...)`를 통한 인터랙티브 리바인딩; 디바이스 타입 필터링 및 취소 컨트롤 지원.
  - 에셋/액션맵/액션 단위 바인딩 오버라이드 직렬화: JSON으로 저장 및 적용.
  - 전체 에셋 또는 개별 액션맵 활성화/비활성화.
- `InputSystemNameExporterDrawer`: Input System 액션 이름을 문자열 상수로 내보내는 에디터 Drawer 추가.
- `LocalizationCharacterCollectorDrawer`: Localization 스트링 테이블에서 문자를 수집·확인하는 에디터 Drawer 추가.
- `PresenterBase.CloseSelf(bool forceClose = false)`: Presenter가 외부 호출자 없이 주입된 `IMVPManager`를 통해 스스로를 닫을 수 있습니다.

### 변경됨
- `PresenterBase.Initialize(View, Arguments)` 및 `PresenterBase.Initialize(Arguments)`가 `public`에서 `internal`로 변경됨.
  - 초기화는 이제 `MVPManager`가 독점적으로 관리하며, 외부 코드에서 직접 호출할 수 없습니다.
- `LayerCanvas`가 각 항목에 맞게 Layer 값을 자동으로 동기화합니다.

## [0.2.3] - 2026-02-16
### 변경됨
- Audio 볼륨 계약에서 이벤트 버스 상속 분리:
  - `IAudioVolumeSettings`가 더 이상 `IAudioManagerEventBus`를 상속하지 않습니다.
- Observable API가 외부 소비자에 대한 읽기 전용 노출을 지원:
  - `Value` + `Subscribe(...)` 접근을 위한 `IObservableValue<TValue>` 추가
  - `ObservableProperty<T>` 및 `ObservableDictionary<TKey, TValue>`에 `ReadOnly` 노출 추가
  - 구독 토큰 처리를 전용 `SubscriptionToken` 파일로 분리
- MVP UI 설정과 런타임 안정성이 개선됨:
  - `OutPanelSettings`가 명시적 직렬화 필드를 가진 직렬화 가능한 구조체로 개선됨
  - `OutPanelSettings.Standard` 기본 dim 색상 알파값이 `0.5f`로 변경됨
  - `MVPManager`가 포커스 갱신 시 `EventSystem` 존재를 검증하고 누락 시 명시적 오류를 로그함

## [0.2.0] - 2026-02-14

### 추가됨
- Scene UniTask 비동기 API 추가 (취소 지원):
  - `ISceneLoader.LoadSingleSceneAsync`
  - `ISceneLoader.LoadAdditiveSceneAsync`
  - `ISceneLoader.UnloadAdditiveSceneAsync`

### 변경됨
- Addressables/Localization 초기화 계약이 명시적인 결과 상태를 제공:
  - `IsInitialized`
  - `IsInitializing`
  - `InitializeError`
  - `OnInitializeResult` 이벤트
  - `InitializeWithResult(Action<bool, string>)` API
- WebRequest API에 구조화된 결과형 추가:
  - `WebRequestResult<T>` (`IsSuccess`, `StatusCode`, `ErrorMessage`, `ResponseHeaders`, `Data`)
  - 콜백 API: `GetWithResult`, `PostWithResult`, `GetDownloadSizeWithResult`
  - UniTask API: `GetResultAsync`, `PostResultAsync`, `GetDownloadSizeResultAsync`
  - 결과형 API에 요청 타임아웃/재시도 옵션 추가
- CoreBase 직렬화 실행 순서 설정으로 Core 실행 순서 결정성 강화
- Scene 언로드 생명주기 이벤트 명시화:
  - `OnAdditiveSceneUnloadStart`
  - `OnAdditiveSceneUnloadEnd`
  - `LoadingCount`가 Additive 언로드 작업까지 포함하도록 개선
- 서비스 이벤트 계약을 전용 이벤트 버스 인터페이스로 분리:
  - `IAddressablesManagerEventBus`
  - `ILocalizationManagerEventBus`
  - `ISceneLoaderEventBus`
  - `IMVPManagerEventBus`
  - `IAudioManagerEventBus`
  - 기존 서비스 인터페이스에서 해당 `event` 멤버는 직접 선언하지 않음

## [0.1.11] - 2026-02-08

### 🌟 개요

**대규모 아키텍처 개편** - 새로운 DI 컨테이너 시스템, 향상된 MVP 패턴, 포괄적인 매니저 시스템을 포함한 완전한 패키지 구조 재편. 이번 릴리스는 라이브러리 핵심 아키텍처의 근본적인 재설계를 의미합니다.

### ⚠️ 호환성 파괴 변경사항 (BREAKING CHANGES)

#### 패키지 구조
- **완전한 폴더 구조 재편성**: `Runtime/`과 `Editor/` 디렉토리로 분리
- **어셈블리 정의 업데이트**: 적절한 의존성을 가진 새로운 asmdef 파일
- **네임스페이스 변경**: 모든 코드가 `Mu3Library` 네임스페이스 계층으로 이동
  - `Mu3Library.DI` - 의존성 주입(Dependency Injection)
  - `Mu3Library.MVP` - UI 패턴
  - Audio, Scene 등을 위한 모듈별 네임스페이스

#### API 변경사항
- **MVP 패턴**: MVP 시스템 완전 재작성
  - `Presenter<TView, TModel, TArgs>` 시그니처 변경
  - `View` 생명주기 메서드 구조 변경
  - `MVPManager` API 완전 재설계
  - 애니메이션 시스템이 MVP 뷰에 통합
- **DI 컨테이너**: 새로운 주입 시스템이 수동 초기화를 대체
  - 의존성 주입을 위한 `[Inject]` 어트리뷰트
  - `CoreBase`와 `CoreRoot` 기반 아키텍처
- **ResourceLoader**: DI 기반으로 변경, 더 이상 정적이 아님
- **SceneLoader**: 개별 구현을 가진 인터페이스 기반
  - Editor 씬을 위한 `ISceneLoader.Editor`
  - Addressables 씬을 위한 `ISceneLoader.Addressables`

#### 제거된 기능
- ❌ **InputSystem 헬퍼** (`InputSystem_Actions`, `InputSystemHelper`)
- ❌ **MarchingCubes 시스템** (전체 컴퓨트 셰이더 시스템 제거)
- ❌ **PostEffect/CommandBuffer 이펙트** (Blur, EdgeDetect, GrayScale, Toon 셰이더)
- ❌ **카메라 뷰 컴포넌트** (FirstPerson, ThirdPerson, FreeView 카메라)
- ❌ **커스텀 UI 컴포넌트** (DatePicker, IntSlider)
- ❌ **기존 샘플 씬** (Sample_InputAction, Sample_CustomUI, Sample_CommandBufferEffect, Sample_CameraView, Sample_MarchingCubes, Sample_RenderingPipeline)

### ✨ 주요 기능

#### 🏗️ 의존성 주입(DI) Container
- 세 가지 생명주기 범위를 가진 커스텀 DI 컨테이너:
  - `Singleton`: 컨테이너당 하나의 인스턴스
  - `Transient`: 요청마다 새 인스턴스
  - `Scoped`: 스코프당 하나의 인스턴스
- 모듈식 시스템 설계를 위한 `CoreBase` 아키텍처
- 자동 의존성 해결을 위한 `[Inject]` 어트리뷰트
- `[Inject(typeof(OtherCore))]`를 통한 크로스 코어 주입 지원
- 인터페이스 기반 생명주기 관리:
  - 초기화를 위한 `IInitializable`
  - Update 루프를 위한 `IUpdatable`
  - LateUpdate 루프를 위한 `ILateUpdatable`
  - 정리를 위한 `IDisposable`

#### 🎨 향상된 MVP 패턴
- DI 통합이 완료된 완전히 재설계된 MVP 시스템
- **AnimationView** 시스템과 설정 가능한 애니메이션:
  - 단일 커브 애니메이션을 위한 `OneCurveAnimation`
  - 이중 커브 애니메이션을 위한 `TwoCurveAnimation`
  - 재사용 가능한 설정을 위한 `AnimationConfig` ScriptableObject
- **MVPCanvasSettings**를 통한 세밀한 캔버스 설정:
  - Canvas 컴포넌트 설정
  - CanvasScaler 설정
  - GraphicRaycaster 설정
  - 배경/디밍을 위한 OutPanel 시스템
- 적절한 초기화 순서를 가진 향상된 뷰 생명주기
- 리소스 기반 및 카메라 기반 뷰 로딩
- 로딩 화면 통합

#### 📦 매니저 시스템
- **AddressablesManager**: 캐싱을 포함한 완전한 Addressables 지원
  - 참조 카운팅을 통한 에셋 로드/언로드
  - 씬 로딩 지원
  - 진행률 추적
  - UniTask 통합
- **LocalizationManager**: Unity 로컬라이제이션 통합
  - 비동기 초기화
  - 로케일 전환
  - 문자열 테이블 접근
  - UniTask 지원
- **WebRequestManager**: HTTP 요청 처리
  - GET/POST 요청
  - 다운로드 크기 조회
  - UniTask 통합
  - 콜백 기반 대안
- **AudioManager**: 향상된 오디오 시스템
  - 3D 공간 오디오 지원
  - 별도의 BGM 및 SFX 컨트롤러
  - `IVolumeSettings`를 통한 볼륨 관리
  - AudioSource 풀링
- **SceneLoader**: 유연한 씬 로딩
  - 에디터 씬 로딩
  - Addressables 씬 로딩
  - 추가(Additive) 씬 지원
  - 진행률 이벤트
  - 씬 로드 정책 (중복 허용/방지)
- **ResourceLoader**: 향상된 Resources 폴더 관리
  - 타입 안전 로딩
  - 참조 카운팅을 통한 캐싱
  - UniTask 지원

#### 🔧 유틸리티 & 확장 기능
- **Observable 타입**: 데이터 바인딩 지원
  - `ObservableProperty<T>`, `ObservableBool`, `ObservableInt`, `ObservableFloat`, `ObservableLong`, `ObservableString`
  - 컬렉션 변경 이벤트를 가진 `ObservableList<T>`
  - 딕셔너리 이벤트를 가진 `ObservableDictionary<TKey, TValue>`
- **GameObjectPool**: 컴포넌트 풀링 시스템
- **Extensions**: 풍부한 확장 메서드
  - `CameraExtensions`: 카메라 속성 복사
  - `TransformExtensions`: 자식을 포함한 레이어 관리
  - `intExtensions`: 비트 연산
  - Canvas 관련 확장
- **PlayerPrefsLoader**: 타입 안전 PlayerPrefs 접근

### 🎯 추가됨

#### 핵심 시스템
- 모듈식 아키텍처를 위한 `CoreBase`와 `CoreRoot`
- 서비스 등록 및 해결을 위한 `ContainerScope`
- 서비스 설정을 위한 `ServiceDescriptor`
- 인터페이스를 통한 자동 생명주기 관리

#### UI/MVP
- 뷰 애니메이션을 위한 `AnimationHandler`
- `AnimationConfig`, `OneCurveAnimation`, `TwoCurveAnimation` ScriptableObject
- UI 배경을 위한 `OutPanel` 시스템
- 세밀한 캔버스 제어를 위한 `MVPCanvasSettings`
- Camera 및 Resource 변형을 가진 `IMVPManager` 인터페이스

#### 매니저
- 완전한 CRUD 작업을 가진 `IAddressablesManager`
- 로컬라이제이션을 위한 `ILocalizationManager`
- 네트워크 요청을 위한 `IWebRequestManager`
- 볼륨 제어 인터페이스를 가진 `IAudioManager`
- Editor 및 Addressables 구현을 가진 `ISceneLoader`
- Resources 관리를 위한 `IResourceLoader`
- PlayerPrefs를 위한 `IPlayerPrefsLoader`

#### 에디터 도구
- **Mu3Window**: 통합 유틸리티 윈도우
  - MVPHelper: MVP 보일러플레이트 코드 생성
  - SceneList: 빠른 씬 네비게이션
  - FileFinder: 에셋 검색 및 정리
  - ScreenCapture: 에디터 내 스크린샷
  - DependencyChecker: 패키지 의존성 검증
- Observable 타입을 위한 커스텀 프로퍼티 드로어
- 코드 생성 헬퍼인 `ScriptBuilder`

#### 샘플
- **Sample_Template**: 포괄적인 샘플 프로젝트
  - Sample_MVP: MVP 패턴 데모
  - Sample_Audio: 오디오 시스템 쇼케이스
  - Sample_Audio3D: 3D 공간 오디오 예제
  - Sample_WebRequest: HTTP 요청 예제
  - Sample_Addressables: 에셋 로딩 데모
  - Sample_AddressablesAdditive: 추가 씬 로딩
  - Sample_Localization: 다국어 지원
  - LoadingScreen 구현
  - 애니메이션이 포함된 스플래시 화면

#### 에셋
- 기본 색상 머티리얼 (Black, White, Red, Green, Blue, Magenta)
- 샘플 폰트 (SDF가 포함된 NotoSans, NotoSansJP, NotoSansKR)
- 샘플 BGM 트랙 (3곡)
- 샘플 SFX 사운드 (3개 효과)
- UI 텍스처 에셋 (그림자가 있는 원, 1px 사각형)
- 샘플용 씬 썸네일

### 🔧 변경됨

#### 아키텍처
- 패키지 이름: `com.github.doqltl179.mu3library.base`
- Unity 버전 요구사항: 6000.0+ (Unity 6)
- 네임스페이스 구조 변경: 모든 코드가 `Mu3Library.*` 아래
- 어셈블리 분리: Runtime 및 Editor 어셈블리

#### MVP 시스템
- `Presenter` 생명주기 완전 재설계
- `View`가 이제 애니메이션 통합 지원
- Model-View-Presenter 바인딩 개선
- `MVPCanvasSettings`에 캔버스 관리 중앙화
- 뷰 인스턴스화가 이제 Resources 및 Camera 기반 로딩 지원

#### 오디오 시스템
- `BgmController`와 `SfxController`로 분리
- 세밀한 제어를 위한 `AudioSourceSettings` 추가
- 3D 오디오 위치 지정 지원
- `IAudioVolumeSettings`를 통한 볼륨 변경 이벤트

#### 씬 관리
- 여러 구현을 가진 인터페이스 기반 설계
- 로딩 작업을 위한 진행률 이벤트
- 중복 씬 로드 정책
- 더 나은 비동기 작업 지원

#### Observable 패턴
- 여러 기본 타입을 지원하도록 확장
- 컬렉션 타입 추가 (List, Dictionary)
- 에디터 통합을 위한 커스텀 프로퍼티 드로어
- 값 변경 콜백

#### 확장 기능
- `Overwrite`를 `CopyTo`로 이름 변경
- 컴포넌트 타입별로 정리
- 레이어 관리 헬퍼 추가
- 카메라 속성 복사

### 🐛 수정됨

#### 중요 수정사항
- **DI 컨테이너 생명주기 버그**: 서비스 생명주기 관리 문제 수정
- 여러 인터페이스 구현이 이제 올바르게 단일 인스턴스를 공유
- 컬렉션 불변성: 적절한 곳에 컬렉션을 `readonly`로 변경
- SceneLoader 이벤트 타이밍: `OnSceneLoadEnd` 콜백 타이밍 수정
- 코드베이스 전체의 null 참조 처리

#### 안정성 개선
- AnimationView 예외 처리 강화
- LocalizationManager 초기화 견고성
- 씬 로딩 상태 관리
- MVP 뷰 생명주기 엣지 케이스

### 🗑️ 제거됨

#### 완전한 시스템 제거
- InputSystem 헬퍼 클래스 및 생성된 코드
- MarchingCubes 컴퓨트 셰이더 시스템
- CommandBuffer 포스트 프로세싱 이펙트
- 카메라 컨트롤러 컴포넌트
- 커스텀 UI 컴포넌트 (DatePicker, IntSlider)
- 기존 샘플 씬 (6개 샘플 제거)
- 셰이더 컬렉션 (Toon, Blur, EdgeDetect, GrayScale 등)

#### 코드 정리
- 사용하지 않는 유틸리티 함수 제거
- 더 이상 사용되지 않는 MVP 구현 제거
- 기존 풀 시스템 제거 (GameObjectPool로 대체)
- 기존 싱글톤 구현 제거
- 레거시 Observable 구현 제거

### 📦 의존성

#### 추가됨
- ✅ **com.cysharp.unitask**: 비동기 작업을 위한 UniTask
- ✅ **com.coplaydev.unity-mcp**: Unity MCP 통합
- ✅ **com.unity.localization** (1.5.9): 로컬라이제이션 지원
- ✅ **com.unity.addressables** (패키지 매니저를 통한 암시적 의존성)

#### 업데이트됨
- Unity 6000.0+ (Unity 6) 필요
- .NET Standard 2.1

#### 제거됨
- ❌ 기존 Unity-MCP 패키지 (IvanMurzak)

### 📚 문서

#### 추가됨
- 다국어 README 파일 (영어, 일본어, 한국어)
- MIT 라이선스
- 포괄적인 인라인 문서 (XML 주석)
- 개발 지원을 위한 GitHub Copilot 에이전트 파일
- Unity 전용 지침 파일

#### 개선됨
- 상세한 기능 설명이 포함된 README
- 설치 지침 (Git URL 및 로컬 디스크 방법)
- 모든 주요 기능에 대한 코드 예제
- 샘플 씬 문서

### 🔄 마이그레이션 가이드

#### v0.0.20 사용자를 위한 가이드

**⚠️ 이것은 주요 호환성 파괴 릴리스입니다. 전체 프로젝트 업데이트를 권장합니다.**

##### 1단계: 클린 설치
1. 프로젝트에서 기존 패키지 제거
2. `Library/`의 모든 캐시 파일 삭제
3. 새로운 Git URL을 사용하여 v0.1.11 설치:
   ```
   https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base
   ```

##### 2단계: 네임스페이스 업데이트
```csharp
// 기존 (0.0.20)
using Mu3LibraryAssets;

// 신규 (0.1.11)
using Mu3Library;
using Mu3Library.DI;
using Mu3Library.MVP;
```

##### 3단계: DI 아키텍처로 마이그레이션
새 버전은 의존성 주입을 사용합니다. 초기화 코드를 업데이트하세요:

```csharp
// 기존: 수동 초기화
public class GameManager : MonoBehaviour
{
    private AudioManager audioManager;
    
    void Start()
    {
        audioManager = FindObjectOfType<AudioManager>();
    }
}

// 신규: DI 기반 접근
public class AudioCore : CoreBase
{
    protected override void ConfigureContainer(ContainerScope scope)
    {
        scope.Register<IAudioManager, AudioManager>(ServiceLifetime.Singleton);
    }
}

public class GameManager : CoreBase
{
    [Inject] private IAudioManager _audioManager;
    
    protected override void Start()
    {
        base.Start(); // 주입을 위해 필수
        // _audioManager 사용
    }
}
```

##### 4단계: MVP 코드 업데이트
기존 MVP 패턴을 사용하고 있었다면:

```csharp
// 기존 Presenter
public class OldPresenter : Presenter<MyView, MyModel>
{
    // 기존 구조
}

// 신규 Presenter
public class NewPresenter : Presenter<MyView, MyModel, MyArgs>
{
    // Arguments 클래스 정의 필수
}

public class MyArgs : Arguments { }
```

##### 5단계: 제거된 기능 대체
- **InputSystem**: Unity의 Input System을 직접 사용
- **카메라 컨트롤러**: 커스텀 구현 또는 서드파티 솔루션 사용
- **포스트이펙트**: Unity의 Post Processing Stack 또는 URP/HDRP 볼륨 시스템 사용
- **커스텀 UI**: Unity의 UI Toolkit 사용 또는 커스텀 컴포넌트 생성

##### 6단계: 리소스 로딩 업데이트
```csharp
// 기존: 정적 호출
var asset = ResourceLoader.Load<Sprite>("path");

// 신규: DI 기반
public class MyCore : CoreBase
{
    [Inject] private IResourceLoader _resourceLoader;
    
    void LoadAsset()
    {
        _resourceLoader.Load<Sprite>("path", (sprite) => {
            // sprite 사용
        });
    }
}
```

##### 7단계: 철저한 테스트
- 모든 DI 주입이 작동하는지 확인
- MVP 뷰가 올바르게 로드되는지 확인
- 오디오 재생 테스트
- 씬 전환 검증
- 사용되는 경우 Addressables 로딩 확인

### 🎉 감사의 말

오픈소스 커뮤니티에 감사드립니다:
- Cysharp의 UniTask (async/await 지원)
- CoplayDev의 Unity MCP (Model Context Protocol)
- 다양한 Creative Commons 출처의 샘플 오디오 에셋

---

## [0.0.20] - 이전 릴리스

### 추가됨
- ObservableProperty 구현

이전 버전에 대해서는 커밋 히스토리를 참조하세요.

[Unreleased]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.2.3...HEAD
[0.2.3]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.2.0...v0.2.3
[0.2.0]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.1.11...v0.2.0
[0.1.11]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.0.20...v0.1.11
[0.0.20]: https://github.com/doqltl179/Mu3Library_ForUnity/releases/tag/v0.0.20
