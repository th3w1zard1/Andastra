using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using CSharpKOTOR.Common;
using CSharpKOTOR.Common.Script;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using Odyssey.Content.Interfaces;
using Odyssey.Core.Actions;
using Odyssey.Core.Combat;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Scripting.Interfaces;
using Odyssey.Scripting.Types;
using Odyssey.Scripting.VM;

namespace Odyssey.Scripting.EngineApi
{
    /// <summary>
    /// KOTOR 1 engine API implementation.
    /// </summary>
    public class K1EngineApi : BaseEngineApi
    {
        private readonly NcsVm _vm;

        public K1EngineApi()
        {
            _vm = new NcsVm();
        }

        protected override void RegisterFunctions()
        {
            // Register function names from ScriptDefs
            int idx = 0;
            foreach (ScriptFunction func in ScriptDefs.KOTOR_FUNCTIONS)
            {
                _functionNames[idx] = func.Name;
                idx++;
            }

            // Mark implemented functions
            _implementedFunctions.Add(0);   // Random
            _implementedFunctions.Add(1);   // PrintString
            _implementedFunctions.Add(168); // GetTag
            // Note: GetLocalInt/SetLocalInt are handled by the KOTOR-specific local variable functions
            // which use different IDs (679-682 for boolean/number locals)
        }

