using System;
using System.Collections.Generic;
using Andastra.Parsing.Common.Script;
using Andastra.Runtime.Scripting.EngineApi;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Runtime.Engines.Aurora.EngineApi
{
    /// <summary>
    /// Aurora Engine engine API implementation for Neverwinter Nights and Neverwinter Nights 2.
    /// </summary>
    /// <remarks>
    /// Aurora Engine API (NWScript Functions):
    /// - Based on nwmain.exe (NWN) and nwn2main.exe (NWN2) NWScript engine API implementations
    /// - Located via string references: Script function dispatch system handles ACTION opcodes in NCS VM
    /// - Original implementation: NCS VM executes ACTION opcode (0x2A) with routine ID, calls engine function handlers
    /// - Function IDs match nwscript.nss definitions
    /// - NWN has ~600 engine functions, NWN2 has ~700 engine functions
    /// - Original engine uses function dispatch table indexed by routine ID
    /// - Function implementations must match NWScript semantics (parameter types, return types, behavior)
    /// - Reverse engineered from nwmain.exe using Ghidra MCP:
    ///   - Function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30 (nwmain.exe)
    ///   - ExecuteCommandRandom @ 0x1403148d0 (nwmain.exe: routine ID 0)
    ///   - ExecuteCommandPrintString @ 0x1403147c0 (nwmain.exe: routine ID 1)
    ///   - ExecuteCommandPrintFloat @ 0x1403144c0 (nwmain.exe: routine ID 2)
    ///   - ExecuteCommandFloatToString (nwmain.exe: routine ID 3)
    ///   - ExecuteCommandPrintInteger @ 0x140314640 (nwmain.exe: routine ID 4)
    ///   - ExecuteCommandPrintObject @ 0x140314700 (nwmain.exe: routine ID 5)
    ///   - ExecuteCommandGetPosition (nwmain.exe: routine ID 27)
    ///   - ExecuteCommandGetFacing (nwmain.exe: routine ID 28)
    ///   - ExecuteCommandGetDistanceToObject (nwmain.exe: routine ID 41)
    ///   - ExecuteCommandGetObjectValid (nwmain.exe: routine ID 42)
    ///   - ExecuteCommandGetTag @ 0x140540 (nwmain.exe: routine ID 168, offset 0x540 in dispatch table)
    ///   - ExecuteCommandGetObjectByTag @ 0x140640 (nwmain.exe: routine ID 200, offset 0x640 in dispatch table)
    ///   - Global variable functions (routine IDs 578-581)
    ///   - Local variable functions (routine IDs 679-682)
    /// </remarks>
    public class AuroraEngineApi : BaseEngineApi
    {
        // Dictionary-based function dispatch for efficient lookup
        // Maps routine ID to function handler delegate
        private readonly Dictionary<int, Func<IReadOnlyList<Variable>, IExecutionContext, Variable>> _functionDispatch;

        public AuroraEngineApi()
        {
            _functionDispatch = new Dictionary<int, Func<IReadOnlyList<Variable>, IExecutionContext, Variable>>();
        }

        protected override void RegisterFunctions()
        {
            // Register function names based on reverse-engineered dispatch table from nwmain.exe
            // Function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30 (nwmain.exe)

            // Basic I/O functions (routine IDs 0-5)
            RegisterFunctionName(0, "Random");
            RegisterFunctionName(1, "PrintString");
            RegisterFunctionName(2, "PrintFloat");
            RegisterFunctionName(3, "FloatToString");
            RegisterFunctionName(4, "PrintInteger");
            RegisterFunctionName(5, "PrintObject");

            // Action functions (routine IDs 6-10)
            RegisterFunctionName(6, "AssignCommand");
            RegisterFunctionName(7, "DelayCommand");
            RegisterFunctionName(8, "ExecuteScript");
            RegisterFunctionName(9, "ClearAllActions");
            RegisterFunctionName(10, "SetFacing");

            // Object functions (routine IDs 27-28, 41-42)
            RegisterFunctionName(27, "GetPosition");
            RegisterFunctionName(28, "GetFacing");
            RegisterFunctionName(41, "GetDistanceToObject");
            RegisterFunctionName(42, "GetIsObjectValid");

            // Tag functions (routine IDs 168, 200)
            RegisterFunctionName(168, "GetTag");
            RegisterFunctionName(200, "GetObjectByTag");

            // Global variable functions (routine IDs 578-581)
            RegisterFunctionName(578, "GetGlobalBoolean");
            RegisterFunctionName(579, "SetGlobalBoolean");
            RegisterFunctionName(580, "GetGlobalNumber");
            RegisterFunctionName(581, "SetGlobalNumber");

            // Local variable functions (routine IDs 679-682)
            RegisterFunctionName(679, "GetLocalInt");
            RegisterFunctionName(680, "SetLocalInt");
            RegisterFunctionName(681, "GetLocalFloat");
            RegisterFunctionName(682, "SetLocalFloat");

            // Initialize function dispatch dictionary
            InitializeFunctionDispatch();
        }

        /// <summary>
        /// Initializes the function dispatch dictionary with all implemented functions.
        /// Uses base class methods for common functions, Aurora-specific implementations for others.
        /// </summary>
        private void InitializeFunctionDispatch()
        {
            // Basic I/O functions - delegate to base class (common across all engines)
            _functionDispatch[0] = Func_Random;
            _functionDispatch[1] = Func_PrintString;
            _functionDispatch[2] = Func_PrintFloat;
            _functionDispatch[3] = Func_FloatToString;
            _functionDispatch[4] = Func_PrintInteger;
            _functionDispatch[5] = Func_PrintObject;

            // Action functions - delegate to base class (common across all engines)
            _functionDispatch[6] = Func_AssignCommand;
            _functionDispatch[7] = Func_DelayCommand;
            _functionDispatch[8] = Func_ExecuteScript;
            _functionDispatch[9] = Func_ClearAllActions;
            _functionDispatch[10] = Func_SetFacing;

            // Object functions - delegate to base class (common across all engines)
            _functionDispatch[27] = Func_GetPosition;
            _functionDispatch[28] = Func_GetFacing;
            _functionDispatch[41] = Func_GetDistanceToObject;
            _functionDispatch[42] = Func_GetIsObjectValid;

            // Tag functions - delegate to base class (common across all engines)
            _functionDispatch[168] = Func_GetTag;
            _functionDispatch[200] = Func_GetObjectByTag;

            // Global variable functions - delegate to base class (common across all engines)
            _functionDispatch[578] = Func_GetGlobalBoolean;
            _functionDispatch[579] = Func_SetGlobalBoolean;
            _functionDispatch[580] = Func_GetGlobalNumber;
            _functionDispatch[581] = Func_SetGlobalNumber;

            // Local variable functions - delegate to base class (common across all engines)
            _functionDispatch[679] = Func_GetLocalInt;
            _functionDispatch[680] = Func_SetLocalInt;
            _functionDispatch[681] = Func_GetLocalFloat;
            _functionDispatch[682] = Func_SetLocalFloat;
        }

        /// <summary>
        /// Helper method to register function names for debugging and logging.
        /// </summary>
        private void RegisterFunctionName(int routineId, string name)
        {
            _functionNames[routineId] = name;
            _implementedFunctions.Add(routineId);
        }

        public override Variable CallEngineFunction(int routineId, IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Use dictionary-based dispatch for efficient lookup
            // Based on nwmain.exe: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
            // Function dispatch table is an array of function pointers indexed by routine ID
            if (_functionDispatch.TryGetValue(routineId, out Func<IReadOnlyList<Variable>, IExecutionContext, Variable> handler))
            {
                return handler(args, ctx);
            }

            // Fall back to unimplemented function logging
            string funcName = GetFunctionName(routineId);
            Console.WriteLine("[Aurora] Unimplemented function: " + routineId + " (" + funcName + ")");
            return Variable.Void();
        }

        #region Common Object Functions

        // GetPosition and GetFacing are now implemented in BaseEngineApi (common across all engines)
        // These methods delegate to the base class implementations

        #endregion

        #region Aurora-Specific Functions

        // Aurora-specific functions that differ from other engines will be implemented here
        // Examples of Aurora-specific functions that may differ from Odyssey:
        // - Area management functions
        // - Creature spawning functions
        // - Item creation functions
        // - Spell casting functions
        // - Dialogue system functions
        // - Combat system functions
        // - Party management functions

        #endregion
    }
}

