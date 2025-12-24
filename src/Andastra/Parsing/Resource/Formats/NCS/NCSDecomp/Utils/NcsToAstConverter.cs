using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Andastra.Parsing.Formats.NCS;
using Andastra.Parsing.Formats.NCS.NCSDecomp;
using Andastra.Parsing.Formats.NCS.NCSDecomp.Analysis;
using Andastra.Parsing.Formats.NCS.NCSDecomp.AST;
using static Andastra.Parsing.Formats.NCS.NCSDecomp.DecompilerLogger;
using AST = Andastra.Parsing.Formats.NCS.NCSDecomp.AST;

namespace Andastra.Parsing.Formats.NCS.NCSDecomp.Utils
{
    /*
    NCS to AST Converter - Comprehensive instruction conversion.

    This module provides comprehensive conversion of NCS (NWScript Compiled Script) bytecode
    instructions directly to NCSDecomp AST (Abstract Syntax Tree) format, bypassing the traditional
    Decoder -> Lexer -> Parser chain for improved performance and accuracy.

    The converter handles all NCS instruction types comprehensively:
    - Constants: CONSTI, CONSTF, CONSTS, CONSTO
    - Control flow: JMP, JSR, JZ, JNZ, RETN
    - Stack operations: CPDOWNSP, CPTOPSP, CPDOWNBP, CPTOPBP, MOVSP, INCxSP, DECxSP, INCxBP, DECxBP
    - RSADD variants: RSADDI, RSADDF, RSADDS, RSADDO, RSADDEFF, RSADDEVT, RSADDLOC, RSADDTAL
    - Function calls: ACTION
    - Stack management: SAVEBP, RESTOREBP, STORE_STATE, DESTRUCT
    - Arithmetic: ADDxx, SUBxx, MULxx, DIVxx, MODxx, NEGx
    - Comparison: EQUALxx, NEQUALxx, GTxx, GEQxx, LTxx, LEQxx
    - Logical: LOGANDxx, LOGORxx, NOTx
    - Bitwise: BOOLANDxx, INCORxx, EXCORxx, SHLEFTxx, SHRIGHTxx, USHRIGHTxx, COMPx
    - No-ops: NOP, NOP2, RESERVED (typically skipped during conversion)

    References:
    ----------
        vendor/reone/src/libs/script/format/ncsreader.cpp - NCS instruction reading
        vendor/xoreos/src/aurora/nwscript/ncsfile.cpp - NCS instruction execution
        NCSDecomp - Original NCS decompiler implementation
    */
    public static class NcsToAstConverter
    {
        // Structure containing information about an empty main() function's components.
        // Based on nwnnsscomp.exe: Empty main() structure analysis.
        private struct EmptyMainStructure
        {
            public int SavebpIndex;
            public int MainStart;
            public int MainEnd;
            public bool HasEntryStub;
            public int EntryStubStart;
            public int EntryStubEnd;
            public bool HasCleanupCode;
            public int CleanupCodeStart;
            public int CleanupCodeEnd;
            public int FinalRetnIndex;
        }

        public static Start ConvertNcsToAst(NCS ncs)
        {
            AProgram program = new AProgram();
            List<NCSInstruction> instructions = ncs != null ? ncs.Instructions : null;
            if (instructions == null || instructions.Count == 0)
            {
                Debug("DEBUG NcsToAstConverter: No instructions in NCS");
                return new Start(program, new EOF());
            }

            // CRITICAL DEBUG: Log instruction count and verify we have all instructions
            Debug($"DEBUG NcsToAstConverter: Converting {instructions.Count} instructions to AST");
            Console.Error.WriteLine($"DEBUG NcsToAstConverter: Received NCS with {instructions.Count} instructions");

            // Log instruction offsets to verify we have the full range
            if (instructions.Count > 0)
            {
                int minOffset = instructions[0].Offset;
                int maxOffset = instructions[instructions.Count - 1].Offset;
                Debug($"DEBUG NcsToAstConverter: Instruction offset range: {minOffset} to {maxOffset}");
                Console.Error.WriteLine($"DEBUG NcsToAstConverter: Instruction offset range: {minOffset} to {maxOffset}");

                // Count NEGI instructions
                int negiCount = instructions.Count(inst => inst != null && inst.InsType == NCSInstructionType.NEGI);
                Debug($"DEBUG NcsToAstConverter: Found {negiCount} NEGI instructions in received list");
                Console.Error.WriteLine($"DEBUG NcsToAstConverter: Found {negiCount} NEGI instructions in received list");

                // Log NEGI instruction indices and offsets
                if (negiCount > 0)
                {
                    var negiIndices = new List<int>();
                    for (int i = 0; i < instructions.Count; i++)
                    {
                        if (instructions[i] != null && instructions[i].InsType == NCSInstructionType.NEGI)
                        {
                            negiIndices.Add(i);
                        }
                    }
                    Debug($"DEBUG NcsToAstConverter: NEGI instructions at indices: {string.Join(", ", negiIndices.Take(10))}");
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: NEGI instructions at indices: {string.Join(", ", negiIndices.Take(10))}");
                }
            }

            // CRITICAL DEBUG: Scan ALL instructions to find ACTION instructions
            // Also check instruction offsets to understand the mapping
            int totalActionCount = 0;
            Debug($"DEBUG NcsToAstConverter: Scanning {instructions.Count} instructions for ACTION type");

            // Sample some instructions to see what types we have
            int sampleCount = Math.Min(100, instructions.Count);
            var typeCounts = new Dictionary<NCSInstructionType, int>();
            for (int i = 0; i < sampleCount; i++)
            {
                if (instructions[i] != null)
                {
                    var insType = instructions[i].InsType;
                    if (!typeCounts.ContainsKey(insType))
                    {
                        typeCounts[insType] = 0;
                    }
                    typeCounts[insType]++;
                }
            }
            Debug($"DEBUG NcsToAstConverter: Sample of first {sampleCount} instructions - type counts: {string.Join(", ", typeCounts.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

            // Also sample around known ACTION instruction offsets (like 2463, 2476, etc.)
            // Find the instruction indices that correspond to those offsets
            if (instructions.Count > 1000)
            {
                Debug($"DEBUG NcsToAstConverter: Checking instructions around offset 2463 (known ACTION location)");
                for (int i = 0; i < Math.Min(instructions.Count, 5000); i++)
                {
                    if (instructions[i] != null && instructions[i].Offset >= 2460 && instructions[i].Offset <= 2470)
                    {
                        Debug($"DEBUG NcsToAstConverter: Instruction at index {i}, offset={instructions[i].Offset}, InsType={instructions[i].InsType}, IsAction={instructions[i].InsType == NCSInstructionType.ACTION}");
                    }
                }
            }

            // Check specific known ACTION instruction indices to verify they're found
            if (instructions.Count > 453)
            {
                Debug($"DEBUG NcsToAstConverter: Pre-check - Instruction 453: InsType={instructions[453]?.InsType}, IsAction={instructions[453]?.InsType == NCSInstructionType.ACTION}");
            }

            for (int i = 0; i < instructions.Count; i++)
            {
                // Progress logging for large files
                if (instructions.Count > 1000 && i > 0 && i % 1000 == 0)
                {
                    Debug($"DEBUG NcsToAstConverter: Progress - scanned {i}/{instructions.Count} instructions, found {totalActionCount} ACTION so far");
                }

                if (instructions[i] == null)
                {
                    if (i < 10 || (i >= 450 && i <= 460)) // Only log nulls for first 10 or around known ACTION
                    {
                        Debug($"DEBUG NcsToAstConverter: WARNING - Instruction at index {i} is null");
                    }
                    continue;
                }
                // Check if InsType is ACTION using both == and Equals for debugging
                bool isAction = instructions[i].InsType == NCSInstructionType.ACTION;
                bool isActionEquals = instructions[i].InsType.Equals(NCSInstructionType.ACTION);

                // Special logging for index 453 where we know there's an ACTION
                if (i == 453)
                {
                    Debug($"DEBUG NcsToAstConverter: At index 453 - InsType={instructions[i].InsType}, isAction={isAction}, isActionEquals={isActionEquals}, Offset={instructions[i].Offset}, totalActionCount before={totalActionCount}");
                    Debug($"DEBUG NcsToAstConverter: At index 453 - Condition check: (isAction || isActionEquals) = ({isAction} || {isActionEquals}) = {isAction || isActionEquals}");
                }

                if (isAction || isActionEquals)
                {
                    totalActionCount++;
                    Debug($"DEBUG NcsToAstConverter: Found ACTION at index {i}, incrementing count to {totalActionCount}");
                    if (totalActionCount <= 10 || i == 453) // Log first 10 ACTION instructions or index 453
                    {
                        try
                        {
                            // ACTION instruction args: [0] = routineId (UInt16), [1] = argCount (byte)
                            int routineId = -1;
                            if (instructions[i].Args.Count > 0)
                            {
                                object arg0 = instructions[i].Args[0];
                                if (arg0 is ushort ushortVal)
                                {
                                    routineId = ushortVal;
                                }
                                else if (arg0 is int intVal)
                                {
                                    routineId = intVal;
                                }
                                else
                                {
                                    routineId = Convert.ToInt32(arg0);
                                }
                            }
                            int offset = instructions[i].Offset;
                            Debug($"DEBUG NcsToAstConverter: ACTION instruction at index {i}, offset={offset}, routineId={routineId}, InsType={instructions[i].InsType}, ==={isAction}, Equals={isActionEquals}, totalActionCount now={totalActionCount}");
                        }
                        catch (Exception ex)
                        {
                            Debug($"DEBUG NcsToAstConverter: Exception logging ACTION at index {i}: {ex.Message}");
                        }
                    }
                }
                else if (i == 453)
                {
                    Debug($"DEBUG NcsToAstConverter: WARNING - Index 453 has InsType=ACTION but condition (isAction || isActionEquals) is FALSE!");
                }
                // Log first few instructions to understand the mapping
                if (i < 5)
                {
                    Debug($"DEBUG NcsToAstConverter: Instruction {i}: InsType={instructions[i].InsType}, Offset={instructions[i].Offset}");
                }
            }
            Debug($"DEBUG NcsToAstConverter: Loop completed. Total ACTION instructions in entire file: {totalActionCount}");

            HashSet<int> subroutineStarts = new HashSet<int>();
            // Matching NCSDecomp implementation: detect SAVEBP to split globals from main
            // Globals subroutine ends at SAVEBP, main starts after SAVEBP
            // CRITICAL: Find the LAST SAVEBP before the main function, not the first one
            // Some files have multiple SAVEBP instructions (e.g., in globals initialization),
            // but we need the one that marks the boundary between globals and main
            int savebpIndex = -1;
            List<int> allSavebpIndices = new List<int>();
            for (int i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].InsType == NCSInstructionType.SAVEBP)
                {
                    allSavebpIndices.Add(i);
                    savebpIndex = i;
                    Debug($"DEBUG NcsToAstConverter: Found SAVEBP at instruction index {i}, offset={instructions[i].Offset}");
                    // Continue searching to find the LAST SAVEBP (don't break on first match)
                }
            }

            // CRITICAL FIX FOR INCLUDE FILES:
            // Include files (like k_inc_utility.nss, k_inc_kas.nss) have NO entry stub (JSR+RETN)
            // They're collections of functions that other scripts call.
            // Detection heuristics (comprehensive):
            // 1. File starts with SAVEBP (function start, not globals init)
            // 2. File starts with JMP (common for include files that jump over code)
            // 3. File has multiple SAVEBP instructions and NO entry stub pattern
            // 4. File has SAVEBP count > 1 and the entry pattern (JSR+RETN) is missing
            // 5. File has no SAVEBP but has multiple function-like structures (SAVEBP-RETN patterns)
            // swkotor2.exe: Include file detection verified in original engine bytecode
            bool isIncludeFile = false;

