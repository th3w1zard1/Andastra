using System;
using System.Collections.Generic;
using System.Numerics;
using CSharpKOTOR.Common.Script;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Scripting.EngineApi;
using Odyssey.Scripting.Interfaces;
using Odyssey.Scripting.VM;

namespace Odyssey.Kotor.EngineApi
{
    /// <summary>
    /// KOTOR 2 (TSL) engine API implementation.
    /// Extends K1 API with TSL-specific functions.
    /// </summary>
    /// <remarks>
    /// KOTOR 2 Engine API (TSL NWScript Functions):
    /// - Based on swkotor2.exe NWScript engine API implementation
    /// - Located via string references: ACTION opcode handler dispatches to engine function implementations
    /// - Original implementation: TSL adds ~100 additional engine functions beyond K1's ~850 functions
    /// - Function IDs: K1 functions 0-799 are shared, TSL adds functions 800+ (total ~950 functions)
    /// - Influence system: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774, "BaseInfluence" @ 0x007bf6fc
    /// - "Influence" @ 0x007c4f78, "LBL_INFLUENCE_RECV" @ 0x007c8b38, "LBL_INFLUENCE_LOST" @ 0x007c8b0c
    /// - TSL-specific additions include:
    ///   - Influence system functions (GetInfluence, SetInfluence, ModifyInfluence)
    ///   - Party puppet functions (GetPartyMemberByIndex, IsAvailableCreature, AddAvailableNPCByTemplate)
    ///   - Workbench/lab functions (ShowUpgradeScreen, GetBaseItemType)
    ///   - Combat form functions (GetIsFormActive)
    ///   - Enhanced visual effect functions
    ///   - Stealth system functions (IsStealthed, GetStealthXPEnabled, SetStealthXPEnabled)
    ///   - Swoop minigame functions (SWMG_GetPlayerOffset, SWMG_GetPlayerInvincibility)
    /// - Original engine uses function dispatch table indexed by routine ID (matches nwscript.nss definitions)
    /// - Function implementations must match NWScript semantics for TSL script compatibility
    /// </remarks>
    public class K2EngineApi : BaseEngineApi
    {
        private readonly NcsVm _vm;

        public K2EngineApi()
        {
            _vm = new NcsVm();
        }

        protected override void RegisterFunctions()
        {
            // Register function names from ScriptDefs for TSL
            int idx = 0;
            foreach (ScriptFunction func in ScriptDefs.TSL_FUNCTIONS)
            {
                _functionNames[idx] = func.Name;
                idx++;
            }
        }

