using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuroraEngine.Common.Common;
using AuroraEngine.Common.Extract;
using AuroraEngine.Common.Formats.Capsule;
using AuroraEngine.Common.Installation;
using AuroraEngine.Common.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;
using ResourceResult = AuroraEngine.Common.Installation.ResourceResult;
using LocationResult = AuroraEngine.Common.Resources.LocationResult;

namespace HolocronToolset.NET.Tests.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:45
    // Original: class TestHTInstallation(TestCase):
    [Collection("Avalonia Test Collection")]
    public class HTInstallationTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        public HTInstallationTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:46-50
        // Original: @classmethod def setUpClass(cls):
        static HTInstallationTests()
        {
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            if (!string.IsNullOrEmpty(k1Path) && File.Exists(Path.Combine(k1Path, "chitin.key")))
            {
                _installation = new HTInstallation(k1Path, "Test");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:52-89
        // Original: def test_resource(self):
        [Fact]
        public void TestResource()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:55
            // Original: assert installation.resource("c_bantha", ResourceType.UTC, []) is None
            _installation.Resource("c_bantha", ResourceType.UTC, new SearchLocation[0]).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:56
            // Original: assert installation.resource("c_bantha", ResourceType.UTC) is not None
            _installation.Resource("c_bantha", ResourceType.UTC).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:58
            // Original: assert installation.resource("c_bantha", ResourceType.UTC, [SearchLocation.CHITIN]) is not None
            _installation.Resource("c_bantha", ResourceType.UTC, new[] { SearchLocation.CHITIN }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:59
            // Original: assert installation.resource("xxx", ResourceType.UTC, [SearchLocation.CHITIN]) is None
            _installation.Resource("xxx", ResourceType.UTC, new[] { SearchLocation.CHITIN }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:60
            // Original: assert installation.resource("m13aa", ResourceType.ARE, [SearchLocation.MODULES]) is not None
            _installation.Resource("m13aa", ResourceType.ARE, new[] { SearchLocation.MODULES }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:61
            // Original: assert installation.resource("xxx", ResourceType.ARE, [SearchLocation.MODULES]) is None
            _installation.Resource("xxx", ResourceType.ARE, new[] { SearchLocation.MODULES }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:62
            // Original: assert installation.resource("xxx", ResourceType.NSS, [SearchLocation.OVERRIDE]) is None
            _installation.Resource("xxx", ResourceType.NSS, new[] { SearchLocation.OVERRIDE }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:63
            // Original: assert installation.resource("NM03ABCITI06004_", ResourceType.WAV, [SearchLocation.VOICE]) is not None
            _installation.Resource("NM03ABCITI06004_", ResourceType.WAV, new[] { SearchLocation.VOICE }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:64
            // Original: assert installation.resource("xxx", ResourceType.WAV, [SearchLocation.VOICE]) is None
            _installation.Resource("xxx", ResourceType.WAV, new[] { SearchLocation.VOICE }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:65
            // Original: assert installation.resource("P_hk47_POIS", ResourceType.WAV, [SearchLocation.SOUND]) is not None
            _installation.Resource("P_hk47_POIS", ResourceType.WAV, new[] { SearchLocation.SOUND }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:66
            // Original: assert installation.resource("xxx", ResourceType.WAV, [SearchLocation.SOUND]) is None
            _installation.Resource("xxx", ResourceType.WAV, new[] { SearchLocation.SOUND }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:67
            // Original: assert installation.resource("mus_theme_carth", ResourceType.WAV, [SearchLocation.MUSIC]) is not None
            _installation.Resource("mus_theme_carth", ResourceType.WAV, new[] { SearchLocation.MUSIC }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:68
            // Original: assert installation.resource("xxx", ResourceType.WAV, [SearchLocation.MUSIC]) is None
            _installation.Resource("xxx", ResourceType.WAV, new[] { SearchLocation.MUSIC }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:69
            // Original: assert installation.resource("n_gendro_coms1", ResourceType.LIP, [SearchLocation.LIPS]) is not None
            _installation.Resource("n_gendro_coms1", ResourceType.LIP, new[] { SearchLocation.LIPS }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:70
            // Original: assert installation.resource("xxx", ResourceType.LIP, [SearchLocation.LIPS]) is None
            _installation.Resource("xxx", ResourceType.LIP, new[] { SearchLocation.LIPS }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:73
            // Original: assert installation.resource("blood", ResourceType.TPC, [SearchLocation.TEXTURES_TPA]) is not None
            _installation.Resource("blood", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPA }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:74
            // Original: assert installation.resource("xxx", ResourceType.TPC, [SearchLocation.TEXTURES_TPA]) is None
            _installation.Resource("xxx", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPA }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:75
            // Original: assert installation.resource("blood", ResourceType.TPC, [SearchLocation.TEXTURES_TPB]) is not None
            _installation.Resource("blood", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPB }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:76
            // Original: assert installation.resource("xxx", ResourceType.TPC, [SearchLocation.TEXTURES_TPB]) is None
            _installation.Resource("xxx", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPB }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:77
            // Original: assert installation.resource("blood", ResourceType.TPC, [SearchLocation.TEXTURES_TPC]) is not None
            _installation.Resource("blood", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPC }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:78
            // Original: assert installation.resource("xxx", ResourceType.TPC, [SearchLocation.TEXTURES_TPC]) is None
            _installation.Resource("xxx", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPC }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:79
            // Original: assert installation.resource("PO_PCarth", ResourceType.TPC, [SearchLocation.TEXTURES_GUI]) is not None
            _installation.Resource("PO_PCarth", ResourceType.TPC, new[] { SearchLocation.TEXTURES_GUI }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:80
            // Original: assert installation.resource("xxx", ResourceType.TPC, [SearchLocation.TEXTURES_GUI]) is None
            _installation.Resource("xxx", ResourceType.TPC, new[] { SearchLocation.TEXTURES_GUI }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:82-89
            // Original: resource = installation.resource("m13aa", ResourceType.ARE, [SearchLocation.CUSTOM_MODULES], capsules=[Capsule(...)])
            string modulePath = _installation.ModulePath();
            string rimPath = Path.Combine(modulePath, "danm13.rim");
            if (File.Exists(rimPath))
            {
                var capsule = new LazyCapsule(rimPath);
                var resource = _installation.Resource("m13aa", ResourceType.ARE, new[] { SearchLocation.CUSTOM_MODULES }, new List<LazyCapsule> { capsule });
                resource.Should().NotBeNull();
                resource.Data.Should().NotBeNull();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:91-166
        // Original: def test_resources(self):
        [Fact]
        public void TestResources()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:94-99
            // Original: chitin_resources = [ResourceIdentifier.from_path("c_bantha.utc"), ResourceIdentifier.from_path("x.utc")]
            var chitinResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("c_bantha.utc"),
                ResourceIdentifier.FromPath("x.utc")
            };
            var chitinResults = _installation.Resources(chitinResources, new[] { SearchLocation.CHITIN });
            AssertFromPathTests(chitinResults, "c_bantha.utc", "x.utc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:100-105
            // Original: modules_resources = [ResourceIdentifier.from_path("m01aa.are"), ResourceIdentifier.from_path("x.tpc")]
            var modulesResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("m01aa.are"),
                ResourceIdentifier.FromPath("x.tpc")
            };
            var modulesResults = _installation.Resources(modulesResources, new[] { SearchLocation.MODULES });
            AssertFromPathTests(modulesResults, "m01aa.are", "x.tpc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:106-111
            // Original: voices_resources = [ResourceIdentifier.from_path("NM17AE04NI04008_.wav"), ResourceIdentifier.FromPath("x.mp3")]
            var voicesResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("NM17AE04NI04008_.wav"),
                ResourceIdentifier.FromPath("x.mp3")
            };
            var voicesResults = _installation.Resources(voicesResources, new[] { SearchLocation.VOICE });
            AssertFromPathTests(voicesResults, "NM17AE04NI04008_.wav", "x.mp3");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:112-117
            // Original: music_resources = [ResourceIdentifier.from_path("mus_theme_carth.wav"), ResourceIdentifier.from_path("x.mp3")]
            var musicResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("mus_theme_carth.wav"),
                ResourceIdentifier.FromPath("x.mp3")
            };
            var musicResults = _installation.Resources(musicResources, new[] { SearchLocation.MUSIC });
            AssertFromPathTests(musicResults, "mus_theme_carth.wav", "x.mp3");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:118-123
            // Original: sounds_resources = [ResourceIdentifier.from_path("P_ZAALBAR_POIS.wav"), ResourceIdentifier.from_path("x.mp3")]
            var soundsResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("P_ZAALBAR_POIS.wav"),
                ResourceIdentifier.FromPath("x.mp3")
            };
            var soundsResults = _installation.Resources(soundsResources, new[] { SearchLocation.SOUND });
            AssertFromPathTests(soundsResults, "P_ZAALBAR_POIS.wav", "x.mp3");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:124-129
            // Original: lips_resources = [ResourceIdentifier.from_path("n_gendro_coms1.lip"), ResourceIdentifier.from_path("x.lip")]
            var lipsResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("n_gendro_coms1.lip"),
                ResourceIdentifier.FromPath("x.lip")
            };
            var lipsResults = _installation.Resources(lipsResources, new[] { SearchLocation.LIPS });
            AssertFromPathTests(lipsResults, "n_gendro_coms1.lip", "x.lip");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:136-141
            // Original: texa_resources = [ResourceIdentifier.from_path("blood.tpc"), ResourceIdentifier.from_path("x.tpc")]
            var texaResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("blood.tpc"),
                ResourceIdentifier.FromPath("x.tpc")
            };
            var texaResults = _installation.Resources(texaResources, new[] { SearchLocation.TEXTURES_TPA });
            AssertFromPathTests(texaResults, "blood.tpc", "x.tpc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:142-147
            // Original: texb_resources = [ResourceIdentifier.from_path("blood.tpc"), ResourceIdentifier.from_path("x.tpc")]
            var texbResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("blood.tpc"),
                ResourceIdentifier.FromPath("x.tpc")
            };
            var texbResults = _installation.Resources(texbResources, new[] { SearchLocation.TEXTURES_TPB });
            AssertFromPathTests(texbResults, "blood.tpc", "x.tpc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:148-153
            // Original: texc_resources = [ResourceIdentifier.from_path("blood.tpc"), ResourceIdentifier.from_path("x.tpc")]
            var texcResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("blood.tpc"),
                ResourceIdentifier.FromPath("x.tpc")
            };
            var texcResults = _installation.Resources(texcResources, new[] { SearchLocation.TEXTURES_TPC });
            AssertFromPathTests(texcResults, "blood.tpc", "x.tpc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:154-159
            // Original: texg_resources = [ResourceIdentifier.from_path("1024x768back.tpc"), ResourceIdentifier.from_path("x.tpc")]
            var texgResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("1024x768back.tpc"),
                ResourceIdentifier.FromPath("x.tpc")
            };
            var texgResults = _installation.Resources(texgResources, new[] { SearchLocation.TEXTURES_GUI });
            AssertFromPathTests(texgResults, "1024x768back.tpc", "x.tpc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:160-166
            // Original: capsules = [Capsule(installation.module_path() / "danm13.rim")]
            string modulePath = _installation.ModulePath();
            string rimPath = Path.Combine(modulePath, "danm13.rim");
            if (File.Exists(rimPath))
            {
                var capsules = new List<LazyCapsule> { new LazyCapsule(rimPath) };
                var capsulesResources = new List<ResourceIdentifier>
                {
                    ResourceIdentifier.FromPath("m13aa.are"),
                    ResourceIdentifier.FromPath("xyz.ifo")
                };
                var capsulesResults = _installation.Resources(capsulesResources, new[] { SearchLocation.CUSTOM_MODULES }, capsules);
                AssertFromPathTests(capsulesResults, "m13aa.are", "xyz.ifo");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:168-198
        // Original: def test_location(self):
        [Fact]
        public void TestLocation()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:171
            // Original: assert not installation.location("m13aa", ResourceType.ARE, [])
            _installation.Location("m13aa", ResourceType.ARE, new SearchLocation[0]).Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:172
            // Original: assert installation.location("m13aa", ResourceType.ARE)
            _installation.Location("m13aa", ResourceType.ARE).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:174
            // Original: assert installation.location("m13aa", ResourceType.ARE, [SearchLocation.MODULES])
            _installation.Location("m13aa", ResourceType.ARE, new[] { SearchLocation.MODULES }).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:176
            // Original: assert installation.location("c_bantha", ResourceType.UTC, [SearchLocation.CHITIN])
            _installation.Location("c_bantha", ResourceType.UTC, new[] { SearchLocation.CHITIN }).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:177
            // Original: assert not installation.location("xxx", ResourceType.UTC, [SearchLocation.CHITIN])
            _installation.Location("xxx", ResourceType.UTC, new[] { SearchLocation.CHITIN }).Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:178
            // Original: assert installation.location("m13aa", ResourceType.ARE, [SearchLocation.MODULES])
            _installation.Location("m13aa", ResourceType.ARE, new[] { SearchLocation.MODULES }).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:179
            // Original: assert not installation.location("xxx", ResourceType.ARE, [SearchLocation.MODULES])
            _installation.Location("xxx", ResourceType.ARE, new[] { SearchLocation.MODULES }).Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:180
            // Original: assert not installation.location("xxx", ResourceType.NSS, [SearchLocation.OVERRIDE])
            _installation.Location("xxx", ResourceType.NSS, new[] { SearchLocation.OVERRIDE }).Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:181
            // Original: assert installation.location("NM03ABCITI06004_", ResourceType.WAV, [SearchLocation.VOICE])
            _installation.Location("NM03ABCITI06004_", ResourceType.WAV, new[] { SearchLocation.VOICE }).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:182
            // Original: assert not installation.location("xxx", ResourceType.WAV, [SearchLocation.VOICE])
            _installation.Location("xxx", ResourceType.WAV, new[] { SearchLocation.VOICE }).Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:183
            // Original: assert installation.location("P_hk47_POIS", ResourceType.WAV, [SearchLocation.SOUND])
            _installation.Location("P_hk47_POIS", ResourceType.WAV, new[] { SearchLocation.SOUND }).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:184
            // Original: assert not installation.location("xxx", ResourceType.WAV, [SearchLocation.SOUND])
            _installation.Location("xxx", ResourceType.WAV, new[] { SearchLocation.SOUND }).Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:185
            // Original: assert installation.location("mus_theme_carth", ResourceType.WAV, [SearchLocation.MUSIC])
            _installation.Location("mus_theme_carth", ResourceType.WAV, new[] { SearchLocation.MUSIC }).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:186
            // Original: assert not installation.location("xxx", ResourceType.WAV, [SearchLocation.MUSIC])
            _installation.Location("xxx", ResourceType.WAV, new[] { SearchLocation.MUSIC }).Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:187
            // Original: assert installation.location("n_gendro_coms1", ResourceType.LIP, [SearchLocation.LIPS])
            _installation.Location("n_gendro_coms1", ResourceType.LIP, new[] { SearchLocation.LIPS }).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:188
            // Original: assert not installation.location("xxx", ResourceType.LIP, [SearchLocation.LIPS])
            _installation.Location("xxx", ResourceType.LIP, new[] { SearchLocation.LIPS }).Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:191
            // Original: assert installation.location("blood", ResourceType.TPC, [SearchLocation.TEXTURES_TPA])
            _installation.Location("blood", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPA }).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:192
            // Original: assert not installation.location("xxx", ResourceType.TPC, [SearchLocation.TEXTURES_TPA])
            _installation.Location("xxx", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPA }).Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:193
            // Original: assert installation.location("blood", ResourceType.TPC, [SearchLocation.TEXTURES_TPB])
            _installation.Location("blood", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPB }).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:194
            // Original: assert not installation.location("xxx", ResourceType.TPC, [SearchLocation.TEXTURES_TPB])
            _installation.Location("xxx", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPB }).Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:195
            // Original: assert installation.location("blood", ResourceType.TPC, [SearchLocation.TEXTURES_TPC])
            _installation.Location("blood", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPC }).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:196
            // Original: assert not installation.location("xxx", ResourceType.TPC, [SearchLocation.TEXTURES_TPC])
            _installation.Location("xxx", ResourceType.TPC, new[] { SearchLocation.TEXTURES_TPC }).Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:197
            // Original: assert installation.location("PO_PCarth", ResourceType.TPC, [SearchLocation.TEXTURES_GUI])
            _installation.Location("PO_PCarth", ResourceType.TPC, new[] { SearchLocation.TEXTURES_GUI }).Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:198
            // Original: assert not installation.location("xxx", ResourceType.TPC, [SearchLocation.TEXTURES_GUI])
            _installation.Location("xxx", ResourceType.TPC, new[] { SearchLocation.TEXTURES_GUI }).Should().BeEmpty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:200-275
        // Original: def test_locations(self):
        [Fact]
        public void TestLocations()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:203-208
            // Original: chitin_resources = [ResourceIdentifier.from_path("c_bantha.utc"), ResourceIdentifier.from_path("x.utc")]
            var chitinResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("c_bantha.utc"),
                ResourceIdentifier.FromPath("x.utc")
            };
            var chitinResults = _installation.Locations(chitinResources, new[] { SearchLocation.CHITIN });
            AssertFromPathTests(chitinResults, "c_bantha.utc", "x.utc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:209-214
            // Original: modules_resources = [ResourceIdentifier.from_path("m01aa.are"), ResourceIdentifier.from_path("x.tpc")]
            var modulesResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("m01aa.are"),
                ResourceIdentifier.FromPath("x.tpc")
            };
            var modulesResults = _installation.Locations(modulesResources, new[] { SearchLocation.MODULES });
            AssertFromPathTests(modulesResults, "m01aa.are", "x.tpc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:215-220
            // Original: voices_resources = [ResourceIdentifier.from_path("NM17AE04NI04008_.wav"), ResourceIdentifier.from_path("x.mp3")]
            var voicesResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("NM17AE04NI04008_.wav"),
                ResourceIdentifier.FromPath("x.mp3")
            };
            var voicesResults = _installation.Locations(voicesResources, new[] { SearchLocation.VOICE });
            AssertFromPathTests(voicesResults, "NM17AE04NI04008_.wav", "x.mp3");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:221-226
            // Original: music_resources = [ResourceIdentifier.from_path("mus_theme_carth.wav"), ResourceIdentifier.from_path("x.mp3")]
            var musicResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("mus_theme_carth.wav"),
                ResourceIdentifier.FromPath("x.mp3")
            };
            var musicResults = _installation.Locations(musicResources, new[] { SearchLocation.MUSIC });
            AssertFromPathTests(musicResults, "mus_theme_carth.wav", "x.mp3");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:227-232
            // Original: sounds_resources = [ResourceIdentifier.from_path("P_ZAALBAR_POIS.wav"), ResourceIdentifier.from_path("x.mp3")]
            var soundsResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("P_ZAALBAR_POIS.wav"),
                ResourceIdentifier.FromPath("x.mp3")
            };
            var soundsResults = _installation.Locations(soundsResources, new[] { SearchLocation.SOUND });
            AssertFromPathTests(soundsResults, "P_ZAALBAR_POIS.wav", "x.mp3");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:233-238
            // Original: lips_resources = [ResourceIdentifier.from_path("n_gendro_coms1.lip"), ResourceIdentifier.from_path("x.lip")]
            var lipsResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("n_gendro_coms1.lip"),
                ResourceIdentifier.FromPath("x.lip")
            };
            var lipsResults = _installation.Locations(lipsResources, new[] { SearchLocation.LIPS });
            AssertFromPathTests(lipsResults, "n_gendro_coms1.lip", "x.lip");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:245-250
            // Original: texa_resources = [ResourceIdentifier.from_path("blood.tpc"), ResourceIdentifier.from_path("x.tpc")]
            var texaResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("blood.tpc"),
                ResourceIdentifier.FromPath("x.tpc")
            };
            var texaResults = _installation.Locations(texaResources, new[] { SearchLocation.TEXTURES_TPA });
            AssertFromPathTests(texaResults, "blood.tpc", "x.tpc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:251-256
            // Original: texb_resources = [ResourceIdentifier.from_path("blood.tpc"), ResourceIdentifier.from_path("x.tpc")]
            var texbResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("blood.tpc"),
                ResourceIdentifier.FromPath("x.tpc")
            };
            var texbResults = _installation.Locations(texbResources, new[] { SearchLocation.TEXTURES_TPB });
            AssertFromPathTests(texbResults, "blood.tpc", "x.tpc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:257-262
            // Original: texc_resources = [ResourceIdentifier.from_path("blood.tpc"), ResourceIdentifier.from_path("x.tpc")]
            var texcResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("blood.tpc"),
                ResourceIdentifier.FromPath("x.tpc")
            };
            var texcResults = _installation.Locations(texcResources, new[] { SearchLocation.TEXTURES_TPC });
            AssertFromPathTests(texcResults, "blood.tpc", "x.tpc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:263-268
            // Original: texg_resources = [ResourceIdentifier.from_path("1024x768back.tpc"), ResourceIdentifier.from_path("x.tpc")]
            var texgResources = new List<ResourceIdentifier>
            {
                ResourceIdentifier.FromPath("1024x768back.tpc"),
                ResourceIdentifier.FromPath("x.tpc")
            };
            var texgResults = _installation.Locations(texgResources, new[] { SearchLocation.TEXTURES_GUI });
            AssertFromPathTests(texgResults, "1024x768back.tpc", "x.tpc");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:269-275
            // Original: capsules = [Capsule(installation.module_path() / "danm13.rim")]
            string modulePath = _installation.ModulePath();
            string rimPath = Path.Combine(modulePath, "danm13.rim");
            if (File.Exists(rimPath))
            {
                var capsules = new List<LazyCapsule> { new LazyCapsule(rimPath) };
                var capsulesResources = new List<ResourceIdentifier>
                {
                    ResourceIdentifier.FromPath("m13aa.are"),
                    ResourceIdentifier.FromPath("xyz.ifo")
                };
                var capsulesResults = _installation.Locations(capsulesResources, new[] { SearchLocation.CUSTOM_MODULES }, capsules);
                AssertFromPathTests(capsulesResults, "m13aa.are", "xyz.ifo");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:278-281
        // Original: def _assert_from_path_tests(self, arg0, arg1, arg2):
        private void AssertFromPathTests(Dictionary<ResourceIdentifier, ResourceResult> results, string arg1, string arg2)
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:279
            // Original: assert arg0[ResourceIdentifier.from_path(arg1)]
            results[ResourceIdentifier.FromPath(arg1)].Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:280
            // Original: assert not arg0[ResourceIdentifier.from_path(arg2)]
            results[ResourceIdentifier.FromPath(arg2)].Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:281
            // Original: assert len(arg0) == 2
            results.Count.Should().Be(2);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:278-281
        // Original: def _assert_from_path_tests(self, arg0, arg1, arg2):
        private void AssertFromPathTests(Dictionary<ResourceIdentifier, List<LocationResult>> results, string arg1, string arg2)
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:279
            // Original: assert arg0[ResourceIdentifier.from_path(arg1)]
            results[ResourceIdentifier.FromPath(arg1)].Should().NotBeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:280
            // Original: assert not arg0[ResourceIdentifier.from_path(arg2)]
            results[ResourceIdentifier.FromPath(arg2)].Should().BeEmpty();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:281
            // Original: assert len(arg0) == 2
            results.Count.Should().Be(2);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:283-299
        // Original: def test_texture(self):
        [Fact]
        public void TestTexture()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:286
            // Original: assert installation.texture("m03ae_03a_lm4", [SearchLocation.CHITIN]) is not None
            _installation.Texture("m03ae_03a_lm4", new[] { SearchLocation.CHITIN }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:287
            // Original: assert installation.texture("x", [SearchLocation.CHITIN]) is None
            _installation.Texture("x", new[] { SearchLocation.CHITIN }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:289
            // Original: assert installation.texture("LEH_FLOOR01", [SearchLocation.TEXTURES_TPA]) is not None
            _installation.Texture("LEH_FLOOR01", new[] { SearchLocation.TEXTURES_TPA }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:290
            // Original: assert installation.texture("x", [SearchLocation.TEXTURES_TPA]) is None
            _installation.Texture("x", new[] { SearchLocation.TEXTURES_TPA }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:292
            // Original: assert installation.texture("LEH_Floor01", [SearchLocation.TEXTURES_TPB]) is not None
            _installation.Texture("LEH_Floor01", new[] { SearchLocation.TEXTURES_TPB }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:293
            // Original: assert installation.texture("x", [SearchLocation.TEXTURES_TPB]) is None
            _installation.Texture("x", new[] { SearchLocation.TEXTURES_TPB }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:295
            // Original: assert installation.texture("leh_floor01", [SearchLocation.TEXTURES_TPC]) is not None
            _installation.Texture("leh_floor01", new[] { SearchLocation.TEXTURES_TPC }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:296
            // Original: assert installation.texture("x", [SearchLocation.TEXTURES_TPC]) is None
            _installation.Texture("x", new[] { SearchLocation.TEXTURES_TPC }).Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:298
            // Original: assert installation.texture("bluearrow", [SearchLocation.TEXTURES_GUI]) is not None
            _installation.Texture("bluearrow", new[] { SearchLocation.TEXTURES_GUI }).Should().NotBeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:299
            // Original: assert installation.texture("x", [SearchLocation.TEXTURES_GUI]) is None
            _installation.Texture("x", new[] { SearchLocation.TEXTURES_GUI }).Should().BeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:301-332
        // Original: def test_textures(self):
        [Fact]
        public void TestTextures()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:304-308
            // Original: chitin_textures = ["m03ae_03a_lm4", "x"]
            var chitinTextures = new List<string> { "m03ae_03a_lm4", "x" };
            var chitinResults = _installation.Textures(chitinTextures, new[] { SearchLocation.CHITIN });
            chitinResults["m03ae_03a_lm4"].Should().NotBeNull();
            chitinResults["x"].Should().BeNull();
            chitinResults.Count.Should().Be(2);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:310-314
            // Original: tpa_textures = ["LEH_Floor01", "x"]
            var tpaTextures = new List<string> { "LEH_Floor01", "x" };
            var tpaResults = _installation.Textures(tpaTextures, new[] { SearchLocation.TEXTURES_TPA });
            tpaResults["leh_floor01"].Should().NotBeNull();
            tpaResults["x"].Should().BeNull();
            tpaResults.Count.Should().Be(2);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:316-320
            // Original: tpb_textures = ["LEH_Floor01", "x"]
            var tpbTextures = new List<string> { "LEH_Floor01", "x" };
            var tpbResults = _installation.Textures(tpbTextures, new[] { SearchLocation.TEXTURES_TPB });
            tpbResults["leh_floor01"].Should().NotBeNull();
            tpbResults["x"].Should().BeNull();
            tpbResults.Count.Should().Be(2);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:322-326
            // Original: tpc_textures = ["LEH_Floor01", "x"]
            var tpcTextures = new List<string> { "LEH_Floor01", "x" };
            var tpcResults = _installation.Textures(tpcTextures, new[] { SearchLocation.TEXTURES_TPC });
            tpcResults["leh_floor01"].Should().NotBeNull();
            tpcResults["x"].Should().BeNull();
            tpcResults.Count.Should().Be(2);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:328-332
            // Original: gui_textures = ["bluearrow", "x"]
            var guiTextures = new List<string> { "bluearrow", "x" };
            var guiResults = _installation.Textures(guiTextures, new[] { SearchLocation.TEXTURES_GUI });
            guiResults["bluearrow"].Should().NotBeNull();
            guiResults["x"].Should().BeNull();
            guiResults.Count.Should().Be(2);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:334-360
        // Original: def test_sounds(self):
        [Fact]
        public void TestSounds()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:337-340
            // Original: chitin_sounds = ["as_an_dantext_01", "x"]
            var chitinSounds = new List<string> { "as_an_dantext_01", "x" };
            var chitinResults = _installation.Sounds(chitinSounds, new[] { SearchLocation.CHITIN });
            chitinResults["as_an_dantext_01"].Should().NotBeNull();
            chitinResults["x"].Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:347-350
            // Original: sound_sounds = ["al_an_flybuzz_01", "x"]
            var soundSounds = new List<string> { "al_an_flybuzz_01", "x" };
            var soundResults = _installation.Sounds(soundSounds, new[] { SearchLocation.SOUND });
            soundResults["al_an_flybuzz_01"].Should().NotBeNull();
            soundResults["x"].Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:352-355
            // Original: music_sounds = ["al_en_cityext", "x"]
            var musicSounds = new List<string> { "al_en_cityext", "x" };
            var musicResults = _installation.Sounds(musicSounds, new[] { SearchLocation.MUSIC });
            musicResults["al_en_cityext"].Should().NotBeNull();
            musicResults["x"].Should().BeNull();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:357-360
            // Original: voice_sounds = ["n_gengamm_scrm", "x"]
            var voiceSounds = new List<string> { "n_gengamm_scrm", "x" };
            var voiceResults = _installation.Sounds(voiceSounds, new[] { SearchLocation.VOICE });
            voiceResults["n_gengamm_scrm"].Should().NotBeNull();
            voiceResults["x"].Should().BeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:362-372
        // Original: def test_string(self):
        [Fact]
        public void TestString()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:366
            // Original: locstring1 = LocalizedString.from_invalid()
            var locstring1 = LocalizedString.FromInvalid();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:367
            // Original: locstring2 = LocalizedString.from_english("Some text.")
            var locstring2 = LocalizedString.FromEnglish("Some text.");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:368
            // Original: locstring3 = LocalizedString(2)
            var locstring3 = new LocalizedString(2);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:370
            // Original: assert installation.string(locstring1, "default text") == "default text"
            _installation.String(locstring1, "default text").Should().Be("default text");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:371
            // Original: assert installation.string(locstring2, "default text") == "Some text."
            _installation.String(locstring2, "default text").Should().Be("Some text.");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:372
            // Original: assert installation.string(locstring3, "default text") == "ERROR: FATAL COMPILER ERROR"
            _installation.String(locstring3, "default text").Should().Be("ERROR: FATAL COMPILER ERROR");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:374-385
        // Original: def test_strings(self):
        [Fact]
        public void TestStrings()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:378
            // Original: locstring1 = LocalizedString.from_invalid()
            var locstring1 = LocalizedString.FromInvalid();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:379
            // Original: locstring2 = LocalizedString.from_english("Some text.")
            var locstring2 = LocalizedString.FromEnglish("Some text.");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:380
            // Original: locstring3 = LocalizedString(2)
            var locstring3 = new LocalizedString(2);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:382
            // Original: results = installation.strings([locstring1, locstring2, locstring3], "default text")
            var locstrings = new List<LocalizedString> { locstring1, locstring2, locstring3 };
            var results = _installation.Strings(locstrings, "default text");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:383
            // Original: assert results[locstring1] == "default text"
            results[locstring1].Should().Be("default text");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:384
            // Original: assert results[locstring2] == "Some text."
            results[locstring2].Should().Be("Some text.");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_htinstallation.py:385
            // Original: assert results[locstring3] == "ERROR: FATAL COMPILER ERROR"
            results[locstring3].Should().Be("ERROR: FATAL COMPILER ERROR");
        }
    }
}
