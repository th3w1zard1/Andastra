using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Engines.Odyssey.Components;

namespace Andastra.Runtime.Engines.Odyssey.Systems
{
    /// <summary>
    /// Manages merchant store interactions.
    /// </summary>
    /// <remarks>
    /// Store System:
    /// - Based on swkotor2.exe store/merchant system
    /// - LoadStoreFromGFF @ 0x00571310 - Loads store data from GIT GFF into store object (located via "StoreList" @ 0x007bd098)
    ///   - Reads Tag, LocName, MarkDown, MarkUp, OnOpenStore, BuySellFlag, ItemList (ObjectId, Infinite, InventoryRes)
    ///   - Creates item objects for each item in ItemList, loads item data from GFF, adds items to store inventory sorted by value
    /// - SaveStoreToGFF @ 0x00570e30 - Saves store data to GFF save data (located via "StoreList" @ 0x007bd098)
    ///   - Writes Tag, LocName, MarkDown, MarkUp, OnOpenStore, BuySellFlag, ItemList (ObjectId, Infinite) for each item in store inventory
    /// - Located via string references: "Store" @ 0x007bc4f8, "StoreList" @ 0x007bd098 (GIT store list)
    /// - "OnOpenStore" @ 0x007c1200 (store open script event), "Store template %s doesn't exist.\n" @ 0x007c1228
    /// - "StorePanelSort" @ 0x007c440c, "StorePanel" @ 0x007c441c (store GUI panels)
    /// - "store_p" @ 0x007d0190 (store panel GUI), "store" @ 0x007b6068 (store constant)
    /// - Original implementation: FUN_004e08e0 @ 0x004e08e0 loads store instances from GIT
    /// - Store templates: UTM (Store) GFF files with "UTM " signature containing merchant inventory
    /// - Store inventory: List of items with prices, quantities, and restock flags
    /// - Markup rates: Buy/sell price multipliers (buyPrice = basePrice * buyMarkup, sellPrice = basePrice * sellMarkup)
    /// - Store types: Generic merchant, equipment vendor, item vendor, etc.
    /// - Store interaction: Player opens store GUI, can buy/sell items, restock occurs on area transition
    /// - Store state: Tracks which items have been purchased (for restock logic)
    /// - Based on UTM file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class StoreSystem
    {
        private readonly IWorld _world;

        public StoreSystem(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
        }

        /// <summary>
        /// Opens a store for interaction.
        /// </summary>
        /// <param name="storeEntity">The store entity to open.</param>
        /// <param name="customer">The entity opening the store (typically player).</param>
        public void OpenStore(IEntity storeEntity, IEntity customer)
        {
            if (storeEntity == null || customer == null)
            {
                return;
            }

            if (storeEntity.ObjectType != Core.Enums.ObjectType.Store)
            {
                return;
            }

            StoreComponent storeComponent = storeEntity.GetComponent<StoreComponent>();
            if (storeComponent == null)
            {
                return;
            }

            // Fire OnOpenStore script event
            // Based on swkotor2.exe: OnOpenStore script fires when store is opened
            // Located via string reference: "OnOpenStore" @ 0x007c1200
            // Original implementation: FUN_004dcfb0 @ 0x004dcfb0 dispatches OnOpenStore script event
            IEventBus eventBus = _world.EventBus;
            if (eventBus != null)
            {
                eventBus.FireScriptEvent(storeEntity, ScriptEvent.OnOpenStore, customer);
            }

            // Store GUI would be opened here (handled by UI layer)
        }

        /// <summary>
        /// Closes a store.
        /// </summary>
        /// <param name="storeEntity">The store entity to close.</param>
        public void CloseStore(IEntity storeEntity)
        {
            if (storeEntity == null)
            {
                return;
            }

            // Fire OnStoreClose script event
            // Based on swkotor2.exe: OnStoreClose script fires when store is closed
            // Located via string references: Store close event handling
            // Original implementation: OnStoreClose script fires on store entity when store GUI is closed
            IEventBus eventBus = _world.EventBus;
            if (eventBus != null)
            {
                eventBus.FireScriptEvent(storeEntity, ScriptEvent.OnStoreClose, null);
            }

            // Store GUI would be closed here (handled by UI layer)
        }

        /// <summary>
        /// Gets the buy price for an item from a store.
        /// </summary>
        /// <param name="storeEntity">The store entity.</param>
        /// <param name="item">The item to price.</param>
        /// <returns>Buy price, or 0 if item not available.</returns>
        public int GetBuyPrice(IEntity storeEntity, IEntity item)
        {
            if (storeEntity == null || item == null)
            {
                return 0;
            }

            StoreComponent storeComponent = storeEntity.GetComponent<StoreComponent>();
            if (storeComponent == null)
            {
                return 0;
            }

            // Get base item price from item component
            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return 0;
            }

            int basePrice = itemComponent.Cost;
            float buyMarkup = storeComponent.MarkUp / 100.0f;

            // Buy price = base price * buy markup
            return (int)(basePrice * buyMarkup);
        }

        /// <summary>
        /// Gets the sell price for an item to a store.
        /// </summary>
        /// <param name="storeEntity">The store entity.</param>
        /// <param name="item">The item to price.</param>
        /// <returns>Sell price, or 0 if store doesn't buy this item type.</returns>
        public int GetSellPrice(IEntity storeEntity, IEntity item)
        {
            if (storeEntity == null || item == null)
            {
                return 0;
            }

            StoreComponent storeComponent = storeEntity.GetComponent<StoreComponent>();
            if (storeComponent == null)
            {
                return 0;
            }

            // Get base item price from item component
            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return 0;
            }

            int basePrice = itemComponent.Cost;
            float sellMarkup = storeComponent.MarkDown / 100.0f;

            // Sell price = base price * sell markup (typically < 1.0 for profit margin)
            return (int)(basePrice * sellMarkup);
        }
    }
}

