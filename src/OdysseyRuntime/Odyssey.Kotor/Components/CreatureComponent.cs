using System.Collections.Generic;
using BioWareEngines.Core.Interfaces;

namespace BioWareEngines.Kotor.Components
{
    /// <summary>
    /// Component for creature entities (NPCs and PCs).
    /// </summary>
    /// <remarks>
    /// Creature Component:
    /// - Based on swkotor2.exe creature system
    /// - Located via string references: "Creature" @ 0x007bc544, "Creature List" @ 0x007bd01c
    /// - "CreatureSize" @ 0x007bf680, "CreatureSpeed" @ 0x007c4b8c
    /// - "CreatureList" @ 0x007c0c80, "RecCreatures" @ 0x007c0cb4, "MaxCreatures" @ 0x007c0cc4 (encounter creature lists)
    /// - "GetCreatureRadius" @ 0x007bb128 (creature collision radius calculation)
    /// - Script events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE" @ 0x007bc5ec
    /// - "EVENT_SUMMON_CREATURE" @ 0x007bcc08 (creature summoning event)
    /// - Error messages:
    ///   - "Creature template '%s' doesn't exist.\n" @ 0x007bf78c
    ///   - "Cannot set creature %s to faction %d because faction does not exist! Setting to Hostile1." @ 0x007bf2a8
    ///   - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc
    ///   - "CSWCCreatureAppearance::CreateBTypeBody(): Failed to load model '%s'." @ 0x007cdc40
    ///   - "Tried to reduce XP of creature '%s' to '%d'. Cannot reduce XP." @ 0x007c3fa8
    /// - Pathfinding errors:
    ///   - "    failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
    ///   - "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0
    ///   - "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 (save creature data to GFF)
    /// - FUN_004dfbb0 @ 0x004dfbb0 (load creature instances from GIT)
    /// - FUN_005261b0 @ 0x005261b0 (load creature from UTC template)
    /// - FUN_0050c510 @ 0x0050c510 (load creature script hooks from GFF)
    ///   - Original implementation (from decompiled FUN_0050c510):
    ///     - Function signature: `void FUN_0050c510(void *this, void *param_1, uint *param_2)`
    ///     - param_1: GFF structure pointer
    ///     - param_2: GFF field pointer
    ///     - Reads script ResRef fields from GFF and stores at offsets in creature object:
    ///       - "ScriptHeartbeat" @ this + 0x270 (OnHeartbeat script)
    ///       - "ScriptOnNotice" @ this + 0x278 (OnPerception script)
    ///       - "ScriptSpellAt" @ this + 0x280 (OnSpellCastAt script)
    ///       - "ScriptAttacked" @ this + 0x288 (OnAttacked script)
    ///       - "ScriptDamaged" @ this + 0x290 (OnDamaged script)
    ///       - "ScriptDisturbed" @ this + 0x298 (OnDisturbed script)
    ///       - "ScriptEndRound" @ this + 0x2a0 (OnEndRound script)
    ///       - "ScriptDialogue" @ this + 0x2a8 (OnDialogue script)
    ///       - "ScriptSpawn" @ this + 0x2b0 (OnSpawn script)
    ///       - "ScriptRested" @ this + 0x2b8 (OnRested script)
    ///       - "ScriptDeath" @ this + 0x2c0 (OnDeath script)
    ///       - "ScriptUserDefine" @ this + 0x2c8 (OnUserDefined script)
    ///       - "ScriptOnBlocked" @ this + 0x2d0 (OnBlocked script)
    ///       - "ScriptEndDialogue" @ this + 0x2d8 (OnEndDialogue script)
    ///     - Uses FUN_00412f30 to read GFF string fields, FUN_00630c50 to store strings
    ///     - Script hooks stored as ResRef strings (16 bytes, null-terminated)
    /// - Creatures have appearance, stats, equipment, classes, feats, force powers
    /// - Based on UTC file format (GFF with "UTC " signature)
    /// - Script events: OnHeartbeat, OnPerception, OnAttacked, OnDamaged, OnDeath, etc.
    /// - Equip_ItemList stores equipped items, ItemList stores inventory, PerceptionList stores perception data
    /// - CombatRoundData stores combat state (FUN_00529470 saves combat round data)
    /// </remarks>
    public class CreatureComponent : IComponent
    {
        public IEntity Owner { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }

        public CreatureComponent()
        {
            TemplateResRef = string.Empty;
            Tag = string.Empty;
            Conversation = string.Empty;
            FeatList = new List<int>();
            ClassList = new List<CreatureClass>();
            EquippedItems = new Dictionary<int, string>();
            KnownPowers = new List<int>();
        }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Creature tag.
        /// </summary>
        public string Tag { get; set; }

        #region Appearance

        /// <summary>
        /// Racial type ID (index into racialtypes.2da).
        /// </summary>
        public int RaceId { get; set; }

