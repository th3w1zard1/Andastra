using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using TSLPatcher.Core.Common.Script;

namespace TSLPatcher.Core.Formats.NCS.Compiler
{

    /// <summary>
    /// Root compilation context for NSS compilation.
    ///
    /// Manages global scope, function definitions, constants, and compilation state.
    /// Provides symbol resolution and type checking during NSS to NCS compilation.
    ///
    /// References:
    ///     vendor/KotOR.js/src/nwscript/NWScriptCompiler.ts (TypeScript compiler architecture)
    ///     vendor/xoreos-tools/src/nwscript/decompiler.cpp (NCS decompiler, reverse reference for compilation)
    ///     vendor/HoloLSP/server/src/nwscript-parser.ts (NSS parser and AST generation)
    /// </summary>
    public class CodeRoot
    {
        public List<TopLevelObject> Objects { get; set; }
        public Dictionary<string, byte[]> Library { get; set; }
        public List<ScriptFunction> Functions { get; set; }
        public List<ScriptConstant> Constants { get; set; }
        public List<string> LibraryLookup { get; set; }
        public Dictionary<string, FunctionReference> FunctionMap { get; set; }
        private readonly List<ScopedValue> _globalScope = new List<ScopedValue>();
        public Dictionary<string, Struct> StructMap { get; set; }

        public CodeRoot(
            [CanBeNull] List<ScriptConstant> constants,
            List<ScriptFunction> functions,
            IEnumerable<string> libraryLookup,
            Dictionary<string, byte[]> library)
        {
            Objects = new List<TopLevelObject>();
            Library = library ?? new Dictionary<string, byte[]>();
            Functions = functions ?? new List<ScriptFunction>();
            Constants = constants ?? new List<ScriptConstant>();
            LibraryLookup = libraryLookup?.ToList() ?? new List<string>();
            FunctionMap = new Dictionary<string, FunctionReference>();
            StructMap = new Dictionary<string, Struct>();
        }

        public void AddScoped(Identifier identifier, DynamicDataType datatype)
        {
            _globalScope.Insert(0, new ScopedValue(identifier, datatype));
        }

        public GetScopedResult GetScoped(Identifier identifier, CodeRoot root)
        {
            int offset = 0;
            ScopedValue found = null;
            foreach (ScopedValue scoped in _globalScope)
            {
                offset -= scoped.DataType.Size(root);
                if (scoped.Identifier.Equals(identifier))
                {
                    found = scoped;
                    break;
                }
            }

            if (found == null)
            {
                // Provide helpful error with available globals
                List<string> available = _globalScope.Take(10).Select(s => s.Identifier.Label).ToList();
                int more = _globalScope.Count - 10;
                string moreText = more > 0 ? $" (and {more} more)" : "";
                string msg = $"Undefined variable '{identifier}'\n" +
                             $"  Available globals: {string.Join(", ", available)}{moreText}";
                throw new CompileError(msg);
            }

            return new GetScopedResult(true, found.DataType, offset);
        }

        public void Compile(NCS ncs)
        {
            // nwnnsscomp processes the includes and global variable declarations before functions regardless if they are
            // placed before or after function definitions. We will replicate this behavior.

            List<IncludeScript> included = new List<IncludeScript>();
            while (Objects.Any(obj => obj is IncludeScript))
            {
                List<IncludeScript> includes = Objects.OfType<IncludeScript>().ToList();
                IncludeScript include = includes.Last();
                Objects.Remove(include);
                included.Add(include);
                include.Compile(ncs, this);
            }

            List<TopLevelObject> scriptGlobals = Objects.OfType<GlobalVariableDeclaration>()
                .Concat<TopLevelObject>(Objects.OfType<GlobalVariableInitialization>())
                .Concat(Objects.OfType<StructDefinition>())
                .ToList();
            List<TopLevelObject> others = Objects.Where(obj => !included.Contains(obj) && !scriptGlobals.Contains(obj)).ToList();

            if (scriptGlobals.Any())
            {
                foreach (TopLevelObject globalDef in scriptGlobals)
                {
                    globalDef.Compile(ncs, this);
                }
                ncs.Add(NCSInstructionType.SAVEBP, new List<object>());
            }
            int entryIndex = ncs.Instructions.Count;

            foreach (TopLevelObject obj in others)
            {
                obj.Compile(ncs, this);
            }

            if (FunctionMap.ContainsKey("main"))
            {
                ncs.Add(NCSInstructionType.RETN, new List<object>(), null, entryIndex);
                ncs.Add(NCSInstructionType.JSR, new List<object>(), FunctionMap["main"].Instruction, entryIndex);
            }
            else if (FunctionMap.ContainsKey("StartingConditional"))
            {
                ncs.Add(NCSInstructionType.RETN, new List<object>(), null, entryIndex);
                ncs.Add(NCSInstructionType.JSR, new List<object>(), FunctionMap["StartingConditional"].Instruction, entryIndex);
                ncs.Add(NCSInstructionType.RSADDI, new List<object>(), null, entryIndex);
            }
            else
            {
                string msg = "This file has no entry point and cannot be compiled (Most likely an include file).";
                throw new EntryPointError(msg);
            }
        }

