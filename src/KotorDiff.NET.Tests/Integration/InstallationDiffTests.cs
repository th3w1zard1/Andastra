// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_kotordiff_comprehensive.py
// Integration tests for KotorDiff installation comparison
using System;
using System.IO;
using System.Diagnostics;
using AuroraEngine.Common.Installation;
using AuroraEngine.Common.Mods;
using KotorDiff.NET.Resolution;
using KotorDiff.NET.Diff;
using Xunit;

namespace KotorDiff.NET.Tests.Integration
{
    public class InstallationDiffTests
    {
        [Fact]
        public void DiffInstallationsWithResolution_BasicComparison()
        {
            var stopwatch = Stopwatch.StartNew();

            // Create temporary directories with mock installation data
            string tempDir1 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string tempDir2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(tempDir1);
                Directory.CreateDirectory(tempDir2);

                // Create minimal installation structure
                File.WriteAllText(Path.Combine(tempDir1, "swkotor.exe"), "");
                File.WriteAllText(Path.Combine(tempDir1, "chitin.key"), "");
                File.WriteAllText(Path.Combine(tempDir2, "swkotor.exe"), "");
                File.WriteAllText(Path.Combine(tempDir2, "chitin.key"), "");

                // Don't create any files - test basic comparison with empty installations
                // (Creating game resource files would require more complex setup)

                var install1 = new Installation(tempDir1);
                var install2 = new Installation(tempDir2);

                var modifications = ModificationsByType.CreateEmpty();
                var logLines = new System.Collections.Generic.List<string>();
                Action<string> logFunc = msg => { }; // Silent logging for performance

                bool? result = InstallationDiffWithResolution.DiffInstallationsWithResolution(
                    new System.Collections.Generic.List<object> { install1, install2 },
                    filters: null,
                    logFunc: logFunc,
                    compareHashes: true,
                    modificationsByType: modifications,
                    incrementalWriter: null);

                stopwatch.Stop();

                // Verify result is not null (either true or false)
                Assert.NotNull(result);

                // Verify modifications were collected
                Assert.NotNull(modifications);

                // Performance check: should complete in under 2 minutes
                Assert.True(stopwatch.Elapsed.TotalMinutes < 2.0,
                    $"Diff operation took {stopwatch.Elapsed.TotalMinutes:F2} minutes, exceeding 2 minute limit. Actual time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

                Console.WriteLine($"Comparison completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
                Console.WriteLine($"Result: {(result.Value ? "IDENTICAL" : "DIFFERENT")}");
                Console.WriteLine($"TLK modifications: {modifications.Tlk.Count}");
                Console.WriteLine($"2DA modifications: {modifications.Twoda.Count}");
                Console.WriteLine($"GFF modifications: {modifications.Gff.Count}");
                Console.WriteLine($"SSF modifications: {modifications.Ssf.Count}");
                Console.WriteLine($"NCS modifications: {modifications.Ncs.Count}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"Test failed after {stopwatch.Elapsed.TotalSeconds:F2} seconds: {ex.Message}");
                throw;
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir1))
                {
                    Directory.Delete(tempDir1, true);
                }
                if (Directory.Exists(tempDir2))
                {
                    Directory.Delete(tempDir2, true);
                }
            }
        }

        [Fact]
        public void DiffInstallationsWithResolution_EmptyInstallations()
        {
            var stopwatch = Stopwatch.StartNew();

            // Create temporary directories
            string tempDir1 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string tempDir2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            try
            {
                Directory.CreateDirectory(tempDir1);
                Directory.CreateDirectory(tempDir2);

                // Create minimal installation structure (Installation requires swkotor.exe or swkotor2.exe)
                // Also need chitin.key for fallback detection
                File.WriteAllText(Path.Combine(tempDir1, "swkotor.exe"), "");
                File.WriteAllText(Path.Combine(tempDir1, "chitin.key"), "");
                File.WriteAllText(Path.Combine(tempDir2, "swkotor.exe"), "");
                File.WriteAllText(Path.Combine(tempDir2, "chitin.key"), "");

                var install1 = new Installation(tempDir1);
                var install2 = new Installation(tempDir2);

                var modifications = ModificationsByType.CreateEmpty();
                var logLines = new System.Collections.Generic.List<string>();
                Action<string> logFunc = msg => { }; // Silent logging for performance

                bool? result = InstallationDiffWithResolution.DiffInstallationsWithResolution(
                    new System.Collections.Generic.List<object> { install1, install2 },
                    filters: null,
                    logFunc: logFunc,
                    compareHashes: true,
                    modificationsByType: modifications,
                    incrementalWriter: null);

                stopwatch.Stop();

                // Should complete without errors
                Assert.NotNull(result);
                // Empty installations should be identical
                Assert.True(result.Value);

                // Performance check: should complete in under 2 minutes
                Assert.True(stopwatch.Elapsed.TotalMinutes < 2.0,
                    $"Empty installation diff took {stopwatch.Elapsed.TotalMinutes:F2} minutes, exceeding 2 minute limit. Actual time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

                Console.WriteLine($"Empty installation diff completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir1))
                {
                    Directory.Delete(tempDir1, true);
                }
                if (Directory.Exists(tempDir2))
                {
                    Directory.Delete(tempDir2, true);
                }
            }
        }
    }
}

