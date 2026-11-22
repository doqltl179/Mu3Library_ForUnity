# Mu3Library For Unity

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](../../README.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](README.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](README.ja.md)

[![Unity Version](https://img.shields.io/badge/Unity-6000.3%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../LICENSE)

</div>

**Mu3Library**は、Unityプロジェクト向けのモジュール化されたアーキテクチャフレームワークです。カスタムDI（Dependency Injection）システムとMVP（Model-View-Presenter）UIパターンを中心に、拡張可能で保守しやすいゲーム開発をサポートします。

## 📘 ドキュメント

### パッケージドキュメント

- [English README](../../README.md) · [Korean README](README.ko.md)
- [パッケージ変更履歴 (English)](../../CHANGELOG.md) · [韓国語](../changelog/CHANGELOG.ko.md) · [日本語](../changelog/CHANGELOG.ja.md)

### コントリビュータードキュメント

- [リポジトリ workflow 変更履歴](../repository/CHANGELOG.md)
- [AI agent・コントリビューター向け workflow](../../AGENTS.md) — [agentkit](https://github.com/doqltl179/ai-agents) が `.ai/` から生成し、作業の担当、手順、検証経路へ案内します。
- [リポジトリツール](../../tools/README.md)

## ✨ 主な特徴

- 🏗 **モジュール化されたCoreシステム**: 独立した`CoreBase`による明確な責任分離を、独立した DI パッケージで提供
- 💉 **カスタムDIコンテナ**: Singleton、Transient、Scopedライフタイムのサポートを、独立した DI パッケージで提供
- 🎨 **MVP UIパターン**: View-Presenter-Model分離によるテスト可能なUIロジック
- 🔄 **自動ライフサイクル管理**: `IInitializable`、`IUpdatable`、`IDisposable`インターフェースベース
- 📦 **オプションパッケージサポート**: UniTask、Addressables、Input Systemの条件付き有効化
- 🎵 **Audioシステム**: BGM/SFX/Environment チャンネルのボリューム制御、ミキサーグループのルーティング、BGM ダッキング、ボリュームの保存/復元、await 可能なフェード
- ✨ **Particle Handler**: 必須の `ParticleSystem` に対する再生、一時停止、停止、クリア、再起動、`Loop`/`IsLooping`/`SetLoop`/`PlayLoop`/`PlayOnce` 制御、状態取得、ライフサイクルイベントを提供する便利なコンポーネント
- 🌐 **WebRequest**: HTTP GET/POST/PUT/PATCH/DELETE、ダウンロードサイズクエリ、リトライバックオフ、実行中のキャンセル、ダウンロード進捗、UniTaskサポート
- 📊 **Observableパターン**: データ変更検出とイベントベースバインディング
- 🛠 **ユーティリティコレクション**: Extension Methods、ObjectPool、EasingFunctions
- ✅ **初期化結果コントラクト**: Addressables/Localization の初期化成功/失敗状態を明示的に提供
- 🔁 **高信頼ネットワーキング**: WebRequest 結果型 API にステータス、ヘッダー、タイムアウト、リトライと、必要時のみのキャンセル伝播を提供
- 🧭 **決定的 Core 更新**: Core 実行順序が明示的かつ安定した更新を、独立した DI パッケージで提供
- ⏳ **Scene 非同期 API**: built-in / Addressables シーン向けの phase ベース UniTask helper と、resolved scene name を含む構造化 lifecycle callback
- 🎮 **Input System Manager**: アクションアセット管理、対話的リバインド、バインディングオーバーライドの永続化と名前エクスポーターのドロワーを、独立した Input System パッケージで提供
- 🌍 **Localization Manager**: 文字列とローカライズされたアセットの取得、すべての購読者に通知されるロケール変更と、ロケール/テーブル/エントリを役割別スクリプトへ分けるデータエクスポーター、文字収集ツールを、独立した Localization パッケージで提供
- 🗂 **Addressables Manager**: key 単位のアセットのロードとキャッシュ、ダウンロードサイズの取得と依存関係のダウンロード、1 つずつ Addressables に届くカタログの確認と更新と、ラベル、グループ、エントリを役割別スクリプトへ分け、そのスクリプトを define シンボルで囲めるグループデータエクスポーターを、独立した Addressables パッケージで提供
- 🖼 **World-Space Background ユーティリティ**: 必須の `SpriteRenderer` 背景をカメラのビューポートに合わせ、任意で自動フィット、カメラ前面への配置、レンダラー表示設定を利用できます
- 🍉 **スイカゲームテンプレート**: 11 個のアイテムによる 2D マージボード、設定可能な `BoardConfig`、ボード fitting / 座標変換 helper を提供する再利用可能な `Mu3Library.Game.WatermelonGame` runtime assembly

## 📋 要件

- Unity 6.3 (6000.3+)
- .NET Standard 2.1

## 📦 インストール方法

### 方法 1: パッケージマネージャー (Git URL)
1. Unity Editorで`Window` > `Package Manager`を開きます
2. `+`ボタンをクリック > `Add package from git URL...`
3. 以下のURLのいずれかを入力:
   ```
    # Base パッケージ
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/base#base/v0.32.0

    # Notifications パッケージ
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/notifications#notifications/v0.1.0

    # Input System パッケージ（先に Base をインストール）
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/inputsystem#inputsystem/v0.1.0

    # Localization パッケージ（先に Base をインストール）
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/localization#localization/v0.1.0

    # Addressables パッケージ（先に Base をインストール）
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/addressables#addressables/v0.2.0

    # DI パッケージ（先に Base をインストール）
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/di#di/v0.2.0

    # Steam パッケージ（先に Steamworks.NET と Base をインストール）
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/steam#steam/v0.3.0

    # URP パッケージ（先に Base をインストール）
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/urp#urp/v0.5.0

    # Watermelon Game パッケージ（先に Base をインストール）
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/game/watermelon#game/watermelon/v0.8.1
   ```

URP と Watermelon Game はどちらも Base に依存するため、どちらよりも先に Base をインストールしてください。Watermelon Game に URP は必要ありません。

Notifications パッケージは Android と iOS のローカル通知を扱います。ほかの Mu3Library パッケージは必要ありません。

Input System パッケージは `InputSystemManager` とその名前エクスポーターのドロワーを含みます。先に Base をインストールしてください。

Localization パッケージは `LocalizationManager` とそのデータエクスポーター、文字収集のドロワーを含みます。先に Base をインストールしてください。

Addressables パッケージは `AddressablesManager` とそのグループデータエクスポーターのドロワーを含みます。先に Base をインストールしてください。

DI パッケージは `CoreBase` のコンテナとその link.xml プロセッサー、検証ドロワーとランタイム診断ドロワーを含みます。先に Base をインストールしてください。

Steam パッケージは Steam でマルチプレイセッションを実行する `SteamBackend` を含みます。先に Steamworks.NET と Base をインストールしてください。

### 方法 2: パッケージマネージャー (ローカルディスク)
1. このリポジトリをクローンします
2. Unity Editorで`Window` > `Package Manager`を開きます
3. `+`ボタン > `Add package from disk...`をクリック
4. 以下のいずれかを選択:
    - `packages/base/package.json`
    - `packages/notifications/package.json`
    - `packages/inputsystem/package.json`（先に Base をインストール）
    - `packages/localization/package.json`（先に Base をインストール）
    - `packages/addressables/package.json`（先に Base をインストール）
    - `packages/di/package.json`（先に Base をインストール）
    - `packages/steam/package.json`（先に Steamworks.NET と Base をインストール）
    - `packages/urp/package.json`（先に Base をインストール）
    - `packages/game/watermelon/package.json`（先に Base をインストール）

## 📚 コアモジュール

### DI (Dependency Injection)
`CoreBase` と `CoreRoot` を含む DI コンテナは、独立した DI パッケージに移りました。登録、注入、コア、そしてマネージドコードをストリッピングするプレイヤービルドのためにこのパッケージが書き出す link.xml については、その [README](../../packages/di/README.md) を参照してください。Base は、マネージャーが実装する契約 `IInitializable`、`IUpdatable`、`ILateUpdatable`、`IObjectInjector`、`[Inject]` を `Mu3Library.Foundation` アセンブリに残します。以下の例は、DI パッケージのコアと同じように `[Inject]` で Base のサービスを注入します。

### MVP (Model-View-Presenter)
UIをView、Presenter、Modelに分離し、ビジネスロジックをテスト可能にします。view はコンポーネントなので、クラス名と同じ名前のファイル `MainMenuView.cs` に置きます。後述の `UICore` と `UIBootstrap` も同じです。

```csharp
using Mu3Library.Audio;
using Mu3Library.DI;
using Mu3Library.UI.MVP;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Arguments: Open のたびに渡すデータ
public class MainMenuArgs : Arguments
{
    public string PlayerName;
}

// Model: データコンテナ。presenter の LoadFunc() が実行される前に、引数から値が入ります
public class MainMenuModel : Model<MainMenuArgs>
{
    public string PlayerName { get; private set; }

    // 基底の Init は空なので、model に必要な値をここで引数からコピーします
    public override void Init(MainMenuArgs args)
    {
        PlayerName = args?.PlayerName;
    }
}

// View: Unityコンポーネント参照
public class MainMenuView : View
{
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private AudioClip _clickClip;

    public Button StartButton => _startButton;
    public TextMeshProUGUI TitleText => _titleText;
    public AudioClip ClickClip => _clickClip;
}

// Presenter: ビジネスロジック
public class MainMenuPresenter : Presenter<MainMenuView, MainMenuModel, MainMenuArgs>
{
    // DI パッケージの README にある、AudioManager を登録するコア AudioCore から注入します。
    // このメンバーは必須なので、シーンに AudioCore がないと Open は例外を投げます
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

    protected override void UnloadFunc()
    {
        // 閉じた presenter は view とともにプールされ、次の Open でその presenter の LoadFunc() が再び実行されます
        _view.StartButton.onClick.RemoveListener(OnStartClicked);
    }

    private void OnStartClicked()
    {
        _audioManager.PlaySfx(_view.ClickClip);
        // ゲーム開始ロジック
    }
}
```

`MVPManager` が presenter を開くのは、プレハブを持っている view 型に対してだけです。各 view のプレハブ（ルートに view コンポーネントを持つもの）を、最初の `Open` より前に `RegisterViewResource` または `RegisterViewResources` で登録してください。ルートに `MainMenuView` を追加すると、view が必要とする `Canvas`、`CanvasScaler`、`GraphicRaycaster`、`CanvasGroup` も追加されます。登録されたプレハブがないと、`Open` は `View not found` をログに出して null を返します。`Open` は `Init(args)` で presenter の model を作り、view がまだ非表示のうちに `LoadFunc()` を呼びます。view は次の `MVPManager.Update()` で表示されて開き始め、その後の更新が開く処理を終わらせ、ウィンドウが閉じ終わるとアンロードします。そのため、何かが毎フレーム `Update()` を呼ぶ必要があります。

DI パッケージを使う場合は、コアがマネージャーを登録し、`CoreRoot` が毎フレームその `Update()` を呼び、コアが破棄されるときにマネージャーを破棄します。`MVPManager` を `CoreBase` 経由で登録すると、presenter の `[Inject]` フィールドとプロパティが初期化前に設定されます。

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
        base.Start(); // _mvpManager を注入します

        _mvpManager.RegisterViewResource(_mainMenuViewPrefab);
        _mvpManager.Open<MainMenuPresenter>(new MainMenuArgs { PlayerName = "Player1" });
    }
}
```

DI パッケージの `CoreBase` を使わない場合は、マネージャーを作るコードが毎フレームその `Update()` を呼び、使い終わったら `Dispose()` を呼びます。そうしたマネージャーには何も `IObjectInjector` を渡さないため、presenter の `[Inject]` メンバーは設定されないままです。その場合は `MainMenuPresenter` から、`CoreBase` を継承する `AudioCore` を指定した `[Inject]` フィールドと、`PlaySfx` の呼び出しを外してください。

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

presenter を開くたびに変わるデータは `Arguments` で渡してください。

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

子は `LoadFunc()` と `OpenFunc()` を含むどの presenter コールバックからでも開けます。owner がまだ開き終わっている必要はありません。owner を閉じると、まだ loading 中の子まで連鎖して閉じます。owner の view の下に host された子は、owner が先に閉じ終わっても unload されます。その場合、子の閉じる遷移は owner の view が非表示になった時点で終わります。`OnDisable` を override する view が `base.OnDisable()` を呼ばなくてもマネージャーがその close を終わらせますが、そうした view でも呼ぶことを勧めます。閉じ始めた presenter は新しい子を受け取りません。

`HostOptions.Host` が非アクティブな場合のように、非表示の host の下で開いたウィンドウは、そこでは開く遷移を進められません。代わりに待機を省略します。`OpenStart()` と `OpenEnd()` が続けて実行され、`AnimationView` はすぐに開いた姿勢になり、ウィンドウは次のマネージャー更新で開いたものとして扱われます。host が表示されると完全に開いた状態で現れ、通常の `Close` で閉じられ、`OpenAsync` も完了します。host が非表示のまま閉じると閉じる遷移を省略し、`AnimationView` は閉じた姿勢になるため、次に開くときは再びアニメーションします。

開くアニメーションの途中でタブを切り替えて奥のパネルが非表示になる場合のように、開いている途中で host や owner が非表示になったウィンドウは、開く途中のまま留まらず、その時点で開き終わります。残りの開く遷移は省略しますが `OpenEnd()` は実行され、`AnimationView` は開いた姿勢になり、ウィンドウは次のマネージャー更新で開いたものとして扱われます。そのため通常の `Close` で閉じられ、`OpenAsync` も完了します。host が再び表示されると、ウィンドウは完全に開いた状態です。view が `base.OnDisable()` を呼ばずに `OnDisable` を override していても、view が非表示のあいだにマネージャーが次の更新でこの開く処理を終わらせます。ただし、そうした view が同じフレームのうちに再び表示されると開く途中のまま残るため、そうした view でも `base.OnDisable()` を呼ぶことを勧めます。どのアニメーションでも途中で非表示になった `AnimationView` はそのアニメーションの終わりの姿勢になるため、次のアニメーションはその姿勢から始まります。開いている途中で破棄される view も同じように開く処理を終えるため、たとえば host のあるシーンがアンロードされるときは後片付けの途中でその `OpenEnd()` が実行されます。`DestroySelf()` で破棄するときだけ、開く処理をそのままにします。

`AnimationView` のアニメーションは既定で非スケール時間で進むため、`Time.timeScale` を 0 にする一時停止メニューから開いたポップアップも開閉します。ゲームのタイムスケールに従わせたいアニメーションでは、view の `AnimationHandler` で **Time Mode** を `Scaled` に設定するか、コードで `AnimationHandler.TimeMode` を設定してください。`TimeMode` は `Mu3Library.Utility` 名前空間にあります。

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
    _audioManager.PlaySfx(sfxClip);

    // 既定の SFX 設定をもとに、この効果音だけの音量で再生
    AudioSourceSettings sfxSettings = AudioSourceSettings.SfxStandard;
    sfxSettings.Volume = 0.5f;
    _audioManager.PlaySfx(sfxClip, sfxSettings);
}
```

フェード、ダッキング、ループ間隔、プレイリストのトラック間の間隔は既定で非スケール時間で進むため、ソース自体が再生を続けるのと同じく、`Time.timeScale` が 0 の間も進み続けます。ゲームのタイムスケールに従わせるには `_audioManager.TimeMode = TimeMode.Scaled;` を設定してください。`PlayBgm`、`PlayBgmForce`、`TransitionBgm` は `StopBgm` と同じく、再生中の BGM プレイリストを終了します。

### Addressables
key でアセットをロードしてキャッシュし、カタログの確認と更新を 1 つずつ実行する `AddressablesManager` は、独立した Addressables パッケージに移りました。使い方はその [README](../../packages/addressables/README.md) を参照してください。Base は下の Scene Loader の Addressables シーンロードを残します。

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

// Addressables がある場合、開始した後に失敗したロードは、そのコマンドが終わってから通知されます。
// そのため、ここでは IsLoading はすでに false で、ハンドラーから別のロードを開始できます。
_sceneLoaderEventBus.OnSceneLoadFailed += info =>
{
    Debug.LogWarning($"Load failed: target={info.Target}, additive={info.IsAdditive}, {info.ErrorMessage}");
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

// 結果には HTTP エラーレスポンスのステータスコードとヘッダーが入ります。コールバック API と UniTask のどちらでも同じです。
// 接続が拒否された場合のように応答を受け取れなかったリクエストのステータスコードは 0 です。
WebRequestResult<DataModel> result = await _webRequest.GetResultAsync<DataModel>("https://api.example.com/data");
if (!result.IsSuccess && result.StatusCode == 401)
{
    // ログインし直してから再試行します。
}
```

### Observableパターン
値の変更を検出し、イベントを発行します。

```csharp
[Serializable]
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

Observable フィールドは、コンポーネントや ScriptableObject の `[Serializable]` クラス、配列、リストの中に置くこともできます。Inspector で値を変えると、値が適用され購読者に通知されます。その編集を Undo や Redo したときや、プレハブインスタンスを元に戻したときも、元に戻った値で通知されます。`ObservableDictionary` では同じキーを持つ行のうち最初の行が使われ、キーが重なる行はキーを変えるまで警告付きでリストに残ります。

### Multiplayer
`MultiplayerManager`（`Mu3Library.Multiplayer`）はプレイヤーホスト型セッションを実行します。1 人のプレイヤーがホストになり、ほかのプレイヤーはホストのロビーを見つけてホストに接続し、参加者一覧、ルール、ゲームを始めるタイミングはホストが決めます。マネージャーはバックエンドの上で動作します。バックエンドは、1 つのプロセス内の複数のプレイヤー、または `LocalNetwork.CreateMachineWide` で作ったネットワーク上で同じ PC のエディターとプレイヤービルドに使う `LocalBackend`（`Mu3Library.Multiplayer.Local`）か、独立した [Steam パッケージ](../../packages/steam/README.md)の `SteamBackend` です。独自のアセンブリ定義にあるコードは、`Mu3Library.Multiplayer` を参照して使います。すべてはメインスレッドで実行され、バックエンドが報告する内容、ほかのプレイヤーのメッセージ、ロビーへのリクエストへの応答は `Update` の中から届くため、`Update` は毎フレーム呼んでください。

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
        // 同じ PC のプレイヤー。Steam のゲームは SteamBackend.TryCreate でバックエンドを作るか、
        // エディターでは LocalBackend に切り替わる SteamBackend.CreateOrFallback で作ります。
        _backend = new LocalBackend(LocalNetwork.CreateMachineWide("my-game"), "Player");
        _manager = new MultiplayerManager();
        _manager.Initialize(_backend, new MultiplayerConfig("my.game", 1));

        // すべてのプレイヤーが、同じメッセージの種類を同じポリシーで登録します。
        _manager.RegisterMessage(MoveMessage, OnMove);

        _manager.OnGameStarted += startData =>
        {
            // 開始データが示すレベルをロードしてから、このプレイヤーがプレイの準備ができたことをホストに伝えます。
            _manager.ReportLoaded();
        };
        _manager.OnAllPlayersLoaded += () => Debug.Log("Every player loaded"); // ホストのみ
    }

    private void Update() => _manager.Update();

    private void OnDestroy()
    {
        _manager.Dispose(); // バックエンドより先に。バックエンドはそれを作った側のものです
        _backend.Dispose();
    }

    // ホストがロビーを作ります。
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

    // クライアントがロビーを検索して参加します。
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

    public void SetReady() => _manager.SetReady(true); // ロビーにいるクライアント

    // ルールが許せば、ホストがゲームを開始します。結果は準備ができていないプレイヤーを示します。
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
        // context.Sender のキャラクターをその位置に動かします。
    }

    public void ReturnToLobby() => _manager.EndGame(); // ホスト

    public void Quit() => _manager.Leave();
}
```

- **メッセージポリシー**: `MessagePolicy.Default` ではどのプレイヤーもその種類を送ることができ、ホストはクライアントのメッセージを、自身のハンドラーが実行される前に中継します。`HostOnly` はスコアやスポーンのようにホストだけが決めるもののためのもので、クライアントが送るとホストは破棄します。`NoClientRelay` は攻撃のように、ほかの誰かが見る前にホストが確認しなければならないクライアントのリクエストのためのもので、クライアントはそれを `MessageTarget.Host` に送り、ホストのハンドラーが受け入れたものを転送します。ポリシーを守らせるのはホストなので、すべてのプレイヤーが各種類を同じポリシーで登録します。
- **送信モード**: 既定の `DeliveryMode.Reliable` は、同じ送信者から送った順に届き、失われてはならないイベントに向いています。`Unreliable` は失われたり順序が入れ替わって届いたりすることがあり、毎フレーム送る位置のように、次のメッセージで置き換わる状態に向いています。
- **送信量の制限**: `SessionRules` は既定では制限を設けません。`MaxMessagesPerSecond` と `MaxBytesPerSecond` は、ホストが 1 つのクライアントから 1 秒間に受け取る量の上限を決め、準備の変更やロード報告を含め、クライアントが送るすべてのパケットを数えます。そのため、ゲームが最も多く送るときの量より大きく設定してください。位置を毎秒 30 回送るクライアントは `SessionRules.Default.WithCapacity(4).WithMaxMessagesPerSecond(60).WithMaxBytesPerSecond(16 * 1024)` の範囲に収まります。ホストは制限を超えたゲームメッセージを破棄して `OnRateLimitExceeded` を発生させ、`RateLimitKickSeconds` が 0 より大きいと、その秒数のあいだ毎秒制限を超え続けたクライアントをキックします。
- **ロード**: `StartGame` の後、各プレイヤーはレベルの準備ができたら `ReportLoaded` を呼びます。ホストは `OnAllPlayersLoaded` を受け取るか、`SessionRules.LoadTimeoutSeconds`（既定 60 秒）が過ぎると、報告しなかったプレイヤーを示す `OnLoadTimedOut` を受け取ります。1 つのゲームの中でレベルを切り替えるゲームは、次のレベルの名前のようなデータを渡して、ホストで `BeginLoadPhase` を呼びます。すると全プレイヤーのロード済みフラグがクリアされ、全プレイヤーがそのデータとともに `OnLoadPhaseBegan` を受け取ってロードし、再び報告します。ホストは同じルールで再び待ちます。フェーズが始まる前に送った報告は、そのフェーズでは数えられません。

```csharp
// Awake の中で OnGameStarted と並べて登録します。ホストを含む全プレイヤーが、新しいフェーズごとにこれを受け取ります。
_manager.OnLoadPhaseBegan += phaseData =>
{
    // フェーズのデータが示すレベルをロードしてから、再び報告します。
    _manager.ReportLoaded();
};

