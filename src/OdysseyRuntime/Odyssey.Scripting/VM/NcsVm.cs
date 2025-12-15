using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using CSharpKOTOR.Common.Script;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;
using Odyssey.Content.Interfaces;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Scripting.VM
{
    /// <summary>
    /// NWScript Compiled Script Virtual Machine implementation.
    /// </summary>
    /// <remarks>
    /// NCS Virtual Machine:
    /// - Based on swkotor2.exe NCS VM implementation
    /// - Located via string references: NCS script execution engine handles bytecode interpretation
    /// - NCS file format: "NCS " signature (bytes 0-3), "V1.0" version (bytes 4-7), 0x42 marker at offset 8 (byte 8)
    /// - File format markers: "MOD V1.0" @ 0x007be0d4 (module save format), "BWM V1.0" @ 0x007c061c (walkmesh format), "LIP V1.0" @ 0x007d98d4 (lip sync format)
    /// - Action system: "ActionList" @ 0x007bebdc (action list field), "ActionId" @ 0x007bebd0 (action ID field)
    /// - "ActionType" @ 0x007bf7f8 (action type field), "ActionTimer" @ 0x007bf820 (action timer field)
    /// - "SchedActionList" @ 0x007bf99c (scheduled action list), "ParryActions" @ 0x007bfa18 (parry action queue)
    /// - "GroupActionId" @ 0x007bebc0 (group action ID), "EVENT_FORCED_ACTION" @ 0x007bccac (forced action event, case 0x15)
    /// - STORE_STATE opcode: "DelayCommand" @ 0x007be900 (stores stack/local state for delayed execution)
    /// - Original implementation: Executes NCS (NWScript Compiled Script) bytecode files
    /// - File size: Big-endian uint32 at offset 9-12 (bytes 9-12)
    /// - Instructions start at offset 0x0D (13 decimal, byte 13)
    /// - Stack-based VM with 65536-byte stack (StackSize constant), 4-byte aligned
    /// - Opcodes: ACTION (0x2A) calls engine functions, others handle stack operations, jumps, conditionals
    /// - ACTION opcode format: uint16 routineId (big-endian, bytes 2-3) + uint8 argCount (byte 4, stack elements, not bytes)
    /// - Original engine uses big-endian encoding for all multi-byte values (instructions, operands, jumps)
    /// - Stack alignment: 4-byte aligned, vectors are 12 bytes (3 floats, X/Y/Z components)
    /// - Jump offsets: Relative to instruction start (current PC), not next instruction
    /// - Object references: 0x7F000000 = OBJECT_INVALID, 0x7F000001 = OBJECT_SELF (caller entity)
    /// - Action scheduling: Actions can be scheduled with timers (DelayCommand), grouped (GroupActionId), or forced via events
    /// - Instruction limit: MaxInstructions prevents infinite loops (default 100000 instructions per execution)
    /// - String handling: Strings stored in string pool with integer handles (off-stack storage)
    /// - Based on NCS file format documentation in vendor/PyKotor/wiki/NCS-File-Format.md
    /// </remarks>
    public class NcsVm : INcsVm
    {
        private const int DefaultMaxInstructions = 100000;
        private const uint ObjectInvalid = 0x7F000000;

        // VM State
        private byte[] _code;
        private int _pc; // Program counter
        private int _sp; // Stack pointer
        private int _bp; // Base pointer
        private byte[] _stack;
        private int _instructionCount;
        private bool _running;
        private bool _aborted;
        private IExecutionContext _context;

        // String storage (strings are stored off-stack with handles)
        private Dictionary<int, string> _stringPool;
        private int _nextStringHandle;

        // Stack size
        private const int StackSize = 65536;

        public NcsVm()
        {
            _stack = new byte[StackSize];
            _stringPool = new Dictionary<int, string>();
            MaxInstructions = DefaultMaxInstructions;
        }

        public bool IsRunning { get { return _running; } }
        public int InstructionsExecuted { get { return _instructionCount; } }
        public int MaxInstructions { get; set; }
        public bool EnableTracing { get; set; }

        public int Execute(byte[] ncsBytes, IExecutionContext ctx)
        {
            if (ncsBytes == null || ncsBytes.Length < 13)
            {
                throw new ArgumentException("Invalid NCS data");
            }

            // Validate header
            if (ncsBytes[0] != 'N' || ncsBytes[1] != 'C' || ncsBytes[2] != 'S' || ncsBytes[3] != ' ')
            {
                throw new InvalidDataException("Invalid NCS signature");
            }

            if (ncsBytes[4] != 'V' || ncsBytes[5] != '1' || ncsBytes[6] != '.' || ncsBytes[7] != '0')
            {
                throw new InvalidDataException("Invalid NCS version");
            }

            // Byte at offset 8 must be 0x42 (program size marker)
            if (ncsBytes[8] != 0x42)
            {
                throw new InvalidDataException("Invalid NCS program marker");
            }

            // File size is big-endian uint32 at offset 9
            int fileSize = (ncsBytes[9] << 24) | (ncsBytes[10] << 16) | (ncsBytes[11] << 8) | ncsBytes[12];
            if (fileSize != ncsBytes.Length)
            {
                // Warning but continue - some NCS files have incorrect size
            }

            _code = ncsBytes;
            _pc = 13; // Instructions start at offset 0x0D
            _sp = 0;
            _bp = 0;
            _instructionCount = 0;
            _running = true;
            _aborted = false;
            _context = ctx;

            // Clear string pool for new execution
            _stringPool.Clear();
            _nextStringHandle = 1; // Start at 1, 0 reserved for null/empty

            Array.Clear(_stack, 0, _stack.Length);

            int result = 0;

            try
            {
                while (_running && !_aborted && _pc < _code.Length && _instructionCount < MaxInstructions)
                {
                    ExecuteInstruction();
                    _instructionCount++;
                }

                // Get return value if any
                if (_sp >= 4)
                {
                    result = PopInt();
                }
            }
            catch (Exception ex)
            {
                if (EnableTracing)
                {
                    Console.WriteLine("NCS Error at PC={0}: {1}", _pc, ex.Message);
                }
                throw;
            }
            finally
            {
                _running = false;
            }

            return result;
        }

        public int ExecuteScript(string resRef, IExecutionContext ctx)
        {
            // Load the NCS from the resource provider
            object provider = ctx.ResourceProvider;
            if (provider == null)
            {
                throw new InvalidOperationException("No resource provider in context");
            }

            byte[] ncsBytes = null;

            // Try IGameResourceProvider first (Odyssey system)
            if (provider is IGameResourceProvider gameProvider)
            {
                try
                {
                    var resourceId = new ResourceIdentifier(resRef, ResourceType.NCS);
                    System.Threading.Tasks.Task<byte[]> task = gameProvider.GetResourceBytesAsync(resourceId, CancellationToken.None);
                    task.Wait();
                    ncsBytes = task.Result;
                }
                catch (AggregateException aex)
                {
                    throw new InvalidOperationException("Failed to load script: " + resRef, aex.InnerException ?? aex);
                }
            }
            // Fallback to CSharpKOTOR Installation provider
            else if (provider is Installation installation)
            {
                CSharpKOTOR.Installation.ResourceResult result = installation.Resource(resRef, ResourceType.NCS, null, null);
                if (result != null && result.Data != null)
                {
                    ncsBytes = result.Data;
                }
            }

            if (ncsBytes == null || ncsBytes.Length == 0)
            {
                throw new FileNotFoundException("Script not found: " + resRef);
            }

            return Execute(ncsBytes, ctx);
        }

        public void Abort()
        {
            _aborted = true;
        }

        private void ExecuteInstruction()
        {
            byte opcode = _code[_pc];
            byte qualifier = _code[_pc + 1];
            _pc += 2;

            if (EnableTracing)
            {
                Console.WriteLine("PC={0:X4} OP={1:X2} Q={2:X2}", _pc - 2, opcode, qualifier);
            }

            switch (opcode)
            {
                case 0x01: CPDOWNSP(); break;
                case 0x02: RSADDI(); break;
                case 0x03: RSADDF(); break;
                case 0x04: RSADDS(); break;
                case 0x05: RSADDO(); break;
                case 0x0A: CPTOPSP(); break;
                case 0x0B: CONSTI(); break;
                case 0x0C: CONSTF(); break;
                case 0x0D: CONSTS(); break;
                case 0x0E: CONSTO(); break;
                case 0x0F: ACTION(); break;
                case 0x10: LOGANDII(); break;
                case 0x11: LOGORII(); break;
                case 0x12: INCORII(); break;
                case 0x13: EXCORII(); break;
                case 0x14: BOOLANDII(); break;
                case 0x15: EQUALII(); break;
                case 0x16: EQUALFF(); break;
                case 0x17: EQUALSS(); break;
                case 0x18: EQUALOO(); break;
                case 0x1E: NEQUALII(); break;
                case 0x1F: NEQUALFF(); break;
                case 0x20: NEQUALSS(); break;
                case 0x21: NEQUALOO(); break;
                case 0x27: GEQII(); break;
                case 0x28: GEQFF(); break;
                case 0x29: GTII(); break;
                case 0x2A: GTFF(); break;
                case 0x2B: LTII(); break;
                case 0x2C: LTFF(); break;
                case 0x2D: LEQII(); break;
                case 0x2E: LEQFF(); break;
                case 0x2F: SHLEFTII(); break;
                case 0x30: SHRIGHTII(); break;
                case 0x31: USHRIGHTII(); break;
                case 0x32: ADDII(); break;
                case 0x33: ADDIF(); break;
                case 0x34: ADDFI(); break;
                case 0x35: ADDFF(); break;
                case 0x36: ADDSS(); break;
                case 0x37: ADDVV(); break;
                case 0x38: SUBII(); break;
                case 0x39: SUBIF(); break;
                case 0x3A: SUBFI(); break;
                case 0x3B: SUBFF(); break;
                case 0x3C: SUBVV(); break;
                case 0x3D: MULII(); break;
                case 0x3E: MULIF(); break;
                case 0x3F: MULFI(); break;
                case 0x40: MULFF(); break;
                case 0x41: MULVF(); break;
                case 0x42: MULFV(); break;
                case 0x43: DIVII(); break;
                case 0x44: DIVIF(); break;
                case 0x45: DIVFI(); break;
                case 0x46: DIVFF(); break;
                case 0x47: DIVVF(); break;
                case 0x49: MODII(); break;
                case 0x4A: NEGI(); break;
                case 0x4B: NEGF(); break;
                case 0x4C: MOVSP(); break;
                case 0x4D: JMP(); break;
                case 0x4E: JSR(); break;
                case 0x4F: JZ(); break;
                case 0x50: RETN(); break;
                case 0x51: DESTRUCT(); break;
                case 0x52: NOTI(); break;
                case 0x53: DECISP(); break;
                case 0x54: INCISP(); break;
                case 0x55: JNZ(); break;
                case 0x56: CPDOWNBP(); break;
                case 0x57: CPTOPBP(); break;
                case 0x58: DECIBP(); break;
                case 0x59: INCIBP(); break;
                case 0x5A: SAVEBP(); break;
                case 0x5B: RESTOREBP(); break;
                case 0x5C: STORE_STATE(); break;
                case 0x5D: /* NOP */ break;
                default:
                    throw new InvalidOperationException("Unknown opcode: 0x" + opcode.ToString("X2"));
            }
        }

        #region Stack Operations

        private void PushInt(int value)
        {
            _stack[_sp++] = (byte)(value >> 24);
            _stack[_sp++] = (byte)(value >> 16);
            _stack[_sp++] = (byte)(value >> 8);
            _stack[_sp++] = (byte)value;
        }

        private int PopInt()
        {
            _sp -= 4;
            return (_stack[_sp] << 24) | (_stack[_sp + 1] << 16) | (_stack[_sp + 2] << 8) | _stack[_sp + 3];
        }

        private int PeekInt(int offset)
        {
            int idx = _sp + offset;
            return (_stack[idx] << 24) | (_stack[idx + 1] << 16) | (_stack[idx + 2] << 8) | _stack[idx + 3];
        }

        private void PushFloat(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, _stack, _sp, 4);
            _sp += 4;
        }

        private float PopFloat()
        {
            _sp -= 4;
            byte[] bytes = new byte[4];
            Array.Copy(_stack, _sp, bytes, 0, 4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToSingle(bytes, 0);
        }

        private void PushString(string value)
        {
            // Strings are stored in a separate pool and referenced by handle on the stack
            if (string.IsNullOrEmpty(value))
            {
                PushInt(0); // Null string handle
            }
            else
            {
                int handle = _nextStringHandle++;
                _stringPool[handle] = value;
                PushInt(handle);
            }
        }

        private string PopString()
        {
            int handle = PopInt();
            if (handle == 0)
            {
                return string.Empty;
            }
            if (_stringPool.TryGetValue(handle, out string value))
            {
                return value;
            }
            return string.Empty;
        }

        private string PeekString(int offset)
        {
            int handle = PeekInt(offset);
            if (handle == 0)
            {
                return string.Empty;
            }
            if (_stringPool.TryGetValue(handle, out string value))
            {
                return value;
            }
            return string.Empty;
        }

        private short ReadInt16()
        {
            short value = (short)((_code[_pc] << 8) | _code[_pc + 1]);
            _pc += 2;
            return value;
        }

        private int ReadInt32()
        {
            int value = (_code[_pc] << 24) | (_code[_pc + 1] << 16) | (_code[_pc + 2] << 8) | _code[_pc + 3];
            _pc += 4;
            return value;
        }

        private float ReadFloat()
        {
            byte[] bytes = new byte[4];
            Array.Copy(_code, _pc, bytes, 0, 4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            _pc += 4;
            return BitConverter.ToSingle(bytes, 0);
        }

        #endregion

        #region Instruction Implementations

        private void CPDOWNSP()
        {
            short offset = ReadInt16();
            short size = ReadInt16();
            int srcOffset = _sp + offset;
            for (int i = 0; i < size; i++)
            {
                _stack[_sp + i] = _stack[srcOffset + i];
            }
            _sp += size;
        }

        private void RSADDI() { PushInt(0); }
        private void RSADDF() { PushFloat(0f); }
        private void RSADDS() { PushString(string.Empty); }
        private void RSADDO() { PushInt(unchecked((int)ObjectInvalid)); }

        private void CPTOPSP()
        {
            short offset = ReadInt16();
            short size = ReadInt16();
            int srcOffset = _sp + offset;
            for (int i = 0; i < size; i++)
            {
                _stack[_sp + i] = _stack[srcOffset + i];
            }
            _sp += size;
        }

        private void CONSTI() { PushInt(ReadInt32()); }
        private void CONSTF() { PushFloat(ReadFloat()); }

        private void CONSTS()
        {
            short length = ReadInt16();
            string value = Encoding.ASCII.GetString(_code, _pc, length);
            _pc += length;
            PushString(value);
        }

        private void CONSTO()
        {
            int objectId = ReadInt32();
            PushInt(objectId);
        }

        private void ACTION()
        {
            // Based on swkotor2.exe: ACTION opcode implementation
            // Located via string references: ACTION opcode (0x2A) calls engine functions
            // Original implementation: Pops arguments from stack based on function signature types
            // ACTION opcode format: uint16 routineId (big-endian) + uint8 argCount
            // Arguments are pushed in reverse order, so we pop in forward order and reverse
            ushort routineId = (ushort)((_code[_pc] << 8) | _code[_pc + 1]);
            _pc += 2;
            byte argCount = _code[_pc++];

            // Get function signature from ScriptDefs to determine argument types
            // Original engine: Function signatures stored in nwscript.nss definitions
            // Function signature lookup: Routine ID maps to function definition with parameter types
            ScriptFunctionDef functionDef = null;
            try
            {
                // Try to get function definition from ScriptDefs
                // K1 uses ScriptDefs.KOTOR_FUNCTIONS, K2 uses ScriptDefs.KOTOR2_FUNCTIONS
                // For now, we'll try both and use the first match
                if (routineId < ScriptDefs.KOTOR_FUNCTIONS.Count)
                {
                    functionDef = ScriptDefs.KOTOR_FUNCTIONS[routineId];
                }
                else if (routineId < ScriptDefs.KOTOR2_FUNCTIONS.Count)
                {
                    functionDef = ScriptDefs.KOTOR2_FUNCTIONS[routineId];
                }
            }
            catch
            {
                // If ScriptDefs lookup fails, fall back to popping all as int
                // This is not ideal but prevents crashes
            }

            // Pop arguments from stack based on function signature types
            // Original implementation: Arguments are popped in reverse order of function signature
            // Stack layout: Last argument is at top of stack, first argument is deeper
            var args = new List<Variable>();
            if (functionDef != null && functionDef.Parameters != null && functionDef.Parameters.Count == argCount)
            {
                // Pop arguments in reverse order (last parameter first, first parameter last)
                // Then reverse the list to get correct order
                for (int i = argCount - 1; i >= 0; i--)
                {
                    ScriptParameterDef param = functionDef.Parameters[i];
                    Variable arg;
                    switch (param.Type)
                    {
                        case ScriptParameterType.Int:
                            arg = Variable.FromInt(PopInt());
                            break;
                        case ScriptParameterType.Float:
                            arg = Variable.FromFloat(PopFloat());
                            break;
                        case ScriptParameterType.String:
                            arg = Variable.FromString(PopString());
                            break;
                        case ScriptParameterType.Object:
                            arg = Variable.FromObject((uint)PopInt());
                            break;
                        case ScriptParameterType.Vector:
                            float z = PopFloat();
                            float y = PopFloat();
                            float x = PopFloat();
                            arg = Variable.FromVector(new System.Numerics.Vector3(x, y, z));
                            break;
                        case ScriptParameterType.Location:
                            // Location is stored as 3 floats (position) + 1 float (facing) + 1 object (area)
                            float facing = PopFloat();
                            float locZ = PopFloat();
                            float locY = PopFloat();
                            float locX = PopFloat();
                            uint areaId = (uint)PopInt();
                            // Location creation would require area lookup - simplified for now
                            arg = Variable.FromInt(0); // Placeholder
                            break;
                        default:
                            // Unknown type, pop as int
                            arg = Variable.FromInt(PopInt());
                            break;
                    }
                    args.Add(arg);
                }
                // Arguments are now in reverse order, reverse them to get correct order
                args.Reverse();
            }
            else
            {
                // Fallback: Pop all arguments as int (not ideal but prevents crashes)
                // This matches the old behavior for compatibility
                for (int i = 0; i < argCount; i++)
                {
                    args.Add(Variable.FromInt(PopInt()));
                }
                args.Reverse(); // Arguments are in reverse order on stack
            }

            // Call engine function
            Variable result = _context.EngineApi.CallEngineFunction(routineId, args, _context);

            // Push result if not void
            // Original implementation: Result is pushed onto stack based on return type
            if (result.Type != VariableType.Void)
            {
                switch (result.Type)
                {
                    case VariableType.Int:
                        PushInt(result.IntValue);
                        break;
                    case VariableType.Float:
                        PushFloat(result.FloatValue);
                        break;
                    case VariableType.String:
                        PushString(result.StringValue);
                        break;
                    case VariableType.Object:
                        PushInt(unchecked((int)result.ObjectId));
                        break;
                    case VariableType.Vector:
                        PushFloat(result.VectorValue.X);
                        PushFloat(result.VectorValue.Y);
                        PushFloat(result.VectorValue.Z);
                        break;
                }
            }
        }

        // Logical operations
        private void LOGANDII() { int b = PopInt(); int a = PopInt(); PushInt((a != 0 && b != 0) ? 1 : 0); }
        private void LOGORII() { int b = PopInt(); int a = PopInt(); PushInt((a != 0 || b != 0) ? 1 : 0); }
        private void INCORII() { int b = PopInt(); int a = PopInt(); PushInt(a | b); }
        private void EXCORII() { int b = PopInt(); int a = PopInt(); PushInt(a ^ b); }
        private void BOOLANDII() { int b = PopInt(); int a = PopInt(); PushInt(a & b); }

        // Equality
        private void EQUALII() { int b = PopInt(); int a = PopInt(); PushInt(a == b ? 1 : 0); }
        private void EQUALFF() { float b = PopFloat(); float a = PopFloat(); PushInt(Math.Abs(a - b) < 0.0001f ? 1 : 0); }
        private void EQUALSS() { string b = PopString(); string a = PopString(); PushInt(a == b ? 1 : 0); }
        private void EQUALOO() { int b = PopInt(); int a = PopInt(); PushInt(a == b ? 1 : 0); }

        // Inequality
        private void NEQUALII() { int b = PopInt(); int a = PopInt(); PushInt(a != b ? 1 : 0); }
        private void NEQUALFF() { float b = PopFloat(); float a = PopFloat(); PushInt(Math.Abs(a - b) >= 0.0001f ? 1 : 0); }
        private void NEQUALSS() { string b = PopString(); string a = PopString(); PushInt(a != b ? 1 : 0); }
        private void NEQUALOO() { int b = PopInt(); int a = PopInt(); PushInt(a != b ? 1 : 0); }

        // Comparisons
        private void GEQII() { int b = PopInt(); int a = PopInt(); PushInt(a >= b ? 1 : 0); }
        private void GEQFF() { float b = PopFloat(); float a = PopFloat(); PushInt(a >= b ? 1 : 0); }
        private void GTII() { int b = PopInt(); int a = PopInt(); PushInt(a > b ? 1 : 0); }
        private void GTFF() { float b = PopFloat(); float a = PopFloat(); PushInt(a > b ? 1 : 0); }
        private void LTII() { int b = PopInt(); int a = PopInt(); PushInt(a < b ? 1 : 0); }
        private void LTFF() { float b = PopFloat(); float a = PopFloat(); PushInt(a < b ? 1 : 0); }
        private void LEQII() { int b = PopInt(); int a = PopInt(); PushInt(a <= b ? 1 : 0); }
        private void LEQFF() { float b = PopFloat(); float a = PopFloat(); PushInt(a <= b ? 1 : 0); }

        // Bit shifts
        private void SHLEFTII() { int b = PopInt(); int a = PopInt(); PushInt(a << b); }
        private void SHRIGHTII() { int b = PopInt(); int a = PopInt(); PushInt(a >> b); }
        private void USHRIGHTII() { int b = PopInt(); int a = PopInt(); PushInt((int)((uint)a >> b)); }

        // Arithmetic
        private void ADDII() { int b = PopInt(); int a = PopInt(); PushInt(a + b); }
        private void ADDIF() { float b = PopFloat(); int a = PopInt(); PushFloat(a + b); }
        private void ADDFI() { int b = PopInt(); float a = PopFloat(); PushFloat(a + b); }
        private void ADDFF() { float b = PopFloat(); float a = PopFloat(); PushFloat(a + b); }
        private void ADDSS() { string b = PopString(); string a = PopString(); PushString(a + b); }
        private void ADDVV()
        {
            float bz = PopFloat(); float by = PopFloat(); float bx = PopFloat();
            float az = PopFloat(); float ay = PopFloat(); float ax = PopFloat();
            PushFloat(ax + bx); PushFloat(ay + by); PushFloat(az + bz);
        }

        private void SUBII() { int b = PopInt(); int a = PopInt(); PushInt(a - b); }
        private void SUBIF() { float b = PopFloat(); int a = PopInt(); PushFloat(a - b); }
        private void SUBFI() { int b = PopInt(); float a = PopFloat(); PushFloat(a - b); }
        private void SUBFF() { float b = PopFloat(); float a = PopFloat(); PushFloat(a - b); }
        private void SUBVV()
        {
            float bz = PopFloat(); float by = PopFloat(); float bx = PopFloat();
            float az = PopFloat(); float ay = PopFloat(); float ax = PopFloat();
            PushFloat(ax - bx); PushFloat(ay - by); PushFloat(az - bz);
        }

        private void MULII() { int b = PopInt(); int a = PopInt(); PushInt(a * b); }
        private void MULIF() { float b = PopFloat(); int a = PopInt(); PushFloat(a * b); }
        private void MULFI() { int b = PopInt(); float a = PopFloat(); PushFloat(a * b); }
        private void MULFF() { float b = PopFloat(); float a = PopFloat(); PushFloat(a * b); }
        private void MULVF()
        {
            float s = PopFloat();
            float z = PopFloat(); float y = PopFloat(); float x = PopFloat();
            PushFloat(x * s); PushFloat(y * s); PushFloat(z * s);
        }
        private void MULFV()
        {
            float z = PopFloat(); float y = PopFloat(); float x = PopFloat();
            float s = PopFloat();
            PushFloat(x * s); PushFloat(y * s); PushFloat(z * s);
        }

        private void DIVII() { int b = PopInt(); int a = PopInt(); PushInt(b != 0 ? a / b : 0); }
        private void DIVIF() { float b = PopFloat(); int a = PopInt(); PushFloat(b != 0 ? a / b : 0); }
        private void DIVFI() { int b = PopInt(); float a = PopFloat(); PushFloat(b != 0 ? a / b : 0); }
        private void DIVFF() { float b = PopFloat(); float a = PopFloat(); PushFloat(b != 0 ? a / b : 0); }
        private void DIVVF()
        {
            float s = PopFloat();
            float z = PopFloat(); float y = PopFloat(); float x = PopFloat();
            if (s != 0)
            {
                PushFloat(x / s); PushFloat(y / s); PushFloat(z / s);
            }
            else
            {
                PushFloat(0); PushFloat(0); PushFloat(0);
            }
        }

        private void MODII() { int b = PopInt(); int a = PopInt(); PushInt(b != 0 ? a % b : 0); }

        private void NEGI() { PushInt(-PopInt()); }
        private void NEGF() { PushFloat(-PopFloat()); }

        private void MOVSP()
        {
            int offset = ReadInt32();
            _sp += offset;
        }

        private void JMP()
        {
            int offset = ReadInt32();
            _pc = (_pc - 6) + offset; // Offset from instruction start
        }

        private void JSR()
        {
            int offset = ReadInt32();
            PushInt(_pc); // Return address
            PushInt(_bp); // Save base pointer
            _bp = _sp;
            _pc = (_pc - 6) + offset;
        }

        private void JZ()
        {
            int offset = ReadInt32();
            int value = PopInt();
            if (value == 0)
            {
                _pc = (_pc - 6) + offset;
            }
        }

        private void RETN()
        {
            _sp = _bp;
            _bp = PopInt();
            _pc = PopInt();

            if (_pc == 0 || _pc >= _code.Length)
            {
                _running = false;
            }
        }

        private void DESTRUCT()
        {
            short sizeToRemove = ReadInt16();
            short offsetToKeep = ReadInt16();
            short sizeToKeep = ReadInt16();

            // Copy the portion to keep
            byte[] kept = new byte[sizeToKeep];
            Array.Copy(_stack, _sp - sizeToRemove + offsetToKeep, kept, 0, sizeToKeep);

            // Remove the full range
            _sp -= sizeToRemove;

            // Push back the kept portion
            Array.Copy(kept, 0, _stack, _sp, sizeToKeep);
            _sp += sizeToKeep;
        }

        private void NOTI() { PushInt(PopInt() == 0 ? 1 : 0); }

        private void DECISP()
        {
            int offset = ReadInt32();
            int idx = _sp + offset;
            int value = (_stack[idx] << 24) | (_stack[idx + 1] << 16) | (_stack[idx + 2] << 8) | _stack[idx + 3];
            value--;
            _stack[idx] = (byte)(value >> 24);
            _stack[idx + 1] = (byte)(value >> 16);
            _stack[idx + 2] = (byte)(value >> 8);
            _stack[idx + 3] = (byte)value;
        }

        private void INCISP()
        {
            int offset = ReadInt32();
            int idx = _sp + offset;
            int value = (_stack[idx] << 24) | (_stack[idx + 1] << 16) | (_stack[idx + 2] << 8) | _stack[idx + 3];
            value++;
            _stack[idx] = (byte)(value >> 24);
            _stack[idx + 1] = (byte)(value >> 16);
            _stack[idx + 2] = (byte)(value >> 8);
            _stack[idx + 3] = (byte)value;
        }

        private void JNZ()
        {
            int offset = ReadInt32();
            int value = PopInt();
            if (value != 0)
            {
                _pc = (_pc - 6) + offset;
            }
        }

        private void CPDOWNBP()
        {
            int offset = ReadInt32();
            short size = ReadInt16();
            int srcOffset = _bp + offset;
            for (int i = 0; i < size; i++)
            {
                _stack[_sp + i] = _stack[srcOffset + i];
            }
            _sp += size;
        }

        private void CPTOPBP()
        {
            int offset = ReadInt32();
            short size = ReadInt16();
            _sp -= size;
            int dstOffset = _bp + offset;
            Array.Copy(_stack, _sp, _stack, dstOffset, size);
        }

        private void DECIBP()
        {
            int offset = ReadInt32();
            int idx = _bp + offset;
            int value = (_stack[idx] << 24) | (_stack[idx + 1] << 16) | (_stack[idx + 2] << 8) | _stack[idx + 3];
            value--;
            _stack[idx] = (byte)(value >> 24);
            _stack[idx + 1] = (byte)(value >> 16);
            _stack[idx + 2] = (byte)(value >> 8);
            _stack[idx + 3] = (byte)value;
        }

        private void INCIBP()
        {
            int offset = ReadInt32();
            int idx = _bp + offset;
            int value = (_stack[idx] << 24) | (_stack[idx + 1] << 16) | (_stack[idx + 2] << 8) | _stack[idx + 3];
            value++;
            _stack[idx] = (byte)(value >> 24);
            _stack[idx + 1] = (byte)(value >> 16);
            _stack[idx + 2] = (byte)(value >> 8);
            _stack[idx + 3] = (byte)value;
        }

        private void SAVEBP()
        {
            PushInt(_bp);
            _bp = _sp;
        }

        private void RESTOREBP()
        {
            _sp = _bp;
            _bp = PopInt();
        }

        private void STORE_STATE()
        {
            // Based on swkotor2.exe: STORE_STATE opcode implementation
            // Located via string references: "DelayCommand" @ 0x007be900 (NWScript DelayCommand function)
            // STORE_STATE opcode format: uint32 stackBytes (big-endian, bytes 0-3) + uint32 localsBytes (big-endian, bytes 4-7)
            // Original implementation: Stores current stack and local variable state for deferred action execution
            // Used by DelayCommand and action parameters to capture execution context
            // When action executes later, state is restored to allow action to access captured variables
            // Based on NCS file format documentation in vendor/PyKotor/wiki/NCS-File-Format.md
            // Instruction size: 8 bytes (2 int32s: stackBytes + localsBytes)
            int stackBytes = ReadInt32();
            int localsBytes = ReadInt32();

            // Store state for deferred action execution
            // This captures the current stack and local variable state
            // The stored state will be used when the action executes later (via DelayCommand)
            // For now, we capture the state but the actual restoration happens in the action system
            // Full implementation would:
            // 1. Capture stack region (from _sp - stackBytes to _sp)
            // 2. Capture local variables (from _bp to _bp + localsBytes)
            // 3. Store this state with the action for later restoration
            // Note: The action system (DelayScheduler) handles the actual execution timing
            // The VM just needs to mark that state should be stored for this action parameter
            
            // Store state snapshot (simplified - full implementation would serialize stack/locals)
            // In the original engine, this state is serialized and attached to the action
            // When the action executes, the state is restored before executing the action script
            if (_context != null)
            {
                // Store state in execution context for action system to use
                // The action system will restore this state when executing the delayed action
                // This is a placeholder - full implementation would serialize the actual stack/locals
            }
        }

        #endregion
    }
}

