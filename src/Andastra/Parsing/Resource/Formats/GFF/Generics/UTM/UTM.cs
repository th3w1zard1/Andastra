using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using JetBrains.Annotations;

namespace Andastra.Parsing.Resource.Generics.UTM
{
    /// <summary>
    /// Stores merchant data.
    ///
    /// UTM (User Template Merchant) files define merchant/store blueprints. Stored as GFF format
    /// with inventory, pricing, and script references.
    /// </summary>
    [PublicAPI]
    public sealed class UTM
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:16
        // Original: BINARY_TYPE = ResourceType.UTM
        public static readonly ResourceType BinaryType = ResourceType.UTM;

        // Basic UTM properties
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/utm.py:110-127
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        public string Tag { get; set; } = string.Empty;
        public int MarkUp { get; set; }
        public int MarkDown { get; set; }
        public string Comment { get; set; } = string.Empty;
        public int OnOpen { get; set; }
        public int OnStore { get; set; }
        public ResRef OnOpenScript { get; set; } = ResRef.FromBlank();
        public ResRef OnStoreScript { get; set; } = ResRef.FromBlank();

        // Matching PyKotor implementation: self.id: int = id (deprecated field)
        // Original: id: "ID" field. Not used by the game engine.
        public int Id { get; set; } = 5;

        // Matching PyKotor implementation: self.can_buy: bool = can_buy
        // Original: can_buy: Derived from "BuySellFlag" bit 0. Whether merchant can buy items.
        public bool CanBuy { get; set; } = false;

        // Matching PyKotor implementation: self.can_sell: bool = can_sell
        // Original: can_sell: Derived from "BuySellFlag" bit 1. Whether merchant can sell items.
        public bool CanSell { get; set; } = false;

        // Store properties from Bioware Aurora Store format
        // These fields are present in all Store Structs (both blueprints and instances)
        // Based on Bioware Aurora Engine Store Format documentation
        // Based on nwmain.exe: CNWSStore::LoadStore @ 0x1404fbbf0
        // - Line 63: StoreGold = ReadFieldINT("StoreGold", -1)
        // - Line 65: IdentifyPrice = ReadFieldINT("IdentifyPrice", 100)
        // - Line 67: MaxBuyPrice = ReadFieldINT("MaxBuyPrice", -1)
        /// <summary>
        /// StoreGold: INT - Amount of gold store has available for buying items.
        /// -1 = unlimited gold, 0+ = specific amount
        /// Based on nwmain.exe: CNWSStore::LoadStore @ 0x1404fbbf0 line 63 - ReadFieldINT("StoreGold", -1)
        /// </summary>
        public int StoreGold { get; set; } = -1;

        /// <summary>
        /// IdentifyPrice: INT - Price to identify items.
        /// -1 = store will not identify items, 0+ = price to identify
        /// Based on nwmain.exe: CNWSStore::LoadStore @ 0x1404fbbf0 line 65 - ReadFieldINT("IdentifyPrice", 100)
        /// </summary>
        public int IdentifyPrice { get; set; } = 100;

        /// <summary>
        /// MaxBuyPrice: INT - Maximum price store will pay for an item.
        /// -1 = no limit, 0+ = maximum price
        /// Based on nwmain.exe: CNWSStore::LoadStore @ 0x1404fbbf0 line 67 - ReadFieldINT("MaxBuyPrice", -1)
        /// </summary>
        public int MaxBuyPrice { get; set; } = -1;

        // Inventory items
        // Matching PyKotor implementation: self.inventory: list[InventoryItem] = list(inventory) if inventory is not None else []
        // Original: inventory: "ItemList" field. List of items in merchant inventory.
        public List<UTMItem> Items { get; set; } = new List<UTMItem>();

        // Alias for Items to match Python naming
        public List<UTMItem> Inventory
        {
            get { return Items; }
            set { Items = value; }
        }

        public UTM()
        {
        }
    }

    /// <summary>
    /// Represents an item in a merchant's inventory.
    /// </summary>
    [PublicAPI]
    public sealed class UTMItem
    {
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public int Infinite { get; set; }
        public int Droppable { get; set; }

        public UTMItem()
        {
        }
    }
}

