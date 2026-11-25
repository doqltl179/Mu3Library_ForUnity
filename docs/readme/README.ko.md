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
- 🖥 **디버그 콘솔**: 타입 지정 인자를 받는 명령, 개발자 전용 접근과 플레이어용 접근, 진행에 영향을 주는 명령의 표시, Unity 로그를 갖춘 플레이어 빌드용 게임 내 콘솔
- 🔀 **상태 머신**: 게임, UI, AI 흐름을 위한 키 기반 상태. 코루틴처럼, 또는 UniTask가 있으면 비동기 메서드로 기다릴 수 있는 진입과 종료, 전환이 기다리는 동안 순서대로 대기하는 요청, 가드, 이전 상태로 돌아가기를 제공
- 💾 **세이브 시스템**: 데이터 버전과 마이그레이션, 안전하게 기록되고 무결성을 검사하는 파일, `Vector3` 같은 Unity 타입을 그대로 저장하는 기능을 갖추고 섹션 단위로 슬롯에 저장하는 세이브와, 각 플레이어의 데이터를 그 ID 아래에 두고 호스트가 다른 플레이어와 공유할 수 있는 멀티플레이 세션 세이브
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
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/base#base/v0.32.0

    # Notifications 패키지
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/notifications#notifications/v0.1.0

    # Input System 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/inputsystem#inputsystem/v0.1.0

    # Localization 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/localization#localization/v0.1.0

    # Addressables 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/addressables#addressables/v0.2.0

    # DI 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/di#di/v0.2.0

    # Steam 패키지 (먼저 Steamworks.NET과 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/steam#steam/v0.4.0

    # URP 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/urp#urp/v0.5.0

    # Watermelon Game 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/game/watermelon#game/watermelon/v0.8.1
   ```

URP와 Watermelon Game은 각각 Base에 의존하므로, 어느 쪽이든 Base를 먼저 설치합니다. Watermelon Game에는 URP가 필요하지 않습니다.

Notifications 패키지는 Android와 iOS의 로컬 알림을 담습니다. 다른 Mu3Library 패키지는 필요하지 않습니다.

Input System 패키지는 `InputSystemManager`와 그 이름 내보내기 드로어를 담습니다. 먼저 Base를 설치하세요.

Localization 패키지는 `LocalizationManager`와 그 데이터 내보내기, 문자 수집 드로어를 담습니다. 먼저 Base를 설치하세요.

Addressables 패키지는 `AddressablesManager`와 그 그룹 데이터 내보내기 드로어를 담습니다. 먼저 Base를 설치하세요.

DI 패키지는 `CoreBase` 컨테이너와 그 link.xml 처리기, 검증 드로어와 런타임 진단 드로어를 담습니다. 먼저 Base를 설치하세요.

Steam 패키지는 멀티플레이가 없는 게임을 위해 Steam을 시작하는 `SteamManager`, Steam에서 멀티플레이 세션을 실행하는 `SteamBackend`, 선택적인 디버그 콘솔 명령을 갖춘 Steam 업적과 통계, 그리고 세이브 시스템용 Steam Cloud 저장소를 담습니다. 먼저 Steamworks.NET과 Base를 설치하세요.

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
    - `packages/game/watermelon/package.json` (먼저 Base 설치)

## 📚 핵심 모듈

### DI (Dependency Injection)
`CoreBase`와 `CoreRoot`를 포함한 DI 컨테이너는 별도의 DI 패키지로 옮겨졌습니다. 등록, 주입, 코어, 그리고 관리 코드를 스트리핑하는 플레이어 빌드를 위해 이 패키지가 만드는 link.xml은 그 [README](../../packages/di/README.md)를 참고하세요. Base는 매니저가 구현하는 계약 `IInitializable`, `IUpdatable`, `ILateUpdatable`, `IObjectInjector`, `[Inject]`를 `Mu3Library.Foundation` 어셈블리에 유지합니다. 아래 예제는 DI 패키지의 코어처럼 `[Inject]`로 Base의 서비스를 주입합니다.

### MVP (Model-View-Presenter)
UI를 View, Presenter, Model로 분리하여 비즈니스 로직을 테스트 가능하게 만듭니다. view는 컴포넌트이므로 클래스 이름을 딴 파일 `MainMenuView.cs`에 두며, 아래의 `UICore`와 `UIBootstrap`도 마찬가지입니다.

```csharp
using Mu3Library.Audio;
using Mu3Library.DI;
using Mu3Library.UI.MVP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Arguments: Open할 때마다 전달하는 데이터
public class MainMenuArgs : Arguments
{
    public string PlayerName;
}

// Model: 데이터 컨테이너. presenter의 LoadFunc()가 실행되기 전에 인자로 채워짐
public class MainMenuModel : Model<MainMenuArgs>
{
    public string PlayerName { get; private set; }

    // 기본 Init은 비어 있으므로, 모델에 필요한 값을 여기서 인자로부터 복사합니다
    public override void Init(MainMenuArgs args)
    {
        PlayerName = args?.PlayerName;
    }
}

// View: Unity 컴포넌트 참조
public class MainMenuView : View
{
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private AudioClip _clickClip;

    public Button StartButton => _startButton;
    public TextMeshProUGUI TitleText => _titleText;
    public AudioClip ClickClip => _clickClip;
}

// Presenter: 비즈니스 로직
public class MainMenuPresenter : Presenter<MainMenuView, MainMenuModel, MainMenuArgs>
{
    // DI 패키지 README에 나오는, AudioManager를 등록하는 코어 AudioCore에서 받습니다.
    // 이 멤버는 필수이므로 씬에 AudioCore가 없으면 Open이 예외를 던집니다
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

    protected override void UnloadFunc()
    {
        // 닫힌 presenter는 view와 함께 풀에 들어가고, 다음 Open이 그 presenter에서 LoadFunc()를 다시 실행합니다
        _view.StartButton.onClick.RemoveListener(OnStartClicked);
    }

    private void OnStartClicked()
    {
        _audioManager.PlaySfx(_view.ClickClip);
        // 게임 시작 로직
    }
}
```

`MVPManager`는 프리팹을 가진 view 타입의 presenter만 엽니다. 루트에 view 컴포넌트가 있는 view 프리팹을 첫 `Open` 전에 각각 `RegisterViewResource`나 `RegisterViewResources`로 등록하세요. 루트에 `MainMenuView`를 추가하면 그 view에 필요한 `Canvas`, `CanvasScaler`, `GraphicRaycaster`, `CanvasGroup`도 함께 추가됩니다. 등록된 프리팹이 없으면 `Open`은 `View not found`를 로그로 남기고 null을 반환합니다. `Open`은 `Init(args)`로 presenter의 모델을 만들고, view가 아직 숨겨져 있는 동안 `LoadFunc()`를 호출합니다. view는 다음 `MVPManager.Update()`에서 보이며 열리기 시작하고, 그 뒤의 업데이트가 열림을 마치고 창이 닫힌 뒤에는 그 창을 unload하므로, 무언가가 매 프레임 `Update()`를 호출해야 합니다.

DI 패키지를 쓰면 코어가 매니저를 등록하고, `CoreRoot`가 매 프레임 그 `Update()`를 호출하며 코어가 파괴될 때 매니저를 해제합니다. `MVPManager`를 `CoreBase`를 통해 등록하면 presenter의 `[Inject]` 필드와 프로퍼티가 초기화 전에 채워집니다:

```csharp
using Mu3Library.DI;
using Mu3Library.UI.MVP;
using UnityEngine;

