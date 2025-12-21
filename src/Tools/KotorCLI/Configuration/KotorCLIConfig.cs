using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Model;
using Environment = System.Environment;

namespace KotorCLI.Configuration
{
    /// <summary>
    /// Represents a kotorcli.cfg configuration file (TOML format, cli-compatible).
    /// </summary>
    public class KotorCLIConfig
    {
        private readonly TomlTable _data;
        public string ConfigPath { get; }
        public string RootDir { get; }

        public KotorCLIConfig(string configPath)
        {
            ConfigPath = configPath;
            RootDir = Path.GetDirectoryName(configPath);

            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Configuration file not found: {configPath}");
            }

            string content = File.ReadAllText(configPath, Encoding.UTF8);
            _data = Toml.ToModel(content);
        }

        /// <summary>
        /// Get the package configuration.
        /// </summary>
        public TomlTable GetPackage()
        {
            if (_data.TryGetValue("package", out object packageObj) && packageObj is TomlTable package)
            {
                return package;
            }
            return new TomlTable();
        }

        /// <summary>
        /// Get all target configurations.
        /// </summary>
        public List<TomlTable> GetTargets()
        {
            var result = new List<TomlTable>();
            
            foreach (var kvp in _data)
            {
                if (kvp.Key == "target" || (kvp.Value is TomlTable table && table.ContainsKey("name")))
                {
                    if (kvp.Value is TomlTable target)
                    {
                        result.Add(target);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get a target by name, or the default target if name is null.
        /// </summary>
        public TomlTable GetTarget(string name = null)
        {
            var targets = GetTargets();
            if (targets.Count == 0)
            {
                return null;
            }

            if (name == null)
            {
                // Return first target (default) or target marked as default
                var package = GetPackage();
                if (package.TryGetValue("default", out object defaultNameObj) && defaultNameObj is string defaultName)
                {
                    foreach (var target in targets)
                    {
                        if (target.TryGetValue("name", out object targetNameObj) && targetNameObj is string targetName && targetName == defaultName)
                        {
                            return target;
                        }
                    }
                }
                return targets[0];
            }

            // Find target by name
            foreach (var target in targets)
            {
                if (target.TryGetValue("name", out object targetNameObj) && targetNameObj is string targetName && targetName == name)
                {
                    return target;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolve a target value, inheriting from parent or package if needed.
        /// </summary>
        public object ResolveTargetValue(TomlTable target, string key, object defaultValue = null)
        {
            // Check target first
            if (target.TryGetValue(key, out object value))
            {
                if (value is string strValue)
                {
                    var targetVars = new Dictionary<string, string>();
                    if (target.TryGetValue("variables", out object varsObj) && varsObj is TomlTable varsTable)
                    {
                        foreach (var varKvp in varsTable)
                        {
                            if (varKvp.Value is string varValue)
                            {
                                targetVars[varKvp.Key] = varValue;
                            }
                        }
                    }
                    string targetName = null;
                    if (target.TryGetValue("name", out object nameObj) && nameObj is string name)
                    {
                        targetName = name;
                    }
                    return ExpandVariables(strValue, targetName, targetVars);
                }
                return value;
            }

            // Check parent target
            if (target.TryGetValue("parent", out object parentNameObj) && parentNameObj is string parentName)
            {
                var parent = GetTarget(parentName);
                if (parent != null && parent.TryGetValue(key, out object parentValue))
                {
                    if (parentValue is string strParentValue)
                    {
                        var parentVars = new Dictionary<string, string>();
                        if (parent.TryGetValue("variables", out object parentVarsObj) && parentVarsObj is TomlTable parentVarsTable)
                        {
                            foreach (var varKvp in parentVarsTable)
                            {
                                if (varKvp.Value is string varValue)
                                {
                                    parentVars[varKvp.Key] = varValue;
                                }
                            }
                        }
                        string parentTargetName = null;
                        if (parent.TryGetValue("name", out object parentNameObj2) && parentNameObj2 is string parentName2)
                        {
                            parentTargetName = parentName2;
                        }
                        return ExpandVariables(strParentValue, parentTargetName, parentVars);
                    }
                    return parentValue;
                }
            }

            // Check package
            var package = GetPackage();
            if (package.TryGetValue(key, out object packageValue))
            {
                if (packageValue is string strPackageValue)
                {
                    return ExpandVariables(strPackageValue, target.TryGetValue("name", out object tname) ? tname as string : null);
                }
                return packageValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Get source patterns for a target.
        /// </summary>
        public Dictionary<string, List<string>> GetTargetSources(TomlTable target)
        {
            var sources = new Dictionary<string, List<string>>();
            
            TomlTable sourceTable = null;
            if (target.TryGetValue("sources", out object targetSourcesObj) && targetSourcesObj is TomlTable targetSources)
            {
                sourceTable = targetSources;
            }
            else
            {
                var package = GetPackage();
                if (package.TryGetValue("sources", out object packageSourcesObj) && packageSourcesObj is TomlTable packageSources)
                {
                    sourceTable = packageSources;
                }
            }

            if (sourceTable != null)
            {
                sources["include"] = GetStringList(sourceTable, "include");
                sources["exclude"] = GetStringList(sourceTable, "exclude");
                sources["filter"] = GetStringList(sourceTable, "filter");
                sources["skipCompile"] = GetStringList(sourceTable, "skipCompile");
            }
            else
            {
                sources["include"] = new List<string>();
                sources["exclude"] = new List<string>();
                sources["filter"] = new List<string>();
                sources["skipCompile"] = new List<string>();
            }

            return sources;
        }

        /// <summary>
        /// Get unpack rules for a target.
        /// </summary>
        public Dictionary<string, string> GetTargetRules(TomlTable target)
        {
            var rules = new Dictionary<string, string>();
            
            TomlTable rulesTable = null;
            if (target.TryGetValue("rules", out object targetRulesObj) && targetRulesObj is TomlTable targetRules)
            {
                rulesTable = targetRules;
            }
            else
            {
                var package = GetPackage();
                if (package.TryGetValue("rules", out object packageRulesObj) && packageRulesObj is TomlTable packageRules)
                {
                    rulesTable = packageRules;
                }
            }

            if (rulesTable != null)
            {
                foreach (var kvp in rulesTable)
                {
                    if (kvp.Value is string value)
                    {
                        rules[kvp.Key] = value;
                    }
                }
            }

            return rules;
        }

        private List<string> GetStringList(TomlTable table, string key)
        {
            var result = new List<string>();
            if (table.TryGetValue(key, out object value))
            {
                if (value is TomlArray array)
                {
                    foreach (var item in array)
                    {
                        if (item is string str)
                        {
                            result.Add(str);
                        }
                    }
                }
                else if (value is string str)
                {
                    result.Add(str);
                }
            }
            return result;
        }

        /// <summary>
        /// Expand variables in a string value.
        /// Variables can be in the form $variable or ${variable}.
        /// Checks package variables, target variables, and environment variables.
        /// </summary>
        private string ExpandVariables(string value, string targetName = null, Dictionary<string, string> variables = null)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var varDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Add package variables
            var package = GetPackage();
            if (package.TryGetValue("variables", out object packageVarsObj) && packageVarsObj is TomlTable packageVars)
            {
                foreach (var kvp in packageVars)
                {
                    if (kvp.Value is string varValue)
                    {
                        varDict[kvp.Key] = varValue;
                    }
                }
            }

            // Add target-specific variables
            if (variables != null)
            {
                foreach (var kvp in variables)
                {
                    varDict[kvp.Key] = kvp.Value;
                }
            }

            // Add special $target variable
            if (targetName != null)
            {
                varDict["target"] = targetName;
            }

            // Add environment variables
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                varDict[entry.Key.ToString()] = entry.Value.ToString();
            }

            // Expand ${var} and $var patterns
            string result = Regex.Replace(value, @"\$\{(\w+)\}", match =>
            {
                string varName = match.Groups[1].Value;
                return varDict.TryGetValue(varName, out string varValue) ? varValue : match.Value;
            });

            result = Regex.Replace(result, @"\$(\w+)", match =>
            {
                string varName = match.Groups[1].Value;
                return varDict.TryGetValue(varName, out string varValue) ? varValue : match.Value;
            });

            return result;
        }
    }
}

