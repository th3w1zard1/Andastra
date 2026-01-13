using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using BioWare.NET.Common.Script;
using BioWare.NET.Extract.Installation;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Scripting.Interfaces;
using Andastra.Runtime.Scripting.Types;

namespace Andastra.Game.Scripting.VM
{
    /// <summary>
    /// NWScript Compiled Script Virtual Machine implementation.
    /// </summary>
    /// <remarks>
    /// NCS Virtual Machine:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) NCS VM implementation
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
    public class NcsVm : INcsVm, IDisposable
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

        // Location storage (Location objects are stored off-stack with IDs)
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Location object management system
        // Located via string references: "LOCATION" @ 0x007c2850 (location type constant)
        // Original implementation: Location objects are managed by engine, stored in catalog with unique IDs
        // Location IDs start from 0x80000000 to avoid conflicts with object IDs (0x7F000000+ range)
        private Dictionary<uint, Location> _locationPool;
        private uint _nextLocationId;
        private const uint LocationIdBase = 0x80000000; // Location IDs start here to avoid conflicts with object IDs

        // Stack size
        private const int StackSize = 65536;

        public NcsVm()
        {
            _stack = new byte[StackSize];
            _stringPool = new Dictionary<int, string>();
            _locationPool = new Dictionary<uint, Location>();
            _nextLocationId = LocationIdBase;
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

            // Set current execution context for async execution flow tracking
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Execution context stack tracking for delayed script execution
            // Original implementation: Sets current execution context so delayed actions can access caller/triggerer
            ExecutionContext.SetCurrent(ctx);

            // Clear pools for new execution
            _stringPool.Clear();
            _nextStringHandle = 1; // Start at 1, 0 reserved for null/empty
            ClearLocations(); // Clear location pool for new script execution

            Array.Clear(_stack, 0, _stack.Length);

            // Restore stored VM state if present (for DelayCommand state restoration)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): State restoration for delayed actions
            // Original implementation: Restores stack, locals, and pools before executing delayed action
            if (ctx != null && ctx is ExecutionContext execContext && execContext.StoredVmState != null)
            {
                RestoreState(execContext.StoredVmState);
                // Clear the stored state after restoration to prevent re-restoration
                execContext.StoredVmState = null;
            }

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
                // Clear current execution context after script execution completes
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Execution context stack tracking cleanup
                // Original implementation: Clears current execution context when script execution ends
                ExecutionContext.SetCurrent(null);
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
            // Fallback to BioWare.NET Installation provider
            else if (provider is Installation installation)
            {
                BioWare.NET.Extract.Installation.ResourceResult result = installation.Resource(resRef, ResourceType.NCS, null, null);
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
                case 0x06: RSADDEFF(); break;
                case 0x07: RSADDEVT(); break;
                case 0x08: RSADDLOC(); break;
                case 0x09: RSADDTAL(); break;
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
                case 0x19: EQUALTT(); break;
                case 0x1A: EQUALEFFEFF(); break;
                case 0x1B: EQUALEVTEVT(); break;
                case 0x1C: EQUALLOCLOC(); break;
                case 0x1D: EQUALTALTAL(); break;
                case 0x1E: NEQUALII(); break;
                case 0x1F: NEQUALFF(); break;
                case 0x20: NEQUALSS(); break;
                case 0x21: NEQUALOO(); break;
                case 0x22: NEQUALTT(); break;
                case 0x23: NEQUALEFFEFF(); break;
                case 0x24: NEQUALEVTEVT(); break;
                case 0x25: NEQUALLOCLOC(); break;
                case 0x26: NEQUALTALTAL(); break;
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
                case 0x48: DIVFV(); break;
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
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): RSADDEFF reserves 4 bytes for effect type on stack
        // Located via string references: Effect type storage (4 bytes, same as object)
        // Original implementation: Effect types are stored as 4-byte values on stack
        private void RSADDEFF() { PushInt(0); } // Effect type (4 bytes, initialized to 0)
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): RSADDEVT reserves 4 bytes for event type on stack
        // Located via string references: Event type storage (4 bytes, same as object)
        // Original implementation: Event types are stored as 4-byte values on stack
        private void RSADDEVT() { PushInt(0); } // Event type (4 bytes, initialized to 0)
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): RSADDLOC reserves 4 bytes for location type on stack
        // Located via string references: Location type storage (4 bytes, same as object)
        // Original implementation: Location types are stored as 4-byte object references on stack
        private void RSADDLOC() { PushInt(unchecked((int)ObjectInvalid)); } // Location (4 bytes, initialized to OBJECT_INVALID)
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): RSADDTAL reserves 4 bytes for talent type on stack
        // Located via string references: Talent type storage (4 bytes, same as object)
        // Original implementation: Talent types are stored as 4-byte values on stack
        private void RSADDTAL() { PushInt(0); } // Talent type (4 bytes, initialized to 0)

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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ACTION opcode implementation
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ACTION opcode uses function dispatch table indexed by routine ID
            // Routine IDs match function indices from nwscript.nss compilation (0-based index into function table)
            ScriptFunction functionDef = null;
            try
            {
                // Determine which ScriptDefs list to use based on EngineApi type
                // Kotor1 uses ScriptDefs.KOTOR_FUNCTIONS, TheSithLords uses ScriptDefs.TSL_FUNCTIONS
                // Original engine: Each game version has its own function table
                bool isK1 = _context.EngineApi.GetType().Name.Contains("Kotor1");

                if (isK1)
                {
                    // Kotor1: Use KOTOR_FUNCTIONS list
                    if (routineId < ScriptDefs.KOTOR_FUNCTIONS.Count)
                    {
                        functionDef = ScriptDefs.KOTOR_FUNCTIONS[routineId];
                    }
                }
                else
                {
                    // TheSithLords: Use TSL_FUNCTIONS list (TheSithLords has more functions than Kotor1)
                    if (routineId < ScriptDefs.TSL_FUNCTIONS.Count)
                    {
                        functionDef = ScriptDefs.TSL_FUNCTIONS[routineId];
                    }
                    // Fallback: Try Kotor1 list if TheSithLords lookup fails (some functions are shared)
                    else if (routineId < ScriptDefs.KOTOR_FUNCTIONS.Count)
                    {
                        functionDef = ScriptDefs.KOTOR_FUNCTIONS[routineId];
                    }
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
            if (functionDef != null && functionDef.Params != null && functionDef.Params.Count == argCount)
            {
                // Pop arguments in reverse order (last parameter first, first parameter last)
                // Then reverse the list to get correct order
                for (int i = argCount - 1; i >= 0; i--)
                {
                    ScriptParam param = functionDef.Params[i];
                    Variable arg;
                    switch (param.DataType)
                    {
                        case DataType.Int:
                            arg = Variable.FromInt(PopInt());
                            break;
                        case DataType.Float:
                            arg = Variable.FromFloat(PopFloat());
                            break;
                        case DataType.String:
                            arg = Variable.FromString(PopString());
                            break;
                        case DataType.Object:
                            arg = Variable.FromObject((uint)PopInt());
                            break;
                        case DataType.Vector:
                            float z = PopFloat();
                            float y = PopFloat();
                            float x = PopFloat();
                            arg = Variable.FromVector(new System.Numerics.Vector3(x, y, z));
                            break;
                        case DataType.Location:
                            // Location is stored on stack as a single object reference (4 bytes = 1 word)
                            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Location type storage
                            // Located via string references: "LOCATION" @ 0x007c2850, "ValLocation" @ 0x007c26ac
                            // Original implementation: Location stored as object reference (off-stack complex object)
                            // The actual Location object (position, facing, area) is managed by the engine
                            // We pop the location ID and retrieve the Location object from registry
                            // Note: Location objects are managed by the VM registry, engine API functions receive Location objects
                            int locationId = PopInt();
                            // Retrieve Location object from registry by ID
                            Location location = GetLocation(unchecked((uint)locationId));
                            // Store the Location object (or null if ID is invalid)
                            arg = Variable.FromLocation(location);
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
                    case VariableType.Location:
                        // Location is pushed as an integer handle/ID (same as Object)
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Location type result pushing
                        // Original implementation: Location objects are managed by engine, stack stores integer handle
                        // Handle both integer IDs (for arguments) and Location objects (for return values)
                        if (result.ComplexValue is int locationId)
                        {
                            // Already an ID, push it directly
                            PushInt(locationId);
                        }
                        else if (result.ComplexValue is Location location)
                        {
                            // Store Location object in registry and return ID
                            uint id = StoreLocation(location);
                            PushInt(unchecked((int)id));
                        }
                        else
                        {
                            // Invalid location, push OBJECT_INVALID equivalent for locations
                            PushInt(unchecked((int)ObjectInvalid));
                        }
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
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EQUALTT compares structures byte-by-byte
        // Located via string references: Structure comparison (qualifier 0x24 = struct/struct)
        // Original implementation: Reads uint16 size field, compares size bytes from stack
        // Format: [0x0B][0x24][uint16 size] - size must be multiple of 4
        // Stack layout: Second structure is at SP-size, first structure is at SP-size*2
        private void EQUALTT()
        {
            short size = ReadInt16(); // Size in bytes (must be multiple of 4)
            bool equal = true;
            // Compare structures byte-by-byte
            // Second structure (b) is at offset -size from SP
            // First structure (a) is at offset -size*2 from SP
            for (int i = 0; i < size; i++)
            {
                int offsetB = _sp - size + i;
                int offsetA = _sp - size * 2 + i;
                if (offsetA < 0 || offsetB < 0 || offsetA >= _stack.Length || offsetB >= _stack.Length)
                {
                    equal = false;
                    break;
                }
                if (_stack[offsetA] != _stack[offsetB])
                {
                    equal = false;
                    break;
                }
            }
            _sp -= size * 2; // Remove both structures from stack
            PushInt(equal ? 1 : 0);
        }
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EQUALEFFEFF compares effect types (4 bytes each)
        // Located via string references: Effect type comparison
        // Original implementation: Compares two 4-byte effect values
        private void EQUALEFFEFF() { int b = PopInt(); int a = PopInt(); PushInt(a == b ? 1 : 0); }
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EQUALEVTEVT compares event types (4 bytes each)
        // Located via string references: Event type comparison
        // Original implementation: Compares two 4-byte event values
        private void EQUALEVTEVT() { int b = PopInt(); int a = PopInt(); PushInt(a == b ? 1 : 0); }
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EQUALLOCLOC compares location types (4 bytes each)
        // Located via string references: Location type comparison
        // Original implementation: Compares two 4-byte location object references
        private void EQUALLOCLOC() { int b = PopInt(); int a = PopInt(); PushInt(a == b ? 1 : 0); }
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EQUALTALTAL compares talent types (4 bytes each)
        // Located via string references: Talent type comparison
        // Original implementation: Compares two 4-byte talent values
        private void EQUALTALTAL() { int b = PopInt(); int a = PopInt(); PushInt(a == b ? 1 : 0); }

        // Inequality
        private void NEQUALII() { int b = PopInt(); int a = PopInt(); PushInt(a != b ? 1 : 0); }
        private void NEQUALFF() { float b = PopFloat(); float a = PopFloat(); PushInt(Math.Abs(a - b) >= 0.0001f ? 1 : 0); }
        private void NEQUALSS() { string b = PopString(); string a = PopString(); PushInt(a != b ? 1 : 0); }
        private void NEQUALOO() { int b = PopInt(); int a = PopInt(); PushInt(a != b ? 1 : 0); }
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): NEQUALTT compares structures byte-by-byte for inequality
        // Located via string references: Structure comparison (qualifier 0x24 = struct/struct)
        // Original implementation: Reads uint16 size field, compares size bytes from stack
        // Format: [0x0C][0x24][uint16 size] - size must be multiple of 4
        // Stack layout: Second structure is at SP-size, first structure is at SP-size*2
        private void NEQUALTT()
        {
            short size = ReadInt16(); // Size in bytes (must be multiple of 4)
            bool equal = true;
            // Compare structures byte-by-byte
            // Second structure (b) is at offset -size from SP
            // First structure (a) is at offset -size*2 from SP
            for (int i = 0; i < size; i++)
            {
                int offsetB = _sp - size + i;
                int offsetA = _sp - size * 2 + i;
                if (offsetA < 0 || offsetB < 0 || offsetA >= _stack.Length || offsetB >= _stack.Length)
                {
                    equal = false;
                    break;
                }
                if (_stack[offsetA] != _stack[offsetB])
                {
                    equal = false;
                    break;
                }
            }
            _sp -= size * 2; // Remove both structures from stack
            PushInt(equal ? 0 : 1); // Inverted for NEQUAL
        }
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): NEQUALEFFEFF compares effect types for inequality (4 bytes each)
        // Located via string references: Effect type comparison
        // Original implementation: Compares two 4-byte effect values
        private void NEQUALEFFEFF() { int b = PopInt(); int a = PopInt(); PushInt(a != b ? 1 : 0); }
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): NEQUALEVTEVT compares event types for inequality (4 bytes each)
        // Located via string references: Event type comparison
        // Original implementation: Compares two 4-byte event values
        private void NEQUALEVTEVT() { int b = PopInt(); int a = PopInt(); PushInt(a != b ? 1 : 0); }
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): NEQUALLOCLOC compares location types for inequality (4 bytes each)
        // Located via string references: Location type comparison
        // Original implementation: Compares two 4-byte location object references
        private void NEQUALLOCLOC() { int b = PopInt(); int a = PopInt(); PushInt(a != b ? 1 : 0); }
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): NEQUALTALTAL compares talent types for inequality (4 bytes each)
        // Located via string references: Talent type comparison
        // Original implementation: Compares two 4-byte talent values
        private void NEQUALTALTAL() { int b = PopInt(); int a = PopInt(); PushInt(a != b ? 1 : 0); }

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
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DIVFV divides vector by float (float, vector)
        // Located via string references: Vector/float division
        // Original implementation: Divides each component of vector by float scalar
        // Stack: [float s][vector z][vector y][vector x] -> [result z][result y][result x]
        private void DIVFV()
        {
            float z = PopFloat(); float y = PopFloat(); float x = PopFloat();
            float s = PopFloat();
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): STORE_STATE opcode implementation
            // Located via string references: "DelayCommand" @ 0x007be900 (NWScript DelayCommand function)
            // STORE_STATE opcode format: uint32 stackBytes (big-endian, bytes 0-3) + uint32 localsBytes (big-endian, bytes 4-7)
            // Original implementation: Stores current stack and local variable state for deferred action execution
            // Used by DelayCommand and action parameters to capture execution context
            // When action executes later, state is restored to allow action to access captured variables
            // Based on NCS file format documentation in vendor/PyKotor/wiki/NCS-File-Format.md
            // Instruction size: 8 bytes (2 int32s: stackBytes + localsBytes)
            int stackBytes = ReadInt32();
            int localsBytes = ReadInt32();

            // Validate stack and locals sizes
            if (stackBytes < 0 || stackBytes > StackSize)
            {
                throw new InvalidOperationException($"Invalid stackBytes in STORE_STATE: {stackBytes}");
            }
            if (localsBytes < 0 || localsBytes > StackSize)
            {
                throw new InvalidOperationException($"Invalid localsBytes in STORE_STATE: {localsBytes}");
            }

            // Calculate source regions
            int stackStart = _sp - stackBytes;
            int localsStart = _bp;
            int localsEnd = _bp + localsBytes;

            // Validate bounds
            if (stackStart < 0 || stackStart > _sp || _sp > _stack.Length)
            {
                throw new InvalidOperationException($"Invalid stack region in STORE_STATE: SP={_sp}, stackBytes={stackBytes}");
            }
            if (localsStart < 0 || localsEnd > _stack.Length)
            {
                throw new InvalidOperationException($"Invalid locals region in STORE_STATE: BP={_bp}, localsBytes={localsBytes}");
            }

            // Create VM state object
            var vmState = new VmState
            {
                StackBytes = stackBytes,
                LocalsBytes = localsBytes,
                StackPointer = _sp,
                BasePointer = _bp,
                NextStringHandle = _nextStringHandle,
                NextLocationId = _nextLocationId
            };

            // Capture stack region (from _sp - stackBytes to _sp)
            if (stackBytes > 0)
            {
                vmState.StackData = new byte[stackBytes];
                Array.Copy(_stack, stackStart, vmState.StackData, 0, stackBytes);
            }
            else
            {
                vmState.StackData = new byte[0];
            }

            // Capture local variables (from _bp to _bp + localsBytes)
            if (localsBytes > 0)
            {
                vmState.LocalsData = new byte[localsBytes];
                Array.Copy(_stack, localsStart, vmState.LocalsData, 0, localsBytes);
            }
            else
            {
                vmState.LocalsData = new byte[0];
            }

            // Capture string pool - need to find all string handles referenced in stack/locals
            // Scan stack and locals data for 4-byte integer values that could be string handles
            var referencedStringHandles = new HashSet<int>();
            ScanForStringHandles(vmState.StackData, referencedStringHandles);
            ScanForStringHandles(vmState.LocalsData, referencedStringHandles);

            // Copy referenced strings to state
            foreach (int handle in referencedStringHandles)
            {
                if (_stringPool.TryGetValue(handle, out string str))
                {
                    vmState.StringPool[handle] = str;
                }
            }

            // Capture location pool - need to find all location IDs referenced in stack/locals
            // Location IDs are in the range 0x80000000 and above
            var referencedLocationIds = new HashSet<uint>();
            ScanForLocationIds(vmState.StackData, referencedLocationIds);
            ScanForLocationIds(vmState.LocalsData, referencedLocationIds);

            // Copy referenced locations to state
            foreach (uint locationId in referencedLocationIds)
            {
                if (_locationPool.TryGetValue(locationId, out Location location))
                {
                    vmState.LocationPool[locationId] = location;
                }
            }

            // Store state in execution context
            if (_context != null && _context is ExecutionContext execContext)
            {
                execContext.StoredVmState = vmState;
            }
        }

        /// <summary>
        /// Scans byte array for 4-byte integer values that could be string handles.
        /// String handles are non-zero integers that exist in the string pool.
        /// </summary>
        private void ScanForStringHandles(byte[] data, HashSet<int> handles)
        {
            if (data == null || data.Length < 4)
            {
                return;
            }

            // Scan 4-byte aligned integers
            for (int i = 0; i <= data.Length - 4; i += 4)
            {
                int value = (data[i] << 24) | (data[i + 1] << 16) | (data[i + 2] << 8) | data[i + 3];
                // Check if this could be a string handle (non-zero and in valid range)
                if (value != 0 && value > 0 && value < int.MaxValue)
                {
                    // Check if it exists in string pool
                    if (_stringPool.ContainsKey(value))
                    {
                        handles.Add(value);
                    }
                }
            }
        }

        /// <summary>
        /// Scans byte array for 4-byte integer values that could be location IDs.
        /// Location IDs are in the range 0x80000000 and above.
        /// </summary>
        private void ScanForLocationIds(byte[] data, HashSet<uint> locationIds)
        {
            if (data == null || data.Length < 4)
            {
                return;
            }

            // Scan 4-byte aligned integers
            for (int i = 0; i <= data.Length - 4; i += 4)
            {
                uint value = (uint)((data[i] << 24) | (data[i + 1] << 16) | (data[i + 2] << 8) | data[i + 3]);
                // Check if this could be a location ID (0x80000000 or above)
                if (value >= LocationIdBase)
                {
                    // Check if it exists in location pool
                    if (_locationPool.ContainsKey(value))
                    {
                        locationIds.Add(value);
                    }
                }
            }
        }

        /// <summary>
        /// Restores VM state from a previously stored VmState.
        /// Used when executing delayed actions to restore the execution context.
        /// </summary>
        /// <param name="state">The VM state to restore.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): State restoration for DelayCommand.
        /// Original implementation: Restores stack, locals, and pools before executing delayed action.
        /// </remarks>
        public void RestoreState(VmState state)
        {
            if (state == null)
            {
                return;
            }

            // Restore stack and base pointers first
            _sp = state.StackPointer;
            _bp = state.BasePointer;

            // Restore stack region (from _sp - stackBytes to _sp)
            if (state.StackData != null && state.StackBytes > 0)
            {
                int stackStart = _sp - state.StackBytes;
                if (stackStart >= 0 && stackStart + state.StackBytes <= _stack.Length)
                {
                    Array.Copy(state.StackData, 0, _stack, stackStart, state.StackBytes);
                }
            }

            // Restore local variables (from _bp to _bp + localsBytes)
            if (state.LocalsData != null && state.LocalsBytes > 0)
            {
                if (_bp + state.LocalsBytes <= _stack.Length)
                {
                    Array.Copy(state.LocalsData, 0, _stack, _bp, state.LocalsBytes);
                }
            }

            // Restore string pool
            foreach (var kvp in state.StringPool)
            {
                _stringPool[kvp.Key] = kvp.Value;
            }
            _nextStringHandle = state.NextStringHandle;

            // Restore location pool
            foreach (var kvp in state.LocationPool)
            {
                _locationPool[kvp.Key] = kvp.Value;
            }
            _nextLocationId = state.NextLocationId;
        }

        /// <summary>
        /// Stores a Location object in the registry and returns its ID.
        /// </summary>
        /// <param name="location">The Location object to store.</param>
        /// <returns>The unique ID for the Location object.</returns>
        private uint StoreLocation(Location location)
        {
            if (location == null)
            {
                return ObjectInvalid;
            }

            uint id = _nextLocationId++;
            _locationPool[id] = location;
            return id;
        }

        /// <summary>
        /// Retrieves a Location object by ID.
        /// </summary>
        /// <param name="locationId">The Location object ID.</param>
        /// <returns>The Location object, or null if not found.</returns>
        private Location GetLocation(uint locationId)
        {
            if (locationId == ObjectInvalid || locationId < LocationIdBase)
            {
                return null;
            }

            if (_locationPool.TryGetValue(locationId, out Location location))
            {
                return location;
            }

            return null;
        }

        /// <summary>
        /// Removes a Location object from the registry.
        /// </summary>
        /// <param name="locationId">The Location object ID to remove.</param>
        private void RemoveLocation(uint locationId)
        {
            if (locationId >= LocationIdBase)
            {
                _locationPool.Remove(locationId);
            }
        }

        /// <summary>
        /// Clears all Location objects from the registry.
        /// </summary>
        private void ClearLocations()
        {
            _locationPool.Clear();
            _nextLocationId = LocationIdBase;
        }

        /// <summary>
        /// Clears all string objects from the string pool.
        /// </summary>
        private void ClearStrings()
        {
            _stringPool.Clear();
            _nextStringHandle = 1; // Start at 1, 0 reserved for null/empty
        }

        /// <summary>
        /// Clears vector-related resources.
        /// Note: Vectors are stored on the stack, so this is a no-op for now.
        /// </summary>
        private void ClearVectors()
        {
            // Vectors are stored directly on the stack, no pool to clear
        }

        #endregion

        /// <summary>
        /// Disposes of resources used by the NCS VM.
        /// </summary>
        public void Dispose()
        {
            // Clear all pools and reset state
            ClearStrings();
            ClearVectors();
            ClearLocations();
            _code = null;
            _stack = null;
        }
    }
}