public class UICore : CoreBase
{
    [SerializeField] private MainMenuView _mainMenuViewPrefab;

    [Inject] private IMVPManager _mvpManager;

    protected override void ConfigureContainer()
    {
        RegisterClass<MVPManager>();
    }

    protected override void Start()
    {
        base.Start(); // _mvpManager를 주입합니다

        _mvpManager.RegisterViewResource(_mainMenuViewPrefab);
        _mvpManager.Open<MainMenuPresenter>(new MainMenuArgs { PlayerName = "Player1" });
    }
}
```

DI 패키지의 `CoreBase` 없이 쓸 때는 매니저를 만든 코드가 매 프레임 그 `Update()`를 호출하고, 다 쓰고 나면 `Dispose()`를 호출합니다. 이런 매니저에는 `IObjectInjector`를 넘겨주는 곳이 없어 presenter의 `[Inject]` 멤버가 채워지지 않으므로, 이때는 `MainMenuPresenter`에서 `[Inject]` 필드를 `CoreBase`를 상속하는 그 `AudioCore`와 함께 빼고, `PlaySfx` 호출도 빼세요:

```csharp
using Mu3Library.UI.MVP;
using UnityEngine;

public class UIBootstrap : MonoBehaviour
{
    [SerializeField] private MainMenuView _mainMenuViewPrefab;

    private MVPManager _mvpManager;

    private void Awake()
    {
        _mvpManager = new MVPManager();
        _mvpManager.RegisterViewResource(_mainMenuViewPrefab);
    }

    private void Start()
    {
        _mvpManager.Open<MainMenuPresenter>(new MainMenuArgs { PlayerName = "Player1" });
    }

    private void Update()
    {
        _mvpManager.Update();
    }

    private void OnDestroy()
    {
        _mvpManager.Dispose();
    }
}
```

presenter를 열 때마다 달라지는 데이터는 `Arguments`에 전달합니다.

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

### 상태 머신
`StateMachine<TKey>`(`Mu3Library.States`)는 게임이 키(보통 enum) 아래에 등록한 상태 사이를 오갑니다. 부팅에서 타이틀, 로비, 게임, 결과로 이어지는 게임 흐름이나 UI 흐름, 적의 행동 같은 것입니다. 상태는 보통 `State`의 하위 클래스이며 필요한 것만 재정의합니다. 코루틴처럼 `yield`로 기다릴 수 있는 `Enter`와 `Exit`, 그리고 `Update`입니다. 머신은 이 루틴을 자기 `Update`에서 진행하므로 `null`, 중첩된 루틴, `WaitUntil`이나 `WaitForSecondsRealtime` 같은 `CustomYieldInstruction`, `AsyncOperation`을 기다리지만, `WaitForSeconds`와 그 밖의 `YieldInstruction`은 경고와 함께 한 프레임만 기다립니다. 시간을 기다리려면 대신 `WaitForSecondsRealtime`이나 `WaitUntil`을 쓰세요. 전환은 현재 상태에서 나가며 그 `Exit`가 끝날 때까지 기다리고, 새 상태에 들어가며 그 `Enter`가 끝날 때까지 기다린 뒤 `OnStateChanged`를 발생시킵니다. 아무것도 기다리지 않는 전환은 `ChangeState` 안에서 끝납니다. 전환이 기다리는 동안 들어온 요청은 들어온 순서대로 하나씩 실행됩니다. `AddGuard`는 전환을 막을 수 있는 조건을 추가하며, 막힌 전환은 `OnTransitionBlocked`가 알립니다. `Back`은 이전 상태로 한 단계 돌아갑니다. 머신은 이력을 보관하지 않으므로, 두 번째 `Back`은 첫 번째 `Back`이 떠나온 상태로 돌아갑니다. 소유자는 매 프레임 `Update`를 호출하며, `Update`는 기다리는 전환이 있으면 그 전환을 진행하고, 없으면 현재 상태를 업데이트합니다. UniTask가 있으면 `AsyncState`를 상속한 상태는 대신 `EnterAsync`와 `ExitAsync`를 await합니다. 머신은 어떤 씬 로더나 UI 시스템에도 묶여 있지 않습니다. 다음 예에서는 게임 흐름이 `SceneLoader`로 씬을 로드합니다.

```csharp
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks; // AsyncSceneState에서만 씁니다
using Mu3Library.Scene;
using Mu3Library.States;
using UnityEngine;

public enum GameFlow { Boot, Title, Game }

public class SceneState : State
{
    private readonly ISceneLoader _sceneLoader;
    private readonly string _sceneName;

    public SceneState(ISceneLoader sceneLoader, string sceneName)
    {
        _sceneLoader = sceneLoader;
        _sceneName = sceneName;
    }

    // 다음 상태는 씬이 로드된 뒤에 들어옵니다. 루틴은 null, 중첩된 루틴,
    // WaitUntil이나 WaitForSecondsRealtime 같은 CustomYieldInstruction, AsyncOperation을 yield할 수 있습니다.
    // WaitForSeconds는 한 프레임만 기다립니다.
    public override IEnumerator Enter()
    {
        _sceneLoader.LoadSingleScene(_sceneName);
        yield return new WaitUntil(() => !_sceneLoader.IsLoading);
    }
}

// UniTask가 있으면 같은 상태가 대신 await합니다.
public class AsyncSceneState : AsyncState
{
    private readonly ISceneLoader _sceneLoader;
    private readonly string _sceneName;

    public AsyncSceneState(ISceneLoader sceneLoader, string sceneName)
    {
        _sceneLoader = sceneLoader;
        _sceneName = sceneName;
    }

    public override async UniTask EnterAsync(CancellationToken cancellationToken)
    {
        await _sceneLoader.LoadSingleSceneAsync(_sceneName);
    }
}

// 소유자는 코어나 DontDestroyOnLoad로 유지하는 오브젝트처럼, 자기가 로드하는 씬보다 오래 남습니다.
_flow = new StateMachine<GameFlow>();
_flow.Add(GameFlow.Boot, new SceneState(_sceneLoader, "Boot"));
_flow.Add(GameFlow.Title, new SceneState(_sceneLoader, "Title"));
_flow.Add(GameFlow.Game, new SceneState(_sceneLoader, "Game"));
_flow.AddGuard(transition => transition.To != GameFlow.Game || _isSignedIn);
_flow.OnStateChanged += transition => Debug.Log($"Flow: {transition}");
_flow.ChangeState(GameFlow.Boot);

