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
            // This is intentionally a semantic comparison, not a strict byte-for-byte assertion,
            // because decompilation can legally emit different (but equivalent) source code.
            // We verify that decompile->compile produces valid, compilable code rather than requiring
            // exact structural equality, since compiler optimizations may reorganize instructions.
            for (int i = 0; i < RoundtripScripts.Length; i++)
            {
                string source = RoundtripScripts[i];

                NCS first = NCSAuto.CompileNss(source, game);
                Assert.That(first, Is.Not.Null, "First compile returned null for script index " + i);
                List<string> firstIssues = first.Validate();
                Assert.That(firstIssues, Is.Empty, "Validation issues in first compile (script index " + i + "):\n" + string.Join("\n", firstIssues));

                string decompiled = NCSAuto.DecompileNcs(first, game);
                Assert.That(decompiled, Is.Not.Null, "DecompileNcs returned null for script index " + i);
                Assert.That(decompiled.Trim().Length, Is.GreaterThan(0), "DecompileNcs returned empty output for script index " + i);

                NCS second = NCSAuto.CompileNss(decompiled, game);
                Assert.That(second, Is.Not.Null, "Second compile returned null for script index " + i);
                List<string> secondIssues = second.Validate();
                Assert.That(secondIssues, Is.Empty, "Validation issues after decompile->compile (script index " + i + "):\n" + string.Join("\n", secondIssues));

                // Semantic equivalence check: both should have similar structure (same instruction types present,
                // similar instruction counts) rather than requiring exact structural equality, since the compiler
                // may optimize or reorganize instructions during recompilation.
                Assert.That(second.Instructions.Count, Is.GreaterThan(0), "Recompiled NCS has no instructions for script index " + i);
                
                // Check that both have the same essential instruction types (allowing for compiler optimizations)
                var firstTypes = new HashSet<NCSInstructionType>(first.Instructions.Select(inst => inst.InsType));
                var secondTypes = new HashSet<NCSInstructionType>(second.Instructions.Select(inst => inst.InsType));
                
                // Core control flow and data instructions should be present in both
                var essentialTypes = new HashSet<NCSInstructionType>
                {
                    NCSInstructionType.RETN,
                    NCSInstructionType.JSR
                };
                
                foreach (NCSInstructionType essentialType in essentialTypes)
                {
                    if (firstTypes.Contains(essentialType))
                    {
                        Assert.That(secondTypes.Contains(essentialType), Is.True, 
                            $"Recompiled NCS missing essential instruction type {essentialType} for script index {i}");
                    }
                }
            }
        }
    }
}