        public override Variable CallEngineFunction(int routineId, IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // TSL has the same base functions as K1 with additional functions
            // Most functions 0-799 are shared, TSL adds functions 800+

            switch (routineId)
            {
                // ===== Base Functions (shared with K1) =====
                case 0: return Func_Random(args, ctx);
                case 1: return Func_PrintString(args, ctx);
                case 2: return Func_PrintFloat(args, ctx);
                case 3: return Func_FloatToString(args, ctx);
                case 4: return Func_PrintInteger(args, ctx);
                case 5: return Func_PrintObject(args, ctx);
                case 6: return Func_AssignCommand(args, ctx);
                case 7: return Func_DelayCommand(args, ctx);
                case 8: return Func_ExecuteScript(args, ctx);
                case 9: return Func_ClearAllActions(args, ctx);
                case 10: return Func_SetFacing(args, ctx);

                // Object functions
                case 27: return Func_GetPosition(args, ctx);
                case 28: return Func_GetFacing(args, ctx);
                case 41: return Func_GetDistanceToObject(args, ctx);
                case 42: return Func_GetIsObjectValid(args, ctx);

                // Math functions (same as K1)
                case 67: return Func_fabs(args, ctx);
                case 68: return Func_cos(args, ctx);
                case 69: return Func_sin(args, ctx);
                case 70: return Func_tan(args, ctx);
                case 71: return Func_acos(args, ctx);
                case 72: return Func_asin(args, ctx);
                case 73: return Func_atan(args, ctx);
                case 74: return Func_log(args, ctx);
                case 75: return Func_pow(args, ctx);
                case 76: return Func_sqrt(args, ctx);
                case 77: return Func_abs(args, ctx);
                case 92: return Func_IntToString(args, ctx);

                // Dice functions (same as K1)
                case 95: return Func_d2(args, ctx);
                case 96: return Func_d3(args, ctx);
                case 97: return Func_d4(args, ctx);
                case 98: return Func_d6(args, ctx);
                case 99: return Func_d8(args, ctx);
                case 100: return Func_d10(args, ctx);
                case 101: return Func_d12(args, ctx);
                case 102: return Func_d20(args, ctx);
                case 103: return Func_d100(args, ctx);
                case 104: return Func_VectorMagnitude(args, ctx);
                case 106: return Func_GetObjectType(args, ctx);

                // String functions (same as K1)
                case 59: return Func_GetStringLength(args, ctx);
                case 60: return Func_GetStringUpperCase(args, ctx);
                case 61: return Func_GetStringLowerCase(args, ctx);
                case 62: return Func_GetStringRight(args, ctx);
                case 63: return Func_GetStringLeft(args, ctx);
                case 64: return Func_InsertString(args, ctx);
                case 65: return Func_GetSubString(args, ctx);
                case 66: return Func_FindSubString(args, ctx);

                // Core object lookup
                case 168: return Func_GetTag(args, ctx);
                case 200: return Func_GetObjectByTag(args, ctx);
                case 242: return Func_GetModule(args, ctx);

                // Global variables (TSL uses same IDs as K1)
                case 578: return Func_GetGlobalBoolean(args, ctx);
                case 579: return Func_SetGlobalBoolean(args, ctx);
                case 580: return Func_GetGlobalNumber(args, ctx);
                case 581: return Func_SetGlobalNumber(args, ctx);

                // Local variables
                case 679: return Func_GetLocalBoolean(args, ctx);
                case 680: return Func_SetLocalBoolean(args, ctx);
                case 681: return Func_GetLocalNumber(args, ctx);
                case 682: return Func_SetLocalNumber(args, ctx);

                // ===== TSL-Specific Functions =====

                // Influence system (TSL only)
                case 800: return Func_GetInfluence(args, ctx);
                case 801: return Func_SetInfluence(args, ctx);
                case 802: return Func_ModifyInfluence(args, ctx);

                // Party puppet functions (TSL only)
                case 814: return Func_GetPartyMemberByIndex(args, ctx);
                case 817: return Func_IsAvailableCreature(args, ctx);
                case 822: return Func_AddAvailableNPCByTemplate(args, ctx);
                case 827: return Func_GetNPCSelectability(args, ctx);
                case 828: return Func_SetNPCSelectability(args, ctx);

                // Remote/stealth functions (TSL only)
                case 834: return Func_IsStealthed(args, ctx);
                case 836: return Func_GetStealthXPEnabled(args, ctx);
                case 837: return Func_SetStealthXPEnabled(args, ctx);

                // Workbench/lab functions (TSL only)
                case 850: return Func_ShowUpgradeScreen(args, ctx);
                case 856: return Func_GetBaseItemType(args, ctx);

                // Combat form functions (TSL only)
                case 862: return Func_GetIsFormActive(args, ctx);

                // Visual effect functions (TSL extensions)
                case 890: return Func_SWMG_GetPlayerOffset(args, ctx);
                case 891: return Func_SWMG_GetPlayerInvincibility(args, ctx);
                case 892: return Func_SWMG_SetPlayerInvincibility(args, ctx);

                default:
                    // Fall back to unimplemented function logging
                    string funcName = GetFunctionName(routineId);
                    Console.WriteLine("[NCS-TSL] Unimplemented function: " + routineId + " (" + funcName + ")");
                    return Variable.Void();
            }
        }

        #region Shared Functions (copied from K1 for efficiency)

        private Variable Func_AssignCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IAction action = args.Count > 1 ? args[1].ComplexValue as IAction : null;

            IEntity target = ResolveObject(targetId, ctx);
            if (target != null && action != null)
            {
                IActionQueue queue = target.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }

            return Variable.Void();
        }

        private Variable Func_DelayCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // DelayCommand(float fSeconds, action aActionToDelay)
            float delay = args.Count > 0 ? args[0].AsFloat() : 0f;
            IAction action = args.Count > 1 ? args[1].ComplexValue as IAction : null;

            if (action != null && ctx.Caller != null && ctx.World != null)
            {
                // Schedule the action in the world's delay scheduler
                ctx.World.DelayScheduler.ScheduleDelay(delay, action, ctx.Caller);
            }