// 매 프레임.
_flow.Update();

// 나중에: 부트 씬이 아직 로드되는 동안 한 요청은 자기 차례를 기다립니다.
_flow.ChangeState(GameFlow.Title);
_flow.Back(); // 현재 상태 직전의 상태로

// 소유자가 사라질 때.
_flow.Dispose();
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

### 세이브 시스템
게임을 섹션 단위로 저장하고 불러옵니다. 각 시스템이나 스테이지가 게임에서 자기가 맡은 부분을 섹션 하나로 기술하고, 게임은 그 순간의 세부 사항을 모두 저장하는 대신 체크포인트나 스테이지 끝처럼 자기가 고른 시점에 섹션을 저장하고 불러옵니다. `Mu3Library.Save` 어셈블리에 있으며, 자체 어셈블리 정의에서 이 어셈블리를 참조해 씁니다.

```csharp
using System;
using Mu3Library.Save;
using UnityEngine;

[Serializable]
public class PlayerSaveData
{
    public Vector3 Position;
    public int Level;
}

public class PlayerSaveSection : SaveSection<PlayerSaveData>
{
    private readonly Transform _player;
    private readonly PlayerStats _stats;

    public override string Key => "player";
    public override int Version => 2;

    public PlayerSaveSection(Transform player, PlayerStats stats)
    {
        _player = player;
        _stats = stats;
    }

    protected override PlayerSaveData Capture() =>
        new PlayerSaveData { Position = _player.position, Level = _stats.Level };

    protected override void Restore(PlayerSaveData data)
    {
        _player.position = data.Position;
        _stats.Level = data.Level;
    }

    // 버전 1은 레벨을 저장하지 않았으므로, 그 세이브에서는 레벨이 0으로 읽힙니다.
    protected override PlayerSaveData Migrate(int savedVersion, PlayerSaveData data)
    {
        if (savedVersion < 2)
        {
            data.Level = 1;
        }

        return data;
    }
}

// Application.persistentDataPath 아래 Saves 폴더의 파일에 JSON으로 씁니다.
var saves = new SaveManager();
saves.RegisterSection(new PlayerSaveSection(playerTransform, playerStats));
saves.RegisterSection(SaveSection.Create("stage", () => _stage, data => _stage = data));

saves.Save("slot1");            // 등록된 모든 섹션
saves.Save("slot1", "stage");   // 섹션 하나. 슬롯의 다른 섹션은 그대로 남습니다

saves.SetMetadata("slot1", "chapter", "3. The Forest");   // 슬롯 목록용

SaveResult result = saves.Load("slot1");
if (!result.IsSuccess)
{
    Debug.LogWarning($"Load failed: {result}");   // 실패한 이유는 result.Reason에 있습니다
}
```

- **섹션과 슬롯**: 슬롯은 섹션 키마다 항목 하나를 담습니다. 섹션 하나를 저장해도 슬롯의 다른 섹션은 그대로 남고, 슬롯에 있지만 어떤 코드도 등록하지 않은 섹션도 건드리지 않고 남겨 둡니다. 슬롯 전체를 불러오면 먼저 등록된 모든 섹션을 읽고 검사하고 마이그레이션하므로, 손상되었거나 너무 새로운 섹션이 있거나 마이그레이션이 예외를 던지면 어느 섹션도 복원하지 않습니다. 그다음 슬롯을 저장한 뒤 게임에 추가한 섹션처럼 슬롯에 없는 등록된 섹션마다 `RestoreDefault`를 호출합니다. 손상된 슬롯에 섹션 하나를 저장하면 `SaveFailureReason.Corrupted`로 실패하지만, 슬롯 전체를 저장하면 원래 담긴 내용을 읽을 수 없으므로 등록된 섹션만으로 슬롯을 새로 씁니다.
- **데이터**: 기본 `JsonSaveSerializer`는 섹션의 데이터를 `JsonUtility`로 쓰므로 `Vector3`, `Quaternion`, `Color` 같은 Unity 타입이 그대로 저장됩니다. 데이터는 `[Serializable]` 클래스나 구조체입니다. 기본형, 문자열, 컬렉션을 그 자체로 넘기면 거부되므로 필드 안에 넣으세요. `JsonUtility`는 딕셔너리와 프로퍼티를 쓰지 않습니다.
- **버전**: 섹션 데이터의 형태가 바뀌면 그 섹션의 `Version`을 올립니다. 이전 버전으로 저장된 슬롯은 `Migrate`를 거칩니다. `Migrate`는 현재 타입으로 읽어 들인 데이터를 받으며, 그 데이터에서 이전 버전에 없던 필드는 기본값을 가집니다. 또 `Migrate`는 어느 섹션도 복원되기 전에 실행되므로 게임을 건드려서는 안 됩니다. 더 새로운 버전으로 저장된 슬롯은 `SaveFailureReason.VersionTooNew`로 실패하며, 그 위에 이전 버전을 쓰려는 저장도 마찬가지이므로, 버전을 되돌린 빌드에서도 새 버전이 저장한 것은 사라지지 않습니다. 더 새로운 슬롯 형식으로 쓰인 슬롯도 같은 방식으로 거부됩니다.
- **저장소**: `FileSaveStorage`는 각 슬롯을 `<slot ID>.sav`에 보관하며, 임시 파일에 먼저 쓴 뒤 그 파일로 기존 파일을 바꾸므로, 쓰기가 도중에 멈춰도 이전 세이브를 읽을 수 있습니다. `MemorySaveStorage`는 슬롯을 메모리에 보관합니다. 플랫폼의 클라우드 저장소처럼 다른 곳에 보관하려면 `ISaveStorage`를 구현하세요. 각 슬롯에는 CRC-32 검사값이 붙어 있어, 손상된 파일은 `SaveFailureReason.Corrupted`로 실패합니다.
- **슬롯 목록**: `GetSlots`와 `TryGetSlotInfo`는 아무것도 복원하지 않고 슬롯의 섹션, 버전, 시간, 크기, 메타데이터를 읽습니다. 손상되었거나 너무 새로운 슬롯은 빠집니다. `SetMetadata`는 이런 목록에 보여 줄 챕터 이름이나 플레이 시간 같은 짧은 값을 저장된 슬롯에 설정하며, 256자 이하의 값을 32개까지 둘 수 있고 다시 저장해도 유지됩니다. `Peek`은 섹션 하나의 데이터를 복원하지 않고 읽습니다. `ExportSlot`과 `ImportSlot`은 백업처럼 슬롯의 검사된 바이트를 내보내고 다시 받아들이며, 모든 길이를 읽기 전에 검사하므로 다른 곳에서 온 바이트도 안전하게 파싱할 수 있습니다. 다만 그 안의 섹션은 보낸 쪽이 쓴 그대로 복원됩니다. `MaxSlotSize`(기본 8 MiB)는 모든 섹션을 포함한 슬롯 하나의 크기를 제한합니다.
- 슬롯 ID에는 문자, 숫자, `_`, `-`를 64자까지 쓸 수 있지만, `CON`처럼 Windows가 장치 이름으로 예약한 이름은 쓸 수 없습니다. 섹션 키와 메타데이터 키에는 `.`도 쓸 수 있습니다. 대소문자를 구분하지 않는 파일 시스템에서는 대소문자만 다른 두 ID가 한 슬롯이 됩니다. 호출은 호출한 스레드에서 실행되며, 저장, 불러오기, 저장소의 실패는 `SaveResult`로 돌아옵니다. 예외는 null 인자나 0 이하의 `MaxSlotSize`처럼 호출하는 코드의 실수에만 던져집니다. `ISaveManagerEventBus`는 `OnSaved`, `OnLoaded`, `OnSlotDeleted`, `OnSlotImported`를 발생시킵니다.

