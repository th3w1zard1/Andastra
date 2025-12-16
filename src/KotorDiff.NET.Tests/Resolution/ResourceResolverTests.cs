// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/resolution.py
// Tests for ResourceResolver functionality
using System;
using System.Collections.Generic;
using System.IO;
using AuroraEngine.Common.Installation;
using AuroraEngine.Common.Resources;
using KotorDiff.NET.Resolution;
using Xunit;

namespace KotorDiff.NET.Tests.Resolution
{
    public class ResourceResolverTests
    {
        [Fact]
        public void GetLocationDisplayName_ReturnsCorrectNames()
        {
            // GetLocationDisplayName just returns the input as-is (matching Python implementation)
            Assert.Equal("Override folder", ResourceResolver.GetLocationDisplayName("Override folder"));
            Assert.Equal("Modules (.mod)", ResourceResolver.GetLocationDisplayName("Modules (.mod)"));
            Assert.Equal("Modules (.rim/_s.rim/_dlg.erf)", ResourceResolver.GetLocationDisplayName("Modules (.rim/_s.rim/_dlg.erf)"));
            Assert.Equal("Chitin BIFs", ResourceResolver.GetLocationDisplayName("Chitin BIFs"));
            Assert.Equal("Not Found", ResourceResolver.GetLocationDisplayName(null));
        }

        [Fact]
        public void ShouldProcessTlkFile_ReturnsTrueForDialogTlk()
        {
            var resolved = new ResolvedResource
            {
                Filepath = Path.Combine("C:", "Game", "dialog.tlk"),
                LocationType = "Override folder"
            };
            Assert.True(ResourceResolver.ShouldProcessTlkFile(resolved));

            resolved.Filepath = Path.Combine("C:", "Game", "dialog_f.tlk");
            Assert.True(ResourceResolver.ShouldProcessTlkFile(resolved));
        }

        [Fact]
        public void ShouldProcessTlkFile_ReturnsFalseForNonDialogTlk()
        {
            var resolved = new ResolvedResource
            {
                Filepath = Path.Combine("C:", "Game", "other.tlk"),
                LocationType = "Override folder"
            };
            Assert.False(ResourceResolver.ShouldProcessTlkFile(resolved));
        }
    }
}

