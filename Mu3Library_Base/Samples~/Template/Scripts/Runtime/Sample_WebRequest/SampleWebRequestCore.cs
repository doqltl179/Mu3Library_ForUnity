using System.Collections.Generic;
#if TEMPLATE_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif
using Mu3Library.DI;
using Mu3Library.Extensions;
using Mu3Library.Sample.Template.Global;
using Mu3Library.Scene;
using Mu3Library.WebRequest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Template.WebRequest
{
    public class SampleWebRequestCore : CoreBase
    {
        private IWebRequestManager _webRequestManager;
        private ISceneLoader _sceneLoader;
        private readonly List<string> _logEntries = new();

        [Space(20)]
        [SerializeField] private string _testUrl = "https://upload.wikimedia.org/wikipedia/commons/8/8a/Official_unity_logo.png";
        [SerializeField] private string _testStringUrl = "https://httpbin.org/json";
        [SerializeField] private string _smokeFailureUrl = "http://127.0.0.1:1/";

        [Space(20)]
        [SerializeField] private RectTransform _testImageRect;
        [SerializeField] private RawImage _testTextureImage;
        [SerializeField] private TextMeshProUGUI _testStringText;

        [Space(20)]
        [SerializeField] private KeyCode _getDownloadSizeKey = KeyCode.Q;
        [SerializeField] private KeyCode _getStringKey = KeyCode.W;
        [SerializeField] private KeyCode _getTextureKey = KeyCode.E;
        [SerializeField] private KeyCode _callbackSmokeCheckKey = KeyCode.R;
#if TEMPLATE_UNITASK_SUPPORT
        [SerializeField] private KeyCode _asyncFailureSmokeCheckKey = KeyCode.T;
        [SerializeField] private KeyCode _asyncCancellationSmokeCheckKey = KeyCode.Y;
#endif

        [Space(20)]
        [SerializeField] private Button _backButton;


        protected override void Start()
        {
            base.Start();

            _webRequestManager = GetClassFromOtherCore<NetworkCore, IWebRequestManager>();
            _sceneLoader = GetClassFromOtherCore<SceneCore, ISceneLoader>();

            _backButton.onClick.AddListener(OnBackButtonClicked);

            TestGetDownloadSize();
            TestGetString();
            TestGetTexture();
            RecordLog($"Press {_callbackSmokeCheckKey} to verify callback failure results.");
#if TEMPLATE_UNITASK_SUPPORT
            RecordLog($"Press {_asyncFailureSmokeCheckKey} for async failure results and {_asyncCancellationSmokeCheckKey} for cancellation.");
#endif
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            _backButton.onClick.RemoveListener(OnBackButtonClicked);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_getDownloadSizeKey))
            {
                TestGetDownloadSize();
            }
            else if (Input.GetKeyDown(_getStringKey))
            {
                TestGetString();
            }
            else if (Input.GetKeyDown(_getTextureKey))
            {
                TestGetTexture();
            }
            else if (Input.GetKeyDown(_callbackSmokeCheckKey))
            {
                RunCallbackFailureSmokeCheck();
            }
#if TEMPLATE_UNITASK_SUPPORT
            else if (Input.GetKeyDown(_asyncFailureSmokeCheckKey))
            {
                RunAsyncFailureSmokeCheckAsync().Forget();
            }
            else if (Input.GetKeyDown(_asyncCancellationSmokeCheckKey))
            {
                RunAsyncCancellationSmokeCheckAsync().Forget();
            }
#endif
        }

        private void OnBackButtonClicked()
        {
#if UNITY_EDITOR
            _sceneLoader.LoadSingleSceneWithAssetPath(SceneNames.GetSceneAssetPath(SceneNames.Main));
#else
            _sceneLoader.LoadSingleScene(SceneNames.Main);
#endif
        }

        private void TestGetDownloadSize()
        {
            _webRequestManager.GetDownloadSize(_testUrl, size =>
            {
                string message = $"Download size of {_testUrl} || {size} bytes ||{size.BytesToKB():F3} KB || {size.BytesToMB():F3} MB";
                Debug.Log(message);
                RecordLog(message);
            });
        }

        private void TestGetString()
        {
            _webRequestManager.Get<string>(_testStringUrl, response =>
            {
                string preview = CreateResponsePreview(response);
                Debug.Log($"Get string response from {_testStringUrl} || {response}");
                RecordLog($"Get string response from {_testStringUrl} || {preview}");
            });
        }

        private void TestGetTexture()
        {
            _webRequestManager.Get<Texture2D>(_testUrl, texture =>
            {
                string message = $"Get texture response from {_testUrl} || {texture.width} x {texture.height}";
                Debug.Log(message);
                RecordLog(message);

                _testTextureImage.texture = texture;

                var rectSize = _testImageRect.rect.size;
                var textureSize = new Vector2(texture.width, texture.height);
                if (textureSize.x / textureSize.y > rectSize.x / rectSize.y)
                {
                    _testTextureImage.rectTransform.sizeDelta = new Vector2(
                        rectSize.x,
                        rectSize.x * textureSize.y / textureSize.x);
                }
                else
                {
                    _testTextureImage.rectTransform.sizeDelta = new Vector2(
                        rectSize.y * textureSize.x / textureSize.y,
                        rectSize.y);
                }
            });
        }

        private void RunCallbackFailureSmokeCheck()
        {
            RecordLog($"Running callback smoke check with {_smokeFailureUrl}");
            _webRequestManager.GetWithResult<string>(_smokeFailureUrl, result =>
            {
                bool passed = !result.IsSuccess && !string.IsNullOrEmpty(result.ErrorMessage);
                RecordLog(FormatSmokeStatus(
                    passed,
                    "Callback failure result returned without throwing."));
            });
        }

