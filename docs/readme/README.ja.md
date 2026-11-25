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
- 🖥 **デバッグコンソール**: 型付きのコマンド、開発者専用とプレイヤー向けのアクセス、進行を変えるコマンドの印、Unity のログを備えた、プレイヤービルド向けのゲーム内コンソール
- 🔀 **ステートマシン**: ゲーム、UI、AI のフローに使う、キーで登録するステート。ステートに入る処理と出る処理はコルーチンとして、または UniTask では async メソッドとして待つことができ、遷移が待つあいだのリクエストは順番にキューに入り、ガードと前のステートに戻る手段も備えます
- 💾 **セーブシステム**: データバージョンと移行、安全に書き込まれる検証付きのファイルを備え、`Vector3` のような Unity の型をそのまま保存する、スロットへのセクション単位のセーブと、各プレイヤーのデータをその ID ごとに保持し、ホストがほかのプレイヤーに共有できるマルチプレイのセッションセーブ
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
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=packages/steam#steam/v0.4.0

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

Steam パッケージは、マルチプレイのないゲームのために Steam を開始する `SteamManager`、Steam でマルチプレイセッションを実行する `SteamBackend`、任意で使えるデバッグコンソールのコマンドを備えた Steam の実績と統計、セーブシステム用の Steam Cloud ストレージを含みます。先に Steamworks.NET と Base をインストールしてください。

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

### ステートマシン
`StateMachine<TKey>`（`Mu3Library.States`）は、ゲームがキー（通常は enum）で登録したステートのあいだを遷移します。起動からタイトル、ロビー、ゲーム、リザルトへ進むゲームのフロー、UI のフロー、敵の行動などに使えます。ステートは通常 `State` のサブクラスで、必要なものをオーバーライドします。コルーチンと同じように `yield` で待てる `Enter` と `Exit`、そして `Update` です。マシンはこれらのルーチンを自身の `Update` から進めるため、`null`、入れ子のルーチン、`WaitUntil` や `WaitForSecondsRealtime` のような `CustomYieldInstruction`、`AsyncOperation` を待ちますが、`WaitForSeconds` とそのほかの `YieldInstruction` は警告とともに 1 フレームだけ待ちます。時間を待つには、代わりに `WaitForSecondsRealtime` か `WaitUntil` を使ってください。遷移は現在のステートから出てその `Exit` が終わるまで待ち、次に新しいステートに入ってその `Enter` が終わるまで待ってから、`OnStateChanged` を発生させます。何も待たない遷移は `ChangeState` の中で終わります。遷移が待っているあいだに来たリクエストは、来た順に 1 つずつ実行されます。`AddGuard` は遷移を止められる条件を追加し、止められた遷移は `OnTransitionBlocked` が知らせます。`Back` は 1 つ前のステートに 1 段階だけ戻ります。マシンは履歴を持たないため、2 回目の `Back` は 1 回目の `Back` で離れたステートに戻ります。マシンを持つ側は毎フレーム `Update` を呼びます。`Update` は待っている遷移を進め、遷移がなければ現在のステートを更新します。UniTask では、`AsyncState` を継承したステートは代わりに `EnterAsync` と `ExitAsync` を await します。マシンはどのシーンローダーや UI システムにも結び付いていません。次の例では、ゲームのフローが `SceneLoader` でシーンをロードします。

