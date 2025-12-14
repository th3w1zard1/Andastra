using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Party
{
    /// <summary>
    /// Manages the player's party.
    /// </summary>
    /// <remarks>
    /// KOTOR Party System:
    /// - PC is always party slot 0
    /// - Max 3 active party members in field
    /// - Max 9-10 available party members
    /// - Party NPCs stored in PARTYTABLE.res
    /// - NPCs persist state when not in active party
    /// 
    /// K2 additions:
    /// - Influence system
    /// - Remote party member control
    /// - Solo mode
    /// </remarks>
    public class PartySystem
    {
        /// <summary>
        /// Maximum active party members in field (including PC).
        /// </summary>
        public const int MaxActiveParty = 3;

        /// <summary>
        /// Maximum available NPCs.
        /// </summary>
        public const int MaxAvailableNPCs = 10;

        private readonly IWorld _world;
        private readonly List<PartyMember> _availableMembers;
        private readonly List<PartyMember> _activeParty;

        private PartyMember _playerCharacter;
        private int _leaderIndex;

        /// <summary>
        /// Current party gold.
        /// </summary>
        public int Gold { get; set; }

        /// <summary>
        /// Current party XP.
        /// </summary>
        public int ExperiencePoints { get; set; }

        /// <summary>
        /// Whether solo mode is active (K2 only).
        /// </summary>
        public bool SoloMode { get; set; }

        /// <summary>
        /// Event fired when party composition changes.
        /// </summary>
        public event Action<PartyMember> OnMemberAdded;

        /// <summary>
        /// Event fired when party member removed.
        /// </summary>
        public event Action<PartyMember> OnMemberRemoved;

        /// <summary>
        /// Event fired when leader changes.
        /// </summary>
        public event Action<PartyMember> OnLeaderChanged;

        /// <summary>
        /// Event fired when gold changes.
        /// </summary>
        public event Action<int, int> OnGoldChanged;

        public PartySystem(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _availableMembers = new List<PartyMember>();
            _activeParty = new List<PartyMember>();
            _leaderIndex = 0;
        }

        #region Player Character

        /// <summary>
        /// Gets the player character.
        /// </summary>
        public PartyMember PlayerCharacter
        {
            get { return _playerCharacter; }
        }

        /// <summary>
        /// Sets the player character.
        /// </summary>
        public void SetPlayerCharacter(IEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            _playerCharacter = new PartyMember(entity, true);
            _playerCharacter.IsAvailable = true;
            _playerCharacter.IsSelectable = true;

            // PC is always in active party
            if (!_activeParty.Contains(_playerCharacter))
            {
                _activeParty.Insert(0, _playerCharacter);
            }
        }

        #endregion

        #region Available Members

        /// <summary>
        /// Gets all available party members (excluding PC).
        /// </summary>
        public IReadOnlyList<PartyMember> AvailableMembers
        {
            get { return _availableMembers.AsReadOnly(); }
        }

        /// <summary>
        /// Adds an NPC to available party members.
        /// </summary>
        public void AddAvailableMember(IEntity entity, int npcSlot = -1)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            // Check if already available
            foreach (var member in _availableMembers)
            {
                if (member.Entity.ObjectId == entity.ObjectId)
                {
                    return; // Already available
                }
            }

            if (_availableMembers.Count >= MaxAvailableNPCs)
            {
                throw new InvalidOperationException("Maximum available NPCs reached");
            }

            var newMember = new PartyMember(entity, false);
            newMember.NPCSlot = npcSlot >= 0 ? npcSlot : _availableMembers.Count;
            newMember.IsAvailable = true;
            newMember.IsSelectable = true;

            _availableMembers.Add(newMember);

            if (OnMemberAdded != null)
            {
                OnMemberAdded(newMember);
            }
        }

        /// <summary>
        /// Removes an NPC from available party members.
        /// </summary>
        public bool RemoveAvailableMember(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            for (int i = _availableMembers.Count - 1; i >= 0; i--)
            {
                if (_availableMembers[i].Entity.ObjectId == entity.ObjectId)
                {
                    var member = _availableMembers[i];

                    // Remove from active party if present
                    RemoveFromActiveParty(member);

                    _availableMembers.RemoveAt(i);

                    if (OnMemberRemoved != null)
                    {
                        OnMemberRemoved(member);
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets available member by NPC slot number.
        /// </summary>
        public PartyMember GetAvailableMemberBySlot(int npcSlot)
        {
            foreach (var member in _availableMembers)
            {
                if (member.NPCSlot == npcSlot)
                {
                    return member;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets available member by tag.
        /// </summary>
        public PartyMember GetAvailableMemberByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            foreach (var member in _availableMembers)
            {
                if (string.Equals(member.Entity.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return member;
                }
            }
            return null;
        }

        /// <summary>
        /// Sets whether an NPC is available for selection.
        /// </summary>
        public void SetAvailability(IEntity entity, bool available)
        {
            var member = GetMemberByEntity(entity);
            if (member != null)
            {
                member.IsAvailable = available;
            }
        }

        /// <summary>
        /// Sets whether an NPC can be selected (locked out temporarily).
        /// </summary>
        public void SetSelectability(IEntity entity, bool selectable)
        {
            var member = GetMemberByEntity(entity);
            if (member != null)
            {
                member.IsSelectable = selectable;
            }
        }

        #endregion

        #region Active Party

        /// <summary>
        /// Gets the active party (in field).
        /// </summary>
        public IReadOnlyList<PartyMember> ActiveParty
        {
            get { return _activeParty.AsReadOnly(); }
        }

        /// <summary>
        /// Gets the current party leader.
        /// </summary>
        public PartyMember Leader
        {
            get
            {
                if (_activeParty.Count == 0)
                {
                    return null;
                }
                if (_leaderIndex >= _activeParty.Count)
                {
                    _leaderIndex = 0;
                }
                return _activeParty[_leaderIndex];
            }
        }

        /// <summary>
        /// Adds member to active party.
        /// </summary>
        public bool AddToActiveParty(PartyMember member)
        {
            if (member == null)
            {
                return false;
            }

            if (!member.IsAvailable || !member.IsSelectable)
            {
                return false;
            }

            if (_activeParty.Count >= MaxActiveParty)
            {
                return false;
            }

            if (_activeParty.Contains(member))
            {
                return false;
            }

            _activeParty.Add(member);
            member.IsInActiveParty = true;

            // Spawn entity in world if not present
            SpawnMemberInWorld(member);

            return true;
        }

        /// <summary>
        /// Removes member from active party.
        /// </summary>
        public bool RemoveFromActiveParty(PartyMember member)
        {
            if (member == null || member.IsPlayerCharacter)
            {
                return false; // Cannot remove PC
            }

            int index = _activeParty.IndexOf(member);
            if (index < 0)
            {
                return false;
            }

            _activeParty.RemoveAt(index);
            member.IsInActiveParty = false;

            // Adjust leader index
            if (_leaderIndex >= _activeParty.Count)
            {
                _leaderIndex = 0;
            }

            // Despawn from world
            DespawnMemberFromWorld(member);

            return true;
        }

        /// <summary>
        /// Sets the active party selection.
        /// </summary>
        /// <param name="npcSlot1">First NPC slot (-1 for none).</param>
        /// <param name="npcSlot2">Second NPC slot (-1 for none).</param>
        public void SetPartySelection(int npcSlot1, int npcSlot2)
        {
            // Clear current party (except PC)
            for (int i = _activeParty.Count - 1; i >= 0; i--)
            {
                if (!_activeParty[i].IsPlayerCharacter)
                {
                    var member = _activeParty[i];
                    _activeParty.RemoveAt(i);
                    member.IsInActiveParty = false;
                    DespawnMemberFromWorld(member);
                }
            }

            // Add new selections
            if (npcSlot1 >= 0)
            {
                var member1 = GetAvailableMemberBySlot(npcSlot1);
                if (member1 != null)
                {
                    AddToActiveParty(member1);
                }
            }

            if (npcSlot2 >= 0)
            {
                var member2 = GetAvailableMemberBySlot(npcSlot2);
                if (member2 != null)
                {
                    AddToActiveParty(member2);
                }
            }

            // Reset leader to PC
            _leaderIndex = 0;
        }

        /// <summary>
        /// Cycles to the next party leader.
        /// </summary>
        public void CycleLeader()
        {
            if (_activeParty.Count <= 1)
            {
                return;
            }

            int newIndex = (_leaderIndex + 1) % _activeParty.Count;
            SetLeader(newIndex);
        }

        /// <summary>
        /// Sets the party leader by index.
        /// </summary>
        public void SetLeader(int partyIndex)
        {
            if (partyIndex < 0 || partyIndex >= _activeParty.Count)
            {
                return;
            }

            if (_leaderIndex == partyIndex)
            {
                return;
            }

            _leaderIndex = partyIndex;

            if (OnLeaderChanged != null)
            {
                OnLeaderChanged(Leader);
            }
        }

        /// <summary>
        /// Checks if an entity is in the active party.
        /// </summary>
        public bool IsInParty(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            foreach (var member in _activeParty)
            {
                if (member.Entity.ObjectId == entity.ObjectId)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Gold/XP Management

        /// <summary>
        /// Adds gold to party.
        /// </summary>
        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            int oldGold = Gold;
            Gold += amount;

            if (OnGoldChanged != null)
            {
                OnGoldChanged(oldGold, Gold);
            }
        }

        /// <summary>
        /// Removes gold from party.
        /// </summary>
        /// <returns>True if had enough gold.</returns>
        public bool RemoveGold(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (Gold < amount)
            {
                return false;
            }

            int oldGold = Gold;
            Gold -= amount;

            if (OnGoldChanged != null)
            {
                OnGoldChanged(oldGold, Gold);
            }

            return true;
        }

        /// <summary>
        /// Awards XP to party.
        /// </summary>
        public void AwardXP(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            ExperiencePoints += amount;

            // Distribute to party members
            // In KOTOR, XP is shared equally among active party
            int xpPerMember = amount / _activeParty.Count;
            foreach (var member in _activeParty)
            {
                member.AwardXP(xpPerMember);
            }
        }

        #endregion

        #region Influence (K2)

        /// <summary>
        /// Gets influence with a party member (K2 only).
        /// </summary>
        public int GetInfluence(PartyMember member)
        {
            if (member == null)
            {
                return 50;
            }
            return member.Influence;
        }

        /// <summary>
        /// Modifies influence with a party member (K2 only).
        /// </summary>
        public void ModifyInfluence(PartyMember member, int delta)
        {
            if (member == null || member.IsPlayerCharacter)
            {
                return;
            }

            member.Influence = Math.Max(0, Math.Min(100, member.Influence + delta));
        }

        #endregion

        #region Formation

        /// <summary>
        /// Gets formation positions relative to leader.
        /// </summary>
        public Vector3[] GetFormationPositions()
        {
            var positions = new Vector3[_activeParty.Count];

            if (_activeParty.Count == 0)
            {
                return positions;
            }

            var leaderPos = GetLeaderPosition();
            positions[_leaderIndex] = leaderPos;

            // Calculate follower positions
            float spacing = 2.0f; // Base spacing
            int followerIndex = 0;

            for (int i = 0; i < _activeParty.Count; i++)
            {
                if (i == _leaderIndex)
                {
                    continue;
                }

                // Simple follow formation: behind and to the side
                float angle = (float)Math.PI + (followerIndex - 0.5f) * 0.5f;
                float offsetX = (float)Math.Cos(angle) * spacing;
                float offsetY = (float)Math.Sin(angle) * spacing;

                positions[i] = new Vector3(
                    leaderPos.X + offsetX,
                    leaderPos.Y + offsetY,
                    leaderPos.Z
                );

                followerIndex++;
            }

            return positions;
        }

        private Vector3 GetLeaderPosition()
        {
            var leader = Leader;
            if (leader == null || leader.Entity == null)
            {
                return Vector3.Zero;
            }

            var transform = leader.Entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                return transform.Position;
            }

            return Vector3.Zero;
        }

        #endregion

        #region Private Helpers

        private PartyMember GetMemberByEntity(IEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            if (_playerCharacter != null && _playerCharacter.Entity.ObjectId == entity.ObjectId)
            {
                return _playerCharacter;
            }

            foreach (var member in _availableMembers)
            {
                if (member.Entity.ObjectId == entity.ObjectId)
                {
                    return member;
                }
            }

            return null;
        }

        private void SpawnMemberInWorld(PartyMember member)
        {
            // TODO: Integrate with area/module system
            // Spawn entity at formation position relative to leader
        }

        private void DespawnMemberFromWorld(PartyMember member)
        {
            // TODO: Remove entity from world but preserve state
        }

        #endregion
    }
}
