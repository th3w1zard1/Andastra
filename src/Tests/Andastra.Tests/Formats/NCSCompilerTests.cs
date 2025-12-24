using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Common.Script;
using Andastra.Parsing.Formats.NCS;
using Andastra.Parsing.Formats.NCS.Compiler;
using Andastra.Parsing.Formats.NCS.Compiler.NSS;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Tests for NSS to NCS compilation.
    /// 1:1 port of test_ncs.py from tests/resource/formats/test_ncs.py
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py
    /// </summary>
    public class NCSCompilerTests
    {
        /// <summary>
        /// Compile NSS script to NCS bytecode.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:73
        /// Original: def compile(self, script: str, library: dict[str, bytes] | None = None, library_lookup: Sequence[str | Path] | None = None) -> NCS:
        /// </summary>
        private NCS Compile(string script, Dictionary<string, byte[]> library = null, List<string> libraryLookup = null)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:80
            // Original: if library is None: library = {}
            if (library == null)
            {
                library = new Dictionary<string, byte[]>();
            }

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:94
            // Original: nssParser = NssParser(functions=KOTOR_FUNCTIONS, constants=KOTOR_CONSTANTS, library=library, library_lookup=normalized_lookup)
            var compiler = new NssCompiler(BioWareGame.K1, libraryLookup);
            NCS ncs = compiler.Compile(script, library);
            return ncs;
        }

        #region Engine Call Tests

        /// <summary>
        /// Test basic engine function call.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:147
        /// Original: def test_enginecall(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestEnginecall()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:149
            // Original: ncs = self.compile("""void main() { object oExisting = GetExitingObject(); }""")
            NCS ncs = Compile(@"
            void main()
            {
                object oExisting = GetExitingObject();
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:158
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:161
            // Original: assert len(interpreter.action_snapshots) == 1
            interpreter.ActionSnapshots.Count.Should().Be(1);
            // Original: assert interpreter.action_snapshots[0].function_name == "GetExitingObject"
            interpreter.ActionSnapshots[0].FunctionName.Should().Be("GetExitingObject");
            // Original: assert interpreter.action_snapshots[0].arg_values == []
            interpreter.ActionSnapshots[0].ArgValues.Should().BeEmpty();
        }

        /// <summary>
        /// Test engine function call with return value.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:165
        /// Original: def test_enginecall_return_value(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestEnginecallReturnValue()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:167
            // Original: ncs = self.compile("""void main() { int inescapable = GetAreaUnescapable(); }""")
            NCS ncs = Compile(@"
                void main()
                {
                    int inescapable = GetAreaUnescapable();
                }
            ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:176
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock("GetAreaUnescapable", lambda: 10); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("GetAreaUnescapable", (args) => 10);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:180
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 10
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(10);
        }

        /// <summary>
        /// Test engine function call with parameters.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:182
        /// Original: def test_enginecall_with_params(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestEnginecallWithParams()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:184
            // Original: ncs = self.compile("""void main() { string tag = ""something""; int n = 15; object oSomething = GetObjectByTag(tag, n); }""")
            NCS ncs = Compile(@"
            void main()
            {
                string tag = ""something"";
                int n = 15;
                object oSomething = GetObjectByTag(tag, n);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:195
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:198
            // Original: assert len(interpreter.action_snapshots) == 1
            interpreter.ActionSnapshots.Count.Should().Be(1);
            // Original: assert interpreter.action_snapshots[0].function_name == "GetObjectByTag"
            interpreter.ActionSnapshots[0].FunctionName.Should().Be("GetObjectByTag");
            // Original: assert interpreter.action_snapshots[0].arg_values == [""something"", 15]
            interpreter.ActionSnapshots[0].ArgValues.Count.Should().Be(2);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be("something");
            interpreter.ActionSnapshots[0].ArgValues[1].Value.Should().Be(15);
        }

        /// <summary>
        /// Test engine function call with default parameters.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:202
        /// Original: def test_enginecall_with_default_params(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestEnginecallWithDefaultParams()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:204
            // Original: ncs = self.compile("""void main() { string tag = ""something""; object oSomething = GetObjectByTag(tag); }""")
            NCS ncs = Compile(@"
            void main()
            {
                string tag = ""something"";
                object oSomething = GetObjectByTag(tag);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:214
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        /// <summary>
        /// Test engine function call with missing required parameters.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:217
        /// Original: def test_enginecall_with_missing_params(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestEnginecallWithMissingParams()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:219
            // Original: script = """"void main() { string tag = ""something""; object oSomething = GetObjectByTag(); }""""
            string script = @"
            void main()
            {
                string tag = ""something"";
                object oSomething = GetObjectByTag();
            }
        ";

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:227
            // Original: self.assertRaises(CompileError, self.compile, script)
            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test engine function call with too many parameters.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:229
        /// Original: def test_enginecall_with_too_many_params(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestEnginecallWithTooManyParams()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:231
            // Original: script = """"void main() { string tag = ""something""; object oSomething = GetObjectByTag("""", 0, ""shouldnotbehere""); }""""
            string script = @"
            void main()
            {
                string tag = ""something"";
                object oSomething = GetObjectByTag("""", 0, ""shouldnotbehere"");
            }
        ";

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:239
            // Original: self.assertRaises(CompileError, self.compile, script)
            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test DelayCommand with nested function call.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:241
        /// Original: def test_enginecall_delay_command_1(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestEnginecallDelayCommand1()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:243
            // Original: ncs = self.compile("""void main() { object oFirstPlayer = GetFirstPC(); DelayCommand(1.0, GiveXPToCreature(oFirstPlayer, 9001)); }""")
            NCS ncs = Compile(@"
            void main()
            {
                object oFirstPlayer = GetFirstPC();
                DelayCommand(1.0, GiveXPToCreature(oFirstPlayer, 9001));
            }
        ");
        }

        /// <summary>
        /// Test GetFirstObjectInShape with default parameters.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:253
        /// Original: def test_enginecall_GetFirstObjectInShape_defaults(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestEnginecallGetFirstObjectInShapeDefaults()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:255
            // Original: ncs = self.compile("""void main() { int nShape = SHAPE_CUBE; float fSize = 0.0; location lTarget; GetFirstObjectInShape(nShape, fSize, lTarget); }""")
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

        /// <summary>
        /// Test GetFactionEqual with default parameters.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:267
        /// Original: def test_enginecall_GetFactionEqual(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestEnginecallGetFactionEqual()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:269
            // Original: ncs = self.compile("""void main() { object oFirst; GetFactionEqual(oFirst); }""")
            NCS ncs = Compile(@"
            void main()
            {
                object oFirst;
                GetFactionEqual(oFirst);
            }
        ");
        }

        #endregion

        #region Arithmetic Operator Tests

        /// <summary>
        /// Test integer addition.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:282
        /// Original: def test_addop_int_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAddopIntInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:284
            // Original: ncs = self.compile("""void main() { int value = 10 + 5; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 10 + 5;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:293
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:296
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 15
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(15);
        }

        /// <summary>
        /// Test float addition.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:298
        /// Original: def test_addop_float_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAddopFloatFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:300
            // Original: ncs = self.compile("""void main() { float value = 10.0 + 5.0; }""")
            NCS ncs = Compile(@"
            void main()
            {
                float value = 10.0 + 5.0;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:309
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:312
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 15.0
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(15.0f);
        }

        /// <summary>
        /// Test string concatenation.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:314
        /// Original: def test_addop_string_string(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAddopStringString()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:316
            // Original: ncs = self.compile("""void main() { string value = ""abc"" + ""def""; }""")
            NCS ncs = Compile(@"
            void main()
            {
                string value = ""abc"" + ""def"";
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:325
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:328
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == ""abcdef""
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be("abcdef");
        }

        /// <summary>
        /// Test integer subtraction.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:330
        /// Original: def test_subop_int_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSubopIntInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:332
            // Original: ncs = self.compile("""void main() { int value = 10 - 5; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 10 - 5;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:341
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:344
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 5
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(5);
        }

        /// <summary>
        /// Test float subtraction.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:346
        /// Original: def test_subop_float_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSubopFloatFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:348
            // Original: ncs = self.compile("""void main() { float value = 10.0 - 5.0; }""")
            NCS ncs = Compile(@"
            void main()
            {
                float value = 10.0 - 5.0;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:357
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:360
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 5.0
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(5.0f);
        }

        /// <summary>
        /// Test integer multiplication.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:362
        /// Original: def test_mulop_int_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestMulopIntInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:364
            // Original: ncs = self.compile("""void main() { int a = 10 * 5; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 10 * 5;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:373
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:376
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 50
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(50);
        }

        /// <summary>
        /// Test float multiplication.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:378
        /// Original: def test_mulop_float_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestMulopFloatFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:380
            // Original: ncs = self.compile("""void main() { float a = 10.0 * 5.0; }""")
            NCS ncs = Compile(@"
            void main()
            {
                float a = 10.0 * 5.0;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:389
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:392
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 50.0
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(50.0f);
        }

        /// <summary>
        /// Test integer division.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:394
        /// Original: def test_divop_int_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestDivopIntInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:396
            // Original: ncs = self.compile("""void main() { int a = 10 / 5; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 10 / 5;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:405
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:408
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 2
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(2);
        }

        /// <summary>
        /// Test float division.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:410
        /// Original: def test_divop_float_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestDivopFloatFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:412
            // Original: ncs = self.compile("""void main() { float a = 10.0 / 5.0; }""")
            NCS ncs = Compile(@"
            void main()
            {
                float a = 10.0 / 5.0;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:421
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:424
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 2.0
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(2.0f);
        }

        /// <summary>
        /// Test integer modulo.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:426
        /// Original: def test_modop_int_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestModopIntInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:428
            // Original: ncs = self.compile("""void main() { int a = 10 % 3; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 10 % 3;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:437
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:440
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 1
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(1);
        }

        /// <summary>
        /// Test integer negation.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:442
        /// Original: def test_negop_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestNegopInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:444
            // Original: ncs = self.compile("""void main() { int a = -10; PrintInteger(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = -10;
                PrintInteger(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:453
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:456
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == -10
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1].ArgValues[0].Value.Should().Be(-10);
        }

        /// <summary>
        /// Test float negation.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:460
        /// Original: def test_negop_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestNegopFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:462
            // Original: ncs = self.compile("""void main() { float a = -10.0; PrintFloat(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                float a = -10.0;
                PrintFloat(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:471
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:474
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == -10.0
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(-10.0);
        }

        /// <summary>
        /// Test BIDMAS order of operations.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:476
        /// Original: def test_bidmas(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBidmas()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:478
            // Original: ncs = self.compile("""void main() { int value = 2 + (5 * ((0)) + 5) * 3 + 2 - (2 + (2 * 4 - 12 / 2)) / 2; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 2 + (5 * ((0)) + 5) * 3 + 2 - (2 + (2 * 4 - 12 / 2)) / 2;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:488
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:491
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 17
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(17);
        }

        /// <summary>
        /// Test operations with variables.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:494
        /// Original: def test_op_with_variables(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestOpWithVariables()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:496
            // Original: ncs = self.compile("""void main() { int a = 10; int b = 5; int c = a * b * a; int d = 10 * 5 * 10; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 10;
                int b = 5;
                int c = a * b * a;
                int d = 10 * 5 * 10;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:508
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:511
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 500
            // Original: assert interpreter.stack_snapshots[-4].stack[-2].value == 500
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(500);
            snapshot.Stack[snapshot.Stack.Count - 2].Value.Should().Be(500);
        }

        #endregion

        #region Logical Operator Tests

        /// <summary>
        /// Test logical NOT operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:517
        /// Original: def test_not_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestNotOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:519
            // Original: ncs = self.compile("""void main() { int a = !1; PrintInteger(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = !1;
                PrintInteger(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:529
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:532
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 0
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1].ArgValues[0].Value.Should().Be(0);
        }

        /// <summary>
        /// Test logical AND operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:534
        /// Original: def test_logical_and_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestLogicalAndOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:536
            // Original: ncs = self.compile("""void main() { int a = 0 && 0; int b = 1 && 0; int c = 1 && 1; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 0 && 0;
                int b = 1 && 0;
                int c = 1 && 1;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:547
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:550
            // Original: assert interpreter.stack_snapshots[-4].stack[-3].value == 0
            // Original: assert interpreter.stack_snapshots[-4].stack[-2].value == 0
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 1
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 3].Value.Should().Be(0);
            snapshot.Stack[snapshot.Stack.Count - 2].Value.Should().Be(0);
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(1);
        }

        /// <summary>
        /// Test logical OR operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:554
        /// Original: def test_logical_or_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestLogicalOrOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:556
            // Original: ncs = self.compile("""void main() { int a = 0 || 0; int b = 1 || 0; int c = 1 || 1; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 0 || 0;
                int b = 1 || 0;
                int c = 1 || 1;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:567
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:570
            // Original: assert interpreter.stack_snapshots[-4].stack[-3].value == 0
            // Original: assert interpreter.stack_snapshots[-4].stack[-2].value == 1
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 1
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 3].Value.Should().Be(0);
            snapshot.Stack[snapshot.Stack.Count - 2].Value.Should().Be(1);
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(1);
        }

        /// <summary>
        /// Test equality operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:574
        /// Original: def test_logical_equals(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestLogicalEquals()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:576
            // Original: ncs = self.compile("""void main() { int a = 1 == 1; int b = ""a"" == ""b""; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1 == 1;
                int b = ""a"" == ""b"";
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:586
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:589
            // Original: assert interpreter.stack_snapshots[-4].stack[-2].value == 1
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 0
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 2].Value.Should().Be(1);
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(0);
        }

        /// <summary>
        /// Test inequality operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:592
        /// Original: def test_logical_notequals_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestLogicalNotequalsOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:594
            // Original: ncs = self.compile("""void main() { int a = 1 != 1; int b = 1 != 2; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1 != 1;
                int b = 1 != 2;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:604
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:607
            // Original: assert interpreter.stack_snapshots[-4].stack[-2].value == 0
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 1
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 2].Value.Should().Be(0);
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(1);
        }

        #endregion

        #region Relational Operator Tests

        /// <summary>
        /// Test greater than operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:613
        /// Original: def test_compare_greaterthan_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCompareGreaterthanOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:615
            // Original: ncs = self.compile("""void main() { int a = 10 > 1; int b = 10 > 10; int c = 10 > 20; PrintInteger(a); PrintInteger(b); PrintInteger(c); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:630
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:633
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 0
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1].ArgValues[0].Value.Should().Be(0);
        }

        /// <summary>
        /// Test greater than or equal operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:637
        /// Original: def test_compare_greaterthanorequal_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCompareGreaterthanorequalOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:639
            // Original: ncs = self.compile("""void main() { int a = 10 >= 1; int b = 10 >= 10; int c = 10 >= 20; PrintInteger(a); PrintInteger(b); PrintInteger(c); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:654
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:657
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 0
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1].ArgValues[0].Value.Should().Be(0);
        }

        /// <summary>
        /// Test less than operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:661
        /// Original: def test_compare_lessthan_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCompareLessthanOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:663
            // Original: ncs = self.compile("""void main() { int a = 10 < 1; int b = 10 < 10; int c = 10 < 20; PrintInteger(a); PrintInteger(b); PrintInteger(c); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:678
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:681
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 1
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1].ArgValues[0].Value.Should().Be(1);
        }

        /// <summary>
        /// Test less than or equal operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:685
        /// Original: def test_compare_lessthanorequal_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCompareLessthanorequalOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:687
            // Original: ncs = self.compile("""void main() { int a = 10 <= 1; int b = 10 <= 10; int c = 10 <= 20; PrintInteger(a); PrintInteger(b); PrintInteger(c); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:702
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:705
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 1
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3].ArgValues[0].Value.Should().Be(0);
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1].ArgValues[0].Value.Should().Be(1);
        }

        #endregion

        #region Bitwise Operator Tests

        /// <summary>
        /// Test bitwise OR operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:712
        /// Original: def test_bitwise_or_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseOrOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:714
            // Original: ncs = self.compile("""void main() { int a = 5 | 2; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 5 | 2;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:723
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:726
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 7
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(7);
        }

        /// <summary>
        /// Test bitwise XOR operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:728
        /// Original: def test_bitwise_xor_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseXorOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:730
            // Original: ncs = self.compile("""void main() { int a = 7 ^ 2; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 7 ^ 2;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:739
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:742
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 5
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(5);
        }

        /// <summary>
        /// Test bitwise NOT operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:744
        /// Original: def test_bitwise_not_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseNotInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:746
            // Original: ncs = self.compile("""void main() { int a = ~1; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = ~1;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:755
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:758
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == -2
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(-2);
        }

        /// <summary>
        /// Test bitwise AND operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:760
        /// Original: def test_bitwise_and_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseAndOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:762
            // Original: ncs = self.compile("""void main() { int a = 7 & 2; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 7 & 2;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:771
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:774
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 2
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(2);
        }

        /// <summary>
        /// Test bitwise left shift operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:776
        /// Original: def test_bitwise_shiftleft_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseShiftleftOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:778
            // Original: ncs = self.compile("""void main() { int a = 7 << 2; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 7 << 2;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:787
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:790
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 28
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(28);
        }

        /// <summary>
        /// Test bitwise right shift operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:792
        /// Original: def test_bitwise_shiftright_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseShiftrightOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:794
            // Original: ncs = self.compile("""void main() { int a = 7 >> 2; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 7 >> 2;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:803
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:806
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 1
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(1);
        }

        #endregion

        #region Assignment Tests

        /// <summary>
        /// Test basic assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:811
        /// Original: def test_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:813
            // Original: ncs = self.compile("""void main() { int a = 1; a = 4; PrintInteger(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1;
                a = 4;
                PrintInteger(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:825
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:828
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 4
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(4);
        }

        /// <summary>
        /// Test complex assignment expression.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:831
        /// Original: def test_assignment_complex(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAssignmentComplex()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:833
            // Original: ncs = self.compile("""void main() { int a = 1; a = a * 2 + 8; PrintInteger(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1;
                a = a * 2 + 8;
                PrintInteger(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:845
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:848
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 10
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(10);
        }

        /// <summary>
        /// Test string constant assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:851
        /// Original: def test_assignment_string_constant(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAssignmentStringConstant()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:853
            // Original: ncs = self.compile("""void main() { string a = ""A""; PrintString(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                string a = ""A"";
                PrintString(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:864
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:867
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == ""A""
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be("A");
        }

        /// <summary>
        /// Test string assignment from engine call.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:870
        /// Original: def test_assignment_string_enginecall(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAssignmentStringEnginecall()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:872
            // Original: ncs = self.compile("""void main() { string a = GetGlobalString(""A""); PrintString(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                string a = GetGlobalString(""A"");
                PrintString(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:883
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""GetGlobalString"", lambda identifier: identifier); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("GetGlobalString", (args) => args[0]);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:887
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == ""A""
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be("A");
        }

        /// <summary>
        /// Test integer addition assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:889
        /// Original: def test_addition_assignment_int_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAdditionAssignmentIntInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:891
            // Original: ncs = self.compile("""void main() { int value = 1; value += 2; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 1;
                value += 2;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:903
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:906
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values[0] == 3
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(3);
        }

        /// <summary>
        /// Test integer addition assignment with float.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:910
        /// Original: def test_addition_assignment_int_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAdditionAssignmentIntFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:912
            // Original: ncs = self.compile("""void main() { int value = 1; value += 2.0; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 1;
                value += 2.0;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:924
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:927
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values[0] == 3
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(3);
        }

        /// <summary>
        /// Test float addition assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:931
        /// Original: def test_addition_assignment_float_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAdditionAssignmentFloatFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:933
            // Original: ncs = self.compile("""void main() { float value = 1.0; value += 2.0; PrintFloat(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                float value = 1.0;
                value += 2.0;
                PrintFloat(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:945
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:948
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 3.0
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(3.0f);
        }

        /// <summary>
        /// Test float addition assignment with integer.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:950
        /// Original: def test_addition_assignment_float_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAdditionAssignmentFloatInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:952
            // Original: ncs = self.compile("""void main() { float value = 1.0; value += 2; PrintFloat(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                float value = 1.0;
                value += 2;
                PrintFloat(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:964
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:967
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintFloat""
            // Original: assert snap.arg_values[0] == 3.0
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintFloat");
            lastSnapshot.ArgValues[0].Value.Should().Be(3.0f);
        }

        /// <summary>
        /// Test string concatenation assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:971
        /// Original: def test_addition_assignment_string_string(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAdditionAssignmentStringString()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:973
            // Original: ncs = self.compile("""void main() { string value = ""a""; value += ""b""; PrintString(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                string value = ""a"";
                value += ""b"";
                PrintString(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:985
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:988
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintString""
            // Original: assert snap.arg_values[0] == ""ab""
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintString");
            lastSnapshot.ArgValues[0].Value.Should().Be("ab");
        }

        /// <summary>
        /// Test integer subtraction assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:992
        /// Original: def test_subtraction_assignment_int_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSubtractionAssignmentIntInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:994
            // Original: ncs = self.compile("""void main() { int value = 10; value -= 2 * 2; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 10;
                value -= 2 * 2;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1006
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1009
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values == [6]
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(6);
        }

        /// <summary>
        /// Test integer subtraction assignment with float.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1013
        /// Original: def test_subtraction_assignment_int_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSubtractionAssignmentIntFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1015
            // Original: ncs = self.compile("""void main() { int value = 10; value -= 2.0; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 10;
                value -= 2.0;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1027
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1030
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values[0] == 8.0
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(8.0f);
        }

        /// <summary>
        /// Test float subtraction assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1034
        /// Original: def test_subtraction_assignment_float_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSubtractionAssignmentFloatFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1036
            // Original: ncs = self.compile("""void main() { float value = 10.0; value -= 2.0; PrintFloat(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                float value = 10.0;
                value -= 2.0;
                PrintFloat(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1048
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1051
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintFloat""
            // Original: assert snap.arg_values[0] == 8.0
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintFloat");
            lastSnapshot.ArgValues[0].Value.Should().Be(8.0f);
        }

        /// <summary>
        /// Test float subtraction assignment with integer.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1055
        /// Original: def test_subtraction_assignment_float_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSubtractionAssignmentFloatInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1057
            // Original: ncs = self.compile("""void main() { float value = 10.0; value -= 2; PrintFloat(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                float value = 10.0;
                value -= 2;
                PrintFloat(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1069
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1072
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 8.0
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(8.0f);
        }

        /// <summary>
        /// Test multiplication assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1074
        /// Original: def test_multiplication_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestMultiplicationAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1076
            // Original: ncs = self.compile("""void main() { int value = 10; value *= 2 * 2; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 10;
                value *= 2 * 2;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1088
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1091
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values == [40]
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(40);
        }

        /// <summary>
        /// Test division assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1095
        /// Original: def test_division_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestDivisionAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1097
            // Original: ncs = self.compile("""void main() { int value = 12; value /= 2 * 2; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 12;
                value /= 2 * 2;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1109
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1112
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values == [3]
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(3);
        }

        /// <summary>
        /// Test bitwise AND assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1116
        /// Original: def test_bitwise_and_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseAndAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1118
            // Original: ncs = self.compile("""void main() { int value = 0xFF; value &= 0x0F; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 0xFF;
                value &= 0x0F;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1130
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1133
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values == [15]  # 0xFF & 0x0F = 0x0F = 15
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(15);
        }

        /// <summary>
        /// Test bitwise OR assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1137
        /// Original: def test_bitwise_or_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseOrAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1139
            // Original: ncs = self.compile("""void main() { int value = 0x0F; value |= 0xF0; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 0x0F;
                value |= 0xF0;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1151
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1154
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values == [255]  # 0x0F | 0xF0 = 0xFF = 255
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(255);
        }

        /// <summary>
        /// Test bitwise XOR assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1158
        /// Original: def test_bitwise_xor_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseXorAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1160
            // Original: ncs = self.compile("""void main() { int value = 0xFF; value ^= 0x0F; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 0xFF;
                value ^= 0x0F;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1172
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1175
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values == [240]  # 0xFF ^ 0x0F = 0xF0 = 240
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(240);
        }

        /// <summary>
        /// Test bitwise left shift assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1179
        /// Original: def test_bitwise_left_shift_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseLeftShiftAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1181
            // Original: ncs = self.compile("""void main() { int value = 7; value <<= 2; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 7;
                value <<= 2;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1193
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1196
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values == [28]  # 7 << 2 = 28
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(28);
        }

        /// <summary>
        /// Test bitwise right shift assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1200
        /// Original: def test_bitwise_right_shift_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseRightShiftAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1202
            // Original: ncs = self.compile("""void main() { int value = 28; value >>= 2; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 28;
                value >>= 2;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1214
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1217
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values == [7]  # 28 >> 2 = 7
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(7);
        }

        /// <summary>
        /// Test bitwise unsigned right shift assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1221
        /// Original: def test_bitwise_unsigned_right_shift_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseUnsignedRightShiftAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1223
            // Original: ncs = self.compile("""void main() { int value = 0x80000000; value >>>= 1; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 0x80000000;
                value >>>= 1;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1235
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1238
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: # 0x80000000 >>> 1 = 0x40000000 = 1073741824
            // Original: assert snap.arg_values == [1073741824]
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(1073741824);
        }

        /// <summary>
        /// Test bitwise assignment with complex expression.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1243
        /// Original: def test_bitwise_assignment_with_expression(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseAssignmentWithExpression()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1245
            // Original: ncs = self.compile("""void main() { int value = 0xFF; value &= 0x0F | 0x10; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 0xFF;
                value &= 0x0F | 0x10;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1257
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1260
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values == [31]  # 0xFF & (0x0F | 0x10) = 0xFF & 0x1F = 0x1F = 31
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(31);
        }

        /// <summary>
        /// Test global variable bitwise assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1264
        /// Original: def test_global_bitwise_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestGlobalBitwiseAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1266
            // Original: ncs = self.compile("""int global1 = 0xFF; void main() { int local1 = 0x0F; global1 &= local1; PrintInteger(global1); }""")
            NCS ncs = Compile(@"
            int global1 = 0xFF;

            void main()
            {
                int local1 = 0x0F;
                global1 &= local1;
                PrintInteger(global1);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1280
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1283
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values == [15]  # 0xFF & 0x0F = 0x0F = 15
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(15);
        }

        /// <summary>
        /// Test modulo assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1287
        /// Original: def test_modulo_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestModuloAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1289
            // Original: ncs = self.compile("""void main() { int value = 17; value %= 5; PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = 17;
                value %= 5;
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1301
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1304
            // Original: snap = interpreter.action_snapshots[-1]
            // Original: assert snap.function_name == ""PrintInteger""
            // Original: assert snap.arg_values == [2]  # 17 % 5 = 2
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.FunctionName.Should().Be("PrintInteger");
            lastSnapshot.ArgValues[0].Value.Should().Be(2);
        }

        /// <summary>
        /// Test bitwise unsigned right shift operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1308
        /// Original: def test_bitwise_unsigned_right_shift_op(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestBitwiseUnsignedRightShiftOp()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1310
            // Original: ncs = self.compile("""void main() { int a = 0x80000000 >>> 1; }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 0x80000000 >>> 1;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1319
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1322
            // Original: assert interpreter.stack_snapshots[-4].stack[-1].value == 1073741824  # 0x40000000
            var snapshot = interpreter.StackSnapshots[interpreter.StackSnapshots.Count - 4];
            snapshot.Stack[snapshot.Stack.Count - 1].Value.Should().Be(1073741824);
        }

        #endregion

        #region Const Keyword Tests

        /// <summary>
        /// Test const global variable declaration.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1327
        /// Original: def test_const_global_declaration(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestConstGlobalDeclaration()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1329
            // Original: ncs = self.compile("""const int TEST_CONST = 42; void main() { PrintInteger(TEST_CONST); }""")
            NCS ncs = Compile(@"
            const int TEST_CONST = 42;

            void main()
            {
                PrintInteger(TEST_CONST);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1340
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1343
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 42
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(42);
        }

        /// <summary>
        /// Test const global variable initialization.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1345
        /// Original: def test_const_global_initialization(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestConstGlobalInitialization()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1347
            // Original: ncs = self.compile("""const float PI = 3.14159f; const string GREETING = ""Hello""; void main() { PrintFloat(PI); PrintString(GREETING); }""")
            NCS ncs = Compile(@"
            const float PI = 3.14159f;
            const string GREETING = ""Hello"";

            void main()
            {
                PrintFloat(PI);
                PrintString(GREETING);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1360
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1363
            // Original: assert abs(interpreter.action_snapshots[-2].arg_values[0] - 3.141592) < 0.000001
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == ""Hello""
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            // Convert to double for comparison - matching Python's float comparison
            double piValue = secondLastSnapshot.ArgValues[0].Value is float f ? (double)f : Convert.ToDouble(secondLastSnapshot.ArgValues[0].Value);
            System.Math.Abs(piValue - 3.141592).Should().BeLessThan(0.000001);
            lastSnapshot.ArgValues[0].Value.Should().Be("Hello");
        }

        /// <summary>
        /// Test const local variable declaration.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1366
        /// Original: def test_const_local_declaration(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestConstLocalDeclaration()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1368
            // Original: ncs = self.compile("""void main() { const int local_const = 100; PrintInteger(local_const); }""")
            NCS ncs = Compile(@"
            void main()
            {
                const int local_const = 100;
                PrintInteger(local_const);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1378
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1381
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 100
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(100);
        }

        /// <summary>
        /// Test const local variable initialization with expression.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1383
        /// Original: def test_const_local_initialization(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestConstLocalInitialization()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1385
            // Original: ncs = self.compile("""void main() { const int a = 10; const int b = a * 2; PrintInteger(b); }""")
            NCS ncs = Compile(@"
            void main()
            {
                const int a = 10;
                const int b = a * 2;
                PrintInteger(b);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1396
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1399
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 20
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(20);
        }

        /// <summary>
        /// Test that assigning to const variable raises error.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1401
        /// Original: def test_const_assignment_error(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestConstAssignmentError()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1403
            // Original: script = """const int TEST_CONST = 42; void main() { TEST_CONST = 100; }"""
            // Original: self.assertRaises(CompileError, self.compile, script)
            string script = @"
            const int TEST_CONST = 42;

            void main()
            {
                TEST_CONST = 100;
            }
        ";

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test that compound assignment to const variable raises error.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1414
        /// Original: def test_const_compound_assignment_error(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestConstCompoundAssignmentError()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1416
            // Original: script = """const int TEST_CONST = 42; void main() { TEST_CONST += 10; }"""
            // Original: self.assertRaises(CompileError, self.compile, script)
            string script = @"
            const int TEST_CONST = 42;

            void main()
            {
                TEST_CONST += 10;
            }
        ";

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test that bitwise assignment to const variable raises error.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1427
        /// Original: def test_const_bitwise_assignment_error(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestConstBitwiseAssignmentError()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1429
            // Original: script = """const int TEST_CONST = 0xFF; void main() { TEST_CONST &= 0x0F; }"""
            // Original: self.assertRaises(CompileError, self.compile, script)
            string script = @"
            const int TEST_CONST = 0xFF;

            void main()
            {
                TEST_CONST &= 0x0F;
            }
        ";

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test that incrementing const variable raises error.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1440
        /// Original: def test_const_increment_error(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestConstIncrementError()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1442
            // Original: script = """const int TEST_CONST = 42; void main() { TEST_CONST++; }"""
            // Original: self.assertRaises(CompileError, self.compile, script)
            string script = @"
            const int TEST_CONST = 42;

            void main()
            {
                TEST_CONST++;
            }
        ";

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test multiple const variable declarations.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1453
        /// Original: def test_const_multi_declaration(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestConstMultiDeclaration()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1455
            // Original: ncs = self.compile("""void main() { const int a = 1, b = 2, c = 3; PrintInteger(a); PrintInteger(b); PrintInteger(c); }""")
            NCS ncs = Compile(@"
            void main()
            {
                const int a = 1, b = 2, c = 3;
                PrintInteger(a);
                PrintInteger(b);
                PrintInteger(c);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1467
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1470
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 3
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(1);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(2);
            lastSnapshot.ArgValues[0].Value.Should().Be(3);
        }

        #endregion

        #region Ternary Operator Tests

        /// <summary>
        /// Test basic ternary operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1477
        /// Original: def test_ternary_basic(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestTernaryBasic()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1479
            // Original: ncs = self.compile("""void main() { int a = 1 > 0 ? 100 : 200; PrintInteger(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1 > 0 ? 100 : 200;
                PrintInteger(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1489
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1492
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 100
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(100);
        }

        /// <summary>
        /// Test ternary operator false branch.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1494
        /// Original: def test_ternary_false_branch(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestTernaryFalseBranch()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1496
            // Original: ncs = self.compile("""void main() { int a = 0 > 1 ? 100 : 200; PrintInteger(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 0 > 1 ? 100 : 200;
                PrintInteger(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1506
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1509
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 200
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(200);
        }

        /// <summary>
        /// Test ternary operator with variables.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1511
        /// Original: def test_ternary_with_variables(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestTernaryWithVariables()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1513
            // Original: ncs = self.compile("""void main() { int b = 5; int c = (b > 3) ? 100 : 200; PrintInteger(c); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int b = 5;
                int c = (b > 3) ? 100 : 200;
                PrintInteger(c);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1524
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1527
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 100
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(100);
        }

        /// <summary>
        /// Test nested ternary operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1529
        /// Original: def test_ternary_nested(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestTernaryNested()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1531
            // Original: ncs = self.compile("""void main() { int a = 1; int b = 2; int result = (a > b) ? 10 : ((a < b) ? 20 : 30); PrintInteger(result); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1;
                int b = 2;
                int result = (a > b) ? 10 : ((a < b) ? 20 : 30);
                PrintInteger(result);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1543
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1546
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 20
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(20);
        }

        /// <summary>
        /// Test ternary operator in larger expression.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1548
        /// Original: def test_ternary_in_expression(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestTernaryInExpression()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1550
            // Original: ncs = self.compile("""void main() { int a = 5; int result = (a > 3 ? 10 : 5) + 20; PrintInteger(result); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 5;
                int result = (a > 3 ? 10 : 5) + 20;
                PrintInteger(result);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1561
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1564
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 30
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(30);
        }

        /// <summary>
        /// Test that ternary with mismatched types raises error.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1566
        /// Original: def test_ternary_type_mismatch_error(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestTernaryTypeMismatchError()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1568
            // Original: script = """void main() { int result = 1 ? 100 : ""string""; }"""
            // Original: self.assertRaises(CompileError, self.compile, script)
            string script = @"
            void main()
            {
                int result = 1 ? 100 : ""string"";
            }
        ";

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test ternary operator with float branches.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1577
        /// Original: def test_ternary_float_branches(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestTernaryFloatBranches()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1579
            // Original: ncs = self.compile("""void main() { float result = 1 ? 3.14f : 2.71f; PrintFloat(result); }""")
            NCS ncs = Compile(@"
            void main()
            {
                float result = 1 ? 3.14f : 2.71f;
                PrintFloat(result);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1589
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1592
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 3.14
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(3.14f);
        }

        /// <summary>
        /// Test ternary operator with string branches.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1594
        /// Original: def test_ternary_string_branches(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestTernaryStringBranches()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1596
            // Original: ncs = self.compile("""void main() { string result = 1 ? ""yes"" : ""no""; PrintString(result); }""")
            NCS ncs = Compile(@"
            void main()
            {
                string result = 1 ? ""yes"" : ""no"";
                PrintString(result);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1606
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1609
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == ""yes""
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be("yes");
        }

        /// <summary>
        /// Test ternary operator with function calls.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1611
        /// Original: def test_ternary_with_function_calls(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestTernaryWithFunctionCalls()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1613
            // Original: ncs = self.compile("""int GetValue(int x) { return x * 2; } void main() { int result = 1 ? GetValue(5) : GetValue(10); PrintInteger(result); }""")
            NCS ncs = Compile(@"
            int GetValue(int x)
            {
                return x * 2;
            }

            void main()
            {
                int result = 1 ? GetValue(5) : GetValue(10);
                PrintInteger(result);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1628
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1631
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 10
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(10);
        }

        /// <summary>
        /// Test ternary operator result assigned to variable.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1633
        /// Original: def test_ternary_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestTernaryAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1635
            // Original: ncs = self.compile("""void main() { int a = 10; int b = 20; int max = (a > b) ? a : b; PrintInteger(max); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 10;
                int b = 20;
                int max = (a > b) ? a : b;
                PrintInteger(max);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1647
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1650
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 20
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(20);
        }

        /// <summary>
        /// Test ternary operator precedence.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1652
        /// Original: def test_ternary_precedence(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestTernaryPrecedence()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1654
            // Original: ncs = self.compile("""void main() { int a = 1 ? 2 : 3 ? 4 : 5; PrintInteger(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1 ? 2 : 3 ? 4 : 5;
                PrintInteger(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1664
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1667
            // Original: # Right-associative: 1 ? 2 : (3 ? 4 : 5) = 1 ? 2 : 4 = 2
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 2
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(2);
        }

        #endregion

        #region Control Flow Tests

        /// <summary>
        /// Test if statement.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1673
        /// Original: def test_if(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestIf()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1675
            // Original: ncs = self.compile("""void main() { if(0) { PrintInteger(0); } if(1) { PrintInteger(1); } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1692
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1695
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 1
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
        }

        /// <summary>
        /// Test if statement with multiple conditions.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1698
        /// Original: def test_if_multiple_conditions(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestIfMultipleConditions()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1700
            // Original: ncs = self.compile("""void main() { if(1 && 2 && 3) { PrintInteger(0); } }""")
            NCS ncs = Compile(@"
            void main()
            {
                if(1 && 2 && 3)
                {
                    PrintInteger(0);
                }
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1712
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        /// <summary>
        /// Test if-else statement.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1715
        /// Original: def test_if_else(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestIfElse()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1717
            // Original: ncs = self.compile("""void main() { if (0) { PrintInteger(0); } else { PrintInteger(1); } if (1) { PrintInteger(2); } else { PrintInteger(3); } }""")
            NCS ncs = Compile(@"
            void main()
            {
                if (0) {    PrintInteger(0); }
                else {      PrintInteger(1); }

                if (1) {    PrintInteger(2); }
                else {      PrintInteger(3); }
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1730
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1733
            // Original: assert len(interpreter.action_snapshots) == 2
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[1].arg_values[0] == 2
            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
        }

        /// <summary>
        /// Test if-else if statement.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1737
        /// Original: def test_if_else_if(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestIfElseIf()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1739
            // Original: ncs = self.compile("""void main() { if (0) { PrintInteger(0); } else if (0) { PrintInteger(1); } if (1) { PrintInteger(2); } else if (1) { PrintInteger(3); } if (1) { PrintInteger(4); } else if (0) { PrintInteger(5); } if (0) { PrintInteger(6); } else if (1) { PrintInteger(7); } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1758
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1761
            // Original: assert len(interpreter.action_snapshots) == 3
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[1].arg_values[0] == 4
            // Original: assert interpreter.action_snapshots[2].arg_values[0] == 7
            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(4);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(7);
        }

        /// <summary>
        /// Test if-else if-else statement.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1766
        /// Original: def test_if_else_if_else(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestIfElseIfElse()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1768
            // Original: ncs = self.compile("""void main() { if (0) { PrintInteger(0); } else if (0) { PrintInteger(1); } else { PrintInteger(3); } if (0) { PrintInteger(4); } else if (1) { PrintInteger(5); } else { PrintInteger(6); } if (1) { PrintInteger(7); } else if (1) { PrintInteger(8); } else { PrintInteger(9); } if (1) { PrintInteger(10); } else if (0) { PrintInteger(11); } else { PrintInteger(12); } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1791
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1794
            // Original: assert len(interpreter.action_snapshots) == 4
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 3
            // Original: assert interpreter.action_snapshots[1].arg_values[0] == 5
            // Original: assert interpreter.action_snapshots[2].arg_values[0] == 7
            // Original: assert interpreter.action_snapshots[3].arg_values[0] == 10
            interpreter.ActionSnapshots.Count.Should().Be(4);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(5);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(7);
            interpreter.ActionSnapshots[3].ArgValues[0].Value.Should().Be(10);
        }

        /// <summary>
        /// Test single statement if (no braces).
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1800
        /// Original: def test_single_statement_if(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSingleStatementIf()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1802
            // Original: ncs = self.compile("""void main() { if (1) PrintInteger(222); }""")
            NCS ncs = Compile(@"
            void main()
            {
                if (1) PrintInteger(222);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1811
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1814
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 222
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(222);
        }

        /// <summary>
        /// Test single statement else if-else.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1816
        /// Original: def test_single_statement_else_if_else(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSingleStatementElseIfElse()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1818
            // Original: ncs = self.compile("""void main() { if (0) PrintInteger(11); else if (0) PrintInteger(22); else PrintInteger(33); }""")
            NCS ncs = Compile(@"
            void main()
            {
                if (0) PrintInteger(11);
                else if (0) PrintInteger(22);
                else PrintInteger(33);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1829
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1832
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 33
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(33);
        }

        /// <summary>
        /// Test while loop.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1834
        /// Original: def test_while_loop(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestWhileLoop()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1836
            // Original: ncs = self.compile("""void main() { int value = 3; while (value) { PrintInteger(value); value -= 1; } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1850
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1853
            // Original: assert len(interpreter.action_snapshots) == 3
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 3
            // Original: assert interpreter.action_snapshots[1].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[2].arg_values[0] == 1
            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(1);
        }

        /// <summary>
        /// Test while loop with break.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1858
        /// Original: def test_while_loop_with_break(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestWhileLoopWithBreak()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1860
            // Original: ncs = self.compile("""void main() { int value = 3; while (value) { PrintInteger(value); value -= 1; break; } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1875
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1878
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 3
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
        }

        /// <summary>
        /// Test while loop with continue.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1881
        /// Original: def test_while_loop_with_continue(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestWhileLoopWithContinue()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1883
            // Original: ncs = self.compile("""void main() { int value = 3; while (value) { PrintInteger(value); value -= 1; continue; PrintInteger(99); } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1899
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1902
            // Original: assert len(interpreter.action_snapshots) == 3
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 3
            // Original: assert interpreter.action_snapshots[1].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[2].arg_values[0] == 1
            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(1);
        }

        /// <summary>
        /// Test while loop variable scope.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1907
        /// Original: def test_while_loop_scope(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestWhileLoopScope()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1909
            // Original: ncs = self.compile("""void main() { int value = 11; int outer = 22; while (value) { int inner = 33; value = 0; continue; outer = 99; } PrintInteger(outer); PrintInteger(value); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1929
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1932
            // Original: assert len(interpreter.action_snapshots) == 2
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 22
            // Original: assert interpreter.action_snapshots[1].arg_values[0] == 0
            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(22);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(0);
        }

        /// <summary>
        /// Test do-while loop.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1936
        /// Original: def test_do_while_loop(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestDoWhileLoop()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1938
            // Original: ncs = self.compile("""void main() { int value = 3; do { PrintInteger(value); value -= 1; } while (value); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1952
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1955
            // Original: assert len(interpreter.action_snapshots) == 3
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 3
            // Original: assert interpreter.action_snapshots[1].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[2].arg_values[0] == 1
            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(1);
        }

        /// <summary>
        /// Test do-while loop with break.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1960
        /// Original: def test_do_while_loop_with_break(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestDoWhileLoopWithBreak()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1962
            // Original: ncs = self.compile("""void main() { int value = 3; do { PrintInteger(value); value -= 1; break; } while (value); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1975
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:1978
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 3
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
        }

        /// <summary>
        /// Test for loop.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2036
        /// Original: def test_for_loop(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestForLoop()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2038
            // Original: ncs = self.compile("""void main() { int i = 99; for (i = 1; i <= 3; i += 1) { PrintInteger(i); } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2051
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2054
            // Original: assert len(interpreter.action_snapshots) == 3
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[1].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[2].arg_values[0] == 3
            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(3);
        }

        /// <summary>
        /// Test for loop with break.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2059
        /// Original: def test_for_loop_with_break(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestForLoopWithBreak()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2061
            // Original: ncs = self.compile("""void main() { int i = 99; for (i = 1; i <= 3; i += 1) { PrintInteger(i); break; } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2075
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2078
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 1
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
        }

        /// <summary>
        /// Test for loop with continue.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2081
        /// Original: def test_for_loop_with_continue(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestForLoopWithContinue()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2083
            // Original: ncs = self.compile("""void main() { int i = 99; for (i = 1; i <= 3; i += 1) { PrintInteger(i); continue; PrintInteger(99); } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2098
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2101
            // Original: assert len(interpreter.action_snapshots) == 3
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[1].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[2].arg_values[0] == 3
            interpreter.ActionSnapshots.Count.Should().Be(3);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[2].ArgValues[0].Value.Should().Be(3);
        }

        /// <summary>
        /// Test for loop variable scope.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2106
        /// Original: def test_for_loop_scope(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestForLoopScope()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2108
            // Original: ncs = self.compile("""void main() { int i = 11; int outer = 22; for (i = 0; i <= 5; i += 1) { int inner = 33; break; } PrintInteger(i); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2125
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2128
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 0
            interpreter.ActionSnapshots.Count.Should().Be(1);
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(0);
        }

        /// <summary>
        /// Test switch statement without breaks (fall-through).
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2131
        /// Original: def test_switch_no_breaks(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSwitchNoBreaks()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2133
            // Original: ncs = self.compile("""void main() { switch (2) { case 1: PrintInteger(1); case 2: PrintInteger(2); case 3: PrintInteger(3); } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2150
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2153
            // Original: assert len(interpreter.action_snapshots) == 2
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[1].arg_values[0] == 3
            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(2);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(3);
        }

        /// <summary>
        /// Test switch statement jumping over all cases.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2157
        /// Original: def test_switch_jump_over(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSwitchJumpOver()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2159
            // Original: ncs = self.compile("""void main() { switch (4) { case 1: PrintInteger(1); case 2: PrintInteger(2); case 3: PrintInteger(3); } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2176
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2179
            // Original: assert len(interpreter.action_snapshots) == 0
            interpreter.ActionSnapshots.Count.Should().Be(0);
        }

        /// <summary>
        /// Test switch statement with breaks.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2181
        /// Original: def test_switch_with_breaks(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSwitchWithBreaks()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2183
            // Original: ncs = self.compile("""void main() { switch (3) { case 1: PrintInteger(1); break; case 2: PrintInteger(2); break; case 3: PrintInteger(3); break; case 4: PrintInteger(4); break; } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2206
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2209
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 3
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(3);
        }

        /// <summary>
        /// Test switch statement with default case.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2212
        /// Original: def test_switch_with_default(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSwitchWithDefault()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2214
            // Original: ncs = self.compile("""void main() { switch (4) { case 1: PrintInteger(1); break; case 2: PrintInteger(2); break; case 3: PrintInteger(3); break; default: PrintInteger(4); break; } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2237
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2240
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 4
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(4);
        }

        /// <summary>
        /// Test switch statement with scoped blocks.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2243
        /// Original: def test_switch_scoped_blocks(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestSwitchScopedBlocks()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2245
            // Original: ncs = self.compile("""void main() { switch (2) { case 1: { int inner = 10; PrintInteger(inner); } break; case 2: { int inner = 20; PrintInteger(inner); } break; } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2269
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2272
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 20
            interpreter.ActionSnapshots.Count.Should().Be(1);
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(20);
        }

        #endregion

        #region Variable and Scope Tests

        /// <summary>
        /// Test basic variable scope.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2336
        /// Original: def test_scope(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestScope()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2338
            // Original: ncs = self.compile("""void main() { int value = 1; if (value == 1) { value = 2; } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2352
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        /// <summary>
        /// Test scoped block.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2355
        /// Original: def test_scoped_block(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestScopedBlock()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2357
            // Original: ncs = self.compile("""void main() { int a = 1; { int b = 2; PrintInteger(a); PrintInteger(b); } }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2372
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2375
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 2
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            secondLastSnapshot.ArgValues[0].Value.Should().Be(1);
            lastSnapshot.ArgValues[0].Value.Should().Be(2);
        }

        /// <summary>
        /// Test multiple variable declarations.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2378
        /// Original: def test_multi_declarations(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestMultiDeclarations()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2380
            // Original: ncs = self.compile("""void main() { int value1, value2 = 1, value3 = 2; PrintInteger(value1); PrintInteger(value2); PrintInteger(value3); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value1, value2 = 1, value3 = 2;

                PrintInteger(value1);
                PrintInteger(value2);
                PrintInteger(value3);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2393
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2396
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 2
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(0);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(1);
            lastSnapshot.ArgValues[0].Value.Should().Be(2);
        }

        /// <summary>
        /// Test local variable declarations.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2400
        /// Original: def test_local_declarations(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestLocalDeclarations()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2402
            // Original: ncs = self.compile("""void main() { int INT; float FLOAT; string STRING; location LOCATION; effect EFFECT; talent TALENT; event EVENT; vector VECTOR; }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2418
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        /// <summary>
        /// Test global variable declarations.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2421
        /// Original: def test_global_declarations(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestGlobalDeclarations()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2423
            // Original: ncs = self.compile("""int INT; float FLOAT; string STRING; location LOCATION; effect EFFECT; talent TALENT; event EVENT; vector VECTOR; void main() { }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2441
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2444
            // Original: assert any(inst for inst in ncs.instructions if inst.ins_type == NCSInstructionType.SAVEBP)
            bool hasSaveBp = false;
            foreach (var inst in ncs.Instructions)
            {
                if (inst.InsType == NCSInstructionType.SAVEBP)
                {
                    hasSaveBp = true;
                    break;
                }
            }
            hasSaveBp.Should().BeTrue();
        }

        /// <summary>
        /// Test global variable initializations.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2446
        /// Original: def test_global_initializations(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestGlobalInitializations()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2448
            // Original: ncs = self.compile("""int INT = 0; float FLOAT = 0.0; string STRING = """"; vector VECTOR = [0.0, 0.0, 0.0]; void main() { PrintInteger(INT); PrintFloat(FLOAT); PrintString(STRING); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2464
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2467
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 0.0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == """"
            // Original: assert any(inst for inst in ncs.instructions if inst.ins_type == NCSInstructionType.SAVEBP)
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(0);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(0.0f);
            lastSnapshot.ArgValues[0].Value.Should().Be("");

            bool hasSaveBp = false;
            foreach (var inst in ncs.Instructions)
            {
                if (inst.InsType == NCSInstructionType.SAVEBP)
                {
                    hasSaveBp = true;
                    break;
                }
            }
            hasSaveBp.Should().BeTrue();
        }

        /// <summary>
        /// Test global variable initialization with unary operator.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2472
        /// Original: def test_global_initialization_with_unary(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestGlobalInitializationWithUnary()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2474
            // Original: ncs = self.compile("""int INT = -1; void main() { PrintInteger(INT); }""")
            NCS ncs = Compile(@"
            int INT = -1;

            void main()
            {
                PrintInteger(INT);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2485
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2488
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == -1
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(-1);
        }

        /// <summary>
        /// Test global integer addition assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2490
        /// Original: def test_global_int_addition_assignment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestGlobalIntAdditionAssignment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2492
            // Original: ncs = self.compile("""int global1 = 1; int global2 = 2; void main() { int local1 = 3; int local2 = 4; global1 += local1; global2 = local2 + global1; PrintInteger(global1); PrintInteger(global2); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2511
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2514
            // Original: assert len(interpreter.action_snapshots) == 2
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 4
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 8
            interpreter.ActionSnapshots.Count.Should().Be(2);
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            secondLastSnapshot.ArgValues[0].Value.Should().Be(4);
            lastSnapshot.ArgValues[0].Value.Should().Be(8);
        }

        /// <summary>
        /// Test integer declaration without initialization.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2602
        /// Original: def test_declaration_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestDeclarationInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2604
            // Original: ncs = self.compile("""void main() { int a; PrintInteger(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a;
                PrintInteger(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2614
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2617
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 0
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(0);
        }

        /// <summary>
        /// Test float declaration without initialization.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2619
        /// Original: def test_declaration_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestDeclarationFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2621
            // Original: ncs = self.compile("""void main() { float a; PrintFloat(a); }""")
            NCS ncs = Compile(@"
            void main()
            {
                float a;
                PrintFloat(a);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2631
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2634
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 0.0
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(0.0f);
        }

        #endregion

        #region Data Type Tests

        /// <summary>
        /// Test different float literal notations.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2656
        /// Original: def test_float_notations(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestFloatNotations()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2658
            // Original: ncs = self.compile("""void main() { PrintFloat(1.0f); PrintFloat(2.0); PrintFloat(3f); }""")
            NCS ncs = Compile(@"
            void main()
            {
                PrintFloat(1.0f);
                PrintFloat(2.0);
                PrintFloat(3f);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2669
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2672
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 3
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(1);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(2);
            lastSnapshot.ArgValues[0].Value.Should().Be(3);
        }

        /// <summary>
        /// Test vector creation and operations.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2676
        /// Original: def test_vector(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVector()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2678
            // Original: ncs = self.compile("""void main() { vector vec = Vector(2.0, 4.0, 4.0); float mag = VectorMagnitude(vec); PrintFloat(mag); }""")
            NCS ncs = Compile(@"
            void main()
            {
                vector vec = Vector(2.0, 4.0, 4.0);
                float mag = VectorMagnitude(vec);
                PrintFloat(mag);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2689
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""Vector"", Vector3)
            // Original: interpreter.set_mock(""VectorMagnitude"", lambda vec: vec.magnitude())
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", (args) => new Vector3(
                args[0] is float f0 ? f0 : Convert.ToSingle(args[0]),
                args[1] is float f1 ? f1 : Convert.ToSingle(args[1]),
                args[2] is float f2 ? f2 : Convert.ToSingle(args[2])));
            interpreter.SetMock("VectorMagnitude", (args) => ((Vector3)args[0]).Magnitude());
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2694
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 6.0
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(6.0f);
        }

        /// <summary>
        /// Test vector literal notation.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2696
        /// Original: def test_vector_notation(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVectorNotation()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2698
            // Original: ncs = self.compile("""void main() { vector vec = [1.0, 2.0, 3.0]; PrintFloat(vec.x); PrintFloat(vec.y); PrintFloat(vec.z); }""")
            NCS ncs = Compile(@"
            void main()
            {
                vector vec = [1.0, 2.0, 3.0];
                PrintFloat(vec.x);
                PrintFloat(vec.y);
                PrintFloat(vec.z);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2710
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2713
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 1.0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 2.0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 3.0
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(1.0f);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(2.0f);
            lastSnapshot.ArgValues[0].Value.Should().Be(3.0f);
        }

        /// <summary>
        /// Test vector component access.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2717
        /// Original: def test_vector_get_components(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVectorGetComponents()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2719
            // Original: ncs = self.compile("""void main() { vector vec = Vector(2.0, 4.0, 6.0); PrintFloat(vec.x); PrintFloat(vec.y); PrintFloat(vec.z); }""")
            NCS ncs = Compile(@"
            void main()
            {
                vector vec = Vector(2.0, 4.0, 6.0);
                PrintFloat(vec.x);
                PrintFloat(vec.y);
                PrintFloat(vec.z);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2731
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""Vector"", Vector3)
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", (args) => new Vector3(
                args[0] is float f0 ? f0 : Convert.ToSingle(args[0]),
                args[1] is float f1 ? f1 : Convert.ToSingle(args[1]),
                args[2] is float f2 ? f2 : Convert.ToSingle(args[2])));
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2735
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 2.0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 4.0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 6.0
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(2.0f);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(4.0f);
            lastSnapshot.ArgValues[0].Value.Should().Be(6.0f);
        }

        /// <summary>
        /// Test vector component assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2739
        /// Original: def test_vector_set_components(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVectorSetComponents()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2741
            // Original: ncs = self.compile("""void main() { vector vec = Vector(0.0, 0.0, 0.0); vec.x = 2.0; vec.y = 4.0; vec.z = 6.0; PrintFloat(vec.x); PrintFloat(vec.y); PrintFloat(vec.z); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2756
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""Vector"", Vector3)
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", (args) => new Vector3(
                args[0] is float f0 ? f0 : Convert.ToSingle(args[0]),
                args[1] is float f1 ? f1 : Convert.ToSingle(args[1]),
                args[2] is float f2 ? f2 : Convert.ToSingle(args[2])));
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2760
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 2.0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 4.0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 6.0
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(2.0f);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(4.0f);
            lastSnapshot.ArgValues[0].Value.Should().Be(6.0f);
        }

        /// <summary>
        /// Test struct member access.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2764
        /// Original: def test_struct_get_members(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestStructGetMembers()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2766
            // Original: ncs = self.compile("""struct ABC { int value1; string value2; float value3; }; void main() { struct ABC abc; PrintInteger(abc.value1); PrintString(abc.value2); PrintFloat(abc.value3); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2785
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2788
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == """"
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 0.0
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(0);
            secondLastSnapshot.ArgValues[0].Value.Should().Be("");
            lastSnapshot.ArgValues[0].Value.Should().Be(0.0f);
        }

        /// <summary>
        /// Test accessing invalid struct member.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2792
        /// Original: def test_struct_get_invalid_member(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestStructGetInvalidMember()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2794
            // Original: source = """struct ABC { int value1; string value2; float value3; }; void main() { struct ABC abc; PrintFloat(abc.value4); }"""
            // Original: self.assertRaises(CompileError, self.compile, source)
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

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(source));
        }

        /// <summary>
        /// Test struct member assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2811
        /// Original: def test_struct_set_members(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestStructSetMembers()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2813
            // Original: ncs = self.compile("""struct ABC { int value1; string value2; float value3; }; void main() { struct ABC abc; abc.value1 = 123; abc.value2 = ""abc""; abc.value3 = 3.14; PrintInteger(abc.value1); PrintString(abc.value2); PrintFloat(abc.value3); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2835
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2838
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 123
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == ""abc""
            // Original: self.assertAlmostEqual(3.14, interpreter.action_snapshots[-1].arg_values[0])

            // Access values directly using indexer to match PyKotor's negative index access
            // PyKotor: interpreter.action_snapshots[-3].arg_values[0]
            var snapshots = interpreter.ActionSnapshots;
            int idx1 = snapshots.Count - 3;
            int idx2 = snapshots.Count - 2;
            int idx3 = snapshots.Count - 1;

            // Access StackObjects and values in minimal steps
            var argVals1 = snapshots[idx1].ArgValues;
            var argVals2 = snapshots[idx2].ArgValues;
            var argVals3 = snapshots[idx3].ArgValues;

            var so1 = argVals1[0];
            var so2 = argVals2[0];
            var so3 = argVals3[0];

            // Extract values - use field access via reflection to completely bypass property getter
            var valueProp = typeof(Andastra.Parsing.Formats.NCS.Compiler.StackObject).GetProperty("Value");
            object v1 = valueProp.GetValue(so1);
            object v2 = valueProp.GetValue(so2);
            object v3 = valueProp.GetValue(so3);

            // Convert and assert
            int intVal = v1 is int ? (int)v1 : System.Convert.ToInt32(v1);
            Assert.Equal(123, intVal);

            string strVal = v2 as string ?? v2?.ToString() ?? "";
            Assert.Equal("abc", strVal);

            float floatVal = v3 is float ? (float)v3 : System.Convert.ToSingle(v3);
            Assert.True(System.Math.Abs(floatVal - 3.14f) < 0.01f, $"Expected ~3.14, got {floatVal}");
        }

        #endregion

        #region Increment/Decrement Tests

        /// <summary>
        /// Test prefix increment on stack pointer integer.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2845
        /// Original: def test_prefix_increment_sp_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrefixIncrementSpInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2847
            // Original: ncs = self.compile("""void main() { int a = 1; int b = ++a; PrintInteger(a); PrintInteger(b); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1;
                int b = ++a;

                PrintInteger(a);
                PrintInteger(b);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2860
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2863
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 2
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            secondLastSnapshot.ArgValues[0].Value.Should().Be(2);
            lastSnapshot.ArgValues[0].Value.Should().Be(2);
        }

        /// <summary>
        /// Test prefix increment on base pointer integer.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2866
        /// Original: def test_prefix_increment_bp_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrefixIncrementBpInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2868
            // Original: ncs = self.compile("""int a = 1; void main() { int b = ++a; PrintInteger(a); PrintInteger(b); }""")
            NCS ncs = Compile(@"
            int a = 1;

            void main()
            {
                int b = ++a;

                PrintInteger(a);
                PrintInteger(b);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2882
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2885
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 2
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            secondLastSnapshot.ArgValues[0].Value.Should().Be(2);
            lastSnapshot.ArgValues[0].Value.Should().Be(2);
        }

        /// <summary>
        /// Test postfix increment on stack pointer integer.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2888
        /// Original: def test_postfix_increment_sp_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPostfixIncrementSpInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2890
            // Original: ncs = self.compile("""void main() { int a = 1; int b = a++; PrintInteger(a); PrintInteger(b); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1;
                int b = a++;

                PrintInteger(a);
                PrintInteger(b);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2903
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2906
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 1
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            secondLastSnapshot.ArgValues[0].Value.Should().Be(2);
            lastSnapshot.ArgValues[0].Value.Should().Be(1);
        }

        /// <summary>
        /// Test postfix increment on base pointer integer.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2909
        /// Original: def test_postfix_increment_bp_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPostfixIncrementBpInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2911
            // Original: ncs = self.compile("""int a = 1; void main() { int b = a++; PrintInteger(a); PrintInteger(b); }""")
            NCS ncs = Compile(@"
            int a = 1;

            void main()
            {
                int b = a++;

                PrintInteger(a);
                PrintInteger(b);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2925
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2928
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 1
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            secondLastSnapshot.ArgValues[0].Value.Should().Be(2);
            lastSnapshot.ArgValues[0].Value.Should().Be(1);
        }

        /// <summary>
        /// Test prefix decrement on stack pointer integer.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2931
        /// Original: def test_prefix_decrement_sp_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrefixDecrementSpInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2933
            // Original: ncs = self.compile("""void main() { int a = 1; int b = --a; PrintInteger(a); PrintInteger(b); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1;
                int b = --a;

                PrintInteger(a);
                PrintInteger(b);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2946
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2949
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 0
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            secondLastSnapshot.ArgValues[0].Value.Should().Be(0);
            lastSnapshot.ArgValues[0].Value.Should().Be(0);
        }

        /// <summary>
        /// Test prefix decrement on base pointer integer.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2952
        /// Original: def test_prefix_decrement_bp_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrefixDecrementBpInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2954
            // Original: ncs = self.compile("""int a = 1; void main() { int b = --a; PrintInteger(a); PrintInteger(b); }""")
            NCS ncs = Compile(@"
            int a = 1;

            void main()
            {
                int b = --a;

                PrintInteger(a);
                PrintInteger(b);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2968
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2971
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 0
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            secondLastSnapshot.ArgValues[0].Value.Should().Be(0);
            lastSnapshot.ArgValues[0].Value.Should().Be(0);
        }

        /// <summary>
        /// Test postfix decrement on stack pointer integer.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2974
        /// Original: def test_postfix_decrement_sp_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPostfixDecrementSpInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2976
            // Original: ncs = self.compile("""void main() { int a = 1; int b = a--; PrintInteger(a); PrintInteger(b); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1;
                int b = a--;

                PrintInteger(a);
                PrintInteger(b);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2989
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2992
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 1
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            secondLastSnapshot.ArgValues[0].Value.Should().Be(0);
            lastSnapshot.ArgValues[0].Value.Should().Be(1);
        }

        /// <summary>
        /// Test postfix decrement on base pointer integer.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2995
        /// Original: def test_postfix_decrement_bp_int(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPostfixDecrementBpInt()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:2997
            // Original: ncs = self.compile("""int a = 1; void main() { int b = a--; PrintInteger(a); PrintInteger(b); }""")
            NCS ncs = Compile(@"
            int a = 1;

            void main()
            {
                int b = a--;

                PrintInteger(a);
                PrintInteger(b);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3011
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3014
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 1
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            secondLastSnapshot.ArgValues[0].Value.Should().Be(0);
            lastSnapshot.ArgValues[0].Value.Should().Be(1);
        }

        #endregion

        #region Function Tests

        /// <summary>
        /// Test function prototype with no arguments.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3020
        /// Original: def test_prototype_no_args(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeNoArgs()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3022
            // Original: ncs = self.compile("""void test(); void main() { test(); } void test() { PrintInteger(56); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3038
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3041
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 56
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(56);
        }

        /// <summary>
        /// Test function prototype with argument.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3044
        /// Original: def test_prototype_with_arg(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeWithArg()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3046
            // Original: ncs = self.compile("""void test(int value); void main() { test(57); } void test(int value) { PrintInteger(value); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3062
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3065
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 57
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(57);
        }

        /// <summary>
        /// Test function prototype with three arguments.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3068
        /// Original: def test_prototype_with_three_args(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeWithThreeArgs()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3070
            // Original: ncs = self.compile("""void test(int a, int b, int c) { PrintInteger(a); PrintInteger(b); PrintInteger(c); } void main() { int a = 1, b = 2, c = 3; test(a, b, c); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3087
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3090
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 3
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(1);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(2);
            lastSnapshot.ArgValues[0].Value.Should().Be(3);
        }

        /// <summary>
        /// Test function prototype with many arguments including defaults.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3094
        /// Original: def test_prototype_with_many_args(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeWithManyArgs()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3096
            // Original: ncs = self.compile("""void test(int a, effect z, int b, int c, int d = 4) { PrintInteger(a); PrintInteger(b); PrintInteger(c); PrintInteger(d); } void main() { int a = 1, b = 2, c = 3; effect z; test(a, z, b, c); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3116
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3119
            // Original: assert interpreter.action_snapshots[-4].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 2
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 3
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 4
            var fourthLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 4];
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            fourthLastSnapshot.ArgValues[0].Value.Should().Be(1);
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(2);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(3);
            lastSnapshot.ArgValues[0].Value.Should().Be(4);
        }

        /// <summary>
        /// Test function prototype with default argument.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3124
        /// Original: def test_prototype_with_default_arg(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeWithDefaultArg()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3126
            // Original: ncs = self.compile("""void test(int value = 57); void main() { test(); } void test(int value = 57) { PrintInteger(value); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3142
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3145
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 57
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(57);
        }

        /// <summary>
        /// Test function prototype with default constant argument.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3148
        /// Original: def test_prototype_with_default_constant_arg(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeWithDefaultConstantArg()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3150
            // Original: ncs = self.compile("""void test(int value = DAMAGE_TYPE_COLD); void main() { test(); } void test(int value = DAMAGE_TYPE_COLD) { PrintInteger(value); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3166
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3169
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 32
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(32);
        }

        /// <summary>
        /// Test function call with missing required argument.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3172
        /// Original: def test_prototype_missing_arg(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeMissingArg()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3174
            // Original: source = """void test(int value); void main() { test(); } void test(int value) { PrintInteger(value); }"""
            // Original: self.assertRaises(CompileError, self.compile, source)
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

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(source));
        }

        /// <summary>
        /// Test function call missing required argument when optional exists.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3190
        /// Original: def test_prototype_missing_arg_and_default(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeMissingArgAndDefault()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3192
            // Original: source = """void test(int value1, int value2 = 123); void main() { test(); } void test(int value1, int value2 = 123) { PrintInteger(value1); }"""
            // Original: self.assertRaises(CompileError, self.compile, source)
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

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(source));
        }

        /// <summary>
        /// Test function with default parameter before required (should error).
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3208
        /// Original: def test_prototype_default_before_required(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeDefaultBeforeRequired()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3210
            // Original: source = """void test(int value1 = 123, int value2); void main() { test(123, 123); } void test(int value1 = 123, int value2) { PrintInteger(value1); }"""
            // Original: self.assertRaises(CompileError, self.compile, source)
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

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(source));
        }

        /// <summary>
        /// Test redefining a function (should error).
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3226
        /// Original: def test_redefine_function(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestRedefineFunction()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3228
            // Original: script = """void test() { } void test() { }"""
            // Original: self.assertRaises(CompileError, self.compile, script)
            string script = @"
            void test()
            {

            }

            void test()
            {

            }
        ";

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test duplicate function prototype (should error).
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3241
        /// Original: def test_double_prototype(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestDoublePrototype()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3243
            // Original: script = """void test(); void test();"""
            // Original: self.assertRaises(CompileError, self.compile, script)
            string script = @"
            void test();
            void test();
        ";

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test function prototype after definition (should error).
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3249
        /// Original: def test_prototype_after_definition(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeAfterDefinition()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3251
            // Original: script = """void test() { } void test();"""
            // Original: self.assertRaises(CompileError, self.compile, script)
            string script = @"
            void test()
            {

            }

            void test();
        ";

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test function prototype and definition parameter mismatch.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3261
        /// Original: def test_prototype_and_definition_param_mismatch(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeAndDefinitionParamMismatch()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3263
            // Original: script = """void test(int a); void test() { }"""
            // Original: self.assertRaises(CompileError, self.compile, script)
            string script = @"
            void test(int a);

            void test()
            {

            }
        ";

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test function prototype and definition return type mismatch.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3285
        /// Original: def test_prototype_and_definition_return_mismatch(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPrototypeAndDefinitionReturnMismatch()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3287
            // Original: script = """void test(int a); int test(int a) { }"""
            // Original: self.assertRaises(CompileError, self.compile, script)
            string script = @"
            void test(int a);

            int test(int a)
            {

            }
        ";

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test calling undefined function (should error).
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3297
        /// Original: def test_call_undefined(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCallUndefined()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3299
            // Original: script = """void main() { test(0); }"""
            // Original: self.assertRaises(CompileError, self.compile, script)
            string script = @"
            void main()
            {
                test(0);
            }
        ";

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(script));
        }

        /// <summary>
        /// Test calling void function with no arguments.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3308
        /// Original: def test_call_void_with_no_args(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCallVoidWithNoArgs()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3310
            // Original: ncs = self.compile("""void test() { PrintInteger(123); } void main() { test(); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3324
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3327
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 123
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(123);
        }

        /// <summary>
        /// Test calling void function with one argument.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3330
        /// Original: def test_call_void_with_one_arg(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCallVoidWithOneArg()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3332
            // Original: ncs = self.compile("""void test(int value) { PrintInteger(value); } void main() { test(123); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3346
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3349
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 123
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(123);
        }

        /// <summary>
        /// Test calling void function with two arguments.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3352
        /// Original: def test_call_void_with_two_args(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCallVoidWithTwoArgs()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3354
            // Original: ncs = self.compile("""void test(int value1, int value2) { PrintInteger(value1); PrintInteger(value2); } void main() { test(1, 2); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3368
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3372
            // Original: assert len(interpreter.action_snapshots) == 2
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 1
            // Original: assert interpreter.action_snapshots[1].arg_values[0] == 2
            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
            interpreter.ActionSnapshots[1].ArgValues[0].Value.Should().Be(2);
        }

        /// <summary>
        /// Test calling integer-returning function with no arguments.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3376
        /// Original: def test_call_int_with_no_args(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCallIntWithNoArgs()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3378
            // Original: ncs = self.compile("""int test() { return 5; } void main() { int x = test(); PrintInteger(x); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3393
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3396
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 5
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(5);
        }

        /// <summary>
        /// Test calling forward-declared integer-returning function.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3399
        /// Original: def test_call_int_with_no_args_and_forward_declared(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCallIntWithNoArgsAndForwardDeclared()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3401
            // Original: ncs = self.compile("""int test(); int test() { return 5; } void main() { int x = test(); PrintInteger(x); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3418
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3421
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 5
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(5);
        }

        /// <summary>
        /// Test function call with parameter type mismatch.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3424
        /// Original: def test_call_param_mismatch(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestCallParamMismatch()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3426
            // Original: source = """int test(int a) { return a; } void main() { test(""123""); }"""
            // Original: self.assertRaises(CompileError, self.compile, source)
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

            Assert.Throws<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(source));
        }

        #endregion

        #region Return Statement Tests

        /// <summary>
        /// Test return statement.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3443
        /// Original: def test_return(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestReturn()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3445
            // Original: ncs = self.compile("""void main() { int a = 1; if (a == 1) { PrintInteger(a); return; } PrintInteger(0); return; }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3463
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3466
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 1
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
        }

        /// <summary>
        /// Test return statement with parentheses.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3469
        /// Original: def test_return_parenthesis(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestReturnParenthesis()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3471
            // Original: ncs = self.compile("""int test() { return(321); } void main() { int value = test(); PrintInteger(value); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3486
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3489
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 321
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(321);
        }

        /// <summary>
        /// Test return statement with constant in parentheses.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3491
        /// Original: def test_return_parenthesis_constant(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestReturnParenthesisConstant()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3493
            // Original: ncs = self.compile("""int test() { return(TRUE); } void main() { int value = test(); PrintInteger(value); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3508
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3511
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 1
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(1);
        }

        /// <summary>
        /// Test integer declaration with parentheses.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3513
        /// Original: def test_int_parenthesis_declaration(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestIntParenthesisDeclaration()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3515
            // Original: ncs = self.compile("""void main() { int value = (123); PrintInteger(value); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int value = (123);
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3525
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3528
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 123
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(123);
        }

        #endregion

        #region Include Tests

        /// <summary>
        /// Test #include directive with built-in library.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3533
        /// Original: def test_include_builtin(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestIncludeBuiltin()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3535
            // Original: otherscript = """void TestFunc() { PrintInteger(123); }""".encode(encoding=""windows-1252"")
            // Note: Using UTF-8 instead of windows-1252 for .NET Core compatibility
            byte[] otherscript = System.Text.Encoding.UTF8.GetBytes(@"
            void TestFunc()
            {
                PrintInteger(123);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3542
            // Original: ncs = self.compile("""#include ""otherscript"" void main() { TestFunc(); }""", library={""otherscript"": otherscript})
            var library = new Dictionary<string, byte[]>();
            library["otherscript"] = otherscript;
            NCS ncs = Compile(@"
            #include ""otherscript""

            void main()
            {
                TestFunc();
            }
        ", library);

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3554
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        /// <summary>
        /// Test nested #include directives.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3578
        /// Original: def test_nested_include(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestNestedInclude()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3580
            // Original: first_script: bytes = """int SOME_COST = 13; void TestFunc(int value) { PrintInteger(value); }""".encode(encoding=""windows-1252"")
            // Note: Using UTF-8 instead of windows-1252 for .NET Core compatibility
            byte[] firstScript = System.Text.Encoding.UTF8.GetBytes(@"
            int SOME_COST = 13;

            void TestFunc(int value)
            {
                PrintInteger(value);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3589
            // Original: second_script: bytes = """#include ""first_script""""".encode(encoding=""windows-1252"")
            // Note: Using UTF-8 instead of windows-1252 for .NET Core compatibility
            byte[] secondScript = System.Text.Encoding.UTF8.GetBytes(@"
            #include ""first_script""
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3593
            // Original: ncs: NCS = self.compile("""#include ""second_script"" void main() { TestFunc(SOME_COST); }""", library={""first_script"": first_script, ""second_script"": second_script})
            var library = new Dictionary<string, byte[]>();
            library["first_script"] = firstScript;
            library["second_script"] = secondScript;
            NCS ncs = Compile(@"
            #include ""second_script""

            void main()
            {
                TestFunc(SOME_COST);
            }
        ", library);

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3605
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3608
            // Original: assert len(interpreter.action_snapshots) == 1
            // Original: assert interpreter.action_snapshots[0].arg_values[0] == 13
            interpreter.ActionSnapshots.Count.Should().Be(1);
            interpreter.ActionSnapshots[0].ArgValues[0].Value.Should().Be(13);
        }

        /// <summary>
        /// Test missing #include file (should error).
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3611
        /// Original: def test_missing_include(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestMissingInclude()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3613
            // Original: source = """#include ""otherscript"" void main() { TestFunc(); }"""
            // Original: self.assertRaises(CompileError, self.compile, source)
            // Note: Python's assertRaises catches subclasses, C# throws MissingIncludeError which inherits from CompileError
            string source = @"
            #include ""otherscript""

            void main()
            {
                TestFunc();
            }
        ";

            // Python's assertRaises catches subclasses, so MissingIncludeError (which inherits from CompileError) should be caught
            Assert.ThrowsAny<Andastra.Parsing.Formats.NCS.Compiler.NSS.CompileError>(() => Compile(source));
        }

        /// <summary>
        /// Test using global variable from included file.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3624
        /// Original: def test_imported_global_variable(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestImportedGlobalVariable()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3626
            // Original: otherscript = """int iExperience = 55;""".encode(encoding=""windows-1252"")
            // Note: Using UTF-8 instead of windows-1252 for .NET Core compatibility
            byte[] otherscript = System.Text.Encoding.UTF8.GetBytes(@"
            int iExperience = 55;
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3630
            // Original: ncs = self.compile("""#include ""otherscript"" void main() { object oPlayer = GetPCSpeaker(); GiveXPToCreature(oPlayer, iExperience); }""", library={""otherscript"": otherscript})
            var library = new Dictionary<string, byte[]>();
            library["otherscript"] = otherscript;
            NCS ncs = Compile(@"
            #include ""otherscript""

            void main()
            {
                object oPlayer = GetPCSpeaker();
                GiveXPToCreature(oPlayer, iExperience);
            }
        ", library);

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3643
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3646
            // Original: assert len(interpreter.action_snapshots) == 2
            // Original: assert interpreter.action_snapshots[1].arg_values[1] == 55
            interpreter.ActionSnapshots.Count.Should().Be(2);
            interpreter.ActionSnapshots[1].ArgValues[1].Value.Should().Be(55);
        }

        #endregion

        #region Comment Tests

        /// <summary>
        /// Test single-line comment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3652
        /// Original: def test_comment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestComment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3654
            // Original: ncs = self.compile("""void main() { // int a = ""abc""; // [] /* int a = 0; }""")
            NCS ncs = Compile(@"
            void main()
            {
                // int a = ""abc""; // [] /*
                int a = 0;
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3664
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        /// <summary>
        /// Test multi-line comment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3667
        /// Original: def test_multiline_comment(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestMultilineComment()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3669
            // Original: ncs = self.compile("""void main() { /* int abc = ;; 123 */ string aaa = """"; }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3683
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();
        }

        #endregion

        #region Expression Tests

        /// <summary>
        /// Test expression without assignment.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3689
        /// Original: def test_assignmentless_expression(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestAssignmentlessExpression()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3691
            // Original: ncs = self.compile("""void main() { int a = 123; 1; GetCheatCode(1); ""abc""; PrintInteger(a); }""")
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3705
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3709
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 123
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(123);
        }

        /// <summary>
        /// Test NOP statement.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3711
        /// Original: def test_nop_statement(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestNopStatement()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3713
            // Original: ncs = self.compile("""void main() { NOP ""test message""; PrintInteger(42); }""")
            NCS ncs = Compile(@"
            void main()
            {
                NOP ""test message"";
                PrintInteger(42);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3723
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3726
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 42
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().Be(42);
        }

        #endregion

        #region Vector Arithmetic Tests

        /// <summary>
        /// Test vector addition.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3731
        /// Original: def test_vector_addition(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVectorAddition()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3733
            // Original: ncs = self.compile("""void main() { vector v1 = Vector(1.0, 2.0, 3.0); vector v2 = Vector(4.0, 5.0, 6.0); vector result = v1 + v2; PrintFloat(result.x); PrintFloat(result.y); PrintFloat(result.z); }""")
            NCS ncs = Compile(@"
            void main()
            {
                vector v1 = Vector(1.0, 2.0, 3.0);
                vector v2 = Vector(4.0, 5.0, 6.0);
                vector result = v1 + v2;
                PrintFloat(result.x);
                PrintFloat(result.y);
                PrintFloat(result.z);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3747
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""Vector"", Vector3)
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", (args) => new Vector3(
                args[0] is float f0 ? f0 : Convert.ToSingle(args[0]),
                args[1] is float f1 ? f1 : Convert.ToSingle(args[1]),
                args[2] is float f2 ? f2 : Convert.ToSingle(args[2])));
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3751
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 5.0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 7.0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 9.0
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(5.0f);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(7.0f);
            lastSnapshot.ArgValues[0].Value.Should().Be(9.0f);
        }

        /// <summary>
        /// Test vector subtraction.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3755
        /// Original: def test_vector_subtraction(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVectorSubtraction()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3757
            // Original: ncs = self.compile("""void main() { vector v1 = Vector(5.0, 7.0, 9.0); vector v2 = Vector(1.0, 2.0, 3.0); vector result = v1 - v2; PrintFloat(result.x); PrintFloat(result.y); PrintFloat(result.z); }""")
            NCS ncs = Compile(@"
            void main()
            {
                vector v1 = Vector(5.0, 7.0, 9.0);
                vector v2 = Vector(1.0, 2.0, 3.0);
                vector result = v1 - v2;
                PrintFloat(result.x);
                PrintFloat(result.y);
                PrintFloat(result.z);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3771
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""Vector"", Vector3)
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", (args) => new Vector3(
                args[0] is float f0 ? f0 : Convert.ToSingle(args[0]),
                args[1] is float f1 ? f1 : Convert.ToSingle(args[1]),
                args[2] is float f2 ? f2 : Convert.ToSingle(args[2])));
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3775
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 4.0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 5.0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 6.0
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(4.0f);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(5.0f);
            lastSnapshot.ArgValues[0].Value.Should().Be(6.0f);
        }

        /// <summary>
        /// Test vector multiplication by float.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3779
        /// Original: def test_vector_multiplication_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVectorMultiplicationFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3781
            // Original: ncs = self.compile("""void main() { vector v1 = Vector(2.0, 3.0, 4.0); vector result = v1 * 2.0; PrintFloat(result.x); PrintFloat(result.y); PrintFloat(result.z); }""")
            NCS ncs = Compile(@"
            void main()
            {
                vector v1 = Vector(2.0, 3.0, 4.0);
                vector result = v1 * 2.0;
                PrintFloat(result.x);
                PrintFloat(result.y);
                PrintFloat(result.z);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3794
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""Vector"", Vector3)
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", (args) => new Vector3(
                args[0] is float f0 ? f0 : Convert.ToSingle(args[0]),
                args[1] is float f1 ? f1 : Convert.ToSingle(args[1]),
                args[2] is float f2 ? f2 : Convert.ToSingle(args[2])));
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3798
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 4.0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 6.0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 8.0
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(4.0f);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(6.0f);
            lastSnapshot.ArgValues[0].Value.Should().Be(8.0f);
        }

        /// <summary>
        /// Test vector division by float.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3802
        /// Original: def test_vector_division_float(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVectorDivisionFloat()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3804
            // Original: ncs = self.compile("""void main() { vector v1 = Vector(8.0, 6.0, 4.0); vector result = v1 / 2.0; PrintFloat(result.x); PrintFloat(result.y); PrintFloat(result.z); }""")
            NCS ncs = Compile(@"
            void main()
            {
                vector v1 = Vector(8.0, 6.0, 4.0);
                vector result = v1 / 2.0;
                PrintFloat(result.x);
                PrintFloat(result.y);
                PrintFloat(result.z);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3817
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""Vector"", Vector3)
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", (args) => new Vector3(
                args[0] is float f0 ? f0 : Convert.ToSingle(args[0]),
                args[1] is float f1 ? f1 : Convert.ToSingle(args[1]),
                args[2] is float f2 ? f2 : Convert.ToSingle(args[2])));
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3821
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 4.0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 3.0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 2.0
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(4.0f);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(3.0f);
            lastSnapshot.ArgValues[0].Value.Should().Be(2.0f);
        }

        /// <summary>
        /// Test vector compound assignment with addition.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3825
        /// Original: def test_vector_compound_assignment_addition(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVectorCompoundAssignmentAddition()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3827
            // Original: ncs = self.compile("""void main() { vector v = Vector(1.0, 2.0, 3.0); v += Vector(0.5, 0.5, 0.5); PrintFloat(v.x); PrintFloat(v.y); PrintFloat(v.z); }""")
            NCS ncs = Compile(@"
            void main()
            {
                vector v = Vector(1.0, 2.0, 3.0);
                v += Vector(0.5, 0.5, 0.5);
                PrintFloat(v.x);
                PrintFloat(v.y);
                PrintFloat(v.z);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3840
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""Vector"", Vector3)
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", (args) => new Vector3(
                args[0] is float f0 ? f0 : Convert.ToSingle(args[0]),
                args[1] is float f1 ? f1 : Convert.ToSingle(args[1]),
                args[2] is float f2 ? f2 : Convert.ToSingle(args[2])));
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3844
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 1.5
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 2.5
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 3.5
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(1.5f);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(2.5f);
            lastSnapshot.ArgValues[0].Value.Should().Be(3.5f);
        }

        /// <summary>
        /// Test vector compound assignment with subtraction.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3848
        /// Original: def test_vector_compound_assignment_subtraction(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVectorCompoundAssignmentSubtraction()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3850
            // Original: ncs = self.compile("""void main() { vector v = Vector(5.0, 5.0, 5.0); v -= Vector(1.0, 2.0, 3.0); PrintFloat(v.x); PrintFloat(v.y); PrintFloat(v.z); }""")
            NCS ncs = Compile(@"
            void main()
            {
                vector v = Vector(5.0, 5.0, 5.0);
                v -= Vector(1.0, 2.0, 3.0);
                PrintFloat(v.x);
                PrintFloat(v.y);
                PrintFloat(v.z);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3863
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""Vector"", Vector3)
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", (args) => new Vector3(
                args[0] is float f0 ? f0 : Convert.ToSingle(args[0]),
                args[1] is float f1 ? f1 : Convert.ToSingle(args[1]),
                args[2] is float f2 ? f2 : Convert.ToSingle(args[2])));
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3867
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 4.0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 3.0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 2.0
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(4.0f);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(3.0f);
            lastSnapshot.ArgValues[0].Value.Should().Be(2.0f);
        }

        /// <summary>
        /// Test vector compound assignment with multiplication.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3871
        /// Original: def test_vector_compound_assignment_multiplication(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVectorCompoundAssignmentMultiplication()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3873
            // Original: ncs = self.compile("""void main() { vector v = Vector(2.0, 3.0, 4.0); v *= 2.0; PrintFloat(v.x); PrintFloat(v.y); PrintFloat(v.z); }""")
            NCS ncs = Compile(@"
            void main()
            {
                vector v = Vector(2.0, 3.0, 4.0);
                v *= 2.0;
                PrintFloat(v.x);
                PrintFloat(v.y);
                PrintFloat(v.z);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3886
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""Vector"", Vector3)
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", (args) => new Vector3(
                args[0] is float f0 ? f0 : Convert.ToSingle(args[0]),
                args[1] is float f1 ? f1 : Convert.ToSingle(args[1]),
                args[2] is float f2 ? f2 : Convert.ToSingle(args[2])));
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3890
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 4.0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 6.0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 8.0
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(4.0f);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(6.0f);
            lastSnapshot.ArgValues[0].Value.Should().Be(8.0f);
        }

        /// <summary>
        /// Test vector compound assignment with division.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3894
        /// Original: def test_vector_compound_assignment_division(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestVectorCompoundAssignmentDivision()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3896
            // Original: ncs = self.compile("""void main() { vector v = Vector(8.0, 6.0, 4.0); v /= 2.0; PrintFloat(v.x); PrintFloat(v.y); PrintFloat(v.z); }""")
            NCS ncs = Compile(@"
            void main()
            {
                vector v = Vector(8.0, 6.0, 4.0);
                v /= 2.0;
                PrintFloat(v.x);
                PrintFloat(v.y);
                PrintFloat(v.z);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3910
            // Original: interpreter = Interpreter(ncs); interpreter.set_mock(""Vector"", Vector3)
            var interpreter = new Interpreter(ncs);
            interpreter.SetMock("Vector", (args) => new Vector3(
                args[0] is float f0 ? f0 : Convert.ToSingle(args[0]),
                args[1] is float f1 ? f1 : Convert.ToSingle(args[1]),
                args[2] is float f2 ? f2 : Convert.ToSingle(args[2])));
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3913
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 4.0
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 3.0
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 2.0
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(4.0f);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(3.0f);
            lastSnapshot.ArgValues[0].Value.Should().Be(2.0f);
        }

        #endregion

        #region Nested Struct Tests

        /// <summary>
        /// Test accessing nested struct members.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3920
        /// Original: def test_nested_struct_access(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestNestedStructAccess()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3922
            // Original: ncs = self.compile("""struct Inner { int value; }; struct Outer { struct Inner inner; string name; }; void main() { struct Outer outer; outer.inner.value = 42; outer.name = ""test""; PrintInteger(outer.inner.value); PrintString(outer.name); }""")
            NCS ncs = Compile(@"
            struct Inner
            {
                int value;
            };

            struct Outer
            {
                struct Inner inner;
                string name;
            };

            void main()
            {
                struct Outer outer;
                outer.inner.value = 42;
                outer.name = ""test"";
                PrintInteger(outer.inner.value);
                PrintString(outer.name);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3946
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3949
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == 42
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == ""test""
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            secondLastSnapshot.ArgValues[0].Value.Should().Be(42);
            lastSnapshot.ArgValues[0].Value.Should().Be("test");
        }

        #endregion

        #region Complex Expression Tests

        /// <summary>
        /// Test complex expression with multiple operators and precedence.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3955
        /// Original: def test_complex_expression_precedence(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestComplexExpressionPrecedence()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3957
            // Original: ncs = self.compile("""void main() { int a = 1 + 2 * 3 - 4 / 2; int b = (1 + 2) * (3 - 4) / 2; int c = 1 > 0 ? 10 + 5 : 20 - 5; PrintInteger(a); PrintInteger(b); PrintInteger(c); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 1 + 2 * 3 - 4 / 2;
                int b = (1 + 2) * (3 - 4) / 2;
                int c = 1 > 0 ? 10 + 5 : 20 - 5;
                PrintInteger(a);
                PrintInteger(b);
                PrintInteger(c);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3971
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3974
            // Original: assert interpreter.action_snapshots[-3].arg_values[0] == 5  # 1 + 6 - 2 = 5
            // Original: assert interpreter.action_snapshots[-2].arg_values[0] == -1  # 3 * -1 / 2 = -1 (integer division)
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] == 15  # 1 > 0 ? 15 : 15 = 15
            var thirdLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 3];
            var secondLastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 2];
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            thirdLastSnapshot.ArgValues[0].Value.Should().Be(5);
            secondLastSnapshot.ArgValues[0].Value.Should().Be(-1);
            lastSnapshot.ArgValues[0].Value.Should().Be(15);
        }

        /// <summary>
        /// Test expression combining all operator types.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3978
        /// Original: def test_expression_with_all_operators(self):
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestExpressionWithAllOperators()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3980
            // Original: ncs = self.compile("""void main() { int a = 10; int b = 5; int result = (a + b) * 2 - (a / b) % 3 & 0xFF | 0x0F ^ 0xAA << 1 >> 1; PrintInteger(result); }""")
            NCS ncs = Compile(@"
            void main()
            {
                int a = 10;
                int b = 5;
                int result = (a + b) * 2 - (a / b) % 3 & 0xFF | 0x0F ^ 0xAA << 1 >> 1;
                PrintInteger(result);
            }
        ");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3992
            // Original: interpreter = Interpreter(ncs); interpreter.run()
            var interpreter = new Interpreter(ncs);
            interpreter.Run();

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tests/resource/formats/test_ncs.py:3996
            // Original: # Complex expression evaluation
            // Original: assert interpreter.action_snapshots[-1].arg_values[0] is not None
            var lastSnapshot = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];
            lastSnapshot.ArgValues[0].Value.Should().NotBeNull();
        }

        #endregion
    }
}
