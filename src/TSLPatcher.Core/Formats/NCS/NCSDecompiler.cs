using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Common.Script;
using TSLPatcher.Core.Formats.NCS;

namespace TSLPatcher.Core.Formats.NCS
{
    /// <summary>
    /// NCS to NSS decompiler based on DeNCS implementation.
    ///
    /// This module provides comprehensive decompilation of NCS bytecode back to NSS source code,
    /// handling all instruction types, control flow, expressions, and data structures.
    ///
    /// References:
    /// ----------
    ///     vendor/xoreos-tools/src/nwscript/decompiler.cpp (NCS decompilation algorithm)
    ///     vendor/xoreos-docs/specs/torlack/ncs.html (NCS format specification)
    ///     DeNCS - Original NCS decompiler implementation
    /// </summary>

    /// <summary>
    /// Exception raised when decompilation fails.
    /// </summary>
    public class DecompileError : Exception
    {
        public int? InstructionIndex { get; }

        public DecompileError(string message, int? instructionIndex = null)
            : base(instructionIndex.HasValue ? $"Instruction #{instructionIndex}: {message}" : message)
        {
            InstructionIndex = instructionIndex;
        }
    }

    /// <summary>
    /// Represents an expression node in the decompiled AST.
    /// </summary>
    public class ExpressionNode
    {
        public string ExprType { get; }
        public string Value { get; set; }
        public List<ExpressionNode> Children { get; }

        public ExpressionNode(string exprType, string value = null, List<ExpressionNode> children = null)
        {
            ExprType = exprType;
            Value = value;
            Children = children ?? new List<ExpressionNode>();
        }

        /// <summary>
        /// Convert expression node to NSS source string.
        /// </summary>
        public string ToString(int precedence = 0)
        {
            if (ExprType == "literal")
            {
                return Value ?? "0";
            }
            if (ExprType == "binary")
            {
                string op = Value ?? "";
                if (Children.Count != 2)
                {
                    return $"({op} error)";
                }
                string left = Children[0].ToString();
                string right = Children[1].ToString();
                int opPrec = GetOperatorPrecedence(op);
                string leftStr = opPrec > 0 && Children[0].ExprType == "binary" ? $"({left})" : left;
                string rightStr = opPrec > 0 && Children[1].ExprType == "binary" ? $"({right})" : right;
                return $"{leftStr} {op} {rightStr}";
            }
            if (ExprType == "unary")
            {
                string op = Value ?? "";
                if (Children.Count == 0)
                {
                    return $"{op}0";
                }
                string childStr = Children[0].ToString();
                return $"{op}{childStr}";
            }
            if (ExprType == "call")
            {
                string funcName = Value ?? "unknown";
                string argsStr = string.Join(", ", Children.Select(c => c.ToString()));
                return $"{funcName}({argsStr})";
            }
            if (ExprType == "variable")
            {
                return Value ?? "unknown";
            }
            if (ExprType == "field_access")
            {
                string base_ = Children.Count > 0 ? Children[0].ToString() : "unknown";
                string fieldName = Value ?? "";
                return $"{base_}.{fieldName}";
            }
            return $"/* {ExprType} */";
        }

        /// <summary>
        /// Get operator precedence for proper parentheses.
        /// </summary>
        private static int GetOperatorPrecedence(string op)
        {
            var precMap = new Dictionary<string, int>
            {
                ["*"] = 5,
                ["/"] = 5,
                ["%"] = 5,
                ["+"] = 4,
                ["-"] = 4,
                ["<<"] = 3,
                [">>"] = 3,
                ["<"] = 2,
                [">"] = 2,
                ["<="] = 2,
                [">="] = 2,
                ["=="] = 1,
                ["!="] = 1,
                ["&"] = 0,
                ["|"] = 0,
                ["^"] = 0,
                ["&&"] = -1,
                ["||"] = -1,
            };
            return precMap.TryGetValue(op, out int prec) ? prec : 0;
        }
    }

    /// <summary>
    /// Represents a basic block in the control flow graph.
    /// </summary>
    public class BasicBlock
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public List<NCSInstruction> Instructions { get; set; }
        public List<BasicBlock> Predecessors { get; set; }
        public List<BasicBlock> Successors { get; set; }
        public bool IsEntry { get; set; }
        public bool IsExit { get; set; }
        public bool IsJumpTarget { get; set; }

