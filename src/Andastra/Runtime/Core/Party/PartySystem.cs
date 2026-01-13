using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Party
{
    /// <summary>
    /// Manages the player's party.
    /// </summary>
    /// <remarks>
    /// KOTOR Party System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) party system
    /// - Located via string references: "PARTYTABLE" @ 0x007c1910, "Party" @ 0x007c24dc
    /// - "party" @ 0x007bda4c, "OnPartyDeath" @ 0x007bd9f4 (party member death event)
    /// - "PartyInteract" @ 0x007c1fc0 (party interaction system)
    /// - "SetByPlayerParty" @ 0x007c1d04 (party selection flag)
    /// - "NonPartyKillable" @ 0x007bdad8 (non-party killable flag)
    /// - GUI: "partyselect_p" @ 0x007cfe9c (party selection panel)
    /// - "CB_PARTYKILLED" @ 0x007d29e4 (party killed checkbox)
    /// - "Party Killed" @ 0x007c8470 (party killed message)
    /// - Error message: "While creating the party select screen, NPC %d's ID is Out of Synch with what's in the Client Side Party Table." @ 0x007cff20
    /// - Original implementation: 0x0057bd70 @ 0x0057bd70 (save PARTYTABLE.res to GFF)
    ///   - Original implementation (from decompiled 0x0057bd70):
    ///     - Creates GFF with "PT " signature (4 bytes) and "V2.0" version string
    ///     - Constructs file path: "PARTYTABLE\SAVEGAME\{saveName}" using format string "%06d - %s"
    ///     - Writes fields in this exact order:
    ///       - PT_PCNAME (string): Player character name from party system
    ///       - PT_GOLD (int32): Party gold amount
    ///       - PT_ITEM_COMPONENT (int32): Item component count
    ///       - PT_ITEM_CHEMICAL (int32): Item chemical count
    ///       - PT_SWOOP1, PT_SWOOP2, PT_SWOOP3 (int32): Swoop race times
    ///       - PT_XP_POOL (float): Experience point pool
    ///       - PT_PLAYEDSECONDS (int32): Total seconds played (calculated via 0x0057a300)
    ///       - PT_CONTROLLED_NPC (float): Currently controlled NPC (-1 = none)
    ///       - PT_SOLOMODE (byte): Solo mode flag (K2)
    ///       - PT_CHEAT_USED (byte): Cheat used flag
    ///       - PT_NUM_MEMBERS (byte): Number of active members (max 3)
    ///       - PT_MEMBERS (list): Active party members
    ///         - PT_MEMBER_ID (float): Member ID (-1 = PC, 0-11 = NPC slots)
    ///         - PT_IS_LEADER (byte): Whether member is leader (1 = leader, 0 = not)
    ///       - PT_NUM_PUPPETS (byte): Number of puppets
    ///       - PT_PUPPETS (list): Puppet object IDs
    ///         - PT_PUPPET_ID (float): Puppet object ID
    ///       - PT_AVAIL_PUPS (list, max 3): Available puppets
    ///         - PT_PUP_AVAIL (byte): Puppet available flag
    ///         - PT_PUP_SELECT (byte): Puppet selectable flag
    ///       - PT_AVAIL_NPCS (list, max 12): Available NPCs
    ///         - PT_NPC_AVAIL (byte): NPC available flag
    ///         - PT_NPC_SELECT (byte): NPC selectable flag
    ///       - PT_INFLUENCE (list, max 12, K2): Influence levels
    ///         - PT_NPC_INFLUENCE (float): Influence value (0-100)
    ///       - PT_AISTATE (float): AI state
    ///       - PT_FOLLOWSTATE (float): Follow state
    ///       - GlxyMap (struct): Galaxy map data
    ///         - GlxyMapNumPnts (int32): Number of points (16)
    ///         - GlxyMapPlntMsk (int32): Planet mask bitfield
    ///         - GlxyMapSelPnt (float): Selected point
    ///       - PT_PAZAAKCARDS (list, 23 entries): Pazaak cards
    ///         - PT_PAZAAKCOUNT (float): Card count
    ///       - PT_PAZSIDELIST (list, 10 entries): Pazaak side cards
    ///         - PT_PAZSIDECARD (float): Side card value
    ///       - PT_TUT_WND_SHOWN (byte array, 33 bytes): Tutorial windows shown flags
    ///       - PT_LAST_GUI_PNL (float): Last GUI panel
    ///       - FORFEITVIOL (float): Forfeit violation flag
    ///       - FORFEITCONDS (float): Forfeit conditions flag
    ///       - PT_FB_MSG_LIST (list): Feedback message list
    ///         - PT_FB_MSG_MSG (string): Message text
    ///         - PT_FB_MSG_TYPE (int32): Message type
    ///         - PT_FB_MSG_COLOR (byte): Message color
    ///       - PT_DLG_MSG_LIST (list): Dialogue message list
    ///         - PT_DLG_MSG_SPKR (string): Speaker name
    ///         - PT_DLG_MSG_MSG (string): Message text
    ///       - PT_COM_MSG_LIST (list): Combat message list
    ///         - PT_COM_MSG_MSG (string): Message text
    ///         - PT_COM_MSG_TYPE (int32): Message type
    ///         - PT_COM_MSG_COOR (byte): Message color
    ///       - PT_COST_MULT_LIST (list): Cost multiplier list
    ///         - PT_COST_MULT_VALUE (float): Multiplier value
    ///       - PT_DISABLEMAP (float): Disable map flag
    ///       - PT_DISABLEREGEN (float): Disable regen flag
    ///     - Saves GFF to file path via 0x0057a730
    /// - 0x0057dcd0 @ 0x0057dcd0 (load PARTYTABLE.res from GFF)
    ///   - Original implementation (from decompiled 0x0057dcd0):
    ///     - Loads GFF with "PT " signature from "PARTYTABLE" path
    ///     - Reads all fields in same order as save function
    ///     - Validates list sizes (PT_MEMBERS, PT_PUPPETS, PT_AVAIL_PUPS, PT_AVAIL_NPCS, PT_INFLUENCE)
    ///     - Falls back to PT_PLAYEDMINUTES if PT_PLAYEDSECONDS not found (converts to seconds)
    ///     - Restores party member states, available NPCs, puppets, influence levels
    ///     - Restores message lists (feedback, dialogue, combat), Pazaak cards, tutorial flags
    ///     - Calls 0x00579660 to initialize party system, 0x005798f0 to restore party state
    /// - 0x005a8220 @ 0x005a8220 (handle Party network message)
    /// - Party state stored in PARTYTABLE.res GFF file with "PT " signature
    /// - GFF structure fields (all located via string references):
    ///   - PT_PCNAME @ 0x007c1904: Player character name
    ///   - PT_GOLD @ 0x007c18fc: Party gold
    ///   - PT_ITEM_COMPONENT @ 0x007c18e8: Item component count
    ///   - PT_ITEM_CHEMICAL @ 0x007c18d4: Item chemical count
    ///   - PT_SWOOP1-3 @ 0x007c18c8, 0x007c18bc, 0x007c18b0: Swoop race times
    ///   - PT_XP_POOL @ 0x007c18a4: Experience point pool (float)
    ///   - PT_PLAYEDSECONDS @ 0x007c1890: Total seconds played
    ///   - PT_PLAYEDMINUTES @ 0x007c1934: Total minutes played (fallback)
    ///   - PT_CONTROLLED_NPC @ 0x007c187c: Currently controlled NPC (-1 = none, float)
    ///   - PT_SOLOMODE @ 0x007c1870: Solo mode flag (K2, byte)
    ///   - PT_CHEAT_USED @ 0x007c1860: Cheat used flag (byte)
    ///   - PT_NUM_MEMBERS @ 0x007c1850: Number of active members (max 3, byte)
    ///   - PT_MEMBERS @ 0x007c1844: List of active party members
    ///     - PT_MEMBER_ID @ 0x007c1834: Member ID (float, -1 = PC, 0-11 = NPC slots)
    ///     - PT_IS_LEADER @ 0x007c1824: Whether member is leader (byte)
    ///   - PT_NUM_PUPPETS @ 0x007c1814: Number of puppets (byte)
    ///   - PT_PUPPETS @ 0x007c1808: List of puppets
    ///     - PT_PUPPET_ID @ 0x007c17f8: Puppet object ID (float)
    ///   - PT_AVAIL_PUPS @ 0x007c17e8: Available puppets list (max 3)
    ///     - PT_PUP_AVAIL @ 0x007c17d8: Puppet available flag (byte)
    ///     - PT_PUP_SELECT @ 0x007c17c8: Puppet selectable flag (byte)
    ///   - PT_AVAIL_NPCS @ 0x007c17b8: Available NPCs list (max 12)
    ///     - PT_NPC_AVAIL @ 0x007c17a8: NPC available flag (byte)
    ///     - PT_NPC_SELECT @ 0x007c1798: NPC selectable flag (byte)
    ///   - PT_INFLUENCE @ 0x007c1788: Influence levels list (K2, max 12)
    ///     - PT_NPC_INFLUENCE @ 0x007c1774: Influence value (float, 0-100)
    ///   - PT_AISTATE @ 0x007c1768: AI state (float)
    ///   - PT_FOLLOWSTATE @ 0x007c1758: Follow state (float)
    ///   - PT_PAZAAKCARDS @ 0x007c1710: Pazaak cards list (23 entries)
    ///     - PT_PAZAAKCOUNT @ 0x007c1700: Card count (float)
    ///   - PT_PAZSIDELIST @ 0x007c16f0: Pazaak side list (10 entries)
    ///     - PT_PAZSIDECARD @ 0x007c16e0: Side card value (float)
    ///   - PT_TUT_WND_SHOWN @ 0x007c16cc: Tutorial windows shown (33 bytes)
    ///   - PT_LAST_GUI_PNL @ 0x007c16bc: Last GUI panel (float)
    ///   - PT_FB_MSG_LIST @ 0x007c1690: Feedback message list
    ///     - PT_FB_MSG_MSG @ 0x007c1680: Message text (string)
    ///     - PT_FB_MSG_TYPE @ 0x007c1670: Message type (int32)
    ///     - PT_FB_MSG_COLOR @ 0x007c1660: Message color (byte)
    ///   - PT_DLG_MSG_LIST @ 0x007c1650: Dialogue message list
    ///     - PT_DLG_MSG_SPKR @ 0x007c1640: Speaker name (string)
    ///     - PT_DLG_MSG_MSG @ 0x007c1630: Message text (string)
    ///   - PT_COM_MSG_LIST @ 0x007c1620: Combat message list
    ///     - PT_COM_MSG_MSG @ 0x007c1610: Message text (string)
    ///     - PT_COM_MSG_TYPE @ 0x007c1600: Message type (int32)
    ///     - PT_COM_MSG_COOR @ 0x007c15f0: Message color (byte)
    ///   - PT_COST_MULT_LIST @ 0x007c15dc: Cost multiplier list
    ///     - PT_COST_MULT_VALUE @ 0x007c15c8: Multiplier value (float)
    ///   - PT_DISABLEMAP @ 0x007c15b8: Disable map flag (float)
    ///   - PT_DISABLEREGEN @ 0x007c15a8: Disable regen flag (float)
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
        private IEntityTemplateFactory _templateFactory;
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

        public PartySystem(IWorld world) : this(world, null)
        {
        }

        public PartySystem(IWorld world, IEntityTemplateFactory templateFactory)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _templateFactory = templateFactory;
            _availableMembers = new List<PartyMember>();
            _activeParty = new List<PartyMember>();
            _leaderIndex = 0;
        }

        /// <summary>
        /// Sets the entity template factory for creating party members from templates.
        /// </summary>
        /// <param name="templateFactory">The template factory to use.</param>
        public void SetTemplateFactory(IEntityTemplateFactory templateFactory)
        {
            _templateFactory = templateFactory;
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
        /// Sets influence with a party member (K2 only).
        /// </summary>
        public void SetInfluence(PartyMember member, int influence)
        {
            if (member == null || member.IsPlayerCharacter)
            {
                return;
            }

            member.Influence = Math.Max(0, Math.Min(100, influence));
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

            // Create entity from member's template/resource
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) party member spawning system
            // Located via string references: Party member entity creation
            // Original implementation: Creates creature entity from UTC template stored in party member data
            // Party members use their TemplateResRef to spawn entities
            IEntity entity = null;

            // Try to create from template if available
            if (!string.IsNullOrEmpty(member.TemplateResRef) && _templateFactory != null)
            {
                // Use template factory to create entity from UTC template
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EntityFactory.CreateCreatureFromTemplate loads UTC GFF and creates entity
                // Located via string references: "TemplateResRef" @ 0x007bd00c
                // Original implementation: Loads UTC GFF, reads creature properties (Tag, FirstName, LastName, Appearance_Type,
                // FactionID, CurrentHitPoints, MaxHitPoints, ForcePoints, MaxForcePoints, ClassList, Str/Dex/Con/Int/Wis/Cha,
                // Scripts, Conversation), creates entity with all components properly initialized
                // Tag is set from entity template when entity is created via EntityFactory.CreateCreatureFromTemplate
                // The template (UTC file) contains the Tag field which is applied during entity creation
                entity = _templateFactory.CreateCreatureFromTemplate(member.TemplateResRef, spawnPosition, spawnFacing);
            }

            // Fallback: Create basic creature entity if template factory not available or template not found
            if (entity == null)
            {
                entity = _world.CreateEntity(Enums.ObjectType.Creature, spawnPosition, spawnFacing);

                // If we have a template ResRef but no factory, log a warning
                if (!string.IsNullOrEmpty(member.TemplateResRef) && _templateFactory == null)
                {
                    // Template factory not provided - entity will be a placeholder without template data
                    // This is expected when PartySystem is created without template factory (backward compatibility)
                }
            }

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
