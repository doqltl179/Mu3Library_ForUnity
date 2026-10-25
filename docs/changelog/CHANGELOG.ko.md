# 변경 이력 (Changelog)
<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](../../CHANGELOG.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](CHANGELOG.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](CHANGELOG.ja.md)

</div>
Mu3Library For Unity의 모든 주요 변경사항은 이 파일에 기록됩니다.

이 문서의 형식은 [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)를 기반으로 하며,
이 프로젝트는 [Semantic Versioning](https://semver.org/spec/v2.0.0.html)을 준수합니다.

이 changelog는 패키지 릴리스 변경만 추적합니다. 저장소 개발 워크플로와 툴링 변경은 [`docs/repository/CHANGELOG.md`](../../docs/repository/CHANGELOG.md)에서 관리합니다.

## [Unreleased]

### 추가됨
- Base: `InjectableMembers.GetFields(Type)`와 `InjectableMembers.GetProperties(Type)`는 한 타입에서 `[Inject]`가 채우는 필드와 프로퍼티를 돌려줍니다. 컨테이너와 에디터의 DI 검증기가 모두 이를 쓰므로, 검증기는 컨테이너가 주입하는 멤버를 정확히 그대로 검사합니다.
- Base: `CoreBase.OnDisable()`이 `Awake`, `Start`, `OnEnable`, `OnDestroy`처럼 `protected virtual`입니다. 자체 `OnDisable`을 선언한 코어는 이를 재정의하고 `base.OnDisable()`을 호출해야 합니다. 그 전까지는 컴파일러가 경고 CS0114를 내고, 아래의 준비 관련 수정도 그 코어에는 적용되지 않습니다. `OnDisable`에서 UI 이벤트를 해제하는 Template 샘플 코어들은 이제 그렇게 합니다.
- Base: `TimeMode`(`Mu3Library.Utility`)는 시간에 따라 진행하는 전환이 어느 시계를 따를지 나타냅니다. `Unscaled`는 `Time.unscaledDeltaTime`을, `Scaled`는 `Time.deltaTime`을 따릅니다. 각 `AnimationHandler`에는 그 view의 **Time Mode** 필드와 `TimeMode` 프로퍼티가 있고, `IAudioManager.TimeMode`는 그 매니저의 모든 페이드, 덕킹, 루프 간격, 플레이리스트 트랙 사이 간격이 따를 시계를 정합니다. 둘 다 기본값은 `Unscaled`이며, 변경됨 항목에 설명이 있습니다. `IAudioManager`를 직접 구현한 클래스는 이 프로퍼티를 추가해야 합니다.
- Base: Addressables가 설치되어 있으면 `ISceneLoaderEventBus.OnSceneLoadFailed`가 시작한 뒤 실패한 Addressables single·additive 씬 로드를 알립니다. 예를 들어 어느 카탈로그에도 없는 키로 로드한 경우입니다. 이벤트의 `SceneLoadFailedInfo`에는 키(`Target`), `IsAdditive`, Addressables가 로드를 실패로 끝낸 메시지가 담깁니다. 이 이벤트가 발생할 때는 실패한 명령이 이미 끝난 상태라 `IsLoading`, `LoadingCount`와 상태 조회가 그 명령을 더 이상 세지 않으며, 핸들러가 다음 로드를 바로 시작할 수 있습니다. `ISceneLoaderEventBus`를 직접 구현한 클래스는 Addressables가 설치된 곳에서 이 이벤트를 추가해야 합니다.
- Base: `IAddressablesManager.UpdateCatalogs(callback, autoCleanBundleCache, autoReleaseHandle)`와, UniTask가 있으면 `UpdateCatalogsAsync(autoCleanBundleCache, autoReleaseHandle)`는 `autoCleanBundleCache`가 true일 때 갱신된 카탈로그가 더 이상 참조하지 않는 캐시 번들도 지웁니다. `IAddressablesManager`를 직접 구현한 클래스는 이 오버로드를 추가해야 합니다.
- Base Editor: `ScriptBuilder.TryBuild(spaces, out script, out error, blocks)`는 블록을 끝까지 만들 수 없으면 스크립트 없이 false와 그 이유를 돌려주고, `ScriptBuilder.MaxNestingDepth`는 빌드가 받아들이는 코드 블록의 최대 중첩 깊이(64)입니다. `ScriptIdentifier.IsValidIdentifier`와 `ScriptIdentifier.IsValidNamespace`는 이름을 바꾸지 않고 그대로 생성 코드에 쓸 수 있는지 알려 줍니다.
- Base Editor: Addressable Group Data Exporter와 Localization Data Exporter에 **Define Symbol** 필드가 생겼습니다. 값을 설정하면 내보내는 모든 스크립트를 생성 헤더 아래에서 `#if <심볼>`로 감싸므로, 프로젝트는 그 심볼이 정의된 곳에서만 이 스크립트를 컴파일합니다. 예를 들어 스크립트가 쓰는 패키지에 대해 어셈블리가 `versionDefines`로 정의하는 심볼을 넣을 수 있습니다. 올바른 심볼 이름이 아니면 **Generate C# Scripts**가 비활성화되고 그 이유가 표시됩니다. UtilWindow 샘플의 내보내기 도구는 이 값을 `TEMPLATE_ADDRESSABLES_SUPPORT`와 `TEMPLATE_LOCALIZATION_SUPPORT`로 설정하므로, Template 샘플의 키 스크립트를 다시 생성해도 가드가 유지됩니다.
- Watermelon Game: `BoardItemsConfig.IsComplete()`는 처음 11개 아이템 항목이 모두 아이템을 만들 수 있는지, 곧 항목이 있고 스프라이트를 가지는지 알려 주고, `IsComplete(List<int>)`는 아이템을 만들 수 없는 항목의 인덱스도 담아 줍니다. 보드와 `BoardConfig`는 이것으로 그런 카탈로그를 거부하고 알립니다. 자세한 내용은 수정됨에 적었습니다.
- Watermelon Game: `InputHandler.OnTouchCanceled`, `BoardArea.OnTouchCanceled`, `BoardController.OnDragCancel(Vector2)`는 놓기 전에 빼앗긴 드래그를, `InputHandler`가 그 드래그에 대해 마지막으로 보고한 화면 위치와 함께 알립니다. 이 위치는 보드 밖일 수 있습니다. 수정됨 항목에 설명이 있습니다.
- Watermelon Game: 기본으로 켜져 있는 `InputHandler.IgnorePressOverUI`(**Ignore Press Over UI** 필드)는 UI 위에서 누른 입력이 드래그를 시작하지 않게 합니다. 변경됨 항목에 설명이 있습니다. `InputHandler.DragStartFilter`로는 프로젝트가 자체 규칙으로 누름을 막을 수 있습니다. 마우스 누름과 터치가 드래그를 시작하기 전에 매번 그 화면 위치로 묻고, 추가한 함수가 모두 true를 돌려줘야 합니다. 서브클래스는 대신 `protected virtual`인 `CanStartDrag(Vector2)`와 `IsPointerOverUI(Vector2)`를 재정의할 수 있습니다.
- Watermelon Game: `InputHandler`는 `protected virtual`인 `IsMousePresent`, `MousePosition`, `GetMouseButtonDown()`, `GetMouseButton()`, `TouchCount`, `GetTouch(int)`로 입력을 읽으므로, 서브클래스가 다른 입력 소스로 보드를 움직일 수 있습니다. `BoardArea`는 자기 GameObject에 이미 있는 서브클래스를 쓰고 `InputHandler`를 따로 추가하지 않습니다.

### 변경됨
- Base, URP, Watermelon Game: 세 패키지가 지원하는 가장 낮은 에디터로 Unity 6000.3을 선언합니다(`package.json`의 `"unity": "6000.3"`, 이전에는 `"6000.0"`). README의 배지(6000.3+)와 요구사항(Unity 6.3)도 이에 맞췄습니다. URP 패키지는 이미 Unity 6000.3을 요구하는 URP 17.3.0에 의존했고 Watermelon Game은 URP 패키지에 의존하므로, 이전 하한은 두 패키지의 의존성과 맞지 않았습니다. Base도 6000.3보다 낮은 에디터에서 컴파일된 적이 없었습니다. 세 패키지는 Unity 6000.3.17f1에서 그 에디터에 번들된 Unity 패키지 버전(URP 17.3.0, uGUI 2.0.0 포함)으로 컴파일되고 EditMode 테스트도 통과하므로, 의존성 버전은 그대로 둡니다. Unity 6000.0~6000.2는 더 이상 지원하지 않습니다.
- Base: MVP view 애니메이션과 오디오의 페이드, 덕킹, 루프 간격, BGM 플레이리스트 트랙 사이 간격은 기본적으로 비스케일 시간으로 진행하므로, 일시정지 메뉴처럼 `Time.timeScale`이 0일 때도 계속 진행합니다. 이전에는 `Time.deltaTime`을 따랐습니다. 그래서 타임 스케일 0에서 연 `AnimationView`는 열리는 중에 머물러 `Close`, `CloseFocused`, 바깥 패널 클릭으로 닫을 수 없었고, `TransitionBgm`, `FadeOutBgm`, `DuckBgm`은 진행하지 않았습니다. 1이 아닌 타임 스케일에서도 이제 실제 시간의 속도로 진행합니다. 이전 동작을 유지하려면 `AnimationHandler`나 `AudioManager`에 `TimeMode.Scaled`를 설정하세요. 이 변경 전에 저장된 view에는 저장된 값이 없으므로 새 기본값을 따릅니다.
- Base: `CoreBase`, `CoreRoot`, `MVPManager`, `Singleton`, `GenericSingleton`은 Unity 6.4부터 `FindObjectsSortMode`를 받지 않는 `FindObjectsByType` 오버로드로 씬 오브젝트를 찾습니다. 6.4에서 이 인자를 받는 오버로드가 obsolete가 되었기 때문입니다. 이전 에디터는 기존 호출을 그대로 쓰므로, 어느 버전에서든 같은 오브젝트를 찾고 정렬하지 않은 채 돌려줍니다.
- Base: DI 컨테이너의 등록 로그가 Unity 6.6에서 사용 중단된 `DEVELOPMENT_BUILD` 심볼에 더 이상 기대지 않습니다. 에디터는 항상 로그를 남기고, 플레이어는 시작할 때 `Debug.isDebugBuild`를 한 번 읽으므로 이전처럼 development build에서만 로그가 남습니다.
- Base: `InputSystemManager.Dispose`는 `AddInputActionAsset`으로 추가한 에셋을 포함해 매니저가 가진 모든 Input Action 에셋을 끕니다. id로 에셋을 교체하거나 제거할 때 이미 그랬던 것과 같으며, 파괴하는 것은 여전히 JSON에서 직접 만든 에셋뿐입니다. 이전에는 매니저가 켠 외부 에셋이 스코프가 dispose된 뒤에도 계속 입력을 발생시켰습니다. 그런 에셋이 계속 동작해야 한다면 dispose 뒤에 다시 켜세요.
- Base Editor: Unity 6.4에서 `FindFirstObjectByType`이 사용 중단되어, DI 검증 drawer와 런타임 진단 drawer는 `FindAnyObjectByType`으로 `CoreRoot`를 찾습니다. `AppDomain.GetAssemblies()`는 Unity가 이미 내린 어셈블리까지 돌려줄 수 있어, MVP Helper는 Unity 6.5부터 `CurrentAssemblies.GetLoadedAssemblies()`에서 타입을 찾습니다.
- Watermelon Game: Unity 6.4에서 `FindFirstObjectByType`이 사용 중단되어, 샘플의 `WatermelonGameManager`는 `FindAnyObjectByType`으로 `BoardController`를 찾습니다. 샘플 씬에는 보드가 하나뿐이라 결과는 같습니다.
- Watermelon Game: UI 위에서 누른 입력은 더 이상 보드 드래그를 시작하지 않습니다. 그래서 보드 위에 그린 일시정지 버튼이나 아이템 버튼을 눌러도, 들고 있는 아이템이 따라오다가 손을 뗄 때 떨어지지 않습니다. `InputHandler`는 누른 위치를 현재 `EventSystem`으로 레이캐스트하며, 이 레이캐스트는 uGUI 캔버스와 UI Toolkit 패널에 닿습니다. UI Toolkit 패널에는 그 `EventSystem`에 입력 모듈이 있고 UI Toolkit 연동이 켜져 있는 동안(기본값)에만 닿습니다. `PhysicsRaycaster`나 `Physics2DRaycaster`가 맞힌 것은 씬 오브젝트이므로 세지 않고, `EventSystem`이 없으면 어떤 누름도 막지 않습니다. 보드를 덮는 전체 화면 레이캐스트 대상은 이제 모든 누름을 막습니다. 이전 동작을 유지하려면 **Ignore Press Over UI**를 끕니다.
- Watermelon Game: Input System 패키지가 설치된 "Both"에서 `InputHandler`는 Input Manager 대신 Input System을 읽습니다. "Input System Package (New)"에서와 같으며, 수정됨 항목에 설명이 있습니다. `OnTouchBeganHandler`, `OnTouchMovedHandler`, `OnTouchEndedHandler`가 받는 `Touch`를 읽는 서브클래스는 이때 `fingerId`로 Input System의 터치 ID를 받으며, 이 값은 새 터치가 다시 쓰지 않습니다.

### 제거됨
- Base: Template 샘플의 `Mu3Library.Sample.Template.Editor` 어셈블리 정의를 제거했습니다. 스크립트가 하나도 없어, 샘플을 임포트한 모든 프로젝트에서 Unity가 "will not be compiled, because it has no scripts associated with it" 경고를 남겼습니다.
- Base: Template 샘플의 NotoSans-Bold, NotoSansJP-Bold 폰트와 그 폰트 에셋을 제거했습니다. 둘을 쓰는 곳은 Localization 씬뿐이었고, 각각 텍스트의 폰트와 일본어 폴백이었습니다. 폴백 폰트 에셋에서 그린 문자는 TextMesh Pro가 TMP Essential Resources만 제공하는 설정을 읽게 만들므로, 이제 이 씬은 NotoSansKR-Bold 하나만 씁니다.
- URP: 패키지의 `Mu3Library.URP.Editor` 어셈블리 정의와 ScreenEffect 샘플의 `Mu3Library.URP.Sample.ScreenEffect.Editor`를 제거했습니다. 둘 다 스크립트가 없어, 패키지를 설치하거나 샘플을 임포트한 모든 프로젝트에서 같은 경고가 났습니다. 스크립트가 없는 어셈블리는 컴파일된 적이 없으므로, 이를 참조해도 아무 효과가 없었습니다.
- URP: URP가 설치되어 있으면 `Mu3Library.URP.asmdef`가 `versionDefines`로 정의하던 `MU3LIBRARY_URP_SUPPORT` Define 심볼을 제거했습니다. 이 심볼을 읽는 코드가 없었고, URP는 패키지의 필수 의존성이라 심볼은 항상 정의되어 있었으며 이 심볼로 감싼 코드도 없었습니다. `versionDefines`의 심볼은 그 어셈블리 안에만 존재하므로, 어셈블리 정의 참조로 추가한 스크립트처럼 `Mu3Library.URP`에 컴파일되는 코드만 이 심볼을 볼 수 있었습니다. 그런 코드가 이 심볼을 검사한다면 이제 Player Settings의 Scripting Define Symbols 등에서 직접 정의해야 합니다.

### 수정됨
- Base: 사용하던 API 두 개가 컴파일 오류가 된 Unity 6.5 이후 버전에서 패키지가 컴파일됩니다. `Scene.handle`이 더 이상 `int`로 변환되지 않는 `SceneHandle`이 되어, `SceneLoader`는 작업이 불러오거나 내리는 `Scene` 자체를 들고 씬끼리 비교합니다. `Object.GetInstanceID()`를 쓸 수 없게 되어, `GameObjectPool`은 참조로 풀 안의 오브젝트를 구별합니다. 그래서 컴포넌트가 `Equals`를 재정의해도 영향을 받지 않고, 경고에는 인스턴스 ID 대신 오브젝트 이름을 씁니다. 같은 코드가 패키지가 이제 지원하는 가장 낮은 에디터인 Unity 6000.3에서도 컴파일됩니다.
- Base: `package.json`이 패키지 코드가 define 심볼 밖에서 쓰는 Unity 패키지와 내장 모듈을 선언합니다. uGUI(`com.unity.ugui`)와 UI, Audio, Particle System, JSONSerialize, AssetBundle, UnityWebRequest, UnityWebRequestTexture 모듈, 그리고 에디터 도구가 쓰는 IMGUI, Image Conversion, Screen Capture 모듈입니다. uGUI를 제거했거나 이 모듈 중 하나를 끈 프로젝트에서는 읽기 전용인 패키지 안에서 컴파일 오류가 났지만, 이제 Package Manager가 이들을 켜진 상태로 유지합니다. 선택 통합(UniTask, Addressables, Localization, Input System, Mobile Notifications)은 이전처럼 `dependencies`에 넣지 않고 각자의 define 심볼 안에서만 컴파일됩니다.
- Watermelon Game: `package.json`이 런타임이 쓰는 Physics 2D, Audio, JSONSerialize 모듈도 선언합니다.
- Base: 파일이 한 번도 들어간 적 없는 폴더의 메타데이터 `Runtime/Prefabs.meta`가 더 이상 Git 설치에 포함되지 않습니다. Git은 빈 폴더를 저장하지 않아, 설치본에는 폴더 없이 `.meta`만 들어갔습니다.
- Base: UniTask가 설치되어 있을 때 `CoreBase.PrepareCoreAsync(CancellationToken)`의 기본 구현은 `async` 없이 완료된 `UniTask<bool>`을 돌려줍니다. 이전에는 `await` 없는 `async` 메서드여서 컴파일러 경고 CS1998이 나고, 호출할 때마다 상태 머신이 만들어졌습니다. 시그니처와 `protected virtual`은 그대로라 재정의한 코드도 그대로 컴파일됩니다.
- Base: DI 생명주기 콜백이 하나씩 따로 실행됩니다. 서비스의 `Initialize`, `Update`, `LateUpdate`, `Dispose`나 코어의 갱신에서 난 예외는 `Debug.LogException`으로 기록되고, 나머지 서비스와 코어는 그 프레임에도, 이후 프레임에도 계속 실행됩니다. 이전에는 서비스 하나가 실패하면 그 뒤의 모든 서비스와 코어가 멈췄고, `Initialize`가 실패한 코어는 `OnCoreInitialized`와 `OnCorePrepared`를 끝내 알리지 못했습니다. 이제 그 코어도 둘 다 알립니다. 코어가 초기화된 뒤 처음 resolve될 때 `Initialize`가 예외를 던진 서비스도 같은 방식으로 기록되고 그대로 반환됩니다.
- Base: `CoreRoot`가 코어들을 갱신하는 도중에 등록되거나 빠지는 코어(예: 서비스의 `Update`에서 인스턴스화한 코어)가 더 이상 진행 중인 순회의 순서를 바꾸지 않습니다. 이전에는 그 프레임에 어떤 코어는 두 번 갱신되고 어떤 코어는 건너뛰어졌습니다. 새 코어는 여전히 곧바로 초기화되고, 갱신 순회에는 다음 프레임부터 들어갑니다.
- Base: 기반 클래스가 선언한 `[Inject]` 필드와 프로퍼티가 private까지 포함해 주입됩니다. 파생 타입을 대상으로 한 리플렉션은 기반 클래스의 private 멤버를 보지 못해, 그 멤버는 오류 없이 null로 남았고 필수 멤버도 검사되지 않았습니다. 탐색은 `CoreBase`, `object`, 엔진 자체의 타입에서 멈춥니다. 에디터의 DI 검증기도 같은 멤버를 검사합니다. resolve할 수 없는 필수 기반 클래스 멤버는 이제 코어가 시작할 때 "Inject failed" 예외를 던집니다. 코어 자신의 클래스에 있는 필수 멤버가 늘 그랬던 것과 같으며, 그 코어는 더 이상 준비되지 않습니다. 그런 멤버를 가진 등록된 서비스는 resolve될 때 예외를 던지고, `RegisterClass<T>()`로 추가한 서비스라면 그 시점이 `ConfigureContainer` 안이어서 코어가 아예 등록되지 않습니다. 비어 있어도 되는 멤버는 `[Inject(required: false)]`로 표시하세요.
- Base: 등록 하나(구현 타입, 키, 수명)에서 나온 인스턴스는 어떤 서비스 타입으로 resolve하든 같습니다. 키가 있는 싱글턴은 서비스 타입마다 하나씩 만들어져 인스턴스마다 `Initialize`와 `Update`가 돌았고, 스코프드 등록은 한 스코프 안에서도 서비스 타입마다 다른 인스턴스를 주었습니다. 이제 한 서비스 타입에 등록한 팩토리 여러 개가 `ResolveAll`에서 각자 자기 인스턴스를 만들고, 첫 resolve 이후에 등록한 팩토리도 적용됩니다. 이전에는 싱글턴과 스코프드 팩토리가 모두 첫 팩토리가 만든 인스턴스를 돌려줬습니다. 트랜지언트 팩토리는 영향을 받지 않았습니다.
- Base: 애플리케이션 종료가 시작되면(에디터에서 플레이 모드를 나가는 경우 포함) `CoreRoot.Instance`가 새 `CoreRoot`를 만들지 않고 null을 돌려줍니다. 루트보다 늦게 파괴되는 코어가 더 이상 새 `CoreRoot` 오브젝트를 남기지 않습니다. Unity는 이를 "Some objects were not cleaned up when closing the scene"으로 보고했습니다.
- Base: UniTask가 없을 때, 준비 도중 컴포넌트나 오브젝트가 비활성화된 코어는 준비를 멈추고, 둘 다 다시 활성화되면 경고를 한 번 남기고 준비를 처음부터 다시 시작해 `OnPrepared`에 도달합니다. 이전에는 오브젝트가 비활성화되면 Unity가 준비 중인 코루틴을 멈춰 코어가 영원히 `IsPreparing`에 남았습니다. 컴포넌트만 비활성화하면 예전에는 준비가 계속 진행됐지만 이제는 이때도 준비를 멈춥니다. 그 뒤에 오브젝트를 비활성화하면 이를 알아챌 `OnDisable` 없이 코루틴이 멈췄기 때문이며, 그래서 비활성화된 채로 남은 코어는 더 이상 준비를 마치지 않습니다. `PrepareCoreAsync`가 돌려주는 루틴은 그 루틴이 yield하는 모든 루틴과 함께 코어의 코루틴 하나 안에서 실행되므로, 멈춘 준비가 그 자리를 이어받은 준비와 나란히 계속 진행되는 일은 없습니다. 다만 루틴이 `StartCoroutine`으로 시작한 코루틴이나 기다리는 비동기 작업은 여기에 들지 않아, 컴포넌트만 비활성화된 동안에도 계속 실행됩니다. UniTask 경로는 바뀌지 않습니다.
- Base: `ObservableProperty<T>`에 `[Serializable]`이 붙습니다. 이를 상속한 Observable 타입에는 이미 모두 붙어 있었습니다. Unity 6.6의 직렬화 분석기는 Attribute 샘플의 `ObservableFloat`처럼 이런 타입의 필드마다 경고 UAC1002를 냈습니다. `_value` 필드의 이름과 위치는 그대로이므로, 씬과 에셋에 이미 저장된 값은 전과 같이 읽힙니다.
- Base: 새로 만든 `ObservableList<T>`는 빈 리스트로 시작합니다. 그래서 첫 변경 전에도 `Value`와 `ReadOnly.Value`가 null이 아니고, 즉시 통지를 받는 구독자는 빈 리스트를 받습니다. 이전에는 다른 멤버를 처음 사용할 때에야 리스트가 만들어졌습니다.
- Base: `ObservableProperty<T>`나 `ObservableDictionary<TKey, TValue>`의 `Subscribe(callback, notifyImmediately: true)`가 부른 콜백이 예외를 던지면, 예외가 호출자에게 가기 전에 그 콜백의 등록을 되돌립니다. 이전에는 호출자가 토큰을 받지 못해 콜백이 해제할 방법 없이 구독된 채 남았고, 이후 변경마다 호출되었습니다. 예외는 여전히 호출자에게 전달되며, 이후의 변경 통지가 예외를 던지는 구독자를 격리하는 동작도 그대로입니다.
- Base: Inspector에서 `ObservableDictionary<TKey, TValue>`를 편집해도 항목이 덮어써지거나 초기화되지 않습니다. Inspector, 씬, JSON 어디에서 왔든 직렬화된 행 여러 개의 키가 같으면 이제 마지막 행이 아니라 첫 행이 쓰이므로, "+"로 추가한 행이나 새 행에 입력한 키가 기존 항목을 대신하지 않습니다. 그런 행과 오브젝트 키가 비어 있는 행은 직렬화를 거쳐도 목록에 남고, drawer는 키를 바꾸거나 행을 지울 때까지 목록 아래의 경고에 그 행을 적습니다. 키가 겹치는 행은 런타임 코드가 그 키를 지우면 함께 지워집니다. drawer가 목록에 늘어난 행을 더 이상 초기화하지 않으므로, 런타임 `Add`로 추가되거나 Undo로 되돌아오거나 요소 복제로 생긴 항목도 키와 값을 그대로 가지며, 이를 위해 두던 경로별 크기 캐시도 없어졌습니다. 이전에는 새 행이 모두 기본 키와 값으로 초기화되고 다음 직렬화가 딕셔너리에서 행을 다시 만들었기 때문에, 새 행이 기본 키의 항목을 대신한 뒤 사라졌습니다.
- Base: MVP owner를 닫으면 owner가 먼저 닫힘을 마치는 경우에도, 예를 들어 owner가 일반 `View`이고 자식이 `AnimationView`인 경우에도, owner의 view 아래에 host된 자식 창이 모두 unload됩니다. 이전에는 owner가 숨겨지면서 자식의 닫힘 코루틴이 멈춰 자식이 닫히는 중인 상태로 영원히 남았습니다. `OnWindowClosed`도 `OnWindowUnloaded`도 발생하지 않았고, `UnloadFunc()`가 실행되지 않았으며 풀로도 돌아가지 않았고, 풀에서 꺼낸 owner를 다시 열면 그 아래에 다시 나타났으며, 포커스를 계속 잡아 `CloseFocused()`가 아무 일도 하지 않을 수 있었습니다. 이제 숨겨져서 닫힘이 중간에 끊긴 view는 닫힌 것으로 보므로, 자식은 owner 다음 프레임에 unload됩니다. 아직 loading 중인 owner를 강제로 닫으면, 예를 들어 `LoadFunc()`에서 자식을 연 프레임에 `CloseAll(true)`를 호출하면, 이제 자식을 닫기 전에 owner를 먼저 표시하므로 자식도 다른 창처럼 닫힘을 진행합니다. 이전에는 숨겨진 owner 아래에서 Unity가 자식의 닫힘 코루틴 시작을 거부했습니다. 숨겨진 상태에서 닫히는 view, 예를 들어 owner가 아직 loading 중일 때 직접 닫은 자식은 닫힘 전환을 건너뛰고 바로 닫힌 상태가 됩니다.
- Base: `CloseAsync`는 토스트나 알림 대기열처럼 `OnWindowUnloaded` 핸들러가 같은 presenter 타입을 곧바로 다시 열어도, 자신이 닫은 창이 unload되면 완료됩니다. 풀은 그 핸들러에 같은 presenter 인스턴스를 돌려주는데, `CloseAsync`는 그 인스턴스가 매니저에서 빠지기를 기다렸기 때문에 대기열이 뒤이어 연 창들까지 기다렸고, 대기열이 계속 차 있으면 끝나지 않았습니다. `OpenAsync`도 이제 같은 방식으로 자신이 시작한 열림을 따라가므로, 한 번의 매니저 업데이트 안에서 해제되었다가 다시 열린 창 때문에 나중의 열림까지 기다리지 않습니다.
- Base: EditMode 테스트처럼 플레이 모드 밖에서 첫 창을 열어도 더 이상 `InvalidOperationException`이 발생하지 않습니다. `MVPManager`는 루트와 직접 만든 `EventSystem`에 대한 `DontDestroyOnLoad`를 Unity가 허용하는 플레이 모드에서만 호출합니다. 플레이 모드 밖의 나머지 동작은 그대로입니다. `Dispose`는 여전히 Unity가 플레이 모드 밖에서 거부하는 `Object.Destroy`로 파괴하고, view는 클래스에 `[ExecuteAlways]`가 있을 때만 그곳에서 `OnDisable`을 받습니다.
- Base: 씬에 `EventSystem`이 없을 때 `MVPManager`가 만드는 `EventSystem`이 Active Input Handling이 켜 둔 입력 백엔드의 입력 모듈을 받습니다. 다른 패키지의 의존성으로 Input System 패키지가 들어온 프로젝트처럼 "Input Manager (Old)"에서 패키지가 설치되어 있으면 `InputSystemUIInputModule`을 받았는데, 이 모듈은 Input System 백엔드가 꺼져 있으면 아무 입력도 읽지 못해 어떤 MVP view도 클릭할 수 없었습니다. 이제 그 조합에서는 `StandaloneInputModule`을 받습니다. 패키지가 있는 "Input System Package (New)"와 "Both"는 계속 `InputSystemUIInputModule`을, 패키지가 없는 프로젝트는 `StandaloneInputModule`을 받습니다. README에 네 가지 조합을 정리했습니다.
- Base: 열림 애니메이션 도중에 전환되어 숨겨진 탭 패널 뒤의 창처럼, 열리는 도중에 host나 owner가 숨겨진 창이 열림 상태에 도달합니다. view가 숨겨지면 열림 코루틴이 멈춰서 창이 열리는 중인 상태로 영원히 남았습니다. `OpenAsync`가 끝나지 않았고, 탭이 다시 보인 뒤에도 창 자신의 `CloseSelf()` 같은 강제가 아닌 `Close`가 거부되었으며, `AnimationView`는 숨겨진 프레임의 자세에 머물렀습니다. 이제 view가 숨겨질 때 열림이 끝납니다. 남은 전환은 건너뛰고, `OpenEnd()`가 실행되며, `AnimationView`는 열린 자세를 취하고, 창은 다음 매니저 업데이트에서 열린 것으로 처리됩니다. view가 `base.OnDisable()`을 호출하지 않고 `OnDisable`을 override하면 매니저가 이 열림을 직접 끝냅니다.
- Base: 숨겨지거나 비활성화된 `AnimationHandler`가 자세에 맞는 시간 계수를 유지합니다. 화면의 view는 그 자세에 머무르는데 계수만 닫힌 자세인 0으로 되돌려서, 열린 채로 숨겨졌다가 다시 보인 `AnimationView`가 닫힘 애니메이션 없이 바로 닫혔습니다. 또한 애니메이션 도중에 숨겨진 핸들러는 그 프레임의 자세에서 멈추던 것과 달리 이제 그 애니메이션의 끝 자세를 취합니다.
- Base: Addressables, Localization, Input System이 없는 프로젝트에서도 Template 샘플이 컴파일됩니다. 생성된 Addressables·Localization 키 스크립트, `ISCore`, 그리고 Addressables 씬을 불러오거나, Addressables 라벨을 읽거나, Input System 액션 에셋을 해제하거나, 입력 액션을 새로 고치는 스크립트가 해당 `TEMPLATE_*_SUPPORT` 심볼 밖에서 그 패키지들을 사용해서, 그런 프로젝트에 샘플을 가져오면 샘플의 모든 스크립트가 컴파일되지 않았습니다. 패키지가 하나라도 없으면 그 패키지가 필요한 샘플 부분은 패키지가 설치되지 않았다는 로그를 남깁니다.
- Base: Template 샘플의 키보드 단축키와 Audio3D 샘플의 마우스 클릭이 샘플에 새로 추가된 `SampleInput` 클래스를 통해 프로젝트가 켜 둔 입력 백엔드를 읽습니다. Input System 백엔드가 켜져 있으면 Input System을, 그렇지 않으면 Input Manager를 읽습니다. 이전에는 `UnityEngine.Input`을 호출해서 "Input System Package (New)"에서는 매 프레임 `InvalidOperationException`이 발생했습니다.
- Base, URP: Template·Attribute·ScreenEffect 샘플 씬의 버튼·토글·슬라이더가 Input System 패키지가 없을 때도, 패키지가 있고 "Input Manager (Old)"·"Input System Package (New)"·"Both" 중 어느 것일 때도 입력에 반응합니다. 이 씬들의 `EventSystem`에는 `InputSystemUIInputModule`이 있었는데, 패키지가 없으면 Missing Script가 되고 "Input Manager (Old)"에서는 아무 입력도 읽지 못했습니다. 이제 `EventSystem`에는 Input Manager를 읽는 `StandaloneInputModule`과 샘플에 새로 추가된 `SampleInputModuleSelector`가 있습니다. 이 컴포넌트는 패키지가 설치되어 있고 Active Input Handling이 "Input System Package (New)"나 "Both"이면 씬이 시작될 때 그 모듈을 `InputSystemUIInputModule`로 바꿉니다. `MVPManager`가 직접 만드는 `EventSystem`에 적용하는 규칙과 같습니다. 세 샘플은 이 컴포넌트를 각자 하나씩 가집니다. 이를 위해 Attribute 샘플의 스크립트는 프로젝트의 `Assembly-CSharp` 대신 자체 어셈블리 `Mu3Library.Sample.Attribute`로 컴파일되며, 이 어셈블리는 패키지가 설치되어 있는 동안 `ATTRIBUTE_INPUTSYSTEM_SUPPORT`를 정의합니다. `InputSystemManager`를 보여 주는 Template 샘플의 `Sample_IS` 씬은 자체 기능을 쓰려면 여전히 패키지와 "Input System Package (New)"나 "Both"가 필요하며, 그 UI는 모든 설정에서 반응합니다.
- Base, URP: Template·Attribute·ScreenEffect 샘플의 TextMesh Pro 텍스트가 TMP Essential Resources가 없는 프로젝트에서 만든 플레이어에서도 그려집니다. 모든 텍스트가 그 리소스의 LiberationSans SDF 폰트 에셋을 써서, 그런 프로젝트에서는 텍스트마다 `TMP_Settings`에서 `NullReferenceException`이 발생하고 아무것도 표시되지 않았습니다. TextMesh Pro가 런타임에 설정에서 읽는 것은 폰트만이 아닙니다. 자체 스타일 시트가 없는 텍스트는 그곳의 기본 스타일 시트를 읽고, 폰트 에셋에 없는 문자는 폴백 폰트를, 폴백 폰트 에셋에서 그린 문자는 머티리얼 프리셋 설정을, 줄바꿈되는 중국어·일본어·한국어 텍스트는 줄바꿈 규칙을 읽습니다. TextMesh Pro의 셰이더도 그 리소스에만 들어 있습니다. 그래서 각 샘플은 텍스트가 쓰는 것을 직접 가집니다. TextMesh Pro가 Liberation Sans로 만든 라틴 문자 250개짜리 정적 폰트 에셋 `Fonts/Sample Sans SDF.asset`, Normal 스타일이 든 `Fonts/Sample Style Sheet.asset`, 샘플 고유의 이름을 붙인 TextMesh Pro의 모바일 Distance Field 셰이더 `Shaders/TMP_SDF-Mobile.shader`이며, 모든 텍스트가 이 폰트 에셋과 그 머티리얼, 스타일 시트를 참조합니다. Template 샘플의 Localization 씬은 영어·일본어·한국어 문자열을 라틴 문자·한글·가나를 모두 담은 `NotoSansKR-Bold SDF` 하나로 그리고 줄바꿈하지 않으며, 이 동적 폰트 에셋은 더 이상 런타임에 폰트 기능을 읽지 않습니다. 각 샘플의 `Third-Party Notices.txt`에 라이선스를 적었습니다. 폰트와 그 폰트로 만든 폰트 에셋은 SIL Open Font License 1.1, 셰이더는 Unity Companion License입니다. Editor에서는 TMP Essential Resources를 가져오기 전까지 TextMesh Pro가 여전히 텍스트를 그리지 않으며, 가져오기를 제안합니다. 샘플 폰트에 없는 문자가 든 텍스트에도 그 리소스가 필요합니다.
- Base Editor: Observable drawer가 검사 중인 오브젝트에서 drawer의 필드를 읽는 대신, 선택된 오브젝트마다 프로퍼티 경로를 따라가 편집할 인스턴스를 찾습니다. `[Serializable]` 클래스 안, 배열이나 리스트의 요소, 다중 선택에 있는 Observable 필드도 이제 편집이 적용되고 구독자에게 통지됩니다. 이전에는 직렬화 클래스 안의 필드가 편집할 때마다 `ArgumentException`을 던졌고, 요소의 필드는 통지 없이 바뀌었으며, 다중 선택 편집은 첫 오브젝트에만 통지했습니다. 값 drawer는 편집을 먼저 적용한 뒤 값이 바뀐 인스턴스에 통지합니다.
- Base: Editor에서 Undo, Redo, 프리팹 되돌리기가 Observable의 값을 바꾸면, 작업 직후 메인 스레드에서 구독자에게 되돌아간 값을 통지합니다. Unity는 복원한 데이터를 Observable을 거치지 않고 살아 있는 오브젝트에 직접 쓰기 때문에, Attribute 샘플의 `ObservableFloat`을 표시하는 레이블 같은 구독자가 이전 값에 머물러 있었습니다. Undo와 Redo는 Inspector 편집이 일어나는 곳인 선택된 오브젝트의 Observable을 다루며, 선택된 GameObject의 컴포넌트도 포함합니다. 되돌리기는 되돌린 프리팹 인스턴스를 다룹니다. 숫자, bool, char, 문자열, 열거형, Unity 오브젝트 참조나 그런 필드로만 이루어진 구조체를 값으로 가진 Observable은 `Set`처럼 복원으로 값이 바뀌었을 때만 통지하고, 그런 키와 값을 가진 `ObservableDictionary<TKey, TValue>`는 인덱서와 같은 규칙으로 항목을 비교합니다. Unity는 리스트나 다른 객체를 그 자리에서 다시 채우므로 이전 내용과 비교할 수 없습니다. 그래서 `ObservableList<T>`나 그런 값을 가진 Observable과 딕셔너리는, 마지막 통지 뒤 Unity가 그 오브젝트를 역직렬화한 적이 있으면 그 오브젝트를 다루는 Undo, Redo, 되돌리기마다 내용이 같아도 통지합니다. 역직렬화 자체는 여전히 구독자를 호출하지 않으므로 씬을 불러오거나 프리팹을 인스턴스화해도 아무에게도 통지하지 않습니다. 이를 위해 `ObservableProperty<T>`가 `ISerializationCallbackReceiver`를 명시적으로 구현하며, 그 콜백은 복원을 기록하기만 합니다. `ISerializationCallbackReceiver`를 직접 구현하는 파생 클래스는 이 콜백을 대체하므로 복원이 통지되지 않습니다.
- Base: `PlaySfx`나 `PlayEnvironment`가 풀에서 꺼내거나 가장 오래 재생 중인 사운드에서 넘겨받은 SFX·환경음 소스가 최대 볼륨으로 재생됩니다. `FadeOutSfxAll`, `FadeOutFirstSfx` 또는 그 환경음 버전이 풀로 돌려보낸 소스는 페이드아웃이 끝난 무음 상태를 그대로 가지고 있어, 그 뒤 그 소스로 재생한 사운드가 들리지 않았습니다. `FadeInSfx`와 `FadeInEnvironment`는 여전히 무음에서 시작합니다.
- Base: 길이가 0초 이하인 오디오 페이드가 즉시 적용됩니다. `TransitionBgm(clip, 0f)`, `FadeInSfx(clip, 0f)`, `FadeInEnvironment(clip, 0f)`는 무음으로 남던 것과 달리 최대 볼륨으로 재생되고, `FadeOutBgm(0f)` 같은 0초 페이드아웃은 소스를 일시정지하거나 정지하거나 풀로 돌려보내기 전에 무음으로 만듭니다.
- Base: `PlayBgm`, `PlayBgmForce`, `TransitionBgm`과 그 `WithKey`·비동기 버전은 `StopBgm`이 이미 그랬듯 실행 중인 BGM 플레이리스트를 끝냅니다. 이전에는 플레이리스트가 계속 돌아, 두 트랙 사이 간격이 끝나면 호출한 쪽이 막 시작한 트랙을 다음 트랙으로 바꿨고, 정해진 횟수만 재생하도록 요청한 트랙이 끝나면 플레이리스트가 다시 이어졌습니다. 플레이리스트는 여전히 자신의 트랙을 재생하며, 플레이리스트가 재생 중인 클립으로 `PlayBgm`을 호출하면 플레이리스트는 각 트랙을 한 번만 재생하므로 그 클립을 요청한 설정으로 다시 시작합니다. 플레이리스트 밖에서 페이드아웃 중인 클립으로 `PlayBgm`을 호출하면(예: `FadeOutBgm` 직후) 이제 페이드를 취소하고 그 클립을 최대 볼륨으로 계속 재생합니다. 이전에는 호출이 무시되고 페이드가 클립을 일시정지했습니다.
- Base: `HostOptions.Host`가 비활성인 경우처럼 숨겨진 host 아래에서 연 창이 열림 상태에 도달합니다. Unity가 열림 코루틴의 시작을 거부하고 오류를 남겨, 창이 열리는 중인 상태로 영원히 남았습니다. `OpenAsync`가 끝나지 않았고, 강제가 아닌 `Close`는 무시되었으며, 그 창이 포커스를 잡은 채 `CloseFocused()`가 아무 일도 하지 않을 수 있었습니다. 이제 열림 전환의 대기를 건너뜁니다. `OpenStart()`와 `OpenEnd()`가 연달아 실행되고, `AnimationView`는 곧바로 열린 자세를 취하며, host가 보이면 창이 완전히 열린 모습으로 나타납니다. host가 숨겨진 채로 닫히면 `AnimationView`는 같은 방식으로 닫힌 자세를 취하므로, 다음 열림에서 다시 애니메이션이 재생됩니다.
- Base: owner가 먼저 닫힘을 마쳐도, 그 아래에 host된 자식 창은 자식의 view가 `base.OnDisable()`을 호출하지 않고 `OnDisable`을 override했더라도 unload됩니다. view가 숨겨지면 닫힘이 중간에 끊기는데, 이제 매니저가 그런 닫힘을 다음 업데이트에서 직접 끝내므로 위에서 설명한 unload가 그 호출에 기대지 않습니다.
- Base: EditMode 테스트처럼 플레이 모드 밖에서 `AudioManager`의 루트를 만들어도 더 이상 `InvalidOperationException`이 발생하지 않습니다. `MVPManager`처럼 `DontDestroyOnLoad`를 플레이 모드에서만 호출합니다.
- Base: UniTask가 설치되어 있으면 `GetResultAsync`, `PostResultAsync`, `GetDownloadSizeResultAsync`가 401, 404, 500 같은 HTTP 오류 응답을 콜백 API처럼 그 응답의 `StatusCode`와 `ResponseHeaders`로 답합니다. 그래서 호출하는 쪽이 만료된 로그인과 없는 리소스를 구분하고 `Retry-After` 헤더를 읽을 수 있습니다. UniTask는 이런 응답에서 예외를 던지는데, 매니저는 이를 예기치 못한 실패로 처리해 상태 코드 -1, 헤더 없음, 로그의 예외로 답했습니다. 연결이 거부된 경우처럼 응답을 받지 못한 요청은 역시 콜백 API처럼 상태 코드 0과 헤더 없음으로 답하며, 두 경우 모두 이제 실패한 요청을 오류로 기록합니다. 재시도와 취소 동작은 그대로입니다.
- Base: 시작한 뒤 실패한 Addressables 씬 로드를 추가됨에 설명한 `OnSceneLoadFailed`로 알립니다. 이전에는 로더가 로그만 남겼기 때문에, 로드 시작 이벤트에서 띄우고 로드 완료 이벤트에서 내리는 로딩 화면이 영원히 남았습니다.
- Base: `UpdateCatalogs`와 `UpdateCatalogsAsync`가 자신의 `autoReleaseHandle`을 Addressables에 핸들 해제 여부로 넘깁니다. 이 값이 번들 캐시 정리 매개변수로 들어가서, 기본 호출이 아무도 요청하지 않은 캐시 번들을 지웠고, 캐시를 정리할 수 없는 환경에서는 성공한 갱신이 null로 답하고 알려진 키를 갱신하지 않았습니다. `autoReleaseHandle: false`에서는 핸들이 두 번 해제되었습니다. 이제 번들 캐시는 추가됨에 설명한 오버로드를 통해서만 정리됩니다. `CheckForCatalogUpdatesAsync`와 `UpdateCatalogsAsync`는 결과를 완료 콜백 안에서 받은 뒤 이전처럼 플레이어 루프에서 재개합니다. 이전에는 Addressables가 핸들을 해제한 뒤 한 프레임 늦게 핸들을 읽어, 원격 카탈로그를 대상으로 할 때처럼 확인이나 갱신이 비동기로 끝날 때마다 "Attempting to use an invalid operation handle"을 던졌고, 그래서 성공한 갱신도 알려진 키를 갱신하지 않았습니다.
- Base: `autoReleaseHandle: true`로 부른 `CheckForCatalogUpdates`, `UpdateCatalogs`와 그 UniTask 변형은 Addressables가 돌려준 핸들을 Addressables가 해제하지 않을 때 직접 해제합니다. Addressables는 카탈로그 작업 자체만 자동 해제합니다. 먼저 기다릴 것이 있을 때, 즉 자신의 초기화나, 아직 아는 카탈로그 갱신이 없는 갱신에서 스스로 시작하는 확인을 기다릴 때는 그 작업을 감싼 체인 작업을 돌려주고, 이 체인은 해제하지 않습니다. Addressables 초기화가 끝나기 전의 모든 확인과 갱신, 그리고 앞선 확인이 갱신을 찾지 못한 모든 갱신(기본 `UpdateCatalogs` 호출 포함)에서 그랬고, 체인은 자신이 잡은 작업들과 함께 메모리에 남았습니다. 이제 매니저는 콜백이 끝난 뒤 다음 ResourceManager 업데이트에서, 즉 Addressables가 자동 해제할 것을 해제한 뒤에 핸들을 다시 보고 아직 유효하면 해제하므로, 모든 핸들이 한 번씩 해제됩니다. Addressables 초기화가 끝나기 전에 부른 갱신에서는 Addressables가 그 갱신을 위해 시작하는 확인이 체인 안에 여전히 남고, 매니저는 여기에 닿을 수 없습니다. 이를 피하려면 매니저를 먼저 초기화하세요.
- Base: `ChangeLocaleAsync`는 로케일 변경이 끝난 뒤에 완료됩니다. 즉 Localization 패키지가 테이블 프리로드를 포함해 새 로케일로 다시 초기화를 마치고 그 로케일에 대해 `OnLocaleChanged`를 발생시킨 뒤입니다. `IsLocaleChanging`은 그때까지 true이며, `CancelChangeLocale()`은 선택은 그대로 둔 채 대기를 끝냅니다. 이전에는 패키지가 곧바로 완료해 버리는 선택 로케일 핸들을 기다렸기 때문에, 테이블이 아직 로드 중이고 `OnLocaleChanged`도 발생하기 전인 호출 프레임에 완료되었습니다. 이미 선택된 로케일을 선택하면 아무것도 다시 시작하지 않으므로 호출은 곧바로 완료됩니다. 다만 첫 호출이 아직 기다리는 로케일로 두 번째 호출을 한 경우처럼 초기화나 그 로케일의 알림이 아직 진행 중이면 그것까지 기다립니다.
- Base: `InputSystemManager.StartInteractiveRebind`는 리바인딩이 완료되거나 취소되거나 시작하지 못하면 액션의 활성 상태를 리바인딩 전으로 되돌립니다. 꺼진 에셋 안의 액션을 리바인딩하면 그 액션 하나만 켜진 채로 남았습니다. "2D Vector" 같은 컴포지트를 가리키는 바인딩 인덱스는 범위를 벗어난 인덱스처럼 액션을 건드리기 전에 걸러지고 오류 로그와 null로 답합니다. 이전에는 Input System의 예외가 밖으로 나가고 액션은 꺼진 채로 남았습니다.
- Base: `EaseInOutQuadD`나 `GetEasingFunctionDerivative(Ease.EaseInOutCubic)`처럼 `EaseInOut` 이징 함수의 도함수가 그 이징 함수의 기울기를 돌려줍니다. 각 `EaseInOut` 함수는 앞뒤 절반을 두 배로 늘린 시간 위에서 계산하는데, `EaseInOutSineD`를 뺀 모든 도함수가 이 계수 2를 빠뜨려 거기서 구한 속도가 실제의 절반이었습니다. `EaseInOutElasticD`는 늘어난 시간도 반영하지 않고 위상도 잘못 써서 곡선을 전혀 따르지 않는 값을 냈고, `SpringD`는 한 항을 다섯 배 크게 뺐습니다. 이제 EditMode 테스트가 모든 `EasingFunction.Ease` 종류를 그 이징 함수의 중앙 차분 도함수와 비교합니다. 절반이 된 속도를 메우려고 이 값을 두 배로 만들던 코드는 그 보정을 없애야 합니다.
- Base Editor: `ScriptBuilder`가 1000줄 뒤에서 출력을 멈추지 않습니다. Resources 에셋이 450개쯤 넘는 Resources 경로 스크립트나 액션이 160개쯤 넘는 Input System 이름 스크립트는 클래스 중간에서 끝난 채 "generated" 로그와 함께 그대로 저장되었고 CS1513으로 컴파일에 실패했습니다. 이제 출력 길이에는 제한이 없고, 자기 자신을 담은 블록을 막기 위해 `MaxNestingDepth`보다 깊게 중첩된 코드 블록은 빌드하지 않습니다. 이 때문에, 또는 콘텐츠 항목이 null이거나 예전에는 경고만 남기고 건너뛰던 지원하지 않는 타입이어서 블록을 끝까지 만들 수 없으면, `Build`는 스크립트의 일부를 돌려주는 대신 `InvalidOperationException`을 던집니다. `Build`를 직접 호출하는 코드는 이 예외를 잡거나 `TryBuild`를 쓰면 됩니다. Resources 경로·Input System 익스포터와 MVP Helper는 `TryBuild`를 써서 오류를 기록하고 파일을 쓰지 않습니다. `AppendStackCountMax`는 더 이상 효과가 없어 obsolete로 표시되었고, `Build`는 줄 수를 로그로 남기지 않습니다.
- Base Editor: MVP Helper가 기본 설정으로도 컴파일되는 스크립트를 만듭니다. 네임스페이스를 비워 두면 이름 없는 `namespace`를 쓰는 대신 네임스페이스 블록 없이 네 스크립트를 쓰고, 기존 타입도 한 번도 찾지 못하던 `.{이름}` 대신 전역 네임스페이스에서 찾습니다. `My Popup`처럼 유효한 C# 식별자가 아닌 스크립트 이름이나 네임스페이스는 **Create MVP Components**를 비활성화하고 그 이유를 보여 주며, 앞뒤 공백은 무시합니다. 이미 있는 스크립트 파일을 바꾸기 전에는 확인을 받고, 덮어쓰기를 취소하면 스크립트를 하나도 쓰지 않습니다. UtilWindow 샘플의 `MVPHelper.asset`은 저장 폴더, 스크립트 이름, 네임스페이스가 비어 있고 **Ignore Type Overlap**이 꺼진 상태로 시작합니다. 이전에는 Template 샘플의 `LinkParent` 스크립트를 가리키고 있어, 두 샘플을 모두 임포트한 프로젝트에서는 클릭 한 번에 그 스크립트가 빈 뼈대로 바뀌었습니다.
- Base Editor: Resources Path, InputSystem Data, Addressable Group Data, Localization Data 익스포터가 MVP Helper처럼 네임스페이스를 검사합니다. `My Game`, `Game.`, `1st`, `class`처럼 C# 식별자를 `.`으로 이은 형태가 아닌 네임스페이스를 넣으면, 익스포터의 다른 입력 검사와 같이 생성 버튼 대신 그 이유를 담은 경고가 표시되며, 앞뒤 공백은 무시합니다. 이전에는 검사 없이 `namespace`에 그대로 써서, 저장된 스크립트가 그 어셈블리에서 컴파일되지 않았습니다. 빈 네임스페이스는 지금처럼 네임스페이스 블록 없이 스크립트를 생성합니다.
- Base Editor: `ButtonInvoke` 버튼을 그리는 fallback Inspector가 `CreateInspectorGUI`를 통해 UI Toolkit으로 UI를 만듭니다. Unity의 기본 Inspector를 그린 뒤 그 아래에 버튼을 붙입니다. 이 Inspector는 `ButtonInvoke` 사용 여부와 관계없이 자체 Custom Inspector가 없는 모든 `MonoBehaviour`와 `ScriptableObject`를 그리는데, 이를 IMGUI로 그렸기 때문에 Base를 설치하면 다른 패키지의 UI Toolkit drawer처럼 `CreatePropertyGUI`만 구현한 property drawer를 쓰는 필드가 그런 모든 Inspector에서 "No GUI Implemented"로 표시되었습니다. 이제 그런 drawer가 그려지고, IMGUI drawer는 Unity 자체 기본 Inspector에서처럼 그 안에서 계속 동작하며, 다중 오브젝트 편집과 버튼도 이전처럼 동작합니다. `ButtonInvokeEditorBase`를 상속해 `OnInspectorGUI`를 재정의한 에디터는 그때 `CreateInspectorGUI`가 null을 돌려주므로 계속 그 메서드로 그려집니다. `OnInspectorGUI`를 직접 호출하는 도구에는 여전히 IMGUI 버전이 그려집니다. README에 fallback Inspector 설명이 있습니다.
- URP: ScreenEffect 샘플의 `ScreenEffectCore.OnDestroy`가 자신의 정리 뒤에 `base.OnDestroy()`를 호출하므로, 파괴된 코어가 `CoreRoot`에서 빠지고 서비스를 해제합니다. 이전에는 같은 세션에서 씬을 다시 불러오면 "Core is already exist"가 기록되고 새 코어가 초기화되지 않았습니다.
- URP: Grayscale, GaussianBlur, DepthOutline, Shake 네 가지 스크린 이펙트가 플레이어 빌드에서 동작합니다. 패키지에 새로 추가된 `Runtime/Resources/Mu3Library.URP/ScreenEffectShaders.asset`이 이 이펙트들의 셰이더를 참조하고, 각 이펙트는 이 에셋에서 셰이더를 가져옵니다. 이전에는 이펙트가 `Shader.Find`로 이름만 가지고 셰이더를 찾았고 빌드에 포함된 어떤 것도 그 셰이더를 참조하지 않았기 때문에, 프로젝트가 셰이더를 Always Included Shaders에 직접 추가하지 않는 한 플레이어에서는 셰이더가 빠져 모든 이펙트가 아무것도 그리지 않았습니다. 이제 프로젝트에서 따로 설정할 것이 없으며, 이미 그 목록에 넣어 둔 셰이더도 그대로 동작합니다. 에셋이 Resources 폴더에 있으므로 패키지를 설치한 프로젝트의 플레이어 빌드에는 항상 네 셰이더가 들어갑니다. 프로젝트가 자체 셰이더를 쓰려고 `ScreenPassBase`에서 파생한 패스는 여전히 이름으로 셰이더를 찾으므로, 그 셰이더는 프로젝트가 빌드에 포함해야 합니다.
- URP: 셰이더를 찾지 못한 스크린 이펙트는 플레이 세션마다 셰이더 이름 하나당 오류를 한 번만 기록하고, 그 패스는 셰이더를 더 찾지 않습니다. 이전에는 패스가 머티리얼을 요청할 때마다 다시 찾고 다시 기록했는데, 활성화된 모든 이펙트가 매 프레임 카메라마다 머티리얼을 요청했습니다. 오류 메시지는 셰이더를 빌드에 포함하는 방법을 알려 줍니다. 프로젝트가 나중에 불러오는 셰이더(예: AssetBundle에서)를 쓰려고 파생한 패스는 그 셰이더를 불러온 뒤에 만들어야 합니다.
- Watermelon Game: 게임 도중 일시정지 메뉴나 화면 전환 때문에 보드 계층을 숨겨도 모든 아이템이 그대로 남습니다. `BoardItem.OnDisable`이 아이템을 아이템 풀에 넣을 때처럼 초기화했기 때문에, 계층을 다시 보이면 보드 위의 모든 아이템과 플레이어가 들고 있는 아이템이 인덱스, 스프라이트, 콜라이더, 물리 상태를 잃었습니다. 이 아이템들은 움직이지도, 병합되지도, 게임을 끝내지도, 스냅샷에 들어가지도 않는 보이지 않는 장애물로 보드에 남았고, `SetBoardConfig`는 다음 `Prepare`까지 모든 설정을 거부했습니다. 이제 `OnDisable`은 아이템을 건드리지 않습니다. 메서드는 `protected virtual`로 남아 있으므로 `base.OnDisable()`을 호출하는 재정의도 계속 컴파일됩니다. 아이템 풀로 돌아가는 아이템은 보드가 풀에 넣기 전에 직접 초기화하므로 지금처럼 초기화됩니다.
- Watermelon Game: `OnItemMerged`나 `OnScoreChanged` 핸들러에서 `GameEnd()`나 `Prepare()`를 호출하면, 보드는 그 프레임에 아이템을 더 내주지 않습니다. 이전에는 이 이벤트를 발생시키는 커맨드 뒤에도 `Update`가 계속 진행되어, `OnGameEnded` 뒤나 `GameStart()` 전에 플레이어 손에 새 아이템을 쥐여 줄 수 있었고, 이어지는 드래그가 실행 중이 아닌 보드에 그 아이템을 떨어뜨려 `SpawnCount`를 올리고 드롭 사운드를 재생하고 `OnItemDropped`를 발생시켰습니다. `OnDragging`과 `OnDragEnd`도 실행 중이 아닌 보드에서는 들고 있는 아이템을 그대로 둡니다.
- Watermelon Game: 샘플의 `BoardConfig.asset`이 저장소에 한 번도 들어온 적 없는 오디오 클립 9개를 더 이상 참조하지 않습니다. BGM 트랙, 게임 시작·게임 종료·드롭 사운드, 병합 사운드 5개입니다. 임포트한 샘플에서는 이것들이 "Missing (Audio Clip)"으로 표시되었습니다. 이제 슬롯이 비어 있으므로 샘플 보드는 이전처럼 소리를 내지 않고, 샘플의 에셋과 씬에 있는 모든 참조가 해석됩니다.
- Watermelon Game: 처음 11개 아이템 항목 가운데 스프라이트가 없는 항목이 있는 설정을 `SetBoardConfig`와 `Prepare`가 거부하고, 오류에 그 항목들의 인덱스를 적습니다. 이전에는 그런 항목이 통과했고, 그 항목으로 만든 아이템은 모두 스케일 0으로, 보이지 않고 닿을 콜라이더도 없는 채로 아무 로그 없이 만들어졌습니다. 에셋을 편집하는 동안에는 `BoardConfig`가 같은 인덱스를 적은 경고를 남깁니다.
- Watermelon Game: 게임 도중 `SetBoardConfig`로 보드 스프라이트의 크기가 다른 설정을 적용해도 보드 위의 모든 아이템이 보드 기준으로 같은 자리에 남습니다. 보드는 스프라이트 크기와 관계없이 같은 뷰포트에 맞춰지므로 아이템이 매달린 보드 트랜스폼의 스케일이 바뀌는데, 아이템은 로컬 위치를 그대로 유지했습니다. 그래서 더 작은 스프라이트는 아이템을 벽 밖으로 밀어내 끝없이 떨어지게 했고, 더 큰 스프라이트는 아이템을 가운데로 몰았습니다. 이전에는 들고 있는 아이템만 다시 배치했습니다. 위쪽 가장자리 위에 쌓인 아이템도 그 위의 자리를 지키고, 크기가 같은 스프라이트로 바꾸면 모든 아이템이 제자리에 그대로 있습니다.
- Watermelon Game: `Prepare` 뒤에는, 스냅샷 복원을 포함해, 다음 아이템이 보드의 현재 스폰 횟수에 해당하는 단계에서 나옵니다. `BoardItemSpawnRule`은 단계의 첫 횟수일 때나 가방이 비었을 때만 스폰 가방을 다시 채웠기 때문에, 더 뒤의 횟수로 복원한 보드는 첫 단계의 가방에서 계속 뽑았고, 더 앞의 횟수로 복원한 보드는 그 단계에 없는 큰 아이템을 뽑았습니다. 이제 가방을 채운 단계와 다른 단계에 속하는 횟수가 오면 그 단계의 가방을 다시 채웁니다.
- Watermelon Game: `Prepare`가 점수를 바꾸면 `OnScoreChanged`를 발생시킵니다. 보드를 새로 시작할 때는 0으로, 스냅샷을 복원할 때는 스냅샷의 점수로 발생시킵니다. 이 이벤트는 보드 준비가 끝난 뒤, `OnBoardPrepared` 바로 앞에 발생합니다. 이 이벤트를 따르는 점수 표시는 이전에는 첫 병합 전까지 이전 게임의 점수를 보여 주었습니다.
- Watermelon Game: `InputHandler`는 Active Input Handling이 켜는 입력 백엔드를 읽습니다. 이전에는 `UnityEngine.Input`만 읽었는데, 이것은 "Input System Package (New)"에서 입력을 받지 못하므로 그 설정에서는 보드에서 아무것도 드래그하거나 떨어뜨릴 수 없었습니다. 이제 Input System 패키지가 설치되어 있고 그 백엔드가 켜져 있으면 `Mouse.current`와 EnhancedTouch API의 터치를 읽고, 터치를 읽는 동안 `EnhancedTouchSupport.Enable()` 호출 하나를 쥐고 있다가 `OnDisable`에서 돌려줍니다. 그 밖의 경우에는 Input Manager를 읽습니다. 패키지는 여전히 Input System에 의존하지 않습니다. 어셈블리는 `com.unity.inputsystem`이 설치되어 있는 동안에만 `MU3LIBRARY_INPUTSYSTEM_SUPPORT`를 정의하고 `Unity.InputSystem`을 참조합니다. 패키지 없이 "Input System Package (New)"를 쓰면 핸들러가 읽을 수 있는 입력이 없으므로 경고를 한 번 남깁니다. 지원하는 설정은 패키지 README에 있습니다.
- Watermelon Game: `InputHandler`를 끄거나, 따라가던 터치가 사라지거나 시스템이 그 터치를 취소하거나, 마우스 연결이 끊겨 놓기 전에 빼앗긴 드래그는 취소되고, 들고 있는 아이템은 드래그가 마지막으로 놓은 자리에서 그대로 손에 남습니다. 이전에는 핸들러를 끄거나 터치가 사라지면 화면 위치 (0, 0)에서 놓은 것으로 알렸기 때문에, 아이템이 보드 왼쪽 끝으로 순간 이동해 플레이어의 드롭으로 떨어지고 `SpawnCount`에 세어졌습니다. 시스템이 취소한 터치와 연결이 끊긴 마우스도 놓은 것으로 처리되어 아이템을 떨어뜨렸습니다. `OnTouchEnded`로 드래그를 따라가는 리스너는 추가됨 항목에 설명한 `OnTouchCanceled`도 구독해야 합니다.
- Watermelon Game: 코드에서 `AddComponent<BoardArea>()`로 추가한 `BoardArea`가 예외 없이 활성화되고 터치를 전달합니다. 이때 추가되는 `InputHandler`는 직렬화된 씬 데이터만 채워 주던 `OnTouchBegan`, `OnTouchMoved`, `OnTouchEnded`를 null로 두었기 때문에, `BoardArea.OnEnable`이 `NullReferenceException`을 던지고 보드가 터치를 받지 못했습니다.

## [base/0.26.0] - 2026-08-17

### 추가됨
- Base 패키지에 새 선택적 어셈블리 `Mu3Library.Notifications`가 생겼습니다. `INotificationManager`/`NotificationManager`가 Mobile Notifications 패키지의 통합 `NotificationCenter`를 감쌉니다 — Android 채널을 포함한 초기화, 권한 요청, 시각·지연 예약, 취소, 배지 초기화, 시스템 설정 열기, 앱을 연 알림 조회까지. `INotificationManagerEventBus.OnNotificationReceived`는 앱 실행 중 도착한 알림을 알립니다. 이 어셈블리는 Android·iOS·에디터에서만, 그리고 `com.unity.mobile.notifications`가 설치된 동안에만(`MU3LIBRARY_NOTIFICATIONS_SUPPORT`) 컴파일되므로 패키지나 플랫폼이 없는 프로젝트는 아무 비용도 지불하지 않습니다. 패키지는 yield 방식인 권한·조회 작업에 대해 이벤트를 밀어주지 않으므로, 매니저는 각 작업을 그 작업이 살아있는 동안에만 존재하는 짧은 async 루프로 지켜봅니다 — 대기 중인 것이 없으면 아무것도 폴링하지 않고, 프레임 단위 구동자 없이 어떤 방식으로 호스팅되어도 동일하게 동작합니다. UniTask가 설치되어 있으면 `RequestPermissionAsync`와 `GetLastRespondedNotificationAsync`로 같은 답을 await할 수 있습니다. Template 샘플에 `NotificationCore`가 추가되었습니다.

## [base/0.25.0] - 2026-08-17

### 추가됨
- `Mu3Logger`(Foundation): 순수 C# 계층이 등록된 sink를 통해 로그를 남깁니다. Base 런타임이 시작 시 Unity 콘솔 sink를 등록하고, Foundation 이벤트 클래스들은 그동안 삼켜 왔던 disposed 핸들러와 잘못된 id 상황을 이제 보고합니다.
- `AudioManager`: `SetMixerGroups`가 카테고리별 소스를 `AudioMixerGroup`으로 라우팅합니다. 볼륨 계산은 그대로 `AudioSource.volume`에 남습니다. `SaveVolumes`/`LoadVolumes`는 네 카테고리 볼륨을 `IPlayerPrefsLoader`로 저장·복원하고, `DuckBgm`은 보이스나 큰 SFX 아래로 음악을 잠시 낮추며, `FadeInSfx`/`FadeInEnvironment`는 설정과 위치를 받습니다. UniTask API(`FadeInBgmAsync`, `FadeOutBgmAsync`, `TransitionBgmAsync`, `DuckBgmAsync`)는 페이드가 실제로 끝났을 때 완료되고, 더 새로운 페이드로 대체되면 취소로 완료됩니다.
- `ILocalizationManager`: `GetAsset<T>(tableName, key, callback)`과 `GetAssetAsync<T>(tableName, key)`가 현재 로케일의 AssetTable 에셋 — 언어에 따라 바뀌는 폰트나 스프라이트 — 을 불러옵니다. `GetString`이 StringDatabase를 감싸는 것과 같은 방식으로 Localization 패키지 자체의 AssetDatabase를 그대로 사용합니다.
- `IMVPManager`: `OpenAsync`/`CloseAsync`가 창이 열림을 끝냈거나 매니저에서 완전히 빠져나갔을 때 완료되므로, 호출부가 창 이벤트를 이어 붙이는 대신 애니메이션을 await합니다.
- `IWebRequestManager`: PUT, PATCH, DELETE 메서드가 추가되었습니다. 콜백 API에 지수 백오프 재시도, 진행 중인 요청을 중단하는 `WebRequestCancellation`, 다운로드 진행률을 알리는 `onDownloadProgress` 콜백이 생겼습니다.
- `IPlayerPrefsLoader`: bool, enum(이름으로 저장), JSON 객체 항목과 각각의 기본값을 지원합니다.
- `IInputSystemManagerEventBus`: 에셋 추가/제거와 리바인드 시작/완료/취소 이벤트가 생겼습니다.
- `GameObjectPool`: `MaxSize`는 가득 찬 풀이 담지 못하는 오브젝트를 파괴하고, `Prewarm`은 풀을 미리 채우며, 반환 훅이 모든 enqueue에서 실행되고, `Count`가 대기 중인 오브젝트 수를 알려줍니다.
- 에디터: `DIValidatorDrawer`는 생성자나 필수 `[Inject]` 멤버를 해석할 수 없는 등록을 아무것도 resolve하지 않은 채 보고하고, `RuntimeDiagnosticsDrawer`는 관리 중인 MVP presenter와 Addressables 캐시를 보여주며, 패키지 업데이트 메뉴가 WatermelonGame 패키지를 포함합니다.
- 진단: `Container.GetAllDescriptors`/`IsRegistered`/`GetActiveSingletonInstances`, `CoreBase`의 대응 API, `CoreRoot.RegisteredCores`, `MVPManager.GetPresenterDiagnostics`, Addressables 캐시 카운터가 새 드로어를 뒷받침합니다.
- 테스트: DI 컨테이너, Foundation 이벤트 클래스, Observable, `GameObjectPool`, `PlayerPrefsLoader`의 EditMode 테스트가 추가되었고 BuiltIn 프로젝트에 testable로 등록되었습니다.
- Base 패키지에 패키지 단위 `README.md`/`CHANGELOG.md`가 생겼습니다.

### 변경됨
- `Container.CreateScope()`가 internal이 되었습니다. 코어는 수명 전체 동안 정확히 하나의 스코프를 소유합니다. 두 번째 스코프는 싱글턴의 `Initialize()`를 두 번 실행하거나 컨테이너가 계속 돌려주는 인스턴스를 dispose할 수 있었습니다. **Breaking**: 스코프를 직접 만들던 코드는 `CoreBase`를 거쳐야 합니다.
- `AudioController`: 유한 루프가 normalized time 0.97 대신 `AudioSource.isPlaying`이 꺼지기를 기다립니다. 원샷 SFX와 플레이리스트 트랙이 꼬리를 잃지 않고 마지막 샘플까지 재생됩니다.
- `AudioManager`: 카테고리 볼륨 setter가 0..1로 클램프합니다. BGM 플레이리스트는 null 클립을 미리 걸러내고, 재생할 것이 남지 않은 리스트는 거부합니다. 기존에는 무한 재귀에 빠졌습니다.
- `AddressablesManager`: 에셋 캐시 키가 요청 타입을 함께 담습니다. 하나의 주소를 두 타입으로 불러오면 서로의 핸들을 해제하는 대신 두 항목을 각각 유지합니다. `ReleaseCachedAsset`은 그 키가 만든 모든 것 — 단일, 리스트, 키 딕셔너리 항목 — 을 함께 해제합니다. `IsKeyExist`와 `GetCachedAddress`는 계속 base key로 답하고, 카탈로그 업데이트가 알려진 키 집합을 갱신하며, 실패한 의존성 다운로드도 완료를 보고해서 호출부가 영원히 기다리지 않습니다.
- `InputSystemManager`: 같은 id로 에셋을 교체하면 떠나는 에셋을 비활성화하고, 매니저가 JSON으로 직접 만든 에셋은 떠날 때 파괴되며, `Dispose`가 매니저 소유물을 정리합니다.
- `Container`: 같은 구현을 다른 라이프타임으로 다시 등록하면 조용히 버려지는 대신 경고와 함께 새 등록이 이깁니다.
- Observable이 구독자를 하나씩 호출합니다. 예외를 던진 구독자는 보고되고, 나머지 구독자의 알림을 막지 않습니다.

### 수정됨
- `CoreRoot`: 파괴된 중복 코어가 살아남은 같은 타입의 코어를 레지스트리에서 끌어내리지 않습니다. 등록된 인스턴스만 자신의 타입을 뺄 수 있고, 중복 코어는 여전히 자기 스코프를 정리합니다.
- `SceneLoader`: additive 업데이트 패스가 스냅샷을 순회합니다. 씬 콜백이 다른 씬 작업을 시작하거나 멈춰도 collection-modified 예외로 패스가 깨지지 않습니다. 실패한 Addressables 씬 로드는 핸들을 누수 대신 해제합니다.
- `CoreBase`: 컨테이너가 만들어지기 전의 `GetClass`/`RegisterClass`는 예외 대신 null을 답하거나 보고하고, `Start` 시점에 비활성이던 코어는 활성화될 때 준비를 다시 시도합니다.
- `Singleton`/`GenericSingleton`: 파괴된 중복 인스턴스가 살아남은 인스턴스의 static 참조를 지우지 않고, 앱 종료 중의 접근이 유령 오브젝트를 다시 만들지 않습니다.
- `ResourceLoader`: 같은 경로의 동시 비동기 로드가 요청 하나를 공유하고, 같은 에셋의 재캐싱은 더 이상 경고하지 않습니다.
- `OutPanel.CanvasGroup`이 첫 접근 전에는 null이던 backing field 대신 lazy 프로퍼티로 답합니다.
- `AudioManager`: 오디오 컨트롤러 생성 에러가 항상 소스 탓을 하는 대신 빠진 쪽 — 소스 또는 클립 — 을 지목합니다.

## [urp/0.3.0] - 2026-08-17

### 추가됨
- URP 패키지에 패키지 단위 `README.md`/`CHANGELOG.md`가 생겼습니다.

### 변경됨
- `ScreenEffectManager`가 `IDisposable`을 구현합니다. dispose 시 영원히 붙어 있는 대신 `RenderPipelineManager.beginCameraRendering`에서 스스로 떨어집니다. 이펙트 자체는 소유자에게 남습니다.

### 수정됨
- `ScreenEffectManager.UnregisterEffectAll<TEffect>()`가 다시 이펙트를 제거합니다. 이펙트 타입을 렌더 패스의 이름인 `PassType`과 비교했기 때문에 절대 일치할 수 없었고, 조용히 아무것도 제거하지 못했습니다.

## [game/watermelon/0.6.0] - 2026-08-17

### 추가됨
- `InputHandler`: 마우스가 자신에게 예약된 finger id로 터치와 같은 드래그 파이프라인을 움직입니다. 터치 스크린이 없는 에디터와 데스크톱에서도 게임이 플레이되며, 터치만 받는 프로젝트는 이전과 같이 동작합니다.
- 테스트: `BoardCommandRunner`의 EditMode 테스트가 추가되었고, Base 테스트와 함께 WatermelonGame 프로젝트에 testable로 등록되었습니다.

## [base/0.24.1] - 2026-08-16

### 변경됨
- `ScreenChangeNotifier`: 화면은 게임이 도는 동안에만 따라갑니다. 게임이 화면을 읽기 전까지 `ScreenSize`와 `SafeArea`는 비어 있고, edit mode에서는 `OnChanged`가 발생하지 않습니다.
- `SafeRect`: 안쪽으로 물러날 safe area가 없는 화면에서는 rect가 부모를 통째로 덮습니다. 화면을 게임이 도는 동안에만 따라가게 되면서 edit mode가 이 상태가 되었습니다. 기존에는 크기를 보고하지 않는 화면을 만나면 rect를 그대로 두었기 때문에, edit mode에서 만든 rect는 play mode가 맞춰줄 때까지 생성 당시의 크기를 그대로 들고 있었습니다.

### 수정됨
- `SafeRect`: edit mode에서 rect가 매 프레임 새로운 크기를 갖던 문제를 고쳤습니다. `ScreenChangeNotifier`가 에디터 루프에서 화면을 읽었는데, 이때 `Screen`은 에디터가 그리고 있는 view를 답으로 돌려줍니다. 어떤 틱에서는 game view, 다음 틱에서는 scene view나 inspector였습니다. 그래서 모든 틱이 방금 바뀐 화면처럼 보였고, 화면에서는 아무것도 바뀌지 않았는데도 매번 다른 값 한 쌍으로 safe area가 다시 적용되었습니다.

## [base/0.24.0] - 2026-08-16

### 추가됨
- `ILocalizationManager`: `GetStringAsync(EntryData)`가 추가되어 비동기 API가 완성되었습니다. 기존에는 테이블 이름과 키만 받았지만, 콜백 API는 이미 Localization 데이터 내보내기가 생성하는 `EntryData`를 받고 있었습니다.
- `ILocalizationManagerEventBus`: `OnLocaleChanged`가 선택된 로케일의 모든 변경을 알립니다. 이 매니저 바깥에서 일어난 변경도 포함됩니다. `LocalizationManager`가 Localization 패키지에 대한 구독을 직접 소유하고 `Dispose()`에서 되돌리므로, 호출부가 `LocalizationSettings`에 직접 붙었다가 해제를 잊는 일이 없어집니다.

### 변경됨
- `ILocalizationManager`: `AddLocaleChangedEvent(Action<Locale>)`와 `RemoveLocaleChangedEvent(Action<Locale>)`가 제거되었습니다. 로케일 변경 구독은 이벤트 버스의 몫이므로 `ILocalizationManagerEventBus.OnLocaleChanged`가 둘을 대신합니다. 매니저를 `ILocalizationManagerEventBus`로 해석한 뒤 `+=`와 `-=`를 사용하세요.
- `ILocalizationManager`: `GetAllKeys`가 `IReadOnlyList<string>`을, `GetAvailableLocales`가 `IReadOnlyList<Locale>`을 반환합니다. 기존 `GetAvailableLocales`는 Localization 패키지가 보관하는 `List<Locale>` 자체를 그대로 넘겨주어, 호출부가 정렬하거나 비울 수 있었습니다.
- `Mu3Library.Localization.Data`: `EntryData`, `LocaleData`, `TableData`가 `Mu3Library.Addressable.Data`와 마찬가지로 `MU3LIBRARY_LOCALIZATION_SUPPORT` 안으로 들어갔습니다. 내보내기가 생성하는 스크립트에는 이 가드가 없으므로, define을 끈 채로 생성된 Localization 스크립트를 남겨둔 프로젝트는 그 스크립트도 함께 제거해야 합니다.
- `LocalizationManager.InitializeAsync()`: 초기화 실패가 더 이상 await 바깥으로 다시 던져지지 않습니다. 콜백 API와 같은 방식으로 `IsInitialized`, `InitializeError`, `OnInitializeResult`를 통해 보고되며, 로그도 그대로 남습니다.
- `LocalizationManager`: `LocalizationSettings` 에셋이 없는 프로젝트에는 일회용 기본 설정을 넘기는 대신 빈 문자열, 빈 목록, 또는 대체 로케일로 답합니다. `LocalizationSettings`의 멤버를 읽으면 접근 시점에 기본 설정 객체가 생성되고 그에 대한 경고가 남기 때문에, 모든 진입점이 먼저 `LocalizationSettings.HasSettings`를 확인합니다.
- `AddressablesManager.InitializeAsync()`: 초기화 실패가 더 이상 await 바깥으로 다시 던져지지 않습니다. 콜백 API와 같은 방식으로 `IsInitialized`, `InitializeError`, `OnInitializeResult`를 통해 보고되며 로그도 그대로 남으므로, 세 진입점이 하나의 실패를 같은 방식으로 알립니다. 또한 핸들 획득과 완료 처리 순서를 다시 구현하는 대신 `BeginInitialize()`를 거치므로, 콜백 경로와 async 경로가 서로 갈라질 여지가 없어집니다.

### 수정됨
- `LocalizationManager`: 커스텀 로케일 코드에서 기본 로케일을 해석하거나 영어 이름으로 로케일을 찾을 때 예외가 발생하지 않습니다. `LocaleIdentifier.CultureInfo`는 culture 표에 없는 코드에 대해 null로 남으며 Localization 패키지가 이를 정상 경우로 문서화하고 있는데, 두 조회가 모두 이 값을 그대로 역참조하고 있었습니다.
- `LocalizationManager`: `GetSelectedLocale(Action<Locale>)`이 자신이 확인한 핸들을 그대로 기다립니다. 완료 핸들러를 붙일 때 `LocalizationSettings.SelectedLocaleAsync`를 다시 읽었는데, 그사이 `SelectedLocale`을 대입하면 기존 오퍼레이션이 해제되고 새 오퍼레이션이 만들어지므로, 완료 여부를 검사한 핸들과 콜백을 실은 핸들이 서로 다를 수 있었습니다. 이제 프로퍼티를 한 번만 읽습니다.
- `LocalizationManager`: 취소된 `ChangeLocaleAsync(Locale)`가 자신을 대체한 변경이 끝났다고 보고하지 않습니다. 밀려난 호출이 자신의 `finally`에서 `IsLocaleChanging`을 껐는데, 그 시점에는 새 호출이 이미 켠 뒤였습니다. 이제 취소 소스를 여전히 소유한 호출만 이 상태를 정리합니다.
- `LocalizationManager`: `CurrentLocale`이 매니저 바깥에서 일어난 로케일 변경을 따라갑니다. 이 매니저의 변경 메서드만 이 값을 썼기 때문에, `LocalizationSettings.SelectedLocale`을 직접 설정하거나 시작 시 선택기가 고른 경우에는 이전 로케일을 계속 보고했습니다.
- `LocalizationManager`: `ChangeLocaleToNative()`가 enum 이름과 culture의 영어 이름이 다른 시스템 언어도 찾아냅니다. `SystemLanguage.ToString()`과 `CultureInfo.EnglishName`을 비교하는 방식으로는 `ChineseSimplified`가 `Chinese (Simplified)`와 결코 일치할 수 없었습니다. 이제 Localization 패키지가 `SystemLanguage`로부터 만드는 식별자를 먼저 시도하고, 그다음 지역과 무관한 같은 언어를, 마지막으로 영어 이름을 시도합니다.
- `LocalizationManager`: `GetStringAsync(string, string)`이 테이블 로드 실패 시 의도대로 빈 문자열을 반환합니다. await 뒤의 상태 검사는 실패한 오퍼레이션을 await하면 그전에 예외가 던져지기 때문에 도달할 수 없었습니다.
- `AddressablesManager`: `InitializeAsync()`가 예외가 전파되는 도중에 `finally` 안에서 await하지 않습니다. 이미 초기화가 돌고 있을 때 들어온 호출자가 그 자리에서 `UniTask.WaitUntil`을 기다렸는데, 여기에는 타임아웃도 취소 토큰도 없어서 예외가 드러나지 못한 채 대기에 묶여 있었습니다.
- `AddressablesManager`: `Dispose()`가 초기화 핸들을 해제하기 전에 완료 핸들러를 떼어냅니다. 정리가 끝난 매니저에 뒤늦게 도착한 완료가 `_isInitialized`를 다시 써넣을 수 없습니다.

## [game/watermelon/0.5.0] - 2026-08-16

### 추가됨
- `BoardItemInfo.ColliderOffset`: catalog 항목의 collider 중심을 collider 지름 대비 비율로 지정하는 설정을 추가했습니다. 기본값은 `(0.0, -0.03)`입니다. board는 item index별 collider 지름을 모든 화면 해상도에서 동일하게 유지하므로, 여기에 적은 offset도 어디서나 접촉 영역을 같은 비율만큼 이동시킵니다. 이 필드가 생기기 전에 직렬화된 항목은 `(0, 0)`으로 읽혀 collider가 sprite 중심에 놓입니다.
- `BoardItemScaleRule.GetBoardContactDiameter(int, Vector2)`와 `BoardItemScaleRule.GetBoardContactDiameter(int, Vector2, float, float)`: board local 공간에서 item index의 접촉 지름을 반환하는 메서드를 추가했습니다. index와 board 크기만으로 결정되며, config가 담고 있는 sprite에는 좌우되지 않습니다.
- `BoardItem.BoardLocalColliderCenter`: item 위치를 기준으로 부모(board) local 공간에서 잰 collider 중심을 추가했습니다. 들고 있는 item을 clamp 하는 기준이자 spawn marker가 올라서는 기준입니다.

### 변경됨
- `BoardItemScaleRule`: board 기준 리사이즈가 item sprite가 아니라 item collider의 크기를 맞추도록 변경했습니다. 기존에는 sprite를 item index가 요구하는 지름에 맞춘 뒤 그 안에서 collider를 다시 줄였기 때문에, `BoardItemInfo.ColliderScale`이 실제 플레이 크기를 결정했고 새 config가 들어오면 모든 index의 접촉 영역이 함께 달라졌습니다. 이제 rule은 collider가 그 지름에 도달할 때까지 item을 확대하고 sprite는 그 결과에 맞춰 그려지므로, 어떤 config를 적용해도 item index별 접촉 영역이 같습니다. collider가 sprite에서 차지하는 비율이 작은 항목은 그만큼 sprite가 이전보다 크게 그려지며, 이는 접촉 영역을 일정하게 유지하기 위한 의도된 결과입니다.
- `BoardItemScaleRule.GetBoardScale`: `Sprite`를 받던 자리에서 `BoardItemInfo`를 받습니다. 항목의 collider 비율까지 함께 재기 때문입니다. `GetBoardScale(int, Sprite, Vector2)`와 `GetBoardScale(int, Sprite, Vector2, float, float)`는 제거했으니 catalog 항목을 넘기세요. sprite 오버로드를 override 하던 파생 클래스는 새 오버로드를 override 해야 합니다.
- `BoardController`: 들고 있는 item을 sprite 경계가 아니라 접촉 영역 기준으로 item 영역 안에 clamp 하며, spawn marker도 접촉 중심 위에 놓입니다. item index를 떨어뜨릴 수 있는 위치가 sprite 여백에 좌우되지 않습니다.
## [base/0.23.3] - 2026-08-15

### 수정됨
- `UIAreaGrid`, `UIAreaElement`, `SafeCanvas`: play mode가 도는 중에 프리팹을 로드해도 `SendMessage cannot be called during Awake, CheckConsistency, or OnValidate`가 콘솔을 채우지 않습니다. Unity는 에셋이 역직렬화될 때마다 `OnValidate`를 호출하며, 여기에는 Addressables가 런타임에 로드하는 프리팹도 포함됩니다. 그런데 이 컴포넌트들은 play mode가 도는 동안 바로 그 자리에서 레이아웃을 적용했습니다. grid는 element의 anchor를 잡았고, canvas는 safe rect를 만들어 화면에 맞췄습니다. anchor를 설정하면 Unity가 자식에게 `OnRectTransformDimensionsChange`를 보내는데, 이 메시지는 어느 모드가 돌고 있든 `OnValidate` 안에서는 거부됩니다. 이제 edit mode에서 이미 그랬던 것처럼 play mode에서도 이 작업을 `OnValidate` 바깥으로 미룹니다.

## [base/0.23.2] - 2026-08-15

### 수정됨
- `SafeRect`: anchor가 매 프레임 두 값 사이를 오가며 컴포넌트가 소유한 rect 크기를 계속 바꾸던 문제를 고쳤습니다. 기존에는 `OnRectTransformDimensionsChange`에서 `Screen`을 직접 읽었는데, 이 메시지는 canvas 재구성 중에 전달되고 `Screen`은 그 시점에 묻는 패스의 렌더 타깃을 답으로 돌려줍니다. 즉 scene view 다시 그리기에서는 scene view 크기를, game view 다시 그리기에서는 game view 크기를 보고했고, 두 답이 서로 "화면이 방금 바뀌었다"처럼 보였습니다. 이제 이 컴포넌트가 읽는 화면은 `ScreenChangeNotifier` 하나뿐이므로, 한 프레임은 하나의 화면만 보고하고 anchor가 고정됩니다.
- `SafeRect`: safe area를 보고하지 않는 화면이 매번 "바뀐 화면"으로 취급되지 않습니다. 비어 있는 safe area를 대신하던 전체 화면 rect가 적용된 값으로 기록되었기 때문에, 기록된 rect는 화면이 보고하는 값과 절대 같아질 수 없었고 `IsScreenChanged()`가 계속 true로 남아 컴포넌트가 받는 모든 메시지마다 safe area를 다시 적용했습니다. 이제 읽은 값을 그대로 기록하며, 대체 rect는 기존처럼 anchor 계산과 `OnCalculated(Rect)`에 전달됩니다.
- `ScreenChangeNotifier`: play mode가 도는 동안에는 에디터 루프가 화면을 읽지 않습니다. play mode는 이미 player loop에서 화면을 읽으므로, 두 루프가 같은 프레임을 읽으며 서로 다른 답으로 화면을 두 번 보고할 수 있었습니다.

## [base/0.23.1] - 2026-08-15

### 수정됨
- `MVPManager`: `LoadFunc()`나 `OpenFunc()` 안에서 연 presenter가 이제 부모를 owner로 찾습니다. manager가 presenter를 상태 목록에 넣는 시점이 해당 생명주기 콜백이 이미 끝난 뒤였기 때문에, 두 콜백에서 호출한 `OpenAsChild()`는 owner 항목을 찾지 못하고 `Owner presenter not found or not active` 경고를 남긴 뒤 자식을 분리된 상태로 열었습니다. ownership 연결도, owner `RectTransform`을 host로 쓰는 것도, 연쇄 close도 모두 빠졌습니다. 이제 모든 phase 전이가 그 phase에 해당하는 콜백보다 먼저 일어나므로, presenter는 자신의 `LoadFunc()`, `OpenFunc()`, `CloseFunc()`, `UnloadFunc()`가 실행되는 동안 계속 조회됩니다.
- `MVPManager`: 연쇄 close가 아직 loading 중인 자식까지 닫습니다. 그런 자식은 load 대기열에만 있었고 close 경로는 그 목록을 읽지 않았기 때문에, owner를 강제로 닫으면 사라지는 view 아래에 고아로 남았습니다. loading 중인 view는 한 번도 표시된 적이 없어 비활성 상태이므로, close coroutine이 돌 수 있도록 먼저 활성화합니다.
- `MVPManager`: 닫을 수 없는 presenter가 이제 자식을 건드리지 않습니다. 기존 close 경로는 자식 체인 전체를 먼저 강제로 닫은 다음에야 정작 자기 자신이 닫을 수 없는 상태임을 확인했고, 그래서 그대로 열려 있는 presenter의 자식만 전부 정리되었습니다.
- `MVPManager`: sorting order 배치가 아직 loading 중인 presenter까지 셉니다. 기존에는 view가 화면에 있는 항목만 읽었기 때문에, 같은 프레임에 연 같은 타입 view 두 개가 모두 prefab의 sorting order를 그대로 유지한 채 겹쳤습니다. 이제 레지스트리를 읽으며, 배치 중인 presenter 자신만 건너뜁니다.
- `MVPManager`: `CloseAll()`, `CloseAll(IEnumerable<string>)`, `CloseAllWithoutDefault()`가 아직 loading 중인 presenter도 후보로 삼습니다. close 시점과 같은 프레임에 연 창은 load 대기열에만 있었고 close 경로가 그 목록을 읽지 않았기 때문에, 닫히지 않고 살아남아 바로 뒤에 나타났습니다. 이제 강제 close는 그 창까지 닫고, 강제가 아닌 close는 기존처럼 두고 갑니다.

### 변경됨
- `MVPManager`: 세 개의 close-all 오버로드가 각자 반복문 쌍을 들고 있던 방식 대신 하나의 후보 수집기를 공유합니다. opened, opening, loading 순서로 훑기 때문에 연쇄 close 대상인 자식보다 owner에 항상 먼저 도달합니다.
- `MVPManager`: presenter 조회가 `Dictionary<PresenterBase, PresenterEntry>` 레지스트리를 거칩니다. 이 레지스트리는 항목이 열리는 순간부터 pooling되거나 view가 파괴될 때까지 보관하며, opened/open-check/load-check 세 목록을 선형 탐색하던 방식을 대체합니다. 조회는 O(1)이 되고, 다섯 개의 상태 목록은 순수한 상태별 대기열이 되었으며, owner 거부 판단이 "그 순간 어느 목록에 있었는가"가 아니라 기록된 phase 기준으로 바뀌어 닫히는 중이거나 unload 중인 presenter는 새 자식을 받지 않습니다. `CleanupDestroyedPresenters()`도 다섯 목록 대신 레지스트리를 한 번만 훑습니다.

## [base/0.23.0] - 2026-08-15

### 추가됨
- `ScriptIdentifier`: script exporter가 사용하는 C# 식별자 규칙을 한곳에서 관리하는 editor utility를 추가했습니다. keyword를 회피하는 `Sanitize`, 첫 글자까지 대문자로 올리는 `SanitizePascal`, 원본 표기를 유지하고 사용할 수 없는 문자만 밑줄로 바꾸는 `SanitizeUnderscore`, public 멤버 이름을 만드는 `ToPublicMember`를 제공합니다. 기존에는 각 exporter drawer가 필요한 규칙을 각자 복사해 두고 있어서 같은 수정을 drawer마다 반복해야 했습니다.
- `FileCreator.WriteScript(string, string, string)`: 생성 script를 저장하는 단일 경로를 추가했습니다. 지정한 시스템 폴더에 `{fileName}.cs`를 UTF-8 BOM으로 저장하고 저장한 경로를 반환합니다.
- `FileFinder.IsAssetsFolder(Object)`: asset이 프로젝트 `Assets` 폴더 안에 있는지 확인하는 검사를 추가했습니다. exporter drawer들과 MVP helper drawer가 각각 같은 복사본을 들고 있었습니다.
- `CameraExtensions.IsReady(Camera)`: 화면에 맞춰 배치하는 작업이 기다려야 하는 camera 준비 상태 검사를 추가했습니다. camera가 존재하고, 렌더링 중이며, viewport rect와 pixel 크기가 면적을 가지는지 확인합니다. `WorldSpaceBackground`와 Watermelon board 영역이 각각 같은 복사본을 들고 있었습니다.
- `RectTransformExtensions.AnchorTo(RectTransform, Vector2, Vector2, bool)`: rect가 부모의 정규화된 범위를 차지할 때 거치는 anchor 적용을 추가했습니다. `fill`이 켜져 있으면 offset을 비워 rect가 그 범위를 정확히 채웁니다. 이제 `Stretch()`가 이 함수를 거치며, `UIAreaGrid.Apply(RectTransform, UIAreaType, bool)`와 `SafeRect`가 각각 같은 코드의 복사본을 들고 있었습니다.
- `Mu3WindowDrawer`: exporter drawer들이 각각 복사해 두었던 `DrawRefreshButton(Action)`, `DrawAssetsFolderField(SerializedObject, SerializedProperty, string)`, `DrawNamespaceField(string, Action<string>, string)`, `DrawClassNameField(string, Action<string>, string, string)`를 추가했습니다. `DrawAssetsFolderField`는 `Assets` 밖의 폴더를 비우면서 경고를 남기며, MVP helper의 폴더 필드는 이전까지 경고 없이 비우기만 했습니다.
- `UIAreaGrid`와 `UIAreaElement`: 9분할 영역 anchor 편의 기능을 추가했습니다. `UIAreaGrid`는 자신의 RectTransform을 `UIAreaBoundary` 분할선으로 좌/중앙/우 column과 하단/중앙/상단 row로 나누고, 자식 `UIAreaElement`가 선택한 `UIAreaType` 영역에 anchor를 맞춥니다. 기본값은 각 축의 양쪽 끝이 0.08씩을 차지하고 가운데가 0.84를 가집니다. `GetAreaRect(UIAreaType)`, `GetAreaAnchors(UIAreaType, out Vector2, out Vector2)`, `Apply()`, `Apply(RectTransform, UIAreaType, bool)`로 같은 배치를 code에서 읽고 적용할 수 있습니다.
- `UIAreaGrid.CreateElementsAutomatically`: 모든 영역에 자식 element를 하나씩 만들어 주는 자동 생성을 추가했으며 기본값은 켜짐입니다. `ResolveElements()`는 각 영역과 자식을 연결하고 빠진 영역을 생성하며, `GetElement(UIAreaType)`는 생성 없이 해당 영역의 element를 반환하고, `CreateElement(UIAreaType)`는 직접 하나를 추가합니다. `GetRectTransform(UIAreaType)`과 `TryGetRectTransform(UIAreaType, out RectTransform)`으로 element를 거치지 않고 해당 영역의 RectTransform을 바로 가져올 수 있습니다. 한 영역은 element 하나만 가집니다. `UIAreaElement.AreaType`은 같은 grid의 다른 element가 가진 영역으로의 변경을 거부하고, `UIAreaElement.IsAreaTaken(UIAreaType)`은 해당 영역이 비어 있는지 알려주며, `CreateElement(UIAreaType)`는 두 번째를 만드는 대신 기존 element를 반환하고, `ResolveElements()`는 복사되어 이미 사용 중인 영역에 놓인 element를 빈 영역으로 옮깁니다.
- `UIAreaElement`: grid가 관리하는 값을 `DrivenRectTransformTracker`로 보고하여 RectTransform inspector에서 해당 항목이 읽기 전용으로 표시됩니다. anchor는 항상 관리되고 `FillArea`가 켜져 있으면 위치와 크기까지 포함되며, pivot과 rotation, scale은 관리하지 않습니다. 부모에 grid가 없는 element는 아무것도 관리하지 않습니다.
- `UIAreaGrid.DrawAreaGizmo`와 `UIAreaGrid.AreaGizmoColor`: 9개 영역의 외곽선을 scene view에 그리는 기능을 추가했으며 기본값은 켜짐이고 scene view의 gizmo 표시 설정을 따릅니다. `OnDrawGizmos`에서 grid의 local 공간에 그리므로 회전과 scale을 그대로 따라갑니다. Screen Space - Overlay canvas는 gizmo pass 이후에 그려지므로, 그런 canvas에서는 UI가 덮은 부분의 외곽선이 가려집니다.
- `UIAreaGrid.HorizontalEditMode`와 `UIAreaGrid.VerticalEditMode`: 분할선 하나가 어디까지 영향을 주는지 결정하는 축별 `UIAreaEditMode`를 추가했습니다. `Uniform`은 grid 전체가 분할선 한 쌍을 공유하므로 상단 영역을 오른쪽으로 넓히면 오른쪽 라인 전체가 좁아지고, `Independent`는 row(가로)와 column(세로)마다 분할선을 따로 두므로 같은 편집이 우상단 영역만 좁힙니다. 축을 `Independent`로 바꾸면 공유 분할선을 각 row/column에 먼저 복사하므로 배치가 튀지 않습니다.
- `UIAreaGridEditor`와 `UIAreaElementEditor`: 두 component의 inspector를 추가했습니다. min/max slider로 분할선을 옮기고, 다른 element가 이미 가진 영역이 비활성화되는 3x3 격자에서 영역을 선택하고, 빠진 element를 생성하고, 각 element의 object와 활성 상태, fill 상태를 grid에서 바로 조정하며, 영향을 받는 자식의 anchor를 `Undo`로 기록하며 적용합니다.
- `SafeCanvas`와 `SafeRect`: safe area component를 추가했습니다. `SafeRect`는 자신의 RectTransform을 `Screen.safeArea`에 맞춰 anchor를 잡고 크기나 방향이 바뀌는 화면을 따라가며, `SafeCanvas`는 자신의 canvas에 `SafeRect`를 하나 마련합니다. `SafeRect.Calculate()`로 safe area를 직접 적용할 수 있고, `OnCalculated(Rect)`가 적용된 영역을 하위 class에 넘깁니다. anchor는 부모를 기준으로 하므로 직속 자식에 있는 `SafeRect`만 canvas의 것으로 인정되며, 이는 `UIAreaElement`가 직속 부모의 `UIAreaGrid`만 따르는 것과 같습니다.
- `SafeCanvas.SafeRect`, `SafeCanvas.CreateSafeRect()`, `SafeCanvas.ResolveSafeRect()`: `SafeRect`는 canvas가 가진 safe rect를 보고하고 없으면 null을 반환하며, 직접 생성하지는 않습니다. `CreateSafeRect()`는 직접 생성하되 이미 가진 것이 있으면 두 번째를 만드는 대신 그것을 반환하고, `ResolveSafeRect()`는 빠진 것을 생성한 뒤 화면에 맞춥니다.
- `SafeRect`: component가 소유한 속성을 `DrivenRectTransformTracker`로 보고하므로, RectTransform inspector에서 anchor와 위치, 크기, pivot이 읽기 전용으로 표시됩니다. 회전과 scale은 구동하지 않습니다. edit mode에서도 스스로 맞추므로 editor가 player와 같은 배치를 보여주며, 구동하는 속성은 scene 파일에 기록되지 않습니다.
- `ScreenChangeNotifier`: 화면의 크기나 safe area가 바뀌면 `OnChanged`를 알리고, 확인한 값을 `ScreenSize`와 `SafeArea`로 보고하는 화면 알림을 추가했습니다. Unity는 `Screen.safeArea`에 대한 event를 제공하지 않으므로, 각 listener가 따로 확인하는 대신 이곳 한 곳에서 프레임마다 한 번 화면을 읽습니다. play mode에서는 `Application.onBeforeRender`를, edit mode에서는 editor loop를 사용합니다. 예외를 던지는 listener는 `Debug.LogException`으로 보고되며, 나머지 listener가 화면을 따라가는 것을 막지 않습니다.
- `SafeRect`: 이제 매 프레임 직접 확인하는 대신 화면을 알려주는 쪽을 통해 따라가므로, scene에 safe rect마다 하나씩 있던 `Update()`가 남지 않습니다. canvas가 크기나 방향이 바뀐 화면을 따라가면 Unity가 `OnRectTransformDimensionsChange`를 보내고, 화면 크기는 그대로인 채 safe area만 바뀌는 경우(예: 기기를 위아래로 뒤집은 경우)는 `ScreenChangeNotifier`가 담당합니다. component가 override 하던 `Update()`는 없어졌으므로, 이를 override 하던 하위 class는 `OnRectTransformDimensionsChange()`를 override 하거나 `ScreenChangeNotifier.OnChanged`를 구독해야 합니다.

### 변경됨
- `SubscribeHandler.UnSubscribe(uint)`: `ISubscriptionInfo`와 `SubscriptionInfo`가 이미 사용하던 표기에 맞춰 `Unsubscribe(uint)`로 이름을 변경했습니다.
- `NotificationArguments.CancelmText`: Template sample에서 옆에 있는 `ConfirmText`와 표기를 맞춰 `CancelText`로 이름을 변경했습니다.
- `WorldSpaceBackground`: namespace를 `Mellow.Utility`에서 폴더 경로와 패키지 나머지에 맞는 `Mu3Library.Utility`로 옮겼습니다. 이 타입을 참조하던 프로젝트는 `using`을 수정해야 합니다.
- `CoroutineSafeRunner`: namespace를 `Mu3Library.Coroutine.Foundation`에서 `Foundation/Coroutine` 폴더 경로와 이웃한 `Mu3Library.Foundation.Event`에 맞는 `Mu3Library.Foundation.Coroutine`으로 옮겼습니다. 이 타입을 참조하던 프로젝트는 `using`을 수정해야 합니다.
- `MVPManager.Dispose()`: `OnWindowLoaded`, `OnWindowOpened`, `OnWindowClosed`, `OnWindowUnloaded`를 정리하고, manager root·render camera·out panel 참조를 비우며, 자신이 만든 `EventSystem`을 파괴하도록 변경했습니다. scene에 이미 있던 `EventSystem`은 프로젝트 소유이므로 건드리지 않습니다. 이전에는 만들어 둔 `EventSystem`이 `DontDestroyOnLoad` 오브젝트로 manager보다 오래 남고, event도 구독자를 계속 들고 있었습니다.
- `LocalizationManager`: fallback locale을 최대 하나만 만들고 `Dispose()`에서 파괴하도록 변경했습니다. 프로젝트 설정에서 가져온 locale은 asset이므로 파괴하지 않습니다. 이전에는 캐시한 기본 locale이 비워질 때마다 아무도 해제하지 않는 fallback이 다시 만들어질 수 있었습니다.
- `SceneLoader`: built-in·editor·Addressables scene command가 각각 복사해 두었던 command 가드를 한곳으로 모았습니다: `GuardSingleScenePreload`, `GuardAdditiveScenePreload`, `GuardAdditiveSceneUnload`, `ActivatePreloadedSingleScene`, `ActivatePreloadedAdditiveScene`. 거부 사유와 검사 순서, 발생하는 event는 그대로이며, build settings 검사·scene asset 검사·Addressables handle 조회처럼 각 backend 고유인 부분만 각자 남았습니다.
- `LocalizationManager`: `IDisposable`을 구현하도록 변경했습니다. `AddressablesManager`와 `SceneLoader`처럼 이제 소유한 DI scope가 core와 함께 정리합니다. dispose 시 one-shot 구독을 해제하고, Localization 초기화 operation에 걸어둔 완료 handler를 회수하며, 진행 중인 locale 변경을 취소하고, event와 캐시한 locale을 정리합니다. 초기화 operation 자체는 Localization package 소유이므로 release 하지 않습니다.
- MVP Helper: 생성한 MVP script를 `FileCreator.WriteScript`를 통해 UTF-8 BOM으로 저장하도록 변경했습니다. 기존에는 이 drawer만 플랫폼 기본 인코딩으로 저장해 다른 exporter와 달랐습니다.
- 잡은 exception을 문자열로 만들어 `Debug.LogError`로 남기던 부분을 모두 `Debug.LogException`으로 변경했습니다. exception 타입과 stack trace가 console에 그대로 남습니다: `LocalizationCharacterCollectorDrawer`, `InputSystemManager.AddInputActionAsset(string, ...)`, `WebRequestManager.CreateUnexpectedFailureResult`와 `WebRequestManager.ParseResult`. `WebRequestManager`의 두 곳은 기존 실패 메시지를 `WebRequestResult`로 그대로 반환하므로 호출자가 받는 url·method 정보는 변하지 않습니다. `Application.logMessageReceived`로 이 로그를 걸러내던 프로젝트는 `LogType.Error` 대신 `LogType.Exception`을 받게 됩니다.

### 수정됨
- `CanvasExtensions.CopyTo`: `overwriteScaler`는 이제 `CanvasScaler` 설정을, `overwriteRaycaster`는 `GraphicRaycaster` 설정을 복사해 옵션 이름과 실제 동작이 뒤바뀌지 않습니다.
- `AddressablesManager.Initialize(Action)`, `AddressablesManager.InitializeWithResult(Action<bool, string>)`, `LocalizationManager.Initialize(Action)`, `LocalizationManager.InitializeWithResult(Action<bool, string>)`: callback을 `OnInitialized`·`OnInitializeResult`에 그대로 남겨두지 않고, 이 manager들이 이미 제공하던 one-shot 구독으로 등록하도록 수정했습니다. 초기화가 실패한 뒤 다시 초기화를 호출해도 앞선 호출에 전달한 callback이 다시 호출되지 않으며, 초기화가 끝난 뒤에도 callback과 그것이 캡처한 대상이 manager 수명 동안 남아 있지 않습니다.
- `CoreRoot`: 파괴될 때 구독 handler를 dispose 하고 `OnCoreInitialized`, `OnCorePrepared`를 정리하도록 수정했습니다. 자신이 만든 core 대기 구독이 자신보다 오래 남지 않습니다. `CoreBase`는 이미 파괴 시 같은 처리를 하고 있었습니다.

## [game/watermelon/0.4.0] - 2026-08-15

### 추가됨
- `BoardController`: `OnScoreAdded`, `OnBoardConfigChanged`, `OnHoldingItemChanged`, `OnHoldingItemMoved`, `OnItemDropped`, `OnItemAdded`, `OnItemRemoved`, `OnItemMerged`, `OnMergeComboChanged` event를 추가했습니다. 외부 프로젝트가 board를 polling 하지 않고 UI와 연출을 갱신할 수 있습니다. `OnScoreAdded`는 한 번의 변경이 실제로 지급한 점수를 전달하며 0으로 clamp 되어 변화가 없는 경우에는 발생하지 않고, `OnItemRemoved`는 item이 아직 catalog 정보와 위치를 유지한 상태에서 호출되며, `OnItemAdded`는 플레이어의 drop, 병합, command, 복원된 snapshot을 가리지 않고 board에 들어오는 모든 item에 대해 발생합니다.
- `BoardController`: 들고 있는 item이 대기하는 위치를 board 영역 너비 비율로 읽는 `HoldingNormalizedX`를 추가했습니다. `OnHoldingItemMoved`가 전달하는 값과 같습니다.
- `BoardMergeInfo`: `BoardController.OnItemMerged`로 전달되는 merge 정보를 추가했습니다. 합쳐진 catalog index, 생성된 index와 instance, board 정규화 위치, 지급 점수, `IsValid`를 담습니다.
- `BoardController.CountMerge(BoardMergeInfo)`와 `IBoardCommandContext.CountMerge(BoardMergeInfo)`: merge 내용을 함께 보고하는 merge 집계를 추가했습니다. board와 `MergingCommand`가 수행하는 모든 merge가 이 경로를 사용합니다. 기존 `CountMerge()`는 내용 없이 집계하며 `BoardMergeInfo.Unknown`을 보고합니다.
- `CompositeBoardCommand`: `CompositeBoardCommand(Action, IBoardCommand[])` 생성자와 `Step(float)` hook을 추가했습니다. command 그룹은 자식을 한 단계 진행시키는 방법만 기술하면 됩니다. `SequenceCommand`와 `ParallelCommand`가 각각 완료 callback과 `OnRun`/`OnUpdate`/`OnComplete` 연결을 들고 있었습니다. `OnRun`, `OnUpdate`를 override 해서 자식을 직접 구동하던 그룹은 그대로 동작합니다.

### 변경됨
- `MergingCommand`: merge sound를 집계보다 먼저 재생하도록 변경했습니다. `OnItemMerged` 구독자가 해당 merge의 combo 단계를 바로 확인할 수 있습니다. merge 추적을 위해 `CountMerge()`를 override 하던 `BoardController` 파생 클래스는 `CountMerge(BoardMergeInfo)`를 override 해야 합니다.
- `BoardSnapshot.FromJson`: 읽을 수 없는 snapshot을 문자열로 만들어 `Debug.LogError`로 남기던 것을 `Debug.LogException`으로 변경했습니다. exception 타입과 stack trace가 console에 그대로 남습니다. `Application.logMessageReceived`로 이 로그를 걸러내던 프로젝트는 `LogType.Error` 대신 `LogType.Exception`을 받게 됩니다.

### 제거됨
- `BoardController.SetBoareConfig(BoardConfig)`: `SetBoardConfig(BoardConfig)`를 그대로 호출하기만 하던 오타 호환 별칭을 제거했습니다. `SetBoardConfig(BoardConfig)`를 사용하세요.
- `BoardArea`: board local 대응 메서드로 전달하기만 하면서 하나의 좌표 공간에 두 개의 이름을 만들던 "board world normalized" 변환들을 제거했습니다: `BoardWorldNormalizedPositionToWorld`, `BoardWorldNormalizedPositionToScreen`, `BoardWorldNormalizedPositionToLocal`, `WorldToBoardWorldNormalizedPosition`, `TryWorldToBoardWorldNormalizedPosition`, `ScreenToBoardWorldNormalizedPosition`, `TryScreenToBoardWorldNormalizedPosition`, `LocalToBoardWorldNormalizedPosition`, `TryLocalToBoardWorldNormalizedPosition`. 대응하는 `BoardLocalNormalized` 변환을 사용하세요.

## [game/watermelon/0.3.0] - 2026-08-09

### 추가됨
- `BoardController`: 공개 보드 커맨드 큐(`EnqueueCommand`, `CancelCommand`, `CancelCommands<T>`, `CancelAllCommands`, `HasCommand<T>`, `Commands`, `CommandCount`)와 `OnCommandEnqueued`, `OnCommandFinished`, `OnCommandFailed` 이벤트를 추가했습니다. 실행 중 커맨드가 추가·취소한 변경은 현재 커맨드 진행이 끝난 뒤 적용되며, 예외가 난 커맨드는 로그를 남기고 취소·제거하되 나머지 커맨드는 계속 실행합니다.
- `IUpdatableBoardCommand`와 `ICancelableBoardCommand`를 커맨드 계약의 선택적 부분으로 추가했습니다. `IBoardCommand`는 최소 계약으로 유지하면서 프레임 업데이트와 사전 취소를 지원합니다.
- `BoardCommand`를 `OnRun`, `OnUpdate`, `OnComplete`, `OnCancel`, `OnDispose` hook과 `Complete`, `Cancel` 전환, `BoardCommandState` 상태를 제공하는 lifecycle base로 변경했습니다. 자체 상태 머신이 필요한 커맨드는 `IBoardCommand`를 직접 구현할 수 있습니다.
- `BoardCommandRunner`를 추가해 선택적 계약을 포함한 단일 커맨드를 실행하며, 보드 큐·커맨드 그룹·보드 외부 host가 공유하도록 했습니다.
- `IBoardCommandContext`와 `BoardController.CommandContext`를 추가했습니다. 외부 커맨드는 이 표면을 통해 아이템 생성·교체·삭제, 점수·병합 카운트, 사운드, 큐와 보드 상태를 사용합니다. 표면은 `TrySpawnItem`, `TryReplaceItem`, `RemoveItem`, `ContainsItem`, `GetItemInfo`, `AddScore`, `CountMerge`, `PlayBoardSound`, `PlayItemMergeSound`, `EnqueueCommand`와 읽기용 `Area`, `Config`, `ScoreRule`, `Items`, `HoldingItem`, `ItemIndexCount`, `IsPrepared`, `IsRunning`, `Score`로 이루어집니다.
- `ActionCommand`, `DelayCommand`, `WaitUntilCommand`, `SequenceCommand`, `ParallelCommand`, `CompositeBoardCommand` flow 커맨드를 추가해 프로젝트가 자체 스케줄러를 작성하지 않고도 보드 작업을 순서화·지연·그룹화할 수 있습니다.
- 보너스 아이템, 삭제 power-up, 단일 아이템 승급, 보드 흔들기, 점수 보너스를 위한 `SpawnItemCommand`, `RemoveItemsCommand`, `PromoteItemCommand`, `ShakeBoardCommand`, `AddScoreCommand`를 추가했습니다.
- `MergingCommand`가 보드가 전달한 두 callback을 실행하는 대신 `IBoardCommandContext`를 통해 병합 자체를 수행하도록 변경했습니다. 보드가 찾은 쌍과 프로젝트가 선택한 쌍에 같은 커맨드를 사용할 수 있고, 병합 결과 index, 점수, 병합된 아이템 생성, 그 위치, 사운드는 `protected virtual` hook `GetMergedIndex`, `GetMergeScore`, `SpawnMergedItem`, `TryGetMergedPosition`, `PlayMergeSound`로 확장할 수 있습니다. 보드 전체가 서브클래스를 쓰게 하려면 팩토리 `BoardController.CreateMergingCommand(BoardItem, BoardItem)`를 override하며, 쌍 검사는 `TryCreateMergingCommand`로 옮겼습니다.
- `MergingCommand`를 다른 아이템 커맨드와 함께 `Board.Command.Item`으로 이동하고 `Board.Command.Merge` namespace와 폴더를 제거했습니다.
- `BoardController.AddScore(int)`, `CountMerge()`, `Items`, `Config`, `Area`, `ItemIndexCount`, `ContainsItem(BoardItem)`, public `GetItemInfo(int)`를 추가했습니다. 모든 점수 변경과 병합 카운트가 한 경로를 사용하며 음수 점수는 0 아래로 내려가지 않습니다.
- `BoardItem.AddVelocity(Vector2, float)`를 추가해 보드에 이미 놓인 아이템의 기존 속도를 유지하면서 밀어낼 수 있습니다.
- `BoardConfig`: 보드 사운드를 담는 선택적 `SoundConfig`를 추가했습니다. `BoardSoundType`별 클립(`GameStart`, `GameEnd`, `ItemDrop`)으로 구성되며, 모든 클립은 비워둘 수 있고 클립이 없는 시점은 소리를 내지 않으므로 이미 준비된 사운드만 설정해 사용할 수 있습니다.
- `BoardSoundConfig`: `BgmClips`, `BgmShuffle`, `BgmTrackInterval`, `BgmLoopCount`로 구성되는 선택적 BGM 플레이리스트를 추가했습니다. 게임 시작에 재생을 시작하고 게임 종료에 정지하며, 보드가 직접 시작한 플레이리스트만 정지합니다.
- `BoardSoundConfig`: `ItemMergeClips`와 `MergeComboInterval`로 연속 머지에 따라 달라지는 머지 효과음을 추가했습니다. 첫 머지는 첫 클립을 재생하고, 해당 간격(기본 5초) 안에 이어지는 머지마다 인덱스가 하나씩 올라가 마지막 클립에서 멈춥니다. 간격을 넘겨 발생한 머지는 연속성을 처음부터 다시 시작합니다. 현재 단계는 `BoardController.MergeComboIndex`로 확인할 수 있고 `PlayItemMergeSound()`는 `protected virtual`입니다.
- `BoardController`: `SoundConfig`, `AudioManager`와 `protected virtual PlayBoardSound(BoardSoundType)` 훅을 추가했습니다. 이 패키지에 재생 경로를 따로 만들지 않고 보드 사운드를 `Mu3Library.Audio.AudioManager`로 재생합니다. 프로젝트가 이미 사용 중인 `IAudioManager`를 지정하면 동시 재생 개수 설정을 그대로 공유하며, 보드는 그 수명과 볼륨에 관여하지 않습니다. 지정하지 않으면 보드가 실제로 재생하는 첫 사운드에서 자체 인스턴스를 만들어(클립이 하나도 없는 설정에서는 생성되지 않습니다) `Update`에서 직접 구동하고, 보드와 함께 정리합니다.
- `BoardController`: 보드 사운드 볼륨을 `SfxVolume`과 `BgmVolume`으로 이동했습니다. 플레이어가 바꾸는 볼륨 설정이 닿지 않던 `BoardSoundConfig`에 두는 대신 프로젝트가 보드에서 직접 설정합니다. 두 값은 0–1 범위로 제한되며, `BgmVolume`은 보드가 소유한 오디오 매니저에 즉시 반영되고, 지정된 매니저는 자기 볼륨을 그대로 유지합니다.

### 변경됨
- `BoardArea`: spawn guide line의 가로 크기를 spawn marker 가로의 1/10에서 2/5로 변경했습니다. board 가로 기준으로는 1/25입니다.

### 수정됨
- `BoardArea`: spawn guide line이 다시 의도한 크기로 그려집니다. tiled `SpriteRenderer`는 스프라이트 경계가 아니라 `SpriteRenderer.size` 영역에 그리므로, 이제 크기는 renderer size로 맞추고 자식 transform scale은 반복되는 한 세그먼트의 크기만 결정하도록 두 축을 동일하게 유지합니다.
- `BoardArea`와 Watermelon 샘플: 보드 Gizmo에 아이템 영역 사각형을 추가하고 샘플 보드 배경과 사운드 설정을 갱신했습니다.

## [game/watermelon/0.2.0] - 2026-08-08

### 추가됨
- `BoardArea`: 선택적 아웃라인 스프라이트를 설정된 보드 폭에 맞추고 아이템 초과 경계에 배치하는 기능을 추가했습니다.
- `BoardConfig`: 드래그 중에만 보이는 세로 타일 방식 spawn guide line 스프라이트 설정을 추가했습니다. guide line의 폭은 spawn marker 폭의 1/10이며, guide line은 보드 sorting order `+1`, spawn marker는 `+2`로 렌더링됩니다.
- `BoardController`: 아이템 생성 간격(기본 0.5초)을 추가해 터치할 때마다 아이템이 연속으로 생성되지 않게 했으며, `CanSpawnItem`과 `DropCooldown`으로 상태를 확인할 수 있습니다.
- `BoardController`: 아이템을 놓는 순간 아래 방향으로 가해지는 초기 속도를 추가했습니다. 이 값은 초당 board area 높이의 비율로 설정되며, board area는 화면 해상도에 따라 크기가 달라지므로 어떤 기기에서도 동일한 힘이 적용됩니다. 아이템마다 질량이 다르므로 `BoardItem.SetDropVelocity(Vector2)`는 힘이 아닌 속도로 적용합니다.
- `BoardController`: 낙하 가속도를 board area 높이 대비 비율(초 제곱당)로 설정하고, 아이템을 놓을 때 `BoardItem.GravityScale`로 적용하도록 추가했습니다. 초기 속도와 함께 적용되어 낙하의 시작뿐 아니라 전 구간이 어떤 해상도에서도 보드 대비 같은 비율을 같은 시간에 이동합니다. 보드 아이템만 조정하며 프로젝트 중력 설정은 변경하지 않습니다.
- `BoardSnapshot`: `ToJson`과 `FromJson`을 통한 JSON 직렬화·역직렬화를 추가해 점수, 생성·병합 횟수, 들고 있는 아이템과 미리보기 아이템의 인덱스, 보드 기준 아이템 위치와 회전을 저장합니다.
- `BoardController`: 들고 있는 아이템, 미리보기 아이템, 보드 아이템의 위치와 회전을 포함해 보드를 저장·복원하는 `ExportSnapshot`, `ExportSnapshotJson`, `ImportSnapshotJson`을 추가했습니다.
- `BoardConfig`: 모든 보드 아이템에 적용할 물리 머티리얼, 선형 감쇠, 각 감쇠를 설정하는 선택적 `ItemPhysicsMaterial`과 `ItemRigidbodySettings`를 추가했습니다.
- `BoardItem`: 아이템 프리팹을 변경하지 않고 보드 전체 물리를 설정할 수 있도록 `PhysicsMaterial`, `LinearDamping`, `AngularDamping` 프로퍼티를 추가했습니다.
- `BoardController`: 다음 아이템 미리보기를 `NextItemIndex`, `NextItemInfo`, `OnNextItemChanged`로 추가했습니다. spawn rule에서 한 개를 미리 뽑아두므로 현재 들고 있는 아이템 다음에 무엇이 나올지 확인할 수 있으며, 들고 있는 아이템 자체는 `HoldingItem`으로 노출됩니다.
- `BoardController`: 아이템을 놓은 뒤 drop interval 동안 게임 종료 판정을 중단하는 `IsGameEndCheckPaused`를 추가했습니다. 아이템이 높이 쌓인 상태에서는 낙하 아이템이 거의 즉시 착지 판정을 받기 때문에, 이제 그 시간 동안 아이템이 미끄러져 자리를 잡은 뒤에 판정합니다.

### 변경됨
- `BoardController`: 보드 설정을 검증된 `SetBoardConfig(BoardConfig)` 경계로 적용하도록 변경했습니다(`SetBoareConfig`은 호환성 별칭으로 유지). 활성 및 보유 아이템의 크기와 보드 기준 물리를 원자적으로 갱신합니다.
- `BoardItemsConfig`: 11개 기본 과일 이후의 카탈로그 항목을 향후 규칙을 위해 보존하며, 기본 생성·병합 규칙은 11개 항목만 사용하도록 변경했습니다.
- `BoardController`: 병합 후보를 접촉 확인 전에 인덱스별로 그룹화해 관계없는 쌍을 비교하지 않고 warmup 이후에는 프레임마다 그룹을 할당하지 않게 했으며, 레이아웃 재구성 중 보드 자식 렌더러와 초과 경계 콜라이더를 캐시하도록 변경했습니다.
- `BoardArea`: 공개 world XY bounds API는 유지하면서 카메라 정렬 또는 기울어진 보드 평면에서도 로컬 보드 bounds가 올바르게 계산되도록 변경했습니다.
- `WatermelonGame` 샘플: 의존성을 검증·캐시해 씬이나 설정 의존성이 없으면 보드가 준비되었다고 알리지 않고, 보드 준비가 성공한 뒤에만 게임을 시작하도록 변경했습니다. 샘플 보드에 아이템 초과 경계선과 spawn marker 이미지를 설정했습니다.
- `BoardItemInfo`: 과일 접촉이 설정된 스프라이트 크기에 더 잘 맞도록 기본 콜라이더 지름 비율을 `0.96`에서 `0.98`로 변경했습니다.
- `BoardController`: 떨어뜨릴 아이템이 터치 시점이 아니라 drop interval이 끝나는 즉시 보드 상단에서 대기하도록 변경했습니다. 직전 아이템을 떨어뜨린 위치에서 대기하며, 터치는 그 아이템을 집어 옮기기만 합니다.
- `BoardArea`: 컴포넌트를 `Board/Area` 아래의 단일 책임 파트로 분할했습니다. `BoardAreaBoundsCalculator`는 보드 사각형을 계산하고, `BoardAreaCoordinateConverter`는 좌표를 변환하며, `BoardAreaView`는 보드와 아이템 기준선을 그리고, `BoardAreaOutColliders`는 아이템을 보드 안에 가두며, `BoardAreaInputRelay`는 보드에 속한 터치만 전달합니다. 사각형 자체는 새로 추가한 `BoardAreaBounds`와 `CoordinateBounds` 타입이 담습니다. 컴포넌트의 공개 API는 그대로이며 각 파트로 위임만 합니다.
- `BoardItemScaleRule`: 아이템 지름을 점점 줄어드는 넓이 배율 대신 board area 가로 길이 기준으로 선형 분배합니다. 가장 작은 과일은 `1/20`, 가장 큰 과일은 `2/5`입니다. `GetBoardScale`은 가장 작은 비율과 함께 가장 큰 비율도 받으며, `GetBoardWidthDiameterRatio(int)`는 보드 가로 길이 대비 아이템 지름 비율을 반환합니다.

### 제거됨
- 병합 이펙트: `BoardItemInfo.MergingEffect` / `MergedEffect`, `MergingCommand`의 이펙트 재생, 그리고 샘플의 병합 이펙트 프리팹·머티리얼·텍스처를 제거했습니다. 해당 프리팹은 Unity 5 시절의 레거시 프리팹 포맷으로 저장되어 `ParticleSystemRenderer`에 `serializedVersion`이 없고 대부분의 필드가 누락되었으며 0번 머티리얼이 null이었습니다. 이로 인해 첫 병합에서 이펙트가 생성되는 순간 `ParticleSystemRenderer::PrepareForRender`에서 에디터가 네이티브 크래시로 종료됐습니다.

### 수정됨
- `BoardArea`: drop cooldown 중 시작한 드래그에서 보유 아이템이 준비된 뒤에도 guide line이 표시되도록 수정했으며, guide line의 보드 기준 크기를 transform scale로 적용하도록 수정했습니다. `SpriteRenderer.size`로 적용하면 guide line이 사라졌습니다.
- `MergingCommand`: 유효하지 않거나 취소·실패한 커맨드의 병합 예약을 해제하되, 이미 재사용된 풀 아이템은 변경하지 않도록 수정했습니다.
- `BoardItem`: 풀에서 재사용되는 인스턴스가 다시 초기화되기 전에 표시, 콜라이더, 물리, 지지, 병합 상태를 초기화하도록 수정했습니다.
- `InputHandler`: 터치 이동과 종료가 드래그를 시작한 손가락에 계속 연결되도록 수정했습니다.
- `BoardArea`: `CalculateBounds(Camera, float)`와 `CalculateBounds(Camera, Vector2, float)`가 전달받은 aspect ratio를 실제로 사용하도록 수정했습니다. 기존에는 항상 보드 스프라이트의 비율로 대체되었습니다.
- `BoardController`: 게임 종료 판정을 접촉 기반으로 수정했습니다. 상단이 보드 라인을 넘은 아이템이 바닥 또는 다른 아이템에 지지되어 있으면 게임을 종료합니다. 아이템은 항상 라인 위에 놓이므로 아직 착지하지 않은 아이템은 낙하 상태로 제외되며, 좌우 벽은 지지 대상에서 제외됩니다. 아이템 초과 경계선은 이제 표시용일 뿐입니다.
- `BoardItem`: 낙하가 보드 바닥이나 다른 아이템 위에서만 끝나도록 수정했습니다. 옆 벽을 스치기만 해도 착지로 판정되어, 벽에 붙어 쌓인 아이템이 게임을 너무 일찍 끝냈습니다.
- `BoardController`: 병합 중인 아이템이 커맨드 완료까지 보드에 등록된 상태를 유지하고 동일 아이템이 중복 등록되지 않도록 수정해, 보드를 다시 준비할 때 모든 아이템이 회수됩니다. 기존에는 아이템이 누락되거나 중복 항목이 남아 보드가 무한히 증가했습니다. 오타였던 `OnDestory`를 수정해 커맨드가 실제로 해제되게 했습니다.
- `MergingCommand`: 병합이 두 번 이상 시작되거나 완료되지 않으며, 커맨드 해제 후에는 완료되지 않도록 수정했습니다.

## [game/watermelon/0.1.1] - 2026-08-07

### 추가됨
- `BoardItemInfo`: 병합 효과용 선택적 `ParticleHandler` 리소스와 생성된 효과 완료 후 자동 정리를 추가했습니다.
- Watermelon Game 샘플: 정적 병합 VFX 텍스처와 `WatermelonMergingEffect` / `WatermelonMergedEffect` 프리팹을 추가하고 `BoardConfig`로 연결했습니다.

### 수정됨
- `MergingCommand`: null 병합 효과를 만나도 전체 병합 명령을 중단하지 않고 건너뛰도록 수정했습니다.

## [base/0.22.0] - 2026-08-07

### 추가됨
- `Mu3Library.Base`: `ParticleSystem`을 필수 컴포넌트로 요구하는 `ParticleHandler`를 추가하고 재생, 일시정지, 정지, 초기화, 재시작, loop 제어와 자연 종료 `OnCompleted`를 포함한 생명주기 이벤트를 제공합니다.
- `Mu3Library.Base`: 필수 `SpriteRenderer` 배경을 카메라 뷰포트에 맞추는 `Mellow.Utility.WorldSpaceBackground`를 추가했습니다.
- `GameObjectPool<T>` 및 `GameObjectPool<T, TArgs>`: 일괄 enqueue/dequeue 오버로드와 새로 생성되거나 풀에서 꺼낸 오브젝트에 적용되는 선택적 초기화 콜백을 추가했습니다.
- `GameObjectPool<T, TArgs>`: 생성 콜백을 무인자로 변경하고 `CreateArguments` 파생 클래스를 받는 타입 안전한 초기화 콜백을 추가했습니다.

## [urp/0.2.1] - 2026-08-07

### 추가됨
- `Mu3Library.URP.Cam.CameraStackSetter`: 재사용 가능한 샘플에서 Universal Camera Data를 안전하게 보장하는 `EnsureUniversalCameraData(Camera)`를 추가했습니다.

## [game/watermelon/0.1.0] - 2026-08-07

### 추가됨
- `Mu3Library.Game.WatermelonGame`: 보드, 아이템, 병합, 설정, 입력 runtime 어셈블리를 추가했습니다.
- `BoardArea`: 보드 맞춤, 충돌 경계, 정규화 위치 변환, 카메라 투영 helper, 초기화 안전 `Try...` 변형을 추가했습니다.
- `BoardItemsConfig`: 0부터 시작하는 index로 접근하는 11개 고정 과일 항목을 추가했습니다.
- `BoardItemScoreRule`: 기본 삼각수 progression을 제공하는 가상 `GetScore(int)` 점수 확장 지점을 추가했습니다.
- Watermelon Game 샘플: `BoardConfig` 에셋, 과일/배경 이미지, 샘플 manager/core 스크립트, 플레이 가능한 `Demo` 씬을 추가했습니다.

### 변경됨
- `BoardItemScaleRule`: 아이템 크기가 board area 로컬 가로 길이를 사용하고 직교/원근 카메라 계산을 지원하도록 변경했습니다.

### 수정됨
- `BoardArea`: 보드 bounds와 생성된 경계 collider 배치를 수정했습니다.
- `BoardItem`: 설정된 sprite에 맞춰 원형 collider 반지름을 동기화했습니다.

## [base/0.21.0] - 2026-08-03

### 변경됨
- `ButtonInvokeAttribute`: 직렬화된 필드 방식 대신 매개변수가 없고 `void`를 반환하는 인스턴스 메서드 방식으로 전환했습니다. 적용된 메서드를 직접 호출하므로 동일한 이름의 오버로드가 있어도 `AmbiguousMatchException`이 발생하지 않으며, label과 버튼 높이는 선택 사항이고 label을 생략하면 `Invoke {method name}` 형식으로 표시됩니다.

### 제거됨
- `ButtonInvokeAttribute`의 필드 적용 방식, 메서드 이름 인자, `drawProperty` 옵션.

## [base/0.20.0] - 2026-08-03

### 추가됨
- `IAddressablesManager` / `AddressablesManager`: 여러 에셋을 로드하고 각 resource location의 `PrimaryKey`로 인덱싱한 `Dictionary<string, T>`를 반환하는 `LoadAssetsWithKeys<T>` 및 `LoadAssetsWithKeysAsync<T>`를 추가함.

### 변경됨
- `LocalizationDataExporterDrawer`: `Split by Table` 토글을 제거하고, 공유 `{ClassName}Locales` 스크립트, 각 테이블의 `EntryData` 인스턴스를 포함하는 테이블별 `TableData` 파생 스크립트, 간결한 root 테이블 인덱스를 항상 생성하도록 변경함.

## [base/0.19.1] - 2026-08-02

### 수정됨
- `AddressablesManager`: `MU3LIBRARY_ADDRESSABLES_SUPPORT` 및 `MU3LIBRARY_UNITASK_SUPPORT`가 모두 정의된 경우에만 UniTask 구현이 컴파일되도록 보호함.
- `ResourceLoader`: 캐시된 `GameObject`, `Component`, `AssetBundle` 인스턴스는 로더 캐시에서 해제하되, `Resources.UnloadAsset`를 직접 호출하지 않도록 수정함.

## [base/0.19.0] - 2026-07-29

### 추가됨
- `ICoreRoot` / `CoreRoot`: Core 준비 완료를 감시할 수 있도록 `OnCorePrepared` 이벤트와 일회성 `SubscribeOnCorePreparedOnce<T>(Action)` / `SubscribeOnCorePreparedOnce(Type, Action)` API를 추가함.
- `CoreBase` / `IDICore`: Core 준비 상태를 확인할 수 있도록 `IsPreparing` 및 `IsPrepared` 상태를 추가함.

## [base/0.18.0] - 2026-07-28

### 추가됨
- `ICoreRoot` / `CoreRoot`: Core 초기화 완료를 감시할 수 있도록 `OnCoreInitialized` 이벤트와 일회성 `SubscribeOnCoreInitializedOnce<T>(Action)` / `SubscribeOnCoreInitializedOnce(Type, Action)` API를 추가함.

### 변경됨
- `CoreBase` / `IDICore`: `IsPrepared`를 `IsInitialized`로 이름 변경하고 DI scope 초기화가 완료된 뒤 Core 초기화 알림이 발생하도록 정렬함.
- `CoreRoot`: Core 초기화 알림이 Core scope 초기화 이후에 발생하도록 변경했으며 초기화 구독이 폐기 가능한 `ISubscriptionInfo` token을 반환하도록 함.
- Template 샘플: `WaitForOtherCore` callback을 Core 간 `[Inject]` 의존성으로 교체함.

### 제거됨
- `CoreBase.WaitForOtherCore<TCore>`: 기존 Core 간 준비 상태 대기 helper를 제거함.

## [base/0.17.0] - 2026-07-27

### 추가됨
- `IObjectInjector`: `ContainerScope` 내부를 공개하지 않고 컨테이너 외부에서 생성된 객체에도 기존 `[Inject]` 필드와 프로퍼티 주입을 적용할 수 있는 제한된 주입 계약을 추가함.
- `MVPManager`: 컨테이너가 관리하는 인스턴스가 presenter 풀에서 새로 생성되거나 재사용되는 presenter에 초기화 전에 `[Inject]` 멤버 주입을 적용하도록 변경함.
- `Mu3Library.Foundation`: 재사용 가능한 구독 인프라를 위한 Unity 비의존 런타임 어셈블리를 추가함.

### 변경됨
- `AddressableGroupDataExporterDrawer`: 생성되는 Addressables 데이터를 `{ClassName}Labels` 문자열 라벨 스크립트, 그룹별 `GroupData` 파생 스크립트, 중첩 `EntryData` 파생 에셋 클래스, 간결한 root 그룹 인덱스로 재구성함. split 토글과 `LabelData` 런타임 타입을 제거함.
- `SubscribeHandler`: 재사용 가능한 one-shot 구독 구현을 `Mu3Library.Foundation`으로 이동했으며 `Mu3Library.Event` 네임스페이스와 public API는 유지함.
- `SubscribeHandler`: 로깅 통합을 재설계할 때까지 Foundation 진단 로그를 일시적으로 주석 처리함.
- `SubscribeHandler` / `SubscriptionInfo`: 구독 생명주기의 멱등적 해제, 예외 안전 정리, one-shot callback 정리, 내부 ID 충돌 방지를 보완함.
- Event Bus 인터페이스 및 구현: 일회성 구독 메서드가 `uint` ID 대신 폐기 가능한 `ISubscriptionInfo` token을 반환하도록 변경함.

## [base/0.16.0] - 2026-07-12

### 변경됨
- `ContainerScope`: `RegisterClass<T>()`로 생성된 클래스를 포함해 `CoreBase`에 등록된 서비스가 생성 후 생명주기 콜백 전에 `[Inject]` 필드와 프로퍼티 주입을 받도록 변경함. 팩토리 생성 인스턴스와 미리 생성해 등록한 인스턴스에도 적용함.

## [base/0.15.0] - 2026-07-01

### 추가됨
- `ISceneLoader` / `SceneLoader`: `PreloadSingleSceneWithAddressablesAsync`, `ActivateSingleSceneWithAddressablesAsync`, `LoadSingleSceneWithAddressablesAsync`, `PreloadAdditiveSceneWithAddressablesAsync`, `ActivateAdditiveSceneWithAddressablesAsync`, `LoadAdditiveSceneWithAddressablesAsync`, `UnloadAdditiveSceneWithAddressablesAsync` Addressables UniTask helper를 추가함.
- `ISceneLoaderEventBus` / `SceneLoader`: single/additive 씬 lifecycle 갱신을 위한 구조화된 `SceneLifecycleInfo` 콜백을 추가함. payload는 요청 target/key를 유지하면서 `ResolvedSceneName`을 함께 제공하고, Addressables runtime scene name이 아직 확인되지 않았을 때는 `UnnamedAddressableScene`을 사용함.
- `ScenePhase`: 구조화 lifecycle 콜백에서 additive unload 완료를 명시적으로 보고할 수 있도록 `Unloaded`를 추가함.

## [urp/0.2.0] - 2026-06-24

### 추가됨
- `Mu3Library.URP.Cam.CameraStackSetter`: `SetCameraStackToMainAsFirst`, `SetCameraStackToMain(Camera)`, `SetCameraStackToMain(Camera, int)`, `SetCameraStackAsFirst(Camera, Camera)`, `SetCameraStack(Camera, Camera)`, `SetCameraStack(Camera, Camera, int)` 정적 헬퍼를 추가하여 임의의 URP overlay camera를 `Camera.main` 또는 명시적인 root camera stack에 넣을 수 있도록 함.

### 변경됨
- `Mu3Library_URP/package.json`, `README.md`, 현지화 README, 현지화 changelog: URP 패키지 버전을 `0.2.0`으로 올리고 공개 UPM 설치 태그 참조를 함께 업데이트함.

## [urp/0.1.5] - 2026-06-21

### 제거됨
- `Mu3Library_URP/Runtime/Scripts/AGENTS.md`, `Mu3Library_URP/Runtime/Shaders/AGENTS.md`, `Mu3Library_URP/Samples~/AGENTS.md`: import 가능한 URP 패키지 표면에서 package-local agent 라우팅 문서를 제거하여, Unity 패키지 import 시 `.meta`와 어긋난 AGENTS 문서가 더 이상 포함되지 않도록 정리함.

### 변경됨
- `Mu3Library_URP/package.json`, `README.md`, 현지화 README: URP 패키지 버전을 `0.1.5`로 올리고 공개 UPM 설치 태그 참조를 함께 업데이트함.

## [base/0.14.2] - 2026-06-20

### 변경됨
- `IMVPManager` / `MVPManager` / `PresenterBase`: `OpenOptions`를 제거하고 직접 `HostOptions`를 받는 오버로드들로 교체했으며, `Open` / `OpenAsChild` 편의 오버로드들은 계속 마지막 명시 open 시그니처 하나로만 위임되도록 유지함.

## [base/0.14.1] - 2026-06-17

### 추가됨
- MVP UI 런타임에 `OpenOptions`와 `HostOptions`를 추가함. 이제 체인으로 여는 presenter가 ownership과 visual host, 루트 레이아웃 배치를 분리해서 설정할 수 있음.

### 변경됨
- `IMVPManager` / `MVPManager` / `PresenterBase`: 체인 presenter 오픈 흐름을 `owner` 용어와 명시적 host 옵션 중심으로 리팩토링함. `HostOptions.Host`를 지정하지 않으면 owner의 루트 view 아래에 붙는 기본 동작은 유지되지만, 이제 매 open마다 owner의 `RectTransform` 값을 복사하는 대신 child view resource의 루트 레이아웃을 다시 적용함.

## [base/0.14.0] - 2026-05-25

### 추가됨
- `ButtonInvokeAttribute` / `ButtonInvokeAttributeDrawer`: 직렬화된 필드에 매개변수가 없는 인스턴스 메서드를 호출하는 Inspector 버튼을 표시하는 기능을 추가했고, Attribute 샘플에도 `ConditionalHideAttribute`와 함께 예제를 추가함.

## [base/0.13.0] - 2026-05-24

### 추가됨
- `GameObjectPool<T>`: 사용자가 풀이 비었을 때의 생성을 정의할 수 있는 선택적 `Create` 델리게이트 생성자와, 풀에 보관된 비활성 오브젝트를 파기하는 `Clear()`를 추가함.

### 변경됨
- `GameObjectPool<T>`: 내부 `List<T>`를 `Queue<T>`로 교체하고, 풀에 보관된 인스턴스 ID를 추적해 중복된 비활성 오브젝트 재등록을 방지하며, 더 이상 리소스 참조로 직접 instantiate하지 않도록 변경함.
  기존 `GameObjectPool(T resource)` 생성자는 제거되었으므로, 호출부는 `GameObjectPool(Create onCreate)`로 옮기고 생성 로직을 명시적으로 전달해야 함.

## [urp/0.1.4] - 2026-05-24

### 변경됨
- `Mu3Library_URP/package.json`: URP manifest가 `com.github.doqltl179.mu3library.base` `0.13.0`에 의존하도록 업데이트하고, 패키지 메타데이터를 Base `0.13.0`과 맞춤.

## [base/0.12.0] - 2026-05-24

### 추가됨
- `ISceneLoaderEventBus` / `SceneLoader`: 기존 `SubscribeOnSingleSceneLoadedOnce`를 넘어 single-scene `LoadStarted`, `Preloaded`, `Changed` 콜백과 additive `LoadStarted`, `Preloaded`, `Loaded`, `Unloaded` 콜백까지 일회성 구독 helper를 확장함.
  이 변경으로 `ISceneLoaderEventBus` 구현 계약이 바뀌므로, 커스텀 구현체는 업그레이드 시 새 일회성 구독 메서드를 함께 구현해야 함.
- `ILocalizationManagerEventBus` / `LocalizationManager`, `IAddressablesManagerEventBus` / `AddressablesManager`, `IMVPManagerEventBus` / `MVPManager`: localization 초기화 완료/결과 이벤트, Addressables 초기화 이벤트, MVP window lifecycle 이벤트를 위한 일회성 구독 helper를 추가함.
  이 변경으로 각 event-bus 구현 계약이 바뀌므로, 커스텀 구현체는 업그레이드 시 새 일회성 구독 메서드를 함께 구현해야 함.
- `SubscribeHandler`: `Action`, `Action<T>`, `Action<T1, T2>` 등록을 위한 재사용 가능한 `SubscribeOnce(...)` 오버로드를 추가하여, 각 서비스가 자신의 handler 인스턴스를 통해 일회성 구독을 관리할 수 있게 함.

### 변경됨
- `SceneLoader`: `OnSingleSceneLoaded`, `OnAdditiveSceneLoaded`, `OnAdditiveSceneUnloaded`는 이제 우선적으로 `SceneManager.sceneLoaded` / `SceneManager.sceneUnloaded` 시점에 맞춰 동작하며, `OnAdditiveScenePreloaded`는 계속 activation 이전 milestone으로 유지됩니다. 또한 Built-in 및 Editor additive unload는 더 이상 `allowSceneActivation`으로 완료를 지연시키지 않으므로, unload progress는 기본 async operation 값을 직접 반영합니다.

## [base/0.11.0] - 2026-05-02

### 추가됨
- `.github/workflows/unity-compile-gate.yml`: `scripts/compile-gate/run-unity-compile.ps1`를 실행하는 manual self-hosted Windows 워크플로와, push/pull request 이벤트에서 실행되는 GitHub-hosted 안내 job을 추가함.

### 변경됨
- `IWebRequestManager` / `WebRequestManager`: UniTask WebRequest API에 선택적 `propagateCancellation` 플래그를 추가. 기본 동작에서는 취소를 실패/기본값 경로로 처리하고, 명시적 취소가 필요한 호출부만 opt-in 하도록 변경.
- `ISceneLoader` / `SceneLoader`: 씬 로딩 API를 명시적인 `Preload*`, `Activate*`, `Load*`, `Unload*` 명령 중심으로 단순화. fake loading 제어를 제거하고, phase/status 조회와 동일한 이름 체계의 `*Async` 대기 helper를 추가했으며, Editor 및 Addressables 씬 로딩 표면도 같은 흐름으로 정렬.
  - `ISceneLoaderEventBus`를 `LoadStarted`, `Preloaded`, `Loaded`, `Unloaded` lifecycle callback 중심으로 유지하면서 progress callback을 복원하고, rejection 보고를 `OnSceneCommandRejected(SceneCommandRejectedInfo)` 하나로 통합했으며, single-scene 전환용 `OnSingleSceneChanged(previousSceneName, loadedSceneName)`를 추가.
  - `UseFakeLoading`, `FakeLoadingTime`, 그리고 예전 CancellationToken 기반 scene async helper 계약을 제거.

### 수정됨
- `README.md` 및 현지화 README: WebRequest의 선택적 취소 전파 동작을 문서화.

## [urp/0.1.3] - 2026-05-02

### 변경됨
- `Mu3Library_URP/package.json`: URP manifest가 `com.github.doqltl179.mu3library.base` `0.11.0`에 의존하도록 업데이트하고, 패키지 릴리스를 Base `0.11.0`과 맞춤.

## [urp/0.1.2] - 2026-04-26

### 추가됨
- `ShakeEffect` / `ShakePass`: URP shake screen effect의 흔들림 진폭과 별도로 루프 지속시간을 제어할 수 있도록 `SetPeriod(float period)`를 추가.
- `GaussianBlurEffect` / `GaussianBlurPass`: 대응 pass 및 shader 구현과 함께 새 URP 전체 화면 gaussian blur effect를 추가.
- `DepthOutlineEffect` / `DepthOutlinePass`: threshold를 바꾸지 않고도 depth 기반 outline을 더 두껍게 만들 수 있도록 `SetOutlineThickness(float outlineThickness)`와 대응 샘플 slider를 추가.

### 변경됨
- `IScreenEffect` / `IScreenEffectManager`: URP ScreenEffect 계약 인터페이스 이름을 `IPassInjector`에서 변경하고, 매니저 등록 API를 `RegisterPass` / `UnregisterPass`에서 `RegisterEffect` / `UnregisterEffect`로 정리하여 현재 effect 기반 흐름과 공개 API 명칭을 일치시킴.
- `ScreenEffectBase` / `ScreenPassBase`: 커스텀 URP ScreenEffect 및 Pass 구현을 위한 공용 기반 클래스를 추가하여 활성 상태, dispose 처리, pass 생성, shader/material 생명주기 관리를 공통화함.
- `ScreenEffectManager` / `IScreenEffectManager`: URP ScreenEffect 패스 등록 클래스와 인터페이스 이름을 현재 역할에 맞게 `PostVolumeManager` / `IPostVolumeManager`에서 변경. 더 이상 Unity Volume 기반 책임을 나타내지 않음.
- `ScreenEffect` 샘플: `ScreenEffectCore`를 기존 handler 중심 setup 흐름으로 유지하고, grayscale/shake/gaussian blur/depth outline과 같은 방식으로 새 effect를 붙일 수 있도록 대응 sample handler 스크립트를 추가.
- `GaussianBlurEffect` / `GaussianBlurPass`: 전체 화면 blur API 표면, sample handler, serialized sample field, sample scene object 이름을 정식 gaussian blur 명칭으로 확정하고, 공개 조절 항목도 `Blur Radius`로 통일함. 이 브랜치의 이전 미출시 blur 프로토타입을 사용 중이었다면 `GaussianBlur*`로 옮겨야 함.

### 수정됨
- `ShakeEffect` / `ShakePass`: `SetPeriod(float period)` 값을 바꿀 때 애니메이션 도중 흔들림 위치가 다른 오프셋으로 튀지 않도록 현재 위상을 유지하도록 수정.
- `Mu3Library_URP/package.json`: `ScreenEffect` 샘플을 패키지 manifest의 `samples` 목록에 게시하여 Unity Package Manager에서 검색 및 import할 수 있도록 수정.

## [base/0.10.0] - 2026-04-26

### 추가됨
- `IMVPManager`: 부모 연동 `Open<TPresenter>(IPresenter parent, ...)` 오버로드 4가지를 추가 (인자 없음, `Arguments` 포함, `OutPanelSettings` 포함, 둘 다 포함). 부모 링크로 presenter를 열면 자식의 RectTransform이 부모에 연결되어 anchored position, size delta, local scale을 상속함.
- `IPresenter`: 런타임에 presenter의 RectTransform 레이아웃 값을 읽고 쓸 수 있도록 `AnchoredPosition`, `SizeDelta`, `LocalScale` 프로퍼티를 추가.

## [base/0.8.0] - 2026-04-05

### 추가됨
- `AudioManager`: `PlayBgmPlaylist(AudioClip[] clips, ...)` 및 `StopBgmPlaylist()`를 통한 BGM 플레이리스트 기능 추가.
  - `AudioClip` 배열을 받아 순서대로 연속 재생.
  - `loopCount`: 0 이하 = 무한 반복; 양수 = 해당 횟수만큼 전체 사이클 재생 (기본값: -1).
  - `shuffle`: 매 사이클마다 Fisher-Yates 알고리즘으로 재생 순서를 무작위화 (기본값: false).
  - `interval`: 트랙 간 대기 시간(초) (기본값: 1.0).
  - `PlaySfx`와 동일한 패턴으로 8가지 오버로드 제공.
  - `PlayBgmPlaylist` 호출 시 현재 재생 중인 BGM을 먼저 정지.
  - `StopBgm` 또는 `StopBgmPlaylist` 호출 시 플레이리스트 비활성화.
  - 인터벌 카운트다운은 pause를 인식하여 BGM이 일시정지된 동안 타이머가 진행되지 않음.
- `IAudioManager`: 새 `IAudioManager.BgmPlaylist.cs` partial 파일을 통해 `PlayBgmPlaylist` 오버로드 및 `StopBgmPlaylist` 추가.
- `ResourcesPathExporterDrawer`: 프로젝트 내 `*/Resources/*` 경로의 에셋을 자동으로 탐색하고, 폴더 계층을 중첩 static 클래스로 표현하는 C# 스크립트를 생성하는 에디터 Drawer. 각 에셋은 리소스 상대 경로(확장자 제외)와 파일명을 담은 `ResourcePathData` 필드로 노출됨.
- `ResourcePathData`: `Path`와 `Name` 문자열 프로퍼티를 가진 `Mu3Library.Resource.Data` 네임스페이스의 새 클래스.

### 변경됨
- `LocalizationNameExporterDrawer`, `AddressableGroupNameExporterDrawer`, `InputSystemNameExporterDrawer`: 각각 `LocalizationDataExporterDrawer`, `AddressableGroupDataExporterDrawer`, `InputSystemDataExporterDrawer`로 이름 변경. 관련 샘플 `.asset` 파일도 동일하게 이름 변경.
- `LocaleData`, `EntryData`, `TableData`: `Mu3Library.Localization.Data` 네임스페이스의 독립 public 클래스로 이동; 생성자를 `internal`에서 `public`으로 변경; `#if MU3LIBRARY_LOCALIZATION_SUPPORT` 가드 제거 (Unity.Localization 의존성 없음).
- `EntryData`: `TableName` 프로퍼티 추가; 생성자가 `EntryData(string tableName, string key, string id)`로 업데이트.
- `LocalizationDataExporterDrawer`: 생성 스크립트에서 `LocaleData`, `EntryData`, `TableData` 클래스 정의를 인라인으로 포함하지 않고 `using Mu3Library.Localization.Data;`로 가져옴. `EntryData` 생성 시 첫 번째 인자로 테이블 이름을 전달하도록 변경.
- `LabelData`, `EntryData`, `GroupData`: `Mu3Library.Addressable.Data` 네임스페이스에 독립 public 클래스로 추가 (`#if` 가드 없음; 순수 C#). `GroupData`는 생성된 per-group sealed class의 기반 클래스로 `Name`, `Entries`, `Labels` 딕셔너리를 보유.
- `AddressableGroupDataExporterDrawer`: 생성 스크립트 구조가 Localization 패턴으로 변경 — `Labels` 클래스는 `const string` 대신 `LabelData` 인스턴스를 보유; `Groups` 클래스는 타입된 `*Data` 그룹 인스턴스와 `IReadOnlyDictionary<string, GroupData> All`을 보유; per-group 클래스는 `sealed class *Data : GroupData` 형식으로 생성. 비폴더 entry는 `EntryData` 필드, 폴더 entry는 `EntryData Data` 필드와 `Assets` inner class를 유지. 생성 코드에 `using Mu3Library.Addressable.Data;` 포함.

## [base/0.6.0] - 2026-03-23

### 추가됨
- `MVPManager` / `IMVPManager`: `FocusIgnoredLayers` 프로퍼티와 `SetFocusIgnoredLayer(string layerName, bool ignored)` 메서드 추가.
  - 무시(ignored) 레이어의 Presenter는 포커스 및 `OutPanel` 업데이트 계산에서 제외됨.
  - 무시 레이어는 런타임에 토글 가능하며, 변경 즉시 `UpdateFocus()`가 호출됨.
- `LocalizationNameExporterDrawer`: 생성 스크립트에 루트 `Locales` 클래스(`All` 문자열 배열 및 로케일별 내부 클래스에 `Code`, `EnglishName`, `NativeName`을 `const string`으로 노출)와 루트 `Tables` 클래스(`All` 문자열 배열 및 각 테이블 클래스의 `Name`을 참조하는 `const string` 항목)가 추가됨. 각 테이블 클래스에도 루트 `Locales` 구조를 `const string` 참조로 미러링하는 `Locales` 내부 클래스가 추가됨.
- `AddressableGroupNameExporterDrawer`: 생성 스크립트에 루트 `Groups` 클래스(그룹 클래스의 `Name`을 참조하는 `const string` 항목과 `All` 배열), 루트 `Labels` 클래스(전체 그룹·엔트리에서 수집한 고유 레이블을 `All` 배열 및 `const string` 값으로 제공), 그리고 루트 `Labels` 항목을 `const string` 참조로 미러링하는 그룹별 `Labels` 내부 클래스가 추가됨.

## [base/0.5.0] - 2026-03-18

### 변경됨
- 리포지토리를 모노레포 구조로 개편: `Mu3Library_Base/`와 `Mu3Library_URP/`는 독립 UPM 패키지로, `UnityProject_BuiltIn/`과 `UnityProject_URP/`는 별도 개발 프로젝트로 분리됨.
- `.gitignore`의 무시 패턴에 `**/` 접두어를 추가하여 모노레포 하위 프로젝트 전체를 포함하도록 개선.

### 수정됨
- `CoreBase.WaitForOtherCore`: `CoreRoot.Instance`가 null일 때(예: 앱 종료 시점)에 발생하던 `NullReferenceException` 수정.
- `CoreBase.GetClassFromOtherCore`: 동일한 null 안전 처리 적용.
- `ContainerScope.ResolveFromCore`: 동일한 null 안전 처리 적용.
- 문서: 전체 README의 `ConfigureContainer()` 코드 예제 수정 — 잘못된 `ContainerScope scope` 파라미터를 제거하고 서비스 등록에 `RegisterClass<T>()`를 사용하도록 수정.

## [base/0.4.7] - 2026-03-15

### 추가됨
- `ScriptBuilder`: `ArrayBlock` 구조체(`FieldName`, `Values`)와 `AppendArrayBlock` 메서드 추가.
  - `ArrayBlock`을 `CodeBlock.Content` 목록에 `string`, `CodeBlock`과 함께 배치할 수 있음.
  - 들여쓰기는 `ScriptBuilder`가 자동으로 처리하며 `CodeBlock` 출력과 일관성 유지.

### 변경됨
- `AddressableGroupNameExporterDrawer`: `BuildArrayLines` 헬퍼를 `ScriptBuilder.ArrayBlock`으로 교체.
  - `AllNames`, `AllAddresses`, `Labels.All` 배열 선언이 `foreach` 루프 대신 단일 `.Add()` 호출로 축약됨.

## [base/0.4.6] - 2026-03-15

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

## [base/0.4.5] - 2026-03-14

### 변경됨
- `AddressableGroupNameExporterDrawer`: 하위 에셋 클래스명이 상위 클래스명으로 시작하는 경우 해당 접두어를 제거하도록 변경.
  - 예: 상위 `Views`, 하위 `ViewsDialoguePanelPrefab` → `DialoguePanelPrefab`으로 출력.
  - 중첩된 폴더 계층에도 재귀적으로 적용.

## [base/0.4.4] - 2026-03-14

### 변경됨
- `AddressableGroupNameExporterDrawer`: 폴더 에셋 지원 추가.
  - `AssetDatabase.IsValidFolder()`로 그룹에 폴더로 등록된 에셋을 감지.
  - 폴더 에셋의 경우 `GatherAllAssets()`로 하위 에셋을 수집하여 `Assets` inner static class에 중첩 출력.
  - 에디터 프리뷰에서 폴더 진입점은 `[Folder]` 접두어로 표시되고, 하위 에셋은 들여쓰기하여 표시.

## [base/0.4.3] - 2026-03-14

### 추가됨
- `AddressableGroupNameExporterDrawer`: 에디터 시점에 모든 Addressable 그룹을 읽어 그룹 이름, 에셋 이름, address(key), 레이블을 중첩 C# static 클래스로 내보내는 에디터 Drawer를 추가 (`MU3LIBRARY_ADDRESSABLES_SUPPORT` 조건 컴파일).
  - `Labels` 내부 클래스에 레이블별 `const string` 필드와 함께 모든 레이블 값을 담은 `static readonly string[] All`을 제공.
- `UtilWindow`: `AddressableGroupNameExporter` 샘플 에셋을 유틸리티 윈도우 Drawer 목록에 추가.
- `Template`: Addressable 그룹/어드레스 상수 생성 예제로 `AddressableGroupKeys`를 추가.
- `Mu3Library.Editor.asmdef`: `Unity.Addressables` 및 `Unity.Addressables.Editor` 선택적 참조와 `MU3LIBRARY_ADDRESSABLES_SUPPORT` 버전 정의를 추가.

## [base/0.4.2] - 2026-03-08

### 추가됨
- `LocalizationNameExporterDrawer`: Localization 스트링 테이블 이름과 엔트리 키를 C# 상수로 내보내 미리 선언된 조회에 사용할 수 있는 에디터 Drawer를 추가.
- `UtilWindow`: 유틸리티 윈도우 Drawer 목록에 `LocalizationNameExporter` 샘플 에셋을 추가.
- `Template`: Localization 테이블/키 상수 예제로 생성된 `LocalizationTableKeys`를 추가.

### 변경됨
- `InputSystemNameExporterDrawer` 및 `LocalizationNameExporterDrawer`: 동작 변경 없이 backing field와 캐시된 accessor를 더 쉽게 구분할 수 있도록 private serialized helper 멤버 이름을 정리.

### 수정됨
- `LocalizationNameExporterDrawer`: 엔트리 키에서 올바른 PascalCase 클래스명을 생성하도록 `SanitizeIdentifier`를 수정. `-` 등 비식별자 문자는 단어 경계로 처리되어 생략되고 다음 문자를 대문자화. `_`는 그대로 출력되며 다음 문자를 대문자화 (예: `my-key_name` → `MyKey_Name`).

## [base/0.4.0] - 2026-03-08

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

## [base/0.3.3] - 2026-03-02

### 추가됨
- `AudioManager`: 환경음 재생을 위한 `EnvironmentController` 기능 추가.
  - 새 `EnvironmentController` 클래스: `EnvironmentVolume`을 카테고리 볼륨으로 사용하여 오디오를 재생.
  - `EnvironmentInstanceCountMax` 프로퍼티 추가 (기본값: `3`, 최대: `5`).
  - `EnvironmentVolume`, `CalculatedEnvironmentVolume`, `ResetEnvironmentVolume()` 추가 (`AudioManager` 및 `IVolumeSettings`).
  - `PlayEnvironment`, `StopFirstEnvironment`, `StopEnvironmentAll`, `PauseEnvironmentAll`, `UnPauseEnvironmentAll` 메서드 추가 (`AudioManager` 및 `IAudioManager`).
  - `OnEnvironmentVolumeChanged` 이벤트 추가 (`IAudioManagerEventBus`).
  - `Stop()`, `Pause()`, `UnPause()`가 환경음도 포함하도록 갱신.

## [base/0.3.2] - 2026-03-02

### 수정됨
- `Mu3WindowDrawer`: 반복적인 `BeginChangeCheck` / `RecordObject` / `SetDirty` 보일러플레이트를 제거하기 위해 기반 클래스에 `DrawWithUndo<T>(Func<T>, Action<T>, string)` 헬퍼를 추가.
- `Mu3WindowDrawer`: `DrawFoldoutHeader1` 및 `DrawFoldoutHeader2`가 명시적인 `!=` 비교 대신 `EditorGUI.BeginChangeCheck` / `EndChangeCheck` 방식으로 통일됨.
- `DependencyCheckerDrawer`, `FileFinderDrawer`, `InputSystemNameExporterDrawer`, `MVPHelperDrawer`, `ScreenCaptureDrawer`: 모든 인터랙티브 필드가 새로운 `DrawWithUndo<T>` 헬퍼를 통해 undo/redo 상태를 올바르게 기록하도록 수정.

## [base/0.3.1] - 2026-03-02

### 수정됨
- `MVPManager`: View가 Load 중 기본 상태(예: alpha 1)로 한 프레임 렌더링되는 싱크 문제를 수정.
  - `Open()` 호출 시 즉시 `SetActiveView(true)` 하던 처리를 `SetActiveView(false)`로 변경하고,
    Load 완료 후 `Open()` 시작 직전에 `SetActiveView(true)`를 호출하도록 수정.
  - 이로 인해 애니메이션(예: alpha 0→1)이 View 초기 상태와 동기화된 후 시작됩니다.

## [base/0.3.0] - 2026-03-01

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

## [base/0.2.3] - 2026-02-16
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

## [base/0.2.0] - 2026-02-14

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

## [base/0.1.11] - 2026-02-08

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
- **Template**: 포괄적인 샘플 프로젝트
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

## [base/0.0.20] - 이전 릴리스

### 추가됨
- ObservableProperty 구현

이전 버전에 대해서는 커밋 히스토리를 참조하세요.

[urp/0.1.2]: https://github.com/doqltl179/Mu3Library_ForUnity/releases/tag/urp%2Fv0.1.2
[base/0.10.0]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.6.0...base%2Fv0.10.0
[base/0.2.3]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.2.0...v0.2.3
[base/0.2.0]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.1.11...v0.2.0
[base/0.1.11]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.0.20...v0.1.11
[base/0.0.20]: https://github.com/doqltl179/Mu3Library_ForUnity/releases/tag/v0.0.20
