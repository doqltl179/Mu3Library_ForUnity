using Mu3Library.Attribute;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Demo.Attribute {
    public class SampleSceneAttributeSample : MonoBehaviour {
        [Title("Title Attribute")]
        [SerializeField] private int testTitleProperty = 0;
    }
}