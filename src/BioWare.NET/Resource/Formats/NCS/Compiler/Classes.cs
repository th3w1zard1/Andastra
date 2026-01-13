using System;
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS;
using BioWare.NET.Resource.Formats.NCS.Compiler.NSS.AST;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.NCS.Compiler
{

    /// <summary>
    /// Result of scoped variable lookup.
    /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:251-255
    /// </summary>
    public class GetScopedResult
    {
        public bool IsGlobal { get; }
        public DynamicDataType Datatype { get; }
        public int Offset { get; }
        public bool IsConst { get; }

        public GetScopedResult(bool isGlobal, DynamicDataType datatype, int offset, bool isConst = false)
        {
            IsGlobal = isGlobal;
            Datatype = datatype;
            Offset = offset;
            IsConst = isConst;
        }

        public void Deconstruct(out bool isGlobal, out DynamicDataType datatype, out int offset, out bool isConst)
        {
            isGlobal = IsGlobal;
            datatype = Datatype;
            offset = Offset;
            isConst = IsConst;
        }

        // Backward compatibility - 3-parameter deconstruct (ignores isConst)
        public void Deconstruct(out bool isGlobal, out DynamicDataType datatype, out int offset)
        {
            isGlobal = IsGlobal;
            datatype = Datatype;
            offset = Offset;
        }
    }

    /// <summary>
    /// Scoped variable in a code block or global scope.
    /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:677-680
    /// </summary>
    public class ScopedValue
    {
        public Identifier Identifier { get; }
        public DynamicDataType DataType { get; }
        public bool IsConst { get; }

        public ScopedValue(Identifier identifier, DynamicDataType dataType, bool isConst = false)
        {
            Identifier = identifier;
            DataType = dataType;
            IsConst = isConst;
        }
    }

    /// <summary>
    /// Reference to a function definition or forward declaration.
    /// </summary>
    public class FunctionReference
    {
        public NCSInstruction Instruction { get; }
        public object Definition { get; } // FunctionForwardDeclaration or FunctionDefinition

        public FunctionReference(NCSInstruction instruction, object definition)
        {
            Instruction = instruction;
            Definition = definition;
        }

        public bool IsPrototype()
        {
            return Definition is FunctionForwardDeclaration;
        }
    }

    /// <summary>
    /// Function forward declaration (prototype).
    /// </summary>
    public class FunctionForwardDeclaration : TopLevelObject
    {
        public DynamicDataType ReturnType { get; }
        public Identifier Identifier { get; }
        public List<FunctionParameter> Parameters { get; }

        public FunctionForwardDeclaration(
            DynamicDataType returnType,
            Identifier identifier,
            List<FunctionParameter> parameters)
        {
            ReturnType = returnType;
            Identifier = identifier;
            Parameters = parameters;
        }

        public override void Compile(NCS ncs, CodeRoot root)
        {
            string functionName = Identifier.Label;

            if (root.FunctionMap.ContainsKey(functionName))
            {
                throw new NSS.CompileError($"Function '{functionName}' already has a prototype or been defined.");
            }

            root.FunctionMap[functionName] = new FunctionReference(
                ncs.Add(NCSInstructionType.NOP, new List<object>()),
                this
            );
        }
    }
}
