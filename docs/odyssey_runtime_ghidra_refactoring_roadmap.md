# OdysseyRuntime Ghidra Refactoring Roadmap

Internal tracking document for AI agents. Not public-facing. Do not commit to repository.

**Status**: IN PROGRESS
**Started**: 2025-01-15
**Total Files**: 442
**Completed**: Checking systematically
**Remaining**: Checking systematically

## Engine Abstraction Refactoring

**Status**: IN PROGRESS  
**Started**: 2025-01-15  
**Purpose**: Abstract KOTOR-specific code from AuroraEngine.Common to support multiple BioWare engine families (Odyssey, Aurora, Eclipse) following xoreos pattern with maximum code in base classes.

**Architecture Document**: See `docs/engine_abstraction_refined_architecture.md` for comprehensive architecture plan.

### Strategy

Following xoreos architecture pattern (but cleaner):

- **AuroraEngine.Common**: Pure file format parsers, installation detection, resource management (engine-agnostic)
- **Odyssey.Engines.Common**: Base engine abstraction (IEngine, BaseEngine, etc.)
- **Odyssey.Engines.Odyssey**: KOTOR 1/2 shared code (like xoreos's `kotorbase`)
- **Odyssey.Engines.Aurora**: NWN/NWN2 shared code (future)
- **Odyssey.Engines.Eclipse**: Dragon Age/Mass Effect shared code (future)

**Key Principle**: Maximize code in base classes, minimize duplication. All shared logic goes in `Odyssey.Engines.Common` base classes.

### Architecture Pattern

```
AuroraEngine.Common (src/CSharpKOTOR/)
    ├── Formats/** - Engine-agnostic file parsers (GFF, 2DA, TLK, MDL, TPC, BWM, LYT, VIS, KEY, BIF, ERF, RIM)
    ├── Installation/** - Installation detection (currently KOTOR-specific, but structure allows expansion)
    ├── Resources/** - Resource management
    └── Resource/Generics/UTC.cs, etc. (kept for patcher tools compatibility - DEPRECATED)

Odyssey.Engines.Common
    ├── IEngine, IEngineGame, IEngineModule, IEngineProfile (interfaces)
    └── BaseEngine, BaseEngineGame, BaseEngineModule, BaseEngineProfile (base classes)

Odyssey.Engines.Odyssey (like xoreos's kotorbase)
    ├── Templates/ - All 9 GFF templates (UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW, UTM) ✅
    ├── Module/ - KOTOR-specific module structure (Module.cs, KModuleType, etc.)
    ├── Profiles/ - OdysseyK1GameProfile, OdysseyK2GameProfile ✅
    ├── EngineApi/ - OdysseyK1EngineApi, OdysseyK2EngineApi ✅
    ├── OdysseyEngine.cs ✅
    ├── OdysseyGameSession.cs ✅
    └── OdysseyModuleLoader.cs ✅
```

### Files to Review/Migrate from AuroraEngine.Common

#### GFF Templates (KOTOR/Odyssey-Specific) - ✅ COMPLETED

- [x] Resource\Generics\UTC.cs → Odyssey.Engines.Odyssey.Templates.UTC.cs
- [x] Resource\Generics\UTCHelpers.cs → Odyssey.Engines.Odyssey.Templates.UTCHelpers.cs
- [x] Resource\Generics\UTD.cs → Odyssey.Engines.Odyssey.Templates.UTD.cs
- [x] Resource\Generics\UTDHelpers.cs → Odyssey.Engines.Odyssey.Templates.UTDHelpers.cs
- [x] Resource\Generics\UTE.cs → Odyssey.Engines.Odyssey.Templates.UTE.cs
- [x] Resource\Generics\UTEHelpers.cs → Odyssey.Engines.Odyssey.Templates.UTEHelpers.cs
- [x] Resource\Generics\UTI.cs → Odyssey.Engines.Odyssey.Templates.UTI.cs
- [x] Resource\Generics\UTIHelpers.cs → Odyssey.Engines.Odyssey.Templates.UTIHelpers.cs
- [x] Resource\Generics\UTP.cs → Odyssey.Engines.Odyssey.Templates.UTP.cs
- [x] Resource\Generics\UTPHelpers.cs → Odyssey.Engines.Odyssey.Templates.UTPHelpers.cs
- [x] Resource\Generics\UTS.cs → Odyssey.Engines.Odyssey.Templates.UTS.cs
- [x] Resource\Generics\UTSHelpers.cs → Odyssey.Engines.Odyssey.Templates.UTSHelpers.cs
- [x] Resource\Generics\UTT.cs → Odyssey.Engines.Odyssey.Templates.UTT.cs
- [x] Resource\Generics\UTTHelpers.cs → Odyssey.Engines.Odyssey.Templates.UTTHelpers.cs
- [x] Resource\Generics\UTW.cs → Odyssey.Engines.Odyssey.Templates.UTW.cs
- [x] Resource\Generics\UTWHelpers.cs → Odyssey.Engines.Odyssey.Templates.UTWHelpers.cs
- [x] Resource\Generics\UTM.cs → Odyssey.Engines.Odyssey.Templates.UTM.cs
- [x] Resource\Generics\UTMHelpers.cs → Odyssey.Engines.Odyssey.Templates.UTMHelpers.cs

**Status**: All 9 GFF templates migrated. Original files kept in AuroraEngine.Common for patcher tools compatibility (DEPRECATED).

#### Module/Area Structures (Review for KOTOR-Specific)

- [ ] Common\Module.cs → Odyssey.Engines.Odyssey.Module.Module.cs
  - [ ] Review: Contains KModuleType enum (.rim, _s.rim,_dlg.erf, .mod) - KOTOR-specific
  - [ ] Move: Module class, KModuleType enum, ModulePieceInfo, ModulePieceResource classes
  - [ ] Update references: All code using Module class
  - [ ] Backward compatibility: Keep in AuroraEngine.Common for patcher tools (DEPRECATED)

- [ ] Common\ModuleDataLoader.cs → Odyssey.Engines.Odyssey.Module.ModuleDataLoader.cs
  - [ ] Review: KOTOR-specific module data loading
  - [ ] Move if KOTOR-specific
  - [ ] Update references

- [ ] Resource\Generics\IFO.cs → Review if KOTOR-specific or engine-agnostic
  - [ ] Review: IFO structure - check if used by Aurora/Eclipse engines
  - [ ] Decision: Keep in AuroraEngine.Common if shared, move to Odyssey.Engines.Odyssey if KOTOR-specific
  - [ ] Note: IFO is GFF-based, but structure may be KOTOR-specific

- [ ] Resource\Generics\ARE.cs → Review if KOTOR-specific or engine-agnostic
  - [ ] Review: ARE (Area) structure - check if used by Aurora/Eclipse engines
  - [ ] Decision: Keep in AuroraEngine.Common if shared, move to Odyssey.Engines.Odyssey if KOTOR-specific

- [ ] Resource\Generics\GIT.cs → Review if KOTOR-specific or engine-agnostic
  - [ ] Review: GIT (Game Instance Template) structure - check if used by Aurora/Eclipse engines
  - [ ] Decision: Keep in AuroraEngine.Common if shared, move to Odyssey.Engines.Odyssey if KOTOR-specific

- [ ] Resource\Generics\JRL.cs → Review if KOTOR-specific or engine-agnostic
  - [ ] Review: JRL (Journal) structure - check if used by Aurora/Eclipse engines
  - [ ] Decision: Keep in AuroraEngine.Common if shared, move to Odyssey.Engines.Odyssey if KOTOR-specific

- [ ] Resource\Generics\PTH.cs → Review if KOTOR-specific or engine-agnostic
  - [ ] Review: PTH (Path) structure - check if used by Aurora/Eclipse engines
  - [ ] Decision: Keep in AuroraEngine.Common if shared, move to Odyssey.Engines.Odyssey if KOTOR-specific
  - [ ] Note: Pathfinding may be engine-specific

#### Dialogue Structures (Review for KOTOR-Specific)

- [ ] Resource\Generics\DLG\DLG.cs → Review if KOTOR-specific or engine-agnostic
  - [ ] Review: DLG (Dialogue) structure - check if used by Aurora/Eclipse engines
  - [ ] Decision: Keep in AuroraEngine.Common if shared, move to Odyssey.Engines.Odyssey if KOTOR-specific

- [ ] Resource\Generics\DLG\DLGNode.cs → Review if KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\DLG\DLGLink.cs → Review if KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\DLG\DLGHelper.cs → Review if KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\DLG\DLGAnimation.cs → Review if KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\DLG\DLGStunt.cs → Review if KOTOR-specific or engine-agnostic

#### GUI Structures (Review for KOTOR-Specific)

- [ ] Resource\Generics\GUI\GUI.cs → Review if KOTOR-specific or engine-agnostic
  - [ ] Review: GUI structure - check if used by Aurora/Eclipse engines
  - [ ] Decision: Keep in AuroraEngine.Common if shared, move to Odyssey.Engines.Odyssey if KOTOR-specific

- [ ] Resource\Generics\GUI\GUIControl.cs → Review if KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\GUI\GUIBorder.cs → Review if KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\GUI\GUIText.cs → Review if KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\GUI\GUIScrollbar.cs → Review if KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\GUI\GUIProgress.cs → Review if KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\GUI\GUIReader.cs → Review if KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\GUI\GUIMoveTo.cs → Review if KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\GUI\GUIEnums.cs → Review if KOTOR-specific or engine-agnostic

#### Game Enum (KOTOR-Specific - Kept for Compatibility)

- [x] Common\Game.cs → Decision: KEEP in AuroraEngine.Common
  - [x] Review: Used extensively by patcher tools (HoloPatcher.NET, HolocronToolset, NCSDecomp, KotorDiff)
  - [x] Decision: Keep in AuroraEngine.Common for backward compatibility
  - [x] Note: Documented as KOTOR-specific, but kept for patcher tools

#### Installation Class (KOTOR-Specific - Kept for Compatibility)

- [x] Installation\Installation.cs → Decision: KEEP in AuroraEngine.Common for now
  - [x] Review: KOTOR-specific (checks for swkotor.exe, swkotor2.exe)
  - [x] Decision: Keep for patcher tools compatibility
  - [x] Future: Create IEngineInstallation interface when other engines are implemented

#### Comprehensive File List (671 files total)

**Purpose**: Complete inventory of all AuroraEngine.Common source files for systematic review and migration tracking.

**Status Tracking**:
- `[ ]` = Not reviewed
- `[/]` = In progress
- `[x]` = Reviewed/Completed
- `[K]` = Keep in AuroraEngine.Common (engine-agnostic or patcher tool dependency)
- `[M]` = Move to Odyssey.Engines.Odyssey (KOTOR-specific)

**Note**: Files are organized by directory. Each file should be reviewed to determine if it's:
1. Engine-agnostic (keep in AuroraEngine.Common)
2. KOTOR/Odyssey-specific (move to Odyssey.Engines.Odyssey)
3. Used by patcher tools (may need to keep in AuroraEngine.Common for compatibility)

**Common**

- [ ] Common\AlienSounds.cs
- [ ] Common\ArrayHead.cs
- [ ] Common\BinaryExtensions.cs
- [ ] Common\BinaryReader.cs
- [ ] Common\BinaryWriter.cs
- [ ] Common\CaseAwarePath.cs
- [ ] Common\Face.cs
- [x] Common\Game.cs → Decision: KEEP in AuroraEngine.Common (patcher tools dependency)
- [ ] Common\GameObject.cs
- [ ] Common\GeometryUtils.cs
- [ ] Common\KeyError.cs
- [ ] Common\Language.cs
- [ ] Common\LocalizedString.cs
- [ ] Common\Misc.cs
- [ ] Common\Module.cs → Review: KOTOR-specific (KModuleType enum)
- [ ] Common\ModuleDataLoader.cs → Review: KOTOR-specific
- [ ] Common\Pathfinding.cs
- [ ] Common\Polygon2.cs
- [ ] Common\Polygon3.cs
- [ ] Common\Quaternion.cs
- [ ] Common\ResRef.cs
- [ ] Common\SurfaceMaterial.cs
- [ ] Common\SystemHelpers.cs

**Common\LZMA**

- [ ] Common\LZMA\LzmaHelper.cs

**Common\Script**

- [ ] Common\Script\DataType.cs
- [ ] Common\Script\DataTypeExtensions.cs
- [ ] Common\Script\NwscriptParser.cs
- [ ] Common\Script\ScriptConstant.cs
- [ ] Common\Script\ScriptDefs.cs
- [ ] Common\Script\ScriptFunction.cs
- [ ] Common\Script\ScriptLib.cs
- [ ] Common\Script\ScriptParam.cs

**Config**

- [ ] Config\LogLevel.cs
- [ ] Config\PatcherConfig.cs

**Diff**

- [ ] Diff\DiffAnalyzerFactory.cs
- [ ] Diff\DiffEngine.cs
- [ ] Diff\DiffHelpers.cs
- [ ] Diff\GffDiff.cs
- [ ] Diff\GffDiffAnalyzer.cs
- [ ] Diff\Resolution.cs
- [ ] Diff\SsfDiff.cs
- [ ] Diff\TlkDiff.cs
- [ ] Diff\TwoDaDiff.cs
- [ ] Diff\TwoDaDiffAnalyzer.cs

**Extract**

- [ ] Extract\BZF.cs
- [ ] Extract\ChitinWrapper.cs
- [ ] Extract\FileResourceHelpers.cs
- [ ] Extract\InstallationWrapper.cs
- [ ] Extract\KeyFileWrapper.cs
- [ ] Extract\KeyWriterWrapper.cs
- [ ] Extract\LazyCapsule.cs
- [ ] Extract\TalkTable.cs
- [ ] Extract\TwoDAManager.cs
- [ ] Extract\TwoDARegistry.cs

**Extract\SaveData**

- [ ] Extract\SaveData\GlobalVars.cs
- [ ] Extract\SaveData\PartyTable.cs
- [ ] Extract\SaveData\SaveFolderEntry.cs
- [ ] Extract\SaveData\SaveInfo.cs
- [ ] Extract\SaveData\SaveNestedCapsule.cs

**Formats**

- [ ] Formats\BinaryFormatReaderBase.cs

**Formats\BWM**

- [ ] Formats\BWM\BWM.cs
- [ ] Formats\BWM\BWMAdjacency.cs
- [ ] Formats\BWM\BWMAuto.cs
- [ ] Formats\BWM\BWMBinaryReader.cs
- [ ] Formats\BWM\BWMBinaryWriter.cs
- [ ] Formats\BWM\BWMEdge.cs
- [ ] Formats\BWM\BWMFace.cs
- [ ] Formats\BWM\BWMMostSignificantPlane.cs
- [ ] Formats\BWM\BWMNodeAABB.cs
- [ ] Formats\BWM\BWMType.cs

**Formats\Capsule**

- [ ] Formats\Capsule\Capsule.cs
- [ ] Formats\Capsule\LazyCapsule.cs

**Formats\Chitin**

- [ ] Formats\Chitin\Chitin.cs

**Formats\ERF**

- [ ] Formats\ERF\ERF.cs
- [ ] Formats\ERF\ERFAuto.cs
- [ ] Formats\ERF\ERFBinaryReader.cs
- [ ] Formats\ERF\ERFBinaryWriter.cs
- [ ] Formats\ERF\ERFType.cs

**Formats\GFF**

- [ ] Formats\GFF\GFF.cs
- [ ] Formats\GFF\GFFAuto.cs
- [ ] Formats\GFF\GFFBinaryReader.cs
- [ ] Formats\GFF\GFFBinaryWriter.cs
- [ ] Formats\GFF\GFFContent.cs
- [ ] Formats\GFF\GFFFieldType.cs
- [ ] Formats\GFF\GFFList.cs
- [ ] Formats\GFF\GFFStruct.cs

**Formats\KEY**

- [ ] Formats\KEY\BifEntry.cs
- [ ] Formats\KEY\KEY.cs
- [ ] Formats\KEY\KEYAuto.cs
- [ ] Formats\KEY\KEYBinaryReader.cs
- [ ] Formats\KEY\KEYBinaryWriter.cs
- [ ] Formats\KEY\KeyEntry.cs

**Formats\LIP**

- [ ] Formats\LIP\LIP.cs
- [ ] Formats\LIP\LIPAuto.cs
- [ ] Formats\LIP\LIPBinaryReader.cs
- [ ] Formats\LIP\LIPBinaryWriter.cs
- [ ] Formats\LIP\LIPKeyFrame.cs
- [ ] Formats\LIP\LIPShape.cs

**Formats\LTR**

- [ ] Formats\LTR\LTR.cs
- [ ] Formats\LTR\LTRAuto.cs
- [ ] Formats\LTR\LTRBinaryReader.cs
- [ ] Formats\LTR\LTRBinaryWriter.cs
- [ ] Formats\LTR\LTRBlock.cs

**Formats\LYT**

- [ ] Formats\LYT\LYT.cs
- [ ] Formats\LYT\LYTAsciiReader.cs
- [ ] Formats\LYT\LYTAsciiWriter.cs
- [ ] Formats\LYT\LYTAuto.cs
- [ ] Formats\LYT\LYTDoorHook.cs
- [ ] Formats\LYT\LYTObstacle.cs
- [ ] Formats\LYT\LYTRoom.cs
- [ ] Formats\LYT\LYTTrack.cs

**Formats\MDL**

- [ ] Formats\MDL\MDLAsciiReader.cs
- [ ] Formats\MDL\MDLAsciiWriter.cs
- [ ] Formats\MDL\MDLAuto.cs
- [ ] Formats\MDL\MDLBinaryReader.cs
- [ ] Formats\MDL\MDLBinaryWriter.cs
- [ ] Formats\MDL\MDLData.cs
- [ ] Formats\MDL\MDLTypes.cs

**Formats\NCS**

- [ ] Formats\NCS\Compilers.cs
- [ ] Formats\NCS\INCSOptimizer.cs
- [ ] Formats\NCS\NCS.cs
- [ ] Formats\NCS\NCSAuto.cs
- [ ] Formats\NCS\NCSBinaryReader.cs
- [ ] Formats\NCS\NCSBinaryWriter.cs
- [ ] Formats\NCS\NCSByteCode.cs
- [ ] Formats\NCS\NCSInstruction.cs
- [ ] Formats\NCS\NCSInstructionQualifier.cs
- [ ] Formats\NCS\NCSInstructionType.cs
- [ ] Formats\NCS\NCSType.cs
- [ ] Formats\NCS\NCSTypeCode.cs

**Formats\NCS\Compiler**

- [ ] Formats\NCS\Compiler\Classes.cs
- [ ] Formats\NCS\Compiler\Interpreter.cs
- [ ] Formats\NCS\Compiler\NCSCompiler.cs
- [ ] Formats\NCS\Compiler\NssCompiler.cs
- [ ] Formats\NCS\Compiler\Stack.cs
- [ ] Formats\NCS\Compiler\StackObject.cs
- [ ] Formats\NCS\Compiler\Statements.cs

**Formats\NCS\Compiler\NSS**

- [ ] Formats\NCS\Compiler\NSS\CompilerExceptions.cs
- [ ] Formats\NCS\Compiler\NSS\NssLanguage.cs
- [ ] Formats\NCS\Compiler\NSS\NssLexer.cs
- [ ] Formats\NCS\Compiler\NSS\NssParser.cs
- [ ] Formats\NCS\Compiler\NSS\NssToken.cs

**Formats\NCS\Compiler\NSS\AST**

- [ ] Formats\NCS\Compiler\NSS\AST\CodeBlock.cs
- [ ] Formats\NCS\Compiler\NSS\AST\CodeRoot.cs
- [ ] Formats\NCS\Compiler\NSS\AST\ControlKeyword.cs
- [ ] Formats\NCS\Compiler\NSS\AST\DynamicDataType.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\FieldAccess.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Identifier.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Operator.cs
- [ ] Formats\NCS\Compiler\NSS\AST\OperatorMapping.cs
- [ ] Formats\NCS\Compiler\NSS\AST\OperatorMappings.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\TopLevelObject.cs
- [ ] Formats\NCS\Compiler\NSS\AST\TopLevelObjects.cs

**Formats\NCS\Compiler\NSS\AST\Expressions**

- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\AdditionAssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\AssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\BinaryOperatorExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\BitwiseAndAssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\BitwiseLeftAssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\BitwiseNotExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\BitwiseOrAssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\BitwiseRightAssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\BitwiseUnsignedRightAssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\BitwiseXorAssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\DivisionAssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\EngineCallExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\FloatExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\FunctionCallExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\IdentifierExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\IntExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\LogicalNotExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\ModuloAssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\MultiplicationAssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\ObjectExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\PostDecrementExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\PostIncrementExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\PreDecrementExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\PreIncrementExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\StringExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\SubtractionAssignmentExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\TernaryConditionalExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\UnaryOperatorExpression.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Expressions\VectorExpression.cs

**Formats\NCS\Compiler\NSS\AST\Statements**

- [ ] Formats\NCS\Compiler\NSS\AST\Statements\BreakStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\ContinueStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\DeclarationStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\DoWhileStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\EmptyStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\ExpressionStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\ForStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\IfStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\NopStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\ReturnStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\ScopedBlockStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\SwitchStatement.cs
- [ ] Formats\NCS\Compiler\NSS\AST\Statements\WhileStatement.cs

**Formats\NCS\NCSDecomp** (Note: Large directory with 100+ files - engine-agnostic decompiler)

- [ ] Formats\NCS\NCSDecomp\AActionJumpCmd.cs
- [ ] Formats\NCS\NCSDecomp\AAddVarCmd.cs
- [ ] Formats\NCS\NCSDecomp\ABitAndLogiiOp.cs
- [ ] Formats\NCS\NCSDecomp\ActionsData.cs
- [ ] Formats\NCS\NCSDecomp\ADecibpStackOp.cs
- [ ] Formats\NCS\NCSDecomp\ADecispStackOp.cs
- [ ] Formats\NCS\NCSDecomp\AExpression.cs
- [ ] Formats\NCS\NCSDecomp\AIncibpStackOp.cs
- [ ] Formats\NCS\NCSDecomp\AIncispStackOp.cs
- [ ] Formats\NCS\NCSDecomp\Analysis.cs
- [ ] Formats\NCS\NCSDecomp\AnalysisAdapter.cs
- [ ] Formats\NCS\NCSDecomp\AStackCommand.cs
- [ ] Formats\NCS\NCSDecomp\AStackOpCmd.cs
- [ ] Formats\NCS\NCSDecomp\Cast.cs
- [ ] Formats\NCS\NCSDecomp\CheckIsGlobals.cs
- [ ] Formats\NCS\NCSDecomp\CleanupPass.cs
- [ ] Formats\NCS\NCSDecomp\Cloneable.cs
- [ ] Formats\NCS\NCSDecomp\Collection.cs
- [ ] Formats\NCS\NCSDecomp\CompilerExecutionWrapper.cs
- [ ] Formats\NCS\NCSDecomp\CompilerUtil.cs
- [ ] Formats\NCS\NCSDecomp\Const.cs
- [ ] Formats\NCS\NCSDecomp\Decoder.cs
- [ ] Formats\NCS\NCSDecomp\Decompiler.cs
- [ ] Formats\NCS\NCSDecomp\DecompilerException.cs
- [ ] Formats\NCS\NCSDecomp\DestroyParseTree.cs
- [ ] Formats\NCS\NCSDecomp\DoGlobalVars.cs
- [ ] Formats\NCS\NCSDecomp\DoTypes.cs
- [ ] Formats\NCS\NCSDecomp\FileDecompiler.cs
- [ ] Formats\NCS\NCSDecomp\FlattenSub.cs
- [ ] Formats\NCS\NCSDecomp\FloatConst.cs
- [ ] Formats\NCS\NCSDecomp\HashUtil.cs
- [ ] Formats\NCS\NCSDecomp\IEnumerator.cs
- [ ] Formats\NCS\NCSDecomp\IntConst.cs
- [ ] Formats\NCS\NCSDecomp\JavaStubs.cs
- [ ] Formats\NCS\NCSDecomp\KnownExternalCompilers.cs
- [ ] Formats\NCS\NCSDecomp\Lexer.cs
- [ ] Formats\NCS\NCSDecomp\LexerException.cs
- [ ] Formats\NCS\NCSDecomp\LinkedList.cs
- [ ] Formats\NCS\NCSDecomp\LinkedListExtensions.cs
- [ ] Formats\NCS\NCSDecomp\ListIterator.cs
- [ ] Formats\NCS\NCSDecomp\LocalStack.cs
- [ ] Formats\NCS\NCSDecomp\LocalTypeStack.cs
- [ ] Formats\NCS\NCSDecomp\LocalVarStack.cs
- [ ] Formats\NCS\NCSDecomp\MainPass.cs
- [ ] Formats\NCS\NCSDecomp\NameGenerator.cs
- [ ] Formats\NCS\NCSDecomp\NoCast.cs
- [ ] Formats\NCS\NCSDecomp\Node.cs
- [ ] Formats\NCS\NCSDecomp\NodeAnalysisData.cs
- [ ] Formats\NCS\NCSDecomp\NodeCast.cs
- [ ] Formats\NCS\NCSDecomp\NodeUtils.cs
- [ ] Formats\NCS\NCSDecomp\NoOpRegistrySpoofer.cs
- [ ] Formats\NCS\NCSDecomp\NwnnsscompConfig.cs
- [ ] Formats\NCS\NCSDecomp\NWScriptLocator.cs
- [ ] Formats\NCS\NCSDecomp\ObjectConst.cs
- [ ] Formats\NCS\NCSDecomp\Parser.cs
- [ ] Formats\NCS\NCSDecomp\ParserException.cs
- [ ] Formats\NCS\NCSDecomp\PcodeReader.cs
- [ ] Formats\NCS\NCSDecomp\PcodeReaderTest.cs
- [ ] Formats\NCS\NCSDecomp\PrunedDepthFirstAdapter.cs
- [ ] Formats\NCS\NCSDecomp\PrunedReversedDepthFirstAdapter.cs
- [ ] Formats\NCS\NCSDecomp\PStackCommand.cs
- [ ] Formats\NCS\NCSDecomp\PStackOp.cs
- [ ] Formats\NCS\NCSDecomp\PushbackReader.cs
- [ ] Formats\NCS\NCSDecomp\RegistrySpoofer.cs
- [ ] Formats\NCS\NCSDecomp\RoundTripUtil.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode.cs
- [ ] Formats\NCS\NCSDecomp\ScriptRootNode.cs
- [ ] Formats\NCS\NCSDecomp\SetDeadCode.cs
- [ ] Formats\NCS\NCSDecomp\SetDestinations.cs
- [ ] Formats\NCS\NCSDecomp\SetPositions.cs
- [ ] Formats\NCS\NCSDecomp\Settings.cs
- [ ] Formats\NCS\NCSDecomp\StackEntry.cs
- [ ] Formats\NCS\NCSDecomp\State.cs
- [ ] Formats\NCS\NCSDecomp\StringConst.cs
- [ ] Formats\NCS\NCSDecomp\StructType.cs
- [ ] Formats\NCS\NCSDecomp\SubroutinePathFinder.cs
- [ ] Formats\NCS\NCSDecomp\SubroutineState.cs
- [ ] Formats\NCS\NCSDecomp\SubScriptState.cs
- [ ] Formats\NCS\NCSDecomp\Switch.cs
- [ ] Formats\NCS\NCSDecomp\Switchable.cs
- [ ] Formats\NCS\NCSDecomp\TBlank.cs
- [ ] Formats\NCS\NCSDecomp\TDecibp.cs
- [ ] Formats\NCS\NCSDecomp\TDecisp.cs
- [ ] Formats\NCS\NCSDecomp\TDot.cs
- [ ] Formats\NCS\NCSDecomp\TIncibp.cs
- [ ] Formats\NCS\NCSDecomp\TIncisp.cs
- [ ] Formats\NCS\NCSDecomp\TLPar.cs
- [ ] Formats\NCS\NCSDecomp\TNop.cs
- [ ] Formats\NCS\NCSDecomp\TokenIndex.cs
- [ ] Formats\NCS\NCSDecomp\TreeModelFactory.cs
- [ ] Formats\NCS\NCSDecomp\TRPar.cs
- [ ] Formats\NCS\NCSDecomp\Type.cs
- [ ] Formats\NCS\NCSDecomp\TypedLinkedList.cs
- [ ] Formats\NCS\NCSDecomp\Variable.cs
- [ ] Formats\NCS\NCSDecomp\VarStruct.cs
- [ ] Formats\NCS\NCSDecomp\X1PCmd.cs
- [ ] Formats\NCS\NCSDecomp\X1PSubroutine.cs
- [ ] Formats\NCS\NCSDecomp\X2PCmd.cs
- [ ] Formats\NCS\NCSDecomp\X2PSubroutine.cs
- [ ] Formats\NCS\NCSDecomp\XPCmd.cs
- [ ] Formats\NCS\NCSDecomp\XPSubroutine.cs

**Formats\NCS\NCSDecomp\Analysis**

- [ ] Formats\NCS\NCSDecomp\Analysis\CallGraphBuilder.cs
- [ ] Formats\NCS\NCSDecomp\Analysis\CallSiteAnalyzer.cs
- [ ] Formats\NCS\NCSDecomp\Analysis\PrototypeEngine.cs
- [ ] Formats\NCS\NCSDecomp\Analysis\SCCUtil.cs

**Formats\NCS\NCSDecomp\Node** (Note: Large directory with 100+ files)

- [ ] Formats\NCS\NCSDecomp\Node\AActionCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\AActionCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\AAddBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AAndLogiiOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\ABinaryCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\ABinaryCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\ABoolandLogiiOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\ABpCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\ABpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\ACommandBlock.cs
- [ ] Formats\NCS\NCSDecomp\Node\ACompUnaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AConditionalJumpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\ACondJumpCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\AConstCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\AConstCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\ACopydownbpCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\ACopyDownBpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\ACopydownspCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\ACopyDownSpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\ACopytopbpCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\ACopyTopBpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\ACopytopspCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\ACopyTopSpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\ADestructCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\ADestructCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\ADivBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AEqualBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AExclOrLogiiOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AFloatConstant.cs
- [ ] Formats\NCS\NCSDecomp\Node\AGeqBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AGtBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AInclOrLogiiOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AIntConstant.cs
- [ ] Formats\NCS\NCSDecomp\Node\AJumpCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\AJumpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\AJumpSubCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\AJumpToSubroutine.cs
- [ ] Formats\NCS\NCSDecomp\Node\ALeqBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\ALogiiCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\ALogiiCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\ALtBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AModBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AMovespCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\AMoveSpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\AMulBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\ANegUnaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\ANequalBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\ANonzeroJumpIf.cs
- [ ] Formats\NCS\NCSDecomp\Node\ANotUnaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AOrLogiiOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AProgram.cs
- [ ] Formats\NCS\NCSDecomp\Node\ARestorebpBpOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AReturn.cs
- [ ] Formats\NCS\NCSDecomp\Node\AReturnCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\ARsaddCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\ARsaddCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\ASavebpBpOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AShleftBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AShrightBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\ASize.cs
- [ ] Formats\NCS\NCSDecomp\Node\AStoreStateCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\AStoreStateCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\AStringConstant.cs
- [ ] Formats\NCS\NCSDecomp\Node\ASubBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\ASubroutine.cs
- [ ] Formats\NCS\NCSDecomp\Node\AUnaryCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\AUnaryCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\AUnrightBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\AZeroJumpIf.cs
- [ ] Formats\NCS\NCSDecomp\Node\EOF.cs
- [ ] Formats\NCS\NCSDecomp\Node\PActionCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PBinaryCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PBinaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\PBpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PBpOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\PCmd.cs
- [ ] Formats\NCS\NCSDecomp\Node\PCommandBlock.cs
- [ ] Formats\NCS\NCSDecomp\Node\PConditionalJumpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PConstant.cs
- [ ] Formats\NCS\NCSDecomp\Node\PConstCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PCopyDownBpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PCopyDownSpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PCopyTopBpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PCopyTopSpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PDestructCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PJumpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PJumpIf.cs
- [ ] Formats\NCS\NCSDecomp\Node\PJumpToSubroutine.cs
- [ ] Formats\NCS\NCSDecomp\Node\PLogiiCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PLogiiOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\PMoveSpCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PProgram.cs
- [ ] Formats\NCS\NCSDecomp\Node\PReturn.cs
- [ ] Formats\NCS\NCSDecomp\Node\PRsaddCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PSize.cs
- [ ] Formats\NCS\NCSDecomp\Node\PStoreStateCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PSubroutine.cs
- [ ] Formats\NCS\NCSDecomp\Node\PUnaryCommand.cs
- [ ] Formats\NCS\NCSDecomp\Node\PUnaryOp.cs
- [ ] Formats\NCS\NCSDecomp\Node\Start.cs
- [ ] Formats\NCS\NCSDecomp\Node\TAction.cs
- [ ] Formats\NCS\NCSDecomp\Node\TAdd.cs
- [ ] Formats\NCS\NCSDecomp\Node\TBoolandii.cs
- [ ] Formats\NCS\NCSDecomp\Node\TComp.cs
- [ ] Formats\NCS\NCSDecomp\Node\TConst.cs
- [ ] Formats\NCS\NCSDecomp\Node\TCpdownbp.cs
- [ ] Formats\NCS\NCSDecomp\Node\TCpdownsp.cs
- [ ] Formats\NCS\NCSDecomp\Node\TCptopbp.cs
- [ ] Formats\NCS\NCSDecomp\Node\TCptopsp.cs
- [ ] Formats\NCS\NCSDecomp\Node\TDestruct.cs
- [ ] Formats\NCS\NCSDecomp\Node\TDiv.cs
- [ ] Formats\NCS\NCSDecomp\Node\TEqual.cs
- [ ] Formats\NCS\NCSDecomp\Node\TExcorii.cs
- [ ] Formats\NCS\NCSDecomp\Node\TFloatConstant.cs
- [ ] Formats\NCS\NCSDecomp\Node\TGeq.cs
- [ ] Formats\NCS\NCSDecomp\Node\TGt.cs
- [ ] Formats\NCS\NCSDecomp\Node\TIncorii.cs
- [ ] Formats\NCS\NCSDecomp\Node\TIntegerConstant.cs
- [ ] Formats\NCS\NCSDecomp\Node\TJmp.cs
- [ ] Formats\NCS\NCSDecomp\Node\TJnz.cs
- [ ] Formats\NCS\NCSDecomp\Node\TJsr.cs
- [ ] Formats\NCS\NCSDecomp\Node\TJz.cs
- [ ] Formats\NCS\NCSDecomp\Node\TLeq.cs
- [ ] Formats\NCS\NCSDecomp\Node\TLogandii.cs
- [ ] Formats\NCS\NCSDecomp\Node\TLogorii.cs
- [ ] Formats\NCS\NCSDecomp\Node\TLt.cs
- [ ] Formats\NCS\NCSDecomp\Node\TMod.cs
- [ ] Formats\NCS\NCSDecomp\Node\TMovsp.cs
- [ ] Formats\NCS\NCSDecomp\Node\TMul.cs
- [ ] Formats\NCS\NCSDecomp\Node\TNeg.cs
- [ ] Formats\NCS\NCSDecomp\Node\TNequal.cs
- [ ] Formats\NCS\NCSDecomp\Node\TNot.cs
- [ ] Formats\NCS\NCSDecomp\Node\Token.cs
- [ ] Formats\NCS\NCSDecomp\Node\TRestorebp.cs
- [ ] Formats\NCS\NCSDecomp\Node\TRetn.cs
- [ ] Formats\NCS\NCSDecomp\Node\TRsadd.cs
- [ ] Formats\NCS\NCSDecomp\Node\TSavebp.cs
- [ ] Formats\NCS\NCSDecomp\Node\TSemi.cs
- [ ] Formats\NCS\NCSDecomp\Node\TShleft.cs
- [ ] Formats\NCS\NCSDecomp\Node\TShright.cs
- [ ] Formats\NCS\NCSDecomp\Node\TStorestate.cs
- [ ] Formats\NCS\NCSDecomp\Node\TStringLiteral.cs
- [ ] Formats\NCS\NCSDecomp\Node\TSub.cs
- [ ] Formats\NCS\NCSDecomp\Node\TT.cs
- [ ] Formats\NCS\NCSDecomp\Node\TUnright.cs

**Formats\NCS\NCSDecomp\ScriptNode**

- [ ] Formats\NCS\NCSDecomp\ScriptNode\AActionArgExp.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AActionExp.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\ABinaryExp.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\ABreakStatement.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\ACodeBlock.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AConditionalExp.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AConst.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AContinueStatement.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AControlLoop.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\ADoLoop.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AElse.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AErrorComment.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AExpressionStatement.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AFcnCallExp.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AFor.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AIf.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AModifyExp.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AReturnStatement.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\ASub.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\ASwitch.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\ASwitchCase.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AUnaryExp.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AUnaryModExp.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AUnkLoopControl.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AVarDecl.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AVarRef.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AVectorConstExp.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\AWhileLoop.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\ExpressionFormatter.cs
- [ ] Formats\NCS\NCSDecomp\ScriptNode\ScriptNode.cs

**Formats\NCS\NCSDecomp\SyntaxHighlighting**

- [ ] Formats\NCS\NCSDecomp\SyntaxHighlighting\BytecodeSyntaxHighlighter.cs
- [ ] Formats\NCS\NCSDecomp\SyntaxHighlighting\NWScriptSyntaxHighlighter.cs

**Formats\NCS\NCSDecomp\Utils**

- [ ] Formats\NCS\NCSDecomp\Utils\FileScriptData.cs
- [ ] Formats\NCS\NCSDecomp\Utils\NcsToAstConverter.cs
- [ ] Formats\NCS\NCSDecomp\Utils\SubroutineAnalysisData.cs
- [ ] Formats\NCS\NCSDecomp\Utils\SubroutineIterator.cs

**Formats\NCS\Optimizers**

- [ ] Formats\NCS\Optimizers\MergeAdjacentMoveSPOptimizer.cs
- [ ] Formats\NCS\Optimizers\RemoveJMPToAdjacentOptimizer.cs
- [ ] Formats\NCS\Optimizers\RemoveMoveSPEqualsZeroOptimizer.cs
- [ ] Formats\NCS\Optimizers\RemoveNopOptimizer.cs
- [ ] Formats\NCS\Optimizers\RemoveUnusedBlocksOptimizer.cs
- [ ] Formats\NCS\Optimizers\RemoveUnusedGlobalsInStackOptimizer.cs

**Formats\RIM**

- [ ] Formats\RIM\RIM.cs
- [ ] Formats\RIM\RIMAuto.cs
- [ ] Formats\RIM\RIMBinaryReader.cs
- [ ] Formats\RIM\RIMBinaryWriter.cs

**Formats\SSF**

- [ ] Formats\SSF\SSF.cs
- [ ] Formats\SSF\SSFAuto.cs
- [ ] Formats\SSF\SSFBinaryReader.cs
- [ ] Formats\SSF\SSFBinaryWriter.cs
- [ ] Formats\SSF\SSFSound.cs

**Formats\TLK**

- [ ] Formats\TLK\TalkTable.cs
- [ ] Formats\TLK\TLK.cs
- [ ] Formats\TLK\TLKAuto.cs
- [ ] Formats\TLK\TLKBinaryReader.cs
- [ ] Formats\TLK\TLKBinaryWriter.cs
- [ ] Formats\TLK\TLKEntry.cs

**Formats\TPC**

- [ ] Formats\TPC\TGA.cs
- [ ] Formats\TPC\TPC.cs
- [ ] Formats\TPC\TPCAuto.cs
- [ ] Formats\TPC\TPCBinaryReader.cs
- [ ] Formats\TPC\TPCBinaryWriter.cs
- [ ] Formats\TPC\TPCDDSReader.cs
- [ ] Formats\TPC\TPCDDSWriter.cs
- [ ] Formats\TPC\TPCLayer.cs
- [ ] Formats\TPC\TPCMipmap.cs
- [ ] Formats\TPC\TPCTextureFormat.cs
- [ ] Formats\TPC\TPCTGAReader.cs
- [ ] Formats\TPC\TPCTGAWriter.cs

**Formats\TwoDA**

- [ ] Formats\TwoDA\TwoDA.cs
- [ ] Formats\TwoDA\TwoDAAuto.cs
- [ ] Formats\TwoDA\TwoDABinaryReader.cs
- [ ] Formats\TwoDA\TwoDABinaryWriter.cs
- [ ] Formats\TwoDA\TwoDARow.cs

**Formats\TXI**

- [ ] Formats\TXI\TXI.cs
- [ ] Formats\TXI\TXIAuto.cs
- [ ] Formats\TXI\TXIBinaryReader.cs
- [ ] Formats\TXI\TXIBinaryWriter.cs
- [ ] Formats\TXI\TXICommand.cs
- [ ] Formats\TXI\TXIFeatures.cs
- [ ] Formats\TXI\TXIReaderMode.cs

**Formats\VIS**

- [ ] Formats\VIS\VIS.cs
- [ ] Formats\VIS\VISAsciiReader.cs
- [ ] Formats\VIS\VISAsciiWriter.cs
- [ ] Formats\VIS\VISAuto.cs

**Formats\WAV**

- [ ] Formats\WAV\AudioFormat.cs
- [ ] Formats\WAV\DeobfuscationResult.cs
- [ ] Formats\WAV\WAV.cs
- [ ] Formats\WAV\WAVAuto.cs
- [ ] Formats\WAV\WAVBinaryReader.cs
- [ ] Formats\WAV\WAVBinaryWriter.cs
- [ ] Formats\WAV\WaveEncoding.cs
- [ ] Formats\WAV\WAVObfuscation.cs
- [ ] Formats\WAV\WAVStandardWriter.cs
- [ ] Formats\WAV\WAVType.cs

**Installation**

- [x] Installation\Installation.cs → Decision: KEEP in AuroraEngine.Common (patcher tools dependency)
- [ ] Installation\InstallationResourceManager.cs
- [ ] Installation\ResourceResult.cs
- [ ] Installation\SearchLocation.cs

**Logger**

- [ ] Logger\InstallLogWriter.cs
- [ ] Logger\LogType.cs
- [ ] Logger\PatchLog.cs
- [ ] Logger\PatchLogger.cs
- [ ] Logger\RobustLogger.cs

**Memory**

- [ ] Memory\PatcherMemory.cs
- [ ] Memory\TokenUsage.cs

**Merge**

- [ ] Merge\ModuleManager.cs

**Mods**

- [ ] Mods\InstallFile.cs
- [ ] Mods\ModificationsByType.cs
- [ ] Mods\PatcherModifications.cs
- [ ] Mods\TSLPatcherINISerializer.cs

**Mods\GFF**

- [ ] Mods\GFF\FieldValue.cs
- [ ] Mods\GFF\ModificationsGFF.cs
- [ ] Mods\GFF\ModifyGFF.cs

**Mods\NCS**

- [ ] Mods\NCS\ModificationsNCS.cs

**Mods\NSS**

- [ ] Mods\NSS\ModificationsNSS.cs

**Mods\SSF**

- [ ] Mods\SSF\ModificationsSSF.cs

**Mods\TLK**

- [ ] Mods\TLK\ModificationsTLK.cs

**Mods\TwoDA**

- [ ] Mods\TwoDA\Modifications2DA.cs
- [ ] Mods\TwoDA\Modify2DA.cs
- [ ] Mods\TwoDA\RowValue.cs
- [ ] Mods\TwoDA\Target.cs

**Namespaces**

- [ ] Namespaces\PatcherNamespace.cs

**Patcher**

- [ ] Patcher\ModInstaller.cs

**Reader**

- [ ] Reader\ConfigReader.cs
- [ ] Reader\NamespaceReader.cs

**Resource\Formats\BIF**

- [ ] Resource\Formats\BIF\BIF.cs
- [ ] Resource\Formats\BIF\BIFBinaryReader.cs
- [ ] Resource\Formats\BIF\BIFBinaryWriter.cs
- [ ] Resource\Formats\BIF\BIFResource.cs
- [ ] Resource\Formats\BIF\BIFType.cs

**Resource\Formats\LYT**

- [ ] Resource\Formats\LYT\LYT.cs

**Resource\Formats\VIS**

- [ ] Resource\Formats\VIS\VIS.cs

**Resource\Generics**

- [ ] Resource\Generics\ARE.cs → Review: KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\AREHelpers.cs
- [ ] Resource\Generics\GIT.cs → Review: KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\GITHelpers.cs
- [ ] Resource\Generics\IFO.cs → Review: KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\IFOHelpers.cs
- [ ] Resource\Generics\JRL.cs → Review: KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\PTH.cs → Review: KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\PTHAuto.cs
- [ ] Resource\Generics\PTHHelpers.cs
- [x] Resource\Generics\UTC.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTCHelpers.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTD.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTDHelpers.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTE.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTEHelpers.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTI.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTIHelpers.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTM.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTMHelpers.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTP.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTPHelpers.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTS.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTSHelpers.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTT.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTTAuto.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTTHelpers.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTW.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTWAuto.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)
- [x] Resource\Generics\UTWHelpers.cs → Moved to Odyssey.Engines.Odyssey.Templates (kept for compatibility)

