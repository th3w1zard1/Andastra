using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common.Components
{
    /// <summary>
    /// Base implementation of item component functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Item Component Implementation:
    /// - Common item properties and methods across all engines
    /// - Handles base item data, properties, charges, stack size, cost, identification
    /// - Provides base for engine-specific item component implementations
    /// - Cross-engine analysis: All engines share common item structure patterns
    /// - Common functionality: BaseItem, StackSize, Charges, Cost, Identified, TemplateResRef, Properties, Upgrades
    /// - Engine-specific: File format details, upgrade systems, property calculations, event handling
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Item component system with UTI template loading
    /// - swkotor2.exe: Enhanced item system with upgrade support (FUN_005fb0f0 @ 0x005fb0f0 loads item templates)
    /// - nwmain.exe: Aurora item system using identical UTI format to Odyssey
    /// - daorigins.exe: Eclipse item system with enhanced property system
    /// - DragonAge2.exe: Enhanced Eclipse item system
    /// - /: Infinity item system
    ///
    /// Common structure across engines:
    /// - BaseItem (int): Item type ID from baseitems.2da or equivalent table
    /// - StackSize (int): Current stack quantity (1 = not stackable)
    /// - Charges (int): Number of uses remaining (-1 = unlimited)
    /// - Cost (int): Base item value for trading/selling
    /// - Identified (bool): Whether item has been identified
    /// - TemplateResRef (string): Template resource reference
    /// - Properties (List): Item properties/enchantments that modify behavior
    /// - Upgrades (List): Item upgrades (engine-specific implementation)
    ///
    /// Common item events across engines:
    /// - OnAcquire: Fired when item is acquired
    /// - OnLose: Fired when item is lost
    /// - OnEquip: Fired when item is equipped
    /// - OnActivate: Fired when item is activated/used
    /// </remarks>
    [PublicAPI]
    public abstract class BaseItemComponent : IItemComponent
    {
        protected readonly List<ItemProperty> _properties;
        protected readonly List<ItemUpgrade> _upgrades;

        /// <summary>
        /// The entity this component is attached to.
        /// </summary>
        public IEntity Owner { get; set; }

        /// <summary>
        /// Base item type ID (from baseitems.2da or equivalent table).
        /// </summary>
        public int BaseItem { get; set; }

        /// <summary>
        /// Stack size (for stackable items).
        /// </summary>
        public int StackSize { get; set; }

        /// <summary>
        /// Number of charges remaining (for items with charges).
        /// -1 indicates unlimited charges.
        /// </summary>
        public int Charges { get; set; }

        /// <summary>
        /// Item cost (base price).
        /// </summary>
        public int Cost { get; set; }

        /// <summary>
        /// Whether the item is identified.
        /// </summary>
        public bool Identified { get; set; }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Item properties (effects, bonuses, etc.).
        /// </summary>
        public IReadOnlyList<ItemProperty> Properties
        {
            get { return _properties; }
        }

        /// <summary>
        /// Item upgrades (crystals, modifications, etc.).
        /// </summary>
        public IReadOnlyList<ItemUpgrade> Upgrades
        {
            get { return _upgrades; }
        }

        /// <summary>
        /// Initializes a new instance of the base item component.
        /// </summary>
        protected BaseItemComponent()
        {
            _properties = new List<ItemProperty>();
            _upgrades = new List<ItemUpgrade>();
            BaseItem = 0;
            StackSize = 1;
            Charges = -1; // -1 = unlimited charges
            Cost = 0;
            Identified = true;
            TemplateResRef = string.Empty;
        }

        /// <summary>
        /// Called when the component is attached to an entity.
        /// </summary>
        public virtual void OnAttach()
        {
            // Base implementation does nothing - engine-specific implementations can override
        }

        /// <summary>
        /// Called when the component is detached from an entity.
        /// </summary>
        public virtual void OnDetach()
        {
            // Base implementation does nothing - engine-specific implementations can override
        }

        /// <summary>
        /// Adds a property to the item.
        /// </summary>
        /// <param name="property">The property to add.</param>
        public virtual void AddProperty(ItemProperty property)
        {
            if (property != null)
            {
                _properties.Add(property);
            }
        }

        /// <summary>
        /// Removes a property from the item.
        /// </summary>
        /// <param name="property">The property to remove.</param>
        public virtual void RemoveProperty(ItemProperty property)
        {
            if (property != null)
            {
                _properties.Remove(property);
            }
        }

        /// <summary>
        /// Adds an upgrade to the item.
        /// </summary>
        /// <param name="upgrade">The upgrade to add.</param>
        public virtual void AddUpgrade(ItemUpgrade upgrade)
        {
            if (upgrade != null)
            {
                _upgrades.Add(upgrade);
            }
        }

        /// <summary>
        /// Removes an upgrade from the item.
        /// </summary>
        /// <param name="upgrade">The upgrade to remove.</param>
        public virtual void RemoveUpgrade(ItemUpgrade upgrade)
        {
            if (upgrade != null)
            {
                _upgrades.Remove(upgrade);
            }
        }
    }
}

