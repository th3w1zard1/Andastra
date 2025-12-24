using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Common
{

    /// <summary>
    /// Tests for CaseAwarePath - case-insensitive path handling
    /// Ported from Python tests:
    /// - tests/common/test_case_aware_path.py
    /// - tests/common/test_get_case_sensitive_path.py
    /// - tests/common/test_path_isinstance.py
    /// - tests/common/test_path_mixed_slash_handling.py
    /// </summary>
    public class CaseAwarePathTests
    {
        #region Basic Construction and Validation

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Constructor_ShouldAcceptValidStrings()
        {
            // Python test: test_new_valid_str_argument
            Func<CaseAwarePath> act1 = () => new CaseAwarePath(@"C:\path\to\dir");
            Func<CaseAwarePath> act2 = () => new CaseAwarePath("/path/to/dir");

            act1.Should().NotThrow();
            act2.Should().NotThrow();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Constructor_ShouldRejectInvalidTypes()
        {
            // Python test: test_new_invalid_argument
            Func<CaseAwarePath> act = () => new CaseAwarePath(123 as object);

            act.Should().Throw<ArgumentException>();
        }

        #endregion

        #region Hashing and Equality

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Hashing_ShouldBeCaseInsensitive()
        {
            // Python test: test_hashing
            var path1 = new CaseAwarePath(@"test\path\to\nothing");
            var path2 = new CaseAwarePath(@"tesT\PATH\to\noTHinG");

            path1.Should().Be(path2);
            path1.GetHashCode().Should().Be(path2.GetHashCode());

            var testSet = new HashSet<CaseAwarePath> { path1, path2 };
            testSet.Should().HaveCount(1);
            testSet.Should().Contain(new CaseAwarePath(@"TEST\path\to\nothing"));
        }

        #endregion

        #region Path Properties

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Name_ShouldReturnFileName()
        {
            // Python test: test_valid_name_property
            new CaseAwarePath("test", @"data\something.test").Name.Should().Be("something.test");
            (new CaseAwarePath("test") / "data/something.test").Name.Should().Be("something.test");
            new CaseAwarePath("test").JoinPath(@"data\something.test").Name.Should().Be("something.test");
            new CaseAwarePath("test").JoinPath("data/something.test").Name.Should().Be("something.test");
        }

        #endregion

        #region EndsWith

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void EndsWith_ShouldBeCaseInsensitive()
        {
            // Python test: test_endswith
            var path = new CaseAwarePath(@"C:\path\to\file.txt");

            path.EndsWith(".TXT").Should().BeTrue();
            path.EndsWith(".txt").Should().BeTrue();
            path.EndsWith(".doc").Should().BeFalse();
        }

        #endregion

        #region Find Closest Match

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void FindClosestMatch_ShouldReturnBestCaseMatch()
        {
            // Python test: test_find_closest_match
            var items = new List<CaseAwarePath>
            {
                new CaseAwarePath("test"),
                new CaseAwarePath("TEST"),
                new CaseAwarePath("TesT"),
                new CaseAwarePath("teSt")
            };

            var result = CaseAwarePath.FindClosestMatch("teST", items);
            result.ToString().Should().Be("teSt");
        }

        #endregion

        #region Get Matching Characters Count

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GetMatchingCharactersCount_ShouldCountCaseMatches()
        {
            // Python test: test_get_matching_characters_count
            CaseAwarePath.GetMatchingCharactersCount("test", "tesT").Should().Be(3);
            CaseAwarePath.GetMatchingCharactersCount("test", "teat").Should().Be(-1);
        }

        #endregion

        #region RelativeTo

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void RelativeTo_ShouldWorkWithRelativePaths()
        {
            // Python test: test_relative_to_relpath
            var filePath = new CaseAwarePath(@"TEST\path\to\something.test");
            var folderPath = new CaseAwarePath(@"TesT\Path\");

            filePath.IsRelativeTo(folderPath).Should().BeTrue();

            string relativePath = filePath.RelativeTo(folderPath);
            relativePath.ToString().Should().Be(@"to\something.test");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void RelativeTo_ShouldWorkWithAbsolutePaths()
        {
            // Python test: test_relative_to_abspath
            var filePath = new CaseAwarePath(@"C:\TEST\path\to\something.test");
            var folderPath = new CaseAwarePath(@"C:\TesT\Path\");

            filePath.IsRelativeTo(folderPath).Should().BeTrue();

            string relativePath = filePath.RelativeTo(folderPath);
            relativePath.ToString().Should().Be(@"to\something.test");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void RelativeTo_ShouldBeCaseSensitiveWhenExactCase()
        {
            // Python test: test_relative_to_relpath_case_sensitive & test_relative_to_abspath_case_sensitive
            var filePath1 = new CaseAwarePath(@"TEST\path\to\something.test");
            var folderPath1 = new CaseAwarePath(@"TEST\path\");
            filePath1.IsRelativeTo(folderPath1).Should().BeTrue();

            var filePath2 = new CaseAwarePath(@"C:\TEST\path\to\something.test");
            var folderPath2 = new CaseAwarePath(@"C:\TEST\path\");
            filePath2.IsRelativeTo(folderPath2).Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void IsRelativeTo_BasicTest()
        {
            // Python test: test_basic (TestIsRelativeTo)
            var p1 = new CaseAwarePath("/usr/local/bin");
            var p2 = new CaseAwarePath("/usr/local");
            p1.IsRelativeTo(p2).Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void IsRelativeTo_DifferentPaths()
        {
            // Python test: test_different_paths
            var p1 = new CaseAwarePath("/usr/local/bin");
            var p2 = new CaseAwarePath("/etc");
            p1.IsRelativeTo(p2).Should().BeFalse();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void IsRelativeTo_RelativePaths()
        {
            // Python test: test_relative_paths
            var p1 = new CaseAwarePath("docs/file.txt");
            var p2 = new CaseAwarePath("docs");
            p1.IsRelativeTo(p2).Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void IsRelativeTo_CaseInsensitive()
        {
            // Python test: test_case_insensitive
            var p1 = new CaseAwarePath("/User/Docs");
            var p2 = new CaseAwarePath("/user/docs");
            p1.IsRelativeTo(p2).Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void IsRelativeTo_NotPathType()
        {
            // Python test: test_not_path
            var p1 = new CaseAwarePath("/home");
            string p2 = "/home";
            p1.IsRelativeTo(p2).Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void IsRelativeTo_SamePath()
        {
            // Python test: test_same_path
            var p1 = new CaseAwarePath("/home/user");
            var p2 = new CaseAwarePath("/home/user");
            p1.IsRelativeTo(p2).Should().BeTrue();
        }

        #endregion

        #region Path Normalization

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void StrNorm_ShouldNormalizeSlashes()
        {
            // Python test: test_fix_path_formatting
            CaseAwarePath.StrNorm("C:/path//to/dir/", "\\").Should().Be(@"C:\path\to\dir");
            CaseAwarePath.StrNorm("C:/path//to/dir/", "/").Should().Be("C:/path/to/dir");
            CaseAwarePath.StrNorm(@"\path//to/dir/", "\\").Should().Be(@"\path\to\dir");
            CaseAwarePath.StrNorm(@"\path//to/dir/", "/").Should().Be("/path/to/dir");
            CaseAwarePath.StrNorm("/path//to/dir/", "\\").Should().Be(@"\path\to\dir");
            CaseAwarePath.StrNorm("/path//to/dir/", "/").Should().Be("/path/to/dir");
        }

        #endregion

        #region Split Filename

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SplitFilename_Normal()
        {
            // Python test: test_normal (TestSplitFilename)
            var path = new CaseAwarePath("file.txt");
            (string stem, string ext) = path.SplitFilename();

            stem.Should().Be("file");
            ext.Should().Be("txt");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SplitFilename_MultipleDots()
        {
            // Python test: test_multiple_dots
            var path1 = new CaseAwarePath("file.with.dots.txt");
            (string stem1, string ext1) = path1.SplitFilename(dots: 2);
            stem1.Should().Be("file.with");
            ext1.Should().Be("dots.txt");

            var path2 = new CaseAwarePath("test.asdf.qwerty.tlk.xml");
            (string stem2, string ext2) = path2.SplitFilename(dots: 2);
            stem2.Should().Be("test.asdf.qwerty");
            ext2.Should().Be("tlk.xml");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SplitFilename_NoDots()
        {
            // Python test: test_no_dots
            var path = new CaseAwarePath("filename");
            (string stem, string ext) = path.SplitFilename();

            stem.Should().Be("filename");
            ext.Should().Be("");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SplitFilename_NegativeDots()
        {
            // Python test: test_negative_dots
            var path = new CaseAwarePath("left.right.txt");
            (string stem, string ext) = path.SplitFilename(dots: -1);

            stem.Should().Be("right.txt");
            ext.Should().Be("left");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SplitFilename_MoreDotsThanParts()
        {
            // Python test: test_more_dots_than_parts
            var path = new CaseAwarePath("file.txt");

            (string stem1, string ext1) = path.SplitFilename(dots: 3);
            stem1.Should().Be("file");
            ext1.Should().Be("txt");

            (string stem2, string ext2) = path.SplitFilename(dots: -3);
            stem2.Should().Be("file");
            ext2.Should().Be("txt");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SplitFilename_InvalidDots()
        {
            // Python test: test_invalid_dots
            var path = new CaseAwarePath("file.txt");
            Func<(string stem, string ext)> act = () => path.SplitFilename(dots: 0);

            act.Should().Throw<ArgumentException>();
        }

        #endregion

        #region Mixed Slash Handling - Edge Cases

        [Theory(Timeout = 120000)] // 2 minutes timeout
        [InlineData("C:/", "C:")]
        [InlineData("C:\\", "C:")]
        [InlineData("C:", "C:")]
        [InlineData("C:/Users/test/", "C:/Users/test")]
        [InlineData("C:/Users/test\\", "C:/Users/test")]
        [InlineData("C://Users///test", "C:/Users/test")]
        [InlineData("C:/Users/TEST/", "C:/Users/TEST")]
        public void PathNormalization_EdgeCases(string input, string expected)
        {
            // Python test: test_custom_path_edge_cases_os_specific_case_aware_path (selected cases)
            var path = new CaseAwarePath(input);
            string normalized = path.ToString().Replace("\\", "/");
            normalized.Should().Be(expected);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void PathNormalization_PreservesCase()
        {
            // Python test: Various edge case tests
            var path1 = new CaseAwarePath("C:/Users/TEST/");
            var path2 = new CaseAwarePath("C:/users/test/");

            // Should be equal case-insensitively
            path1.Should().Be(path2);

            // But original casing should be available
            path1.ToString().Should().Contain("TEST");
            path2.ToString().Should().Contain("test");
        }

        #endregion

        #region TrueDivision Operator

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TrueDivEquivalentToJoinPath()
        {
            // Python test: test_truediv_equivalent_to_joinpath
            var path1 = new CaseAwarePath("someDir");
            var path2 = new CaseAwarePath("someFile.txt");

            (path1 / path2).Should().Be(path1.JoinPath(path2));
        }

        #endregion

        #region Windows-Specific Path Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        [Trait("Category", "WindowsOnly")]
        public void WindowsPath_HandlesUNCPaths()
        {
            // Python test: Various UNC path tests
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                // Skip on non-Windows
                return;
            }

            var path1 = new CaseAwarePath(@"\\server\folder");
            path1.ToString().Should().StartWith(@"\\server");

            var path2 = new CaseAwarePath(@"\\wsl.localhost\path\to\file");
            path2.ToString().Should().Contain("wsl.localhost");
        }

        #endregion
    }
}