### 디버그 콘솔
플레이어 빌드용 게임 내 콘솔입니다. 게임이 등록한 명령, 실행한 줄, Unity 로그를 게임 위에 그립니다. `Mu3Library.DebugConsole` 어셈블리에 있으며, 자체 어셈블리 정의에서 이 어셈블리를 참조해 씁니다.

```csharp
using Mu3Library.DebugConsole;
using UnityEngine;

public class ConsoleSetup : MonoBehaviour
{
    private DebugConsoleManager _console;

    private void Awake()
    {
        _console = new DebugConsoleManager();   // 살아 있는 동안 Unity 로그를 수집합니다

        // 개발자 명령은 에디터와 Development Build에서만 실행됩니다.
        _console.RegisterCommand<int>("level", level => LoadLevel(level),
            new ConsoleCommandOptions("Loads a level.", affectsProgress: true));

        // 플레이어 명령은 게임이 의도적으로 플레이어에게 제공하는 명령입니다.
        _console.RegisterCommand<int, int>("give", GiveItem,
            new ConsoleCommandOptions("Gives an item.", ConsoleAccess.Player, affectsProgress: true));

        // 릴리스 빌드에서도 플레이어가 콘솔을 열 수 있게 합니다. 쓸 수 있는 것은 Player 명령뿐입니다.
        _console.PlayerConsoleEnabled = _settings.AllowPlayerConsole;
        _console.OnProgressModified += commandName => _saveData.UsedConsole = true;

        DebugConsoleView.Create(_console);   // ` 키로 엽니다
    }

    private void GiveItem(int item, int count = 1) { /* ... */ }

    private void OnDestroy() => _console.Dispose();
}
```

- **접근**: 기본값인 `Developer` 명령은 에디터와 Development Build에서만 실행됩니다. `Player` 명령은 편의 기능이나 게임이 허용하는 치트처럼 게임이 의도적으로 플레이어에게 제공하는 명령으로, 게임이 `PlayerConsoleEnabled`를 켜면 릴리스 빌드에서도 실행됩니다. 릴리스 빌드는 그때까지 콘솔을 닫아 두며, 그 빌드에서 실행할 수 있는 명령만 나열하고 자동 완성하고 실행합니다. `Developer` 명령에는 알 수 없는 명령이라고 응답합니다.
- **진행**: `affectsProgress`로 표시한 명령은 실행되면, 그 뒤에 예외를 던지더라도 `HasModifiedProgress`를 설정하고 `OnProgressModified`를 발생시키므로, 게임은 자기 규칙에 따라 세이브에 표시를 남기거나 업적을 보류할 수 있습니다. `ResetProgressModified`는 그 표시를 지웁니다.
- **인자**: 명령은 타입 지정 매개변수를 네 개까지 받습니다. `string`, `int`, `long`, `float`, `double`, `bool`(`true`, `false`, `on`, `off`, `yes`, `no`, `1`, `0`), 이름이나 숫자로 받는 enum, `x,y`와 `x,y,z`로 받는 `Vector2`와 `Vector3`입니다. 메서드의 기본값이 있으면 마지막 매개변수들을 생략할 수 있으며, 벡터의 `default`는 0 벡터로 읽히고, 큰따옴표로 묶은 단어는 공백을 유지합니다. `Action<ConsoleCall>`로 등록한 명령은 입력된 그대로의 단어를 받으며 `Print`로 출력할 수 있습니다. 이름은 대소문자를 구분하지 않고 일치시키며, `help`와 `clear`가 기본으로 들어 있습니다.
- **화면**: `DebugConsoleView`는 어느 입력 백엔드에서든 키보드를 읽는 IMGUI로 콘솔을 그립니다. 전환 키(기본 `` ` ``)는 콘솔을 열고 닫으며, Enter는 줄을 실행하고, Up과 Down은 이력을 오가며, Tab은 명령 이름을 자동 완성하고, Escape는 콘솔을 닫습니다. 키보드가 없는 기기에서는 게임 자체의 버튼이나 제스처에서 `Open`을 호출하세요. 키 입력은 게임 자체의 입력에도 전달되므로, `OnOpenChanged`에서 처리하는 식으로 콘솔이 열려 있는 동안에는 게임의 입력을 멈추세요. 화면은 Unity 로그를 레벨별로 거르며, 콘솔은 최신 `MaxLines`줄(기본 1000줄)을 보관합니다.
- `Execute`는 코드에서 줄 하나를 실행하고, 줄이 실패한 이유를 알려 주는 `ConsoleResult`를 반환하며, 예외를 던진 명령은 그 예외도 로그로 남깁니다. `IDebugConsoleManagerEventBus`는 `OnLineAdded`, `OnCleared`, `OnOpenChanged`, `OnProgressModified`, `OnExecuted`를 발생시킵니다. 로그 수집을 멈추려면 매니저를 해제하세요.

### Multiplayer
`MultiplayerManager`(`Mu3Library.Multiplayer`)는 플레이어 호스트 세션을 실행합니다. 한 플레이어가 호스트를 맡고, 다른 플레이어는 호스트의 로비를 찾아 호스트에 연결하며, 참가자 목록과 규칙, 게임 시작 시점은 호스트가 정합니다. 매니저는 백엔드 위에서 동작합니다. 백엔드는 한 프로세스 안의 여러 플레이어나, `LocalNetwork.CreateMachineWide`로 만든 네트워크에서 같은 PC의 에디터와 플레이어 빌드를 위한 `LocalBackend`(`Mu3Library.Multiplayer.Local`), 또는 별도의 [Steam 패키지](../../packages/steam/README.md)에 있는 `SteamBackend`입니다. 자체 어셈블리 정의에 있는 코드는 `Mu3Library.Multiplayer`를 참조해야 매니저를 쓸 수 있습니다. 모든 처리는 메인 스레드에서 실행되며, 백엔드가 알리는 내용, 다른 플레이어의 메시지, 로비 요청에 대한 응답은 `Update` 안에서 도착하므로 매 프레임 `Update`를 호출하세요.

