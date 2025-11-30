using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Common.Script;

namespace TSLPatcher.Core.Formats.NCS.Compiler
{

    /// <summary>
    /// NCS bytecode interpreter for testing and debugging.
    ///
    /// Executes NCS bytecode instructions to test script behavior. Partially implemented
    /// for testing purposes, not used in the compilation process. Supports stack-based
    /// execution, function calls, and instruction limit protection.
    ///
    /// References:
    ///     vendor/KotOR.js/src/odyssey/controllers/ (Runtime script execution)
    ///     vendor/reone/src/libs/script/format/ncsreader.cpp (NCS instruction reading)
    ///     vendor/xoreos-tools/src/nwscript/decompiler.cpp (NCS instruction semantics)
    ///     Note: Interpreter is PyKotor-specific for testing, not a full runtime implementation
    /// </summary>
    public class Interpreter
    {
        private const int DefaultMaxInstructions = 100_000;

        private readonly NCS _ncs;
        [CanBeNull]
        private NCSInstruction _cursor;
        private int _cursorIndex;
        private readonly List<ScriptFunction> _functions;
        private readonly Dictionary<int, int> _instructionIndices;
        private readonly Stack _stack;
        private readonly List<(NCSInstruction, int)> _returns;
        private readonly Dictionary<string, Func<object[], object>> _mocks;
        private readonly int _maxInstructions;
        private int _instructionsExecuted;

        public List<StackSnapshot> StackSnapshots { get; }
        public List<ActionSnapshot> ActionSnapshots { get; }

        public Interpreter(NCS ncs, Game game = Game.K1, int? maxInstructions = null)
        {
            _ncs = ncs ?? throw new ArgumentNullException(nameof(ncs));
            _cursor = ncs.Instructions.Count > 0 ? ncs.Instructions[0] : null;
            _cursorIndex = 0;
            _functions = GetFunctionsForGame(game);
            // Python: self._instruction_indices: dict[int, int] = {id(instruction): idx for idx, instruction in enumerate(ncs.instructions)}
            // Use RuntimeHelpers.GetHashCode for object identity (similar to Python's id())
            _instructionIndices = new Dictionary<int, int>();
            for (int idx = 0; idx < ncs.Instructions.Count; idx++)
            {
                NCSInstruction inst = ncs.Instructions[idx];
                int objectId = RuntimeHelpers.GetHashCode(inst);
                // Handle potential collisions by keeping the first occurrence
                if (!_instructionIndices.ContainsKey(objectId))
                {
                    _instructionIndices[objectId] = idx;
                }
            }
            _stack = new Stack();
            _returns = new List<(NCSInstruction, int)>();
            _mocks = new Dictionary<string, Func<object[], object>>();
            _maxInstructions = maxInstructions ?? DefaultMaxInstructions;
            _instructionsExecuted = 0;
            StackSnapshots = new List<StackSnapshot>();
            ActionSnapshots = new List<ActionSnapshot>();
        }

