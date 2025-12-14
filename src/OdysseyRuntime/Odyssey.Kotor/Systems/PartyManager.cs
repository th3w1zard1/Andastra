using System;
using System.Collections.Generic;
using System.Numerics;
using JetBrains.Annotations;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Systems
{
    /// <summary>
    /// Event arguments for party changes.
    /// </summary>
    public class PartyChangedEventArgs : EventArgs
    {
        public IEntity Member { get; set; }
        public int Slot { get; set; }
        public bool Added { get; set; }
    }

    /// <summary>
    /// Manages the player's party in KOTOR.
    /// </summary>
    /// <remarks>
    /// KOTOR party rules:
    /// - Maximum 3 active party members (including PC)
    /// - Up to 9 available party members can be recruited
    /// - Party selection UI shows available members
    /// - Party members follow the leader
    /// - Party members share XP
    /// - NPCs in party table are stored by NPC index (0-8)
    /// 
    /// Party formation:
    /// - Leader (slot 0): Player character
    /// - Member 1 (slot 1): First party member
    /// - Member 2 (slot 2): Second party member
    /// 
    /// Key 2DA: partytable.2da defines available party members
    /// </remarks>
    public class PartyManager
    {
        /// <summary>
        /// Maximum active party size (including PC).
        /// </summary>
        public const int MaxActivePartySize = 3;

        /// <summary>
        /// Maximum available party members (NPCs that can be recruited).
        /// </summary>
        public const int MaxAvailableMembers = 9;

        private readonly IWorld _world;
        private readonly List<IEntity> _activeParty;
        private readonly Dictionary<int, IEntity> _availableMembers;
        private readonly HashSet<int> _selectedMembers;

        /// <summary>
        /// Event fired when party composition changes.
        /// </summary>
        public event EventHandler<PartyChangedEventArgs> OnPartyChanged;

        /// <summary>
        /// Event fired when the party leader changes.
        /// </summary>
        public event EventHandler<PartyChangedEventArgs> OnLeaderChanged;

        public PartyManager(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _activeParty = new List<IEntity>(MaxActivePartySize);
            _availableMembers = new Dictionary<int, IEntity>();
            _selectedMembers = new HashSet<int>();
        }

        /// <summary>
        /// Gets the current party leader (typically the PC).
        /// </summary>
        [CanBeNull]
        public IEntity Leader
        {
            get { return _activeParty.Count > 0 ? _activeParty[0] : null; }
        }

        /// <summary>
        /// Gets the current active party members.
        /// </summary>
        public IReadOnlyList<IEntity> ActiveParty
        {
            get { return _activeParty; }
        }

        /// <summary>
        /// Gets all available (recruited) party member NPCs.
        /// </summary>
        public IEnumerable<IEntity> AvailableMembers
        {
            get { return _availableMembers.Values; }
        }

        /// <summary>
        /// Gets the number of active party members.
        /// </summary>
        public int ActiveMemberCount
        {
            get { return _activeParty.Count; }
        }

        /// <summary>
        /// Checks if a party member NPC is available (recruited).
        /// </summary>
        /// <param name="npcIndex">Index from partytable.2da (0-8)</param>
        public bool IsAvailable(int npcIndex)
        {
            return _availableMembers.ContainsKey(npcIndex);
        }

        /// <summary>
        /// Checks if a party member is currently selected (in active party).
        /// </summary>
        /// <param name="npcIndex">Index from partytable.2da (0-8)</param>
        public bool IsSelected(int npcIndex)
        {
            return _selectedMembers.Contains(npcIndex);
        }

        /// <summary>
        /// Sets the party leader (PC).
        /// </summary>
        public void SetLeader(IEntity pc)
        {
            if (pc == null)
            {
                throw new ArgumentNullException("pc");
            }

            if (_activeParty.Count == 0)
            {
                _activeParty.Add(pc);
            }
            else
            {
                IEntity oldLeader = _activeParty[0];
                _activeParty[0] = pc;

                if (oldLeader != pc)
                {
                    OnLeaderChanged?.Invoke(this, new PartyChangedEventArgs
                    {
                        Member = pc,
                        Slot = 0,
                        Added = true
                    });
                }
            }
        }

        /// <summary>
        /// Adds a party member NPC to the available pool.
        /// </summary>
        /// <param name="npcIndex">Index from partytable.2da (0-8)</param>
        /// <param name="member">The creature entity</param>
        public void AddAvailableMember(int npcIndex, IEntity member)
        {
            if (npcIndex < 0 || npcIndex >= MaxAvailableMembers)
            {
                throw new ArgumentOutOfRangeException("npcIndex", "NPC index must be 0-8");
            }

            if (member == null)
            {
                throw new ArgumentNullException("member");
            }

            _availableMembers[npcIndex] = member;
        }

        /// <summary>
        /// Removes a party member NPC from the available pool.
        /// </summary>
        /// <param name="npcIndex">Index from partytable.2da (0-8)</param>
        public void RemoveAvailableMember(int npcIndex)
        {
            if (_availableMembers.ContainsKey(npcIndex))
            {
                // Also remove from active party if selected
                if (_selectedMembers.Contains(npcIndex))
                {
                    DeselectMember(npcIndex);
                }
                _availableMembers.Remove(npcIndex);
            }
        }

        /// <summary>
        /// Gets an available party member by NPC index.
        /// </summary>
        [CanBeNull]
        public IEntity GetAvailableMember(int npcIndex)
        {
            IEntity member;
            if (_availableMembers.TryGetValue(npcIndex, out member))
            {
                return member;
            }
            return null;
        }

        /// <summary>
        /// Selects a party member to join the active party.
        /// </summary>
        /// <param name="npcIndex">Index from partytable.2da (0-8)</param>
        /// <returns>True if member was added, false if party full or not available</returns>
        public bool SelectMember(int npcIndex)
        {
            if (!IsAvailable(npcIndex))
            {
                return false;
            }

            if (_selectedMembers.Contains(npcIndex))
            {
                return true; // Already selected
            }

            if (_activeParty.Count >= MaxActivePartySize)
            {
                return false; // Party full
            }

            IEntity member = _availableMembers[npcIndex];
            _activeParty.Add(member);
            _selectedMembers.Add(npcIndex);

            OnPartyChanged?.Invoke(this, new PartyChangedEventArgs
            {
                Member = member,
                Slot = _activeParty.Count - 1,
                Added = true
            });

            return true;
        }

        /// <summary>
        /// Deselects a party member from the active party.
        /// </summary>
        /// <param name="npcIndex">Index from partytable.2da (0-8)</param>
        public void DeselectMember(int npcIndex)
        {
            if (!_selectedMembers.Contains(npcIndex))
            {
                return;
            }

            IEntity member = _availableMembers[npcIndex];
            int slot = _activeParty.IndexOf(member);
            
            _activeParty.Remove(member);
            _selectedMembers.Remove(npcIndex);

            OnPartyChanged?.Invoke(this, new PartyChangedEventArgs
            {
                Member = member,
                Slot = slot,
                Added = false
            });
        }

        /// <summary>
        /// Gets the party member at a specific slot (0 = leader, 1-2 = members).
        /// </summary>
        [CanBeNull]
        public IEntity GetMemberAtSlot(int slot)
        {
            if (slot >= 0 && slot < _activeParty.Count)
            {
                return _activeParty[slot];
            }
            return null;
        }

        /// <summary>
        /// Gets the slot index for a party member.
        /// </summary>
        /// <returns>Slot index (0-2) or -1 if not in party</returns>
        public int GetMemberSlot(IEntity member)
        {
            return _activeParty.IndexOf(member);
        }

        /// <summary>
        /// Checks if an entity is in the active party.
        /// </summary>
        public bool IsInParty(IEntity entity)
        {
            return _activeParty.Contains(entity);
        }

        /// <summary>
        /// Gets the NPC index for a party member.
        /// </summary>
        /// <returns>NPC index (0-8) or -1 if not found</returns>
        public int GetNpcIndex(IEntity member)
        {
            foreach (var kvp in _availableMembers)
            {
                if (kvp.Value == member)
                {
                    return kvp.Key;
                }
            }
            return -1;
        }

        /// <summary>
        /// Clears the active party (keeps available members).
        /// </summary>
        public void ClearActiveParty()
        {
            for (int i = _activeParty.Count - 1; i >= 0; i--)
            {
                var member = _activeParty[i];
                _activeParty.RemoveAt(i);

                // Find and remove from selected
                foreach (var kvp in _availableMembers)
                {
                    if (kvp.Value == member)
                    {
                        _selectedMembers.Remove(kvp.Key);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the follow position for a party member based on formation.
        /// </summary>
        /// <param name="slot">Party slot (1-2)</param>
        /// <param name="leaderPosition">Current leader position</param>
        /// <param name="leaderFacing">Current leader facing direction (radians)</param>
        /// <returns>Target follow position</returns>
        public Vector3 GetFormationPosition(int slot, Vector3 leaderPosition, float leaderFacing)
        {
            // Simple formation: followers behind and to the side
            const float FollowDistance = 2.0f;
            const float SideOffset = 1.5f;

            // Calculate direction behind leader
            float backX = (float)Math.Cos(leaderFacing + Math.PI);
            float backY = (float)Math.Sin(leaderFacing + Math.PI);

            // Calculate perpendicular direction
            float sideX = (float)Math.Cos(leaderFacing + Math.PI / 2);
            float sideY = (float)Math.Sin(leaderFacing + Math.PI / 2);

            float sideMultiplier = slot == 1 ? -1f : 1f;

            return new Vector3(
                leaderPosition.X + backX * FollowDistance + sideX * SideOffset * sideMultiplier,
                leaderPosition.Y + backY * FollowDistance + sideY * SideOffset * sideMultiplier,
                leaderPosition.Z
            );
        }

        /// <summary>
        /// Awards XP to all party members.
        /// </summary>
        /// <param name="xp">Amount of XP to award</param>
        /// <param name="split">Whether to split XP among members</param>
        public void AwardXP(int xp, bool split = false)
        {
            if (xp <= 0 || _activeParty.Count == 0)
            {
                return;
            }

            int xpPerMember = split ? xp / _activeParty.Count : xp;

            foreach (var member in _activeParty)
            {
                // Award XP through creature's stats component
                var stats = member.GetComponent<Odyssey.Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    stats.Experience += xpPerMember;
                }
            }
        }
    }
}
