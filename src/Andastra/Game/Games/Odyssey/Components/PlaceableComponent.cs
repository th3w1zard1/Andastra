using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Odyssey.Components
{
    /// <summary>
    /// Component for placeable entities (containers, furniture, etc.) in Odyssey engine.
    /// </summary>
    /// <remarks>
    /// Odyssey Placeable Component:
    /// - Inherits from BasePlaceableComponent (common functionality)
    /// - Odyssey-specific implementation for swkotor.exe and swkotor2.exe
    /// - LoadPlaceableFromGFF @ 0x00588010 (swkotor2.exe) - Loads placeable data from GIT GFF into placeable object (located via "Placeable List" @ 0x007bd260)
    ///   - Reads Tag, TemplateResRef, LocName, AutoRemoveKey, Faction, Invulnerable, Plot, NotBlastable, Min1HP, PartyInteract, OpenLockDC, OpenLockDiff, OpenLockDiffMod, KeyName, TrapDisarmable, TrapDetectable, DisarmDC, TrapDetectDC, OwnerDemolitionsSkill, TrapFlag, TrapOneShot, TrapType, Useable, Static, Appearance, UseTweakColor, TweakColor, HP, CurrentHP, and other placeable properties from GFF
    /// - SavePlaceableToGFF @ 0x00589520 (swkotor2.exe) - Saves placeable data to GFF save data (located via "Placeable List" @ 0x007bd260)
    ///   - Writes Tag, LocName, AutoRemoveKey, Faction, Plot, NotBlastable, Min1HP, OpenLockDC, OpenLockDiff, OpenLockDiffMod, KeyName, TrapDisarmable, TrapDetectable, DisarmDC, TrapDetectDC, OwnerDemolitionsSkill, TrapFlag, TrapOneShot, TrapType, Useable, Static, GroundPile, Appearance, UseTweakColor, TweakColor, HP, CurrentHP, Hardness, Fort, Will, Ref, Lockable, Locked, HasInventory, KeyRequired, CloseLockDC, Open, PartyInteract, Portrait, Conversation, BodyBag, DieWhenEmpty, LightState, Description, OnClosed, OnDamaged, OnDeath, OnDisarm, OnHeartbeat, OnInvDisturbed, OnLock, OnMeleeAttacked, OnOpen, OnSpellCastAt, OnUnlock, OnUsed, OnUserDefined, OnDialog, OnEndDialogue, OnTrapTriggered, OnFailToOpen, Animation, ItemList (ObjectId) for each item in placeable inventory, Bearing, position (X, Y, Z), IsBodyBag, IsBodyBagVisible, IsCorpse, PCLevel
    /// - Located via string references: "Placeable" @ 0x007bc530 (placeable object type), "Placeable List" @ 0x007bd260 (GFF list field in GIT)
    /// - "Placeables" @ 0x007c4bd0 (placeable objects), "placeableobjsnds" @ 0x007c4bf0 (placeable object sounds directory)
    /// - "placeable" @ 0x007ba030 (placeable tag prefix format)
    /// - Placeable effects: "fx_placeable01" @ 0x007c78b8 (placeable visual effects), "placeablelight" @ 0x007c78c8 (placeable lighting)
    /// - Error message: "CSWCAnimBasePlaceable::ServerToClientAnimation(): Failed to map server anim %i to client anim." @ 0x007d2330
    /// - Original implementation: FUN_004e08e0 @ 0x004e08e0 (load placeable instances from GIT)
    /// - Placeables have appearance, useability, locks, inventory, HP, traps
    /// - Based on UTP file format (GFF with "UTP " signature)
    /// - Script events: OnUsed (CSWSSCRIPTEVENT_EVENTTYPE_ON_USED @ 0x007bc7d8, 0x19), OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath
    /// - Script field names: "OnUsed" @ 0x007be1c4, "ScriptOnUsed" @ 0x007beeb8 (placeable script event fields)
    /// - Containers (HasInventory=true) can store items, open/close states (AnimationState 0=closed, 1=open)
    /// - Placeables can have visual effects and lighting attached (fx_placeable01, placeablelight)
    /// - Lock system: KeyRequired flag, KeyName tag, LockDC difficulty class (checked via Security skill)
    /// - Use distance: ~2.0 units (InteractRange), checked before OnUsed script fires
    /// - Odyssey-specific: Fort/Will/Ref saves, BodyBag, Plot flag, FactionId, AppearanceType, trap system
    /// </remarks>
    public class PlaceableComponent : BasePlaceableComponent
    {
        public PlaceableComponent()
        {
            TemplateResRef = string.Empty;
            KeyName = string.Empty;
            KeyTag = string.Empty;
        }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Appearance type (index into placeables.2da).
        /// </summary>
        /// <remarks>
        /// Appearance Type Property:
        /// - Odyssey-specific: Index into placeables.2da for placeable appearance
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "Appearance" field in UTP template (FUN_00588010 @ 0x00588010)
        /// </remarks>
        public int AppearanceType { get; set; }

        /// <summary>
        /// Current hit points (Odyssey-specific storage).
        /// </summary>
        public int CurrentHP { get; set; }

        /// <summary>
        /// Maximum hit points (Odyssey-specific storage).
        /// </summary>
        public int MaxHP { get; set; }

        /// <summary>
        /// Fortitude save (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// Fortitude Save Property:
        /// - Odyssey-specific: Fortitude save for placeable
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "Fort" field in UTP template (FUN_00589520 @ 0x00589520)
        /// </remarks>
        public int Fort { get; set; }

        /// <summary>
        /// Reflex save (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// Reflex Save Property:
        /// - Odyssey-specific: Reflex save for placeable
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "Ref" field in UTP template (FUN_00589520 @ 0x00589520)
        /// </remarks>
        public int Reflex { get; set; }

        /// <summary>
        /// Will save (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// Will Save Property:
        /// - Odyssey-specific: Will save for placeable
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "Will" field in UTP template (FUN_00589520 @ 0x00589520)
        /// </remarks>
        public int Will { get; set; }

        /// <summary>
        /// Whether a key is required (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// Key Required Property:
        /// - Odyssey-specific: Whether a key is required to unlock
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "KeyRequired" field in UTP template (FUN_00588010 @ 0x00588010)
        /// </remarks>
        public bool KeyRequired { get; set; }

        /// <summary>
        /// Key tag name (Odyssey-specific storage, maps to base KeyTag).
        /// </summary>
        public string KeyName
        {
            get { return KeyTag; }
            set { KeyTag = value; }
        }

        /// <summary>
        /// Whether the placeable is a container (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// Container Property:
        /// - Odyssey-specific: Whether placeable is a container (synonym for HasInventory)
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Container flag in placeable system
        /// </remarks>
        public bool IsContainer { get; set; }

        /// <summary>
        /// Faction ID (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// Faction ID Property:
        /// - Odyssey-specific: Faction ID for placeable
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "Faction" field in UTP template (FUN_00588010 @ 0x00588010, line 77)
        /// </remarks>
        public int FactionId { get; set; }

        /// <summary>
        /// Body bag placeable to spawn on destruction (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// Body Bag Property:
        /// - Odyssey-specific: Body bag placeable to spawn on destruction
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "BodyBag" field in UTP template (FUN_00589520 @ 0x00589520)
        /// </remarks>
        public int BodyBag { get; set; }

        /// <summary>
        /// Whether the placeable is plot-critical (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// Plot Property:
        /// - Odyssey-specific: Whether placeable is plot-critical
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "Plot" field in UTP template (FUN_00588010 @ 0x00588010, line 83)
        /// </remarks>
        public bool Plot { get; set; }

        /// <summary>
        /// IPlaceableComponent interface property mapping.
        /// </summary>
        public override int HitPoints
        {
            get { return CurrentHP; }
            set { CurrentHP = value; }
        }

        /// <summary>
        /// IPlaceableComponent interface property mapping.
        /// </summary>
        public override int MaxHitPoints
        {
            get { return MaxHP; }
            set { MaxHP = value; }
        }

        /// <summary>
        /// Activates the placeable (Odyssey-specific override).
        /// </summary>
        /// <remarks>
        /// Placeable Activation:
        /// - Odyssey-specific: Also checks IsContainer flag
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): OnUsed script event handling
        /// </remarks>
        public override void Activate()
        {
            // Placeable activation logic
            // For containers, this opens them
            if (HasInventory || IsContainer)
            {
                Open();
            }
        }

        /// <summary>
        /// Deactivates the placeable (Odyssey-specific override).
        /// </summary>
        /// <remarks>
        /// Placeable Deactivation:
        /// - Odyssey-specific: Also checks IsContainer flag
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Placeable deactivation handling
        /// </remarks>
        public override void Deactivate()
        {
            // Placeable deactivation logic
            if (HasInventory || IsContainer)
            {
                Close();
            }
        }
    }
}
