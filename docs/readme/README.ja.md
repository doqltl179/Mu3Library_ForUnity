# Mu3Library For Unity

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](../../README.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](README.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](README.ja.md)

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../LICENSE)

</div>

**Mu3Library**は、Unityプロジェクト向けのモジュール化されたアーキテクチャフレームワークです。カスタムDI（Dependency Injection）システムとMVP（Model-View-Presenter）UIパターンを中心に、拡張可能で保守しやすいゲーム開発をサポートします。

## 📘 ドキュメント

### パッケージドキュメント

- [English README](../../README.md) · [Korean README](README.ko.md)
- [パッケージ変更履歴 (English)](../../CHANGELOG.md) · [韓国語](../changelog/CHANGELOG.ko.md) · [日本語](../changelog/CHANGELOG.ja.md)

### コントリビュータードキュメント

- [リポジトリ workflow 変更履歴](../repository/CHANGELOG.md)
- [AI agent・コントリビューター向け workflow](../ai-agents/README.md) — 作業の担当、手順、検証経路を選択します。
- [リポジトリツール](../../tools/README.md)

## ✨ 主な特徴

- 🏗 **モジュール化されたCoreシステム**: 独立した`CoreBase`による明確な責任分離
- 💉 **カスタムDIコンテナ**: Singleton、Transient、Scopedライフタイムをサポート
- 🎨 **MVP UIパターン**: View-Presenter-Model分離によるテスト可能なUIロジック
- 🔄 **自動ライフサイクル管理**: `IInitializable`、`IUpdatable`、`IDisposable`インターフェースベース
- 📦 **オプションパッケージサポート**: UniTask、Addressables、Localizationの条件付き有効化
- 🎵 **Audioシステム**: BGM/SFX分離管理とボリューム制御
- ✨ **Particle Handler**: 必須の `ParticleSystem` に対する再生、一時停止、停止、クリア、再起動、`Loop`/`IsLooping`/`SetLoop`/`PlayLoop`/`PlayOnce` 制御、状態取得、ライフサイクルイベントを提供する便利なコンポーネント
- 🌐 **WebRequest**: HTTP GET/POST、ダウンロードサイズクエリ、UniTaskサポート
- 📊 **Observableパターン**: データ変更検出とイベントベースバインディング
- 🛠 **ユーティリティコレクション**: Extension Methods、ObjectPool、EasingFunctions
- ✅ **初期化結果コントラクト**: Addressables/Localization の初期化成功/失敗状態を明示的に提供
- 🔁 **高信頼ネットワーキング**: WebRequest 結果型 API にステータス、ヘッダー、タイムアウト、リトライと、必要時のみのキャンセル伝播を提供
- 🧭 **決定的 Core 更新**: Core 実行順序が明示的かつ安定
- ⏳ **Scene 非同期 API**: built-in / Addressables シーン向けの phase ベース UniTask helper と、resolved scene name を含む構造化 lifecycle callback
- 🎮 **Input System Manager**: アクションアセット管理、対話的リバインド、バインディングオーバーライドの永続化をサポート（オプション）
- 🧰 **エディタユーティリティドロワー**: Input System / Localization の名前エクスポーター、ラベル/ロケール、グループ/テーブル、エントリを役割別スクリプトへ分ける Addressable グループおよび Localization データエクスポート、Localization 文字収集ツールを提供
- 🖼 **World-Space Background ユーティリティ**: 必須の `SpriteRenderer` 背景をカメラのビューポートに合わせ、任意で自動フィット、カメラ前面への配置、レンダラー表示設定を利用できます
- 🍉 **スイカゲームテンプレート**: 11 個のアイテムによる 2D マージボード、設定可能な `BoardConfig`、ボード fitting / 座標変換 helper、プレイ可能な sample scene を提供する再利用可能な `Mu3Library.Game.WatermelonGame` runtime assembly
## 📋 要件

- Unity 6 (6000.0+)
- .NET Standard 2.1

## 📦 インストール方法

### 方法 1: パッケージマネージャー (Git URL)
1. Unity Editorで`Window` > `Package Manager`を開きます
2. `+`ボタンをクリック > `Add package from git URL...`
3. 以下のURLのいずれかを入力:
   ```
    # Base パッケージ
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base#base/v0.22.0

    # URP パッケージ（先に Base をインストール）
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_URP#urp/v0.2.1
   ```