```csharp
using System;
using System.Text;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using UnityEngine;

public class SessionRunner : MonoBehaviour
{
    private const ushort MoveMessage = 1;

    private IMultiplayerBackend _backend;
    private MultiplayerManager _manager;
    private readonly MessageWriter _writer = new MessageWriter();

    private void Awake()
    {
        // 같은 PC의 플레이어들. Steam 게임은 SteamBackend.TryCreate로 백엔드를 만들거나,
        // 에디터에서는 LocalBackend로 대신하는 SteamBackend.CreateOrFallback으로 만듭니다.
        _backend = new LocalBackend(LocalNetwork.CreateMachineWide("my-game"), "Player");
        _manager = new MultiplayerManager();
        _manager.Initialize(_backend, new MultiplayerConfig("my.game", 1));

        // 모든 플레이어가 같은 메시지 종류를 같은 정책으로 등록합니다.
        _manager.RegisterMessage(MoveMessage, OnMove);

        _manager.OnGameStarted += startData =>
        {
            // 시작 데이터가 가리키는 레벨을 로드한 뒤, 이 플레이어가 플레이할 준비가 되었다고 호스트에 알립니다.
            _manager.ReportLoaded();
        };
        _manager.OnAllPlayersLoaded += () => Debug.Log("Every player loaded"); // 호스트에서만
    }

    private void Update() => _manager.Update();

    private void OnDestroy()
    {
        _manager.Dispose(); // 백엔드보다 먼저 해제합니다. 백엔드는 그것을 만든 쪽의 것입니다
        _backend.Dispose();
    }

    // 호스트가 로비를 만듭니다.
    public void Host()
    {
        LobbyCreateOptions options = new LobbyCreateOptions
        {
            Name = "My lobby",
            Visibility = LobbyVisibility.Public,
            Rules = SessionRules.Default.WithCapacity(4),
        };
        _manager.CreateLobby(options, result => Debug.Log($"Create lobby: {result}"));
    }

    // 클라이언트가 로비를 검색해 참가합니다.
    public void Join()
    {
        _manager.SearchLobbies(new LobbySearchQuery(), found =>
        {
            if (found.IsSuccess && found.Lobbies.Count > 0)
            {
                _manager.JoinLobby(found.Lobbies[0].Id, result => Debug.Log($"Join lobby: {result}"));
            }
        });
    }

    public void SetReady() => _manager.SetReady(true); // 로비에 있는 클라이언트

    // 규칙이 허락하면 호스트가 게임을 시작합니다. 결과에는 준비되지 않은 플레이어가 담깁니다.
    public void StartGame() => _manager.StartGame(Encoding.UTF8.GetBytes("Level1"));

    public void SendMove(Vector3 position)
    {
        _writer.Clear().WriteVector3(position);
        _manager.Send(MoveMessage, _writer.Written, MessageTarget.Others, DeliveryMode.Unreliable);
    }

    private void OnMove(MessageContext context, ReadOnlySpan<byte> payload)
    {
        MessageReader reader = new MessageReader(payload);
        Vector3 position = reader.ReadVector3();
        // context.Sender의 캐릭터를 그 위치로 옮깁니다.
    }

    public void ReturnToLobby() => _manager.EndGame(); // 호스트

    public void Quit() => _manager.Leave();
}
```

- **메시지 정책**: `MessagePolicy.Default`는 어느 플레이어든 그 종류를 보낼 수 있게 하며, 호스트는 클라이언트의 메시지를 자기 핸들러가 실행되기 전에 전달합니다. `HostOnly`는 점수나 스폰처럼 호스트만 정하는 것에 쓰며, 클라이언트가 보내면 호스트가 버립니다. `NoClientRelay`는 공격처럼 다른 누가 보기 전에 호스트가 확인해야 하는 클라이언트의 요청에 씁니다. 클라이언트는 이를 `MessageTarget.Host`로 보내고, 호스트의 핸들러가 받아들인 것을 전달합니다. 호스트가 정책을 적용하므로 모든 플레이어가 각 종류를 같은 정책으로 등록합니다.
- **전송 방식**: 기본값인 `DeliveryMode.Reliable`(신뢰 전송)은 같은 보낸 사람의 메시지끼리 보낸 순서대로 도착하므로, 잃어서는 안 되는 이벤트에 맞습니다. `Unreliable`(비신뢰 전송)은 손실되거나 순서가 바뀌어 도착할 수 있으므로, 매 프레임 보내는 위치처럼 다음 메시지가 대신하는 상태에 맞습니다.
- **송신량 제한**: `SessionRules`는 기본적으로 제한을 두지 않습니다. `MaxMessagesPerSecond`와 `MaxBytesPerSecond`는 호스트가 클라이언트 하나에게서 1초에 받는 양을 제한하며, 준비 상태 변경과 로드 보고를 포함해 클라이언트가 보내는 모든 패킷을 세므로, 게임이 가장 많이 보낼 때보다 높게 설정하세요. 위치를 1초에 30번 보내는 클라이언트는 `SessionRules.Default.WithCapacity(4).WithMaxMessagesPerSecond(60).WithMaxBytesPerSecond(16 * 1024)` 안에 들어갑니다. 호스트는 제한을 넘은 게임 메시지를 버리고 `OnRateLimitExceeded`를 발생시키며, `RateLimitKickSeconds`가 0보다 크면 그 초 수 동안 매초 연속으로 제한을 넘은 클라이언트를 강퇴합니다.
- **로드**: `StartGame` 뒤 각 플레이어는 레벨이 준비되면 `ReportLoaded`를 호출하고, 호스트는 `OnAllPlayersLoaded`를 전달받거나, `SessionRules.LoadTimeoutSeconds`(기본 60초)가 지나면 보고하지 않은 플레이어를 담은 `OnLoadTimedOut`을 전달받습니다. 한 게임 안에서 레벨을 바꾸는 게임은 호스트에서 다음 레벨 이름 같은 데이터로 `BeginLoadPhase`를 호출합니다. 그러면 모든 플레이어의 로드 완료 상태가 지워지고, 모든 플레이어가 그 데이터와 함께 `OnLoadPhaseBegan`을 전달받아 로드한 뒤 다시 보고하며, 호스트는 같은 규칙으로 다시 기다립니다. 로드 단계가 시작되기 전에 보낸 보고는 그 단계에서 인정되지 않습니다.