        public int ScopeSize()
        {
            return 0 - _globalScope.Sum(scoped => scoped.DataType.Size(this));
        }

        public DynamicDataType CompileJsr(NCS ncs, CodeBlock block, string name, List<Expression> args)
        {
            List<Expression> argsList = new List<Expression>(args);

            FunctionReference funcMap = FunctionMap[name];
            object definition = funcMap.Definition;
            NCSInstruction startInstruction = funcMap.Instruction;

            DynamicDataType returnType = GetReturnType(definition);
            if (returnType == DynamicDataType.INT)
            {
                ncs.Add(NCSInstructionType.RSADDI, new List<object>());
            }
            else if (returnType == DynamicDataType.FLOAT)
            {
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
            }
            else if (returnType == DynamicDataType.STRING)
            {
                ncs.Add(NCSInstructionType.RSADDS, new List<object>());
            }
            else if (returnType == DynamicDataType.VECTOR)
            {
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
            }
            else if (returnType == DynamicDataType.OBJECT)
            {
                ncs.Add(NCSInstructionType.RSADDO, new List<object>());
            }
            else if (returnType == DynamicDataType.TALENT)
            {
                ncs.Add(NCSInstructionType.RSADDTAL, new List<object>());
            }
            else if (returnType == DynamicDataType.EVENT)
            {
                ncs.Add(NCSInstructionType.RSADDEVT, new List<object>());
            }
            else if (returnType == DynamicDataType.LOCATION)
            {
                ncs.Add(NCSInstructionType.RSADDLOC, new List<object>());
            }
            else if (returnType == DynamicDataType.EFFECT)
            {
                ncs.Add(NCSInstructionType.RSADDEFF, new List<object>());
            }
            else if (returnType == DynamicDataType.VOID)
            {
                // No stack allocation for void
            }
            else if (returnType.Builtin == DataType.Struct)
            {
                if (returnType.Struct != null && StructMap.TryGetValue(returnType.Struct, out Struct structDef))
                {
                    structDef.Initialize(ncs, this);
                }
                else
                {
                    throw new CompileError("Unknown struct type for return value");
                }
            }
            else
            {
                throw new CompileError($"Trying to return unsupported type '{returnType.Builtin}'");
            }

            List<FunctionParameter> parameters = GetParameters(definition);
            List<FunctionParameter> requiredParams = parameters.Where(p => p.DefaultValue == null).ToList();

            if (requiredParams.Count > argsList.Count)
            {
                List<string> requiredNames = requiredParams.Select(p => p.Name.Label).ToList();
                string msg = $"Missing required parameters in call to '{name}'\n" +
                             $"  Required: {string.Join(", ", requiredNames)}\n" +
                             $"  Provided {argsList.Count} of {parameters.Count} parameters";
                throw new CompileError(msg);
            }

            while (parameters.Count > argsList.Count)
            {
                int paramIndex = argsList.Count;
                Expression defaultExpr = parameters[paramIndex].DefaultValue;
                if (defaultExpr == null)
                {
                    throw new CompileError($"Missing default value for parameter {paramIndex} in '{name}'");
                }
                argsList.Add(defaultExpr);
            }

            int offset = 0;
            for (int i = 0; i < parameters.Count; i++)
            {
                FunctionParameter param = parameters[i];
                Expression arg = argsList[i];
                DynamicDataType argDatatype = arg.Compile(ncs, this, block);
                offset += argDatatype.Size(this);
                block.TempStack += argDatatype.Size(this);
                if (param.DataType != argDatatype)
                {
                    string msg = $"Parameter type mismatch in call to '{GetIdentifier(definition)}'\n" +
                                 $"  Parameter '{param.Name}' expects: {param.DataType.Builtin}\n" +
                                 $"  Got: {argDatatype.Builtin}";
                    throw new CompileError(msg);
                }
            }
            block.TempStack -= offset;
            ncs.Add(NCSInstructionType.JSR, new List<object>(), startInstruction);

            return returnType;
        }