        public override Variable CallEngineFunction(int routineId, IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Dispatch to the appropriate handler based on routine ID
            // The routine IDs are in the order they appear in nwscript.nss
            switch (routineId)
            {
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
                case 11: return Func_SwitchPlayerCharacter(args, ctx);
                case 12: return Func_SetTime(args, ctx);
                case 13: return Func_SetPartyLeader(args, ctx);
                case 14: return Func_SetAreaUnescapable(args, ctx);
                case 15: return Func_GetAreaUnescapable(args, ctx);
                case 16: return Func_GetTimeHour(args, ctx);
                case 17: return Func_GetTimeMinute(args, ctx);
                case 18: return Func_GetTimeSecond(args, ctx);
                case 19: return Func_GetTimeMillisecond(args, ctx);
                case 20: return Func_ActionRandomWalk(args, ctx);
                case 21: return Func_ActionMoveToLocation(args, ctx);
                case 22: return Func_ActionMoveToObject(args, ctx);
                case 23: return Func_ActionMoveAwayFromObject(args, ctx);
                case 24: return Func_GetArea(args, ctx);
                case 25: return Func_GetEnteringObject(args, ctx);
                case 26: return Func_GetExitingObject(args, ctx);
                case 27: return Func_GetPosition(args, ctx);
                case 28: return Func_GetFacing(args, ctx);
                case 29: return Func_GetItemPossessor(args, ctx);
                case 30: return Func_GetItemPossessedBy(args, ctx);
                case 31: return Func_CreateItemOnObject(args, ctx);
                case 32: return Func_ActionEquipItem(args, ctx);
                case 33: return Func_ActionUnequipItem(args, ctx);
                case 34: return Func_ActionPickUpItem(args, ctx);
                case 35: return Func_ActionPutDownItem(args, ctx);
                case 36: return Func_GetLastAttacker(args, ctx);
                case 37: return Func_ActionAttack(args, ctx);
                case 38: return Func_GetNearestCreature(args, ctx);
                case 39: return Func_ActionSpeakString(args, ctx);
                case 40: return Func_ActionPlayAnimation(args, ctx);
                case 41: return Func_GetDistanceToObject(args, ctx);
                case 42: return Func_GetIsObjectValid(args, ctx);
                case 43: return Func_ActionOpenDoor(args, ctx);
                case 44: return Func_ActionCloseDoor(args, ctx);
                case 45: return Func_SetCameraFacing(args, ctx);
                case 46: return Func_PlaySound(args, ctx);
                case 47: return Func_GetSpellTargetObject(args, ctx);
                case 48: return Func_ActionCastSpellAtObject(args, ctx);
                case 49: return Func_GetCurrentHitPoints(args, ctx);
                case 50: return Func_GetMaxHitPoints(args, ctx);
                case 51: return Func_EffectAssuredHit(args, ctx);
                case 52: return Func_GetLastItemEquipped(args, ctx);
                case 53: return Func_GetSubScreenID(args, ctx);
                case 54: return Func_CancelCombat(args, ctx);
                case 55: return Func_GetCurrentForcePoints(args, ctx);
                case 56: return Func_GetMaxForcePoints(args, ctx);
                case 57: return Func_PauseGame(args, ctx);
                case 58: return Func_SetPlayerRestrictMode(args, ctx);
                case 59: return Func_GetStringLength(args, ctx);
                case 60: return Func_GetStringUpperCase(args, ctx);
                case 61: return Func_GetStringLowerCase(args, ctx);
                case 62: return Func_GetStringRight(args, ctx);
                case 63: return Func_GetStringLeft(args, ctx);
                case 64: return Func_InsertString(args, ctx);
                case 65: return Func_GetSubString(args, ctx);
                case 66: return Func_FindSubString(args, ctx);
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
                case 78: return Func_EffectHeal(args, ctx);
                case 79: return Func_EffectDamage(args, ctx);
                case 80: return Func_EffectAbilityIncrease(args, ctx);
                case 81: return Func_EffectDamageResistance(args, ctx);
                case 82: return Func_EffectResurrection(args, ctx);
                case 83: return Func_GetPlayerRestrictMode(args, ctx);
                case 84: return Func_GetCasterLevel(args, ctx);
                case 85: return Func_GetFirstEffect(args, ctx);
                case 86: return Func_GetNextEffect(args, ctx);
                case 87: return Func_RemoveEffect(args, ctx);
                case 88: return Func_GetIsEffectValid(args, ctx);
                case 89: return Func_GetEffectDurationType(args, ctx);
                case 90: return Func_GetEffectSubType(args, ctx);
                case 91: return Func_GetEffectCreator(args, ctx);
                case 92: return Func_IntToString(args, ctx);
                case 93: return Func_GetFirstObjectInArea(args, ctx);
                case 94: return Func_GetNextObjectInArea(args, ctx);
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
                case 105: return Func_GetMetaMagicFeat(args, ctx);
                case 106: return Func_GetObjectType(args, ctx);
                case 107: return Func_GetRacialType(args, ctx);
                case 108: return Func_FortitudeSave(args, ctx);
                case 109: return Func_ReflexSave(args, ctx);
                case 110: return Func_WillSave(args, ctx);
                case 111: return Func_GetSpellSaveDC(args, ctx);
                case 112: return Func_MagicalEffect(args, ctx);
                case 113: return Func_SupernaturalEffect(args, ctx);
                case 114: return Func_ExtraordinaryEffect(args, ctx);
                case 115: return Func_EffectACIncrease(args, ctx);
                case 116: return Func_GetAC(args, ctx);
                case 117: return Func_EffectSavingThrowIncrease(args, ctx);
                case 118: return Func_EffectAttackIncrease(args, ctx);
                case 119: return Func_EffectDamageReduction(args, ctx);
                case 120: return Func_EffectDamageIncrease(args, ctx);
                // ... more functions

                // GetAbilityScore (routine 139)
                case 139: return Func_GetAbilityScore(args, ctx);
                
                // GetItemInSlot (routine 155)
                case 155: return Func_GetItemInSlot(args, ctx);
                
                // PrintVector
                case 141: return Func_PrintVector(args, ctx);
                
                // ApplyEffectToObject (routine 220)
                case 220: return Func_ApplyEffectToObject(args, ctx);

                // Global string (restricted functions)
                case 160: return Func_SetGlobalString(args, ctx);
                case 194: return Func_GetGlobalString(args, ctx);

                // Core object functions (correct IDs from nwscript.nss)
                case 168: return Func_GetTag(args, ctx);
                case 200: return Func_GetObjectByTag(args, ctx);
                case 229: return Func_GetNearestObjectByTag(args, ctx);

                // Module
                case 242: return Func_GetModule(args, ctx);
                case 272: return Func_ObjectToString(args, ctx);

                // GetAbilityModifier (routine 331)
                case 331: return Func_GetAbilityModifier(args, ctx);
                
                // Global variables (KOTOR specific - different from standard NWN)
                case 578: return Func_GetGlobalBoolean(args, ctx);
                case 579: return Func_SetGlobalBoolean(args, ctx);
                case 580: return Func_GetGlobalNumber(args, ctx);
                case 581: return Func_SetGlobalNumber(args, ctx);

                // Local variables (KOTOR uses index-based, not name-based like NWN)
                // 679: GetLocalBoolean(object, int index) - index 0-63
                // 680: SetLocalBoolean(object, int index, int value)
                // 681: GetLocalNumber(object, int index) - index 0
                // 682: SetLocalNumber(object, int index, int value)
                case 679: return Func_GetLocalBoolean(args, ctx);
                case 680: return Func_SetLocalBoolean(args, ctx);
                case 681: return Func_GetLocalNumber(args, ctx);
                case 682: return Func_SetLocalNumber(args, ctx);

                default:
                    // Return default value for unimplemented functions
                    string funcName = GetFunctionName(routineId);
                    Console.WriteLine("[NCS] Unimplemented function: " + routineId + " (" + funcName + ")");
                    return Variable.Void();
            }
        }