```csharp
// Awake에서 OnGameStarted 옆에: 호스트를 포함한 모든 플레이어가 새 단계마다 전달받습니다.
_manager.OnLoadPhaseBegan += phaseData =>
{
    // 단계 데이터가 가리키는 레벨을 로드한 뒤 다시 보고합니다.
    _manager.ReportLoaded();
};

// 게임이 진행되다가, 호스트가 모두를 다음 레벨로 옮깁니다.
_manager.BeginLoadPhase(Encoding.UTF8.GetBytes("Level2"));
```
- **음성**: `SteamBackend`와 `LocalBackend`는 이 플레이어의 음성을 녹음하고 다른 플레이어의 음성을 디코딩하는 `IVoiceBackend`(`Mu3Library.Multiplayer.Backend`)를 구현합니다. Steam에서는 Steam 음성을, 로컬 백엔드에서는 기본 마이크를 쓰므로 같은 코드가 에디터에서도 동작합니다. 게임은 음성을 보통 `Unreliable` 게임 메시지로 실어 보내고, 누가 들을지 정합니다. 보내는 쪽이 `MessageTarget`으로 정하거나, 근접 음성이나 무전 채널에 필요하듯 호스트가 `SetRelayFilter`로 메시지마다, 플레이어마다 정합니다. `VoicePlayer`는 화자마다 게임이 직접 준비한 `AudioSource`로 음성을 재생하며, `spatialBlend`와 롤오프 같은 그 `AudioSource`의 설정이 씬 안에서 음성이 어떻게 들릴지 정합니다. 음성도 송신량 제한에 포함됩니다. 로컬 백엔드의 음성은 녹음하는 동안 무음까지 포함해 1초에 16,000바이트를 씁니다.

```csharp
// 위 세션 코드에 더합니다. using System.Collections.Generic;도 추가하세요
private const ushort VoiceMessage = 2;
private IVoiceBackend _voice;
private readonly Dictionary<PlayerId, VoicePlayer> _voicePlayers = new Dictionary<PlayerId, VoicePlayer>();

// Awake에서 Initialize 뒤에.
_voice = _backend as IVoiceBackend;
_manager.RegisterMessage(VoiceMessage, OnVoice);
// 필터는 호스트에서만 적용됩니다. 멀리 있는 플레이어의 음성은 아무도 듣지 않습니다.
_manager.SetRelayFilter(VoiceMessage, (speaker, listener) => AreClose(speaker, listener));
_manager.OnParticipantLeft += (participant, reason) =>
{
    if (_voicePlayers.TryGetValue(participant.Id, out VoicePlayer player))
    {
        player.Dispose();
        _voicePlayers.Remove(participant.Id);
    }
};

// 눌러서 말하기.
public void SetTalking(bool talking)
{
    if (talking)
    {
        _voice.StartVoiceRecording();
    }
    else
    {
        _voice.StopVoiceRecording();
    }
}

// Update에서 _manager.Update() 뒤에: 지난 프레임 이후 녹음한 것을 보내고 재생합니다.
if (_voice.TryReadVoice(out ReadOnlySpan<byte> voice) && _manager.State == SessionState.InGame)
{
    _manager.Send(VoiceMessage, voice, MessageTarget.Others, DeliveryMode.Unreliable);
}

foreach (VoicePlayer player in _voicePlayers.Values)
{
    player.Update();
}

private void OnVoice(MessageContext context, ReadOnlySpan<byte> payload)
{
    if (!_voicePlayers.TryGetValue(context.Sender, out VoicePlayer player))
    {
        // 게임이 찾아오는, 화자 캐릭터에 붙은 AudioSource.
        player = new VoicePlayer(VoiceSourceOf(context.Sender), _voice.VoiceSampleRate);
        _voicePlayers.Add(context.Sender, player);
    }

    if (_voice.TryDecodeVoice(payload, out ReadOnlySpan<float> samples))
    {
        player.Write(samples);
    }
}
```
- **상태 동기화**: `StateSyncManager`(`Mu3Library.Multiplayer.Sync`)는 바이트 예산 안에서 게임 상태를 모든 플레이어에게 똑같이 유지하므로, 매 프레임 상태가 바뀌는 시스템이 저마다 자기 메시지를 보내지 않아도 됩니다. 각 시스템은 `SyncState` 하위 클래스나 직접 구현한 `ISyncState`에서 상태에 무엇을 담고 얼마나 정밀하게 쓸지를 정하며, 상태 전체를 쓰는 방법과, 재정의한다면 바뀐 것만 쓰는 방법을 둡니다. 얼마를 언제 보낼지는 매니저가 정합니다. 매니저는 `StateSyncConfig.TickIntervalSeconds`(기본 1초에 20번)마다 바뀐 상태를 플레이어와 전송 방식마다 메시지 하나로 모으고, 각 플레이어에게 최대 `MaxBytesPerTick`(기본 512바이트)만큼 보냅니다. 들어가지 못한 상태는 이후 틱을 기다리며 우선순위와 기다린 시간에 따라 선택되고, 변경을 놓쳤거나 게임 중에 들어왔거나 자기 자리로 돌아온 플레이어는 상태 전체를 받습니다. 호스트는 자기가 소유한 상태를 보내고, 클라이언트 자신의 캐릭터처럼 클라이언트가 소유한 상태는 같은 예산 안에서 그 클라이언트에서 호스트로 간 뒤 다른 플레이어에게 전달됩니다. 기본값인 비신뢰 전송 상태는 `FullStateIntervalSeconds`(기본 1초)마다 전체로도 보내지므로, 업데이트를 잃은 사본도 따라잡습니다. 신뢰 전송 상태는 문처럼 가끔 바뀌는 상태에 맞습니다. 관심 필터를 쓰면 호스트는 가까이 있는 플레이어처럼 자기가 고른 플레이어에게만 상태를 보낼 수 있습니다. `TransformSync`는 트랜스폼의 위치와 회전을 양자화해 동기화하고, 각 사본을 업데이트 사이에서 부드럽게 움직입니다. 매니저는 게임이 진행되는 동안(`SessionState.InGame`) 동기화합니다. `CheckRateLimits`는 클라이언트가 보내는 양이 세션의 송신량 제한 안에 드는지 알려 주며, 매니저도 게임이 시작될 때마다 이를 확인해 들지 않으면 로그로 남깁니다. Multiplayer Debugger는 각 틱이 플레이어마다 보낸 것을 보여 줍니다.

