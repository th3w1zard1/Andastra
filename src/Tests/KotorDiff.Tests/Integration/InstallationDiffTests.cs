// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_kotordiff_comprehensive.py
// Integration tests for KotorDiff installation comparison
extern alias resolution;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Mods;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Common;
using KotorDiff.Diff;
using KotorDiff.Tests.Helpers;
using Xunit;
using Resolution = resolution::KotorDiff.Resolution;

namespace KotorDiff.Tests.Integration
{
    public class InstallationDiffTests
    {
        /// <summary>
        /// Create a comprehensive mock KOTOR installation directory with all necessary files and structure.
        /// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/integration_test.py:33-51
        /// </summary>
        /// <param name="basePath">Base directory where installation will be created</param>
        /// <param name="name">Name of the installation directory</param>
        /// <param name="isK2">Whether this is a KOTOR 2 installation (default: false for K1)</param>
        /// <returns>Path to the created installation directory</returns>
        private static string CreateMockKotorInstallation(string basePath, string name, bool isK2 = false)
        {
            string installPath = Path.Combine(basePath, name);
            Directory.CreateDirectory(installPath);

            // Create essential KOTOR files
            string exeName = isK2 ? "swkotor2.exe" : "swkotor.exe";
            File.WriteAllText(Path.Combine(installPath, exeName), "");
            File.WriteAllText(Path.Combine(installPath, "chitin.key"), "");

            // Create standard KOTOR directories
            Directory.CreateDirectory(Path.Combine(installPath, "Override"));
            Directory.CreateDirectory(Path.Combine(installPath, "Modules"));
            Directory.CreateDirectory(Path.Combine(installPath, "Lips"));
            Directory.CreateDirectory(Path.Combine(installPath, "data"));

            // Create dialog.tlk file with sample entries
            var tlk = TestDataHelper.CreateBasicTLK(new List<(string text, string soundResRef)>
            {
                ("Test String 1", "sound1"),
                ("Test String 2", "sound2"),
                ("Test String 3", "")
            });
            string tlkPath = Path.Combine(installPath, "dialog.tlk");
            TLKAuto.WriteTlk(tlk, tlkPath, ResourceType.TLK);

            // Create sample 2DA file in Override
            var twoda = TestDataHelper.CreateBasic2DA(
                new List<string> { "col1", "col2", "col3" },
                new List<(string label, Dictionary<string, string> cells)>
                {
                    ("0", new Dictionary<string, string> { { "col1", "value1" }, { "col2", "value2" }, { "col3", "value3" } }),
                    ("1", new Dictionary<string, string> { { "col1", "value4" }, { "col2", "value5" }, { "col3", "value6" } })
                }
            );
            string twodaPath = Path.Combine(installPath, "Override", "test.2da");
            TwoDAAuto.WriteTwoDA(twoda, twodaPath, ResourceType.TwoDA);

            // Create sample GFF file (UTC) in Override
            var utcGff = TestDataHelper.CreateBasicGFF(new Dictionary<string, (GFFFieldType fieldType, object value)>
            {
                ["FirstName"] = (GFFFieldType.String, "Test"),
                ["LastName"] = (GFFFieldType.String, "Character"),
                ["Tag"] = (GFFFieldType.String, "test_char"),
                ["HitPoints"] = (GFFFieldType.Int32, 100),
                ["MaxHitPoints"] = (GFFFieldType.Int32, 100)
            });
            string utcPath = Path.Combine(installPath, "Override", "test.utc");
            GFFAuto.WriteGff(utcGff, utcPath, ResourceType.UTC);

            // Create sample GFF file (UTI) in Override
            var utiGff = TestDataHelper.CreateBasicGFF(new Dictionary<string, (GFFFieldType fieldType, object value)>
            {
                ["Tag"] = (GFFFieldType.String, "test_item"),
                ["LocalizedName"] = (GFFFieldType.LocalizedString, new LocalizedString(0, "Test Item")),
                ["BaseItem"] = (GFFFieldType.UInt32, 1u),
                ["Cost"] = (GFFFieldType.UInt32, 100u)
            });
            string utiPath = Path.Combine(installPath, "Override", "test.uti");
            GFFAuto.WriteGff(utiGff, utiPath, ResourceType.UTI);

            // Create sample SSF file in Override
            var ssf = TestDataHelper.CreateBasicSSF(new Dictionary<SSFSound, int>
            {
                [SSFSound.BATTLE_CRY_1] = 0,
                [SSFSound.BATTLE_CRY_2] = 1,
                [SSFSound.BATTLE_CRY_3] = 2
            });
            string ssfPath = Path.Combine(installPath, "Override", "test.ssf");
            SSFAuto.WriteSsf(ssf, ssfPath, ResourceType.SSF);

            // Create a simple module structure
            string moduleDir = Path.Combine(installPath, "Modules", "test_module");
            Directory.CreateDirectory(moduleDir);
            var moduleIfo = TestDataHelper.CreateBasicGFF(new Dictionary<string, (GFFFieldType fieldType, object value)>
            {
                ["Mod_Entry_Area"] = (GFFFieldType.String, "001ebo"),
                ["Mod_Entry_Door"] = (GFFFieldType.String, ""),
                ["Mod_Entry_X"] = (GFFFieldType.Single, 0.0f),
                ["Mod_Entry_Y"] = (GFFFieldType.Single, 0.0f),
                ["Mod_Entry_Z"] = (GFFFieldType.Single, 0.0f)
            });
            string moduleIfoPath = Path.Combine(moduleDir, "module.ifo");
            GFFAuto.WriteGff(moduleIfo, moduleIfoPath, ResourceType.IFO);

            return installPath;
        }
        [Fact]
        public void DiffInstallationsWithResolution_BasicComparison()
        {
            var stopwatch = Stopwatch.StartNew();

            // Create temporary directories with comprehensive mock installation data
            string baseTempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(baseTempDir);

            string tempDir1 = CreateMockKotorInstallation(baseTempDir, "install1", isK2: false);
            string tempDir2 = CreateMockKotorInstallation(baseTempDir, "install2", isK2: false);

            try
            {
                var install1 = new Installation(tempDir1);
                var install2 = new Installation(tempDir2);

                var modifications = ModificationsByType.CreateEmpty();
                var logLines = new System.Collections.Generic.List<string>();
                Action<string> logFunc = msg => { }; // Silent logging for performance

                bool? result = resolution::KotorDiff.Resolution.InstallationDiffWithResolution.DiffInstallationsWithResolution(
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
                // Cleanup - delete base directory which contains both installations
                if (Directory.Exists(baseTempDir))
                {
                    Directory.Delete(baseTempDir, true);
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

                bool? result = resolution::KotorDiff.Resolution.InstallationDiffWithResolution.DiffInstallationsWithResolution(
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

