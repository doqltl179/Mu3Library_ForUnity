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

        [Space(20)]
        [SerializeField] private string _testUrl = "https://upload.wikimedia.org/wikipedia/commons/8/8a/Official_unity_logo.png";

        [Space(20)]
        [SerializeField] private RectTransform _testImageRect;
        [SerializeField] private RawImage _testTextureImage;
        [SerializeField] private TextMeshProUGUI _testStringText;

        [Space(20)]
        [SerializeField] private KeyCode _getDownloadSizeKey = KeyCode.Q;
        [SerializeField] private KeyCode _getStringKey = KeyCode.W;
        [SerializeField] private KeyCode _getTextureKey = KeyCode.E;

        [Space(20)]
        [SerializeField] private Button _backButton;


        protected override void Start()
        {
            base.Start();

            _webRequestManager = GetFromCore<NetworkCore, IWebRequestManager>();
            _sceneLoader = GetFromCore<SceneCore, ISceneLoader>();

            _backButton.onClick.AddListener(OnBackButtonClicked);

            TestGetDownloadSize();
            TestGetString();
            TestGetTexture();
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
                TestGetDownloadSize();
            }
            else if (Input.GetKeyDown(_getTextureKey))
            {
                TestGetTexture();
            }
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
                Debug.Log($"Download size of {_testUrl} || {size} bytes ||{size.BytesToKB():F3} KB || {size.BytesToMB():F3} MB");
            });
        }

        private void TestGetString()
        {
            _webRequestManager.Get<string>(_testUrl, response =>
            {
                Debug.Log($"Get string response from {_testUrl} || {response}");
                _testStringText.text = response;
            });
        }

        private void TestGetTexture()
        {
            _webRequestManager.Get<Texture2D>(_testUrl, texture =>
            {
                Debug.Log($"Get texture response from {_testUrl} || {texture.width} x {texture.height}");

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
    }
}