```csharp
// 위 세션 코드에 더합니다. using System.Collections.Generic;과 using Mu3Library.Multiplayer.Sync;도 추가하세요
private const ushort SyncMessage = 3;
private StateSyncManager _sync;
private readonly List<TransformSync> _transforms = new List<TransformSync>();

// Awake에서 Initialize 뒤에. 모든 플레이어가 같은 설정으로 만듭니다.
_sync = new StateSyncManager(_manager, new StateSyncConfig(SyncMessage));

// 게임이 시작되면 모든 플레이어가 같은 상태를 같은 ID로 등록합니다.
_manager.OnGameStarted += startData =>
{
    _transforms.Add(new TransformSync(1, _boss.transform));
    _sync.Register(_transforms[_transforms.Count - 1]); // 호스트가 보냅니다
    foreach (Participant participant in _manager.Participants)
    {
        // 시작 데이터에서의 순서처럼, 모든 플레이어가 이 플레이어의 캐릭터에 똑같이 붙이는 ID.
        TransformSync character = new TransformSync(CharacterIdOf(participant.Id), CharacterOf(participant.Id));
        _transforms.Add(character);
        _sync.Register(character, participant.Id); // 그 플레이어가 보내고, 호스트가 다른 플레이어에게 전달합니다
    }
};

// 게임이 끝나면 모든 플레이어가 등록을 해제하므로, 다음 게임은 자기 상태를 새로 등록합니다.
_manager.OnReturnedToLobby += ClearSyncedStates;
_manager.OnStateChanged += change =>
{
    if (change.Current == SessionState.Offline)
    {
        ClearSyncedStates();
    }
};

// Update에서 _manager.Update() 뒤에: 이번 틱의 변경을 보내고 사본을 움직입니다.
_sync.Update();
foreach (TransformSync synced in _transforms)
{
    synced.Update();
}

// OnDestroy에서 _manager.Dispose() 전에.
_sync.Dispose();

private void ClearSyncedStates()
{
    foreach (TransformSync synced in _transforms)
    {
        _sync.Unregister(synced.Id);
    }

    _transforms.Clear();
}
```
- `IMultiplayerManagerEventBus`에 모든 이벤트가 있고, `IMultiplayerManager`와 `IMultiplayerMessenger`의 XML 주석이 각 호출을 설명합니다. 에디터에서는 **Mu3Library > Windows > Multiplayer Debugger**가 플레이 모드에서 실행 중인 세션을 보여 줍니다.

#### 세션 세이브
`SessionSaveManager`(`Mu3Library.SessionSave`)는 호스트에서 `SaveManager`를 통해 세션을 저장하며, 호스트가 그 세이브를 다른 플레이어에게 보낼 수 있게 합니다. 받은 플레이어는 나중에 그 세이브로 세션의 호스트를 맡을 수 있습니다. `SaveManager`에 등록한 섹션은 월드처럼 세션 전체가 공유하는 것을 담고, 각 `PlayerSaveSection<TData>`나 `PlayerSaveSection.Create`로 만든 섹션은 세션의 플레이어마다 한 번씩 `player.<PlayerId>.<key>`로 저장되므로, 누가 호스트를 맡든 플레이어의 데이터는 그 플레이어의 것으로 남습니다. 자체 어셈블리 정의에 있는 코드는 `Mu3Library.SessionSave`, `Mu3Library.Save`, `Mu3Library.Multiplayer`를 참조합니다.

```csharp
using System;
using System.IO;
using Mu3Library.Save;
using Mu3Library.SessionSave;
using UnityEngine;

// 모든 플레이어가 같은 메시지 종류로 같은 방식으로 만듭니다. 공유는 이 종류와 그다음 종류를 씁니다.
var saves = new SaveManager(new FileSaveStorage(Path.Combine(Application.persistentDataPath, "SessionSaves")));
saves.RegisterSection(SaveSection.Create("world", () => _world, data => _world = data));

var session = new SessionSaveManager(_manager, saves, messageType: 900);
session.RegisterPlayerSection(PlayerSaveSection.Create<InventoryData>("inventory",
    player => _inventories[player],
    (player, data) => _inventories[player] = data,
    restoreDefault: player => _inventories[player] = new InventoryData()));

// 호스트에서, 게임이 고른 시점에. 매 프레임 호출하는 Update가 공유의 속도를 조절합니다.
session.Save("coop-1", sessionName: "Forest Run");
session.Load("coop-1");                          // StartGame 전에
session.Share("coop-1", friendId, out uint shareId);

// 호스트가 공유하는 상대 플레이어에서.
// AcceptShare는 그 ID의 슬롯을 덮어쓰므로, 받은 세이브에는 새 ID를 줍니다.
session.OnShareOffered += offer => session.AcceptShare(offer.ShareId, $"shared-{Guid.NewGuid():N}");
session.OnShareCompleted += transfer => Debug.Log($"Stored {transfer.SlotId}");
```

