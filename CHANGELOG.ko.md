# 변경 이력 (Changelog)
<div align="center">

### 🌐 언어

[English](CHANGELOG.md) · [한국어](CHANGELOG.ko.md) · [日本語](CHANGELOG.ja.md)

</div>
Mu3Library For Unity의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 문서의 형식은 [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](https://semver.org/spec/v2.0.0.html)을 준수합니다.

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
- 패키지 이름: `com.github.doqltl179.mu3libraryassets.base`
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
   https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Assets/Mu3LibraryAssets
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

[0.1.11]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.0.20...v0.1.11
[0.0.20]: https://github.com/doqltl179/Mu3Library_ForUnity/releases/tag/v0.0.20