// その後ゲーム中に、ホストが全員を次のレベルへ移します。
_manager.BeginLoadPhase(Encoding.UTF8.GetBytes("Level2"));
```
- **ボイス**: `SteamBackend` と `LocalBackend` は `IVoiceBackend`（`Mu3Library.Multiplayer.Backend`）を実装します。このインターフェースは、このプレイヤーのボイスを録音し、ほかのプレイヤーのボイスをデコードします。Steam では Steam のボイス機能を、ローカルバックエンドでは既定のマイクを使うため、同じコードがエディターでも動作します。ゲームはボイスをゲームメッセージ（通常は `Unreliable`）として運び、誰に聞かせるかを決めます。送信者が `MessageTarget` で決めるか、近接ボイスや無線チャンネルに必要なように、ホストがメッセージごと・プレイヤーごとに `SetRelayFilter` で決めます。`VoicePlayer` は各話者のボイスをゲーム自身の `AudioSource` で再生し、`spatialBlend` やロールオフのようなその設定が、シーンの中でボイスがどう聞こえるかを決めます。ボイスも送信量の制限に数えられます。ローカルバックエンドのボイスは、録音中は無音も含めて毎秒 16,000 バイトを使います。

```csharp
// 上のセッションのコードに加えます。using System.Collections.Generic; も追加してください
private const ushort VoiceMessage = 2;
private IVoiceBackend _voice;
private readonly Dictionary<PlayerId, VoicePlayer> _voicePlayers = new Dictionary<PlayerId, VoicePlayer>();