**Resource\Generics\DLG**

- [ ] Resource\Generics\DLG\DLG.cs → Review: KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\DLG\DLGAnimation.cs
- [ ] Resource\Generics\DLG\DLGHelper.cs
- [ ] Resource\Generics\DLG\DLGLink.cs
- [ ] Resource\Generics\DLG\DLGNode.cs
- [ ] Resource\Generics\DLG\DLGStunt.cs

**Resource\Generics\GUI**

- [ ] Resource\Generics\GUI\GUI.cs → Review: KOTOR-specific or engine-agnostic
- [ ] Resource\Generics\GUI\GUIBorder.cs
- [ ] Resource\Generics\GUI\GUIControl.cs
- [ ] Resource\Generics\GUI\GUIEnums.cs
- [ ] Resource\Generics\GUI\GUIMoveTo.cs
- [ ] Resource\Generics\GUI\GUIProgress.cs
- [ ] Resource\Generics\GUI\GUIReader.cs
- [ ] Resource\Generics\GUI\GUIScrollbar.cs
- [ ] Resource\Generics\GUI\GUIText.cs

**Resources**

- [ ] Resources\ArchiveResource.cs
- [ ] Resources\FileResource.cs
- [ ] Resources\ResourceAuto.cs
- [ ] Resources\ResourceAutoHelpers.cs
- [ ] Resources\ResourceFormat.cs
- [ ] Resources\ResourceIdentifier.cs
- [ ] Resources\ResourceType.cs
- [ ] Resources\Salvage.cs

