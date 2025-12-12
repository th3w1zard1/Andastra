using System;
using System.IO;

namespace NCSDecomp.Tests.TestHelpers
{
    internal static class TestFileHelper
    {
        public static string GetTestFilesPath()
        {
            return Path.Combine(AppContext.BaseDirectory, "test_files");
        }

        public static string GetTestFile(string fileName)
        {
            return Path.Combine(GetTestFilesPath(), fileName);
        }

        public static void EnsureTestFilesDirectory()
        {
            string testFilesPath = GetTestFilesPath();
            if (!Directory.Exists(testFilesPath))
            {
                Directory.CreateDirectory(testFilesPath);
            }
        }

        public static void CopyTestFile(string sourcePath, string fileName)
        {
            EnsureTestFilesDirectory();
            string destPath = GetTestFile(fileName);
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, destPath, true);
            }
        }
    }
}

