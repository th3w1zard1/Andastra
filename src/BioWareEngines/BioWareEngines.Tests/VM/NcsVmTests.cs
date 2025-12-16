using System;
using System.Collections.Generic;
using BioWareEngines.Core.Entities;
using BioWareEngines.Core.Enums;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Scripting.EngineApi;
using BioWareEngines.Scripting.Interfaces;
using BioWareEngines.Scripting.VM;
using Xunit;

namespace BioWareEngines.Tests.VM
{
    public class NcsVmTests
    {
        private readonly NcsVm _vm;
        private readonly World _world;
        private readonly ScriptGlobals _globals;
        private readonly TestEngineApi _engineApi;
        private readonly ExecutionContext _context;

        public NcsVmTests()
        {
            _vm = new NcsVm();
            _world = new World();
            _globals = new ScriptGlobals();
            _engineApi = new TestEngineApi();

            IEntity caller = _world.CreateEntity(ObjectType.Creature, System.Numerics.Vector3.Zero, 0f);
            _context = new ExecutionContext(caller, _world, _engineApi, _globals);
        }

        [Fact]
        public void ValidateHeader_ValidNcs_Accepted()
        {
            // Create a minimal valid NCS with just header and a NOP
            byte[] ncs = CreateNcsWithInstructions(new byte[] { 0x5D, 0x00 }); // NOP

            // Should not throw
            _vm.Execute(ncs, _context);
        }

        [Fact]
        public void ValidateHeader_InvalidSignature_ThrowsException()
        {
            byte[] ncs = new byte[]
            {
                (byte)'X', (byte)'C', (byte)'S', (byte)' ',
                (byte)'V', (byte)'1', (byte)'.', (byte)'0',
                0x42,
                0, 0, 0, 13 // Size
            };

            Assert.Throws<System.IO.InvalidDataException>(() => _vm.Execute(ncs, _context));
        }

        [Fact]
        public void Execute_ConstI_PushesValueOnStack()
        {
            // NCS that pushes 42 - no return, just let the VM end naturally
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 42, // CONSTI 42
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(42, result);
        }

        [Fact]
        public void Execute_AddII_AddsIntegers()
        {
            // Push 10, push 32, add them
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 10, // CONSTI 10
                0x0B, 0x03, 0x00, 0x00, 0x00, 32, // CONSTI 32
                0x32, 0x20,                     // ADDII
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(42, result);
        }

        [Fact]
        public void Execute_SubII_SubtractsIntegers()
        {
            // Push 50, push 8, subtract
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 50, // CONSTI 50
                0x0B, 0x03, 0x00, 0x00, 0x00, 8,  // CONSTI 8
                0x38, 0x20,                     // SUBII
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(42, result);
        }

        [Fact]
        public void Execute_MulII_MultipliesIntegers()
        {
            // Push 6, push 7, multiply
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 6,  // CONSTI 6
                0x0B, 0x03, 0x00, 0x00, 0x00, 7,  // CONSTI 7
                0x3D, 0x20,                     // MULII
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(42, result);
        }

        [Fact]
        public void Execute_DivII_DividesIntegers()
        {
            // Push 126, push 3, divide
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 126, // CONSTI 126
                0x0B, 0x03, 0x00, 0x00, 0x00, 3,  // CONSTI 3
                0x43, 0x20,                     // DIVII
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(42, result);
        }

        [Fact]
        public void Execute_EqualII_ComparesEqual()
        {
            // Push 42, push 42, compare
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 42, // CONSTI 42
                0x0B, 0x03, 0x00, 0x00, 0x00, 42, // CONSTI 42
                0x15, 0x20,                     // EQUALII
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(1, result); // TRUE
        }

        [Fact]
        public void Execute_EqualII_ComparesNotEqual()
        {
            // Push 42, push 43, compare
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 42, // CONSTI 42
                0x0B, 0x03, 0x00, 0x00, 0x00, 43, // CONSTI 43
                0x15, 0x20,                     // EQUALII
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(0, result); // FALSE
        }