        /// <summary>
        /// Execute the NCS script instructions.
        /// </summary>
        public void Run()
        {
            while (_cursor != null)
            {
                if (_instructionsExecuted >= _maxInstructions)
                {
                    throw new InvalidOperationException(
                        $"Instruction limit exceeded: {_instructionsExecuted} instructions executed " +
                        $"(limit: {_maxInstructions}). Possible infinite loop detected at instruction " +
                        $"index {_cursorIndex} ({_cursor?.InsType})");
                }

                _instructionsExecuted++;
                NCSInstruction cursor = _cursor;
                int index = _cursorIndex;
                object jumpValue = null;

                // For JZ/JNZ, pop the value first (Python does this)
                if (cursor.InsType == NCSInstructionType.JZ || cursor.InsType == NCSInstructionType.JNZ)
                {
                    if (_stack.State().Count > 0)
                    {
                        StackObject top = _stack.Pop();
                        jumpValue = IsZero(top) ? 0 : 1;
                    }
                }

                // Execute instruction based on type
                ExecuteInstruction(cursor);

                // Take stack snapshot after executing instruction
                StackSnapshots.Add(new StackSnapshot(cursor, _stack.State()));

                // Handle RETN separately (Python does this before jump handling)
                if (cursor.InsType == NCSInstructionType.RETN)
                {
                    if (_returns.Count > 0)
                    {
                        (NCSInstruction returnInst, int returnIndex) = _returns[_returns.Count - 1];
                        _returns.RemoveAt(_returns.Count - 1);
                        if (returnInst == null)
                        {
                            _cursor = null;
                            _cursorIndex = -1;
                            break;
                        }
                        SetCursor(returnInst, returnIndex);
                        continue;
                    }
                    else
                    {
                        _cursor = null;
                        _cursorIndex = -1;
                        break;
                    }
                }

                // Handle jumps (JMP, JSR, JZ, JNZ)
                if (cursor.InsType == NCSInstructionType.JMP ||
                    cursor.InsType == NCSInstructionType.JSR ||
                    (cursor.InsType == NCSInstructionType.JZ && jumpValue != null && (int)jumpValue == 0) ||
                    (cursor.InsType == NCSInstructionType.JNZ && jumpValue != null && (int)jumpValue != 0))
                {
                    if (cursor.Jump == null)
                    {
                        throw new InvalidOperationException($"Jump instruction {cursor.InsType} at index {index} has no jump target");
                    }
                    NCSInstruction jumpTarget = cursor.Jump;
                    int? targetIndex = GetInstructionIndex(jumpTarget);
                    if (targetIndex == null)
                    {
                        throw new InvalidOperationException($"Jump target for instruction {cursor.InsType} not found in instruction table");
                    }
                    SetCursor(jumpTarget, targetIndex.Value);
                }
                else
                {
                    // Move to next instruction
                    if (index + 1 >= _ncs.Instructions.Count)
                    {
                        break;
                    }
                    SetCursor(_ncs.Instructions[index + 1], index + 1);
                }
            }
        }

        private void SetCursor(NCSInstruction instruction, int? index = null)
        {
            if (index == null)
            {
                int? lookupIndex = GetInstructionIndex(instruction);
                if (lookupIndex == null)
                {
                    throw new InvalidOperationException("Instruction not present in current instruction table");
                }
                index = lookupIndex.Value;
            }
            _cursor = instruction;
            _cursorIndex = index.Value;
        }

        private int? GetInstructionIndex(NCSInstruction instruction)
        {
            int objectId = RuntimeHelpers.GetHashCode(instruction);
            return _instructionIndices.TryGetValue(objectId, out int idx) ? idx : (int?)null;
        }

