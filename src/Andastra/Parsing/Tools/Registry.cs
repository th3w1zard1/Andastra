using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Common.Logger;
using Andastra.Parsing.Extract.Installation;
using Microsoft.Win32;

namespace Andastra.Parsing.Tools
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py
    // Original: Windows registry paths and game Installation detection
    /// <summary>
    /// Windows registry paths and game Installation detection.
    /// </summary>
    public static class Registry
    {
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:32-62
        // Original: KOTOR_REG_PATHS: dict[Game, dict[ProcessorArchitecture, list[tuple[str, str]]]]
        private static readonly Dictionary<BioWareGame, Dictionary<ProcessorArchitecture, List<Tuple<string, string>>>> KotorRegPaths =
            new Dictionary<BioWareGame, Dictionary<ProcessorArchitecture, List<Tuple<string, string>>>>
            {
                {
                    BioWareGame.K1,
                    new Dictionary<ProcessorArchitecture, List<Tuple<string, string>>>
                    {
                        {
                            ProcessorArchitecture.BIT_32,
                            new List<Tuple<string, string>>
                            {
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 32370", "InstallLocation"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\GOG.com\Games\1207666283", "PATH"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\BioWare\SW\KOTOR", "InternalPath"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\BioWare\SW\KOTOR", "Path"),
                            }
                        },
                        {
                            ProcessorArchitecture.BIT_64,
                            new List<Tuple<string, string>>
                            {
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 32370", "InstallLocation"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\Games\1207666283", "PATH"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\BioWare\SW\KOTOR", "InternalPath"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\BioWare\SW\KOTOR", "Path"),
                            }
                        }
                    }
                },
                {
                    BioWareGame.K2,
                    new Dictionary<ProcessorArchitecture, List<Tuple<string, string>>>
                    {
                        {
                            ProcessorArchitecture.BIT_32,
                            new List<Tuple<string, string>>
                            {
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 208580", "InstallLocation"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\GOG.com\Games\1421404581", "PATH"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\LucasArts\KotOR2", "InternalPath"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\LucasArts\KotOR2", "Path"),
                            }
                        },
                        {
                            ProcessorArchitecture.BIT_64,
                            new List<Tuple<string, string>>
                            {
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 208580", "InstallLocation"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\GOG.com\Games\1421404581", "PATH"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\LucasArts\KotOR2", "InternalPath"),
                                Tuple.Create(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\LucasArts\KotOR2", "Path"),
                            }
                        }
                    }
                }
            };

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:66-83
        // Original: def find_software_key(software_name: str) -> str | None:
        /// <summary>
        /// Amazon's k1 reg key can be found using this code. Doesn't store it in HKLM for some reason.
        /// </summary>
        public static string FindSoftwareKey(string softwareName)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return null;
            }

            try
            {
                using (RegistryKey hkeyUsers = Microsoft.Win32.Registry.Users)
                {
                    int i = 0;
                    while (true)
                    {
                        try
                        {
                            string sid = hkeyUsers.GetSubKeyNames()[i];
                            string softwarePath = $"{sid}\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\{softwareName}";
                            using (RegistryKey softwareKey = hkeyUsers.OpenSubKey(softwarePath))
                            {
                                if (softwareKey != null)
                                {
                                    return softwareKey.GetValue("InstallLocation") as string;
                                }
                            }
                            i++;
                        }
                        catch (ArgumentException)
                        {
                            break; // No more left to iterate through
                        }
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:86-115
        // Original: def resolve_reg_key_to_path(registry: str | int | HKEYType, subkey: str, value_name: str | None = None) -> str | None:
        /// <summary>
        /// Resolve a registry key to a file system path.
        /// </summary>
        public static string ResolveRegKeyToPath(string registry, string subkey, string valueName = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return null;
            }

            try
            {
                RegistryKey rootKey;
                string keyPath;
                string valueToLookup;

                if (registry.Contains("\\"))
                {
                    string[] parts = registry.Split(new[] { '\\' }, 2);
                    string rootName = parts[0];
                    keyPath = parts[1];
                    rootKey = GetRegistryHive(rootName);
                    if (rootKey == null)
                    {
                        return null;
                    }
                    valueToLookup = subkey;
                }
                else
                {
                    throw new ArgumentException("When registry is a string, it must contain a backslash");
                }

                using (RegistryKey key = rootKey.OpenSubKey(keyPath))
                {
                    if (key == null)
                    {
                        return null;
                    }

                    string resolvedPath = key.GetValue(valueToLookup) as string;
                    return resolvedPath ?? "";
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static RegistryKey GetRegistryHive(string hiveName)
        {
            switch (hiveName)
            {
                case "HKEY_CLASSES_ROOT":
                    return Microsoft.Win32.Registry.ClassesRoot;
                case "HKEY_CURRENT_USER":
                    return Microsoft.Win32.Registry.CurrentUser;
                case "HKEY_LOCAL_MACHINE":
                    return Microsoft.Win32.Registry.LocalMachine;
                case "HKEY_USERS":
                    return Microsoft.Win32.Registry.Users;
                case "HKEY_CURRENT_CONFIG":
                    return Microsoft.Win32.Registry.CurrentConfig;
                default:
                    return null;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:149-170
        // Original: def winreg_key(game: Game) -> list[tuple[str, str]]:
        /// <summary>
        /// Returns a list of registry keys that are utilized by KOTOR.
        /// </summary>
        public static List<Tuple<string, string>> WinregKey(BioWareGame game)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new NotSupportedException("Cannot get or set registry keys on a non-Windows OS.");
            }

            ProcessorArchitecture arch = ProcessorArchitectureExtensions.FromOs();
            return KotorRegPaths[game][arch];
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:173-194
        // Original: def get_winreg_path(game: Game) -> tuple[Any, int] | None | Literal[""]:
        /// <summary>
        /// (untested) Returns the specified path value in the windows registry for the given game.
        /// </summary>
        public static Tuple<object, int> GetWinregPath(BioWareGame game)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new NotSupportedException("Cannot get or set registry keys on a non-Windows OS.");
            }

            List<Tuple<string, string>> possibleKotorRegPaths = WinregKey(game);

            try
            {
                foreach (Tuple<string, string> regPath in possibleKotorRegPaths)
                {
                    string keyPath = regPath.Item1;
                    string subkey = regPath.Item2;

                    string[] parts = keyPath.Split(new[] { '\\' }, 2);
                    RegistryKey rootKey = GetRegistryHive(parts[0]);
                    if (rootKey == null)
                    {
                        continue;
                    }

                    using (RegistryKey key = rootKey.OpenSubKey(parts[1], false))
                    {
                        if (key != null)
                        {
                            object value = key.GetValue(subkey);
                            if (value != null)
                            {
                                return Tuple.Create(value, 1); // REG_SZ = 1
                            }
                        }
                    }
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:197-224
        // Original: def set_winreg_path(game: Game, path: str) -> None:
        /// <summary>
        /// (untested) Sets the kotor install folder path value in the windows registry for the given game.
        /// </summary>
        public static void SetWinregPath(BioWareGame game, string path)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new NotSupportedException("Cannot get or set registry keys on a non-Windows OS.");
            }

            List<Tuple<string, string>> possibleKotorRegPaths = WinregKey(game);

            foreach (Tuple<string, string> regPath in possibleKotorRegPaths)
            {
                string keyPath = regPath.Item1;
                string subkey = regPath.Item2;

                string[] parts = keyPath.Split(new[] { '\\' }, 2);
                RegistryKey rootKey = GetRegistryHive(parts[0]);
                if (rootKey == null)
                {
                    continue;
                }

                using (RegistryKey key = rootKey.CreateSubKey(parts[1], true))
                {
                    if (key != null)
                    {
                        key.SetValue(subkey, path, RegistryValueKind.String);
                    }
                }
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:227-247
        // Original: def create_registry_path(hive: HKEYType | int, path: str) -> None:
        /// <summary>
        /// Recursively creates the registry path if it doesn't exist.
        /// </summary>
        public static void CreateRegistryPath(RegistryKey hive, string path)
        {
            RobustLogger log = new RobustLogger();
            try
            {
                string currentPath = "";
                string[] parts = path.Split('\\');
                foreach (string part in parts)
                {
                    currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}\\{part}";
                    try
                    {
                        hive.CreateSubKey(currentPath);
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        throw new UnauthorizedAccessException("Permission denied. Administrator privileges required.", e);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"Failed to create registry key: {currentPath}", e);
                    }
                }
            }
            catch (Exception)
            {
                log.Exception("An unexpected error occurred while creating a registry path.");
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:250-253
        // Original: def get_retail_key(game: Game) -> str:
        /// <summary>
        /// Gets the retail registry key path for the given game.
        /// </summary>
        public static string GetRetailKey(BioWareGame game)
        {
            ProcessorArchitecture arch = ProcessorArchitectureExtensions.FromOs();
            if (arch == ProcessorArchitecture.BIT_64)
            {
                return game.IsK2()
                    ? @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\LucasArts\KotOR2"
                    : @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\BioWare\SW\KOTOR";
            }
            return game.IsK2()
                ? @"HKEY_LOCAL_MACHINE\SOFTWARE\LucasArts\KotOR2"
                : @"HKEY_LOCAL_MACHINE\SOFTWARE\BioWare\SW\KOTOR";
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:300-350
        // Original: def set_registry_key_value(full_key_path: str, value_name: str, value_data: str) -> None:
        /// <summary>
        /// Sets a registry key value, creating the key (and its parents, if necessary).
        /// </summary>
        public static void SetRegistryKeyValue(string fullKeyPath, string valueName, string valueData)
        {
            RobustLogger log = new RobustLogger();
            try
            {
                string[] parts = fullKeyPath.Split(new[] { '\\' }, 2);
                if (parts.Length != 2)
                {
                    log.Error(string.Format("Invalid registry path '{0}'.", fullKeyPath));
                    return;
                }

                string hiveName = parts[0];
                string subKey = parts[1];
                RegistryKey hive = GetRegistryHive(hiveName);
                if (hive == null)
                {
                    log.Error(string.Format("Invalid registry hive '{0}'.", hiveName));
                    return;
                }

                try
                {
                    CreateRegistryPath(hive, subKey);
                }
                catch (UnauthorizedAccessException)
                {
                    throw;
                }
                catch (Exception)
                {
                    log.Exception("set_registry_key_value raised an error other than the expected PermissionError");
                    return;
                }

                using (RegistryKey key = hive.CreateSubKey(subKey, true))
                {
                    if (key != null)
                    {
                        key.SetValue(valueName, valueData, RegistryValueKind.String);
                        log.Debug(string.Format("Successfully set {0} to {1} at {2}\\{3}", valueName, valueData, hiveName, subKey));
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception)
            {
                log.Exception("An unexpected error occurred while setting the registry.");
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:353-369
        // Original: def remove_winreg_path(game: Game):
        /// <summary>
        /// (untested) Removes the registry path for the given game.
        /// </summary>
        public static void RemoveWinregPath(BioWareGame game)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            List<Tuple<string, string>> possibleKotorRegPaths = WinregKey(game);

            try
            {
                foreach (Tuple<string, string> regPath in possibleKotorRegPaths)
                {
                    string keyPath = regPath.Item1;
                    string subkey = regPath.Item2;

                    string[] parts = keyPath.Split(new[] { '\\' }, 2);
                    RegistryKey rootKey = GetRegistryHive(parts[0]);
                    if (rootKey == null)
                    {
                        continue;
                    }

                    using (RegistryKey key = rootKey.OpenSubKey(parts[1], true))
                    {
                        if (key != null)
                        {
                            key.DeleteValue(subkey, false);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }

    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:256-297
    // Original: class SpoofKotorRegistry:
    /// <summary>
    /// A context manager used to safely spoof the KOTOR 1/2 disk retail registry path temporarily.
    /// </summary>
    public class SpoofKotorRegistry : IDisposable
    {
        private readonly string _key;
        private readonly string _spoofedPath;
        private readonly string _registryPath;
        private readonly string _originalValue;
        private bool _wasModified;

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:259-281
        // Original: def __init__(self, installation_path: os.PathLike | str, game: Game | None = None):
        public SpoofKotorRegistry(string installationPath, BioWareGame? game = null)
        {
            _key = "Path";
            _spoofedPath = Path.GetFullPath(installationPath);

            BioWareGame determinedGame;
            if (game.HasValue)
            {
                determinedGame = game.Value;
            }
            else
            {
                BioWareGame? determinedGameNullable = Installation.DetermineGame(installationPath);
                if (determinedGameNullable == null)
                {
                    throw new ArgumentException($"Could not auto-determine the game k1 or k2 from '{installationPath}'. Try sending 'game' enum to prevent auto-detections like this.");
                }
                determinedGame = determinedGameNullable.Value;
            }

            _registryPath = Registry.GetRetailKey(determinedGame);
            _originalValue = Registry.ResolveRegKeyToPath(_registryPath, _key);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:283-286
        // Original: def __enter__(self) -> Self:
        public void Activate()
        {
            if (_spoofedPath != _originalValue)
            {
                Registry.SetRegistryKeyValue(_registryPath, _key, _spoofedPath);
                _wasModified = true;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py:288-297
        // Original: def __exit__(self, exc_type, exc_val, exc_tb):
        public void Dispose()
        {
            if (_wasModified)
            {
                if (_originalValue != null && _spoofedPath != _originalValue)
                {
                    // Restore original value
                    Registry.SetRegistryKeyValue(_registryPath, _key, _originalValue);
                }
                else if (_originalValue == null)
                {
                    // Registry key didn't exist originally - set to empty string to clean up
                    // Note: Deleting registry keys is complex and risky, so we set to empty string instead
                    Registry.SetRegistryKeyValue(_registryPath, _key, "");
                }
            }
        }
    }

    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py (ProcessorArchitecture enum)
    /// <summary>
    /// Processor architecture enumeration.
    /// </summary>
    public enum ProcessorArchitecture
    {
        BIT_32,
        BIT_64
    }

    /// <summary>
    /// Extension methods for ProcessorArchitecture.
    /// </summary>
    public static class ProcessorArchitectureExtensions
    {
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tools/registry.py
        // Original: ProcessorArchitecture.from_os()
        /// <summary>
        /// Gets the processor architecture from the operating system.
        /// </summary>
        public static ProcessorArchitecture FromOs()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Environment.Is64BitOperatingSystem
                ? ProcessorArchitecture.BIT_64
                : ProcessorArchitecture.BIT_32;
        }
    }
}
