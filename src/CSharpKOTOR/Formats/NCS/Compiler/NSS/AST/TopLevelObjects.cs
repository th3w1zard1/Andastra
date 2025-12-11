using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpKOTOR.Common.Script;
using CSharpKOTOR.Formats.NCS;
using CSharpKOTOR.Formats.NCS.Compiler.NSS;
using MissingIncludeError = CSharpKOTOR.Formats.NCS.Compiler.NSS.MissingIncludeError;
using ScriptDataType = CSharpKOTOR.Common.Script.DataType;

namespace CSharpKOTOR.Formats.NCS.Compiler
{
    public class GlobalVariableDeclaration : TopLevelObject
    {
        public Identifier Identifier { get; set; }
        public DynamicDataType DataType { get; set; }

        public GlobalVariableDeclaration(Identifier identifier, DynamicDataType dataType)
        {
            Identifier = identifier;
            DataType = dataType;
        }

        public override void Compile(NCS ncs, CodeRoot root)
        {
            if (DataType.Builtin == ScriptDataType.Int)
            {
                ncs.Add(NCSInstructionType.RSADDI, new List<object>());
            }
            else if (DataType.Builtin == ScriptDataType.Float)
            {
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
            }
            else if (DataType.Builtin == ScriptDataType.String)
            {
                ncs.Add(NCSInstructionType.RSADDS, new List<object>());
            }
            else if (DataType.Builtin == ScriptDataType.Object)
            {
                ncs.Add(NCSInstructionType.RSADDO, new List<object>());
            }
            else if (DataType.Builtin == ScriptDataType.Event)
            {
                ncs.Add(NCSInstructionType.RSADDEVT, new List<object>());
            }
            else if (DataType.Builtin == ScriptDataType.Location)
            {
                ncs.Add(NCSInstructionType.RSADDLOC, new List<object>());
            }
            else if (DataType.Builtin == ScriptDataType.Talent)
            {
                ncs.Add(NCSInstructionType.RSADDTAL, new List<object>());
            }
            else if (DataType.Builtin == ScriptDataType.Effect)
            {
                ncs.Add(NCSInstructionType.RSADDEFF, new List<object>());
            }
            else if (DataType.Builtin == ScriptDataType.Vector)
            {
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
                ncs.Add(NCSInstructionType.RSADDF, new List<object>());
            }
            else if (DataType.Builtin == ScriptDataType.Struct)
            {
                string structName = DataType.Struct;
                if (structName != null && root.StructMap.ContainsKey(structName))
                {
                    root.StructMap[structName].Initialize(ncs, root);
                }
                else
                {
                    string msg = $"Unknown struct type for variable '{Identifier}'";
                    throw new CompileError(msg);
                }
            }
            else if (DataType.Builtin == ScriptDataType.Void)
            {
                string msg = $"Cannot declare variable '{Identifier}' with void type\n" +
                            "  void can only be used as a function return type";
                throw new CompileError(msg);
            }
            else
            {
                string msg = $"Unsupported type '{DataType.Builtin}' for global variable '{Identifier}'\n" +
                            "  This may indicate a compiler bug or unsupported type";
                throw new CompileError(msg);
            }

            root.AddScoped(Identifier, DataType);
        }
    }

    public class GlobalVariableInitialization : TopLevelObject
    {
        public Identifier Identifier { get; set; }
        public DynamicDataType DataType { get; set; }
        public Expression Expression { get; set; }

        public GlobalVariableInitialization(Identifier identifier, DynamicDataType dataType, Expression expression)
        {
            Identifier = identifier;
            DataType = dataType;
            Expression = expression;
        }

        public override void Compile(NCS ncs, CodeRoot root)
        {
            // Allocate storage for the global variable (this also registers it in the global scope)
            GlobalVariableDeclaration declaration = new GlobalVariableDeclaration(Identifier, DataType);
            declaration.Compile(ncs, root);

            CodeBlock block = new CodeBlock();
            DynamicDataType expressionType = Expression.Compile(ncs, root, block);
            if (expressionType != DataType)
            {
                string msg = $"Type mismatch in initialization of global variable '{Identifier}'\n" +
                             $"  Declared type: {DataType.Builtin}\n" +
                             $"  Initializer type: {expressionType.Builtin}";
                throw new CompileError(msg);
            }

            GetScopedResult scoped = root.GetScoped(Identifier, root);
            // Global storage resides on the stack before base pointer is saved, so use stack-pointer-relative copy.
            // Python: scoped.offset already points to the correct location, but for globals we need to subtract size
            // because global offset calculation is different (starts from 0 and goes negative)
            int stackIndex = scoped.Offset - scoped.Datatype.Size(root);
            ncs.Instructions.Add(
                new NCSInstruction(NCSInstructionType.CPDOWNSP, new List<object> { stackIndex, scoped.Datatype.Size(root) })
            );
            // Remove the initializer value from the stack
            ncs.Add(NCSInstructionType.MOVSP, new List<object> { -scoped.Datatype.Size(root) });
        }
    }

