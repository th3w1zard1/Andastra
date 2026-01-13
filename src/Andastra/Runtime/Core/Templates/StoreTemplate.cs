using System;
using System.Numerics;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Templates
{
    /// <summary>
    /// Store template implementation for spawning stores from UTM data.
    /// </summary>
    /// <remarks>
    /// Store Template:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) store/merchant system
    /// - Located via string references: Store functions handle merchant interactions
    /// - Template loading: FUN_005226d0 @ 0x005226d0 (entity serialization references store templates)
    /// - Original implementation: UTM (Store) GFF templates define store properties
    /// - UTM file format: GFF with "UTM " signature containing store data
    /// - Stores are merchants that sell items, buy items from player
    /// - Stores have mark-up/mark-down percentages for pricing, gold limits, item lists
    /// - Can identify items for a fee, buy items from player based on allowed item types
    /// - Based on UTM file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class StoreTemplate : IStoreTemplate
    {
        #region Properties

        public string ResRef { get; set; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get { return ObjectType.Store; } }
        public int MarkupBuy { get; set; }
        public int MarkdownSell { get; set; }
        public int IdentifyPrice { get; set; }

        // Additional properties
        public string DisplayName { get; set; }
        public string Comment { get; set; }
        public int MaxBuyPrice { get; set; }
        public bool BlackMarket { get; set; }
        public int StoreMoney { get; set; }
        public int OnOpenStoreScript { get; set; }

        // Script hooks
        public string OnOpenStore { get; set; }

        #endregion

        public StoreTemplate()
        {
            ResRef = string.Empty;
            Tag = string.Empty;
            DisplayName = string.Empty;
            Comment = string.Empty;
            OnOpenStore = string.Empty;
            MarkupBuy = 0;
            MarkdownSell = 0;
            MaxBuyPrice = -1;
        }

        public IEntity Spawn(IWorld world, Vector3 position, float facing)
        {
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            var entity = new Entity(ObjectType.Store, (World)world);
            entity.Tag = Tag;
            entity.TemplateResRef = ResRef;

            // Apply position
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                transform.Position = position;
                transform.Facing = facing;
            }

            // Register in world
            world.RegisterEntity(entity);

            return entity;
        }
    }
}
