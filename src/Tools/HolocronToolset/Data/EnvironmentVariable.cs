using System;

namespace HolocronToolset.Data
{
    /// <summary>
    /// Represents an environment variable with a key-value pair.
    /// Used for DataGrid binding in EnvVarsWidget.
    /// </summary>
    public class EnvironmentVariable
    {
        public string Key { get; set; }
        public string Value { get; set; }

        public EnvironmentVariable()
        {
            Key = "";
            Value = "";
        }

        public EnvironmentVariable(string key, string value)
        {
            Key = key ?? "";
            Value = value ?? "";
        }
    }
}

