using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Interfaces.Components;

namespace BioWareEngines.Kotor.Components
{
    /// <summary>
    /// Component for placeable entities (containers, furniture, etc.).
    /// </summary>
    /// <remarks>
    /// Placeable Component:
    /// - Based on swkotor2.exe placeable system
    /// - Located via string references: "Placeable" @ 0x007bc530 (placeable object type), "Placeable List" @ 0x007bd260 (GFF list field in GIT)
    /// - "Placeables" @ 0x007c4bd0 (placeable objects), "placeableobjsnds" @ 0x007c4bf0 (placeable object sounds directory)
    /// - "placeable" @ 0x007ba030 (placeable tag prefix format)
    /// - Placeable effects: "fx_placeable01" @ 0x007c78b8 (placeable visual effects), "placeablelight" @ 0x007c78c8 (placeable lighting)
    /// - Error message: "CSWCAnimBasePlaceable::ServerToClientAnimation(): Failed to map server anim %i to client anim." @ 0x007d2330
    /// - Original implementation: FUN_00585ec0 @ 0x00585ec0 (save placeable data to GFF)
    /// - FUN_004e08e0 @ 0x004e08e0 (load placeable instances from GIT)
    /// - FUN_00586e00 @ 0x00586e00 loads placeable properties from UTP template
    /// - Placeables have appearance, useability, locks, inventory, HP, traps
    /// - Based on UTP file format (GFF with "UTP " signature)
    /// - Script events: OnUsed (CSWSSCRIPTEVENT_EVENTTYPE_ON_USED @ 0x007bc7d8, 0x19), OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath
    /// - Script field names: "OnUsed" @ 0x007be1c4, "ScriptOnUsed" @ 0x007beeb8 (placeable script event fields)
    /// - Containers (HasInventory=true) can store items, open/close states (AnimationState 0=closed, 1=open)
    /// - Placeables can have visual effects and lighting attached (fx_placeable01, placeablelight)
    /// - Lock system: KeyRequired flag, KeyName tag, LockDC difficulty class (checked via Security skill)
    /// - Use distance: ~2.0 units (InteractRange), checked before OnUsed script fires
    /// </remarks>
    public class PlaceableComponent : IPlaceableComponent
    {
        public IEntity Owner { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }

        public PlaceableComponent()
        {
            TemplateResRef = string.Empty;
            KeyName = string.Empty;
        }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Appearance type (index into placeables.2da).
        /// </summary>
        public int AppearanceType { get; set; }

        /// <summary>
        /// Current hit points.
        /// </summary>
        public int CurrentHP { get; set; }

        /// <summary>
        /// Maximum hit points.
        /// </summary>
        public int MaxHP { get; set; }

        /// <summary>
        /// Hardness (damage reduction).
        /// </summary>
        public int Hardness { get; set; }

        /// <summary>
        /// Fortitude save.
        /// </summary>
        public int Fort { get; set; }

        /// <summary>
        /// Reflex save.
        /// </summary>
        public int Reflex { get; set; }

        /// <summary>
        /// Will save.
        /// </summary>
        public int Will { get; set; }

        /// <summary>
        /// Whether the placeable is useable.
        /// </summary>
        public bool IsUseable { get; set; }

        /// <summary>
        /// Whether the placeable is locked.
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// Lock difficulty class.
        /// </summary>
        public int LockDC { get; set; }

        /// <summary>
        /// Whether a key is required.
        /// </summary>
        public bool KeyRequired { get; set; }

        /// <summary>
        /// Key tag name.
        /// </summary>
        public string KeyName { get; set; }

        /// <summary>
        /// Whether the placeable is a container.
        /// </summary>
        public bool IsContainer { get; set; }

        /// <summary>
        /// Whether the placeable has inventory.
        /// </summary>
        public bool HasInventory { get; set; }

        /// <summary>
        /// Whether the placeable is static (no interaction).
        /// </summary>
        public bool IsStatic { get; set; }

        /// <summary>
        /// Whether the placeable is currently open.
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// Current animation state.
        /// </summary>
        public int AnimationState { get; set; }

        /// <summary>
        /// Faction ID.
        /// </summary>
        public int FactionId { get; set; }

        /// <summary>
        /// Conversation file.
        /// </summary>
        public string Conversation { get; set; }

        /// <summary>
        /// Body bag placeable to spawn on destruction.
        /// </summary>
        public int BodyBag { get; set; }

        /// <summary>
        /// Whether the placeable is plot-critical.
        /// </summary>
        public bool Plot { get; set; }

        /// <summary>
        /// Key tag (alias for KeyName for interface compatibility).
        /// </summary>
        public string KeyTag
        {
            get { return KeyName; }
            set { KeyName = value; }
        }

        /// <summary>
        /// Unlocks the placeable.
        /// </summary>
        public void Unlock()
        {
            IsLocked = false;
        }

        /// <summary>
        /// Opens the placeable (for containers).
        /// </summary>
        public void Open()
        {
            IsOpen = true;
            AnimationState = 1; // Open state
        }

        /// <summary>
        /// Closes the placeable.
        /// </summary>
        public void Close()
        {
            IsOpen = false;
            AnimationState = 0; // Closed state
        }

        /// <summary>
        /// Activates the placeable.
        /// </summary>
        public void Activate()
        {
            // Placeable activation logic
            // For containers, this opens them
            if (HasInventory || IsContainer)
            {
                Open();
            }
        }

        /// <summary>
        /// Deactivates the placeable.
        /// </summary>
        public void Deactivate()
        {
            // Placeable deactivation logic
            if (HasInventory || IsContainer)
            {
                Close();
            }
        }

        // IPlaceableComponent interface properties
        public int HitPoints
        {
            get { return CurrentHP; }
            set { CurrentHP = value; }
        }

        public int MaxHitPoints
        {
            get { return MaxHP; }
            set { MaxHP = value; }
        }
    }
}