**Tools**

- [ ] Tools\Archives.cs
- [ ] Tools\Conversions.cs
- [ ] Tools\Creature.cs
- [ ] Tools\Door.cs
- [ ] Tools\Encoding.cs
- [ ] Tools\Heuristics.cs
- [ ] Tools\Kit.cs
- [ ] Tools\Misc.cs
- [ ] Tools\Model.cs
- [ ] Tools\Module.cs
- [ ] Tools\Patching.cs
- [ ] Tools\Path.cs
- [ ] Tools\PazaakGui.cs
- [ ] Tools\Placeable.cs
- [ ] Tools\PlayPazaak.cs
- [ ] Tools\ReferenceCache.cs
- [ ] Tools\Registry.cs
- [ ] Tools\ResourceConversions.cs
- [ ] Tools\Scripts.cs
- [ ] Tools\StringUtils.cs
- [ ] Tools\Template.cs
- [ ] Tools\Utilities.cs
- [ ] Tools\Validation.cs

**TSLPatcher**

- [ ] TSLPatcher\GeneratorValidation.cs
- [ ] TSLPatcher\IncrementalTSLPatchDataWriter.cs
- [ ] TSLPatcher\INIManager.cs
- [ ] TSLPatcher\InstallFolderDeterminer.cs
- [ ] TSLPatcher\TSLPatchDataGenerator.cs