### 方法 2: パッケージマネージャー (ローカルディスク)
1. このリポジトリをクローンします
2. Unity Editorで`Window` > `Package Manager`を開きます
3. `+`ボタン > `Add package from disk...`をクリック
4. 以下のいずれかを選択:
    - `Mu3Library_Base/package.json`
    - `Mu3Library_URP/package.json`（先に Base をインストール）
    - `Mu3Library_Game_WatermelonGame/package.json`（先に Base と URP をインストール）

## 📚 コアモジュール

### DI (Dependency Injection)
カスタムDIコンテナがサービス登録と依存性注入を自動化します。

```csharp
using Mu3Library.DI;

public class AudioCore : CoreBase
{
    protected override void ConfigureContainer()
    {
        // AudioManagerをシングルトンとして登録 — IAudioManagerにも自動マッピング
        RegisterClass<AudioManager>();
    }
}

public class GameCore : CoreBase
{
    [SerializeField] private AudioClip _mainThemeClip;

    // 自動注入（同じCore内）
    [Inject] private IAudioManager _audioManager;

    // 異なるCoreからの注入
    [Inject(typeof(UICore))] private IMVPManager _mvpManager;

    protected override void Start()
    {
        base.Start(); // 注入が先に実行される必要があります！
        _audioManager.PlayBgm(_mainThemeClip);
    }
}
```

登録されたサービスも、生成後かつライフサイクル callback の前に `[Inject]` フィールドとプロパティへ自動注入されます。

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
        RegisterClass<AudioPlaybackService>(); // [Inject] メンバーが自動的に設定される
    }
}
```

`MVPManager`も、狭い公開契約である `IObjectInjector` を使って、コンテナ外で生成された presenter に `[Inject]` フィールドとプロパティの注入を適用します。具体的な `ContainerScope` と内部の注入実装は DI アセンブリ内に保持されます。

### MVP (Model-View-Presenter)
UIをView、Presenter、Modelに分離し、ビジネスロジックをテスト可能にします。

```csharp
// Model: データコンテナ
public class MainMenuModel : Model<MainMenuArgs>
{
    public string PlayerName { get; set; }
}

// View: Unityコンポーネント参照
public class MainMenuView : View
{
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _titleText;

    public Button StartButton => _startButton;
    public TextMeshProUGUI TitleText => _titleText;
}

// Presenter: ビジネスロジック
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
        // オープンアニメーションなど
    }

    private void OnStartClicked()
    {
        // ゲーム開始ロジック
    }
}

// 使用方法
_mvpManager.Open<MainMenuPresenter>(new MainMenuArgs { PlayerName = "Player1" });
```

`MVPManager` を `CoreBase` 経由で登録すると、presenter の `[Inject]` フィールドとプロパティが初期化前に設定されます。presenter を開くたびに変わるデータは `Arguments` で渡してください。

presenter を連結して開く場合は、ownership と visual host を分けて設定します。

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

`Owner` はライフサイクル連結を制御し、`HostOptions` は子 view の取り付け先とルート `RectTransform` レイアウトの適用方法を制御します。

manager から直接開く場合でも、同じ ownership / host 構成を 1 つのオーバーロードで維持できます。

```csharp
_mvpManager.Open<TooltipPresenter>(inventoryPresenter, new HostOptions
{
    Host = tooltipHost,
});
```

### Audioシステム
BGMとSFXを分離管理し、ボリューム制御をサポートします。

```csharp
[Inject] private IAudioManager _audioManager;
[Inject] private IAudioVolumeSettings _audioVolumeSettings;

void Start()
{
    // ボリューム設定
    _audioVolumeSettings.MasterVolume = 0.8f;
    _audioVolumeSettings.BgmVolume = 0.6f;

    // BGM再生
    _audioManager.PlayBgm(bgmClip);

    // SFX再生
    _audioManager.PlaySfx(sfxClip, volume: 1.0f);
}
```

### Addressables
Addressables サポートが有効な場合、key から解決したすべてのアセットを、対応する resource location の `PrimaryKey` でインデックスした `Dictionary<string, T>` としてロードできます。`PrimaryKey` は空でなく一意である必要があり、満たさない場合はロード操作が失敗します。

```csharp
[Inject] private IAddressablesManager _addressablesManager;

_addressablesManager.LoadAssetsWithKeys<Texture2D>("test-image", textures =>
{
    if (textures != null && textures.TryGetValue("TestImage", out Texture2D texture))
    {
        Debug.Log(texture.name);
    }
});

