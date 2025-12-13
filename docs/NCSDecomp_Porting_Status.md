# NCSDecomp Porting Status

This document provides a comprehensive overview of what has been ported from `vendor/DeNCS` to the C# implementation in `src/CSharpKOTOR/Formats/NCS/NCSDecomp`.

## Core Logic - Fully Ported ✅

### Main Decompiler Components

- ✅ **FileDecompiler.cs** - Core decompiler coordinator (165KB, 3632 lines)
- ✅ **Decompiler.cs** - Static settings and utilities (4.4KB, 86 lines)
- ✅ **Decoder.cs** - Bytecode decoder (18KB)
- ✅ **Parser.cs** - NCS parser (61KB, 1572 lines)
- ✅ **Lexer.cs** - Token lexer (39KB)

### Analysis Components

- ✅ **Analysis.cs** - Analysis interface (6.7KB)
- ✅ **AnalysisAdapter.cs** - Analysis adapter base (18KB, 755 lines)
- ✅ **CallGraphBuilder.cs** - Call graph construction
- ✅ **CallSiteAnalyzer.cs** - Call site analysis
- ✅ **PrototypeEngine.cs** - Subroutine prototyping engine
- ✅ **SCCUtil.cs** - Strongly-connected components (Tarjan's algorithm)
- ✅ **PrunedDepthFirstAdapter.cs** - Depth-first traversal (24KB, 905 lines)
- ✅ **PrunedReversedDepthFirstAdapter.cs** - Reversed depth-first traversal (23KB, 885 lines)

### Script Node Components

All script node classes have been ported:

- ✅ **ScriptNode.cs** - Base script node class
- ✅ **ScriptRootNode.cs** - Root node with children management (10KB)
- ✅ **AExpression.cs** - Expression interface
- ✅ **AExpressionStatement.cs** - Expression statement wrapper
- ✅ All expression types (ABinaryExp, AUnaryExp, AConditionalExp, etc.)
- ✅ All statement types (AIf, AWhileLoop, AFor, ASwitch, etc.)
- ✅ All control flow types (AControlLoop, ADoLoop, etc.)
- ✅ **ExpressionFormatter.cs** - Expression pretty-printer

### Stack and Variable Management

- ✅ **Variable.cs** - Variable representation (14KB, 317 lines)
- ✅ **VarStruct.cs** - Variable structure (12KB, 255 lines)
- ✅ **LocalVarStack.cs** - Local variable stack (11KB)
- ✅ **LocalTypeStack.cs** - Local type stack (7.0KB, 181 lines)
- ✅ **LocalStack.cs** - Local stack base (2.0KB)
- ✅ **StackEntry.cs** - Stack entry representation (3.3KB, 74 lines)
- ✅ **Const.cs** - Constant base class (4.3KB, 109 lines)
- ✅ **IntConst.cs**, **FloatConst.cs**, **StringConst.cs**, **ObjectConst.cs** - Type-specific constants

### State Management

- ✅ **SubScriptState.cs** - Subroutine script state (92KB, 2150 lines)
- ✅ **SubroutineState.cs** - Subroutine state (25KB, 566 lines)
- ✅ **State.cs** - Parser state (837B, 28 lines)
- ✅ **MainPass.cs** - Main decompilation pass (28KB, 699 lines)
- ✅ **DoGlobalVars.cs** - Global variable processing (5.0KB, 108 lines)
- ✅ **DoTypes.cs** - Type processing (35KB, 790 lines)

### Analysis Data Structures

- ✅ **NodeAnalysisData.cs** - Node analysis data (13KB, 370 lines)
- ✅ **SubroutineAnalysisData.cs** - Subroutine analysis data (in Utils/)
- ✅ **SubroutinePathFinder.cs** - Subroutine path finding (12KB, 352 lines)
- ✅ **NodeUtils.cs** - Node utilities (35KB, 966 lines)

### Utility Classes

- ✅ **Settings.cs** - Application settings (6.2KB)
- ✅ **CompilerUtil.cs** - Compiler utilities (16KB, 353 lines)
- ✅ **NwnnsscompConfig.cs** - Compiler configuration (9.8KB)
- ✅ **KnownExternalCompilers.cs** - External compiler registry (8.3KB)
- ✅ **RoundTripUtil.cs** - Round-trip decompilation utilities (9.5KB)
- ✅ **TreeModelFactory.cs** - Tree model factory for UI (1.6KB)
- ✅ **HashUtil.cs** - Hash utilities (3.3KB)
- ✅ **NWScriptLocator.cs** - NWScript file locator (11KB)

### Compiler Integration

- ✅ **RegistrySpoofer.cs** - Windows registry spoofing for legacy compilers (39KB, 860 lines)
- ✅ **NoOpRegistrySpoofer.cs** - No-op registry spoofer (1.9KB, 42 lines)
- ✅ **CompilerExecutionWrapper.cs** - Compiler execution wrapper (28KB, 558 lines)

### Syntax Highlighting (UI Utilities)

- ✅ **BytecodeSyntaxHighlighter.cs** - Bytecode syntax highlighting patterns
- ✅ **NWScriptSyntaxHighlighter.cs** - NWScript syntax highlighting patterns

### Cleanup and Transformation

- ✅ **CleanupPass.cs** - Code cleanup pass (9.0KB, 199 lines)
- ✅ **DestroyParseTree.cs** - Parse tree destruction (14KB, 489 lines)
- ✅ **FlattenSub.cs** - Subroutine flattening (5.8KB, 167 lines)
- ✅ **SetDestinations.cs** - Destination setting (11KB, 248 lines)
- ✅ **SetPositions.cs** - Position setting (4.9KB, 101 lines)
- ✅ **SetDeadCode.cs** - Dead code elimination (9.4KB)
- ✅ **CheckIsGlobals.cs** - Global variable checking (2.1KB)
- ✅ **NameGenerator.cs** - Name generation (34KB, 896 lines)

### Type System

- ✅ **Type.cs** - Type representation (12KB, 447 lines)
- ✅ **StructType.cs** - Structure type (11KB, 271 lines)
- ✅ **ActionsData.cs** - Actions data (11KB, 230 lines)

### AST Node Classes

All AST node classes from the `node/` directory have been ported:

- ✅ All command nodes (AActionCmd, AAddVarCmd, AConstCmd, etc.)
- ✅ All operator nodes (ABinaryOp, AUnaryOp, etc.)
- ✅ All token nodes (TAdd, TSub, TJmp, etc.)
- ✅ All production nodes (AProgram, ACommandBlock, ASubroutine, etc.)

### Parser Components

- ✅ **Parser.cs** - Main parser (61KB, 1572 lines)
- ✅ **TokenIndex.cs** - Token indexing (6.4KB, 289 lines)
- ✅ **State.cs** - Parser state (837B, 28 lines)
- ✅ **ParserException.cs** - Parser exceptions (1.4KB)
- ✅ **LexerException.cs** - Lexer exceptions (742B)

### Java Compatibility Layer

- ✅ **JavaStubs.cs** - Java API compatibility layer (19KB, 574 lines)
- ✅ **LinkedList.cs** - Linked list implementation (8.3KB, 309 lines)
- ✅ **LinkedListExtensions.cs** - List extensions (5.8KB)
- ✅ **TypedLinkedList.cs** - Typed linked list (4.2KB)
- ✅ **Collection.cs**, **IEnumerator.cs**, **ListIterator.cs** - Collection interfaces

### Exception Classes

- ✅ **DecompilerException.cs** - Main decompiler exception (1.8KB)

## UI Functionality - Documented for Separate Project

The UI functionality from `Decompiler.java` has been documented in:

- ✅ **docs/NCSDecomp_UI_Features.md** - Comprehensive UI feature documentation

This includes:

- Main window layout and components
- Menu bar and toolbar
- Tabbed workspace with multiple views
- Navigation tree with search
- Syntax highlighting integration points
- Keyboard shortcuts
- Drag and drop support
- Settings dialog structure
- Registry spoofing UI prompts

## Not Ported (By Design)

### CLI Tool

- ❌ **NCSDecompCLI.java** - Separate CLI entry point (not core library logic)
  - This is a standalone CLI tool, not part of the core decompiler library
  - CLI functionality can be implemented separately if needed

### UI-Specific Code

- ❌ Swing-specific UI code (JFrame, JPanel, JTextPane, etc.)
  - Documented in `docs/NCSDecomp_UI_Features.md` for implementation in a separate UI project
  - Syntax highlighting patterns provided as utilities for UI projects

## Verification Status

### Core Logic Completeness

✅ All core decompiler logic has been ported
✅ All analysis algorithms are present
✅ All AST node types are implemented
✅ All utility functions are available
✅ Compiler integration is complete

### Code Quality

✅ Source comments reference original Java files
✅ Line numbers and code snippets included where applicable
✅ C# 7.3 compatibility maintained
✅ 1:1 logic parity (not syntax, but functionality)

## Summary

**Total Files Ported**: ~270 Java files → ~270 C# files
**Core Logic**: 100% complete
**UI Documentation**: Complete
**Syntax Highlighting Utilities**: Complete

All core logic from `vendor/DeNCS` has been successfully ported to the C# implementation. The UI functionality has been documented for implementation in a separate UI project (e.g., using Avalonia for cross-platform support).