        private void ExecuteInstruction(NCSInstruction instruction)
        {
            var stackState = _stack.State();
            string argsStr = instruction.Args != null && instruction.Args.Count > 0 ? $" args=[{string.Join(", ", instruction.Args)}]" : "";
            System.Console.WriteLine($"DEBUG ExecuteInstruction: {instruction.InsType}{argsStr}, stack_len={stackState.Count}, stack=[{string.Join(", ", stackState.Select(x => x.ToString()))}]");
            switch (instruction.InsType)
            {
                case NCSInstructionType.CONSTS:
                    if (instruction.Args.Count > 0)
                    {
                        _stack.Add(DataType.String, instruction.Args[0]);
                    }
                    break;

                case NCSInstructionType.CONSTI:
                    if (instruction.Args.Count > 0)
                    {
                        _stack.Add(DataType.Int, instruction.Args[0]);
                    }
                    break;

                case NCSInstructionType.CONSTF:
                    if (instruction.Args.Count > 0)
                    {
                        _stack.Add(DataType.Float, instruction.Args[0]);
                    }
                    break;

                case NCSInstructionType.CONSTO:
                    if (instruction.Args.Count > 0)
                    {
                        _stack.Add(DataType.Object, instruction.Args[0]);
                    }
                    break;

                case NCSInstructionType.CPTOPSP:
                    if (instruction.Args.Count >= 2)
                    {
                        _stack.CopyToTop(Convert.ToInt32(instruction.Args[0]), Convert.ToInt32(instruction.Args[1]));
                    }
                    break;

                case NCSInstructionType.CPDOWNSP:
                    if (instruction.Args.Count >= 2)
                    {
                        _stack.CopyDown(Convert.ToInt32(instruction.Args[0]), Convert.ToInt32(instruction.Args[1]));
                    }
                    break;

                case NCSInstructionType.ACTION:
                    ExecuteAction(instruction);
                    break;

                case NCSInstructionType.MOVSP:
                    if (instruction.Args.Count > 0)
                    {
                        _stack.Move(Convert.ToInt32(instruction.Args[0]));
                    }
                    break;

                case NCSInstructionType.ADDII:
                case NCSInstructionType.ADDIF:
                case NCSInstructionType.ADDFF:
                case NCSInstructionType.ADDFI:
                case NCSInstructionType.ADDSS:
                case NCSInstructionType.ADDVV:
                    _stack.AdditionOp();
                    break;

                case NCSInstructionType.SUBII:
                case NCSInstructionType.SUBIF:
                case NCSInstructionType.SUBFF:
                case NCSInstructionType.SUBFI:
                case NCSInstructionType.SUBVV:
                    _stack.SubtractionOp();
                    break;

                case NCSInstructionType.MULII:
                case NCSInstructionType.MULIF:
                case NCSInstructionType.MULFF:
                case NCSInstructionType.MULFI:
                case NCSInstructionType.MULVF:
                case NCSInstructionType.MULFV:
                    _stack.MultiplicationOp();
                    break;

                case NCSInstructionType.DIVII:
                case NCSInstructionType.DIVIF:
                case NCSInstructionType.DIVFF:
                case NCSInstructionType.DIVFI:
                case NCSInstructionType.DIVVF:
                    _stack.DivisionOp();
                    break;

                case NCSInstructionType.MODII:
                    _stack.ModulusOp();
                    break;

                case NCSInstructionType.NEGI:
                case NCSInstructionType.NEGF:
                    _stack.NegationOp();
                    break;

                case NCSInstructionType.COMPI:
                    _stack.BitwiseNotOp();
                    break;

                case NCSInstructionType.NOTI:
                    _stack.LogicalNotOp();
                    break;

                case NCSInstructionType.LOGANDII:
                    _stack.LogicalAndOp();
                    break;

                case NCSInstructionType.LOGORII:
                    _stack.LogicalOrOp();
                    break;

                case NCSInstructionType.INCORII:
                    _stack.BitwiseOrOp();
                    break;

                case NCSInstructionType.EXCORII:
                    _stack.BitwiseXorOp();
                    break;

                case NCSInstructionType.BOOLANDII:
                    _stack.BitwiseAndOp();
                    break;

                case NCSInstructionType.EQUALII:
                case NCSInstructionType.EQUALFF:
                case NCSInstructionType.EQUALSS:
                case NCSInstructionType.EQUALOO:
                    _stack.LogicalEqualityOp();
                    break;

                case NCSInstructionType.NEQUALII:
                case NCSInstructionType.NEQUALFF:
                case NCSInstructionType.NEQUALSS:
                case NCSInstructionType.NEQUALOO:
                    _stack.LogicalInequalityOp();
                    break;

                case NCSInstructionType.GTII:
                case NCSInstructionType.GTFF:
                    _stack.CompareGreaterThanOp();
                    break;

                case NCSInstructionType.GEQII:
                case NCSInstructionType.GEQFF:
                    _stack.CompareGreaterThanOrEqualOp();
                    break;

                case NCSInstructionType.LTII:
                case NCSInstructionType.LTFF:
                    _stack.CompareLessThanOp();
                    break;

                case NCSInstructionType.LEQII:
                case NCSInstructionType.LEQFF:
                    _stack.CompareLessThanOrEqualOp();
                    break;

                case NCSInstructionType.SHLEFTII:
                    _stack.BitwiseLeftShiftOp();
                    break;

                case NCSInstructionType.SHRIGHTII:
                    _stack.BitwiseRightShiftOp();
                    break;

                case NCSInstructionType.INCxBP:
                    if (instruction.Args.Count > 0)
                    {
                        _stack.IncrementBp(Convert.ToInt32(instruction.Args[0]));
                    }
                    break;

                case NCSInstructionType.DECxBP:
                    if (instruction.Args.Count > 0)
                    {
                        _stack.DecrementBp(Convert.ToInt32(instruction.Args[0]));
                    }
                    break;

                case NCSInstructionType.INCxSP:
                    if (instruction.Args.Count > 0)
                    {
                        _stack.Increment(Convert.ToInt32(instruction.Args[0]));
                    }
                    break;

                case NCSInstructionType.DECxSP:
                    if (instruction.Args.Count > 0)
                    {
                        _stack.Decrement(Convert.ToInt32(instruction.Args[0]));
                    }
                    break;

                case NCSInstructionType.RSADDI:
                    _stack.Add(DataType.Int, 0);
                    break;

                case NCSInstructionType.RSADDF:
                    _stack.Add(DataType.Float, 0.0f);
                    break;

                case NCSInstructionType.RSADDS:
                    _stack.Add(DataType.String, "");
                    break;

                case NCSInstructionType.RSADDO:
                    _stack.Add(DataType.Object, 1);
                    break;

                case NCSInstructionType.RSADDEFF:
                    _stack.Add(DataType.Effect, 0);
                    break;

                case NCSInstructionType.RSADDTAL:
                    _stack.Add(DataType.Talent, 0);
                    break;

                case NCSInstructionType.RSADDLOC:
                    _stack.Add(DataType.Location, 0);
                    break;

                case NCSInstructionType.RSADDEVT:
                    _stack.Add(DataType.Event, 0);
                    break;

                case NCSInstructionType.SAVEBP:
                    _stack.SaveBp();
                    break;

                case NCSInstructionType.RESTOREBP:
                    _stack.RestoreBp();
                    break;

                case NCSInstructionType.CPTOPBP:
                    if (instruction.Args.Count >= 2)
                    {
                        _stack.CopyTopBp(Convert.ToInt32(instruction.Args[0]), Convert.ToInt32(instruction.Args[1]));
                    }
                    break;

                case NCSInstructionType.CPDOWNBP:
                    if (instruction.Args.Count >= 2)
                    {
                        _stack.CopyDownBp(Convert.ToInt32(instruction.Args[0]), Convert.ToInt32(instruction.Args[1]));
                    }
                    break;

                case NCSInstructionType.NOP:
                    break;

                case NCSInstructionType.JSR:
                    if (instruction.Jump != null)
                    {
                        int indexReturnTo = _cursorIndex + 1;
                        NCSInstruction returnTo = indexReturnTo < _ncs.Instructions.Count ? _ncs.Instructions[indexReturnTo] : null;
                        if (returnTo != null)
                        {
                            _returns.Add((returnTo, indexReturnTo));
                        }
                    }
                    break;

                case NCSInstructionType.JZ:
                case NCSInstructionType.JNZ:
                    // Value already popped in Run() method before ExecuteInstruction
                    break;

                case NCSInstructionType.JMP:
                    // Jump handling done in Run() method
                    break;

                case NCSInstructionType.STORE_STATE:
                    StoreState(instruction);
                    break;

                case NCSInstructionType.RETN:
                    if (_returns.Count > 0)
                    {
                        (NCSInstruction returnInst, int returnIndex) = _returns[_returns.Count - 1];
                        _returns.RemoveAt(_returns.Count - 1);
                        _cursor = returnInst;
                        _cursorIndex = returnIndex;
                    }
                    else
                    {
                        _cursor = null;
                        _cursorIndex = -1;
                    }
                    break;

                default:
                    throw new NotImplementedException($"Instruction {instruction.InsType} not implemented");
            }
        }

