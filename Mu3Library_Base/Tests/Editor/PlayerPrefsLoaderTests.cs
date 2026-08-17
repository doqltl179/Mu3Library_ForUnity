using System;
using Mu3Library.Preference;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    public class PlayerPrefsLoaderTests
    {
        private const string KeyPrefix = "Mu3Library.Tests.";

        [Serializable]
        private class Payload
        {
            public int Number;
            public string Text;
        }

        private enum SampleEnum
        {
            First = 0,
            Second = 1,
        }

        private PlayerPrefsLoader _loader;



        [SetUp]
        public void SetUp()
        {
            _loader = new PlayerPrefsLoader();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string suffix in new[] { "bool", "enum", "json", "int" })
            {
                _loader.ClearPref(KeyPrefix + suffix);
            }

            _loader.ClearAllDefaults();
        }

        [Test]
        public void Bool_Roundtrip()
        {
            _loader.SetBool(KeyPrefix + "bool", true);

            Assert.IsTrue(_loader.GetBool(KeyPrefix + "bool"));
        }

        [Test]
        public void Enum_Roundtrip()
        {
            _loader.SetEnum(KeyPrefix + "enum", SampleEnum.Second);

            Assert.AreEqual(SampleEnum.Second, _loader.GetEnum<SampleEnum>(KeyPrefix + "enum"));
        }

        [Test]
        public void Enum_MissingKey_ReturnsDefault()
        {
            Assert.AreEqual(SampleEnum.First, _loader.GetEnum<SampleEnum>(KeyPrefix + "enum"));
        }

        [Test]
        public void Json_Roundtrip()
        {
            Payload payload = new() { Number = 42, Text = "hello" };
            _loader.SetJson(KeyPrefix + "json", payload);

            Payload loaded = _loader.GetJson<Payload>(KeyPrefix + "json");

            Assert.IsNotNull(loaded);
            Assert.AreEqual(42, loaded.Number);
            Assert.AreEqual("hello", loaded.Text);
        }

        [Test]
        public void Json_MissingKey_ReturnsDefault()
        {
            Assert.IsNull(_loader.GetJson<Payload>(KeyPrefix + "json"));
        }

        [Test]
        public void DefaultInt_AnswersWhileKeyMissing()
        {
            _loader.SetDefaultInt(KeyPrefix + "int", 7);

            Assert.AreEqual(7, _loader.GetInt(KeyPrefix + "int"));

            _loader.SetInt(KeyPrefix + "int", 9);

            Assert.AreEqual(9, _loader.GetInt(KeyPrefix + "int"));
        }

        [Test]
        public void DefaultBool_AnswersWhileKeyMissing()
        {
            _loader.SetDefaultBool(KeyPrefix + "bool", true);

            Assert.IsTrue(_loader.GetBool(KeyPrefix + "bool"));
        }
    }
}
