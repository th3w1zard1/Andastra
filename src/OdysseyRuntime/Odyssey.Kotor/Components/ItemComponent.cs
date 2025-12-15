using System.Collections.Generic;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for item entities that stores UTI template data.
    /// </summary>
    /// <remarks>
    /// Item Component:
    /// - Based on swkotor2.exe item system
    /// - Located via string references: "ItemList" @ 0x007bf580 (item list field), "Equip_ItemList" @ 0x007bf5a4 (equipped items list)
    /// - "BaseItem" @ 0x007c0a78 (base item type ID field), "Properties" @ 0x007c2f3c (item properties list field)
    /// - "Charges" @ 0x007c2f48 (item charges field), "Cost" @ 0x007c2f50 (item cost/price field)
    /// - "StackSize" @ 0x007c2f5c (stack size for stackable items), "Identified" @ 0x007c2f64 (identified flag)
    /// - Item events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM" @ 0x007bc594 (equip item script event), "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM" @ 0x007bc8c4 (acquire item event)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM" @ 0x007bc89c (lose item event), "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc8f0 (activate item event)
    /// - "EVENT_ACQUIRE_ITEM" @ 0x007bcbf4 (acquire item event, case 0x1c), "ITEMRECEIVED" @ 0x007bdf58 (item received global variable)
    /// - "ITEMLOST" @ 0x007bdf4c (item lost global variable), "Mod_OnAcquirItem" @ 0x007be7e0 (module acquire item script)
    /// - "Mod_OnUnAqreItem" @ 0x007be7cc (module unacquire item script), "Mod_OnEquipItem" @ 0x007beac8 (module equip item script)
    /// - "Mod_OnActvtItem" @ 0x007be7f4 (module activate item script)
    /// - Item fields: "ItemId" @ 0x007bef40 (item ID field), "ItemPropertyIndex" @ 0x007beb58 (item property index)
    /// - "ItemType" @ 0x007c437c (item type field), "ItemClass" @ 0x007c455c (item class field)
    /// - "ItemValue" @ 0x007c4f24 (item value field), "ItemCreate" @ 0x007c4f84 (item create function)
    /// - "BaseItemStatRef" @ 0x007c4428 (base item stat reference), "ItemComponent" @ 0x007c41e4 (item component name)
    /// - "PROTOITEM" @ 0x007b6c0c (prototype item constant), "BASEITEMS" @ 0x007c4594 (base items table name)
    /// - Template loading: FUN_005fb0f0 @ 0x005fb0f0 loads item templates from UTI GFF files
    /// - Error messages: "Item template %s doesn't exist.\n" @ 0x007c2028 (template not found error), "Error: Invalid item" @ 0x007d110c (invalid item error)
    /// - "CreateItem::CreateItemEntry() -- Could not find a row for an item. Major error: " @ 0x007d07c8 (create item error)
    /// - Original implementation: Items have base item ID, properties, upgrades, charges, stack size
    /// - UTI file format: GFF with "UTI " signature containing item data (BaseItem, Properties, Charges, Cost, StackSize, Identified)
    /// - Item properties modify item behavior (damage bonuses, AC bonuses, effects, etc.) - stored as PropertyList array in UTI
    /// - Upgrades modify item stats (damage, AC, etc.) - stored as UpgradeList array (K2 feature, upgradeitems.2da lookup)
    /// - Charges: -1 = unlimited charges, 0+ = limited charges (items with charges consume one per use, charged items show charge count)
    /// - Stack size: 1 = not stackable, 2+ = stackable (maximum stack size from baseitems.2da MaxStackSize column)
    /// - Identified: false = unidentified item (shows generic name, can be identified via IdentifyItem NWScript function)
    /// - Item value: Cost field stores item base value (for selling/trading, modified by merchant markups)
    /// - Item properties: Properties array contains ItemProperty entries (PropertyName, Subtype, CostValue, ParamTable, etc.)
    /// - Based on UTI file format documentation in vendor/PyKotor/wiki/ and baseitems.2da table structure
    /// </remarks>
    public class ItemComponent : IItemComponent
    {
        private readonly List<ItemProperty> _properties;
        private readonly List<ItemUpgrade> _upgrades;

        public IEntity Owner { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }

        public ItemComponent()
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

        public int BaseItem { get; set; }
        public int StackSize { get; set; }
        public int Charges { get; set; }
        public int Cost { get; set; }
        public bool Identified { get; set; }
        public string TemplateResRef { get; set; }

        public IReadOnlyList<ItemProperty> Properties
        {
            get { return _properties; }
        }

        public IReadOnlyList<ItemUpgrade> Upgrades
        {
            get { return _upgrades; }
        }

        public void AddProperty(ItemProperty property)
        {
            if (property != null)
            {
                _properties.Add(property);
            }
        }

        public void RemoveProperty(ItemProperty property)
        {
            if (property != null)
            {
                _properties.Remove(property);
            }
        }

        public void AddUpgrade(ItemUpgrade upgrade)
        {
            if (upgrade != null)
            {
                _upgrades.Add(upgrade);
            }
        }

        public void RemoveUpgrade(ItemUpgrade upgrade)
        {
            if (upgrade != null)
            {
                _upgrades.Remove(upgrade);
            }
        }
    }
}