        private void StoreState(NCSInstruction instruction)
        {
            _stack.StoreState();
            // Python implementation stores action queue - simplified for now
        }

        private void ExecuteAction(NCSInstruction instruction)
        {
            // ACTION instruction: Args[0] = action ID, Args[1] = parameter count
            if (instruction.Args.Count < 2)
            {
                throw new InvalidOperationException("ACTION instruction requires at least 2 arguments");
            }

            int actionId = Convert.ToInt32(instruction.Args[0]);
            int paramCount = Convert.ToInt32(instruction.Args[1]);

            if (actionId < 0 || actionId >= _functions.Count)
            {
                throw new InvalidOperationException($"Action ID {actionId} is out of range (0-{_functions.Count - 1})");
            }

            ScriptFunction function = _functions[actionId];

            if (paramCount != function.Params.Count)
            {
                throw new InvalidOperationException(
                    $"Action '{function.Name}' called with {paramCount} arguments " +
                    $"but expects {function.Params.Count} parameters");
            }

            var argsSnap = new List<StackObject>();

            // Pop arguments from stack in reverse order (last param popped first)
            for (int i = 0; i < paramCount; i++)
            {
                int paramIndex = paramCount - 1 - i; // Reverse order
                if (paramIndex >= function.Params.Count)
                {
                    throw new InvalidOperationException($"Action '{function.Name}' parameter index {paramIndex} out of range");
                }

                if (function.Params[paramIndex].DataType == DataType.Vector)
                {
                    // Vectors are three floats on stack (z, y, x order when popping)
                    if (_stack.State().Count < 3)
                    {
                        throw new InvalidOperationException($"Stack underflow while popping vector for '{function.Name}'");
                    }
                    object z = _stack.Pop().Value;
                    object y = _stack.Pop().Value;
                    object x = _stack.Pop().Value;
                    var vector = new Vector3(
                        Convert.ToSingle(x),
                        Convert.ToSingle(y),
                        Convert.ToSingle(z)
                    );
                    argsSnap.Add(new StackObject(DataType.Vector, vector));
                }
                else
                {
                    if (_stack.State().Count == 0)
                    {
                        throw new InvalidOperationException($"Stack underflow while popping argument for '{function.Name}'");
                    }
                    argsSnap.Add(_stack.Pop());
                }
            }

            // Validate argument types
            for (int i = 0; i < paramCount; i++)
            {
                if (function.Params[i].DataType != argsSnap[i].DataType)
                {
                    throw new InvalidOperationException(
                        $"Action '{function.Name}' parameter '{function.Params[i].Name}' " +
                        $"expects type {function.Params[i].DataType} but got " +
                        $"{argsSnap[i].DataType} with value '{argsSnap[i].Value}'");
                }
            }

            object value = null;
            if (function.ReturnType != DataType.Void)
            {
                if (_mocks.TryGetValue(function.Name, out Func<object[], object> mock))
                {
                    object[] mockArgs = argsSnap.Select(a => a.Value).ToArray();
                    value = mock(mockArgs);
                }
                else
                {
                    value = null;
                }

                if (function.ReturnType == DataType.Vector)
                {
                    if (value == null)
                    {
                        _stack.Add(DataType.Float, 0.0f);
                        _stack.Add(DataType.Float, 0.0f);
                        _stack.Add(DataType.Float, 0.0f);
                    }
                    else
                    {
                        Vector3 vec = value is Vector3 v ? v : new Vector3(0, 0, 0);
                        _stack.Add(DataType.Float, vec.X);
                        _stack.Add(DataType.Float, vec.Y);
                        _stack.Add(DataType.Float, vec.Z);
                    }
                }
                else
                {
                    _stack.Add(function.ReturnType, value);
                }
            }

            ActionSnapshots.Add(new ActionSnapshot(function.Name, argsSnap, null));
        }

