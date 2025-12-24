using System;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{

    /// <summary>
    /// Tests for TalkTable (read-only TLK accessor).
    /// 1:1 port of Python test_talk_table_vendor.py from tests/common/test_talk_table_vendor.py
    /// </summary>
    public class TalkTableTests
    {
        private readonly TalkTable _talkTable;

        public TalkTableTests()
        {
            // Register code pages encoding provider for windows-1252 support
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            string testFile = TestFileHelper.GetPath("test.tlk");
            _talkTable = new TalkTable(testFile);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestStringsAndResrefsAreCorrect()
        {
            _talkTable.GetString(0).Should().Be("abcdef");
            _talkTable.GetSound(0).ToString().Should().Be("resref01");

            _talkTable.GetString(1).Should().Be("ghijklmnop");
            _talkTable.GetSound(1).ToString().Should().Be("resref02");

            _talkTable.GetString(2).Should().Be("qrstuvwxyz");
            _talkTable.GetSound(2).ToString().Should().Be("");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestLanguageIsCorrect()
        {
            _talkTable.GetLanguage().Should().Be(Language.English);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSizeIsCorrect()
        {
            _talkTable.Size().Should().Be(3);
        }

    }
}
