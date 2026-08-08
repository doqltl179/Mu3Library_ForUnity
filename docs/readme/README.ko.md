# Mu3Library For Unity

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](../../README.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](README.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](README.ja.md)

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../LICENSE)

</div>

**Mu3Library**는 Unity 프로젝트를 위한 모듈화된 아키텍처 프레임워크입니다. 커스텀 DI(Dependency Injection) 시스템과 MVP(Model-View-Presenter) UI 패턴을 중심으로, 확장 가능하고 유지보수가 쉬운 게임 개발을 지원합니다.

## 📘 문서

### 패키지 문서

- [English README](../../README.md) · [Japanese README](README.ja.md)
- [패키지 변경 이력 (English)](../../CHANGELOG.md) · [한국어](../changelog/CHANGELOG.ko.md) · [일본어](../changelog/CHANGELOG.ja.md)

### 기여자 문서

- [저장소 워크플로 변경 이력](../repository/CHANGELOG.md)
- [AI agent 및 기여자 워크플로](../ai-agents/README.md) — 작업 소유자, 절차, 검증 경로를 선택합니다.
- [저장소 도구](../../tools/README.md)

## ✨ 주요 특징

- 🏗 **모듈화된 Core 시스템**: 기능별로 독립된 `CoreBase`를 통한 명확한 책임 분리
- 💉 **커스텀 DI 컨테이너**: Singleton, Transient, Scoped 라이프타임 지원
- 🎨 **MVP UI 패턴**: View-Presenter-Model 분리로 테스트 가능한 UI 로직
- 🔄 **자동 생명주기 관리**: `IInitializable`, `IUpdatable`, `IDisposable` 인터페이스 기반
- 📦 **선택적 패키지 지원**: UniTask, Addressables, Localization 조건부 활성화
- 🎵 **Audio 시스템**: BGM/SFX 분리 관리 및 볼륨 제어
- ✨ **Particle Handler**: 필수 `ParticleSystem`의 재생, 일시정지, 정지, 초기화, 재시작, `Loop`/`IsLooping`/`SetLoop`/`PlayLoop`/`PlayOnce` 제어, 상태 조회와 생명주기 이벤트를 제공하는 편의 컴포넌트
- 🌐 **WebRequest**: HTTP GET/POST, 다운로드 크기 조회, UniTask 지원
- 📊 **Observable 패턴**: 데이터 변경 감지 및 이벤트 기반 바인딩
- 🛠 **유틸리티 모음**: Extension Methods, ObjectPool, EasingFunctions
- ✅ **초기화 결과 계약**: Addressables/Localization 초기화 성공/실패 상태를 명시적으로 제공
- 🔁 **안정적인 네트워킹**: WebRequest 결과형 API에 상태 코드, 헤더, 타임아웃, 재시도 옵션과 선택적 취소 전파를 제공
- 🧭 **결정론적 Core 업데이트**: Core 실행 순서가 명시적이고 안정적으로 동작
- ⏳ **Scene 비동기 API**: built-in 및 Addressables 씬용 phase 기반 UniTask 헬퍼와 resolved scene name을 포함한 구조화 lifecycle 콜백 제공
- 🎮 **Input System Manager**: 액션 에셋 관리, 인터랙티브 리바인딩와 바인딩 오버라이드 퍼시스턴스 지원 (선택)
- 🧰 **에디터 유틸리티 Drawer**: Input System/Localization 이름 내보내기, 라벨/로케일, 그룹/테이블, 엔트리를 역할별 스크립트로 분리하는 Addressable 그룹 및 Localization 데이터 내보내기, Localization 문자 수집 도구 제공
- 🖼 **World-Space Background 유틸리티**: 필수 `SpriteRenderer` 배경을 카메라 뷰포트에 맞추고, 자동 맞춤·카메라 전면 배치·렌더러 표시 설정을 선택적으로 제공합니다
- 🍉 **수박 게임 템플릿**: 11개 아이템 2D 병합 보드, 설정 가능한 `BoardConfig`, 보드 맞춤/좌표 변환 helper와 플레이 가능한 샘플 씬을 제공하는 재사용 가능한 `Mu3Library.Game.WatermelonGame` 런타임 어셈블리
## 📋 요구사항

- Unity 6 (6000.0+)
- .NET Standard 2.1

## 📦 설치 방법

### 방법 1: 패키지 매니저 (Git URL)
1. Unity Editor에서 `Window` > `Package Manager`를 엽니다
2. `+` 버튼 클릭 > `Add package from git URL...`
3. 다음 URL 중 하나를 입력:
   ```
    # Base 패키지
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base#base/v0.22.0

    # URP 패키지 (먼저 Base 설치)
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_URP#urp/v0.2.1
   ```

