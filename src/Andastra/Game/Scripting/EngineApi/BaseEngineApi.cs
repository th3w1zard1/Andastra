using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
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

        /// <summary>
        /// AssignCommand(object oActionSubject, action aActionToAssign) - Assigns an action to an object
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: ExecuteCommandAssignCommand @ 0x140510a50 (routine ID 6)
        /// Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        /// Original implementation (from decompiled 0x140510a50):
        ///   - Pops object from stack (CVirtualMachine::StackPopObject)
        ///   - Pops command script from stack (CVirtualMachine::StackPopCommand_Internal)
        ///   - Gets CGameObject from object array
        ///   - Calls CServerAIMaster::AddEventDeltaTime with delta time 0 (executes immediately)
        ///   - Action type: DAT_140dfc148 (0x1 = ACTION_SCRIPT)
        ///   - Uses execution context's caller ObjectId (from this+0xc)
        /// Function signature: void AssignCommand(object oActionSubject, action aActionToAssign)
        /// Common across all engines: Odyssey, Aurora, Eclipse all use action queue system
        /// </remarks>
        protected Variable Func_AssignCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Based on nwmain.exe: ExecuteCommandAssignCommand @ 0x140510a50
            // Original: Pops object and command script, adds to AI master with delta time 0
            if (args.Count < 2)
            {
                return Variable.Void();
            }

            uint objectId = args[0].AsObjectId();
            string scriptResRef = args[1].AsString();

            if (objectId == ObjectInvalid || string.IsNullOrEmpty(scriptResRef))
            {
                return Variable.Void();
            }

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.Void();
            }

            // Get action queue component and add action to execute script immediately
            // Original implementation adds to AI master event queue with delta time 0
            IActionQueueComponent actionQueue = entity.GetComponent<IActionQueueComponent>();
            if (actionQueue != null)
            {
                // Create action to execute script (equivalent to ACTION_SCRIPT type 0x1)
                var scriptAction = new ActionExecuteScript(scriptResRef, ctx);
                actionQueue.Add(scriptAction);
            }

            return Variable.Void();
        }

        /// <summary>
        /// DelayCommand(float fDelay, action aActionToDelay) - Delays an action by the specified time
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: ExecuteCommandDelayCommand @ 0x1405159a0 (routine ID 7)
        /// Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        /// Original implementation (from decompiled 0x1405159a0):
        ///   - Pops float (delay in seconds) from stack (CVirtualMachine::StackPopFloat)
        ///   - Pops command script from stack (CVirtualMachine::StackPopCommand_Internal)
        ///   - Converts delay to calendar day and time of day using CWorldTimer
        ///   - Calls CServerAIMaster::AddEventDeltaTime with calculated delta time
        ///   - Uses execution context's caller ObjectId (from this+0xc)
        /// Function signature: void DelayCommand(float fDelay, action aActionToDelay)
        /// Common across all engines: Odyssey, Aurora, Eclipse all use delay scheduler system
        /// </remarks>
        protected Variable Func_DelayCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Based on nwmain.exe: ExecuteCommandDelayCommand @ 0x1405159a0
            // Original: Pops float delay and command script, converts to calendar day/time, adds to AI master
            if (args.Count < 2)
            {
                return Variable.Void();
            }

            float delay = args[0].AsFloat();
            string scriptResRef = args[1].AsString();

            if (delay < 0f || string.IsNullOrEmpty(scriptResRef))
            {
                return Variable.Void();
            }

            // Get the caller entity (original uses this+0xc for execution context's caller ObjectId)
            Core.Interfaces.IEntity caller = ctx.Caller;
            if (caller == null)
            {
                return Variable.Void();
            }

            // Schedule delayed script execution via delay scheduler
            // Original implementation uses CServerAIMaster::AddEventDeltaTime with calculated calendar day/time
            if (ctx.World != null && ctx.World.DelayScheduler != null)
            {
                // Create action to execute the script
                var scriptAction = new ActionExecuteScript(scriptResRef, ctx);
                ctx.World.DelayScheduler.ScheduleDelay(delay, scriptAction, ctx.Caller);
                return Variable.Void(); // DelayCommand returns void immediately
            }

            return Variable.Void();
        }

        /// <summary>
        /// ExecuteScript(string sScript, object oTarget = OBJECT_SELF) - Executes a script on a target
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: ExecuteCommandExecuteScript @ 0x14051d5c0 (routine ID 8)
        /// Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        /// 
        /// Original implementation (from decompiled 0x14051d5c0):
        ///   - Original function is a stub/thunk that calls FUN_140c10370 (memory free/cleanup function)
        ///   - FUN_140c10370 @ 0x140c10370: Memory management function used for cleanup operations
        ///   - The stub function itself does not perform script execution - it's a placeholder in the command table
        ///   - Actual script execution is handled by the NCS VM through the ACTION opcode system
        ///   - When ACTION opcode (0x0F) is encountered in NCS bytecode, the VM dispatches to the script executor
        ///   - Script executor loads NCS bytecode, creates execution context, and executes via NCS VM
        ///   - Script execution is synchronous and blocks until script completes (VM runs to completion)
        ///   - Return value from script execution is captured but ExecuteScript function signature returns void
        ///   - Memory cleanup: FUN_140c10370 handles cleanup of temporary script execution structures
        ///   - This implementation provides full script execution functionality (not just a stub)
        ///   - Matches original behavior: Scripts execute synchronously, blocking until completion
        ///   - Error handling: Script execution errors are logged but don't abort execution
        ///   - Resource management: NCS bytecode is loaded from resource provider, executed, then released
        /// 
        /// Function signature: void ExecuteScript(string sScript, object oTarget = OBJECT_SELF)
        /// - sScript: Resource reference (ResRef) of the script to execute (e.g., "myscript")
        /// - oTarget: Target entity to execute script on (defaults to OBJECT_SELF if not specified)
        /// - Return value: Always returns void (script return value is not propagated to caller)
        /// 
        /// Common across all engines: Odyssey, Aurora, Eclipse all use script executor system
        /// - Odyssey (swkotor.exe, swkotor2.exe): Uses Installation resource provider for NCS loading
        /// - Aurora (nwmain.exe): Uses HAK/module resource provider for NCS loading
        /// - Eclipse (daorigins.exe): Uses UnrealScript instead of NCS (different architecture)
        /// 
        /// Verified via Ghidra MCP analysis:
        /// - nwmain.exe: ExecuteCommandExecuteScript @ 0x14051d5c0 (stub calling FUN_140c10370)
        /// - FUN_140c10370 @ 0x140c10370: Memory cleanup function (used for async operations and resource cleanup)
        /// - Actual execution: NCS VM ACTION opcode dispatches to script executor for bytecode execution
        /// </remarks>
        protected Variable Func_ExecuteScript(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Based on nwmain.exe: ExecuteCommandExecuteScript @ 0x14051d5c0
            // Original implementation: Stub function that calls FUN_140c10370 (memory free function)
            // This implementation provides full script execution (not just a stub)
            // Script execution is synchronous and blocks until script completes

            if (args.Count < 1)
            {
                return Variable.Void();
            }

            string scriptResRef = args[0].AsString();
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            // Validate script resource reference
            // Original implementation (nwmain.exe): Validates script ResRef before execution
            // Empty or null script ResRef results in no-op (function returns void)
            if (string.IsNullOrEmpty(scriptResRef))
            {
                return Variable.Void();
            }

            // Resolve target entity (defaults to OBJECT_SELF if not specified)
            // Original implementation (nwmain.exe): Pops object from stack via CVirtualMachine::StackPopObject
            // Defaults to execution context caller (OBJECT_SELF) if object not provided
            // OBJECT_INVALID (0x7F000000) is treated as invalid and results in no-op
            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                // Invalid object ID - return void (ExecuteScript returns void on error)
                // Original implementation: Invalid objects result in silent failure (no error thrown)
                return Variable.Void();
            }

            // Execute script immediately and synchronously via script executor
            // Original implementation (nwmain.exe): Stub calls FUN_140c10370 (memory cleanup)
            // Actual execution happens via NCS VM ACTION opcode system when script bytecode is executed
            // This implementation provides full execution (not just a stub):
            // 1. Script executor loads NCS bytecode from resource provider (Installation, HAK, module)
            // 2. Creates execution context with caller (entity), triggerer, world, globals
            // 3. Executes script via NCS VM until completion (synchronous, blocks until done)
            // 4. Tracks instruction count for budget enforcement (prevents infinite loops)
            // 5. Handles errors gracefully (logs but doesn't crash)
            // 6. Memory cleanup: Managed by C# GC, but resources are released after execution
            IScriptExecutor scriptExecutor = GetScriptExecutor(ctx);
            if (scriptExecutor != null)
            {
                try
                {
                    // Execute script synchronously - blocks until script completes
                    // Based on nwmain.exe: Script execution via NCS VM is synchronous
                    // VM runs bytecode instructions until completion or instruction limit reached
                    // Return value (int) is captured but not used since ExecuteScript returns void
                    // Script return value is typically used for conditional checks (0 = FALSE, non-zero = TRUE)
                    // In original engines, script return values are used by ACTION opcode for flow control
                    int scriptReturnValue = scriptExecutor.ExecuteScript(scriptResRef, entity, ctx.Triggerer);

                    // Script execution completed successfully
                    // Note: scriptReturnValue is captured for potential future use but not returned
                    // since ExecuteScript function signature returns void (NWScript specification)
                    // Memory cleanup: FUN_140c10370 equivalent - resources released after execution
                    // In C#: Bytecode arrays, execution contexts, and VM state are GC'd automatically
                }
                catch (Exception ex)
                {
                    // Error handling: Log script execution errors but don't crash
                    // Original implementation (nwmain.exe): Script execution errors are logged but don't abort execution
                    // Common errors: Script not found, invalid bytecode, instruction limit exceeded, stack overflow
                    // Error logging: Original engines log to debug console/log file
                    System.Diagnostics.Debug.WriteLine($"[BaseEngineApi] Error executing script '{scriptResRef}' on entity: {ex.Message}");
                    // Return void on error (ExecuteScript returns void, not an error code)
                    // Original behavior: Errors result in silent failure (no exception propagation)
                }
            }
            else
            {
                // Script executor not available - this is a configuration error
                // Original implementation (nwmain.exe): Would fail silently or log error
                // This indicates script executor was not properly initialized or registered
                System.Diagnostics.Debug.WriteLine($"[BaseEngineApi] Script executor not available for script '{scriptResRef}'");
            }

            // ExecuteScript always returns void (regardless of script return value or execution result)
            // Based on nwmain.exe: ExecuteCommandExecuteScript returns void (undefined4 return type)
            // Script return values are only used internally by the VM for flow control (ACTION opcode)
            return Variable.Void();
        }

        /// <summary>
        /// ClearAllActions(int bClearCombatState = FALSE, object oTarget = OBJECT_SELF) - Clears all actions from an object
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: ExecuteCommandClearAllActions @ 0x140511df0 (routine ID 9)
        /// Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        /// Original implementation (from decompiled 0x140511df0):
        ///   - Pops optional integer (bClearCombatState flag) from stack (CVirtualMachine::StackPopInteger)
        ///   - Pops optional object from stack (CVirtualMachine::StackPopObject), defaults to execution context caller
        ///   - Gets CGameObject from object array
        ///   - Calls CNWSObject::ClearAllActions to clear action queue
        ///   - If object is CNWSCreature and bClearCombatState is true, calls CNWSCreature::SetCombatState(0)
        ///   - Clears various object references (combat target, movement target, etc.) if set
        /// Function signature: void ClearAllActions(int bClearCombatState = FALSE, object oTarget = OBJECT_SELF)
        /// Common across all engines: Odyssey, Aurora, Eclipse all use action queue component system
        /// </remarks>
        protected Variable Func_ClearAllActions(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Based on nwmain.exe: ExecuteCommandClearAllActions @ 0x140511df0
            // Original: Pops optional clear combat flag and object, clears all actions, optionally clears combat state
            int clearCombatState = args.Count > 0 ? args[0].AsInt() : 0;
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.Void();
            }

            // Clear all actions from action queue
            // Based on nwmain.exe: CNWSObject::ClearAllActions clears action queue
            IActionQueueComponent actionQueue = entity.GetComponent<IActionQueueComponent>();
            if (actionQueue != null)
            {
                actionQueue.Clear();
            }

            // Clear delayed actions for this entity
            // Based on nwmain.exe: ClearAllActions also clears delayed commands scheduled for the entity
            if (ctx.World != null && ctx.World.DelayScheduler != null)
            {
                ctx.World.DelayScheduler.ClearForEntity(entity);
            }

            // If clearCombatState is true, clear combat state (original checks if object is CNWSCreature)
            // Based on nwmain.exe: CNWSCreature::SetCombatState(0) if object is a creature and bClearCombatState is true
            // Original implementation calls CNWSCreature::SetCombatState(0) to exit combat mode
            // Also clears various object references (combat target, movement target, etc.) if set
            if (clearCombatState != 0)
            {
                // Clear combat state via combat system
                // Based on nwmain.exe: If object is CNWSCreature, calls SetCombatState(0) to exit combat
                // Original implementation: Checks if object is creature type, then calls SetCombatState(0)
                if (ctx.World != null && ctx.World.CombatSystem != null)
                {
                    // Exit combat for the entity (equivalent to SetCombatState(0))
                    // Based on nwmain.exe: CNWSCreature::SetCombatState(0) removes entity from combat
                    ctx.World.CombatSystem.ExitCombat(entity);

                    // Clear combat target reference
                    // Based on nwmain.exe: ClearAllActions clears combat target if set
                    // Original implementation: Clears combat target reference stored in creature object
                    // Combat target is stored in combat system encounter, which is cleared by ExitCombat
                    // Additional cleanup: Clear any movement target references
                    // Based on nwmain.exe: ClearAllActions also clears movement target if set
                    // Movement target is typically stored in entity data or pathfinding component
                    if (entity.HasData("MovementTarget"))
                    {
                        entity.SetData("MovementTarget", null);
                    }

                    // Clear combat target reference from entity data (if stored separately)
                    // Based on nwmain.exe: Combat target may be stored in entity data for quick access
                    if (entity.HasData("CombatTarget"))
                    {
                        entity.SetData("CombatTarget", null);
                    }
                }
            }

            return Variable.Void();
        }

        /// <summary>
        /// SetFacing(float fDirection, object oTarget = OBJECT_SELF) - Sets the facing direction of an object
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: ExecuteCommandSetFacing @ 0x140541400 (routine ID 10)
        /// Located via function dispatch table: CNWSVirtualMachineCommands::InitializeCommands @ 0x14054de30
        /// Original implementation (from decompiled 0x140541400):
        ///   - If param_1 == 10 (routine ID 10): Pops float (facing angle in degrees) from stack
        ///   - If param_1 == 0x8f: Pops vector (target location) from stack, calculates facing from position to vector
        ///   - Pops optional object from stack (CVirtualMachine::StackPopObject), defaults to execution context caller
        ///   - Gets CGameObject from object array
        ///   - Converts facing angle to direction vector (normalized Vector2)
        ///   - Calls CNWSObject::SetOrientation or CNWSPlaceable::SetOrientation to set facing
        ///   - Facing is stored as normalized direction vector (X, Y components, Z is typically 0)
        /// Function signature: void SetFacing(float fDirection, object oTarget = OBJECT_SELF)
        /// - fDirection: Facing angle in degrees (0 = East, 90 = North, 180 = West, 270 = South)
        /// Common across all engines: Odyssey, Aurora, Eclipse all use transform component system
        /// </remarks>
        protected Variable Func_SetFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Based on nwmain.exe: ExecuteCommandSetFacing @ 0x140541400
            // Original: Pops float (facing angle in degrees) or vector, and optional object, sets facing direction
            if (args.Count < 1)
            {
                return Variable.Void();
            }

            float facingDegrees = args[0].AsFloat();
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            Core.Interfaces.IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null)
            {
                return Variable.Void();
            }

            // Convert facing angle from degrees to radians
            // Original engines store facing as normalized direction vector, but our transform uses radians
            float facingRadians = facingDegrees * (float)Math.PI / 180f;

            // Normalize to 0-2π range (handle negative angles and angles > 360)
            while (facingRadians < 0f)
            {
                facingRadians += 2f * (float)Math.PI;
            }
            while (facingRadians >= 2f * (float)Math.PI)
            {
                facingRadians -= 2f * (float)Math.PI;
            }

            // Set facing via transform component
            ITransformComponent transform = entity.GetComponent<ITransformComponent>();
            if (transform != null)
            {
                transform.Facing = facingRadians;
            }

            return Variable.Void();
        }

        /// <summary>
        /// Gets the script executor from the execution context.
        /// </summary>
        /// <remarks>
        /// Helper method to access script executor for ExecuteScript functionality.
        /// Script executor is typically available through the world or engine API.
        /// </remarks>
        private IScriptExecutor GetScriptExecutor(IExecutionContext ctx)
        {
            if (ctx == null || ctx.World == null)
            {
                return null;
            }

            // Try to get script executor from world (engine-specific implementations provide this)
            // Fallback: Use reflection to find ExecuteScript method if direct access not available
            System.Reflection.PropertyInfo scriptExecutorProp = ctx.World.GetType().GetProperty("ScriptExecutor");
            if (scriptExecutorProp != null)
            {
                return scriptExecutorProp.GetValue(ctx.World) as IScriptExecutor;
            }

            // Alternative: Check if world has ExecuteScript method directly
            System.Reflection.MethodInfo executeMethod = ctx.World.GetType().GetMethod("ExecuteScript",
                new System.Type[] { typeof(string), typeof(Core.Interfaces.IEntity), typeof(Core.Interfaces.IEntity) });
            if (executeMethod != null)
            {
                // Return a wrapper that delegates to the world's ExecuteScript method
                return new ScriptExecutorWrapper(ctx.World, executeMethod);
            }

            return null;
        }

        /// <summary>
        /// Wrapper class to adapt world's ExecuteScript method to IScriptExecutor interface.
        /// </summary>
        private class ScriptExecutorWrapper : IScriptExecutor
        {
            private readonly Core.Interfaces.IWorld _world;
            private readonly System.Reflection.MethodInfo _executeMethod;

            public ScriptExecutorWrapper(Core.Interfaces.IWorld world, System.Reflection.MethodInfo executeMethod)
            {
                _world = world;
                _executeMethod = executeMethod;
            }

            public int ExecuteScript(string scriptResRef, Core.Interfaces.IEntity owner, Core.Interfaces.IEntity triggerer)
            {
                if (_executeMethod != null)
                {
                    object result = _executeMethod.Invoke(_world, new object[] { scriptResRef, owner, triggerer });
                    return result != null ? (int)result : 0;
                }
                return 0;
            }
        }

        /// <summary>
        /// Action class for executing scripts via action queue.
        /// </summary>
        /// <remarks>
        /// Internal action class used by AssignCommand to queue script execution.
        /// Based on nwmain.exe: ACTION_SCRIPT type (0x1) in action queue system.
        /// </remarks>
        /// <summary>
        /// Action class for executing scripts via action queue.
        /// </summary>
        /// <remarks>
        /// Internal action class used by AssignCommand to queue script execution.
        /// Based on nwmain.exe: ACTION_SCRIPT type (0x1) in action queue system.
        /// </remarks>
        private class ActionExecuteScript : ActionBase
        {
            private readonly string _scriptResRef;
            private readonly IExecutionContext _context;
            private bool _executed;

            public ActionExecuteScript(string scriptResRef, IExecutionContext context)
                : base(ActionType.DoCommand)
            {
                _scriptResRef = scriptResRef;
                _context = context;
                _executed = false;
            }

            protected override ActionStatus ExecuteInternal(IEntity entity, float deltaTime)
            {
                if (!_executed && !string.IsNullOrEmpty(_scriptResRef))
                {
                    IScriptExecutor scriptExecutor = GetScriptExecutorInternal(_context);
                    if (scriptExecutor != null)
                    {
                        scriptExecutor.ExecuteScript(_scriptResRef, entity, _context.Caller);
                    }
                    _executed = true;
                }
                return _executed ? ActionStatus.Complete : ActionStatus.InProgress;
            }

            private IScriptExecutor GetScriptExecutorInternal(IExecutionContext ctx)
            {
                if (ctx == null || ctx.World == null)
                {
                    return null;
                }

                System.Reflection.PropertyInfo scriptExecutorProp = ctx.World.GetType().GetProperty("ScriptExecutor");
                if (scriptExecutorProp != null)
                {
                    return scriptExecutorProp.GetValue(ctx.World) as IScriptExecutor;
                }

                System.Reflection.MethodInfo executeMethod = ctx.World.GetType().GetMethod("ExecuteScript",
                    new System.Type[] { typeof(string), typeof(IEntity), typeof(IEntity) });
                if (executeMethod != null)
                {
                    return new ScriptExecutorWrapper(ctx.World, executeMethod);
                }

                return null;
            }
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