// Awake の中で、Initialize の後に。
_voice = _backend as IVoiceBackend;
_manager.RegisterMessage(VoiceMessage, OnVoice);
// フィルターが効くのはホストでだけです。遠くにいるプレイヤーの声は誰にも聞こえません。
_manager.SetRelayFilter(VoiceMessage, (speaker, listener) => AreClose(speaker, listener));
_manager.OnParticipantLeft += (participant, reason) =>
{
    if (_voicePlayers.TryGetValue(participant.Id, out VoicePlayer player))
    {
        player.Dispose();
        _voicePlayers.Remove(participant.Id);
    }
};

// プッシュトゥトーク。
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

// Update の中で、_manager.Update() の後に。前のフレーム以降に録音した分を送り、再生します。
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
        // 話者のキャラクターにある AudioSource。ゲームが探して渡します。
        player = new VoicePlayer(VoiceSourceOf(context.Sender), _voice.VoiceSampleRate);
        _voicePlayers.Add(context.Sender, player);
    }

    if (_voice.TryDecodeVoice(payload, out ReadOnlySpan<float> samples))
    {
        player.Write(samples);
    }
}
```
- `IMultiplayerManagerEventBus` にすべてのイベントがまとめられ、`IMultiplayerManager` と `IMultiplayerMessenger` の XML コメントが各呼び出しを説明しています。エディターでは、**Mu3Library > Windows > Multiplayer Debugger** がプレイモードで動作中のセッションを表示します。

## 🔧 オプションパッケージ

以下のパッケージがインストールされると、該当機能が自動的に有効になります:

| パッケージ | Define | 機能 |
|-------|--------|------|
| [UniTask](https://github.com/Cysharp/UniTask) | `MU3LIBRARY_UNITASK_SUPPORT` | async/await非同期API |
| Unity Addressables | `MU3LIBRARY_ADDRESSABLES_SUPPORT` | Addressables シーンロード |
| Unity Input System | `MU3LIBRARY_INPUTSYSTEM_SUPPORT` | 新しい入力システム |

Android と iOS のローカル通知は独立した Notifications パッケージに移りました。このパッケージは Unity Mobile Notifications を依存パッケージとして一緒にインストールするため、define シンボルで有効にするものではありません。詳しくはその [README](../../packages/notifications/README.md) を参照してください。

`InputSystemManager` は独立した Input System パッケージに移りました。このパッケージは Unity Input System を依存パッケージとして一緒にインストールします。詳しくはその [README](../../packages/inputsystem/README.md) を参照してください。Base は、後述する `MVPManager` が作成する `EventSystem` の入力モジュールのために `MU3LIBRARY_INPUTSYSTEM_SUPPORT` を残します。

`LocalizationManager` は独立した Localization パッケージに移りました。このパッケージは Unity Localization を依存パッケージとして一緒にインストールするため、define シンボルで有効にするものではありません。詳しくはその [README](../../packages/localization/README.md) を参照してください。

`AddressablesManager` は独立した Addressables パッケージに移りました。このパッケージは Unity Addressables を依存パッケージとして一緒にインストールします。詳しくはその [README](../../packages/addressables/README.md) を参照してください。Base は、前述の `SceneLoader` の Addressables シーンロードのために `MU3LIBRARY_ADDRESSABLES_SUPPORT` を残します。

マルチプレイセッションの Steam バックエンドは独立した Steam パッケージに移りました。Steamworks.NET は Git で配布されるため、このパッケージは一緒にインストールできず、`MU3LIBRARY_STEAMWORKS_SUPPORT` のゲートを残します。インストールの順序と対応プラットフォームはその [README](../../packages/steam/README.md) を参照してください。

Unity 6.6 では UniTask 2.5.11 以降を使ってください。それより前のリリースのエディタトラッカーウィンドウは非ジェネリックの `TreeView` を使っているため、6.6 では `UniTask.Editor` がコンパイルできません。

`EventSystem` がまだない場合は `MVPManager` が作成します。その入力モジュールは Input System パッケージがインストールされているかどうかだけでなく、Player Settings の **Active Input Handling** が有効にしている入力バックエンドに従います。Input System バックエンドが無効のとき、`InputSystemUIInputModule` は入力を何も読まないためです。

| Active Input Handling | Input System パッケージ | 作成される `EventSystem` の入力モジュール |
|---|---|---|
| Input Manager (Old) | インストール済み | `StandaloneInputModule` |
| Input System Package (New) | インストール済み | `InputSystemUIInputModule` |
| Both | インストール済み | `InputSystemUIInputModule` |
| すべての値 | 未インストール | `StandaloneInputModule` |

パッケージがない場合、`StandaloneInputModule` が入力を読むのは "Input Manager (Old)" と "Both" のときだけです。シーンにすでにある `EventSystem` はそのまま使います。

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

fallback Inspector はすべての `MonoBehaviour` と `ScriptableObject` に `isFallback` で登録されるため、Base をインストールすると、`ButtonInvoke` を使うかどうかに関係なく、独自の Custom Inspector を持たないそれらの型はすべてこの Inspector で描画されます。この Inspector は `CreateInspectorGUI` を通じて UI Toolkit で構築され、Unity の既定の Inspector を描画したうえで、その下にボタンを追加します。そのため、`CreatePropertyGUI` だけを実装したプロパティドロワーも IMGUI のプロパティドロワーも Unity 自身の既定の Inspector と同じように描画され、複数オブジェクトの編集も動作し、ボタンは選択中のすべてのオブジェクトでメソッドを実行します。独自のエディターは `ButtonInvokeEditorBase` を継承すると同じ既定の Inspector とボタンを得られ、そのエディターが `OnInspectorGUI` をオーバーライドしている場合、Inspector は UI Toolkit ではなく、その IMGUI メソッドを引き続き呼び出します。`OnInspectorGUI` を直接呼び出して IMGUI でエディターを描画するツールでは IMGUI 版が描画され、そこでは `CreatePropertyGUI` だけを実装したドロワーは "No GUI Implemented" と表示されます。

## 📖 全モジュールリスト

- **Addressable**: 独立した Addressables パッケージに移動（[README](../../packages/addressables/README.md)）
- **Attribute**: `ConditionalHideAttribute` や `ButtonInvokeAttribute` などのカスタム属性
- **Audio**: BGM/SFX管理システム
- **Particle**: `ParticleSystem` の便利な操作、loop 設定、`OnPlayed`、`OnStopped`、`OnPaused`、`OnUnPaused`、`OnCleared`、`OnRestarted`、`OnLoopChanged`、`OnCompleted` イベントを提供する `ParticleHandler` MonoBehaviour
- **DI**: 独立した DI パッケージに移動（[README](../../packages/di/README.md)）。ライフサイクルと注入の契約は Base の `Mu3Library.Foundation` に残る
- **Event**: `SubscribeHandler` を通した owner 管理型 subscription utility、再利用可能な one-shot helper、破棄可能な `ISubscriptionInfo` token
- **Extensions**: GameObject、Transform、Vector3などの拡張メソッド
- **Localization**: 独立した Localization パッケージに移動（[README](../../packages/localization/README.md)）
- **Multiplayer**: 差し替え可能なバックエンド上で、ロビー、参加者一覧、準備確認、ホストが設定・削除するロビーデータと各プレイヤーが設定・削除するプレイヤーごとのデータ（どちらも変更を全プレイヤーが受け取る）、ロビーチャット、定員ルール、クライアントごとの送信量の制限、ゲーム開始、キック、ゲーム中に接続が切れたプレイヤーがクライアントごとに決めた試行時間内に再接続するまで保持される枠とその後のロビーへの入り直し（失敗するとゲームに通知）、開始後と、ゲーム中にホストが始める各ロードフェーズで全プレイヤーのロード完了をホストが待つ同期、ホストが中継し `MessageWriter` と `MessageReader` で内容を書き込み・読み取るゲームメッセージ、各接続の品質（ping、パケットロス、毎秒のバイト数）と、全プレイヤーが読める、ホストが測った各プレイヤーの ping、テクスチャとして取得し、プレイヤーが画像を変えれば追従し、表示しなくなればゲームが解放するプレイヤーのアバター、プレゼンスのステータス、そしてバックエンドが録音・デコードし、ゲームがメッセージとして、ゲーム自身またはホストの中継フィルターが選んだプレイヤーに運び、`VoicePlayer` が話者ごとに `AudioSource` で再生するボイスを扱うプレイヤーホスト型セッション。バックエンドには `LocalBackend`（1 つのプロセス内で複数のプレイヤー、または `LocalNetwork.CreateMachineWide` で作ったネットワーク上で、同じ PC のエディターとプレイヤービルド。このネットワークはレイテンシー、ジッター、パケットロス、通信断をシミュレートできます）があり、Steam ロビーと Steam 接続でプレイし、プレイヤーの Steam アバターと、フレンドリストにステータスを表示する Steam のリッチプレゼンスを扱う `SteamBackend` は、独立した Steam パッケージ（[README](../../packages/steam/README.md)）にあります。メッセージの種類ごとの数も取得でき、エディターには、プレイモードで動作中の各セッションの状態、プレイヤー、接続品質、メッセージ数を表示する **Mu3Library > Windows > Multiplayer Debugger** ウィンドウがあり、その **Host ping** 列は、ホストでもクライアントでも、ホストが測った各プレイヤーの ping を表示します
  - **バックエンドの作成**: 他のプラットフォームのバックエンドは `Mu3Library.Multiplayer.Backend` の `IMultiplayerBackend` を実装し、マネージャーが探す 2 つのオプションのインターフェースも実装できます。公開でないロビーに招待でしか入れない場合は `ILobbyInviteBackend` を実装して、保持される枠に戻ったプレイヤーをホストが招待し直せるようにし、バックエンドが利用できない理由を伝えるには、Steam バックエンドが `SteamNotRunning` を伝えるように `IUnavailableReasonBackend` を実装します。3 つ目の `IVoiceBackend` はゲーム自身が使うインターフェースで、ボイスを録音・デコードします。`LobbyDataKeys` は、マネージャーが `mu3.` プレフィックスで予約し、自身が作成するすべてのロビーに書き込むロビーデータのキーをまとめたもので、バックエンドはこれらを他のロビーデータと同じように保存し、読み取ることもできます。`SearchLobbies` が戻った後に検索を始めるバックエンドは、`LobbySearchQuery.Clone()` で作ったクエリのコピーを保持します
- **ObjectPool**: 重複した非アクティブオブジェクトの再登録防止、`List<T>` の一括 enqueue と count または引数リスト指定で `List<T>` を返す dequeue、任意の引数なし生成コールバック、型安全な `CreateArguments` 初期化コールバック、`Clear()` によるクリーンアップを備えたキュー方式のオブジェクトプーリング
- **Observable**: データ変更検出システム
- **Preference**: PlayerPrefsラッパー
- **Resource**: Resourcesフォルダローディング
- **Scene**: phase/status 参照、lifecycle/progress callback、one-shot lifecycle 購読 helper、統合 rejection event を提供するシーンローディング抽象化
- **UI**: MVPパターン実装、軸ごとの一括/個別の分割線で子を9つの領域のいずれかに anchor で合わせる `UIAreaGrid`/`UIAreaElement`、canvas を画面の safe area 内に保つ `SafeCanvas`/`SafeRect`
- **IS**: 独立した Input System パッケージに移動（[README](../../packages/inputsystem/README.md)）
- **Utility**: Singleton、EasingFunctions、Settings、カメラのビューポートに `SpriteRenderer` 背景を合わせる `Mu3Library.Utility.WorldSpaceBackground`、ゲームが動いている間、毎フレーム自分で確認せずに大きさや safe area が変わった画面に追従する `Mu3Library.Utility.ScreenChangeNotifier`
- **WebRequest**: HTTPリクエスト管理

## 🧱 URP と Watermelon Game パッケージ

URP と Watermelon Game パッケージの主な機能を、パッケージごとに以下にまとめます:

URP パッケージ (**Mu3 Library URP**):
- **Camera Stack Helper**: `Mu3Library.URP.Cam.CameraStackSetter` が `SetCameraStackToMain(...)` と `SetCameraStack(...)` helper を提供し、任意の URP overlay camera を `Camera.main` または明示的な root camera stack に挿入しつつ、必要に応じて挿入 index を制御できます。

Watermelon Game パッケージ (**Mu3 Library Game - Watermelon**):
- **Fruit item indexes**: ボード設定は常に11個のフルーツ項目を含み、0始まりのリスト index でアクセスします。
- **Item contact area**: board 基準の resize が item collider の大きさを合わせるため、どの `BoardConfig` でも item index ごとの接触領域は同じです。`BoardItemInfo.ColliderScale` は collider が sprite に占める割合を、`BoardItemInfo.ColliderOffset` は collider 直径に対する比率でその中心を決め、sprite は collider を包むように描かれます。
- **Board fitting**: `BoardArea.Fit(...)` で設定したカメラビューポートの padding 内にボードスプライトを合わせられます。
- **Board collision boundaries**: `BoardArea.SetOutColliders()` で item-area viewport padding を基準に left、right、bottom の `BoxCollider2D` 境界を生成・更新でき、top は開いたままにします。
- **Board-relative coordinate conversions**: `BoardArea` は world、screen、local の正規化位置変換と初期化安全な `Try...` variant を提供します。
- **Board input**: `InputHandler` は、Input System パッケージがインストールされていて Active Input Handling がそのバックエンドを有効にしていれば Input System を、それ以外は Input Manager を読みます。UI の上で押した入力はドラッグを始めず、`InputHandler.DragStartFilter` でプロジェクト独自のルールにより押下を止められ、離す前に取り上げられたドラッグは保持中のアイテムを手に残したままキャンセルされます。
- **Merge effects**: `BoardItemInfo` は任意の merge effect 用 `ParticleHandler` resource をサポートし、完了した生成 effect instance を自動的に cleanup します。
- **Scoring extension point**: `BoardItemScoreRule.GetScore(int)` を override して標準の三角数 score progression をカスタマイズできます。
- **Board commands**: `BoardController.EnqueueCommand(...)` は board の公開 command queue に command を追加します。`IUpdatableBoardCommand` と `ICancelableBoardCommand` により command をフレーム単位で実行し、キャンセルでき、`BoardCommand` はその両方を lifecycle hook の上に組み立てます。`BoardController.CommandContext` は外部 command が item・score・sound に触れるための狭い surface で、package は `MergingCommand` を含む Flow / Item / Score command を用意しています。
- **Board sound volumes**: `BoardController.SfxVolume` と `BoardController.BgmVolume` で `BoardConfig.SoundConfig` とは別に board の再生 volume を設定でき、両方とも 0–1 に clamp されます。

新しい helper で MVP render camera を stack したい場合は、render camera を明示的に渡してください:

```csharp
using Mu3Library.URP.Cam;

