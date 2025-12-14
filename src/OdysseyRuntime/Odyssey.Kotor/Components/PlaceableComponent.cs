using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for placeable entities (containers, furniture, etc.).
    /// </summary>
    /// <remarks>
    /// Based on UTP file format documentation.
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
    }
}
