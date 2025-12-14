using System;
using System.Collections.Generic;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Party
{
    /// <summary>
    /// Represents a party member (PC or NPC).
    /// </summary>
    /// <remarks>
    /// Party members persist their state when not in active party.
    /// State includes:
    /// - Entity data (stats, equipment, inventory)
    /// - XP progression
    /// - Influence (K2)
    /// - AI behavior settings
    /// </remarks>
    public class PartyMember
    {
        private readonly bool _isPlayerCharacter;

        /// <summary>
        /// The entity in world.
        /// </summary>
        public IEntity Entity { get; private set; }

        /// <summary>
        /// Whether this is the player character.
        /// </summary>
        public bool IsPlayerCharacter
        {
            get { return _isPlayerCharacter; }
        }

        /// <summary>
        /// NPC slot number (0-9).
        /// </summary>
        public int NPCSlot { get; set; }

        /// <summary>
        /// Whether available for party selection.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Whether can currently be selected (may be temporarily locked out).
        /// </summary>
        public bool IsSelectable { get; set; }

        /// <summary>
        /// Whether currently in the active party (in field).
        /// </summary>
        public bool IsInActiveParty { get; set; }

        /// <summary>
        /// Influence level (K2 only, 0-100, 50 = neutral).
        /// </summary>
        public int Influence { get; set; }

        /// <summary>
        /// Current XP.
        /// </summary>
        public int XP { get; private set; }

        /// <summary>
        /// AI attack preference.
        /// </summary>
        public PartyAIMode AIMode { get; set; }

        /// <summary>
        /// Whether to use items automatically.
        /// </summary>
        public bool AutoUseItems { get; set; }

        /// <summary>
        /// Whether to use powers automatically.
        /// </summary>
        public bool AutoUsePowers { get; set; }

        /// <summary>
        /// Portrait index.
        /// </summary>
        public int PortraitId { get; set; }

        /// <summary>
        /// Template ResRef (for respawning).
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Event fired when XP changes.
        /// </summary>
        public event Action<int, int> OnXPChanged;

        /// <summary>
        /// Event fired when level up is available.
        /// </summary>
        public event Action OnLevelUpAvailable;

        public PartyMember(IEntity entity, bool isPlayerCharacter)
        {
            Entity = entity ?? throw new ArgumentNullException("entity");
            _isPlayerCharacter = isPlayerCharacter;
            
            NPCSlot = -1;
            IsAvailable = false;
            IsSelectable = false;
            IsInActiveParty = false;
            Influence = 50; // Neutral
            XP = 0;
            AIMode = PartyAIMode.Normal;
            AutoUseItems = true;
            AutoUsePowers = true;

            if (entity is Entities.Entity ent)
            {
                TemplateResRef = ent.TemplateResRef;
            }
        }

        #region XP Management

        /// <summary>
        /// Awards XP to this party member.
        /// </summary>
        public void AwardXP(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int oldXP = XP;
            XP += amount;

            if (OnXPChanged != null)
            {
                OnXPChanged(oldXP, XP);
            }

            // Check for level up
            if (CanLevelUp())
            {
                if (OnLevelUpAvailable != null)
                {
                    OnLevelUpAvailable();
                }
            }
        }

        /// <summary>
        /// Gets XP required for next level.
        /// </summary>
        public int GetXPForNextLevel()
        {
            int level = GetLevel();
            // KOTOR XP curve: level * (level + 1) * 500
            return (level + 1) * (level + 2) * 500;
        }

        /// <summary>
        /// Checks if can level up.
        /// </summary>
        public bool CanLevelUp()
        {
            return XP >= GetXPForNextLevel();
        }

        /// <summary>
        /// Gets current total level.
        /// </summary>
        public int GetLevel()
        {
            var stats = Entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                // Would need class levels tracking
                // For now, estimate from XP
                int level = 1;
                int xpNeeded = 2000; // Level 2
                while (XP >= xpNeeded && level < 20)
                {
                    level++;
                    xpNeeded = (level + 1) * (level + 2) * 500;
                }
                return level;
            }
            return 1;
        }

        #endregion

        #region Equipment Access

        /// <summary>
        /// Gets equipped weapon(s).
        /// </summary>
        public void GetEquippedWeapons(out IEntity mainHand, out IEntity offHand)
        {
            // TODO: Integrate with inventory/equipment system
            mainHand = null;
            offHand = null;
        }

        /// <summary>
        /// Gets equipped armor.
        /// </summary>
        public IEntity GetEquippedArmor()
        {
            // TODO: Integrate with inventory/equipment system
            return null;
        }

        #endregion

        #region State Queries

        /// <summary>
        /// Checks if party member is alive.
        /// </summary>
        public bool IsAlive()
        {
            var stats = Entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                return stats.CurrentHP > 0;
            }
            return true;
        }

        /// <summary>
        /// Gets current HP.
        /// </summary>
        public int GetCurrentHP()
        {
            var stats = Entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                return stats.CurrentHP;
            }
            return 1;
        }

        /// <summary>
        /// Gets max HP.
        /// </summary>
        public int GetMaxHP()
        {
            var stats = Entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                return stats.MaxHP;
            }
            return 1;
        }

        /// <summary>
        /// Gets current Force points (if applicable).
        /// </summary>
        public int GetCurrentFP()
        {
            var stats = Entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                return stats.CurrentFP;
            }
            return 0;
        }

        /// <summary>
        /// Gets max Force points.
        /// </summary>
        public int GetMaxFP()
        {
            var stats = Entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                return stats.MaxFP;
            }
            return 0;
        }

        #endregion

        #region AI Settings

        /// <summary>
        /// Sets AI behavior mode.
        /// </summary>
        public void SetAIMode(PartyAIMode mode)
        {
            AIMode = mode;
        }

        /// <summary>
        /// Gets NWScript-style AI level (0-5).
        /// </summary>
        public int GetAILevel()
        {
            switch (AIMode)
            {
                case PartyAIMode.Passive:
                    return 0;
                case PartyAIMode.Defensive:
                    return 1;
                case PartyAIMode.Normal:
                    return 2;
                case PartyAIMode.Aggressive:
                    return 3;
                default:
                    return 2;
            }
        }

        #endregion

        /// <summary>
        /// Updates the entity reference (for save/load).
        /// </summary>
        public void UpdateEntity(IEntity newEntity)
        {
            Entity = newEntity ?? throw new ArgumentNullException("newEntity");
        }
    }

    /// <summary>
    /// Party AI behavior modes.
    /// </summary>
    public enum PartyAIMode
    {
        /// <summary>
        /// Don't attack unless explicitly commanded.
        /// </summary>
        Passive = 0,

        /// <summary>
        /// Only attack if attacked first.
        /// </summary>
        Defensive = 1,

        /// <summary>
        /// Normal combat behavior.
        /// </summary>
        Normal = 2,

        /// <summary>
        /// Aggressive - attack any hostile.
        /// </summary>
        Aggressive = 3
    }
}
