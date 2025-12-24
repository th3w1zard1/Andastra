using System;
using System.Collections.Generic;
using System.IO;

using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Installation;

using Xunit;

namespace Andastra.Tests.Parsing
{
    public class ModuleFileDiscoveryTests
    {
        [Fact]
        public void GetModuleRoot_DoesNotSplitOnUnderscores()
        {
            Assert.Equal("tar_m02aa", Installation.GetModuleRoot("tar_m02aa.rim"));
            Assert.Equal("tar_m02aa", Installation.GetModuleRoot("tar_m02aa_s.rim"));
            Assert.Equal("tar_m02aa", Installation.GetModuleRoot("tar_m02aa_dlg.erf"));
            Assert.Equal("foo", Installation.GetModuleRoot("foo.mod"));
        }

        [Fact]
        public void DiscoverModuleFiles_ModOverridesAllOtherPieces()
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), "and_moddisc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);

            try
            {
                // Use uppercase on disk to validate case-insensitive discovery
                File.WriteAllText(Path.Combine(tmpDir, "test01.MOD"), "");
                File.WriteAllText(Path.Combine(tmpDir, "test01.rim"), "");
                File.WriteAllText(Path.Combine(tmpDir, "test01_s.rim"), "");
                File.WriteAllText(Path.Combine(tmpDir, "test01_dlg.erf"), "");

                ModuleFileGroup group = ModuleFileDiscovery.DiscoverModuleFiles(tmpDir, "test01", BioWareGame.TSL);
                Assert.NotNull(group);
                Assert.True(group.UsesModOverride);
                Assert.NotNull(group.ModFile);
                Assert.Null(group.MainRimFile);
                Assert.Null(group.DataRimFile);
                Assert.Null(group.DlgErfFile);
            }
            finally
            {
                try { Directory.Delete(tmpDir, true); }
                catch { }
            }
        }

        [Fact]
        public void DiscoverModuleFiles_CompositeModuleRequiresMainRim()
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), "and_moddisc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);

            try
            {
                // Only _s.rim present, no main .rim
                File.WriteAllText(Path.Combine(tmpDir, "test02_s.rim"), "");

                ModuleFileGroup group = ModuleFileDiscovery.DiscoverModuleFiles(tmpDir, "test02", BioWareGame.K1);
                Assert.Null(group);
            }
            finally
            {
                try { Directory.Delete(tmpDir, true); }
                catch { }
            }
        }

        [Fact]
        public void DiscoverModuleFiles_CompositeModuleIncludesOptionalPieces()
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), "and_moddisc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);

            try
            {
                File.WriteAllText(Path.Combine(tmpDir, "test03.rim"), "");
                File.WriteAllText(Path.Combine(tmpDir, "test03_s.rim"), "");
                File.WriteAllText(Path.Combine(tmpDir, "test03_dlg.erf"), "");

                ModuleFileGroup k2 = ModuleFileDiscovery.DiscoverModuleFiles(tmpDir, "test03", BioWareGame.TSL);
                Assert.NotNull(k2);
                Assert.False(k2.UsesModOverride);
                Assert.NotNull(k2.MainRimFile);
                Assert.NotNull(k2.DataRimFile);
                Assert.NotNull(k2.DlgErfFile);

                ModuleFileGroup k1 = ModuleFileDiscovery.DiscoverModuleFiles(tmpDir, "test03", BioWareGame.K1);
                Assert.NotNull(k1);
                Assert.False(k1.UsesModOverride);
                Assert.NotNull(k1.MainRimFile);
                Assert.NotNull(k1.DataRimFile);
                Assert.Null(k1.DlgErfFile);
            }
            finally
            {
                try { Directory.Delete(tmpDir, true); }
                catch { }
            }
        }

        [Fact]
        public void DiscoverAllModuleRoots_UsesKnownSuffixStrippingOnly()
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), "and_modroots_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);

            try
            {
                // Mix of module containers and noise
                File.WriteAllText(Path.Combine(tmpDir, "tar_m02aa.rim"), "");
                File.WriteAllText(Path.Combine(tmpDir, "tar_m02aa_s.rim"), "");
                File.WriteAllText(Path.Combine(tmpDir, "tar_m02aa_dlg.erf"), "");
                File.WriteAllText(Path.Combine(tmpDir, "random.erf"), ""); // should be ignored
                File.WriteAllText(Path.Combine(tmpDir, "notamodule.txt"), "");

                HashSet<string> roots = ModuleFileDiscovery.DiscoverAllModuleRoots(tmpDir);
                Assert.Contains("tar_m02aa", roots);
                Assert.DoesNotContain("random", roots);
            }
            finally
            {
                try { Directory.Delete(tmpDir, true); }
                catch { }
            }
        }
    }
}


