using System;
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
            SaveIfRequested(saveImmediately);
        }

        public void SetFloat(string key, float value, bool saveImmediately = false)
        {
            PlayerPrefs.SetFloat(key, value);
            SaveIfRequested(saveImmediately);
        }

        public void SetString(string key, string value, bool saveImmediately = false)
        {
            PlayerPrefs.SetString(key, value);
            SaveIfRequested(saveImmediately);
        }

        /// <summary>
        /// Stores a bool as 0/1 through the int slot, which PlayerPrefs itself has no type for.
        /// </summary>
        public void SetBool(string key, bool value, bool saveImmediately = false)
        {
            SetInt(key, value ? 1 : 0, saveImmediately);
        }

        /// <summary>
        /// Stores an enum by its name, so a reordered enum keeps its saved meaning.
        /// </summary>
        public void SetEnum<TEnum>(string key, TEnum value, bool saveImmediately = false)
            where TEnum : struct, Enum
        {
            SetString(key, value.ToString(), saveImmediately);
        }

        /// <summary>
        /// Stores an object as JSON through <see cref="JsonUtility"/>, so it follows that
        /// serializer's rules: a serializable class or struct, not a bare collection.
        /// </summary>
        public void SetJson<T>(string key, T value, bool saveImmediately = false)
        {
            SetString(key, value != null ? JsonUtility.ToJson(value) : string.Empty, saveImmediately);
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

        public bool GetBool(string key)
        {
            return GetInt(key) != 0;
        }

        public TEnum GetEnum<TEnum>(string key)
            where TEnum : struct, Enum
        {
            string stored = GetString(key);
            if (!string.IsNullOrEmpty(stored) && Enum.TryParse(stored, out TEnum value))
            {
                return value;
            }

            return default;
        }

        public T GetJson<T>(string key)
        {
            string stored = GetString(key);
            if (string.IsNullOrEmpty(stored))
            {
                return default;
            }

            try
            {
                return JsonUtility.FromJson<T>(stored);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return default;
            }
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

        public void SetDefaultBool(string key, bool defaultValue)
        {
            SetDefaultInt(key, defaultValue ? 1 : 0);
        }

        public void SetDefaultEnum<TEnum>(string key, TEnum defaultValue)
            where TEnum : struct, Enum
        {
            SetDefaultString(key, defaultValue.ToString());
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

        private static void SaveIfRequested(bool saveImmediately)
        {
            if (saveImmediately)
            {
                PlayerPrefs.Save();
            }
        }
    }
}