        /// <summary>
        /// Appearance type (index into appearance.2da).
        /// </summary>
        public int AppearanceType { get; set; }

        /// <summary>
        /// Body variation index.
        /// </summary>
        public int BodyVariation { get; set; }

        /// <summary>
        /// Texture variation index.
        /// </summary>
        public int TextureVar { get; set; }

        /// <summary>
        /// Portrait ID.
        /// </summary>
        public int PortraitId { get; set; }

        #endregion

        #region Vital Statistics

        /// <summary>
        /// Current hit points.
        /// </summary>
        public int CurrentHP { get; set; }

        /// <summary>
        /// Maximum hit points.
        /// </summary>
        public int MaxHP { get; set; }

        /// <summary>
        /// Current force points.
        /// </summary>
        public int CurrentForce { get; set; }

        /// <summary>
        /// Maximum force points.
        /// </summary>
        public int MaxForce { get; set; }

        /// <summary>
        /// Walk speed multiplier.
        /// </summary>
        public float WalkRate { get; set; }

        /// <summary>
        /// Natural armor class bonus.
        /// </summary>
        public int NaturalAC { get; set; }

        #endregion

        #region Attributes

        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }

        /// <summary>
        /// Gets the attribute modifier for an ability score.
        /// </summary>
        public int GetModifier(int abilityScore)
        {
            return (abilityScore - 10) / 2;
        }

        #endregion

        #region Combat

        /// <summary>
        /// Faction ID (for hostility checks).
        /// </summary>
        public int FactionId { get; set; }

        /// <summary>
        /// Conversation file (dialogue ResRef).
        /// Stored in UTC template as "ScriptDialogue" field.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: FUN_0050c510 @ 0x0050c510 loads creature data including ScriptDialogue field
        /// Located via string reference: "ScriptDialogue" @ 0x007bee40, "Conversation" @ 0x007c1abc
        /// </remarks>
        public string Conversation { get; set; }

        /// <summary>
        /// Perception range for sight.
        /// </summary>
        public float PerceptionRange { get; set; }

        /// <summary>
        /// Challenge rating.
        /// </summary>
        public float ChallengeRating { get; set; }

        /// <summary>
        /// Whether creature is immortal.
        /// </summary>
        public bool IsImmortal { get; set; }

        /// <summary>
        /// Whether creature has no death script.
        /// </summary>
        public bool NoPermDeath { get; set; }

        /// <summary>
        /// Whether creature is disarmable.
        /// </summary>
        public bool Disarmable { get; set; }

        /// <summary>
        /// Whether creature is interruptable.
        /// </summary>
        public bool Interruptable { get; set; }

        #endregion

        #region Classes and Levels

        /// <summary>
        /// List of class levels.
        /// </summary>
        public List<CreatureClass> ClassList { get; set; }

        /// <summary>
        /// Gets total character level.
        /// </summary>
        public int GetTotalLevel()
        {
            int total = 0;
            foreach (CreatureClass cls in ClassList)
            {
                total += cls.Level;
            }
            return total;
        }

        /// <summary>
        /// Gets base attack bonus.
        /// </summary>
        public int GetBaseAttackBonus()
        {
            int bab = 0;
            foreach (CreatureClass cls in ClassList)
            {
                // Simplified BAB calculation based on class type
                // Full implementation would use classes.2da
                bab += cls.Level;
            }
            return bab;
        }

        #endregion

        #region Feats and Powers

        /// <summary>
        /// List of feat IDs.
        /// </summary>
        public List<int> FeatList { get; set; }

        /// <summary>
        /// List of known force powers.
        /// </summary>
        public List<int> KnownPowers { get; set; }

        /// <summary>
        /// Checks if creature has a feat.
        /// </summary>
        public bool HasFeat(int featId)
        {
            return FeatList.Contains(featId);
        }

        #endregion

        #region Equipment

        /// <summary>
        /// Equipped items by slot.
        /// </summary>
        public Dictionary<int, string> EquippedItems { get; set; }

        #endregion

        #region AI Behavior

        /// <summary>
        /// Whether creature is currently in combat.
        /// </summary>
        public bool IsInCombat { get; set; }

        /// <summary>
        /// Time since last heartbeat.
        /// </summary>
        public float TimeSinceHeartbeat { get; set; }

        /// <summary>
        /// Combat round state.
        /// </summary>
        public CombatRoundState CombatState { get; set; }

        #endregion
    }

    /// <summary>
    /// Creature class information.
    /// </summary>
    public class CreatureClass
    {
        /// <summary>
        /// Class ID (index into classes.2da).
        /// </summary>
        public int ClassId { get; set; }

        /// <summary>
        /// Level in this class.
        /// </summary>
        public int Level { get; set; }
    }

    /// <summary>
    /// Combat round state for creatures.
    /// </summary>
    public enum CombatRoundState
    {
        NotInCombat,
        Starting,
        FirstAttack,
        SecondAttack,
        Cooldown,
        Finished
    }
}
