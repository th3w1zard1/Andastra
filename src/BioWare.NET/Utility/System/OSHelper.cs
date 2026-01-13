using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.Annotations;

namespace BioWare.NET.Utility.System
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/utility/system/os_helper.py:15-59
    // Original: def get_size_on_disk(file_path: Path, stat_result: os.stat_result | None = None) -> int:
    public static class OSHelper
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetCompressedFileSizeW(string lpFileName, out uint lpFileSizeHigh);

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/system/os_helper.py:15-59
        // Original: def get_size_on_disk(file_path: Path, stat_result: os.stat_result | None = None) -> int:
        public static long GetSizeOnDisk(string filePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                uint fileSizeHigh = 0;
                uint fileSizeLow = GetCompressedFileSizeW(filePath, out fileSizeHigh);

                if (fileSizeLow == 0xFFFFFFFF)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != 0)
                    {
                        throw new IOException($"GetCompressedFileSizeW failed with error {error}");
                    }
                }

                return ((long)fileSizeHigh << 32) + fileSizeLow;
            }
            else
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                {
                    throw new FileNotFoundException($"File not found: {filePath}");
                }
                return fileInfo.Length;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/system/os_helper.py:62-72
        // Original: def get_app_dir() -> Path:
        public static string GetAppDir()
        {
            if (IsFrozen())
            {
                return Path.GetDirectoryName(global::System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
            }
            return AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/system/os_helper.py:75-81
        // Original: def is_frozen() -> bool:
        public static bool IsFrozen()
        {
            return !string.IsNullOrEmpty(AppDomain.CurrentDomain.SetupInformation.ApplicationBase) &&
                   !string.IsNullOrEmpty(global::System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/system/os_helper.py:84-93
        // Original: def requires_admin(path: os.PathLike | str) -> bool:
        public static bool RequiresAdmin(string path)
        {
            if (Directory.Exists(path))
            {
                return DirRequiresAdmin(path);
            }
            if (File.Exists(path))
            {
                return FileRequiresAdmin(path);
            }
            return false;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/system/os_helper.py:96-104
        // Original: def file_requires_admin(file_path: os.PathLike | str) -> bool:
        public static bool FileRequiresAdmin(string filePath)
        {
            try
            {
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Write))
                {
                    // File opened successfully
                }
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/system/os_helper.py:107-129
        // Original: def dir_requires_admin(dirpath: os.PathLike | str, *, ignore_errors: bool = True) -> bool:
        public static bool DirRequiresAdmin(string dirPath, bool ignoreErrors = true)
        {
            string dummyFilePath = Path.Combine(dirPath, Guid.NewGuid().ToString());
            try
            {
                using (FileStream fs = new FileStream(dummyFilePath, FileMode.Create, FileAccess.Write))
                {
                    // File created successfully
                }
                File.Delete(dummyFilePath);
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                if (ignoreErrors)
                {
                    return true;
                }
                throw;
            }
            catch (DirectoryNotFoundException)
            {
                if (ignoreErrors)
                {
                    return true;
                }
                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(dummyFilePath))
                    {
                        File.Delete(dummyFilePath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/system/os_helper.py:132-150
        // Original: def remove_any(path: os.PathLike | str, *, ignore_errors: bool = True, missing_ok: bool = True):
        public static void RemoveAny(string path, bool ignoreErrors = true, bool missingOk = true)
        {
            if (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, true);
                }
                catch
                {
                    if (!ignoreErrors)
                    {
                        throw;
                    }
                }
            }
            else if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    if (!ignoreErrors)
                    {
                        throw;
                    }
                }
            }
            else if (!missingOk)
            {
                throw new FileNotFoundException($"Path not found: {path}");
            }
        }

        /// <summary>
        /// Attempts to gain native access to a file or directory by taking ownership and setting permissions.
        /// 1:1 port from PyKotor implementation at Libraries/PyKotor/src/utility/system/path.py:903-1002
        /// Original: def request_native_access(self: Path, *, elevate: bool = False, recurse: bool = True, log_func: Callable[[str], Any] | None = None)
        /// </summary>
        /// <param name="path">The file or directory path to gain access to</param>
        /// <param name="recurse">Whether to recursively apply permissions to subdirectories (default: true)</param>
        /// <param name="logAction">Optional action to log messages (default: writes to Console.WriteLine)</param>
        /// <returns>True if access was successfully gained, False otherwise</returns>
        public static bool RequestNativeAccess(
            string path,
            bool recurse = true,
            [CanBeNull] Action<string> logAction = null)
        {
            if (logAction == null)
            {
                logAction = message => Console.WriteLine(message);
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logAction("RequestNativeAccess is only supported on Windows");
                return false;
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                logAction($"Path does not exist: {path}");
                return false;
            }

            bool isDirectory = Directory.Exists(path);
            string pathStr = path;

            // Quote the path if it contains spaces
            if (pathStr.Contains(" "))
            {
                pathStr = $"\"{pathStr}\"";
            }

            bool allStepsSucceeded = true;

            // Step 1: Reset permissions and re-enable inheritance using icacls
            logAction($"Step 1: Resetting permissions and re-enabling inheritance for {path}...");
            if (!RunIcaclsReset(pathStr, isDirectory, recurse, logAction))
            {
                allStepsSucceeded = false;
            }

            // Step 2: Take ownership using takeown
            logAction($"Step 2: Attempting to take ownership of {path}...");
            if (!RunTakeOwn(pathStr, isDirectory, recurse, logAction))
            {
                allStepsSucceeded = false;
            }

            // Step 3: Grant full access using icacls
            logAction($"Step 3: Attempting to set access rights of {path} using icacls...");
            if (!RunIcaclsGrant(pathStr, isDirectory, recurse, logAction))
            {
                allStepsSucceeded = false;
            }

            // Step 4: Remove read-only/system/hidden attributes using attrib
            logAction($"Step 4: Removing system/hidden/read-only attributes from {path}...");
            if (!RunAttribRemove(pathStr, isDirectory, recurse, logAction))
            {
                allStepsSucceeded = false;
            }

            return allStepsSucceeded;
        }

        /// <summary>
        /// Runs icacls /reset command to reset permissions and re-enable inheritance.
        /// </summary>
        private static bool RunIcaclsReset(string pathStr, bool isDirectory, bool recurse, Action<string> logAction)
        {
            var args = new List<string> { pathStr, "/reset", "/Q" };
            if (isDirectory && recurse)
            {
                args.Add("/T");
            }

            return RunProcess("icacls", string.Join(" ", args), 60, logAction);
        }

        /// <summary>
        /// Runs takeown command to take ownership of the file or directory.
        /// </summary>
        private static bool RunTakeOwn(string pathStr, bool isDirectory, bool recurse, Action<string> logAction)
        {
            var args = new List<string> { "/F", pathStr, "/SKIPSL" };
            if (isDirectory)
            {
                args.Add("/D");
                args.Add("Y");
                if (recurse)
                {
                    args.Add("/R");
                }
            }

            return RunProcess("takeown", string.Join(" ", args), 60, logAction);
        }

        /// <summary>
        /// Runs icacls /grant command to grant full access to everyone.
        /// </summary>
        private static bool RunIcaclsGrant(string pathStr, bool isDirectory, bool recurse, Action<string> logAction)
        {
            // *S-1-1-0 is the SID for "Everyone" group
            // (OI) = Object Inherit, (CI) = Container Inherit, F = Full Control
            var args = new List<string> { pathStr, "/grant", "*S-1-1-0:(OI)(CI)F", "/C", "/L", "/Q" };
            if (recurse)
            {
                args.Add("/T");
            }

            bool success = RunProcess("icacls", string.Join(" ", args), 60, logAction);
            if (success)
            {
                logAction($"Permissions set successfully for {pathStr}");
            }
            return success;
        }

        /// <summary>
        /// Runs attrib command to remove read-only, system, and hidden attributes.
        /// </summary>
        private static bool RunAttribRemove(string pathStr, bool isDirectory, bool recurse, Action<string> logAction)
        {
            // Check attributes first to determine what needs to be removed
            string unquotedPath = pathStr.Trim('"');
            bool isReadOnly = false;
            bool isHidden = false;
            bool isSystem = false;

            try
            {
                if (isDirectory)
                {
                    var dirInfo = new DirectoryInfo(unquotedPath);
                    if (dirInfo.Exists)
                    {
                        isReadOnly = (dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
                        isHidden = (dirInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
                        isSystem = (dirInfo.Attributes & FileAttributes.System) == FileAttributes.System;
                    }
                }
                else
                {
                    var fileInfo = new FileInfo(unquotedPath);
                    if (fileInfo.Exists)
                    {
                        isReadOnly = fileInfo.IsReadOnly;
                        isHidden = (fileInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
                        isSystem = (fileInfo.Attributes & FileAttributes.System) == FileAttributes.System;
                    }
                }
            }
            catch (Exception ex)
            {
                logAction($"Warning: Could not check attributes for {unquotedPath}: {ex.Message}");
            }

            var args = new List<string>();
            if (isSystem)
            {
                args.Add("-S");
            }
            else if (isHidden)
            {
                args.Add("-H");
            }
            args.Add("-R");

            if (isDirectory)
            {
                args.Add("/D");
                if (recurse)
                {
                    args.Add("/S");
                }
            }

            args.Add(pathStr);

            bool success = RunProcess("attrib", string.Join(" ", args), 60, logAction);
            if (success)
            {
                logAction($"Attributes removed successfully for {pathStr}");
            }

            // If the item was hidden, re-apply the hidden attribute after removing read-only
            if (isHidden && success)
            {
                logAction($"Step 4.5: Re-applying the hidden attribute to {pathStr}...");
                var rehideArgs = new List<string> { "+H" };
                if (isDirectory)
                {
                    rehideArgs.Add("/D");
                    if (recurse)
                    {
                        rehideArgs.Add("/S");
                    }
                }
                rehideArgs.Add(pathStr);
                RunProcess("attrib", string.Join(" ", rehideArgs), 60, logAction);
            }

            return success;
        }

        /// <summary>
        /// Runs a process with the given executable name and arguments.
        /// </summary>
        private static bool RunProcess(string executable, string arguments, int timeoutSeconds, Action<string> logAction)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        logAction($"Failed to start process: {executable}");
                        return false;
                    }

                    var outputBuilder = new StringBuilder();
                    var errorBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errorBuilder.AppendLine(e.Data);
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    bool completed = process.WaitForExit(timeoutSeconds * 1000);
                    if (!completed)
                    {
                        process.Kill();
                        logAction($"Process {executable} timed out after {timeoutSeconds} seconds");
                        return false;
                    }

                    // Wait a short time for async output/error reading to complete
                    // The process has exited, but async handlers may still be processing
                    global::System.Threading.Thread.Sleep(100);
                    process.CancelOutputRead();
                    process.CancelErrorRead();

                    string output = outputBuilder.ToString().Trim();
                    string error = errorBuilder.ToString().Trim();

                    if (process.ExitCode != 0)
                    {
                        logAction($"Process {executable} failed with exit code {process.ExitCode}");
                        if (!string.IsNullOrEmpty(output))
                        {
                            logAction($"Output: {output}");
                        }
                        if (!string.IsNullOrEmpty(error))
                        {
                            logAction($"Error: {error}");
                        }
                        return false;
                    }

                    if (!string.IsNullOrEmpty(output))
                    {
                        logAction(output);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                logAction($"Exception running {executable}: {ex.Message}");
                return false;
            }
        }
    }
}

