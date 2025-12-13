// Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:1-781
// Copyright 2021-2025 NCSDecomp
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:36-780
    // Original: public class RegistrySpoofer implements AutoCloseable
    /// <summary>
    /// Temporarily spoofs Windows registry keys to point legacy compilers to the correct installation path.
    /// Some legacy nwnnsscomp variants (KOTOR Tool, KOTOR Scripting Tool) read the game installation
    /// path from the Windows registry instead of accepting command-line arguments.
    /// </summary>
    public class RegistrySpoofer : IDisposable
    {
        private readonly string registryPath;
        private readonly string keyName;
        private readonly string spoofedPath;
        private string originalValue;
        private bool wasModified = false;
        private static readonly string DONT_SHOW_INFO_MARKER_FILE = "ncsdecomp_registry_info_dont_show.txt";

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:51-80
        // Original: public RegistrySpoofer(File installationPath, boolean isK2)
        public RegistrySpoofer(File installationPath, bool isK2)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new NotSupportedException("Registry spoofing is only supported on Windows");
            }

            this.spoofedPath = installationPath.GetAbsolutePath();
            this.keyName = "Path";

            // Determine registry path based on game and architecture
            bool is64Bit = Is64BitArchitecture();
            if (isK2)
            {
                if (is64Bit)
                {
                    this.registryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\LucasArts\KotOR2";
                }
                else
                {
                    this.registryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\LucasArts\KotOR2";
                }
            }
            else
            {
                if (is64Bit)
                {
                    this.registryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\BioWare\SW\KOTOR";
                }
                else
                {
                    this.registryPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\BioWare\SW\KOTOR";
                }
            }

            // Read original value
            this.originalValue = ReadRegistryValue(this.registryPath, this.keyName);
            JavaSystem.@out.Println("[INFO] RegistrySpoofer: Created spoofer for " + (isK2 ? "K2" : "K1") +
                ", registryPath=" + this.registryPath + ", spoofedPath=" + this.spoofedPath +
                ", originalValue=" + (this.originalValue != null ? this.originalValue : "(null)"));
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:95-161
        // Original: public RegistrySpoofer activate()
        public RegistrySpoofer Activate()
        {
            // Log current registry state BEFORE attempting change
            string currentValue = ReadRegistryValue(this.registryPath, this.keyName);
            JavaSystem.@out.Println("[INFO] RegistrySpoofer: BEFORE activation - registry key " + this.registryPath +
                "\\" + this.keyName + " = " + (currentValue != null ? currentValue : "(null/not set)"));

            // ALWAYS create required file structure (chitin.key, override/, etc.) - even if registry already matches
            // This ensures the files exist and are valid (previous runs may have left corrupt files)
            CreateRequiredFileStructure(this.spoofedPath);

            if (this.originalValue != null && this.originalValue.Equals(this.spoofedPath))
            {
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Registry value already matches spoofed path, skipping registry write");
                // File structure already created above, we're done
                return this;
            }

            try
            {
                WriteRegistryValue(this.registryPath, this.keyName, this.spoofedPath);

                // Verify the registry was actually set
                string verifyValue = ReadRegistryValue(this.registryPath, this.keyName);
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: AFTER write - registry key " + this.registryPath +
                    "\\" + this.keyName + " = " + (verifyValue != null ? verifyValue : "(null/not set)"));

                if (verifyValue != null && verifyValue.Equals(this.spoofedPath))
                {
                    this.wasModified = true;
                    JavaSystem.@out.Println("[INFO] RegistrySpoofer: Successfully set and verified registry key " + this.registryPath +
                        "\\" + this.keyName + " to " + this.spoofedPath);
                }
                else
                {
                    JavaSystem.@out.Println("[INFO] RegistrySpoofer: WARNING - Registry write appeared to succeed but verification failed! " +
                        "Expected: " + this.spoofedPath + ", Got: " + verifyValue);
                }
            }
            catch (SecurityException e)
            {
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Failed to set registry key: " + e.Message);

                // ALWAYS attempt elevation when we see the NwnStdLoader error (it's REQUIRED)
                // The elevation prompt should always be shown
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Attempting elevated registry write (REQUIRED for NwnStdLoader error)...");
                if (AttemptElevatedRegistryWrite())
                {
                    // Verify the registry was actually set after elevation
                    string verifyValue = ReadRegistryValue(this.registryPath, this.keyName);
                    JavaSystem.@out.Println("[INFO] RegistrySpoofer: AFTER elevation - registry key " + this.registryPath +
                        "\\" + this.keyName + " = " + (verifyValue != null ? verifyValue : "(null/not set)"));

                    if (verifyValue != null && verifyValue.Equals(this.spoofedPath))
                    {
                        // Successfully set via elevation
                        this.wasModified = true;
                        JavaSystem.@out.Println("[INFO] RegistrySpoofer: Successfully set and verified registry key via elevation");
                        // File structure already created at the start of activate()
                        return this;
                    }
                    else
                    {
                        JavaSystem.@out.Println("[INFO] RegistrySpoofer: WARNING - Elevation appeared to succeed but registry verification failed! " +
                            "Expected: " + this.spoofedPath + ", Got: " + verifyValue);
                        // Show informational message after verification failure
                        ShowSubsequentElevationMessage();
                        throw new SecurityException("Permission denied. Registry elevation succeeded but verification failed. " +
                            "Expected: " + this.spoofedPath + ", Got: " + verifyValue, e);
                    }
                }
                else
                {
                    // User refused or elevation failed - show informational message with "don't show again" option
                    ShowSubsequentElevationMessage();
                    throw new SecurityException("Permission denied. Administrator privileges required to spoof registry. " +
                        "Error: " + e.Message, e);
                }
            }
            return this;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:168-198
        // Original: public void close()
        public void Dispose()
        {
            if (!this.wasModified)
            {
                // Nothing was changed, nothing to restore
                return;
            }

            try
            {
                if (this.originalValue == null)
                {
                    // Original value didn't exist - delete the registry key we created
                    DeleteRegistryValue(this.registryPath, this.keyName);
                    JavaSystem.@out.Println("[INFO] RegistrySpoofer: Deleted registry key " + this.registryPath +
                        "\\" + this.keyName + " (it didn't exist originally)");
                }
                else if (!this.originalValue.Equals(this.spoofedPath))
                {
                    // Restore to original value
                    WriteRegistryValue(this.registryPath, this.keyName, this.originalValue);
                    JavaSystem.@out.Println("[INFO] RegistrySpoofer: Restored registry key " + this.registryPath +
                        "\\" + this.keyName + " to original value: " + this.originalValue);
                }
                else
                {
                    // Original value was the same as spoofed path, so no change needed
                    JavaSystem.@out.Println("[INFO] RegistrySpoofer: Registry key already matches original value, no restoration needed");
                }
            }
            catch (Exception e)
            {
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Failed to restore registry key: " + e.Message);
                e.PrintStackTrace();
                // Don't throw - we've done our best to restore, but log the error
            }
            finally
            {
                // Always reset the flag so we don't try again
                this.wasModified = false;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:200-212
        // Original: public String getRegistryPath()
        public string GetRegistryPath()
        {
            return registryPath;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:207-212
        // Original: public String getOriginalValue()
        public string GetOriginalValue()
        {
            return originalValue;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:217-220
        // Original: private static boolean is64BitArchitecture()
        private static bool Is64BitArchitecture()
        {
            return Environment.Is64BitOperatingSystem;
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:229-302
        // Original: private static String readRegistryValue(String registryPath, String keyName)
        private static string ReadRegistryValue(string registryPath, string keyName)
        {
            try
            {
                // Parse registry path
                int firstBackslash = registryPath.IndexOf('\\');
                if (firstBackslash < 0)
                {
                    return null;
                }
                string hive = registryPath.Substring(0, firstBackslash);
                string keyPath = registryPath.Substring(firstBackslash + 1);

                // Convert hive name to reg.exe format
                string regHive;
                if (hive.Equals("HKEY_LOCAL_MACHINE"))
                {
                    regHive = "HKLM";
                }
                else if (hive.Equals("HKEY_CURRENT_USER"))
                {
                    regHive = "HKCU";
                }
                else if (hive.Equals("HKEY_CLASSES_ROOT"))
                {
                    regHive = "HKCR";
                }
                else if (hive.Equals("HKEY_USERS"))
                {
                    regHive = "HKU";
                }
                else if (hive.Equals("HKEY_CURRENT_CONFIG"))
                {
                    regHive = "HKCC";
                }
                else
                {
                    return null;
                }

                // Use reg.exe query to read the value
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = "query \"" + regHive + "\\" + keyPath + "\" /v \"" + keyName + "\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        return null;
                    }

                    // Read output
                    StringBuilder output = new StringBuilder();
                    using (var reader = proc.StandardOutput)
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            output.Append(line).Append("\n");
                        }
                    }

                    // Read error output
                    StringBuilder errorOutput = new StringBuilder();
                    using (var reader = proc.StandardError)
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            errorOutput.Append(line).Append("\n");
                        }
                    }

                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        // Key doesn't exist or access denied
                        return null;
                    }

                    // Parse output: "    Path    REG_SZ    C:\path\to\install"
                    string outputStr = output.ToString();
                    string[] lines = outputStr.Split('\n');
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith(keyName))
                        {
                            // Find the value (third column)
                            string[] parts = trimmed.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 3)
                            {
                                return parts[2].Trim();
                            }
                        }
                    }
                    return null;
                }
            }
            catch (Exception e)
            {
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Error reading registry value: " + e.Message);
                return null;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:311-371
        // Original: private static void deleteRegistryValue(String registryPath, String keyName)
        private static void DeleteRegistryValue(string registryPath, string keyName)
        {
            try
            {
                // Parse registry path
                int firstBackslash = registryPath.IndexOf('\\');
                if (firstBackslash < 0)
                {
                    throw new ArgumentException("Invalid registry path: " + registryPath);
                }
                string hive = registryPath.Substring(0, firstBackslash);
                string keyPath = registryPath.Substring(firstBackslash + 1);

                // Convert hive name to reg.exe format
                string regHive;
                if (hive.Equals("HKEY_LOCAL_MACHINE"))
                {
                    regHive = "HKLM";
                }
                else if (hive.Equals("HKEY_CURRENT_USER"))
                {
                    regHive = "HKCU";
                }
                else if (hive.Equals("HKEY_CLASSES_ROOT"))
                {
                    regHive = "HKCR";
                }
                else if (hive.Equals("HKEY_USERS"))
                {
                    regHive = "HKU";
                }
                else if (hive.Equals("HKEY_CURRENT_CONFIG"))
                {
                    regHive = "HKCC";
                }
                else
                {
                    throw new ArgumentException("Unsupported registry hive: " + hive);
                }

                // Delete the registry value
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = "delete \"" + regHive + "\\" + keyPath + "\" /v \"" + keyName + "\" /f",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        throw new InvalidOperationException("Failed to start reg.exe process");
                    }

                    // Read output
                    StringBuilder output = new StringBuilder();
                    using (var reader = proc.StandardOutput)
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            output.Append(line).Append("\n");
                        }
                    }

                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        string errorMsg = output.ToString();
                        if (errorMsg.Contains("access") || errorMsg.Contains("denied") || errorMsg.Contains("privilege"))
                        {
                            throw new SecurityException("Access denied. Administrator privileges required. " + errorMsg);
                        }
                        // If key doesn't exist, that's okay - it means it's already deleted
                        if (!errorMsg.ToLower().Contains("the system cannot find"))
                        {
                            throw new InvalidOperationException("Failed to delete registry value. Exit code: " + proc.ExitCode + ", Error: " + errorMsg);
                        }
                    }
                }
            }
            catch (SecurityException)
            {
                throw;
            }
            catch (Exception e)
            {
                if (e is SecurityException)
                {
                    throw;
                }
                throw new InvalidOperationException("Error deleting registry value: " + e.Message, e);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:381-444
        // Original: private static void writeRegistryValue(String registryPath, String keyName, String value)
        private static void WriteRegistryValue(string registryPath, string keyName, string value)
        {
            try
            {
                // Parse registry path
                int firstBackslash = registryPath.IndexOf('\\');
                if (firstBackslash < 0)
                {
                    throw new ArgumentException("Invalid registry path: " + registryPath);
                }
                string hive = registryPath.Substring(0, firstBackslash);
                string keyPath = registryPath.Substring(firstBackslash + 1);

                // Convert hive name to reg.exe format
                string regHive;
                if (hive.Equals("HKEY_LOCAL_MACHINE"))
                {
                    regHive = "HKLM";
                }
                else if (hive.Equals("HKEY_CURRENT_USER"))
                {
                    regHive = "HKCU";
                }
                else if (hive.Equals("HKEY_CLASSES_ROOT"))
                {
                    regHive = "HKCR";
                }
                else if (hive.Equals("HKEY_USERS"))
                {
                    regHive = "HKU";
                }
                else if (hive.Equals("HKEY_CURRENT_CONFIG"))
                {
                    regHive = "HKCC";
                }
                else
                {
                    throw new ArgumentException("Unsupported registry hive: " + hive);
                }

                // Create the registry path if it doesn't exist
                ProcessStartInfo createPsi = new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = "add \"" + regHive + "\\" + keyPath + "\" /f",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process createProc = Process.Start(createPsi))
                {
                    if (createProc != null)
                    {
                        createProc.WaitForExit(); // Ignore exit code - path might already exist
                    }
                }

                // Set the registry value using reg.exe add (creates key if needed)
                // Note: reg.exe add requires /f flag to overwrite existing values
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "reg",
                    Arguments = "add \"" + regHive + "\\" + keyPath + "\" /v \"" + keyName + "\" /t REG_SZ /d \"" + value + "\" /f",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        throw new InvalidOperationException("Failed to start reg.exe process");
                    }

                    // Read error output to detect permission errors
                    StringBuilder errorOutput = new StringBuilder();
                    using (var reader = proc.StandardError)
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            errorOutput.Append(line).Append("\n");
                        }
                    }

                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        string errorMsg = errorOutput.ToString();
                        if (errorMsg.Contains("access") || errorMsg.Contains("denied") || errorMsg.Contains("privilege"))
                        {
                            throw new SecurityException("Access denied. Administrator privileges required. " + errorMsg);
                        }
                        throw new InvalidOperationException("Failed to set registry value. Exit code: " + proc.ExitCode + ", Error: " + errorMsg);
                    }
                }
            }
            catch (SecurityException)
            {
                throw;
            }
            catch (Exception e)
            {
                if (e is SecurityException)
                {
                    throw;
                }
                throw new InvalidOperationException("Error writing registry value: " + e.Message, e);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:449-456
        // Original: private static boolean shouldShowInfoMessage()
        private static bool ShouldShowInfoMessage()
        {
            try
            {
                string markerPath = Path.Combine(JavaSystem.GetProperty("user.dir"), DONT_SHOW_INFO_MARKER_FILE);
                return !File.Exists(markerPath);
            }
            catch (Exception)
            {
                return true; // Default to showing if we can't check
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:461-470
        // Original: private static void markDontShowInfoMessage()
        private static void MarkDontShowInfoMessage()
        {
            try
            {
                string markerPath = Path.Combine(JavaSystem.GetProperty("user.dir"), DONT_SHOW_INFO_MARKER_FILE);
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: CREATING marker file: " + Path.GetFullPath(markerPath));
                File.Create(markerPath).Close();
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Created marker file: " + Path.GetFullPath(markerPath));
            }
            catch (Exception e)
            {
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Failed to create don't show info marker: " + e.Message);
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:478-590
        // Original: private boolean attemptElevatedRegistryWrite()
        private bool AttemptElevatedRegistryWrite()
        {
            // Show prompt to user (simplified for C# - UI layer should handle this)
            string message = "NCSDecomp needs administrator privileges to set a Windows registry key.\n\n" +
                "This is required for the " + (registryPath.Contains("KotOR2") ? "KotOR 2" : "KotOR 1") +
                " compiler (nwnnsscomp_ktool.exe or nwnnsscomp_kscript.exe) to work correctly.\n\n" +
                "The registry key will be temporarily set to:\n" +
                registryPath + "\\" + keyName + " = " + spoofedPath + "\n\n" +
                "Please run as administrator or allow elevation when prompted.";

            JavaSystem.@out.Println("[INFO] RegistrySpoofer: " + message);

            // Parse registry path for reg.exe
            int firstBackslash = registryPath.IndexOf('\\');
            if (firstBackslash < 0)
            {
                return false;
            }
            string hive = registryPath.Substring(0, firstBackslash);
            string keyPath = registryPath.Substring(firstBackslash + 1);

            string regHive;
            if (hive.Equals("HKEY_LOCAL_MACHINE"))
            {
                regHive = "HKLM";
            }
            else
            {
                return false;
            }

            try
            {
                // Create a temporary batch file to run the reg command elevated
                // This avoids complex PowerShell escaping issues
                string tempBatch = Path.GetTempFileName();
                File.Delete(tempBatch); // Delete the temp file created by GetTempFileName
                tempBatch = Path.ChangeExtension(tempBatch, ".bat");

                // Write the batch file content
                // First create the key path, then set the value
                // Escape % for batch files (need to double them)
                string escapedPath = spoofedPath.Replace("%", "%%");
                string batchContent = "@echo off\n" +
                    "reg add \"" + regHive + "\\" + keyPath + "\" /f >nul 2>&1\n" +
                    "reg add \"" + regHive + "\\" + keyPath + "\" /v \"" + keyName + "\" /t REG_SZ /d \"" +
                    escapedPath + "\" /f\n" +
                    "if errorlevel 1 (\n" +
                    "  echo Registry write failed\n" +
                    "  exit /b 1\n" +
                    ") else (\n" +
                    "  echo Registry write succeeded\n" +
                    "  exit /b 0\n" +
                    ")\n";

                JavaSystem.@out.Println("[INFO] RegistrySpoofer: CREATING temporary batch file: " + tempBatch);
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: WRITING batch file content (length: " + Encoding.UTF8.GetByteCount(batchContent) + " bytes)");
                File.WriteAllText(tempBatch, batchContent, Encoding.UTF8);
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Created temporary batch file: " + tempBatch);
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Batch file content:\n" + batchContent);

                // Run the batch file elevated using PowerShell
                // Note: -NoNewWindow doesn't work with -Verb RunAs, so we omit it
                // Use single quotes in PowerShell to avoid escaping issues with double quotes
                string batchPath = tempBatch.Replace("'", "''"); // Escape single quotes for PowerShell
                string psCommand = "Start-Process -FilePath '" + batchPath + "' -Verb RunAs -Wait";

                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Executing PowerShell command: " + psCommand);

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-Command " + psCommand,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process proc = Process.Start(psi))
                {
                    if (proc == null)
                    {
                        return false;
                    }

                    // Read output
                    StringBuilder output = new StringBuilder();
                    using (var reader = proc.StandardOutput)
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            output.Append(line).Append("\n");
                        }
                    }

                    proc.WaitForExit();

                    JavaSystem.@out.Println("[INFO] RegistrySpoofer: PowerShell exit code: " + proc.ExitCode);
                    string outputStr = output.ToString();
                    if (!string.IsNullOrWhiteSpace(outputStr))
                    {
                        JavaSystem.@out.Println("[INFO] RegistrySpoofer: PowerShell output: " + outputStr);
                    }

                    // Clean up temp file
                    try
                    {
                        JavaSystem.@out.Println("[INFO] RegistrySpoofer: DELETING temporary batch file: " + tempBatch);
                        File.Delete(tempBatch);
                        JavaSystem.@out.Println("[INFO] RegistrySpoofer: Deleted temporary batch file");
                    }
                    catch (Exception e)
                    {
                        JavaSystem.@out.Println("[INFO] RegistrySpoofer: Failed to delete temp file: " + e.Message);
                    }

                    if (proc.ExitCode == 0)
                    {
                        JavaSystem.@out.Println("[INFO] RegistrySpoofer: Elevated process completed successfully");
                        return true;
                    }
                    else
                    {
                        JavaSystem.@out.Println("[INFO] RegistrySpoofer: Elevated registry write failed. Exit code: " + proc.ExitCode +
                            ", Output: " + outputStr);
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Exception during elevated registry write: " + e.Message);
                e.PrintStackTrace();
                return false;
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:624-648
        // Original: private void showSubsequentElevationMessage()
        private void ShowSubsequentElevationMessage()
        {
            // Check if user has previously chosen "don't show again"
            if (!ShouldShowInfoMessage())
            {
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Skipping info message (user chose 'don't show again')");
                return;
            }

            string message = "NCSDecomp cannot set the Windows registry key (requires administrator privileges).\n\n" +
                "To avoid this message, you can either:\n" +
                "1. Run NCSDecomp as administrator, or\n" +
                "2. Use a different nwnnsscomp.exe compiler if available\n\n" +
                "Compilation will be attempted anyway, but may fail if the registry key is not set correctly.";

            JavaSystem.@out.Println("[INFO] RegistrySpoofer: " + message);
            // Note: UI layer should show this as a dialog with "don't show again" checkbox
            // For now, we just log it
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:691-769
        // Original: private void createRequiredFileStructure(String installationPath)
        private void CreateRequiredFileStructure(string installationPath)
        {
            try
            {
                File installDir = new File(installationPath);
                if (!installDir.Exists())
                {
                    JavaSystem.@out.Println("[INFO] RegistrySpoofer: Installation directory doesn't exist, creating: " + installationPath);
                    JavaSystem.@out.Println("[INFO] RegistrySpoofer: CREATING directory: " + installDir.GetAbsolutePath());
                    if (!installDir.Mkdirs())
                    {
                        JavaSystem.@out.Println("[INFO] RegistrySpoofer: WARNING - Failed to create installation directory: " + installationPath);
                        return;
                    }
                    JavaSystem.@out.Println("[INFO] RegistrySpoofer: Created directory: " + installDir.GetAbsolutePath());
                }

                // Create chitin.key - this is CRITICAL for initialization
                // ALWAYS recreate it to ensure it's valid (previous versions may have been corrupt)
                File chitinKey = new File(installDir, "chitin.key");
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Creating chitin.key file: " + chitinKey.GetAbsolutePath());
                if (chitinKey.Exists())
                {
                    JavaSystem.@out.Println("[INFO] RegistrySpoofer: DELETING existing chitin.key file: " + chitinKey.GetAbsolutePath());
                    chitinKey.Delete();
                }

                // BioWare KEY file format - MUST use LITTLE ENDIAN for integers
                byte[] keyFile = new byte[32]; // Header is 32 bytes (0x20)

                // Bytes 0-3: Magic "KEY " (ASCII)
                keyFile[0] = (byte)'K';
                keyFile[1] = (byte)'E';
                keyFile[2] = (byte)'Y';
                keyFile[3] = (byte)' ';

                // Bytes 4-7: Version "V1  " (ASCII)
                keyFile[4] = (byte)'V';
                keyFile[5] = (byte)'1';
                keyFile[6] = (byte)' ';
                keyFile[7] = (byte)' ';

                // Bytes 8-11: BIF count = 0 (little endian)
                WriteLittleEndianInt(keyFile, 8, 0);

                // Bytes 12-15: Key count = 0 (little endian)
                WriteLittleEndianInt(keyFile, 12, 0);

                // Bytes 16-19: Offset to file table = 32 (0x20, end of header, little endian)
                WriteLittleEndianInt(keyFile, 16, 32);

                // Bytes 20-23: Offset to key table = 32 (0x20, end of header, little endian)
                WriteLittleEndianInt(keyFile, 20, 32);

                // Bytes 24-27: Build year = 2003 (little endian)
                WriteLittleEndianInt(keyFile, 24, 2003);

                // Bytes 28-31: Build day = 1 (little endian)
                WriteLittleEndianInt(keyFile, 28, 1);

                JavaSystem.@out.Println("[INFO] RegistrySpoofer: WRITING chitin.key file: " + chitinKey.GetAbsolutePath() + " (size: " + keyFile.Length + " bytes)");
                System.IO.File.WriteAllBytes(chitinKey.GetAbsolutePath(), keyFile);
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: Created valid chitin.key file (size: " + chitinKey.Length() + " bytes, header: KEY V1)");

                // Create required directories - these ARE checked by the loader
                string[] dirs = { "override", "modules", "hak" };
                foreach (string dirName in dirs)
                {
                    File dir = new File(installDir, dirName);
                    if (!dir.Exists())
                    {
                        JavaSystem.@out.Println("[INFO] RegistrySpoofer: CREATING directory: " + dir.GetAbsolutePath());
                        if (dir.Mkdirs())
                        {
                            JavaSystem.@out.Println("[INFO] RegistrySpoofer: Created directory: " + dir.GetAbsolutePath());
                        }
                        else
                        {
                            JavaSystem.@out.Println("[INFO] RegistrySpoofer: WARNING - Failed to create directory: " + dir.GetAbsolutePath());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                JavaSystem.@out.Println("[INFO] RegistrySpoofer: WARNING - Failed to create required file structure: " + e.Message);
                e.PrintStackTrace();
                // Don't throw - this is not critical enough to fail the whole operation
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/RegistrySpoofer.java:774-779
        // Original: private static void writeLittleEndianInt(byte[] array, int offset, int value)
        private static void WriteLittleEndianInt(byte[] array, int offset, int value)
        {
            array[offset] = (byte)(value & 0xFF);
            array[offset + 1] = (byte)((value >> 8) & 0xFF);
            array[offset + 2] = (byte)((value >> 16) & 0xFF);
            array[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}

