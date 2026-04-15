# 変更履歴 (Changelog)

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](../../CHANGELOG.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](CHANGELOG.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](CHANGELOG.ja.md)

</div>

Mu3Library For Unityのすべての注目すべき変更はこのファイルに記録されます。

このフォーマットは[Keep a Changelog](https://keepachangelog.com/en/1.0.0/)に基づいており、
このプロジェクトは[Semantic Versioning](https://semver.org/spec/v2.0.0.html)に準拠しています。

## [Unreleased]

### 変更
- `ScreenEffectManager` / `IScreenEffectManager`: URP の ScreenEffect パス登録クラスとインターフェース名を、現在の責務に合わせて `PostVolumeManager` / `IPostVolumeManager` から改名。Unity Volume ベースの責務を表さなくなったため。

### 修正
- `Mu3Library_URP/package.json`: `ScreenEffect` サンプルをパッケージ manifest の `samples` 一覧に公開し、Unity Package Manager から検出およびインポートできるよう修正。

## [0.8.0] - 2026-04-05

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

## [0.6.0] - 2026-03-23

### 追加
- `MVPManager` / `IMVPManager`: `FocusIgnoredLayers` プロパティと `SetFocusIgnoredLayer(string layerName, bool ignored)` メソッドを追加。
  - 無視（ignored）レイヤーの Presenter はフォーカスおよび `OutPanel` 更新の計算から除外される。
  - 無視レイヤーは実行時にトグル可能で、変更時に即座に `UpdateFocus()` が呼ばれる。
- `LocalizationNameExporterDrawer`: 生成スクリプトにルート `Locales` クラス（`All` 文字列配列、およびロケールごとに `Code`・`EnglishName`・`NativeName` を `const string` で公開する内部クラス）とルート `Tables` クラス（`All` 文字列配列、および各テーブルクラスの `Name` を参照する `const string` エントリ）を追加。各テーブルクラスにも、ルート `Locales` 構造を `const string` 参照でミラーリングする `Locales` 内部クラスが追加される。
- `AddressableGroupNameExporterDrawer`: 生成スクリプトにルート `Groups` クラス（グループクラスの `Name` を参照する `const string` エントリと `All` 配列）、ルート `Labels` クラス（全グループ・エントリから収集した一意のラベルを `All` 配列と `const string` 値で提供）、およびルート `Labels` エントリを `const string` 参照でミラーリングするグループごとの `Labels` 内部クラスを追加。

## [0.5.0] - 2026-03-18

### 変更
- リポジトリをモノレポ構成に再編: `Mu3Library_Base/` と `Mu3Library_URP/` は独立した UPM パッケージに、`UnityProject_BuiltIn/` と `UnityProject_URP/` は別個の開発プロジェクトとして分離。
- `.gitignore` のパターンに `**/` プレフィックスを追加し、モノレポ配下の全サブプロジェクトを対象に包含するよう改善。

### 修正
- `CoreBase.WaitForOtherCore`: `CoreRoot.Instance` が null のとき（例: アプリ終了時）に発生していた `NullReferenceException` を修正。
- `CoreBase.GetClassFromOtherCore`: 同様の null 安全処理を適用。
- `ContainerScope.ResolveFromCore`: 同様の null 安全処理を適用。
- ドキュメント: 全 README の `ConfigureContainer()` コード例を修正 — 誤った `ContainerScope scope` パラメーターを削除し、サービス登録に `RegisterClass<T>()` を使用するよう修正。

## [0.4.7] - 2026-03-15

### 追加
- `ScriptBuilder`: `ArrayBlock` 構造体（`FieldName`、`Values`）と `AppendArrayBlock` メソッドを追加。
  - `ArrayBlock` を `CodeBlock.Content` リストに `string`・`CodeBlock` と並べて配置可能。
  - インデントは `ScriptBuilder` が自動処理し、`CodeBlock` の出力と統一。

### 変更
- `AddressableGroupNameExporterDrawer`: `BuildArrayLines` ヘルパーを `ScriptBuilder.ArrayBlock` で置き換え。
  - `AllNames`・`AllAddresses`・`Labels.All` 配列の宣言が `foreach` ループから単一の `.Add()` 呼び出しに短縮。

## [0.4.6] - 2026-03-15

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

## [0.4.5] - 2026-03-14

### 変更
- `AddressableGroupNameExporterDrawer`: サブアセットのクラス名が親クラス名で始まる場合、その接頭辞を除去するよう変更。
  - 例: 親 `Views`、サブアセット `ViewsDialoguePanelPrefab` → `DialoguePanelPrefab` として出力。
  - ネストされたフォルダー階層にも再帰的に適用。

## [0.4.4] - 2026-03-14

### 変更
- `AddressableGroupNameExporterDrawer`: フォルダーエントリーのサポートを追加。
  - `AssetDatabase.IsValidFolder()` でグループにフォルダーとして登録されたアセットを検出。
  - フォルダーエントリーの場合、`GatherAllAssets()` でサブアセットを収集し、`Assets` inner static class にネストして出力。
  - エディタープレビューでフォルダーエントリーは `[Folder]` プレフィックスで示され、サブアセットはインデントして表示。

## [0.4.3] - 2026-03-14

### 追加
- `AddressableGroupNameExporterDrawer`: エディター上で全 Addressable グループを読み取り、グループ名・アセット名・アドレス(key)・ラベルをネストした C# static クラスとして書き出すエディタードロワーを追加（`MU3LIBRARY_ADDRESSABLES_SUPPORT` 条件付きコンパイル）。
  - `Labels` 内部クラスにラベルごとの `const string` フィールドと、全ラベル値を格納した `static readonly string[] All` を提供。
- `UtilWindow`: ユーティリティウィンドウのドロワー一覧に `AddressableGroupNameExporter` サンプルアセットを追加。
- `Template`: Addressable グループ/アドレス定数の生成例として `AddressableGroupKeys` を追加。
- `Mu3Library.Editor.asmdef`: `Unity.Addressables` および `Unity.Addressables.Editor` のオプション参照と `MU3LIBRARY_ADDRESSABLES_SUPPORT` バージョン定義を追加。

## [0.4.2] - 2026-03-08

### 追加
- `LocalizationNameExporterDrawer`: Localization の string table 名と entry key を C# 定数として書き出し、事前宣言された参照に使えるエディタドロワーを追加。
- `UtilWindow`: ユーティリティウィンドウのドロワー一覧に `LocalizationNameExporter` サンプルアセットを追加。
- `Template`: Localization テーブル/キー定数の生成例として `LocalizationTableKeys` を追加。

### 変更
- `InputSystemNameExporterDrawer` と `LocalizationNameExporterDrawer`: 動作は変えずに、backing field とキャッシュ済み accessor を区別しやすいよう private serialized helper メンバー名を整理。

### 修正
- `LocalizationNameExporterDrawer`: エントリキーから正しい PascalCase クラス名を生成するよう `SanitizeIdentifier` を修正。`-` などの非識別子文字は単語境界として扱われ、省略されて次の文字が大文字化。`_` はそのまま出力され次の文字も大文字化（例: `my-key_name` → `MyKey_Name`）。

## [0.4.0] - 2026-03-08

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

## [0.3.3] - 2026-03-02

### 追加
- `AudioManager`: 環境音再生のための `EnvironmentController` 機能を追加。
  - 新しい `EnvironmentController` クラス: `EnvironmentVolume` をカテゴリボリュームとして音声を再生。
  - `EnvironmentInstanceCountMax` プロパティを追加（デフォルト: `3`、最大: `5`）。
  - `EnvironmentVolume`、`CalculatedEnvironmentVolume`、`ResetEnvironmentVolume()` を `AudioManager` および `IVolumeSettings` に追加。
  - `PlayEnvironment`、`StopFirstEnvironment`、`StopEnvironmentAll`、`PauseEnvironmentAll`、`UnPauseEnvironmentAll` メソッドを `AudioManager` および `IAudioManager` に追加。
  - `OnEnvironmentVolumeChanged` イベントを `IAudioManagerEventBus` に追加。
  - `Stop()`、`Pause()`、`UnPause()` が環境音も対象に含むよう更新。

## [0.3.2] - 2026-03-02

### 修正
- `Mu3WindowDrawer`: 派生 Drawer での `BeginChangeCheck` / `RecordObject` / `SetDirty` の定型コードを排除するため、基底クラスに `DrawWithUndo<T>(Func<T>, Action<T>, string)` ヘルパーを追加。
- `Mu3WindowDrawer`: `DrawFoldoutHeader1` および `DrawFoldoutHeader2` が明示的な `!=` 比較ではなく `EditorGUI.BeginChangeCheck` / `EndChangeCheck` パターンに統一された。
- `DependencyCheckerDrawer`、`FileFinderDrawer`、`InputSystemNameExporterDrawer`、`MVPHelperDrawer`、`ScreenCaptureDrawer`: すべてのインタラクティブフィールドが新しい `DrawWithUndo<T>` ヘルパーを通じて undo/redo 状態を正しく記録するよう修正。

## [0.3.1] - 2026-03-02

### 修正
- `MVPManager`: View が Load 中に素の状態（例: alpha 1）で一フレームレンダリングされる同期ズレを修正。
  - `Open()` 呼び出し時に即座に `SetActiveView(true)` していた処理を `SetActiveView(false)` に変更し、
    Load 完了後・`Open()` 開始直前に `SetActiveView(true)` を呼ぶように修正。
  - これにより、アニメーション（例: alpha 0→1）が View の初期状態と同期してから開始されるようになります。

## [0.3.0] - 2026-03-01

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

## [0.2.3] - 2026-02-16
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

## [0.2.0] - 2026-02-14

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

## [0.1.11] - 2026-02-08

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

## [0.0.20] - 以前のリリース

### 追加
- ObservablePropertyの実装

以前のバージョンについては、コミット履歴を参照してください。

[Unreleased]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.2.3...HEAD
[0.2.3]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.2.0...v0.2.3
[0.2.0]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.1.11...v0.2.0
[0.1.11]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.0.20...v0.1.11
[0.0.20]: https://github.com/doqltl179/Mu3Library_ForUnity/releases/tag/v0.0.20
