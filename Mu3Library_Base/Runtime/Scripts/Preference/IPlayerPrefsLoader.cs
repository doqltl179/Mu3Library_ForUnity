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

        public int GetInt(string key);
        public float GetFloat(string key);
        public string GetString(string key);

        public void SetDefaultInt(string key, int defaultValue);
        public void SetDefaultFloat(string key, float defaultValue);
        public void SetDefaultString(string key, string defaultValue);

        public void ClearDefaults(string key);
        public void ClearAllDefaults();

        public void ClearPref(string key);
        public void ClearAllPrefs();

        public void Save();
    }
}