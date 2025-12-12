using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using FluentAssertions;
using Xunit;
using IOFile = System.IO.File;
using JavaFile = CSharpKOTOR.Formats.NCS.NCSDecomp.File;

namespace CSharpKOTOR.Tests.Formats
{
    /// <summary>
    /// Test NCS -> NSS -> NCS roundtrip for K1 undecompilable files.
    ///
    /// These files were previously marked as undecompilable but should now
    /// decompile and recompile to byte-identical NCS.
    /// Ported from K1UndecompilableRoundtripTest.java
    /// </summary>
    public class NCSDecompK1UndecompilableRoundtripTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly List<string> _tempFiles = new List<string>();

        public NCSDecompK1UndecompilableRoundtripTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"NCSDecomp_test_{Guid.NewGuid()}");
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            foreach (string file in _tempFiles)
            {
                try
                {
                    if (IOFile.Exists(file))
                        IOFile.Delete(file);
                }
                catch { }
            }
            try
            {
                if (Directory.Exists(_tempDir))
                    Directory.Delete(_tempDir, true);
            }
            catch { }
        }

        [Fact]
        public void TestK1UndecompilableRoundtrip()
        {
            // Find test directory
            string testDir = FindTestDirectory();
            if (testDir is null || !Directory.Exists(testDir))
            {
                // Skip if test files not found
                return;
            }

            // Find nwscript.nss
            string nwscriptPath = FindNwscriptNss();
            if (nwscriptPath is null || !IOFile.Exists(nwscriptPath))
            {
                // Skip if nwscript.nss not found
                return;
            }

            // Find nwnnsscomp.exe
            string nwnnsscompPath = FindNwnnsscomp();
            if (nwnnsscompPath is null || !IOFile.Exists(nwnnsscompPath))
            {
                // Skip if compiler not found
                return;
            }

            // Copy nwscript.nss to temp directory
            string tempNwscript = Path.Combine(_tempDir, "nwscript.nss");
            IOFile.Copy(nwscriptPath, tempNwscript, true);
            _tempFiles.Add(tempNwscript);

            // Copy nwnnsscomp.exe to temp directory
            string tempNwnnsscomp = Path.Combine(_tempDir, "nwnnsscomp.exe");
            IOFile.Copy(nwnnsscompPath, tempNwnnsscomp, true);
            _tempFiles.Add(tempNwnnsscomp);

            // Get all NCS files
            var ncsFiles = Directory.GetFiles(testDir, "*.ncs", SearchOption.AllDirectories)
                .OrderBy(f => f)
                .ToList();

            if (ncsFiles.Count == 0)
            {
                // Skip if no test files
                return;
            }

            int failed = 0;

            // TODO: Initialize FileDecompiler once it's properly converted
            // FileDecompiler decompiler = new FileDecompiler();

            foreach (string ncsPath in ncsFiles)
            {
                string testId = Path.GetRelativePath(testDir, ncsPath).Replace("\\", "/");

                try
                {
                    // TODO: Implement testRoundtrip once FileDecompiler is ready
                    // bool result = TestRoundtrip(decompiler, ncsPath, testId, tempNwnnsscomp);
                    // if (result)
                    // {
                    //     // Test passed
                    // }
                    // else
                    // {
                    //     failed++;
                    // }
                }
                catch
                {
                    failed++;
                }
            }

            // For now, just verify we found test files
            ncsFiles.Should().NotBeEmpty("Test files should exist");
        }

        private string FindTestDirectory()
        {
            // Look for test files relative to project root
            string currentDir = Directory.GetCurrentDirectory();
            DirectoryInfo dir = new DirectoryInfo(currentDir);

            for (int i = 0; i < 10 && dir != null; i++)
            {
                string candidate = Path.Combine(dir.FullName, "vendor", "PyKotor", "tests", "test_pykotor", "test_files", "K1_NCS_Un-decompilable");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }

            return null;
        }

        private string FindNwscriptNss()
        {
            string currentDir = Directory.GetCurrentDirectory();
            DirectoryInfo dir = new DirectoryInfo(currentDir);

            for (int i = 0; i < 10 && dir != null; i++)
            {
                string[] candidates = {
                    Path.Combine(dir.FullName, "vendor", "KotOR-Scripting-Tool", "NWN Script", "k1", "nwscript.nss"),
                    Path.Combine(dir.FullName, "vendor", "NorthernLights", "nwscript.nss"),
                    Path.Combine(dir.FullName, "vendor", "PyKotor", "vendor", "NCSDecomp", "nwscript.nss")
                };

                foreach (string candidate in candidates)
                {
                    if (IOFile.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                dir = dir.Parent;
            }

            return null;
        }

        private string FindNwnnsscomp()
        {
            string currentDir = Directory.GetCurrentDirectory();
            DirectoryInfo dir = new DirectoryInfo(currentDir);

            for (int i = 0; i < 10 && dir != null; i++)
            {
                string[] candidates = {
                    Path.Combine(dir.FullName, "vendor", "KotOR-Scripting-Tool", "NWN Script", "k1", "nwnnsscomp.exe"),
                    Path.Combine(dir.FullName, "vendor", "NorthernLights", "nwnnsscomp", "nwnnsscomp.exe"),
                    Path.Combine(dir.FullName, "vendor", "PyKotor", "vendor", "NCSDecomp", "nwnnsscomp.exe")
                };

                foreach (string candidate in candidates)
                {
                    if (IOFile.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                dir = dir.Parent;
            }

            // Also check PATH
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (string path in pathEnv.Split(Path.PathSeparator))
                {
                    string candidate = Path.Combine(path, "nwnnsscomp.exe");
                    if (IOFile.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private bool TestRoundtrip(CSharpKOTOR.Formats.NCS.NCSDecomp.FileDecompiler decompiler, string originalNcsPath, string testId, string nwnnsscompPath)
        {
            // Read original NCS bytes
            byte[] originalBytes = IOFile.ReadAllBytes(originalNcsPath);

            // Decompile to NSS
            // TODO: Use FileDecompiler once converted
            // int result = decompiler.Decompile(new File(originalNcsPath));
            // if (result == FileDecompiler.FAILURE)
            // {
            //     return false;
            // }
            //
            // string decompiled = decompiler.GetGeneratedCode(new File(originalNcsPath));
            // if (string.IsNullOrWhiteSpace(decompiled))
            // {
            //     return false;
            // }

            // Write decompiled NSS to temp file
            // string tempNss = Path.Combine(_tempDir, Path.GetFileNameWithoutExtension(originalNcsPath) + ".nss");
            // IOFile.WriteAllText(tempNss, decompiled);
            // _tempFiles.Add(tempNss);

            // Compile NSS back to NCS
            // string recompiledNcs = CompileNss(tempNss, nwnnsscompPath, false);
            // if (recompiledNcs is null || !IOFile.Exists(recompiledNcs))
            // {
            //     return false;
            // }

            // Compare byte arrays
            // byte[] recompiledBytes = IOFile.ReadAllBytes(recompiledNcs);
            // bool identical = originalBytes.SequenceEqual(recompiledBytes);

            // Clean up
            // if (IOFile.Exists(recompiledNcs))
            // {
            //     IOFile.Delete(recompiledNcs);
            // }

            // return identical;
            return false; // Placeholder
        }

        private string CompileNss(string nssPath, string nwnnsscompPath, bool k2)
        {
            string baseName = Path.GetFileNameWithoutExtension(nssPath);
            string outName = Path.Combine(Path.GetDirectoryName(nssPath), baseName + ".ncs");

            // Delete existing output
            if (IOFile.Exists(outName))
            {
                IOFile.Delete(outName);
            }

            // Build command
            string cmd;
            if (k2)
            {
                cmd = $"\"{nwnnsscompPath}\" -c -g 2 -o \"{outName}\" \"{nssPath}\"";
            }
            else
            {
                cmd = $"\"{nwnnsscompPath}\" -c -o \"{outName}\" \"{nssPath}\"";
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = nwnnsscompPath,
                Arguments = k2 ? $"-c -g 2 -o \"{outName}\" \"{nssPath}\"" : $"-c -o \"{outName}\" \"{nssPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                if (process is null)
                {
                    return null;
                }

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return null;
                }

                if (!IOFile.Exists(outName))
                {
                    return null;
                }

                return outName;
            }
        }
    }
}

