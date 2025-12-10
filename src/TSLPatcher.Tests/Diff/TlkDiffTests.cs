using CSharpKOTOR.Diff;
using CSharpKOTOR.Formats.TLK;
using FluentAssertions;
using Xunit;

namespace CSharpKOTOR.Tests.Diff
{

    /// <summary>
    /// Tests for TLK diff functionality
    /// Ported from tests/tslpatcher/diff/test_tlk.py
    /// </summary>
    public class TlkDiffTests
    {
        [Fact]
        public void Compare_ShouldDetectAddedEntries()
        {
            var original = new TLK();
            original.Add("Text1");

            var modified = new TLK();
            modified.Add("Text1");
            modified.Add("Text2");

            TlkCompareResult result = TlkDiff.Compare(original, modified);

            result.AddedEntries.Should().HaveCount(1);
            result.AddedEntries.Should().ContainKey(1);
            result.AddedEntries[1].Text.Should().Be("Text2");
        }

        [Fact]
        public void Compare_ShouldDetectChangedText()
        {
            var original = new TLK();
            original.Add("OldText");

            var modified = new TLK();
            modified.Add("NewText");

            TlkCompareResult result = TlkDiff.Compare(original, modified);

            result.ChangedEntries.Should().ContainKey(0);
            result.ChangedEntries[0].Text.Should().Be("NewText");
            result.ChangedEntries[0].Sound.Should().BeNull();
        }

        [Fact]
        public void Compare_ShouldDetectChangedSound()
        {
            var original = new TLK();
            original.Add("Text", "Sound1");

            var modified = new TLK();
            modified.Add("Text", "Sound2");

            TlkCompareResult result = TlkDiff.Compare(original, modified);

            result.ChangedEntries.Should().ContainKey(0);
            result.ChangedEntries[0].Text.Should().BeNull();
            result.ChangedEntries[0].Sound.Should().Be("Sound2");
        }

    }
}
