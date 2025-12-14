using System;
using System.Numerics;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Templates
{
    /// <summary>
    /// Store template implementation for spawning stores from UTM data.
    /// </summary>
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
            var transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
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
