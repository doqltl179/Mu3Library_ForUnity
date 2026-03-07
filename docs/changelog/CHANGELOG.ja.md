# 変更履歴 (Changelog)

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](../../CHANGELOG.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](CHANGELOG.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](CHANGELOG.ja.md)

</div>

Mu3Library For Unityのすべての注目すべき変更はこのファイルに記録されます。

このフォーマットは[Keep a Changelog](https://keepachangelog.com/en/1.0.0/)に基づいており、
このプロジェクトは[Semantic Versioning](https://semver.org/spec/v2.0.0.html)に準拠しています。

## [Unreleased]

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
- **Sample_Template**: 包括的なサンプルプロジェクト
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
- パッケージ名: `com.github.doqltl179.mu3libraryassets.base`
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
   https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Assets/Mu3LibraryAssets
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
