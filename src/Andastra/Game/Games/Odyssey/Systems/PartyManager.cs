using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common.Systems;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Odyssey.Systems.PerceptionManager
{
    /// <summary>
    /// Event arguments for party changes (Odyssey-specific, extends base).
    /// </summary>
    public class OdysseyPartyChangedEventArgs : PartyChangedEventArgs
    {
        // Odyssey-specific party change data can be added here if needed
    }

    /// <summary>
    /// Manages the player's party in Odyssey engine (KOTOR 1/2).
    /// </summary>
    /// <remarks>
    /// Party Management System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) party system
    /// - Located via string references: "PARTYTABLE" @ 0x007c1910, "Party" @ 0x007c24dc
    /// - "PartyInteract" @ 0x007c1fc0, "SetByPlayerParty" @ 0x007c1d04
    /// - "OnPartyDeath" @ 0x007bd9f4, "CB_PARTYKILLED" @ 0x007d29e4
    /// - Original implementation: Party state stored in PARTYTABLE.res GFF file (see SaveSerializer)
    /// - KOTOR party rules:
    ///   - Maximum 3 active party members (including PC)
    ///   - Up to 9 available party members can be recruited
    ///   - Party selection UI shows available members
    ///   - Party members follow the leader
    ///   - Party members share XP
    ///   - NPCs in party table are stored by NPC index (0-8)
    ///
    /// Party formation:
    /// - Leader (slot 0): Player character
    /// - Member 1 (slot 1): First party member
    /// - Member 2 (slot 2): Second party member
    ///
    /// Key 2DA: partytable.2da defines available party members
    /// </remarks>
    public class PartyManager : BasePartyManager
    {
        /// <summary>
        /// Maximum available party members (NPCs that can be recruited).
        /// </summary>
        public const int MaxAvailableMembers = 9;

        private readonly Dictionary<int, IEntity> _availableMembers;
        private readonly HashSet<int> _selectedMembers;

        public PartyManager(IWorld world)
            : base(world)
        {
            _availableMembers = new Dictionary<int, IEntity>();
            _selectedMembers = new HashSet<int>();
        }

        /// <summary>
        /// Maximum active party size (including PC).
        /// </summary>
        public override int MaxActivePartySize => 3;


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
        /// Sets the party leader (PC) - Odyssey-specific override.
        /// </summary>
        public override void SetLeader(IEntity leader)
        {
            if (leader == null)
            {
                throw new ArgumentNullException(nameof(leader));
            }

            if (ActivePartyList.Count == 0)
            {
                ActivePartyList.Add(leader);
            }
            else
            {
                IEntity oldLeader = ActivePartyList[0];
                ActivePartyList[0] = leader;

                if (oldLeader != leader)
                {
                    FireOnLeaderChanged(new PartyChangedEventArgs
                    {
                        Member = leader,
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

            if (ActivePartyList.Count >= MaxActivePartySize)
            {
                return false; // Party full
            }

            IEntity member = _availableMembers[npcIndex];
            ActivePartyList.Add(member);
            _selectedMembers.Add(npcIndex);

            FireOnPartyChanged(new PartyChangedEventArgs
            {
                Member = member,
                Slot = ActivePartyList.Count - 1,
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
            int slot = ActivePartyList.IndexOf(member);

            ActivePartyList.Remove(member);
            _selectedMembers.Remove(npcIndex);

            FireOnPartyChanged(new PartyChangedEventArgs
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
            if (slot >= 0 && slot < ActivePartyList.Count)
            {
                return ActivePartyList[slot];
            }
            return null;
        }

        /// <summary>
        /// Gets the slot index for a party member.
        /// </summary>
        /// <returns>Slot index (0-2) or -1 if not in party</returns>
        public int GetMemberSlot(IEntity member)
        {
            return ActivePartyList.IndexOf(member);
        }

        /// <summary>
        /// Checks if an entity is in the active party.
        /// </summary>
        public bool IsInParty(IEntity entity)
        {
            return ActivePartyList.Contains(entity);
        }

        /// <summary>
        /// Gets the NPC index for a party member.
        /// </summary>
        /// <returns>NPC index (0-8) or -1 if not found</returns>
        public int GetNpcIndex(IEntity member)
        {
            foreach (KeyValuePair<int, IEntity> kvp in _availableMembers)
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
            for (int i = ActivePartyList.Count - 1; i >= 0; i--)
            {
                IEntity member = ActivePartyList[i];
                ActivePartyList.RemoveAt(i);

                // Find and remove from selected
                foreach (KeyValuePair<int, IEntity> kvp in _availableMembers)
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
            if (xp <= 0 || ActivePartyList.Count == 0)
            {
                return;
            }

            int xpPerMember = split ? xp / ActivePartyList.Count : xp;

            foreach (IEntity member in ActivePartyList)
            {
                // Award XP through creature's stats component
                Components.StatsComponent stats = member.GetComponent<Components.StatsComponent>();
                if (stats != null)
                {
                    stats.Experience += xpPerMember;
                }
            }
        }

        /// <summary>
        /// Adds a member to the active party (base class implementation).
        /// </summary>
        public override bool AddMember(IEntity member, int slot = -1)
        {
            if (member == null)
            {
                return false;
            }

            // Find NPC index for this member
            int npcIndex = GetNpcIndex(member);
            if (npcIndex >= 0)
            {
                return SelectMember(npcIndex);
            }

            // If not in available members, add directly (for PC or custom members)
            if (ActivePartyList.Count >= MaxActivePartySize)
            {
                return false;
            }

            if (slot < 0)
            {
                ActivePartyList.Add(member);
                slot = ActivePartyList.Count - 1;
            }
            else
            {
                if (slot >= ActivePartyList.Count)
                {
                    ActivePartyList.Add(member);
                }
                else
                {
                    ActivePartyList.Insert(slot, member);
                }
            }

            FireOnPartyChanged(new PartyChangedEventArgs
            {
                Member = member,
                Slot = slot,
                Added = true
            });

            return true;
        }

        /// <summary>
        /// Removes a member from the active party (base class implementation).
        /// </summary>
        public override bool RemoveMember(IEntity member)
        {
            if (member == null)
            {
                return false;
            }

            int slot = ActivePartyList.IndexOf(member);
            if (slot < 0)
            {
                return false;
            }

            // If it's an NPC, deselect it
            int npcIndex = GetNpcIndex(member);
            if (npcIndex >= 0)
            {
                DeselectMember(npcIndex);
            }
            else
            {
                ActivePartyList.RemoveAt(slot);
                FireOnPartyChanged(new PartyChangedEventArgs
                {
                    Member = member,
                    Slot = slot,
                    Added = false
                });
            }

            return true;
        }
    }
}
