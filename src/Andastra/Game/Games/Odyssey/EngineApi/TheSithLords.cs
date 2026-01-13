using System;
using System.Collections.Generic;
using System.Numerics;
using BioWare.NET.Common.Script;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Engines.Odyssey.Game;
using Andastra.Runtime.Engines.Odyssey.Loading;
using Andastra.Runtime.Engines.Odyssey.Systems;
using Andastra.Runtime.Scripting.EngineApi;
using Andastra.Runtime.Scripting.Interfaces;
using Andastra.Runtime.Scripting.VM;

namespace Andastra.Game.Engines.Odyssey.EngineApi
{
    /// <summary>
    /// KOTOR 2 (TSL) engine API implementation.
    /// Extends Kotor1 API with TheSithLords-specific functions.
    /// </summary>
    /// <remarks>
    /// KOTOR 2 Engine API (TSL NWScript Functions):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) NWScript engine API implementation
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
    public class TheSithLords : OdysseyBaseEngineApi
    {
        private readonly Kotor1 _kotor1Api;

        public TheSithLords()
        {
            // Create Kotor1 instance to handle shared functions (0-799)
            _kotor1Api = new Kotor1();
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
            // TheSithLords has the same base functions as Kotor1 with additional functions
            // Most functions 0-799 are shared, TheSithLords adds functions 800+
            // Some TSL-specific functions are in the 0-799 range (e.g., GetFeatAcquired = 783)

            // Check for TSL-specific functions in 0-799 range
            if (routineId == 783) // GetFeatAcquired (TSL only, but in shared range)
            {
                return Func_GetFeatAcquired(args, ctx);
            }

            // Check if this is a TheSithLords-specific function (800+)
            if (routineId >= 800)
            {
                return CallTheSithLordsSpecificFunction(routineId, args, ctx);
            }

            // For Kotor1 functions (0-799), delegate to Kotor1 implementation
            return _kotor1Api.CallEngineFunction(routineId, args, ctx);
        }

        /// <summary>
        /// Handles TheSithLords-specific engine functions (routine IDs 800+).
        /// </summary>
        private Variable CallTheSithLordsSpecificFunction(int routineId, IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            switch (routineId)
            {
                // ===== TSL-Specific Functions =====

                // Influence system (TSL only, IDs 800-810)
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
                    Console.WriteLine("[NCS-TheSithLords] Unimplemented function: " + routineId + " (" + funcName + ")");
                    return Variable.Void();
            }
        }

        #region TheSithLords-Specific Functions

        // Influence system (TheSithLords only, IDs 800-810)
        /// <summary>
        /// GetInfluence(int nNPC) - Returns influence value for NPC (0-100)
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
            // Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
            // Original implementation: Reads influence from PARTYTABLE.res GFF structure
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;

