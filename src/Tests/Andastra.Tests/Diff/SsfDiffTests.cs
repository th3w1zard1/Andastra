using Andastra.Parsing.Diff;
using Andastra.Parsing.Formats.SSF;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Diff
{

    /// <summary>
    /// Tests for SSF diff functionality
    /// Ported from tests/tslpatcher/diff/test_ssf.py
    /// </summary>
    public class SsfDiffTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
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
