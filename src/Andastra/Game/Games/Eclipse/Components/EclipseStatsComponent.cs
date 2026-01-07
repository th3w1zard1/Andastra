using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Games.Eclipse.Components
{
    /// <summary>
    /// Eclipse Engine (Dragon Age Origins, Dragon Age 2) specific stats component implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Stats Component (daorigins.exe, DragonAge2.exe):
    /// - Based on Eclipse engine stats system from daorigins.exe and DragonAge2.exe
    /// - Located via string references: "CurrentHealth" @ 0x00aedb28 (daorigins.exe), "MaxHealth" @ 0x00aedb1c (daorigins.exe)
    /// - "CurrentStamina" @ 0x00aedb0c (daorigins.exe), "MaxStamina" @ 0x00aedb00 (daorigins.exe)
    /// - "Attributes" @ 0x00af78c8 (daorigins.exe) - Attributes system (maps to Ability enum)
    /// - "FloatyLayer.BatchUpdateStatsCurrentHealths" @ 0x00aed6c0 (daorigins.exe) - Health update system
    /// - "FloatyLayer.BatchUpdateStatsTotalHealths" @ 0x00aed694 (daorigins.exe) - Max health update system
    /// - Original implementation: Eclipse engines use Health/Stamina instead of HP/FP terminology
    /// - Health maps to HP (CurrentHealth -> CurrentHP, MaxHealth -> MaxHP)
    /// - Stamina maps to FP (CurrentStamina -> CurrentFP, MaxStamina -> MaxFP) for interface compatibility
    /// - Attributes system: Uses "Attributes" terminology internally but maps to D20 Ability enum (STR, DEX, CON, INT, WIS, CHA)
    /// - Dragon Age Origins: Uses D&D 3rd Edition style attributes with modifiers = (score - 10) / 2
    /// - Dragon Age 2: Similar attribute system with some calculation differences
    /// - Skills: Dragon Age has different skill systems than KOTOR/NWN (varies by game)
    /// - Defense: Dragon Age uses different defense calculation than AC (Defense = base + modifiers)
    /// - No Force Points: Eclipse uses Stamina instead (mapped to FP for interface compatibility)
    /// - CurrentFP and MaxFP map to CurrentStamina and MaxStamina internally
    /// 
    /// Key differences from Odyssey/Aurora:
    /// - Health/Stamina terminology instead of HP/FP
    /// - Different skill systems (Dragon Age has different skills than KOTOR/NWN)
    /// - Different defense calculation (not AC-based, uses Defense stat)
    /// - Attributes terminology but same D20 ability system
    /// - No Force Points (uses Stamina instead, mapped to FP for compatibility)
    /// 
    /// Cross-engine comparison:
    /// - Odyssey (swkotor.exe, swkotor2.exe): HP/FP, 8 skills, AC-based defense
    /// - Aurora (nwmain.exe, nwn2main.exe): HP only (no FP), 27 skills, AC-based defense with size modifiers
    /// - Eclipse (daorigins.exe, DragonAge2.exe): Health/Stamina, different skills, Defense-based system
    /// </remarks>
    public class EclipseStatsComponent : IStatsComponent
    {
        private readonly Dictionary<Ability, int> _attributes; // Eclipse uses "Attributes" terminology
        private readonly Dictionary<int, int> _skills; // Skill ID -> Skill Rank (Dragon Age has different skills)
        private readonly HashSet<int> _knownSpells; // Spell/Ability ID -> Known (for talents/abilities)
        private readonly Dictionary<int, int> _spellLevels; // Spell/Ability ID -> Level/Rank (tracks level at which creature knows each spell)
        private int _currentHealth; // Eclipse uses "Health" instead of "HP"
        private int _maxHealth; // Eclipse uses "MaxHealth" instead of "MaxHP"
        private int _baseLevel;
        private int _baseAttackBonus;
        private int _baseFortitude;
        private int _baseReflex;
        private int _baseWill;
        private int _armorBonus;
        private int _naturalArmor;
        private int _deflectionBonus;
        private int _effectACBonus; // AC bonus from effects
        private int _effectAttackBonus; // Attack bonus from effects
        private int _currentStamina; // Eclipse uses "Stamina" instead of "FP"
        private int _maxStamina; // Eclipse uses "MaxStamina" instead of "MaxFP"
        private int _defense; // Eclipse uses Defense instead of AC (different calculation)

        public EclipseStatsComponent()
        {
            _attributes = new Dictionary<Ability, int>();
            _skills = new Dictionary<int, int>();
            _knownSpells = new HashSet<int>();
            _spellLevels = new Dictionary<int, int>();

            // Default attribute scores (10 = average human, D&D standard)
            // Eclipse uses "Attributes" terminology but same D20 system
            foreach (Ability ability in Enum.GetValues(typeof(Ability)))
            {
                _attributes[ability] = 10;
            }

            // Initialize Dragon Age skills to 0 (untrained)
            // Dragon Age Origins: Different skill set than KOTOR/NWN
            // Skills vary by game, but we support up to 255 for extensibility
            // Typical Dragon Age skills: Combat, Magic, Stealth, etc. (varies by game)
            for (int i = 0; i < 20; i++) // Dragon Age typically has fewer skills than NWN
            {
                _skills[i] = 0;
            }

            _currentHealth = 10;
            _maxHealth = 10;
            _currentStamina = 0;
            _maxStamina = 0;
            _baseLevel = 1;
            _baseAttackBonus = 0;
            _baseFortitude = 0;
            _baseReflex = 0;
            _baseWill = 0;
            _armorBonus = 0;
            _naturalArmor = 0;
            _deflectionBonus = 0;
            _effectACBonus = 0;
            _effectAttackBonus = 0;
            _defense = 10; // Default defense (Eclipse uses Defense instead of AC)

            // Default movement speeds (from Dragon Age appearance data averages)
            _baseWalkSpeed = 2.0f;
            _baseRunSpeed = 4.5f;
        }

        #region IComponent Implementation

        public IEntity Owner { get; set; }

        public void OnAttach()
        {
            // Initialize from entity data if available
            if (Owner != null)
            {
                // Try to load stats from entity's stored data
                LoadFromEntityData();
            }
        }

        public void OnDetach()
        {
            // Save stats back to entity data if needed
        }

        #endregion

        #region IStatsComponent Implementation

        /// <summary>
        /// Current hit points (maps to CurrentHealth in Eclipse).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: "CurrentHealth" @ 0x00aedb28
        /// Eclipse uses "Health" terminology but maps to HP for interface compatibility.
        /// </remarks>
        public int CurrentHP
        {
            get { return _currentHealth; }
            set { _currentHealth = Math.Max(0, Math.Min(value, MaxHP)); }
        }

        /// <summary>
        /// Maximum hit points (maps to MaxHealth in Eclipse).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: "MaxHealth" @ 0x00aedb1c
        /// Eclipse uses "MaxHealth" terminology but maps to MaxHP for interface compatibility.
        /// </remarks>
        public int MaxHP
        {
            get { return _maxHealth; }
            set { _maxHealth = Math.Max(1, value); }
        }

        /// <summary>
        /// Current Force points (maps to CurrentStamina in Eclipse).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: "CurrentStamina" @ 0x00aedb0c
        /// Eclipse uses "Stamina" instead of Force Points, but maps to FP for interface compatibility.
        /// </remarks>
        public int CurrentFP
        {
            get { return _currentStamina; }
            set { _currentStamina = Math.Max(0, Math.Min(value, MaxFP)); }
        }

        /// <summary>
        /// Maximum Force points (maps to MaxStamina in Eclipse).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: "MaxStamina" @ 0x00aedb00
        /// Eclipse uses "MaxStamina" instead of MaxFP, but maps to MaxFP for interface compatibility.
        /// </remarks>
        public int MaxFP
        {
            get { return _maxStamina; }
            set { _maxStamina = Math.Max(0, value); }
        }

        /// <summary>
        /// Gets an ability score (Eclipse uses "Attributes" terminology).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: "Attributes" @ 0x00af78c8
        /// Eclipse uses "Attributes" terminology but maps to D20 Ability enum for compatibility.
        /// </remarks>
        public int GetAbility(Ability ability)
        {
            int value;
            if (_attributes.TryGetValue(ability, out value))
            {
                return value;
            }
            return 10; // Default attribute score
        }

        /// <summary>
        /// Sets an ability score (Eclipse uses "Attributes" terminology).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: Attributes system
        /// Eclipse uses "Attributes" terminology but maps to D20 Ability enum for compatibility.
        /// </remarks>
        public void SetAbility(Ability ability, int value)
        {
            _attributes[ability] = Math.Max(1, Math.Min(100, value)); // D&D ability range is typically 1-100
        }

        /// <summary>
        /// Gets the modifier for an ability (D20 formula).
        /// </summary>
        /// <remarks>
        /// D20 formula: (score - 10) / 2, rounded down
        /// Same as Odyssey/Aurora - Eclipse uses same D20 attribute modifier calculation.
        /// </remarks>
        public int GetAbilityModifier(Ability ability)
        {
            // D20 formula: (score - 10) / 2, rounded down
            int score = GetAbility(ability);
            return (score - 10) / 2;
        }

        /// <summary>
        /// Whether the creature is dead.
        /// </summary>
        public bool IsDead
        {
            get { return _currentHealth <= 0; }
        }

        /// <summary>
        /// Base attack bonus.
        /// </summary>
        /// <remarks>
        /// BAB + STR modifier for melee (or DEX for ranged/finesse) + effect bonuses
        /// Eclipse uses same BAB calculation as Odyssey/Aurora.
        /// </remarks>
        public int BaseAttackBonus
        {
            get
            {
                // BAB + STR modifier for melee (or DEX for ranged/finesse) + effect bonuses
                // Effect bonuses are tracked via AddEffectAttackBonus/RemoveEffectAttackBonus called by EffectSystem
                return _baseAttackBonus + GetAbilityModifier(Ability.Strength) + _effectAttackBonus;
            }
        }

        /// <summary>
        /// Armor class / defense (Eclipse uses Defense instead of AC).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: Eclipse uses Defense instead of AC
        /// Defense = base + DEX mod + Armor + Natural + Deflection + Effect bonuses
        /// Eclipse defense calculation is similar to AC but uses "Defense" terminology.
        /// </remarks>
        public int ArmorClass
        {
            get
            {
                // Defense = base + DEX mod + Armor + Natural + Deflection + Effect bonuses
                // Eclipse uses Defense instead of AC, but calculation is similar
                // Effect bonuses are tracked via AddEffectACBonus/RemoveEffectACBonus called by EffectSystem

                // Use Defense if set, otherwise calculate from components
                if (_defense > 10)
                {
                    return _defense + _effectACBonus;
                }

                // Calculate Defense similar to AC
                return 10
                    + GetAbilityModifier(Ability.Dexterity)
                    + _armorBonus
                    + _naturalArmor
                    + _deflectionBonus
                    + _effectACBonus;
            }
        }

        /// <summary>
        /// Fortitude save.
        /// </summary>
        public int FortitudeSave
        {
            get { return _baseFortitude + GetAbilityModifier(Ability.Constitution); }
        }

        /// <summary>
        /// Reflex save.
        /// </summary>
        public int ReflexSave
        {
            get { return _baseReflex + GetAbilityModifier(Ability.Dexterity); }
        }

        /// <summary>
        /// Will save.
        /// </summary>
        public int WillSave
        {
            get { return _baseWill + GetAbilityModifier(Ability.Wisdom); }
        }

        private float _baseWalkSpeed;
        private float _baseRunSpeed;

        /// <summary>
        /// Walk speed in meters per second.
        /// </summary>
        public float WalkSpeed
        {
            get
            {
                return CalculateMovementSpeed(_baseWalkSpeed);
            }
            set
            {
                _baseWalkSpeed = value;
            }
        }

        /// <summary>
        /// Run speed in meters per second.
        /// </summary>
        public float RunSpeed
        {
            get
            {
                return CalculateMovementSpeed(_baseRunSpeed);
            }
            set
            {
                _baseRunSpeed = value;
            }
        }

        /// <summary>
        /// Gets the skill rank for a given skill.
        /// </summary>
        /// <remarks>
        /// Dragon Age has different skills than KOTOR/NWN.
        /// Returns skill rank, or 0 if untrained, or -1 if skill doesn't exist.
        /// </remarks>
        public int GetSkillRank(int skill)
        {
            // Returns skill rank, or 0 if untrained, or -1 if skill doesn't exist
            // Dragon Age supports different skills than KOTOR/NWN, but we allow up to 255 for extensibility
            if (skill < 0 || skill > 255)
            {
                return -1; // Invalid skill ID
            }

            int rank;
            if (_skills.TryGetValue(skill, out rank))
            {
                return rank;
            }

            return 0; // Untrained (default)
        }

        /// <summary>
        /// Checks if the creature knows a spell/ability/talent.
        /// </summary>
        /// <remarks>
        /// Eclipse uses talents/abilities system instead of spells.
        /// This maps to known talents/abilities for compatibility.
        /// </remarks>
        public bool HasSpell(int spellId)
        {
            // Eclipse uses talents/abilities instead of spells
            // This maps to known talents/abilities for compatibility
            return _knownSpells.Contains(spellId);
        }

        /// <summary>
        /// Character level (total class levels).
        /// </summary>
        public int Level
        {
            get { return _baseLevel; }
            set { _baseLevel = Math.Max(1, value); }
        }

        #endregion

        #region Extended Properties (Eclipse-specific)

        /// <summary>
        /// Defense value (Eclipse-specific, used instead of AC).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: Eclipse uses Defense instead of AC
        /// Defense is similar to AC but uses different terminology and calculation.
        /// </remarks>
        public int Defense
        {
            get { return _defense; }
            set { _defense = Math.Max(0, value); }
        }

        /// <summary>
        /// Current Health (Eclipse terminology).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: "CurrentHealth" @ 0x00aedb28
        /// Maps to CurrentHP for interface compatibility.
        /// </remarks>
        public int CurrentHealth
        {
            get { return _currentHealth; }
            set { CurrentHP = value; }
        }

        /// <summary>
        /// Maximum Health (Eclipse terminology).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: "MaxHealth" @ 0x00aedb1c
        /// Maps to MaxHP for interface compatibility.
        /// </remarks>
        public int MaxHealth
        {
            get { return _maxHealth; }
            set { MaxHP = value; }
        }

        /// <summary>
        /// Current Stamina (Eclipse terminology).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: "CurrentStamina" @ 0x00aedb0c
        /// Maps to CurrentFP for interface compatibility.
        /// </remarks>
        public int CurrentStamina
        {
            get { return _currentStamina; }
            set { CurrentFP = value; }
        }

        /// <summary>
        /// Maximum Stamina (Eclipse terminology).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe: "MaxStamina" @ 0x00aedb00
        /// Maps to MaxFP for interface compatibility.
        /// </remarks>
        public int MaxStamina
        {
            get { return _maxStamina; }
            set { MaxFP = value; }
        }

        /// <summary>
        /// Armor bonus from equipped armor.
        /// </summary>
        public int ArmorBonus
        {
            get { return _armorBonus; }
            set { _armorBonus = Math.Max(0, value); }
        }

        /// <summary>
        /// Natural armor bonus.
        /// </summary>
        public int NaturalArmor
        {
            get { return _naturalArmor; }
            set { _naturalArmor = Math.Max(0, value); }
        }

        /// <summary>
        /// Deflection bonus (from shields, effects).
        /// </summary>
        public int DeflectionBonus
        {
            get { return _deflectionBonus; }
            set { _deflectionBonus = Math.Max(0, value); }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets the skill rank for a given skill.
        /// </summary>
        public void SetSkillRank(int skill, int rank)
        {
            if (skill >= 0 && skill <= 255)
            {
                _skills[skill] = Math.Max(0, rank);
            }
        }

        /// <summary>
        /// Adds a spell/talent/ability to the creature's known list.
        /// </summary>
        /// <param name="spellId">Spell/talent/ability ID</param>
        /// <param name="level">Optional spell level/rank (defaults to 1 if not specified)</param>
        public void AddSpell(int spellId, int level = 1)
        {
            _knownSpells.Add(spellId);
            _spellLevels[spellId] = Math.Max(1, level);
        }

        /// <summary>
        /// Removes a spell/talent/ability from the creature's known list.
        /// </summary>
        public void RemoveSpell(int spellId)
        {
            _knownSpells.Remove(spellId);
            _spellLevels.Remove(spellId);
        }

        /// <summary>
        /// Gets all known spells/talents/abilities.
        /// </summary>
        public System.Collections.Generic.IEnumerable<int> GetKnownSpells()
        {
            return _knownSpells;
        }

        /// <summary>
        /// Gets the level/rank at which the creature knows a spell/talent/ability.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: GetSpellLevel implementation (daorigins.exe, DragonAge2.exe)
        /// Returns the level/rank at which the creature knows the specified spell.
        /// In Dragon Age, talents/abilities can have ranks/levels that determine their effectiveness.
        /// Returns 0 if the creature doesn't know the spell or if no level is stored.
        /// </remarks>
        /// <param name="spellId">Spell/talent/ability ID</param>
        /// <returns>The spell level/rank, or 0 if the creature doesn't know the spell or level is not set</returns>
        public int GetSpellLevel(int spellId)
        {
            if (!_knownSpells.Contains(spellId))
            {
                return 0;
            }

            int level;
            if (_spellLevels.TryGetValue(spellId, out level))
            {
                return level;
            }

            // If spell is known but no level is stored, return 1 as default (minimum level)
            return 1;
        }

        /// <summary>
        /// Sets the level/rank at which the creature knows a spell/talent/ability.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: Spell level storage (daorigins.exe, DragonAge2.exe)
        /// Sets the level/rank for a spell. If the spell is not yet known, it will be added to the known spells list.
        /// Level must be at least 1.
        /// </remarks>
        /// <param name="spellId">Spell/talent/ability ID</param>
        /// <param name="level">Spell level/rank (must be >= 1)</param>
        public void SetSpellLevel(int spellId, int level)
        {
            if (level < 1)
            {
                level = 1;
            }

            // Ensure spell is in known list
            _knownSpells.Add(spellId);
            _spellLevels[spellId] = level;
        }

        /// <summary>
        /// Sets the maximum HP (maps to MaxHealth).
        /// </summary>
        public void SetMaxHP(int value)
        {
            _maxHealth = Math.Max(1, value);
            if (_currentHealth > _maxHealth)
            {
                _currentHealth = _maxHealth;
            }
        }

        /// <summary>
        /// Sets the base attack bonus.
        /// </summary>
        public void SetBaseAttackBonus(int value)
        {
            _baseAttackBonus = Math.Max(0, value);
        }

        /// <summary>
        /// Sets the base saving throws.
        /// </summary>
        public void SetBaseSaves(int fortitude, int reflex, int will)
        {
            _baseFortitude = fortitude;
            _baseReflex = reflex;
            _baseWill = will;
        }

        /// <summary>
        /// Adds an AC bonus from an effect.
        /// </summary>
        public void AddEffectACBonus(int bonus)
        {
            _effectACBonus += bonus;
        }

        /// <summary>
        /// Removes an AC bonus from an effect.
        /// </summary>
        public void RemoveEffectACBonus(int bonus)
        {
            _effectACBonus -= bonus;
        }

        /// <summary>
        /// Adds an attack bonus from an effect.
        /// </summary>
        public void AddEffectAttackBonus(int bonus)
        {
            _effectAttackBonus += bonus;
        }

        /// <summary>
        /// Removes an attack bonus from an effect.
        /// </summary>
        public void RemoveEffectAttackBonus(int bonus)
        {
            _effectAttackBonus -= bonus;
        }

        /// <summary>
        /// Applies damage to the creature.
        /// </summary>
        /// <param name="damage">Amount of damage</param>
        /// <returns>Actual damage dealt</returns>
        public int TakeDamage(int damage)
        {
            if (damage <= 0)
            {
                return 0;
            }

            int actualDamage = Math.Min(damage, _currentHealth);
            _currentHealth -= actualDamage;
            return actualDamage;
        }

        /// <summary>
        /// Heals the creature.
        /// </summary>
        /// <param name="amount">Amount to heal</param>
        /// <returns>Actual amount healed</returns>
        public int Heal(int amount)
        {
            if (amount <= 0 || IsDead)
            {
                return 0;
            }

            int actualHeal = Math.Min(amount, _maxHealth - _currentHealth);
            _currentHealth += actualHeal;
            return actualHeal;
        }

        /// <summary>
        /// Makes a saving throw.
        /// </summary>
        /// <param name="saveType">Type of save (0=Fort, 1=Ref, 2=Will)</param>
        /// <param name="dc">Difficulty class</param>
        /// <param name="roll">The d20 roll result</param>
        /// <returns>True if save succeeded</returns>
        public bool MakeSavingThrow(int saveType, int dc, int roll)
        {
            int bonus;
            switch (saveType)
            {
                case 0:
                    bonus = FortitudeSave;
                    break;
                case 1:
                    bonus = ReflexSave;
                    break;
                case 2:
                    bonus = WillSave;
                    break;
                default:
                    bonus = 0;
                    break;
            }

            // Natural 20 always succeeds, natural 1 always fails
            if (roll == 20)
            {
                return true;
            }
            if (roll == 1)
            {
                return false;
            }

            return roll + bonus >= dc;
        }

        /// <summary>
        /// Loads stats from entity's stored data.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: Stats are loaded from entity templates
        /// Eclipse uses "CurrentHealth", "MaxHealth", "CurrentStamina", "MaxStamina", "Attributes" terminology
        /// Maps to interface properties (CurrentHP, MaxHP, CurrentFP, MaxFP, Abilities) for compatibility
        /// </remarks>
        private void LoadFromEntityData()
        {
            if (Owner == null)
            {
                return;
            }

            // Load Health from entity data (Eclipse uses "CurrentHealth" and "MaxHealth")
            // Based on daorigins.exe: "CurrentHealth" @ 0x00aedb28, "MaxHealth" @ 0x00aedb1c
            // Map to CurrentHP/MaxHP for interface compatibility
            if (Owner.HasData("CurrentHealth"))
            {
                int currentHealth = Owner.GetData<int>("CurrentHealth", 0);
                if (currentHealth > 0)
                {
                    _currentHealth = currentHealth;
                }
            }
            else if (Owner.HasData("CurrentHP"))
            {
                // Fallback: Also support "CurrentHP" for compatibility
                int currentHP = Owner.GetData<int>("CurrentHP", 0);
                if (currentHP > 0)
                {
                    _currentHealth = currentHP;
                }
            }

            if (Owner.HasData("MaxHealth"))
            {
                int maxHealth = Owner.GetData<int>("MaxHealth", 0);
                if (maxHealth > 0)
                {
                    SetMaxHP(maxHealth);
                }
            }
            else if (Owner.HasData("MaxHP"))
            {
                // Fallback: Also support "MaxHP" for compatibility
                int maxHP = Owner.GetData<int>("MaxHP", 0);
                if (maxHP > 0)
                {
                    SetMaxHP(maxHP);
                }
            }

            // Load Stamina from entity data (Eclipse uses "CurrentStamina" and "MaxStamina")
            // Based on daorigins.exe: "CurrentStamina" @ 0x00aedb0c, "MaxStamina" @ 0x00aedb00
            // Map to CurrentFP/MaxFP for interface compatibility
            if (Owner.HasData("CurrentStamina"))
            {
                int currentStamina = Owner.GetData<int>("CurrentStamina", 0);
                if (currentStamina >= 0)
                {
                    _currentStamina = currentStamina;
                }
            }
            else if (Owner.HasData("CurrentFP"))
            {
                // Fallback: Also support "CurrentFP" for compatibility
                int currentFP = Owner.GetData<int>("CurrentFP", 0);
                if (currentFP >= 0)
                {
                    _currentStamina = currentFP;
                }
            }

            if (Owner.HasData("MaxStamina"))
            {
                int maxStamina = Owner.GetData<int>("MaxStamina", 0);
                if (maxStamina >= 0)
                {
                    _maxStamina = maxStamina;
                }
            }
            else if (Owner.HasData("MaxFP"))
            {
                // Fallback: Also support "MaxFP" for compatibility
                int maxFP = Owner.GetData<int>("MaxFP", 0);
                if (maxFP >= 0)
                {
                    _maxStamina = maxFP;
                }
            }

            // Load Attributes from entity data (Eclipse uses "Attributes" terminology)
            // Based on daorigins.exe: "Attributes" @ 0x00af78c8
            // Map to Abilities for interface compatibility
            if (Owner.HasData("Attributes"))
            {
                // Attributes stored as a struct/dictionary
                object attributesObj = Owner.GetData("Attributes");
                if (attributesObj != null)
                {
                    // Try to get individual attribute values from the struct
                    System.Collections.Generic.IDictionary<string, object> attributesDict = attributesObj as System.Collections.Generic.IDictionary<string, object>;
                    if (attributesDict != null)
                    {
                        if (attributesDict.ContainsKey("STR"))
                        {
                            int str = Convert.ToInt32(attributesDict["STR"]);
                            SetAbility(Ability.Strength, str);
                        }
                        if (attributesDict.ContainsKey("DEX"))
                        {
                            int dex = Convert.ToInt32(attributesDict["DEX"]);
                            SetAbility(Ability.Dexterity, dex);
                        }
                        if (attributesDict.ContainsKey("CON"))
                        {
                            int con = Convert.ToInt32(attributesDict["CON"]);
                            SetAbility(Ability.Constitution, con);
                        }
                        if (attributesDict.ContainsKey("INT"))
                        {
                            int intel = Convert.ToInt32(attributesDict["INT"]);
                            SetAbility(Ability.Intelligence, intel);
                        }
                        if (attributesDict.ContainsKey("WIS"))
                        {
                            int wis = Convert.ToInt32(attributesDict["WIS"]);
                            SetAbility(Ability.Wisdom, wis);
                        }
                        if (attributesDict.ContainsKey("CHA"))
                        {
                            int cha = Convert.ToInt32(attributesDict["CHA"]);
                            SetAbility(Ability.Charisma, cha);
                        }
                    }
                }
            }
            else if (Owner.HasData("Abilities"))
            {
                // Fallback: Also support "Abilities" for compatibility
                object abilitiesObj = Owner.GetData("Abilities");
                if (abilitiesObj != null)
                {
                    System.Collections.Generic.IDictionary<string, object> abilitiesDict = abilitiesObj as System.Collections.Generic.IDictionary<string, object>;
                    if (abilitiesDict != null)
                    {
                        if (abilitiesDict.ContainsKey("STR"))
                        {
                            int str = Convert.ToInt32(abilitiesDict["STR"]);
                            SetAbility(Ability.Strength, str);
                        }
                        if (abilitiesDict.ContainsKey("DEX"))
                        {
                            int dex = Convert.ToInt32(abilitiesDict["DEX"]);
                            SetAbility(Ability.Dexterity, dex);
                        }
                        if (abilitiesDict.ContainsKey("CON"))
                        {
                            int con = Convert.ToInt32(abilitiesDict["CON"]);
                            SetAbility(Ability.Constitution, con);
                        }
                        if (abilitiesDict.ContainsKey("INT"))
                        {
                            int intel = Convert.ToInt32(abilitiesDict["INT"]);
                            SetAbility(Ability.Intelligence, intel);
                        }
                        if (abilitiesDict.ContainsKey("WIS"))
                        {
                            int wis = Convert.ToInt32(abilitiesDict["WIS"]);
                            SetAbility(Ability.Wisdom, wis);
                        }
                        if (abilitiesDict.ContainsKey("CHA"))
                        {
                            int cha = Convert.ToInt32(abilitiesDict["CHA"]);
                            SetAbility(Ability.Charisma, cha);
                        }
                    }
                }
            }
            else
            {
                // Fallback: Load attributes from individual keys
                if (Owner.HasData("STR"))
                {
                    int str = Owner.GetData<int>("STR", 10);
                    SetAbility(Ability.Strength, str);
                }
                if (Owner.HasData("DEX"))
                {
                    int dex = Owner.GetData<int>("DEX", 10);
                    SetAbility(Ability.Dexterity, dex);
                }
                if (Owner.HasData("CON"))
                {
                    int con = Owner.GetData<int>("CON", 10);
                    SetAbility(Ability.Constitution, con);
                }
                if (Owner.HasData("INT"))
                {
                    int intel = Owner.GetData<int>("INT", 10);
                    SetAbility(Ability.Intelligence, intel);
                }
                if (Owner.HasData("WIS"))
                {
                    int wis = Owner.GetData<int>("WIS", 10);
                    SetAbility(Ability.Wisdom, wis);
                }
                if (Owner.HasData("CHA"))
                {
                    int cha = Owner.GetData<int>("CHA", 10);
                    SetAbility(Ability.Charisma, cha);
                }
            }

            // Load base attack bonus from entity data
            if (Owner.HasData("BaseAttackBonus") || Owner.HasData("BAB"))
            {
                int bab = Owner.GetData<int>("BaseAttackBonus", Owner.GetData<int>("BAB", 0));
                if (bab >= 0)
                {
                    SetBaseAttackBonus(bab);
                }
            }

            // Load base saving throws from entity data
            int fortitude = Owner.GetData<int>("FortitudeSave", Owner.GetData<int>("fortbonus", -1));
            int reflex = Owner.GetData<int>("ReflexSave", Owner.GetData<int>("refbonus", -1));
            int will = Owner.GetData<int>("WillSave", Owner.GetData<int>("willbonus", -1));
            if (fortitude >= 0 || reflex >= 0 || will >= 0)
            {
                SetBaseSaves(
                    fortitude >= 0 ? fortitude : _baseFortitude,
                    reflex >= 0 ? reflex : _baseReflex,
                    will >= 0 ? will : _baseWill
                );
            }

            // Load level from entity data
            if (Owner.HasData("Level"))
            {
                int level = Owner.GetData<int>("Level", 1);
                if (level >= 1)
                {
                    Level = level;
                }
            }

            // Load armor bonuses from entity data
            if (Owner.HasData("NaturalArmor") || Owner.HasData("NaturalAC"))
            {
                int naturalArmor = Owner.GetData<int>("NaturalArmor", Owner.GetData<int>("NaturalAC", 0));
                if (naturalArmor >= 0)
                {
                    NaturalArmor = naturalArmor;
                }
            }

            if (Owner.HasData("ArmorBonus"))
            {
                int armorBonus = Owner.GetData<int>("ArmorBonus", 0);
                if (armorBonus >= 0)
                {
                    ArmorBonus = armorBonus;
                }
            }

            if (Owner.HasData("DeflectionBonus"))
            {
                int deflectionBonus = Owner.GetData<int>("DeflectionBonus", 0);
                if (deflectionBonus >= 0)
                {
                    DeflectionBonus = deflectionBonus;
                }
            }

            // Load Defense from entity data (Eclipse-specific)
            if (Owner.HasData("Defense"))
            {
                int defense = Owner.GetData<int>("Defense", 10);
                if (defense >= 0)
                {
                    Defense = defense;
                }
            }

            // Load movement speeds from entity data
            if (Owner.HasData("WalkSpeed"))
            {
                float walkSpeed = Owner.GetData<float>("WalkSpeed", 0f);
                if (walkSpeed > 0f)
                {
                    WalkSpeed = walkSpeed;
                }
            }

            if (Owner.HasData("RunSpeed"))
            {
                float runSpeed = Owner.GetData<float>("RunSpeed", 0f);
                if (runSpeed > 0f)
                {
                    RunSpeed = runSpeed;
                }
            }

            // Load skills from entity data (Dragon Age has different skills)
            // Skills are loaded by ID, supporting up to 255 for extensibility
            for (int skillId = 0; skillId < 255; skillId++)
            {
                string skillKey = "Skill" + skillId;
                if (Owner.HasData(skillKey))
                {
                    int skillRank = Owner.GetData<int>(skillKey, 0);
                    if (skillRank >= 0)
                    {
                        SetSkillRank(skillId, skillRank);
                    }
                }
            }

            // Load known spells/talents/abilities from entity data
            if (Owner.HasData("KnownSpells") || Owner.HasData("KnownTalents") || Owner.HasData("KnownAbilities"))
            {
                object spellsObj = Owner.GetData("KnownSpells") ?? Owner.GetData("KnownTalents") ?? Owner.GetData("KnownAbilities");
                if (spellsObj != null)
                {
                    System.Collections.Generic.IEnumerable<int> spells = spellsObj as System.Collections.Generic.IEnumerable<int>;
                    if (spells != null)
                    {
                        foreach (int spellId in spells)
                        {
                            if (spellId >= 0)
                            {
                                // Load spell level if stored, otherwise default to 1
                                string spellLevelKey = "SpellLevel_" + spellId;
                                int spellLevel = 1; // Default level
                                if (Owner.HasData(spellLevelKey))
                                {
                                    int storedLevel = Owner.GetData<int>(spellLevelKey, 1);
                                    if (storedLevel >= 1)
                                    {
                                        spellLevel = storedLevel;
                                    }
                                }
                                AddSpell(spellId, spellLevel);
                            }
                        }
                    }
                    else
                    {
                        // Try as list of objects that can be converted to int
                        System.Collections.IEnumerable enumerable = spellsObj as System.Collections.IEnumerable;
                        if (enumerable != null)
                        {
                            foreach (object spellObj in enumerable)
                            {
                                int spellId = Convert.ToInt32(spellObj);
                                if (spellId >= 0)
                                {
                                    // Load spell level if stored, otherwise default to 1
                                    string spellLevelKey = "SpellLevel_" + spellId;
                                    int spellLevel = 1; // Default level
                                    if (Owner.HasData(spellLevelKey))
                                    {
                                        int storedLevel = Owner.GetData<int>(spellLevelKey, 1);
                                        if (storedLevel >= 1)
                                        {
                                            spellLevel = storedLevel;
                                        }
                                    }
                                    AddSpell(spellId, spellLevel);
                                }
                            }
                        }
                    }
                }
            }

            // Also load spell levels individually (for backward compatibility with existing entity data)
            // This allows spell levels to be loaded even if KnownSpells/KnownTalents/KnownAbilities isn't set
            // Scan for keys matching "SpellLevel_<id>" pattern
            // Note: This is less efficient but provides backward compatibility
            for (int spellId = 0; spellId < 10000; spellId++) // Reasonable upper limit for spell IDs
            {
                string spellLevelKey = "SpellLevel_" + spellId;
                if (Owner.HasData(spellLevelKey))
                {
                    int spellLevel = Owner.GetData<int>(spellLevelKey, 1);
                    if (spellLevel >= 1)
                    {
                        // If spell is already in known list, update its level
                        // Otherwise, add it with the specified level
                        SetSpellLevel(spellId, spellLevel);
                    }
                }
            }
        }

        /// <summary>
        /// Calculates final movement speed after applying all movement speed modifiers.
        /// </summary>
        /// <param name="baseSpeed">Base movement speed before modifiers</param>
        /// <returns>Final movement speed with all effects applied</returns>
        /// <remarks>
        /// Movement Speed Calculation (daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine: Movement speed modifiers work similarly to Odyssey/Aurora
        /// - Haste/Slow effects modify movement speed
        /// - EffectMovementSpeedIncrease/Decrease work the same as Odyssey/Aurora
        /// - Effects are applied multiplicatively in order
        /// </remarks>
        private float CalculateMovementSpeed(float baseSpeed)
        {
            if (baseSpeed <= 0.0f)
            {
                return 0.1f; // Minimum speed
            }

            float speed = baseSpeed;
            float speedMultiplier = 1.0f;
            float speedPercent = 100.0f; // Percentage-based speed (100% = unchanged)
            bool hasSpeedPercent = false;

            // Query EffectSystem for all movement speed modifiers
            if (Owner != null && Owner.World != null && Owner.World.EffectSystem != null)
            {
                foreach (ActiveEffect activeEffect in Owner.World.EffectSystem.GetEffects(Owner))
                {
                    EffectType effectType = activeEffect.Effect.Type;
                    int effectAmount = activeEffect.Effect.Amount;

                    if (effectType == EffectType.Haste)
                    {
                        // Haste doubles movement speed (100% increase = 2.0x multiplier)
                        speedMultiplier *= 2.0f;
                    }
                    else if (effectType == EffectType.Slow)
                    {
                        // Slow halves movement speed (50% reduction = 0.5x multiplier)
                        speedMultiplier *= 0.5f;
                    }
                    else if (effectType == EffectType.MovementSpeedIncrease)
                    {
                        // EffectMovementSpeedIncrease: Percentage-based speed modifier
                        if (effectAmount < 100)
                        {
                            speedPercent = 100.0f + effectAmount;
                        }
                        else
                        {
                            speedPercent = effectAmount;
                        }
                        hasSpeedPercent = true;
                    }
                    else if (effectType == EffectType.MovementSpeedDecrease)
                    {
                        // EffectMovementSpeedDecrease: Percentage reduction
                        if (effectAmount > 0 && effectAmount < 100)
                        {
                            speedPercent *= (100.0f - effectAmount) / 100.0f;
                        }
                        else if (effectAmount < 0)
                        {
                            // Negative values result in speed increase
                            speedPercent *= (100.0f - effectAmount) / 100.0f;
                        }
                    }
                }
            }

            // Apply multipliers first (Haste/Slow)
            speed = speed * speedMultiplier;

            // Apply percentage-based modifiers (MovementSpeedIncrease/Decrease)
            if (hasSpeedPercent || speedPercent != 100.0f)
            {
                speed = speed * (speedPercent / 100.0f);
            }

            // Ensure minimum speed to prevent zero/negative values
            return Math.Max(0.1f, speed);
        }

        #endregion
    }
}

