using System;
using JetBrains.Annotations;
using Odyssey.Core.Enums;
using Odyssey.Kotor.Data;
using Odyssey.Kotor.Components;

namespace Odyssey.Kotor.Systems
{
    /// <summary>
    /// Resolves model ResRefs from entity appearance types and object types.
    /// </summary>
    /// <remarks>
    /// Model Resolver:
    /// - Based on swkotor2.exe model resolution system
    /// - Located via string references: Model loading from appearance.2da, placeables.2da, genericdoors.2da
    /// - "Appearance_Type" @ 0x007c40f0 (appearance type field), "ModelResRef" @ 0x007c2f6c (model resource reference)
    /// - Model resolution: FUN_005261b0 @ 0x005261b0 resolves creature model from appearance.2da row
    /// - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc (model loading error)
    /// - Original implementation: Resolves model ResRefs from appearance IDs using 2DA tables
    /// - appearance.2da: modela/modelb columns for creatures (ModelA for variation 0, ModelB for variation 1)
    /// - placeables.2da: modelname column for placeables (placeable model ResRef)
    /// - genericdoors.2da: modelname column for doors (door model ResRef)
    /// - Body variation determines which model variant to use (0 = ModelA, 1 = ModelB, etc.)
    /// - Model resolution order: 1) RenderableComponent.ModelResRef (if set), 2) Resolve from appearance.2da/placeables.2da/genericdoors.2da
    /// - Based on swkotor2.exe: FUN_005261b0 @ 0x005261b0 (resolve creature model from appearance)
    /// </remarks>
    public static class ModelResolver
    {
        /// <summary>
        /// Resolves the model ResRef for a creature based on appearance type.
        /// </summary>
        /// <param name="gameData">Game data manager for 2DA lookups.</param>
        /// <param name="appearanceType">Appearance type index into appearance.2da.</param>
        /// <param name="bodyVariation">Body variation (0 = modela, 1 = modelb, etc.).</param>
        /// <returns>Model ResRef or null if not found.</returns>
        [CanBeNull]
        public static string ResolveCreatureModel([NotNull] GameDataManager gameData, int appearanceType, int bodyVariation = 0)
        {
            if (gameData == null)
            {
                throw new ArgumentNullException("gameData");
            }

            AppearanceData appearance = gameData.GetAppearance(appearanceType);
            if (appearance == null)
            {
                return null;
            }

            // Use modela for variation 0, modelb for variation 1, etc.
            // Based on appearance.2da: modela, modelb, modelc, etc. columns
            switch (bodyVariation)
            {
                case 0:
                    return appearance.ModelA;
                case 1:
                    return appearance.ModelB;
                default:
                    // Fallback to modela if variation is out of range
                    return appearance.ModelA;
            }
        }

        /// <summary>
        /// Resolves the model ResRef for a placeable based on appearance type.
        /// </summary>
        /// <param name="gameData">Game data manager for 2DA lookups.</param>
        /// <param name="appearanceType">Appearance type index into placeables.2da.</param>
        /// <returns>Model ResRef or null if not found.</returns>
        [CanBeNull]
        public static string ResolvePlaceableModel([NotNull] GameDataManager gameData, int appearanceType)
        {
            if (gameData == null)
            {
                throw new ArgumentNullException("gameData");
            }

            PlaceableData placeable = gameData.GetPlaceable(appearanceType);
            if (placeable == null)
            {
                return null;
            }

            return placeable.ModelName;
        }

        /// <summary>
        /// Resolves the model ResRef for a door based on appearance type.
        /// </summary>
        /// <param name="gameData">Game data manager for 2DA lookups.</param>
        /// <param name="appearanceType">Appearance type index into genericdoors.2da.</param>
        /// <returns>Model ResRef or null if not found.</returns>
        [CanBeNull]
        public static string ResolveDoorModel([NotNull] GameDataManager gameData, int appearanceType)
        {
            if (gameData == null)
            {
                throw new ArgumentNullException("gameData");
            }

            DoorData door = gameData.GetDoor(appearanceType);
            if (door == null)
            {
                return null;
            }

            return door.ModelName;
        }

        /// <summary>
        /// Resolves the model ResRef for an entity based on its type and appearance.
        /// </summary>
        /// <param name="gameData">Game data manager for 2DA lookups.</param>
        /// <param name="entity">The entity to resolve model for.</param>
        /// <returns>Model ResRef or null if not found.</returns>
        [CanBeNull]
        public static string ResolveEntityModel([NotNull] GameDataManager gameData, [NotNull] IEntity entity)
        {
            if (gameData == null)
            {
                throw new ArgumentNullException("gameData");
            }
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            // Check if entity already has a renderable component with model
            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            if (renderable != null && !string.IsNullOrEmpty(renderable.ModelResRef))
            {
                return renderable.ModelResRef;
            }

            // Resolve based on object type
            switch (entity.ObjectType)
            {
                case ObjectType.Creature:
                    CreatureComponent creature = entity.GetComponent<CreatureComponent>();
                    if (creature != null)
                    {
                        return ResolveCreatureModel(gameData, creature.AppearanceType, creature.BodyVariation);
                    }
                    break;

                case ObjectType.Placeable:
                    PlaceableComponent placeable = entity.GetComponent<PlaceableComponent>();
                    if (placeable != null)
                    {
                        return ResolvePlaceableModel(gameData, placeable.AppearanceType);
                    }
                    break;

                case ObjectType.Door:
                    DoorComponent door = entity.GetComponent<DoorComponent>();
                    if (door != null)
                    {
                        return ResolveDoorModel(gameData, door.GenericType);
                    }
                    break;
            }

            return null;
        }
    }
}

