using System.Collections.Generic;
using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for merchant store entities.
    /// </summary>
    /// <remarks>
    /// Based on UTM file format documentation.
    /// Stores are merchants that sell items.
    /// </remarks>
    public class StoreComponent : IComponent
    {
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
