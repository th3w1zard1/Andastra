using System;
using System.Collections.Generic;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Concrete implementation of creature stats for KOTOR.
    /// </summary>
    /// <remarks>
    /// KOTOR D20 System:
    /// - Ability scores: 1-30+ range, modifier = (score - 10) / 2
    /// - Hit points: Based on class hit dice + Con modifier per level
    /// - Attack: BAB + STR/DEX mod vs. Defense
    /// - Defense: 10 + DEX mod + Armor + Class bonus
    /// - Saves: Base + ability mod (Fort=CON, Ref=DEX, Will=WIS)
    /// 
    /// Key 2DA tables:
    /// - classes.2da: Hit dice, BAB progression, saves
    /// - appearance.2da: Walk/run speed
    /// </remarks>
    public class StatsComponent : IStatsComponent
    {
        private readonly Dictionary<Ability, int> _abilities;
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

        public StatsComponent()
        {
            _abilities = new Dictionary<Ability, int>();
            
            // Default ability scores (10 = average human)
            foreach (Ability ability in Enum.GetValues(typeof(Ability)))
            {
                _abilities[ability] = 10;
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
            
            // Default movement speeds (from appearance.2da averages)
            WalkSpeed = 1.75f;
            RunSpeed = 4.0f;
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
        }

        public int GetAbility(Ability ability)
        {
            int value;
            if (_abilities.TryGetValue(ability, out value))
            {
                return value;
            }
            return 10;
        }

        public void SetAbility(Ability ability, int value)
        {
            _abilities[ability] = Math.Max(1, Math.Min(100, value));
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
                // BAB + STR modifier for melee (or DEX for ranged/finesse)
                return _baseAttackBonus + GetAbilityModifier(Ability.Strength);
            }
        }

        public int ArmorClass
        {
            get
            {
                // Defense = 10 + DEX mod + Armor + Natural + Deflection + Class bonus
                return 10 
                    + GetAbilityModifier(Ability.Dexterity)
                    + _armorBonus
                    + _naturalArmor
                    + _deflectionBonus;
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

        public float WalkSpeed { get; set; }
        public float RunSpeed { get; set; }

        #endregion

        #region Extended Properties

        /// <summary>
        /// Character level (total class levels).
        /// </summary>
        public int Level
        {
            get { return _baseLevel; }
            set { _baseLevel = Math.Max(1, value); }
        }

        /// <summary>
        /// Experience points.
        /// </summary>
        public int Experience { get; set; }

        /// <summary>
        /// Current force points.
        /// </summary>
        public int CurrentFP { get; set; }

        /// <summary>
        /// Maximum force points.
        /// </summary>
        public int MaxFP { get; set; }

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
        /// Calculates XP needed for next level.
        /// </summary>
        public int XPForNextLevel()
        {
            // KOTOR uses: XP = level * (level - 1) * 500
            int nextLevel = Level + 1;
            return nextLevel * (nextLevel - 1) * 500;
        }

        /// <summary>
        /// Checks if creature can level up.
        /// </summary>
        public bool CanLevelUp()
        {
            return Experience >= XPForNextLevel() && Level < 20;
        }

        /// <summary>
        /// Loads stats from entity's stored data.
        /// </summary>
        private void LoadFromEntityData()
        {
            if (Owner == null)
            {
                return;
            }

            // Try to get stored ability scores
            foreach (Ability ability in Enum.GetValues(typeof(Ability)))
            {
                string key = "Ability_" + ability;
                if (Owner.HasData(key))
                {
                    _abilities[ability] = Owner.GetData<int>(key, 10);
                }
            }

            // Load HP
            if (Owner.HasData("CurrentHitPoints"))
            {
                _currentHP = Owner.GetData<int>("CurrentHitPoints", 10);
            }
            if (Owner.HasData("MaxHitPoints"))
            {
                _maxHP = Owner.GetData<int>("MaxHitPoints", 10);
            }

            // Load movement speeds
            if (Owner.HasData("WalkRate"))
            {
                WalkSpeed = Owner.GetData<float>("WalkRate", 1.75f);
            }
            if (Owner.HasData("RunRate"))
            {
                RunSpeed = Owner.GetData<float>("RunRate", 4.0f);
            }
        }

        #endregion
    }
}
