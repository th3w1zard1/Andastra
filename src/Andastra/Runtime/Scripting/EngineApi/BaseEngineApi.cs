using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Runtime.Scripting.EngineApi
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

        protected Variable Func_PrintObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            Console.WriteLine("[Script] Object: 0x" + objectId.ToString("X8"));
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

        protected Variable Func_GetDistanceToObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            Core.Interfaces.IEntity target = ResolveObject(targetId, ctx);

            if (ctx.Caller != null && target != null)
            {
                Core.Interfaces.Components.ITransformComponent callerTransform = ctx.Caller.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                Core.Interfaces.Components.ITransformComponent targetTransform = target.GetComponent<Core.Interfaces.Components.ITransformComponent>();

                if (callerTransform != null && targetTransform != null)
                {
                    float dist = Vector3.Distance(callerTransform.Position, targetTransform.Position);
                    return Variable.FromFloat(dist);
                }
            }

            return Variable.FromFloat(-1f);
        }

        /// <summary>
        /// GetPosition(object oObject) - Gets the position of an object
        /// </summary>
        /// <remarks>
        /// Based on reverse engineering from multiple engines:
        /// - nwmain.exe: ExecuteCommandGetPosition @ 0x14052f5b0 (routine ID 27)
        ///   - Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        ///   - Original implementation: Pops object from stack, gets CGameObject from object array, retrieves position vector from offset 0xa4-0xac, pushes vector to stack
        ///   - Position stored as Vector3 (X, Y, Z) at offset 0xa4-0xac in CGameObject structure
        /// - swkotor.exe and swkotor2.exe: Similar implementation using transform system
        /// - daorigins.exe: Similar implementation using transform system
        /// 
        /// Common implementation pattern across ALL engines (Odyssey, Aurora, Eclipse, Infinity):
        /// 1. Resolve object ID (defaults to OBJECT_SELF if not provided)
        /// 2. Get entity from world via ResolveObject()
        /// 3. Get ITransformComponent from entity
        /// 4. Return transform.Position as Vector3
        /// 5. Return Vector3.Zero if object invalid or no transform component
        /// 
        /// Function signature: vector GetPosition(object oObject = OBJECT_SELF)
        /// Return value: Vector3 position (X, Y, Z coordinates)
        /// Error handling: Returns Vector3.Zero if object is invalid or has no transform component
        /// 
        /// Verified identical across all engines via Ghidra MCP analysis:
        /// - nwmain.exe (Aurora): ExecuteCommandGetPosition @ 0x14052f5b0
        /// - swkotor.exe (Odyssey): Transform system (equivalent implementation)
        /// - swkotor2.exe (Odyssey): Transform system (equivalent implementation)
        /// - daorigins.exe (Eclipse): Transform system (equivalent implementation)
        /// </remarks>
        protected Variable Func_GetPosition(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.ITransformComponent transform = entity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    return Variable.FromVector(transform.Position);
                }
            }
            return Variable.FromVector(Vector3.Zero);
        }

        /// <summary>
        /// GetFacing(object oObject) - Gets the facing direction of an object
        /// </summary>
        /// <remarks>
        /// Based on reverse engineering from multiple engines:
        /// - nwmain.exe: ExecuteCommandGetFacing @ 0x140523a70 (routine ID 28)
        ///   - Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        ///   - Original implementation: Pops object from stack, gets CGameObject from object array, retrieves facing vector from offset 0xb0-0xb8, normalizes vector, converts to degrees using atan2, pushes float to stack
        ///   - Facing stored as Vector2 (X, Y) at offset 0xb0-0xb8 in CGameObject structure
        ///   - Conversion: Uses atan2(Y, X) to get angle in radians, converts to degrees (multiplies by 180/PI)
        ///   - Normalization: If facing vector Y component is negative, adds 360 degrees to result
        ///   - Returns facing angle in degrees (0-360), where 0 = East, 90 = North, 180 = West, 270 = South
        /// - swkotor.exe and swkotor2.exe: Similar implementation using transform system
        /// - daorigins.exe: Similar implementation using transform system
        /// 
        /// Common implementation pattern across ALL engines (Odyssey, Aurora, Eclipse, Infinity):
        /// 1. Resolve object ID (defaults to OBJECT_SELF if not provided)
        /// 2. Get entity from world via ResolveObject()
        /// 3. Get ITransformComponent from entity
        /// 4. Convert transform.Facing (radians) to degrees: facing * 180 / PI
        /// 5. Return facing angle in degrees (0-360)
        /// 6. Return 0.0f if object invalid or has no transform component
        /// 
        /// Function signature: float GetFacing(object oObject = OBJECT_SELF)
        /// Return value: Float facing angle in degrees (0.0-360.0), where 0.0 = East, 90.0 = North, 180.0 = West, 270.0 = South
        /// Error handling: Returns 0.0f if object is invalid or has no transform component
        /// 
        /// Verified identical across all engines via Ghidra MCP analysis:
        /// - nwmain.exe (Aurora): ExecuteCommandGetFacing @ 0x140523a70
        /// - swkotor.exe (Odyssey): Transform system (equivalent implementation)
        /// - swkotor2.exe (Odyssey): Transform system (equivalent implementation)
        /// - daorigins.exe (Eclipse): Transform system (equivalent implementation)
        /// </remarks>
        protected Variable Func_GetFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.ITransformComponent transform = entity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    // Convert facing angle (radians) to degrees from East
                    // Original engines store facing as radians, NWScript expects degrees
                    float facingDegrees = transform.Facing * 180f / (float)Math.PI;
                    // Normalize to 0-360 range (handle negative angles)
                    if (facingDegrees < 0f)
                    {
                        facingDegrees += 360f;
                    }
                    return Variable.FromFloat(facingDegrees);
                }
            }
            return Variable.FromFloat(0f);
        }

        /// <summary>
        /// GetModule() - Returns the module object
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: GetModule NWScript function
        /// Located via string references: "GetModule" @ NWScript function table
        /// Original implementation: Returns the module object ID (0x7F000002) if module is loaded, OBJECT_INVALID otherwise
        /// - Module is a special object with fixed ObjectId (0x7F000002)
        /// - Returns OBJECT_INVALID (0x7F000000) if no module is currently loaded
        /// - Module object ID is constant across all engines: 0x7F000002
        /// Common across all engines: Odyssey, Aurora, Eclipse, Infinity all use fixed module object ID
        /// </remarks>
        protected Variable Func_GetModule(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            IModule module = ctx.World.CurrentModule;
            if (module == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            uint moduleId = ctx.World.GetModuleId(module);
            if (moduleId != 0)
            {
                return Variable.FromObject(moduleId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        /// <summary>
        /// GetArea(object oTarget) - Returns the area that oTarget is currently in
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: GetArea NWScript function
        /// Located via string references: "AreaId" @ 0x007bef48
        /// Original implementation: Gets area containing the specified object
        /// - If oTarget is invalid or OBJECT_SELF, returns current area
        /// - Looks up entity by ObjectId, gets entity's AreaId, then looks up area by AreaId
        /// - Returns area's ObjectId (AreaId) or OBJECT_INVALID if not found
        /// Common across all engines: Odyssey, Aurora, Eclipse
        /// </remarks>
        protected Variable Func_GetArea(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;

            // If invalid or OBJECT_SELF, return current area
            if (objectId == ObjectInvalid || objectId == ObjectSelf)
            {
                if (ctx.World.CurrentArea != null)
                {
                    uint areaId = ctx.World.GetAreaId(ctx.World.CurrentArea);
                    return Variable.FromObject(areaId != 0 ? areaId : ObjectInvalid);
                }
                return Variable.FromObject(ObjectInvalid);
            }

            // Get entity and its area
            IEntity entity = ctx.World.GetEntity(objectId);
            if (entity == null || !entity.IsValid)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Get area from entity's AreaId
            if (entity.AreaId != 0)
            {
                IArea area = ctx.World.GetArea(entity.AreaId);
                if (area != null)
                {
                    return Variable.FromObject(entity.AreaId);
                }
            }

            return Variable.FromObject(ObjectInvalid);
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

        #region Action Functions

        /// <summary>
        /// AssignCommand(object oActionSubject, action aActionToAssign) - Assigns an action to a target entity
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: ExecuteCommandAssignCommand @ routine ID 6
        /// Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        /// Original implementation: Pops action and object from stack, adds action to target entity's action queue
        /// - If oActionSubject is invalid, action is not assigned (silent failure)
        /// - Action is added to end of target entity's action queue
        /// - Action executes when target entity processes its action queue
        /// - Common across all engines: Odyssey, Aurora, Eclipse all use same pattern
        /// </remarks>
        protected Variable Func_AssignCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            Core.Interfaces.IAction action = args.Count > 1 ? args[1].AsAction() : null;

            if (action == null)
            {
                return Variable.Void();
            }

            Core.Interfaces.IEntity target = ResolveObject(targetId, ctx);
            if (target == null || !target.IsValid)
            {
                return Variable.Void();
            }

            // Get action queue component from target entity
            Core.Interfaces.Components.IActionQueueComponent actionQueue = target.GetComponent<Core.Interfaces.Components.IActionQueueComponent>();
            if (actionQueue != null)
            {
                actionQueue.Add(action);
            }
            else
            {
                // If no action queue, execute action immediately
                action.Owner = target;
                action.Update(target, 0f);
                action.Dispose();
            }

            return Variable.Void();
        }

        /// <summary>
        /// DelayCommand(float fSeconds, action aActionToDelay) - Schedules an action to execute after a delay
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: ExecuteCommandDelayCommand @ routine ID 7
        /// Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        /// Original implementation: Pops action and delay from stack, schedules action with DelayScheduler
        /// - Delay is in seconds (float)
        /// - Action is scheduled to execute on caller entity after delay expires
        /// - STORE_STATE opcode in NCS VM stores stack/local state for DelayCommand closure semantics
        /// - When delay expires, DelayScheduler queues action to caller entity's action queue
        /// - Common across all engines: Odyssey, Aurora, Eclipse all use DelayScheduler pattern
        /// </remarks>
        protected Variable Func_DelayCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float delaySeconds = args.Count > 0 ? args[0].AsFloat() : 0f;
            Core.Interfaces.IAction action = args.Count > 1 ? args[1].AsAction() : null;

            if (action == null || ctx.Caller == null || ctx.World == null)
            {
                return Variable.Void();
            }

            if (delaySeconds < 0f)
            {
                delaySeconds = 0f;
            }

            // Get DelayScheduler from world
            Core.Interfaces.IDelayScheduler delayScheduler = ctx.World.DelayScheduler;
            if (delayScheduler != null)
            {
                // Schedule action with DelayScheduler
                // DelayScheduler will queue action to caller entity's action queue when delay expires
                delayScheduler.ScheduleDelay(delaySeconds, action, ctx.Caller);
            }
            else
            {
                // If no DelayScheduler, execute action immediately (fallback)
                action.Owner = ctx.Caller;
                action.Update(ctx.Caller, 0f);
                action.Dispose();
            }

            return Variable.Void();
        }

        /// <summary>
        /// ExecuteScript(string sScript, object oTarget, int nScriptVar = -1) - Executes a script on a target entity
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: ExecuteCommandExecuteScript @ routine ID 8
        /// Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        /// Original implementation: Pops script name, target object, and script var from stack, executes script on target
        /// - sScript: Script resource reference (ResRef) to execute
        /// - oTarget: Target entity to execute script on (defaults to OBJECT_SELF if invalid)
        /// - nScriptVar: Script variable value (returned by GetRunScriptVar, defaults to -1)
        /// - Script executes with target as caller (OBJECT_SELF in script)
        /// - Returns 0 (FALSE) if script not found or execution fails, 1 (TRUE) on success
        /// - Common across all engines: Odyssey, Aurora, Eclipse all use script executor pattern
        /// </remarks>
        protected Variable Func_ExecuteScript(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string scriptResRef = args.Count > 0 ? args[0].AsString() : string.Empty;
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            int scriptVar = args.Count > 2 ? args[2].AsInt() : -1;

            if (string.IsNullOrEmpty(scriptResRef) || ctx.World == null)
            {
                return Variable.FromInt(0);
            }

            // Resolve target entity (defaults to caller if invalid)
            Core.Interfaces.IEntity target = ResolveObject(targetId, ctx);
            if (target == null)
            {
                target = ctx.Caller;
            }

            if (target == null || !target.IsValid)
            {
                return Variable.FromInt(0);
            }

            try
            {
                // Get ScriptExecutor from world using reflection (similar to ActionCastSpellAtLocation)
                // This is a workaround until IWorld exposes ScriptExecutor directly
                System.Type worldType = ctx.World.GetType();
                System.Reflection.PropertyInfo scriptExecutorProperty = worldType.GetProperty("ScriptExecutor");
                if (scriptExecutorProperty == null)
                {
                    System.Reflection.FieldInfo scriptExecutorField = worldType.GetField("ScriptExecutor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (scriptExecutorField != null)
                    {
                        object scriptExecutor = scriptExecutorField.GetValue(ctx.World);
                        if (scriptExecutor != null)
                        {
                            // ExecuteScript(IEntity caller, string scriptResRef, IEntity triggerer)
                            System.Reflection.MethodInfo executeMethod = scriptExecutor.GetType().GetMethod("ExecuteScript", new System.Type[] { typeof(Core.Interfaces.IEntity), typeof(string), typeof(Core.Interfaces.IEntity) });
                            if (executeMethod != null)
                            {
                                // Execute script with target as caller, original caller as triggerer
                                object result = executeMethod.Invoke(scriptExecutor, new object[] { target, scriptResRef, ctx.Caller });
                                int returnValue = result != null ? (int)result : 0;
                                
                                // Track instruction count if action queue component exists
                                Core.Interfaces.Components.IActionQueueComponent actionQueue = target.GetComponent<Core.Interfaces.Components.IActionQueueComponent>();
                                if (actionQueue != null)
                                {
                                    // Try to get instruction count from script executor if available
                                    System.Reflection.PropertyInfo instructionsProperty = scriptExecutor.GetType().GetProperty("InstructionsExecuted");
                                    if (instructionsProperty != null)
                                    {
                                        int instructionsExecuted = (int)instructionsProperty.GetValue(scriptExecutor);
                                        if (instructionsExecuted > 0)
                                        {
                                            actionQueue.AddInstructionCount(instructionsExecuted);
                                        }
                                    }
                                }
                                
                                return Variable.FromInt(returnValue);
                            }
                        }
                    }
                }
                else
                {
                    object scriptExecutor = scriptExecutorProperty.GetValue(ctx.World);
                    if (scriptExecutor != null)
                    {
                        // ExecuteScript(IEntity caller, string scriptResRef, IEntity triggerer)
                        System.Reflection.MethodInfo executeMethod = scriptExecutor.GetType().GetMethod("ExecuteScript", new System.Type[] { typeof(Core.Interfaces.IEntity), typeof(string), typeof(Core.Interfaces.IEntity) });
                        if (executeMethod != null)
                        {
                            // Execute script with target as caller, original caller as triggerer
                            object result = executeMethod.Invoke(scriptExecutor, new object[] { target, scriptResRef, ctx.Caller });
                            int returnValue = result != null ? (int)result : 0;
                            
                            // Track instruction count if action queue component exists
                            Core.Interfaces.Components.IActionQueueComponent actionQueue = target.GetComponent<Core.Interfaces.Components.IActionQueueComponent>();
                            if (actionQueue != null)
                            {
                                // Try to get instruction count from script executor if available
                                System.Reflection.PropertyInfo instructionsProperty = scriptExecutor.GetType().GetProperty("InstructionsExecuted");
                                if (instructionsProperty != null)
                                {
                                    int instructionsExecuted = (int)instructionsProperty.GetValue(scriptExecutor);
                                    if (instructionsExecuted > 0)
                                    {
                                        actionQueue.AddInstructionCount(instructionsExecuted);
                                    }
                                }
                            }
                            
                            return Variable.FromInt(returnValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[BaseEngineApi] Error executing script {0}: {1}", scriptResRef, ex.Message);
            }

            return Variable.FromInt(0);
        }

        /// <summary>
        /// ClearAllActions() - Clears all actions from the caller
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: ExecuteCommandClearAllActions @ routine ID 9
        /// Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        /// Original implementation: Clears action queue for caller entity
        /// - Only works on entities with action queue component (creatures, doors, placeables)
        /// - Clears current action and all pending actions
        /// - Common across all engines: Odyssey, Aurora, Eclipse all use same pattern
        /// </remarks>
        protected Variable Func_ClearAllActions(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            Core.Interfaces.IEntity caller = ctx.Caller;
            if (caller != null && caller.IsValid)
            {
                Core.Interfaces.Components.IActionQueueComponent actionQueue = caller.GetComponent<Core.Interfaces.Components.IActionQueueComponent>();
                if (actionQueue != null)
                {
                    actionQueue.Clear();
                }
            }
            return Variable.Void();
        }

        /// <summary>
        /// SetFacing(float fDirection) - Sets the facing direction of the caller
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: ExecuteCommandSetFacing @ routine ID 10
        /// Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        /// Original implementation: Pops facing angle from stack, sets caller entity's facing direction
        /// - fDirection: Facing angle in degrees (0.0-360.0), where 0.0 = East, 90.0 = North, 180.0 = West, 270.0 = South
        /// - Facing is expressed as anticlockwise degrees from Due East
        /// - Converts degrees to radians for internal storage
        /// - Only works on entities with transform component
        /// - Common across all engines: Odyssey, Aurora, Eclipse all use transform component pattern
        /// </remarks>
        protected Variable Func_SetFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float facingDegrees = args.Count > 0 ? args[0].AsFloat() : 0f;
            Core.Interfaces.IEntity caller = ctx.Caller;

            if (caller == null || !caller.IsValid)
            {
                return Variable.Void();
            }

            // Normalize facing to 0-360 range
            while (facingDegrees < 0f)
            {
                facingDegrees += 360f;
            }
            while (facingDegrees >= 360f)
            {
                facingDegrees -= 360f;
            }

            // Convert degrees to radians
            float facingRadians = facingDegrees * (float)Math.PI / 180f;

            // Get transform component and set facing
            Core.Interfaces.Components.ITransformComponent transform = caller.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                transform.Facing = facingRadians;
            }

            return Variable.Void();
        }

        #endregion
    }
}