CameraStackSetter.SetCameraStack(targetCamera, _mvpManager.RenderCamera);
```

## 🏗 アーキテクチャ概要

コアと各コアが持つコンテナ、あるコアが別のコアのサービスに届く方法は、コンテナとともに DI パッケージに移りました。その README の [Cores and CoreRoot](../../packages/di/README.md#cores-and-coreroot) を参照してください。

## 📝 最近のアップデート

- [変更履歴](../changelog/CHANGELOG.ja.md)には、すべてのパッケージバージョンのリリースノートがパッケージとバージョンごとの見出しの下に、まだリリースされていない変更が Unreleased の下にあります。
- インストール方法のインストール URL は、各パッケージの最新リリースタグを指します。各パッケージのバージョンと依存関係は、そのパッケージの `package.json` にあります。

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
- Base: `com.github.doqltl179.mu3library.base`
- Notifications: `com.github.doqltl179.mu3library.notifications`
- Input System: `com.github.doqltl179.mu3library.inputsystem`（Base に依存）
- Localization: `com.github.doqltl179.mu3library.localization`（Base に依存）
- Addressables: `com.github.doqltl179.mu3library.addressables`（Base に依存）
- DI: `com.github.doqltl179.mu3library.di`（Base に依存）
- Steam: `com.github.doqltl179.mu3library.steam`（Base に依存、Steamworks.NET が必要）
- URP: `com.github.doqltl179.mu3library.urp`（Base に依存）
- Watermelon Game: `com.github.doqltl179.mu3library.game.watermelon`（Base に依存）
- バージョンとリリースノート: [変更履歴](../changelog/CHANGELOG.ja.md)

Unity開発者のために制作
