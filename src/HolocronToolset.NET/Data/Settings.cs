using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HolocronToolset.NET.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/settings.py:160
    // Original: class Settings:
    public class Settings
    {
        private readonly string _scope;
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HolocronToolsetV3");

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/settings.py:161-165
        // Original: def __init__(self, scope: str): self.settings: QSettings = QSettings("HolocronToolsetV3", scope)
        public Settings(string scope)
        {
            _scope = scope;
            Load();
        }

        private string GetSettingsFilePath()
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }
            return Path.Combine(SettingsDirectory, $"{_scope}.json");
        }

        private void Load()
        {
            string filePath = GetSettingsFilePath();
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    if (loaded != null)
                    {
                        foreach (var kvp in loaded)
                        {
                            _values[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch
                {
                    // If loading fails, use defaults
                }
            }
        }

        public void Save()
        {
            try
            {
                string filePath = GetSettingsFilePath();
                string json = JsonConvert.SerializeObject(_values, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }

        public T GetValue<T>(string name, T defaultValue)
        {
            if (_values.TryGetValue(name, out object value))
            {
                try
                {
                    if (value is JToken token)
                    {
                        return token.ToObject<T>();
                    }
                    if (value is T)
                    {
                        return (T)value;
                    }
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public void SetValue<T>(string name, T value)
        {
            _values[name] = value;
            Save();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/settings.py:174-181
        // Original: def get_property(self, name: str) -> SettingsProperty[T]:
        public SettingsProperty<T> GetProperty<T>(string name)
        {
            var prop = GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.GetValue(this) is SettingsProperty<T> settingsProp)
            {
                return settingsProp;
            }
            throw new ArgumentException($"'{GetType().Name}' object has no property '{name}'");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/settings.py:183-188
        // Original: def get_default(self, name: str) -> Any:
        public object GetDefault(string name)
        {
            var prop = GetProperty<object>(name);
            return prop.Default;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/settings.py:190-195
        // Original: def reset_setting(self, name: str):
        public void ResetSetting(string name)
        {
            var prop = GetProperty<object>(name);
            prop.ResetToDefault(this);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:104
        // Original: GlobalSettings().settings.clear()
        public void Clear()
        {
            _values.Clear();
            Save();
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/settings.py:28-157
    // Original: class SettingsProperty(property, Generic[T]):
    public class SettingsProperty<T>
    {
        public string Name { get; }
        public T Default { get; }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/settings.py:29-44
        // Original: def __init__(self, name: str, default: Any):
        public SettingsProperty(string name, T defaultValue)
        {
            Name = name;
            Default = defaultValue;
        }

        public T GetValue(Settings settings)
        {
            return settings.GetValue(Name, Default);
        }

        public void SetValue(Settings settings, T value)
        {
            settings.SetValue(Name, value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/settings.py:92-103
        // Original: def reset_to_default(self, instance: Settings):
        public void ResetToDefault(Settings settings)
        {
            settings.SetValue(Name, Default);
        }
    }
}