// UniTask サポートも有効な場合:
Dictionary<string, Texture2D> textures =
    await _addressablesManager.LoadAssetsWithKeysAsync<Texture2D>("test-image");
```

### Scene Loader
built-in と Addressables の両方に対して明示的な scene command を提供します。

```csharp
[Inject] private ISceneLoader _sceneLoader;
[Inject] private ISceneLoaderEventBus _sceneLoaderEventBus;

// 既存 callback は要求 target または Addressables key をそのまま渡します。
_sceneLoaderEventBus.OnSingleSceneLoaded += target =>
{
    Debug.Log($"Loaded target: {target}");
};

// 構造化 lifecycle callback は実際の Unity scene name を公開します。
// Addressables scene は runtime 名が確定するまで "UnnamedAddressableScene" を使います。
_sceneLoaderEventBus.OnSingleSceneLifecycle += info =>
{
    Debug.Log($"{info.Phase}: target={info.Target}, resolved={info.ResolvedSceneName}");
};

await _sceneLoader.LoadSingleSceneAsync("Main");
await _sceneLoader.LoadSingleSceneWithAddressablesAsync("Sample_Addressables");
```

### WebRequest
HTTPリクエストを簡単に処理します。

```csharp
[Inject] private IWebRequestManager _webRequest;

// GETリクエスト
_webRequest.Get<string>("https://api.example.com/data", response =>
{
    Debug.Log(response);
});

// POSTリクエスト
var requestBody = new { username = "player", score = 100 };
_webRequest.Post<object, ServerResponse>("https://api.example.com/submit", requestBody, response =>
{
    Debug.Log($"Success: {response.message}");
});

// UniTaskサポート（MU3LIBRARY_UNITASK_SUPPORT有効時）
var data = await _webRequest.GetAsync<DataModel>("https://api.example.com/data");

// 呼び出し側で必要な場合だけキャンセルを例外として伝播
var cancellableData = await _webRequest.GetAsync<DataModel>(
    "https://api.example.com/data",
    cancellationToken: token,
    propagateCancellation: true);
```

### Observableパターン
値の変更を検出し、イベントを発行します。

```csharp
public class PlayerData
{
    public ObservableInt Health = new ObservableInt();
    public ObservableString PlayerName = new ObservableString();
}

// イベント購読
_playerData.Health.AddEvent(newHealth =>
{
    Debug.Log($"Health changed: {newHealth}");
    UpdateHealthUI(newHealth);
});

