using System;
using System.Collections.Generic;
using CSharpKOTOR.Formats.TwoDA;
using CSharpKOTOR.Installation;
using JetBrains.Annotations;

namespace Odyssey.Kotor.Data
{
    /// <summary>
    /// Manages game data tables (2DA files) for KOTOR.
    /// </summary>
    /// <remarks>
    /// Key 2DA tables:
    /// - appearance.2da: Creature appearance definitions
    /// - baseitems.2da: Item base types and properties
    /// - classes.2da: Character class definitions
    /// - feat.2da: Feat definitions
    /// - spells.2da: Force power definitions
    /// - skills.2da: Skill definitions
    /// - surfacemat.2da: Walkmesh surface materials
    /// - portraits.2da: Character portraits
    /// - placeables.2da: Placeable object appearances
    /// - genericdoors.2da: Door models
    /// - repute.2da: Faction relationships
    /// - partytable.2da: Party member definitions
    /// </remarks>
    public class GameDataManager
    {
        private readonly Installation _installation;
        private readonly Dictionary<string, TwoDA> _tableCache;

        public GameDataManager(Installation installation)
        {
            _installation = installation ?? throw new ArgumentNullException("installation");
            _tableCache = new Dictionary<string, TwoDA>(StringComparer.OrdinalIgnoreCase);
        }

        #region Table Access

        /// <summary>
        /// Gets a 2DA table by name.
        /// </summary>
        /// <param name="tableName">Table name without extension (e.g., "appearance")</param>
        /// <returns>The loaded TwoDA, or null if not found</returns>
        [CanBeNull]
        public TwoDA GetTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return null;
            }

            // Check cache first
            TwoDA cached;
            if (_tableCache.TryGetValue(tableName, out cached))
            {
                return cached;
            }

