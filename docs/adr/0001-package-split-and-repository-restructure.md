Status: accepted

# 0001. 패키지 분리와 저장소 구조 개편

- 작성일: 2026-11-15
- 승인일: 2026-11-15 (결정 D1–D22 확정, D10은 제외)
- 결정한 사람: 저장소 소유자
- 관련 이슈: [#519](https://github.com/doqltl179/Mu3Library_ForUnity/issues/519)
- 대상: Base, URP, Watermelon Game 패키지, 개발 프로젝트, 저장소 도구

결정에는 논의하면서 붙인 ID(D1–D22)를 그대로 쓴다. 이후 작업의 이슈와 PR은 이 ID로 결정을 가리킨다.

## 1. 맥락

### 1.1 문제

Mu3Library는 외부 Unity 프로젝트가 Git URL로 설치해 쓰는 UPM 패키지 모음이다. 프로젝트에 따라서는 제공하는 기능 일부를 쓰지 않고, 직접 만들거나 다른 도구를 쓰려고 한다. 그런데 지금 구조에서는 그런 기능을 끌 수 없는 경우가 있다 (2026-11-15 코드 확인).

| 분류 | 해당 기능 | 지금 강제되는 것 |
|---|---|---|
| ① 외부 SDK를 설치하면 자동으로 켜지는 독립 기능 | Steam 백엔드와 Notifications(각각 별도 어셈블리). AddressablesManager, LocalizationManager, InputSystemManager(메인 어셈블리 `Mu3Library` 안의 `#if`) | 프로젝트가 다른 용도로 그 SDK를 설치해도 함께 컴파일되고, 끌 방법이 없다. SDK의 API가 바뀌면 쓰지 않는 우리 코드 때문에 프로젝트 컴파일이 깨질 수 있다. |
| ② 외부 SDK를 설치하면 켜지는 기존 기능의 확장 | 매니저들의 UniTask 비동기 API, SceneLoader의 Addressables 씬 로드, UI의 Input System 지원 | ①과 같은 결합이지만, 코어 매니저 API의 일부다. |
| ③ 항상 포함되는 인프라 | DI | Base의 매니저 6개가 `Mu3Library.DI.IUpdatable`을 구현한다. 플레이어 빌드마다 `DILinkerProcessor`가 link.xml을 만들고, 끌 방법이 없다. |
| ④ 항상 포함되는 일반 기능 | Scene, Audio, UI/MVP, Multiplayer(코어와 로컬 백엔드), ObjectPool, Observable, WebRequest 등 | 쓰지 않는 코드가 들어 있을 뿐, 외부 SDK와 엮이거나 빌드 단계가 도는 일은 없다. |

런타임 부작용은 확인한 범위에서 없다. 예를 들어 Steam은 `SteamBackend.Create`를 호출하기 전에는 초기화되지 않는다. 문제는 컴파일, 빌드, 의존 관계에서 생기는 강제성이다.

저장소 쪽에도 문제가 있다. AI 에이전트와 Unity CLI로 저장소를 유지하면서, 사람이 에디터에서 다듬어야 하는 샘플(Base만 해도 씬 13개, 프리팹 8개, 에셋 29개)과 커밋된 개발 프로젝트 3개가 유지 부담이 되었다. 저장소 루트에 패키지 폴더와 프로젝트 폴더가 섞여 있어 구분도 잘 되지 않는다.

### 1.2 동인

- 프로젝트가 원하지 않는 기능이 강제로 켜지지 않아야 한다.
- 외부 사용자는 지금처럼 Git URL로 설치할 수 있어야 하고, 기존 사용자의 이전 비용은 작아야 한다.
- AI 에이전트와 Unity CLI만으로 컴파일, 테스트, 유지보수를 할 수 있어야 한다.
- 패키지의 공개 API를 깨는 변경은 그 이유와 이전 방법을 함께 안내해야 한다.

### 1.3 제약

- Git URL로 설치한 UPM 패키지는 다른 Git 패키지를 `package.json` 의존성으로 자동 설치하지 못한다. 그래서 지금도 README가 "Base 먼저 설치"를 안내한다.
- Unity는 batch mode에서도 프로젝트(`-projectPath`)가 있어야 컴파일하고 테스트한다.
- Windows에서 Unity는 프로젝트 폴더가 98자 이상이면 일부 패키지 파일을 읽지 못한다 (`tools/README.md` «Fresh Clone Setup»). 세션 워크트리 아래의 개발 프로젝트는 이미 이 길이를 넘는다.
- Unity Localization은 Addressables를 의존성으로 함께 설치한다.

## 2. 검토한 선택지

가장 위의 질문은 "프로젝트가 원하지 않는 기능을 어떻게 빼게 할 것인가"였다.

| 선택지 | 장점 | 비용과 위험 |
|---|---|---|
| A. 지금 구조를 유지하고, 기능을 끄는 define(`!MU3LIBRARY_DISABLE_STEAM` 등)을 더한다 | 구조를 바꾸지 않아 싸다. | 기본값이 여전히 "켜짐"이다. 사용자가 심볼을 알아야 하고 플랫폼마다 설정해야 한다. 강제성을 줄일 뿐 없애지 못하고, DI 문제는 그대로다. |
| B. 외부 SDK에 묶인 독립 기능(①)과 DI(③)를 별도 패키지로 나눈다 | 원하는 프로젝트만 설치한다. 패키지 경계가 외부 SDK 경계와 같아 기준이 분명하다. | 패키지가 3개에서 9개로 늘고, 릴리스와 설치 URL과 도구가 그만큼 늘어난다. ②의 강제성은 남는다. |
| C. ④까지 기능별로 모두 나눈다 | 가장 세밀하게 고를 수 있다. | 패키지가 15개를 넘어 Git URL 설치와 릴리스 부담이 크다. ④는 외부 결합이 없어 나눠도 얻는 것이 적다. |
| D. 처음 제안한 범위인 Steam과 DI만 나눈다 | B보다 작다. | Addressables, Localization, Input System 매니저의 강제성이 남는다. |

세부 결정마다 검토한 다른 선택지는 3장의 각 항목에 적는다.

## 3. 결정

B를 택한다. 강제성이 실제로 생기는 곳은 ①(외부 SDK와의 컴파일 결합)과 ③(항상 붙는 의존과 빌드 단계)이고, 패키지 경계를 외부 SDK 경계와 맞추는 것이 가장 분명한 기준이기 때문이다. 세부 결정은 아래와 같다.

### 3.1 패키지 구성

**D1. 분리 기준.** ①과 ③만 별도 패키지로 나누고, ②와 ④는 Base에 남긴다. ②가 남기는 강제성은 받아들인다. 예를 들어 Localization만 설치한 프로젝트에도 Addressables가 함께 설치되므로, SceneLoader의 Addressables 씬 로드 코드가 컴파일된다. 검토한 다른 선택지는 2장의 C와 D다.

**D7. 패키지 목록과 이름.** 패키지는 아래 9개다. 표시 이름은 지금의 "Mu3 Library …" 형식을 따른다.

| 폴더 | 패키지 ID | 표시 이름 | 런타임 어셈블리 | 네임스페이스 |
|---|---|---|---|---|
| `base` | `com.github.doqltl179.mu3library.base` | Mu3 Library | `Mu3Library`, `Mu3Library.Foundation`, `Mu3Library.Multiplayer` (그대로) | 그대로 |
| `di` | `com.github.doqltl179.mu3library.di` | Mu3 Library DI | `Mu3Library.DI` (그대로) | `Mu3Library.DI` |
| `steam` | `com.github.doqltl179.mu3library.steam` | Mu3 Library Steam | `Mu3Library.Steam.Multiplayer` (D19) | `Mu3Library.Steam.Multiplayer` |
| `notifications` | `com.github.doqltl179.mu3library.notifications` | Mu3 Library Notifications | `Mu3Library.Notifications` (그대로) | `Mu3Library.Notifications` |
| `addressables` | `com.github.doqltl179.mu3library.addressables` | Mu3 Library Addressables | `Mu3Library.Addressables` (새로) | `Mu3Library.Addressable` (그대로) |
| `localization` | `com.github.doqltl179.mu3library.localization` | Mu3 Library Localization | `Mu3Library.Localization` (새로) | `Mu3Library.Localization` |
| `inputsystem` | `com.github.doqltl179.mu3library.inputsystem` | Mu3 Library Input System | `Mu3Library.InputSystem` (새로) | `Mu3Library.IS` (그대로) |
| `urp` | `com.github.doqltl179.mu3library.urp` | Mu3 Library URP | `Mu3Library.URP` (그대로) | 그대로 |
| `game/watermelon` | `com.github.doqltl179.mu3library.game.watermelon` | Mu3 Library Game - Watermelon | `Mu3Library.Game.WatermelonGame` (그대로) | 그대로 |

새로 만드는 어셈블리 3개는 패키지 이름을 따라도 깨지는 것이 없다. 네임스페이스는 사용자 코드의 `using`을 지키려고 그대로 둔다. 검토한 다른 선택지: 네임스페이스도 패키지 이름에 맞추기, InputSystemManager(파일 3개)는 Base에 남기기.

**D19. Steam 패키지.** Steam은 `packages/steam` 한 패키지로 두고, Steam 기능은 그 안에서 어셈블리로 나눈다. 지금 있는 멀티플레이 백엔드는 `Runtime/Multiplayer/`로 옮기고, 어셈블리와 네임스페이스를 `Mu3Library.Steam.Multiplayer`로 바꾼다. 초기화, 콜백 실행, 종료, App ID를 맡는 공통 코어(`Mu3Library.Steam`)는 두 번째 Steam 기능이 생길 때 `SteamBackend`에서 떼어낸다. 그때도 `SteamBackendOptions`가 App ID를 계속 받아 코어로 넘기면 기존 코드는 깨지지 않는다. Steam 기능은 모두 같은 Steamworks.NET에 묶이므로, 기능마다 패키지를 나눠도 줄어드는 강제성이 없다. 검토한 다른 선택지: 기능마다 별도 패키지(`steam/core`, `steam/multiplayer`), 네임스페이스 `Mu3Library.Multiplayer.Steam` 유지, 이번 개편에서 공통 코어까지 떼어내기.

**D8. DI 분리 방법.** 계약은 Base, 구현은 DI 패키지로 나눈다.

- 매니저가 쓰는 계약 5개(`IInitializable`, `IUpdatable`, `ILateUpdatable`, `IObjectInjector`, `InjectAttribute`)를 Base의 `Mu3Library.Foundation` 어셈블리로 옮기고, 네임스페이스 `Mu3Library.DI`는 그대로 둔다.
- 컨테이너(`Container`, `ContainerScope`, `CoreBase`, `CoreRoot` 등)는 di 패키지에 두고, di 패키지가 Base에 의존한다.
- `MVPManager`의 `[Inject] IObjectInjector`는 계약이 Base에 있으므로 그대로 동작한다. 우리 DI가 없으면 값이 비어 있고, 지금처럼 건너뛴다.
- Multiplayer Debugger가 `CoreRoot`에서 매니저를 찾던 `MultiplayerManagerLookup`은, `MultiplayerManager`가 Base 안의 인스턴스 목록에 스스로 등록하고 디버거가 그 목록을 읽는 방식으로 바꾼다.
- `Singleton`과 `GenericSingleton`의 문서 주석이 `CoreBase`를 가리키는 부분을 고친다.

계약 5개는 다른 타입에 의존하지 않아서, 엔진 참조가 없는 Foundation으로 옮길 수 있다. 그러면 Base가 DI 컨테이너 없이 동작하고, 다른 DI를 쓰는 프로젝트는 매니저를 등록한 뒤 `Update()`만 호출하면 된다. Foundation 안에 `Mu3Library.DI` 네임스페이스의 타입이 생겨 이름과 위치가 어긋나지만, 사용자 코드를 깨지 않으려는 선택이다. 검토한 다른 선택지: Base가 di 패키지를 필수로 의존하기(DI를 골라 쓸 수 없다), 계약을 새 네임스페이스로 옮기기(`using`이 바뀐다), 매니저가 계약을 구현하지 않고 di 패키지가 감싸서 호출하기(변경이 크다).

**D22. 백엔드 작성용 API.** Steam 백엔드는 지금 `Mu3Library.Multiplayer`가 `InternalsVisibleTo("Mu3Library.Multiplayer.Steam")`로 열어 준 내부 API를 쓴다. 코드 주석에 적힌 그 근거는 "같은 어셈블리와 함께 배포된다"는 것인데, 패키지가 나뉘면 이 전제가 사라진다. 그래서 백엔드가 구현하거나 써야 하는 API(`ILobbyInviteBackend`, `IUnavailableReasonBackend`, 예약 로비 키 `LobbyDataKeys` 등)를 public 백엔드 작성용 API로 연다. 패키지 사이에 내부 API를 열어 두면 Base만 업데이트했을 때 steam 패키지가 조용히 깨질 수 있어 D4와 맞지 않는다. 열어 두면 다른 사람이 만든 백엔드도 같은 기능을 쓸 수 있다. 대신 연 API는 앞으로 호환성을 지켜야 하므로, 열기 전에 이름과 모양을 다듬는다. 검토한 다른 선택지: `InternalsVisibleTo`의 대상 이름만 바꿔 유지하고, steam 패키지가 정확히 같은 Base 버전을 요구하게 하기.

**D21. 외부 SDK 의존 선언.** Unity 레지스트리에 있는 SDK는 분리한 패키지의 `package.json` `dependencies`에 적고, 그 패키지 안의 define 게이트(`#if`, `versionDefines`)를 없앤다. addressables는 `com.unity.addressables`, localization은 `com.unity.localization`, inputsystem은 `com.unity.inputsystem`, notifications는 `com.unity.mobile.notifications`를 적는다. Git으로 배포되는 Steamworks.NET과 UniTask는 의존으로 적을 수 없으므로 게이트를 유지하고, README에 설치 방법을 적는다. Notifications의 플랫폼 제한(Android, iOS, Editor)과 Steam의 플랫폼 제한(Standalone, Editor), Base에 남는 ② 확장의 게이트는 그대로 둔다. 분리한 패키지는 그 SDK를 쓰려는 프로젝트만 설치하므로, SDK가 없는 경우를 대비한 게이트가 더는 필요 없다. 검토한 다른 선택지: 모든 패키지에서 게이트를 유지하고 SDK는 사용자가 따로 설치하기.

### 3.2 저장소와 패키지 안 구조

**D16. 최상위 폴더.** 저장소 루트에 `packages/`를 두고, `unity/` 단계는 만들지 않는다. 프로젝트 구성 정의는 `tools/unity-projects/` 아래에 구성마다 폴더 하나로 둔다. 커밋된 프로젝트가 없어지면(D15) `unity/` 아래에는 `packages/` 하나만 남고, 한 단계를 줄이면 설치 URL과 경로가 짧아진다. 설치 URL은 `https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/<이름>#<이름>/vX.Y.Z`가 된다. 검토한 다른 선택지: `unity/packages/`와 `unity/projects/`로 나누기, 구성 정의를 루트의 `projects/`에 두기.

**D3. 이름 규칙.** 패키지 폴더는 `packages/<이름>`(`base`, `urp`, `game/watermelon` 등)이다. 패키지 ID는 `com.github.doqltl179.mu3library.<이름>`이고, 이름의 `/`는 `.`으로 바꾼다. 태그는 `<이름>/vX.Y.Z`다. Base의 ID는 바꾸지 않는다. 폴더 이름이 태그 경로와 같아지고, UPM은 폴더 이름이 아니라 `package.json`의 ID로 패키지를 구분하므로 ID만 유지하면 기존 사용자는 다시 설치하지 않아도 된다. 검토한 다른 선택지: 지금의 `Mu3Library_<이름>` 폴더 이름 유지.

**D20. 패키지 안 구조.** 모든 패키지가 Unity 권장 구조를 따른다. 루트에 `Runtime/`, `Editor/`(에디터 도구가 있을 때), `Tests/`를 두고 그 안에서 기능별 폴더로 나눈다. 지금 있는 `Scripts/` 단계는 없애고, 기능에 딸린 에셋은 그 기능 폴더 안에 둔다. URP의 셰이더와 `Resources/`는 `Runtime/ScreenEffect/` 아래로 옮기고, `Resources/` 안의 경로(`Mu3Library.URP/ScreenEffectShaders`)는 그대로 둬서 이 경로로 불러오는 코드는 바뀌지 않는다. `Scripts/` 단계는 같은 `Runtime/` 안의 머티리얼, 텍스처 폴더와 코드를 구분하려고 있던 것인데, D18로 그 에셋이 사라진다. 폴더를 옮겨도 `.meta` GUID가 유지되므로 어셈블리 참조는 깨지지 않는다. 검토한 다른 선택지: `Scripts/` 단계 유지, URP 셰이더와 `Resources/`를 `Runtime/` 바로 아래에 두기.

**D9. 에디터 도구.** 에디터 도구는 그 도구가 다루는 패키지로 옮기고, 에디터 도구가 있는 패키지마다 에디터 어셈블리 `Mu3Library.<이름>.Editor`를 둔다.

| 도구 | 갈 곳 | 에디터 어셈블리 |
|---|---|---|
| `DILinkerProcessor`, `DIValidatorDrawer` | di | `Mu3Library.DI.Editor` |
| `RuntimeDiagnosticsDrawer` | di. 지금처럼 Addressables가 있을 때만 컴파일되며, 기준이 addressables 패키지로 바뀐다 | `Mu3Library.DI.Editor` |
| `AddressableGroupDataExporterDrawer` | addressables | `Mu3Library.Addressables.Editor` |
| `LocalizationDataExporterDrawer`, `LocalizationCharacterCollectorDrawer` | localization | `Mu3Library.Localization.Editor` |
| `InputSystemDataExporterDrawer` | inputsystem | `Mu3Library.InputSystem.Editor` |
| 나머지: Attribute, FileUtil, Observable, UI, Utility 폴더, `Mu3Window`, Multiplayer Debugger, drawer 9개(기반 클래스 포함) | base | `Mu3Library.Editor` (그대로) |

drawer는 `ScriptableObject`이고 창은 drawer 목록 에셋으로 drawer를 찾으므로, 다른 어셈블리에 있어도 목록에 넣어 쓸 수 있다. `Mu3WindowDrawer`와 FileUtil 도우미는 public이라 다른 패키지에서 바로 쓸 수 있다. `RuntimeDiagnosticsDrawer`는 실행 중인 서비스를 DI 코어에서 찾으므로 di가 맡는다. 옮기고 나면 Base 에디터 어셈블리는 Addressables, Localization, Input System, DI를 참조하지 않는다. 검토한 다른 선택지: 에디터 도구를 모두 Base에 두고 지금처럼 define 게이트로 켜고 끄기(D1과 어긋난다), `RuntimeDiagnosticsDrawer`를 MVP 부분과 Addressables 부분으로 나누기.

**D11. 테스트.** 테스트는 시험하는 코드와 같은 패키지로 옮기고, 테스트가 있는 패키지마다 `Mu3Library.<이름>.Tests.Editor`(필요하면 `.Tests.Runtime`)를 둔다. 지금 테스트가 없는 notifications에는 `Tests/`를 만들지 않는다.

| 갈 곳 | Editor 테스트 | Runtime 테스트 |
|---|---|---|
| di | `ContainerTests`, `CoreRootTests`, `DILinkerProcessorTests`, `DIValidatorDrawerTests` | `CorePreparationTests`, `CoreRootLifetimeTests` |
| steam | `MultiplayerSteamTests`, `MultiplayerSteamIntegrationTests` | — |
| addressables | `AddressablesManagerTests` | — |
| localization | `LocalizationManagerTests` | — |
| inputsystem | `InputSystemManagerTests` | — |
| base | 나머지 42개. `SceneLoaderAddressablesTests`(② 확장)와 공용 테스트 도구(`CountedRepeat` 계열, `InspectorHostWindow`, `TestTimeScale`) 포함 | 나머지 3개 |

공용 테스트 도구는 지금 Base에 남는 테스트만 쓰므로 공유 어셈블리가 필요 없다. 각 어셈블리의 `InternalsVisibleTo("Mu3Library.Tests.Editor")`는 새 테스트 어셈블리 이름을 가리키도록 바꾸고, `MultiplayerDebuggerLookupTests`는 D8의 인스턴스 목록을 시험하도록 바꾼다. 검토한 다른 선택지: 테스트를 테스트 전용 패키지 한곳에 모으기.

### 3.3 개발 환경과 검증 도구

**D15. 개발 프로젝트.** 커밋된 개발 프로젝트(`UnityProject_BuiltIn`, `UnityProject_URP`, `UnityProject_Game_WatermelonGame`)를 없애고, 프로젝트 구성 정의만 저장소에 둔다. 도구가 필요할 때 `MU3_WORK_ROOT` 아래에 프로젝트를 만들어 컴파일하고 테스트한다. 지금 개발 프로젝트에는 씬이 없고 설정(manifest, ProjectSettings)과 설정 에셋 몇 개만 있어서 생성 방식으로 바꾸기 쉽다. DI strip harness가 이미 같은 방식으로 프로젝트를 만들어 쓰고, 짧은 경로에 만들면 1.3의 98자 경로 문제도 사라진다. 비용은 `compile-unity.sh`와 `test-unity.sh`(합계 3,351줄), 샘플 링크 도구, verify-build 도구를 다시 만드는 것과, URP 설정 에셋, Input Actions, `steam_appid.txt`를 템플릿 파일로 보관하는 것이다. 검토한 다른 선택지: 커밋된 프로젝트를 유지하고 위치와 이름만 바꾸기(`unity/projects/built-in` 등).

**D12. 구성 목록과 도구.** `tools/unity-projects/`에 구성 4개를 둔다. 지금 세 프로젝트가 맡던 define 조합을 그대로 잇고, 비어 있던 "선택 SDK 없음" 조합을 더한다.

| 구성 | 담는 패키지 | 선택 SDK | 대신하는 것과 특징 |
|---|---|---|---|
| `minimal` | base, di | 없음 | 새 구성. 지금은 모든 개발 프로젝트에 Input System이 있어서, Input System 없이 Base를 컴파일해 본 적이 없다. DI의 UniTask 없는 경로도 여기서 컴파일된다. |
| `full` | base, di, steam, notifications, addressables, localization, inputsystem | UniTask, Steamworks.NET, Addressables, Localization, Input System, Mobile Notifications | `UnityProject_BuiltIn`. 입력 백엔드 Both, `steam_appid.txt` |
| `urp` | base, urp | Input System | `UnityProject_URP`. 입력 백엔드 Input System, URP 설정 에셋 |
| `watermelon` | base, urp, game/watermelon | UniTask, Input System | `UnityProject_Game_WatermelonGame`. 입력 백엔드 Both, URP 설정 에셋, Input Actions |

- 구성 하나에는 `Packages/manifest.json`(패키지 경로는 도구가 채우고, `testables` 포함), `ProjectSettings/`(지금 프로젝트의 것을 출발점으로), 템플릿 파일을 둔다.
- 도구는 `${MU3_WORK_ROOT}/Mu3Library_ForUnity/<unit>/<구성>/`에 프로젝트를 만들거나 갱신하고, 그 안의 `Library/`를 다음 실행에 다시 쓴다.
- 바뀐 패키지를 담은 구성을 고르는 규칙은 지금과 같다.
- `unity-cli-packages.tsv`는 구성 폴더가 그 역할을 대신하므로 없앤다. verify-build와 DI strip harness도 구성 정의를 쓴다.

검토한 다른 선택지: `minimal` 없이 구성 3개만 두기, 패키지마다 자기 SDK만 담은 구성을 따로 두기(구성이 9개 가까이로 늘어난다).

**D17. 샘플이 맡던 검증 역할.**

- 열린 [#187](https://github.com/doqltl179/Mu3Library_ForUnity/issues/187)(Steam 계정 두 개로 하는 점검)은 지금의 Multiplayer 샘플로 먼저 끝내고, 그 뒤에 샘플을 지운다. 앞으로의 여러 인스턴스 점검은 DI strip harness처럼 스크립트로 씬을 만드는 멀티플레이 하네스를 `tools/` 아래에 새로 만들어 대신한다.
- verify-build는 개발 프로젝트 사본 대신 구성 정의로 만든 프로젝트를 빌드하고, 지금처럼 빈 씬을 기본으로 쓴다. 샘플 씬을 넘기던 `--scene` 용도만 없어진다.
- Addressables 원격 번들을 쓰던 Template 샘플이 사라지므로, 이 저장소에서는 번들 배포 설정(`UnityProject_BuiltIn`의 `GitHub_Windows` 프로필)을 더 관리하지 않는다. 이미 배포된 사이트를 내릴지는 이 저장소 밖의 일이라 따로 정한다. Addressables 콘텐츠 빌드를 확인해야 할 때는 스크립트로 최소 그룹을 만들어 빌드한다.

세 역할 모두 사람이 다듬는 씬 없이 스크립트만으로 다시 만들 수 있다. 검토한 다른 선택지: 샘플을 지우기 전에 하네스를 먼저 만들고 #187도 하네스로 점검하기, 번들 배포 설정을 구성 정의의 템플릿으로 옮겨 계속 관리하기.

### 3.4 지우는 것

**D14. 샘플.** 모든 패키지의 샘플(Base의 Template, Attribute, UtilWindow, Multiplayer, URP의 ScreenEffect, Watermelon Game의 WatermelonGame)과 `package.json`의 `samples` 항목을 지운다. README의 Samples 섹션도 지우고, README에 이미 있는 코드 예제가 사용법 안내로 남는다. AI 에이전트와 Unity CLI 중심의 최소 구성에서는 샘플의 의미가 대부분 사라지고, 샘플은 유지하기도 가장 어렵다. 검토한 다른 선택지: 씬을 코드로 만드는 최소 샘플만 남기기, 지금처럼 유지하기.

**D18. 범용 유틸 에셋.** Base의 머티리얼 6개(`Runtime/Materials`)와 텍스처 7개(`Runtime/Textures`)를 지운다. 저장소 안에서 이 에셋을 참조하는 곳은 Template 샘플뿐이고, 코드에서 불러오는 곳은 없다. URP의 셰이더 4개와 `ScreenEffectShaders.asset`은 스크린 이펙트 기능 그 자체라서 남긴다. README가 기본 색 머티리얼이 Base와 함께 제공된다고 안내하는 문단은 지우고, 외부 프로젝트의 참조가 끊길 수 있으므로 CHANGELOG에 알린다.

### 3.5 배포, 버전, 이전

**D2. 배포 방식.** Git URL 설치만 유지한다. OpenUPM 같은 레지스트리는 의존 패키지를 자동으로 설치해 주지만, 외부 서비스에 배포하고 관리해야 하는 별도 결정이다. D1의 범위라면 추가 패키지는 원하는 프로젝트만 URL을 하나씩 더 넣으면 된다. 검토한 다른 선택지: scoped registry로도 배포하기.

**D4. 버전 정책.** 지금처럼 패키지마다 버전을 따로 올린다. 패키지 사이의 호환은 각 `package.json`의 의존 버전으로 나타낸다. 검토한 다른 선택지: 모든 패키지를 같은 버전으로 묶어 릴리스하기.

**D5. 기존 사용자 이전.** 옮기는 코드의 어셈블리 이름, 네임스페이스, `.meta` GUID를 될 수 있는 한 유지한다. 이 개편은 Base의 breaking release로 내고, CHANGELOG에 이전 방법을 안내한다. 이름을 유지하지 못하는 예외는 다음과 같다.

- D8: 계약 5개가 `Mu3Library.Foundation` 어셈블리로 옮겨 간다. asmdef로 `Mu3Library.DI`만 참조하고 이 계약을 쓰던 프로젝트는 `Mu3Library.Foundation` 참조를 더해야 한다.
- D19: Steam 네임스페이스가 `Mu3Library.Steam.Multiplayer`로 바뀐다. `using Mu3Library.Multiplayer.Steam;`을 `using Mu3Library.Steam.Multiplayer;`로 바꿔야 한다.
- D7: Addressables, Localization, Input System은 어셈블리가 새로 생긴다. asmdef로 `Mu3Library`를 참조하던 프로젝트는 새 어셈블리 참조를 더해야 한다. asmdef 없이 쓰는 코드(Assembly-CSharp)는 바꿀 것이 없다.

검토한 다른 선택지: 한 버전 동안 Base에 옛 코드를 남기는 과도기(같은 이름의 타입이 두 패키지에 동시에 있으면 충돌한다).

**D6. 용어.** 패키지(UPM으로 설치하는 단위), 어셈블리(asmdef로 컴파일하는 단위), 기능 모듈(Scene, Audio 같은 기능 폴더)을 구분해서 쓴다. "기본 기능 패키지"는 Base를 가리킨다.

### 3.6 작업 순서

**D13. 작업 순서와 단위.** 아래 15개 단위를 차례로 진행한다. 단위마다 이슈 하나, 브랜치 하나, PR 하나로 하고, 각 단위는 Unity CLI 컴파일과 테스트로 검증한다. 공유 파일(Base의 asmdef, `package.json`, README, 구성 정의)이 겹치므로 한 번에 하나씩 진행하고, 14번만 #187이 닫히면 언제든 진행할 수 있다.

| 순서 | 단위 | 담는 결정 | 시작 조건 |
|---|---|---|---|
| 1 | 결정 기록(이 ADR) | 전체 | 지금 |
| 2 | 샘플과 범용 에셋 제거(Multiplayer 샘플 제외) | D14, D18 | 1 다음 |
| 3 | 구성 정의와 도구 전환, 개발 프로젝트 제거 | D12, D15, D16, D17 | 2 다음. `minimal`은 처음엔 base만 담는다 |
| 4 | 폴더 이동(`packages/`, `Scripts/` 단계 제거) | D3, D16, D20 | 3 다음, 진행 중인 다른 브랜치가 없을 때 |
| 5 | DI 계약을 Foundation으로 옮기기 | D8 | 4 다음 |
| 6 | Multiplayer 백엔드 작성용 API 공개 | D22 | 5 다음 |
| 7 | notifications 분리 | D1, D7, D11, D21 | 6 다음 |
| 8 | inputsystem 분리 | D1, D7, D9, D11, D21 | 7 다음 |
| 9 | localization 분리 | D1, D7, D9, D11, D21 | 8 다음 |
| 10 | addressables 분리 | D1, D7, D9, D11, D21 | 9 다음 |
| 11 | di 분리 | D1, D7, D8, D9, D11 | 10 다음 |
| 12 | steam 분리 | D1, D7, D11, D19, D21, D22 | 11 다음 |
| 13 | 릴리스: Base의 breaking release와 새 패키지 6개의 첫 릴리스 | D4, D5 | 12 다음. 릴리스 시점은 따로 정한다 |
| 14 | 멀티플레이 하네스 만들기와 Multiplayer 샘플 제거 | D14, D17 | #187이 닫힌 뒤 |
| 15 | 에이전트 역할 정리(`sample-integrity-engineer` 제거, `optional-integration-engineer` 범위 갱신) | D14, D21 | 14 다음. role-governor 승인이 필요하다 |

이 순서인 이유는 다음과 같다.

- 샘플과 에셋을 먼저 지우면 뒤에서 옮기고 나눌 파일이 줄어든다.
- 도구를 먼저 바꿔야 개발 프로젝트를 지운 뒤에도 매 단위를 Unity CLI로 컴파일하고 테스트할 수 있다.
- 폴더 이동을 분리보다 먼저 하면 새 패키지를 처음부터 제자리에 만들 수 있다. 다른 작업이 끼기 전, 이른 시점에 옮겨야 충돌도 적다.
- D8과 D22는 분리의 전제다. Base가 DI를 모르고, steam이 public API만 쓰게 된 뒤에 떼어낸다.
- 분리는 Base를 참조하지 않는 단순한 패키지부터 해서 방식을 먼저 굳히고, 다른 패키지를 참조하는 di(`RuntimeDiagnosticsDrawer`가 addressables를 본다)와 steam(D22의 API를 쓴다)을 뒤에 둔다.
- 릴리스는 분리가 모두 끝난 뒤 한 번에 한다. Base에서 빠진 기능을 새 패키지가 같은 날 제공해야 사용자가 넘어올 수 있다.

각 단위의 README, CHANGELOG, 위키 경로 갱신은 그 단위의 PR에 함께 넣는다. Unity를 오래 또는 여러 번 돌리는 단위는 시작 전에 실행 시간대를 정한다. 4번을 시작하기 전에 다른 워크트리에 진행 중인 작업이 없는지 확인한다.

검토한 다른 선택지: 패키지 분리를 먼저 하고 폴더 이동을 마지막에 하기(새 패키지를 옛 위치에 만들었다가 다시 옮겨야 한다), 분리 6개를 한 단위로 묶기(PR이 너무 커져 검토하고 되돌리기 어렵다).

## 4. 결과

### 4.1 쉬워지는 것

- 프로젝트가 필요한 패키지만 설치한다. Steamworks.NET이나 Unity SDK를 다른 용도로 설치해도 우리 코드가 함께 켜지지 않는다.
- 다른 DI 컨테이너를 쓰는 프로젝트도 Base의 매니저를 쓸 수 있다.
- 개발 프로젝트를 짧은 경로에 만들게 되어, 워크트리에서 생기던 98자 경로 문제가 사라진다.
- 사람이 에디터에서 다듬어야 하는 파일이 저장소에서 없어진다.
- Input System 없이 Base를 컴파일하는 구성(`minimal`)이 생긴다.

### 4.2 어려워지는 것

- 패키지가 9개가 되어 릴리스, CHANGELOG 패키지 헤더, 설치 URL, profile의 `release.packages`가 늘어난다.
- 사용자는 Base를 먼저 설치한 뒤, 필요한 패키지의 URL을 하나씩 더 넣어야 한다.
- Multiplayer 백엔드 작성용 API를 공개하므로, 그 호환성을 지켜야 한다.
- 샘플이 없어져 처음 쓰는 사람은 README의 코드 예제에 기대야 한다.
- ②의 강제성은 남는다.

### 4.3 후속 작업

3.6의 2–15번 단위. 각 단위는 시작할 때 이슈를 만들고 이 ADR의 결정 ID를 가리킨다.

### 4.4 다시 볼 신호

- OpenUPM 같은 레지스트리로 배포하게 되면 패키지 의존이 자동으로 풀리므로, 더 세밀한 분리(2장의 C)를 다시 검토한다.
- ②의 결합 때문에 실제로 컴파일이 깨지는 일이 생기면, SceneLoader의 Addressables 부분을 addressables 패키지로 옮기고 SceneLoader에 확장 지점을 만든다.
- 두 번째 Steam 기능이 생기면 Steam 공통 코어를 떼어낸다(D19).
- 공개한 백엔드 작성용 API를 자주 바꿔야 하면 D22를 다시 본다.