**Uninstall**

- [ ] Uninstall\ModUninstaller.cs
- [ ] Uninstall\UninstallHelpers.cs

**Utility**

- [ ] Utility\CaseInsensitiveDict.cs
- [ ] Utility\ErrorHandling.cs
- [ ] Utility\Misc.cs
- [ ] Utility\OrderedSet.cs

**Utility\MiscString**

- [ ] Utility\MiscString\CaseInsensImmutableStr.cs
- [ ] Utility\MiscString\StringUtilFunctions.cs
- [ ] Utility\MiscString\WrappedStr.cs

**Utility\System**

- [ ] Utility\System\OSHelper.cs

### Base Class Maximization Strategy

Following xoreos pattern, maximize code in base classes:

#### Odyssey.Engines.Common (Base Classes)

- [x] IEngine.cs - Core engine interface
- [x] IEngineGame.cs - Game session interface
- [x] IEngineModule.cs - Module management interface
- [x] IEngineProfile.cs - Engine profile interface
- [x] BaseEngine.cs - Base engine implementation (maximize shared logic here)
- [x] BaseEngineGame.cs - Base game session (maximize shared logic here)
- [x] BaseEngineModule.cs - Base module loader (maximize shared logic here)
- [x] BaseEngineProfile.cs - Base profile (maximize shared logic here)