    public class StructDefinition : TopLevelObject
    {
        public Identifier Identifier { get; set; }
        public List<StructMember> Members { get; set; }

        public StructDefinition(Identifier identifier, List<StructMember> members)
        {
            Identifier = identifier;
            Members = members;
        }

        public override void Compile(NCS ncs, CodeRoot root)
        {
            if (Members.Count == 0)
            {
                string msg = $"Struct '{Identifier}' cannot be empty\n" +
                            "  Structs must have at least one member";
                throw new CompileError(msg);
            }
            root.StructMap[Identifier.Label] = new Struct(Identifier, Members);
        }
    }

    public class IncludeScript : TopLevelObject
    {
        public StringExpression File { get; set; }
        public Dictionary<string, byte[]> Library { get; set; }

        public IncludeScript(StringExpression file, Dictionary<string, byte[]> library = null)
        {
            File = file;
            Library = library ?? new Dictionary<string, byte[]>();
        }

        public override void Compile(NCS ncs, CodeRoot root)
        {
            List<string> lookupPaths = root.LibraryLookup != null
                ? new List<string>(root.LibraryLookup)
                : null;

            var nssParser = new NssParser(
                root.Functions,
                root.Constants,
                root.Library,
                lookupPaths);
            nssParser.Library = Library;
            nssParser.Constants = root.Constants;
            string source = GetScript(root);
            CodeRoot t = nssParser.Parse(source);

            // Merge include objects (excluding further IncludeScript nodes) so they are compiled.
            var includeObjects = t.Objects.Where(o => !(o is IncludeScript)).ToList();
            root.Objects = includeObjects.Concat(root.Objects).ToList();
            // Constants are already shared via root.constants, so no need to merge
        }

        private string GetScript(CodeRoot root)
        {
            // Try to find in filesystem first
            string source = null;
            foreach (string folder in root.LibraryLookup)
            {
                string filepath = Path.Combine(folder, $"{File.Value}.nss");
                if (System.IO.File.Exists(filepath))
                {
                    try
                    {
                        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:879
                        // Original: source_bytes = filepath.read_bytes(); source = source_bytes.decode(errors="ignore")
                        // Note: Using UTF-8 with fallback for .NET Core compatibility
                        byte[] sourceBytes = System.IO.File.ReadAllBytes(filepath);
                        source = System.Text.Encoding.UTF8.GetString(sourceBytes);
                        break;
                    }
                    catch (Exception e)
                    {
                        string msg = $"Failed to read include file '{filepath}': {e.Message}";
                        throw new NSS.MissingIncludeError(msg, File.Value);
                    }
                }
            }

            if (source == null)
            {
                // Not found in filesystem, try library
                bool caseSensitive = root.LibraryLookup == null || root.LibraryLookup.Count == 0 ||
                    root.LibraryLookup.All(lookupPath => Path.IsPathRooted(lookupPath));
                string includeFilename = caseSensitive ? File.Value : File.Value.ToLowerInvariant();

                if (Library.ContainsKey(includeFilename))
                {
                    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ncs/compiler/classes.py:894
                    // Original: source = self.library[include_filename].decode(errors="ignore")
                    // Note: Using UTF-8 with fallback for .NET Core compatibility
                    source = System.Text.Encoding.UTF8.GetString(Library[includeFilename]);
                }
                else
                {
                    // Build helpful error message with search paths
                    List<string> searchPaths = root.LibraryLookup != null
                        ? new List<string>(root.LibraryLookup)
                        : new List<string>();
                    string pathsText = searchPaths.Count > 0
                        ? string.Join(", ", searchPaths.Take(3)) + (searchPaths.Count > 3 ? "..." : "")
                        : "none";
                    string msg = $"Could not find included script '{includeFilename}.nss'\n" +
                                $"  Searched in {searchPaths.Count} path(s): {pathsText}\n" +
                                $"  Also checked {Library.Count} library file(s)";
                    throw new NSS.MissingIncludeError(msg, includeFilename);
                }
            }
            return source;
        }
    }
}