        private DynamicDataType GetReturnType(object definition)
        {
            if (definition is FunctionDefinition fd)
            {
                return fd.ReturnType;
            }
            if (definition is FunctionForwardDeclaration ffd)
            {
                return ffd.ReturnType;
            }
            throw new CompileError("Invalid function definition type");
        }

        private List<FunctionParameter> GetParameters(object definition)
        {
            if (definition is FunctionDefinition fd)
            {
                return fd.Parameters;
            }
            if (definition is FunctionForwardDeclaration ffd)
            {
                return ffd.Parameters;
            }
            throw new CompileError("Invalid function definition type");
        }

        private Identifier GetIdentifier(object definition)
        {
            if (definition is FunctionDefinition fd)
            {
                return fd.Name;
            }
            if (definition is FunctionForwardDeclaration ffd)
            {
                return ffd.Identifier;
            }
            throw new CompileError("Invalid function definition type");
        }

        [CanBeNull]
        public ScriptFunction FindEngineFunction(string name)
        {
            return Functions.FirstOrDefault(f => f.Name == name);
        }

        [CanBeNull]
        public ScriptConstant FindConstant(string name)
        {
            return Constants.FirstOrDefault(c => c.Name == name);
        }

        [CanBeNull]
        public byte[] FindInclude(string includePath)
        {
            // Can be null if data not found
            if (Library.TryGetValue(includePath, out byte[] data))
            {
                return data;
            }

            foreach (string lookupPath in LibraryLookup)
            {
                string fullPath = Path.Combine(lookupPath, includePath);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllBytes(fullPath);
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Represents a user-defined struct type.
    /// </summary>
    public class Struct
    {
        public Identifier Identifier { get; set; }
        public List<StructMember> Members { get; set; }
        private int? _cachedSize;

        public Struct(Identifier identifier, List<StructMember> members)
        {
            Identifier = identifier;
            Members = members;
        }

        public void Initialize(NCS ncs, CodeRoot root)
        {
            foreach (StructMember member in Members)
            {
                member.Initialize(ncs, root);
            }
        }

        public int Size(CodeRoot root)
        {
            if (_cachedSize.HasValue)
            {
                return _cachedSize.Value;
            }

            _cachedSize = Members.Sum(m => m.Size(root));
            return _cachedSize.Value;
        }

        public int ChildOffset(CodeRoot root, Identifier identifier)
        {
            int size = 0;
            foreach (StructMember member in Members)
            {
                if (member.Identifier.Equals(identifier))
                {
                    return size;
                }
                size += member.Size(root);
            }

            string available = string.Join(", ", Members.Select(m => m.Identifier.Label));
            throw new CompileError(
                $"Unknown member '{identifier}' in struct '{Identifier}'\n" +
                $"  Available members: {available}");
        }

        public DynamicDataType ChildType(CodeRoot root, Identifier identifier)
        {
            foreach (StructMember member in Members)
            {
                if (member.Identifier.Equals(identifier))
                {
                    return member.DataType;
                }
            }

            string available = string.Join(", ", Members.Select(m => m.Identifier.Label));
            throw new CompileError(
                $"Member '{identifier}' not found in struct '{Identifier}'\n" +
                $"  Available members: {available}");
        }
    }

    /// <summary>
    /// Represents a member of a user-defined struct.
    /// </summary>
    public class StructMember
    {
        public DynamicDataType DataType { get; set; }
        public Identifier Identifier { get; set; }

        public StructMember(DynamicDataType dataType, Identifier identifier)
        {
            DataType = dataType;
            Identifier = identifier;
        }

        public void Initialize(NCS ncs, CodeRoot root)
        {
            switch (DataType.Builtin)
            {
                case Common.Script.DataType.Int:
                    ncs.Add(NCSInstructionType.RSADDI, new List<object>());
                    break;
                case Common.Script.DataType.Float:
                    ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                    break;
                case Common.Script.DataType.String:
                    ncs.Add(NCSInstructionType.RSADDS, new List<object>());
                    break;
                case Common.Script.DataType.Object:
                    ncs.Add(NCSInstructionType.RSADDO, new List<object>());
                    break;
                case Common.Script.DataType.Event:
                    ncs.Add(NCSInstructionType.RSADDEVT, new List<object>());
                    break;
                case Common.Script.DataType.Effect:
                    ncs.Add(NCSInstructionType.RSADDEFF, new List<object>());
                    break;
                case Common.Script.DataType.Location:
                    ncs.Add(NCSInstructionType.RSADDLOC, new List<object>());
                    break;
                case Common.Script.DataType.Talent:
                    ncs.Add(NCSInstructionType.RSADDTAL, new List<object>());
                    break;
                case Common.Script.DataType.Vector:
                    ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                    ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                    ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                    break;
                case Common.Script.DataType.Struct:
                    // Can be null if struct not found
                    if (DataType.Struct != null && root.StructMap.TryGetValue(DataType.Struct, out Struct structDef))
                    {
                        structDef.Initialize(ncs, root);
                    }
                    else
                    {
                        throw new CompileError($"Unknown struct type for member '{Identifier}'");
                    }
                    break;
                default:
                    throw new CompileError($"Unsupported struct member type: {DataType.Builtin}");
            }
        }

        public int Size(CodeRoot root)
        {
            return DataType.Size(root);
        }
    }

    /// <summary>
    /// Represents a function definition with implementation.
    /// Contains the function signature (return type, parameters) and the code block
    /// that implements the function body.
    /// <summary>
    /// Represents a function definition with implementation.
    ///
    /// Contains the function signature (return type, parameters) and the code block
    /// that implements the function body.
    ///
    /// Note: Signature and block are currently coupled in this class. Future refactoring
    /// could split these into separate FunctionSignature and CodeBlock for better reusability.
    /// </summary>
    public class FunctionDefinition : TopLevelObject
    {
        public Identifier Name { get; set; }
        public DynamicDataType ReturnType { get; set; }
        public List<FunctionParameter> Parameters { get; set; }
        public CodeBlock Body { get; set; }

        public FunctionDefinition(Identifier name, DynamicDataType returnType, List<FunctionParameter> parameters, CodeBlock body)
        {
            Name = name;
            ReturnType = returnType;
            Parameters = parameters;
            Body = body;

            foreach (FunctionParameter param in parameters)
            {
                body.AddScoped(param.Name, param.DataType);
            }
        }

        public override void Compile(NCS ncs, CodeRoot root)
        {
            string name = Name.Label;

            // Make sure all default parameters appear after the required parameters
            bool previousIsDefault = false;
            foreach (FunctionParameter param in Parameters)
            {
                bool isDefault = param.DefaultValue != null;
                if (previousIsDefault && !isDefault)
                {
                    throw new CompileError(
                        "Function parameter without a default value can't follow one with a default value.");
                }
                previousIsDefault = isDefault;
            }

            // Make sure params are all constant values
            foreach (FunctionParameter param in Parameters)
            {
                if (param.DefaultValue is IdentifierExpression identifierExpr)
                {
                    // Check if it's a constant
                    if (!identifierExpr.IsConstant(root))
                    {
                        throw new CompileError(
                            $"Non-constant default value specified for function prototype parameter '{param.Name}'.");
                    }
                }
            }

            if (root.FunctionMap.ContainsKey(name) && !root.FunctionMap[name].IsPrototype())
            {
                throw new CompileError(
                    $"Function '{name}' is already defined\n" +
                    "  Cannot redefine a function that already has an implementation");
            }
            if (root.FunctionMap.ContainsKey(name) && root.FunctionMap[name].IsPrototype())
            {
                CompileFunctionWithPrototype(root, name, ncs);
            }
            else
            {
                NCSInstruction retn = new NCSInstruction(NCSInstructionType.RETN);

                NCSInstruction functionStart = ncs.Add(NCSInstructionType.NOP, new List<object>());
                Body.Compile(ncs, root, null, retn, null, null);
                ncs.Instructions.Add(retn);

                root.FunctionMap[name] = new FunctionReference(functionStart, this);
            }
        }

        private void CompileFunctionWithPrototype(CodeRoot root, string name, NCS ncs)
        {
            object prototypeDef = root.FunctionMap[name].Definition;
            if (!IsMatchingSignature(prototypeDef))
            {
                // Build detailed error message
                List<string> details = new List<string>();
                if (ReturnType != GetReturnType(prototypeDef))
                {
                    details.Add(
                        $"Return type mismatch: prototype has {GetReturnType(prototypeDef).Builtin}, " +
                        $"definition has {ReturnType.Builtin}");
                }
                if (Parameters.Count != GetParameters(prototypeDef).Count)
                {
                    details.Add(
                        $"Parameter count mismatch: prototype has {GetParameters(prototypeDef).Count}, " +
                        $"definition has {Parameters.Count}");
                }
                else
                {
                    List<FunctionParameter> protoParams = GetParameters(prototypeDef);
                    for (int i = 0; i < Parameters.Count; i++)
                    {
                        if (Parameters[i].DataType != protoParams[i].DataType)
                        {
                            details.Add(
                                $"Parameter {i + 1} type mismatch: prototype has {protoParams[i].DataType.Builtin}, " +
                                $"definition has {Parameters[i].DataType.Builtin}");
                        }
                    }
                }

                string msg = $"Function '{name}' definition does not match its prototype\n" +
                            "  " + string.Join("\n  ", details);
                throw new CompileError(msg);
            }

            // Function has forward declaration, insert the compiled definition after the stub
            NCS temp = new NCS();
            NCSInstruction retn = new NCSInstruction(NCSInstructionType.RETN);
            Body.Compile(temp, root, null, retn, null, null);
            temp.Instructions.Add(retn);

            int stubIndex = ncs.Instructions.IndexOf(root.FunctionMap[name].Instruction);
            ncs.Instructions.InsertRange(stubIndex + 1, temp.Instructions);
        }

        private bool IsMatchingSignature(object prototype)
        {
            if (ReturnType != GetReturnType(prototype))
            {
                return false;
            }
            if (Parameters.Count != GetParameters(prototype).Count)
            {
                return false;
            }
            List<FunctionParameter> protoParams = GetParameters(prototype);
            for (int i = 0; i < Parameters.Count; i++)
            {
                if (Parameters[i].DataType != protoParams[i].DataType)
                {
                    return false;
                }
            }
            return true;
        }

        private DynamicDataType GetReturnType(object definition)
        {
            if (definition is FunctionDefinition fd)
            {
                return fd.ReturnType;
            }
            if (definition is FunctionForwardDeclaration ffd)
            {
                return ffd.ReturnType;
            }
            throw new CompileError("Invalid function definition type");
        }

        private List<FunctionParameter> GetParameters(object definition)
        {
            if (definition is FunctionDefinition fd)
            {
                return fd.Parameters;
            }
            if (definition is FunctionForwardDeclaration ffd)
            {
                return ffd.Parameters;
            }
            throw new CompileError("Invalid function definition type");
        }
    }

    /// <summary>
    /// Represents a function parameter with optional default value.
    /// </summary>
    public class FunctionParameter
    {
        public Identifier Name { get; set; }
        public DynamicDataType DataType { get; set; }
        [CanBeNull]
        public Expression DefaultValue { get; set; }

        public FunctionParameter(Identifier name, [CanBeNull] DynamicDataType dataType, [CanBeNull] Expression defaultValue = null)
        {
            Name = name;
            DataType = dataType;
            DefaultValue = defaultValue;
        }
    }
}

