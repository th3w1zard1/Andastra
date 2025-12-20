// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py
// Tests for ResourceResolver functionality
extern alias resolution;
using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Xunit;
using Resolution = resolution::KotorDiff.Resolution;

namespace KotorDiff.Tests.Resolution
{
    public class ResourceResolverTests
    {
        [Fact]
        public void GetLocationDisplayName_ReturnsCorrectNames()
        {
            // GetLocationDisplayName just returns the input as-is (matching Python implementation)
            Assert.Equal("Override folder", Resolution.ResourceResolver.GetLocationDisplayName("Override folder"));
            Assert.Equal("Modules (.mod)", Resolution.ResourceResolver.GetLocationDisplayName("Modules (.mod)"));
            Assert.Equal("Modules (.rim/_s.rim/_dlg.erf)", Resolution.ResourceResolver.GetLocationDisplayName("Modules (.rim/_s.rim/_dlg.erf)"));
            Assert.Equal("Chitin BIFs", Resolution.ResourceResolver.GetLocationDisplayName("Chitin BIFs"));
            Assert.Equal("Not Found", Resolution.ResourceResolver.GetLocationDisplayName(null));
        }

        [Fact]
        public void ShouldProcessTlkFile_ReturnsTrueForDialogTlk()
        {
            var resolved = new Resolution.ResolvedResource
            {
                Filepath = Path.Combine("C:", "Game", "dialog.tlk"),
                LocationType = "Override folder"
            };
            Assert.True(Resolution.ResourceResolver.ShouldProcessTlkFile(resolved));

            resolved.Filepath = Path.Combine("C:", "Game", "dialog_f.tlk");
            Assert.True(Resolution.ResourceResolver.ShouldProcessTlkFile(resolved));
        }

        [Fact]
        public void ShouldProcessTlkFile_ReturnsFalseForNonDialogTlk()
        {
            var resolved = new Resolution.ResolvedResource
            {
                Filepath = Path.Combine("C:", "Game", "other.tlk"),
                LocationType = "Override folder"
            };
            Assert.False(Resolution.ResourceResolver.ShouldProcessTlkFile(resolved));
        }
    }
}

