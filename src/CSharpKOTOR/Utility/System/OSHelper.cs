using System;
using System.IO;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace AuroraEngine.Common.Utility.System
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
    }
}