### 방법 2: 패키지 매니저 (로컬 디스크)
1. 이 저장소를 클론합니다
2. Unity Editor에서 `Window` > `Package Manager`
3. `+` 버튼 > `Add package from disk...`
4. 다음 중 하나를 선택합니다:
    - `Mu3Library_Base/package.json`
    - `Mu3Library_URP/package.json` (먼저 Base 설치)
    - `Mu3Library_Game_WatermelonGame/package.json` (먼저 Base와 URP 설치)

## 📚 핵심 모듈

### DI (Dependency Injection)
커스텀 DI 컨테이너로 서비스 등록 및 의존성 주입을 자동화합니다.

```csharp
using Mu3Library.DI;

public class AudioCore : CoreBase
{
    protected override void ConfigureContainer()
    {
        // AudioManager를 싱글톤으로 등록 — IAudioManager에도 자동 매핑됨
        RegisterClass<AudioManager>();
    }
}

public class GameCore : CoreBase
{
    [SerializeField] private AudioClip _mainThemeClip;

    // 자동 주입 (같은 Core 내)
    [Inject] private IAudioManager _audioManager;

    // 다른 Core에서 주입
    [Inject(typeof(UICore))] private IMVPManager _mvpManager;

    protected override void Start()
    {
        base.Start(); // 주입이 먼저 실행되어야 함!
        _audioManager.PlayBgm(_mainThemeClip);
    }
}
```

등록된 서비스도 생성이 완료된 후 생명주기 콜백이 실행되기 전에 `[Inject]` 필드와 프로퍼티 주입을 받습니다.

```csharp
public class AudioPlaybackService
{
    [Inject] private IAudioManager _audioManager;
}

public class AudioCore : CoreBase
{
    protected override void ConfigureContainer()
    {
        RegisterClass<AudioManager>();
        RegisterClass<AudioPlaybackService>(); // [Inject] 멤버가 자동으로 채워짐
    }
}
```

`MVPManager`도 좁은 공개 계약인 `IObjectInjector`를 사용해 컨테이너 외부에서 생성된 presenter에 `[Inject]` 필드와 프로퍼티 주입을 적용합니다. 구체적인 `ContainerScope`와 내부 주입 구현은 DI 어셈블리 내부에 유지됩니다.

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
    _audioManager.PlaySfx(sfxClip, volume: 1.0f);
}
```

### Addressables
Addressables 지원이 활성화된 경우, key로 해석되는 모든 에셋을 해당 resource location의 `PrimaryKey`로 인덱싱한 `Dictionary<string, T>`로 로드할 수 있습니다. `PrimaryKey`는 비어 있지 않고 고유해야 하며, 그렇지 않으면 로드 작업이 실패합니다.

```csharp
[Inject] private IAddressablesManager _addressablesManager;

_addressablesManager.LoadAssetsWithKeys<Texture2D>("test-image", textures =>
{
    if (textures != null && textures.TryGetValue("TestImage", out Texture2D texture))
    {
        Debug.Log(texture.name);
    }
});

// UniTask 지원도 활성화된 경우:
Dictionary<string, Texture2D> textures =
    await _addressablesManager.LoadAssetsWithKeysAsync<Texture2D>("test-image");
```

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
```

### Observable 패턴
값 변경을 감지하고 이벤트를 발행합니다.

