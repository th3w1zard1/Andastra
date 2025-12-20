using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HolocronToolset.Config;

namespace HolocronToolset.Update
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:419
    // Original: class AppUpdate(LibUpdate):
    /// <summary>
    /// Handles downloading, extracting, and applying application updates.
    /// This class manages the complete update lifecycle from download to restart.
    /// </summary>
    public class AppUpdate
    {
        private readonly string _updateFolder;
        private readonly List<string> _updateUrls;
        private readonly string _filestem;
        private readonly string _currentVersion;
        private readonly string _latestVersion;
        private readonly List<Action<Dictionary<string, object>>> _progressHooks;
        private readonly Action<bool> _exithook;
        private readonly Func<string, string> _versionToTagParser;
        private Func<List<string>> _getArchiveNames;
        private bool _isDownloading;
        private bool _downloadStatus;
        private string _archiveName;

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:422-450
        // Original: def __init__(self, update_urls: list[str], filestem: str, current_version: str, latest: str, ...):
        public AppUpdate(
            List<string> updateUrls,
            string filestem,
            string currentVersion,
            string latestVersion,
            List<Action<Dictionary<string, object>>> progressHooks = null,
            Action<bool> exithook = null,
            Func<string, string> versionToTagParser = null)
        {
            if (updateUrls == null || updateUrls.Count == 0)
            {
                throw new ArgumentException("Update URLs cannot be null or empty.", nameof(updateUrls));
            }
            if (string.IsNullOrEmpty(filestem))
            {
                throw new ArgumentException("Filestem cannot be null or empty.", nameof(filestem));
            }
            if (string.IsNullOrEmpty(currentVersion))
            {
                throw new ArgumentException("Current version cannot be null or empty.", nameof(currentVersion));
            }
            if (string.IsNullOrEmpty(latestVersion))
            {
                throw new ArgumentException("Latest version cannot be null or empty.", nameof(latestVersion));
            }

            _updateUrls = updateUrls;
            _filestem = filestem;
            _currentVersion = currentVersion;
            _latestVersion = latestVersion;
            _progressHooks = progressHooks ?? new List<Action<Dictionary<string, object>>>();
            _exithook = exithook;
            _versionToTagParser = versionToTagParser;

            // Create temporary update folder
            _updateFolder = Path.Combine(Path.GetTempPath(), $"holotoolset_update_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_updateFolder);

            // Set default archive name getter
            _getArchiveNames = GetArchiveNames;
            _archiveName = _getArchiveNames()[0];
        }

        /// <summary>
        /// Sets a custom function to get archive names. This allows overriding the default archive name detection.
        /// </summary>
        public Func<List<string>> GetArchiveNamesFunc
        {
            set => _getArchiveNames = value ?? GetArchiveNames;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:140-184
        // Original: def get_archive_names(self) -> list[str]:
        private List<string> GetArchiveNames()
        {
            string osName = RuntimeInformation.OSDescription;
            string arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

            // Normalize OS name
            if (osName.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    $"{_filestem}_Win_{arch}.zip",
                    $"{_filestem}_Windows_{arch}.zip",
                    $"{_filestem}_Windows_PyQt5_{arch}.zip"
                };
            }
            if (osName.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    $"{_filestem}_Linux_{arch}.zip",
                    $"{_filestem}_Linux_{arch}.tar.gz",
                    $"{_filestem}_Linux_{arch}.tar.bz2",
                    $"{_filestem}_Linux_{arch}.tar.xz",
                    $"{_filestem}_Linux_PyQt5_{arch}.zip",
                    $"{_filestem}_Linux_PyQt5_{arch}.tar.gz",
                    $"{_filestem}_Linux_PyQt5_{arch}.tar.bz2",
                    $"{_filestem}_Linux_PyQt5_{arch}.tar.xz"
                };
            }
            if (osName.Contains("Darwin", StringComparison.OrdinalIgnoreCase) || osName.Contains("macOS", StringComparison.OrdinalIgnoreCase))
            {
                return new List<string>
                {
                    $"{_filestem}_Mac_{arch}.tar.gz",
                    $"{_filestem}_macOS_{arch}.tar.gz",
                    $"{_filestem}_Mac_{arch}.tar.bz2",
                    $"{_filestem}_macOS_{arch}.tar.bz2",
                    $"{_filestem}_Mac_{arch}.tar.xz",
                    $"{_filestem}_macOS_{arch}.tar.xz",
                    $"{_filestem}_Mac_{arch}.zip",
                    $"{_filestem}_macOS_{arch}.zip",
                    $"{_filestem}_macOS_PyQt5_{arch}.zip",
                    $"{_filestem}_macOS_PyQt5_{arch}.tar.gz",
                    $"{_filestem}_macOS_PyQt5_{arch}.tar.bz2",
                    $"{_filestem}_macOS_PyQt5_{arch}.tar.xz"
                };
            }

            throw new NotSupportedException($"Unsupported OS: {osName}");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:195-212
        // Original: def download(self, *, background: bool = False) -> bool | None:
        /// <summary>
        /// Downloads the update archive. If background is false, blocks until download completes.
        /// </summary>
        public bool Download(bool background = false)
        {
            if (background)
            {
                if (!_isDownloading)
                {
                    _isDownloading = true;
                    Task.Run(() => _Download());
                }
                return false; // Background download, status unknown
            }
            if (!_isDownloading)
            {
                _isDownloading = true;
                return _Download();
            }
            return false;
        }

        private bool _Download()
        {
            try
            {
                if (IsDownloaded())
                {
                    _downloadStatus = true;
                }
                else
                {
                    _downloadStatus = _FullUpdate();
                }
            }
            finally
            {
                _isDownloading = false;
            }
            return _downloadStatus;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:349-356
        // Original: def _is_downloaded(self) -> bool:
        private bool IsDownloaded()
        {
            string originalDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(_updateFolder);
                foreach (string archiveName in _getArchiveNames())
                {
                    if (File.Exists(archiveName))
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:358-413
        // Original: def _full_update(self) -> bool:
        private bool _FullUpdate()
        {
            string originalDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(_updateFolder);
                string archivePath = Path.Combine(_updateFolder, _getArchiveNames()[0]);

                foreach (string url in _updateUrls)
                {
                    try
                    {
                        string parsedUrl = url;
                        if (_versionToTagParser != null)
                        {
                            string tag = _versionToTagParser(_latestVersion);
                            parsedUrl = parsedUrl.Replace("{tag}", tag);
                        }

                        // Download the file
                        using (var httpClient = new HttpClient())
                        {
                            httpClient.Timeout = TimeSpan.FromSeconds(30);
                            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HolocronToolset-Updater/1.0");

                            var response = httpClient.GetAsync(parsedUrl, HttpCompletionOption.ResponseHeadersRead).Result;
                            response.EnsureSuccessStatusCode();

                            long? contentLength = response.Content.Headers.ContentLength;
                            string downloadedFilename = response.Content.Headers.ContentDisposition?.FileName?.Trim('"', '\'')
                                ?? Path.GetFileName(parsedUrl);

                            using (var fileStream = new FileStream(downloadedFilename, FileMode.Create, FileAccess.Write, FileShare.None))
                            using (var httpStream = response.Content.ReadAsStreamAsync().Result)
                            {
                                byte[] buffer = new byte[8192];
                                long totalBytesRead = 0;
                                int bytesRead;

                                while ((bytesRead = httpStream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    fileStream.Write(buffer, 0, bytesRead);
                                    totalBytesRead += bytesRead;

                                    // Report progress
                                    if (contentLength.HasValue && _progressHooks.Count > 0)
                                    {
                                        int percent = (int)((totalBytesRead * 100) / contentLength.Value);
                                        var progressData = new Dictionary<string, object>
                                        {
                                            ["status"] = "downloading",
                                            ["downloaded"] = totalBytesRead,
                                            ["total"] = contentLength.Value,
                                            ["percent"] = percent
                                        };
                                        foreach (var hook in _progressHooks)
                                        {
                                            try
                                            {
                                                hook(progressData);
                                            }
                                            catch
                                            {
                                                // Ignore hook errors
                                            }
                                        }
                                    }
                                }
                            }

                            archivePath = Path.Combine(_updateFolder, downloadedFilename);
                            if (File.Exists(archivePath))
                            {
                                _archiveName = downloadedFilename;
                                if (!_getArchiveNames().Contains(downloadedFilename))
                                {
                                    // Override archive names getter to return the actual downloaded filename
                                    _getArchiveNames = () => new List<string> { downloadedFilename };
                                }
                                return true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Exception while downloading {url}: {ex}");
                        // Continue to next URL
                    }
                }
                return false;
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:453-466
        // Original: def extract_restart(self):
        /// <summary>
        /// Extracts the update, overwrites the current binary, and restarts the application.
        /// </summary>
        public void ExtractRestart()
        {
            try
            {
                _ExtractUpdate();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _WinRename(restart: true);
                }
                else
                {
                    _UnixOverwrite();
                    _UnixRestart();
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error in extract_restart: {ex}");
                throw;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:248-265
        // Original: def _extract_update(self):
        private void _ExtractUpdate()
        {
            string originalDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(_updateFolder);
                string archivePath = null;

                foreach (string archiveName in _getArchiveNames())
                {
                    string testPath = Path.Combine(_updateFolder, archiveName);
                    if (File.Exists(testPath))
                    {
                        archivePath = testPath;
                        break;
                    }
                }

                if (archivePath == null)
                {
                    // Try with different extensions
                    foreach (string archiveName in _getArchiveNames())
                    {
                        string baseName = Path.GetFileNameWithoutExtension(archiveName);
                        foreach (string ext in new[] { ".gz", ".tar", ".zip", ".bz2" })
                        {
                            string testPath = Path.Combine(_updateFolder, baseName + ext);
                            if (File.Exists(testPath))
                            {
                                _RecursiveExtract(testPath);
                                return;
                            }
                        }
                    }
                    throw new FileNotFoundException("Archive not found in update folder");
                }

                _RecursiveExtract(archivePath);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:267-285
        // Original: @classmethod def _recursive_extract(cls, archive_path: Path):
        private void _RecursiveExtract(string archivePath)
        {
            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException("Archive file not found", archivePath);
            }

            string ext = Path.GetExtension(archivePath).ToLowerInvariant();
            if (ext == ".zip")
            {
                ExtractZip(archivePath, recursiveExtract: true);
            }
            else if (ext == ".gz" || ext == ".tar" || archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            {
                ExtractTar(archivePath, recursiveExtract: true);
            }
            else
            {
                throw new NotSupportedException($"Unsupported archive format: {ext}");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:323-347
        // Original: @classmethod def extract_zip(cls, archive_path: os.PathLike | str, *, recursive_extract: bool = False):
        private void ExtractZip(string archivePath, bool recursiveExtract = false)
        {
            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue; // Skip directories
                    }

                    string destinationPath = Path.Combine(_updateFolder, entry.FullName);
                    string destinationDir = Path.GetDirectoryName(destinationPath);
                    if (!string.IsNullOrEmpty(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    // Sanitize path to prevent directory traversal
                    if (destinationPath.Contains("..") || !destinationPath.StartsWith(_updateFolder, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    entry.ExtractToFile(destinationPath, overwrite: true);

                    if (recursiveExtract)
                    {
                        string entryExt = Path.GetExtension(entry.Name).ToLowerInvariant();
                        if (new[] { ".gz", ".bz2", ".tar", ".zip" }.Contains(entryExt) && File.Exists(destinationPath))
                        {
                            _RecursiveExtract(destinationPath);
                        }
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:287-321
        // Original: @classmethod def extract_tar(cls, archive_path: os.PathLike | str, *, recursive_extract: bool = False):
        private void ExtractTar(string archivePath, bool recursiveExtract = false)
        {
            string tarPath = archivePath;
            string originalDir = Directory.GetCurrentDirectory();

            try
            {
                // Change to update folder directory for extraction
                Directory.SetCurrentDirectory(_updateFolder);

                // First, decompress if it's .gz
                if (archivePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) || archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                {
                    tarPath = Path.Combine(_updateFolder, Path.GetFileNameWithoutExtension(archivePath));
                    if (!tarPath.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                    {
                        // Remove .gz extension and add .tar
                        if (tarPath.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
                        {
                            // Already correct
                        }
                        else
                        {
                            tarPath = tarPath + ".tar";
                        }
                    }

                    using (FileStream sourceStream = File.OpenRead(archivePath))
                    using (FileStream tarStream = File.Create(tarPath))
                    using (var gzip = new GZipStream(sourceStream, CompressionMode.Decompress))
                    {
                        gzip.CopyTo(tarStream);
                    }
                }
                else if (archivePath.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase) || archivePath.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase))
                {
                    // Bzip2 compression requires a third-party library
                    throw new NotSupportedException("BZIP2 compressed TAR files are not supported. Please use ZIP or GZIP compressed archives for updates.");
                }
                else if (archivePath.EndsWith(".xz", StringComparison.OrdinalIgnoreCase) || archivePath.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase))
                {
                    // XZ compression requires a third-party library
                    throw new NotSupportedException("XZ compressed TAR files are not supported. Please use ZIP or GZIP compressed archives for updates.");
                }

                // Extract tar file using System.Formats.Tar (available in .NET 7+)
                try
                {
#if NET7_0_OR_GREATER
                    // Extract to current directory (which is _updateFolder)
                    System.Formats.Tar.TarFile.ExtractToDirectory(tarPath, ".", overwriteFiles: true);
#else
                    throw new NotSupportedException("TAR extraction requires .NET 7 or greater. Please use ZIP archives for updates.");
#endif
                }
                catch (Exception ex)
                {
                    throw new NotSupportedException($"TAR extraction failed: {ex.Message}. Please use ZIP archives for updates.", ex);
                }

                // Handle recursive extraction if needed
                if (recursiveExtract)
                {
                    string[] extractedFiles = Directory.GetFiles(_updateFolder, "*", SearchOption.AllDirectories);
                    foreach (string extractedFile in extractedFiles)
                    {
                        string ext = Path.GetExtension(extractedFile).ToLowerInvariant();
                        if (new[] { ".gz", ".bz2", ".tar", ".zip" }.Contains(ext))
                        {
                            _RecursiveExtract(extractedFile);
                        }
                    }
                }
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:530-639
        // Original: def _win_rename(self, *, restart: bool = False) -> tuple[Path, Path]:
        private void _WinRename(bool restart = false)
        {
            string exeName = GetExpectedFilename();
            string currentAppDir = GetCurrentAppDir();
            string curAppFilepath = Path.Combine(currentAppDir, exeName);
            string oldAppPath = Path.Combine(currentAppDir, $"{exeName}.old");
            string tempAppFilepath = Path.Combine(_updateFolder, exeName);

            // Find the updated app in the extracted archive
            if (!File.Exists(tempAppFilepath))
            {
                // Check if archive expanded into a folder
                string archiveStem = Path.GetFileNameWithoutExtension(_archiveName);
                string checkPath1 = Path.Combine(_updateFolder, archiveStem, exeName);
                string checkPath2 = Path.Combine(_updateFolder, archiveStem.Replace("_Win-", "_Windows_"), exeName);

                if (File.Exists(checkPath1))
                {
                    tempAppFilepath = checkPath1;
                }
                else if (File.Exists(checkPath2))
                {
                    tempAppFilepath = checkPath2;
                }
                else
                {
                    throw new FileNotFoundException($"Updated app not found at {tempAppFilepath}");
                }
            }

            // Remove old app from previous updates
            if (File.Exists(oldAppPath))
            {
                try
                {
                    File.Delete(oldAppPath);
                }
                catch
                {
                    // If deletion fails, try renaming with random suffix
                    string randomizedOldAppPath = oldAppPath + "." + Guid.NewGuid().ToString("N").Substring(0, 7);
                    File.Move(oldAppPath, randomizedOldAppPath);
                }
            }

            // Rename current app to .old
            if (File.Exists(curAppFilepath))
            {
                File.Move(curAppFilepath, oldAppPath);
            }

            // Copy updated app to current location
            File.Copy(tempAppFilepath, curAppFilepath, overwrite: true);

            // Hide the old app file on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && File.Exists(oldAppPath))
            {
                try
                {
                    File.SetAttributes(oldAppPath, File.GetAttributes(oldAppPath) | FileAttributes.Hidden);
                }
                catch
                {
                    // Ignore if hiding fails
                }
            }

            if (restart)
            {
                _WinRestart(curAppFilepath, tempAppFilepath);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:659-690
        // Original: def _win_overwrite(self, *, restart: bool = False):
        private void _WinOverwrite(bool restart = false)
        {
            string currentAppDir = GetCurrentAppDir();
            string currentAppPath = Path.Combine(currentAppDir, GetExpectedFilename());
            string archiveStem = Path.GetFileNameWithoutExtension(_archiveName);

            string updatedAppPath = null;
            string checkPath1 = Path.Combine(_updateFolder, archiveStem, GetExpectedFilename());
            string checkPath2 = Path.Combine(_updateFolder, archiveStem.Replace("_Win-", "_Windows_"), GetExpectedFilename());

            if (Directory.Exists(checkPath1))
            {
                updatedAppPath = checkPath1;
            }
            else if (Directory.Exists(checkPath2))
            {
                updatedAppPath = checkPath2;
            }
            else
            {
                updatedAppPath = Path.Combine(_updateFolder, GetExpectedFilename());
            }

            if (restart)
            {
                _WinRestart(currentAppPath, updatedAppPath);
            }
        }

        private void _WinRestart(string currentAppPath, string updatedAppPath)
        {
            // Launch the updated application
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = currentAppPath,
                UseShellExecute = true
            };
            Process.Start(startInfo);

            // Exit the current application
            _exithook?.Invoke(true);
            Environment.Exit(0);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:482-507
        // Original: def _unix_overwrite(self):
        private void _UnixOverwrite()
        {
            string currentAppDir = GetCurrentAppDir();
            string appUpdatePath = Path.Combine(_updateFolder, GetExpectedFilename());
            string currentAppPath = Path.Combine(currentAppDir, GetExpectedFilename());

            if (Directory.Exists(appUpdatePath))
            {
                if (Directory.Exists(currentAppPath))
                {
                    Directory.Delete(currentAppPath, recursive: true);
                }
                else if (File.Exists(currentAppPath))
                {
                    File.Delete(currentAppPath);
                }
            }

            if (File.Exists(appUpdatePath) || Directory.Exists(appUpdatePath))
            {
                if (File.Exists(appUpdatePath))
                {
                    File.Move(appUpdatePath, currentAppPath);
                }
                else
                {
                    // Move directory
                    Directory.Move(appUpdatePath, currentAppPath);
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:509-528
        // Original: def _unix_restart(self):
        private void _UnixRestart()
        {
            string currentAppDir = GetCurrentAppDir();
            string appPath = Path.Combine(currentAppDir, GetExpectedFilename());

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = appPath,
                UseShellExecute = true
            };
            Process.Start(startInfo);

            _exithook?.Invoke(true);
            Environment.Exit(0);
        }

        private string GetExpectedFilename()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"{_filestem}.exe";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return $"{_filestem}.app";
            }
            return _filestem;
        }

        private string GetCurrentAppDir()
        {
            // Get the directory of the current executable
            string exePath = Process.GetCurrentProcess().MainModule?.FileName
                ?? System.Reflection.Assembly.GetExecutingAssembly().Location
                ?? AppDomain.CurrentDomain.BaseDirectory;

            return Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/updater/update.py:415-416
        // Original: def cleanup(self):
        /// <summary>
        /// Cleans up temporary update files and folders.
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_updateFolder))
                {
                    Directory.Delete(_updateFolder, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