**Goal**: All common engine logic (initialization, shutdown, resource management, world management) should be in base classes. Engine-specific projects should only contain what differs.

#### Odyssey.Engines.Odyssey (KOTOR-Specific)

- [x] OdysseyEngine.cs - Inherits from BaseEngine, implements KOTOR-specific initialization
- [x] OdysseyGameSession.cs - Inherits from BaseEngineGame, implements KOTOR-specific game logic
- [x] OdysseyModuleLoader.cs - Inherits from BaseEngineModule, implements KOTOR-specific module loading
- [x] Profiles\OdysseyK1GameProfile.cs - Inherits from BaseEngineProfile
- [x] Profiles\OdysseyK2GameProfile.cs - Inherits from BaseEngineProfile
- [x] EngineApi\OdysseyK1EngineApi.cs - KOTOR 1 NWScript API implementation
- [x] EngineApi\OdysseyK2EngineApi.cs - KOTOR 2 NWScript API implementation
- [x] Templates\ - All 9 GFF templates ✅

### Progress Summary

**Completed**:

- ✅ All 9 GFF templates migrated to Odyssey.Engines.Odyssey.Templates
- ✅ Base engine abstraction layer created (Odyssey.Engines.Common)
- ✅ Odyssey.Engines.Odyssey project structure created
- ✅ OdysseyEngine, OdysseyGameSession, OdysseyModuleLoader created
- ✅ Profiles and EngineApi classes migrated

**In Progress**:

- [ ] Module.cs migration (deferred due to patcher tool dependencies)
- [ ] Review IFO/ARE/GIT/JRL/PTH structures
- [ ] Review DLG structures
- [ ] Review GUI structures

**Remaining**:

