using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.Preference
{
    public class PlayerPrefsLoader : IPlayerPrefsLoader
    {
        private readonly Dictionary<string, int> _defaultInts = new();
        private readonly Dictionary<string, float> _defaultFloats = new();
        private readonly Dictionary<string, string> _defaultStrings = new();

        public IEnumerable<string> Keys => Enumerable.Empty<string>()
            .Concat(_defaultInts.Keys)
            .Concat(_defaultFloats.Keys)
            .Concat(_defaultStrings.Keys)
            .Distinct();



        public bool HasKey(string key)
        {
            return PlayerPrefs.HasKey(key);
        }

        public void SetInt(string key, int value, bool saveImmediately = false)
        {
            PlayerPrefs.SetInt(key, value);

            if (saveImmediately)
            {
                PlayerPrefs.Save();
            }
        }

        public void SetFloat(string key, float value, bool saveImmediately = false)
        {
            PlayerPrefs.SetFloat(key, value);

            if (saveImmediately)
            {
                PlayerPrefs.Save();
            }
        }

        public void SetString(string key, string value, bool saveImmediately = false)
        {
            PlayerPrefs.SetString(key, value);

            if (saveImmediately)
            {
                PlayerPrefs.Save();
            }
        }

        public int GetInt(string key)
        {
            return PlayerPrefs.GetInt(key, _defaultInts.GetValueOrDefault(key));
        }

        public float GetFloat(string key)
        {
            return PlayerPrefs.GetFloat(key, _defaultFloats.GetValueOrDefault(key));
        }

        public string GetString(string key)
        {
            return PlayerPrefs.GetString(key, _defaultStrings.GetValueOrDefault(key));
        }

        public void SetDefaultInt(string key, int defaultValue)
        {
            _defaultInts[key] = defaultValue;
        }

        public void SetDefaultFloat(string key, float defaultValue)
        {
            _defaultFloats[key] = defaultValue;
        }

        public void SetDefaultString(string key, string defaultValue)
        {
            _defaultStrings[key] = defaultValue;
        }

        public void ClearDefaults(string key)
        {
            _defaultInts.Remove(key);
            _defaultFloats.Remove(key);
            _defaultStrings.Remove(key);
        }

        public void ClearAllDefaults()
        {
            _defaultInts.Clear();
            _defaultFloats.Clear();
            _defaultStrings.Clear();
        }

        public void ClearPref(string key)
        {
            PlayerPrefs.DeleteKey(key);
        }

        public void ClearAllPrefs()
        {
            PlayerPrefs.DeleteAll();
        }

        public void Save()
        {
            PlayerPrefs.Save();
        }
    }
}