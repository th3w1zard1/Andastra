using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for item entities that stores UTI template data.
    /// </summary>
    /// <remarks>
    /// Item Component Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe item system
    /// - Located via string references: "Item" @ 0x007bc550 (item object type), "Item List" @ 0x007bd028 (item list field)
    /// - "BaseItem" @ 0x007c0a78 (base item ID field), "ItemType" @ 0x007c437c (item type field)
    /// - "ItemPropertyIndex" @ 0x007beb58 (item property index), "ItemProperty" @ 0x007cb2f8 (item property field)
    /// - "Item_Property" @ 0x007cb2f8 (item property structure), "ITEMPROPS" @ 0x007caec4 (item properties constant)
    /// - "StackSize" @ 0x007c0a88 (stack size field), "Charges" @ 0x007c0a94 (charges field)
    /// - "ItemValue" @ 0x007c4f24 (item value field), "Cost" @ 0x007c0aa0 (item cost field)
    /// - "Identified" @ 0x007c0aac (identified flag), "ItemComponent" @ 0x007c41e4 (item component field)
    /// - "ItemClass" @ 0x007c455c (item class field), "BaseItemStatRef" @ 0x007c4428 (base item stat reference)
    /// - "PoweredItem" @ 0x007c43b0 (powered item flag), "AmmoItem" @ 0x007bf84c (ammo item field)
    /// - "NewItem" @ 0x007c0930 (new item flag), "ItemCreate" @ 0x007c4f84 (item creation field)
    /// - "PROTOITEM" @ 0x007b6c0c (prototype item constant), "BASEITEMS" @ 0x007c4594 (base items table)
    /// - Item fields: "ItemId" @ 0x007bef40 (item ID field), "ItemList" @ 0x007bf580 (item list field)
    /// - "Equip_ItemList" @ 0x007bf5a4 (equipped item list), "MaxItemPoints" @ 0x007bdb2c (max item points field)
    /// - "LoseItems" @ 0x007bdaa8 (lose items field), "LoseItemsNum" @ 0x007bda6c (lose items number field)
    /// - "LoseStolenItems" @ 0x007bdab4 (lose stolen items field), "MAXSINGLEITEMVALUE" @ 0x007c0774 (max single item value constant)
    /// - Item events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM" @ 0x007bc8c4, "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM" @ 0x007bc89c
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc8f0, "CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM" @ 0x007bc594
    /// - "EVENT_ACQUIRE_ITEM" @ 0x007bcbf4, "EVENT_ITEM_ON_HIT_SPELL_IMPACT" @ 0x007bcc8c
    /// - "ITEMLOST" @ 0x007bdf4c, "ITEMRECEIVED" @ 0x007bdf58
    /// - Module item events: "Mod_OnAcquirItem" @ 0x007be7e0, "Mod_OnUnAqreItem" @ 0x007be7cc, "Mod_OnActvtItem" @ 0x007be7f4, "Mod_OnEquipItem" @ 0x007beac8
    /// - Item types: "PT_ITEM_CHEMICAL" @ 0x007c18d4, "PT_ITEM_COMPONENT" @ 0x007c18e8
    /// - Error: "Item template %s doesn't exist.\n" @ 0x007c2028
    /// - Item loading: FUN_005226d0 @ 0x005226d0 (load item from UTI template), FUN_005fb0f0 @ 0x005fb0f0 (item creation)
    /// - Original implementation: Items have base item ID, properties, upgrades, charges, stack size
    /// - UTI file format: GFF with "UTI " signature containing item data (BaseItem, Properties, Charges, Cost)
    /// - Item properties modify item behavior (damage bonuses, AC bonuses, effects, etc.)
    /// - Upgrades modify item stats (damage, AC, etc.) - crystals, modifications
    /// - Stack size: Items can stack in inventory (StackSize field, max stack size from baseitems.2da)
    /// - Charges: Items with charges (potions, grenades, etc.) have Charges field tracking remaining uses
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