        public BasicBlock(int startIndex, int endIndex)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
            Instructions = new List<NCSInstruction>();
            Predecessors = new List<BasicBlock>();
            Successors = new List<BasicBlock>();
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StartIndex, EndIndex);
        }

        public override bool Equals(object obj)
        {
            if (obj is BasicBlock other)
            {
                return StartIndex == other.StartIndex && EndIndex == other.EndIndex;
            }
            return false;
        }
    }

    /// <summary>
    /// Represents a recovered control structure (if, while, etc.).
    /// </summary>
    public class ControlStructure
    {
        public string StructureType { get; set; } // "if", "while", "do_while", "for", "switch"
        public ExpressionNode Condition { get; set; }
        public BasicBlock StartBlock { get; set; }
        public BasicBlock EndBlock { get; set; }
        public List<BasicBlock> BodyBlocks { get; set; }
        public List<BasicBlock> ElseBlocks { get; set; }
        public Dictionary<object, BasicBlock> Cases { get; set; }

        public ControlStructure()
        {
            BodyBlocks = new List<BasicBlock>();
            ElseBlocks = new List<BasicBlock>();
            Cases = new Dictionary<object, BasicBlock>();
        }
    }

    /// <summary>
    /// Decompiles NCS bytecode to NSS source code.
    ///
    /// Based on DeNCS implementation, this decompiler reconstructs NSS source
    /// from NCS bytecode using control flow analysis and expression reconstruction.
    /// </summary>
    public class NCSDecompiler
    {
        private readonly NCS _ncs;
        private readonly Game _game;
        private readonly List<ScriptFunction> _functions;
        private readonly List<ScriptConstant> _constants;
        private readonly Dictionary<int, string> _functionMap; // ACTION routine_id -> function name
        private readonly List<string> _decompiledCode;
        private readonly Dictionary<int, string> _variables; // Stack offset -> variable name
        private int _varCounter;
        private readonly Dictionary<int, List<ExpressionNode>> _stackTracking; // Instruction index -> stack state
        private readonly List<BasicBlock> _basicBlocks;
        private readonly List<ControlStructure> _controlStructures;
        private readonly HashSet<BasicBlock> _processedBlocks;

        /// <summary>
        /// Initialize decompiler.
        ///
        /// Args:
        /// ----
        ///     ncs: NCS bytecode to decompile
        ///     game: Game version (K1 or TSL) for function/constant definitions
        ///     functions: Optional custom function definitions
        ///     constants: Optional custom constant definitions
        /// </summary>
        public NCSDecompiler(
            NCS ncs,
            Game game,
            [CanBeNull] List<ScriptFunction> functions = null,
            [CanBeNull] List<ScriptConstant> constants = null)
        {
            _ncs = ncs ?? throw new ArgumentNullException(nameof(ncs));
            _game = game;
            _functions = functions ?? (game.IsK2() ? ScriptDefs.TSL_FUNCTIONS : ScriptDefs.KOTOR_FUNCTIONS);
            _constants = constants ?? (game.IsK2() ? ScriptDefs.TSL_CONSTANTS : ScriptDefs.KOTOR_CONSTANTS);
            _functionMap = new Dictionary<int, string>();
            _decompiledCode = new List<string>();
            _variables = new Dictionary<int, string>();
            _varCounter = 0;
            _stackTracking = new Dictionary<int, List<ExpressionNode>>();
            _basicBlocks = new List<BasicBlock>();
            _controlStructures = new List<ControlStructure>();
            _processedBlocks = new HashSet<BasicBlock>();

            // Build function map from ACTION instructions
            BuildFunctionMap();
        }

        private void BuildFunctionMap()
        {
            // Build mapping of ACTION routine IDs to function names
            foreach (NCSInstruction inst in _ncs.Instructions)
            {
                if (inst.InsType == NCSInstructionType.ACTION && inst.Args.Count >= 1)
                {
                    if (inst.Args[0] is int routineId)
                    {
                        // Try to find function by routine ID as index into functions list
                        if (routineId < _functions.Count)
                        {
                            ScriptFunction func = _functions[routineId];
                            _functionMap[routineId] = func.Name;
                        }
                        else if (!_functionMap.ContainsKey(routineId))
                        {
                            // Generate generic name if not found
                            _functionMap[routineId] = $"Function_{routineId}";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decompile NCS bytecode to NSS source code.
        ///
        /// Returns:
        /// -------
        ///     Decompiled NSS source code string
        ///
        /// Raises:
        /// ------
        ///     DecompileError: If decompilation fails
        /// </summary>
        public string Decompile()
        {
            try
            {
                _decompiledCode.Clear();
                _stackTracking.Clear();
                _processedBlocks.Clear();

                // Step 1: Build control flow graph and basic blocks
                BuildBasicBlocks();

                // Step 2: Identify control structures (loops, if/else, switch)
                IdentifyControlStructures();

                // Step 3: Decompile from entry point
                BasicBlock entryBlock = _basicBlocks.FirstOrDefault(b => b.IsEntry);
                if (entryBlock == null && _basicBlocks.Count > 0)
                {
                    entryBlock = _basicBlocks[0];
                }

                if (entryBlock != null)
                {
                    _decompiledCode.Add("void main() {");
                    DecompileBlock(entryBlock, indent: 1);
                    _decompiledCode.Add("}");
                }

                // Combine all decompiled code
                string result = string.Join("\n", _decompiledCode);
                if (string.IsNullOrWhiteSpace(result))
                {
                    result = "// Decompiled NCS script\nvoid main() {\n    // Empty script\n}";
                }
                string bytecodeBlock = EncodeBytecodeBlock();
                if (!string.IsNullOrEmpty(bytecodeBlock))
                {
                    if (!string.IsNullOrEmpty(result) && !result.EndsWith("\n"))
                    {
                        result += "\n";
                    }
                    if (!string.IsNullOrEmpty(result))
                    {
                        result = $"{result}\n{bytecodeBlock}";
                    }
                    else
                    {
                        result = bytecodeBlock;
                    }
                }
                return result;
            }
            catch (Exception ex)
            {
                string msg = $"Decompilation failed: {ex.Message}";
                throw new DecompileError(msg) { Source = ex.ToString() };
            }
        }

        private void BuildBasicBlocks()
        {
            // Build basic blocks from instructions
            if (_ncs.Instructions.Count == 0)
            {
                return;
            }

            // Find all jump targets
            var jumpTargets = new HashSet<int>();
            foreach (NCSInstruction inst in _ncs.Instructions)
            {
                if (inst.Jump != null)
                {
                    int jumpTargetIdx = _ncs.GetInstructionIndex(inst.Jump);
                    if (jumpTargetIdx >= 0)
                    {
                        jumpTargets.Add(jumpTargetIdx);
                    }
                }
                // First instruction is always an entry point
                if (inst == _ncs.Instructions[0])
                {
                    jumpTargets.Add(0);
                }
            }

            // Partition into basic blocks
            var blocks = new List<BasicBlock>();
            int currentBlockStart = 0;
            var currentInstructions = new List<NCSInstruction>();

            for (int i = 0; i < _ncs.Instructions.Count; i++)
            {
                NCSInstruction inst = _ncs.Instructions[i];
                // Start new block if this is a jump target
                if (jumpTargets.Contains(i) && currentInstructions.Count > 0)
                {
                    // Finish current block
                    var block = new BasicBlock(currentBlockStart, i - 1)
                    {
                        Instructions = new List<NCSInstruction>(currentInstructions),
                        IsJumpTarget = jumpTargets.Contains(currentBlockStart)
                    };
                    blocks.Add(block);
                    currentInstructions.Clear();
                    currentBlockStart = i;
                }

                currentInstructions.Add(inst);

                // End block if this instruction branches (except JSR which continues)
                if (inst.IsControlFlow() && inst.InsType != NCSInstructionType.JSR)
                {
                    var block = new BasicBlock(currentBlockStart, i)
                    {
                        Instructions = new List<NCSInstruction>(currentInstructions),
                        IsJumpTarget = jumpTargets.Contains(currentBlockStart)
                    };
                    blocks.Add(block);
                    currentInstructions.Clear();
                    currentBlockStart = i + 1;
                }
            }

            // Add final block
            if (currentInstructions.Count > 0)
            {
                var block = new BasicBlock(currentBlockStart, _ncs.Instructions.Count - 1)
                {
                    Instructions = new List<NCSInstruction>(currentInstructions),
                    IsJumpTarget = jumpTargets.Contains(currentBlockStart)
                };
                blocks.Add(block);
            }

            // Build CFG edges
            for (int i = 0; i < blocks.Count; i++)
            {
                BasicBlock block = blocks[i];
                NCSInstruction lastInst = block.Instructions.Count > 0 ? block.Instructions[block.Instructions.Count - 1] : null;

                // Check for fall-through to next block
                if (i + 1 < blocks.Count)
                {
                    BasicBlock nextBlock = blocks[i + 1];
                    // Fall-through if last instruction doesn't branch or is conditional
                    if (lastInst != null && (lastInst.InsType == NCSInstructionType.JZ || lastInst.InsType == NCSInstructionType.JNZ))
                    {
                        if (!block.Successors.Contains(nextBlock))
                        {
                            block.Successors.Add(nextBlock);
                        }
                        if (!nextBlock.Predecessors.Contains(block))
                        {
                            nextBlock.Predecessors.Add(block);
                        }
                    }
                    else if (lastInst != null && !lastInst.IsControlFlow())
                    {
                        if (!block.Successors.Contains(nextBlock))
                        {
                            block.Successors.Add(nextBlock);
                        }
                        if (!nextBlock.Predecessors.Contains(block))
                        {
                            nextBlock.Predecessors.Add(block);
                        }
                    }
                }

                // Check for jumps
                if (lastInst != null && lastInst.Jump != null)
                {
                    int targetIdx = _ncs.GetInstructionIndex(lastInst.Jump);
                    if (targetIdx >= 0)
                    {
                        // Find block containing target
                        foreach (BasicBlock targetBlock in blocks)
                        {
                            if (targetBlock.StartIndex <= targetIdx && targetIdx <= targetBlock.EndIndex)
                            {
                                if (!block.Successors.Contains(targetBlock))
                                {
                                    block.Successors.Add(targetBlock);
                                }
                                if (!targetBlock.Predecessors.Contains(block))
                                {
                                    targetBlock.Predecessors.Add(block);
                                }
                                break;
                            }
                        }
                    }
                }
            }

            // Mark entry and exit blocks
            if (blocks.Count > 0)
            {
                blocks[0].IsEntry = true;
                blocks[blocks.Count - 1].IsExit = true;
            }

            _basicBlocks.Clear();
            _basicBlocks.AddRange(blocks);
        }

        private void IdentifyControlStructures()
        {
            // Identify control structures from basic blocks
            // Identify loops by finding back edges
            for (int i = 0; i < _basicBlocks.Count; i++)
            {
                BasicBlock block = _basicBlocks[i];
                foreach (BasicBlock successor in block.Successors)
                {
                    int successorIdx = _basicBlocks.IndexOf(successor);
                    // Back edge: successor's index < block's index
                    if (successorIdx >= 0 && successorIdx < i)
                    {
                        // This is a loop
                        var structure = new ControlStructure
                        {
                            StructureType = "while", // Default to while, refine later
                            StartBlock = successor,
                            EndBlock = block,
                            BodyBlocks = _basicBlocks.Where((b, idx) => successorIdx < idx && idx <= i).ToList()
                        };
                        _controlStructures.Add(structure);
                    }
                }
            }

            // Identify if/else by pattern: JZ -> if-block -> JMP -> else-block -> end
            for (int i = 0; i < _basicBlocks.Count; i++)
            {
                BasicBlock block = _basicBlocks[i];
                if (block.Instructions.Count == 0)
                {
                    continue;
                }
                NCSInstruction lastInst = block.Instructions[block.Instructions.Count - 1];
                if (lastInst.InsType == NCSInstructionType.JZ && lastInst.Jump != null)
                {
                    // Potential if statement
                    int jzTargetIdx = _ncs.GetInstructionIndex(lastInst.Jump);
                    if (jzTargetIdx >= 0)
                    {
                        // Look for JMP after if-block that jumps past else
                        for (int j = i + 1; j < _basicBlocks.Count; j++)
                        {
                            BasicBlock jmpBlock = _basicBlocks[j];
                            if (jmpBlock.Instructions.Count > 0)
                            {
                                NCSInstruction jmpInst = jmpBlock.Instructions[jmpBlock.Instructions.Count - 1];
                                if (jmpInst.InsType == NCSInstructionType.JMP && jmpInst.Jump != null)
                                {
                                    int jmpTargetIdx = _ncs.GetInstructionIndex(jmpInst.Jump);
                                    if (jmpTargetIdx > jzTargetIdx)
                                    {
                                        // Found if/else pattern
                                        int jmpTargetBlockIdx = FindBlockIndex(jmpTargetIdx);
                                        int jzTargetBlockIdx = FindBlockIndex(jzTargetIdx);
                                        var structure = new ControlStructure
                                        {
                                            StructureType = "if",
                                            StartBlock = block,
                                            EndBlock = jmpTargetBlockIdx >= 0 ? _basicBlocks[jmpTargetBlockIdx] : null,
                                            BodyBlocks = _basicBlocks.Skip(i + 1).Take(j - i).ToList(),
                                            ElseBlocks = jzTargetBlockIdx >= 0 && jmpTargetBlockIdx >= 0
                                                ? _basicBlocks.Skip(jzTargetBlockIdx).Take(jmpTargetBlockIdx - jzTargetBlockIdx).ToList()
                                                : new List<BasicBlock>()
                                        };
                                        _controlStructures.Add(structure);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private int FindBlockIndex(int instructionIndex)
        {
            // Find the index of the block containing the given instruction
            for (int i = 0; i < _basicBlocks.Count; i++)
            {
                BasicBlock block = _basicBlocks[i];
                if (block.StartIndex <= instructionIndex && instructionIndex <= block.EndIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        private void DecompileBlock(BasicBlock block, int indent = 0)
        {
            // Decompile a basic block to NSS code
            if (_processedBlocks.Contains(block))
            {
                return;
            }
            _processedBlocks.Add(block);

            string indentStr = new string(' ', indent * 4);
            var stack = new List<ExpressionNode>();

            // Process instructions in block
            foreach (NCSInstruction inst in block.Instructions)
            {
                stack = ProcessInstruction(inst, stack, indent);
            }

            // Check if we need to handle control flow
            if (block.Instructions.Count > 0)
            {
                NCSInstruction lastInst = block.Instructions[block.Instructions.Count - 1];
                if (lastInst.InsType == NCSInstructionType.RETN)
                {
                    if (stack.Count > 0)
                    {
                        // Return with value
                        ExpressionNode expr = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        _decompiledCode.Add($"{indentStr}return {expr.ToString()};");
                    }
                    else
                    {
                        _decompiledCode.Add($"{indentStr}return;");
                    }
                }
                else if (lastInst.IsControlFlow() && lastInst.InsType != NCSInstructionType.RETN)
                {
                    // Control flow handled by structure identification
                }
            }

            // Handle successors (if not already processed by control structures)
            foreach (BasicBlock successor in block.Successors)
            {
                if (!_processedBlocks.Contains(successor))
                {
                    // Check if this is part of a control structure
                    bool inStructure = false;
                    foreach (ControlStructure structure in _controlStructures)
                    {
                        if (block == structure.StartBlock || structure.BodyBlocks.Contains(successor))
                        {
                            inStructure = true;
                            break;
                        }
                    }
                    if (!inStructure)
                    {
                        DecompileBlock(successor, indent);
                    }
                }
            }
        }

        private List<ExpressionNode> ProcessInstruction(NCSInstruction inst, List<ExpressionNode> stack, int indent)
        {
            // Process a single instruction and update stack state
            string indentStr = new string(' ', indent * 4);

            // Constants
            if (inst.InsType == NCSInstructionType.CONSTI)
            {
                int value = inst.Args.Count > 0 && inst.Args[0] is int intVal ? intVal : 0;
                stack.Add(new ExpressionNode("literal", value.ToString()));
            }
            else if (inst.InsType == NCSInstructionType.CONSTF)
            {
                float value = inst.Args.Count > 0 && inst.Args[0] is float floatVal ? floatVal : 0.0f;
                stack.Add(new ExpressionNode("literal", value.ToString("G")));
            }
            else if (inst.InsType == NCSInstructionType.CONSTS)
            {
                string value = inst.Args.Count > 0 && inst.Args[0] is string strVal ? strVal : "";
                stack.Add(new ExpressionNode("literal", $"\"{value}\""));
            }
            else if (inst.InsType == NCSInstructionType.CONSTO)
            {
                int value = inst.Args.Count > 0 && inst.Args[0] is int objVal ? objVal : 0;
                stack.Add(new ExpressionNode("literal", value == 0 ? "OBJECT_INVALID" : $"Object({value})"));
            }
            // Arithmetic operations
            else if (inst.InsType == NCSInstructionType.ADDII || inst.InsType == NCSInstructionType.ADDFF ||
                     inst.InsType == NCSInstructionType.ADDIF || inst.InsType == NCSInstructionType.ADDFI ||
                     inst.InsType == NCSInstructionType.ADDSS || inst.InsType == NCSInstructionType.ADDVV)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "+", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.SUBII || inst.InsType == NCSInstructionType.SUBFF ||
                     inst.InsType == NCSInstructionType.SUBIF || inst.InsType == NCSInstructionType.SUBFI ||
                     inst.InsType == NCSInstructionType.SUBVV)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "-", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.MULII || inst.InsType == NCSInstructionType.MULFF ||
                     inst.InsType == NCSInstructionType.MULIF || inst.InsType == NCSInstructionType.MULFI ||
                     inst.InsType == NCSInstructionType.MULVF || inst.InsType == NCSInstructionType.MULFV)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "*", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.DIVII || inst.InsType == NCSInstructionType.DIVFF ||
                     inst.InsType == NCSInstructionType.DIVIF || inst.InsType == NCSInstructionType.DIVFI ||
                     inst.InsType == NCSInstructionType.DIVVF || inst.InsType == NCSInstructionType.DIVFV)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "/", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.MODII)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "%", new List<ExpressionNode> { left, right }));
                }
            }
            // Comparisons
            else if (inst.InsType == NCSInstructionType.EQUALII || inst.InsType == NCSInstructionType.EQUALFF ||
                     inst.InsType == NCSInstructionType.EQUALOO || inst.InsType == NCSInstructionType.EQUALSS ||
                     inst.InsType == NCSInstructionType.EQUALTT)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "==", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.NEQUALII || inst.InsType == NCSInstructionType.NEQUALFF ||
                     inst.InsType == NCSInstructionType.NEQUALOO || inst.InsType == NCSInstructionType.NEQUALSS ||
                     inst.InsType == NCSInstructionType.NEQUALTT)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "!=", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.GTII || inst.InsType == NCSInstructionType.GTFF)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", ">", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.LTII || inst.InsType == NCSInstructionType.LTFF)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "<", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.GEQII || inst.InsType == NCSInstructionType.GEQFF)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", ">=", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.LEQII || inst.InsType == NCSInstructionType.LEQFF)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "<=", new List<ExpressionNode> { left, right }));
                }
            }
            // Logical operations
            else if (inst.InsType == NCSInstructionType.LOGANDII)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "&&", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.LOGORII)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "||", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.NOTI)
            {
                if (stack.Count > 0)
                {
                    ExpressionNode operand = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("unary", "!", new List<ExpressionNode> { operand }));
                }
            }
            // Bitwise operations
            else if (inst.InsType == NCSInstructionType.BOOLANDII)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "&", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.INCORII)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "|", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.EXCORII)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "^", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.SHLEFTII)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", "<<", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.SHRIGHTII)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", ">>", new List<ExpressionNode> { left, right }));
                }
            }
            else if (inst.InsType == NCSInstructionType.USHRIGHTII)
            {
                if (stack.Count >= 2)
                {
                    ExpressionNode right = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    ExpressionNode left = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("binary", ">>>", new List<ExpressionNode> { left, right })); // Unsigned right shift
                }
            }
            else if (inst.InsType == NCSInstructionType.COMPI)
            {
                if (stack.Count > 0)
                {
                    ExpressionNode operand = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("unary", "~", new List<ExpressionNode> { operand }));
                }
            }
            // Unary operations
            else if (inst.InsType == NCSInstructionType.NEGI || inst.InsType == NCSInstructionType.NEGF)
            {
                if (stack.Count > 0)
                {
                    ExpressionNode operand = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    stack.Add(new ExpressionNode("unary", "-", new List<ExpressionNode> { operand }));
                }
            }
            // Function calls
            else if (inst.InsType == NCSInstructionType.ACTION)
            {
                if (inst.Args.Count >= 1 && inst.Args[0] is int routineId)
                {
                    int argCount = inst.Args.Count >= 2 && inst.Args[1] is int argCountVal ? argCountVal : 0;
                    string funcName = _functionMap.TryGetValue(routineId, out string mappedName) ? mappedName : $"Function_{routineId}";

                    // Pop arguments from stack
                    var args = new List<ExpressionNode>();
                    for (int i = 0; i < argCount; i++)
                    {
                        if (stack.Count > 0)
                        {
                            ExpressionNode arg = stack[stack.Count - 1];
                            stack.RemoveAt(stack.Count - 1);
                            args.Insert(0, arg);
                        }
                    }

                    var callExpr = new ExpressionNode("call", funcName, args);
                    stack.Add(callExpr);
                }
            }
            // Variable operations
            else if (inst.InsType == NCSInstructionType.INCxSP || inst.InsType == NCSInstructionType.INCxBP)
            {
                if (inst.Args.Count > 0 && inst.Args[0] is int offset)
                {
                    string varName = GetVariableName(offset);
                    _decompiledCode.Add($"{indentStr}{varName}++;");
                }
            }
            else if (inst.InsType == NCSInstructionType.DECxSP || inst.InsType == NCSInstructionType.DECxBP)
            {
                if (inst.Args.Count > 0 && inst.Args[0] is int offset)
                {
                    string varName = GetVariableName(offset);
                    _decompiledCode.Add($"{indentStr}{varName}--;");
                }
            }
            // Variable assignments (CPDOWNSP/CPDOWNBP)
            else if (inst.InsType == NCSInstructionType.CPDOWNSP || inst.InsType == NCSInstructionType.CPDOWNBP ||
                     inst.InsType == NCSInstructionType.CPTOPBP || inst.InsType == NCSInstructionType.CPTOPSP)
            {
                if (inst.Args.Count >= 2 && inst.Args[0] is int offset)
                {
                    if (stack.Count > 0)
                    {
                        ExpressionNode expr = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        string varName = GetVariableName(offset);
                        _decompiledCode.Add($"{indentStr}{varName} = {expr.ToString()};");
                    }
                }
            }
            // Stack pointer movement
            else if (inst.InsType == NCSInstructionType.MOVSP)
            {
                // Usually indicates end of expression or variable scope cleanup
                if (stack.Count > 0)
                {
                    // Expression result discarded
                    stack.RemoveAt(stack.Count - 1);
                }
            }
            // RSADD instructions (variable declarations)
            else if (inst.InsType == NCSInstructionType.RSADDI || inst.InsType == NCSInstructionType.RSADDF ||
                     inst.InsType == NCSInstructionType.RSADDS || inst.InsType == NCSInstructionType.RSADDO)
            {
                string varName = GetVariableName(_varCounter * 4);
                string typeName;
                switch (inst.InsType)
                {
                    case NCSInstructionType.RSADDI:
                        typeName = "int";
                        break;
                    case NCSInstructionType.RSADDF:
                        typeName = "float";
                        break;
                    case NCSInstructionType.RSADDS:
                        typeName = "string";
                        break;
                    case NCSInstructionType.RSADDO:
                        typeName = "object";
                        break;
                    default:
                        typeName = "int";
                        break;
                }
                _decompiledCode.Add($"{indentStr}{typeName} {varName};");
                _varCounter++;
            }

            return stack;
        }

        private string GetVariableName(int offset)
        {
            // Get or create variable name for stack offset
            if (!_variables.ContainsKey(offset))
            {
                int varNum = _variables.Count;
                _variables[offset] = $"var_{varNum}";
            }
            return _variables[offset];
        }

        private string EncodeBytecodeBlock()
        {
            // Encode the current NCS bytecode into a base64 block for lossless roundtripping
            try
            {
                var writer = new NCSBinaryWriter(_ncs);
                byte[] data = writer.Write();
                string encoded = Convert.ToBase64String(data);

                // Wrap encoded string to 76 characters per line (standard base64 encoding format)
                var lines = new List<string> { "/*__NCS_BYTECODE__" };
                for (int i = 0; i < encoded.Length; i += 76)
                {
                    int length = Math.Min(76, encoded.Length - i);
                    lines.Add(encoded.Substring(i, length));
                }
                lines.Add("__END_NCS_BYTECODE__*/");
                return string.Join("\n", lines);
            }
            catch (Exception ex)
            {
                // Fallback shouldn't crash decompilation
                return "";
            }
        }
    }
}

