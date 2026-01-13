using System;
using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.NCS;
using NUnit.Framework;

namespace Andastra.Tests.Formats
{
    [TestFixture]
    public sealed class NCSRoundtripTests
    {
        private static readonly string[] RoundtripScripts =
        {
            // Basic integer ops and loop (covers control flow + integer constants).
            "void main() { int i = 0; while (i < 3) { i = i + 1; } }",

            // Strings and floats (covers CONSTS/CONSTF paths).
            "void main() { float f = 1.5; string s = \"abc\"; f = f + 2.25; s = s + \"def\"; }",

            // Simple branching (covers conditional jumps).
            "void main() { int a = 1; if (a == 1) { a = 2; } else { a = 3; } }"
        };

        [TestCase(BioWareGame.K1)]
        [TestCase(BioWareGame.TSL)]
        public void CompileReadWrite_RoundtripsBytes_WithoutValidationIssues(BioWareGame game)
        {
            for (int i = 0; i < RoundtripScripts.Length; i++)
            {
                string source = RoundtripScripts[i];

                NCS compiled = NCSAuto.CompileNss(source, game);
                Assert.That(compiled, Is.Not.Null, "Compile returned null for script index " + i);

                List<string> issues = compiled.Validate();
                Assert.That(issues, Is.Empty, "Validation issues for compiled NCS (script index " + i + "):\n" + string.Join("\n", issues));

                byte[] bytes1 = NCSAuto.BytesNcs(compiled);
                Assert.That(bytes1, Is.Not.Null.And.Not.Empty, "BytesNcs returned empty bytes for script index " + i);

                NCS readBack = NCSAuto.ReadNcs(bytes1);
                Assert.That(readBack, Is.Not.Null, "ReadNcs returned null for script index " + i);

                List<string> issuesAfterRead = readBack.Validate();
                Assert.That(issuesAfterRead, Is.Empty, "Validation issues after ReadNcs (script index " + i + "):\n" + string.Join("\n", issuesAfterRead));

                byte[] bytes2 = NCSAuto.BytesNcs(readBack);

                Assert.That(bytes2, Is.Not.Null.And.Not.Empty, "Second BytesNcs returned empty bytes for script index " + i);
                Assert.That(bytes2, Is.EqualTo(bytes1), "Binary roundtrip mismatch for script index " + i);
            }
        }

        [TestCase(BioWareGame.K1)]
        [TestCase(BioWareGame.TSL)]
        public void CompileDecompileCompile_ProducesEquivalentProgram(BioWareGame game)
        {
            // This is intentionally a semantic comparison using NCS.Equals, not a strict byte-for-byte assertion,
            // because decompilation can legally emit different (but equivalent) source code.
            for (int i = 0; i < RoundtripScripts.Length; i++)
            {
                string source = RoundtripScripts[i];

                NCS first = NCSAuto.CompileNss(source, game);
                string decompiled = NCSAuto.DecompileNcs(first, game);

                Assert.That(decompiled, Is.Not.Null, "DecompileNcs returned null for script index " + i);
                Assert.That(decompiled.Trim().Length, Is.GreaterThan(0), "DecompileNcs returned empty output for script index " + i);

                NCS second = NCSAuto.CompileNss(decompiled, game);

                Assert.That(second, Is.Not.Null, "Second compile returned null for script index " + i);
                Assert.That(second.Validate(), Is.Empty, "Validation issues after decompile->compile (script index " + i + ")");

                Assert.That(second.Equals(first), Is.True, "Decompile->compile did not produce an equivalent NCS for script index " + i);
            }
        }
    }
}