#if TEMPLATE_UNITASK_SUPPORT
        private async UniTaskVoid RunAsyncFailureSmokeCheckAsync()
        {
            RecordLog($"Running async failure smoke check with {_smokeFailureUrl}");

            try
            {
                WebRequestResult<string> result = await _webRequestManager.GetResultAsync<string>(_smokeFailureUrl);
                bool passed = !result.IsSuccess && !string.IsNullOrEmpty(result.ErrorMessage);
                RecordLog(FormatSmokeStatus(
                    passed,
                    "Async failure returned a WebRequestResult failure."));
            }
            catch (System.Exception ex)
            {
                RecordLog(FormatSmokeStatus(
                    false,
                    $"Async failure smoke check threw {ex.GetType().Name}."));
            }
        }

        private async UniTaskVoid RunAsyncCancellationSmokeCheckAsync()
        {
            RecordLog("Running async cancellation smoke check with a pre-canceled token");

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            try
            {
                await _webRequestManager.GetResultAsync<string>(_testStringUrl, cancellationToken: cts.Token);
                RecordLog(FormatSmokeStatus(
                    false,
                    "Async cancellation smoke check unexpectedly completed."));
            }
            catch (System.OperationCanceledException)
            {
                RecordLog(FormatSmokeStatus(
                    true,
                    "Async cancellation stayed explicit."));
            }
            catch (System.Exception ex)
            {
                RecordLog(FormatSmokeStatus(
                    false,
                    $"Async cancellation smoke check threw {ex.GetType().Name}."));
            }
        }
#endif

        private string FormatSmokeStatus(bool passed, string description)
        {
            string prefix = passed ? "[PASS]" : "[FAIL]";
            return $"{prefix} {description}";
        }

        private string CreateResponsePreview(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return "Response Preview || <empty>";
            }

            const int maxLength = 240;
            string preview = response.Length <= maxLength
                ? response
                : $"{response.Substring(0, maxLength)}...";
            return $"Response Preview || {preview}";
        }

        private void RecordLog(string message)
        {
            string timestampedMessage = $"[{System.DateTime.Now:HH:mm:ss}] {message}";
            _logEntries.Insert(0, timestampedMessage);

            RefreshOutputText();
        }

        private void RefreshOutputText()
        {
            if (_testStringText == null)
            {
                return;
            }

            _testStringText.text = string.Join("\n\n", _logEntries);
        }
    }
}
