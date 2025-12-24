using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing.Common.Script;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Scripting.EngineApi;
using Andastra.Runtime.Scripting.Interfaces;
using Andastra.Runtime.Scripting.Types;
using Andastra.Runtime.Scripting.VM;

namespace Andastra.Runtime.Engines.Odyssey.EngineApi
{
    /// <summary>
    /// Base engine API implementation for Odyssey engine family (KOTOR, TSL, Jade Empire).
    /// Contains common functionality shared between Kotor1 and TheSithLords.
    /// </summary>
    /// <remarks>
    /// Odyssey Engine API Base:
    /// - Based on swkotor.exe and swkotor2.exe NWScript engine API implementations
    /// - Contains common functions shared between K1 and K2
    /// - Odyssey-specific features: Action system, dialogue system, combat system, party system
    /// - Common functions: Movement actions, combat actions, item management, dialogue functions
    /// - Inheritance: BaseEngineApi (common across all engines) -> OdysseyBaseEngineApi (Odyssey-specific) -> Kotor1/TheSithLords (game-specific)
    /// </remarks>
    public abstract class OdysseyBaseEngineApi : BaseEngineApi
    {
        protected readonly NcsVm _vm;

        // Iteration state for GetFirstFactionMember/GetNextFactionMember
        // Key: caller entity ID, Value: list of faction members and current index
        protected readonly Dictionary<uint, FactionMemberIteration> _factionMemberIterations;

        // Iteration state for GetFirstObjectInArea/GetNextObjectInArea
        // Key: caller entity ID, Value: list of area objects and current index
        protected readonly Dictionary<uint, AreaObjectIteration> _areaObjectIterations;

        // Iteration state for GetFirstEffect/GetNextEffect
        // Key: caller entity ID, Value: list of effects and current index
        protected readonly Dictionary<uint, EffectIteration> _effectIterations;

        // Iteration state for GetFirstInPersistentObject/GetNextInPersistentObject
        // Key: caller entity ID, Value: list of persistent objects and current index
        protected readonly Dictionary<uint, PersistentObjectIteration> _persistentObjectIterations;

        // Iteration state for GetFirstItemInInventory/GetNextItemInInventory
        // Key: caller entity ID, Value: list of inventory items and current index
        protected readonly Dictionary<uint, InventoryItemIteration> _inventoryItemIterations;

        // Track last spell target for GetSpellTargetObject
        // Key: caster entity ID, Value: target entity ID
        protected readonly Dictionary<uint, uint> _lastSpellTargets;

        // Track last equipped item for GetLastItemEquipped
        // Key: creature entity ID, Value: item entity ID
        protected readonly Dictionary<uint, uint> _lastEquippedItems;

        // Track player restriction state
        protected bool _playerRestricted;

        // Track last spell cast metamagic type for GetMetaMagicFeat
        // Key: caster entity ID, Value: metamagic feat type (METAMAGIC_* constants)
        protected readonly Dictionary<uint, int> _lastMetamagicTypes;

        // Track last spell ID for GetSpellId
        // Key: caster entity ID, Value: spell ID
        protected readonly Dictionary<uint, int> _lastSpellIds;

        // Track last spell caster for GetLastSpellCaster
        // Key: target entity ID, Value: caster entity ID
        protected readonly Dictionary<uint, uint> _lastSpellCasters;

        // Track last spell target location for GetSpellTargetLocation
        // Key: caster entity ID, Value: target location
        protected readonly Dictionary<uint, Location> _lastSpellTargetLocations;

        // Track user-defined event number for GetUserDefinedEventNumber
        protected int _userDefinedEventNumber;

        // Track run script variable for GetRunScriptVar
        protected Variable _runScriptVar;

        protected OdysseyBaseEngineApi()
        {
            _vm = new NcsVm();
            _factionMemberIterations = new Dictionary<uint, FactionMemberIteration>();
            _areaObjectIterations = new Dictionary<uint, AreaObjectIteration>();
            _effectIterations = new Dictionary<uint, EffectIteration>();
            _persistentObjectIterations = new Dictionary<uint, PersistentObjectIteration>();
            _inventoryItemIterations = new Dictionary<uint, InventoryItemIteration>();
            _lastSpellTargets = new Dictionary<uint, uint>();
            _lastEquippedItems = new Dictionary<uint, uint>();
            _lastMetamagicTypes = new Dictionary<uint, int>();
            _lastSpellIds = new Dictionary<uint, int>();
            _lastSpellCasters = new Dictionary<uint, uint>();
            _lastSpellTargetLocations = new Dictionary<uint, Location>();
            _userDefinedEventNumber = 0;
            _runScriptVar = Variable.Void();
            _playerRestricted = false;
        }

        protected class FactionMemberIteration
        {
            public List<IEntity> Members { get; set; }
            public int CurrentIndex { get; set; }
        }

        protected class AreaObjectIteration
        {
            public List<IEntity> Objects { get; set; }
            public int CurrentIndex { get; set; }
        }

        protected class EffectIteration
        {
            public List<Andastra.Runtime.Core.Combat.ActiveEffect> Effects { get; set; }
            public int CurrentIndex { get; set; }
        }

