using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Scripting.EngineApi
{
    /// <summary>
    /// Base implementation of engine API with common functions.
    /// </summary>
    /// <remarks>
    /// Base Engine API:
    /// - Based on swkotor2.exe NWScript engine API system
    /// - Located via string references: ACTION opcode handler dispatches to engine function implementations
    /// - "PRINTSTRING: %s\n" @ 0x007c29f8 (PrintString function debug output format)
    /// - "ActionList" @ 0x007bebdc (action list GFF field), "ActionId" @ 0x007bebd0, "ActionType" @ 0x007bf7f8
    /// - PrintString implementation: FUN_005c4ff0 @ 0x005c4ff0 (prints string with "PRINTSTRING: %s\n" format)
    /// - ActionList loading: FUN_00508260 @ 0x00508260 (loads ActionList from GFF, parses ActionId, GroupActionId, NumParams, Paramaters)
    ///   - Original implementation (from decompiled FUN_00508260):
    ///     - Reads "ActionList" list from GFF structure
    ///     - For each action entry, reads:
    ///       - ActionId (int32): Action type identifier
    ///       - GroupActionId (int16): Group action identifier
    ///       - NumParams (int16): Number of parameters (0-13 max)
    ///       - Paramaters (list): Parameter list with Type and Value fields
    ///     - Parameter types: 1 = int, 2 = float, 3 = int (signed), 4 = string, 5 = object
    ///     - Parameter values: Stored as Type-specific values (int, float, string, object)
    ///     - Calls FUN_00507fd0 to create action from parsed parameters
    ///     - Cleans up allocated parameter memory after action creation
    /// - Original implementation: Common NWScript functions shared between K1 and K2
    /// - Object constants: OBJECT_INVALID (0x7F000000), OBJECT_SELF (0x7F000001)
    /// - ACTION opcode: Calls engine function by routine ID (uint16 routineId + uint8 argCount)
    /// - Function dispatch: Original engine uses dispatch table indexed by routine ID to call function implementations
    /// - Routine IDs: Match function indices from nwscript.nss compilation (0-based index into function table)
    /// - Function signature: All functions receive variable arguments list and execution context (caller, triggerer, world, globals)
    /// - Return value: Functions return Variable (can be int, float, string, object, location, void)
    /// - Default return values: Missing arguments default to 0, empty string, OBJECT_INVALID, etc.
    /// - Function implementations must match original engine behavior for script compatibility
    /// - Error handling: Functions should handle invalid arguments gracefully (return defaults, don't crash)
    /// - Common functions: PrintString, Random, GetTag, GetObjectByTag, GetLocalInt, SetLocalInt, GetGlobalInt, SetGlobalInt
    /// - Math functions: fabs, cos, sin, tan, acos, asin, atan, log, pow, sqrt, abs
    /// - String functions: GetStringLength, GetStringUpperCase, GetStringLowerCase, GetStringRight, GetStringLeft, InsertString, GetSubString, FindSubString
    /// - Dice functions: d2, d3, d4, d6, d8, d10, d12, d20, d100 (D20 system dice rolls)
    /// - Object functions: GetPosition, GetFacing, GetDistanceToObject, GetIsObjectValid, GetObjectType
    /// - Action functions: AssignCommand, DelayCommand, ExecuteScript, ClearAllActions, SetFacing
    /// </remarks>
    public abstract class BaseEngineApi : IEngineApi
    {
        protected readonly Random _random;
        protected readonly Dictionary<int, string> _functionNames;
        protected readonly HashSet<int> _implementedFunctions;

        public const uint ObjectInvalid = 0x7F000000;
        public const uint ObjectSelf = 0x7F000001;

        protected BaseEngineApi()
        {
            _random = new Random();
            _functionNames = new Dictionary<int, string>();
            _implementedFunctions = new HashSet<int>();
            RegisterFunctions();
        }

        protected abstract void RegisterFunctions();

        public abstract Variable CallEngineFunction(int routineId, IReadOnlyList<Variable> args, IExecutionContext ctx);

        public string GetFunctionName(int routineId)
        {
            if (_functionNames.TryGetValue(routineId, out string name))
            {
                return name;
            }
            return "Unknown_" + routineId;
        }

        public int GetArgumentCount(int routineId)
        {
            // This would be populated from ScriptDefs
            return -1;
        }

        public bool IsImplemented(int routineId)
        {
            return _implementedFunctions.Contains(routineId);
        }

        #region Common Functions

        /// <summary>
        /// Random(int nMax) - Returns a random integer between 0 and nMax-1
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Random implementation
        /// Located via string references: "Random" @ 0x007c1080 (random number generation)
        /// Original implementation: Returns random integer in range [0, nMax) using engine RNG
        /// Returns 0 if nMax <= 0
        /// </remarks>
        protected Variable Func_Random(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int max = args.Count > 0 ? args[0].AsInt() : 0;
            if (max <= 0)
            {
                return Variable.FromInt(0);
            }
            return Variable.FromInt(_random.Next(max));
        }

        /// <summary>
        /// PrintString(string sString) - Prints a string to the console/log
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: PrintString implementation
        /// Located via string references: "PRINTSTRING: %s\n" @ 0x007c29f8 (PrintString debug output format)
        /// Original implementation: FUN_005c4ff0 @ 0x005c4ff0 (prints string with "PRINTSTRING: %s\n" format to console/log)
        ///   - Original implementation (from decompiled FUN_005c4ff0):
        ///     - Function signature: `undefined4 FUN_005c4ff0(undefined4 param_1, int param_2)`
        ///     - param_1: Execution context pointer
        ///     - param_2: Parameter count (requires at least 2 parameters for valid call)
        ///     - Parameter validation: If param_2 < 2, skips parameter reading and uses default format
        ///     - Parameter reading: Calls FUN_0061cc20 to read parameter value from execution context
        ///     - Parameter type check: If parameter type is 1 (string type), uses "PRINTSTRING: %s\n" format
        ///     - Format string: "PRINTSTRING: %s\n" @ 0x007c29f8 (used when parameter type is string)
        ///     - Alternative format: Uses different format string (DAT_007b8f34) if parameter type is not string
        ///     - Output: Calls FUN_006306c0 to output formatted string to console/log via engine logging system
        ///     - Return value: Returns 0 on success, 0xfffff82f (error code) on failure
        ///     - Error handling: Returns error code if parameter reading fails or execution context is invalid
        /// </remarks>
        protected Variable Func_PrintString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Based on swkotor2.exe: FUN_005c4ff0 @ 0x005c4ff0
            // Located via string reference: "PRINTSTRING: %s\n" @ 0x007c29f8
            // Original implementation: Checks parameter count, formats with "PRINTSTRING: %s\n", outputs to console/log
            string msg = args.Count > 0 ? args[0].AsString() : string.Empty;
            Console.WriteLine("PRINTSTRING: {0}\n", msg);
            return Variable.Void();
        }

        protected Variable Func_PrintInteger(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int value = args.Count > 0 ? args[0].AsInt() : 0;
            Console.WriteLine("[Script] " + value);
            return Variable.Void();
        }

        protected Variable Func_PrintFloat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            Console.WriteLine("[Script] " + value);
            return Variable.Void();
        }

        protected Variable Func_IntToString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int value = args.Count > 0 ? args[0].AsInt() : 0;
            return Variable.FromString(value.ToString());
        }

        protected Variable Func_FloatToString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            int width = args.Count > 1 ? args[1].AsInt() : 18;
            int decimals = args.Count > 2 ? args[2].AsInt() : 9;
            return Variable.FromString(value.ToString("F" + decimals));
        }

        protected Variable Func_StringToInt(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            int.TryParse(s, out int result);
            return Variable.FromInt(result);
        }

        protected Variable Func_StringToFloat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            float.TryParse(s, out float result);
            return Variable.FromFloat(result);
        }

        protected Variable Func_GetTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                return Variable.FromString(entity.Tag ?? string.Empty);
            }
            return Variable.FromString(string.Empty);
        }

        /// <summary>
        /// GetObjectByTag(string sTag, int nNth = 0) - Returns the object with the given tag
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: GetObjectByTag implementation
        /// Located via string references: "Tag" @ 0x007c1a18 (entity tag field for lookup)
        /// Original implementation: Searches world for entity with matching tag (case-insensitive), returns nth match
        /// Returns OBJECT_INVALID (0x7F000000) if not found
        /// </remarks>
        protected Variable Func_GetObjectByTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string tag = args.Count > 0 ? args[0].AsString() : string.Empty;
            int nth = args.Count > 1 ? args[1].AsInt() : 0;

            Core.Interfaces.IEntity entity = ctx.World.GetEntityByTag(tag, nth);
            return Variable.FromObject(entity?.ObjectId ?? ObjectInvalid);
        }

        protected Variable Func_GetLocalInt(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string name = args.Count > 1 ? args[1].AsString() : string.Empty;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                return Variable.FromInt(ctx.Globals.GetLocalInt(entity, name));
            }
            return Variable.FromInt(0);
        }

        protected Variable Func_SetLocalInt(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string name = args.Count > 1 ? args[1].AsString() : string.Empty;
            int value = args.Count > 2 ? args[2].AsInt() : 0;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                ctx.Globals.SetLocalInt(entity, name, value);
            }
            return Variable.Void();
        }

        protected Variable Func_GetLocalFloat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string name = args.Count > 1 ? args[1].AsString() : string.Empty;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                return Variable.FromFloat(ctx.Globals.GetLocalFloat(entity, name));
            }
            return Variable.FromFloat(0f);
        }

        protected Variable Func_SetLocalFloat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string name = args.Count > 1 ? args[1].AsString() : string.Empty;
            float value = args.Count > 2 ? args[2].AsFloat() : 0f;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                ctx.Globals.SetLocalFloat(entity, name, value);
            }
            return Variable.Void();
        }

        protected Variable Func_GetLocalString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string name = args.Count > 1 ? args[1].AsString() : string.Empty;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                return Variable.FromString(ctx.Globals.GetLocalString(entity, name));
            }
            return Variable.FromString(string.Empty);
        }

        protected Variable Func_SetLocalString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string name = args.Count > 1 ? args[1].AsString() : string.Empty;
            string value = args.Count > 2 ? args[2].AsString() : string.Empty;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                ctx.Globals.SetLocalString(entity, name, value);
            }
            return Variable.Void();
        }

        protected Variable Func_GetIsObjectValid(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;

            if (objectId == ObjectInvalid)
            {
                return Variable.FromInt(0);
            }

            Core.Interfaces.IEntity entity = ctx.World.GetEntity(objectId);
            return Variable.FromInt(entity != null && entity.IsValid ? 1 : 0);
        }

        protected Variable Func_GetModule(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Module is a special object - return a placeholder ID
            return Variable.FromObject(0x7F000002);
        }

        protected Variable Func_GetArea(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            // Area is a special object - return a placeholder ID
            return Variable.FromObject(0x7F000003);
        }

        protected Variable Func_GetGlobalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromInt(ctx.Globals.GetGlobalInt(name));
        }

        protected Variable Func_SetGlobalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            int value = args.Count > 1 ? args[1].AsInt() : 0;
            ctx.Globals.SetGlobalInt(name, value);
            return Variable.Void();
        }

        protected Variable Func_GetGlobalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromInt(ctx.Globals.GetGlobalBool(name) ? 1 : 0);
        }

        protected Variable Func_SetGlobalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            bool value = args.Count > 1 && args[1].AsInt() != 0;
            ctx.Globals.SetGlobalBool(name, value);
            return Variable.Void();
        }

        protected Variable Func_GetGlobalString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromString(ctx.Globals.GetGlobalString(name));
        }

        protected Variable Func_SetGlobalString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            string value = args.Count > 1 ? args[1].AsString() : string.Empty;
            ctx.Globals.SetGlobalString(name, value);
            return Variable.Void();
        }

        protected Variable Func_GetNearestObjectByTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string tag = args.Count > 0 ? args[0].AsString() : string.Empty;
            uint target = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            int nth = args.Count > 2 ? args[2].AsInt() : 0;

            Core.Interfaces.IEntity targetEntity = ResolveObject(target, ctx);
            if (targetEntity == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            Core.Interfaces.Components.ITransformComponent transform = targetEntity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            if (transform == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get all entities with this tag
            var candidates = new List<Core.Interfaces.IEntity>();
            int index = 0;
            Core.Interfaces.IEntity candidate;
            while ((candidate = ctx.World.GetEntityByTag(tag, index)) != null)
            {
                if (candidate.ObjectId != targetEntity.ObjectId)
                {
                    candidates.Add(candidate);
                }
                index++;
            }

            // Sort by distance
            candidates.Sort((a, b) =>
            {
                Core.Interfaces.Components.ITransformComponent ta = a.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                Core.Interfaces.Components.ITransformComponent tb = b.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                if (ta == null || tb == null) return 0;

                float distA = (ta.Position - transform.Position).Length();
                float distB = (tb.Position - transform.Position).Length();
                return distA.CompareTo(distB);
            });

            if (nth < candidates.Count)
            {
                return Variable.FromObject(candidates[nth].ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        protected Variable Func_ObjectToString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            return Variable.FromString("0x" + objectId.ToString("X8"));
        }

        protected Variable Func_StringToObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;

            // Parse hex string like "0x7F000001"
            if (s.StartsWith("0x") || s.StartsWith("0X"))
            {
                s = s.Substring(2);
            }

            if (uint.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out uint objectId))
            {
                return Variable.FromObject(objectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        protected Variable Func_PrintVector(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count > 0)
            {
                Vector3 vec = args[0].AsVector();
                Console.WriteLine("[Script] Vector(" + vec.X + ", " + vec.Y + ", " + vec.Z + ")");
            }
            return Variable.Void();
        }

        protected Variable Func_VectorToString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count > 0)
            {
                Vector3 vec = args[0].AsVector();
                return Variable.FromString("(" + vec.X + ", " + vec.Y + ", " + vec.Z + ")");
            }
            return Variable.FromString("(0, 0, 0)");
        }

        protected Core.Interfaces.IEntity ResolveObject(uint objectId, IExecutionContext ctx)
        {
            if (objectId == ObjectInvalid)
            {
                return null;
            }

            if (objectId == ObjectSelf)
            {
                return ctx.Caller;
            }

            return ctx.World.GetEntity(objectId);
        }

        #endregion
    }
}