            // Get NPC entity from PartyManager
            if (ctx is Andastra.Runtime.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
        /// Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
        /// Original implementation: Writes influence to PARTYTABLE.res GFF structure (PT_NPC_INFLUENCE field)
        /// Influence value clamped to 0-100 range
        /// </remarks>
        private Variable Func_SetInfluence(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // SetInfluence(int nNPC, int nInfluence) - sets influence value for NPC (0-100)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
            // Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
            // Original implementation: Writes influence to PARTYTABLE.res GFF structure
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            int influence = args.Count > 1 ? args[1].AsInt() : 50;

            // Clamp influence to valid range (0-100)
            influence = Math.Max(0, Math.Min(100, influence));

            // Get NPC entity from PartyManager and set influence
            if (ctx is Andastra.Runtime.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
        /// Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
        /// Original implementation: Reads current influence, adds modifier, writes back to PARTYTABLE.res GFF
        /// Influence value clamped to 0-100 range after modification
        /// </remarks>
        private Variable Func_ModifyInfluence(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ModifyInfluence(int nNPC, int nModifier) - modifies influence value for NPC by modifier amount
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Influence system (TSL only)
            // Located via string references: "PT_INFLUENCE" @ 0x007c1788, "PT_NPC_INFLUENCE" @ 0x007c1774
            // Original implementation: Reads current influence, adds modifier, writes back to GFF
            int npcIndex = args.Count > 0 ? args[0].AsInt() : 0;
            int modifier = args.Count > 1 ? args[1].AsInt() : 0;

            // Get NPC entity from PartyManager and modify influence
            if (ctx is Andastra.Runtime.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
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
            if (ctx is Andastra.Runtime.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetMemberAtSlot(index);
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
            if (ctx is Andastra.Runtime.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
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
            if (ctx is Andastra.Runtime.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager && services.ModuleLoader is Andastra.Runtime.Engines.Odyssey.Game.ModuleLoader moduleLoader)
            {
                // Try to find existing entity by template tag first
                IEntity existingEntity = ctx.World.GetEntityByTag(template, 0);
                if (existingEntity != null)
                {
                    try
                    {
                        partyManager.AddAvailableMember(npcIndex, existingEntity);
                        return Variable.FromInt(1); // Success
                    }
                    catch
                    {
                        return Variable.FromInt(0); // Failed
                    }
                }

                // Create entity from template using EntityFactory
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): AddAvailableNPCByTemplate implementation
                // Located via string references: "TemplateResRef" @ 0x007bd00c, entity creation from UTC templates
                // Original implementation: Creates creature from UTC template and adds to available party members
                // Ghidra analysis: FUN_0057bd70 @ 0x0057bd70 saves party data, FUN_0057dcd0 @ 0x0057dcd0 loads party data
                // EntityFactory accessed via ModuleLoader.EntityFactory property
                // Get current module from ModuleLoader
                BioWare.NET.Extract.Installation.Module module = moduleLoader.GetCurrentModule();
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

                    // Create creature from template using EntityFactory
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EntityFactory.CreateCreatureFromTemplate creates creature from UTC template
                    // Located via string references: "TemplateResRef" @ 0x007bd00c, "Creature template '%s' doesn't exist.\n" @ 0x007bf78c
                    // Original implementation: Loads UTC GFF template, creates entity with template data, registers in world
                    Loading.EntityFactory entityFactory = moduleLoader.EntityFactory;
                    if (entityFactory != null)
                    {
                        IEntity newEntity = entityFactory.CreateCreatureFromTemplate(module, template, spawnPosition, spawnFacing);
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
                                partyManager.AddAvailableMember(npcIndex, newEntity);
                                return Variable.FromInt(1); // Success
                            }
                            catch
                            {
                                return Variable.FromInt(0); // Failed
                            }
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
            if (ctx is Andastra.Runtime.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
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
            if (ctx is Andastra.Runtime.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PartyManager is PartyManager partyManager)
            {
                IEntity member = partyManager.GetAvailableMember(npcIndex);
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Stealth system (TSL only)
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Stealth system (TSL only)
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

        /// <summary>
        /// GetStealthXPEnabled() - Returns TRUE if stealth XP is enabled for the current area
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GetStealthXPEnabled @ routine ID 836
        /// Located via string references: "StealthXPEnabled" @ 0x007bd1b4
        /// Ghidra analysis: FUN_004e26d0 @ 0x004e26d0 reads "StealthXPEnabled" from AreaProperties GFF structure
        /// Stored at object offset +0x2f4 as byte (boolean) in area properties
        /// Original implementation: Reads boolean from GFF field "StealthXPEnabled" in AreaProperties
        /// </remarks>
        private Variable Func_GetStealthXPEnabled(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // Get stealth XP enabled state from current area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_004e26d0 @ 0x004e26d0 reads StealthXPEnabled from AreaProperties GFF
            // Located via string references: "StealthXPEnabled" @ 0x007bd1b4
            // Original implementation: Reads boolean from AreaProperties GFF structure at offset +0x2f4
            if (ctx.World != null && ctx.World.CurrentArea != null)
            {
                return Variable.FromInt(ctx.World.CurrentArea.StealthXPEnabled ? 1 : 0);
            }
            return Variable.FromInt(1); // Default: enabled
        }

        /// <summary>
        /// SetStealthXPEnabled(int nEnabled) - Sets whether stealth XP is enabled for the current area
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): SetStealthXPEnabled @ routine ID 837
        /// Located via string references: "StealthXPEnabled" @ 0x007bd1b4
        /// Ghidra analysis: FUN_004e11d0 @ 0x004e11d0 writes "StealthXPEnabled" to AreaProperties GFF structure
        /// Stored at object offset +0x2f4 as byte (boolean) in area properties
        /// Original implementation: Writes boolean to GFF field "StealthXPEnabled" in AreaProperties
        /// </remarks>
        private Variable Func_SetStealthXPEnabled(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            int enabled = args.Count > 0 ? args[0].AsInt() : 1;

            // Set stealth XP enabled state in current area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_004e11d0 @ 0x004e11d0 writes StealthXPEnabled to AreaProperties GFF
            // Located via string references: "StealthXPEnabled" @ 0x007bd1b4
            // Original implementation: Writes boolean to AreaProperties GFF structure at offset +0x2f4
            if (ctx.World != null && ctx.World.CurrentArea != null)
            {
                ctx.World.CurrentArea.StealthXPEnabled = enabled != 0;
            }
            return Variable.Void();
        }

        // Workbench functions (TSL only)
        /// <summary>
        /// ShowUpgradeScreen(object oItem, object oCharacter, int nDisableItemCreation, int nDisableUpgrade, string sOverride2DA) - Displays the upgrade screen where the player can modify weapons and armor
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ShowUpgradeScreen @ 0x00680cb0 (routine ID 850)
        /// Located via string references: "upgradeitems_p" @ 0x007d09e4, "BTN_UPGRADEITEM" @ 0x007d09d4
        /// "BTN_UPGRADEITEMS" @ 0x007d0b58, "BTN_CREATEITEMS" @ 0x007d0b48, "upgradesel_p" (upgrade selection screen)
        /// Original implementation:
        /// - Validates item exists if oItem != OBJECT_INVALID (0x7f000000) via object lookup
        /// - Creates upgrade selection screen GUI ("upgradesel_p") with item type filters (All, Lightsaber, Ranged, Melee, Armor)
        /// - Creates upgrade items screen GUI ("upgradeitems_p") with item list, description, and upgrade buttons
        /// - Sets item ID in GUI object (offset 0x629)
        /// - Sets character ID in GUI object (offset 0x18a8) - character skills extracted and used for item creation/upgrading
        /// - Sets disableItemCreation flag (offset 0x18c8: 0 = false, 1 = true)
        /// - Sets disableUpgrade flag (offset 0x18cc: 0 = false, 1 = true)
        /// - Sets override2DA string (if provided, otherwise empty string)
        /// - Initializes screen via FUN_0067c8f0
        /// - Shows screen via GUI manager (FUN_0040bf90 adds to GUI manager, FUN_00638bb0 sets screen mode)
        /// - If oItem is NOT invalid, then the player will be forced to upgrade oItem and only oItem
        /// - If oCharacter is NOT invalid, then that character's various skills will be used for:
        ///   - Upgrade availability checks (skill requirements)
        ///   - Item creation success rates (higher skills = better success)
        ///   - Upgrade application skill checks (skill requirements for applying upgrades)
        ///   [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Character skills stored in IStatsComponent, accessed via GetSkillRank()
        ///   Skills extracted when character is set via BaseUpgradeScreen.ExtractCharacterSkills()
        ///   Original implementation: Character skills were NOT IMPLEMENTED in original ShowUpgradeScreen
        /// - If nDisableItemCreation = TRUE, then the player will not be able to access the item creation screen
        /// - If nDisableUpgrade = TRUE, then the player will be forced straight to item creation and not be able to access Item Upgrading
        /// - sOverride2DA: Override 2DA file name (empty string for default upgradeitems.2da)
        /// GUI Elements:
        /// - Upgrade Selection Screen ("upgradesel_p"): BTN_ALL, BTN_LIGHTSABER, BTN_RANGED, BTN_MELEE, BTN_ARMOR, LB_UPGRADELIST, LB_DESCRIPTION, BTN_BACK, BTN_UPGRADEITEMS, BTN_CREATEITEMS
        /// - Upgrade Items Screen ("upgradeitems_p"): LBL_TITLE, LB_ITEMS, LB_DESCRIPTION, BTN_UPGRADEITEM, BTN_BACK
        /// </remarks>
        private Variable Func_ShowUpgradeScreen(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            // ShowUpgradeScreen(object oItem = OBJECT_INVALID, object oCharacter = OBJECT_INVALID,
            //                   int nDisableItemCreation = FALSE, int nDisableUpgrade = FALSE, string sOverride2DA = "")
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ShowUpgradeScreen @ 0x00680cb0
            uint item = args.Count > 0 ? args[0].AsObjectId() : ObjectInvalid;
            uint character = args.Count > 1 ? args[1].AsObjectId() : ObjectInvalid;
            int disableItemCreation = args.Count > 2 ? args[2].AsInt() : 0;
            int disableUpgrade = args.Count > 3 ? args[3].AsInt() : 0;
            string override2DA = args.Count > 4 ? args[4].AsString() : string.Empty;

            // Validate item exists if specified (original checks via FUN_004dc020 if item != OBJECT_INVALID)
            if (item != ObjectInvalid && ctx.World != null)
            {
                IEntity itemEntity = ResolveObject(item, ctx);
                if (itemEntity == null)
                {
                    // Item not found - original returns early if validation fails
                    return Variable.Void();
                }
            }

            // Get UI system from game services context
            if (ctx is Andastra.Runtime.Scripting.VM.ExecutionContext execCtx &&
                execCtx.AdditionalContext is IGameServicesContext services &&
                services.UISystem != null)
            {
                // Show upgrade screen via UI system
                // Original creates upgrade selection screen ("upgradesel_p") and upgrade items screen ("upgradeitems_p")
                // Sets flags: disableItemCreation, disableUpgrade, override2DA
                // Shows screen via GUI manager
                services.UISystem.ShowUpgradeScreen(item, character, disableItemCreation != 0, disableUpgrade != 0, override2DA);
            }
            else
            {
                // UI system not available - log warning
                Console.WriteLine("[TheSithLords] ShowUpgradeScreen: UI system not available");
            }

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
            if (ctx is Andastra.Runtime.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PlayerEntity != null)
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
            if (ctx is Andastra.Runtime.Scripting.VM.ExecutionContext execCtx && execCtx.AdditionalContext is IGameServicesContext services && services.PlayerEntity != null)
            {
                // Store invincibility state in player entity data
                services.PlayerEntity.SetData("SwoopMinigameInvincible", invincible != 0);
            }

            return Variable.Void();
        }

        /// <summary>
        /// GetFeatAcquired(int nFeat, object oCreature=OBJECT_SELF) - Returns whether creature has access to a feat, even if unusable
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GetFeatAcquired function (TSL only, function ID 783)
        /// Located via string references: Feat acquisition checking (separate from usability)
        /// Original implementation: Checks if creature has the feat in their feat list, regardless of daily limits or restrictions
        /// Returns TRUE if creature has the feat (even if exhausted or restricted)
        /// Returns FALSE if creature doesn't have the feat
        /// Difference from GetHasFeat: GetHasFeat checks usability (daily limits, restrictions), GetFeatAcquired only checks acquisition
        /// </remarks>
        private Variable Func_GetFeatAcquired(IReadOnlyList<Variable> args, IExecutionContext ctx)
        {
            if (args.Count < 1)
            {
                return Variable.FromInt(0);
            }

            int featId = args[0].AsInt();
            uint objectId = args.Count > 1 ? args[1].AsObjectId() : ObjectSelf;
            IEntity entity = ResolveObject(objectId, ctx);
            if (entity == null || entity.ObjectType != Core.Enums.ObjectType.Creature)
            {
                return Variable.FromInt(0);
            }

            Components.CreatureComponent creature = entity.GetComponent<Components.CreatureComponent>();
            if (creature == null || creature.FeatList == null)
            {
                return Variable.FromInt(0);
            }

            // GetFeatAcquired only checks if creature has the feat, not if it's usable
            // This is different from GetHasFeat which also checks daily limits and restrictions
            bool hasFeat = creature.HasFeat(featId);
            return Variable.FromInt(hasFeat ? 1 : 0);
        }

        #endregion
    }
}