            return Variable.Void();
        }

        private Variable Func_ExecuteScript(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ExecuteScript(string sScript, object oTarget = OBJECT_SELF)
            string scriptName = args.Count > 0 ? args[0].AsString() : string.Empty;
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            if (string.IsNullOrEmpty(scriptName))
            {
                return Variable.Void();
            }

            IEntity target = ResolveObject(targetId, ctx);
            if (target == null)
            {
                target = ctx.Caller;
            }

            if (target == null || ctx.World == null)
            {
                return Variable.Void();
            }

            // Execute script on target entity
            try
            {
                // Use VM to execute script with new context
                if (ctx.ResourceProvider != null)
                {
                    IExecutionContext scriptCtx = ctx.WithCaller(target);
                    int result = _vm.ExecuteScript(scriptName, scriptCtx);
                    return Variable.FromInt(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[K2EngineApi] ExecuteScript error: " + ex.Message);
            }

            return Variable.Void();
        }

        private Variable Func_ClearAllActions(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            IEntity caller = ctx.Caller;
            if (caller != null)
            {
                IActionQueue queue = caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Clear();
                }
            }
            return Variable.Void();
        }

        private Variable Func_SetFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float facing = args.Count > 0 ? args[0].AsFloat() : 0f;
            if (ctx.Caller != null)
            {
                Core.Interfaces.Components.ITransformComponent transform = ctx.Caller.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    transform.Facing = facing * (float)Math.PI / 180f;
                }
            }
            return Variable.Void();
        }

        private Variable Func_GetFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.ITransformComponent transform = entity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    return Variable.FromFloat(transform.Facing * 180f / (float)Math.PI);
                }
            }
            return Variable.FromFloat(0f);
        }

        private Variable Func_GetPosition(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
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

        private Variable Func_GetDistanceToObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            IEntity target = ResolveObject(targetId, ctx);

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

        private Variable Func_GetObjectType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                return Variable.FromInt((int)entity.ObjectType);
            }
            return Variable.FromInt(0);
        }

        private Variable Func_PrintObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            Console.WriteLine("[Script] Object: 0x" + objectId.ToString("X8"));
            return Variable.Void();
        }

        #endregion

        #region Math Functions

        private Variable Func_fabs(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat(Math.Abs(value));
        }

        private Variable Func_cos(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Cos(value));
        }

        private Variable Func_sin(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Sin(value));
        }

        private Variable Func_tan(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Tan(value));
        }

        private Variable Func_acos(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Acos(value));
        }

        private Variable Func_asin(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Asin(value));
        }

        private Variable Func_atan(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Atan(value));
        }

        private Variable Func_log(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Log(value));
        }

        private Variable Func_pow(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float x = args.Count > 0 ? args[0].AsFloat() : 0f;
            float y = args.Count > 1 ? args[1].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Pow(x, y));
        }

        private Variable Func_sqrt(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float value = args.Count > 0 ? args[0].AsFloat() : 0f;
            return Variable.FromFloat((float)Math.Sqrt(value));
        }

        private Variable Func_abs(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int value = args.Count > 0 ? args[0].AsInt() : 0;
            return Variable.FromInt(Math.Abs(value));
        }

        private Variable Func_VectorMagnitude(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            Vector3 v = args.Count > 0 ? args[0].AsVector() : Vector3.Zero;
            return Variable.FromFloat(v.Length());
        }

        #endregion

        #region Dice Functions

        private Variable Func_d2(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 3);
            return Variable.FromInt(total);
        }

        private Variable Func_d3(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 4);
            return Variable.FromInt(total);
        }

        private Variable Func_d4(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 5);
            return Variable.FromInt(total);
        }

        private Variable Func_d6(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 7);
            return Variable.FromInt(total);
        }

        private Variable Func_d8(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 9);
            return Variable.FromInt(total);
        }

        private Variable Func_d10(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 11);
            return Variable.FromInt(total);
        }

        private Variable Func_d12(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 13);
            return Variable.FromInt(total);
        }

        private Variable Func_d20(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 21);
            return Variable.FromInt(total);
        }

        private Variable Func_d100(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int count = args.Count > 0 ? args[0].AsInt() : 1;
            int total = 0;
            for (int i = 0; i < count; i++) total += _random.Next(1, 101);
            return Variable.FromInt(total);
        }

        #endregion

        #region String Functions

        private Variable Func_GetStringLength(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromInt(s.Length);
        }

        private Variable Func_GetStringUpperCase(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromString(s.ToUpperInvariant());
        }

        private Variable Func_GetStringLowerCase(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromString(s.ToLowerInvariant());
        }

        private Variable Func_GetStringRight(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            int count = args.Count > 1 ? args[1].AsInt() : 0;
            if (count <= 0 || s.Length == 0) return Variable.FromString(string.Empty);
            if (count >= s.Length) return Variable.FromString(s);
            return Variable.FromString(s.Substring(s.Length - count));
        }

        private Variable Func_GetStringLeft(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            int count = args.Count > 1 ? args[1].AsInt() : 0;
            if (count <= 0 || s.Length == 0) return Variable.FromString(string.Empty);
            if (count >= s.Length) return Variable.FromString(s);
            return Variable.FromString(s.Substring(0, count));
        }

        private Variable Func_InsertString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string dest = args.Count > 0 ? args[0].AsString() : string.Empty;
            string src = args.Count > 1 ? args[1].AsString() : string.Empty;
            int pos = args.Count > 2 ? args[2].AsInt() : 0;
            if (pos < 0) pos = 0;
            if (pos > dest.Length) pos = dest.Length;
            return Variable.FromString(dest.Insert(pos, src));
        }

        private Variable Func_GetSubString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            int start = args.Count > 1 ? args[1].AsInt() : 0;
            int count = args.Count > 2 ? args[2].AsInt() : 0;
            if (start < 0) start = 0;
            if (start >= s.Length || count <= 0) return Variable.FromString(string.Empty);
            if (start + count > s.Length) count = s.Length - start;
            return Variable.FromString(s.Substring(start, count));
        }

        private Variable Func_FindSubString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string s = args.Count > 0 ? args[0].AsString() : string.Empty;
            string sub = args.Count > 1 ? args[1].AsString() : string.Empty;
            int start = args.Count > 2 ? args[2].AsInt() : 0;
            if (start < 0) start = 0;
            return Variable.FromInt(s.IndexOf(sub, start, StringComparison.Ordinal));
        }

        #endregion

        #region Global Variable Functions

        private Variable Func_GetGlobalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromInt(ctx.Globals.GetGlobalInt(name));
        }

        private Variable Func_SetGlobalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            int value = args.Count > 1 ? args[1].AsInt() : 0;
            ctx.Globals.SetGlobalInt(name, value);
            return Variable.Void();
        }

        private Variable Func_GetGlobalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromInt(ctx.Globals.GetGlobalBool(name) ? 1 : 0);
        }

        private Variable Func_SetGlobalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            bool value = args.Count > 1 && args[1].AsInt() != 0;
            ctx.Globals.SetGlobalBool(name, value);
            return Variable.Void();
        }

        private Variable Func_GetLocalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int index = args.Count > 1 ? args[1].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && index >= 0 && index < 64)
            {
                return Variable.FromInt(ctx.Globals.GetLocalInt(entity, "_LB_" + index));
            }
            return Variable.FromInt(0);
        }

        private Variable Func_SetLocalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int index = args.Count > 1 ? args[1].AsInt() : 0;
            int value = args.Count > 2 ? args[2].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && index >= 0 && index < 64)
            {
                ctx.Globals.SetLocalInt(entity, "_LB_" + index, value != 0 ? 1 : 0);
            }
            return Variable.Void();
        }

        private Variable Func_GetLocalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int index = args.Count > 1 ? args[1].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && index == 0)
            {
                return Variable.FromInt(ctx.Globals.GetLocalInt(entity, "_LN_" + index));
            }
            return Variable.FromInt(0);
        }

        private Variable Func_SetLocalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int index = args.Count > 1 ? args[1].AsInt() : 0;
            int value = args.Count > 2 ? args[2].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && index == 0)
            {
                if (value > 127) value = 127;
                if (value < -128) value = -128;
                ctx.Globals.SetLocalInt(entity, "_LN_" + index, value);
            }
            return Variable.Void();
        }

        #endregion

        #region TSL-Specific Functions

        // Influence system (TSL only, IDs 800-810)
        /// <summary>
        /// GetInfluence(int nNPC) - Returns influence value for NPC (0-100)
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Influence system (TSL only)
        /// Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
        /// "BaseInfluence" @ 0x007bf6fc, "Influence" @ 0x007c4f78
        /// GUI: "LBL_INFLUENCE_RECV" @ 0x007c8b38, "LBL_INFLUENCE_LOST" @ 0x007c8b0c
        /// Original implementation: Influence values stored in PARTYTABLE.res GFF (PT_INFLUENCE list)
        /// Each NPC has influence value 0-100 stored in PT_NPC_INFLUENCE field
        /// Influence affects dialogue options, companion reactions, and story outcomes
        /// </remarks>
        private Variable Func_GetInfluence(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetInfluence(int nNPC) - returns influence value for NPC (0-100)
            // Based on swkotor2.exe: Influence system (TSL only)
            // Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
            // Original implementation: Reads influence from PARTYTABLE.res GFF structure
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            
            // Get NPC entity from PartyManager
            if (ctx.AdditionalContext is GameSession.GameServicesContext services && services.PartyManager != null)
            {
                IEntity member = services.PartyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    // Get influence from entity data (stored as "Influence")
                    if (member.HasData("Influence"))
                    {
                        int influence = member.GetData<int>("Influence", 50);
                        return Variable.FromInt(influence);
                    }
                    // Default to neutral if not set
                    return Variable.FromInt(50);
                }
            }
            return Variable.FromInt(50); // Default neutral influence
        }

        /// <summary>
        /// SetInfluence(int nNPC, int nInfluence) - Sets influence value for NPC (0-100)
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Influence system (TSL only)
        /// Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
        /// Original implementation: Writes influence to PARTYTABLE.res GFF structure (PT_NPC_INFLUENCE field)
        /// Influence value clamped to 0-100 range
        /// </remarks>
        private Variable Func_SetInfluence(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // SetInfluence(int nNPC, int nInfluence) - sets influence value for NPC (0-100)
            // Based on swkotor2.exe: Influence system (TSL only)
            // Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
            // Original implementation: Writes influence to PARTYTABLE.res GFF structure
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            int influence = args.Count > 1 ? args[1].AsInt() : 50;
            
            // Clamp influence to valid range (0-100)
            influence = Math.Max(0, Math.Min(100, influence));
            
            // Get NPC entity from PartyManager and set influence
            if (ctx.AdditionalContext is GameSession.GameServicesContext services && services.PartyManager != null)
            {
                IEntity member = services.PartyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    // Store influence in entity data
                    member.SetData("Influence", influence);
                }
            }
            return Variable.Void();
        }

        /// <summary>
        /// ModifyInfluence(int nNPC, int nModifier) - Modifies influence value for NPC by modifier amount
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Influence system (TSL only)
        /// Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
        /// Original implementation: Reads current influence, adds modifier, writes back to PARTYTABLE.res GFF
        /// Influence value clamped to 0-100 range after modification
        /// </remarks>
        private Variable Func_ModifyInfluence(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ModifyInfluence(int nNPC, int nModifier) - modifies influence value for NPC by modifier amount
            // Based on swkotor2.exe: Influence system (TSL only)
            // Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
            // Original implementation: Reads current influence, adds modifier, writes back to GFF
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            int modifier = args.Count > 1 ? args[1].AsInt() : 0;
            
            // Get NPC entity from PartyManager and modify influence
            if (ctx.AdditionalContext is GameSession.GameServicesContext services && services.PartyManager != null)
            {
                IEntity member = services.PartyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    // Get current influence (default to 50 if not set)
                    int currentInfluence = member.GetData<int>("Influence", 50);
                    int newInfluence = Math.Max(0, Math.Min(100, currentInfluence + modifier));
                    member.SetData("Influence", newInfluence);
                }
            }
            return Variable.Void();
        }

        // Party puppet functions (TSL only)
        private Variable Func_GetPartyMemberByIndex(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int index = args.Count > 0 ? args[0].AsInt() : 0;
            
            // Get party member at index (0 = leader, 1-2 = members)
            if (ctx.AdditionalContext is GameSession.GameServicesContext services && services.PartyManager != null)
            {
                IEntity member = services.PartyManager.GetMemberAtSlot(index);
                if (member != null)
                {
                    return Variable.FromObject(member.ObjectId);
                }
            }
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_IsAvailableCreature(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            
            // Check if NPC is available for party selection
            if (ctx.AdditionalContext is GameSession.GameServicesContext services && services.PartyManager != null)
            {
                IEntity member = services.PartyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    return Variable.FromInt(1); // Available
                }
            }
            return Variable.FromInt(0); // Not available
        }

        private Variable Func_AddAvailableNPCByTemplate(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            string template = args.Count > 1 ? args[1].AsString() : string.Empty;
            
            if (string.IsNullOrEmpty(template) || ctx.World == null)
            {
                return Variable.FromInt(0); // Failed
            }
            
            // Add NPC to available party members
            if (ctx.AdditionalContext is GameSession.GameServicesContext services && services.PartyManager != null && services.ModuleLoader != null)
            {
                // Try to find existing entity by template tag first
                IEntity existingEntity = ctx.World.GetEntityByTag(template, 0);
                if (existingEntity != null)
                {
                    try
                    {
                        services.PartyManager.AddAvailableMember(npcIndex, existingEntity);
                        return Variable.FromInt(1); // Success
                    }
                    catch
                    {
                        return Variable.FromInt(0); // Failed
                    }
                }
                
                // Create entity from template using EntityFactory
                // Get current module from ModuleLoader
                CSharpKOTOR.Common.Module module = services.ModuleLoader.CurrentModule;
                if (module != null)
                {
                    // Get spawn position (use player position or default)
                    System.Numerics.Vector3 spawnPosition = System.Numerics.Vector3.Zero;
                    float spawnFacing = 0.0f;
                    
                    if (services.PlayerEntity != null)
                    {
                        Core.Interfaces.Components.ITransformComponent playerTransform = services.PlayerEntity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                        if (playerTransform != null)
                        {
                            spawnPosition = playerTransform.Position;
                            spawnFacing = playerTransform.Facing;
                        }
                    }
                    
                    // Create entity from template
                    IEntity newEntity = services.ModuleLoader.EntityFactory.CreateCreatureFromTemplate(module, template, spawnPosition, spawnFacing);
                    if (newEntity != null)
                    {
                        // Register entity with world if not already registered
                        if (ctx.World.GetEntity(newEntity.ObjectId) == null)
                        {
                            ctx.World.RegisterEntity(newEntity);
                        }
                        
                        // Add to PartyManager
                        try
                        {
                            services.PartyManager.AddAvailableMember(npcIndex, newEntity);
                            return Variable.FromInt(1); // Success
                        }
                        catch
                        {
                            return Variable.FromInt(0); // Failed
                        }
                    }
                }
            }
            
            return Variable.FromInt(0); // Failed
        }

        private Variable Func_GetNPCSelectability(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            
            // Get NPC entity from PartyManager
            if (ctx.AdditionalContext is GameSession.GameServicesContext services && services.PartyManager != null)
            {
                IEntity member = services.PartyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    // Get selectability from entity data (stored as "IsSelectable")
                    // Default to true if available but not explicitly set
                    if (member.HasData("IsSelectable"))
                    {
                        bool isSelectable = member.GetData<bool>("IsSelectable", true);
                        return Variable.FromInt(isSelectable ? 1 : 0);
                    }
                    // If available but selectability not set, default to selectable
                    return Variable.FromInt(1);
                }
            }
            return Variable.FromInt(0); // Not available/selectable
        }

        private Variable Func_SetNPCSelectability(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            int selectable = args.Count > 1 ? args[1].AsInt() : 1;
            
            // Get NPC entity from PartyManager and set selectability
            if (ctx.AdditionalContext is GameSession.GameServicesContext services && services.PartyManager != null)
            {
                IEntity member = services.PartyManager.GetAvailableMember(npcIndex);
                if (member != null)
                {
                    // Store selectability in entity data
                    member.SetData("IsSelectable", selectable != 0);
                }
            }
            return Variable.Void();
        }

        // Remote/stealth functions (TSL only)
        /// <summary>
        /// IsStealthed(object oTarget) - Returns TRUE if target is stealthed (has invisibility effect)
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Stealth system (TSL only)
        /// Located via string references: "StealthMode" @ 0x007bf690, "StealthXPEnabled" @ 0x007bd1b4
        /// "StealthXPCurrent" @ 0x007bd1d8, "StealthXPMax" @ 0x007bd1ec, "StealthXPLoss" @ 0x007bd1c8
        /// "STEALTHXP" @ 0x007bdf08, "setstealth" @ 0x007c79fc
        /// GUI: "LBL_STEALTH" @ 0x007c8c0c, "LBL_STEALTHXP" @ 0x007ccd7c, "TB_STEALTH" @ 0x007cd1dc
        /// Original implementation: Checks if entity has Invisibility effect active
        /// Stealth system: Entities with stealth can avoid detection, gain stealth XP, lose stealth on damage
        /// </remarks>
        private Variable Func_IsStealthed(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // IsStealthed(object oTarget) - returns TRUE if target is stealthed (has invisibility effect)
            // Based on swkotor2.exe: Stealth system (TSL only)
            // Located via string references: "StealthMode" @ 0x007bf690, "StealthXPEnabled" @ 0x007bd1b4
            // Original implementation: Checks if entity has Invisibility effect active
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            
            if (entity == null || ctx.World == null || ctx.World.EffectSystem == null)
            {
                return Variable.FromInt(0);
            }
            
            // Check if entity has Invisibility effect (stealth)
            bool isStealthed = ctx.World.EffectSystem.HasEffect(entity, EffectType.Invisibility);
            return Variable.FromInt(isStealthed ? 1 : 0);
        }

        private Variable Func_GetStealthXPEnabled(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Get stealth XP enabled state from GameSession
            if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is GameSession.GameServicesContext services && services.GameSession != null)
            {
                return Variable.FromInt(services.GameSession.StealthXPEnabled ? 1 : 0);
            }
            return Variable.FromInt(1); // Default: enabled
        }

        private Variable Func_SetStealthXPEnabled(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int enabled = args.Count > 0 ? args[0].AsInt() : 1;
            
            // Set stealth XP enabled state in GameSession
            if (ctx is VM.ExecutionContext execCtx && execCtx.AdditionalContext is GameSession.GameServicesContext services && services.GameSession != null)
            {
                services.GameSession.StealthXPEnabled = (enabled != 0);
            }
            return Variable.Void();
        }

        // Workbench functions (TSL only)
        private Variable Func_ShowUpgradeScreen(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint item = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            uint workbench = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;
            int upgradeType = args.Count > 2 ? args[2].AsInt() : 0;
            // TODO: Show upgrade screen UI
            return Variable.Void();
        }

        private Variable Func_GetBaseItemType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint itemId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            
            IEntity item = ResolveObject(itemId, ctx);
            if (item == null)
            {
                return Variable.FromInt(0);
            }

            // Get base item type from item component
            Core.Interfaces.Components.IItemComponent itemComponent = item.GetComponent<Core.Interfaces.Components.IItemComponent>();
            if (itemComponent != null)
            {
                // BaseItem is the base item type ID from baseitems.2da
                return Variable.FromInt(itemComponent.BaseItem);
            }

            return Variable.FromInt(0);
        }

        // Combat form functions (TSL only)
        private Variable Func_GetIsFormActive(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creature = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int form = args.Count > 1 ? args[1].AsInt() : 0;
            
            IEntity entity = ResolveObject(creature, ctx);
            if (entity == null)
            {
                return Variable.FromInt(0);
            }
            
            // Get active combat form from entity data (stored as "ActiveCombatForm")
            // Combat forms: 0 = None, 1 = Beast, 2 = Droid, 3 = Force, etc.
            if (entity.HasData("ActiveCombatForm"))
            {
                int activeForm = entity.GetData<int>("ActiveCombatForm", 0);
                return Variable.FromInt(activeForm == form ? 1 : 0);
            }
            
            return Variable.FromInt(0); // No form active
        }

        // Swoop minigame functions (TSL extensions)
        private Variable Func_SWMG_GetPlayerOffset(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Get swoop minigame player offset
            return Variable.FromVector(Vector3.Zero);
        }

        private Variable Func_SWMG_GetPlayerInvincibility(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Get swoop minigame invincibility state from player entity
            if (ctx.AdditionalContext is GameSession.GameServicesContext services && services.PlayerEntity != null)
            {
                bool invincible = services.PlayerEntity.GetData<bool>("SwoopMinigameInvincible", false);
                return Variable.FromInt(invincible ? 1 : 0);
            }
            
            return Variable.FromInt(0); // Not invincible
        }

        private Variable Func_SWMG_SetPlayerInvincibility(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int invincible = args.Count > 0 ? args[0].AsInt() : 0;
            
            // Store swoop minigame invincibility state in player entity
            if (ctx.AdditionalContext is GameSession.GameServicesContext services && services.PlayerEntity != null)
            {
                // Store invincibility state in player entity data
                services.PlayerEntity.SetData("SwoopMinigameInvincible", invincible != 0);
            }
            
            return Variable.Void();
        }

        #endregion
    }
}
