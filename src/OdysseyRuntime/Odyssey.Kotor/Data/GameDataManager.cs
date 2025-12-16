using System;
using System.Collections.Generic;
using AuroraEngine.Common.Formats.TwoDA;
using AuroraEngine.Common.Installation;
using JetBrains.Annotations;

namespace Odyssey.Kotor.Data
{
    /// <summary>
    /// Manages game data tables (2DA files) for KOTOR.
    /// </summary>
    /// <remarks>
    /// Game Data Manager:
    /// - Based on swkotor2.exe 2DA table loading system
    /// - Located via string references: "2DAName" @ 0x007c3980, " 2DA file" @ 0x007c4674
    /// - Error messages: "CSWClass::LoadFeatGain: can't load featgain.2da" @ 0x007c46bc
    /// - "CSWClass::LoadFeatTable: Can't load feat.2da" @ 0x007c4720
    /// - "CSWClass::LoadSkillsTable: Can't load skills.2da" @ 0x007c47ac
    /// - "CSWClass::LoadSpellsTable: Can't load spells.2da" @ 0x007c4918
    /// - "CLASSES" @ 0x007c2ba4, "Appearance_Type" @ 0x007c40f0
    /// - Appearance fields: "Appearance" @ 0x007bf144, "Appearance_Type" @ 0x007c40f0, "Appearance_Head" @ 0x007c40e0
    /// - "Appearance_01" @ 0x007c0220, "Appearance_02" @ 0x007c0200, "Appearance_03" @ 0x007c01e0, "Appearance_04" @ 0x007c01c0
    /// - "Appearance_0%d" @ 0x007cadd4 (appearance format string), "AppearanceNumber" @ 0x007d1900
    /// - "AppearanceSndSet" @ 0x007c498c, "AppearanceForceUpdate" @ 0x007c7e48, "AppearanceType" @ 0x007c84c8
    /// - "Appearance_L" @ 0x007d18e0, "Appearance_S" @ 0x007d18f0, "PM_Appearance" @ 0x007bf5d4
    /// - Debug: "    Appearance: " @ 0x007caf3c, "Appearance: " @ 0x007cb19c
    /// - Error: "CSWCCreatureAppearance::CreateBTypeBody(): Failed to load model '%s'." @ 0x007cdc40
    /// - Original implementation: Loads and caches 2DA (two-dimensional array) data files
    /// - 2DA file format: Tab-separated text file with row labels and column headers
    /// - Table lookup: Uses row label (string) or row index (int) to access data
    /// - Column access: Column names are case-insensitive (e.g., "ModelA", "modela" both work)
    /// - Table caching: Caches loaded tables in memory to avoid redundant file reads
    /// - Key 2DA tables:
    ///   - appearance.2da: Creature appearance definitions (Appearance_Type field)
    ///   - baseitems.2da: Item base types and properties
    ///   - classes.2da: Character class definitions (CLASSES)
    ///   - feat.2da: Feat definitions
    ///   - featgain.2da: Feat gain progression
    ///   - spells.2da: Force power definitions
    ///   - skills.2da: Skill definitions
    ///   - surfacemat.2da: Walkmesh surface materials
    ///   - portraits.2da: Character portraits
    ///   - placeables.2da: Placeable object appearances
    ///   - genericdoors.2da: Door models
    ///   - repute.2da: Faction relationships
    ///   - partytable.2da: Party member definitions
    /// - Based on 2DA file format documentation in vendor/PyKotor/wiki/
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
                ResourceResult resource = _installation.Resources.LookupResource(tableName, AuroraEngine.Common.Resources.ResourceType.TwoDA);
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
                HitDie = row.GetInteger("hitdie") ?? 8,
                AttackBonusTable = row.GetString("attackbonustable"),
                FeatsBonusTable = row.GetString("featstable"),
                SavingThrowTable = row.GetString("savingthrowtable"),
                SkillsPerLevel = row.GetInteger("skillpointbase") ?? 1,
                PrimaryAbility = row.GetString("primaryabil"),
                SpellCaster = row.GetInteger("spellcaster") == 1,
                ForceUser = row.GetInteger("forcedie") > 0
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
                EquipableSlots = row.GetInteger("equipableslots") ?? 0,
                DefaultModel = row.GetString("defaultmodel"),
                WeaponType = row.GetInteger("weapontype") ?? 0,
                DamageType = row.GetInteger("damagetype") ?? 0,
                NumDice = row.GetInteger("numdice") ?? 0,
                DieToRoll = row.GetInteger("dietoroll") ?? 0,
                CriticalThreat = row.GetInteger("criticalthreat") ?? 20,
                CriticalMultiplier = row.GetInteger("critmultiplier") ?? 2,
                BaseCost = row.GetInteger("basecost") ?? 0,
                MaxStack = row.GetInteger("stacking") ?? 1
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
                PrereqFeat1 = row.GetInteger("prereqfeat1") ?? -1,
                PrereqFeat2 = row.GetInteger("prereqfeat2") ?? -1,
                MinLevel = row.GetInteger("minlevel") ?? 1,
                MinLevelClass = row.GetInteger("minlevelclass") ?? -1,
                Selectable = row.GetInteger("allclassescanuse") == 1 || row.GetInteger("selectable") == 1
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
                Walk = row.GetInteger("walk") == 1,
                WalkCheck = row.GetInteger("walkcheck") == 1,
                LineOfSight = row.GetInteger("lineofsight") == 1,
                Grass = row.GetInteger("grass") == 1,
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
                SoundAppType = row.GetInteger("soundapptype") ?? 0
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
                SoundAppType = row.GetInteger("soundapptype") ?? 0,
                BlockSight = row.GetInteger("blocksight") == 1
            };
        }

        #endregion

        #region Spell Data

        /// <summary>
        /// Gets spell/force power data from spells.2da.
        /// </summary>
        /// <remarks>
        /// Spell Data Access:
        /// - Based on swkotor2.exe spell system
        /// - Located via string references: "spells.2da" @ 0x007c2e60
        /// - Original implementation: Loads spell data from spells.2da for Force powers
        /// - Spell ID is row index in spells.2da
        /// - Based on spells.2da format documentation in vendor/PyKotor/wiki/2DA-spells.md
        /// </remarks>
        [CanBeNull]
        public SpellData GetSpell(int spellId)
        {
            TwoDA table = GetTable("spells");
            if (table == null || spellId < 0 || spellId >= table.GetHeight())
            {
                return null;
            }

            TwoDARow row = table.GetRow(spellId);
            return new SpellData
            {
                RowIndex = spellId,
                Label = row.Label(),
                NameStrRef = row.GetInteger("name") ?? -1,
                DescriptionStrRef = row.GetInteger("spelldesc") ?? row.GetInteger("description") ?? -1,
                Icon = row.GetString("iconresref") ?? row.GetString("icon") ?? string.Empty,
                ConjTime = row.GetFloat("conjtime") ?? 0f,
                CastTime = row.GetFloat("casttime") ?? 0f,
                Range = row.GetInteger("range") ?? 0,
                TargetType = row.GetInteger("targettype") ?? 0,
                HostileSetting = row.GetInteger("hostilesetting") ?? 0,
                ImpactScript = row.GetString("impactscript") ?? string.Empty,
                Projectile = row.GetString("projectile") ?? string.Empty,
                ProjectileModel = row.GetString("projmodel") ?? row.GetString("projectilemodel") ?? string.Empty,
                ConjHandVfx = row.GetInteger("conjhandvfx") ?? row.GetInteger("casthandvisual") ?? 0,
                ConjHeadVfx = row.GetInteger("conjheadvfx") ?? 0,
                ConjGrndVfx = row.GetInteger("conjgrndvfx") ?? 0,
                ConjCastVfx = row.GetInteger("conjcastvfx") ?? 0,
                Innate = row.GetInteger("innate") ?? 0,
                FeatId = row.GetInteger("featid") ?? -1
            };
        }

        /// <summary>
        /// Gets the Force point cost for a spell.
        /// </summary>
        /// <remarks>
        /// Force Point Cost:
        /// - Based on swkotor2.exe Force point calculation
        /// - Located via string references: "GetSpellBaseForcePointCost" @ 0x007c2e60
        /// - Original implementation: Calculates base Force point cost from spell level and innate value
        /// - Base cost = spell level (from innate column) * 2, minimum 1
        /// - Some spells have fixed costs in feat.2da (forcepoints column)
        /// </remarks>
        public int GetSpellForcePointCost(int spellId)
        {
            SpellData spell = GetSpell(spellId);
            if (spell == null)
            {
                return 0;
            }

            // Check if spell has a feat with Force point cost
            if (spell.FeatId >= 0)
            {
                FeatData feat = GetFeat(spell.FeatId);
                if (feat != null)
                {
                    TwoDA featTable = GetTable("feat");
                    if (featTable != null)
                    {
                        TwoDARow featRow = featTable.GetRow(spell.FeatId);
                        int? forcePoints = featRow.GetInteger("forcepoints");
                        if (forcePoints.HasValue && forcePoints.Value > 0)
                        {
                            return forcePoints.Value;
                        }
                    }
                }
            }

            // Base cost calculation: spell level * 2, minimum 1
            int spellLevel = spell.Innate;
            if (spellLevel <= 0)
            {
                spellLevel = 1; // Default to level 1 if not specified
            }

            return Math.Max(1, spellLevel * 2);
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
        public int AppearanceId { get { return RowIndex; } set { RowIndex = value; } }
        public string Label { get; set; }
        public string ModelType { get; set; }
        public string ModelA { get; set; }
        public string ModelB { get; set; }
        public string TexA { get; set; }
        public string TexB { get; set; }
        public string Race { get; set; }
        public int RacialType { get; set; }
        public float WalkSpeed { get; set; }
        public float WalkDist { get { return WalkSpeed; } set { WalkSpeed = value; } }
        public float RunSpeed { get; set; }
        public float RunDist { get { return RunSpeed; } set { RunSpeed = value; } }
        public float PerceptionRange { get; set; }
        public int PerceptionDist { get { return (int)PerceptionRange; } set { PerceptionRange = value; } }
        public float Height { get; set; }
        public int SizeCategory { get; set; }
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
        public int BaseItemId { get { return RowIndex; } set { RowIndex = value; } }
        public string Label { get; set; }
        public string Name { get; set; }
        public int EquipableSlots { get; set; }
        public string DefaultIcon { get; set; }
        public string DefaultModel { get; set; }
        public int WeaponType { get; set; }
        public int WeaponWield { get; set; }
        public int DamageType { get; set; }
        public int DamageFlags { get; set; }
        public int WeaponSize { get; set; }
        public int NumDice { get; set; }
        public int DieToRoll { get; set; }
        public int CriticalThreat { get; set; }
        public int CriticalMultiplier { get; set; }
        public int BaseCost { get; set; }
        public int MaxStack { get; set; }
        public bool RangedWeapon { get; set; }
        public int MaxRange { get; set; }
        public int MinRange { get; set; }
    }

    /// <summary>
    /// Feat data from feat.2da.
    /// </summary>
    public class FeatData
    {
        public int RowIndex { get; set; }
        public int FeatId { get { return RowIndex; } set { RowIndex = value; } }
        public string Label { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int DescriptionStrRef { get; set; }
        public string Icon { get; set; }
        public int FeatCategory { get; set; }
        public int MaxRanks { get; set; }
        public int PrereqFeat1 { get; set; }
        public int PrereqFeat2 { get; set; }
        public int MinLevel { get; set; }
        public int MinLevelClass { get; set; }
        public bool Selectable { get; set; }
        public bool RequiresAction { get; set; }
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

    /// <summary>
    /// Spell/Force power data from spells.2da.
    /// </summary>
    public class SpellData
    {
        public int RowIndex { get; set; }
        public int SpellId { get { return RowIndex; } set { RowIndex = value; } }
        public string Label { get; set; }
        public int Name { get; set; } // StrRef
        public int NameStrRef { get; set; }
        public int DescriptionStrRef { get; set; }
        public string Icon { get; set; }
        public int School { get; set; }
        public float ConjTime { get; set; }
        public float CastTime { get; set; }
        public float CastingTime { get { return CastTime; } set { CastTime = value; } }
        public int Range { get; set; }
        public int TargetType { get; set; }
        public int HostileSetting { get; set; }
        public string ImpactScript { get; set; }
        public int SpellLevel { get; set; }
        public string Projectile { get; set; }
        public string ProjectileModel { get; set; }
        public int ConjHandVfx { get; set; }
        public int ConjHeadVfx { get; set; }
        public int ConjGrndVfx { get; set; }
        public int ConjCastVfx { get; set; }
        public int Innate { get; set; }
        public int FeatId { get; set; }
    }

    #endregion
}
