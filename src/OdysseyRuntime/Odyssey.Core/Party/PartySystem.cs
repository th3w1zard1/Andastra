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
    /// - Based on swkotor2.exe party system
    /// - Located via string references: "PARTYTABLE" @ 0x007c1910, "Party" @ 0x007c24dc
    /// - Original implementation: FUN_0057bd70 @ 0x0057bd70 (save PARTYTABLE.res to GFF)
    /// - FUN_0057dcd0 @ 0x0057dcd0 (load PARTYTABLE.res from GFF)
    /// - FUN_005a8220 @ 0x005a8220 (handle Party network message)
    /// - Party state stored in PARTYTABLE.res GFF file with "PT " signature
    /// - GFF structure fields:
    ///   - PT_PCNAME: Player character name
    ///   - PT_GOLD: Party gold
    ///   - PT_MEMBERS: List of active party members (PT_MEMBER_ID, PT_IS_LEADER)
    ///   - PT_NUM_MEMBERS: Number of active members (max 3)
    ///   - PT_PUPPETS: List of puppets (PT_PUPPET_ID) - controlled NPCs
    ///   - PT_NUM_PUPPETS: Number of puppets
    ///   - PT_AVAIL_PUPS: Available puppets list (PT_PUP_AVAIL, PT_PUP_SELECT) - max 3
    ///   - PT_AVAIL_NPCS: Available NPCs list (PT_NPC_AVAIL, PT_NPC_SELECT) - max 12 (0xc)
    ///   - PT_INFLUENCE: Influence levels (PT_NPC_INFLUENCE) - K2 only, 12 NPCs
    ///   - PT_SOLOMODE: Solo mode flag (K2)
    ///   - PT_CONTROLLED_NPC: Currently controlled NPC (-1 = none)
    ///   - PT_XP_POOL: Experience point pool
    ///   - PT_AISTATE: AI state
    ///   - PT_FOLLOWSTATE: Follow state
    /// - PC is always party slot 0 in PT_MEMBERS
    /// - Max 3 active party members in field (PT_NUM_MEMBERS)
    /// - Max 12 available NPCs (PT_AVAIL_NPCS list size)
    /// - NPCs persist state when not in active party
    /// 
    /// K2 additions:
    /// - Influence system (PT_INFLUENCE)
    /// - Remote party member control (PT_PUPPETS)
    /// - Solo mode (PT_SOLOMODE)
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
            foreach (PartyMember member in _availableMembers)
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
                    PartyMember member = _availableMembers[i];

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
            foreach (PartyMember member in _availableMembers)
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

            foreach (PartyMember member in _availableMembers)
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
            PartyMember member = GetMemberByEntity(entity);
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
            PartyMember member = GetMemberByEntity(entity);
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
                    PartyMember member = _activeParty[i];
                    _activeParty.RemoveAt(i);
                    member.IsInActiveParty = false;
                    DespawnMemberFromWorld(member);
                }
            }

            // Add new selections
            if (npcSlot1 >= 0)
            {
                PartyMember member1 = GetAvailableMemberBySlot(npcSlot1);
                if (member1 != null)
                {
                    AddToActiveParty(member1);
                }
            }

            if (npcSlot2 >= 0)
            {
                PartyMember member2 = GetAvailableMemberBySlot(npcSlot2);
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

            foreach (PartyMember member in _activeParty)
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
            foreach (PartyMember member in _activeParty)
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

            Vector3 leaderPos = GetLeaderPosition();
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
            PartyMember leader = Leader;
            if (leader == null || leader.Entity == null)
            {
                return Vector3.Zero;
            }

            Interfaces.Components.ITransformComponent transform = leader.Entity.GetComponent<Interfaces.Components.ITransformComponent>();
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

            foreach (PartyMember member in _availableMembers)
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
            if (member == null || member.Entity != null)
            {
                // Member already has an entity or is invalid
                return;
            }

            // Get leader position for formation
            PartyMember leader = Leader;
            if (leader == null || leader.Entity == null)
            {
                // No leader, cannot spawn at formation position
                return;
            }

            Core.Interfaces.Components.ITransformComponent leaderTransform = leader.Entity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            if (leaderTransform == null)
            {
                return;
            }

            // Calculate formation position relative to leader
            // Formation: Leader at center, members offset by formation distance
            int memberIndex = _activeParty.IndexOf(member);
            float formationDistance = 2.0f; // Default formation distance
            float angleOffset = (memberIndex - _leaderIndex) * 1.0f; // Angle offset in radians

            System.Numerics.Vector3 formationOffset = new System.Numerics.Vector3(
                (float)Math.Cos(angleOffset) * formationDistance,
                0.0f,
                (float)Math.Sin(angleOffset) * formationDistance
            );

            System.Numerics.Vector3 spawnPosition = leaderTransform.Position + formationOffset;
            float spawnFacing = leaderTransform.Facing;

            // TODO: Create entity from member's template/resource
            // For now, create a basic creature entity
            // Full implementation would load from UTC template stored in member data
            IEntity entity = _world.CreateEntity(Enums.ObjectType.Creature, spawnPosition, spawnFacing);
            if (entity != null)
            {
                member.UpdateEntity(entity);
            }
        }

        private void DespawnMemberFromWorld(PartyMember member)
        {
            if (member == null || member.Entity == null)
            {
                return;
            }

            // Preserve member state before removing entity
            // State is already stored in PartyMember properties, so we just need to remove the entity
            // The entity's components and data are preserved in the member's state
            
            // Preserve member state before removing entity
            // State is already stored in PartyMember properties, so we just need to remove the entity
            // The entity's components and data are preserved in the member's state
            
            // Remove entity from world
            uint entityId = member.Entity.ObjectId;
            _world.DestroyEntity(entityId);
            
            // Note: Entity reference remains in PartyMember but the entity is destroyed
            // When respawning, UpdateEntity will be called with a new entity
            // This preserves the member's state while removing the entity from the world
        }

        #endregion
    }
}
