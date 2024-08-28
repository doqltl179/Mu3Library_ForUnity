using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TestDrawClass : ISerializationCallbackReceiver {
    public string testString = "Test";
    public int testInt = 9999;
    public List<float> testFloatList = new List<float>();



    public void OnAfterDeserialize() {

    }

    public void OnBeforeSerialize() {

    }
}
