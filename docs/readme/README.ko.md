# Mu3Library For Unity

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](../../README.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](README.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](README.ja.md)

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../LICENSE)

</div>

**Mu3Library**는 Unity 프로젝트를 위한 모듈화된 아키텍처 프레임워크입니다. 커스텀 DI(Dependency Injection) 시스템과 MVP(Model-View-Presenter) UI 패턴을 중심으로, 확장 가능하고 유지보수가 쉬운 게임 개발을 지원합니다.

## 📘 문서

- English README: `../../README.md`
- Japanese README: `README.ja.md`
- Changelog (EN): `../../CHANGELOG.md`
- Changelog (KO): `../changelog/CHANGELOG.ko.md`
- Changelog (JA): `../changelog/CHANGELOG.ja.md`

## ✨ 주요 특징

- 🏗 **모듈화된 Core 시스템**: 기능별로 독립된 `CoreBase`를 통한 명확한 책임 분리
- 💉 **커스텀 DI 컨테이너**: Singleton, Transient, Scoped 라이프타임 지원
- 🎨 **MVP UI 패턴**: View-Presenter-Model 분리로 테스트 가능한 UI 로직
- 🔄 **자동 생명주기 관리**: `IInitializable`, `IUpdatable`, `IDisposable` 인터페이스 기반
- 📦 **선택적 패키지 지원**: UniTask, Addressables, Localization 조건부 활성화
- 🎵 **Audio 시스템**: BGM/SFX 분리 관리 및 볼륨 제어
- 🌐 **WebRequest**: HTTP GET/POST, 다운로드 크기 조회, UniTask 지원
- 📊 **Observable 패턴**: 데이터 변경 감지 및 이벤트 기반 바인딩
- 🛠 **유틸리티 모음**: Extension Methods, ObjectPool, EasingFunctions
- ✅ **초기화 결과 계약**: Addressables/Localization 초기화 성공/실패 상태를 명시적으로 제공
- 🔁 **안정적인 네트워킹**: WebRequest 결과형 API에 상태 코드, 헤더, 타임아웃, 재시도 옵션 제공
- 🧭 **결정론적 Core 업데이트**: Core 실행 순서가 명시적이고 안정적으로 동작
- ⏳ **Scene 비동기 API**: UniTask + CancellationToken 기반 씬 로드/언로드 헬퍼 제공
- 🎮 **Input System Manager**: 액션 에셋 관리, 인터랙티브 리바인딩와 바인딩 오버라이드 퍼시스턴스 지원 (선택)
- 🧰 **에디터 유틸리티 Drawer**: Input System/Localization 이름 내보내기와 Localization 문자 수집 도구 제공
## 📋 요구사항

- Unity 6 (6000.0+)
- .NET Standard 2.1

## 📦 설치 방법

### 방법 1: 패키지 매니저 (Git URL)
1. Unity Editor에서 `Window` > `Package Manager`를 엽니다
2. `+` 버튼 클릭 > `Add package from git URL...`
3. 다음 URL을 입력:
   ```
   https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base
   ```

### 방법 2: 패키지 매니저 (로컬 디스크)
1. 이 저장소를 클론합니다
2. Unity Editor에서 `Window` > `Package Manager`
3. `+` 버튼 > `Add package from disk...`
4. `Mu3Library_Base/package.json` 선택

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

## 📖 전체 모듈 목록

- **Addressable**: Addressables 통합 (선택)
- **Attribute**: `ConditionalHideAttribute` 등 커스텀 속성
- **Audio**: BGM/SFX 관리 시스템
- **DI**: Dependency Injection 컨테이너
- **Extensions**: GameObject, Transform, Vector3 등 확장 메서드
- **Localization**: Unity Localization 래퍼 (선택)
- **ObjectPool**: 제네릭 오브젝트 풀링
- **Observable**: 데이터 변경 감지 시스템
- **Preference**: PlayerPrefs 래퍼
- **Resource**: Resources 폴더 로딩
- **Scene**: 씬 로딩 추상화
- **UI**: MVP 패턴 구현
- **IS**: Unity Input System 래퍼 및 바인딩 매니저 (선택)
- **Utility**: Singleton, EasingFunctions, Settings
- **WebRequest**: HTTP 요청 관리

## 🎓 샘플

패키지 매니저의 **Samples** 탭에서 다음 샘플을 가져올 수 있습니다:

Base 패키지 (**Mu3 Library**):
- **Template**: 기본 Core 구조 및 사용 예제
- **Attribute**: ConditionalHide 사용법
- **UtilWindow**: 커스텀 에디터 윈도우 및 유틸리티 Drawer 예제

URP 패키지 (**Mu3 Library URP**):
- **ScreenEffect**: Grayscale, Shake, GaussianBlur, DepthOutline 스크린 이펙트와 대응 handler 스크립트를 포함한 URP 스크린 이펙트 샘플 씬 및 보조 스크립트

이 저장소에서는 Base 샘플 소스를 `Mu3Library_Base/Samples~`에서, URP 샘플 소스를 `Mu3Library_URP/Samples~/ScreenEffect`에서 확인할 수 있습니다.

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

## 📝 최근 업데이트

- 최신 Base 패키지 버전: `0.9.0`
- 최신 URP 패키지 버전: `0.1.2` (manifest 의존성: `com.github.doqltl179.mu3library.base` `0.5.0`)
- 최신 URP `ScreenEffect` 변경사항을 포함한 미출시 변경사항은 `CHANGELOG.md`를 참고하세요.

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
- Base: `com.github.doqltl179.mu3library.base` `0.9.0`
- URP: `com.github.doqltl179.mu3library.urp` `0.1.2` (manifest 의존성: `com.github.doqltl179.mu3library.base` `0.5.0`)

Unity 개발자를 위해 제작됨