        [Fact]
        public void Execute_LogAndII_BothTrue()
        {
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 1,  // CONSTI 1 (TRUE)
                0x0B, 0x03, 0x00, 0x00, 0x00, 1,  // CONSTI 1 (TRUE)
                0x10, 0x20,                     // LOGANDII
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(1, result);
        }

        [Fact]
        public void Execute_LogAndII_OneFalse()
        {
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 1,
                0x0B, 0x03, 0x00, 0x00, 0x00, 0,
                0x10, 0x20,
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(0, result);
        }

        [Fact]
        public void Execute_LogOrII_BothFalse()
        {
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 0,
                0x0B, 0x03, 0x00, 0x00, 0x00, 0,
                0x11, 0x20,
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(0, result);
        }

        [Fact]
        public void Execute_LogOrII_OneTrue()
        {
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 0,
                0x0B, 0x03, 0x00, 0x00, 0x00, 1,
                0x11, 0x20,
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(1, result);
        }

        [Fact]
        public void Execute_NotI_InvertTrue()
        {
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 1,
                0x52, 0x00,  // NOTI
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(0, result);
        }

        [Fact]
        public void Execute_NotI_InvertFalse()
        {
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 0,
                0x52, 0x00,  // NOTI
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(1, result);
        }

        [Fact]
        public void Execute_NegI_NegatesPositive()
        {
            byte[] ncs = CreateNcsWithInstructions(new byte[]
            {
                0x0B, 0x03, 0x00, 0x00, 0x00, 42,
                0x4A, 0x00,  // NEGI
            });

            int result = _vm.Execute(ncs, _context);
            Assert.Equal(-42, result);
        }

        [Fact]
        public void ScriptGlobals_SetAndGetGlobalInt()
        {
            _globals.SetGlobalInt("test_var", 42);
            int value = _globals.GetGlobalInt("test_var");
            Assert.Equal(42, value);
        }

        [Fact]
        public void ScriptGlobals_GetNonexistentGlobalInt_ReturnsZero()
        {
            int value = _globals.GetGlobalInt("nonexistent");
            Assert.Equal(0, value);
        }

        [Fact]
        public void ScriptGlobals_SetAndGetLocalInt()
        {
            IEntity entity = _world.CreateEntity(ObjectType.Creature, System.Numerics.Vector3.Zero, 0f);
            _globals.SetLocalInt(entity, "local_var", 100);
            int value = _globals.GetLocalInt(entity, "local_var");
            Assert.Equal(100, value);
        }

        private byte[] CreateMinimalNcs()
        {
            return CreateNcsWithInstructions(new byte[] { 0x50, 0x00 }); // RETN
        }

        private byte[] CreateNcsWithInstructions(byte[] instructions)
        {
            int totalSize = 13 + instructions.Length;
            byte[] ncs = new byte[totalSize];

            // Header
            ncs[0] = (byte)'N';
            ncs[1] = (byte)'C';
            ncs[2] = (byte)'S';
            ncs[3] = (byte)' ';
            ncs[4] = (byte)'V';
            ncs[5] = (byte)'1';
            ncs[6] = (byte)'.';
            ncs[7] = (byte)'0';
            ncs[8] = 0x42;

            // Size (big-endian)
            ncs[9] = (byte)(totalSize >> 24);
            ncs[10] = (byte)(totalSize >> 16);
            ncs[11] = (byte)(totalSize >> 8);
            ncs[12] = (byte)totalSize;

            // Instructions
            Array.Copy(instructions, 0, ncs, 13, instructions.Length);

            return ncs;
        }

        /// <summary>
        /// Test engine API that tracks calls.
        /// </summary>
        private class TestEngineApi : IEngineApi
        {
            public List<(int routineId, IReadOnlyList<Variable> args)> Calls { get; } = new List<(int, IReadOnlyList<Variable>)>();

            public Variable CallEngineFunction(int routineId, IReadOnlyList<Variable> args, IExecutionContext ctx)
            {
                Calls.Add((routineId, args));
                return Variable.Void();
            }

            public string GetFunctionName(int routineId)
            {
                return "Test_" + routineId;
            }

            public int GetArgumentCount(int routineId)
            {
                return -1;
            }

            public bool IsImplemented(int routineId)
            {
                return true;
            }
        }
    }
}