        #region Basic Utility Functions

        private new Variable Func_Random(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Random(int nMaxInteger) - returns 0 to nMaxInteger-1
            int maxValue = args.Count > 0 ? args[0].AsInt() : 0;
            if (maxValue <= 0)
            {
                return Variable.FromInt(0);
            }
            return Variable.FromInt(_random.Next(maxValue));
        }

        private new Variable Func_PrintString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // PrintString(string sString) - outputs to console/log
            string message = args.Count > 0 ? args[0].AsString() : "";
            Console.WriteLine("[SCRIPT] " + message);
            return Variable.Void();
        }

        private new Variable Func_GetTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetTag(object oObject) - returns the tag string
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                return Variable.FromString(entity.Tag ?? "");
            }
            return Variable.FromString("");
        }

        private new Variable Func_GetLocalInt(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLocalInt(object oObject, string sVarName) - get local integer variable
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string varName = args.Count > 1 ? args[1].AsString() : "";
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && !string.IsNullOrEmpty(varName))
            {
                return Variable.FromInt(ctx.Globals.GetLocalInt(entity, varName));
            }
            return Variable.FromInt(0);
        }

        private new Variable Func_SetLocalInt(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // SetLocalInt(object oObject, string sVarName, int nValue) - set local integer variable
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string varName = args.Count > 1 ? args[1].AsString() : "";
            int value = args.Count > 2 ? args[2].AsInt() : 0;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && !string.IsNullOrEmpty(varName))
            {
                ctx.Globals.SetLocalInt(entity, varName, value);
            }
            return Variable.Void();
        }

        #endregion

        #region Action Functions

        private Variable Func_AssignCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // AssignCommand(object oActionSubject, action aActionToAssign)
            // This pushes an action onto the target's action queue
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
                Console.WriteLine($"[K1EngineApi] Error executing script {scriptName}: {ex.Message}");
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

        private Variable Func_ActionRandomWalk(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionRandomWalk() - no parameters in KOTOR
            if (ctx.Caller != null)
            {
                var action = new ActionRandomWalk();
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }
            return Variable.Void();
        }

        private Variable Func_ActionMoveToLocation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionMoveToLocation(location lDestination, int bRun=FALSE)
            object locationObj = args.Count > 0 ? args[0].ComplexValue : null;
            bool run = args.Count > 1 && args[1].AsInt() != 0;

            if (ctx.Caller == null)
            {
                return Variable.Void();
            }

            // Extract position from location object
            Vector3 destination = Vector3.Zero;
            if (locationObj is Location location)
            {
                destination = location.Position;
            }
            else if (locationObj is Vector3 vector)
            {
                destination = vector;
            }
            else
            {
                // Try to extract from complex value
                // Location should have Position and Facing properties
                var locationType = locationObj?.GetType();
                if (locationType != null)
                {
                    var positionProp = locationType.GetProperty("Position");
                    if (positionProp != null)
                    {
                        object posValue = positionProp.GetValue(locationObj);
                        if (posValue is Vector3 pos)
                        {
                            destination = pos;
                        }
                    }
                }
            }

            // Create and queue move action
            var moveAction = new ActionMoveToLocation(destination, run);
            IActionQueueComponent actionQueue = ctx.Caller.GetComponent<IActionQueueComponent>();
            if (actionQueue != null)
            {
                actionQueue.Add(moveAction);
            }

            return Variable.Void();
        }

        private Variable Func_ActionMoveToObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionMoveToObject(object oMoveTo, int bRun=FALSE, float fRange=1.0f)
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            bool run = args.Count > 1 && args[1].AsInt() != 0;
            float range = args.Count > 2 ? args[2].AsFloat() : 1.0f;

            if (ctx.Caller != null)
            {
                var action = new ActionMoveToObject(targetId, run, range);
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }

            return Variable.Void();
        }

        private Variable Func_ActionMoveAwayFromObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionMoveAwayFromObject(object oFleeFrom, int bRun=FALSE, float fMoveAwayRange=40.0f)
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            bool run = args.Count > 1 && args[1].AsInt() != 0;
            float distance = args.Count > 2 ? args[2].AsFloat() : 40.0f;

            if (ctx.Caller != null)
            {
                var action = new ActionMoveAwayFromObject(targetId, run, distance);
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }

            return Variable.Void();
        }

        private Variable Func_ActionSpeakString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string text = args.Count > 0 ? args[0].AsString() : string.Empty;
            int volume = args.Count > 1 ? args[1].AsInt() : 0;

            var action = new ActionSpeakString(text, volume);
            if (ctx.Caller != null)
            {
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }

            return Variable.Void();
        }

        private Variable Func_ActionPlayAnimation(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int animation = args.Count > 0 ? args[0].AsInt() : 0;
            float speed = args.Count > 1 ? args[1].AsFloat() : 1.0f;
            float duration = args.Count > 2 ? args[2].AsFloat() : 0f;

            var action = new ActionPlayAnimation(animation, speed, duration);
            if (ctx.Caller != null)
            {
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }

            return Variable.Void();
        }

        private Variable Func_ActionOpenDoor(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint doorId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            if (ctx.Caller != null)
            {
                var action = new ActionOpenDoor(doorId);
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }
            return Variable.Void();
        }

        private Variable Func_ActionCloseDoor(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint doorId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            if (ctx.Caller != null)
            {
                var action = new ActionCloseDoor(doorId);
                IActionQueue queue = ctx.Caller.GetComponent<IActionQueue>();
                if (queue != null)
                {
                    queue.Add(action);
                }
            }
            return Variable.Void();
        }

        private Variable Func_ActionAttack(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ActionAttack(object oTarget, int bPassive = FALSE)
            uint targetId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            bool passive = args.Count > 1 && args[1].AsInt() != 0;

            if (ctx.Caller == null)
            {
                return Variable.Void();
            }

            IEntity target = ctx.World.GetEntity(targetId);
            if (target == null || !target.IsValid)
            {
                return Variable.Void();
            }

            // Create and queue attack action
            var attackAction = new ActionAttack(targetId, passive);
            IActionQueueComponent actionQueue = ctx.Caller.GetComponent<IActionQueueComponent>();
            if (actionQueue != null)
            {
                actionQueue.Add(attackAction);
            }

            return Variable.Void();
        }

        #endregion

        #region Object Functions

        private Variable Func_PrintObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            Console.WriteLine("[Script] Object: 0x" + objectId.ToString("X8"));
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

            return Variable.FromFloat(-1f); // Invalid
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

        private Variable Func_GetEnteringObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Triggerer != null)
            {
                return Variable.FromObject(ctx.Triggerer.ObjectId);
            }
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetExitingObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (ctx.Triggerer != null)
            {
                return Variable.FromObject(ctx.Triggerer.ObjectId);
            }
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetNearestCreature(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetNearestCreature(int nFirstCriteriaType, int nFirstCriteriaValue, ...)
            // Simplified implementation
            if (ctx.Caller != null)
            {
                Core.Interfaces.Components.ITransformComponent transform = ctx.Caller.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    IEnumerable<IEntity> entities = ctx.World.GetEntitiesInRadius(transform.Position, 100f, ObjectType.Creature);
                    float nearestDist = float.MaxValue;
                    IEntity nearest = null;

                    foreach (IEntity e in entities)
                    {
                        if (e == ctx.Caller) continue;

                        Core.Interfaces.Components.ITransformComponent t = e.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                        if (t != null)
                        {
                            float dist = Vector3.DistanceSquared(transform.Position, t.Position);
                            if (dist < nearestDist)
                            {
                                nearestDist = dist;
                                nearest = e;
                            }
                        }
                    }

                    if (nearest != null)
                    {
                        return Variable.FromObject(nearest.ObjectId);
                    }
                }
            }
            return Variable.FromObject(ObjectInvalid);
        }

        #endregion

        #region Global Variable Functions

        private new Variable Func_GetGlobalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromInt(ctx.Globals.GetGlobalInt(name));
        }

        private new Variable Func_SetGlobalNumber(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            int value = args.Count > 1 ? args[1].AsInt() : 0;
            ctx.Globals.SetGlobalInt(name, value);
            return Variable.Void();
        }

        private new Variable Func_GetGlobalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromInt(ctx.Globals.GetGlobalBool(name) ? 1 : 0);
        }

        private new Variable Func_SetGlobalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            bool value = args.Count > 1 && args[1].AsInt() != 0;
            ctx.Globals.SetGlobalBool(name, value);
            return Variable.Void();
        }

        private new Variable Func_GetGlobalString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            return Variable.FromString(ctx.Globals.GetGlobalString(name));
        }

        private new Variable Func_SetGlobalString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string name = args.Count > 0 ? args[0].AsString() : string.Empty;
            string value = args.Count > 1 ? args[1].AsString() : string.Empty;
            ctx.Globals.SetGlobalString(name, value);
            return Variable.Void();
        }

        private Variable Func_GetLocalObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            string name = args.Count > 1 ? args[1].AsString() : string.Empty;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                IEntity localObj = ctx.Globals.GetLocalObject(entity, name);
                return Variable.FromObject(localObj?.ObjectId ?? ObjectInvalid);
            }
            return Variable.FromObject(ObjectInvalid);
        }

        // KOTOR Local Boolean/Number functions (index-based, not name-based like NWN)
        private Variable Func_GetLocalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetLocalBoolean(object oObject, int nIndex)
            // Index range: 0-63
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int index = args.Count > 1 ? args[1].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && index >= 0 && index < 64)
            {
                // Store as local int with index-based key
                return Variable.FromInt(ctx.Globals.GetLocalInt(entity, "_LB_" + index));
            }
            return Variable.FromInt(0);
        }

        private Variable Func_SetLocalBoolean(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // SetLocalBoolean(object oObject, int nIndex, int nValue)
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
            // GetLocalNumber(object oObject, int nIndex)
            // Index range: 0 only
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
            // SetLocalNumber(object oObject, int nIndex, int nValue)
            // Value range: -128 to +127
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int index = args.Count > 1 ? args[1].AsInt() : 0;
            int value = args.Count > 2 ? args[2].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null && index == 0)
            {
                // Clamp to -128 to 127
                if (value > 127) value = 127;
                if (value < -128) value = -128;
                ctx.Globals.SetLocalInt(entity, "_LN_" + index, value);
            }
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

        #region Placeholder Functions

        private Variable Func_SwitchPlayerCharacter(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_SetTime(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_SetPartyLeader(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_SetAreaUnescapable(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_GetAreaUnescapable(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private Variable Func_GetTimeHour(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(12);
        }

        private Variable Func_GetTimeMinute(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private Variable Func_GetTimeSecond(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private Variable Func_GetTimeMillisecond(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private new Variable Func_GetArea(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return base.Func_GetArea(args, ctx);
        }

        private new Variable Func_GetObjectByTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return base.Func_GetObjectByTag(args, ctx);
        }

        private new Variable Func_GetNearestObjectByTag(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return base.Func_GetNearestObjectByTag(args, ctx);
        }

        private new Variable Func_GetModule(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return base.Func_GetModule(args, ctx);
        }

        private new Variable Func_ObjectToString(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return base.Func_ObjectToString(args, ctx);
        }

        private Variable Func_GetItemPossessor(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetItemPossessedBy(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_CreateItemOnObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // CreateItemOnObject(string sItemTemplate, object oTarget=OBJECT_SELF, int nStackSize=1, int nHideMessage = 0)
            string itemTemplate = args.Count > 0 ? args[0].AsString() : string.Empty;
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            int stackSize = args.Count > 2 ? args[2].AsInt() : 1;
            int hideMessage = args.Count > 3 ? args[3].AsInt() : 0;

            if (string.IsNullOrEmpty(itemTemplate) || ctx.World == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            IEntity target = ResolveObject(targetId, ctx);
            if (target == null)
            {
                target = ctx.Caller;
            }

            if (target == null)
            {
                return Variable.FromObject(ObjectInvalid);
            }

            // Load UTI template from resource provider
            UTI utiTemplate = null;
            if (ctx.ResourceProvider != null)
            {
                try
                {
                    // Try IGameResourceProvider first
                    if (ctx.ResourceProvider is IGameResourceProvider gameProvider)
                    {
                        var resourceId = new ResourceIdentifier(itemTemplate, ResourceType.UTI);
                        System.Threading.Tasks.Task<byte[]> task = gameProvider.GetResourceBytesAsync(resourceId, CancellationToken.None);
                        task.Wait();
                        byte[] utiData = task.Result;

                        if (utiData != null && utiData.Length > 0)
                        {
                            using (var stream = new MemoryStream(utiData))
                            {
                                var reader = new GFFBinaryReader(stream);
                                GFF gff = reader.Load();
                                utiTemplate = UTIHelpers.ConstructUti(gff);
                            }
                        }
                    }
                    // Fallback to CSharpKOTOR Installation provider
                    else if (ctx.ResourceProvider is CSharpKOTOR.Installation.Installation installation)
                    {
                        CSharpKOTOR.Installation.ResourceResult result = installation.Resource(itemTemplate, ResourceType.UTI, null, null);
                        if (result != null && result.Data != null)
                        {
                            using (var stream = new MemoryStream(result.Data))
                            {
                                var reader = new GFFBinaryReader(stream);
                                GFF gff = reader.Load();
                                utiTemplate = UTIHelpers.ConstructUti(gff);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[K1EngineApi] Error loading UTI template '{itemTemplate}': {ex.Message}");
                }
            }

            // Create item entity
            IEntity itemEntity = ctx.World.CreateEntity(ObjectType.Item, Vector3.Zero, 0f);
            if (itemEntity != null)
            {
                // Set tag from template or use template name
                if (utiTemplate != null && !string.IsNullOrEmpty(utiTemplate.Tag))
                {
                    itemEntity.Tag = utiTemplate.Tag;
                }
                else
                {
                    itemEntity.Tag = itemTemplate;
                }

                // TODO: Add item component with UTI template data
                // This would require creating an IItemComponent interface that stores:
                // - BaseItem, StackSize, Charges, Cost, Properties, etc.
                // For now, the entity is created with the tag

                // Add to target's inventory if target has inventory component
                IInventoryComponent inventory = target.GetComponent<IInventoryComponent>();
                if (inventory != null)
                {
                    // Add item to inventory (finds first available slot)
                    if (!inventory.AddItem(itemEntity))
                    {
                        // Inventory full, destroy item entity
                        ctx.World.DestroyEntity(itemEntity.ObjectId);
                        return Variable.FromObject(ObjectInvalid);
                    }
                }

                return Variable.FromObject(itemEntity.ObjectId);
            }

            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_ActionEquipItem(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_ActionUnequipItem(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_ActionPickUpItem(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_ActionPutDownItem(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_GetLastAttacker(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_SetCameraFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_PlaySound(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_GetSpellTargetObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_ActionCastSpellAtObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_GetCurrentHitPoints(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.CurrentHP);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetMaxHitPoints(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.MaxHP);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetAbilityScore(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetAbilityScore(object oCreature, int nAbilityType)
            // nAbilityType: ABILITY_STRENGTH (0), ABILITY_DEXTERITY (1), ABILITY_CONSTITUTION (2),
            //              ABILITY_INTELLIGENCE (3), ABILITY_WISDOM (4), ABILITY_CHARISMA (5)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            int abilityType = args.Count > 1 ? args[1].AsInt() : 0;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    // Map ability type to Ability enum
                    if (abilityType >= 0 && abilityType <= 5)
                    {
                        Ability ability = (Ability)abilityType;
                        return Variable.FromInt(stats.GetAbility(ability));
                    }
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetAbilityModifier(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetAbilityModifier(int nAbility, object oCreature=OBJECT_SELF)
            // nAbility: ABILITY_STRENGTH (0), ABILITY_DEXTERITY (1), ABILITY_CONSTITUTION (2),
            //          ABILITY_INTELLIGENCE (3), ABILITY_WISDOM (4), ABILITY_CHARISMA (5)
            int abilityType = args.Count > 0 ? args[0].AsInt() : 0;
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    // Map ability type to Ability enum
                    if (abilityType >= 0 && abilityType <= 5)
                    {
                        Ability ability = (Ability)abilityType;
                        return Variable.FromInt(stats.GetAbilityModifier(ability));
                    }
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetItemInSlot(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetItemInSlot(int nInventorySlot, object oCreature=OBJECT_SELF)
            int inventorySlot = args.Count > 0 ? args[0].AsInt() : 0;
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;

            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IInventoryComponent inventory = entity.GetComponent<Core.Interfaces.Components.IInventoryComponent>();
                if (inventory != null)
                {
                    IEntity item = inventory.GetItemInSlot(inventorySlot);
                    if (item != null)
                    {
                        return Variable.FromObject(item.ObjectId);
                    }
                }
            }
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_ApplyEffectToObject(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ApplyEffectToObject(int nDurationType, effect eEffect, object oTarget, float fDuration=0.0f)
            // nDurationType: DURATION_TYPE_INSTANT (0), DURATION_TYPE_TEMPORARY (1), DURATION_TYPE_PERMANENT (2)
            int durationType = args.Count > 0 ? args[0].AsInt() : 0;
            object effectObj = args.Count > 1 ? args[1].ComplexValue : null;
            uint targetId = args.Count > 2 ? args[2].AsObjectId() : ObjectSelf;
            float duration = args.Count > 3 ? args[3].AsFloat() : 0f;

            if (effectObj == null || ctx.World == null)
            {
                return Variable.Void();
            }

            IEntity target = ResolveObject(targetId, ctx);
            if (target == null)
            {
                target = ctx.Caller;
            }

            if (target == null)
            {
                return Variable.Void();
            }

            // Convert effect object to Effect
            Effect effect = null;
            if (effectObj is Effect directEffect)
            {
                effect = directEffect;
            }
            else
            {
                // TODO: Convert from script effect type to Effect
                // For now, return void
                return Variable.Void();
            }

            // Map duration type
            EffectDurationType effectDurationType;
            switch (durationType)
            {
                case 0: // DURATION_TYPE_INSTANT
                    effectDurationType = EffectDurationType.Instant;
                    break;
                case 1: // DURATION_TYPE_TEMPORARY
                    effectDurationType = EffectDurationType.Temporary;
                    if (duration > 0f)
                    {
                        // Convert seconds to rounds (assuming 6 seconds per round)
                        effect.DurationRounds = (int)Math.Ceiling(duration / 6f);
                    }
                    break;
                case 2: // DURATION_TYPE_PERMANENT
                    effectDurationType = EffectDurationType.Permanent;
                    break;
                default:
                    return Variable.Void();
            }

            effect.DurationType = effectDurationType;

            // Apply effect using EffectSystem from world
            if (ctx.World != null && ctx.World.EffectSystem != null)
            {
                ctx.World.EffectSystem.ApplyEffect(target, effect, ctx.Caller);
            }

            return Variable.Void();
        }

        // Effect placeholders
        private Variable Func_EffectAssuredHit(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(new object());
        }

        private Variable Func_GetLastItemEquipped(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetSubScreenID(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private Variable Func_CancelCombat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_GetCurrentForcePoints(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetCurrentForcePoints(object oCreature = OBJECT_SELF)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.CurrentFP);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetMaxForcePoints(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // GetMaxForcePoints(object oCreature = OBJECT_SELF)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.MaxFP);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_PauseGame(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_SetPlayerRestrictMode(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_GetPlayerRestrictMode(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private Variable Func_GetCasterLevel(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(1);
        }

        private Variable Func_GetFirstEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(null);
        }

        private Variable Func_GetNextEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(null);
        }

        private Variable Func_RemoveEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.Void();
        }

        private Variable Func_GetIsEffectValid(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private Variable Func_GetEffectDurationType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private Variable Func_GetEffectSubType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private Variable Func_GetEffectCreator(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetFirstObjectInArea(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetNextObjectInArea(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromObject(ObjectInvalid);
        }

        private Variable Func_GetMetaMagicFeat(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private Variable Func_GetRacialType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(0);
        }

        private Variable Func_FortitudeSave(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // FortitudeSave(object oCreature = OBJECT_SELF)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.FortitudeSave);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_ReflexSave(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ReflexSave(object oCreature = OBJECT_SELF)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.ReflexSave);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_WillSave(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // WillSave(object oCreature = OBJECT_SELF)
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.WillSave);
                }
            }
            return Variable.FromInt(0);
        }

        private Variable Func_GetSpellSaveDC(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromInt(10);
        }

        private Variable Func_MagicalEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(args.Count > 0 ? args[0].ComplexValue : null);
        }

        private Variable Func_SupernaturalEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(args.Count > 0 ? args[0].ComplexValue : null);
        }

        private Variable Func_ExtraordinaryEffect(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(args.Count > 0 ? args[0].ComplexValue : null);
        }

        private Variable Func_EffectACIncrease(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(new object());
        }

        private Variable Func_GetAC(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                Core.Interfaces.Components.IStatsComponent stats = entity.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    return Variable.FromInt(stats.ArmorClass);
                }
            }
            return Variable.FromInt(10);
        }

        private Variable Func_EffectHeal(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(new object());
        }

        private Variable Func_EffectDamage(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(new object());
        }

        private Variable Func_EffectAbilityIncrease(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(new object());
        }

        private Variable Func_EffectDamageResistance(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(new object());
        }

        private Variable Func_EffectResurrection(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(new object());
        }

        private Variable Func_EffectSavingThrowIncrease(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(new object());
        }

        private Variable Func_EffectAttackIncrease(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(new object());
        }

        private Variable Func_EffectDamageReduction(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(new object());
        }

        private Variable Func_EffectDamageIncrease(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            return Variable.FromEffect(new object());
        }

        #endregion
    }
}

