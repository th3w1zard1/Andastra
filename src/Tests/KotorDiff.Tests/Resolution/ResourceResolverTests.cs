// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py
// Tests for ResourceResolver functionality
extern alias resolution;
using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Resolution = resolution::KotorDiff.Resolution;
using Xunit;

namespace KotorDiff.Tests.Resolution
{
    public class ResourceResolverTests
    {
        [Fact]
        public void GetLocationDisplayName_ReturnsCorrectNames()
        {
            // GetLocationDisplayName just returns the input as-is (matching Python implementation)
            Assert.Equal("Override folder", resolution::KotorDiff.Resolution.ResourceResolver.GetLocationDisplayName("Override folder"));
            Assert.Equal("Modules (.mod)", resolution::KotorDiff.Resolution.ResourceResolver.GetLocationDisplayName("Modules (.mod)"));
            Assert.Equal("Modules (.rim/_s.rim/_dlg.erf)", resolution::KotorDiff.Resolution.ResourceResolver.GetLocationDisplayName("Modules (.rim/_s.rim/_dlg.erf)"));
            Assert.Equal("Chitin BIFs", resolution::KotorDiff.Resolution.ResourceResolver.GetLocationDisplayName("Chitin BIFs"));
            Assert.Equal("Not Found", resolution::KotorDiff.Resolution.ResourceResolver.GetLocationDisplayName(null));
        }

        [Fact]
        public void ShouldProcessTlkFile_ReturnsTrueForDialogTlk()
        {
            var resolved = new resolution::KotorDiff.Resolution.ResolvedResource
            {
                Filepath = Path.Combine("C:", "Game", "dialog.tlk"),
                LocationType = "Override folder"
            };
            Assert.True(resolution::KotorDiff.Resolution.ResourceResolver.ShouldProcessTlkFile(resolved));

            resolved.Filepath = Path.Combine("C:", "Game", "dialog_f.tlk");
            Assert.True(resolution::KotorDiff.Resolution.ResourceResolver.ShouldProcessTlkFile(resolved));
        }

        [Fact]
        public void ShouldProcessTlkFile_ReturnsFalseForNonDialogTlk()
        {
            var resolved = new resolution::KotorDiff.Resolution.ResolvedResource
            {
                Filepath = Path.Combine("C:", "Game", "other.tlk"),
                LocationType = "Override folder"
            };
            Assert.False(resolution::KotorDiff.Resolution.ResourceResolver.ShouldProcessTlkFile(resolved));
        }
    }
}