- [ ] Complete Module.cs migration when patcher dependencies resolved
- [ ] Review and migrate module/area structures
- [ ] Review and migrate dialogue structures
- [ ] Review and migrate GUI structures
- [ ] Update all references to use new namespaces
- [ ] Verify compilation and fix issues
- [ ] Add Ghidra documentation to all migrated code

## Update Instructions

When processing a file:

- Mark as `- [/]` when starting work
- Mark as `- [x]` when complete with Ghidra references added
- Add notes about function addresses, string references, and implementation details
- Use format: `- [x] FileName.cs - Function addresses, string references, key findings`

## Refactoring Strategy

1. Search Ghidra for relevant functions using string searches and function name searches
2. Decompile relevant functions to understand original implementation
3. Add detailed comments with Ghidra function addresses and context
4. Update implementation to match original behavior where possible
5. Document any deviations or improvements

## Files to Process

### Odyssey.Content (18 files)

- [ ] Cache\ContentCache.cs
- [ ] Converters\BwmToNavigationMeshConverter.cs
- [ ] Interfaces\IContentCache.cs
- [ ] Interfaces\IContentConverter.cs
- [ ] Interfaces\IGameResourceProvider.cs
- [ ] Interfaces\IResourceProvider.cs
- [ ] Loaders\GITLoader.cs
- [ ] Loaders\TemplateLoader.cs
- [ ] MDL\MDLBulkReader.cs
- [ ] MDL\MDLCache.cs
- [ ] MDL\MDLConstants.cs
- [ ] MDL\MDLDataTypes.cs
- [ ] MDL\MDLFastReader.cs
- [ ] MDL\MDLLoader.cs
- [ ] MDL\MDLOptimizedReader.cs
- [ ] ResourceProviders\GameResourceProvider.cs
- [ ] Save\SaveDataProvider.cs
- [ ] Save\SaveSerializer.cs

### Odyssey.Core (99 files)

- [ ] Actions\ActionAttack.cs
- [ ] Actions\ActionBase.cs
- [ ] Actions\ActionCastSpellAtLocation.cs
- [ ] Actions\ActionCastSpellAtObject.cs
- [ ] Actions\ActionCloseDoor.cs
- [ ] Actions\ActionDestroyObject.cs
- [ ] Actions\ActionDoCommand.cs
- [ ] Actions\ActionEquipItem.cs
- [ ] Actions\ActionFollowObject.cs
- [ ] Actions\ActionJumpToLocation.cs
- [ ] Actions\ActionJumpToObject.cs
- [ ] Actions\ActionMoveAwayFromObject.cs
- [ ] Actions\ActionMoveToLocation.cs
- [ ] Actions\ActionMoveToObject.cs
- [ ] Actions\ActionOpenDoor.cs
- [ ] Actions\ActionPickUpItem.cs
- [ ] Actions\ActionPlayAnimation.cs
- [ ] Actions\ActionPutDownItem.cs
- [ ] Actions\ActionQueue.cs
- [ ] Actions\ActionRandomWalk.cs
- [ ] Actions\ActionSpeakString.cs
- [ ] Actions\ActionUnequipItem.cs
- [ ] Actions\ActionUseItem.cs
- [ ] Actions\ActionUseObject.cs
- [ ] Actions\ActionWait.cs
- [ ] Actions\DelayScheduler.cs
- [ ] AI\AIController.cs
- [ ] Animation\AnimationSystem.cs
- [ ] Audio\ISoundPlayer.cs
- [ ] Camera\CameraController.cs
- [ ] Combat\CombatSystem.cs
- [ ] Combat\CombatTypes.cs
- [ ] Combat\EffectSystem.cs
- [ ] Dialogue\DialogueInterfaces.cs
- [ ] Dialogue\DialogueSystem.cs
- [ ] Dialogue\LipSyncController.cs
- [ ] Dialogue\RuntimeDialogue.cs
- [ ] Entities\Entity.cs
- [ ] Entities\EventBus.cs
- [ ] Entities\TimeManager.cs
- [ ] Entities\World.cs
- [ ] Enums\Ability.cs
- [ ] Enums\ActionStatus.cs
- [ ] Enums\ActionType.cs
- [ ] Enums\ObjectType.cs
- [ ] Enums\ScriptEvent.cs
- [ ] GameLoop\FixedTimestepGameLoop.cs
- [ ] GameSettings.cs
- [ ] Interfaces\Components\IActionQueueComponent.cs
- [ ] Interfaces\Components\IAnimationComponent.cs
- [ ] Interfaces\Components\IDoorComponent.cs
- [ ] Interfaces\Components\IFactionComponent.cs
- [ ] Interfaces\Components\IInventoryComponent.cs
- [ ] Interfaces\Components\IItemComponent.cs
- [ ] Interfaces\Components\IPerceptionComponent.cs
- [ ] Interfaces\Components\IPlaceableComponent.cs
- [ ] Interfaces\Components\IQuickSlotComponent.cs
- [ ] Interfaces\Components\IRenderableComponent.cs
- [ ] Interfaces\Components\IScriptHooksComponent.cs
- [ ] Interfaces\Components\IStatsComponent.cs
- [ ] Interfaces\Components\ITransformComponent.cs
- [ ] Interfaces\Components\ITriggerComponent.cs
- [ ] Interfaces\IAction.cs
- [ ] Interfaces\IActionQueue.cs
- [ ] Interfaces\IArea.cs
- [ ] Interfaces\IComponent.cs
- [ ] Interfaces\IDelayScheduler.cs
- [ ] Interfaces\IEntity.cs
- [ ] Interfaces\IEventBus.cs
- [ ] Interfaces\IGameServicesContext.cs
- [ ] Interfaces\IModule.cs
- [ ] Interfaces\INavigationMesh.cs
- [ ] Interfaces\ITimeManager.cs
- [ ] Interfaces\IWorld.cs
- [ ] Journal\JournalSystem.cs
- [ ] Module\ModuleTransitionSystem.cs
- [ ] Module\RuntimeArea.cs
- [ ] Module\RuntimeModule.cs
- [ ] Movement\CharacterController.cs
- [ ] Movement\PlayerInputHandler.cs
- [ ] Navigation\NavigationMesh.cs
- [ ] Navigation\NavigationMeshFactory.cs
- [ ] Party\PartyInventory.cs
- [ ] Party\PartyMember.cs
- [ ] Party\PartySystem.cs
- [ ] Perception\PerceptionSystem.cs
- [ ] Save\AreaState.cs
- [ ] Save\SaveGameData.cs
- [ ] Save\SaveSystem.cs
- [ ] Templates\CreatureTemplate.cs
- [ ] Templates\DoorTemplate.cs
- [ ] Templates\EncounterTemplate.cs
- [ ] Templates\IEntityTemplate.cs
- [ ] Templates\PlaceableTemplate.cs
- [ ] Templates\SoundTemplate.cs
- [ ] Templates\StoreTemplate.cs
- [ ] Templates\TriggerTemplate.cs
- [ ] Templates\WaypointTemplate.cs
- [ ] Triggers\TriggerSystem.cs

### Odyssey.Engines.Aurora (1 file)

- [ ] AuroraEngine.cs

### Odyssey.Engines.Common (8 files)

- [ ] BaseEngine.cs
- [ ] BaseEngineGame.cs
- [ ] BaseEngineModule.cs
- [ ] BaseEngineProfile.cs
- [ ] IEngine.cs
- [ ] IEngineGame.cs
- [ ] IEngineModule.cs
- [ ] IEngineProfile.cs

### Odyssey.Engines.Eclipse (1 file)

- [ ] EclipseEngine.cs

### Odyssey.Engines.Odyssey (9 files)

- [ ] EngineApi\OdysseyK1EngineApi.cs
- [ ] EngineApi\OdysseyK2EngineApi.cs
- [ ] OdysseyEngine.cs
- [ ] OdysseyGameSession.cs
- [ ] OdysseyModuleLoader.cs
- [ ] Profiles\OdysseyK1GameProfile.cs
- [ ] Profiles\OdysseyK2GameProfile.cs
- [ ] Templates\UTC.cs
- [ ] Templates\UTCHelpers.cs

### Odyssey.Game (8 files)

- [ ] Core\GamePathDetector.cs
- [ ] Core\GameSettings.cs
- [ ] Core\GameState.cs
- [ ] Core\GraphicsBackendFactory.cs
- [ ] Core\OdysseyGame.cs
- [ ] GUI\MenuRenderer.cs
- [ ] GUI\SaveLoadMenu.cs
- [ ] Program.cs

### Odyssey.Graphics (22 files)

