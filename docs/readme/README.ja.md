# Mu3Library For Unity

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](../../README.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](README.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](README.ja.md)

[![Unity Version](https://img.shields.io/badge/Unity-6000.0%2B-blue.svg)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](../../LICENSE)

</div>

**Mu3Library**は、Unityプロジェクト向けのモジュール化されたアーキテクチャフレームワークです。カスタムDI（Dependency Injection）システムとMVP（Model-View-Presenter）UIパターンを中心に、拡張可能で保守しやすいゲーム開発をサポートします。

## 📘 ドキュメント

- English README: `../../README.md`
- Korean README: `README.ko.md`
- Changelog (EN): `../../CHANGELOG.md`
- Changelog (KO): `../changelog/CHANGELOG.ko.md`
- Changelog (JA): `../changelog/CHANGELOG.ja.md`

## ✨ 主な特徴

- 🏗 **モジュール化されたCoreシステム**: 独立した`CoreBase`による明確な責任分離
- 💉 **カスタムDIコンテナ**: Singleton、Transient、Scopedライフタイムをサポート
- 🎨 **MVP UIパターン**: View-Presenter-Model分離によるテスト可能なUIロジック
- 🔄 **自動ライフサイクル管理**: `IInitializable`、`IUpdatable`、`IDisposable`インターフェースベース
- 📦 **オプションパッケージサポート**: UniTask、Addressables、Localizationの条件付き有効化
- 🎵 **Audioシステム**: BGM/SFX分離管理とボリューム制御
- 🌐 **WebRequest**: HTTP GET/POST、ダウンロードサイズクエリ、UniTaskサポート
- 📊 **Observableパターン**: データ変更検出とイベントベースバインディング
- 🛠 **ユーティリティコレクション**: Extension Methods、ObjectPool、EasingFunctions
- ✅ **初期化結果コントラクト**: Addressables/Localization の初期化成功/失敗状態を明示的に提供
- 🔁 **高信頼ネットワーキング**: WebRequest 結果型 API にステータス、ヘッダー、タイムアウト、リトライを提供
- 🧭 **決定的 Core 更新**: Core 実行順序が明示的かつ安定
- ⏳ **Scene 非同期 API**: UniTask + CancellationToken ベースのシーン load/unload ヘルパー
- 🎮 **Input System Manager**: アクションアセット管理、対話的リバインド、バインディングオーバーライドの永続化をサポート（オプション）
- 🧰 **エディタユーティリティドロワー**: Input System / Localization の名前エクスポーターと Localization 文字収集ツールを提供
## 📋 要件

- Unity 6 (6000.0+)
- .NET Standard 2.1

## 📦 インストール方法

### 方法 1: パッケージマネージャー (Git URL)
1. Unity Editorで`Window` > `Package Manager`を開きます
2. `+`ボタンをクリック > `Add package from git URL...`
3. 以下のURLを入力:
   ```
   https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base
   ```

### 方法 2: パッケージマネージャー (ローカルディスク)
1. このリポジトリをクローンします
2. Unity Editorで`Window` > `Package Manager`を開きます
3. `+`ボタン > `Add package from disk...`をクリック
4. `Mu3Library_Base/package.json`を選択

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

## 📖 全モジュールリスト

- **Addressable**: Addressables統合（オプション）
- **Attribute**: `ConditionalHideAttribute`などのカスタム属性
- **Audio**: BGM/SFX管理システム
- **DI**: Dependency Injectionコンテナ
- **Extensions**: GameObject、Transform、Vector3などの拡張メソッド
- **Localization**: Unity Localizationラッパー（オプション）
- **ObjectPool**: ジェネリックオブジェクトプーリング
- **Observable**: データ変更検出システム
- **Preference**: PlayerPrefsラッパー
- **Resource**: Resourcesフォルダローディング
- **Scene**: シーンローディング抽象化
- **UI**: MVPパターン実装
- **IS**: Unity Input System ラッパーおよびバインディングマネージャー（オプション）
- **Utility**: Singleton、EasingFunctions、Settings
- **WebRequest**: HTTPリクエスト管理

## 🎓 サンプル

パッケージマネージャーの**Samples**タブから以下のサンプルをインポートできます:

Base パッケージ (**Mu3 Library**):
- **Template**: 基本的なCore構造と使用例
- **Attribute**: ConditionalHideの使用方法
- **UtilWindow**: カスタムエディターウィンドウとユーティリティドロワーの例

URP パッケージ (**Mu3 Library URP**):
- **ScreenEffect**: Grayscale / Shake / GaussianBlur / DepthOutline の 4 種類のスクリーンエフェクトと対応 handler スクリプトを含む URP スクリーンエフェクトのサンプルシーンと補助スクリプト

このリポジトリでは、Base サンプルのソースは `Mu3Library_Base/Samples~`、URP サンプルのソースは `Mu3Library_URP/Samples~/ScreenEffect` にあります。

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

## 📝 最近のアップデート

- 最新の Base パッケージ版: `0.9.0`
- 最新の URP パッケージ版: `0.1.2`（manifest 依存関係: `com.github.doqltl179.mu3library.base` `0.5.0`）
- 最新の URP `ScreenEffect` 変更を含む未リリース変更は `CHANGELOG.md` を参照してください。

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
- Base: `com.github.doqltl179.mu3library.base` `0.9.0`
- URP: `com.github.doqltl179.mu3library.urp` `0.1.2`（manifest 依存関係: `com.github.doqltl179.mu3library.base` `0.5.0`）

Unity開発者のために制作
