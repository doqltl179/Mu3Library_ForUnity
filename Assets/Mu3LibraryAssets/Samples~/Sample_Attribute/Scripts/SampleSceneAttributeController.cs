using Mu3Library.Attribute;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Sample.Attribute {
    public class SampleSceneAttributeSample : MonoBehaviour {
        [Title("Title Attribute")]
        [SerializeField] private int testTitleProperty = 0;

        [Title("Conditional Hide Attribute")]
        [SerializeField] private bool testConditionProperty = false;
        [ConditionalHide(nameof(testConditionProperty), true)]
        [SerializeField] private string testConditionHideProperty = "";
    }
}