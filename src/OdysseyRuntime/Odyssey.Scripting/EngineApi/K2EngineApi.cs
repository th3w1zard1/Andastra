using System;
using System.Collections.Generic;
using System.Numerics;
using CSharpKOTOR.Common.Script;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Scripting.EngineApi
{
    /// <summary>
    /// KOTOR 2 (TSL) engine API implementation.
    /// Extends K1 API with TSL-specific functions.
    /// </summary>
    /// <remarks>
    /// TSL adds ~100 additional engine functions beyond K1, including:
    /// - Influence system functions
    /// - Party puppet functions
    /// - Workbench/lab functions
    /// - Combat form functions
    /// - Enhanced visual effect functions
    /// </remarks>
    public class K2EngineApi : BaseEngineApi
    {
        public K2EngineApi()
        {
        }
        
        protected override void RegisterFunctions()
        {
            // Register function names from ScriptDefs for TSL
            int idx = 0;
            foreach (var func in ScriptDefs.TSL_FUNCTIONS)
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
            var action = args.Count > 1 ? args[1].ComplexValue as IAction : null;
            
            var target = ResolveObject(targetId, ctx);
            if (target != null && action != null)
            {
                var queue = target.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }
            
            return Variable.Void();
        }
        
        private Variable Func_DelayCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // TODO: Schedule action in delay scheduler
            return Variable.Void();
        }
        
        private Variable Func_ExecuteScript(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // TODO: Execute nested script
            return Variable.Void();
        }
        
        private Variable Func_ClearAllActions(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            var caller = ctx.Caller;
            if (caller != null)
            {
                var queue = caller.GetComponent<IActionQueue>();
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
                var transform = ctx.Caller.GetComponent<Core.Interfaces.Components.ITransformComponent>();
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
            var entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                var transform = entity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
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
            var entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                var transform = entity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
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
            var target = ResolveObject(targetId, ctx);
            
            if (ctx.Caller != null && target != null)
            {
                var callerTransform = ctx.Caller.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                var targetTransform = target.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                
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
            var entity = ResolveObject(objectId, ctx);
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
            var v = args.Count > 0 ? args[0].AsVector() : Vector3.Zero;
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
            
            var entity = ResolveObject(objectId, ctx);
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
            
            var entity = ResolveObject(objectId, ctx);
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
            
            var entity = ResolveObject(objectId, ctx);
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
            
            var entity = ResolveObject(objectId, ctx);
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
        private Variable Func_GetInfluence(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetInfluence(int nNPC)
            // Returns influence value for NPC (0-100)
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            // TODO: Implement influence tracking
            return Variable.FromInt(50); // Default neutral influence
        }
        
        private Variable Func_SetInfluence(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // SetInfluence(int nNPC, int nInfluence)
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            int influence = args.Count > 1 ? args[1].AsInt() : 50;
            // TODO: Implement influence tracking
            return Variable.Void();
        }
        
        private Variable Func_ModifyInfluence(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ModifyInfluence(int nNPC, int nModifier)
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            int modifier = args.Count > 1 ? args[1].AsInt() : 0;
            // TODO: Implement influence modification
            return Variable.Void();
        }
        
        // Party puppet functions (TSL only)
        private Variable Func_GetPartyMemberByIndex(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int index = args.Count > 0 ? args[0].AsInt() : 0;
            // TODO: Implement party member lookup
            return Variable.FromObject(ObjectInvalid);
        }
        
        private Variable Func_IsAvailableCreature(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            // TODO: Check if NPC is available for party selection
            return Variable.FromInt(0);
        }
        
        private Variable Func_AddAvailableNPCByTemplate(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            string template = args.Count > 1 ? args[1].AsString() : string.Empty;
            // TODO: Add NPC to available party members
            return Variable.FromInt(1);
        }
        
        private Variable Func_GetNPCSelectability(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            // TODO: Return NPC selectability status
            return Variable.FromInt(1);
        }
        
        private Variable Func_SetNPCSelectability(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            int selectable = args.Count > 1 ? args[1].AsInt() : 1;
            // TODO: Set NPC selectability
            return Variable.Void();
        }
        
        // Remote/stealth functions (TSL only)
        private Variable Func_IsStealthed(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            // TODO: Check stealth status
            return Variable.FromInt(0);
        }
        
        private Variable Func_GetStealthXPEnabled(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // TODO: Check if stealth XP is enabled
            return Variable.FromInt(1);
        }
        
        private Variable Func_SetStealthXPEnabled(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int enabled = args.Count > 0 ? args[0].AsInt() : 1;
            // TODO: Enable/disable stealth XP
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
            // TODO: Get item base type from baseitems.2da
            return Variable.FromInt(0);
        }
        
        // Combat form functions (TSL only)
        private Variable Func_GetIsFormActive(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint creature = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int form = args.Count > 1 ? args[1].AsInt() : 0;
            // TODO: Check if combat form is active
            return Variable.FromInt(0);
        }
        
        // Swoop minigame functions (TSL extensions)
        private Variable Func_SWMG_GetPlayerOffset(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Get swoop minigame player offset
            return Variable.FromVector(Vector3.Zero);
        }
        
        private Variable Func_SWMG_GetPlayerInvincibility(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Get swoop minigame invincibility
            return Variable.FromInt(0);
        }
        
        private Variable Func_SWMG_SetPlayerInvincibility(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int invincible = args.Count > 0 ? args[0].AsInt() : 0;
            // TODO: Set swoop minigame invincibility
            return Variable.Void();
        }
        
        #endregion
    }
}