// 値変更（自動的にイベント発行）
_playerData.Health.Set(80);
```

## 🔧 オプションパッケージ

以下のパッケージがインストールされると、該当機能が自動的に有効になります:

| パッケージ | Define | 機能 |
|-------|--------|------|
| [UniTask](https://github.com/Cysharp/UniTask) | `MU3LIBRARY_UNITASK_SUPPORT` | async/await非同期API |
| Unity Addressables | `MU3LIBRARY_ADDRESSABLES_SUPPORT` | 動的アセットロード |
| Unity Localization | `MU3LIBRARY_LOCALIZATION_SUPPORT` | 多言語サポート |
| Unity Input System | `MU3LIBRARY_INPUTSYSTEM_SUPPORT` | 新しい入力システム |

## 🧩 Inspector 属性

`ButtonInvokeAttribute` は、引数なしで `void` を返すインスタンスメソッドに適用します。label とボタン高さは任意であり、label を省略すると `Invoke {method name}` 形式で表示されます。ボタンは fallback Inspector が既定のシリアライズ済みプロパティを描画した後に追加されるため、ソースの宣言順でフィールド間の位置を指定することはできません。独自の Custom Inspector を持つ型では、その Inspector で `ButtonInvoke` ボタンを描画する必要があります。

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

## 📖 全モジュールリスト

- **Addressable**: Addressables統合（オプション）
- **Attribute**: `ConditionalHideAttribute` や `ButtonInvokeAttribute` などのカスタム属性
- **Audio**: BGM/SFX管理システム
- **Particle**: `ParticleSystem` の便利な操作、loop 設定、`OnPlayed`、`OnStopped`、`OnPaused`、`OnUnPaused`、`OnCleared`、`OnRestarted`、`OnLoopChanged`、`OnCompleted` イベントを提供する `ParticleHandler` MonoBehaviour
- **DI**: Dependency Injectionコンテナ
- **Event**: `SubscribeHandler` を通した owner 管理型 subscription utility、再利用可能な one-shot helper、破棄可能な `ISubscriptionInfo` token
- **Extensions**: GameObject、Transform、Vector3などの拡張メソッド
- **Localization**: Unity Localizationラッパー（オプション）
- **ObjectPool**: 重複した非アクティブオブジェクトの再登録防止、`List<T>` の一括 enqueue と count または引数リスト指定で `List<T>` を返す dequeue、任意の引数なし生成コールバック、型安全な `CreateArguments` 初期化コールバック、`Clear()` によるクリーンアップを備えたキュー方式のオブジェクトプーリング
- **Observable**: データ変更検出システム
- **Preference**: PlayerPrefsラッパー
- **Resource**: Resourcesフォルダローディング
- **Scene**: phase/status 参照、lifecycle/progress callback、one-shot lifecycle 購読 helper、統合 rejection event を提供するシーンローディング抽象化
- **UI**: MVPパターン実装、軸ごとの一括/個別の分割線で子を9つの領域のいずれかに anchor で合わせる `UIAreaGrid`/`UIAreaElement`、canvas を画面の safe area 内に保つ `SafeCanvas`/`SafeRect`
- **IS**: Unity Input System ラッパーおよびバインディングマネージャー（オプション）
- **Utility**: Singleton、EasingFunctions、Settings、カメラのビューポートに `SpriteRenderer` 背景を合わせる `Mu3Library.Utility.WorldSpaceBackground`
- **WebRequest**: HTTPリクエスト管理

## 🎓 サンプル

パッケージマネージャーの**Samples**タブから以下のサンプルをインポートできます:

Base パッケージ (**Mu3 Library**):
- **Template**: 基本的なCore構造と使用例
- **Attribute**: `ConditionalHideAttribute` と `ButtonInvokeAttribute` の使用方法
- **UtilWindow**: カスタムエディターウィンドウとユーティリティドロワーの例

URP パッケージ (**Mu3 Library URP**):
- **ScreenEffect**: Grayscale / Shake / GaussianBlur / DepthOutline の 4 種類のスクリーンエフェクトと対応 handler スクリプトを含む URP スクリーンエフェクトのサンプルシーンと補助スクリプト
- **Camera Stack Helper**: `Mu3Library.URP.Cam.CameraStackSetter` が `SetCameraStackToMain(...)` と `SetCameraStack(...)` helper を提供し、任意の URP overlay camera を `Camera.main` または明示的な root camera stack に挿入しつつ、必要に応じて挿入 index を制御できます。

Watermelon Game パッケージ (**Mu3 Library Watermelon Game**):
- **Watermelon Game**: 設定可能なボード、フルーツ sprite、sample manager/core script を含むプレイ可能な 2D 落下フルーツマージ scene
- **Fruit item indexes**: ボード設定は常に11個のフルーツ項目を含み、0始まりのリスト index でアクセスします。
- **Board fitting**: `BoardArea.Fit(...)` で設定したカメラビューポートの padding 内にボードスプライトを合わせられます。
- **Board collision boundaries**: `BoardArea.SetOutColliders()` で item-area viewport padding を基準に left、right、bottom の `BoxCollider2D` 境界を生成・更新でき、top は開いたままにします。
- **Board-relative coordinate conversions**: `BoardArea` は world、screen、local の正規化位置変換と初期化安全な `Try...` variant を提供します。
- **Merge effects**: `BoardItemInfo` は任意の merge effect 用 `ParticleHandler` resource をサポートし、完了した生成 effect instance を自動的に cleanup します。
- **Scoring extension point**: `BoardItemScoreRule.GetScore(int)` を override して標準の三角数 score progression をカスタマイズできます。
- **Board commands**: `BoardController.EnqueueCommand(...)` で公開 command queue を使い、`IUpdatableBoardCommand` / `ICancelableBoardCommand` と `BoardCommand` の lifecycle hook でフレーム単位の実行・cancel を組み立てられます。`BoardController.CommandContext` は外部 command が item・score・sound に触れるための狭い surface を提供し、Flow / Item / Score command と `MergingCommand` も用意されています。
- **Board sound volumes**: `BoardController.SfxVolume` と `BoardController.BgmVolume` で `BoardConfig.SoundConfig` とは別に board の再生 volume を設定でき、両方とも 0–1 に clamp されます。
- **Sample assets**: package sample には `BoardConfig` asset、フルーツ/背景画像、sample manager/core script、`Demo` scene が含まれます。

このリポジトリでは、Base サンプルのソースは `Mu3Library_Base/Samples~`、URP サンプルのソースは `Mu3Library_URP/Samples~/ScreenEffect`、Watermelon Game サンプルのソースは `Mu3Library_Game_WatermelonGame/Samples~/WatermelonGame` にあります。

新しい helper で MVP render camera を stack したい場合は、render camera を明示的に渡してください:

```csharp
using Mu3Library.URP.Cam;