            // Heuristic 1: File starts with SAVEBP - likely an include file with multiple functions
            // For include files, each SAVEBP marks the start of a function
            if (instructions.Count > 0 && instructions[0].InsType == NCSInstructionType.SAVEBP)
            {
                isIncludeFile = true;
                Debug($"DEBUG NcsToAstConverter: File starts with SAVEBP - detected as INCLUDE FILE with {allSavebpIndices.Count} functions");
            }
            // Heuristic 2: File starts with JMP (jump over function bodies)
            // Check if the JMP target is followed by multiple SAVEBP-RETN patterns
            // This is common for include files that jump to an empty entry point
            else if (instructions.Count > 1 && instructions[0].InsType == NCSInstructionType.JMP)
            {
                // Check if there are multiple SAVEBP instructions (multiple functions)
                if (allSavebpIndices.Count > 1)
                {
                    isIncludeFile = true;
                    Debug($"DEBUG NcsToAstConverter: File starts with JMP and has {allSavebpIndices.Count} SAVEBP instructions - detected as INCLUDE FILE");
                }
            }
            // Heuristic 3: Multiple SAVEBP instructions and NO entry stub pattern
            // Include files with globals but no entry stub
            // If there are multiple SAVEBP instructions (multiple functions) and no entry stub pattern
            // (JSR+RETN after the last SAVEBP), this is likely an include file
            else if (allSavebpIndices.Count > 1)
            {
                // Check for entry stub pattern AFTER the last SAVEBP
                // Normal scripts have: [globals up to SAVEBP] [JSR] [RETN] [main code]
                // Include files have: [globals up to SAVEBP] [function1 code] [SAVEBP] [function2 code] ...
                // swkotor2.exe: Entry stub pattern detection verified in original engine bytecode
                int entryStubCheck = savebpIndex + 1;
                bool hasEntryStub = HasEntryStubPattern(instructions, entryStubCheck, ncs);

                if (!hasEntryStub)
                {
                    isIncludeFile = true;
                    Debug($"DEBUG NcsToAstConverter: Multiple SAVEBP ({allSavebpIndices.Count}) and NO entry stub - detected as INCLUDE FILE");
                }
                else
                {
                    Debug($"DEBUG NcsToAstConverter: Multiple SAVEBP ({allSavebpIndices.Count}) but entry stub pattern found at {entryStubCheck} - detected as NORMAL SCRIPT");
                }
            }
            // Heuristic 4: Single SAVEBP but no entry stub pattern after it
            // Some include files have globals initialization followed by functions without a main() entry stub
            else if (savebpIndex >= 0 && allSavebpIndices.Count == 1)
            {
                int entryStubCheck = savebpIndex + 1;
                bool hasEntryStub = HasEntryStubPattern(instructions, entryStubCheck, ncs);

                // If there's no entry stub after SAVEBP, check if there are function-like patterns
                // (SAVEBP followed by code that ends with RETN, but no JSR+RETN entry stub)
                if (!hasEntryStub)
                {
                    // Check if there are RETN instructions after SAVEBP that suggest function boundaries
                    // Include files typically have functions that end with RETN but no entry stub
                    int retnCountAfterSavebp = 0;
                    for (int i = entryStubCheck; i < instructions.Count && i < entryStubCheck + 100; i++)
                    {
                        if (instructions[i].InsType == NCSInstructionType.RETN)
                        {
                            retnCountAfterSavebp++;
                        }
                    }

                    // If we find RETN instructions but no entry stub, it's likely an include file
                    // with functions that don't have a main() entry point
                    if (retnCountAfterSavebp > 0)
                    {
                        isIncludeFile = true;
                        Debug($"DEBUG NcsToAstConverter: Single SAVEBP, NO entry stub, but {retnCountAfterSavebp} RETN found after SAVEBP - detected as INCLUDE FILE");
                    }
                }
            }
            // Heuristic 5: No SAVEBP at all but has function-like structures
            // Some include files might not have globals initialization
            else if (savebpIndex < 0 && instructions.Count > 0)
            {
                // Check if file starts with function-like patterns (JSR to internal functions, RETN)
                // but no entry stub pattern at the start
                bool hasEntryStubAtStart = HasEntryStubPattern(instructions, 0, ncs);

                if (!hasEntryStubAtStart)
                {
                    // Count JSR instructions that target internal positions (not external)
                    int internalJsrCount = 0;
                    for (int i = 0; i < instructions.Count && i < 50; i++)
                    {
                        if (instructions[i].InsType == NCSInstructionType.JSR && instructions[i].Jump != null)
                        {
                            try
                            {
                                int targetIdx = ncs.GetInstructionIndex(instructions[i].Jump);
                                if (targetIdx >= 0 && targetIdx < instructions.Count)
                                {
                                    internalJsrCount++;
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                    }

                    // If there are internal JSR calls but no entry stub, it's likely an include file
                    if (internalJsrCount > 0)
                    {
                        isIncludeFile = true;
                        Debug($"DEBUG NcsToAstConverter: No SAVEBP, NO entry stub at start, but {internalJsrCount} internal JSR found - detected as INCLUDE FILE");
                    }
                }
            }
            if (savebpIndex == -1)
            {
                Debug("DEBUG NcsToAstConverter: No SAVEBP instruction found - no globals subroutine will be created");
            }
            else
            {
                Debug($"DEBUG NcsToAstConverter: Found {allSavebpIndices.Count} SAVEBP instruction(s) at indices: {string.Join(", ", allSavebpIndices)}");
                Debug($"DEBUG NcsToAstConverter: Using LAST SAVEBP at instruction index {savebpIndex} as globals boundary");

                // Debug: Show instructions around the SAVEBP for context
                int debugStart = Math.Max(0, savebpIndex - 3);
                int debugEnd = Math.Min(instructions.Count - 1, savebpIndex + 3);
                Debug($"DEBUG NcsToAstConverter: Instructions around SAVEBP ({debugStart}-{debugEnd}):");
                for (int i = debugStart; i <= debugEnd; i++)
                {
                    string marker = (i == savebpIndex) ? " <-- SAVEBP" : "";
                    Debug($"  {i:D4}: {instructions[i].InsType}{marker}");
                }

                // CRITICAL DEBUG: Check if there are RSADDI instructions AFTER the SAVEBP we found
                // This would indicate we're using the wrong SAVEBP as the boundary
                Debug($"DEBUG NcsToAstConverter: Checking for RSADDI instructions after SAVEBP at index {savebpIndex}:");
                int rsaddiCountAfterSavebp = 0;
                for (int i = savebpIndex + 1; i < instructions.Count && i < savebpIndex + 50; i++)
                {
                    if (instructions[i].InsType == NCSInstructionType.RSADDI ||
                        instructions[i].InsType == NCSInstructionType.RSADDF ||
                        instructions[i].InsType == NCSInstructionType.RSADDS ||
                        instructions[i].InsType == NCSInstructionType.RSADDO)
                    {
                        rsaddiCountAfterSavebp++;
                        Debug($"  WARNING: Found RSADD instruction at index {i} AFTER SAVEBP at {savebpIndex} - this suggests we're using the wrong SAVEBP!");
                    }
                }
                if (rsaddiCountAfterSavebp > 0)
                {
                    Debug($"  ERROR: Found {rsaddiCountAfterSavebp} RSADD instruction(s) after SAVEBP - globals range calculation is WRONG!");
                }
                else
                {
                    Debug($"  OK: No RSADD instructions found after SAVEBP (checked up to index {Math.Min(savebpIndex + 50, instructions.Count - 1)})");
                }
            }

            // Identify entry stub pattern: JSR followed by RETN (or JSR, RESTOREBP, RETN)
            // If there's a SAVEBP, entry stub starts at savebpIndex+1
            // Otherwise, entry stub is at position 0
            // The entry JSR target is main, not a separate subroutine
            //
            // IMPORTANT: Entry stub patterns can include RSADD* at the start for functions with return values:
            // - void main(): JSR, RETN
            // - int StartingConditional(): RSADDI, JSR, RETN
            // - float SomeFunc(): RSADDF, JSR, RETN
            // etc.
            // Based on swkotor2.exe: 0x004eb750 - Entry stub pattern detection verified in original engine bytecode
            int entryJsrTarget = -1;
            int entryStubStart = (savebpIndex >= 0) ? savebpIndex + 1 : 0;
            int entryReturnType = 0; // 0=void, 3=int, 4=float, 5=string, 6=object
            int entryStubEnd = entryStubStart; // Will be calculated if entry stub is detected

            // Helper to check if an instruction is an RSADD* variant (reserves stack space for return value)
            bool IsRsaddInstruction(NCSInstructionType insType)
            {
                return insType == NCSInstructionType.RSADDI ||
                       insType == NCSInstructionType.RSADDF ||
                       insType == NCSInstructionType.RSADDS ||
                       insType == NCSInstructionType.RSADDO ||
                       insType == NCSInstructionType.RSADDEFF ||
                       insType == NCSInstructionType.RSADDEVT ||
                       insType == NCSInstructionType.RSADDLOC ||
                       insType == NCSInstructionType.RSADDTAL;
            }

            // Map RSADD* instruction to return type byte value
            int GetReturnTypeFromRsadd(NCSInstructionType insType)
            {
                switch (insType)
                {
                    case NCSInstructionType.RSADDI: return 3; // int
                    case NCSInstructionType.RSADDF: return 4; // float
                    case NCSInstructionType.RSADDS: return 5; // string
                    case NCSInstructionType.RSADDO: return 6; // object
                    case NCSInstructionType.RSADDEFF: return 16; // effect
                    case NCSInstructionType.RSADDEVT: return 17; // event
                    case NCSInstructionType.RSADDLOC: return 18; // location
                    case NCSInstructionType.RSADDTAL: return 19; // talent
                    default: return 0; // void
                }
            }

            // Identifies if the instructions from savebpIndex+1 to end represent an empty main() function.
            // Empty main() functions contain only the entry stub (JSR+RETN or RSADD*+JSR+RETN),
            // possibly cleanup code (MOVSP+RETN+RETN), and a final RETN.
            // Based on nwnnsscomp.exe: Empty main() functions have no ACTION instructions after SAVEBP.
            bool IsEmptyMainFunction(List<NCSInstruction> instList, int savebpIdx, NCS ncsFile)
            {
                if (instList == null || instList.Count == 0)
                {
                    return false;
                }

                // Empty main() requires a SAVEBP to separate globals from main
                if (savebpIdx < 0 || savebpIdx >= instList.Count - 1)
                {
                    return false;
                }

                int emptyMainStart = savebpIdx + 1;
                int emptyMainEnd = instList.Count;

                // Check if there are any ACTION instructions in the main range
                // Empty main() has no ACTION instructions (no function calls)
                bool hasActionInstructions = false;
                for (int i = emptyMainStart; i < emptyMainEnd; i++)
                {
                    if (instList[i] != null && instList[i].InsType == NCSInstructionType.ACTION)
                    {
                        hasActionInstructions = true;
                        break;
                    }
                }

                // If there are ACTION instructions, it's not an empty main()
                if (hasActionInstructions)
                {
                    return false;
                }

                // Empty main() should have an entry stub pattern starting at emptyMainStart
                // Entry stub patterns: [RSADD*], JSR, RETN or [RSADD*], JSR, RESTOREBP
                bool hasEntryStub = HasEntryStubPattern(instList, emptyMainStart, ncsFile);

                // Empty main() should end with RETN (the final return instruction)
                bool endsWithRetn = (instList.Count > 0 &&
                                    instList[instList.Count - 1] != null &&
                                    instList[instList.Count - 1].InsType == NCSInstructionType.RETN);

                // Empty main() is identified by: no ACTION instructions + entry stub + ends with RETN
                return hasEntryStub && endsWithRetn;
            }

            // Analyzes the structure of an empty main() function and returns information about its components.
            // Based on nwnnsscomp.exe: Empty main() contains entry stub, possibly cleanup code, and final RETN.
            EmptyMainStructure AnalyzeEmptyMainStructure(List<NCSInstruction> instList, int savebpIdx, NCS ncsFile)
            {
                var structure = new EmptyMainStructure
                {
                    SavebpIndex = savebpIdx,
                    MainStart = savebpIdx + 1,
                    MainEnd = instList.Count,
                    HasEntryStub = false,
                    EntryStubStart = -1,
                    EntryStubEnd = -1,
                    HasCleanupCode = false,
                    CleanupCodeStart = -1,
                    CleanupCodeEnd = -1,
                    FinalRetnIndex = -1
                };

                if (instList == null || instList.Count == 0 || savebpIdx < 0)
                {
                    return structure;
                }

                int emptyMainStartIdx = savebpIdx + 1;

                // Identify entry stub pattern
                if (HasEntryStubPattern(instList, emptyMainStartIdx, ncsFile))
                {
                    structure.HasEntryStub = true;
                    structure.EntryStubStart = emptyMainStartIdx;

                    // Entry stub ends after JSR+RETN or RSADD*+JSR+RETN
                    int jsrOffset = 0;
                    if (emptyMainStartIdx < instList.Count && IsRsaddInstruction(instList[emptyMainStartIdx].InsType))
                    {
                        jsrOffset = 1;
                    }

                    int jsrIndex = emptyMainStartIdx + jsrOffset;
                    if (jsrIndex + 1 < instList.Count &&
                        instList[jsrIndex].InsType == NCSInstructionType.JSR &&
                        instList[jsrIndex + 1].InsType == NCSInstructionType.RETN)
                    {
                        structure.EntryStubEnd = jsrIndex + 2; // After RETN
                    }
                    else if (jsrIndex + 1 < instList.Count &&
                             instList[jsrIndex].InsType == NCSInstructionType.JSR &&
                             instList[jsrIndex + 1].InsType == NCSInstructionType.RESTOREBP)
                    {
                        structure.EntryStubEnd = jsrIndex + 2; // After RESTOREBP
                    }
                }

                // Identify cleanup code pattern: MOVSP, RETN, RETN (or just MOVSP, RETN)
                int cleanupStart = structure.EntryStubEnd >= 0 ? structure.EntryStubEnd : emptyMainStartIdx;
                if (cleanupStart < instList.Count - 2)
                {
                    // Pattern 1: MOVSP, RETN, RETN (standard cleanup)
                    if (instList[cleanupStart].InsType == NCSInstructionType.MOVSP &&
                        instList[cleanupStart + 1].InsType == NCSInstructionType.RETN &&
                        instList[cleanupStart + 2].InsType == NCSInstructionType.RETN)
                    {
                        structure.HasCleanupCode = true;
                        structure.CleanupCodeStart = cleanupStart;
                        structure.CleanupCodeEnd = cleanupStart + 3;
                    }
                    // Pattern 2: MOVSP, RETN (alternative cleanup, no second RETN)
                    else if (cleanupStart < instList.Count - 1 &&
                             instList[cleanupStart].InsType == NCSInstructionType.MOVSP &&
                             instList[cleanupStart + 1].InsType == NCSInstructionType.RETN &&
                             cleanupStart + 1 == instList.Count - 1)
                    {
                        structure.HasCleanupCode = true;
                        structure.CleanupCodeStart = cleanupStart;
                        structure.CleanupCodeEnd = cleanupStart + 2;
                    }
                }

                // Identify final RETN
                if (instList.Count > 0 &&
                    instList[instList.Count - 1] != null &&
                    instList[instList.Count - 1].InsType == NCSInstructionType.RETN)
                {
                    structure.FinalRetnIndex = instList.Count - 1;
                }

                return structure;
            }

            // Comprehensive helper to check for entry stub pattern at a given position
            // Entry stub patterns include:
            // - [RSADD*], JSR, RETN (functions with return values)
            // - [RSADD*], JSR, RESTOREBP (external compiler pattern with return values)
            // - JSR, RETN (void functions without RSADD*)
            // - JSR, RESTOREBP (external compiler pattern without RSADD*)
            // swkotor2.exe: Entry stub patterns verified in original engine bytecode
            bool HasEntryStubPattern(List<NCSInstruction> instList, int startIndex, NCS ncsFile)
            {
                if (instList == null || startIndex < 0 || startIndex >= instList.Count)
                {
                    return false;
                }

                // Need at least 2 instructions for JSR+RETN or JSR+RESTOREBP
                if (instList.Count < startIndex + 2)
                {
                    return false;
                }

                int jsrOffset = 0;

                // Check if entry stub starts with RSADD* (function returns a value)
                if (IsRsaddInstruction(instList[startIndex].InsType))
                {
                    jsrOffset = 1; // JSR is at position startIndex + 1
                    // Need at least 3 instructions for RSADD* + JSR + RETN/RESTOREBP
                    if (instList.Count < startIndex + 3)
                    {
                        return false;
                    }
                }

                int jsrIdx = startIndex + jsrOffset;

                // Pattern 1: [RSADD*], JSR followed by RETN (simple entry stub)
                if (instList.Count > jsrIdx + 1 &&
                    instList[jsrIdx].InsType == NCSInstructionType.JSR &&
                    instList[jsrIdx].Jump != null &&
                    instList[jsrIdx + 1].InsType == NCSInstructionType.RETN)
                {
                    // Valid entry stub pattern found
                    return true;
                }

                // Pattern 2: [RSADD*], JSR, RESTOREBP (entry stub with RESTOREBP, used by external compiler)
                // swkotor2.exe: 0x004eb750 - If RESTOREBP is followed by MOVSP+RETN+RETN at the end, it's cleanup code, not entry stub
                if (instList.Count > jsrIdx + 1 &&
                    instList[jsrIdx].InsType == NCSInstructionType.JSR &&
                    instList[jsrIdx].Jump != null &&
                    instList[jsrIdx + 1].InsType == NCSInstructionType.RESTOREBP)
                {
                    int restorebpIndex = jsrIdx + 1;
                    // Check if RESTOREBP is followed by cleanup code at the end of the file
                    if (IsRestorebpFollowedByCleanupCode(instList, restorebpIndex))
                    {
                        // This is cleanup code, not an entry stub
                        return false;
                    }
                    // Valid entry stub pattern found
                    return true;
                }

                // Fallback: Check if first instruction IS JSR (void main pattern without RSADD*)
                if (jsrOffset == 0 && // Only check if we didn't find RSADD* prefix
                    instList[startIndex].InsType == NCSInstructionType.JSR &&
                    instList[startIndex].Jump != null &&
                    instList.Count > startIndex + 1 &&
                    instList[startIndex + 1].InsType == NCSInstructionType.RETN)
                {
                    // Valid entry stub pattern found
                    return true;
                }

                return false;
            }

            // CRITICAL: Skip entry stub detection for include files
            // Include files have NO entry stub (JSR+RETN) - they're collections of functions
            // swkotor2.exe: Include files verified to have no entry stub in original engine bytecode
            if (!isIncludeFile && instructions.Count >= entryStubStart + 2)
            {
                Debug($"DEBUG NcsToAstConverter: Checking entry stub at {entryStubStart}: {instructions[entryStubStart].InsType}, next: {instructions[entryStubStart + 1].InsType}");

                // Check if entry stub starts with RSADD* (function returns a value)
                int jsrOffset = 0; // Offset to JSR instruction from entryStubStart
                if (IsRsaddInstruction(instructions[entryStubStart].InsType))
                {
                    entryReturnType = GetReturnTypeFromRsadd(instructions[entryStubStart].InsType);
                    jsrOffset = 1; // JSR is at position entryStubStart + 1
                    Debug($"DEBUG NcsToAstConverter: Entry stub has RSADD* prefix ({instructions[entryStubStart].InsType}), return type={entryReturnType}");
                }

                int jsrIdx = entryStubStart + jsrOffset;

                // Pattern 1: [RSADD*], JSR followed by RETN (simple entry stub)
                // Based on CalculateEntryStubEnd: Entry stub ends after RETN (exclusive index)
                if (instructions.Count > jsrIdx + 1 &&
                    instructions[jsrIdx].InsType == NCSInstructionType.JSR &&
                    instructions[jsrIdx].Jump != null &&
                    instructions[jsrIdx + 1].InsType == NCSInstructionType.RETN)
                {
                    try
                    {
                        entryJsrTarget = ncs.GetInstructionIndex(instructions[jsrIdx].Jump);
                        // Entry stub ends after RETN (exclusive index: jsrIdx + 2)
                        entryStubEnd = jsrIdx + 2;
                        Debug($"DEBUG NcsToAstConverter: Detected entry stub pattern ({(jsrOffset > 0 ? instructions[entryStubStart].InsType + "+" : "")}JSR+RETN) - JSR at {jsrIdx} targets {entryJsrTarget} (main), returnType={entryReturnType}, entryStubEnd={entryStubEnd}");
                    }
                    catch (Exception)
                    {
                    }
                }
                // Pattern 2: [RSADD*], JSR, RESTOREBP (entry stub with RESTOREBP, used by external compiler)
                // Based on CalculateEntryStubEnd: Entry stub ends after RESTOREBP (exclusive index)
                // swkotor2.exe: 0x004eb750 - If RESTOREBP is followed by MOVSP+RETN+RETN at the end, it's cleanup code, not entry stub
                else if (instructions.Count > jsrIdx + 1 &&
                         instructions[jsrIdx].InsType == NCSInstructionType.JSR &&
                         instructions[jsrIdx].Jump != null &&
                         instructions[jsrIdx + 1].InsType == NCSInstructionType.RESTOREBP)
                {
                    int restorebpIndex = jsrIdx + 1;
                    // Check if RESTOREBP is followed by cleanup code at the end of the file
                    if (IsRestorebpFollowedByCleanupCode(instructions, restorebpIndex))
                    {
                        // This is cleanup code, not an entry stub - don't set entryStubEnd
                        Debug($"DEBUG NcsToAstConverter: RESTOREBP at {restorebpIndex} is followed by cleanup code (MOVSP+RETN+RETN at end) - not an entry stub");
                    }
                    else
                    {
                        try
                        {
                            entryJsrTarget = ncs.GetInstructionIndex(instructions[jsrIdx].Jump);
                            // Entry stub ends after RESTOREBP (exclusive index: jsrIdx + 2)
                            entryStubEnd = jsrIdx + 2;
                            Debug($"DEBUG NcsToAstConverter: Detected entry stub pattern ({(jsrOffset > 0 ? instructions[entryStubStart].InsType + "+" : "")}JSR+RESTOREBP) - JSR at {jsrIdx} targets {entryJsrTarget} (main), returnType={entryReturnType}, entryStubEnd={entryStubEnd}");
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                // Fallback: Check if first instruction IS JSR (void main pattern without RSADD*)
                // Based on CalculateEntryStubEnd: Entry stub ends after RETN (exclusive index)
                else if (instructions[entryStubStart].InsType == NCSInstructionType.JSR &&
                         instructions[entryStubStart].Jump != null &&
                         instructions.Count > entryStubStart + 1 &&
                         instructions[entryStubStart + 1].InsType == NCSInstructionType.RETN)
                {
                    try
                    {
                        entryJsrTarget = ncs.GetInstructionIndex(instructions[entryStubStart].Jump);
                        entryReturnType = 0; // void
                        // Entry stub ends after RETN (exclusive index: entryStubStart + 2)
                        entryStubEnd = entryStubStart + 2;
                        Debug($"DEBUG NcsToAstConverter: Detected entry stub pattern (JSR+RETN, no RSADD*) - JSR at {entryStubStart} targets {entryJsrTarget} (main), returnType=void, entryStubEnd={entryStubEnd}");
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            else if (isIncludeFile)
            {
                // Include files have NO entry stub - skip detection
                Debug($"DEBUG NcsToAstConverter: Skipping entry stub detection for INCLUDE FILE (include files have no entry stub)");
            }

            for (int i = 0; i < instructions.Count; i++)
            {
                NCSInstruction inst = instructions[i];
                if (inst.InsType == NCSInstructionType.JSR && inst.Jump != null)
                {
                    try
                    {
                        int jumpIdx = ncs.GetInstructionIndex(inst.Jump);
                        // Exclude entry JSR target (main) and position 0 from subroutine starts
                        // Also exclude positions within globals range (0 to savebpIndex+1) and entry stub
                        // swkotor2.exe: 0x004eb750 - Entry stub pattern detection verified in original engine bytecode
                        // CRITICAL: Include files have NO entry stub, so skip entry stub calculation
                        int globalsAndStubEnd = (savebpIndex >= 0) ? savebpIndex + 1 : 0;
                        if (savebpIndex >= 0 && !isIncludeFile)
                        {
                            // Check for entry stub and extend globalsAndStubEnd using comprehensive pattern detection
                            // Skip for include files - they have no entry stub
                            globalsAndStubEnd = CalculateEntryStubEnd(instructions, globalsAndStubEnd, ncs);
                        }

                        // Comprehensive validation: Only add jumpIdx to subroutineStarts if it meets ALL criteria:
                        // 1. jumpIdx is strictly after globals/entry stub range (globalsAndStubEnd is exclusive end index)
                        //    - Instructions 0 to globalsAndStubEnd-1 are in globals/stub range
                        //    - Instruction globalsAndStubEnd is the first instruction AFTER the range
                        //    - Subroutines must start AFTER this boundary
                        // 2. jumpIdx is not the entry JSR target (main function entry point)
                        //    - The entry JSR target is the main() function, not a subroutine
                        //    - entryJsrTarget may be -1 if no entry stub was detected (include files, edge cases)
                        // 3. jumpIdx is within valid instruction range (0 to instructions.Count-1)
                        //    - Prevents out-of-bounds access
                        //    - A subroutine start at instructions.Count would be invalid
                        // swkotor2.exe: 0x004eb750 - Subroutine detection verified in original engine bytecode
                        // Based on original engine: Subroutines are identified by JSR targets that are:
                        // - After globals initialization (savebpIndex+1 or 0 if no SAVEBP)
                        // - After entry stub (JSR+RETN pattern) if present
                        // - Not the main function entry point (entryJsrTarget)
                        // - Within valid instruction bounds
                        bool isValidSubroutineStart = false;
                        string skipReason = null;

                        // Check 1: Validate jumpIdx is within bounds
                        if (jumpIdx < 0 || jumpIdx >= instructions.Count)
                        {
                            skipReason = $"out of bounds (jumpIdx={jumpIdx}, instructions.Count={instructions.Count})";
                        }
                        // Check 2: Validate jumpIdx is after globals/entry stub range
                        // globalsAndStubEnd is exclusive, so jumpIdx must be >= globalsAndStubEnd
                        // However, if jumpIdx == globalsAndStubEnd, it's the first instruction after the range
                        // which could be main() or a subroutine. We need to check if it's the entry target.
                        else if (jumpIdx < globalsAndStubEnd)
                        {
                            skipReason = $"in globals/stub range (jumpIdx={jumpIdx}, globalsAndStubEnd={globalsAndStubEnd})";
                        }
                        // Check 3: Validate jumpIdx is not the entry JSR target (main function)
                        // entryJsrTarget is -1 if no entry stub was detected (include files, edge cases)
                        // In that case, entryJsrTarget != jumpIdx is always true for valid indices
                        // CRITICAL: Only check if entryJsrTarget is valid (>= 0), otherwise skip this check
                        else if (entryJsrTarget >= 0 && jumpIdx == entryJsrTarget)
                        {
                            skipReason = $"is entry JSR target (main function at {entryJsrTarget})";
                        }
                        // All checks passed - this is a valid subroutine start
                        // jumpIdx is: within bounds, after globals/stub range, and not the entry JSR target
                        else
                        {
                            isValidSubroutineStart = true;
                        }

                        if (isValidSubroutineStart)
                        {
                            // Only add if not already in the set (HashSet prevents duplicates, but being explicit)
                            if (!subroutineStarts.Contains(jumpIdx))
                            {
                                subroutineStarts.Add(jumpIdx);
                                Debug($"DEBUG NcsToAstConverter: Found subroutine start at {jumpIdx} (JSR at {i} targets {jumpIdx}, globalsAndStubEnd={globalsAndStubEnd}, entryJsrTarget={entryJsrTarget})");
                            }
                            else
                            {
                                Debug($"DEBUG NcsToAstConverter: Subroutine start at {jumpIdx} already in set (JSR at {i} targets {jumpIdx})");
                            }
                        }
                        else
                        {
                            Debug($"DEBUG NcsToAstConverter: Skipping JSR at {i} targeting {jumpIdx} ({skipReason})");
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            int mainStart = 0;
            int alternativeMainStart = -1;
            int mainEnd = instructions.Count;
            bool mainStartIsAfterSavebp = false; // Flag to indicate mainStart was intentionally set to SAVEBP+1

            // Calculate mainEnd - it should be the minimum of all subroutine starts that are AFTER mainStart
            // But we need to calculate mainStart first, so we'll do this after mainStart is determined
            // mainEnd will be recalculated after mainStart is determined (see below after mainStart calculation)

            // If SAVEBP is found, create globals subroutine (0 to SAVEBP+1)
            // Then calculate where main should start (after globals and entry stub)
            // Main start calculation ensures main function always begins after globals initialization
            // and entry stub (JSR+RETN pattern), which is critical for correct AST generation.
            // The calculation handles multiple cases:
            // 1. Normal case: entryJsrTarget points to main function after entry stub
            // 2. Alternative case: JSR at position 0 targets main (when entry JSR targets last RETN)
            // 3. Entry stub wrapper case: Entry stub wraps main, main code starts at SAVEBP+1 or after stub
            // 4. Fallback case: Use entryStubEnd when entryJsrTarget is invalid or in globals range
            // All cases ensure mainStart >= entryStubEnd to maintain the invariant that main
            // always starts after globals and entry stub.
            // SKIP for include files - they don't have globals, each SAVEBP is a function start
            bool entryJsrTargetIsLastRetn = (entryJsrTarget >= 0 && entryJsrTarget == instructions.Count - 1);
            bool shouldDeferGlobals = false;
            int actionCountInGlobalsEarly = 0; // Declare outside if block for later use

            if (savebpIndex >= 0 && !isIncludeFile)
            {
                // CRITICAL: Count ACTION instructions in globals range BEFORE deciding whether to defer globals
                // This helps detect when main code is in the globals range (SAVEBP is very late)
                int actionCountInGlobalsForDefer = 0;
                for (int i = 0; i <= savebpIndex && i < instructions.Count; i++)
                {
                    if (instructions[i].InsType == NCSInstructionType.ACTION)
                    {
                        actionCountInGlobalsForDefer++;
                    }
                }
                Debug($"DEBUG NcsToAstConverter: ACTION instructions in globals range (0-{savebpIndex}): {actionCountInGlobalsForDefer}");

                // Check if entry JSR targets last RETN - if so, main code might be in globals range
                // Also check if there are ACTION instructions in globals - if so, main code might be in globals
                // In that case, we'll split globals later after determining main start
                bool mightNeedSplit = entryJsrTargetIsLastRetn || actionCountInGlobalsForDefer > 0;

                if (!mightNeedSplit)
                {
                    // Normal case: create globals subroutine from 0 to SAVEBP+1
                    ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, savebpIndex + 1, 0);
                    if (globalsSub != null)
                    {
                        program.GetSubroutine().Add(globalsSub);
                    }
                }
                else
                {
                    // Special case: entry JSR targets last RETN OR there are ACTION instructions in globals
                    // Defer globals creation - we'll create it after determining main start, splitting if needed
                    shouldDeferGlobals = true;
                    Debug($"DEBUG NcsToAstConverter: Deferring globals creation (entry JSR targets last RETN={entryJsrTargetIsLastRetn} OR actionCountInGlobalsForDefer={actionCountInGlobalsForDefer} > 0, may need to split)");
                }

                // Calculate where globals and entry stub end
                // swkotor2.exe: 0x004eb750 - Entry stub pattern detection verified in original engine bytecode
                int globalsEnd = savebpIndex + 1;
                int calculatedEntryStubEnd = CalculateEntryStubEnd(instructions, globalsEnd, ncs);
                if (calculatedEntryStubEnd > globalsEnd)
                {
                    Debug($"DEBUG NcsToAstConverter: Entry stub pattern detected at {globalsEnd}, entry stub ends at {calculatedEntryStubEnd}");
                }
                else
                {
                    Debug($"DEBUG NcsToAstConverter: No entry stub pattern found at {globalsEnd}, entry stub ends at {calculatedEntryStubEnd}");
                }

                // CRITICAL: Ensure mainStart is ALWAYS after globals and entry stub
                // If entryJsrTarget points to globals range (0 to calculatedEntryStubEnd), ignore it
                // Also ignore if entryJsrTarget points to the last RETN (likely wrong target)
                // The last RETN is typically at instructions.Count - 1
                bool entryJsrTargetIsLastRetn2 = (entryJsrTarget >= 0 && entryJsrTarget == instructions.Count - 1);

                // Special case: If entry JSR targets last RETN, check if there's a JSR at position 0
                // that might be the actual main entry point (common in some compiler outputs)
                // CRITICAL: Only use this if JSR at 0 targets a position AFTER SAVEBP, otherwise it's just
                // part of globals initialization (like in asd.nss where JSR 0->2 is globals, not main)
                if (entryJsrTargetIsLastRetn2 && instructions.Count > 0 &&
                    instructions[0].InsType == NCSInstructionType.JSR && instructions[0].Jump != null)
                {
                    try
                    {
                        int jsr0Target = ncs.GetInstructionIndex(instructions[0].Jump);
                        // FIXED: Only consider this as alternative main start if it's AFTER SAVEBP
                        // If it's before SAVEBP, it's part of globals initialization, not main
                        // Main function should be empty in that case (entry JSR targets last RETN)
                        if (jsr0Target > savebpIndex && jsr0Target < calculatedEntryStubEnd)
                        {
                            alternativeMainStart = jsr0Target;
                            Debug($"DEBUG NcsToAstConverter: Found alternative main start at {alternativeMainStart} (JSR at 0 targets {jsr0Target}, entry JSR targets last RETN, target is after SAVEBP)");
                        }
                        else if (jsr0Target <= savebpIndex)
                        {
                            Debug($"DEBUG NcsToAstConverter: JSR at 0 targets {jsr0Target} which is before/at SAVEBP ({savebpIndex}) - this is globals initialization, not main function");
                        }
                    }
                    catch (Exception)
                    {
                    }
                }

                if (entryJsrTarget >= 0 && entryJsrTarget > calculatedEntryStubEnd && !entryJsrTargetIsLastRetn2)
                {
                    // entryJsrTarget is valid and after entry stub and not the last RETN - use it
                    // swkotor2.exe: 0x004eb750 - Entry JSR target points to main function start
                    // This is the normal case: entry stub (JSR+RETN) calls main function at entryJsrTarget
                    // Validation: Ensure entryJsrTarget is within valid instruction range
                    if (entryJsrTarget < instructions.Count)
                    {
                        // Additional validation: Ensure entryJsrTarget doesn't point to another entry stub pattern
                        // This can happen in malformed scripts or edge cases
                        bool pointsToAnotherStub = false;
                        if (entryJsrTarget < instructions.Count - 1)
                        {
                            // Check if entryJsrTarget points to a JSR+RETN pattern (another entry stub)
                            if (instructions[entryJsrTarget].InsType == NCSInstructionType.JSR &&
                                entryJsrTarget + 1 < instructions.Count &&
                                instructions[entryJsrTarget + 1].InsType == NCSInstructionType.RETN)
                            {
                                pointsToAnotherStub = true;
                                Debug($"DEBUG NcsToAstConverter: WARNING - entryJsrTarget {entryJsrTarget} points to another entry stub pattern (JSR+RETN), this may indicate malformed script");
                            }
                        }

                        if (!pointsToAnotherStub)
                        {
                            // Valid entryJsrTarget - use it as mainStart
                            mainStart = entryJsrTarget;
                            Debug($"DEBUG NcsToAstConverter: Using entryJsrTarget {entryJsrTarget} as mainStart (after entry stub at {calculatedEntryStubEnd}, valid main function entry point)");
                        }
                        else
                        {
                            // entryJsrTarget points to another stub - fall back to calculatedEntryStubEnd
                            mainStart = calculatedEntryStubEnd;
                            Debug($"DEBUG NcsToAstConverter: entryJsrTarget {entryJsrTarget} points to another entry stub, falling back to calculatedEntryStubEnd {calculatedEntryStubEnd} as mainStart");
                        }
                    }
                    else
                    {
                        // entryJsrTarget is out of bounds - fall back to calculatedEntryStubEnd
                        mainStart = calculatedEntryStubEnd;
                        Debug($"DEBUG NcsToAstConverter: entryJsrTarget {entryJsrTarget} is out of bounds (instructions.Count={instructions.Count}), falling back to calculatedEntryStubEnd {calculatedEntryStubEnd} as mainStart");
                    }
                }
                else if (alternativeMainStart >= 0)
                {
                    // Use alternative main start from JSR at 0
                    // CRITICAL: Ensure mainStart is ALWAYS after globals and entry stub
                    // If alternativeMainStart is before calculatedEntryStubEnd, we must use calculatedEntryStubEnd instead
                    // This ensures the invariant that mainStart >= calculatedEntryStubEnd is maintained
                    if (alternativeMainStart < calculatedEntryStubEnd)
                    {
                        Debug($"DEBUG NcsToAstConverter: alternativeMainStart {alternativeMainStart} is before calculatedEntryStubEnd {calculatedEntryStubEnd}, correcting to calculatedEntryStubEnd to ensure mainStart is after globals and entry stub");
                        mainStart = calculatedEntryStubEnd;
                    }
                    else
                    {
                        mainStart = alternativeMainStart;
                        Debug($"DEBUG NcsToAstConverter: Using alternative mainStart {alternativeMainStart} (JSR at 0 target, entry JSR targets last RETN, after entry stub at {calculatedEntryStubEnd})");
                    }
                }
                else if (entryJsrTargetIsLastRetn2 && entryJsrTarget >= 0 && entryJsrTarget >= calculatedEntryStubEnd)
                {
                    // CRITICAL FIX: If entry JSR targets last RETN, determine where main code actually starts
                    // swkotor2.exe: 0x004eb750 - Entry stub (JSR+RETN) is just a wrapper - actual main code is after globals initialization
                    // The entry stub wraps the main function call, but the actual main code is after globals (SAVEBP+1)
                    // If there's only cleanup code after entry stub (MOVSP+RETN+RETN), main code is before entry stub
                    // If there's real code after entry stub, that's the main code

                    // Check if code after entry stub is cleanup code or real main code
                    bool isCleanupAfterStub = IsCodeAfterEntryStubCleanup(instructions, calculatedEntryStubEnd);

                    if (isCleanupAfterStub)
                    {
                        // Only cleanup code after entry stub - main code is before entry stub (after globals initialization)
                        // Entry stub is just a wrapper, actual main code starts at SAVEBP+1
                        // CRITICAL: However, mainStart must ALWAYS be after calculatedEntryStubEnd, so if SAVEBP+1 is before calculatedEntryStubEnd,
                        // we must use calculatedEntryStubEnd instead. This ensures mainStart is always after globals and entry stub.
                        int potentialMainStart = savebpIndex + 1;
                        if (potentialMainStart <= calculatedEntryStubEnd)
                        {
                            // SAVEBP+1 is within globals/entry stub range - must use calculatedEntryStubEnd instead
                            mainStart = calculatedEntryStubEnd;
                            Debug($"DEBUG NcsToAstConverter: entryJsrTarget {entryJsrTarget} is last RETN, code after entry stub at {calculatedEntryStubEnd} is cleanup code, but SAVEBP+1 ({potentialMainStart}) is before calculatedEntryStubEnd ({calculatedEntryStubEnd}) - using calculatedEntryStubEnd as mainStart to ensure it's after globals and entry stub");
                        }
                        else
                        {
                            // SAVEBP+1 is after entry stub - safe to use it
                            mainStart = potentialMainStart;
                            mainStartIsAfterSavebp = true; // Mark that mainStart was intentionally set to SAVEBP+1
                            Debug($"DEBUG NcsToAstConverter: entryJsrTarget {entryJsrTarget} is last RETN, code after entry stub at {calculatedEntryStubEnd} is cleanup code - entry stub is wrapper, main code starts at SAVEBP+1 ({mainStart})");
                        }
                    }
                    else
                    {
                        // There's actual main code after entry stub - use calculatedEntryStubEnd as mainStart
                        // This handles cases where entry stub wraps main but main code continues after stub
                        // CRITICAL: calculatedEntryStubEnd is exactly the boundary, so we use it as mainStart (it's the first instruction after the stub)
                        mainStart = calculatedEntryStubEnd;
                        Debug($"DEBUG NcsToAstConverter: entryJsrTarget {entryJsrTarget} is last RETN, code after entry stub at {calculatedEntryStubEnd} is real main code - using calculatedEntryStubEnd ({mainStart}) as mainStart");
                    }
                }
                else
                {
                    // entryJsrTarget is invalid, points to globals, or points to last RETN before entry stub - use calculatedEntryStubEnd
                    // CRITICAL: calculatedEntryStubEnd is exactly the boundary between globals/entry stub and main code
                    // This ensures mainStart is always at or after the entry stub end
                    mainStart = calculatedEntryStubEnd;
                    if (entryJsrTargetIsLastRetn)
                    {
                        Debug($"DEBUG NcsToAstConverter: entryJsrTarget {entryJsrTarget} points to last RETN before entry stub, using calculatedEntryStubEnd {calculatedEntryStubEnd} as mainStart");
                    }
                    else
                    {
                        Debug($"DEBUG NcsToAstConverter: entryJsrTarget {entryJsrTarget} invalid or in globals range, using calculatedEntryStubEnd {calculatedEntryStubEnd} as mainStart");
                    }
                }

                // CRITICAL VALIDATION: Ensure mainStart is ALWAYS after globals and entry stub (>= calculatedEntryStubEnd)
                // This is a final safety check to guarantee the invariant after all assignment paths
                if (mainStart < calculatedEntryStubEnd)
                {
                    Debug($"DEBUG NcsToAstConverter: WARNING - mainStart ({mainStart}) is before calculatedEntryStubEnd ({calculatedEntryStubEnd}), correcting to calculatedEntryStubEnd to ensure it's after globals and entry stub");
                    mainStart = calculatedEntryStubEnd;
                }
                else if (mainStart == entryStubEnd)
                {
                    // This is valid - entryStubEnd is the first instruction after the entry stub
                    Debug($"DEBUG NcsToAstConverter: mainStart ({mainStart}) equals entryStubEnd ({entryStubEnd}) - this is correct (first instruction after entry stub)");
                }
                else
                {
                    Debug($"DEBUG NcsToAstConverter: mainStart ({mainStart}) is after entryStubEnd ({entryStubEnd}) - this is correct");
                }
            }
            else
            {
                // No SAVEBP - no globals, main starts at 0 or after entry stub
                // CRITICAL: When there's no SAVEBP, entry stub is at position 0 (if present)
                // Based on swkotor2.exe: 0x004eb750 - Entry stub at position 0 when no SAVEBP
                // We need to detect entry stub at position 0 and skip it when setting mainStart
                // CRITICAL: Include files have NO entry stub, so skip detection for include files

                // Calculate entry stub end at position 0 (if entry stub exists)
                // entryStubStart is already set to 0 when there's no SAVEBP (line 340)
                // Skip for include files - they have no entry stub
                int entryStubEndNoSavebp = isIncludeFile ? entryStubStart : CalculateEntryStubEnd(instructions, entryStubStart, ncs);

                if (entryStubEndNoSavebp > entryStubStart)
                {
                    // Entry stub detected at position 0
                    entryStubEnd = entryStubEndNoSavebp;
                    Debug($"DEBUG NcsToAstConverter: No SAVEBP - Entry stub detected at position 0, entry stub ends at {entryStubEnd}");

                    // Main starts after entry stub (or at entryJsrTarget if valid and after stub)
                    if (entryJsrTarget >= 0 && entryJsrTarget >= entryStubEnd)
                    {
                        // entryJsrTarget is valid and after entry stub - use it
                        mainStart = entryJsrTarget;
                        Debug($"DEBUG NcsToAstConverter: No SAVEBP - Using entryJsrTarget {entryJsrTarget} as mainStart (after entry stub at {entryStubEnd})");
                    }
                    else if (entryJsrTarget >= 0 && entryJsrTarget < entryStubEnd)
                    {
                        // entryJsrTarget is within entry stub - this shouldn't happen, but use entryStubEnd
                        mainStart = entryStubEnd;
                        Debug($"DEBUG NcsToAstConverter: No SAVEBP - entryJsrTarget {entryJsrTarget} is within entry stub (ends at {entryStubEnd}), using entryStubEnd as mainStart");
                    }
                    else
                    {
                        // entryJsrTarget is invalid - use entry stub end
                        mainStart = entryStubEnd;
                        Debug($"DEBUG NcsToAstConverter: No SAVEBP - entryJsrTarget invalid, using entryStubEnd {entryStubEnd} as mainStart");
                    }
                }
                else
                {
                    // No entry stub detected at position 0
                    // Main starts at 0 or entryJsrTarget (if valid)
                    if (entryJsrTarget >= 0 && entryJsrTarget > 0)
                    {
                        mainStart = entryJsrTarget;
                        Debug($"DEBUG NcsToAstConverter: No SAVEBP - No entry stub at position 0, using entryJsrTarget {entryJsrTarget} as mainStart");
                    }
                    else
                    {
                        // No entry stub and no valid entryJsrTarget - main starts at 0
                        mainStart = 0;
                        Debug($"DEBUG NcsToAstConverter: No SAVEBP - No entry stub at position 0 and no valid entryJsrTarget, mainStart=0");
                    }
                }
            }

            // Only create main subroutine if mainStart is valid and after globals
            // If mainStart is 0 or within globals range, main should be empty
            // CRITICAL: Include files have NO entry stub, so skip entry stub calculation
            int globalsEndForMain = (savebpIndex >= 0) ? savebpIndex + 1 : 0;
            if (savebpIndex >= 0 && !isIncludeFile)
            {
                // Check for entry stub and adjust globalsEndForMain using comprehensive pattern detection
                // swkotor2.exe: 0x004eb750 - Entry stub pattern detection verified in original engine bytecode
                // Skip for include files - they have no entry stub
                globalsEndForMain = CalculateEntryStubEnd(instructions, globalsEndForMain, ncs);
            }

            // CRITICAL: Don't adjust mainStart here if we might need to split globals later
            // The split logic (which runs later) will set mainStart correctly if main code is in globals
            // Only adjust mainStart if we're NOT deferring globals (normal case where main is after globals)
            // If shouldDeferGlobals is true, it means we detected ACTION instructions in globals, so we'll split later
            bool isAlternativeMainStart = (alternativeMainStart >= 0 && mainStart == alternativeMainStart);
            int mainStartBeforeAdjustment = mainStart;

            // Only adjust if we're NOT deferring globals (normal case)
            // If shouldDeferGlobals is true, the split logic will handle mainStart adjustment
            if (!shouldDeferGlobals && mainStart <= globalsEndForMain && !isAlternativeMainStart)
            {
                mainStart = globalsEndForMain;
                Debug($"DEBUG NcsToAstConverter: Final adjustment: mainStart set to {mainStart} (after globals/entry stub at {globalsEndForMain}, was {mainStartBeforeAdjustment})");
            }
            else if (isAlternativeMainStart)
            {
                Debug($"DEBUG NcsToAstConverter: Keeping alternative mainStart {mainStart} (in globals range, entry JSR targets last RETN)");
            }
            else if (shouldDeferGlobals)
            {
                Debug($"DEBUG NcsToAstConverter: Deferring mainStart adjustment (shouldDeferGlobals=true, split logic will set mainStart correctly)");
            }

            // Calculate mainEnd based on subroutine starts that are AFTER mainStart
            // mainEnd should be the minimum of all subroutine starts that are AFTER mainStart
            // If there are no subroutines after mainStart, mainEnd should be instructions.Count (end of file)
            // This ensures main function includes all instructions from mainStart to either:
            // 1. The first subroutine start after mainStart (if subroutines exist and are boundaries), OR
            // 2. The end of the file (instructions.Count) if no subroutines exist after mainStart
            // NOTE: Subroutines are typically separate functions called from main, so they don't truncate main.
            // However, in some cases (e.g., inline subroutines or specific compiler patterns), subroutines
            // may serve as boundaries. The calculation below handles both cases by finding the minimum
            // subroutine start after mainStart, but the split globals logic and final check below will
            // ensure mainEnd is set correctly based on the actual code structure.
            if (subroutineStarts.Count > 0 && mainStart >= 0)
            {
                // Find all subroutine starts that are AFTER mainStart
                List<int> subStartsAfterMain = subroutineStarts.Where(subStart => subStart > mainStart).ToList();
                if (subStartsAfterMain.Count > 0)
                {
                    // Set mainEnd to the minimum (first) subroutine start after mainStart
                    int minSubStartAfterMain = subStartsAfterMain.Min();
                    mainEnd = minSubStartAfterMain;
                    Debug($"DEBUG NcsToAstConverter: Calculated mainEnd={mainEnd} as minimum subroutine start after mainStart={mainStart} (found {subStartsAfterMain.Count} subroutines after main: {string.Join(", ", subStartsAfterMain.OrderBy(x => x))})");
                }
                else
                {
                    // No subroutines after mainStart - main should include all instructions to end of file
                    mainEnd = instructions.Count;
                    Debug($"DEBUG NcsToAstConverter: No subroutines found after mainStart={mainStart}, setting mainEnd={mainEnd} (end of file, {instructions.Count} instructions)");
                }
            }
            else
            {
                // No subroutines at all, or mainStart is invalid - main should include all instructions
                mainEnd = instructions.Count;
                if (subroutineStarts.Count == 0)
                {
                    Debug($"DEBUG NcsToAstConverter: No subroutines found in file, setting mainEnd={mainEnd} (end of file, {instructions.Count} instructions)");
                }
                else
                {
                    Debug($"DEBUG NcsToAstConverter: mainStart={mainStart} is invalid, setting mainEnd={mainEnd} (end of file, {instructions.Count} instructions)");
                }
            }

            // CRITICAL: Main function should include ALL instructions from mainStart
            // to the last RETN (instructions.Count), not just up to the first subroutine start.
            // Subroutines are separate functions called from main, but they don't truncate the main function.
            // NOTE: If mainStart is in globals range (alternative main start), mainEnd will be updated
            // in the split globals logic below to include SAVEBP
            // The calculation above provides an initial mainEnd value, but the split globals logic
            // and final check below will ensure mainEnd is set correctly based on the actual code structure.
            Debug($"DEBUG NcsToAstConverter: mainEnd INITIALLY calculated as {mainEnd} (mainStart={mainStart}, instructions.Count={instructions.Count})");
            if (isAlternativeMainStart && savebpIndex >= 0)
            {
                // Alternative main start is in globals range - split logic below will update mainEnd if needed
                // But we still want mainEnd to include ALL instructions, not just up to SAVEBP
                Debug($"DEBUG NcsToAstConverter: Alternative main start detected (mainStart={mainStart} in globals range), mainEnd={mainEnd} will be updated by split logic if needed");
            }
            if (subroutineStarts.Count > 0)
            {
                List<int> sortedSubStarts = new List<int>(subroutineStarts);
                sortedSubStarts.Sort();
                Debug($"DEBUG NcsToAstConverter: Found {subroutineStarts.Count} subroutine starts: {string.Join(", ", sortedSubStarts)} (these are separate functions, not boundaries for main)");
            }

            // If we deferred globals creation (entry JSR targets last RETN), create it now
            // Also check if main code is in globals even when globals were created normally
            // Split globals if mainStart is in the globals range
            // CRITICAL: Include files have NO entry stub, so skip entry stub calculation
            if (savebpIndex >= 0 && !isIncludeFile)
            {
                // Calculate entryStubEndInner using comprehensive entry stub pattern detection
                // swkotor2.exe: 0x004eb750 - Entry stub pattern detection verified in original engine bytecode
                // Skip for include files - they have no entry stub
                int globalsEnd = savebpIndex + 1;
                int entryStubEndInner = CalculateEntryStubEnd(instructions, globalsEnd, ncs);
                if (entryStubEndInner > globalsEnd)
                {
                    Debug($"DEBUG NcsToAstConverter: Entry stub pattern detected at {globalsEnd}, entry stub ends at {entryStubEndInner}");
                }

                // If globals were created normally OR deferred, check if we need to split them
                // This handles cases where main code is in the globals range
                Debug($"DEBUG NcsToAstConverter: Checking if we need to split globals - shouldDeferGlobals={shouldDeferGlobals}, savebpIndex={savebpIndex}, mainStart={mainStart}, entryStubEndInner={entryStubEndInner}");

                // Check if main code is in globals range - this applies to BOTH normal and deferred cases
                // When entry JSR targets last RETN, main code is often in the globals range (0 to SAVEBP)
                bool mainCodeInGlobals = false;
                bool entryJsrTargetIsLastRetnCheck = (entryJsrTarget >= 0 && entryJsrTarget == instructions.Count - 1);

                // First, scan the globals range (0 to SAVEBP) to see what instruction types are there
                Debug($"DEBUG NcsToAstConverter: Scanning globals range (0-{savebpIndex}) for instruction types");
                int actionCountInGlobals = 0;
                for (int i = 0; i <= savebpIndex && i < instructions.Count; i++)
                {
                    if (instructions[i].InsType == NCSInstructionType.ACTION)
                    {
                        actionCountInGlobals++;
                        if (actionCountInGlobals <= 5) // Log first 5 ACTION instructions
                        {
                            Debug($"DEBUG NcsToAstConverter: Found ACTION instruction at index {i} in globals range (0-{savebpIndex})");
                        }
                    }
                }
                Debug($"DEBUG NcsToAstConverter: Total ACTION instructions in globals range (0-{savebpIndex}): {actionCountInGlobals}");

                // CRITICAL FIX: Always check if main code is in globals range when SAVEBP is very late
                // This handles files where SAVEBP is near the end and all main code is before SAVEBP
                // The condition should check if there are ACTION instructions in globals AND no ACTION after SAVEBP
                // CRITICAL: Always check this - if shouldDeferGlobals is true, it means we detected ACTION instructions in globals
                // Also check if actionCountInGlobals > 0 (which means main code is definitely in globals)
                bool shouldCheckMainInGlobals = true; // Always check - we need to detect when main code is in globals

                if (shouldCheckMainInGlobals)
                {
                    // Check if main code is actually in the globals range (0 to SAVEBP)
                    // If mainStart is at or after entryStubEndInner and there are no ACTION instructions after SAVEBP+1,
                    // the main code must be in the globals range
                    Debug($"DEBUG NcsToAstConverter: Checking if main code is in globals - entryJsrTarget={entryJsrTarget}, instructions.Count-1={instructions.Count - 1}, entryJsrTargetIsLastRetnCheck={entryJsrTargetIsLastRetnCheck}, mainStart={mainStart}, entryStubEndInner={entryStubEndInner}, savebpIndex={savebpIndex}, actionCountInGlobals={actionCountInGlobals}, shouldCheckMainInGlobals={shouldCheckMainInGlobals}");

                    // Check if there are ACTION instructions in the range from SAVEBP+1 to last RETN
                    int actionCount = 0;
                    int checkStart = savebpIndex + 1;
                    for (int i = checkStart; i < instructions.Count - 1; i++)
                    {
                        if (instructions[i].InsType == NCSInstructionType.ACTION)
                        {
                            actionCount++;
                            Debug($"DEBUG NcsToAstConverter: Found ACTION instruction at index {i} in range {checkStart} to {instructions.Count - 1}");
                        }
                    }

                    // CRITICAL: If there are ACTION instructions in globals but none after SAVEBP, main code is in globals
                    // Also check if shouldDeferGlobals is true (which means actionCountInGlobalsForDefer > 0 was detected)
                    // CRITICAL FIX: Always check this condition, not just when shouldCheckMainInGlobals is true
                    // This ensures we detect when main code is in globals even when shouldDeferGlobals is true
                    Debug($"DEBUG NcsToAstConverter: Checking split condition - actionCount={actionCount}, actionCountInGlobals={actionCountInGlobals}, shouldDeferGlobals={shouldDeferGlobals}");
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: Checking split condition - actionCount={actionCount}, actionCountInGlobals={actionCountInGlobals}, shouldDeferGlobals={shouldDeferGlobals}, condition={actionCount == 0 && (actionCountInGlobals > 0 || shouldDeferGlobals)}");
                    if (actionCount == 0 && (actionCountInGlobals > 0 || shouldDeferGlobals))
                    {
                        // No ACTION instructions between SAVEBP+1 and last RETN
                        // Main function code must be in the globals range (0 to SAVEBP) - need to split
                        int mainCodeStartInGlobals = -1;
                        for (int i = 0; i <= savebpIndex; i++)
                        {
                            if (instructions[i].InsType == NCSInstructionType.ACTION)
                            {
                                mainCodeStartInGlobals = i;
                                Debug($"DEBUG NcsToAstConverter: Found first ACTION instruction in globals range at index {i}");
                                break;
                            }
                        }
                        if (mainCodeStartInGlobals >= 0)
                        {
                            mainCodeInGlobals = true;
                            Debug($"DEBUG NcsToAstConverter: No ACTION instructions found between SAVEBP+1 ({checkStart}) and last RETN ({instructions.Count - 1}), but found ACTION at {mainCodeStartInGlobals} in globals range (0-{savebpIndex}) - will split globals at {mainCodeStartInGlobals}");
                            Console.Error.WriteLine($"DEBUG NcsToAstConverter: SPLIT LOGIC RUNNING - mainCodeStartInGlobals={mainCodeStartInGlobals}, will set mainStart to this value");
                        }
                        else
                        {
                            // CRITICAL FIX: No ACTION instructions found ANYWHERE in the file!
                            // This happens for scripts that only include other files with globals, but have an empty main().
                            // Example: k_act_com41.nss has #include statements that bring in globals, but main() is empty (all code commented out)
                            // In this case:
                            // - All instructions from 0 to savebpIndex are global variable declarations (RSADDI, CONSTI, NEGI, etc.)
                            // - Instructions from savebpIndex+1 to end are the empty main() (SAVEBP at savebpIndex + entry stub + RETN)
                            // Based on nwnnsscomp.exe: Empty main() contains entry stub (JSR+RETN or RSADD*+JSR+RETN),
                            // possibly cleanup code (MOVSP+RETN+RETN), and final RETN
                            // CRITICAL: Even though main() is "empty" (no ACTION), it still contains ALL instructions from SAVEBP+1 to the end

                            // Identify empty main() structure using helper function
                            bool isEmptyMain = IsEmptyMainFunction(instructions, savebpIndex, ncs);
                            EmptyMainStructure emptyMainStruct = AnalyzeEmptyMainStructure(instructions, savebpIndex, ncs);

                            Console.Error.WriteLine($"DEBUG NcsToAstConverter: EMPTY MAIN CASE - No ACTION instructions found anywhere! actionCount={actionCount}, actionCountInGlobals={actionCountInGlobals}, shouldDeferGlobals={shouldDeferGlobals}, isEmptyMain={isEmptyMain}");
                            Debug($"DEBUG NcsToAstConverter: EMPTY MAIN CASE - No ACTION instructions found anywhere!");
                            Debug($"DEBUG NcsToAstConverter: This is a script with globals only and an empty main(). Creating globals subroutine and empty main.");
                            Debug($"DEBUG NcsToAstConverter: EMPTY MAIN CASE - instructions.Count={instructions.Count}, savebpIndex={savebpIndex}, will create main from {savebpIndex + 1} to {instructions.Count}");

                            if (isEmptyMain)
                            {
                                Debug($"DEBUG NcsToAstConverter: Empty main() structure identified:");
                                Debug($"  - Entry stub: {emptyMainStruct.HasEntryStub} (start={emptyMainStruct.EntryStubStart}, end={emptyMainStruct.EntryStubEnd})");
                                Debug($"  - Cleanup code: {emptyMainStruct.HasCleanupCode} (start={emptyMainStruct.CleanupCodeStart}, end={emptyMainStruct.CleanupCodeEnd})");
                                Debug($"  - Final RETN: index={emptyMainStruct.FinalRetnIndex}");
                            }

                            // Create globals subroutine with all instructions up to SAVEBP
                            if (savebpIndex >= 0)
                            {
                                int globalsInitEnd = savebpIndex + 1; // Include all instructions up to SAVEBP
                                ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsInitEnd, 0);
                                if (globalsSub != null)
                                {
                                    program.GetSubroutine().Add(globalsSub);
                                    Debug($"DEBUG NcsToAstConverter: Created globals subroutine for EMPTY MAIN case (range 0-{globalsInitEnd}, {globalsInitEnd} instructions)");
                                }

                                // Set mainStart and mainEnd for the empty main function
                                // CRITICAL FIX: The main function starts right after SAVEBP and goes to the END (instructions.Count)
                                // Based on nwnnsscomp.exe: Empty main() contains ALL instructions from SAVEBP+1 to the end:
                                // - Entry stub (JSR+RETN or RSADD*+JSR+RETN) at savebpIndex+1
                                // - Possibly cleanup code (MOVSP+RETN+RETN) after entry stub
                                // - Final RETN at the end
                                // We MUST include ALL instructions from SAVEBP+1 to the end, not just a few
                                mainStart = savebpIndex + 1;
                                mainEnd = instructions.Count; // CRITICAL: Must be instructions.Count, not a smaller value!
                                mainCodeInGlobals = true; // Mark as handled so we don't fall into other branches
                                Debug($"DEBUG NcsToAstConverter: Set mainStart={mainStart}, mainEnd={mainEnd} for EMPTY MAIN case (main includes ALL {mainEnd - mainStart} instructions from SAVEBP+1 to end)");
                                Console.Error.WriteLine($"DEBUG NcsToAstConverter: EMPTY MAIN CASE - mainStart={mainStart}, mainEnd={mainEnd}, instructions.Count={instructions.Count}, main will include {mainEnd - mainStart} instructions");
                            }
                            else
                            {
                                Debug($"DEBUG NcsToAstConverter: WARNING - savebpIndex={savebpIndex}, cannot create globals subroutine!");
                            }
                        }
                        if (mainCodeStartInGlobals >= 0)
                        {

                            // Remove the globals subroutine that was created earlier (it includes main code)
                            // We'll recreate it with the correct split
                            var subroutines = program.GetSubroutine();
                            for (int i = subroutines.Count - 1; i >= 0; i--)
                            {
                                var s = subroutines[i];
                                if (s is ASubroutine)
                                {
                                    var aSub = (ASubroutine)s;
                                    if (aSub.GetId() == 0)
                                    {
                                        subroutines.RemoveAt(i);
                                    }
                                }
                            }
                            Debug($"DEBUG NcsToAstConverter: Removed globals subroutine(s) that included main code");

                            // Create split globals
                            // CRITICAL: Globals must include ALL instructions up to SAVEBP, not just up to first ACTION
                            // This ensures RSADDI and other instructions before SAVEBP are included in globals
                            // Globals: 0 to savebpIndex+1 (includes all variable initialization up to SAVEBP)
                            // Main: mainCodeStartInGlobals to last RETN (includes main code and everything after)
                            int globalsInitEnd = savebpIndex + 1; // Always include all instructions up to SAVEBP
                            ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsInitEnd, 0);
                            if (globalsSub != null)
                            {
                                program.GetSubroutine().Add(globalsSub);
                                Debug($"DEBUG NcsToAstConverter: Created split globals subroutine (range 0-{globalsInitEnd}, includes all instructions up to SAVEBP, first ACTION at {mainCodeStartInGlobals})");
                            }

                            // Update mainStart to include the main function code (from first ACTION to last RETN)
                            mainStart = mainCodeStartInGlobals;
                            mainEnd = instructions.Count;
                            Debug($"DEBUG NcsToAstConverter: Updated mainStart to {mainStart} and mainEnd to {mainEnd} (main includes all code from first ACTION at {mainStart} to last RETN)");
                            Console.Error.WriteLine($"DEBUG NcsToAstConverter: SPLIT LOGIC SET mainStart={mainStart}, mainEnd={mainEnd}");
                        }
                    }
                }

                // CRITICAL FIX: Only create deferred globals if mainCodeInGlobals is false
                // If mainCodeInGlobals is true, the split logic above already created the globals and set mainStart/mainEnd
                if (shouldDeferGlobals && !mainCodeInGlobals && mainStart < savebpIndex + 1 && mainStart > 0)
                {
                    // Split globals:
                    // CRITICAL: Globals must include ALL instructions up to SAVEBP, not just up to mainStart
                    // This ensures RSADDI and other instructions before SAVEBP are included in globals
                    // - Globals: 0 to savebpIndex+1 (includes all variable initialization up to SAVEBP)
                    // - Main: mainStart to last RETN (includes main code and everything after)
                    // NOTE: There will be overlap (mainStart to SAVEBP), but that's handled by the main function
                    int globalsInitEnd = savebpIndex + 1; // Always include all instructions up to SAVEBP
                    ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsInitEnd, 0);
                    if (globalsSub != null)
                    {
                        program.GetSubroutine().Add(globalsSub);
                        Debug($"DEBUG NcsToAstConverter: Created split globals subroutine (range 0-{globalsInitEnd}, includes all instructions up to SAVEBP, mainStart={mainStart})");
                    }
                    // Update mainEnd to include all instructions up to the last RETN
                    // CRITICAL: mainEnd must be instructions.Count to include ALL instructions, not just up to SAVEBP+1
                    mainEnd = instructions.Count;
                    Debug($"DEBUG NcsToAstConverter: Updated mainEnd to {mainEnd} (all {instructions.Count} instructions, includes main code from {mainStart} to last RETN)");
                }
                else
                {
                    // Main start is at or after entry stub end - comprehensive handling
                    // CRITICAL: When mainStart >= entryStubEnd, the main function code might actually be in the globals range
                    // This happens when entry JSR targets last RETN and there's no actual code between entryStubEnd and last RETN
                    // Check if there's actual meaningful code between entryStubEnd and the last RETN
                    // If not (just entry stub + cleanup patterns like MOVSP/RETN), the main code is in globals and we need to split
                    // swkotor2.exe: 0x004eb750 - Entry stub pattern detection and main code location verified in original engine bytecode
                    bool mainCodeInGlobalsInner = false;
                    bool entryJsrTargetIsLastRetnCheckInner = (entryJsrTarget >= 0 && entryJsrTarget == instructions.Count - 1);
                    Debug($"DEBUG NcsToAstConverter: Checking if main code is in globals - entryJsrTarget={entryJsrTarget}, instructions.Count-1={instructions.Count - 1}, entryJsrTargetIsLastRetnCheck={entryJsrTargetIsLastRetnCheckInner}, mainStart={mainStart}, entryStubEnd={entryStubEnd}, globalsEndForMain={globalsEndForMain}, savebpIndex={savebpIndex}, mainStartBeforeAdjustment={mainStartBeforeAdjustment}");

                    // CRITICAL: Check whenever mainStart >= entryStubEnd (not just when entryJsrTargetIsLastRetnCheckInner)
                    // This handles the general case where mainStart is at or after entry stub end
                    if (mainStart >= entryStubEnd && savebpIndex >= 0 && entryStubEnd > savebpIndex + 1)
                    {
                        // Check if there's actual meaningful code between entryStubEnd and last RETN
                        // This includes the entry stub area and everything up to the last RETN
                        // Meaningful code includes: ACTION, JSR (to functions, not entry stub), JMP, JZ, JNZ, constants, arithmetic, etc.
                        // Exclude: Entry stub patterns (already accounted for), cleanup patterns (MOVSP+RETN+RETN), and stack management only
                        int meaningfulCodeCount = 0;
                        int checkStart = entryStubEnd; // Start checking from right after entry stub
                        int checkEnd = instructions.Count - 1; // End at last RETN (exclusive)

                        Debug($"DEBUG NcsToAstConverter: Checking for meaningful code between entryStubEnd ({entryStubEnd}) and last RETN ({checkEnd})");

                        for (int i = checkStart; i < checkEnd; i++)
                        {
                            if (i >= instructions.Count)
                            {
                                break;
                            }

                            NCSInstructionType insType = instructions[i].InsType;

                            // Count meaningful instructions that indicate real main code (not just entry stub or cleanup)
                            // ACTION: Function calls (definitely meaningful)
                            if (insType == NCSInstructionType.ACTION)
                            {
                                meaningfulCodeCount++;
                                Debug($"DEBUG NcsToAstConverter: Found ACTION instruction at index {i} between entryStubEnd and last RETN");
                            }
                            // JSR: Function calls (but exclude entry stub JSR which is already accounted for)
                            else if (insType == NCSInstructionType.JSR)
                            {
                                // Check if this JSR is part of the entry stub (already counted)
                                // Entry stub JSR is at savebpIndex+1 (with possible RSADD* prefix) up to entryStubEnd
                                if (i >= entryStubEnd)
                                {
                                    // This JSR is after entry stub, so it's meaningful code
                                    meaningfulCodeCount++;
                                    Debug($"DEBUG NcsToAstConverter: Found JSR instruction at index {i} (after entry stub) between entryStubEnd and last RETN");
                                }
                            }
                            // Control flow: JMP, JZ, JNZ (definitely meaningful)
                            else if (insType == NCSInstructionType.JMP ||
                                     insType == NCSInstructionType.JZ ||
                                     insType == NCSInstructionType.JNZ)
                            {
                                meaningfulCodeCount++;
                                Debug($"DEBUG NcsToAstConverter: Found control flow instruction ({insType}) at index {i} between entryStubEnd and last RETN");
                            }
                            // Constants: CONSTI, CONSTF, CONSTS, CONSTO (indicates real code, not just stack management)
                            else if (insType == NCSInstructionType.CONSTI ||
                                     insType == NCSInstructionType.CONSTF ||
                                     insType == NCSInstructionType.CONSTS ||
                                     insType == NCSInstructionType.CONSTO)
                            {
                                meaningfulCodeCount++;
                                Debug($"DEBUG NcsToAstConverter: Found constant instruction ({insType}) at index {i} between entryStubEnd and last RETN");
                            }
                            // Arithmetic and logical operations (indicate real code)
                            else if (insType == NCSInstructionType.ADDII ||
                                     insType == NCSInstructionType.SUBII ||
                                     insType == NCSInstructionType.MULII ||
                                     insType == NCSInstructionType.DIVII ||
                                     insType == NCSInstructionType.MODII ||
                                     insType == NCSInstructionType.NEGI ||
                                     insType == NCSInstructionType.EQUALII ||
                                     insType == NCSInstructionType.NEQUALII ||
                                     insType == NCSInstructionType.GTII ||
                                     insType == NCSInstructionType.GEQII ||
                                     insType == NCSInstructionType.LTII ||
                                     insType == NCSInstructionType.LEQII ||
                                     insType == NCSInstructionType.LOGANDII ||
                                     insType == NCSInstructionType.LOGORII ||
                                     insType == NCSInstructionType.NOTI)
                            {
                                meaningfulCodeCount++;
                                Debug($"DEBUG NcsToAstConverter: Found arithmetic/logical instruction ({insType}) at index {i} between entryStubEnd and last RETN");
                            }
                            // Note: We exclude stack management only instructions like MOVSP, RSADD*, CPDOWNSP, etc.
                            // as they might be part of cleanup patterns or variable initialization
                        }

                        // Also check if code after entry stub is just cleanup code
                        bool isJustCleanupCode = IsCodeAfterEntryStubCleanup(instructions, entryStubEnd);

                        Debug($"DEBUG NcsToAstConverter: Meaningful code count between entryStubEnd ({entryStubEnd}) and last RETN ({checkEnd}): {meaningfulCodeCount}, isJustCleanupCode={isJustCleanupCode}");

                        // If there's no meaningful code between entryStubEnd and last RETN (or it's just cleanup),
                        // the main code must be in the globals range (0 to SAVEBP) - need to split
                        if (meaningfulCodeCount == 0 || isJustCleanupCode)
                        {
                            Debug($"DEBUG NcsToAstConverter: No meaningful code found between entryStubEnd ({entryStubEnd}) and last RETN ({checkEnd}), checking if main code is in globals range");

                            // Find where the main code actually starts by looking for the first ACTION instruction in the 0 to SAVEBP range
                            int mainCodeStartInGlobals = -1;
                            for (int i = 0; i <= savebpIndex; i++)
                            {
                                if (instructions[i].InsType == NCSInstructionType.ACTION)
                                {
                                    mainCodeStartInGlobals = i;
                                    Debug($"DEBUG NcsToAstConverter: Found first ACTION instruction in globals range at index {i}");
                                    break;
                                }
                            }

                            if (mainCodeStartInGlobals >= 0)
                            {
                                mainCodeInGlobalsInner = true;
                                Debug($"DEBUG NcsToAstConverter: No meaningful code found between entryStubEnd ({entryStubEnd}) and last RETN ({checkEnd}), but found ACTION at {mainCodeStartInGlobals} in globals range (0-{savebpIndex}) - will split globals at {mainCodeStartInGlobals}");
                            }
                            else
                            {
                                Debug($"DEBUG NcsToAstConverter: No meaningful code found between entryStubEnd and last RETN, and no ACTION instructions in globals range - file may be empty or have empty main");
                            }
                        }
                        else
                        {
                            Debug($"DEBUG NcsToAstConverter: Found {meaningfulCodeCount} meaningful instructions between entryStubEnd ({entryStubEnd}) and last RETN ({checkEnd}), main code is after entry stub");
                        }
                    }
                    // Special case: When entry JSR targets last RETN AND mainStart >= entryStubEnd, also check for ACTION instructions
                    // This is a more specific check that complements the general check above
                    else if (entryJsrTargetIsLastRetnCheckInner && mainStart >= entryStubEnd && savebpIndex >= 0)
                    {
                        // Check if there are ACTION instructions in the range from SAVEBP+1 to last RETN
                        // This includes the entry stub area and everything up to the last RETN
                        // If there are no ACTION instructions in this entire range, the main code must be in the globals range (0 to SAVEBP)
                        int actionCount = 0;
                        int checkStart = savebpIndex + 1; // Start checking from right after SAVEBP
                        for (int i = checkStart; i < instructions.Count - 1; i++)
                        {
                            if (instructions[i].InsType == NCSInstructionType.ACTION)
                            {
                                actionCount++;
                                Debug($"DEBUG NcsToAstConverter: Found ACTION instruction at index {i} in range {checkStart} to {instructions.Count - 1}");
                            }
                        }
                        if (actionCount == 0 && !mainCodeInGlobalsInner)
                        {
                            // No ACTION instructions between SAVEBP+1 and last RETN
                            // Main function code must be in the globals range (0 to SAVEBP) - need to split
                            // Find where the main code actually starts by looking for the first ACTION instruction in the 0 to SAVEBP range
                            int mainCodeStartInGlobals = -1;
                            for (int i = 0; i <= savebpIndex; i++)
                            {
                                if (instructions[i].InsType == NCSInstructionType.ACTION)
                                {
                                    mainCodeStartInGlobals = i;
                                    Debug($"DEBUG NcsToAstConverter: Found first ACTION instruction in globals range at index {i}");
                                    break;
                                }
                            }
                            if (mainCodeStartInGlobals >= 0)
                            {
                                mainCodeInGlobalsInner = true;
                                Debug($"DEBUG NcsToAstConverter: No ACTION instructions found between SAVEBP+1 ({checkStart}) and last RETN ({instructions.Count - 1}), but found ACTION at {mainCodeStartInGlobals} in globals range (0-{savebpIndex}) - will split globals at {mainCodeStartInGlobals}");
                            }
                            else
                            {
                                Debug($"DEBUG NcsToAstConverter: No ACTION instructions found anywhere - file may be empty or malformed");
                            }
                        }
                        else if (actionCount > 0)
                        {
                            Debug($"DEBUG NcsToAstConverter: Found {actionCount} ACTION instructions between SAVEBP+1 ({checkStart}) and last RETN ({instructions.Count - 1}), main code is after SAVEBP");
                        }
                    }

                    // CRITICAL FIX: Don't run duplicate split logic if mainCodeInGlobals is already true
                    // The split logic at line 698 should have already handled this case
                    if (mainCodeInGlobalsInner && !mainCodeInGlobals)
                    {
                        // Main function code is in globals range - split globals
                        // Find where the main code actually starts in the 0 to SAVEBP range
                        int mainCodeStartInGlobals = -1;
                        for (int i = 0; i <= savebpIndex; i++)
                        {
                            if (instructions[i].InsType == NCSInstructionType.ACTION)
                            {
                                mainCodeStartInGlobals = i;
                                break;
                            }
                        }

                        if (mainCodeStartInGlobals >= 0)
                        {
                            // Split at the first ACTION instruction
                            // CRITICAL: Globals must include ALL instructions up to SAVEBP, not just up to first ACTION
                            // This ensures RSADDI and other instructions before SAVEBP are included in globals
                            // Globals: 0 to savebpIndex+1 (includes all variable initialization up to SAVEBP)
                            // Main: mainCodeStartInGlobals to last RETN (includes main code and everything after)
                            // NOTE: There will be overlap (mainCodeStartInGlobals to SAVEBP), but that's handled by the main function
                            int globalsInitEnd = savebpIndex + 1; // Always include all instructions up to SAVEBP

                            // Create globals subroutine (includes all variable initialization up to SAVEBP)
                            ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsInitEnd, 0);
                            if (globalsSub != null)
                            {
                                program.GetSubroutine().Add(globalsSub);
                                Debug($"DEBUG NcsToAstConverter: Created split globals subroutine (range 0-{globalsInitEnd}, includes all instructions up to SAVEBP, first ACTION at {mainCodeStartInGlobals})");
                            }

                            // Update mainStart to include the main function code (from first ACTION to last RETN)
                            mainStart = mainCodeStartInGlobals;
                            mainEnd = instructions.Count; // Include all instructions from first ACTION to last RETN
                            Debug($"DEBUG NcsToAstConverter: Updated mainStart to {mainStart} and mainEnd to {mainEnd} (main includes all code from first ACTION at {mainStart} to last RETN)");
                            mainCodeInGlobals = true; // Mark that split logic has run
                        }
                        else
                        {
                            // No ACTION instructions found in globals range either - use SAVEBP+1 as split point
                            int globalsInitEnd = savebpIndex + 1;
                            ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsInitEnd, 0);
                            if (globalsSub != null)
                            {
                                program.GetSubroutine().Add(globalsSub);
                                Debug($"DEBUG NcsToAstConverter: Created split globals subroutine (range 0-{globalsInitEnd}, no ACTION found, using SAVEBP+1 as split)");
                            }
                            mainStart = globalsInitEnd;
                            mainEnd = instructions.Count;
                            Debug($"DEBUG NcsToAstConverter: Updated mainStart to {mainStart} and mainEnd to {mainEnd} (no ACTION in globals, using SAVEBP+1 as main start)");
                        }
                    }
                    else if (!mainCodeInGlobals)
                    {
                        // CRITICAL: Only create globals subroutine if mainCodeInGlobals is false
                        // If mainCodeInGlobals is true, the EMPTY MAIN case (or earlier split logic) already created it

                        // Main start is after globals - create normal globals subroutine
                        // Globals subroutine ends at SAVEBP+1 (includes SAVEBP and entry stub up to but not including main)
                        // For files like asd.nss where main is at the last RETN, globals includes everything up to entry stub end
                        // swkotor2.exe: 0x004eb750 - Entry stub pattern detection verified in original engine bytecode
                        int globalsSubEnd = savebpIndex + 1;
                        // If entry stub exists, extend globals to include it (but not main) using comprehensive pattern detection
                        int calculatedEntryStubEnd = CalculateEntryStubEnd(instructions, globalsSubEnd, ncs);
                        if (calculatedEntryStubEnd > globalsSubEnd)
                        {
                            globalsSubEnd = calculatedEntryStubEnd;
                            Debug($"DEBUG NcsToAstConverter: Extended globals to include entry stub, globalsSubEnd={globalsSubEnd}");
                        }
                        ASubroutine globalsSub = ConvertInstructionRangeToSubroutine(ncs, instructions, 0, globalsSubEnd, 0);
                        if (globalsSub != null)
                        {
                            program.GetSubroutine().Add(globalsSub);
                            Debug($"DEBUG NcsToAstConverter: Created globals subroutine (range 0-{globalsSubEnd})");
                        }
                    }
                    else
                    {
                        Debug($"DEBUG NcsToAstConverter: Skipping globals subroutine creation - mainCodeInGlobals=true (already handled)");
                    }
                }
            }

            // CRITICAL: Ensure mainEnd always includes all instructions when there are no subroutines after main
            // This prevents missing instructions at the end of the main function (e.g., RSADDI before final RETN)
            Debug($"DEBUG NcsToAstConverter: BEFORE final mainEnd check - mainStart={mainStart}, mainEnd={mainEnd}, instructions.Count={instructions.Count}, subroutineStarts.Count={subroutineStarts.Count}");
            if (subroutineStarts.Count == 0 || !subroutineStarts.Any(subStart => subStart > mainStart))
            {
                // No subroutines after main - main should include ALL instructions up to the last RETN
                if (mainEnd < instructions.Count)
                {
                    Debug($"DEBUG NcsToAstConverter: WARNING - mainEnd ({mainEnd}) < instructions.Count ({instructions.Count}), correcting to include all instructions");
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: CRITICAL FIX - mainEnd was {mainEnd}, correcting to {instructions.Count} to include all instructions");
                    mainEnd = instructions.Count;
                }
            }
            Debug($"DEBUG NcsToAstConverter: AFTER final mainEnd check - mainStart={mainStart}, mainEnd={mainEnd}, instructions.Count={instructions.Count}, main will process {mainEnd - mainStart} instructions");
            Console.Error.WriteLine($"DEBUG NcsToAstConverter: FINAL mainEnd={mainEnd}, mainStart={mainStart}, will process instructions {mainStart} to {mainEnd - 1} (inclusive)");

            // CRITICAL FIX: Check if RESTOREBP right before mainStart is actually cleanup code at the end of main
            // If RESTOREBP is followed by MOVSP+RETN+RETN at the end of the file, it's cleanup code, not entry stub
            // In that case, include it in the main function by adjusting mainStart backward
            // Based on swkotor2.exe: 0x004eb750 - Cleanup code pattern detection for proper function boundary identification
            if (mainStart > 0 && mainStart < instructions.Count)
            {
                int prevIdx = mainStart - 1;
                if (prevIdx >= 0 && instructions[prevIdx].InsType == NCSInstructionType.RESTOREBP)
                {
                    // Use the comprehensive helper function to check if RESTOREBP is followed by cleanup code
                    // This ensures consistent detection logic across all code paths
                    bool isCleanupCode = IsRestorebpFollowedByCleanupCode(instructions, prevIdx);

                    // Also check for alternative cleanup patterns that might occur
                    // Some compilers may generate MOVSP+RETN (without second RETN) as cleanup
                    if (!isCleanupCode && mainStart + 1 < instructions.Count)
                    {
                        // Check if we have MOVSP, RETN pattern at the very end (alternative cleanup pattern)
                        if (instructions[mainStart].InsType == NCSInstructionType.MOVSP &&
                            instructions[mainStart + 1].InsType == NCSInstructionType.RETN &&
                            mainStart + 1 == instructions.Count - 1)
                        {
                            // This is cleanup code at the end (MOVSP+RETN pattern, no second RETN)
                            isCleanupCode = true;
                            Debug($"DEBUG NcsToAstConverter: RESTOREBP at index {prevIdx} is cleanup code (followed by MOVSP+RETN at end, alternative pattern), including in main");
                        }
                    }

                    if (isCleanupCode)
                    {
                        // Include RESTOREBP in main function
                        // CRITICAL: Don't override mainStart if it was set by split logic
                        // If mainStart is in globals range (before SAVEBP), it was set by split logic
                        bool mainStartSetBySplit = (savebpIndex >= 0 && mainStart < savebpIndex + 1 && mainStart > 0);
                        if (!mainStartSetBySplit)
                        {
                            mainStart = prevIdx;
                            Debug($"DEBUG NcsToAstConverter: Adjusted mainStart to {mainStart} to include RESTOREBP cleanup code");
                        }
                        else
                        {
                            Debug($"DEBUG NcsToAstConverter: Skipping RESTOREBP adjustment (mainStart={mainStart} is in globals range, split logic already set it correctly)");
                        }
                    }
                }
            }

            // Only create main if it has valid range AND this is NOT an include file
            // Include files have no main function - they're collections of utility functions
            Debug($"DEBUG NcsToAstConverter: Checking main subroutine creation - mainStart={mainStart}, mainEnd={mainEnd}, mainStart < mainEnd={mainStart < mainEnd}, mainStart >= 0={mainStart >= 0}, isIncludeFile={isIncludeFile}, instructions.Count={instructions.Count}");
            Console.Error.WriteLine($"DEBUG NcsToAstConverter: FINAL CHECK - mainStart={mainStart}, mainEnd={mainEnd}, mainStart < mainEnd={mainStart < mainEnd}, mainStart >= 0={mainStart >= 0}, isIncludeFile={isIncludeFile}, instructions.Count={instructions.Count}");
            if (!isIncludeFile && mainStart < mainEnd && mainStart >= 0)
            {
                Console.Error.WriteLine($"DEBUG NcsToAstConverter: Creating main subroutine, range={mainStart}-{mainEnd}, total instructions={instructions.Count}");
                Debug($"DEBUG NcsToAstConverter: Creating main subroutine, range={mainStart}-{mainEnd}, total instructions={instructions.Count}");
                // Log instruction types in main range
                for (int i = mainStart; i < Math.Min(mainEnd, instructions.Count) && i < mainStart + 20; i++)
                {
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: Main instruction[{i}]={instructions[i].InsType}");
                    Debug($"DEBUG NcsToAstConverter: Main instruction[{i}]={instructions[i].InsType}");
                }
                ASubroutine mainSub = ConvertInstructionRangeToSubroutine(ncs, instructions, mainStart, mainEnd, mainStart, (byte)entryReturnType);
                if (mainSub != null)
                {
                    // Check if main subroutine has commands
                    var cmdBlock = mainSub.GetCommandBlock();
                    int cmdCount = 0;
                    if (cmdBlock is AST.ACommandBlock aCmdBlock)
                    {
                        cmdCount = aCmdBlock.GetCmd().Count;
                    }
                    Debug($"DEBUG NcsToAstConverter: Main subroutine created with {cmdCount} commands, subId={mainSub.GetId()}");
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: Main subroutine created with {cmdCount} commands, subId={mainSub.GetId()}");
                    program.GetSubroutine().Add(mainSub);
                    Debug($"DEBUG NcsToAstConverter: Main subroutine added to program, total subroutines now: {program.GetSubroutine().Count}");
                }
                else
                {
                    Debug($"DEBUG NcsToAstConverter: WARNING - Main subroutine creation returned null for range {mainStart}-{mainEnd}");
                    Console.Error.WriteLine($"DEBUG NcsToAstConverter: WARNING - Main subroutine creation returned null for range {mainStart}-{mainEnd}");
                }
            }
            else if (isIncludeFile)
            {
                Debug($"DEBUG NcsToAstConverter: Skipping main subroutine creation for INCLUDE FILE - functions will be created from SAVEBP positions");
            }
            else
            {
                Debug($"DEBUG NcsToAstConverter: Skipping main subroutine creation - mainStart={mainStart}, mainEnd={mainEnd}, globalsEnd={globalsEndForMain}");
            }

            // CRITICAL FIX FOR INCLUDE FILES:
            // If this is an include file (detected earlier), create subroutines from each SAVEBP position
            // instead of trying to find a main function
            if (isIncludeFile && allSavebpIndices.Count > 0)
            {
                Debug($"DEBUG NcsToAstConverter: Processing INCLUDE FILE with {allSavebpIndices.Count} functions (each SAVEBP is a function start)");

                // For include files, each SAVEBP marks the start of a function
                // Functions end at RETN or the next SAVEBP
                for (int funcIdx = 0; funcIdx < allSavebpIndices.Count; funcIdx++)
                {
                    int funcStart = allSavebpIndices[funcIdx];
                    int funcEnd = instructions.Count; // Default to end of file

                    // Find the end of this function (next SAVEBP or RETN)
                    for (int i = funcStart + 1; i < instructions.Count; i++)
                    {
                        if (instructions[i].InsType == NCSInstructionType.RETN)
                        {
                            // Found RETN - this is the end of the function
                            funcEnd = i + 1; // Include the RETN
                            break;
                        }
                        // If we find another SAVEBP before RETN, the function ends here
                        // (This handles nested functions or unusual patterns)
                        if (allSavebpIndices.Contains(i) && i > funcStart)
                        {
                            funcEnd = i; // Exclude the next function's SAVEBP
                            break;
                        }
                    }

                    // Create a subroutine for this function
                    ASubroutine funcSub = ConvertInstructionRangeToSubroutine(
                        ncs,
                        instructions,
                        funcStart,
                        funcEnd,
                        program.GetSubroutine().Count);
                    if (funcSub != null)
                    {
                        program.GetSubroutine().Add(funcSub);
                        Debug($"DEBUG NcsToAstConverter: Created include file function {funcIdx} (range {funcStart}-{funcEnd}), total subs now: {program.GetSubroutine().Count}");
                    }
                    else
                    {
                        Debug($"DEBUG NcsToAstConverter: WARNING - Include file function {funcIdx} creation returned null (range {funcStart}-{funcEnd})");
                    }
                }

                // Skip the regular subroutine handling since we've processed all functions
                Debug($"DEBUG NcsToAstConverter: Include file processing complete, created {program.GetSubroutine().Count} subroutines");
            }
            else
            {
                // Regular handling for normal scripts (with main function)
                List<int> sortedStarts = new List<int>(subroutineStarts);
                sortedStarts.Sort();
                for (int idx = 0; idx < sortedStarts.Count; idx++)
                {
                    int subStart = sortedStarts[idx];
                    int subEnd = instructions.Count;
                    for (int i = subStart + 1; i < instructions.Count; i++)
                    {
                        if (subroutineStarts.Contains(i))
                        {
                            subEnd = i;
                            break;
                        }

                        if (instructions[i].InsType == NCSInstructionType.RETN)
                        {
                            subEnd = i + 1;
                            break;
                        }
                    }

                    ASubroutine sub = ConvertInstructionRangeToSubroutine(
                        ncs,
                        instructions,
                        subStart,
                        subEnd,
                        program.GetSubroutine().Count);
                    if (sub != null)
                    {
                        program.GetSubroutine().Add(sub);
                    }
                }
            }

            // CRITICAL: Ensure we always have at least one subroutine (main)
            // Comprehensive edge case handling for files where entry stub detection fails or files have unusual structure
            // Based on nwnnsscomp.exe and swkotor2.exe: Original engine handles malformed NCS files gracefully
            // Edge cases handled:
            // 1. Files with no SAVEBP and no entry stub pattern
            // 2. Files where entry stub detection partially succeeds but end calculation fails
            // 3. Files with malformed entry stubs (JSR without RETN, incomplete patterns)
            // 4. Files where ConvertInstructionRangeToSubroutine returns null even with valid ranges
            // 5. Files with unusual instruction sequences that don't match normal patterns
            // 6. Files where entry stub end is calculated but the range is still invalid
            // 7. Files with no recognizable structure (empty or corrupted)
            int subroutineCount = program.GetSubroutine().Count;
            Debug($"DEBUG NcsToAstConverter: Final subroutine count before fallback check: {subroutineCount}, instructions: {instructions.Count}");
            if (subroutineCount == 0 && instructions.Count > 0)
            {
                Debug("DEBUG NcsToAstConverter: No subroutines created, creating fallback main subroutine from entire instruction range");
                int fallbackMainStart = 0;
                int fallbackMainEnd = instructions.Count;

                // EDGE CASE 1: Handle files with SAVEBP
                if (savebpIndex >= 0)
                {
                    fallbackMainStart = savebpIndex + 1;

                    // EDGE CASE 2: Try to detect and skip entry stub using comprehensive detection
                    // swkotor2.exe: 0x004eb750 - Entry stub patterns: [RSADD*], JSR, RETN or [RSADD*], JSR, RESTOREBP
                    if (HasEntryStubPattern(instructions, fallbackMainStart, ncs))
                    {
                        int fallbackEntryStubStart = fallbackMainStart;
                        int fallbackEntryStubEnd = -1;

                        // Calculate entry stub end position with comprehensive error handling
                        // Entry stub can be: [RSADD*], JSR, RETN or [RSADD*], JSR, RESTOREBP
                        int jsrOffset = 0;
                        if (fallbackEntryStubStart < instructions.Count && IsRsaddInstruction(instructions[fallbackEntryStubStart].InsType))
                        {
                            jsrOffset = 1; // JSR is at position fallbackEntryStubStart + 1
                        }

                        int jsrIndex = fallbackEntryStubStart + jsrOffset;

                        // EDGE CASE 3: Handle malformed entry stubs (JSR without RETN, incomplete patterns)
                        if (jsrIndex + 1 < instructions.Count)
                        {
                            if (instructions[jsrIndex].InsType == NCSInstructionType.JSR &&
                                instructions[jsrIndex + 1].InsType == NCSInstructionType.RETN)
                            {
                                fallbackEntryStubEnd = jsrIndex + 2; // After RETN
                            }
                            else if (instructions[jsrIndex].InsType == NCSInstructionType.JSR &&
                                     instructions[jsrIndex + 1].InsType == NCSInstructionType.RESTOREBP)
                            {
                                // Check if RESTOREBP is followed by cleanup code at the end (not an entry stub)
                                int restorebpIndex = jsrIndex + 1;
                                if (!IsRestorebpFollowedByCleanupCode(instructions, restorebpIndex))
                                {
                                    fallbackEntryStubEnd = jsrIndex + 2; // After RESTOREBP (valid entry stub)
                                }
                                // If RESTOREBP is cleanup code, fallbackEntryStubEnd remains -1 (no entry stub)
                            }
                            // EDGE CASE 4: Handle incomplete entry stub patterns (JSR without RETN/RESTOREBP)
                            else if (instructions[jsrIndex].InsType == NCSInstructionType.JSR)
                            {
                                // Malformed entry stub: JSR found but no RETN/RESTOREBP follows
                                // Try to find the next RETN as a heuristic
                                for (int i = jsrIndex + 1; i < Math.Min(jsrIndex + 10, instructions.Count); i++)
                                {
                                    if (instructions[i].InsType == NCSInstructionType.RETN)
                                    {
                                        fallbackEntryStubEnd = i + 1; // After RETN
                                        Debug($"DEBUG NcsToAstConverter: Malformed entry stub detected - JSR at {jsrIndex} but RETN found at {i}, using heuristic end={fallbackEntryStubEnd}");
                                        break;
                                    }
                                }
                                // If no RETN found, treat JSR as part of main code (don't skip it)
                                if (fallbackEntryStubEnd == -1)
                                {
                                    Debug($"DEBUG NcsToAstConverter: Malformed entry stub - JSR at {jsrIndex} but no RETN/RESTOREBP found, treating as main code");
                                }
                            }
                        }
                        // EDGE CASE 5: Handle entry stub detection failure (pattern detected but end calculation fails)
                        else if (jsrIndex >= instructions.Count)
                        {
                            Debug($"DEBUG NcsToAstConverter: Entry stub pattern detected but JSR index {jsrIndex} is out of bounds (instructions.Count={instructions.Count})");
                        }

                        if (fallbackEntryStubEnd > fallbackEntryStubStart && fallbackEntryStubEnd <= instructions.Count)
                        {
                            fallbackMainStart = fallbackEntryStubEnd; // Skip entire entry stub
                            Debug($"DEBUG NcsToAstConverter: Detected entry stub at {fallbackEntryStubStart}-{fallbackEntryStubEnd - 1}, adjusted fallbackMainStart to {fallbackMainStart}");
                        }
                        else if (fallbackEntryStubEnd > instructions.Count)
                        {
                            Debug($"DEBUG NcsToAstConverter: Entry stub end {fallbackEntryStubEnd} exceeds instructions.Count {instructions.Count}, using fallbackMainStart={fallbackMainStart}");
                        }
                        else
                        {
                            Debug($"DEBUG NcsToAstConverter: Entry stub pattern detected but could not determine valid end position (start={fallbackEntryStubStart}, end={fallbackEntryStubEnd}), using fallbackMainStart={fallbackMainStart}");
                        }
                    }
                    else
                    {
                        Debug($"DEBUG NcsToAstConverter: No entry stub pattern detected at {fallbackMainStart}, using fallbackMainStart={fallbackMainStart}");
                    }
                }
                // EDGE CASE 6: Handle files with no SAVEBP (no globals section)
                else
                {
                    Debug("DEBUG NcsToAstConverter: No SAVEBP found, checking for entry stub at position 0");
                    // Try to detect entry stub at position 0
                    if (HasEntryStubPattern(instructions, 0, ncs))
                    {
                        int fallbackEntryStubEnd = -1;
                        int jsrOffset = 0;
                        if (instructions.Count > 0 && IsRsaddInstruction(instructions[0].InsType))
                        {
                            jsrOffset = 1;
                        }

                        int jsrIndex = jsrOffset;
                        if (jsrIndex + 1 < instructions.Count)
                        {
                            if (instructions[jsrIndex].InsType == NCSInstructionType.JSR &&
                                instructions[jsrIndex + 1].InsType == NCSInstructionType.RETN)
                            {
                                fallbackEntryStubEnd = jsrIndex + 2;
                            }
                            else if (instructions[jsrIndex].InsType == NCSInstructionType.JSR &&
                                     instructions[jsrIndex + 1].InsType == NCSInstructionType.RESTOREBP)
                            {
                                int restorebpIndex = jsrIndex + 1;
                                if (!IsRestorebpFollowedByCleanupCode(instructions, restorebpIndex))
                                {
                                    fallbackEntryStubEnd = jsrIndex + 2;
                                }
                            }
                        }

                        if (fallbackEntryStubEnd > 0 && fallbackEntryStubEnd <= instructions.Count)
                        {
                            fallbackMainStart = fallbackEntryStubEnd;
                            Debug($"DEBUG NcsToAstConverter: Detected entry stub at 0-{fallbackEntryStubEnd - 1}, adjusted fallbackMainStart to {fallbackMainStart}");
                        }
                    }
                }

                // EDGE CASE 7: Validate and fix invalid ranges before attempting conversion
                // This handles very small files where the calculated start might be after the end
                if (fallbackMainStart >= fallbackMainEnd)
                {
                    Debug($"DEBUG NcsToAstConverter: Fallback start ({fallbackMainStart}) >= end ({fallbackMainEnd}), using entire range (0-{instructions.Count})");
                    fallbackMainStart = 0;
                    fallbackMainEnd = instructions.Count;
                }

                // Ensure range is within bounds
                if (fallbackMainStart < 0)
                {
                    Debug($"DEBUG NcsToAstConverter: Fallback start ({fallbackMainStart}) is negative, clamping to 0");
                    fallbackMainStart = 0;
                }
                if (fallbackMainEnd > instructions.Count)
                {
                    Debug($"DEBUG NcsToAstConverter: Fallback end ({fallbackMainEnd}) exceeds instructions.Count ({instructions.Count}), clamping to {instructions.Count}");
                    fallbackMainEnd = instructions.Count;
                }

                // EDGE CASE 8: Attempt to create fallback subroutine with comprehensive error handling
                if (fallbackMainStart < fallbackMainEnd && fallbackMainStart >= 0 && fallbackMainEnd <= instructions.Count)
                {
                    ASubroutine fallbackMain = ConvertInstructionRangeToSubroutine(ncs, instructions, fallbackMainStart, fallbackMainEnd, fallbackMainStart);
                    if (fallbackMain != null)
                    {
                        program.GetSubroutine().Add(fallbackMain);
                        Debug($"DEBUG NcsToAstConverter: Created fallback main subroutine (range {fallbackMainStart}-{fallbackMainEnd})");
                    }
                    else
                    {
                        Debug($"DEBUG NcsToAstConverter: Fallback main subroutine creation returned null (range {fallbackMainStart}-{fallbackMainEnd})");
                        // EDGE CASE 9: Create minimal subroutine with last few instructions as last resort
                        if (instructions.Count > 0)
                        {
                            Debug("DEBUG NcsToAstConverter: Attempting to create minimal subroutine as last resort");
                            AST.ASubroutine minimalSub = new AST.ASubroutine();
                            minimalSub.SetId(0);
                            AST.ACommandBlock minimalCmdBlock = new AST.ACommandBlock();

                            // Try to convert instructions from the calculated start position
                            // If that fails, try the last few instructions
                            int minimalStart = Math.Max(fallbackMainStart, Math.Max(0, instructions.Count - 10));
                            int convertedCount = 0;
                            for (int i = minimalStart; i < instructions.Count && convertedCount < 20; i++)
                            {
                                if (i >= 0 && i < instructions.Count && instructions[i] != null)
                                {
                                    PCmd cmd = ConvertInstructionToCmd(ncs, instructions[i], i, instructions);
                                    if (cmd != null)
                                    {
                                        minimalCmdBlock.AddCmd((AST.PCmd)(object)cmd);
                                        convertedCount++;
                                    }
                                }
                            }

                            minimalSub.SetCommandBlock(minimalCmdBlock);

                            // Find RETN and set it as return (search from end backwards)
                            for (int i = instructions.Count - 1; i >= 0 && i >= instructions.Count - 10; i--)
                            {
                                if (instructions[i] != null && instructions[i].InsType == NCSInstructionType.RETN)
                                {
                                    AReturn ret = ConvertRetn(instructions[i], i);
                                    if (ret != null)
                                    {
                                        minimalSub.SetReturn((AST.PReturn)(object)ret);
                                    }
                                    break;
                                }
                            }

                            program.GetSubroutine().Add(minimalSub);
                            Debug($"DEBUG NcsToAstConverter: Created minimal fallback subroutine with {convertedCount} commands");
                        }
                    }
                }
                else
                {
                    Debug($"DEBUG NcsToAstConverter: Fallback main subroutine range invalid (start={fallbackMainStart}, end={fallbackMainEnd}, instructions.Count={instructions.Count})");
                    // EDGE CASE 10: Create empty subroutine as absolute last resort for corrupted files
                    if (instructions.Count > 0)
                    {
                        Debug("DEBUG NcsToAstConverter: Creating empty subroutine as absolute last resort");
                        AST.ASubroutine emptySub = new AST.ASubroutine();
                        emptySub.SetId(0);
                        emptySub.SetCommandBlock(new AST.ACommandBlock());

                        // Try to find and add at least one RETN if possible
                        for (int i = 0; i < instructions.Count && i < 50; i++)
                        {
                            if (instructions[i] != null && instructions[i].InsType == NCSInstructionType.RETN)
                            {
                                AReturn ret = ConvertRetn(instructions[i], i);
                                if (ret != null)
                                {
                                    emptySub.SetReturn((AST.PReturn)(object)ret);
                                    break;
                                }
                            }
                        }

                        program.GetSubroutine().Add(emptySub);
                        Debug("DEBUG NcsToAstConverter: Created empty fallback subroutine");
                    }
                }
            }

            return new Start(program, new EOF());
        }

        private static ASubroutine ConvertInstructionRangeToSubroutine(
            NCS ncs,
            List<NCSInstruction> instructions,
            int startIdx,
            int endIdx,
            int subId,
            byte returnType = 0)
        {
            if (startIdx >= endIdx || startIdx >= instructions.Count)
            {
                Debug($"DEBUG ConvertInstructionRangeToSubroutine: Invalid range - startIdx={startIdx}, endIdx={endIdx}, instructions.Count={instructions.Count}, returning null");
                return null;
            }

            AST.ASubroutine sub = new AST.ASubroutine();
            sub.SetId(subId);
            sub.SetReturnType(returnType); // Store return type for main function identification
            AST.ACommandBlock cmdBlock = new AST.ACommandBlock();

            int limit = Math.Min(endIdx, instructions.Count);
            int convertedCount = 0;
            int nullCount = 0;
            int actionCount = 0;
            int negiCount = 0;
            int constiCount = 0;

            // CRITICAL DEBUG: Log the range being processed with full context
            Debug($"DEBUG ConvertInstructionRangeToSubroutine: Processing range {startIdx} to {limit - 1} (inclusive), subId={subId}, instructions.Count={instructions.Count}");
            Console.Error.WriteLine($"DEBUG ConvertInstructionRangeToSubroutine: Processing range {startIdx} to {limit - 1} (inclusive), subId={subId}, instructions.Count={instructions.Count}");

            // Log first and last few instructions in the range to understand what we're processing
            if (startIdx < instructions.Count && limit > startIdx)
            {
                Debug($"DEBUG ConvertInstructionRangeToSubroutine: First instruction in range: index={startIdx}, type={instructions[startIdx]?.InsType}, offset={instructions[startIdx]?.Offset}");
                if (limit - 1 < instructions.Count)
                {
                    Debug($"DEBUG ConvertInstructionRangeToSubroutine: Last instruction in range: index={limit - 1}, type={instructions[limit - 1]?.InsType}, offset={instructions[limit - 1]?.Offset}");
                }
            }

            for (int i = startIdx; i < limit; i++)
            {
                if (instructions[i] == null)
                {
                    Debug($"DEBUG ConvertInstructionRangeToSubroutine: WARNING - Instruction at index {i} is null!");
                    nullCount++;
                    continue;
                }

                // Track critical instruction types
                if (instructions[i].InsType == NCSInstructionType.ACTION)
                {
                    actionCount++;
                    Console.Error.WriteLine($"DEBUG ConvertInstructionRangeToSubroutine: Found ACTION instruction at index {i}, offset={instructions[i].Offset}");
                    Debug($"DEBUG ConvertInstructionRangeToSubroutine: Found ACTION instruction at index {i}, offset={instructions[i].Offset}");
                }
                else if (instructions[i].InsType == NCSInstructionType.NEGI)
                {
                    negiCount++;
                    // Log NEGI instructions - they're critical for negative constants
                    Debug($"DEBUG ConvertInstructionRangeToSubroutine: Found NEGI instruction at index {i}, offset={instructions[i].Offset}");
                    Console.Error.WriteLine($"DEBUG ConvertInstructionRangeToSubroutine: Found NEGI instruction at index {i}, offset={instructions[i].Offset}");
                }
                else if (instructions[i].InsType == NCSInstructionType.CONSTI)
                {
                    constiCount++;
                    // Log CONSTI near NEGI to understand the pattern
                    if (i < limit - 1 && instructions[i + 1]?.InsType == NCSInstructionType.NEGI)
                    {
                        Debug($"DEBUG ConvertInstructionRangeToSubroutine: Found CONSTI at index {i} followed by NEGI at {i + 1}");
                    }
                }

                PCmd cmd = ConvertInstructionToCmd(ncs, instructions[i], i, instructions);
                if (cmd != null)
                {
                    cmdBlock.AddCmd((AST.PCmd)(object)cmd);
                    convertedCount++;
                    // DEBUG: Log ACTION conversions, especially near the end
                    if (instructions[i].InsType == NCSInstructionType.ACTION && (i >= limit - 5 || i == limit - 1))
                    {
                        Debug($"DEBUG ConvertInstructionRangeToSubroutine: Successfully converted ACTION at index {i} (near end, limit={limit})");
                    }
                }
                else
                {
                    nullCount++;
                    // CRITICAL: Log if ACTION returns null (should never happen)
                    if (instructions[i].InsType == NCSInstructionType.ACTION)
                    {
                        Debug($"DEBUG ConvertInstructionRangeToSubroutine: ERROR - ACTION at index {i} returned null!");
                        Console.Error.WriteLine($"ERROR ConvertInstructionRangeToSubroutine: ACTION at index {i} returned null!");
                    }
                    // CRITICAL: Log if NEGI returns null (should never happen)
                    if (instructions[i].InsType == NCSInstructionType.NEGI)
                    {
                        Debug($"DEBUG ConvertInstructionRangeToSubroutine: ERROR - NEGI at index {i}, offset={instructions[i].Offset} returned null!");
                        Console.Error.WriteLine($"ERROR ConvertInstructionRangeToSubroutine: NEGI at index {i}, offset={instructions[i].Offset} returned null!");
                    }
                    if (nullCount <= 10) // Log first 10 null conversions
                    {
                        Debug($"DEBUG NcsToAstConverter: Instruction at index {i} ({instructions[i].InsType}, offset={instructions[i].Offset}) returned null");
                        Console.Error.WriteLine($"DEBUG NcsToAstConverter: Instruction at index {i} ({instructions[i].InsType}, offset={instructions[i].Offset}) returned null");
                    }
                }
            }
            Console.Error.WriteLine($"DEBUG ConvertInstructionRangeToSubroutine: Range {startIdx}-{limit}: Found {actionCount} ACTION, {negiCount} NEGI, {constiCount} CONSTI instructions");
            Debug($"DEBUG ConvertInstructionRangeToSubroutine: Range {startIdx}-{limit}: Found {actionCount} ACTION, {negiCount} NEGI, {constiCount} CONSTI instructions");
            Debug($"DEBUG NcsToAstConverter: Converted {convertedCount} commands, {nullCount} returned null (range {startIdx}-{limit})");

            sub.SetCommandBlock(cmdBlock);

            for (int i = startIdx; i < limit; i++)
            {
                if (instructions[i].InsType == NCSInstructionType.RETN)
                {
                    AReturn ret = ConvertRetn(instructions[i], i);
                    if (ret != null)
                    {
                        sub.SetReturn((AST.PReturn)(object)ret);
                    }

                    break;
                }
            }

            return sub;
        }

        private static PCmd ConvertInstructionToCmd(
            NCS ncs,
            NCSInstruction inst,
            int pos,
            List<NCSInstruction> instructions)
        {
            NCSInstructionType insType = inst.InsType;
            if (insType == NCSInstructionType.CONSTI ||
                insType == NCSInstructionType.CONSTF ||
                insType == NCSInstructionType.CONSTS ||
                insType == NCSInstructionType.CONSTO)
            {
                return ConvertConstCmd(inst, pos);
            }

            if (insType == NCSInstructionType.ACTION)
            {
                return ConvertActionCmd(inst, pos);
            }

            if (insType == NCSInstructionType.JMP || insType == NCSInstructionType.JSR)
            {
                return ConvertJumpCmd(ncs, inst, pos, instructions);
            }

            if (insType == NCSInstructionType.JZ || insType == NCSInstructionType.JNZ)
            {
                return ConvertConditionalJumpCmd(ncs, inst, pos, instructions);
            }

            if (insType == NCSInstructionType.RETN)
            {
                return ConvertRetnCmd(inst, pos);
            }

            if (insType == NCSInstructionType.CPDOWNSP || insType == NCSInstructionType.CPTOPSP)
            {
                return ConvertCopySpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.CPDOWNBP || insType == NCSInstructionType.CPTOPBP)
            {
                return ConvertCopyBpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.MOVSP)
            {
                return ConvertMovespCmd(inst, pos);
            }

            if (insType == NCSInstructionType.INCxSP ||
                insType == NCSInstructionType.DECxSP ||
                insType == NCSInstructionType.INCxBP ||
                insType == NCSInstructionType.DECxBP)
            {
                return ConvertStackOpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.RSADDI ||
                insType == NCSInstructionType.RSADDF ||
                insType == NCSInstructionType.RSADDS ||
                insType == NCSInstructionType.RSADDO ||
                insType == NCSInstructionType.RSADDEFF ||
                insType == NCSInstructionType.RSADDEVT ||
                insType == NCSInstructionType.RSADDLOC ||
                insType == NCSInstructionType.RSADDTAL)
            {
                return ConvertRsaddCmd(inst, pos);
            }

            if (insType == NCSInstructionType.DESTRUCT)
            {
                return ConvertDestructCmd(inst, pos);
            }

            if (insType == NCSInstructionType.SAVEBP || insType == NCSInstructionType.RESTOREBP)
            {
                return ConvertBpCmd(inst, pos);
            }

            if (insType == NCSInstructionType.STORE_STATE)
            {
                return ConvertStoreStateCmd(inst, pos);
            }

            if (insType == NCSInstructionType.NOP ||
                insType == NCSInstructionType.NOP2 ||
                insType == NCSInstructionType.RESERVED ||
                insType == NCSInstructionType.RESERVED_01)
            {
                return null;
            }

            if (insType == NCSInstructionType.NEGI ||
                insType == NCSInstructionType.NEGF ||
                insType == NCSInstructionType.NOTI ||
                insType == NCSInstructionType.COMPI)
            {
                return ConvertUnaryCmd(inst, pos);
            }

            if (inst.IsArithmetic() || inst.IsComparison() || inst.IsBitwise())
            {
                return ConvertBinaryCmd(inst, pos);
            }

            if (inst.IsLogical() ||
                insType == NCSInstructionType.BOOLANDII ||
                insType == NCSInstructionType.INCORII ||
                insType == NCSInstructionType.EXCORII)
            {
                return ConvertLogiiCmd(inst, pos);
            }

            System.Diagnostics.Debug.WriteLine(
                "Unknown instruction type at position " + pos + ": " + insType +
                " (value: " + insType + ")");
            return null;
        }

        private static AConstCmd ConvertConstCmd(NCSInstruction inst, int pos)
        {
            AConstCmd constCmd = new AConstCmd();
            AConstCommand constCommand = new AConstCommand();

            constCommand.SetConst(new TConst(pos, 0));
            constCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            // For constant instructions, the type should match the instruction type, not the qualifier
            // CONSTI = 3 (int), CONSTF = 4 (float), CONSTS = 5 (string), CONSTO = 6 (object)
            int typeVal;
            if (inst.InsType == NCSInstructionType.CONSTI)
            {
                typeVal = 3; // VT_INTEGER
            }
            else if (inst.InsType == NCSInstructionType.CONSTF)
            {
                typeVal = 4; // VT_FLOAT
            }
            else if (inst.InsType == NCSInstructionType.CONSTS)
            {
                typeVal = 5; // VT_STRING
            }
            else if (inst.InsType == NCSInstructionType.CONSTO)
            {
                typeVal = 6; // VT_OBJECT
            }
            else
            {
                // Fallback to qualifier for unknown constant types
                typeVal = GetQualifier(inst.InsType);
            }
            constCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));

            if (inst.Args != null && inst.Args.Count > 0)
            {
                if (inst.InsType == NCSInstructionType.CONSTI)
                {
                    int intVal = GetArgAsInt(inst, 0);
                    AIntConstant constConstant = new AIntConstant();
                    constConstant.SetIntegerConstant(new TIntegerConstant(Convert.ToString(intVal, CultureInfo.InvariantCulture), pos, 0));
                    constCommand.SetConstant(constConstant);
                }
                else if (inst.InsType == NCSInstructionType.CONSTF)
                {
                    double floatVal = GetArgAsDouble(inst, 0);
                    AFloatConstant constConstant = new AFloatConstant();
                    constConstant.SetFloatConstant(new TFloatConstant(floatVal.ToString(CultureInfo.InvariantCulture), pos, 0));
                    constCommand.SetConstant(constConstant);
                }
                else if (inst.InsType == NCSInstructionType.CONSTS)
                {
                    string strVal = GetArgAsString(inst, 0, "");
                    AStringConstant constConstant = new AStringConstant();
                    constConstant.SetStringLiteral(new TStringLiteral("\"" + strVal + "\"", pos, 0));
                    constCommand.SetConstant(constConstant);
                }
                else if (inst.InsType == NCSInstructionType.CONSTO)
                {
                    int objVal = GetArgAsInt(inst, 0);
                    AIntConstant constConstant = new AIntConstant();
                    constConstant.SetIntegerConstant(new TIntegerConstant(Convert.ToString(objVal, CultureInfo.InvariantCulture), pos, 0));
                    constCommand.SetConstant(constConstant);
                }
            }

            constCommand.SetSemi(new TSemi(pos, 0));
            constCmd.SetConstCommand(constCommand);

            return constCmd;
        }

        private static AActionCmd ConvertActionCmd(NCSInstruction inst, int pos)
        {
            int idVal = 0;
            int argCountVal = 0;
            if (inst.Args != null && inst.Args.Count >= 1)
            {
                idVal = GetArgAsInt(inst, 0);
                if (inst.Args.Count > 1)
                {
                    argCountVal = GetArgAsInt(inst, 1);
                }
            }
            Console.Error.WriteLine($"DEBUG ConvertActionCmd: pos={pos}, actionId={idVal}, argCount={argCountVal}");
            Debug($"DEBUG ConvertActionCmd: pos={pos}, actionId={idVal}, argCount={argCountVal}");
            AActionCmd actionCmd = new AActionCmd();
            AActionCommand actionCommand = new AActionCommand();

            actionCommand.SetAction(new TAction(pos, 0));
            actionCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            actionCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));

            actionCommand.SetId(new TIntegerConstant(Convert.ToString(idVal, CultureInfo.InvariantCulture), pos, 0));
            actionCommand.SetArgCount(new TIntegerConstant(Convert.ToString(argCountVal, CultureInfo.InvariantCulture), pos, 0));
            actionCommand.SetSemi(new TSemi(pos, 0));

            actionCmd.SetActionCommand(actionCommand);
            return actionCmd;
        }

        private static PCmd ConvertJumpCmd(
            NCS ncs,
            NCSInstruction inst,
            int pos,
            List<NCSInstruction> instructions)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);

            int offset = 0;
            if (inst.Jump != null)
            {
                try
                {
                    int jumpIdx = ncs.GetInstructionIndex(inst.Jump);
                    if (jumpIdx >= 0)
                    {
                        offset = jumpIdx - pos;
                    }
                }
                catch (Exception)
                {
                    offset = 0;
                }
            }

            if (insType == NCSInstructionType.JSR)
            {
                AJumpSubCmd jsrCmd = new AJumpSubCmd();
                AJumpToSubroutine jsrToSub = new AJumpToSubroutine();

                jsrToSub.SetJsr(new TJsr(pos, 0));
                jsrToSub.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
                jsrToSub.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
                jsrToSub.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
                jsrToSub.SetSemi(new TSemi(pos, 0));

                jsrCmd.SetJumpToSubroutine(jsrToSub);
                return jsrCmd;
            }

            AJumpCmd jumpCmd = new AJumpCmd();
            AJumpCommand jumpCommand = new AJumpCommand();

            jumpCommand.SetJmp(new TJmp(pos, 0));
            jumpCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            jumpCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            jumpCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            jumpCommand.SetSemi(new TSemi(pos, 0));

            jumpCmd.SetJumpCommand(jumpCommand);
            return jumpCmd;
        }