```csharp
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks; // AsyncSceneState でのみ使用
using Mu3Library.Scene;
using Mu3Library.States;
using UnityEngine;

public enum GameFlow { Boot, Title, Game }

public class SceneState : State
{
    private readonly ISceneLoader _sceneLoader;
    private readonly string _sceneName;

    public SceneState(ISceneLoader sceneLoader, string sceneName)
    {
        _sceneLoader = sceneLoader;
        _sceneName = sceneName;
    }

    // シーンのロードが終わってから次のステートが始まります。ルーチンは null、入れ子のルーチン、
    // WaitUntil や WaitForSecondsRealtime のような CustomYieldInstruction、AsyncOperation を yield できます。
    // WaitForSeconds は 1 フレームしか待ちません。
    public override IEnumerator Enter()
    {
        _sceneLoader.LoadSingleScene(_sceneName);
        yield return new WaitUntil(() => !_sceneLoader.IsLoading);
    }
}

// UniTask では、同じステートが代わりに await します。
public class AsyncSceneState : AsyncState
{
    private readonly ISceneLoader _sceneLoader;
    private readonly string _sceneName;

    public AsyncSceneState(ISceneLoader sceneLoader, string sceneName)
    {
        _sceneLoader = sceneLoader;
        _sceneName = sceneName;
    }

    public override async UniTask EnterAsync(CancellationToken cancellationToken)
    {
        await _sceneLoader.LoadSingleSceneAsync(_sceneName);
    }
}

// マシンを持つ側は、コアや DontDestroyOnLoad で残すオブジェクトのように、自分がロードするシーンより長く存続します。
_flow = new StateMachine<GameFlow>();
_flow.Add(GameFlow.Boot, new SceneState(_sceneLoader, "Boot"));
_flow.Add(GameFlow.Title, new SceneState(_sceneLoader, "Title"));
_flow.Add(GameFlow.Game, new SceneState(_sceneLoader, "Game"));
_flow.AddGuard(transition => transition.To != GameFlow.Game || _isSignedIn);
_flow.OnStateChanged += transition => Debug.Log($"Flow: {transition}");
_flow.ChangeState(GameFlow.Boot);

// 毎フレーム。
_flow.Update();

// その後: 起動シーンのロード中に出したリクエストは、順番を待ちます。
_flow.ChangeState(GameFlow.Title);
_flow.Back(); // 現在のステートの 1 つ前のステートへ

// マシンを持つ側がなくなるとき。
_flow.Dispose();
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

### セーブシステム
ゲームをセクション単位でセーブ・ロードします。各システムやステージは、ゲームの中で自分が受け持つ部分を 1 つのセクションとして記述し、ゲームはその瞬間のあらゆる細部ではなく、チェックポイントやステージの終わりのように自分で選んだタイミングでセクションをセーブ・ロードします。`Mu3Library.Save` アセンブリにあり、独自のアセンブリ定義から参照して使います。

```csharp
using System;
using Mu3Library.Save;
using UnityEngine;

[Serializable]
public class PlayerSaveData
{
    public Vector3 Position;
    public int Level;
}

public class PlayerSaveSection : SaveSection<PlayerSaveData>
{
    private readonly Transform _player;
    private readonly PlayerStats _stats;

    public override string Key => "player";
    public override int Version => 2;

    public PlayerSaveSection(Transform player, PlayerStats stats)
    {
        _player = player;
        _stats = stats;
    }

    protected override PlayerSaveData Capture() =>
        new PlayerSaveData { Position = _player.position, Level = _stats.Level };

    protected override void Restore(PlayerSaveData data)
    {
        _player.position = data.Position;
        _stats.Level = data.Level;
    }

    // バージョン 1 はレベルを保存していなかったため、そのセーブでは 0 として読み取られます。
    protected override PlayerSaveData Migrate(int savedVersion, PlayerSaveData data)
    {
        if (savedVersion < 2)
        {
            data.Level = 1;
        }

        return data;
    }
}

// Application.persistentDataPath の下の Saves にある、JSON で書き込まれるファイル。
var saves = new SaveManager();
saves.RegisterSection(new PlayerSaveSection(playerTransform, playerStats));
saves.RegisterSection(SaveSection.Create("stage", () => _stage, data => _stage = data));

saves.Save("slot1");            // 登録されたすべてのセクション
saves.Save("slot1", "stage");   // 1 つのセクション。スロットはほかのセクションを残します

saves.SetMetadata("slot1", "chapter", "3. The Forest");   // スロット一覧のため

