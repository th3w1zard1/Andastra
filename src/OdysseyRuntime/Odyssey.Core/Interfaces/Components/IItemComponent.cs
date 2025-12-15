using System.Collections.Generic;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for item entities that stores UTI template data.
    /// </summary>
    /// <remarks>
    /// Item Component Interface:
    /// - Based on swkotor2.exe item system
    /// - Located via string references: Item loading functions handle UTI templates
    /// - Original implementation: Items have base item ID, properties, upgrades, charges, stack size
    /// - UTI file format: GFF with "UTI " signature containing item data (BaseItem, Properties, Charges, Cost)
    /// - Item properties modify item behavior (damage bonuses, AC bonuses, effects, etc.)
    /// - Upgrades modify item stats (damage, AC, etc.) - crystals, modifications
    /// - Based on UTI file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public interface IItemComponent : IComponent
    {
        /// <summary>
        /// Base item type ID (from baseitems.2da).
        /// </summary>
        int BaseItem { get; set; }

        /// <summary>
        /// Stack size (for stackable items).
        /// </summary>
        int StackSize { get; set; }

        /// <summary>
        /// Number of charges remaining (for items with charges).
        /// </summary>
        int Charges { get; set; }

        /// <summary>
        /// Item cost (base price).
        /// </summary>
        int Cost { get; set; }

        /// <summary>
        /// Whether the item is identified.
        /// </summary>
        bool Identified { get; set; }

        /// <summary>
        /// Item properties (effects, bonuses, etc.).
        /// </summary>
        IReadOnlyList<ItemProperty> Properties { get; }

        /// <summary>
        /// Item upgrades (crystals, modifications, etc.).
        /// </summary>
        IReadOnlyList<ItemUpgrade> Upgrades { get; }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        string TemplateResRef { get; set; }

        /// <summary>
        /// Adds a property to the item.
        /// </summary>
        void AddProperty(ItemProperty property);

        /// <summary>
        /// Removes a property from the item.
        /// </summary>
        void RemoveProperty(ItemProperty property);

        /// <summary>
        /// Adds an upgrade to the item.
        /// </summary>
        void AddUpgrade(ItemUpgrade upgrade);

        /// <summary>
        /// Removes an upgrade from the item.
        /// </summary>
        void RemoveUpgrade(ItemUpgrade upgrade);
    }

    /// <summary>
    /// Represents an item property (effect, bonus, etc.).
    /// </summary>
    public class ItemProperty
    {
        /// <summary>
        /// Property type ID (from itempropdef.2da).
        /// </summary>
        public int PropertyType { get; set; }

        /// <summary>
        /// Subtype ID (varies by property type).
        /// </summary>
        public int Subtype { get; set; }

        /// <summary>
        /// Cost table value.
        /// </summary>
        public int CostTable { get; set; }

        /// <summary>
        /// Cost table value (alternative).
        /// </summary>
        public int CostValue { get; set; }

        /// <summary>
        /// Parameter 1 (varies by property type).
        /// </summary>
        public int Param1 { get; set; }

        /// <summary>
        /// Parameter 2 (varies by property type).
        /// </summary>
        public int Param1Value { get; set; }
    }

    /// <summary>
    /// Represents an item upgrade (crystal, modification, etc.).
    /// </summary>
    public class ItemUpgrade
    {
        /// <summary>
        /// Upgrade type ID.
        /// </summary>
        public int UpgradeType { get; set; }

        /// <summary>
        /// Upgrade index (slot position).
        /// </summary>
        public int Index { get; set; }
    }
}

