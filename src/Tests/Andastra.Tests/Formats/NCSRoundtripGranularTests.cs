using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.NCS;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Granular tests for NCS roundtrip compilation/decompilation.
    /// 1:1 port of test_ncs_roundtrip_granular.py from tests/resource/formats/test_ncs_roundtrip_granular.py
    /// </summary>
    public class NCSRoundtripGranularTests
    {
        private static byte[] CanonicalBytes(NCS ncs)
        {
            return NCSAuto.BytesNcs(ncs);
        }

        private static void AssertBidirectionalRoundtrip(
            string source, BioWareGame game,
            List<string> libraryLookup = null)
        {
            NCS compiled = NCSAuto.CompileNss(source, game, null, null, libraryLookup);
            string decompiled = NCSAuto.DecompileNcs(compiled, game);

            // NSS -> NCS -> NSS -> NCS
            NCS recompiled = NCSAuto.CompileNss(decompiled, game, null, null, libraryLookup);
            CanonicalBytes(compiled).Should().Equal(
                CanonicalBytes(recompiled),
                "Recompiled bytecode diverged from initial compile");

            // NCS -> NSS -> NCS using freshly parsed binary payload
            byte[] binaryBlob = CanonicalBytes(compiled);
            NCS reloaded = NCSAuto.ReadNcs(binaryBlob);
            NCS ncsFromBinary = NCSAuto.CompileNss(
                NCSAuto.DecompileNcs(reloaded, game),
                game,
                null,
                null,
                libraryLookup);
            CanonicalBytes(reloaded).Should().Equal(
                CanonicalBytes(ncsFromBinary),
                "Roundtrip from binary payload not stable");
        }

        private static string Dedent(string script)
        {
            string[] lines = script.Split('\n');
            if (lines.Length == 0) return script;

            // Find minimum indentation (excluding empty lines)
            int minIndent = int.MaxValue;
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                int indent = 0;
                foreach (char c in line)
                {
                    if (c == ' ' || c == '\t')
                        indent++;
                    else
                        break;
                }
                if (indent < minIndent)
                    minIndent = indent;
            }

            if (minIndent == int.MaxValue || minIndent == 0)
                return script.Trim() + "\n";

            // Remove minimum indentation from all lines
            IEnumerable<string> dedented = lines.Select(line =>
            {
                if (string.IsNullOrWhiteSpace(line))
                    return line;
                if (line.Length >= minIndent)
                    return line.Substring(minIndent);
                return line;
            });

            return string.Join("\n", dedented).Trim() + "\n";
        }

        private static void AssertSubstrings(string source, string[] substrings)
        {
            foreach (string snippet in substrings)
            {
                source.Should().Contain(snippet, $"Expected snippet '{snippet}' to be present in decompiled script:\n{source}");
            }
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripPrimitivesAndStructuralTypes()
        {
            string source = Dedent(@"
                void main()
                {
                    int valueInt = 42;
                    float valueFloat = 3.5;
                    string valueString = ""kotor"";
                    object valueObject = OBJECT_SELF;
                    vector valueVector = Vector(1.0, 2.0, 3.0);
                    location valueLocation = Location(valueVector, 180.0);
                    effect valueEffect = GetFirstEffect(OBJECT_SELF);
                    event valueEvent = EventUserDefined(12);
                    talent valueTalent = TalentFeat(FEAT_POWER_ATTACK);

                    if (GetIsEffectValid(valueEffect))
                    {
                        RemoveEffect(OBJECT_SELF, valueEffect);
                    }
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "int valueInt = 42;",
                "float valueFloat = 3.5;",
                "string valueString = \"kotor\";",
                "object valueObject = OBJECT_SELF;",
                "vector valueVector = Vector(1.0, 2.0, 3.0);",
                "location valueLocation = Location(valueVector, 180.0);",
                "event valueEvent = EventUserDefined(12);",
                "talent valueTalent = TalentFeat(FEAT_POWER_ATTACK);",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripArithmeticOperations()
        {
            string source = Dedent(@"
                float CalculateAverage(int first, int second, float weight)
                {
                    float total = IntToFloat(first + second);
                    float average = total / 2.0;
                    return (average * weight) - 1.5;
                }

                void main()
                {
                    int a = 10;
                    int b = 7;
                    int sum = a + b;
                    int difference = sum - 5;
                    int product = difference * 3;
                    int quotient = product / 4;
                    int remainder = quotient % 2;
                    float weighted = CalculateAverage(sum, difference, 4.5);
                    float negated = -weighted;
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "int sum = a + b;",
                "int difference = sum - 5;",
                "int product = difference * 3;",
                "int quotient = product / 4;",
                "int remainder = quotient % 2;",
                "float negated = -weighted;",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripBitwiseAndShiftOperations()
        {
            string source = Dedent(@"
                void main()
                {
                    int mask = 0xFF;
                    int value = 0x35;
                    int andResult = mask & value;
                    int orResult = mask | value;
                    int xorResult = mask ^ value;
                    int leftShift = value << 2;
                    int rightShift = mask >> 3;
                    int unsignedShift = mask >>> 1;
                    int inverted = ~value;
                    int combined = (andResult | xorResult) & ~leftShift;
                    int logicalMix = (combined != 0) && (xorResult == 0xCA);
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "int andResult = mask & value;",
                "int orResult = mask | value;",
                "int xorResult = mask ^ value;",
                "int leftShift = value << 2;",
                "int rightShift = mask >> 3;",
                "int unsignedShift = mask >>> 1;",
                "int inverted = ~value;",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripLogicalAndRelationalOperations()
        {
            // Note: KOTOR has ANIMATION_LOOPING_GET_LOW (10) and ANIMATION_LOOPING_GET_MID (11),
            // but not ANIMATION_LOOPING_GET_UP which is NWN-specific
            string source = Dedent(@"
                int Evaluate(int a, int b, int c)
                {
                    if ((a > b && b >= c) || (a == c))
                    {
                        return 1;
                    }
                    else if (!(c < a) && (b != 0))
                    {
                        return 2;
                    }
                    return 0;
                }

                void main()
                {
                    int flag = Evaluate(5, 3, 4);
                    if (flag == 1 || flag == 2)
                    {
                        AssignCommand(OBJECT_SELF, PlayAnimation(ANIMATION_LOOPING_GET_LOW, 1.0, 0.5));
                    }
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "if ((a > b && b >= c) || (a == c))",
                "else if (!(c < a) && (b != 0))",
                "if (flag == 1 || flag == 2)",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripCompoundAssignments()
        {
            string source = Dedent(@"
                void main()
                {
                    int counter = 0;
                    counter += 5;
                    counter -= 2;
                    counter *= 3;
                    counter /= 2;
                    counter %= 4;

                    float distance = 10.0;
                    distance += 2.5;
                    distance -= 1.5;
                    distance *= 1.25;
                    distance /= 3.0;

                    vector offset = Vector(1.0, 2.0, 3.0);
                    offset += Vector(0.5, 0.5, 0.5);
                    offset -= Vector(0.5, 1.0, 1.5);
                    offset *= 2.0;
                    offset /= 4.0;
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "counter += 5;",
                "counter -= 2;",
                "counter *= 3;",
                "counter /= 2;",
                "counter %= 4;",
                "distance += 2.5;",
                "offset += Vector(0.5, 0.5, 0.5);",
                "offset *= 2.0;",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripIncrementAndDecrement()
        {
            string source = Dedent(@"
                void main()
                {
                    int i = 0;
                    int first = i++;
                    int second = ++i;
                    int third = i--;
                    int fourth = --i;
                    if (first < second && third >= fourth)
                    {
                        i += 1;
                    }
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "int first = i++;",
                "int second = ++i;",
                "int third = i--;",
                "int fourth = --i;",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripIfElseNesting()
        {
            string source = Dedent(@"
                int EvaluateState(int state)
                {
                    if (state == 0)
                    {
                        return 10;
                    }
                    else if (state == 1)
                    {
                        if (GetIsNight())
                        {
                            return 20;
                        }
                        else
                        {
                            return 30;
                        }
                    }
                    else
                    {
                        return -1;
                    }
                }

                void main()
                {
                    int result = EvaluateState(1);
                    if (result == 20)
                    {
                        ActionStartConversation(OBJECT_SELF, ""result_20"");
                    }
                    else
                    {
                        ActionStartConversation(OBJECT_SELF, ""other_result"");
                    }
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "if (state == 0)",
                "else if (state == 1)",
                "if (GetIsNight())",
                "else",
                "ActionStartConversation(OBJECT_SELF, \"result_20\");",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripWhileForDoLoops()
        {
            string source = Dedent(@"
                void main()
                {
                    int total = 0;
                    int i = 0;
                    while (i < 5)
                    {
                        total += i;
                        i++;
                    }

                    for (int j = 0; j < 3; j++)
                    {
                        total += j * 2;
                    }

                    int k = 0;
                    do
                    {
                        total -= k;
                        k++;
                    }
                    while (k < 2);
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "while (i < 5)",
                "for (int j = 0; j < 3; j++)",
                "do",
                "while (k < 2);",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripSwitchCase()
        {
            // Note: KOTOR doesn't have GetLocalInt/SetLocalInt like NWN, so we use
            // Random() to get a test value and SendMessageToPC to perform actions
            string source = Dedent(@"
                void main()
                {
                    int value = Random(5);
                    switch (value)
                    {
                        case 0:
                            SendMessageToPC(OBJECT_SELF, ""zero"");
                            break;
                        case 1:
                        case 2:
                            SendMessageToPC(OBJECT_SELF, ""one or two"");
                            break;
                        default:
                            SendMessageToPC(OBJECT_SELF, ""other"");
                            break;
                    }
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "switch (value)",
                "case 0:",
                "case 1:",
                "case 2:",
                "default:",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripStructUsage()
        {
            string source = Dedent(@"
                struct CombatStats
                {
                    int attack;
                    int defense;
                    float multiplier;
                    string label;
                };

                CombatStats BuildStats(int base)
                {
                    CombatStats result;
                    result.attack = base + 2;
                    result.defense = base * 2;
                    result.multiplier = IntToFloat(result.defense) / 3.0;
                    result.label = ""stat_"" + IntToString(base);
                    return result;
                }

                void main()
                {
                    CombatStats stats = BuildStats(5);
                    if (stats.attack > stats.defense)
                    {
                        stats.label = ""attack_bias"";
                    }
                    else
                    {
                        stats.label = ""defense_bias"";
                    }
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "struct CombatStats",
                "result.attack = base + 2;",
                "result.defense = base * 2;",
                "stats.label = \"attack_bias\";",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripFunctionDefinitionsAndReturns()
        {
            string source = Dedent(@"
                int CountPartyMembers()
                {
                    int count = 0;
                    object creature = GetFirstFactionMember(OBJECT_SELF, FALSE);
                    while (GetIsObjectValid(creature))
                    {
                        count++;
                        creature = GetNextFactionMember(OBJECT_SELF, FALSE);
                    }
                    return count;
                }

                void Announce(int members)
                {
                    string message = ""members:"" + IntToString(members);
                    SendMessageToPC(OBJECT_SELF, message);
                }

                void main()
                {
                    int members = CountPartyMembers();
                    Announce(members);
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "int CountPartyMembers()",
                "while (GetIsObjectValid(creature))",
                "void Announce(int members)",
                "Announce(members);",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripActionQueueAndDelays()
        {
            string source = Dedent(@"
                void ApplyBuff(object target)
                {
                    effect buff = EffectAbilityIncrease(ABILITY_STRENGTH, 2);
                    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, buff, target, 6.0);
                }

                void main()
                {
                    object player = GetFirstPC();
                    DelayCommand(1.5, AssignCommand(player, ApplyBuff(player)));
                    ClearAllActions();
                    ActionDoCommand(AssignCommand(OBJECT_SELF, PlaySound(""pc_action"")));
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K1),
                BioWareGame.K1);
            AssertBidirectionalRoundtrip(source, BioWareGame.K1);
            AssertSubstrings(decompiled, new[]
            {
                "DelayCommand(1.5, AssignCommand(player, ApplyBuff(player)));",
                "ClearAllActions();",
                "ActionDoCommand(AssignCommand(OBJECT_SELF, PlaySound(\"pc_action\")));",
            });
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripIncludeResolution()
        {
            // Python uses tmp_path fixture, we'll use a temporary directory
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            try
            {
                string includePath = Path.Combine(tempDir, "rt_helper.nss");
                // Note: KOTOR doesn't have SetLocalInt like NWN, so we use SendMessageToPC instead
                File.WriteAllText(includePath, Dedent(@"
                    int HelperFunction(int value)
                    {
                        return value * 2;
                    }
                "), Encoding.UTF8);

                string source = Dedent(@"
                    #include ""rt_helper""

                    void main()
                    {
                        int result = HelperFunction(5);
                        SendMessageToPC(OBJECT_SELF, IntToString(result));
                    }
                ");

                var libraryLookup = new List<string> { tempDir };
                string decompiled = NCSAuto.DecompileNcs(
                    NCSAuto.CompileNss(source, BioWareGame.K1, null, null, libraryLookup),
                    BioWareGame.K1);
                AssertBidirectionalRoundtrip(source, BioWareGame.K1, libraryLookup);
                // Note: The decompiler preserves #include directives rather than inlining
                // the function, so we check for the include and the function call, not the definition
                AssertSubstrings(decompiled, new[]
                {
                    "#include \"rt_helper\"",
                    "HelperFunction(5)",
                });
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRoundtripTslSpecificFunctionality()
        {
            string source = Dedent(@"
                void main()
                {
                    object target = GetFirstPC();
                    effect penalty = EffectAttackDecrease(2, ATTACK_BONUS_MISC);
                    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, penalty, target, 5.0);
                    AssignCommand(target, ClearAllActions());
                }
            ");

            string decompiled = NCSAuto.DecompileNcs(
                NCSAuto.CompileNss(source, BioWareGame.K2),
                BioWareGame.K2);
            AssertBidirectionalRoundtrip(source, BioWareGame.K2);
            AssertSubstrings(decompiled, new[]
            {
                "effect penalty = EffectAttackDecrease(2, ATTACK_BONUS_MISC);",
                "ApplyEffectToObject(DURATION_TYPE_TEMPORARY, penalty, target, 5.0);",
            });
        }
    }

    /// <summary>
    /// Binary roundtrip tests for sample NCS files.
    /// 1:1 port of TestNcsBinaryRoundtripSamples from test_ncs_roundtrip_granular.py
    /// </summary>
    public class NcsBinaryRoundtripSamples
    {
        private static readonly (string RelativePath, BioWareGame game)[] SampleFiles = new[]
        {
            ("tests/files/test.ncs", BioWareGame.K1),
            ("tests/test_pykotor/test_files/test.ncs", BioWareGame.K1),
            ("tests/test_toolset/test_files/90sk99.ncs", BioWareGame.K2),
        };

        private static byte[] CanonicalBytes(NCS ncs)
        {
            return NCSAuto.BytesNcs(ncs);
        }

        [Theory(Timeout = 120000)] // 2 minutes timeout
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void TestBinaryRoundtripSamples(int fileIndex)
        {
            (string relativePath, BioWareGame game) = SampleFiles[fileIndex];
            string ncsPath = Path.Combine("vendor", "PyKotor", relativePath);

            if (!File.Exists(ncsPath))
            {
                // Try alternative path
                ncsPath = relativePath;
            }

            if (!File.Exists(ncsPath))
            {
                return; // Skip if sample file doesn't exist
            }

            NCS original = NCSAuto.ReadNcs(ncsPath);
            string decompiled = NCSAuto.DecompileNcs(original, game);
            NCS recompilation = NCSAuto.CompileNss(decompiled, game);

            CanonicalBytes(original).Should().Equal(
                CanonicalBytes(recompilation),
                $"Roundtrip failed for {relativePath}");
            decompiled.Trim().Length.Should().BeGreaterThan(0, "Decompiled source should not be empty");
        }
    }
}