CameraStackSetter.SetCameraStack(targetCamera, _mvpManager.RenderCamera);
```

**Template 主要構成:**
- Scenes: Main、Sample_MVP、Sample_Addressables、Sample_Localization、Sample_WebRequest、Sample_Audio、Sample_Audio3D、Sample_IS
- Localization: Locales（KO/JA/EN）、String Tableサンプル
- Resources: MVPサンプル用のPrefabと設定
- Materials: デフォルトカラーマテリアル（Black、Blue、Green、Magenta、Red、White）

## 🏗 アーキテクチャ概要

### Coreシステム
各`CoreBase`は独立したDIコンテナ（`ContainerScope`）を所有し、`CoreRoot`がライフサイクルを管理します。

```
CoreRoot (Singleton)
├── AudioCore (独立ContainerScope)
│   └── AudioManager, BgmController, SfxController
├── UICore (独立ContainerScope)
│   └── MVPManager, Presenters...
└── NetworkCore (独立ContainerScope)
    └── WebRequestManager
```

### Core間通信
異なるCoreのサービスにアクセスするには:

```csharp
// 方法1: Start()で手動取得
protected override void Start()
{
    base.Start();
    _audioManager = GetClassFromOtherCore<AudioCore, IAudioManager>();
}

// 方法2: Inject属性（CoreBase専用）
[Inject(typeof(AudioCore))] private IAudioManager _audioManager;
```

特定の Core の初期化完了後に一度だけコードを実行するには、`ICoreRoot` 経由で購読します:

```csharp
CoreRoot.Instance.SubscribeOnCoreInitializedOnce<AudioCore>(() =>
{
    _audioManager = CoreRoot.Instance.GetClass<AudioCore, IAudioManager>();
});
```

callback は対象 Core の初期化完了後に一度だけ呼び出されます。

非同期の準備処理がある場合は、準備完了通知を購読できます:

```csharp
CoreRoot.Instance.SubscribeOnCorePreparedOnce<AudioCore>(() =>
{
    // AudioCore の準備完了後にコードを実行します。
});
```

callback は対象 Core の準備処理完了後に一度だけ呼び出されます。

## 📝 最近のアップデート

- このリポジトリ上の現在の Base パッケージ版: `0.22.0`
- このリポジトリ上の現在の URP パッケージ版: `0.2.1`（manifest 依存関係: `com.github.doqltl179.mu3library.base` `0.22.0`）
- このリポジトリ上の現在の Watermelon Game パッケージ版: `0.3.0`（Base `0.22.0`、URP `0.2.1` に依存）
- リポジトリのリリースノートと草案版の履歴は `CHANGELOG.md` を参照してください。

## 🤝 貢献

IssueとPull Requestを歓迎します！以下の点にご注意ください:
- **コーディングスタイル**: プライベートフィールドは`_camelCase`、Allmanブレースを使用
- **コミットメッセージ**: 明確な説明（例: `feat: Add retry logic to WebRequest`）

## 📄 ライセンス

このプロジェクトはMITライセンスに従います。

## 📞 お問い合わせ

- **GitHub Issues**: [https://github.com/doqltl179/Mu3Library_ForUnity/issues](https://github.com/doqltl179/Mu3Library_ForUnity/issues)
- **Author**: Mu3 ([GitHub](https://github.com/doqltl179))

---

**パッケージ情報:**
- Base: `com.github.doqltl179.mu3library.base` `0.22.0`
- URP: `com.github.doqltl179.mu3library.urp` `0.2.1`（manifest 依存関係: `com.github.doqltl179.mu3library.base` `0.22.0`）
- Watermelon Game: `com.github.doqltl179.mu3library.game.watermelon` `0.3.0`（Base `0.22.0`、URP `0.2.1` に依存）

Unity開発者のために制作
