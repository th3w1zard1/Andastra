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
    /// - Located via string references: Item loading functions handle UTI templates
    /// - Original implementation: Items have base item ID, properties, upgrades, charges, stack size
    /// - UTI file format: GFF with "UTI " signature containing item data (BaseItem, Properties, Charges, Cost)
    /// - Item properties modify item behavior (damage bonuses, AC bonuses, effects, etc.)
    /// - Upgrades modify item stats (damage, AC, etc.)
    /// - Based on UTI file format documentation in vendor/PyKotor/wiki/
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