            // Load from installation
            try
            {
                var resource = _installation.Resources.LookupResource(tableName, CSharpKOTOR.Resources.ResourceType.TwoDA);
                if (resource == null || resource.Data == null)
                {
                    return null;
                }

                TwoDA table = TwoDA.FromBytes(resource.Data);
                _tableCache[tableName] = table;
                return table;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Clears the table cache.
        /// </summary>
        public void ClearCache()
        {
            _tableCache.Clear();
        }

        /// <summary>
        /// Reloads a specific table from disk.
        /// </summary>
        public void ReloadTable(string tableName)
        {
            _tableCache.Remove(tableName);
            GetTable(tableName);
        }

        #endregion

        #region Appearance Data

        /// <summary>
        /// Gets appearance data for a creature.
        /// </summary>
        [CanBeNull]
        public AppearanceData GetAppearance(int appearanceType)
        {
            TwoDA table = GetTable("appearance");
            if (table == null || appearanceType < 0 || appearanceType >= table.GetHeight())
            {
                return null;
            }

            TwoDARow row = table.GetRow(appearanceType);
            return new AppearanceData
            {
                RowIndex = appearanceType,
                Label = row.Label(),
                ModelA = row.GetString("modela"),
                ModelB = row.GetString("modelb"),
                TexA = row.GetString("texa"),
                TexB = row.GetString("texb"),
                Race = row.GetString("race"),
                WalkSpeed = row.GetFloat("walkdist") ?? 1.75f,
                RunSpeed = row.GetFloat("rundist") ?? 4.0f,
                PerceptionRange = row.GetFloat("perspace") ?? 20.0f,
                Height = row.GetFloat("height") ?? 1.8f
            };
        }

        #endregion

        #region Class Data

        /// <summary>
        /// Gets class data.
        /// </summary>
        [CanBeNull]
        public ClassData GetClass(int classId)
        {
            TwoDA table = GetTable("classes");
            if (table == null || classId < 0 || classId >= table.GetHeight())
            {
                return null;
            }

            TwoDARow row = table.GetRow(classId);
            return new ClassData
            {
                RowIndex = classId,
                Label = row.Label(),
                Name = row.GetString("name"),
                HitDie = row.GetInt("hitdie") ?? 8,
                AttackBonusTable = row.GetString("attackbonustable"),
                FeatsBonusTable = row.GetString("featstable"),
                SavingThrowTable = row.GetString("savingthrowtable"),
                SkillsPerLevel = row.GetInt("skillpointbase") ?? 1,
                PrimaryAbility = row.GetString("primaryabil"),
                SpellCaster = row.GetInt("spellcaster") == 1,
                ForceUser = row.GetInt("forcedie") > 0
            };
        }

        #endregion

        #region Base Item Data

        /// <summary>
        /// Gets base item data.
        /// </summary>
        [CanBeNull]
        public BaseItemData GetBaseItem(int baseItem)
        {
            TwoDA table = GetTable("baseitems");
            if (table == null || baseItem < 0 || baseItem >= table.GetHeight())
            {
                return null;
            }

            TwoDARow row = table.GetRow(baseItem);
            return new BaseItemData
            {
                RowIndex = baseItem,
                Label = row.Label(),
                Name = row.GetString("name"),
                EquipableSlots = row.GetInt("equipableslots") ?? 0,
                DefaultModel = row.GetString("defaultmodel"),
                WeaponType = row.GetInt("weapontype") ?? 0,
                DamageType = row.GetInt("damagetype") ?? 0,
                NumDice = row.GetInt("numdice") ?? 0,
                DieToRoll = row.GetInt("dietoroll") ?? 0,
                CriticalThreat = row.GetInt("criticalthreat") ?? 20,
                CriticalMultiplier = row.GetInt("critmultiplier") ?? 2,
                BaseCost = row.GetInt("basecost") ?? 0,
                MaxStack = row.GetInt("stacking") ?? 1
            };
        }

        #endregion

        #region Feat Data

        /// <summary>
        /// Gets feat data.
        /// </summary>
        [CanBeNull]
        public FeatData GetFeat(int featId)
        {
            TwoDA table = GetTable("feat");
            if (table == null || featId < 0 || featId >= table.GetHeight())
            {
                return null;
            }

            TwoDARow row = table.GetRow(featId);
            return new FeatData
            {
                RowIndex = featId,
                Label = row.Label(),
                Name = row.GetString("name"),
                Description = row.GetString("description"),
                Icon = row.GetString("icon"),
                PrereqFeat1 = row.GetInt("prereqfeat1") ?? -1,
                PrereqFeat2 = row.GetInt("prereqfeat2") ?? -1,
                MinLevel = row.GetInt("minlevel") ?? 1,
                MinLevelClass = row.GetInt("minlevelclass") ?? -1,
                Selectable = row.GetInt("allclassescanuse") == 1 || row.GetInt("selectable") == 1
            };
        }

        #endregion

        #region Surface Material Data

        /// <summary>
        /// Gets surface material data for walkmesh.
        /// </summary>
        [CanBeNull]
        public SurfaceMatData GetSurfaceMaterial(int surfaceId)
        {
            TwoDA table = GetTable("surfacemat");
            if (table == null || surfaceId < 0 || surfaceId >= table.GetHeight())
            {
                return null;
            }

            TwoDARow row = table.GetRow(surfaceId);
            return new SurfaceMatData
            {
                RowIndex = surfaceId,
                Label = row.Label(),
                Walk = row.GetInt("walk") == 1,
                WalkCheck = row.GetInt("walkcheck") == 1,
                LineOfSight = row.GetInt("lineofsight") == 1,
                Grass = row.GetInt("grass") == 1,
                Sound = row.GetString("sound"),
                Name = row.GetString("name")
            };
        }

        /// <summary>
        /// Checks if a surface material is walkable.
        /// </summary>
        public bool IsSurfaceWalkable(int surfaceId)
        {
            SurfaceMatData data = GetSurfaceMaterial(surfaceId);
            return data != null && data.Walk;
        }

        #endregion

        #region Placeable Data

        /// <summary>
        /// Gets placeable appearance data.
        /// </summary>
        [CanBeNull]
        public PlaceableData GetPlaceable(int placeableType)
        {
            TwoDA table = GetTable("placeables");
            if (table == null || placeableType < 0 || placeableType >= table.GetHeight())
            {
                return null;
            }

            TwoDARow row = table.GetRow(placeableType);
            return new PlaceableData
            {
                RowIndex = placeableType,
                Label = row.Label(),
                ModelName = row.GetString("modelname"),
                LowGore = row.GetString("lowgore"),
                SoundAppType = row.GetInt("soundapptype") ?? 0
            };
        }

        #endregion

        #region Door Data

        /// <summary>
        /// Gets door appearance data.
        /// </summary>
        [CanBeNull]
        public DoorData GetDoor(int doorType)
        {
            TwoDA table = GetTable("genericdoors");
            if (table == null || doorType < 0 || doorType >= table.GetHeight())
            {
                return null;
            }

            TwoDARow row = table.GetRow(doorType);
            return new DoorData
            {
                RowIndex = doorType,
                Label = row.Label(),
                ModelName = row.GetString("modelname"),
                SoundAppType = row.GetInt("soundapptype") ?? 0,
                BlockSight = row.GetInt("blocksight") == 1
            };
        }

        #endregion
    }