```csharp
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

## 🔧 선택적 패키지

다음 패키지들이 설치되면 해당 기능이 자동으로 활성화됩니다:

| 패키지 | Define | 기능 |
|-------|--------|------|
| [UniTask](https://github.com/Cysharp/UniTask) | `MU3LIBRARY_UNITASK_SUPPORT` | async/await 비동기 API |
| Unity Addressables | `MU3LIBRARY_ADDRESSABLES_SUPPORT` | 동적 에셋 로딩 |
| Unity Localization | `MU3LIBRARY_LOCALIZATION_SUPPORT` | 다국어 지원 |
| Unity Input System | `MU3LIBRARY_INPUTSYSTEM_SUPPORT` | 새로운 입력 시스템 |

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

## 📖 전체 모듈 목록

- **Addressable**: Addressables 통합 (선택)
- **Attribute**: `ConditionalHideAttribute`, `ButtonInvokeAttribute` 등의 커스텀 속성
- **Audio**: BGM/SFX 관리 시스템
- **Particle**: 편리한 `ParticleSystem` 제어, loop 설정과 `OnPlayed`, `OnStopped`, `OnPaused`, `OnUnPaused`, `OnCleared`, `OnRestarted`, `OnLoopChanged`, `OnCompleted` 이벤트를 제공하는 `ParticleHandler` MonoBehaviour
- **DI**: Dependency Injection 컨테이너
- **Event**: `SubscribeHandler`를 통한 owner 관리형 subscription 유틸리티, 재사용 가능한 일회성 helper, 폐기 가능한 `ISubscriptionInfo` token
- **Extensions**: GameObject, Transform, Vector3 등 확장 메서드
- **Localization**: Unity Localization 래퍼 (선택)
- **ObjectPool**: 중복된 비활성 오브젝트 재등록 방지, `List<T>` 일괄 enqueue 및 count/인자 목록 기반 `List<T>` 반환 dequeue, 선택적 무인자 생성 콜백과 타입 안전한 `CreateArguments` 초기화 콜백, `Clear()` 정리를 지원하는 큐 기반 오브젝트 풀링
- **Observable**: 데이터 변경 감지 시스템
- **Preference**: PlayerPrefs 래퍼
- **Resource**: Resources 폴더 로딩
- **Scene**: phase/status 조회, lifecycle/progress callback, 일회성 lifecycle 구독 helper, 통합 rejection event를 제공하는 씬 로딩 추상화
- **UI**: MVP 패턴 구현
- **IS**: Unity Input System 래퍼 및 바인딩 매니저 (선택)
- **Utility**: Singleton, EasingFunctions, Settings, 카메라 뷰포트에 `SpriteRenderer` 배경을 맞추는 `Mellow.Utility.WorldSpaceBackground`
- **WebRequest**: HTTP 요청 관리

## 🎓 샘플

패키지 매니저의 **Samples** 탭에서 다음 샘플을 가져올 수 있습니다:

Base 패키지 (**Mu3 Library**):
- **Template**: 기본 Core 구조 및 사용 예제
- **Attribute**: `ConditionalHideAttribute`와 `ButtonInvokeAttribute` 사용법
- **UtilWindow**: 커스텀 에디터 윈도우 및 유틸리티 Drawer 예제

URP 패키지 (**Mu3 Library URP**):
- **ScreenEffect**: Grayscale, Shake, GaussianBlur, DepthOutline 스크린 이펙트와 대응 handler 스크립트를 포함한 URP 스크린 이펙트 샘플 씬 및 보조 스크립트
- **Camera Stack Helper**: `Mu3Library.URP.Cam.CameraStackSetter`가 `SetCameraStackToMain(...)` 및 `SetCameraStack(...)` 헬퍼를 제공하여 임의의 URP overlay camera를 `Camera.main` 또는 명시적인 root camera stack에 넣고, 필요하면 삽입 인덱스를 제어할 수 있습니다.

Watermelon Game 패키지 (**Mu3 Library Watermelon Game**):
- **Watermelon Game**: 설정 가능한 보드, 과일 스프라이트, 샘플 manager/core 스크립트를 포함한 플레이 가능한 2D 낙하 과일 병합 씬
- **Fruit item indexes**: 보드 설정은 항상 11개의 과일 항목을 포함하며 0부터 시작하는 목록 index로 접근합니다.
- **Board fitting**: `BoardArea.Fit(...)`으로 설정된 카메라 뷰포트 padding 안에 보드 스프라이트를 맞출 수 있습니다.
- **Board collision boundaries**: `BoardArea.SetOutColliders()`로 아이템 영역 viewport padding 기준의 left, right, bottom `BoxCollider2D` 경계를 생성·갱신할 수 있으며 top은 열려 있습니다.
- **Board-relative coordinate conversions**: `BoardArea`는 world, screen, local 정규화 위치 변환과 초기화 안전성을 위한 `Try...` 변형을 제공합니다.
- **Merge effects**: `BoardItemInfo`는 선택적 병합 효과용 `ParticleHandler` 리소스를 지원하며, 완료된 생성 효과 인스턴스는 자동으로 정리됩니다.
- **Scoring extension point**: `BoardItemScoreRule.GetScore(int)`를 override하여 기본 삼각수 점수 progression을 커스터마이즈할 수 있습니다.
- **Board commands**: `BoardController.EnqueueCommand(...)`로 공개 커맨드 큐를 사용하고, `IUpdatableBoardCommand`/`ICancelableBoardCommand`와 `BoardCommand` lifecycle hook으로 프레임 단위 실행·취소를 구성할 수 있습니다. `BoardController.CommandContext`는 외부 커맨드가 아이템·점수·사운드에 접근하는 좁은 표면을 제공하며, Flow/Item/Score 커맨드와 `MergingCommand`를 함께 제공합니다.
- **Board sound volumes**: `BoardController.SfxVolume`과 `BoardController.BgmVolume`으로 `BoardConfig.SoundConfig`와 별도로 보드 재생 볼륨을 설정할 수 있으며, 두 값 모두 0–1 범위로 제한됩니다.
- **Sample assets**: 패키지 샘플에는 `BoardConfig` 에셋, 과일/배경 이미지, 샘플 manager/core 스크립트, `Demo` 씬이 포함됩니다.

이 저장소에서는 Base 샘플 소스를 `Mu3Library_Base/Samples~`에서, URP 샘플 소스를 `Mu3Library_URP/Samples~/ScreenEffect`에서, Watermelon Game 샘플 소스를 `Mu3Library_Game_WatermelonGame/Samples~/WatermelonGame`에서 확인할 수 있습니다.

새 helper로 MVP render camera를 스택에 넣으려면 render camera를 직접 전달하세요:

```csharp
using Mu3Library.URP.Cam;

