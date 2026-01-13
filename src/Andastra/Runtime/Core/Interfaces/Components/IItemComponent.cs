using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for item entities that stores item template data.
    /// </summary>
    /// <remarks>
    /// Item Component Interface:
    /// - Common interface for item components across all BioWare engines
    /// - Base implementation: BaseItemComponent in Runtime.Games.Common.Components
    /// - Engine-specific implementations: OdysseyItemComponent, AuroraItemComponent, EclipseItemComponent, InfinityItemComponent
    ///
    /// Based on reverse engineering of:
    /// - Odyssey (swkotor.exe, swkotor2.exe): UTI GFF format
    ///   - swkotor.exe: Item component system with UTI template loading
    ///   - swkotor2.exe: Enhanced item system with upgrade support
    ///   - Located via string references: "Item" @ 0x007bc550 (item object type), "Item List" @ 0x007bd028 (item list field)
    ///   - "BaseItem" @ 0x007c0a78 (base item ID field), "ItemType" @ 0x007c437c (item type field)
    ///   - "ItemPropertyIndex" @ 0x007beb58 (item property index), "ItemProperty" @ 0x007cb2f8 (item property field)
    ///   - "StackSize" @ 0x007c0a88 (stack size field), "Charges" @ 0x007c0a94 (charges field)
    ///   - "Cost" @ 0x007c0aa0 (item cost field), "Identified" @ 0x007c0aac (identified flag)
    ///   - Item loading: 0x005226d0 @ 0x005226d0 (load item from UTI template), 0x005fb0f0 @ 0x005fb0f0 (item creation)
    ///   - Item events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM" @ 0x007bc8c4, "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM" @ 0x007bc89c
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc8f0, "CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM" @ 0x007bc594
    ///   - Module item events: "Mod_OnAcquirItem" @ 0x007be7e0, "Mod_OnUnAqreItem" @ 0x007be7cc, "Mod_OnActvtItem" @ 0x007be7f4, "Mod_OnEquipItem" @ 0x007beac8
    /// - Aurora (nwmain.exe, nwn2main.exe): UTI GFF format (identical to Odyssey)
    ///   - Uses same UTI file format as Odyssey engines
    ///   - Item component system with similar structure to Odyssey
    ///   - Property system and cost calculation identical to Odyssey
    /// - Eclipse (daorigins.exe, DragonAge2.exe): Enhanced item system
    ///   - Item component system with enhanced property calculations
    ///   - Upgrade system with different mechanics than Odyssey
    ///   - Item property availability and cost calculations differ from Odyssey/Aurora
    /// - Infinity (, ): Modern item system
    ///   - Item component system with streamlined property system
    ///   - Different upgrade/modification mechanics
    ///
    /// Common functionality across all engines:
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
    ///
    /// File formats:
    /// - Odyssey/Aurora: UTI (GFF with "UTI " signature)
    /// - Eclipse/Infinity: Engine-specific formats (to be reverse engineered)
    ///
    /// Based on UTI file format documentation in vendor/PyKotor/wiki/ and Bioware Aurora Item Format specification
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

        /// <summary>
        /// Template ResRef for the upgrade (UTI resource reference).
        /// </summary>
        public string TemplateResRef { get; set; }
    }
}