SaveResult result = saves.Load("slot1");
if (!result.IsSuccess)
{
    Debug.LogWarning($"Load failed: {result}");   // result.Reason が理由を示します
}
```

- **セクションとスロット**: スロットはセクションのキーごとに 1 つのエントリを持ちます。1 つのセクションをセーブしてもスロットのほかのセクションは残り、スロットにあってもどのコードも登録していないセクションは、手を付けられずにそのまま残ります。スロット全体のロードは、まず登録されたすべてのセクションを読み取り、検証し、移行します。そのため、破損したセクションや新しすぎるセクションがあるか、移行が例外を投げると、どのセクションも復元されません。その後、スロットが持たない登録済みのセクション（スロットのセーブ後にゲームが追加したものなど）に対して `RestoreDefault` を呼びます。破損したスロットに 1 つのセクションをセーブすると `SaveFailureReason.Corrupted` で失敗しますが、スロット全体をセーブすると、それまでの内容は読み取れないため、登録されたセクションだけでスロットを新しく書き込みます。
- **データ**: 既定の `JsonSaveSerializer` はセクションのデータを `JsonUtility` で書き込むため、`Vector3`、`Quaternion`、`Color` のような Unity の型はそのまま保存されます。データは `[Serializable]` のクラスか構造体です。プリミティブ型、文字列、コレクションを単体で渡すと拒否されるため、フィールドに入れてください。`JsonUtility` はディクショナリもプロパティも書き込みません。
- **バージョン**: データの形が変わったら、セクションの `Version` を上げてください。古いバージョンでセーブされたスロットは `Migrate` を通ります。`Migrate` は現在の型に読み込んだデータを受け取り、古いバージョンになかったフィールドには既定値が入っています。また、どのセクションが復元されるよりも前に実行されるため、ゲームに触れてはいけません。新しいバージョンでセーブされたスロットは `SaveFailureReason.VersionTooNew` で失敗し、その上に古いバージョンを書き込もうとするセーブも同じく失敗します。そのため、バージョンを戻したビルドでも、新しいビルドがセーブしたものは何も失われません。新しいスロット形式で書き込まれたスロットも同じように拒否されます。
- **ストレージ**: `FileSaveStorage` は各スロットを `<slot ID>.sav` に保存し、一時ファイルに書き込んでからそれで置き換えるため、途中で止まった書き込みがあっても前のセーブは読み取れるままです。`MemorySaveStorage` はスロットをメモリに保持します。プラットフォームのクラウドストレージのようなほかの保存先には、`ISaveStorage` を実装してください。各スロットは CRC-32 の検証値を持つため、破損したファイルは `SaveFailureReason.Corrupted` で失敗します。
- **スロット一覧**: `GetSlots` と `TryGetSlotInfo` は、何も復元せずに、スロットのセクション、バージョン、日時、サイズ、メタデータを読み取ります。破損したスロットや新しすぎるスロットは含まれません。`SetMetadata` は、章の名前やプレイ時間のように、そうした一覧のための短い値をセーブ済みのスロットに設定します。値は 256 文字までで 32 個まで設定でき、セーブしても残ります。`Peek` は 1 つのセクションのデータを、復元せずに読み取ります。`ExportSlot` と `ImportSlot` は、バックアップなどのために、検証済みのスロットのバイト列を書き出し、受け取ります。すべての長さは読み取る前に確認されるため、ほかから来たバイト列も安全に解析できますが、その中のセクションは送り手が書いたとおりに復元されます。`MaxSlotSize`（既定 8 MiB）は、すべてのセクションを含めたスロットの大きさの上限です。
- スロット ID には英字、数字、`_`、`-` を 64 文字まで使えますが、`CON` のように Windows がデバイス用に予約している名前は使えません。セクションとメタデータのキーには `.` も使えます。大文字と小文字を区別しないファイルシステムでは、大文字と小文字だけが違う 2 つの ID は 1 つのスロットになります。呼び出しは呼び出し元のスレッドで実行され、セーブ、ロード、ストレージの失敗は `SaveResult` として返ります。例外を投げるのは、null の引数や 0 以下の `MaxSlotSize` のような、呼び出し側のコードの誤りだけです。`ISaveManagerEventBus` は `OnSaved`、`OnLoaded`、`OnSlotDeleted`、`OnSlotImported` を発生させます。

### デバッグコンソール
プレイヤービルド向けのゲーム内コンソールです。ゲームが登録したコマンド、実行した行、Unity のログを、ゲームの上に描画します。`Mu3Library.DebugConsole` アセンブリにあり、独自のアセンブリ定義から参照して使います。

```csharp
using Mu3Library.DebugConsole;
using UnityEngine;

