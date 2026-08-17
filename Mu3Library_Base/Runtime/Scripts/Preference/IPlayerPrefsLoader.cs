using System.Collections.Generic;

namespace Mu3Library.Preference
{
    public interface IPlayerPrefsLoader
    {
        public IEnumerable<string> Keys { get; }



        public bool HasKey(string key);

        public void SetInt(string key, int value, bool saveImmediately = false);
        public void SetFloat(string key, float value, bool saveImmediately = false);
        public void SetString(string key, string value, bool saveImmediately = false);
        public void SetBool(string key, bool value, bool saveImmediately = false);
        public void SetEnum<TEnum>(string key, TEnum value, bool saveImmediately = false) where TEnum : struct, System.Enum;
        public void SetJson<T>(string key, T value, bool saveImmediately = false);

        public int GetInt(string key);
        public float GetFloat(string key);
        public string GetString(string key);
        public bool GetBool(string key);
        public TEnum GetEnum<TEnum>(string key) where TEnum : struct, System.Enum;
        public T GetJson<T>(string key);

        public void SetDefaultInt(string key, int defaultValue);
        public void SetDefaultFloat(string key, float defaultValue);
        public void SetDefaultString(string key, string defaultValue);
        public void SetDefaultBool(string key, bool defaultValue);
        public void SetDefaultEnum<TEnum>(string key, TEnum defaultValue) where TEnum : struct, System.Enum;

        public void ClearDefaults(string key);
        public void ClearAllDefaults();

        public void ClearPref(string key);
        public void ClearAllPrefs();

        public void Save();
    }
}