        private static PCmd ConvertConditionalJumpCmd(
            NCS ncs,
            NCSInstruction inst,
            int pos,
            List<NCSInstruction> instructions)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);

            int offset = 0;
            if (inst.Jump != null)
            {
                try
                {
                    int jumpIdx = ncs.GetInstructionIndex(inst.Jump);
                    if (jumpIdx >= 0)
                    {
                        offset = jumpIdx - pos;
                    }
                }
                catch (Exception)
                {
                    offset = 0;
                }
            }

            ACondJumpCmd condJumpCmd = new ACondJumpCmd();
            AConditionalJumpCommand condJumpCommand = new AConditionalJumpCommand();

            if (insType == NCSInstructionType.JZ)
            {
                AZeroJumpIf zeroJumpIf = new AZeroJumpIf();
                zeroJumpIf.SetJz(new TJz(pos, 0));
                condJumpCommand.SetJumpIf(zeroJumpIf);
            }
            else
            {
                ANonzeroJumpIf nonzeroJumpIf = new ANonzeroJumpIf();
                nonzeroJumpIf.SetJnz(new TJnz(pos, 0));
                condJumpCommand.SetJumpIf(nonzeroJumpIf);
            }

            condJumpCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            condJumpCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            condJumpCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            condJumpCommand.SetSemi(new TSemi(pos, 0));