public class ConsoleSetup : MonoBehaviour
{
    private DebugConsoleManager _console;

    private void Awake()
    {
        _console = new DebugConsoleManager();   // 存在するあいだ Unity のログを取り込みます

        // 開発者コマンドは、エディターと Development Build でのみ実行されます。
        _console.RegisterCommand<int>("level", level => LoadLevel(level),
            new ConsoleCommandOptions("Loads a level.", affectsProgress: true));

        // プレイヤーコマンドは、ゲームが意図してプレイヤーに提供するものです。
        _console.RegisterCommand<int, int>("give", GiveItem,
            new ConsoleCommandOptions("Gives an item.", ConsoleAccess.Player, affectsProgress: true));

        // リリースビルドでもプレイヤーがコンソールを開けるようにします。使えるのは Player コマンドだけです。
        _console.PlayerConsoleEnabled = _settings.AllowPlayerConsole;
        _console.OnProgressModified += commandName => _saveData.UsedConsole = true;

        DebugConsoleView.Create(_console);   // ` キーで開きます
    }

    private void GiveItem(int item, int count = 1) { /* ... */ }

    private void OnDestroy() => _console.Dispose();
}
```

- **アクセス**: 既定の `Developer` コマンドは、エディターと Development Build でのみ実行されます。`Player` コマンドは、便利機能やゲームが認めるチートのように、ゲームが意図してプレイヤーに提供するコマンドで、ゲームが `PlayerConsoleEnabled` を有効にすればリリースビルドでも実行されます。それまでリリースビルドはコンソールを閉じたままにし、そのビルドで実行できるコマンドだけを一覧表示、補完、実行します。`Developer` コマンドには、不明なコマンドとして応答します。
- **進行**: `affectsProgress` を付けたコマンドは、実行されると、その後に例外を投げても `HasModifiedProgress` を設定して `OnProgressModified` を発生させます。そのため、ゲームは独自のルールで、セーブに印を付けたり、実績の解除を控えたりできます。`ResetProgressModified` はその印を消します。
- **引数**: コマンドは型付きのパラメーターを 4 つまで取ります。`string`、`int`、`long`、`float`、`double`、`bool`（`true`、`false`、`on`、`off`、`yes`、`no`、`1`、`0`）、名前か数値で指定する enum、`x,y` と `x,y,z` で指定する `Vector2` と `Vector3` です。メソッドの既定値により末尾のパラメーターを省略でき、ベクトルの `default` はゼロとして読み取られます。ダブルクォートで囲んだ語は空白を保ちます。`Action<ConsoleCall>` で登録したコマンドは入力されたとおりの語を受け取り、`Print` できます。名前は大文字と小文字を区別せずに照合され、`help` と `clear` は組み込みで用意されています。
- **ビュー**: `DebugConsoleView` は、どちらの入力バックエンドでもキーボードを読み取る IMGUI でコンソールを描画します。切り替えキー（既定は `` ` ``）でコンソールを開閉し、Enter で行を実行し、上下キーで履歴をたどり、Tab でコマンド名を補完し、Escape で閉じます。キーボードのないデバイスでは、ゲーム独自のボタンやジェスチャーから `Open` を呼んでください。キー入力はゲーム自身の入力にも届くため、コンソールが開いているあいだは、`OnOpenChanged` からなどでゲームの入力を止めてください。ビューは Unity のログをレベルで絞り込み、コンソールは最新の `MaxLines` 行（既定 1000 行）を保持します。
- `Execute` はコードから行を実行し、行が失敗した理由を示す `ConsoleResult` を返します。例外を投げたコマンドは、その例外もログに出します。`IDebugConsoleManagerEventBus` は `OnLineAdded`、`OnCleared`、`OnOpenChanged`、`OnProgressModified`、`OnExecuted` を発生させます。ログの取り込みを止めるには、マネージャーを破棄してください。

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
- **状態同期**: `StateSyncManager`（`Mu3Library.Multiplayer.Sync`）は、バイト予算の範囲で、ゲームの状態を全プレイヤーで同じに保ちます。そのため、毎フレーム状態が変わるシステムが、それぞれ独自のメッセージを送る必要はありません。各システムは、`SyncState` のサブクラスか独自の `ISyncState` の中で、状態が何を持ち、どれだけの精度で書き込むかを決めます。状態全体の書き込みを用意し、オーバーライドすれば変わった部分の書き込みも用意できます。どれだけをいつ送るかはマネージャーが決めます。`StateSyncConfig.TickIntervalSeconds` ごと（既定では毎秒 20 回）に、変わった状態をプレイヤーと送信モードごとに 1 つのメッセージにまとめ、各プレイヤーに最大 `MaxBytesPerTick`（既定 512 バイト）を送ります。収まらない状態は後のティックまで待ち、そのティックで送る状態は優先度と待った時間で選ばれます。変更を受け取れなかったプレイヤー、ゲーム中に参加したプレイヤー、自分の枠に戻ったプレイヤーは状態全体を受け取ります。ホストは自分が所有する状態を送り、自分のキャラクターのようにクライアントが所有する状態は、同じ予算の範囲でそのクライアントからホストへ送られ、ほかのプレイヤーに転送されます。既定の信頼性なしの状態は、`FullStateIntervalSeconds`（既定 1 秒）ごとにも状態全体が送られるため、更新を失ったコピーも追いつきます。信頼性ありの状態は、ドアのように時々しか変わらない状態に向いています。関心フィルターを使うと、ホストは近くにいるプレイヤーのように、自分が選んだプレイヤーにだけ状態を送れます。`TransformSync` は Transform の位置と回転を量子化して同期し、更新のあいだで各コピーを滑らかに動かします。マネージャーはゲームの実行中（`SessionState.InGame`）に同期します。`CheckRateLimits` は、クライアントが送る量がセッションの送信量の制限に収まるかを示します。マネージャーも各ゲームの開始時にこれを確認し、収まらなければログに出します。Multiplayer Debugger は、各ティックで各プレイヤーに送った内容を表示します。

