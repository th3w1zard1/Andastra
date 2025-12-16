using System.Collections.Generic;
using AuroraEngine.Common;
using AuroraEngine.Common.Resources;
using JetBrains.Annotations;

namespace BioWareEngines.Engines.Odyssey.Templates
{
    // Moved from AuroraEngine.Common.Resource.Generics.UTI to Odyssey.Engines.Odyssey.Templates
    // This is KOTOR/Odyssey-specific GFF template structure
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uti.py:20
    /// <summary>
    /// Stores item data.
    ///
    /// UTI files are GFF-based format files that store item definitions including
    /// properties, costs, charges, and upgrade information.
    /// </summary>
    /// <remarks>
    /// UTI (Item Template) Format:
    /// - Based on swkotor2.exe item template system
    /// - Located via string references: "Item" @ 0x007bc544, "ItemList" @ 0x007c0c80, "Item template '%s' doesn't exist.\n" @ 0x007bf78c
    /// - Item loading: FUN_005223a0 @ 0x005223a0 loads item from GFF (construct_uti equivalent)
    /// - Item saving: FUN_005226d0 @ 0x005226d0 saves item to GFF (dismantle_uti equivalent)
    /// - Original implementation: UTI files are GFF with "UTI " signature containing item template data
    /// - GFF fields: TemplateResRef, BaseItem, LocalizedName, DescIdentified, Description, Tag, Cost, StackSize, Charges, etc.
    /// - Properties: PropertiesList contains item properties (PropertyName, Subtype, CostTable, CostValue, Param1, Param1Value, ChanceAppear, UpgradeType)
    /// - Upgrades: UpgradeLevel (K2 only), upgrade system for item enhancement
    /// - Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uti.py:20
    /// </remarks>
    [PublicAPI]
    public sealed class UTI
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/uti.py:20
        // Original: BINARY_TYPE = ResourceType.UTI
        public static readonly ResourceType BinaryType = ResourceType.UTI;

        // Base Item IDs that are considered armor
        public static readonly HashSet<int> ArmorBaseItems = new HashSet<int>
        {
            35, 36, 37, 38, 39, 40, 41, 42, 43, 53, 58, 63, 64, 65, 69, 71, 85, 89, 98, 100, 102, 103
        };

        // Basic UTI properties
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public int BaseItem { get; set; }
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        public LocalizedString Description { get; set; } = LocalizedString.FromInvalid();
        public LocalizedString DescriptionUnidentified { get; set; } = LocalizedString.FromInvalid();
        public int Cost { get; set; }
        public int StackSize { get; set; }
        public int Charges { get; set; }
        public int Plot { get; set; }
        public int AddCost { get; set; }
        public int Stolen { get; set; }
        public int Identified { get; set; }
        public int ItemType { get; set; }
        public int BaseItemType { get; set; }
        public int UpgradeLevel { get; set; }
        public int BodyVariation { get; set; }
        public int TextureVariation { get; set; }
        public int ModelVariation { get; set; }
        public int PaletteId { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;

        // Properties
        public List<UTIProperty> Properties { get; set; } = new List<UTIProperty>();

        // Upgrade properties
        public List<UTIUpgrade> Upgrades { get; set; } = new List<UTIUpgrade>();

        public UTI()
        {
        }
    }

    /// <summary>
    /// Represents an item property.
    /// </summary>
    [PublicAPI]
    public sealed class UTIProperty
    {
        public int PropertyName { get; set; }
        public int Subtype { get; set; }
        public int CostTable { get; set; }
        public int CostValue { get; set; }
        public int Param1 { get; set; }
        public int Param1Value { get; set; }
        public int ChanceAppear { get; set; }
        public int? UpgradeType { get; set; }

        public UTIProperty()
        {
            ChanceAppear = 100;
        }
    }

    /// <summary>
    /// Represents an item upgrade.
    /// </summary>
    [PublicAPI]
    public sealed class UTIUpgrade
    {
        public ResRef Upgrade { get; set; } = ResRef.FromBlank();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public UTIUpgrade()
        {
        }
    }
}