- [x] GraphicsBackend.cs - Graphics Options @ 0x007b56a8, BTN_GRAPHICS @ 0x007d0d8c, optgraphics_p @ 0x007d2064, 2D3DBias @ 0x007c612c, 2D3D Bias @ 0x007c71f8
- [x] IContentManager.cs - Resource @ 0x007c14d4, Loading @ 0x007c7e40, CExoKeyTable @ 0x007b6078, FUN_00633270 @ 0x00633270
- [x] IDepthStencilBuffer.cs - GL_ARB_depth_texture @ 0x007b8848, m_sDepthTextureName @ 0x007baaa8, depth_texture @ 0x007bab5c, glDepthMask @ 0x0080aa38, glDepthFunc @ 0x0080ad96, glStencilOp @ 0x0080a9f0, glStencilMask @ 0x0080aa0c, glStencilFunc @ 0x0080aa68, glClearStencil @ 0x0080ada4, GL_EXT_stencil_two_side @ 0x007b8a68
- [x] IEffect.cs - Vertex program for skinned animations @ 0x0081c228, 0x0081fe20, DirectX 8/9 fixed-function pipeline
- [x] IEntityModelRenderer.cs - ModelName @ 0x007c1c8c, Model @ 0x007c1ca8, VisibleModel @ 0x007c1c98, ModelType @ 0x007c4568, MODELTYPE @ 0x007c036c, ModelVariation @ 0x007c0990, ModelPart @ 0x007bd42c, CSWCCreature::LoadModel @ 0x007c82fc, FUN_005261b0 @ 0x005261b0
- [x] IFont.cs - dialogfont16x16 @ 0x007b6380, fontheight @ 0x007b6eb8, Use Small Fonts @ 0x007c8538
- [x] IGraphicsBackend.cs - Graphics Options @ 0x007b56a8, BTN_GRAPHICS @ 0x007d0d8c, optgraphics_p @ 0x007d2064, Render Window @ 0x007b5680, render @ 0x007bab34, renderorder @ 0x007bab50, FUN_00404250 @ 0x00404250
- [x] IGraphicsDevice.cs - Render Window @ 0x007b5680, render @ 0x007bab34, WGL_NV_render_texture_rectangle @ 0x007b880c, WGL_ARB_render_texture @ 0x007b8890, DirectX 8/9 device
- [x] IIndexBuffer.cs - GetNextIndex: Duplicate triangle @ 0x007bb308, 0x007bb330, GetNextIndex: Triangle doesn't have all of its vertices @ 0x007bb36c
- [x] IInputManager.cs - Already has comprehensive Ghidra references
- [x] IModel.cs - ModelName @ 0x007c1c8c, Model @ 0x007c1ca8, VisibleModel @ 0x007c1c98, ModelType @ 0x007c4568, MODELTYPE @ 0x007c036c, ModelVariation @ 0x007c0990, ModelPart @ 0x007bd42c, CSWCCreature::LoadModel @ 0x007c82fc, FUN_005261b0 @ 0x005261b0
- [x] IRenderState.cs - GL_ARB_depth_texture @ 0x007b8848, glDepthMask @ 0x0080aa38, glDepthFunc @ 0x0080ad96, glStencilOp @ 0x0080a9f0, glStencilMask @ 0x0080aa0c, glStencilFunc @ 0x0080aa68, glClearStencil @ 0x0080ada4, GL_EXT_stencil_two_side @ 0x007b8a68
- [x] IRenderTarget.cs - WGL_NV_render_texture_rectangle @ 0x007b880c, WGL_ARB_render_texture @ 0x007b8890, m_sDepthTextureName @ 0x007baaa8, depth_texture @ 0x007bab5c
- [x] IRoomMeshRenderer.cs - roomcount @ 0x007b96c0, RoomName @ 0x007bd484, Rooms @ 0x007bd490, trimesh @ 0x007bac30, animmesh @ 0x007bac24, danglymesh @ 0x007bac18, VISIBLEVALUE @ 0x007b6a58, %s/%s.VIS @ 0x007b972c, VisibleModel @ 0x007c1c98, render @ 0x007bab34, renderorder @ 0x007bab50, AREANAME @ 0x007be1dc, AreaName @ 0x007be340
- [x] ISpatialAudio.cs - EnvAudio @ 0x007bd478, EAX2 room rolloff @ 0x007c5f24, EAX3 room LF @ 0x007c6010, EAX2 room HF @ 0x007c6040, EAX2 room @ 0x007c6050, EAX3 modulation depth @ 0x007c5f74, EAX3 echo depth @ 0x007c5fa4, _AIL_set_digital_master_room_type@8 @ 0x0080a0f6, _AIL_set_3D_room_type@8 @ 0x0080a11c,_AIL_3D_room_type@4 @ 0x0080a1ec
- [x] ISpriteBatch.cs - gui3D_room @ 0x007cc144, Render Window @ 0x007b5680, DirectX 8/9 sprite rendering
- [x] ITexture2D.cs - texturewidth @ 0x007b6e98, GL_ARB_texture_compression @ 0x007b88fc, GL_EXT_texture_compression_s3tc @ 0x007b88dc, GL_EXT_texture_filter_anisotropic @ 0x007b8974, GL_EXT_texture_cube_map @ 0x007b89dc, GL_EXT_texture_env_combine @ 0x007b8a2c, GL_ARB_multitexture @ 0x007b8a48, glActiveTextureARB @ 0x007b8738, glClientActiveTextureARB @ 0x007b871c, glBindTextureUnitParameterEXT @ 0x007b7774
- [x] IVertexBuffer.cs - Disable Vertex Buffer Objects @ 0x007b56bc, glVertexArrayRangeNV @ 0x007b7ce8, glVertexAttrib4fvNV @ 0x007b7d24, glVertexAttrib3fvNV @ 0x007b7d38, glVertexAttrib2fvNV @ 0x007b7d4c, glDeleteVertexShadersEXT @ 0x007b7974, glGenVertexShadersEXT @ 0x007b7990, glBindVertexShaderEXT @ 0x007b79a8
- [x] IVertexDeclaration.cs - Disable Vertex Buffer Objects @ 0x007b56bc, glVertexAttrib4fvNV @ 0x007b7d24, glVertexAttrib3fvNV @ 0x007b7d38, glVertexAttrib2fvNV @ 0x007b7d4c, DirectX 8/9 FVF
- [x] IWindow.cs - Render Window @ 0x007b5680, SW Movie Player Window @ 0x007b57dc, SWMovieWindow @ 0x007b57f4, Exo Base Window @ 0x007b74a0, AllowWindowedMode @ 0x007c75d0, GetProcessWindowStation @ 0x007d95f4, GetActiveWindow @ 0x007d963c, SetWindowTextA @ 0x00809e1a, DestroyWindow @ 0x00809e8a, ShowWindow @ 0x00809e9a
- [x] MatrixHelper.cs - DirectX 8/9 matrix operations (D3DXMatrix* functions)
- [x] VertexPositionColor.cs - Disable Vertex Buffer Objects @ 0x007b56bc, DirectX 8/9 FVF D3DFVF_XYZ | D3DFVF_DIFFUSE

### Odyssey.Graphics.Common (15 files)

- [ ] Backends\BaseDirect3D11Backend.cs
- [ ] Backends\BaseDirect3D12Backend.cs
- [ ] Backends\BaseGraphicsBackend.cs
- [ ] Backends\BaseVulkanBackend.cs
- [ ] Enums\GraphicsBackendType.cs
- [ ] Interfaces\ILowLevelBackend.cs
- [ ] Interfaces\IPostProcessingEffect.cs
- [ ] Interfaces\IRaytracingSystem.cs
- [ ] Interfaces\IUpscalingSystem.cs
- [ ] PostProcessing\BasePostProcessingEffect.cs
- [ ] Raytracing\BaseRaytracingSystem.cs
- [ ] Remix\BaseRemixBridge.cs
- [ ] Rendering\RenderSettings.cs
- [ ] Structs\GraphicsStructs.cs
- [ ] Upscaling\BaseUpscalingSystem.cs

### Odyssey.Kotor (56 files)

- [ ] Combat\CombatManager.cs
- [ ] Combat\CombatRound.cs
- [ ] Combat\DamageCalculator.cs
- [ ] Combat\WeaponDamageCalculator.cs
- [ ] Components\ActionQueueComponent.cs
- [ ] Components\CreatureComponent.cs
- [ ] Components\DoorComponent.cs
- [ ] Components\EncounterComponent.cs
- [ ] Components\FactionComponent.cs
- [ ] Components\InventoryComponent.cs
- [ ] Components\ItemComponent.cs
- [ ] Components\PerceptionComponent.cs
- [ ] Components\PlaceableComponent.cs
- [ ] Components\QuickSlotComponent.cs
- [ ] Components\RenderableComponent.cs
- [ ] Components\ScriptHooksComponent.cs
- [ ] Components\SoundComponent.cs
- [ ] Components\StatsComponent.cs
- [ ] Components\StoreComponent.cs
- [ ] Components\TransformComponent.cs
- [ ] Components\TriggerComponent.cs
- [ ] Components\WaypointComponent.cs
- [ ] Data\GameDataManager.cs
- [ ] Data\TwoDATableManager.cs
- [ ] Dialogue\ConversationContext.cs
- [ ] Dialogue\DialogueManager.cs
- [ ] Dialogue\DialogueState.cs
- [ ] Dialogue\KotorDialogueLoader.cs
- [ ] Dialogue\KotorLipDataLoader.cs
- [ ] EngineApi\K1EngineApi.cs
- [ ] EngineApi\K2EngineApi.cs
- [ ] Game\GameSession.cs
- [ ] Game\ModuleLoader.cs
- [ ] Game\ModuleTransitionSystem.cs
- [ ] Game\PlayerController.cs
- [ ] Game\ScriptExecutor.cs
- [ ] Input\PlayerController.cs
- [ ] Loading\EntityFactory.cs
- [ ] Loading\KotorModuleLoader.cs
- [ ] Loading\ModuleLoader.cs
- [ ] Loading\NavigationMeshFactory.cs
- [ ] Profiles\GameProfileFactory.cs
- [ ] Profiles\IGameProfile.cs
- [ ] Profiles\K1GameProfile.cs
- [ ] Profiles\K2GameProfile.cs
- [ ] Save\SaveGameManager.cs
- [ ] Systems\AIController.cs
- [ ] Systems\ComponentInitializer.cs
- [ ] Systems\EncounterSystem.cs
- [ ] Systems\FactionManager.cs
- [ ] Systems\HeartbeatSystem.cs
- [ ] Systems\ModelResolver.cs
- [ ] Systems\PartyManager.cs
- [ ] Systems\PerceptionManager.cs
- [ ] Systems\StoreSystem.cs
- [ ] Systems\TriggerSystem.cs

### Odyssey.MonoGame (159 files)