```csharp
// 上のセッションのコードに加えます。using System.Collections.Generic; と using Mu3Library.Multiplayer.Sync; も追加してください
private const ushort SyncMessage = 3;
private StateSyncManager _sync;
private readonly List<TransformSync> _transforms = new List<TransformSync>();

// Awake の中で、Initialize の後に。全プレイヤーが同じ設定で作ります。
_sync = new StateSyncManager(_manager, new StateSyncConfig(SyncMessage));

// ゲームが始まったら、全プレイヤーが同じ状態を同じ ID で登録します。
_manager.OnGameStarted += startData =>
{
    _transforms.Add(new TransformSync(1, _boss.transform));
    _sync.Register(_transforms[_transforms.Count - 1]); // ホストが送ります
    foreach (Participant participant in _manager.Participants)
    {
        // 開始データの中での位置のように、全プレイヤーがこのプレイヤーのキャラクターに与える ID。
        TransformSync character = new TransformSync(CharacterIdOf(participant.Id), CharacterOf(participant.Id));
        _transforms.Add(character);
        _sync.Register(character, participant.Id); // そのプレイヤーが送り、ホストが転送します
    }
};

// ゲームが終わったら全プレイヤーが登録を解除し、次のゲームは自分の状態を登録します。
_manager.OnReturnedToLobby += ClearSyncedStates;
_manager.OnStateChanged += change =>
{
    if (change.Current == SessionState.Offline)
    {
        ClearSyncedStates();
    }
};

// Update の中で、_manager.Update() の後に。このティックの変更を送り、コピーを動かします。
_sync.Update();
foreach (TransformSync synced in _transforms)
{
    synced.Update();
}

// OnDestroy の中で、_manager.Dispose() の前に。
_sync.Dispose();

private void ClearSyncedStates()
{
    foreach (TransformSync synced in _transforms)
    {
        _sync.Unregister(synced.Id);
    }

    _transforms.Clear();
}
```
- `IMultiplayerManagerEventBus` にすべてのイベントがまとめられ、`IMultiplayerManager` と `IMultiplayerMessenger` の XML コメントが各呼び出しを説明しています。エディターでは、**Mu3Library > Windows > Multiplayer Debugger** がプレイモードで動作中のセッションを表示します。

