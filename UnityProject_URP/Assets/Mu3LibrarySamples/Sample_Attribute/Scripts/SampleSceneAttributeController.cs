using Mu3Library.Attribute;
using Mu3Library.Observable;
using TMPro;
using UnityEngine;

namespace Mu3Library.Sample.Attribute
{
    public class SampleSceneAttributeSample : MonoBehaviour
    {
#pragma warning disable 0414 // "unused variable" 경고 번호
        [Title("Title Attribute")]
        [SerializeField] private int testTitleProperty = 0;

        [Title("Conditional Hide Attribute")]
        [SerializeField] private bool testConditionProperty = false;
        [ConditionalHide(nameof(testConditionProperty), true)]
        [SerializeField] private string testConditionHideProperty = "";
#pragma warning restore 0414

        [Title("Observable")]
        [SerializeField] private TextMeshProUGUI _observableFloatText;
        [SerializeField] private ObservableFloat _observableFloat;



        private void OnEnable()
        {
            _observableFloat.AddEvent(OnObservableFloatChanged);
        }

        private void OnDisable()
        {
            _observableFloat.RemoveEvent(OnObservableFloatChanged);
        }

        private void OnObservableFloatChanged(float value)
        {
            _observableFloatText.text = $"{value:F3}";
        }
    }
}