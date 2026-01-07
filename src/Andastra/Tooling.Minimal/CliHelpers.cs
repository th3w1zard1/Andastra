using System;
using System.IO;
using JetBrains.Annotations;

namespace Andastra.Tooling.Minimal
{
    /// <summary>
    /// Common CLI utilities shared across tools
    /// </summary>
    public static class CliHelpers
    {
        /// <summary>
        /// Validates that a file exists and returns the full path
        /// </summary>
        [CanBeNull]
        public static string ValidateInputFile([NotNull] string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                Console.Error.WriteLine("Error: Input file path is required");
                return null;
            }

            string fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
            {
                Console.Error.WriteLine($"Error: Input file not found: {fullPath}");
                return null;
            }

            return fullPath;
        }

        /// <summary>
        /// Ensures output directory exists, creating it if necessary
        /// </summary>
        [CanBeNull]
        public static string ValidateOutputDirectory([NotNull] string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir))
            {
                Console.Error.WriteLine("Error: Output directory is required");
                return null;
            }

            string fullPath = Path.GetFullPath(outputDir);
            try
            {
                if (!Directory.Exists(fullPath))
                {
                    Directory.CreateDirectory(fullPath);
                    Console.WriteLine($"Created output directory: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: Cannot create output directory '{fullPath}': {ex.Message}");
                return null;
            }

            return fullPath;
        }

        /// <summary>
        /// Gets output file path, ensuring directory exists
        /// </summary>
        [CanBeNull]
        public static string GetOutputFilePath([NotNull] string inputFile, [CanBeNull] string outputDir, [CanBeNull] string outputFile)
        {
            string directory = outputDir;
            string fileName;

            if (!string.IsNullOrWhiteSpace(outputFile))
            {
                fileName = Path.GetFileName(outputFile);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    directory = Path.GetDirectoryName(outputFile);
                    if (string.IsNullOrWhiteSpace(directory))
                    {
                        directory = Directory.GetCurrentDirectory();
                    }
                }
            }
            else
            {
                fileName = Path.GetFileName(inputFile);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    directory = Directory.GetCurrentDirectory();
                }
            }

            string validatedDir = ValidateOutputDirectory(directory);
            if (validatedDir == null)
            {
                return null;
            }

            return Path.Combine(validatedDir, fileName);
        }

        /// <summary>
        /// Writes text to file safely with error handling
        /// </summary>
        public static bool WriteTextFile([NotNull] string filePath, [NotNull] string content)
        {
            try
            {
                File.WriteAllText(filePath, content);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error writing to file '{filePath}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads all bytes from file safely with error handling
        /// </summary>
        [CanBeNull]
        public static byte[] ReadBinaryFile([NotNull] string filePath)
        {
            try
            {
                return File.ReadAllBytes(filePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading file '{filePath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads all text from file safely with error handling
        /// </summary>
        [CanBeNull]
        public static string ReadTextFile([NotNull] string filePath)
        {
            try
            {
                return File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading file '{filePath}': {ex.Message}");
                return null;
            }
        }
    }
}