            condJumpCmd.SetConditionalJumpCommand(condJumpCommand);
            return condJumpCmd;
        }

        private static AReturn ConvertRetn(NCSInstruction inst, int pos)
        {
            AReturn ret = new AReturn();
            ret.SetRetn(new TRetn(pos, 0));
            ret.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            ret.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            ret.SetSemi(new TSemi(pos, 0));

            return ret;
        }

        private static AReturnCmd ConvertRetnCmd(NCSInstruction inst, int pos)
        {
            AReturnCmd retnCmd = new AReturnCmd();
            AReturn retn = ConvertRetn(inst, pos);
            retnCmd.SetReturn(retn);
            return retnCmd;
        }

        private static PCmd ConvertCopySpCmd(NCSInstruction inst, int pos)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);
            int offset = GetArgAsInt(inst, 0);
            int size = GetArgAsInt(inst, 1);

            if (insType == NCSInstructionType.CPDOWNSP)
            {
                ACopydownspCmd cmd = new ACopydownspCmd();
                ACopyDownSpCommand command = new ACopyDownSpCommand();
                command.SetCpdownsp(new TCpdownsp(pos, 0));
                command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
                command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
                command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
                command.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
                command.SetSemi(new TSemi(pos, 0));
                cmd.SetCopyDownSpCommand(command);
                return cmd;
            }

            ACopytopspCmd topCmd = new ACopytopspCmd();
            ACopyTopSpCommand topCommand = new ACopyTopSpCommand();
            topCommand.SetCptopsp(new TCptopsp(pos, 0));
            topCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSemi(new TSemi(pos, 0));
            topCmd.SetCopyTopSpCommand(topCommand);
            return topCmd;
        }

        private static PCmd ConvertCopyBpCmd(NCSInstruction inst, int pos)
        {
            NCSInstructionType insType = inst.InsType;
            int typeVal = GetQualifier(insType);
            int offset = GetArgAsInt(inst, 0);
            int size = GetArgAsInt(inst, 1);

            if (insType == NCSInstructionType.CPDOWNBP)
            {
                ACopydownbpCmd cmd = new ACopydownbpCmd();
                ACopyDownBpCommand command = new ACopyDownBpCommand();
                command.SetCpdownbp(new TCpdownbp(pos, 0));
                command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
                command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
                command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
                command.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
                command.SetSemi(new TSemi(pos, 0));
                cmd.SetCopyDownBpCommand(command);
                return cmd;
            }

            ACopytopbpCmd topCmd = new ACopytopbpCmd();
            ACopyTopBpCommand topCommand = new ACopyTopBpCommand();
            topCommand.SetCptopbp(new TCptopbp(pos, 0));
            topCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSize(new TIntegerConstant(Convert.ToString(size, CultureInfo.InvariantCulture), pos, 0));
            topCommand.SetSemi(new TSemi(pos, 0));
            topCmd.SetCopyTopBpCommand(topCommand);
            return topCmd;
        }

        private static PCmd ConvertMovespCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);
            int offset = GetArgAsInt(inst, 0);

            AMovespCmd cmd = new AMovespCmd();
            AMoveSpCommand command = new AMoveSpCommand();
            command.SetMovsp(new TMovsp(pos, 0));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetMoveSpCommand(command);

            return cmd;
        }

        private static PCmd ConvertRsaddCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);

            AST.ARsaddCmd cmd = new AST.ARsaddCmd();
            AST.ARsaddCommand command = new AST.ARsaddCommand();
            command.SetRsadd(new AST.TRsadd(pos, 0));
            command.SetPos(new AST.TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new AST.TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new AST.TSemi(pos, 0));
            cmd.SetRsaddCommand(command);

            return cmd;
        }

        private static PCmd ConvertStackOpCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);
            int offset = GetArgAsInt(inst, 0);

            PCmd stackOpCmd = null;
            PStackOp stackOp = null;

            if (inst.InsType == NCSInstructionType.INCxSP)
            {
                stackOp = new AIncispStackOp();
                ((AIncispStackOp)stackOp).SetIncisp(new TIncisp(pos, 0));
            }
            else if (inst.InsType == NCSInstructionType.DECxSP)
            {
                stackOp = new ADecispStackOp();
                ((ADecispStackOp)stackOp).SetDecisp(new TDecisp(pos, 0));
            }
            else if (inst.InsType == NCSInstructionType.INCxBP)
            {
                stackOp = new AIncibpStackOp();
                ((AIncibpStackOp)stackOp).SetIncibp(new TIncibp(pos, 0));
            }
            else if (inst.InsType == NCSInstructionType.DECxBP)
            {
                stackOp = new ADecibpStackOp();
                ((ADecibpStackOp)stackOp).SetDecibp(new TDecibp(pos, 0));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    "Unexpected instruction type in _convert_stack_op_cmd: " + inst.InsType);
                return null;
            }

            AStackCommand stackCommand = new AStackCommand();
            stackCommand.SetStackOp(stackOp);
            stackCommand.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            stackCommand.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            stackCommand.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            stackCommand.SetSemi(new TSemi(pos, 0));

            AStackOpCmd cmd = new AStackOpCmd();
            cmd.SetStackCommand(stackCommand);
            stackOpCmd = cmd;

            return stackOpCmd;
        }

        private static PCmd ConvertDestructCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);
            int sizeRem = GetArgAsInt(inst, 0);
            int offset = GetArgAsInt(inst, 1);
            int sizeSave = GetArgAsInt(inst, 2);

            ADestructCmd cmd = new ADestructCmd();
            ADestructCommand command = new ADestructCommand();
            command.SetDestruct(new TDestruct(pos, 0));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeRem(new TIntegerConstant(Convert.ToString(sizeRem, CultureInfo.InvariantCulture), pos, 0));
            command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeSave(new TIntegerConstant(Convert.ToString(sizeSave, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetDestructCommand(command);

            return cmd;
        }

        private static PCmd ConvertBpCmd(NCSInstruction inst, int pos)
        {
            int typeVal = GetQualifier(inst.InsType);

            AST.ABpCmd cmd = new AST.ABpCmd();
            AST.ABpCommand command = new AST.ABpCommand();

            if (inst.InsType == NCSInstructionType.SAVEBP)
            {
                AST.ASavebpBpOp bpOp = new AST.ASavebpBpOp();
                bpOp.SetSavebp(new AST.TSavebp(pos, 0));
                command.SetBpOp(bpOp);
            }
            else
            {
                AST.ARestorebpBpOp bpOp = new AST.ARestorebpBpOp();
                bpOp.SetRestorebp(new AST.TRestorebp(pos, 0));
                command.SetBpOp(bpOp);
            }

            command.SetPos(new AST.TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetType(new AST.TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new AST.TSemi(pos, 0));
            cmd.SetBpCommand(command);

            return cmd;
        }

        private static PCmd ConvertStoreStateCmd(NCSInstruction inst, int pos)
        {
            int offset = GetArgAsInt(inst, 0);
            int sizeBp = inst.Args != null && inst.Args.Count > 1 ? GetArgAsInt(inst, 1) : 0;
            int sizeSp = inst.Args != null && inst.Args.Count > 2 ? GetArgAsInt(inst, 2) : 0;

            AStoreStateCmd cmd = new AStoreStateCmd();
            AStoreStateCommand command = new AStoreStateCommand();
            command.SetStorestate(new TStorestate(pos, 0));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));
            command.SetOffset(new TIntegerConstant(Convert.ToString(offset, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeBp(new TIntegerConstant(Convert.ToString(sizeBp, CultureInfo.InvariantCulture), pos, 0));
            command.SetSizeSp(new TIntegerConstant(Convert.ToString(sizeSp, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetStoreStateCommand(command);

            return cmd;
        }

        private static PCmd ConvertBinaryCmd(NCSInstruction inst, int pos)
        {
            ABinaryCmd cmd = new ABinaryCmd();
            ABinaryCommand command = new ABinaryCommand();

            command.SetBinaryOp(CreateBinaryOperator(inst.InsType, pos));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));

            int resultSize = 1;
            command.SetSize(new TIntegerConstant(Convert.ToString(resultSize, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetBinaryCommand(command);

            return cmd;
        }

        private static PCmd ConvertUnaryCmd(NCSInstruction inst, int pos)
        {
            Debug($"DEBUG ConvertUnaryCmd: Converting {inst.InsType} at pos={pos}");
            AUnaryCmd cmd = new AUnaryCmd();
            AUnaryCommand command = new AUnaryCommand();

            command.SetUnaryOp(CreateUnaryOperator(inst.InsType, pos));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetUnaryCommand(command);

            Debug($"DEBUG ConvertUnaryCmd: Created AUnaryCmd with op={command.GetUnaryOp()?.GetType().Name}");

            return cmd;
        }

        private static PCmd ConvertLogiiCmd(NCSInstruction inst, int pos)
        {
            ALogiiCmd cmd = new ALogiiCmd();
            ALogiiCommand command = new ALogiiCommand();

            command.SetLogiiOp(CreateLogiiOperator(inst.InsType, pos));
            command.SetPos(new TIntegerConstant(Convert.ToString(pos, CultureInfo.InvariantCulture), pos, 0));

            int typeVal = GetQualifier(inst.InsType);
            command.SetType(new TIntegerConstant(Convert.ToString(typeVal, CultureInfo.InvariantCulture), pos, 0));
            command.SetSemi(new TSemi(pos, 0));
            cmd.SetLogiiCommand(command);

            return cmd;
        }

        private static PBinaryOp CreateBinaryOperator(NCSInstructionType insType, int pos)
        {
            if (insType == NCSInstructionType.USHRIGHTII)
            {
                return new AUnrightBinaryOp();
            }

            string insName = insType.ToString();
            if (insName.StartsWith("ADD"))
            {
                return new AAddBinaryOp();
            }

            if (insName.StartsWith("SUB"))
            {
                return new ASubBinaryOp();
            }

            if (insName.StartsWith("MUL"))
            {
                return new AMulBinaryOp();
            }

            if (insName.StartsWith("DIV"))
            {
                return new ADivBinaryOp();
            }

            if (insName.StartsWith("MOD"))
            {
                return new AModBinaryOp();
            }

            if (insName.StartsWith("EQUAL"))
            {
                return new AEqualBinaryOp();
            }

            if (insName.StartsWith("NEQUAL"))
            {
                return new ANequalBinaryOp();
            }

            if (insName.StartsWith("GT"))
            {
                return new AGtBinaryOp();
            }

            if (insName.StartsWith("LT"))
            {
                return new ALtBinaryOp();
            }

            if (insName.StartsWith("GEQ"))
            {
                return new AGeqBinaryOp();
            }

            if (insName.StartsWith("LEQ"))
            {
                return new ALeqBinaryOp();
            }

            if (insName.StartsWith("SHLEFT"))
            {
                return new AShleftBinaryOp();
            }

            if (insName.StartsWith("SHRIGHT"))
            {
                return new AShrightBinaryOp();
            }

            if (insName.StartsWith("USHRIGHT"))
            {
                return new AUnrightBinaryOp();
            }

            System.Diagnostics.Debug.WriteLine("Unknown binary operator: " + insType + ", creating placeholder");
            return new PlaceholderBinaryOp();
        }

        private static PUnaryOp CreateUnaryOperator(NCSInstructionType insType, int pos)
        {
            if (insType == NCSInstructionType.NEGI || insType == NCSInstructionType.NEGF)
            {
                return new ANegUnaryOp();
            }

            if (insType == NCSInstructionType.NOTI)
            {
                return new ANotUnaryOp();
            }

            if (insType == NCSInstructionType.COMPI)
            {
                return new ACompUnaryOp();
            }

            string insName = insType.ToString();
            if (insName.StartsWith("NEG"))
            {
                return new ANegUnaryOp();
            }

            if (insName.StartsWith("NOT"))
            {
                return new ANotUnaryOp();
            }

            if (insName.StartsWith("COMP"))
            {
                return new ACompUnaryOp();
            }

            System.Diagnostics.Debug.WriteLine("Unknown unary operator: " + insType + ", creating placeholder");
            return new PlaceholderUnaryOp();
        }

        private static PLogiiOp CreateLogiiOperator(NCSInstructionType insType, int pos)
        {
            if (insType == NCSInstructionType.LOGANDII)
            {
                return new AAndLogiiOp();
            }

            if (insType == NCSInstructionType.LOGORII)
            {
                return new AOrLogiiOp();
            }

            if (insType == NCSInstructionType.BOOLANDII)
            {
                return new ABitAndLogiiOp();
            }

            if (insType == NCSInstructionType.EXCORII)
            {
                return new AExclOrLogiiOp();
            }

            if (insType == NCSInstructionType.INCORII)
            {
                return new AInclOrLogiiOp();
            }

            string insName = insType.ToString();
            if (insName.StartsWith("LOGAND"))
            {
                return new AAndLogiiOp();
            }

            if (insName.StartsWith("LOGOR"))
            {
                return new AOrLogiiOp();
            }

            if (insName.StartsWith("BOOLAND"))
            {
                return new ABitAndLogiiOp();
            }

            if (insName.StartsWith("EXCOR"))
            {
                return new AExclOrLogiiOp();
            }

            if (insName.StartsWith("INCOR"))
            {
                return new AInclOrLogiiOp();
            }

            System.Diagnostics.Debug.WriteLine("Unknown logical operator: " + insType + ", creating placeholder");
            return new PlaceholderLogiiOp();
        }

        private static int GetQualifier(NCSInstructionType insType)
        {
            return insType.GetValue().Qualifier;
        }

        private static int GetArgAsInt(NCSInstruction inst, int index)
        {
            if (inst.Args != null && inst.Args.Count > index && inst.Args[index] != null)
            {
                object value = inst.Args[index];
                if (value is uint uintVal)
                {
                    return unchecked((int)uintVal);
                }

                if (value is long longVal)
                {
                    return unchecked((int)longVal);
                }

                if (value is IConvertible convertible)
                {
                    return convertible.ToInt32(CultureInfo.InvariantCulture);
                }

                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            return 0;
        }

        private static double GetArgAsDouble(NCSInstruction inst, int index)
        {
            if (inst.Args != null && inst.Args.Count > index && inst.Args[index] != null)
            {
                return Convert.ToDouble(inst.Args[index], CultureInfo.InvariantCulture);
            }

            return 0.0;
        }

        private static string GetArgAsString(NCSInstruction inst, int index, string defaultValue)
        {
            if (inst.Args != null && inst.Args.Count > index && inst.Args[index] != null)
            {
                return Convert.ToString(inst.Args[index], CultureInfo.InvariantCulture) ?? defaultValue;
            }

            return defaultValue;
        }

        // swkotor2.exe: 0x004eb750 - Calculate the end position (exclusive) of an entry stub pattern starting at the given index
        // Entry stub patterns include:
        // - [RSADD*], JSR, RETN (functions with return values)
        // - [RSADD*], JSR, RESTOREBP (external compiler pattern with return values)
        // - JSR, RETN (void functions without RSADD*)
        // - JSR, RESTOREBP (external compiler pattern without RSADD*)
        // Returns the end index (exclusive) of the entry stub, or startIndex if no pattern is found
        private static int CalculateEntryStubEnd(List<NCSInstruction> instructions, int startIndex, NCS ncsFile)
        {
            if (instructions == null || startIndex < 0 || startIndex >= instructions.Count)
            {
                return startIndex; // No pattern found, return start index
            }

            // Use HasEntryStubPattern to check if there's a pattern
            // This method is defined as a local function in the caller, so we need to inline the logic here
            // However, since we need to calculate the end position, we'll implement the pattern detection inline

            // Need at least 2 instructions for JSR+RETN or JSR+RESTOREBP
            if (instructions.Count < startIndex + 2)
            {
                return startIndex; // Not enough instructions
            }

            int jsrOffset = 0;

            // Check if entry stub starts with RSADD* (function returns a value)
            // Helper to check if an instruction is an RSADD* variant
            bool IsRsaddInstruction(NCSInstructionType insType)
            {
                return insType == NCSInstructionType.RSADDI ||
                       insType == NCSInstructionType.RSADDF ||
                       insType == NCSInstructionType.RSADDS ||
                       insType == NCSInstructionType.RSADDO ||
                       insType == NCSInstructionType.RSADDEFF ||
                       insType == NCSInstructionType.RSADDEVT ||
                       insType == NCSInstructionType.RSADDLOC ||
                       insType == NCSInstructionType.RSADDTAL;
            }

            if (IsRsaddInstruction(instructions[startIndex].InsType))
            {
                jsrOffset = 1; // JSR is at position startIndex + 1
                // Need at least 3 instructions for RSADD* + JSR + RETN/RESTOREBP
                if (instructions.Count < startIndex + 3)
                {
                    return startIndex; // Not enough instructions
                }
            }

            int jsrIdx = startIndex + jsrOffset;

            // Pattern 1: [RSADD*], JSR followed by RETN (simple entry stub)
            if (instructions.Count > jsrIdx + 1 &&
                instructions[jsrIdx].InsType == NCSInstructionType.JSR &&
                instructions[jsrIdx].Jump != null &&
                instructions[jsrIdx + 1].InsType == NCSInstructionType.RETN)
            {
                // Entry stub ends after RETN (exclusive index)
                return jsrIdx + 2;
            }

            // Pattern 2: [RSADD*], JSR, RESTOREBP (entry stub with RESTOREBP, used by external compiler)
            // swkotor2.exe: 0x004eb750 - If RESTOREBP is followed by MOVSP+RETN+RETN at the end, it's cleanup code, not entry stub
            if (instructions.Count > jsrIdx + 1 &&
                instructions[jsrIdx].InsType == NCSInstructionType.JSR &&
                instructions[jsrIdx].Jump != null &&
                instructions[jsrIdx + 1].InsType == NCSInstructionType.RESTOREBP)
            {
                int restorebpIndex = jsrIdx + 1;
                // Check if RESTOREBP is followed by cleanup code at the end of the file
                if (IsRestorebpFollowedByCleanupCode(instructions, restorebpIndex))
                {
                    // This is cleanup code, not an entry stub
                    return startIndex;
                }
                // Entry stub ends after RESTOREBP (exclusive index)
                return jsrIdx + 2;
            }

            // Fallback: Check if first instruction IS JSR (void main pattern without RSADD*)
            if (jsrOffset == 0 && // Only check if we didn't find RSADD* prefix
                instructions[startIndex].InsType == NCSInstructionType.JSR &&
                instructions[startIndex].Jump != null &&
                instructions.Count > startIndex + 1 &&
                instructions[startIndex + 1].InsType == NCSInstructionType.RETN)
            {
                // Entry stub ends after RETN (exclusive index)
                return startIndex + 2;
            }

            // No entry stub pattern found
            return startIndex;
        }

        // swkotor2.exe: 0x004eb750 - Check if RESTOREBP is followed by cleanup code pattern (MOVSP+RETN+RETN) at the end of the file
        // If RESTOREBP is followed by MOVSP+RETN+RETN at the end, it's cleanup code, not an entry stub
        // This distinguishes between:
        // - Entry stub: JSR + RESTOREBP (followed by main code)
        // - Cleanup code: RESTOREBP + MOVSP + RETN + RETN (at the end of file)
        private static bool IsRestorebpFollowedByCleanupCode(List<NCSInstruction> instructions, int restorebpIndex)
        {
            if (instructions == null || restorebpIndex < 0 || restorebpIndex >= instructions.Count)
            {
                return false;
            }

            // Check if RESTOREBP is at the specified index
            if (instructions[restorebpIndex].InsType != NCSInstructionType.RESTOREBP)
            {
                return false;
            }

            // Check if RESTOREBP is followed by MOVSP+RETN+RETN pattern
            // Pattern must be at the END of the file (within last 4 instructions: RESTOREBP, MOVSP, RETN, RETN)
            int patternStart = restorebpIndex + 1;
            if (patternStart + 2 < instructions.Count)
            {
                // Check for MOVSP, RETN, RETN pattern after RESTOREBP
                if (instructions[patternStart].InsType == NCSInstructionType.MOVSP &&
                    instructions[patternStart + 1].InsType == NCSInstructionType.RETN &&
                    instructions[patternStart + 2].InsType == NCSInstructionType.RETN)
                {
                    // Verify this pattern is at the end of the file
                    // The last instruction should be the second RETN
                    if (patternStart + 2 == instructions.Count - 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // swkotor2.exe: 0x004eb750 - Detect if code after entry stub is cleanup code or real main code
        // Entry stub (JSR+RETN) is just a wrapper - actual main code is after globals initialization
        // This method determines if code after entry stub is:
        // - Cleanup code: MOVSP+RETN+RETN pattern (or similar cleanup patterns)
        // - Real main code: ACTION instructions, meaningful control flow, etc.
        // Returns true if code after entry stub is cleanup code, false if it's real main code
        private static bool IsCodeAfterEntryStubCleanup(List<NCSInstruction> instructions, int entryStubEnd)
        {
            if (instructions == null || entryStubEnd < 0 || entryStubEnd >= instructions.Count)
            {
                return false; // Can't determine, assume it's not cleanup
            }

            int codeAfterStub = instructions.Count - entryStubEnd;

            // If there's no code after entry stub, it's not cleanup (edge case)
            if (codeAfterStub == 0)
            {
                return false;
            }

            // Check for cleanup patterns at the end of the file
            // Pattern 1: MOVSP+RETN+RETN (standard cleanup pattern)
            if (codeAfterStub >= 3)
            {
                int movspIdx = entryStubEnd;
                int retn1Idx = entryStubEnd + 1;
                int retn2Idx = entryStubEnd + 2;

                if (movspIdx < instructions.Count && retn1Idx < instructions.Count && retn2Idx < instructions.Count &&
                    instructions[movspIdx].InsType == NCSInstructionType.MOVSP &&
                    instructions[retn1Idx].InsType == NCSInstructionType.RETN &&
                    instructions[retn2Idx].InsType == NCSInstructionType.RETN &&
                    retn2Idx == instructions.Count - 1) // Must be at the very end
                {
                    // This is cleanup code pattern - check if there's any meaningful code before it
                    // If there are only these 3 instructions, it's cleanup
                    if (codeAfterStub == 3)
                    {
                        return true;
                    }

                    // If there are more instructions, check if they're meaningful (ACTION, JSR, etc.)
                    // or just more cleanup/stack management
                    bool hasMeaningfulCode = false;
                    for (int i = entryStubEnd; i < movspIdx; i++)
                    {
                        if (i < instructions.Count)
                        {
                            NCSInstructionType insType = instructions[i].InsType;
                            // Meaningful instructions that indicate real main code
                            if (insType == NCSInstructionType.ACTION ||
                                insType == NCSInstructionType.JSR ||
                                insType == NCSInstructionType.JMP ||
                                insType == NCSInstructionType.JZ ||
                                insType == NCSInstructionType.JNZ ||
                                insType == NCSInstructionType.CONSTI ||
                                insType == NCSInstructionType.CONSTF ||
                                insType == NCSInstructionType.CONSTS ||
                                insType == NCSInstructionType.CONSTO)
                            {
                                hasMeaningfulCode = true;
                                break;
                            }
                        }
                    }

                    // If no meaningful code before cleanup pattern, it's all cleanup
                    return !hasMeaningfulCode;
                }
            }

            // Pattern 2: MOVSP+RETN (alternative cleanup pattern, shorter)
            if (codeAfterStub >= 2)
            {
                int movspIdx = entryStubEnd;
                int retnIdx = entryStubEnd + 1;

                if (movspIdx < instructions.Count && retnIdx < instructions.Count &&
                    instructions[movspIdx].InsType == NCSInstructionType.MOVSP &&
                    instructions[retnIdx].InsType == NCSInstructionType.RETN &&
                    retnIdx == instructions.Count - 1) // Must be at the very end
                {
                    // This is cleanup code pattern
                    return true;
                }
            }

            // Pattern 3: Just RETN at the end (minimal cleanup)
            if (codeAfterStub == 1)
            {
                int retnIdx = entryStubEnd;
                if (retnIdx < instructions.Count &&
                    instructions[retnIdx].InsType == NCSInstructionType.RETN &&
                    retnIdx == instructions.Count - 1)
                {
                    // Single RETN at the end - likely cleanup if entry stub exists
                    return true;
                }
            }

            // If we get here, there's likely real main code after entry stub
            // Check for meaningful instructions that indicate real code
            int meaningfulInstructionCount = 0;
            int maxCheck = Math.Min(entryStubEnd + 10, instructions.Count); // Check up to 10 instructions after stub
            for (int i = entryStubEnd; i < maxCheck; i++)
            {
                if (i < instructions.Count)
                {
                    NCSInstructionType insType = instructions[i].InsType;
                    // Count meaningful instructions that indicate real main code
                    if (insType == NCSInstructionType.ACTION ||
                        insType == NCSInstructionType.JSR ||
                        insType == NCSInstructionType.JMP ||
                        insType == NCSInstructionType.JZ ||
                        insType == NCSInstructionType.JNZ)
                    {
                        meaningfulInstructionCount++;
                    }
                }
            }

            // If we find meaningful instructions, it's real main code, not cleanup
            return meaningfulInstructionCount == 0;
        }

        private class PlaceholderBinaryOp : PBinaryOp
        {
            public override object Clone()
            {
                return new PlaceholderBinaryOp();
            }
            public override void Apply(Switch sw)
            {
                ((AnalysisAdapter)sw).DefaultCase(this);
            }

            public override void RemoveChild(Node child)
            {
            }

            public override void ReplaceChild(Node oldChild, Node newChild)
            {
            }
        }

        private class PlaceholderUnaryOp : PUnaryOp
        {
            public override object Clone()
            {
                return new PlaceholderUnaryOp();
            }
            public override void Apply(Switch sw)
            {
                ((AnalysisAdapter)sw).DefaultCase(this);
            }

            public override void RemoveChild(Node child)
            {
            }

            public override void ReplaceChild(Node oldChild, Node newChild)
            {
            }
        }

        private class PlaceholderLogiiOp : PLogiiOp
        {
            public override object Clone()
            {
                return new PlaceholderLogiiOp();
            }
            public override void Apply(Switch sw)
            {
                ((AnalysisAdapter)sw).DefaultCase(this);
            }

            public override void RemoveChild(Node child)
            {
            }

            public override void ReplaceChild(Node oldChild, Node newChild)
            {
            }
        }
    }
}





