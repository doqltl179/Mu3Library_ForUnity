# 変更履歴 (Changelog)

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](../../CHANGELOG.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](CHANGELOG.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](CHANGELOG.ja.md)

</div>

Mu3Library For Unityのすべての注目すべき変更はこのファイルに記録されます。

このフォーマットは[Keep a Changelog](https://keepachangelog.com/en/1.0.0/)に基づいており、
このプロジェクトは[Semantic Versioning](https://semver.org/spec/v2.0.0.html)に準拠しています。

この changelog はパッケージリリース変更のみを追跡します。リポジトリ開発 workflow と tooling の変更は [`docs/repository/CHANGELOG.md`](../../docs/repository/CHANGELOG.md) で管理します。

## [Unreleased]

### 追加
- `BoardArea`: 任意の outline sprite を設定した board 幅に合わせ、item 超過境界へ配置する機能を追加しました。
- `BoardController`: item の生成間隔（既定 0.5 秒）を追加し、touch のたびに item が連続生成されないようにしました。状態は `CanSpawnItem` と `DropCooldown` で確認できます。
- `BoardController`: item を放した瞬間に下方向へ与える初速度を追加しました。値は 1 秒あたりの board area 高さの比率として設定し、board area は画面解像度に応じて大きさが変わるため、どの端末でも同じ力が加わります。item ごとに質量が異なるため、`BoardItem.SetDropVelocity(Vector2)` は force ではなく velocity として適用します。
- `BoardController`: 落下加速度を board area 高さに対する比率（1 秒の 2 乗あたり）で設定し、item を落とす際に `BoardItem.GravityScale` として適用するようにしました。初速度と併せて、落下の開始だけでなく全区間が、どの解像度でも board に対して同じ割合を同じ時間で移動します。調整されるのは board item のみで、project の gravity 設定は変更しません。
- `BoardSnapshot`: `ToJson` と `FromJson` による JSON の serialize / deserialize を追加し、score、spawn / merge count、holding item と preview item の index、board 基準の item 位置と回転を保存できるようにしました。
- `BoardController`: holding item、preview item、board item の位置と回転を含めて board を保存・復元する `ExportSnapshot`、`ExportSnapshotJson`、`ImportSnapshotJson` を追加しました。
- `BoardConfig`: すべての board item に適用する physics material、linear damping、angular damping を設定する任意の `ItemPhysicsMaterial` と `ItemRigidbodySettings` を追加しました。
- `BoardItem`: item prefab を変更せず board 全体の physics を設定できる `PhysicsMaterial`、`LinearDamping`、`AngularDamping` property を追加しました。

- `BoardController`: 次の item の preview を `NextItemIndex`、`NextItemInfo`、`OnNextItemChanged` として追加しました。spawn rule から 1 つ先に引いておくため、手持ちの item の次に何が来るかを確認でき、手持ちの item 自体は `HoldingItem` で公開されます。
- `BoardController`: item を落とした後の drop interval の間、game end 判定を停止する `IsGameEndCheckPaused` を追加しました。item が高く積まれた状態では落下 item がほぼ即座に着地判定となるため、その間に item が滑って落ち着いてから判定します。

### 変更
- `BoardController`: board 設定を検証済みの `SetBoardConfig(BoardConfig)` 境界から適用するように変更しました（`SetBoareConfig` は compatibility alias として維持）。active item と holding item の scale、board 基準の physics を atomic に更新します。
- `BoardItemsConfig`: 11 個の default fruit より後ろの catalog entry を将来の rule 用に保持し、default spawn / merge rule は 11 個の entry のみに制限するように変更しました。
- `BoardController`: merge candidate を contact 確認前に index ごとに group 化し、layout rebuild 中の board child renderer と out collider を cache するように変更しました。
- `BoardArea`: 公開している world XY bounds API を維持しつつ、camera に aligned または tilted な board plane でも local board bounds が正しくなるように変更しました。
- `WatermelonGame` sample: dependency を検証・cache し、board の prepare が成功した後だけ game start するように変更しました。
- `BoardItemInfo`: fruit の contact が設定した sprite size により近くなるよう、default collider diameter ratio を `0.96` から `0.98` に変更しました。
- `BoardController`: 落とす item が touch 時ではなく drop interval の終了と同時に board 上端で待機するようにしました。直前の item を落とした位置で待機し、touch はその item を掴んで動かすだけになります。
- `BoardArea`: component を `Board/Area` 配下の単一責務の部品へ分割しました。`BoardAreaBoundsCalculator` が board の矩形を計算し、`BoardAreaCoordinateConverter` が座標を変換し、`BoardAreaView` が board と item 基準線を描画し、`BoardAreaOutColliders` が item を board 内に留め、`BoardAreaInputRelay` が board に属する touch のみを通知します。矩形そのものは新規の `BoardAreaBounds` と `CoordinateBounds` が保持します。component の public API は変更なく、各部品へ委譲するだけです。
- `BoardItemScaleRule`: item の直径を、減少していく面積倍率ではなく board area の幅を基準に線形配分するようにしました。最小の果物は `1/20`、最大の果物は `2/5` です。`GetBoardScale` は最小比率に加えて最大比率も受け取り、`GetBoardWidthDiameterRatio(int)` は board 幅に対する item 直径の比率を返します。

### 削除
- merge effect: `BoardItemInfo.MergingEffect` / `MergedEffect`、`MergingCommand` の effect 再生、および sample の merge effect prefab・material・texture を削除しました。当該 prefab は Unity 5 時代の legacy prefab format で保存されており、`ParticleSystemRenderer` に `serializedVersion` が無く大半の field が欠落し、0 番の material が null でした。このため最初の merge で effect を生成した瞬間に `ParticleSystemRenderer::PrepareForRender` で editor が native crash していました。

### 修正
- `MergingCommand`: 無効・cancel・failure となった command の merge reservation を解放し、すでに pool で再利用された item を変更しないように修正しました。
- `BoardItem`: pool から再利用する instance の presentation、collider、physics、support、merge state を次の初期化前に reset するように修正しました。
- `InputHandler`: touch の移動と終了が drag を開始した finger に紐づいたままになるように修正しました。
- `BoardArea`: `CalculateBounds(Camera, float)` と `CalculateBounds(Camera, Vector2, float)` が渡された aspect ratio を実際に使用するようにしました。従来は常に board sprite の比率にフォールバックしていました。
- `BoardController`: game end 判定を接触ベースに変更しました。上端が board line を越えた item が床または他の item に支えられている場合に game end とします。item は必ず line より上に置かれるため、まだ着地していない item は落下状態として除外され、左右の壁は支持対象から除外されます。
- `BoardController`: merge 中の item が command 完了まで board に登録されたままになり、同一 item の重複登録を防ぐようにしました。board を再準備するとすべての item が回収されます。従来は item が失われたり重複 entry が残って board が際限なく増えていました。誤記だった `OnDestory` を修正し、command が実際に破棄されるようにしました。
- `MergingCommand`: merge が二度以上開始・完了せず、command 破棄後には完了しないように修正しました。

## [watermelon/0.1.1] - 2026-08-07

### 追加
- `BoardItemInfo`: merge effect 用の任意 `ParticleHandler` resource と、生成 effect 完了後の自動 cleanup を追加しました。

### 修正
- `MergingCommand`: null の merge effect があっても merge command 全体を停止せずスキップするようにしました。

## [base/0.22.0] - 2026-08-07

### 追加
- `Mu3Library.Base`: `ParticleSystem` を必須コンポーネントとする `ParticleHandler` を追加し、再生、一時停止、停止、クリア、再起動、loop 制御、自然終了 `OnCompleted` を含むライフサイクルイベントを提供します。
- `Mu3Library.Base`: 必須の `SpriteRenderer` 背景をカメラのビューポートに合わせる `Mellow.Utility.WorldSpaceBackground` を追加しました。
- `GameObjectPool<T>` と `GameObjectPool<T, TArgs>`: 一括 enqueue/dequeue overload と、新規生成・pool から取得したオブジェクトへ適用できる任意の初期化 callback を追加しました。
- `GameObjectPool<T, TArgs>`: 生成 callback を引数なしに変更し、`CreateArguments` 派生クラスを受け取る型安全な初期化 callback を追加しました。

## [urp/0.2.1] - 2026-08-07

### 追加
- `Mu3Library.URP.Cam.CameraStackSetter`: 再利用可能な sample で Universal Camera Data を安全に保証する `EnsureUniversalCameraData(Camera)` を追加しました。

## [watermelon/0.1.0] - 2026-08-07

### 追加
- `Mu3Library.Game.WatermelonGame`: board、item、merge、configuration、input の runtime assembly を追加しました。
- `BoardArea`: board fitting、collision boundary、正規化位置変換、camera projection helper、初期化安全な `Try...` variant を追加しました。
- `BoardItemsConfig`: 0 始まりの index で扱う 11 個の固定 fruit item を追加しました。
- `BoardItemScoreRule`: 標準の三角数 progression を提供する virtual `GetScore(int)` score extension point を追加しました。
- Watermelon Game sample: `BoardConfig` asset、fruit/background image、sample manager/core script、プレイ可能な `Demo` scene を追加しました。

### 変更
- `BoardItemScaleRule`: item size が board area の local 幅を使用し、orthographic / perspective camera 計算に対応するよう変更しました。

### 修正
- `BoardArea`: board bounds と生成された boundary collider の配置を修正しました。
- `BoardItem`: 設定 sprite に合わせて円形 collider の半径を同期しました。

## [base/0.21.0] - 2026-08-03

### 変更
- `ButtonInvokeAttribute`: シリアライズされたフィールド方式を、引数なしで `void` を返すインスタンスメソッド方式へ置き換えました。適用されたメソッドを直接呼び出すため、同名のオーバーロードがあっても `AmbiguousMatchException` は発生しません。label とボタン高さは任意で、label を省略すると `Invoke {method name}` 形式で表示されます。

### 削除
- `ButtonInvokeAttribute` のフィールド適用、メソッド名引数、`drawProperty` オプション。

## [base/0.20.0] - 2026-08-03

### 追加
- `IAddressablesManager` / `AddressablesManager`: 複数のアセットをロードし、各 resource location の `PrimaryKey` でインデックスした `Dictionary<string, T>` を返す `LoadAssetsWithKeys<T>` と `LoadAssetsWithKeysAsync<T>` を追加しました。

### 変更
- `LocalizationDataExporterDrawer`: `Split by Table` トグルを削除し、共有 `{ClassName}Locales` スクリプト、各テーブルの `EntryData` インスタンスを含むテーブルごとの `TableData` 派生スクリプト、簡潔な root テーブルインデックスを常に生成するようにしました。

## [base/0.19.1] - 2026-08-02

### 修正
- `AddressablesManager`: `MU3LIBRARY_ADDRESSABLES_SUPPORT` と `MU3LIBRARY_UNITASK_SUPPORT` の両方が定義されている場合にのみ UniTask 実装がコンパイルされるように保護しました。
- `ResourceLoader`: キャッシュされた `GameObject`、`Component`、`AssetBundle` インスタンスはローダーのキャッシュから解放しつつ、`Resources.UnloadAsset` を直接呼び出さないように修正しました。

## [base/0.19.0] - 2026-07-29

### 追加
- `ICoreRoot` / `CoreRoot`: Core の準備完了を監視する `OnCorePrepared` イベントと、一度だけ実行する `SubscribeOnCorePreparedOnce<T>(Action)` / `SubscribeOnCorePreparedOnce(Type, Action)` API を追加しました。
- `CoreBase` / `IDICore`: Core の準備状態を確認する `IsPreparing` および `IsPrepared` を追加しました。

## [base/0.18.0] - 2026-07-28

### 追加
- `ICoreRoot` / `CoreRoot`: Core の初期化完了を監視する `OnCoreInitialized` イベントと、一度だけ実行する `SubscribeOnCoreInitializedOnce<T>(Action)` / `SubscribeOnCoreInitializedOnce(Type, Action)` API を追加しました。

### 変更
- `CoreBase` / `IDICore`: `IsPrepared` を `IsInitialized` に改名し、DI scope の初期化完了後に Core 初期化通知が発生するようにしました。
- `CoreRoot`: Core 初期化通知を Core scope の初期化後に行うようにし、初期化購読が破棄可能な `ISubscriptionInfo` token を返すようにしました。
- Template サンプル: `WaitForOtherCore` callback を Core 間の `[Inject]` 依存性に置き換えました。

### 削除
- `CoreBase.WaitForOtherCore<TCore>`: 旧 Core 間準備状態待機 helper を削除しました。

## [base/0.17.0] - 2026-07-27

### 追加
- `IObjectInjector`: `ContainerScope` の内部を公開せず、コンテナ外で生成されたオブジェクトにも既存の `[Inject]` フィールドとプロパティ注入を適用できる限定的な注入契約を追加しました。
- `MVPManager`: コンテナ管理下のインスタンスが presenter pool から新規作成または再利用された presenter に、初期化前に `[Inject]` メンバー注入を適用するようにしました。
- `Mu3Library.Foundation`: 再利用可能な subscription infrastructure のための Unity 非依存 runtime assembly を追加しました。

### 変更
- `AddressableGroupDataExporterDrawer`: 生成される Addressables データを `{ClassName}Labels` の文字列ラベルスクリプト、グループごとの `GroupData` 派生スクリプト、ネストした `EntryData` 派生アセットクラス、簡潔な root グループインデックスへ再構成しました。split トグルと `LabelData` ランタイム型を削除しました。
- `SubscribeHandler`: 再利用可能な one-shot subscription 実装を `Mu3Library.Foundation` へ移動し、`Mu3Library.Event` namespace と public API は維持しました。
- `SubscribeHandler`: logging integration を再設計するまで Foundation の診断ログを一時的にコメントアウトしました。
- `SubscribeHandler` / `SubscriptionInfo`: subscription lifecycle の冪等な解除、例外安全な cleanup、one-shot callback cleanup、内部 ID 衝突防止を強化しました。
- Event Bus interface と実装: one-shot subscription メソッドが `uint` ID ではなく、破棄可能な `ISubscriptionInfo` token を返すように変更しました。

## [base/0.16.0] - 2026-07-12

### 変更
- `ContainerScope`: `RegisterClass<T>()` で生成されたクラスを含む `CoreBase` 登録サービスが、生成後かつライフサイクル callback の前に `[Inject]` フィールドとプロパティへ自動注入されるようにしました。factory 生成インスタンスと事前生成して登録したインスタンスにも適用されます。

## [base/0.15.0] - 2026-07-01

### 追加
- `ISceneLoader` / `SceneLoader`: `PreloadSingleSceneWithAddressablesAsync`、`ActivateSingleSceneWithAddressablesAsync`、`LoadSingleSceneWithAddressablesAsync`、`PreloadAdditiveSceneWithAddressablesAsync`、`ActivateAdditiveSceneWithAddressablesAsync`、`LoadAdditiveSceneWithAddressablesAsync`、`UnloadAdditiveSceneWithAddressablesAsync` の Addressables UniTask helper を追加しました。
- `ISceneLoaderEventBus` / `SceneLoader`: single / additive scene lifecycle 更新向けに構造化 `SceneLifecycleInfo` callback を追加しました。payload は要求 target/key を維持したまま `ResolvedSceneName` を公開し、Addressables の runtime scene name がまだ解決されていない間は `UnnamedAddressableScene` を使います。
- `ScenePhase`: 構造化 lifecycle callback が additive unload 完了を明示的に報告できるよう `Unloaded` を追加しました。

## [urp/0.2.0] - 2026-06-24

### 追加
- `Mu3Library.URP.Cam.CameraStackSetter`: `SetCameraStackToMainAsFirst`、`SetCameraStackToMain(Camera)`、`SetCameraStackToMain(Camera, int)`、`SetCameraStackAsFirst(Camera, Camera)`、`SetCameraStack(Camera, Camera)`、`SetCameraStack(Camera, Camera, int)` helper を追加し、任意の URP overlay camera を `Camera.main` または明示的な root camera stack に挿入できるようにしました。

### 変更
- `Mu3Library_URP/package.json`、`README.md`、各ローカライズ README、各ローカライズ changelog: URP パッケージ version を `0.2.0` へ更新し、公開 UPM install tag 参照もあわせて更新しました。

## [urp/0.1.5] - 2026-06-21

### 削除
- `Mu3Library_URP/Runtime/Scripts/AGENTS.md`、`Mu3Library_URP/Runtime/Shaders/AGENTS.md`、`Mu3Library_URP/Samples~/AGENTS.md`: import 対象の URP パッケージ表面から package-local agent routing 文書を削除し、Unity package import 時に `.meta` とずれた AGENTS 文書が含まれないよう整理しました。

### 変更
- `Mu3Library_URP/package.json`、`README.md`、各ローカライズ README: URP パッケージ version を `0.1.5` へ更新し、公開 UPM install tag 参照もあわせて更新しました。

## [base/0.14.2] - 2026-06-20

### 変更
- `IMVPManager` / `MVPManager` / `PresenterBase`: `OpenOptions` を削除し、直接 `HostOptions` を受け取るオーバーロードへ置き換えつつ、`Open` / `OpenAsChild` の convenience オーバーロードは引き続き最後の明示 open シグネチャ 1 つへだけ委譲する形を維持しました。

## [base/0.14.1] - 2026-06-17

### 追加
- MVP UI runtime に `OpenOptions` と `HostOptions` を追加しました。これにより、連結して開く presenter で ownership と visual host、およびルートレイアウト配置を分けて設定できます。

### 変更
- `IMVPManager` / `MVPManager` / `PresenterBase`: 連結 presenter の open フローを `owner` 用語と明示的な host option 中心へリファクタリングしました。`HostOptions.Host` を指定しない場合は owner のルート view 配下へ配置する既定動作を維持しますが、毎回の open では owner の `RectTransform` 値をコピーする代わりに child view resource のルートレイアウトを再適用します。

## [base/0.14.0] - 2026-05-25

### 追加
- `ButtonInvokeAttribute` / `ButtonInvokeAttributeDrawer`: シリアライズされたフィールドに引数なしのインスタンスメソッドを呼び出す Inspector ボタンを表示する機能を追加し、Attribute サンプルでも `ConditionalHideAttribute` とあわせて例を追加しました。

## [base/0.13.0] - 2026-05-24

### 追加
- `GameObjectPool<T>`: 利用側がプール空時の生成を定義できる任意の `Create` デリゲートコンストラクターと、プール済みの非アクティブオブジェクトを破棄する `Clear()` を追加しました。

### 変更
- `GameObjectPool<T>`: 内部 `List<T>` を `Queue<T>` に切り替え、プール済みインスタンス ID を追跡して重複した非アクティブオブジェクトの再登録を防ぎ、resource 参照から直接 instantiate しないように変更しました。
  以前の `GameObjectPool(T resource)` コンストラクターは削除されたため、呼び出し側は `GameObjectPool(Create onCreate)` へ移行し、生成ロジックを明示的に渡す必要があります。

## [urp/0.1.4] - 2026-05-24

### 変更
- `Mu3Library_URP/package.json`: URP manifest が `com.github.doqltl179.mu3library.base` `0.13.0` に依存するよう更新し、パッケージメタデータを Base `0.13.0` に揃えました。

## [base/0.12.0] - 2026-05-24

### 追加
- `ISceneLoaderEventBus` / `SceneLoader`: 既存の `SubscribeOnSingleSceneLoadedOnce` を超えて、single-scene の `LoadStarted`、`Preloaded`、`Changed` コールバックと、additive の `LoadStarted`、`Preloaded`、`Loaded`、`Unloaded` コールバックまで one-shot 購読 helper を拡張しました。
  この変更により `ISceneLoaderEventBus` の実装契約が変わるため、custom 実装は upgrade 時に新しい one-shot 購読 method も実装する必要があります。
- `ILocalizationManagerEventBus` / `LocalizationManager`、`IAddressablesManagerEventBus` / `AddressablesManager`、`IMVPManagerEventBus` / `MVPManager`: localization の初期化完了/結果イベント、Addressables の初期化イベント、MVP の window lifecycle イベント向けに one-shot 購読 helper を追加しました。
  この変更により各 event-bus の実装契約が変わるため、custom 実装は upgrade 時に新しい one-shot 購読 method も実装する必要があります。
- `SubscribeHandler`: `Action`、`Action<T>`、`Action<T1, T2>` 登録向けの再利用可能な `SubscribeOnce(...)` オーバーロードを追加し、各 service が自分の handler instance を通して one-shot 購読を管理できるようにしました。

### 変更
- `SceneLoader`: `OnSingleSceneLoaded`、`OnAdditiveSceneLoaded`、`OnAdditiveSceneUnloaded` は、優先的に `SceneManager.sceneLoaded` / `SceneManager.sceneUnloaded` のタイミングへ揃えるように変更しました。一方で `OnAdditiveScenePreloaded` は引き続き activation 前の milestone のままです。あわせて Built-in / Editor の additive unload は `allowSceneActivation` で完了を遅延させなくなり、unload progress は基盤となる async operation の値をそのまま反映します。

## [base/0.11.0] - 2026-05-02

### 追加
- `.github/workflows/unity-compile-gate.yml`: `scripts/compile-gate/run-unity-compile.ps1` を実行する manual self-hosted Windows ワークフローと、push / pull request イベントで動く GitHub-hosted の案内 job を追加。

### 変更
- `IWebRequestManager` / `WebRequestManager`: UniTask WebRequest API に任意の `propagateCancellation` フラグを追加。デフォルトではキャンセルを失敗/既定値の経路として扱い、明示的なキャンセルが必要な呼び出し側だけが opt-in できるように変更。
- `ISceneLoader` / `SceneLoader`: シーン読み込み API を明示的な `Preload*`、`Activate*`、`Load*`、`Unload*` コマンド中心へ簡素化。fake loading 制御を削除し、phase/status 参照と同じ命名規則の `*Async` 待機 helper を追加し、Editor と Addressables のシーン読み込み surface も同じフローに揃えました。
  - `ISceneLoaderEventBus` を `LoadStarted`、`Preloaded`、`Loaded`、`Unloaded` の lifecycle callback 中心に保ちつつ progress callback を復元し、rejection 報告を `OnSceneCommandRejected(SceneCommandRejectedInfo)` へ統合したうえで、single-scene 遷移用の `OnSingleSceneChanged(previousSceneName, loadedSceneName)` を追加しました。
  - `UseFakeLoading`、`FakeLoadingTime`、および従来の CancellationToken ベース scene async helper 契約を削除。

### 修正
- `README.md` と各ローカライズ README: WebRequest の opt-in キャンセル伝播動作を文書化。

## [urp/0.1.3] - 2026-05-02

### 変更
- `Mu3Library_URP/package.json`: URP manifest が `com.github.doqltl179.mu3library.base` `0.11.0` に依存するよう更新し、パッケージリリースを Base `0.11.0` に揃えました。

## [urp/0.1.2] - 2026-04-26

### 追加
- `ShakeEffect` / `ShakePass`: URP の shake screen effect で振幅とは独立してループ周期を制御できるよう、`SetPeriod(float period)` を追加。
- `GaussianBlurEffect` / `GaussianBlurPass`: 対応する pass と shader 実装を含む新しい URP フルスクリーン gaussian blur effect を追加。
- `DepthOutlineEffect` / `DepthOutlinePass`: threshold を変えずに depth ベースの outline を太くできるよう、`SetOutlineThickness(float outlineThickness)` と対応するサンプル slider を追加。

### 変更
- `IScreenEffect` / `IScreenEffectManager`: URP ScreenEffect の契約インターフェース名を `IPassInjector` から変更し、マネージャー登録 API を `RegisterPass` / `UnregisterPass` から `RegisterEffect` / `UnregisterEffect` に整理して、現在の effect ベースのフローと公開 API 名を一致させました。
- `ScreenEffectBase` / `ScreenPassBase`: カスタム URP ScreenEffect と Pass 実装向けの共通基底クラスを追加し、active 状態、dispose、pass 生成、shader/material のライフサイクル管理を共通化しました。
- `ScreenEffectManager` / `IScreenEffectManager`: URP の ScreenEffect パス登録クラスとインターフェース名を、現在の責務に合わせて `PostVolumeManager` / `IPostVolumeManager` から改名。Unity Volume ベースの責務を表さなくなったため。
- `ScreenEffect` サンプル: `ScreenEffectCore` を既存の handler 中心セットアップのまま維持し、grayscale / shake / gaussian blur / depth outline と同じ統合パターンで effect を追加できるよう、対応する sample handler スクリプトを追加。
- `GaussianBlurEffect` / `GaussianBlurPass`: フルスクリーン blur API surface、sample handler、serialized sample field、sample scene object 名を正式な gaussian blur 命名へ確定し、公開調整項目も `Blur Radius` に統一。もしこのブランチ上の未リリース blur プロトタイプを採用していた場合は `GaussianBlur*` へ移行が必要。

### 修正
- `ShakeEffect` / `ShakePass`: `SetPeriod(float period)` の変更時にアニメーション途中で揺れ位置が別オフセットへ跳ばないよう、現在の位相を維持するよう修正。
- `Mu3Library_URP/package.json`: `ScreenEffect` サンプルをパッケージ manifest の `samples` 一覧に公開し、Unity Package Manager から検出およびインポートできるよう修正。

## [base/0.10.0] - 2026-04-26

### 追加
- `IMVPManager`: 親リンク付き `Open<TPresenter>(IPresenter parent, ...)` オーバーロード 4 種を追加 (引数なし、`Arguments` あり、`OutPanelSettings` あり、両方あり)。親リンクで presenter を開くと、子の RectTransform が親に連結され、anchored position・size delta・local scale を引き継ぎます。
- `IPresenter`: ランタイムで presenter の RectTransform レイアウト値を読み書きできるよう `AnchoredPosition`、`SizeDelta`、`LocalScale` プロパティを追加。

## [base/0.8.0] - 2026-04-05

### 追加
- `AudioManager`: `PlayBgmPlaylist(AudioClip[] clips, ...)` および `StopBgmPlaylist()` による BGM プレイリスト機能を追加。
  - `AudioClip` の配列を受け取り、順番に連続再生する。
  - `loopCount`: 0 以下 = 無限サイクル; 正の値 = その回数だけ全サイクルを再生 (デフォルト: -1)。
  - `shuffle`: 各サイクル前に Fisher-Yates アルゴリズムで再生順をランダム化 (デフォルト: false)。
  - `interval`: トラック間の待機時間（秒）(デフォルト: 1.0)。
  - `PlaySfx` と同じパターンで 8 種類のオーバーロードを提供。
  - `PlayBgmPlaylist` 呼び出し時、現在再生中の BGM を先に停止する。
  - `StopBgm` または `StopBgmPlaylist` 呼び出し時にプレイリストを非アクティブ化する。
  - インターバルのカウントダウンはポーズを考慮し、BGM が一時停止中はタイマーが進まない。
- `IAudioManager`: 新しい `IAudioManager.BgmPlaylist.cs` partial ファイルを通じて `PlayBgmPlaylist` オーバーロードおよび `StopBgmPlaylist` を追加。
- `ResourcesPathExporterDrawer`: プロジェクト内の `*/Resources/*` パスのアセットを自動スキャンし、フォルダー階層を入れ子 static クラスで表現する C# スクリプトを生成するエディター Drawer。各アセットはリソース相対パス（拡張子なし）とファイル名を保持する `ResourcePathData` フィールドとして公開される。
- `ResourcePathData`: `Path` と `Name` 文字列プロパティを持つ `Mu3Library.Resource.Data` 名前空間の新しいクラス。

### 変更
- `LocalizationNameExporterDrawer`、`AddressableGroupNameExporterDrawer`、`InputSystemNameExporterDrawer`: それぞれ `LocalizationDataExporterDrawer`、`AddressableGroupDataExporterDrawer`、`InputSystemDataExporterDrawer` に改名。関連するサンプル `.asset` ファイルも同様に改名。
- `LocaleData`、`EntryData`、`TableData`: `Mu3Library.Localization.Data` 名前空間のスタンドアロン public クラスに移動; コンストラクターを `internal` から `public` に変更; `#if MU3LIBRARY_LOCALIZATION_SUPPORT` ガードを削除（Unity.Localization への依存なし）。
- `EntryData`: `TableName` プロパティを追加; コンストラクターが `EntryData(string tableName, string key, string id)` に更新。
- `LocalizationDataExporterDrawer`: 生成スクリプトに `LocaleData`・`EntryData`・`TableData` クラス定義をインラインで含めず、`using Mu3Library.Localization.Data;` でインポート。`EntryData` 構築時に最初の引数としてテーブル名を渡すよう変更。
- `LabelData`、`EntryData`、`GroupData`: `Mu3Library.Addressable.Data` 名前空間にスタンドアロン public クラスとして追加（`#if` ガードなし; 純粋 C#）。`GroupData` は生成される per-group sealed class の基底クラスとなり、`Name`、`Entries`、`Labels` 辞書を保持。
- `AddressableGroupDataExporterDrawer`: 生成スクリプトの構造を Localization パターンに合わせるよう変更 — `Labels` クラスは `const string` の代わりに `LabelData` インスタンスを保持; `Groups` クラスは型付き `*Data` グループインスタンスと `IReadOnlyDictionary<string, GroupData> All` を保持; per-group クラスは `sealed class *Data : GroupData` 形式で生成。非フォルダーエントリーは `EntryData` フィールド、フォルダーエントリーは `EntryData Data` フィールドと `Assets` 内部クラスを維持。生成コードに `using Mu3Library.Addressable.Data;` を含む。

## [base/0.6.0] - 2026-03-23

### 追加
- `MVPManager` / `IMVPManager`: `FocusIgnoredLayers` プロパティと `SetFocusIgnoredLayer(string layerName, bool ignored)` メソッドを追加。
  - 無視（ignored）レイヤーの Presenter はフォーカスおよび `OutPanel` 更新の計算から除外される。
  - 無視レイヤーは実行時にトグル可能で、変更時に即座に `UpdateFocus()` が呼ばれる。
- `LocalizationNameExporterDrawer`: 生成スクリプトにルート `Locales` クラス（`All` 文字列配列、およびロケールごとに `Code`・`EnglishName`・`NativeName` を `const string` で公開する内部クラス）とルート `Tables` クラス（`All` 文字列配列、および各テーブルクラスの `Name` を参照する `const string` エントリ）を追加。各テーブルクラスにも、ルート `Locales` 構造を `const string` 参照でミラーリングする `Locales` 内部クラスが追加される。
- `AddressableGroupNameExporterDrawer`: 生成スクリプトにルート `Groups` クラス（グループクラスの `Name` を参照する `const string` エントリと `All` 配列）、ルート `Labels` クラス（全グループ・エントリから収集した一意のラベルを `All` 配列と `const string` 値で提供）、およびルート `Labels` エントリを `const string` 参照でミラーリングするグループごとの `Labels` 内部クラスを追加。

## [base/0.5.0] - 2026-03-18

### 変更
- リポジトリをモノレポ構成に再編: `Mu3Library_Base/` と `Mu3Library_URP/` は独立した UPM パッケージに、`UnityProject_BuiltIn/` と `UnityProject_URP/` は別個の開発プロジェクトとして分離。
- `.gitignore` のパターンに `**/` プレフィックスを追加し、モノレポ配下の全サブプロジェクトを対象に包含するよう改善。

### 修正
- `CoreBase.WaitForOtherCore`: `CoreRoot.Instance` が null のとき（例: アプリ終了時）に発生していた `NullReferenceException` を修正。
- `CoreBase.GetClassFromOtherCore`: 同様の null 安全処理を適用。
- `ContainerScope.ResolveFromCore`: 同様の null 安全処理を適用。
- ドキュメント: 全 README の `ConfigureContainer()` コード例を修正 — 誤った `ContainerScope scope` パラメーターを削除し、サービス登録に `RegisterClass<T>()` を使用するよう修正。

## [base/0.4.7] - 2026-03-15

### 追加
- `ScriptBuilder`: `ArrayBlock` 構造体（`FieldName`、`Values`）と `AppendArrayBlock` メソッドを追加。
  - `ArrayBlock` を `CodeBlock.Content` リストに `string`・`CodeBlock` と並べて配置可能。
  - インデントは `ScriptBuilder` が自動処理し、`CodeBlock` の出力と統一。

### 変更
- `AddressableGroupNameExporterDrawer`: `BuildArrayLines` ヘルパーを `ScriptBuilder.ArrayBlock` で置き換え。
  - `AllNames`・`AllAddresses`・`Labels.All` 配列の宣言が `foreach` ループから単一の `.Add()` 呼び出しに短縮。

## [base/0.4.6] - 2026-03-15

### 追加
- `AudioManager.Resource`: キーベースの `AudioClip` 登録システムを追加。
  - `RegisterAudioResource(string key, AudioClip clip)`: 単一のクリップをキーに登録。
  - `RegisterAudioResources(Dictionary<string, AudioClip> resources)`: 複数のクリップを一括登録。
- `IAudioManager` / `AudioManager`: 登録済みキーでオーディオを再生する `WithKey` オーバーロードを全チャンネルタイプに追加。
  - BGM: `PlayBgmWithKey`, `PlayBgmForceWithKey`, `TransitionBgmWithKey`
  - SFX: `PlaySfxWithKey`, `StopFirstSfxWithKey`, `FadeInSfxWithKey`, `FadeOutFirstSfxWithKey`
  - Environment: `PlayEnvironmentWithKey`, `StopFirstEnvironmentWithKey`, `FadeInEnvironmentWithKey`, `FadeOutFirstEnvironmentWithKey`

### 変更
- `IAudioManager.Bgm`, `IAudioManager.Sfx`, `IAudioManager.Environment`: インターフェース宣言をアルファベット順に並べ替え、アクション種別ごとにグループ化して可読性を向上。
- `AudioManager.Bgm`, `AudioManager.Sfx`, `AudioManager.Environment`: publicメソッドをアルファベット順に並べ替え。
- `WithKey` オーバーロードは委譲パターンを使用 — 短いオーバーロードはフル引数のオーバーロードに委譲し、`TryGetCachedAudioResource` の呼び出しはそこで一度だけ行われる。

## [base/0.4.5] - 2026-03-14

### 変更
- `AddressableGroupNameExporterDrawer`: サブアセットのクラス名が親クラス名で始まる場合、その接頭辞を除去するよう変更。
  - 例: 親 `Views`、サブアセット `ViewsDialoguePanelPrefab` → `DialoguePanelPrefab` として出力。
  - ネストされたフォルダー階層にも再帰的に適用。

## [base/0.4.4] - 2026-03-14

### 変更
- `AddressableGroupNameExporterDrawer`: フォルダーエントリーのサポートを追加。
  - `AssetDatabase.IsValidFolder()` でグループにフォルダーとして登録されたアセットを検出。
  - フォルダーエントリーの場合、`GatherAllAssets()` でサブアセットを収集し、`Assets` inner static class にネストして出力。
  - エディタープレビューでフォルダーエントリーは `[Folder]` プレフィックスで示され、サブアセットはインデントして表示。

## [base/0.4.3] - 2026-03-14

### 追加
- `AddressableGroupNameExporterDrawer`: エディター上で全 Addressable グループを読み取り、グループ名・アセット名・アドレス(key)・ラベルをネストした C# static クラスとして書き出すエディタードロワーを追加（`MU3LIBRARY_ADDRESSABLES_SUPPORT` 条件付きコンパイル）。
  - `Labels` 内部クラスにラベルごとの `const string` フィールドと、全ラベル値を格納した `static readonly string[] All` を提供。
- `UtilWindow`: ユーティリティウィンドウのドロワー一覧に `AddressableGroupNameExporter` サンプルアセットを追加。
- `Template`: Addressable グループ/アドレス定数の生成例として `AddressableGroupKeys` を追加。
- `Mu3Library.Editor.asmdef`: `Unity.Addressables` および `Unity.Addressables.Editor` のオプション参照と `MU3LIBRARY_ADDRESSABLES_SUPPORT` バージョン定義を追加。

## [base/0.4.2] - 2026-03-08

### 追加
- `LocalizationNameExporterDrawer`: Localization の string table 名と entry key を C# 定数として書き出し、事前宣言された参照に使えるエディタドロワーを追加。
- `UtilWindow`: ユーティリティウィンドウのドロワー一覧に `LocalizationNameExporter` サンプルアセットを追加。
- `Template`: Localization テーブル/キー定数の生成例として `LocalizationTableKeys` を追加。

### 変更
- `InputSystemNameExporterDrawer` と `LocalizationNameExporterDrawer`: 動作は変えずに、backing field とキャッシュ済み accessor を区別しやすいよう private serialized helper メンバー名を整理。

### 修正
- `LocalizationNameExporterDrawer`: エントリキーから正しい PascalCase クラス名を生成するよう `SanitizeIdentifier` を修正。`-` などの非識別子文字は単語境界として扱われ、省略されて次の文字が大文字化。`_` はそのまま出力され次の文字も大文字化（例: `my-key_name` → `MyKey_Name`）。

## [base/0.4.0] - 2026-03-08

### 追加
- `AudioSourceSettings`: ループ動作をインスタンスごとに制御できる `LoopCount` および `LoopInterval` プロパティを追加。
  - `LoopCount`: 再生回数（`≤0` = 無限ループ、`1` = 1回再生）。
  - `LoopInterval`: ループサイクル間の待機時間（秒）。
- `AudioSourceSettings`: よく使われる設定のための名前付きプリセットインスタンスを追加。
  - `Standard`（無限ループ、2D）、`OneShot`（1回再生、2D）
  - `BgmStandard`、`BgmStandard3D`
  - `SfxStandard`、`SfxStandard3D`
  - `EnvironmentStandard`、`EnvironmentStandard3D`
- `Audio3dSoundSettings.Standard3D`: 完全な3D空間ブレンド（`spatialBlend = 1`）を持つ新しいプリセットを追加。
- `AudioController`: `AudioSourceSettings` の `LoopCount` および `LoopInterval` によって制御されるインターバル付きループ再生機能を追加。
- `AudioController`: 完了コールバックをサポートする `FadeIn` / `FadeOut` コルーチン API を追加。

### 変更
- `FadeInFirstSfx(AudioClip, float)` を `FadeInSfx(AudioClip, float)` に改名し動作を変更: 既存の再生中インスタンスを対象とする代わりに、**新しい SFX インスタンス**をボリューム `0` から再生してフェードイン。
- `FadeInFirstEnvironment(AudioClip, float)` を `FadeInEnvironment(AudioClip, float)` に改名し、同様の動作変更を適用。
- `IAudioManager`: `SourceSettings`、`BaseSettings`、`SoundSettings` プロパティを削除（呼び出しごとの `AudioSourceSettings` パラメータで代替）。
- `AudioManager` および `IAudioManager` をカテゴリ（`Bgm`、`Sfx`、`Environment`）別の partial クラスファイルに分割。公開 API の変更なし。

## [base/0.3.3] - 2026-03-02

### 追加
- `AudioManager`: 環境音再生のための `EnvironmentController` 機能を追加。
  - 新しい `EnvironmentController` クラス: `EnvironmentVolume` をカテゴリボリュームとして音声を再生。
  - `EnvironmentInstanceCountMax` プロパティを追加（デフォルト: `3`、最大: `5`）。
  - `EnvironmentVolume`、`CalculatedEnvironmentVolume`、`ResetEnvironmentVolume()` を `AudioManager` および `IVolumeSettings` に追加。
  - `PlayEnvironment`、`StopFirstEnvironment`、`StopEnvironmentAll`、`PauseEnvironmentAll`、`UnPauseEnvironmentAll` メソッドを `AudioManager` および `IAudioManager` に追加。
  - `OnEnvironmentVolumeChanged` イベントを `IAudioManagerEventBus` に追加。
  - `Stop()`、`Pause()`、`UnPause()` が環境音も対象に含むよう更新。

## [base/0.3.2] - 2026-03-02

### 修正
- `Mu3WindowDrawer`: 派生 Drawer での `BeginChangeCheck` / `RecordObject` / `SetDirty` の定型コードを排除するため、基底クラスに `DrawWithUndo<T>(Func<T>, Action<T>, string)` ヘルパーを追加。
- `Mu3WindowDrawer`: `DrawFoldoutHeader1` および `DrawFoldoutHeader2` が明示的な `!=` 比較ではなく `EditorGUI.BeginChangeCheck` / `EndChangeCheck` パターンに統一された。
- `DependencyCheckerDrawer`、`FileFinderDrawer`、`InputSystemNameExporterDrawer`、`MVPHelperDrawer`、`ScreenCaptureDrawer`: すべてのインタラクティブフィールドが新しい `DrawWithUndo<T>` ヘルパーを通じて undo/redo 状態を正しく記録するよう修正。

## [base/0.3.1] - 2026-03-02

### 修正
- `MVPManager`: View が Load 中に素の状態（例: alpha 1）で一フレームレンダリングされる同期ズレを修正。
  - `Open()` 呼び出し時に即座に `SetActiveView(true)` していた処理を `SetActiveView(false)` に変更し、
    Load 完了後・`Open()` 開始直前に `SetActiveView(true)` を呼ぶように修正。
  - これにより、アニメーション（例: alpha 0→1）が View の初期状態と同期してから開始されるようになります。

## [base/0.3.0] - 2026-03-01

### 追加
- `InputSystemManager`: 新しい Input System モジュールを追加（`MU3LIBRARY_INPUTSYSTEM_SUPPORT` が必要）:
  - カスタム ID で `InputActionAsset` を登録; GUID ベースおよび名前ベースのアクション/マップ検索をサポート。
  - `StartInteractiveRebind(...)` による対話的リバインド; デバイスタイプフィルタリングとキャンセルコントロールをサポート。
  - エセット/アクションマップ/アクション単位でのバインディングオーバーライドの JSON 保存・適用。
  - エセット全体または個別アクションマップの有効化/無効化。
- `InputSystemNameExporterDrawer`: Input System アクション名を文字列定数としてエクスポートするエディタドロワーを追加。
- `LocalizationCharacterCollectorDrawer`: Localization ストリングテーブルから文字を収集・確認するエディタドロワーを追加。
- `PresenterBase.CloseSelf(bool forceClose = false)`: Presenterが外部の呼び出し元を必要とせず、注入された `IMVPManager` 参照を通じて自分自身を閉じることができます。

### 変更
- `PresenterBase.Initialize(View, Arguments)` および `PresenterBase.Initialize(Arguments)` が `public` から `internal` に変更されました。
  - 初期化処理は `MVPManager` が独占的に管理するようになり、外部コードから直接呼び出すことはできません。
- `LayerCanvas` が各アイテムに合わせて Layer 値を自動的に同期するようになりました。

## [base/0.2.3] - 2026-02-16
### 変更
- Audio ボリューム契約から EventBus 継承を分離:
  - `IAudioVolumeSettings` は `IAudioManagerEventBus` を継承しません。
- Observable API が外部利用向けの読み取り専用公開をサポート:
  - `Value` + `Subscribe(...)` 参照用の `IObservableValue<TValue>` を追加
  - `ObservableProperty<T>` と `ObservableDictionary<TKey, TValue>` に `ReadOnly` 公開を追加
  - 購読トークン処理を専用 `SubscriptionToken` ファイルへ分離
- MVP UI 設定とランタイム安全性を改善:
  - `OutPanelSettings` は明示的なシリアライズ済みバックフィールドを持つシリアライズ可能構造体に更新
  - `OutPanelSettings.Standard` のデフォルト dim カラーのアルファ値を `0.5f` に変更
  - `MVPManager` はフォーカス更新時に `EventSystem` の存在を検証し、欠落時に明示的なエラーログを出力

## [base/0.2.0] - 2026-02-14

### 追加
- Scene UniTask 非同期 API を追加（キャンセル対応）:
  - `ISceneLoader.LoadSingleSceneAsync`
  - `ISceneLoader.LoadAdditiveSceneAsync`
  - `ISceneLoader.UnloadAdditiveSceneAsync`

### 変更
- Addressables/Localization の初期化契約が明示的な結果状態を提供:
  - `IsInitialized`
  - `IsInitializing`
  - `InitializeError`
  - `OnInitializeResult` イベント
  - `InitializeWithResult(Action<bool, string>)` API
- WebRequest API に構造化された結果型を追加:
  - `WebRequestResult<T>` (`IsSuccess`, `StatusCode`, `ErrorMessage`, `ResponseHeaders`, `Data`)
  - コールバック API: `GetWithResult`, `PostWithResult`, `GetDownloadSizeWithResult`
  - UniTask API: `GetResultAsync`, `PostResultAsync`, `GetDownloadSizeResultAsync`
  - 結果型 API にリクエストのタイムアウト/リトライ設定を追加
- CoreBase のシリアライズ実行順序設定により Core 実行順序の決定性を強化
- Scene アンロードのライフサイクルイベントを明示化:
  - `OnAdditiveSceneUnloadStart`
  - `OnAdditiveSceneUnloadEnd`
  - `LoadingCount` が Additive アンロード処理を含むように改善
- サービスイベント契約を専用の EventBus インターフェースへ分離:
  - `IAddressablesManagerEventBus`
  - `ILocalizationManagerEventBus`
  - `ISceneLoaderEventBus`
  - `IMVPManagerEventBus`
  - `IAudioManagerEventBus`
  - 既存サービスインターフェースはこれらの `event` メンバーを直接宣言しない

## [base/0.1.11] - 2026-02-08

### 🌟 概要

**大規模なアーキテクチャの見直し** - 新しいDIコンテナシステム、強化されたMVPパターン、包括的なマネージャーシステムを含む完全なパッケージ構造の再編。このリリースは、ライブラリのコアアーキテクチャの根本的な再設計を表しています。

### ⚠️ 破壊的変更 (BREAKING CHANGES)

#### パッケージ構造
- **完全なフォルダー構造の再編成**: `Runtime/`と`Editor/`ディレクトリに分離
- **アセンブリ定義の更新**: 適切な依存関係を持つ新しいasmdefファイル
- **ネームスペースの変更**: すべてのコードが`Mu3Library`ネームスペース階層に移動
  - `Mu3Library.DI` - 依存性注入(Dependency Injection)
  - `Mu3Library.MVP` - UIパターン
  - Audio、Sceneなどのモジュール固有のネームスペース

#### API変更
- **MVPパターン**: MVPシステムの完全な書き直し
  - `Presenter<TView, TModel, TArgs>`シグネチャの変更
  - `View`ライフサイクルメソッドの構造変更
  - `MVPManager` APIの完全な再設計
  - アニメーションシステムがMVPビューに統合
- **DIコンテナ**: 新しい注入システムが手動初期化に置き換わる
  - 依存性注入のための`[Inject]`属性
  - `CoreBase`と`CoreRoot`ベースのアーキテクチャ
- **ResourceLoader**: DIベースに変更、もはや静的ではない
- **SceneLoader**: 個別の実装を持つインターフェースベース
  - Editorシーンのための`ISceneLoader.Editor`
  - Addressablesシーンのための`ISceneLoader.Addressables`

#### 削除された機能
- ❌ **InputSystemヘルパー** (`InputSystem_Actions`, `InputSystemHelper`)
- ❌ **MarchingCubesシステム** (コンピュートシェーダーシステム全体を削除)
- ❌ **PostEffect/CommandBufferエフェクト** (Blur、EdgeDetect、GrayScale、Toonシェーダー)
- ❌ **カメラビューコンポーネント** (FirstPerson、ThirdPerson、FreeViewカメラ)
- ❌ **カスタムUIコンポーネント** (DatePicker、IntSlider)
- ❌ **古いサンプルシーン** (Sample_InputAction、Sample_CustomUI、Sample_CommandBufferEffect、Sample_CameraView、Sample_MarchingCubes、Sample_RenderingPipeline)

### ✨ 主要機能

#### 🏗️ 依存性注入(DI) Container
- 3つのライフタイムスコープを持つカスタムDIコンテナ:
  - `Singleton`: コンテナごとに1つのインスタンス
  - `Transient`: リクエストごとに新しいインスタンス
  - `Scoped`: スコープごとに1つのインスタンス
- モジュール式システム設計のための`CoreBase`アーキテクチャ
- 自動依存性解決のための`[Inject]`属性
- `[Inject(typeof(OtherCore))]`によるクロスコア注入のサポート
- インターフェースベースのライフサイクル管理:
  - 初期化のための`IInitializable`
  - UpdateループのためのI`Updatable`
  - LateUpdateループのための`ILateUpdatable`
  - クリーンアップのための`IDisposable`

#### 🎨 強化されたMVPパターン
- DI統合を備えた完全に再設計されたMVPシステム
- **AnimationView**システムと設定可能なアニメーション:
  - シングルカーブアニメーションのための`OneCurveAnimation`
  - デュアルカーブアニメーションのための`TwoCurveAnimation`
  - 再利用可能な設定のための`AnimationConfig` ScriptableObject
- **MVPCanvasSettings**による細かいCanvas設定:
  - Canvasコンポーネント設定
  - CanvasScaler設定
  - GraphicRaycaster設定
  - 背景/ディミングのためのOutPanelシステム
- 適切な初期化順序を持つ強化されたビューライフサイクル
- リソースベースおよびカメラベースのビュー読み込み
- ローディング画面の統合

#### 📦 マネージャーシステム
- **AddressablesManager**: キャッシングを含む完全なAddressablesサポート
  - 参照カウントによるアセットのロード/アンロード
  - シーン読み込みのサポート
  - 進行状況の追跡
  - UniTask統合
- **LocalizationManager**: Unityローカライゼーション統合
  - 非同期初期化
  - ロケール切り替え
  - 文字列テーブルへのアクセス
  - UniTaskサポート
- **WebRequestManager**: HTTPリクエスト処理
  - GET/POSTリクエスト
  - ダウンロードサイズのクエリ
  - UniTask統合
  - コールバックベースの代替
- **AudioManager**: 強化されたオーディオシステム
  - 3D空間オーディオサポート
  - 個別のBGMとSFXコントローラー
  - `IVolumeSettings`を通じたボリューム管理
  - AudioSourceプーリング
- **SceneLoader**: 柔軟なシーン読み込み
  - エディターシーン読み込み
  - Addressablesシーン読み込み
  - 追加(Additive)シーンサポート
  - 進行状況イベント
  - シーン読み込みポリシー(重複許可/防止)
- **ResourceLoader**: 強化されたResourcesフォルダー管理
  - 型安全な読み込み
  - 参照カウントによるキャッシング
  - UniTaskサポート

#### 🔧 ユーティリティ & 拡張機能
- **Observable型**: データバインディングサポート
  - `ObservableProperty<T>`, `ObservableBool`, `ObservableInt`, `ObservableFloat`, `ObservableLong`, `ObservableString`
  - コレクション変更イベントを持つ`ObservableList<T>`
  - ディクショナリイベントを持つ`ObservableDictionary<TKey, TValue>`
- **GameObjectPool**: コンポーネントプーリングシステム
- **Extensions**: 豊富な拡張メソッド
  - `CameraExtensions`: カメラプロパティのコピー
  - `TransformExtensions`: 子を含むレイヤー管理
  - `intExtensions`: ビット演算
  - Canvas関連の拡張
- **PlayerPrefsLoader**: 型安全なPlayerPrefsアクセス

### 🎯 追加

#### コアシステム
- モジュール式アーキテクチャのための`CoreBase`と`CoreRoot`
- サービス登録と解決のための`ContainerScope`
- サービス設定のための`ServiceDescriptor`
- インターフェースを通じた自動ライフサイクル管理

#### UI/MVP
- ビューアニメーションのための`AnimationHandler`
- `AnimationConfig`, `OneCurveAnimation`, `TwoCurveAnimation` ScriptableObject
- UI背景のための`OutPanel`システム
- 細かいキャンバス制御のための`MVPCanvasSettings`
- CameraとResourceバリアントを持つ`IMVPManager`インターフェース

#### マネージャー
- 完全なCRUD操作を持つ`IAddressablesManager`
- ローカライゼーションのための`ILocalizationManager`
- ネットワークリクエストのための`IWebRequestManager`
- ボリューム制御インターフェースを持つ`IAudioManager`
- EditorとAddressables実装を持つ`ISceneLoader`
- Resources管理のための`IResourceLoader`
- PlayerPrefsのための`IPlayerPrefsLoader`

#### エディターツール
- **Mu3Window**: 統合ユーティリティウィンドウ
  - MVPHelper: MVPボイラープレートコードの生成
  - SceneList: 高速シーンナビゲーション
  - FileFinder: アセット検索と整理
  - ScreenCapture: エディター内スクリーンショット
  - DependencyChecker: パッケージ依存関係の検証
- Observable型のためのカスタムプロパティドロワー
- コード生成ヘルパー`ScriptBuilder`

#### サンプル
- **Template**: 包括的なサンプルプロジェクト
  - Sample_MVP: MVPパターンのデモ
  - Sample_Audio: オーディオシステムのショーケース
  - Sample_Audio3D: 3D空間オーディオの例
  - Sample_WebRequest: HTTPリクエストの例
  - Sample_Addressables: アセット読み込みのデモ
  - Sample_AddressablesAdditive: 追加シーン読み込み
  - Sample_Localization: 多言語サポート
  - LoadingScreenの実装
  - アニメーション付きスプラッシュ画面

#### アセット
- 基本色マテリアル(Black、White、Red、Green、Blue、Magenta)
- サンプルフォント(SDFを含むNotoSans、NotoSansJP、NotoSansKR)
- サンプルBGMトラック(3曲)
- サンプルSFXサウンド(3つのエフェクト)
- UIテクスチャアセット(影付きの円、1pxの正方形)
- サンプル用のシーンサムネイル

### 🔧 変更

#### アーキテクチャ
- パッケージ名: `com.github.doqltl179.mu3library.base`
- Unity バージョン要件: 6000.0+ (Unity 6)
- ネームスペース構造の変更: すべてのコードが`Mu3Library.*`下に
- アセンブリの分離: RuntimeとEditorアセンブリ

#### MVPシステム
- `Presenter`ライフサイクルの完全な再設計
- `View`がアニメーション統合をサポート
- Model-View-Presenterバインディングの改善
- `MVPCanvasSettings`にキャンバス管理を集中化
- ビューのインスタンス化がResourcesとCameraベースの読み込みをサポート

#### オーディオシステム
- `BgmController`と`SfxController`に分離
- きめ細かい制御のための`AudioSourceSettings`を追加
- 3Dオーディオ位置指定のサポート
- `IAudioVolumeSettings`を通じたボリューム変更イベント

#### シーン管理
- 複数の実装を持つインターフェースベースの設計
- ロード操作のための進行状況イベント
- 重複シーン読み込みポリシー
- より良い非同期操作サポート

#### Observableパターン
- 複数のプリミティブ型をサポートするように拡張
- コレクション型の追加(List、Dictionary)
- エディター統合のためのカスタムプロパティドロワー
- 値変更コールバック

#### 拡張機能
- `Overwrite`を`CopyTo`に名前変更
- コンポーネントタイプ別に整理
- レイヤー管理ヘルパーの追加
- カメラプロパティのコピー

### 🐛 修正

#### 重要な修正
- **DIコンテナのライフサイクルバグ**: サービスライフタイム管理の問題を修正
- 複数のインターフェース実装が正しく単一のインスタンスを共有するように
- コレクションの不変性: 適切な場所でコレクションを`readonly`に変更
- SceneLoaderイベントタイミング: `OnSceneLoadEnd`コールバックタイミングを修正
- コードベース全体でのnull参照処理

#### 安定性の向上
- AnimationView例外処理の強化
- LocalizationManager初期化の堅牢性
- シーン読み込み状態管理
- MVPビューライフサイクルのエッジケース

### 🗑️ 削除

#### 完全なシステムの削除
- InputSystemヘルパークラスと生成されたコード
- MarchingCubesコンピュートシェーダーシステム
- CommandBufferポストプロセッシングエフェクト
- カメラコントローラーコンポーネント
- カスタムUIコンポーネント(DatePicker、IntSlider)
- 古いサンプルシーン(6つのサンプルを削除)
- シェーダーコレクション(Toon、Blur、EdgeDetect、GrayScaleなど)

#### コードのクリーンアップ
- 未使用のユーティリティ関数の削除
- 非推奨のMVP実装の削除
- 古いプールシステムの削除(GameObjectPoolで置き換え)
- 古いシングルトン実装の削除
- レガシーObservable実装の削除

### 📦 依存関係

#### 追加
- ✅ **com.cysharp.unitask**: 非同期操作のためのUniTask
- ✅ **com.coplaydev.unity-mcp**: Unity MCP統合
- ✅ **com.unity.localization** (1.5.9): ローカライゼーションサポート
- ✅ **com.unity.addressables** (パッケージマネージャーを通じた暗黙的な依存関係)

#### 更新
- Unity 6000.0+ (Unity 6) が必要
- .NET Standard 2.1

#### 削除
- ❌ 古いUnity-MCPパッケージ(IvanMurzak)

### 📚 ドキュメント

#### 追加
- 多言語READMEファイル(英語、日本語、韓国語)
- MITライセンス
- 包括的なインラインドキュメント(XMLコメント)
- 開発支援のためのGitHub Copilotエージェントファイル
- Unity固有の指示ファイル

#### 改善
- 詳細な機能説明を含むREADME
- インストール手順(Git URLとローカルディスクメソッド)
- すべての主要機能のコード例
- サンプルシーンのドキュメント

### 🔄 マイグレーションガイド

#### v0.0.20ユーザー向けガイド

**⚠️ これは主要な破壊的変更リリースです。完全なプロジェクト更新を推奨します。**

##### ステップ1: クリーンインストール
1. プロジェクトから古いパッケージを削除
2. `Library/`内のキャッシュファイルをすべて削除
3. 新しいGit URLを使用してv0.1.11をインストール:
   ```
   https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base
   ```

##### ステップ2: ネームスペースの更新
```csharp
// 旧 (0.0.20)
using Mu3LibraryAssets;

// 新 (0.1.11)
using Mu3Library;
using Mu3Library.DI;
using Mu3Library.MVP;
```

##### ステップ3: DIアーキテクチャへの移行
新しいバージョンは依存性注入を使用します。初期化コードを更新してください:

```csharp
// 旧: 手動初期化
public class GameManager : MonoBehaviour
{
    private AudioManager audioManager;
    
    void Start()
    {
        audioManager = FindObjectOfType<AudioManager>();
    }
}

// 新: DIベースのアプローチ
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
        base.Start(); // 注入のために必須
        // _audioManagerを使用
    }
}
```

##### ステップ4: MVPコードの更新
古いMVPパターンを使用していた場合:

```csharp
// 旧 Presenter
public class OldPresenter : Presenter<MyView, MyModel>
{
    // 旧構造
}

// 新 Presenter
public class NewPresenter : Presenter<MyView, MyModel, MyArgs>
{
    // Argumentsクラスの定義が必須
}

public class MyArgs : Arguments { }
```

##### ステップ5: 削除された機能の置き換え
- **InputSystem**: UnityのInput Systemを直接使用
- **カメラコントローラー**: カスタム実装またはサードパーティのソリューションを使用
- **ポストエフェクト**: UnityのPost Processing StackまたはURP/HDRPボリュームシステムを使用
- **カスタムUI**: UnityのUI Toolkitを使用するか、カスタムコンポーネントを作成

##### ステップ6: リソース読み込みの更新
```csharp
// 旧: 静的呼び出し
var asset = ResourceLoader.Load<Sprite>("path");

// 新: DIベース
public class MyCore : CoreBase
{
    [Inject] private IResourceLoader _resourceLoader;
    
    void LoadAsset()
    {
        _resourceLoader.Load<Sprite>("path", (sprite) => {
            // spriteを使用
        });
    }
}
```

##### ステップ7: 徹底的なテスト
- すべてのDI注入が機能していることを確認
- MVPビューが正しく読み込まれることを確認
- オーディオ再生をテスト
- シーン遷移を検証
- 使用されている場合はAddressables読み込みを確認

### 🎉 謝辞

オープンソースコミュニティに感謝します:
- CysharpによるUniTask(async/awaitサポート)
- CoplayDevによるUnity MCP(Model Context Protocol)
- 様々なCreative Commonsソースからのサンプルオーディオアセット

---

## [base/0.0.20] - 以前のリリース

### 追加
- ObservablePropertyの実装

以前のバージョンについては、コミット履歴を参照してください。

[urp/0.1.2]: https://github.com/doqltl179/Mu3Library_ForUnity/releases/tag/urp%2Fv0.1.2
[base/0.10.0]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.6.0...base%2Fv0.10.0
[base/0.2.3]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.2.0...v0.2.3
[base/0.2.0]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.1.11...v0.2.0
[base/0.1.11]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.0.20...v0.1.11
[base/0.0.20]: https://github.com/doqltl179/Mu3Library_ForUnity/releases/tag/v0.0.20
