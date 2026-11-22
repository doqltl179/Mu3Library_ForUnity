# Mu3Library For Unity

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](../../README.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](README.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](README.ja.md)

[![Unity Version](https://img.shields.io/badge/Unity-6000.3%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../LICENSE)

</div>

**Mu3Library**는 Unity 프로젝트를 위한 모듈화된 아키텍처 프레임워크입니다. 커스텀 DI(Dependency Injection) 시스템과 MVP(Model-View-Presenter) UI 패턴을 중심으로, 확장 가능하고 유지보수가 쉬운 게임 개발을 지원합니다.

## 📘 문서

### 패키지 문서

- [English README](../../README.md) · [Japanese README](README.ja.md)
- [패키지 변경 이력 (English)](../../CHANGELOG.md) · [한국어](../changelog/CHANGELOG.ko.md) · [일본어](../changelog/CHANGELOG.ja.md)

### 기여자 문서

- [저장소 워크플로 변경 이력](../repository/CHANGELOG.md)
- [AI agent 및 기여자 워크플로](../../AGENTS.md) — [agentkit](https://github.com/doqltl179/ai-agents)이 `.ai/`에서 생성하며, 작업 소유자, 절차, 검증 경로로 안내합니다.
- [저장소 도구](../../tools/README.md)

## ✨ 주요 특징

- 🏗 **모듈화된 Core 시스템**: 기능별로 독립된 `CoreBase`를 통한 명확한 책임 분리를 별도의 DI 패키지로 제공
- 💉 **커스텀 DI 컨테이너**: Singleton, Transient, Scoped 라이프타임 지원을 별도의 DI 패키지로 제공
- 🎨 **MVP UI 패턴**: View-Presenter-Model 분리로 테스트 가능한 UI 로직
- 🔄 **자동 생명주기 관리**: `IInitializable`, `IUpdatable`, `IDisposable` 인터페이스 기반
- 📦 **선택적 패키지 지원**: UniTask, Addressables, Input System 조건부 활성화
- 🎵 **Audio 시스템**: BGM/SFX/Environment 채널의 볼륨 제어, 믹서 그룹 라우팅, BGM 덕킹, 볼륨 저장/복원, await 가능한 페이드
- ✨ **Particle Handler**: 필수 `ParticleSystem`의 재생, 일시정지, 정지, 초기화, 재시작, `Loop`/`IsLooping`/`SetLoop`/`PlayLoop`/`PlayOnce` 제어, 상태 조회와 생명주기 이벤트를 제공하는 편의 컴포넌트
- 🌐 **WebRequest**: HTTP GET/POST/PUT/PATCH/DELETE, 다운로드 크기 조회, 재시도 백오프, 진행 중 취소, 다운로드 진행률, UniTask 지원
- 📊 **Observable 패턴**: 데이터 변경 감지 및 이벤트 기반 바인딩
- 🛠 **유틸리티 모음**: Extension Methods, ObjectPool, EasingFunctions
- ✅ **초기화 결과 계약**: Addressables/Localization 초기화 성공/실패 상태를 명시적으로 제공
- 🔁 **안정적인 네트워킹**: WebRequest 결과형 API에 상태 코드, 헤더, 타임아웃, 재시도 옵션과 선택적 취소 전파를 제공
- 🧭 **결정론적 Core 업데이트**: Core 실행 순서가 명시적이고 안정적으로 동작하는 업데이트를 별도의 DI 패키지로 제공
- ⏳ **Scene 비동기 API**: built-in 및 Addressables 씬용 phase 기반 UniTask 헬퍼와 resolved scene name을 포함한 구조화 lifecycle 콜백 제공
- 🎮 **Input System Manager**: 액션 에셋 관리, 인터랙티브 리바인딩, 바인딩 오버라이드 퍼시스턴스와 이름 내보내기 드로어를 별도의 Input System 패키지로 제공
- 🌍 **Localization Manager**: 문자열과 로컬라이즈된 에셋 조회, 모든 구독자가 전달받는 로케일 변경, 로케일/테이블/엔트리를 역할별 스크립트로 분리하는 데이터 내보내기와 문자 수집 도구를 별도의 Localization 패키지로 제공
- 🗂 **Addressables Manager**: key 단위 에셋 로드와 캐시, 다운로드 크기 조회와 의존성 다운로드, 한 번에 하나씩 Addressables에 닿는 카탈로그 확인과 갱신, 라벨, 그룹, 엔트리를 역할별 스크립트로 분리하고 그 스크립트를 define 심볼로 감쌀 수 있는 그룹 데이터 내보내기를 별도의 Addressables 패키지로 제공
- 🖼 **World-Space Background 유틸리티**: 필수 `SpriteRenderer` 배경을 카메라 뷰포트에 맞추고, 자동 맞춤·카메라 전면 배치·렌더러 표시 설정을 선택적으로 제공합니다
- 🍉 **수박 게임 템플릿**: 11개 아이템 2D 병합 보드, 설정 가능한 `BoardConfig`, 보드 맞춤/좌표 변환 helper를 제공하는 재사용 가능한 `Mu3Library.Game.WatermelonGame` 런타임 어셈블리

## 📋 요구사항

- Unity 6.3 (6000.3+)
- .NET Standard 2.1

## 📦 설치 방법

### 방법 1: 패키지 매니저 (Git URL)
1. Unity Editor에서 `Window` > `Package Manager`를 엽니다
2. `+` 버튼 클릭 > `Add package from git URL...`
3. 다음 URL 중 하나를 입력:
   ```
    # Base 패키지
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/base#base/v0.31.0

    # Notifications 패키지
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/notifications#notifications/v0.1.0

    # Input System 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/inputsystem#inputsystem/v0.1.0

    # Localization 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/localization#localization/v0.1.0

    # Addressables 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/addressables#addressables/v0.1.1

    # DI 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/di#di/v0.2.0

    # Steam 패키지 (먼저 Steamworks.NET과 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/steam#steam/v0.2.0

    # URP 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/urp#urp/v0.5.0

    # Watermelon Game 패키지 (먼저 Base와 URP 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/game/watermelon#game/watermelon/v0.8.1
   ```

패키지는 Base, URP, Watermelon Game 순서로 설치합니다. URP는 Base에, Watermelon Game은 그 둘 모두에 의존합니다.

Notifications 패키지는 Android와 iOS의 로컬 알림을 담습니다. 다른 Mu3Library 패키지는 필요하지 않습니다.

Input System 패키지는 `InputSystemManager`와 그 이름 내보내기 드로어를 담습니다. 먼저 Base를 설치하세요.

Localization 패키지는 `LocalizationManager`와 그 데이터 내보내기, 문자 수집 드로어를 담습니다. 먼저 Base를 설치하세요.

Addressables 패키지는 `AddressablesManager`와 그 그룹 데이터 내보내기 드로어를 담습니다. 먼저 Base를 설치하세요.

DI 패키지는 `CoreBase` 컨테이너와 그 link.xml 처리기, 검증 드로어와 런타임 진단 드로어를 담습니다. 먼저 Base를 설치하세요.

Steam 패키지는 Steam에서 멀티플레이 세션을 실행하는 `SteamBackend`를 담습니다. 먼저 Steamworks.NET과 Base를 설치하세요.

### 방법 2: 패키지 매니저 (로컬 디스크)
1. 이 저장소를 클론합니다
2. Unity Editor에서 `Window` > `Package Manager`
3. `+` 버튼 > `Add package from disk...`
4. 다음 중 하나를 선택합니다:
    - `packages/base/package.json`
    - `packages/notifications/package.json`
    - `packages/inputsystem/package.json` (먼저 Base 설치)
    - `packages/localization/package.json` (먼저 Base 설치)
    - `packages/addressables/package.json` (먼저 Base 설치)
    - `packages/di/package.json` (먼저 Base 설치)
    - `packages/steam/package.json` (먼저 Steamworks.NET과 Base 설치)
    - `packages/urp/package.json` (먼저 Base 설치)
    - `packages/game/watermelon/package.json` (먼저 Base와 URP 설치)

## 📚 핵심 모듈

### DI (Dependency Injection)
`CoreBase`와 `CoreRoot`를 포함한 DI 컨테이너는 별도의 DI 패키지로 옮겨졌습니다. 등록, 주입, 코어, 그리고 관리 코드를 스트리핑하는 플레이어 빌드를 위해 이 패키지가 만드는 link.xml은 그 [README](../../packages/di/README.md)를 참고하세요. Base는 매니저가 구현하는 계약 `IInitializable`, `IUpdatable`, `ILateUpdatable`, `IObjectInjector`, `[Inject]`를 `Mu3Library.Foundation` 어셈블리에 유지합니다. 아래 예제는 DI 패키지의 코어처럼 `[Inject]`로 Base의 서비스를 주입합니다.

### MVP (Model-View-Presenter)
UI를 View, Presenter, Model로 분리하여 비즈니스 로직을 테스트 가능하게 만듭니다.

```csharp
// Model: 데이터 컨테이너
public class MainMenuModel : Model<MainMenuArgs>
{
    public string PlayerName { get; set; }
}

// View: Unity 컴포넌트 참조
public class MainMenuView : View
{
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _titleText;

    public Button StartButton => _startButton;
    public TextMeshProUGUI TitleText => _titleText;
}

// Presenter: 비즈니스 로직
public class MainMenuPresenter : Presenter<MainMenuView, MainMenuModel, MainMenuArgs>
{
    [Inject(typeof(AudioCore))] private IAudioManager _audioManager;

    protected override void LoadFunc()
    {
        _view.StartButton.onClick.AddListener(OnStartClicked);
        _view.TitleText.text = $"Welcome, {_model.PlayerName}";
    }

    protected override void OpenFunc()
    {
        // 오픈 애니메이션 등
    }

    private void OnStartClicked()
    {
        // 게임 시작 로직
    }
}

// 사용
_mvpManager.Open<MainMenuPresenter>(new MainMenuArgs { PlayerName = "Player1" });
```

`MVPManager`를 `CoreBase`를 통해 등록하면 presenter의 `[Inject]` 필드와 프로퍼티가 초기화 전에 채워집니다. presenter를 열 때마다 달라지는 데이터는 `Arguments`에 전달합니다.

presenter를 체인으로 여는 경우에는 ownership과 visual host를 분리해서 설정합니다.

```csharp
public class InventoryPresenter : Presenter<InventoryView, InventoryModel, InventoryArgs>
{
    private void OpenTooltip()
    {
        OpenAsChild<TooltipPresenter>(new HostOptions
        {
            Host = _view.TooltipHost,
            ApplyLayout = rectTransform =>
            {
                rectTransform.anchoredPosition = new Vector2(24f, -16f);
            },
        });
    }
}
```

`Owner`는 생명주기 체인을 제어하고, `HostOptions`는 자식 view를 어디에 붙일지와 루트 `RectTransform` 레이아웃을 어떻게 적용할지를 제어합니다.

자식은 `LoadFunc()`와 `OpenFunc()`를 포함한 어떤 presenter 콜백에서도 열 수 있으며, owner가 아직 열리지 않았어도 됩니다. owner를 닫으면 아직 loading 중인 자식까지 연쇄적으로 닫힙니다. owner의 view 아래에 host된 자식은 owner가 먼저 닫힘을 마쳐도 unload되며, 이때 자식의 닫힘 전환은 owner의 view가 숨겨지는 시점에 끝납니다. `OnDisable`을 override하는 view가 `base.OnDisable()`을 호출하지 않아도 매니저가 그 닫힘을 끝내지만, 그런 view도 호출하는 편이 좋습니다. 닫히기 시작한 presenter는 더 이상 새 자식을 받지 않습니다.

`HostOptions.Host`가 비활성인 경우처럼 숨겨진 host 아래에서 연 창은 그곳에서 열림 전환을 진행할 수 없습니다. 대신 대기를 건너뜁니다. `OpenStart()`와 `OpenEnd()`가 연달아 실행되고, `AnimationView`는 곧바로 열린 자세를 취하며, 창은 다음 매니저 업데이트에서 열린 것으로 처리됩니다. host가 보이면 완전히 열린 모습으로 나타나고, 일반 `Close`로 닫을 수 있으며, `OpenAsync`도 완료됩니다. host가 숨겨진 채로 닫히면 닫힘 전환을 건너뛰고, `AnimationView`는 닫힌 자세를 취하므로 다음 열림에서 다시 애니메이션이 재생됩니다.

열림 애니메이션 도중에 탭을 바꿔 그 뒤의 패널이 숨겨지는 경우처럼, 창이 열리는 도중에 host나 owner가 숨겨지면 창은 열리는 중에 머무르지 않고 그 시점에 열림을 마칩니다. 남은 열림 전환은 건너뛰지만 `OpenEnd()`는 실행되고, `AnimationView`는 열린 자세를 취하며, 창은 다음 매니저 업데이트에서 열린 것으로 처리됩니다. 따라서 일반 `Close`로 닫을 수 있고 `OpenAsync`도 완료됩니다. host가 다시 보이면 창은 완전히 열린 모습입니다. view가 `base.OnDisable()`을 호출하지 않고 `OnDisable`을 override해도 view가 숨겨져 있는 동안 매니저가 다음 업데이트에서 이 열림을 마칩니다. 다만 그런 view가 같은 프레임 안에 다시 보이면 열리는 중에 남으므로, 그런 view도 `base.OnDisable()`을 호출하는 편이 좋습니다. 어떤 애니메이션이든 진행 도중에 숨겨진 `AnimationView`는 그 애니메이션의 끝 자세를 취하므로, 다음 애니메이션은 그 자세에서 시작합니다. 열리는 도중에 파괴되는 view도 같은 방식으로 열림을 마치므로, 예를 들어 host가 있는 씬이 언로드될 때 정리 과정에서 그 `OpenEnd()`가 실행됩니다. `DestroySelf()`로 파괴할 때만 열림을 그대로 둡니다.

`AnimationView` 애니메이션은 기본적으로 비스케일 시간으로 진행하므로, `Time.timeScale`을 0으로 만드는 일시정지 메뉴에서 연 팝업도 열리고 닫힙니다. 게임의 타임 스케일을 따라야 하는 애니메이션이라면 view의 `AnimationHandler`에서 **Time Mode**를 `Scaled`로 설정하거나 코드에서 `AnimationHandler.TimeMode`를 설정하세요. `TimeMode`는 `Mu3Library.Utility` 네임스페이스에 있습니다.

manager에서 직접 여는 경우에도 같은 ownership/host 구성을 하나의 오버로드로 유지할 수 있습니다.

```csharp
_mvpManager.Open<TooltipPresenter>(inventoryPresenter, new HostOptions
{
    Host = tooltipHost,
});
```

### Audio 시스템
BGM과 SFX를 분리 관리하며 볼륨 제어를 지원합니다.

```csharp
[Inject] private IAudioManager _audioManager;
[Inject] private IAudioVolumeSettings _audioVolumeSettings;

void Start()
{
    // 볼륨 설정
    _audioVolumeSettings.MasterVolume = 0.8f;
    _audioVolumeSettings.BgmVolume = 0.6f;

    // BGM 재생
    _audioManager.PlayBgm(bgmClip);

    // SFX 재생
    _audioManager.PlaySfx(sfxClip);

    // 기본 SFX 설정에서 시작해 이 효과음만의 볼륨으로 재생
    AudioSourceSettings sfxSettings = AudioSourceSettings.SfxStandard;
    sfxSettings.Volume = 0.5f;
    _audioManager.PlaySfx(sfxClip, sfxSettings);
}
```

페이드, 덕킹, 루프 간격, 플레이리스트 트랙 사이 간격은 기본적으로 비스케일 시간으로 진행하므로, 소스 자체가 계속 재생되듯 `Time.timeScale`이 0일 때도 계속 진행합니다. 게임의 타임 스케일을 따르게 하려면 `_audioManager.TimeMode = TimeMode.Scaled;`로 설정하세요. `PlayBgm`, `PlayBgmForce`, `TransitionBgm`은 `StopBgm`처럼 실행 중인 BGM 플레이리스트를 끝냅니다.

### Addressables
key로 에셋을 로드해 캐시하고 카탈로그 확인과 갱신을 한 번에 하나씩 실행하는 `AddressablesManager`는 별도의 Addressables 패키지로 옮겨졌습니다. 사용법은 그 [README](../../packages/addressables/README.md)를 참고하세요. Base는 아래 Scene Loader의 Addressables 씬 로드를 유지합니다.

### Scene Loader
built-in 및 Addressables 씬 모두에 대해 명시적인 씬 명령을 지원합니다.

```csharp
[Inject] private ISceneLoader _sceneLoader;
[Inject] private ISceneLoaderEventBus _sceneLoaderEventBus;

// 기존 콜백은 요청 target 또는 Addressables key를 그대로 유지합니다.
_sceneLoaderEventBus.OnSingleSceneLoaded += target =>
{
    Debug.Log($"Loaded target: {target}");
};

// 구조화된 lifecycle 콜백은 실제 Unity scene name을 함께 제공합니다.
// Addressables 씬은 runtime 이름이 확인되기 전까지 "UnnamedAddressableScene"을 사용합니다.
_sceneLoaderEventBus.OnSingleSceneLifecycle += info =>
{
    Debug.Log($"{info.Phase}: target={info.Target}, resolved={info.ResolvedSceneName}");
};

// Addressables가 있으면, 시작한 뒤 실패한 로드를 그 명령이 끝난 다음에 알립니다.
// 그래서 여기서는 IsLoading이 이미 false이고, 핸들러가 다른 로드를 시작해도 됩니다.
_sceneLoaderEventBus.OnSceneLoadFailed += info =>
{
    Debug.LogWarning($"Load failed: target={info.Target}, additive={info.IsAdditive}, {info.ErrorMessage}");
};

await _sceneLoader.LoadSingleSceneAsync("Main");
await _sceneLoader.LoadSingleSceneWithAddressablesAsync("Sample_Addressables");
```

### WebRequest
HTTP 요청을 간단하게 처리합니다.

```csharp
[Inject] private IWebRequestManager _webRequest;

// GET 요청
_webRequest.Get<string>("https://api.example.com/data", response =>
{
    Debug.Log(response);
});

// POST 요청
var requestBody = new { username = "player", score = 100 };
_webRequest.Post<object, ServerResponse>("https://api.example.com/submit", requestBody, response =>
{
    Debug.Log($"Success: {response.message}");
});

// UniTask 지원 (MU3LIBRARY_UNITASK_SUPPORT 활성화 시)
var data = await _webRequest.GetAsync<DataModel>("https://api.example.com/data");

// 호출부에서 필요할 때만 취소를 예외로 전파
var cancellableData = await _webRequest.GetAsync<DataModel>(
    "https://api.example.com/data",
    cancellationToken: token,
    propagateCancellation: true);

// 결과에는 HTTP 오류 응답의 상태 코드와 헤더가 담깁니다. 콜백 API와 UniTask 모두 같습니다.
// 연결이 거부된 경우처럼 응답을 받지 못한 요청의 상태 코드는 0입니다.
WebRequestResult<DataModel> result = await _webRequest.GetResultAsync<DataModel>("https://api.example.com/data");
if (!result.IsSuccess && result.StatusCode == 401)
{
    // 다시 로그인한 뒤 재시도합니다.
}
```

### Observable 패턴
값 변경을 감지하고 이벤트를 발행합니다.

```csharp
[Serializable]
public class PlayerData
{
    public ObservableInt Health = new ObservableInt();
    public ObservableString PlayerName = new ObservableString();
}

// 이벤트 구독
_playerData.Health.AddEvent(newHealth =>
{
    Debug.Log($"Health changed: {newHealth}");
    UpdateHealthUI(newHealth);
});

// 값 변경 (자동으로 이벤트 발행)
_playerData.Health.Set(80);
```

Observable 필드는 컴포넌트나 ScriptableObject의 `[Serializable]` 클래스, 배열, 리스트 안에 두어도 됩니다. Inspector에서 값을 바꾸면 값이 적용되고 구독자에게 통지됩니다. 그 편집을 Undo나 Redo하거나 프리팹 인스턴스를 되돌릴 때도 되돌아간 값으로 통지됩니다. `ObservableDictionary`에서는 같은 키를 가진 행 가운데 첫 행이 쓰이며, 키가 겹치는 행은 키를 바꿀 때까지 경고와 함께 목록에 남습니다.

## 🔧 선택적 패키지

다음 패키지들이 설치되면 해당 기능이 자동으로 활성화됩니다:

| 패키지 | Define | 기능 |
|-------|--------|------|
| [UniTask](https://github.com/Cysharp/UniTask) | `MU3LIBRARY_UNITASK_SUPPORT` | async/await 비동기 API |
| Unity Addressables | `MU3LIBRARY_ADDRESSABLES_SUPPORT` | Addressables 씬 로드 |
| Unity Input System | `MU3LIBRARY_INPUTSYSTEM_SUPPORT` | 새로운 입력 시스템 |

Android와 iOS의 로컬 알림은 별도의 Notifications 패키지로 옮겨졌습니다. 이 패키지는 Unity Mobile Notifications를 의존 패키지로 함께 설치하므로, define 심볼로 켜지 않습니다. 자세한 내용은 그 [README](../../packages/notifications/README.md)를 참고하세요.

`InputSystemManager`는 별도의 Input System 패키지로 옮겨졌습니다. 이 패키지는 Unity Input System을 의존 패키지로 함께 설치합니다. 자세한 내용은 그 [README](../../packages/inputsystem/README.md)를 참고하세요. Base는 아래에서 설명하는, `MVPManager`가 만드는 `EventSystem`의 입력 모듈을 위해 `MU3LIBRARY_INPUTSYSTEM_SUPPORT`를 유지합니다.

`LocalizationManager`는 별도의 Localization 패키지로 옮겨졌습니다. 이 패키지는 Unity Localization을 의존 패키지로 함께 설치하므로, define 심볼로 켜지 않습니다. 자세한 내용은 그 [README](../../packages/localization/README.md)를 참고하세요.

`AddressablesManager`는 별도의 Addressables 패키지로 옮겨졌습니다. 이 패키지는 Unity Addressables를 의존 패키지로 함께 설치합니다. 자세한 내용은 그 [README](../../packages/addressables/README.md)를 참고하세요. Base는 위에서 설명한 `SceneLoader`의 Addressables 씬 로드를 위해 `MU3LIBRARY_ADDRESSABLES_SUPPORT`를 유지합니다.

멀티플레이 세션의 Steam 백엔드는 별도의 Steam 패키지로 옮겨졌습니다. Steamworks.NET은 Git으로 배포되므로 이 패키지가 함께 설치할 수 없어, 이 패키지는 `MU3LIBRARY_STEAMWORKS_SUPPORT` 게이트를 유지합니다. 설치 순서와 지원 플랫폼은 그 [README](../../packages/steam/README.md)를 참고하세요.

Unity 6.6에서는 UniTask 2.5.11 이상을 쓰세요. 그 이전 릴리스의 에디터 트래커 창은 제네릭이 아닌 `TreeView`를 써서, 6.6에서는 `UniTask.Editor`가 컴파일되지 않습니다.

아직 `EventSystem`이 없으면 `MVPManager`가 하나를 만듭니다. 그 입력 모듈은 Input System 패키지 설치 여부만이 아니라 Player Settings의 **Active Input Handling**이 켜 둔 입력 백엔드를 따릅니다. Input System 백엔드가 꺼져 있으면 `InputSystemUIInputModule`은 아무 입력도 읽지 못하기 때문입니다.

| Active Input Handling | Input System 패키지 | 만들어지는 `EventSystem`의 입력 모듈 |
|---|---|---|
| Input Manager (Old) | 설치됨 | `StandaloneInputModule` |
| Input System Package (New) | 설치됨 | `InputSystemUIInputModule` |
| Both | 설치됨 | `InputSystemUIInputModule` |
| 모든 값 | 설치되지 않음 | `StandaloneInputModule` |

패키지가 없으면 `StandaloneInputModule`은 "Input Manager (Old)"나 "Both"에서만 입력을 읽습니다. 씬에 이미 있는 `EventSystem`은 그대로 사용합니다.

## 🧩 Inspector 속성

`ButtonInvokeAttribute`는 매개변수가 없고 `void`를 반환하는 인스턴스 메서드에 적용합니다. label과 버튼 높이는 선택 사항이며, label을 생략하면 `Invoke {method name}` 형식으로 표시됩니다. 버튼은 fallback Inspector가 기본 직렬화 프로퍼티를 그린 뒤 추가하므로 소스 선언 순서로 필드 사이 위치를 지정할 수 없습니다. 자체 Custom Inspector가 있는 타입은 해당 Inspector에서 `ButtonInvoke` 버튼을 직접 그려야 합니다.

```csharp
[ButtonInvoke("Refresh Data", ButtonHeight = 36f)]
private void RefreshData()
{
}

[ButtonInvoke]
private void ResetData()
{
    // Inspector label: Invoke ResetData
}
```

fallback Inspector는 모든 `MonoBehaviour`와 `ScriptableObject`에 `isFallback`으로 등록되므로, Base를 설치하면 `ButtonInvoke` 사용 여부와 관계없이 자체 Custom Inspector가 없는 모든 해당 타입을 이 Inspector가 그립니다. 이 Inspector는 `CreateInspectorGUI`를 통해 UI Toolkit으로 만들어지며, Unity의 기본 Inspector를 먼저 그리고 그 아래에 버튼을 붙입니다. 따라서 `CreatePropertyGUI`만 구현한 property drawer와 IMGUI property drawer가 Unity 자체 기본 Inspector에서처럼 그려지고, 다중 오브젝트 편집이 동작하며, 버튼은 선택된 모든 오브젝트에서 메서드를 실행합니다. 자체 에디터는 `ButtonInvokeEditorBase`를 상속해 같은 기본 Inspector와 버튼을 얻을 수 있으며, 그 에디터가 `OnInspectorGUI`를 재정의하면 Inspector는 UI Toolkit 대신 그 IMGUI 메서드를 계속 호출합니다. `OnInspectorGUI`를 직접 호출해 IMGUI로 에디터를 그리는 도구에는 IMGUI 버전이 그려지며, 그곳에서 `CreatePropertyGUI`만 구현한 drawer는 "No GUI Implemented"로 표시됩니다.

## 📖 전체 모듈 목록

- **Addressable**: 별도의 Addressables 패키지로 옮겨짐 ([README](../../packages/addressables/README.md))
- **Attribute**: `ConditionalHideAttribute`, `ButtonInvokeAttribute` 등의 커스텀 속성
- **Audio**: BGM/SFX 관리 시스템
- **Particle**: 편리한 `ParticleSystem` 제어, loop 설정과 `OnPlayed`, `OnStopped`, `OnPaused`, `OnUnPaused`, `OnCleared`, `OnRestarted`, `OnLoopChanged`, `OnCompleted` 이벤트를 제공하는 `ParticleHandler` MonoBehaviour
- **DI**: 별도의 DI 패키지로 옮겨짐 ([README](../../packages/di/README.md)). 생명주기와 주입 계약은 Base의 `Mu3Library.Foundation`에 남음
- **Event**: `SubscribeHandler`를 통한 owner 관리형 subscription 유틸리티, 재사용 가능한 일회성 helper, 폐기 가능한 `ISubscriptionInfo` token
- **Extensions**: GameObject, Transform, Vector3 등 확장 메서드
- **Localization**: 별도의 Localization 패키지로 옮겨짐 ([README](../../packages/localization/README.md))
- **Multiplayer**: 교체 가능한 백엔드 위에서 로비, 참가자 목록, 준비 확인, 호스트가 설정하고 지우는 로비 데이터와 각 플레이어가 설정하고 지우는 플레이어별 데이터(둘 다 변경을 모든 플레이어가 전달받음), 로비 채팅, 정원 규칙, 클라이언트별 송신량 제한, 게임 시작, 강퇴, 게임 중 연결이 끊긴 플레이어가 클라이언트마다 정한 시도 시간 안에 다시 접속할 때까지 유지되는 자리와 그 뒤의 로비 재입장(실패하면 게임에 알림), 시작 뒤 모든 플레이어의 로드 완료를 호스트가 기다리는 동기화, 호스트가 중계하며 `MessageWriter`와 `MessageReader`로 내용을 쓰고 읽는 게임 메시지, 각 연결의 품질(핑, 패킷 손실, 초당 바이트)과 모든 플레이어가 읽는, 호스트가 잰 각 플레이어의 핑, 텍스처로 받고, 플레이어가 사진을 바꾸면 따라 바뀌며, 더 보여 주지 않으면 게임이 놓아주는 플레이어의 아바타, 프레즌스 상태를 다루는 플레이어 호스트 세션. 백엔드로는 `LocalBackend`(한 프로세스 안에서 여러 플레이어, 또는 `LocalNetwork.CreateMachineWide`로 만든 네트워크에서 같은 PC의 에디터와 플레이어 빌드이며, 이 네트워크는 지연 시간, 지터, 패킷 손실, 네트워크 끊김을 시뮬레이션할 수 있음)가 있고, Steam 로비와 Steam 연결로 플레이하며 플레이어의 Steam 아바타와, 친구 목록에 상태를 보여 주는 Steam 리치 프레즌스를 다루는 `SteamBackend`는 별도의 Steam 패키지([README](../../packages/steam/README.md))에 있습니다. 메시지 종류별 개수도 제공하며, 에디터에는 플레이 모드에서 실행 중인 각 세션의 상태, 플레이어, 연결 품질, 메시지 개수를 보여 주는 **Mu3Library > Windows > Multiplayer Debugger** 창이 있으며, 이 창의 **Host ping** 열은 호스트에서든 클라이언트에서든 호스트가 잰 각 플레이어의 핑을 보여 줍니다
  - **백엔드 작성**: 다른 플랫폼의 백엔드는 `Mu3Library.Multiplayer.Backend`의 `IMultiplayerBackend`를 구현하며, 매니저가 찾는 두 선택 인터페이스도 구현할 수 있습니다. 공개가 아닌 로비에 초대로만 들어갈 수 있으면 `ILobbyInviteBackend`를 구현해 호스트가 유지되는 자리로 돌아온 플레이어를 다시 초대하게 하고, 백엔드를 사용할 수 없는 이유를 알리려면 Steam 백엔드가 `SteamNotRunning`을 알리듯 `IUnavailableReasonBackend`를 구현합니다. `LobbyDataKeys`는 매니저가 `mu3.` 접두사로 예약해 자기가 만드는 모든 로비에 쓰는 로비 데이터 키를 담으며, 백엔드는 이 키를 다른 로비 데이터처럼 저장하며, 읽을 수도 있습니다. `SearchLobbies`가 반환된 뒤에 검색을 시작하는 백엔드는 `LobbySearchQuery.Clone()`으로 만든 쿼리 사본을 보관합니다
- **ObjectPool**: 중복된 비활성 오브젝트 재등록 방지, `List<T>` 일괄 enqueue 및 count/인자 목록 기반 `List<T>` 반환 dequeue, 선택적 무인자 생성 콜백과 타입 안전한 `CreateArguments` 초기화 콜백, `Clear()` 정리를 지원하는 큐 기반 오브젝트 풀링
- **Observable**: 데이터 변경 감지 시스템
- **Preference**: PlayerPrefs 래퍼
- **Resource**: Resources 폴더 로딩
- **Scene**: phase/status 조회, lifecycle/progress callback, 일회성 lifecycle 구독 helper, 통합 rejection event를 제공하는 씬 로딩 추상화
- **UI**: MVP 패턴 구현, 축별 일괄/단일 분할선으로 자식을 9개 영역 중 하나에 anchor로 맞추는 `UIAreaGrid`/`UIAreaElement`, canvas를 화면 safe area 안에 유지하는 `SafeCanvas`/`SafeRect`
- **IS**: 별도의 Input System 패키지로 옮겨짐 ([README](../../packages/inputsystem/README.md))
- **Utility**: Singleton, EasingFunctions, Settings, 카메라 뷰포트에 `SpriteRenderer` 배경을 맞추는 `Mu3Library.Utility.WorldSpaceBackground`, 게임이 도는 동안 매 프레임 직접 확인하지 않고 크기나 safe area가 바뀐 화면을 따라가는 `Mu3Library.Utility.ScreenChangeNotifier`
- **WebRequest**: HTTP 요청 관리

## 🧱 URP와 Watermelon Game 패키지

URP와 Watermelon Game 패키지의 주요 기능을 아래에 패키지별로 정리했습니다:

URP 패키지 (**Mu3 Library URP**):
- **Camera Stack Helper**: `Mu3Library.URP.Cam.CameraStackSetter`가 `SetCameraStackToMain(...)` 및 `SetCameraStack(...)` 헬퍼를 제공하여 임의의 URP overlay camera를 `Camera.main` 또는 명시적인 root camera stack에 넣고, 필요하면 삽입 인덱스를 제어할 수 있습니다.

Watermelon Game 패키지 (**Mu3 Library Game - Watermelon**):
- **Fruit item indexes**: 보드 설정은 항상 11개의 과일 항목을 포함하며 0부터 시작하는 목록 index로 접근합니다.
- **Item contact area**: 보드 기준 리사이즈가 아이템 collider 크기를 맞추므로 어떤 `BoardConfig`에서도 아이템 index별 접촉 영역이 같습니다. `BoardItemInfo.ColliderScale`은 collider가 sprite에서 차지하는 비율을, `BoardItemInfo.ColliderOffset`은 collider 지름 대비 비율로 그 중심을 정하며, sprite는 collider를 감싸도록 그려집니다.
- **Board fitting**: `BoardArea.Fit(...)`으로 설정된 카메라 뷰포트 padding 안에 보드 스프라이트를 맞출 수 있습니다.
- **Board collision boundaries**: `BoardArea.SetOutColliders()`로 아이템 영역 viewport padding 기준의 left, right, bottom `BoxCollider2D` 경계를 생성·갱신할 수 있으며 top은 열려 있습니다.
- **Board-relative coordinate conversions**: `BoardArea`는 world, screen, local 정규화 위치 변환과 초기화 안전성을 위한 `Try...` 변형을 제공합니다.
- **Board input**: `InputHandler`는 Input System 패키지가 설치되어 있고 Active Input Handling이 그 백엔드를 켜면 Input System을, 그 밖에는 Input Manager를 읽습니다. UI 위에서 누른 입력은 드래그를 시작하지 않고, `InputHandler.DragStartFilter`로 프로젝트 자체 규칙에 따라 누름을 막을 수 있으며, 놓기 전에 빼앗긴 드래그는 들고 있는 아이템을 손에 둔 채 취소됩니다.
- **Merge effects**: `BoardItemInfo`는 선택적 병합 효과용 `ParticleHandler` 리소스를 지원하며, 완료된 생성 효과 인스턴스는 자동으로 정리됩니다.
- **Scoring extension point**: `BoardItemScoreRule.GetScore(int)`를 override하여 기본 삼각수 점수 progression을 커스터마이즈할 수 있습니다.
- **Board commands**: `BoardController.EnqueueCommand(...)`는 보드의 공개 커맨드 큐에 커맨드를 넣습니다. `IUpdatableBoardCommand`와 `ICancelableBoardCommand`로 커맨드를 프레임 단위로 실행하고 취소할 수 있으며, `BoardCommand`는 그 둘을 lifecycle hook 위에 구성합니다. `BoardController.CommandContext`는 외부 커맨드가 아이템·점수·사운드에 접근하는 좁은 표면이고, 패키지는 `MergingCommand`를 포함한 Flow/Item/Score 커맨드를 제공합니다.
- **Board sound volumes**: `BoardController.SfxVolume`과 `BoardController.BgmVolume`으로 `BoardConfig.SoundConfig`와 별도로 보드 재생 볼륨을 설정할 수 있으며, 두 값 모두 0–1 범위로 제한됩니다.

새 helper로 MVP render camera를 스택에 넣으려면 render camera를 직접 전달하세요:

```csharp
using Mu3Library.URP.Cam;

CameraStackSetter.SetCameraStack(targetCamera, _mvpManager.RenderCamera);
```

## 🏗 아키텍처 개요

코어와 각 코어가 소유하는 컨테이너, 한 코어가 다른 코어의 서비스에 접근하는 방법은 컨테이너와 함께 DI 패키지로 옮겨졌습니다. 그 README의 [Cores and CoreRoot](../../packages/di/README.md#cores-and-coreroot)를 참고하세요.

## 📝 최근 업데이트

- [변경 이력](../changelog/CHANGELOG.ko.md)에는 모든 패키지 버전의 릴리스 노트가 패키지와 버전마다 헤더를 두고 실려 있고, 아직 릴리스되지 않은 변경은 Unreleased 아래에 있습니다.
- 설치 방법의 설치 URL은 각 패키지의 최신 릴리스 태그를 가리킵니다. 각 패키지의 버전과 의존성은 그 패키지의 `package.json`에 있습니다.

## 🤝 기여

이슈와 풀 리퀘스트를 환영합니다! 다음 사항을 참고해주세요:
- **코딩 스타일**: 프라이빗 필드는 `_camelCase`, Allman 브레이스 사용
- **커밋 메시지**: 명확한 설명 (예: `feat: Add retry logic to WebRequest`)

## 📄 라이선스

이 프로젝트는 MIT 라이선스를 따릅니다.

## 📞 문의

- **GitHub Issues**: [https://github.com/doqltl179/Mu3Library_ForUnity/issues](https://github.com/doqltl179/Mu3Library_ForUnity/issues)
- **Author**: Mu3 ([GitHub](https://github.com/doqltl179))

---

**패키지 정보:**
- Base: `com.github.doqltl179.mu3library.base`
- Notifications: `com.github.doqltl179.mu3library.notifications`
- Input System: `com.github.doqltl179.mu3library.inputsystem` (Base 의존)
- Localization: `com.github.doqltl179.mu3library.localization` (Base 의존)
- Addressables: `com.github.doqltl179.mu3library.addressables` (Base 의존)
- DI: `com.github.doqltl179.mu3library.di` (Base 의존)
- Steam: `com.github.doqltl179.mu3library.steam` (Base 의존, Steamworks.NET 필요)
- URP: `com.github.doqltl179.mu3library.urp` (Base 의존)
- Watermelon Game: `com.github.doqltl179.mu3library.game.watermelon` (Base, URP 의존)
- 버전과 릴리스 노트: [변경 이력](../changelog/CHANGELOG.ko.md)

Unity 개발자를 위해 제작됨
