# Mu3Library For Unity - AI Coding Instructions

You are an expert C# and Unity developer assistant. Follow these project-specific patterns and architectural principles when contributing to this codebase.

## 🏗 Big Picture Architecture
This project uses a **Distributed Core & Custom DI** architecture.
- **Cores (`CoreBase`)**: Independent modules (e.g., `AudioCore`, `UICore`) that manage their own local `ContainerScope`.
  - Override `ConfigureContainer(ContainerScope scope)` to register services.
  - Lifecycle: `InitializeCore()`, `UpdateCore()`, `LateUpdateCore()`, `DisposeCore()` are called by `CoreRoot`.
- **DI System**: A custom dependency injection library.
  - **LIFETIMES**: `Singleton` (one per container), `Transient` (new per resolve), `Scoped` (one per scope).
  - **REGISTRATION**: Use `scope.Register<TService, TImpl>()`, `scope.RegisterInstance()`, or `scope.RegisterFactory()`.
  - **LIFECYCLE**: Automatic management for `IInitializable` (`Initialize()`), `IUpdatable` (`Update()`), `ILateUpdatable` (`LateUpdate()`), and `IDisposable` (`Dispose()`).
- **Inter-Core Communication**: Facilitated by `CoreRoot`.
  - Use `GetClassFromOtherCore<TCore, T>()` for manual resolution.
  - Use `[Inject(typeof(TCore))]` for automatic cross-core injection in `CoreBase`.

## 🎨 UI Pattern (MVP)
The project strictly follows an **MVP (Model-View-Presenter)** pattern managed by `IMVPManager`.
- **View**: MonoBehaviors inheriting from `View`. Handles Unity components.
  - Use `_rectTransform`, `_canvas`, `_canvasGroup` getters (lazily initialized).
  - Layers: `SortingOrder` and `CanvasLayerName` manage UI depth.
- **Presenter**: Logic classes inheriting from `Presenter<TView, TModel, TArgs>`.
  - Lifecycle: `LoadFunc()`, `UnloadFunc()`, `OpenFunc()`, `CloseFunc()`.
  - Data Flow: Presenter mediates between `_model` and `_view`.
- **Model**: Data containers inheriting from `Model<TArgs>`.

## 🛠 Critical Workflows & Patterns
- **Injection**: Annotate private fields with `[Inject]`.
  ```csharp
  [Inject(typeof(UICore))] private IMVPManager _mvpManager;
  [Inject] private ILocalService _localService; // Local to same core
  ```
  Injection occurs in `CoreBase.Start()`. Call `base.Start()` first!
- **Optional Packages**: Use preprocessor defines:
  - `MU3LIBRARY_UNITASK_SUPPORT` (UniTask)
  - `MU3LIBRARY_ADDRESSABLES_SUPPORT` (Addressables)
  - `MU3LIBRARY_LOCALIZATION_SUPPORT` (Localization)
  - `MU3LIBRARY_INPUTSYSTEM_SUPPORT` (Input System)
- **Extension Methods**: check `Mu3Library.Extensions` for `GetOrAddComponent`, `SetAlpha`, `SetInteraction`, etc.
- **Scene Loading**: Use `ISceneLoader` (often in `SceneCore`) which supports both standard and Addressables-based loading.

## 📝 Coding Conventions
- **Naming**: Private fields: `_myField`. Serialized private fields: `_myField`.
- **Formatting**: Use [Braces] on new lines. Space after `if`, `foreach`, etc.
- **Attributes**:
  - `[Inject]`: Dependency identification.
  - `[ConditionalHide(nameof(_boolField), true)]`: Inspector organization.
- **Namespaces**: `Mu3Library.DI`, `Mu3Library.UI.MVP`, `Mu3Library.Extensions`.

## 📂 Key Files
- `CoreBase.cs`: `Assets/Mu3LibraryAssets/Runtime/Scripts/DI/CoreBase.cs`
- `ContainerScope.cs`: `Assets/Mu3LibraryAssets/Runtime/Scripts/DI/ContainerScope.cs`
- `Presenter.cs` & `View.cs`: `Assets/Mu3LibraryAssets/Runtime/Scripts/UI/MVP/`
- `MVPManager.cs`: `Assets/Mu3LibraryAssets/Runtime/Scripts/UI/MVP/MVPManager.cs`

<!--
# Mu3Library For Unity - AI 코딩 지침 (한글 참고용)

당신은 전문 C# 및 Unity 개발자 보조입니다. 이 코드베이스에 기여할 때 다음 프로젝트 전용 패턴과 설계 원칙을 따르십시오.