    #region Data Structures

    /// <summary>
    /// Appearance data from appearance.2da.
    /// </summary>
    public class AppearanceData
    {
        public int RowIndex { get; set; }
        public string Label { get; set; }
        public string ModelA { get; set; }
        public string ModelB { get; set; }
        public string TexA { get; set; }
        public string TexB { get; set; }
        public string Race { get; set; }
        public float WalkSpeed { get; set; }
        public float RunSpeed { get; set; }
        public float PerceptionRange { get; set; }
        public float Height { get; set; }
    }

    /// <summary>
    /// Class data from classes.2da.
    /// </summary>
    public class ClassData
    {
        public int RowIndex { get; set; }
        public string Label { get; set; }
        public string Name { get; set; }
        public int HitDie { get; set; }
        public string AttackBonusTable { get; set; }
        public string FeatsBonusTable { get; set; }
        public string SavingThrowTable { get; set; }
        public int SkillsPerLevel { get; set; }
        public string PrimaryAbility { get; set; }
        public bool SpellCaster { get; set; }
        public bool ForceUser { get; set; }
    }

    /// <summary>
    /// Base item data from baseitems.2da.
    /// </summary>
    public class BaseItemData
    {
        public int RowIndex { get; set; }
        public string Label { get; set; }
        public string Name { get; set; }
        public int EquipableSlots { get; set; }
        public string DefaultModel { get; set; }
        public int WeaponType { get; set; }
        public int DamageType { get; set; }
        public int NumDice { get; set; }
        public int DieToRoll { get; set; }
        public int CriticalThreat { get; set; }
        public int CriticalMultiplier { get; set; }
        public int BaseCost { get; set; }
        public int MaxStack { get; set; }
    }

    /// <summary>
    /// Feat data from feat.2da.
    /// </summary>
    public class FeatData
    {
        public int RowIndex { get; set; }
        public string Label { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public int PrereqFeat1 { get; set; }
        public int PrereqFeat2 { get; set; }
        public int MinLevel { get; set; }
        public int MinLevelClass { get; set; }
        public bool Selectable { get; set; }
    }

    /// <summary>
    /// Surface material data from surfacemat.2da.
    /// </summary>
    public class SurfaceMatData
    {
        public int RowIndex { get; set; }
        public string Label { get; set; }
        public bool Walk { get; set; }
        public bool WalkCheck { get; set; }
        public bool LineOfSight { get; set; }
        public bool Grass { get; set; }
        public string Sound { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// Placeable data from placeables.2da.
    /// </summary>
    public class PlaceableData
    {
        public int RowIndex { get; set; }
        public string Label { get; set; }
        public string ModelName { get; set; }
        public string LowGore { get; set; }
        public int SoundAppType { get; set; }
    }

    /// <summary>
    /// Door data from genericdoors.2da.
    /// </summary>
    public class DoorData
    {
        public int RowIndex { get; set; }
        public string Label { get; set; }
        public string ModelName { get; set; }
        public int SoundAppType { get; set; }
        public bool BlockSight { get; set; }
    }

    #endregion
}
