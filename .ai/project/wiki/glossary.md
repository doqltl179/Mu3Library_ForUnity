---
owns: "Approved rendering of every project term in each language, terms kept as-is, and per-locale style choices for translated text"
volatility: evolving
reviewed: 2026-10-09
---

# Glossary

The single owner of how project terms are written in each language. Look terms up here before translating and add new terms before their first use. Rules: the kit's `core/wiki/workflows/translation.md` («Terminology»).

## Terms

One row per concept, one column per language of the localized copies («Human-Facing Docs» in [overview.md](overview.md)). `=` means "keep exactly as written in the source".

| Source term | Meaning and context | ko | ja |
|---|---|---|---|
| Mu3Library | Product name | = | = |
| Addressables, Base, DI, Input System, Localization, Notifications, Steam, URP, Watermelon Game | Package names in headings, tables, and release titles | = | = |
| `CoreBase`, DI, MVP | Code-level module names | = | = |
| sample | A package sample under `Samples~` | 샘플 | サンプル |
| define symbol | A `MU3LIBRARY_*` scripting define | = (`Define`) | = (`Define`) |
| optional integration | Code that compiles only while an optional package (UniTask, Addressables, Input System, Steamworks.NET) is installed | 선택 통합 | オプション統合 |
| built-in module | A `com.unity.modules.*` package that switches an engine feature on or off | 내장 모듈 | 組み込みモジュール |
| unscaled time, scaled time | The clock a timed transition follows: `Time.unscaledDeltaTime` (keeps running while `Time.timeScale` is 0) or `Time.deltaTime`; the values of `TimeMode` | 비스케일 시간, 스케일 시간 | 非スケール時間、スケール時間 |
| time scale | `Time.timeScale` named in prose | 타임 스케일 | タイムスケール |
| input backend | The input system that Player Settings > Active Input Handling enables: the Input Manager, the Input System package, or both | 입력 백엔드 | 入力バックエンド |
| Active Input Handling | The Player Settings option and its values "Input Manager (Old)", "Input System Package (New)", "Both" | = | = |
| time factor | `AnimationHandler`'s 0-to-1 progress between the closed and the opened pose | 시간 계수 | 時間係数 |
| board item, held item | An item on the Watermelon Game board, and the one waiting in the player's hand until it is dropped | 보드 아이템, 들고 있는 아이템 | ボードアイテム、保持中のアイテム |
| drag, canceled drag | A press the Watermelon Game board follows from where it starts to where it is released, and one taken away before it is released (input disabled, application focus lost or paused, touch lost or canceled by the system, mouse disconnected or reset by the Input System), which drops nothing | 드래그, 취소된 드래그 | ドラッグ、キャンセルされたドラッグ |
| merge | Two board items of one entry turning into the next one | 병합 | 合成 |
| item pool | The pool a board item goes back to when it leaves the board | 아이템 풀 | アイテムプール |
| item entry | One element of the `BoardItemsConfig` catalog, the entry an item index is made from | 아이템 항목 | アイテムエントリ |
| spawn bag, stage | The weighted bag of item indices `BoardItemSpawnRule` draws from, and the spawn-count range whose weights fill it | 스폰 가방, 단계 | スポーンバッグ、ステージ |
| snapshot | A saved board, `BoardSnapshot` | 스냅샷 | スナップショット |
| screen effect | A full-screen render pass of the URP package (Grayscale, GaussianBlur, DepthOutline, Shake) driven through `ScreenEffectManager` | 스크린 이펙트 | スクリーンエフェクト |
| player build, player | A standalone build of a project and the application it produces, as opposed to the Editor | 플레이어 빌드, 플레이어 | プレイヤービルド、プレイヤー |
| Managed Stripping Level | The Player Settings option that sets how much unused managed code a build removes: Disabled, Minimal, Low, Medium, High | = | = |
| Unity linker | The build tool (UnityLinker) that removes unused managed code, and that link.xml files instruct | Unity 링커 | Unity リンカー |
| script assembly, precompiled assembly | An assembly Unity compiles from scripts (an assembly definition or `Assembly-CSharp`), and a managed DLL added to a project as a plug-in | 스크립트 어셈블리, 사전 컴파일된 어셈블리 | スクリプトアセンブリ、プリコンパイル済みアセンブリ |
| Always Included Shaders | The Graphics settings list of shaders every build includes | = | = |
| Resources folder | A folder named `Resources` whose assets every build includes and `Resources.Load` reads by path | Resources 폴더 | Resources フォルダー |
| chain operation | An Addressables operation that waits for another operation before it starts the one it wraps, and that Addressables hands back in place of that one | 체인 작업 | チェーン操作 |
| session, host, client | A player-hosted multiplayer session, the player whose machine hosts it, and every other player in it | 세션, 호스트, 클라이언트 | セッション、ホスト、クライアント |
| roster | The players in a session, `IMultiplayerManager.Participants` | 참가자 목록 | 参加者一覧 |
| lobby data, participant data | The session's key-value data the host sets, and one player's own key-value data (`Participant.Data`) | 로비 데이터, 참가자 데이터 | ロビーデータ、参加者データ |
| lobby chat | Text messages between the players while the session is in the lobby | 로비 채팅 | ロビーチャット |
| reliable, unreliable delivery | `DeliveryMode.Reliable` (resent and in order) and `DeliveryMode.Unreliable` (may be lost) | 신뢰 전송, 비신뢰 전송 | 信頼性あり、信頼性なし |
| ping, packet loss | A connection's round-trip time and the share of packets lost on it, as `ConnectionQuality` reports them | 핑, 패킷 손실 | ping、パケットロス |
| latency, jitter | The delay a simulated network adds to each packet, and how much that delay varies | 지연 시간, 지터 | レイテンシー、ジッター |
| rate limit | The host's per-client cap on messages and bytes per second (`SessionRules.MaxMessagesPerSecond`, `MaxBytesPerSecond`) | 송신량 제한 | 送信量の制限 |
| kick | The host removing a player from the session, who then cannot rejoin it | 강퇴 | キック |
| reconnect, reservation | A dropped client connecting back to its session, and the time the host keeps its slot for it (`ReconnectReservationSeconds`) | 재접속, 예약 | 再接続、予約 |
| outage | A simulated temporary loss of a local backend's connections (`LocalBackend.SimulateOutage`) | 네트워크 끊김 | 通信断 |
| avatar | A player's Steam profile picture, loaded as a texture (`LoadAvatar`) | 아바타 | アバター |
| release (an avatar) | The game letting the multiplayer manager destroy avatar textures it no longer shows (`ReleaseAvatar`, `ReleaseAvatars`) | 놓아주다 | 解放する |
| Multiplayer Play Mode, virtual player, main Editor | Unity's `com.unity.multiplayer.playmode` package, the extra Editor instances it runs as players, and the Editor that starts them | Multiplayer Play Mode, 가상 플레이어, 메인 에디터 | Multiplayer Play Mode、仮想プレイヤー、メインエディター |
| rich presence, friends list | Steam's per-player key-value status that friends see, and the Steam friends list that shows it | 리치 프레즌스, 친구 목록 | リッチプレゼンス、フレンドリスト |
| TMP Essential Resources | The TextMesh Pro resources a project imports from the Editor: `TMP Settings`, the LiberationSans SDF font asset, the TextMesh Pro shaders, and the default style sheet | = | = |
| font asset, style sheet | A TextMesh Pro `TMP_FontAsset` and `TMP_StyleSheet` | 폰트 에셋, 스타일 시트 | フォントアセット、スタイルシート |
| input module | The `BaseInputModule` on an `EventSystem` that turns device input into UI events, such as `StandaloneInputModule` and `InputSystemUIInputModule` | 입력 모듈 | 入力モジュール |
| drawer, drawer list | A section of a `Mu3Window` editor window (`Mu3WindowDrawer`), and the `DrawerListDrawer` asset that lists the drawers a window shows | 드로어, 드로어 목록 | ドロワー、ドロワーリスト |
| embedded package, local package | A package in the project's `Packages/` folder, and one a manifest references as a folder with `file:`; both can be changed, unlike a Git, registry, or tarball install | 임베디드 패키지, 로컬 패키지 | 埋め込みパッケージ、ローカルパッケージ |
| load phase | A wait the host of a multiplayer game begins within the game with `BeginLoadPhase`, after which every player loads and reports again, as after `StartGame` | 로드 단계 | ロードフェーズ |
| voice, speaker | A player's voice in a multiplayer session, which `IVoiceBackend` records and decodes and `VoicePlayer` plays, and the player whose voice plays | 음성, 화자 | ボイス、話者 |
| relay filter | The host's per-message, per-player filter set with `SetRelayFilter` that decides who receives a relayed message | 중계 필터 | 中継フィルター |
| message policy, delivery mode | `MessagePolicy` and `DeliveryMode` named in prose | 메시지 정책, 전송 방식 | メッセージポリシー、送信モード |
| push to talk | Recording voice only while a key is held | 눌러서 말하기 | プッシュトゥトーク |
| save slot, save section | A named save of `SaveManager` that the game writes and loads as a whole, and one system's part of it with its own key and data version (`SaveSection<TData>`); not the slot a multiplayer session keeps for a player who dropped | 세이브 슬롯, 세이브 섹션 | セーブスロット、セーブセクション |
| slot metadata | The short values a slot list reads from a save slot without loading it (`SetMetadata`) | 슬롯 메타데이터 | スロットメタデータ |
| session save, player section | A save slot of a multiplayer session that the host writes through `SessionSaveManager`, and the part of it that each player has their own of, saved under their `PlayerId` (`PlayerSaveSection<TData>`) | 세션 세이브, 플레이어 섹션 | セッションセーブ、プレイヤーセクション |
| save share | The host's sending of a session save to another player, who stores it as a slot of their own (`SessionSaveManager.Share`) | 세이브 공유 | セーブ共有 |
| debug console, player console | The in-game console of `DebugConsoleManager`, and the same console opened to players in a release build (`PlayerConsoleEnabled`) | 디버그 콘솔, 플레이어 콘솔 | デバッグコンソール、プレイヤーコンソール |
| developer command, player command | A console command that runs only in the editor and a Development Build (`ConsoleAccess.Developer`), and one the game offers its players on purpose (`ConsoleAccess.Player`) | 개발자 명령, 플레이어 명령 | 開発者コマンド、プレイヤーコマンド |
| affects progress | A console command marked to change the game's progress, which sets `HasModifiedProgress` | 진행에 영향을 주는 | 進行に影響する |

## Style Notes

Register and tone chosen per text type and locale, so later translations keep them.

| Text type | Locale | Register and tone |
|---|---|---|