- [ ] Animation\AnimationCompression.cs
- [ ] Animation\SkeletalAnimationBatching.cs
- [ ] Assets\AssetHotReload.cs
- [ ] Assets\AssetValidator.cs
- [ ] Audio\MonoGameSoundPlayer.cs
- [ ] Audio\MonoGameVoicePlayer.cs
- [ ] Audio\SpatialAudio.cs
- [ ] Backends\BackendFactory.cs
- [ ] Backends\Direct3D10Backend.cs
- [ ] Backends\Direct3D11Backend.cs
- [ ] Backends\Direct3D12Backend.cs
- [ ] Backends\OpenGLBackend.cs
- [ ] Backends\VulkanBackend.cs
- [ ] Camera\ChaseCamera.cs
- [ ] Camera\MonoGameDialogueCameraController.cs
- [ ] Compute\ComputeShaderFramework.cs
- [ ] Converters\MdlToMonoGameModelConverter.cs
- [ ] Converters\RoomMeshRenderer.cs
- [ ] Converters\TpcToMonoGameTextureConverter.cs
- [ ] Culling\DistanceCuller.cs
- [ ] Culling\Frustum.cs
- [ ] Culling\GPUCulling.cs
- [ ] Culling\OcclusionCuller.cs
- [ ] Debug\DebugRendering.cs
- [ ] Debug\RenderStatistics.cs
- [ ] Enums\GraphicsBackend.cs
- [ ] Enums\MaterialType.cs
- [ ] Graphics\MonoGameBasicEffect.cs
- [ ] Graphics\MonoGameContentManager.cs
- [ ] Graphics\MonoGameDepthStencilBuffer.cs
- [ ] Graphics\MonoGameEntityModelRenderer.cs
- [ ] Graphics\MonoGameFont.cs
- [ ] Graphics\MonoGameGraphicsBackend.cs
- [ ] Graphics\MonoGameGraphicsDevice.cs
- [ ] Graphics\MonoGameIndexBuffer.cs
- [ ] Graphics\MonoGameInputManager.cs
- [ ] Graphics\MonoGameRenderState.cs
- [ ] Graphics\MonoGameRenderTarget.cs
- [ ] Graphics\MonoGameRoomMeshRenderer.cs
- [ ] Graphics\MonoGameSpatialAudio.cs
- [ ] Graphics\MonoGameSpriteBatch.cs
- [ ] Graphics\MonoGameTexture2D.cs
- [ ] Graphics\MonoGameVertexBuffer.cs
- [ ] Graphics\MonoGameWindow.cs
- [ ] GUI\KotorGuiManager.cs
- [ ] GUI\MyraMenuRenderer.cs
- [ ] Interfaces\ICommandList.cs
- [ ] Interfaces\IDevice.cs
- [ ] Interfaces\IDynamicLight.cs
- [ ] Interfaces\IGraphicsBackend.cs
- [ ] Interfaces\IPbrMaterial.cs
- [ ] Interfaces\IRaytracingSystem.cs
- [ ] Lighting\ClusteredLightCulling.cs
- [ ] Lighting\ClusteredLightingSystem.cs
- [ ] Lighting\DynamicLight.cs
- [ ] Lighting\LightProbeSystem.cs
- [ ] Lighting\VolumetricLighting.cs
- [ ] Loading\AsyncResourceLoader.cs
- [ ] LOD\LODFadeSystem.cs
- [ ] LOD\LODSystem.cs
- [ ] Materials\KotorMaterialConverter.cs
- [ ] Materials\KotorMaterialFactory.cs
- [ ] Materials\MaterialInstancing.cs
- [ ] Materials\PbrMaterial.cs
- [ ] Memory\GPUMemoryPool.cs
- [ ] Memory\MemoryTracker.cs
- [ ] Memory\ObjectPool.cs
- [ ] Models\MDLModelConverter.cs
- [ ] Particles\GPUParticleSystem.cs
- [ ] Particles\ParticleSorter.cs
- [ ] Performance\FramePacing.cs
- [ ] Performance\FrameTimeBudget.cs
- [ ] Performance\GPUTimestamps.cs
- [ ] Performance\Telemetry.cs
- [ ] PostProcessing\Bloom.cs
- [ ] PostProcessing\ColorGrading.cs
- [ ] PostProcessing\ExposureAdaptation.cs
- [ ] PostProcessing\MotionBlur.cs
- [ ] PostProcessing\SSAO.cs
- [ ] PostProcessing\SSR.cs
- [ ] PostProcessing\TemporalAA.cs
- [ ] PostProcessing\ToneMapping.cs
- [ ] Raytracing\NativeRaytracingSystem.cs
- [ ] Raytracing\RaytracedEffects.cs
- [ ] Remix\Direct3D9Wrapper.cs
- [ ] Remix\RemixBridge.cs
- [ ] Remix\RemixMaterialExporter.cs
- [ ] Rendering\AdaptiveQuality.cs
- [ ] Rendering\BatchOptimizer.cs
- [ ] Rendering\BindlessTextures.cs
- [ ] Rendering\CommandBuffer.cs
- [ ] Rendering\CommandListOptimizer.cs
- [ ] Rendering\ContactShadows.cs
- [ ] Rendering\DecalSystem.cs
- [ ] Rendering\DeferredRenderer.cs
- [ ] Rendering\DepthPrePass.cs
- [ ] Rendering\DrawCallSorter.cs
- [ ] Rendering\DynamicBatching.cs
- [ ] Rendering\DynamicResolution.cs
- [ ] Rendering\EntityModelRenderer.cs
- [ ] Rendering\FrameGraph.cs
- [ ] Rendering\GeometryCache.cs
- [ ] Rendering\GeometryStreaming.cs
- [ ] Rendering\GPUInstancing.cs
- [ ] Rendering\GPUMemoryBudget.cs
- [ ] Rendering\GPUMemoryDefragmentation.cs
- [ ] Rendering\GPUSynchronization.cs
- [ ] Rendering\HDRPipeline.cs
- [ ] Rendering\IndirectRenderer.cs
- [ ] Rendering\MemoryAliasing.cs
- [ ] Rendering\MeshCompression.cs
- [ ] Rendering\ModernRenderer.cs
- [ ] Rendering\MultiThreadedRenderer.cs
- [ ] Rendering\MultiThreadedRendering.cs
- [ ] Rendering\OcclusionQueries.cs
- [ ] Rendering\OdysseyRenderer.cs
- [ ] Rendering\PipelineStateCache.cs
- [ ] Rendering\QualityPresets.cs
- [ ] Rendering\RenderBatchManager.cs
- [ ] Rendering\RenderGraph.cs
- [ ] Rendering\RenderOptimizer.cs
- [ ] Rendering\RenderPipeline.cs
- [ ] Rendering\RenderProfiler.cs
- [ ] Rendering\RenderQueue.cs
- [ ] Rendering\RenderSettings.cs
- [ ] Rendering\RenderTargetCache.cs
- [ ] Rendering\RenderTargetChain.cs
- [ ] Rendering\RenderTargetManager.cs
- [ ] Rendering\RenderTargetPool.cs
- [ ] Rendering\RenderTargetScaling.cs
- [ ] Rendering\ResourceBarriers.cs
- [ ] Rendering\ResourcePreloader.cs
- [ ] Rendering\SceneGraph.cs
- [ ] Rendering\ShaderCache.cs
- [ ] Rendering\StateCache.cs
- [ ] Rendering\SubsurfaceScattering.cs
- [ ] Rendering\TemporalReprojection.cs
- [ ] Rendering\TextureAtlas.cs
- [ ] Rendering\TextureCompression.cs
- [ ] Rendering\TriangleStripGenerator.cs
- [ ] Rendering\Upscaling\DLSS.cs
- [ ] Rendering\Upscaling\FSR.cs
- [ ] Rendering\VariableRateShading.cs
- [ ] Rendering\VertexCacheOptimizer.cs
- [ ] Rendering\VisibilityBuffer.cs
- [ ] Save\AsyncSaveSystem.cs
- [ ] Scene\SceneBuilder.cs
- [ ] Shaders\ShaderCache.cs
- [ ] Shaders\ShaderPermutationSystem.cs
- [ ] Shadows\CascadedShadowMaps.cs
- [ ] Spatial\Octree.cs
- [ ] Textures\TextureFormatConverter.cs
- [ ] Textures\TextureStreamingManager.cs
- [ ] UI\BasicHUD.cs
- [ ] UI\DialoguePanel.cs
- [ ] UI\LoadingScreen.cs
- [ ] UI\MainMenu.cs
- [ ] UI\PauseMenu.cs
- [ ] UI\ScreenFade.cs

### Odyssey.Scripting (11 files)

- [ ] EngineApi\BaseEngineApi.cs
- [ ] Interfaces\IEngineApi.cs
- [ ] Interfaces\IExecutionContext.cs
- [ ] Interfaces\INcsVm.cs
- [ ] Interfaces\IScriptGlobals.cs
- [ ] Interfaces\Variable.cs
- [ ] ScriptExecutor.cs
- [ ] Types\Location.cs
- [ ] VM\ExecutionContext.cs
- [ ] VM\NcsVm.cs
- [ ] VM\ScriptGlobals.cs

### Odyssey.Stride (31 files)

- [ ] Audio\StrideSoundPlayer.cs
- [ ] Audio\StrideVoicePlayer.cs
- [ ] Backends\StrideBackendFactory.cs
- [ ] Backends\StrideDirect3D11Backend.cs
- [ ] Backends\StrideDirect3D12Backend.cs
- [ ] Backends\StrideVulkanBackend.cs
- [ ] Camera\StrideDialogueCameraController.cs
- [ ] Graphics\StrideBasicEffect.cs
- [ ] Graphics\StrideContentManager.cs
- [ ] Graphics\StrideDepthStencilBuffer.cs
- [ ] Graphics\StrideEntityModelRenderer.cs
- [ ] Graphics\StrideFont.cs
- [ ] Graphics\StrideGraphicsBackend.cs
- [ ] Graphics\StrideGraphicsDevice.cs
- [ ] Graphics\StrideIndexBuffer.cs
- [ ] Graphics\StrideInputManager.cs
- [ ] Graphics\StrideRenderState.cs
- [ ] Graphics\StrideRenderTarget.cs
- [ ] Graphics\StrideRoomMeshRenderer.cs
- [ ] Graphics\StrideSpatialAudio.cs
- [ ] Graphics\StrideSpriteBatch.cs
- [ ] Graphics\StrideTexture2D.cs
- [ ] Graphics\StrideVertexBuffer.cs
- [ ] Graphics\StrideWindow.cs
- [ ] PostProcessing\StrideBloomEffect.cs
- [ ] PostProcessing\StrideSsaoEffect.cs
- [ ] PostProcessing\StrideTemporalAaEffect.cs
- [ ] Raytracing\StrideRaytracingSystem.cs
- [ ] Remix\StrideRemixBridge.cs
- [ ] Upscaling\StrideDlssSystem.cs
- [ ] Upscaling\StrideFsrSystem.cs

### Odyssey.Tests (3 files)

- [ ] UI\FallbackUITests.cs
- [ ] UI\KotorGuiManagerTests.cs
- [ ] VM\NcsVmTests.cs

### Odyssey.Tooling (1 file)

- [ ] Program.cs

## Notes

- Focus on core game logic first (Odyssey.Core, Odyssey.Kotor, Odyssey.Scripting)
- Graphics/MonoGame adapters can be lower priority unless they affect gameplay
- Use Ghidra string searches to locate functions (e.g., "GLOBALVARS", "PARTYTABLE", "savenfo")
- Document all Ghidra function addresses and string references in comments
- Match original engine behavior exactly where documented
- Modern graphics enhancements (DLSS, FSR, RTX Remix, raytracing) are not in original game - note as enhancements
