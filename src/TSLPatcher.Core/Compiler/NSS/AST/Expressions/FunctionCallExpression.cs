using System;
using System.Collections.Generic;
using System.Linq;
using TSLPatcher.Core.Formats.NCS;

namespace TSLPatcher.Core.Compiler.NSS.AST.Expressions;

/// <summary>
/// Represents a call to a user-defined function.
/// </summary>
public class FunctionCallExpression : Expression
{
    public Identifier FunctionName { get; set; }
    public List<Expression> Arguments { get; set; }

    public FunctionCallExpression(Identifier functionName, List<Expression> arguments)
    {
        FunctionName = functionName ?? throw new ArgumentNullException(nameof(functionName));
        Arguments = arguments ?? new List<Expression>();
    }

    public override DynamicDataType Compile(NCS ncs, CodeRoot root, CodeBlock block)
    {
        if (!root.FunctionMap.TryGetValue(FunctionName, out FunctionDefinition? funcDef))
        {
            // Provide helpful error with similar function names
            IEnumerable<string> availableFuncs = root.FunctionMap.Keys.Take(10).Select(k => k.Label);
            throw new CompileError(
                $"Undefined function '{FunctionName}'\n" +
                $"  Available functions: {string.Join(", ", availableFuncs)}" +
                $"{(root.FunctionMap.Count > 10 ? "..." : "")}");
        }

        // Compile JSR (Jump to Subroutine)
        DynamicDataType returnType = CompileJsr(ncs, root, block, FunctionName, Arguments);

        return returnType;
    }

    private static DynamicDataType CompileJsr(NCS ncs, CodeRoot root, CodeBlock block, Identifier functionName, List<Expression> args)
    {
        if (!root.FunctionMap.TryGetValue(functionName, out FunctionDefinition? funcDef))
        {
            throw new CompileError($"Function '{functionName}' not found");
        }

        // Compile arguments in reverse order
        for (int i = args.Count - 1; i >= 0; i--)
        {
            DynamicDataType argType = args[i].Compile(ncs, root, block);
            block.TempStack += argType.Size(root);
        }

        // Save base pointer
        ncs.Add(NCSInstructionType.SAVEBP, new List<object>());

        // JSR to function - jump target will be set during function compilation
        NCSInstruction jsrInstruction = ncs.Add(NCSInstructionType.JSR, new List<object>());

        // Store reference for later resolution (this would typically be handled in a second pass)
        // For now, we'll leave it as a placeholder that needs to be resolved

        // Restore base pointer
        ncs.Add(NCSInstructionType.RESTOREBP, new List<object>());

        // Clean up arguments from stack
        int argsSize = args.Sum(a => 4); // Most args are 4 bytes, simplified for now
        if (argsSize > 0)
        {
            ncs.Add(NCSInstructionType.MOVSP, new List<object> { -argsSize });
            block.TempStack -= argsSize;
        }

        return funcDef.ReturnType;
    }

    public override string ToString()
    {
        string args = string.Join(", ", Arguments.Select(a => a.ToString()));
        return $"{FunctionName}({args})";
    }
}