#### セッションセーブ
`SessionSaveManager`（`Mu3Library.SessionSave`）は、ホストで `SaveManager` を通してセッションをセーブし、ホストがそのセーブをほかのプレイヤーに送れるようにします。受け取ったプレイヤーは、後でそのセーブからセッションをホストできます。`SaveManager` に登録したセクションは、ワールドのようにセッション全体で共有するものを持ちます。各 `PlayerSaveSection<TData>`、または `PlayerSaveSection.Create` で作ったものは、セッションのプレイヤーごとに 1 回ずつ `player.<PlayerId>.<key>` としてセーブされるため、誰がホストしてもプレイヤーのデータはそのプレイヤーのものです。独自のアセンブリ定義にあるコードは、`Mu3Library.SessionSave`、`Mu3Library.Save`、`Mu3Library.Multiplayer` を参照します。

```csharp
using System;
using System.IO;
using Mu3Library.Save;
using Mu3Library.SessionSave;
using UnityEngine;

// 全プレイヤーが、同じメッセージの種類で同じように作ります。共有はその種類と次の種類を使います。
var saves = new SaveManager(new FileSaveStorage(Path.Combine(Application.persistentDataPath, "SessionSaves")));
saves.RegisterSection(SaveSection.Create("world", () => _world, data => _world = data));

var session = new SessionSaveManager(_manager, saves, messageType: 900);
session.RegisterPlayerSection(PlayerSaveSection.Create<InventoryData>("inventory",
    player => _inventories[player],
    (player, data) => _inventories[player] = data,
    restoreDefault: player => _inventories[player] = new InventoryData()));

// ホストが、ゲームの選んだタイミングで。毎フレームの Update が共有の速さを調整します。
session.Save("coop-1", sessionName: "Forest Run");
session.Load("coop-1");                          // StartGame の前に
session.Share("coop-1", friendId, out uint shareId);

// ホストが共有する相手のプレイヤー。
// AcceptShare はその ID のスロットを置き換えるため、セーブには新しい ID を使います。
session.OnShareOffered += offer => session.AcceptShare(offer.ShareId, $"shared-{Guid.NewGuid():N}");
session.OnShareCompleted += transfer => Debug.Log($"Stored {transfer.SlotId}");
```