## 🏗 전체 아키텍처
이 프로젝트는 **분산형 코어(Distributed Core) 및 커스텀 DI** 아키텍처를 사용합니다.
- **Cores (CoreBase)**: 자체 로컬 ContainerScope를 관리하는 독립 모듈 (예: AudioCore, UICore).
  - ConfigureContainer(ContainerScope scope)를 오버라이드하여 서비스를 등록합니다.
  - 라이프사이클: InitializeCore(), UpdateCore(), LateUpdateCore(), DisposeCore()는 CoreRoot에 의해 호출됩니다.
- **DI 시스템**: 커스텀 의존성 주입 라이브러리.
  - **LIFETIMES**: Singleton (컨테이너당 하나), Transient (해결할 때마다 새로 생성), Scoped (스코프당 하나).
  - **REGISTRATION**: scope.Register<TService, TImpl>(), scope.RegisterInstance(), 또는 scope.RegisterFactory()를 사용합니다.
  - **LIFECYCLE**: IInitializable, IUpdatable, ILateUpdatable, IDisposable 인터페이스를 통한 자동 라이프사이클 관리.
- **코어 간 통신**: CoreRoot를 통해 이루어집니다.
  - GetClassFromOtherCore<TCore, T>()를 사용하여 수동으로 서비스를 가져옵니다.
  - CoreBase 내에서 [Inject(typeof(TCore))]를 사용하여 자동 교차 코어 주입을 수행합니다.

## 🎨 UI 패턴 (MVP)
이 프로젝트는 IMVPManager가 관리하는 **MVP (Model-View-Presenter)** 패턴을 엄격히 따릅니다.
- **View**: View를 상속받은 MonoBehaviour. Unity 컴포넌트 처리를 담당합니다.
  - _rectTransform, _canvas, _canvasGroup 등의 게터를 사용하십시오 (지연 초기화).
  - SortingOrder 및 CanvasLayerName으로 UI 깊이를 관리합니다.
- **Presenter**: Presenter<TView, TModel, TArgs>를 상속받은 로직 클래스.
  - 라이프사이클: LoadFunc(), UnloadFunc(), OpenFunc(), CloseFunc().
  - 데이터 흐름: Presenter가 Model과 View 사이를 중재합니다.
- **Model**: Model<TArgs>를 상속받은 데이터 컨테이너.

## 🛠 주요 워크플로우 및 패턴
- **주입(Injection)**: 프라이빗 필드에 [Inject]를 표시합니다.
  ```csharp
  [Inject(typeof(UICore))] private IMVPManager _mvpManager;
  [Inject] private ILocalService _localService; // 같은 코어 내 로컬 서비스
  ```
  주입은 CoreBase.Start()에서 발생합니다. base.Start()를 가장 먼저 호출하십시오!
- **선택적 패키지**: 전처리기 정의를 사용합니다:
  - MU3LIBRARY_UNITASK_SUPPORT (UniTask)
  - MU3LIBRARY_ADDRESSABLES_SUPPORT (Addressables)
  - MU3LIBRARY_LOCALIZATION_SUPPORT (Localization)
  - MU3LIBRARY_INPUTSYSTEM_SUPPORT (Input System)
- **확장 메서드**: Mu3Library.Extensions에서 GetOrAddComponent, SetAlpha, SetInteraction 등을 확인하십시오.
- **씬 로딩**: 표준 및 어드레서블 로딩을 모두 지원하는 ISceneLoader(주로 SceneCore에 위치)를 사용하십시오.

## 📝 코딩 컨벤션
- **명명 규칙**: 프라이빗 필드: _myField. 직렬화된 프라이빗 필드: _myField.
- **포맷팅**: 줄바꿈 브레이스 [Braces] 사용. if, foreach 뒤에 한 칸 공백.
- **속성(Attributes)**:
  - [Inject]: 의존성 식별.
  - [ConditionalHide(nameof(_boolField), true)]: 인스펙터 정리용.
- **네임스페이스**: Mu3Library.DI, Mu3Library.UI.MVP, Mu3Library.Extensions.

## 📂 주요 파일
- CoreBase.cs: Assets/Mu3LibraryAssets/Runtime/Scripts/DI/CoreBase.cs
- ContainerScope.cs: Assets/Mu3LibraryAssets/Runtime/Scripts/DI/ContainerScope.cs
- Presenter.cs & View.cs: Assets/Mu3LibraryAssets/Runtime/Scripts/UI/MVP/
- MVPManager.cs: Assets/Mu3LibraryAssets/Runtime/Scripts/UI/MVP/MVPManager.cs
-->
