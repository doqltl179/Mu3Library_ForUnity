---
owns: "Approved rendering of every project term in each language, terms kept as-is, and per-locale style choices for translated text"
volatility: evolving
reviewed: 2026-10-09
---

# Glossary

The single owner of how project terms are written in each language. Look terms up here before translating and add new terms before their first use. Rules: the kit's `core/wiki/workflows/translation.md` («Terminology»).

## Terms

One row per concept, one column per locale in `docs.locales`. `=` means "keep exactly as written in the source".

| Source term | Meaning and context | ko | ja |
|---|---|---|---|
| Mu3Library | Product name | = | = |
| Base, URP, Watermelon Game | Package names in headings, tables, and release titles | = | = |
| `CoreBase`, DI, MVP | Code-level module names | = | = |
| sample | A package sample under `Samples~` | 샘플 | サンプル |
| define symbol | A `MU3LIBRARY_*` scripting define | = (`Define`) | = (`Define`) |
| optional integration | Code that compiles only while an optional package (UniTask, Addressables, Localization, Input System, Mobile Notifications) is installed | 선택 통합 | オプション統合 |
| built-in module | A `com.unity.modules.*` package that switches an engine feature on or off | 내장 모듈 | 組み込みモジュール |
| unscaled time, scaled time | The clock a timed transition follows: `Time.unscaledDeltaTime` (keeps running while `Time.timeScale` is 0) or `Time.deltaTime`; the values of `TimeMode` | 비스케일 시간, 스케일 시간 | 非スケール時間、スケール時間 |
| time scale | `Time.timeScale` named in prose | 타임 스케일 | タイムスケール |
| input backend | The input system that Player Settings > Active Input Handling enables: the Input Manager, the Input System package, or both | 입력 백엔드 | 入力バックエンド |
| Active Input Handling | The Player Settings option and its values "Input Manager (Old)", "Input System Package (New)", "Both" | = | = |
| time factor | `AnimationHandler`'s 0-to-1 progress between the closed and the opened pose | 시간 계수 | 時間係数 |
| board item, held item | An item on the Watermelon Game board, and the one waiting in the player's hand until it is dropped | 보드 아이템, 들고 있는 아이템 | ボードアイテム、保持中のアイテム |
| drag, canceled drag | A press the Watermelon Game board follows from where it starts to where it is released, and one taken away before it is released (input disabled, touch lost or canceled by the system, mouse disconnected), which drops nothing | 드래그, 취소된 드래그 | ドラッグ、キャンセルされたドラッグ |
| merge | Two board items of one entry turning into the next one | 병합 | 合成 |
| item pool | The pool a board item goes back to when it leaves the board | 아이템 풀 | アイテムプール |
| item entry | One element of the `BoardItemsConfig` catalog, the entry an item index is made from | 아이템 항목 | アイテムエントリ |
| spawn bag, stage | The weighted bag of item indices `BoardItemSpawnRule` draws from, and the spawn-count range whose weights fill it | 스폰 가방, 단계 | スポーンバッグ、ステージ |
| snapshot | A saved board, `BoardSnapshot` | 스냅샷 | スナップショット |
| screen effect | A full-screen render pass of the URP package (Grayscale, GaussianBlur, DepthOutline, Shake) driven through `ScreenEffectManager` | 스크린 이펙트 | スクリーンエフェクト |
| player build, player | A standalone build of a project and the application it produces, as opposed to the Editor | 플레이어 빌드, 플레이어 | プレイヤービルド、プレイヤー |
| Always Included Shaders | The Graphics settings list of shaders every build includes | = | = |
| Resources folder | A folder named `Resources` whose assets every build includes and `Resources.Load` reads by path | Resources 폴더 | Resources フォルダー |
| chain operation | An Addressables operation that waits for another operation before it starts the one it wraps, and that Addressables hands back in place of that one | 체인 작업 | チェーン操作 |
| TMP Essential Resources | The TextMesh Pro resources a project imports from the Editor: `TMP Settings`, the LiberationSans SDF font asset, the TextMesh Pro shaders, and the default style sheet | = | = |
| font asset, style sheet | A TextMesh Pro `TMP_FontAsset` and `TMP_StyleSheet` | 폰트 에셋, 스타일 시트 | フォントアセット、スタイルシート |
| input module | The `BaseInputModule` on an `EventSystem` that turns device input into UI events, such as `StandaloneInputModule` and `InputSystemUIInputModule` | 입력 모듈 | 入力モジュール |

## Style Notes

Register and tone chosen per text type and locale, so later translations keep them.

| Text type | Locale | Register and tone |
|---|---|---|
