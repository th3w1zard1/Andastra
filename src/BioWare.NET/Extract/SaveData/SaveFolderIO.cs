using System;
using System.Collections.Generic;
using System.IO;

namespace BioWare.NET.Extract.SaveData
{
    /// <summary>
    /// Centralized filesystem I/O for save folders.
    /// </summary>
    /// <remarks>
    /// This is intentionally format-agnostic: it only reads/writes known file names and returns raw bytes.
    /// Parsing/serialization is handled by the appropriate format layers (e.g., GFF/ERF/NFO).
    /// </remarks>
    public static class SaveFolderIO
    {
        public const string SaveNfoFileName = "savenfo.res";
        public const string SaveArchiveFileName = "savegame.sav";
        public const string ScreenshotFileName = "screen.tga";

        public static void EnsureDirectoryExists(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) throw new ArgumentException("Directory path cannot be null or empty.", nameof(directoryPath));
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        public static bool DirectoryExists(string directoryPath)
        {
            return !string.IsNullOrEmpty(directoryPath) && Directory.Exists(directoryPath);
        }

        public static IEnumerable<string> GetDirectories(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                yield break;
            }

            foreach (string dir in Directory.GetDirectories(directoryPath))
            {
                yield return dir;
            }
        }

        public static DateTime GetDirectoryLastWriteTime(string directoryPath)
        {
            return Directory.GetLastWriteTime(directoryPath);
        }

        public static void DeleteDirectoryRecursive(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) throw new ArgumentException("Directory path cannot be null or empty.", nameof(directoryPath));
            if (!Directory.Exists(directoryPath)) return;
            Directory.Delete(directoryPath, true);
        }

        public static void WriteSaveNfo(string saveDirectoryPath, byte[] nfoBytes)
        {
            if (nfoBytes == null) throw new ArgumentNullException(nameof(nfoBytes));
            EnsureDirectoryExists(saveDirectoryPath);
            string path = Path.Combine(saveDirectoryPath, SaveNfoFileName);
            File.WriteAllBytes(path, nfoBytes);
        }

        public static byte[] ReadSaveNfo(string saveDirectoryPath)
        {
            if (string.IsNullOrEmpty(saveDirectoryPath)) return null;
            string path = Path.Combine(saveDirectoryPath, SaveNfoFileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public static void WriteSaveArchive(string saveDirectoryPath, byte[] archiveBytes)
        {
            if (archiveBytes == null) throw new ArgumentNullException(nameof(archiveBytes));
            EnsureDirectoryExists(saveDirectoryPath);
            string path = Path.Combine(saveDirectoryPath, SaveArchiveFileName);
            File.WriteAllBytes(path, archiveBytes);
        }

        public static byte[] ReadSaveArchive(string saveDirectoryPath)
        {
            if (string.IsNullOrEmpty(saveDirectoryPath)) return null;
            string path = Path.Combine(saveDirectoryPath, SaveArchiveFileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public static void WriteScreenshot(string saveDirectoryPath, byte[] screenshotBytes)
        {
            if (screenshotBytes == null) throw new ArgumentNullException(nameof(screenshotBytes));
            EnsureDirectoryExists(saveDirectoryPath);
            string path = Path.Combine(saveDirectoryPath, ScreenshotFileName);
            File.WriteAllBytes(path, screenshotBytes);
        }

        public static byte[] ReadScreenshot(string saveDirectoryPath)
        {
            if (string.IsNullOrEmpty(saveDirectoryPath)) return null;
            string path = Path.Combine(saveDirectoryPath, ScreenshotFileName);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }
}