- **プレイヤー**: セーブとロードを行うのはホストだけです。`Save` は、離れているあいだホストが枠を保持しているプレイヤーも含め、セッションの全プレイヤーを書き込みます。スロットに含まれていてもセッションにいないプレイヤーのセクションは、スロットに残ります。`Load` はセッションの各プレイヤーにそのセクションを渡し、セーブしたセッションに初めて参加したプレイヤーのように、スロットに含まれないプレイヤーは `RestoreDefault` から始まります。`LoadPlayer` は、すでに進行中のゲームに参加したプレイヤーのように、1 人のプレイヤーをロードします。プレイヤーセクションのキーは 36 文字までで、ほかのセクションのキーを `player.` で始めることはできません。
- **保存元**: スロットのメタデータには、最後にセーブしたホストと、セーブしたときのセッション名が記録され、`TryGetSessionInfo` がスロットに含まれるプレイヤーとともにそれを読み取ります。ほかのプレイヤーから受け取ったセーブはこれらを保つため、そのプレイヤーがホストとしてセーブするまで、どこから来たセーブかがわかります。
- **共有**: `Share` はセーブ済みのスロットをセッションのほかのプレイヤーに提供します。相手のゲームは `OnShareOffered` に対して、保存先のスロットを指定した `AcceptShare` か、`DeclineShare` で応答します。`AcceptShare` はその ID のスロットを置き換えるため、ホストの `SourceSlotId` や、各ホストが起動するたびに 1 から数え直す `ShareId` ではなく、新しい GUID のように、そのプレイヤー自身のどのセーブも使っていない ID を指定してください。その後ホストはセーブを、最大 `ShareChunkSize` バイト（既定 16 KiB）の信頼性ありのメッセージとして、`ShareBytesPerSecond`（既定は毎秒 128 KiB）の速さで送ります。ホスト自身の送信にはセッションの送信量の制限がかからず、共有はセッションのほかのメッセージのための余裕を残す必要があるためです。プレイヤーの 2 つの応答は、ほかのメッセージと同じようにセッションの送信量の制限に数えられます。受け取るプレイヤーは、自分の `SaveManager.MaxSlotSize` より大きいセーブを拒否し、保存する前にスロットの CRC-32 でセーブ全体を検証するため、破損したバイト列が保存されることはありません。`OnShareProgress` は送受信したバイト数を知らせます。ホストで `Share` が始めた共有も、受け取るプレイヤーで `OnShareOffered` が知らせた共有も、すべてそこで `OnShareCompleted` で終わるか、`SaveShareFailureReason` とともに `OnShareFailed` で終わります。理由には `Declined`、`Corrupted`、`Timeout`（既定 60 秒の `ShareTimeoutSeconds` 以内に応答がない）、`PlayerLeft`、`SessionEnded` などがあります。大きすぎる（`TooLarge`）、または別の共有が進行中（`Busy`）という理由で受け取るプレイヤーが自分で断った提供は、そのプレイヤーでは何も発生させず、ホストがその理由を受け取ります。

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
- **DebugConsole**: `Mu3Library.DebugConsole` アセンブリの、型付きのコマンド、開発者とプレイヤーのアクセス、進行の印、Unity のログ、IMGUI のビューを備えたゲーム内コンソール
- **Event**: `SubscribeHandler` を通した owner 管理型 subscription utility、再利用可能な one-shot helper、破棄可能な `ISubscriptionInfo` token
- **Extensions**: GameObject、Transform、Vector3などの拡張メソッド
- **Localization**: 独立した Localization パッケージに移動（[README](../../packages/localization/README.md)）
- **Multiplayer**: 差し替え可能なバックエンド上で、ロビー、参加者一覧、準備確認、ホストが設定・削除するロビーデータと各プレイヤーが設定・削除するプレイヤーごとのデータ（どちらも変更を全プレイヤーが受け取る）、ロビーチャット、定員ルール、クライアントごとの送信量の制限、ゲーム開始、キック、ゲーム中に接続が切れたプレイヤーがクライアントごとに決めた試行時間内に再接続するまで保持される枠とその後のロビーへの入り直し（失敗するとゲームに通知）、開始後と、ゲーム中にホストが始める各ロードフェーズで全プレイヤーのロード完了をホストが待つ同期、ホストが中継し `MessageWriter` と `MessageReader` で内容を書き込み・読み取るゲームメッセージ、各接続の品質（ping、パケットロス、毎秒のバイト数）と、全プレイヤーが読める、ホストが測った各プレイヤーの ping、テクスチャとして取得し、プレイヤーが画像を変えれば追従し、表示しなくなればゲームが解放するプレイヤーのアバター、プレゼンスのステータス、そしてバックエンドが録音・デコードし、ゲームがメッセージとして、ゲーム自身またはホストの中継フィルターが選んだプレイヤーに運び、`VoicePlayer` が話者ごとに `AudioSource` で再生するボイスを扱うプレイヤーホスト型セッション。バックエンドには `LocalBackend`（1 つのプロセス内で複数のプレイヤー、または `LocalNetwork.CreateMachineWide` で作ったネットワーク上で、同じ PC のエディターとプレイヤービルド。このネットワークはレイテンシー、ジッター、パケットロス、通信断をシミュレートできます）があり、Steam ロビーと Steam 接続でプレイし、プレイヤーの Steam アバターと、フレンドリストにステータスを表示する Steam のリッチプレゼンスを扱う `SteamBackend` は、独立した Steam パッケージ（[README](../../packages/steam/README.md)）にあります。変わった状態をプレイヤーごとに 1 つのメッセージにまとめ、収まるものを優先度と待ち時間で選ぶ、プレイヤーとティックごとのバイト予算の範囲での状態同期（`StateSyncManager`。Transform には `TransformSync`）と、メッセージの種類ごとの数とそのバイト数も備え、エディターには、プレイモードで動作中の各セッションの状態、プレイヤー、接続品質、メッセージ数とバイト数、状態同期の各ティックで各プレイヤーに送った内容を表示する **Mu3Library > Windows > Multiplayer Debugger** ウィンドウがあり、その **Host ping** 列は、ホストでもクライアントでも、ホストが測った各プレイヤーの ping を表示します
  - **バックエンドの作成**: 他のプラットフォームのバックエンドは `Mu3Library.Multiplayer.Backend` の `IMultiplayerBackend` を実装し、マネージャーが探す 2 つのオプションのインターフェースも実装できます。公開でないロビーに招待でしか入れない場合は `ILobbyInviteBackend` を実装して、保持される枠に戻ったプレイヤーをホストが招待し直せるようにし、バックエンドが利用できない理由を伝えるには、Steam バックエンドが `SteamNotRunning` を伝えるように `IUnavailableReasonBackend` を実装します。3 つ目の `IVoiceBackend` はゲーム自身が使うインターフェースで、ボイスを録音・デコードします。`LobbyDataKeys` は、マネージャーが `mu3.` プレフィックスで予約し、自身が作成するすべてのロビーに書き込むロビーデータのキーをまとめたもので、バックエンドはこれらを他のロビーデータと同じように保存し、読み取ることもできます。`SearchLobbies` が戻った後に検索を始めるバックエンドは、`LobbySearchQuery.Clone()` で作ったクエリのコピーを保持します
- **ObjectPool**: 重複した非アクティブオブジェクトの再登録防止、`List<T>` の一括 enqueue と count または引数リスト指定で `List<T>` を返す dequeue、任意の引数なし生成コールバック、型安全な `CreateArguments` 初期化コールバック、`Clear()` によるクリーンアップを備えたキュー方式のオブジェクトプーリング
- **Observable**: データ変更検出システム
- **Preference**: PlayerPrefsラッパー
- **Resource**: Resourcesフォルダローディング
- **Save**: `Mu3Library.Save` アセンブリの、データバージョンと移行、検証付きのスロット形式、ファイルまたはメモリのストレージを備えた、スロットへのセクション単位のセーブ
- **SessionSave**: `Mu3Library.SessionSave` アセンブリの、ホストで行うマルチプレイセッションのセーブ。各プレイヤーのセクションをその ID ごとに持ち、参加するプレイヤーや離れているプレイヤーも扱い、そのセーブからホストできるほかのプレイヤーへ、送信速度を抑えて検証付きで共有できる
- **States**: `State` と、UniTask では `AsyncState` を使う `StateMachine<TKey>`。ルーチンや async メソッドを待ちながら出てから入る遷移、順番どおりにキューに入るリクエスト、ガード、`Back` を備える
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
