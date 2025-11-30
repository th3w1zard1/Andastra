using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.NCS;
using TSLPatcher.Core.Formats.NCS.Compiler;
using TSLPatcher.Core.Formats.NCS.Compiler.NSS;
using Xunit;
using CompileError = TSLPatcher.Core.Formats.NCS.Compiler.NSS.CompileError;

namespace TSLPatcher.Tests.Formats
{
    /// <summary>
    /// Tests for NCS compiler functionality.
    /// 1:1 port of test_ncs_compiler.py from tests/resource/formats/test_ncs_compiler.py
    /// </summary>
    public class NCSCompilerTests
    {
        private NCS Compile(
            string script,
            Dictionary<string, byte[]> library = null,
            List<string> libraryLookup = null)
        {
            if (library == null)
            {
                library = new Dictionary<string, byte[]>();
            }
            return NCSAuto.CompileNss(script, Game.K1, null, libraryLookup);
        }

        #region Engine Call

        [Fact]
        public void TestEnginecall()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    object oExisting = GetExitingObject();
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].FunctionName.Should().Be("GetExitingObject");
            interpreter.ActionSnapshots[0].ArgValues.Should().BeEmpty();
        }

        [Fact]
        public void TestEnginecallReturnValue()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int inescapable = GetAreaUnescapable();
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("GetAreaUnescapable", args => 10);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(10);
        }

        [Fact]
        public void TestEnginecallWithParams()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    string tag = ""something"";
                    int n = 15;
                    object oSomething = GetObjectByTag(tag, n);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].FunctionName.Should().Be("GetObjectByTag");
            interpreter.ActionSnapshots[0].ArgValues.Select(a => a.Value).Should().Equal(new object[] { "something", 15 });
        }

        [Fact]
        public void TestEnginecallWithDefaultParams()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    string tag = ""something"";
                    object oSomething = GetObjectByTag(tag);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        [Fact]
        public void TestEnginecallWithMissingParams()
        {
            string script = @"
                void main()
                {
                    string tag = ""something"";
                    object oSomething = GetObjectByTag();
                }
            ";

            Assert.Throws<CompileError>(() => Compile(script));
        }

        [Fact]
        public void TestEnginecallWithTooManyParams()
        {
            string script = @"
                void main()
                {
                    string tag = ""something"";
                    object oSomething = GetObjectByTag("""", 0, ""shouldnotbehere"");
                }
            ";

            Assert.Throws<CompileError>(() => Compile(script));
        }

        [Fact]
        public void TestEnginecallDelayCommand1()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    object oFirstPlayer = GetFirstPC();
                    DelayCommand(1.0, GiveXPToCreature(oFirstPlayer, 9001));
                }
            ");
        }

        [Fact]
        public void TestEnginecallGetFirstObjectInShapeDefaults()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int nShape = SHAPE_CUBE;
                    float fSize = 0.0;
                    location lTarget;
                    GetFirstObjectInShape(nShape, fSize, lTarget);
                }
            ");
        }

        [Fact]
        public void TestEnginecallGetFactionEqual()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    object oFirst;
                    GetFactionEqual(oFirst);
                }
            ");
        }

        #endregion

        #region Operators

        [Fact]
        public void TestAddopIntInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 10 + 5;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(15);
        }

        [Fact]
        public void TestAddopFloatFloat()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    float value = 10.0 + 5.0;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(15.0f);
        }

        [Fact]
        public void TestAddopStringString()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    string value = ""abc"" + ""def"";
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be("abcdef");
        }

        [Fact]
        public void TestSubopIntInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 10 - 5;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(5);
        }

        [Fact]
        public void TestSubopFloatFloat()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    float value = 10.0 - 5.0;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(5.0f);
        }

        [Fact]
        public void TestMulopIntInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 10 * 5;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(50);
        }

        [Fact]
        public void TestMulopFloatFloat()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    float a = 10.0 * 5.0;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(50.0f);
        }

        [Fact]
        public void TestDivopIntInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 10 / 5;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(2);
        }

        [Fact]
        public void TestDivopFloatFloat()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    float a = 10.0 / 5.0;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(2.0f);
        }

        [Fact]
        public void TestModopIntInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 10 % 3;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(1);
        }

        [Fact]
        public void TestNegopInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = -10;
                    PrintInteger(a);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(-10);
        }

        [Fact]
        public void TestNegopFloat()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    float a = -10.0;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(-10.0f);
        }

        [Fact]
        public void TestBidmas()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 2 + (5 * ((0)) + 5) * 3 + 2 - (2 + (2 * 4 - 12 / 2)) / 2;
                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(17);
        }

        [Fact]
        public void TestOpWithVariables()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 10;
                    int b = 5;
                    int c = a * b * a;
                    int d = 10 * 5 * 10;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(500);
            interpreter.StackSnapshots[^4].Stack[^2].Value.Should().Be(500);
        }

        #endregion

        #region Logical Operator

        [Fact]
        public void TestNotOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = !1;
                    PrintInteger(a);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(0);
        }

        [Fact]
        public void TestLogicalAndOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 0 && 0;
                    int b = 1 && 0;
                    int c = 1 && 1;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^3].Value.Should().Be(0);
            interpreter.StackSnapshots[^4].Stack[^2].Value.Should().Be(0);
            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(1);
        }

        [Fact]
        public void TestLogicalOrOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 0 || 0;
                    int b = 1 || 0;
                    int c = 1 || 1;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^3].Value.Should().Be(0);
            interpreter.StackSnapshots[^4].Stack[^2].Value.Should().Be(1);
            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(1);
        }

        [Fact]
        public void TestLogicalEquals()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 1 == 1;
                    int b = ""a"" == ""b"";
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^2].Value.Should().Be(1);
            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(0);
        }

        [Fact]
        public void TestLogicalNotequalsOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 1 != 1;
                    int b = 1 != 2;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^2].Value.Should().Be(0);
            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(1);
        }

        #endregion

        #region Relational Operator

        [Fact]
        public void TestCompareGreaterthanOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 10 > 1;
                    int b = 10 > 10;
                    int c = 10 > 20;

                    PrintInteger(a);
                    PrintInteger(b);
                    PrintInteger(c);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(0);
        }

        [Fact]
        public void TestCompareGreaterthanorequalOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 10 >= 1;
                    int b = 10 >= 10;
                    int c = 10 >= 20;

                    PrintInteger(a);
                    PrintInteger(b);
                    PrintInteger(c);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(0);
        }

        [Fact]
        public void TestCompareLessthanOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 10 < 1;
                    int b = 10 < 10;
                    int c = 10 < 20;

                    PrintInteger(a);
                    PrintInteger(b);
                    PrintInteger(c);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestCompareLessthanorequalOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 10 <= 1;
                    int b = 10 <= 10;
                    int c = 10 <= 20;

                    PrintInteger(a);
                    PrintInteger(b);
                    PrintInteger(c);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(1);
        }

        #endregion

        #region Bitwise Operator

        [Fact]
        public void TestBitwiseOrOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 5 | 2;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(7);
        }

        [Fact]
        public void TestBitwiseXorOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 7 ^ 2;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(5);
        }

        [Fact]
        public void TestBitwiseNotInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = ~1;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(-2);
        }

        [Fact]
        public void TestBitwiseAndOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 7 & 2;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(2);
        }

        [Fact]
        public void TestBitwiseShiftleftOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 7 << 2;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(28);
        }

        [Fact]
        public void TestBitwiseShiftrightOp()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 7 >> 2;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.StackSnapshots[^4].Stack[^1].Value.Should().Be(1);
        }

        #endregion

        #region Assignment

        [Fact]
        public void TestAssignment()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 1;
                    a = 4;

                    PrintInteger(a);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(4);
        }

        [Fact]
        public void TestAssignmentComplex()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 1;
                    a = a * 2 + 8;

                    PrintInteger(a);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(10);
        }

        [Fact]
        public void TestAssignmentStringConstant()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    string a = ""A"";

                    PrintString(a);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be("A");
        }

        [Fact]
        public void TestAssignmentStringEnginecall()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    string a = GetGlobalString(""A"");

                    PrintString(a);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("GetGlobalString", args => args[0]);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be("A");
        }

        [Fact]
        public void TestAdditionAssignmentIntInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 1;
                    value += 2;

                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            var snap = interpreter.ActionSnapshots[^1];
            snap.FunctionName.Should().Be("PrintInteger");
            snap.ArgValues[0].Value.Should().Be(3);
        }

        [Fact]
        public void TestAdditionAssignmentIntFloat()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 1;
                    value += 2.0;

                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            var snap = interpreter.ActionSnapshots[^1];
            snap.FunctionName.Should().Be("PrintInteger");
            snap.ArgValues[0].Value.Should().Be(3);
        }

        [Fact]
        public void TestAdditionAssignmentFloatFloat()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    float value = 1.0;
                    value += 2.0;

                    PrintFloat(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(3.0f);
        }

        [Fact]
        public void TestAdditionAssignmentFloatInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    float value = 1.0;
                    value += 2;

                    PrintFloat(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            var snap = interpreter.ActionSnapshots[^1];
            snap.FunctionName.Should().Be("PrintFloat");
            snap.ArgValues[0].Value.Should().Be(3.0f);
        }

        [Fact]
        public void TestAdditionAssignmentStringString()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    string value = ""a"";
                    value += ""b"";

                    PrintString(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            var snap = interpreter.ActionSnapshots[^1];
            snap.FunctionName.Should().Be("PrintString");
            snap.ArgValues[0].Value.Should().Be("ab");
        }

        [Fact]
        public void TestSubtractionAssignmentIntInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 10;
                    value -= 2 * 2;

                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            var snap = interpreter.ActionSnapshots[^1];
            snap.FunctionName.Should().Be("PrintInteger");
            snap.ArgValues.Select(a => a.Value).Should().Equal(new object[] { 6 });
        }

        [Fact]
        public void TestSubtractionAssignmentIntFloat()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 10;
                    value -= 2.0;

                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            var snap = interpreter.ActionSnapshots[^1];
            snap.FunctionName.Should().Be("PrintInteger");
            snap.ArgValues[0].Value.Should().Be(8.0f);
        }

        [Fact]
        public void TestSubtractionAssignmentFloatFloat()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    float value = 10.0;
                    value -= 2.0;

                    PrintFloat(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            var snap = interpreter.ActionSnapshots[^1];
            snap.FunctionName.Should().Be("PrintFloat");
            snap.ArgValues[0].Value.Should().Be(8.0f);
        }

        [Fact]
        public void TestSubtractionAssignmentFloatInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    float value = 10.0;
                    value -= 2;

                    PrintFloat(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(8.0f);
        }

        [Fact]
        public void TestMultiplicationAssignment()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 10;
                    value *= 2 * 2;

                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            var snap = interpreter.ActionSnapshots[^1];
            snap.FunctionName.Should().Be("PrintInteger");
            snap.ArgValues.Select(a => a.Value).Should().Equal(new object[] { 40 });
        }

        [Fact]
        public void TestDivisionAssignment()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 12;
                    value /= 2 * 2;

                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            var snap = interpreter.ActionSnapshots[^1];
            snap.FunctionName.Should().Be("PrintInteger");
            snap.ArgValues.Select(a => a.Value).Should().Equal(new object[] { 3 });
        }

        #endregion

        #region Switch Statements

        [Fact]
        public void TestSwitchNoBreaks()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    switch (2)
                    {
                        case 1:
                            PrintInteger(1);
                        case 2:
                            PrintInteger(2);
                        case 3:
                            PrintInteger(3);
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(3);
        }

        [Fact]
        public void TestSwitchJumpOver()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    switch (4)
                    {
                        case 1:
                            PrintInteger(1);
                        case 2:
                            PrintInteger(2);
                        case 3:
                            PrintInteger(3);
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(0);
        }

        [Fact]
        public void TestSwitchWithBreaks()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    switch (3)
                    {
                        case 1:
                            PrintInteger(1);
                            break;
                        case 2:
                            PrintInteger(2);
                            break;
                        case 3:
                            PrintInteger(3);
                            break;
                        case 4:
                            PrintInteger(4);
                            break;
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
        }

        [Fact]
        public void TestSwitchWithDefault()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    switch (4)
                    {
                        case 1:
                            PrintInteger(1);
                            break;
                        case 2:
                            PrintInteger(2);
                            break;
                        case 3:
                            PrintInteger(3);
                            break;
                        default:
                            PrintInteger(4);
                            break;
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(4);
        }

        [Fact]
        public void TestSwitchScopedBlocks()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    switch (2)
                    {
                        case 1:
                        {
                            int inner = 10;
                            PrintInteger(inner);
                        }
                        break;

                        case 2:
                        {
                            int inner = 20;
                            PrintInteger(inner);
                        }
                        break;
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(20);
        }

        #endregion

        [Fact]
        public void TestScope()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 1;

                    if (value == 1)
                    {
                        value = 2;
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        [Fact]
        public void TestScopedBlock()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 1;

                    {
                        int b = 2;
                        PrintInteger(a);
                        PrintInteger(b);
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(2);
        }

        #region If/Else Conditions

        [Fact]
        public void TestIf()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    if(0)
                    {
                        PrintInteger(0);
                    }

                    if(1)
                    {
                        PrintInteger(1);
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestIfMultipleConditions()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    if(1 && 2 && 3)
                    {
                        PrintInteger(0);
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        [Fact]
        public void TestIfElse()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    if (0) {    PrintInteger(0); }
                    else {      PrintInteger(1); }

                    if (1) {    PrintInteger(2); }
                    else {      PrintInteger(3); }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
        }

        [Fact]
        public void TestIfElseIf()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    if (0)      { PrintInteger(0); }
                    else if (0) { PrintInteger(1); }

                    if (1)      { PrintInteger(2); } // hit
                    else if (1) { PrintInteger(3); }

                    if (1)      { PrintInteger(4); } // hit
                    else if (0) { PrintInteger(5); }

                    if (0)      { PrintInteger(6); }
                    else if (1) { PrintInteger(7); } // hit
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(4);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(7);
        }

        [Fact]
        public void TestIfElseIfElse()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    if (0)      { PrintInteger(0); }
                    else if (0) { PrintInteger(1); }
                    else        { PrintInteger(3); } // hit

                    if (0)      { PrintInteger(4); }
                    else if (1) { PrintInteger(5); } // hit
                    else        { PrintInteger(6); }

                    if (1)      { PrintInteger(7); } // hit
                    else if (1) { PrintInteger(8); }
                    else        { PrintInteger(9); }

                    if (1)      { PrintInteger(10); } //hit
                    else if (0) { PrintInteger(11); }
                    else        { PrintInteger(12); }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(4);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(5);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(7);
            interpreter.ActionSnapshots[3].ArgValues[0].Value.Should().Be(10);
        }

        [Fact]
        public void TestSingleStatementIf()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    if (1) PrintInteger(222);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(222);
        }

        [Fact]
        public void TestSingleStatementElseIfElse()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    if (0) PrintInteger(11);
                    else if (0) PrintInteger(22);
                    else PrintInteger(33);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(33);
        }

        #endregion

        #region While

        [Fact]
        public void TestWhileLoop()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 3;
                    while (value)
                    {
                        PrintInteger(value);
                        value -= 1;
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestWhileLoopWithBreak()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 3;
                    while (value)
                    {
                        PrintInteger(value);
                        value -= 1;
                        break;
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
        }

        [Fact]
        public void TestWhileLoopWithContinue()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 3;
                    while (value)
                    {
                        PrintInteger(value);
                        value -= 1;
                        continue;
                        PrintInteger(99);
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestWhileLoopScope()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 11;
                    int outer = 22;
                    while (value)
                    {
                        int inner = 33;
                        value = 0;
                        continue;
                        outer = 99;
                    }

                    PrintInteger(outer);
                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(22);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(0);
        }

        #endregion

        #region Do While

        [Fact]
        public void TestDoWhileLoop()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 3;
                    do
                    {
                        PrintInteger(value);
                        value -= 1;
                    } while (value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestDoWhileLoopWithBreak()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 3;
                    do
                    {
                        PrintInteger(value);
                        value -= 1;
                        break;
                    } while (value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
        }

        [Fact]
        public void TestDoWhileLoopWithContinue()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = 3;
                    do
                    {
                        PrintInteger(value);
                        value -= 1;
                        continue;
                        PrintInteger(99);
                    } while (value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestDoWhileLoopScope()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int outer = 11;
                    int value = 22;
                    do
                    {
                        int inner = 33;
                        value = 0;
                    } while (value);

                    PrintInteger(outer);
                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(11);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(0);
        }

        #endregion

        #region For Loop

        [Fact]
        public void TestForLoop()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int i = 99;
                    for (i = 1; i <= 3; i += 1)
                    {
                        PrintInteger(i);
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(3);
        }

        [Fact]
        public void TestForLoopWithBreak()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int i = 99;
                    for (i = 1; i <= 3; i += 1)
                    {
                        PrintInteger(i);
                        break;
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestForLoopWithContinue()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int i = 99;
                    for (i = 1; i <= 3; i += 1)
                    {
                        PrintInteger(i);
                        continue;
                        PrintInteger(99);
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(3);
        }

        [Fact]
        public void TestForLoopScope()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int i = 11;
                    int outer = 22;
                    for (i = 0; i <= 5; i += 1)
                    {
                        int inner = 33;
                        break;
                    }

                    PrintInteger(i);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(0);
        }

        #endregion

        [Fact]
        public void TestFloatNotations()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    PrintFloat(1.0f);
                    PrintFloat(2.0);
                    PrintFloat(3f);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(1.0f);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(2.0f);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(3.0f);
        }

        [Fact]
        public void TestMultiDeclarations()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value1, value2 = 1, value3 = 2;

                    PrintInteger(value1);
                    PrintInteger(value2);
                    PrintInteger(value3);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(2);
        }

        [Fact]
        public void TestLocalDeclarations()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int INT;
                    float FLOAT;
                    string STRING;
                    location LOCATION;
                    effect EFFECT;
                    talent TALENT;
                    event EVENT;
                    vector VECTOR;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        [Fact]
        public void TestGlobalDeclarations()
        {
            NCS ncs = Compile(@"
                int INT;
                float FLOAT;
                string STRING;
                location LOCATION;
                effect EFFECT;
                talent TALENT;
                event EVENT;
                vector VECTOR;

                void main()
                {

                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            ncs.Instructions.Any(inst => inst.InsType == NCSInstructionType.SAVEBP).Should().BeTrue();
        }

        [Fact]
        public void TestGlobalInitializations()
        {
            NCS ncs = Compile(@"
                int INT = 0;
                float FLOAT = 0.0;
                string STRING = """";
                vector VECTOR = [0.0, 0.0, 0.0];

                void main()
                {
                    PrintInteger(INT);
                    PrintFloat(FLOAT);
                    PrintString(STRING);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(0.0f);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be("");
            ncs.Instructions.Any(inst => inst.InsType == NCSInstructionType.SAVEBP).Should().BeTrue();
        }

        [Fact]
        public void TestGlobalInitializationWithUnary()
        {
            NCS ncs = Compile(@"
                int INT = -1;

                void main()
                {
                    PrintInteger(INT);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(-1);
        }

        [Fact]
        public void TestComment()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    // int a = ""abc""; // [] /*
                    int a = 0;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        [Fact]
        public void TestMultilineComment()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    /* int
                    abc =
                    ;; 123
                    */

                    string aaa = """";
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        [Fact]
        public void TestReturn()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 1;

                    if (a == 1)
                    {
                        PrintInteger(a);
                        return;
                    }

                    PrintInteger(0);
                    return;
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestReturnParenthesis()
        {
            NCS ncs = Compile(@"
                int test()
                {
                    return(321);
                }

                void main()
                {
                    int value = test();
                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(321);
        }

        [Fact]
        public void TestReturnParenthesisConstant()
        {
            NCS ncs = Compile(@"
                int test()
                {
                    return(TRUE);
                }

                void main()
                {
                    int value = test();
                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestIntParenthesisDeclaration()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int value = (123);
                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(123);
        }

        [Fact]
        public void TestIncludeBuiltin()
        {
            byte[] otherscript = Encoding.GetEncoding(1252).GetBytes(@"
                void TestFunc()
                {
                    PrintInteger(123);
                }
            ");

            var library = new Dictionary<string, byte[]> { { "otherscript", otherscript } };
            NCS ncs = Compile(@"
                #include ""otherscript""

                void main()
                {
                    TestFunc();
                }
            ", library);

            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        [Fact]
        public void TestIncludeLookup()
        {
            // Note: This test requires a test file to exist
            // Python: includetest_script_path = Path("""./tests/test_pykotor/test_files""").resolve()
            // For now, we'll skip if the file doesn't exist
            string testFilesPath = Path.Combine("test_files", "includetest.nss");
            if (!File.Exists(testFilesPath))
            {
                return; // Skip if test file doesn't exist
            }

            var libraryLookup = new List<string> { Path.GetDirectoryName(testFilesPath) ?? "" };
            NCS ncs = Compile(@"
                #include ""includetest""

                void main()
                {
                    TestFunc();
                }
            ", null, libraryLookup);

            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        [Fact]
        public void TestNestedInclude()
        {
            byte[] firstScript = Encoding.GetEncoding(1252).GetBytes(@"
                int SOME_COST = 13;

                void TestFunc(int value)
                {
                    PrintInteger(value);
                }
            ");

            byte[] secondScript = Encoding.GetEncoding(1252).GetBytes(@"
                #include ""first_script""
            ");

            var library = new Dictionary<string, byte[]>
            {
                { "first_script", firstScript },
                { "second_script", secondScript }
            };

            NCS ncs = Compile(@"
                #include ""second_script""

                void main()
                {
                    TestFunc(SOME_COST);
                }
            ", library);

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(13);
        }

        [Fact]
        public void TestMissingInclude()
        {
            string source = @"
                #include ""otherscript""

                void main()
                {
                    TestFunc();
                }
            ";

            Assert.Throws<CompileError>(() => Compile(source));
        }

        [Fact]
        public void TestGlobalIntAdditionAssignment()
        {
            NCS ncs = Compile(@"
                int global1 = 1;
                int global2 = 2;

                void main()
                {
                    int local1 = 3;
                    int local2 = 4;

                    global1 += local1;
                    global2 = local2 + global1;

                    PrintInteger(global1);
                    PrintInteger(global2);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(4);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(8);
        }

        [Fact]
        public void TestGlobalIntSubtractionAssignment()
        {
            NCS ncs = Compile(@"
                int global1 = 1;
                int global2 = 10;

                void main()
                {
                    int local1 = 100;
                    int local2 = 1000;

                    global1 -= local1;              // 1 - 100 = -99
                    global2 = local2 - global1;     // 1000 - -99 = 1099

                    PrintInteger(global1);
                    PrintInteger(global2);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(-99);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(1099);
        }

        [Fact]
        public void TestGlobalIntMultiplicationAssignment()
        {
            NCS ncs = Compile(@"
                int global1 = 1;
                int global2 = 10;

                void main()
                {
                    int local1 = 100;
                    int local2 = 1000;

                    global1 *= local1;              // 1 * 100 = 100
                    global2 = local2 * global1;     // 1000 * 100 = 100000

                    PrintInteger(global1);
                    PrintInteger(global2);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(100);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(100000);
        }

        [Fact]
        public void TestGlobalIntDivisionAssignment()
        {
            NCS ncs = Compile(@"
                int global1 = 1000;
                int global2 = 100;

                void main()
                {
                    int local1 = 10;
                    int local2 = 1;

                    global1 /= local1;              // 1000 / 10 = 100
                    global2 = global1 / local2;     // 100 / 1 = 100

                    PrintInteger(global1);
                    PrintInteger(global2);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(100);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(100);
        }

        [Fact]
        public void TestImportedGlobalVariable()
        {
            byte[] otherscript = Encoding.GetEncoding(1252).GetBytes(@"
                int iExperience = 55;
            ");

            var library = new Dictionary<string, byte[]> { { "otherscript", otherscript } };
            NCS ncs = Compile(@"
                #include ""otherscript""

                void main()
                {
                    object oPlayer = GetPCSpeaker();
                    GiveXPToCreature(oPlayer, iExperience);
                }
            ", library);

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[1].ArgValues[1].Value.Should().Be(55);
        }

        [Fact]
        public void TestDeclarationInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a;
                    PrintInteger(a);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(0);
        }

        [Fact]
        public void TestDeclarationFloat()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    float a;
                    PrintFloat(a);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(0.0f);
        }

        [Fact]
        public void TestDeclarationString()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    string a;
                    PrintString(a);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be("");
        }

        [Fact]
        public void TestVector()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    vector vec = Vector(2.0, 4.0, 4.0);
                    float mag = VectorMagnitude(vec);
                    PrintFloat(mag);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", args => new Vector3((float)args[0], (float)args[1], (float)args[2]));
            interpreter.SetMock("VectorMagnitude", args =>
            {
                if (args[0] is Vector3 vec)
                {
                    return (float)Math.Sqrt(vec.X * vec.X + vec.Y * vec.Y + vec.Z * vec.Z);
                }
                return 0.0f;
            });
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(6.0f);
        }

        [Fact]
        public void TestVectorNotation()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    vector vec = [1.0, 2.0, 3.0];
                    PrintFloat(vec.x);
                    PrintFloat(vec.y);
                    PrintFloat(vec.z);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(1.0f);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(2.0f);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(3.0f);
        }

        [Fact]
        public void TestVectorGetComponents()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    vector vec = Vector(2.0, 4.0, 6.0);
                    PrintFloat(vec.x);
                    PrintFloat(vec.y);
                    PrintFloat(vec.z);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", args => new Vector3((float)args[0], (float)args[1], (float)args[2]));
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(2.0f);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(4.0f);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(6.0f);
        }

        [Fact]
        public void TestVectorSetComponents()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    vector vec = Vector(0.0, 0.0, 0.0);
                    vec.x = 2.0;
                    vec.y = 4.0;
                    vec.z = 6.0;
                    PrintFloat(vec.x);
                    PrintFloat(vec.y);
                    PrintFloat(vec.z);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", args => new Vector3((float)args[0], (float)args[1], (float)args[2]));
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(2.0f);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(4.0f);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(6.0f);
        }

        [Fact]
        public void TestStructGetMembers()
        {
            NCS ncs = Compile(@"
                struct ABC
                {
                    int value1;
                    string value2;
                    float value3;
                };

                void main()
                {
                    struct ABC abc;
                    PrintInteger(abc.value1);
                    PrintString(abc.value2);
                    PrintFloat(abc.value3);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be("");
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(0.0f);
        }

        [Fact]
        public void TestStructGetInvalidMember()
        {
            string source = @"
                struct ABC
                {
                    int value1;
                    string value2;
                    float value3;
                };

                void main()
                {
                    struct ABC abc;
                    PrintFloat(abc.value4);
                }
            ";

            Assert.Throws<CompileError>(() => Compile(source));
        }

        [Fact]
        public void TestStructSetMembers()
        {
            NCS ncs = Compile(@"
                struct ABC
                {
                    int value1;
                    string value2;
                    float value3;
                };

                void main()
                {
                    struct ABC abc;
                    abc.value1 = 123;
                    abc.value2 = ""abc"";
                    abc.value3 = 3.14;
                    PrintInteger(abc.value1);
                    PrintString(abc.value2);
                    PrintFloat(abc.value3);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(123);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be("abc");
            // Python: self.assertAlmostEqual(3.14, interpreter.action_snapshots[-1].arg_values[0].value)
            ((float)interpreter.ActionSnapshots[^1].ArgValues[0].Value).Should().BeApproximately(3.14f, 0.01f);
        }

        [Fact]
        public void TestPrefixIncrementSpInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 1;
                    int b = ++a;

                    PrintInteger(a);
                    PrintInteger(b);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(2);
        }

        [Fact]
        public void TestPrefixIncrementBpInt()
        {
            NCS ncs = Compile(@"
                int a = 1;

                void main()
                {
                    int b = ++a;

                    PrintInteger(a);
                    PrintInteger(b);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(2);
        }

        [Fact]
        public void TestPostfixIncrementSpInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 1;
                    int b = a++;

                    PrintInteger(a);
                    PrintInteger(b);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestPostfixIncrementBpInt()
        {
            NCS ncs = Compile(@"
                int a = 1;

                void main()
                {
                    int b = a++;

                    PrintInteger(a);
                    PrintInteger(b);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestPrefixDecrementSpInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 1;
                    int b = --a;

                    PrintInteger(a);
                    PrintInteger(b);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(0);
        }

        [Fact]
        public void TestPrefixDecrementBpInt()
        {
            NCS ncs = Compile(@"
                int a = 1;

                void main()
                {
                    int b = --a;

                    PrintInteger(a);
                    PrintInteger(b);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(0);
        }

        [Fact]
        public void TestPostfixDecrementSpInt()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 1;
                    int b = a--;

                    PrintInteger(a);
                    PrintInteger(b);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestPostfixDecrementBpInt()
        {
            NCS ncs = Compile(@"
                int a = 1;

                void main()
                {
                    int b = a--;

                    PrintInteger(a);
                    PrintInteger(b);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(1);
        }

        [Fact]
        public void TestAssignmentlessExpression()
        {
            NCS ncs = Compile(@"
                void main()
                {
                    int a = 123;

                    1;
                    GetCheatCode(1);
                    ""abc"";

                    PrintInteger(a);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(123);
        }

        #region Script Subroutines

        [Fact]
        public void TestPrototypeNoArgs()
        {
            NCS ncs = Compile(@"
                void test();

                void main()
                {
                    test();
                }

                void test()
                {
                    PrintInteger(56);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(56);
        }

        [Fact]
        public void TestPrototypeWithArg()
        {
            NCS ncs = Compile(@"
                void test(int value);

                void main()
                {
                    test(57);
                }

                void test(int value)
                {
                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(57);
        }

        [Fact]
        public void TestPrototypeWithThreeArgs()
        {
            NCS ncs = Compile(@"
                void test(int a, int b, int c)
                {
                    PrintInteger(a);
                    PrintInteger(b);
                    PrintInteger(c);
                }

                void main()
                {
                    int a = 1, b = 2, c = 3;
                    test(a, b, c);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(3);
        }

        [Fact]
        public void TestPrototypeWithManyArgs()
        {
            NCS ncs = Compile(@"
                void test(int a, effect z, int b, int c, int d = 4)
                {
                    PrintInteger(a);
                    PrintInteger(b);
                    PrintInteger(c);
                    PrintInteger(d);
                }

                void main()
                {
                    int a = 1, b = 2, c = 3;
                    effect z;

                    test(a, z, b, c);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^4].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[^3].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[^2].ArgValues[0].Value.Should().Be(3);
            interpreter.ActionSnapshots[^1].ArgValues[0].Value.Should().Be(4);
        }

        [Fact]
        public void TestPrototypeWithDefaultArg()
        {
            NCS ncs = Compile(@"
                void test(int value = 57);

                void main()
                {
                    test();
                }

                void test(int value = 57)
                {
                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(57);
        }

        [Fact]
        public void TestPrototypeWithDefaultConstantArg()
        {
            NCS ncs = Compile(@"
                void test(int value = DAMAGE_TYPE_COLD);

                void main()
                {
                    test();
                }

                void test(int value = DAMAGE_TYPE_COLD)
                {
                    PrintInteger(value);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(32);
        }

        [Fact]
        public void TestPrototypeMissingArg()
        {
            string source = @"
                void test(int value);

                void main()
                {
                    test();
                }

                void test(int value)
                {
                    PrintInteger(value);
                }
            ";

            Assert.Throws<CompileError>(() => Compile(source));
        }

        [Fact]
        public void TestPrototypeMissingArgAndDefault()
        {
            string source = @"
                void test(int value1, int value2 = 123);

                void main()
                {
                    test();
                }

                void test(int value1, int value2 = 123)
                {
                    PrintInteger(value1);
                }
            ";

            Assert.Throws<CompileError>(() => Compile(source));
        }

        [Fact]
        public void TestPrototypeDefaultBeforeRequired()
        {
            string source = @"
                void test(int value1 = 123, int value2);

                void main()
                {
                    test(123, 123);
                }

                void test(int value1 = 123, int value2)
                {
                    PrintInteger(value1);
                }
            ";

            Assert.Throws<CompileError>(() => Compile(source));
        }

        [Fact]
        public void TestRedefineFunction()
        {
            string script = @"
                void test()
                {

                }

                void test()
                {

                }
            ";
            Assert.Throws<CompileError>(() => Compile(script));
        }

        [Fact]
        public void TestDoublePrototype()
        {
            string script = @"
                void test();
                void test();
            ";
            Assert.Throws<CompileError>(() => Compile(script));
        }

        [Fact]
        public void TestPrototypeAfterDefinition()
        {
            string script = @"
                void test()
                {

                }

                void test();
            ";
            Assert.Throws<CompileError>(() => Compile(script));
        }

        [Fact]
        public void TestPrototypeAndDefinitionParamMismatch()
        {
            string script = @"
                void test(int a);

                void test()
                {

                }
            ";
            Assert.Throws<CompileError>(() => Compile(script));
        }

        [Fact]
        public void TestPrototypeAndDefinitionReturnMismatch()
        {
            string script = @"
                void test(int a);

                int test(int a)
                {

                }
            ";
            Assert.Throws<CompileError>(() => Compile(script));
        }

        [Fact]
        public void TestCallUndefined()
        {
            string script = @"
                void main()
                {
                    test(0);
                }
            ";

            Assert.Throws<CompileError>(() => Compile(script));
        }

        [Fact]
        public void TestCallVoidWithNoArgs()
        {
            NCS ncs = Compile(@"
                void test()
                {
                    PrintInteger(123);
                }

                void main()
                {
                    test();
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(123);
        }

        [Fact]
        public void TestCallVoidWithOneArg()
        {
            NCS ncs = Compile(@"
                void test(int value)
                {
                    PrintInteger(value);
                }

                void main()
                {
                    test(123);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(123);
        }

        [Fact]
        public void TestCallVoidWithTwoArgs()
        {
            NCS ncs = Compile(@"
                void test(int value1, int value2)
                {
                    PrintInteger(value1);
                    PrintInteger(value2);
                }

                void main()
                {
                    test(1, 2);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
        }

        [Fact]
        public void TestCallIntWithNoArgs()
        {
            NCS ncs = Compile(@"
                int test()
                {
                    return 5;
                }

                void main()
                {
                    int x = test();
                    PrintInteger(x);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(5);
        }

        [Fact]
        public void TestCallIntWithNoArgsAndForwardDeclared()
        {
            NCS ncs = Compile(@"
                int test();

                int test()
                {
                    return 5;
                }

                void main()
                {
                    int x = test();
                    PrintInteger(x);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(5);
        }

        [Fact]
        public void TestCallParamMismatch()
        {
            string source = @"
                int test(int a)
                {
                    return a;
                }

                void main()
                {
                    test(""123"");
                }
            ";

            Assert.Throws<CompileError>(() => Compile(source));
        }

        #endregion

        [Fact]
        public void TestSwitchScopeA()
        {
            NCS ncs = Compile(@"
                int shape;
                int harmful;

                void main()
                {
                    object oTarget = OBJECT_SELF;
                    effect e1, e2;
                    effect e3;

                    shape = SHAPE_SPHERE;

                    switch (1)
                    {
                        case 1:
                            harmful = FALSE;
                            e1 = EffectMovementSpeedIncrease(99);

                            if (1 == 1)
                            {
                                e1 = EffectLinkEffects(e1, EffectVisualEffect(VFX_DUR_SPEED));
                            }

                            GiveXPToCreature(OBJECT_SELF, 100);
                            GetHasSpellEffect(FORCE_POWER_SPEED_BURST, oTarget);
                        break;
                    }
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            interpreter.ActionSnapshots[^1].ArgValues.Select(a => a.Value).Should().Equal(new object[] { 8, 0 });
        }

        [Fact]
        public void TestSwitchScopeB()
        {
            NCS ncs = Compile(@"
                int Cort_XP(int abc)
                {
                    GiveXPToCreature(GetFirstPC(), abc);
                }

                void main() {
                    int abc = 2500;
                    Cort_XP(abc);
                }
            ");

            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }
    }
}


