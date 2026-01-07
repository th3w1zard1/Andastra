using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Engines.Odyssey.Components
{
    /// <summary>
    /// Component for merchant store entities.
    /// </summary>
    /// <remarks>
    /// Store Component:
    /// - Based on swkotor2.exe store/merchant system
    /// - Located via string references: "Store" @ 0x007bc4f8 (store entity type), "StoreList" @ 0x007bd098 (GIT store list)
    /// - "store" @ 0x007b6068 (store constant), "OnOpenStore" @ 0x007c1200 (store script event hook)
    /// - Store fields: "MarkUp" @ 0x007c03f0, "MarkDown" @ 0x007c0400 (store pricing fields)
    /// - "StoreGold" @ 0x007c040c (store gold amount), "CanBuy" @ 0x007c0418 (can buy flag)
    /// - "ItemsForSale" @ 0x007c0420, "ItemsWillBuy" @ 0x007c0430 (store item lists)
    /// - "StorePanel" @ 0x007c441c (store panel GUI), "StorePanelSort" @ 0x007c440c (store panel sort field)
    /// - Error messages: "Store template %s doesn't exist.\n" @ 0x007c1228 (store template not found error)
    /// - GUI: "store_p" @ 0x007d0190 (store panel GUI file)
    /// - Original implementation: Stores are merchants that sell items, buy items from player
    /// - UTM file format: GFF with "UTM " signature containing store data (MarkUp, MarkDown, StoreGold, CanBuy, ItemsForSale, ItemsWillBuy)
    /// - Stores have mark-up/mark-down percentages for pricing (MarkUp/MarkDown), gold limits (StoreGold), item lists
    /// - Can identify items for a fee (CanIdentify, IdentifyPrice), buy items from player based on allowed item types (ItemsWillBuy)
    /// - Store opening: Fires OnOpenStore script when store GUI opens (via OpenStore NWScript function)
    /// - Pricing: Items sell for base_price * MarkDown / 100, buy for base_price * MarkUp / 100
    /// - Store gold: Maximum gold store can use to buy items from player (StoreGold field, refills over time or via scripts)
    /// - Items for sale: List of items store sells (ItemsForSale list, can be infinite via Infinite flag)
    /// - Items will buy: List of base item types store accepts from player (ItemsWillBuy list, empty = accepts all)
    /// - Based on UTM file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class StoreComponent : IComponent
    {
        public IEntity Owner { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }

        public StoreComponent()
        {
            TemplateResRef = string.Empty;
            OnOpenStore = string.Empty;
            ItemsForSale = new List<StoreItem>();
            ItemsWillBuy = new List<int>();
            MarkUp = 100;
            MarkDown = 100;
        }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Store mark-up percentage (buy price multiplier).
        /// </summary>
        public int MarkUp { get; set; }

        /// <summary>
        /// Store mark-down percentage (sell price multiplier).
        /// </summary>
        public int MarkDown { get; set; }

        /// <summary>
        /// Gold available for buying from player.
        /// </summary>
        public int Gold { get; set; }

        /// <summary>
        /// Whether player can sell items.
        /// </summary>
        public bool CanBuy { get; set; }

        /// <summary>
        /// Whether store identifies items.
        /// </summary>
        public bool CanIdentify { get; set; }

        /// <summary>
        /// Identify price.
        /// </summary>
        public int IdentifyPrice { get; set; }

        /// <summary>
        /// Maximum price for items to buy.
        /// </summary>
        public int MaxBuyPrice { get; set; }

        /// <summary>
        /// Script to run when store opens.
        /// </summary>
        public string OnOpenStore { get; set; }

        /// <summary>
        /// List of items for sale.
        /// </summary>
        public List<StoreItem> ItemsForSale { get; set; }

        /// <summary>
        /// List of base item types the store will buy.
        /// </summary>
        public List<int> ItemsWillBuy { get; set; }
    }

    /// <summary>
    /// Item available in a store.
    /// </summary>
    public class StoreItem
    {
        /// <summary>
        /// Item template resource reference.
        /// </summary>
        public string ResRef { get; set; }

        /// <summary>
        /// Stack size (for stackable items).
        /// </summary>
        public int StackSize { get; set; }

        /// <summary>
        /// Whether infinite copies are available.
        /// </summary>
        public bool Infinite { get; set; }

        public StoreItem()
        {
            ResRef = string.Empty;
            StackSize = 1;
        }
    }
}
