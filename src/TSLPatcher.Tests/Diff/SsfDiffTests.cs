using CSharpKOTOR.Diff;
using CSharpKOTOR.Formats.SSF;
using FluentAssertions;
using Xunit;

namespace CSharpKOTOR.Tests.Diff
{

    /// <summary>
    /// Tests for SSF diff functionality
    /// Ported from tests/tslpatcher/diff/test_ssf.py
    /// </summary>
    public class SsfDiffTests
    {
        [Fact]
        public void Compare_ShouldDetectChangedSounds()
        {
            var original = new SSF();
            original.SetData(SSFSound.BATTLE_CRY_1, 1);

            var modified = new SSF();
            modified.SetData(SSFSound.BATTLE_CRY_1, 2);

            SsfCompareResult result = SsfDiff.Compare(original, modified);

            result.ChangedSounds.Should().ContainKey(SSFSound.BATTLE_CRY_1);
            result.ChangedSounds[SSFSound.BATTLE_CRY_1].Should().Be(2);
        }

    }
}