        private static bool IsZero(StackObject obj)
        {
            if (obj.Value == null)
            {
                return true;
            }
            if (obj.Value is int i)
            {
                return i == 0;
            }
            if (obj.Value is float f)
            {
                return Math.Abs(f) < float.Epsilon;
            }
            if (obj.Value is string s)
            {
                return string.IsNullOrEmpty(s);
            }
            return false;
        }

        /// <summary>
        /// Set a mock function for testing.
        /// </summary>
        public void SetMock(string functionName, Func<object[], object> mock)
        {
            ScriptFunction function = _functions.FirstOrDefault(f => f.Name == functionName);
            if (function == null)
            {
                throw new ArgumentException($"Function '{functionName}' does not exist.");
            }

            // Python validates parameter count using signature(mock).parameters
            // For C#, we can't easily inspect the Func signature, so we'll validate at call time
            _mocks[functionName] = mock;
        }

        /// <summary>
        /// Remove a mock function.
        /// </summary>
        public void RemoveMock(string functionName)
        {
            _mocks.Remove(functionName);
        }

        private static List<ScriptFunction> GetFunctionsForGame(Game game)
        {
            if (game == Game.K1)
            {
                return ScriptDefs.KOTOR_FUNCTIONS;
            }
            else if (game == Game.K2)
            {
                return ScriptDefs.TSL_FUNCTIONS;
            }
            else
            {
                return new List<ScriptFunction>();
            }
        }
    }

    /// <summary>
    /// Represents a snapshot of the stack at a particular instruction.
    /// </summary>
    public class StackSnapshot
    {
        public NCSInstruction Instruction { get; }
        public List<StackObject> Stack { get; }

        public StackSnapshot(NCSInstruction instruction, List<StackObject> stack)
        {
            Instruction = instruction;
            Stack = stack;
        }
    }

    /// <summary>
    /// Represents a snapshot of an action call.
    /// </summary>
    public class ActionSnapshot
    {
        public string FunctionName { get; }
        public List<StackObject> ArgValues { get; }
        [CanBeNull] public object ReturnValue { get; }

        public ActionSnapshot(string functionName, [CanBeNull] List<StackObject> argValues, object returnValue)
        {
            FunctionName = functionName;
            ArgValues = argValues;
            ReturnValue = returnValue;
        }
    }
}