- **플레이어**: 저장과 불러오기는 호스트만 합니다. `Save`는 자리를 비운 동안 호스트가 자리를 유지하는 플레이어를 포함해 세션의 모든 플레이어를 쓰며, 슬롯에 있지만 세션에 없는 플레이어의 섹션은 슬롯에 그대로 남습니다. `Load`는 세션의 각 플레이어에게 자기 섹션을 주며, 저장된 세션에 처음 들어온 플레이어처럼 슬롯에 없는 플레이어는 `RestoreDefault`에서 시작합니다. `LoadPlayer`는 이미 진행 중인 게임에 들어온 플레이어처럼 플레이어 한 명을 불러옵니다. 플레이어 섹션 키는 36자까지 쓸 수 있으며, 다른 섹션의 키는 `player.`로 시작할 수 없습니다.
- **출처**: 슬롯 메타데이터에는 마지막으로 슬롯을 저장한 호스트와 저장할 때의 세션 이름이 적히며, `TryGetSessionInfo`가 슬롯에 있는 플레이어와 함께 이를 읽습니다. 다른 플레이어에게서 받은 세이브는 이 값을 그대로 유지하므로, 받은 플레이어가 호스트로서 저장하기 전까지는 어디에서 왔는지 보여 줍니다.
- **공유**: `Share`는 저장된 슬롯을 세션의 다른 플레이어에게 제안합니다. 그 플레이어의 게임은 `OnShareOffered`에, 저장할 슬롯을 지정한 `AcceptShare`나 `DeclineShare`로 응답합니다. `AcceptShare`는 그 ID의 슬롯을 덮어쓰므로, 호스트의 `SourceSlotId`나, 각 호스트가 시작할 때마다 1부터 다시 세는 `ShareId`가 아니라 새 GUID처럼 그 플레이어 자신의 세이브가 쓰지 않는 ID를 지정하세요. 그러면 호스트는 최대 `ShareChunkSize`바이트(기본 16 KiB)의 신뢰 전송 메시지로 `ShareBytesPerSecond`(기본 초당 128 KiB)에 맞춰 세이브를 보냅니다. 호스트 자신의 송신은 세션의 송신량 제한을 거치지 않지만, 공유는 세션의 다른 메시지가 지나갈 여유를 남겨야 하기 때문입니다. 플레이어의 두 응답은 다른 메시지와 마찬가지로 세션의 송신량 제한에 포함됩니다. 받는 플레이어는 자기 `SaveManager.MaxSlotSize`보다 큰 세이브를 거부하고, 저장하기 전에 슬롯의 CRC-32로 세이브 전체를 검사하므로 손상된 바이트는 절대 저장되지 않습니다. `OnShareProgress`는 보내고 받은 바이트를 알립니다. 호스트에서 `Share`가 시작했거나 받는 플레이어에서 `OnShareOffered`가 알린 모든 공유는 그곳에서 `OnShareCompleted`나, `Declined`, `Corrupted`, `Timeout`(`ShareTimeoutSeconds`, 기본 60초 안에 응답 없음), `PlayerLeft`, `SessionEnded` 같은 `SaveShareFailureReason`과 함께 `OnShareFailed`로 끝납니다. 너무 크거나(`TooLarge`) 다른 공유가 진행 중이어서(`Busy`) 받는 플레이어가 스스로 거절한 제안은 그곳에서 아무 이벤트도 발생시키지 않으며, 호스트가 그 이유를 전달받습니다.

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
- **DebugConsole**: 타입 지정 인자를 받는 명령, 개발자와 플레이어 접근, 진행에 영향을 주는 명령의 표시, Unity 로그, IMGUI 화면을 갖춘 게임 내 콘솔(`Mu3Library.DebugConsole` 어셈블리)
- **Event**: `SubscribeHandler`를 통한 owner 관리형 subscription 유틸리티, 재사용 가능한 일회성 helper, 폐기 가능한 `ISubscriptionInfo` token
- **Extensions**: GameObject, Transform, Vector3 등 확장 메서드
- **Localization**: 별도의 Localization 패키지로 옮겨짐 ([README](../../packages/localization/README.md))
- **Multiplayer**: 교체 가능한 백엔드 위에서 로비, 참가자 목록, 준비 확인, 호스트가 설정하고 지우는 로비 데이터와 각 플레이어가 설정하고 지우는 플레이어별 데이터(둘 다 변경을 모든 플레이어가 전달받음), 로비 채팅, 정원 규칙, 클라이언트별 송신량 제한, 게임 시작, 강퇴, 게임 중 연결이 끊긴 플레이어가 클라이언트마다 정한 시도 시간 안에 다시 접속할 때까지 유지되는 자리와 그 뒤의 로비 재입장(실패하면 게임에 알림), 시작 뒤와 게임 안에서 호스트가 시작하는 각 로드 단계마다 모든 플레이어의 로드 완료를 호스트가 기다리는 동기화, 호스트가 중계하며 `MessageWriter`와 `MessageReader`로 내용을 쓰고 읽는 게임 메시지, 각 연결의 품질(핑, 패킷 손실, 초당 바이트)과 모든 플레이어가 읽는, 호스트가 잰 각 플레이어의 핑, 텍스처로 받고, 플레이어가 사진을 바꾸면 따라 바뀌며, 더 보여 주지 않으면 게임이 놓아주는 플레이어의 아바타, 프레즌스 상태, 그리고 백엔드가 녹음하고 디코딩하며, 게임이나 호스트의 중계 필터가 고른 플레이어에게 게임이 메시지로 실어 보내고, `VoicePlayer`가 화자마다 `AudioSource`로 재생하는 음성을 다루는 플레이어 호스트 세션. 백엔드로는 `LocalBackend`(한 프로세스 안에서 여러 플레이어, 또는 `LocalNetwork.CreateMachineWide`로 만든 네트워크에서 같은 PC의 에디터와 플레이어 빌드이며, 이 네트워크는 지연 시간, 지터, 패킷 손실, 네트워크 끊김을 시뮬레이션할 수 있음)가 있고, Steam 로비와 Steam 연결로 플레이하며 플레이어의 Steam 아바타와, 친구 목록에 상태를 보여 주는 Steam 리치 프레즌스를 다루는 `SteamBackend`는 별도의 Steam 패키지([README](../../packages/steam/README.md))에 있습니다. 플레이어별, 틱별 바이트 예산 안에서 바뀐 상태를 플레이어마다 메시지 하나로 모으고 우선순위와 기다린 시간으로 들어갈 것을 고르는 상태 동기화(`StateSyncManager`, 트랜스폼에는 `TransformSync`)와 메시지 종류별 개수와 그 바이트 수도 제공하며, 에디터에는 플레이 모드에서 실행 중인 각 세션의 상태, 플레이어, 연결 품질, 메시지 개수와 바이트 수, 그리고 그 세션의 상태 동기화가 틱마다 각 플레이어에게 보낸 것을 보여 주는 **Mu3Library > Windows > Multiplayer Debugger** 창이 있으며, 이 창의 **Host ping** 열은 호스트에서든 클라이언트에서든 호스트가 잰 각 플레이어의 핑을 보여 줍니다
  - **백엔드 작성**: 다른 플랫폼의 백엔드는 `Mu3Library.Multiplayer.Backend`의 `IMultiplayerBackend`를 구현하며, 매니저가 찾는 두 선택 인터페이스도 구현할 수 있습니다. 공개가 아닌 로비에 초대로만 들어갈 수 있으면 `ILobbyInviteBackend`를 구현해 호스트가 유지되는 자리로 돌아온 플레이어를 다시 초대하게 하고, 백엔드를 사용할 수 없는 이유를 알리려면 Steam 백엔드가 `SteamNotRunning`을 알리듯 `IUnavailableReasonBackend`를 구현합니다. 세 번째인 `IVoiceBackend`는 게임이 직접 쓰는 인터페이스로, 음성을 녹음하고 디코딩합니다. `LobbyDataKeys`는 매니저가 `mu3.` 접두사로 예약해 자기가 만드는 모든 로비에 쓰는 로비 데이터 키를 담으며, 백엔드는 이 키를 다른 로비 데이터처럼 저장하며, 읽을 수도 있습니다. `SearchLobbies`가 반환된 뒤에 검색을 시작하는 백엔드는 `LobbySearchQuery.Clone()`으로 만든 쿼리 사본을 보관합니다
- **ObjectPool**: 중복된 비활성 오브젝트 재등록 방지, `List<T>` 일괄 enqueue 및 count/인자 목록 기반 `List<T>` 반환 dequeue, 선택적 무인자 생성 콜백과 타입 안전한 `CreateArguments` 초기화 콜백, `Clear()` 정리를 지원하는 큐 기반 오브젝트 풀링
- **Observable**: 데이터 변경 감지 시스템
- **Preference**: PlayerPrefs 래퍼
- **Resource**: Resources 폴더 로딩
- **Save**: 데이터 버전과 마이그레이션, 무결성을 검사하는 슬롯 형식, 파일 또는 메모리 저장소를 갖추고 섹션 단위로 슬롯에 저장하는 세이브(`Mu3Library.Save` 어셈블리)
- **SessionSave**: 호스트에서 저장하는 멀티플레이 세션 세이브. 각 플레이어의 섹션을 그 ID 아래에 두고, 새로 들어오거나 자리를 비운 플레이어를 처리하며, 그 세이브로 호스트를 맡을 수 있는 다른 플레이어에게 속도를 조절하고 무결성을 검사하며 공유(`Mu3Library.SessionSave` 어셈블리)
- **States**: `State`와, UniTask가 있으면 `AsyncState`를 쓰는 `StateMachine<TKey>`. 루틴이나 비동기 메서드를 기다리며 나간 뒤 들어가는 전환, 순서대로 대기하는 요청, 가드, `Back`
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
- Watermelon Game: `com.github.doqltl179.mu3library.game.watermelon` (Base 의존)
- 버전과 릴리스 노트: [변경 이력](../changelog/CHANGELOG.ko.md)

Unity 개발자를 위해 제작됨