CameraStackSetter.SetCameraStack(targetCamera, _mvpManager.RenderCamera);
```

**Template 주요 구성:**
- Scenes: Main, Sample_MVP, Sample_Addressables, Sample_Localization, Sample_WebRequest, Sample_Audio, Sample_Audio3D, Sample_IS
- Localization: Locales(KO/JA/EN), String Table 샘플
- Resources: MVP 샘플용 Prefab 및 설정
- Materials: 기본 색깔 머티리얼 제공 (Black, Blue, Green, Magenta, Red, White)

## 🏗 아키텍처 개요

### Core 시스템
각 `CoreBase`는 독립적인 DI 컨테이너(`ContainerScope`)를 소유하며, `CoreRoot`가 생명주기를 관리합니다.

```
CoreRoot (Singleton)
├── AudioCore (독립 ContainerScope)
│   └── AudioManager, BgmController, SfxController
├── UICore (독립 ContainerScope)
│   └── MVPManager, Presenters...
└── NetworkCore (독립 ContainerScope)
    └── WebRequestManager
```

### Core 간 통신
다른 Core의 서비스에 접근하려면:

```csharp
// 방법 1: Start()에서 수동 획득
protected override void Start()
{
    base.Start();
    _audioManager = GetClassFromOtherCore<AudioCore, IAudioManager>();
}

// 방법 2: Inject 속성 (CoreBase 전용)
[Inject(typeof(AudioCore))] private IAudioManager _audioManager;
```

특정 Core의 초기화가 완료된 뒤 한 번만 코드를 실행하려면 `ICoreRoot`를 통해 구독합니다:

```csharp
CoreRoot.Instance.SubscribeOnCoreInitializedOnce<AudioCore>(() =>
{
    _audioManager = CoreRoot.Instance.GetClass<AudioCore, IAudioManager>();
});
```

callback은 대상 Core의 초기화가 완료된 후 한 번 호출됩니다.

비동기 준비 작업이 있는 경우, 준비 완료 알림을 구독할 수 있습니다:

```csharp
CoreRoot.Instance.SubscribeOnCorePreparedOnce<AudioCore>(() =>
{
    // AudioCore의 준비가 완료된 뒤 코드를 실행합니다.
});
```

callback은 대상 Core의 준비 단계가 완료된 후 한 번 호출됩니다.

## 📝 최근 업데이트

- 이 저장소의 현재 Base 패키지 버전: `0.22.0`
- 이 저장소의 현재 URP 패키지 버전: `0.2.1` (manifest 의존성: `com.github.doqltl179.mu3library.base` `0.22.0`)
- 이 저장소의 현재 Watermelon Game 패키지 버전: `0.3.0` (Base `0.22.0`, URP `0.2.1` 의존)
- 저장소 릴리스 노트 및 초안 버전 이력은 `CHANGELOG.md`를 참고하세요.

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
- Base: `com.github.doqltl179.mu3library.base` `0.22.0`
- URP: `com.github.doqltl179.mu3library.urp` `0.2.1` (manifest 의존성: `com.github.doqltl179.mu3library.base` `0.22.0`)
- Watermelon Game: `com.github.doqltl179.mu3library.game.watermelon` `0.3.0` (Base `0.22.0`, URP `0.2.1` 의존)

Unity 개발자를 위해 제작됨