        protected class PersistentObjectIteration
        {
            public List<IEntity> Objects { get; set; }
            public int CurrentIndex { get; set; }
        }

        protected class InventoryItemIteration
        {
            public List<IEntity> Items { get; set; }
            public int CurrentIndex { get; set; }
        }

        #region Common Odyssey Action Functions

        /// <summary>
        /// AssignCommand(object oActionSubject, action aActionToAssign) - Assigns an action to an object
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Action system
        /// Original implementation: Pushes action onto target's action queue
        /// </remarks>
        protected new Variable Func_AssignCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
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

        /// <summary>
        /// DelayCommand(float fSeconds, action aActionToDelay) - Delays execution of an action
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: DelayCommand implementation in NCS VM
        /// Original implementation: NCS VM uses STORE_STATE opcode to save stack/local state, then schedules
        /// action execution after delay. Delay wheel processes delayed commands each frame.
        /// </remarks>
        protected new Variable Func_DelayCommand(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float delay = args.Count > 0 ? args[0].AsFloat() : 0f;
            IAction action = args.Count > 1 ? args[1].ComplexValue as IAction : null;

            if (action != null && ctx.Caller != null && ctx.World != null)
            {
                ctx.World.DelayScheduler.ScheduleDelay(delay, action, ctx.Caller);
            }

            return Variable.Void();
        }

        /// <summary>
        /// ExecuteScript(string sScript, object oTarget, int nScriptVar) - Executes a script on a target
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Script execution system
        /// Original implementation: Loads and executes NCS script on target entity
        /// </remarks>
        protected new Variable Func_ExecuteScript(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            string scriptName = args.Count > 0 ? args[0].AsString() : string.Empty;
            uint targetId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            int scriptVar = args.Count > 2 ? args[2].AsInt() : -1;

            if (string.IsNullOrEmpty(scriptName))
            {
                return Variable.Void();
            }

            IEntity target = ResolveObject(targetId, ctx);
            if (target == null)
            {
                target = ctx.Caller;
            }

            if (target == null || ctx.World == null || ctx.ResourceProvider == null)
            {
                return Variable.Void();
            }

            try
            {
                IExecutionContext scriptCtx = ctx.WithCaller(target);
                if (scriptVar >= 0)
                {
                    _runScriptVar = Variable.FromInt(scriptVar);
                }

                // Execute script and track instruction count
                // Based on swkotor2.exe: Script execution with instruction budget tracking
                // Located via string references: Script execution budget limits per frame
                // Original implementation: Tracks instruction count per entity for budget enforcement
                int result = _vm.ExecuteScript(scriptName, scriptCtx);

                // Accumulate instruction count to target entity's action queue component
                // This allows the game loop to enforce per-frame script budget limits
                int instructionsExecuted = _vm.InstructionsExecuted;
                if (instructionsExecuted > 0 && target != null)
                {
                    IActionQueueComponent actionQueue = target.GetComponent<IActionQueueComponent>();
                    if (actionQueue != null)
                    {
                        actionQueue.AddInstructionCount(instructionsExecuted);
                    }
                }

                return Variable.FromInt(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Odyssey] Error executing script {0}: {1}", scriptName, ex.Message);
            }

            return Variable.Void();
        }

        /// <summary>
        /// ClearAllActions() - Clears all actions from the caller
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Action queue system
        /// Original implementation: Clears action queue for caller entity
        /// </remarks>
        protected new Variable Func_ClearAllActions(IReadOnlyList<Variable> args, IExecutionContext ctx)
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

        /// <summary>
        /// SetFacing(float fDirection) - Sets the facing direction of the caller
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Transform system
        /// Original implementation: Sets entity facing angle (degrees, anticlockwise from East)
        /// </remarks>
        protected new Variable Func_SetFacing(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            float facing = args.Count > 0 ? args[0].AsFloat() : 0f;
            if (ctx.Caller != null)
            {
                ITransformComponent transform = ctx.Caller.GetComponent<ITransformComponent>();
                if (transform != null)
                {
                    transform.Facing = facing * (float)Math.PI / 180f;
                }
            }
            return Variable.Void();
        }

        // GetPosition and GetFacing are now implemented in BaseEngineApi
        // They are identical across all engines (Odyssey, Aurora, Eclipse, Infinity)
        // Verified via Ghidra MCP analysis:
        // - nwmain.exe: ExecuteCommandGetPosition @ 0x14052f5b0, ExecuteCommandGetFacing @ 0x140523a70
        // - swkotor.exe/swkotor2.exe: Equivalent transform system implementations
        // - daorigins.exe: Equivalent transform system implementations
        // Kotor1.cs and TheSithLords.cs call base.Func_GetPosition/base.Func_GetFacing which now resolve to BaseEngineApi


        /// <summary>
        /// GetObjectType(object oObject) - Gets the object type
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe: Object type system
        /// Original implementation: Returns ObjectType enum value
        /// </remarks>
        protected Variable Func_GetObjectType(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            uint objectId = args.Count > 0 ? args[0].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity != null)
            {
                return Variable.FromInt((int)entity.ObjectType);
            }
            return Variable.FromInt(0);
        }

        #endregion
    }
}

