using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Game.Games.Aurora.Components
{
    /// <summary>
    /// Aurora Engine (Neverwinter Nights) specific stats component implementation.
    /// </summary>
    /// <remarks>
    /// Aurora Stats Component (nwmain.exe):
    /// - Based on CNWSCreatureStats class in nwmain.exe (Neverwinter Nights)
    /// - Located via string references: "CurrentHP" @ various addresses, "MaxHP" @ various addresses
    /// - CNWSCreatureStats::LoadStats @ 0x1403975e0 (nwmain.exe) - loads stats from GFF
    /// - CNWSCreatureStats::SaveStats @ 0x1403a0a60 (nwmain.exe) - saves stats to GFF
    /// - D&D 3rd Edition rules (same as Odyssey but no Force Points)
    /// - Ability scores: STR, DEX, CON, INT, WIS, CHA (D20 standard, modifiers = (score - 10) / 2)
    /// - Hit points: Based on class hit dice + CON modifier per level
    /// - Base Attack Bonus: From class levels (full BAB, 3/4 BAB, or 1/2 BAB classes)
    /// - Armor Class: 10 + DEX mod + Armor + Natural + Deflection + Shield + Size mods
    /// - Saves: Fortitude (CON), Reflex (DEX), Will (WIS) - base + ability mod + misc bonuses
    /// - Skills: NWN has 27 skills (Animal Empathy, Appraise, Bluff, Concentration, Craft Armor, Craft Trap, Craft Weapon,
    ///   Diplomacy, Disable Trap, Discipline, Heal, Hide, Intimidate, Listen, Lore, Move Silently, Open Lock, Parry,
    ///   Perform, Pick Pocket, Search, Spellcraft, Spot, Taunt, Tumble, Use Magic Device)
    /// - Movement speeds: WalkSpeed and RunSpeed in meters per second (from appearance.2da WALKRATE/RUNRATE)
    /// - No Force Points: NWN uses spell slots instead (spell casting is handled separately)
    /// - CurrentFP and MaxFP are kept for interface compatibility but always return 0 for Aurora
    /// 
    /// Key differences from Odyssey:
    /// - No Force Points (spell slots instead, handled by spell system)
    /// - Different skill set (27 skills vs 8 in KOTOR)
    /// - Slightly different AC calculation (includes Size modifiers)
    /// - Spell system uses spell slots per day rather than Force Points
    /// </remarks>
    public class AuroraStatsComponent : IStatsComponent
    {
        private readonly Dictionary<Ability, int> _abilities;
        private readonly Dictionary<int, int> _skills; // Skill ID -> Skill Rank (NWN has 27 skills)
        private readonly HashSet<int> _knownSpells; // Spell ID -> Known (for spell availability checks)
        private int _currentHP;
        private int _maxHP;
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
        private int _shieldBonus; // Shield AC bonus (Aurora-specific)
        private int _sizeModifier; // Size modifier to AC (Aurora-specific)

        public AuroraStatsComponent()
        {
            _abilities = new Dictionary<Ability, int>();
            _skills = new Dictionary<int, int>();
            _knownSpells = new HashSet<int>();

            // Default ability scores (10 = average human, D&D standard)
            foreach (Ability ability in Enum.GetValues(typeof(Ability)))
            {
                _abilities[ability] = 10;
            }

            // Initialize all NWN skills to 0 (untrained)
            // NWN has 27 skills (IDs 0-26 typically, but we support up to skill ID 255 for extensibility)
            for (int i = 0; i < 27; i++)
            {
                _skills[i] = 0;
            }

            _currentHP = 10;
            _maxHP = 10;
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
            _shieldBonus = 0;
            _sizeModifier = 0;

            // Default movement speeds (from appearance.2da averages for NWN)
            _baseWalkSpeed = 2.0f; // Slightly faster than KOTOR default
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

        public int CurrentHP
        {
            get { return _currentHP; }
            set { _currentHP = Math.Max(0, Math.Min(value, MaxHP)); }
        }

        public int MaxHP
        {
            get { return _maxHP; }
            set { _maxHP = Math.Max(1, value); }
        }

        /// <summary>
        /// Current Force points (Aurora/NWN doesn't use Force Points).
        /// </summary>
        /// <remarks>
        /// Aurora/NWN uses spell slots instead of Force Points.
        /// This property is kept for interface compatibility but always returns 0.
        /// </remarks>
        public int CurrentFP
        {
            get { return 0; } // NWN doesn't use Force Points
            set { /* No-op: NWN doesn't use Force Points */ }
        }

        /// <summary>
        /// Maximum Force points (Aurora/NWN doesn't use Force Points).
        /// </summary>
        /// <remarks>
        /// Aurora/NWN uses spell slots instead of Force Points.
        /// This property is kept for interface compatibility but always returns 0.
        /// </remarks>
        public int MaxFP
        {
            get { return 0; } // NWN doesn't use Force Points
            set { /* No-op: NWN doesn't use Force Points */ }
        }

        public int GetAbility(Ability ability)
        {
            int value;
            if (_abilities.TryGetValue(ability, out value))
            {
                return value;
            }
            return 10; // Default ability score
        }

        public void SetAbility(Ability ability, int value)
        {
            _abilities[ability] = Math.Max(1, Math.Min(100, value)); // D&D ability range is typically 1-100
        }

        public int GetAbilityModifier(Ability ability)
        {
            // D20 formula: (score - 10) / 2, rounded down
            int score = GetAbility(ability);
            return (score - 10) / 2;
        }

        public bool IsDead
        {
            get { return _currentHP <= 0; }
        }

        public int BaseAttackBonus
        {
            get
            {
                // BAB + STR modifier for melee (or DEX for ranged/finesse) + effect bonuses
                // Effect bonuses are tracked via AddEffectAttackBonus/RemoveEffectAttackBonus called by EffectSystem
                return _baseAttackBonus + GetAbilityModifier(Ability.Strength) + _effectAttackBonus;
            }
        }

        public int ArmorClass
        {
            get
            {
                // AC = 10 + DEX mod + Armor + Natural + Deflection + Shield + Size mod + Effect bonuses
                // Based on nwmain.exe: AC calculation includes size modifiers
                // Effect bonuses are tracked via AddEffectACBonus/RemoveEffectACBonus called by EffectSystem
                return 10
                    + GetAbilityModifier(Ability.Dexterity)
                    + _armorBonus
                    + _naturalArmor
                    + _deflectionBonus
                    + _shieldBonus
                    + _sizeModifier
                    + _effectACBonus;
            }
        }

        public int FortitudeSave
        {
            get { return _baseFortitude + GetAbilityModifier(Ability.Constitution); }
        }

        public int ReflexSave
        {
            get { return _baseReflex + GetAbilityModifier(Ability.Dexterity); }
        }

        public int WillSave
        {
            get { return _baseWill + GetAbilityModifier(Ability.Wisdom); }
        }

        private float _baseWalkSpeed;
        private float _baseRunSpeed;

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

        public int GetSkillRank(int skill)
        {
            // Returns skill rank, or 0 if untrained, or -1 if skill doesn't exist
            // NWN supports 27 skills but we allow up to 255 for extensibility
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

        public bool HasSpell(int spellId)
        {
            // Based on nwmain.exe: Spell availability checks
            // NWN uses spell slots, but we track known spells for availability
            return _knownSpells.Contains(spellId);
        }

        public int Level
        {
            get { return _baseLevel; }
            set { _baseLevel = Math.Max(1, value); }
        }

        #endregion

        #region Extended Properties (Aurora-specific)

        /// <summary>
        /// Shield AC bonus (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Shield bonus is separate from armor bonus
        /// </remarks>
        public int ShieldBonus
        {
            get { return _shieldBonus; }
            set { _shieldBonus = Math.Max(0, value); }
        }

        /// <summary>
        /// Size modifier to AC (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Size modifiers affect AC (smaller = higher AC)
        /// </remarks>
        public int SizeModifier
        {
            get { return _sizeModifier; }
            set { _sizeModifier = value; } // Can be negative (larger creatures have negative size mod)
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
        /// Adds a spell to the creature's known spells list.
        /// </summary>
        public void AddSpell(int spellId)
        {
            _knownSpells.Add(spellId);
        }

        /// <summary>
        /// Removes a spell from the creature's known spells list.
        /// </summary>
        public void RemoveSpell(int spellId)
        {
            _knownSpells.Remove(spellId);
        }

        /// <summary>
        /// Gets all known spells.
        /// </summary>
        public System.Collections.Generic.IEnumerable<int> GetKnownSpells()
        {
            return _knownSpells;
        }

        /// <summary>
        /// Sets the maximum HP.
        /// </summary>
        public void SetMaxHP(int value)
        {
            _maxHP = Math.Max(1, value);
            if (_currentHP > _maxHP)
            {
                _currentHP = _maxHP;
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

            int actualDamage = Math.Min(damage, _currentHP);
            _currentHP -= actualDamage;
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

            int actualHeal = Math.Min(amount, _maxHP - _currentHP);
            _currentHP += actualHeal;
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
        /// Based on nwmain.exe: CNWSCreatureStats::LoadStats @ 0x1403975e0
        /// Stats are typically loaded from GFF structures during entity creation
        /// </remarks>
        private void LoadFromEntityData()
        {
            // Stats are now loaded via SetMaxHP, SetAbility, etc. during entity initialization
            // This method is a placeholder for future entity data integration
        }

        /// <summary>
        /// Calculates final movement speed after applying all movement speed modifiers.
        /// </summary>
        /// <param name="baseSpeed">Base movement speed before modifiers</param>
        /// <returns>Final movement speed with all effects applied</returns>
        /// <remarks>
        /// Movement Speed Calculation (nwmain.exe):
        /// - Based on nwmain.exe: GetWalkRate returns GetMovementRateFactor(this) * baseWalkRate * constant
        ///   Located via function: GetWalkRate @ 0x140396730 (nwmain.exe)
        ///   Movement rate factor accumulates all movement speed modifiers
        /// - EffectMovementSpeedIncrease (script function 165):
        ///   If nNewSpeedPercent < 100: final speed = (100 + nNewSpeedPercent)%
        ///   If nNewSpeedPercent >= 100: final speed = nNewSpeedPercent%
        ///   Example: 50 -> 150%, 200 -> 200%
        /// - EffectMovementSpeedDecrease (script function 451):
        ///   nPercentChange expected to be 1-99 (percentage reduction)
        ///   If negative: results in speed increase
        ///   If >= 100: effect is deleted/ignored
        ///   Example: 50 -> 50% reduction (speed becomes 50% of original)
        /// - Effects are applied multiplicatively in order:
        ///   1. Haste/Slow (fixed multipliers)
        ///   2. MovementSpeedIncrease (percentage-based, replaces speed)
        ///   3. MovementSpeedDecrease (percentage reduction)
        /// - Based on original engine behavior: effects stack multiplicatively
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
                        // Based on nwmain.exe: Haste effect
                        speedMultiplier *= 2.0f;
                    }
                    else if (effectType == EffectType.Slow)
                    {
                        // Slow halves movement speed (50% reduction = 0.5x multiplier)
                        // Based on nwmain.exe: Slow effect
                        speedMultiplier *= 0.5f;
                    }
                    else if (effectType == EffectType.MovementSpeedIncrease)
                    {
                        // EffectMovementSpeedIncrease: Percentage-based speed modifier
                        // Based on script function 165: EffectMovementSpeedIncrease(int nNewSpeedPercent)
                        // If nNewSpeedPercent < 100: add 100 to get final percentage (e.g., 50 -> 150%)
                        // If nNewSpeedPercent >= 100: use directly as percentage (e.g., 200 -> 200%)
                        // This replaces any previous speed percentage (does not stack with other MovementSpeedIncrease)
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
                        // Based on script function 451: EffectMovementSpeedDecrease(int nPercentChange)
                        // Expected to be 1-99 (percentage to reduce by)
                        // If negative: results in speed increase
                        // If >= 100: effect is ignored/deleted
                        if (effectAmount > 0 && effectAmount < 100)
                        {
                            // Reduce speed by percentage (e.g., 50 means 50% reduction, speed becomes 50% of current)
                            speedPercent *= (100.0f - effectAmount) / 100.0f;
                        }
                        else if (effectAmount < 0)
                        {
                            // Negative values result in speed increase (unusual but documented behavior)
                            speedPercent *= (100.0f - effectAmount) / 100.0f;
                        }
                        // If >= 100, ignore the effect
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

