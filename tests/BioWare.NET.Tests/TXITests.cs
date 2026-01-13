using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Andastra.Parsing.Formats.TXI;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace Andastra.Parsing.Tests
{
    [TestFixture]
    public class TXIParseBlendingTests
    {
        [TestCase("default", true)]
        [TestCase("DEFAULT", true)]
        [TestCase("Default", true)]
        [TestCase("additive", true)]
        [TestCase("ADDITIVE", true)]
        [TestCase("Additive", true)]
        [TestCase("punchthrough", true)]
        [TestCase("PUNCHTHROUGH", true)]
        [TestCase("Punchthrough", true)]
        [TestCase("DeFaUlT", true)]
        [TestCase("AdDiTiVe", true)]
        [TestCase("PuNcHtHrOuGh", true)]
        [TestCase("invalid", false)]
        [TestCase("", false)]
        [TestCase("blend", false)]
        public void ParseBlending_MatchesPython(string input, bool expected)
        {
            bool result = TXI.ParseBlending(input);
            Assert.AreEqual(expected, result);
        }
    }

    [TestFixture]
    public class TXIReadWriteTests
    {
        private const string SampleTxiData = @"
    mipmap 0
    filter 0
    numchars 256
    fontheight 0.500000
    baselineheight 0.400000
    texturewidth 1.000000
    fontwidth 1.000000
    spacingR 0.002600
    spacingB 0.100000
    caretindent -0.010000
    isdoublebyte 0
    upperleftcoords 256
    0.000000 0.000000 0
    0.062500 0.000000 0
    0.125000 0.000000 0
    lowerrightcoords 256
    0.062500 0.125000 0
    0.125000 0.125000 0
    0.187500 0.125000 0
";

        private static TXI ReadSample()
        {
            using (var ms = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(SampleTxiData)))
            {
                return TXIAuto.ReadTxi(ms);
            }
        }

        [Test]
        public void ReadTxi_ParsesFeatures()
        {
            TXI txi = ReadSample();
            Assert.IsNotNull(txi);
            Assert.IsFalse(txi.Features.Mipmap ?? true);
            Assert.IsFalse(txi.Features.Filter ?? true);
            Assert.AreEqual(256, txi.Features.Numchars);
            Assert.AreEqual(0.5f, txi.Features.Fontheight.GetValueOrDefault(), 1e-6f);
            Assert.AreEqual(0.4f, txi.Features.Baselineheight.GetValueOrDefault(), 1e-6f);
            Assert.AreEqual(1.0f, txi.Features.Texturewidth.GetValueOrDefault(), 1e-6f);
            Assert.AreEqual(1.0f, txi.Features.Fontwidth.GetValueOrDefault(), 1e-6f);
            Assert.AreEqual(0.0026f, txi.Features.SpacingR.GetValueOrDefault(), 1e-6f);
            Assert.AreEqual(0.1f, txi.Features.SpacingB.GetValueOrDefault(), 1e-6f);
            Assert.AreEqual(-0.01f, txi.Features.Caretindent.GetValueOrDefault(), 1e-6f);

            Assert.IsNotNull(txi.Features.Upperleftcoords);
            Assert.AreEqual(256, txi.Features.Upperleftcoords.Count);
            Assert.AreEqual(0.0f, txi.Features.Upperleftcoords[0].Item1, 1e-6f);
            Assert.AreEqual(0.0625f, txi.Features.Upperleftcoords[1].Item1, 1e-6f);
            Assert.AreEqual(0.125f, txi.Features.Upperleftcoords[2].Item1, 1e-6f);

            Assert.IsNotNull(txi.Features.Lowerrightcoords);
            Assert.AreEqual(256, txi.Features.Lowerrightcoords.Count);
            Assert.AreEqual(0.0625f, txi.Features.Lowerrightcoords[0].Item1, 1e-6f);
            Assert.AreEqual(0.125f, txi.Features.Lowerrightcoords[1].Item1, 1e-6f);
            Assert.AreEqual(0.1875f, txi.Features.Lowerrightcoords[2].Item1, 1e-6f);
        }

        [Test]
        public void WriteTxi_RoundTrips()
        {
            TXI txi = ReadSample();
            byte[] bytes = TXIAuto.BytesTxi(txi);

            TXI written = TXIAuto.ReadTxi(bytes);
            Assert.IsFalse(written.Features.Mipmap ?? true);
            Assert.IsFalse(written.Features.Filter ?? true);
            Assert.AreEqual(txi.Features.Numchars, written.Features.Numchars);
            Assert.AreEqual(txi.Features.Fontheight.GetValueOrDefault(), written.Features.Fontheight.GetValueOrDefault(), 1e-6f);
            Assert.IsNotNull(written.Features.Upperleftcoords);
            Assert.AreEqual(txi.Features.Upperleftcoords.Count, written.Features.Upperleftcoords.Count);
            Assert.IsNotNull(written.Features.Lowerrightcoords);
            Assert.AreEqual(txi.Features.Lowerrightcoords.Count, written.Features.Lowerrightcoords.Count);
        }
    }
}

