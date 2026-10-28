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

- 🏗 **モジュール化されたCoreシステム**: 独立した`CoreBase`による明確な責任分離
- 💉 **カスタムDIコンテナ**: Singleton、Transient、Scopedライフタイムをサポート
- 🎨 **MVP UIパターン**: View-Presenter-Model分離によるテスト可能なUIロジック
- 🔄 **自動ライフサイクル管理**: `IInitializable`、`IUpdatable`、`IDisposable`インターフェースベース
- 📦 **オプションパッケージサポート**: UniTask、Addressables、Localization、Mobile Notificationsの条件付き有効化
- 🎵 **Audioシステム**: BGM/SFX/Environment チャンネルのボリューム制御、ミキサーグループのルーティング、BGM ダッキング、ボリュームの保存/復元、await 可能なフェード
- ✨ **Particle Handler**: 必須の `ParticleSystem` に対する再生、一時停止、停止、クリア、再起動、`Loop`/`IsLooping`/`SetLoop`/`PlayLoop`/`PlayOnce` 制御、状態取得、ライフサイクルイベントを提供する便利なコンポーネント
- 🌐 **WebRequest**: HTTP GET/POST/PUT/PATCH/DELETE、ダウンロードサイズクエリ、リトライバックオフ、実行中のキャンセル、ダウンロード進捗、UniTaskサポート
- 📊 **Observableパターン**: データ変更検出とイベントベースバインディング
- 🛠 **ユーティリティコレクション**: Extension Methods、ObjectPool、EasingFunctions
- ✅ **初期化結果コントラクト**: Addressables/Localization の初期化成功/失敗状態を明示的に提供
- 🔁 **高信頼ネットワーキング**: WebRequest 結果型 API にステータス、ヘッダー、タイムアウト、リトライと、必要時のみのキャンセル伝播を提供
- 🧭 **決定的 Core 更新**: Core 実行順序が明示的かつ安定
- ⏳ **Scene 非同期 API**: built-in / Addressables シーン向けの phase ベース UniTask helper と、resolved scene name を含む構造化 lifecycle callback
- 🎮 **Input System Manager**: アクションアセット管理、対話的リバインド、バインディングオーバーライドの永続化をサポート（オプション）
- 🧰 **エディタユーティリティドロワー**: Input System / Localization の名前エクスポーター、ラベル/ロケール、グループ/テーブル、エントリを役割別スクリプトへ分け、そのスクリプトを define シンボルで囲める Addressable グループおよび Localization データエクスポート、Localization 文字収集ツールを提供
- 🖼 **World-Space Background ユーティリティ**: 必須の `SpriteRenderer` 背景をカメラのビューポートに合わせ、任意で自動フィット、カメラ前面への配置、レンダラー表示設定を利用できます
- 🍉 **スイカゲームテンプレート**: 11 個のアイテムによる 2D マージボード、設定可能な `BoardConfig`、ボード fitting / 座標変換 helper、プレイ可能な sample scene を提供する再利用可能な `Mu3Library.Game.WatermelonGame` runtime assembly

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
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base#base/v0.26.0

    # URP パッケージ（先に Base をインストール）
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_URP#urp/v0.3.0

    # Watermelon Game パッケージ（先に Base と URP をインストール）
    https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Game_WatermelonGame#game/watermelon/v0.6.0
   ```

パッケージは Base、URP、Watermelon Game の順にインストールしてください。URP は Base に、Watermelon Game はその両方に依存します。

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

`Awake`、`Start`、`OnEnable`、`OnDisable`、`OnDestroy` をオーバーライドするコアは、必ず base のメソッドを呼び出してください。これらのメソッドが合わせて、コアを `CoreRoot` に登録し、注入し、準備させ、登録を解除します。

基底クラスが宣言した `[Inject]` メンバーも、private を含めて注入されます。クラス階層をさかのぼる探索は `CoreBase` で止まります。エディタの DI 検証ツールも同じメンバーを検査します。

登録されたサービスも、生成後かつライフサイクル callback の前に `[Inject]` フィールドとプロパティへ自動注入されます。1 つの登録（実装型、キー、ライフタイム）は、どのサービス型で resolve しても 1 つのインスタンスを返します。シングルトンはコンテナごとに 1 つ、スコープドのサービスはスコープごとに 1 つです。例外を投げたライフサイクル callback は `Debug.LogException` で記録され、ほかのサービスとコアは動き続けます。

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

カタログの確認と更新（`CheckForCatalogUpdates`、`UpdateCatalogs` とその UniTask 版）は、呼んだ順に 1 つずつ Addressables に届きます。別の呼び出しの実行中に呼んだ呼び出しは（そのコールバックの中で呼んだものを含め）、先のコールバックの次の ResourceManager の更新で、つまりそのハンドルが整理された後に始まり、`Dispose` はまだ Addressables に届いていない呼び出しを null で通知します。更新は、マネージャーが初期化された後にだけ Addressables に届きます。まだ初期化されていないマネージャーは、`Initialize` と同じ初期化を同じ `IsInitializing` の状態とイベントで行い、初期化の実行中に呼んだ更新はその初期化を待ちます。その初期化が失敗すると、更新のコールバックは null を受け取り、以降の更新も初期化の呼び出しが成功するまで再試行せずに同じように失敗します。

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

## 🔧 オプションパッケージ

以下のパッケージがインストールされると、該当機能が自動的に有効になります:

| パッケージ | Define | 機能 |
|-------|--------|------|
| [UniTask](https://github.com/Cysharp/UniTask) | `MU3LIBRARY_UNITASK_SUPPORT` | async/await非同期API |
| Unity Addressables | `MU3LIBRARY_ADDRESSABLES_SUPPORT` | 動的アセットロード |
| Unity Localization | `MU3LIBRARY_LOCALIZATION_SUPPORT` | 多言語サポート |
| Unity Input System | `MU3LIBRARY_INPUTSYSTEM_SUPPORT` | 新しい入力システム |
| Unity Mobile Notifications | `MU3LIBRARY_NOTIFICATIONS_SUPPORT` | ローカル通知 (Android/iOS、独立した `Mu3Library.Notifications` アセンブリ) |

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
- **Utility**: Singleton、EasingFunctions、Settings、カメラのビューポートに `SpriteRenderer` 背景を合わせる `Mu3Library.Utility.WorldSpaceBackground`、ゲームが動いている間、毎フレーム自分で確認せずに大きさや safe area が変わった画面に追従する `Mu3Library.Utility.ScreenChangeNotifier`
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

Watermelon Game パッケージ (**Mu3 Library Game - Watermelon**):
- **Watermelon Game**: 設定可能なボード、フルーツ sprite、sample manager/core script を含むプレイ可能な 2D 落下フルーツマージ scene
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
- **Sample assets**: package sample には `BoardConfig` asset、フルーツ/背景画像、sample manager/core script、`Demo` scene が含まれます。

**サンプルの入力とテキスト:** Base の Template・Attribute サンプルと URP の ScreenEffect サンプルのシーンは、Input System パッケージの有無にかかわらず入力に反応します。シーンの `EventSystem` には、Input Manager を読む `StandaloneInputModule` と `SampleInputModuleSelector` があります。Input System パッケージがインストールされていて **Active Input Handling** が "Input System Package (New)" か "Both" のとき、`SampleInputModuleSelector` がシーンの開始時にそのモジュールを `InputSystemUIInputModule` に置き換えます。`MVPManager` が自分で作成する `EventSystem` に適用する規則と同じです。Template サンプルの `Sample_IS` シーンは `InputSystemManager` を示すため、その機能には Input System パッケージと "Input System Package (New)" か "Both" が必要で、UI はすべての設定で反応します。Template サンプルのキーボードショートカットとマウスクリックはサンプルの `SampleInput` クラスを通り、このクラスは Input System バックエンドが有効なら Input System を、そうでなければ Input Manager を読みます。これらのサンプルの TextMesh Pro テキストは、TMP Essential Resources なしで作成したプレイヤーでも描画されます。各サンプルがテキストの使うフォントアセット・スタイルシート・シェーダーを `Fonts/` と `Shaders/` に含んでおり、そのライセンスはサンプルの `Third-Party Notices.txt` にあります。そのようなプレイヤーでは TextMesh Pro がレイアウトにテキストのサイズを 0 と報告するため、WebRequest シーンのスクロールするログのようにレイアウトがテキストのサイズに合わせる箇所は、Template サンプルの `SampleTextPreferredSize` がテキストを測ります。Editor では、TMP Essential Resources をインポートするまで TextMesh Pro はテキストを描画せず、インポートを提案します。Template サンプルでは Addressables、Localization、UniTask は引き続きオプションです。これらのパッケージがなくてもサンプルはコンパイルでき、足りないパッケージを必要とする部分はパッケージがインストールされていないことをログに出します。UtilWindow サンプルの Addressable・Localization データエクスポーターは **Define Symbol** フィールドに設定された `TEMPLATE_ADDRESSABLES_SUPPORT` と `TEMPLATE_LOCALIZATION_SUPPORT` の中に Template サンプルのキースクリプトを書き出すため、キースクリプトを再生成してもそれらのパッケージなしでコンパイルできます。

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

デフォルトカラーマテリアル（Black、Blue、Green、Magenta、Red、White）は Template サンプルには含まれていません。Base パッケージのランタイムとして `Mu3Library_Base/Runtime/Materials/` に同梱されているため、Base をインストールした時点で `Packages/com.github.doqltl179.mu3library.base/Runtime/Materials/` から使えます。

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

// 方法2: Inject属性（Core、登録済みサービス、presenterで使用可能）
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
- URP: `com.github.doqltl179.mu3library.urp`（Base に依存）
- Watermelon Game: `com.github.doqltl179.mu3library.game.watermelon`（Base、URP に依存）
- バージョンとリリースノート: [変更履歴](../changelog/CHANGELOG.ja.md)

Unity開発者のために制作
