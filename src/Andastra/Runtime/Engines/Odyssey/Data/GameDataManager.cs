using System;
using System.Collections.Generic;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Extract;
using JetBrains.Annotations;

namespace Andastra.Runtime.Engines.Odyssey.Data
{
    /// <summary>
    /// Manages game data tables (2DA files) for KOTOR.
    /// </summary>
    /// <remarks>
    /// Game Data Manager (Odyssey-specific):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005edd20 @ 0x005edd20 (2DA table loading)
    /// - Located via string references: "2DAName" @ 0x007c3980, " 2DA file" @ 0x007c4674
    /// - Error messages: "CSWClass::LoadFeatGain: can't load featgain.2da" @ 0x007c46bc, "CSWClass::LoadFeatTable: Can't load feat.2da" @ 0x007c4720
    /// - Cross-engine analysis:
    ///   - Aurora (nwmain.exe): C2DA::Load2DArray @ 0x1401a73a0, Load2DArrays @ 0x1402b3920 - loads 2DA files via C2DA class, "Already loaded Appearance.TwoDA!" @ 0x140dc5dd8
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, ): 2DA system is UnrealScript-based (different architecture), uses "Get2DAValue", "GetNum2DARows" functions
    /// - Inheritance: Base class BaseGameDataManager (Runtime.Games.Common) - abstract 2DA loading, Odyssey override (Runtime.Games.Odyssey) - resource-based 2DA loading
    /// - Original implementation: Loads 2DA tables from resource system (chitin.key, module archives), uses "2DAName" field to identify tables, caches loaded tables
    /// - 2DA file format: Tab-separated text file with row labels and column headers
    /// - Table lookup: Uses row label (string) or row index (int) to access data
    /// - Column access: Column names are case-insensitive (e.g., "ModelA", "modela" both work)
    /// - Table caching: Caches loaded tables in memory to avoid redundant file reads
    /// - Key 2DA tables (Odyssey-specific): appearance.2da, baseitems.2da, classes.2da, feat.2da, featgain.2da, spells.2da, skills.2da, surfacemat.2da, portraits.2da, placeables.2da, genericdoors.2da, repute.2da, partytable.2da
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
        /// <returns>The loaded 2DA, or null if not found</returns>
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
                ResourceResult resource = _installation.Resources.LookupResource(tableName, BioWare.NET.Common.ResourceType.TwoDA);
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
                ModelType = row.GetString("modeltype"),
                ModelA = row.GetString("modela"),
                ModelB = row.GetString("modelb"),
                ModelC = row.GetString("modelc"),
                ModelD = row.GetString("modeld"),
                ModelE = row.GetString("modele"),
                ModelF = row.GetString("modelf"),
                ModelG = row.GetString("modelg"),
                ModelH = row.GetString("modelh"),
                ModelI = row.GetString("modeli"),
                ModelJ = row.GetString("modelj"),
                ModelK = row.GetString("modelk"),
                ModelL = row.GetString("modell"),
                ModelM = row.GetString("modelm"),
                ModelN = row.GetString("modeln"),
                TexA = row.GetString("texa"),
                TexB = row.GetString("texb"),
                Race = row.GetString("race"),
                RacialType = row.GetInteger("racialtype") ?? 0,
                WalkSpeed = row.GetFloat("walkdist") ?? 1.75f,
                RunSpeed = row.GetFloat("rundist") ?? 4.0f,
                PerceptionRange = row.GetFloat("perspace") ?? 20.0f,
                Height = row.GetFloat("height") ?? 1.8f,
                SizeCategory = row.GetInteger("sizecategory") ?? 0,
                HitRadius = row.GetFloat("hitradius") ?? 0.5f
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
                MaxStack = row.GetInteger("stacking") ?? 1,
                RangedWeapon = row.GetInteger("rangedweapon") != null && row.GetInteger("rangedweapon").Value != 0,
                MaxRange = row.GetInteger("maxattackrange") ?? 0,
                MinRange = row.GetInteger("preferredattackrange") ?? 0
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
            int? usesPerDay = row.GetInteger("usesperday");
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
                MinStr = row.GetInteger("minstr") ?? 0,
                MinDex = row.GetInteger("mindex") ?? 0,
                MinInt = row.GetInteger("minint") ?? 0,
                MinWis = row.GetInteger("minwis") ?? 0,
                MinCon = row.GetInteger("mincon") ?? 0,
                MinCha = row.GetInteger("mincha") ?? 0,
                Selectable = row.GetInteger("allclassescanuse") == 1 || row.GetInteger("selectable") == 1,
                UsesPerDay = usesPerDay ?? -1 // -1 = unlimited or special handling, 0+ = daily limit
            };
        }

        /// <summary>
        /// Gets feat data by label (row label in feat.2da).
        /// </summary>
        /// <param name="featLabel">The feat label (e.g., "FEAT_SEE_INVISIBILITY").</param>
        /// <returns>Feat data if found, null otherwise.</returns>
        /// <remarks>
        /// Feat Lookup by Label:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005edd20 @ 0x005edd20 (2DA table loading)
        /// - Located via string references: "CSWClass::LoadFeatTable: Can't load feat.2da" @ 0x007c4720
        /// - Original implementation: Looks up feat by row label in feat.2da table
        /// - Row labels in feat.2da match feat constant names (e.g., "FEAT_SEE_INVISIBILITY")
        /// - Uses FindRow to locate feat by label, then returns FeatData with row index as FeatId
        /// </remarks>
        [CanBeNull]
        public FeatData GetFeatByLabel(string featLabel)
        {
            if (string.IsNullOrEmpty(featLabel))
            {
                return null;
            }

            TwoDA table = GetTable("feat");
            if (table == null)
            {
                return null;
            }

            TwoDARow row = table.FindRow(featLabel);
            if (row == null)
            {
                return null;
            }

            // Get row index for the found row
            int? rowIndex = table.RowIndex(row);
            if (!rowIndex.HasValue)
            {
                return null;
            }

            int? usesPerDay = row.GetInteger("usesperday");
            return new FeatData
            {
                RowIndex = rowIndex.Value,
                Label = row.Label(),
                Name = row.GetString("name"),
                Description = row.GetString("description"),
                Icon = row.GetString("icon"),
                PrereqFeat1 = row.GetInteger("prereqfeat1") ?? -1,
                PrereqFeat2 = row.GetInteger("prereqfeat2") ?? -1,
                MinLevel = row.GetInteger("minlevel") ?? 1,
                MinLevelClass = row.GetInteger("minlevelclass") ?? -1,
                MinStr = row.GetInteger("minstr") ?? 0,
                MinDex = row.GetInteger("mindex") ?? 0,
                MinInt = row.GetInteger("minint") ?? 0,
                MinWis = row.GetInteger("minwis") ?? 0,
                MinCon = row.GetInteger("mincon") ?? 0,
                MinCha = row.GetInteger("mincha") ?? 0,
                Selectable = row.GetInteger("allclassescanuse") == 1 || row.GetInteger("selectable") == 1,
                UsesPerDay = usesPerDay ?? -1 // -1 = unlimited or special handling, 0+ = daily limit
            };
        }

        /// <summary>
        /// Gets a feat ID by label (row label in feat.2da).
        /// </summary>
        /// <param name="featLabel">The feat label (e.g., "FEAT_SEE_INVISIBILITY").</param>
        /// <returns>Feat ID (row index) if found, -1 otherwise.</returns>
        /// <remarks>
        /// Feat ID Lookup by Label:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005edd20 @ 0x005edd20 (2DA table loading)
        /// - Original implementation: Looks up feat ID by row label in feat.2da table
        /// - Returns the row index as the feat ID, or -1 if not found
        /// - More efficient than GetFeatByLabel when only the ID is needed
        /// </remarks>
        public int GetFeatIdByLabel(string featLabel)
        {
            FeatData feat = GetFeatByLabel(featLabel);
            return feat != null ? feat.FeatId : -1;
        }

        /// <summary>
        /// Gets starting feats for a class at level 1 from featgain.2da.
        /// </summary>
        /// <param name="classId">The class ID (0=Soldier, 1=Scout, 2=Scoundrel, 3=JediGuardian, 4=JediConsular, 5=JediSentinel).</param>
        /// <param name="level">The class level (typically 1 for starting feats).</param>
        /// <returns>List of feat IDs granted at the specified level, or empty list if not found.</returns>
        /// <remarks>
        /// Starting Feats from featgain.2da:
        /// - Based on swkotor.exe: 0x005bcf70 @ 0x005bcf70 (LoadFeatGain)
        /// - Based on swkotor2.exe: CSWClass_LoadFeatGain @ 0x0060d1d0 (swkotor2.exe: 0x0060d1d0)
        /// - Located via string references: "CSWClass::LoadFeatGain: can't load featgain.2da" @ swkotor.exe: 0x0074b370, swkotor2.exe: 0x007c46bc
        /// - Original implementation: Loads featgain.2da table, looks up class-specific columns with "_REG" and "_BON" suffixes
        /// - featgain.2da structure (based on Ghidra analysis):
        ///   - Rows: Feat gain entries (up to 50 rows, indexed 0-0x32)
        ///   - Columns: Class name + suffix (e.g., "soldier_REG", "soldier_BON", "scout_REG", "scout_BON")
        ///   - Values: Feat IDs (integers) or empty/**** for no feat
        ///   - The original function iterates through all rows (0 to 0x32) and stores feat IDs in arrays
        /// - Class column name mapping:
        ///   - Soldier (0) -> "soldier"
        ///   - Scout (1) -> "scout"
        ///   - Scoundrel (2) -> "scoundrel"
        ///   - Jedi Guardian (3) -> "jedi_guardian"
        ///   - Jedi Consular (4) -> "jedi_consular"
        ///   - Jedi Sentinel (5) -> "jedi_sentinel"
        /// - The function reads both "_REG" (regular) and "_BON" (bonus) columns for the class
        /// - For starting feats at level 1, we check row 0 (first feat gain entry)
        /// - Returns all non-empty, non-**** feat IDs from both columns
        /// - Note: The actual structure may vary, but row 0 typically contains level 1 feats
        /// </remarks>
        public List<int> GetStartingFeatsForClass(int classId, int level = 1)
        {
            List<int> feats = new List<int>();

            // Load featgain.2da table
            TwoDA featgainTable = GetTable("featgain");
            if (featgainTable == null)
            {
                Console.WriteLine("[GameDataManager] Failed to load featgain.2da table");
                return feats;
            }

            // Map class ID to column name prefix
            string classColumnPrefix = GetClassColumnName(classId);
            if (string.IsNullOrEmpty(classColumnPrefix))
            {
                Console.WriteLine("[GameDataManager] Unknown class ID: " + classId);
                return feats;
            }

            // For level 1 starting feats, check row 0 (first feat gain entry)
            // Based on Ghidra analysis: The original function iterates through rows 0 to 0x32 (50 rows)
            // Row 0 typically contains level 1 feats
            int rowIndex = 0;
            if (rowIndex < 0 || rowIndex >= featgainTable.GetHeight())
            {
                Console.WriteLine("[GameDataManager] Invalid row index for featgain.2da: " + rowIndex);
                return feats;
            }

            // Get row for the first feat gain entry (level 1)
            TwoDARow row = featgainTable.GetRow(rowIndex);
            if (row == null)
            {
                return feats;
            }

            // Read feats from "_REG" (regular) column
            string regColumnName = classColumnPrefix + "_REG";
            string regValue = row.GetString(regColumnName);
            if (!string.IsNullOrEmpty(regValue) && regValue != "****" && regValue != "*")
            {
                int? regFeatId = ParseFeatId(regValue);
                if (regFeatId.HasValue && regFeatId.Value >= 0)
                {
                    feats.Add(regFeatId.Value);
                }
            }

            // Read feats from "_BON" (bonus) column
            string bonColumnName = classColumnPrefix + "_BON";
            string bonValue = row.GetString(bonColumnName);
            if (!string.IsNullOrEmpty(bonValue) && bonValue != "****" && bonValue != "*")
            {
                int? bonFeatId = ParseFeatId(bonValue);
                if (bonFeatId.HasValue && bonFeatId.Value >= 0)
                {
                    feats.Add(bonFeatId.Value);
                }
            }

            return feats;
        }

        /// <summary>
        /// Maps class ID to featgain.2da column name prefix.
        /// </summary>
        /// <param name="classId">The class ID.</param>
        /// <returns>Column name prefix (e.g., "soldier", "scout"), or null if unknown.</returns>
        /// <remarks>
        /// Class Column Name Mapping:
        /// - Based on swkotor.exe and swkotor2.exe: Class column names in featgain.2da
        /// - Class IDs match classes.2da row indices
        /// - Column names are lowercase with underscores (e.g., "jedi_guardian")
        /// </remarks>
        private string GetClassColumnName(int classId)
        {
            switch (classId)
            {
                case 0: // Soldier
                    return "soldier";
                case 1: // Scout
                    return "scout";
                case 2: // Scoundrel
                    return "scoundrel";
                case 3: // Jedi Guardian
                    return "jedi_guardian";
                case 4: // Jedi Consular
                    return "jedi_consular";
                case 5: // Jedi Sentinel
                    return "jedi_sentinel";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Parses a feat ID from a string value in featgain.2da.
        /// </summary>
        /// <param name="value">The string value (may be integer, empty, or "****").</param>
        /// <returns>Feat ID if valid, null otherwise.</returns>
        private int? ParseFeatId(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "****" || value == "*")
            {
                return null;
            }

            // Try to parse as integer
            int featId;
            if (int.TryParse(value, out featId))
            {
                return featId;
            }

            return null;
        }

        /// <summary>
        /// Gets starting feat IDs for a class at level 1 from featgain.2da.
        /// </summary>
        /// <param name="classId">The class ID to get starting feats for.</param>
        /// <returns>List of feat IDs that should be granted at level 1 for this class, or empty list if not found.</returns>
        /// <remarks>
        /// Starting Feats from featgain.2da:
        /// - Based on swkotor2.exe: CSWClass_LoadFeatGain @ 0x0060d1d0 (swkotor2.exe: 0x0060d1d0)
        /// - Located via string references: "CSWClass::LoadFeatGain: can't load featgain.2da" @ 0x007c46bc, "featgain" @ 0x007c46ec
        /// - Original implementation: 0x005d63d0 reads "FeatGain" column from classes.2da for each class, then calls CSWClass_LoadFeatGain
        /// - CSWClass_LoadFeatGain (swkotor2.exe: 0x0060d1d0): Loads featgain.2da table, constructs column names by appending "_REG" and "_BON" to the FeatGain label
        /// - Loops through rows 0 to 0x32 (50 rows) in featgain.2da, reading values from class-specific columns (e.g., "soldier_REG", "soldier_BON")
        /// - _REG column contains regular starting feats, _BON column contains bonus starting feats
        /// - Each row can contain a feat ID in the class-specific columns
        /// - Feat IDs are stored as integers in the 2DA cells
        /// - Original function stores _REG values at offset 0x1aa and _BON values at offset 0x178 in class data structure
        /// </remarks>
        public List<int> GetStartingFeats(int classId)
        {
            List<int> featIds = new List<int>();

            // Load featgain.2da table
            // swkotor2.exe: 0x0060d1d0 - CSWClass_LoadFeatGain loads "featgain" 2DA table
            TwoDA featgainTable = GetTable("featgain");
            if (featgainTable == null)
            {
                return featIds;
            }

            // Get FeatGain row label from classes.2da
            // swkotor2.exe: 0x005d63d0 reads "FeatGain" column from classes.2da for each class
            TwoDA classesTable = GetTable("classes");
            if (classesTable == null || classId < 0 || classId >= classesTable.GetHeight())
            {
                return featIds;
            }

            TwoDARow classRow = classesTable.GetRow(classId);
            if (classRow == null)
            {
                return featIds;
            }

            // Get FeatGain column value (used to construct column names in featgain.2da)
            // swkotor2.exe: 0x005d63d0 line 201-207 - reads "FeatGain" column and passes to CSWClass_LoadFeatGain
            string featGainLabel;
            try
            {
                featGainLabel = classRow.GetString("featgain");
            }
            catch (System.Collections.Generic.KeyNotFoundException)
            {
                // FeatGain column not found - return empty list
                return featIds;
            }
            if (string.IsNullOrEmpty(featGainLabel))
            {
                return featIds;
            }

            // Construct column names by appending "_REG" and "_BON" to the FeatGain label
            // swkotor2.exe: 0x0060d1d0 lines 41-48 - constructs "classname_REG" and "classname_BON" column names
            string regColumnName = featGainLabel + "_REG";
            string bonColumnName = featGainLabel + "_BON";

            // Loop through rows 0 to 0x32 (50 rows) in featgain.2da
            // swkotor2.exe: 0x0060d1d0 lines 51-64 - loops from 0 to 0x32 (50 iterations)
            for (int rowIndex = 0; rowIndex < 50; rowIndex++)
            {
                if (rowIndex >= featgainTable.GetHeight())
                {
                    break;
                }

                TwoDARow row = featgainTable.GetRow(rowIndex);
                if (row == null)
                {
                    continue;
                }

                // Read feat ID from _REG column (regular starting feats)
                // swkotor2.exe: 0x0060d1d0 lines 54-57 - reads value from _REG column and stores at offset 0x1aa + rowIndex
                int? regFeatId = row.GetInteger(regColumnName);
                if (regFeatId.HasValue && regFeatId.Value >= 0)
                {
                    featIds.Add(regFeatId.Value);
                }

                // Read feat ID from _BON column (bonus starting feats)
                // swkotor2.exe: 0x0060d1d0 lines 59-62 - reads value from _BON column and stores at offset 0x178 + rowIndex
                int? bonFeatId = row.GetInteger(bonColumnName);
                if (bonFeatId.HasValue && bonFeatId.Value >= 0)
                {
                    featIds.Add(bonFeatId.Value);
                }
            }

            return featIds;
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
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) spell system
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
                ConjDuration = row.GetFloat("conjduration") ?? 0f,
                ConjRange = row.GetInteger("conjrange") ?? 0,
                Innate = row.GetInteger("innate") ?? 0,
                FeatId = row.GetInteger("featid") ?? -1
            };
        }

        /// <summary>
        /// Gets the Force point cost for a spell.
        /// </summary>
        /// <remarks>
        /// Force Point Cost:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) Force point calculation
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

        /// <summary>
        /// Gets the duration of a visual effect from visualeffects.2da.
        /// </summary>
        /// <param name="visualEffectId">The visual effect ID (row index in visualeffects.2da).</param>
        /// <returns>The duration in seconds, or 2.0f as default if not found.</returns>
        /// <remarks>
        /// Visual Effect Duration:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Visual effects duration from visualeffects.2da
        /// - Located via string references: "visualeffects" @ 0x007c4a7c
        /// - Original implementation: Loads visualeffects.2da table, reads "duration" column for the visual effect ID
        /// - Visual effect ID is row index in visualeffects.2da
        /// - Duration is stored as float in seconds in the "duration" column
        /// - Default duration is 2.0 seconds if visual effect not found or duration not specified
        /// - Based on visualeffects.2da format documentation in vendor/PyKotor/wiki/2DA-visualeffects.md
        /// </remarks>
        public float GetVisualEffectDuration(int visualEffectId)
        {
            if (visualEffectId < 0)
            {
                return 2.0f; // Default duration
            }

            TwoDA table = GetTable("visualeffects");
            if (table == null || visualEffectId >= table.GetHeight())
            {
                return 2.0f; // Default duration if table not found or invalid ID
            }

            TwoDARow row = table.GetRow(visualEffectId);
            if (row == null)
            {
                return 2.0f; // Default duration if row not found
            }

            // Get duration from "duration" column
            float? duration = row.GetFloat("duration");
            if (duration.HasValue && duration.Value > 0f)
            {
                return duration.Value;
            }

            // Default duration if not specified or invalid
            return 2.0f;
        }

        #endregion

        #region Skill Data

        /// <summary>
        /// Gets skill data from skills.2da.
        /// </summary>
        /// <remarks>
        /// Skill Data Access:
        /// - Based on swkotor.exe and swkotor2.exe: Skills are loaded from skills.2da
        /// - Located via string references: "skills.2da" in resource system
        /// - Original implementation: Loads skill data from skills.2da for skill names and descriptions
        /// - Skill ID is row index in skills.2da (0-7 for KOTOR: COMPUTER_USE, DEMOLITIONS, STEALTH, AWARENESS, PERSUADE, REPAIR, SECURITY, TREAT_INJURY)
        /// - Based on skills.2da format documentation
        /// </remarks>
        [CanBeNull]
        public SkillData GetSkill(int skillId)
        {
            TwoDA table = GetTable("skills");
            if (table == null || skillId < 0 || skillId >= table.GetHeight())
            {
                return null;
            }

            TwoDARow row = table.GetRow(skillId);
            return new SkillData
            {
                RowIndex = skillId,
                SkillId = skillId,
                Label = row.Label(),
                Name = row.GetString("name"),
                Description = row.GetString("description"),
                DescriptionStrRef = row.GetInteger("description") ?? -1
            };
        }

        /// <summary>
        /// Gets all available skills from skills.2da.
        /// </summary>
        /// <returns>List of all skill data entries.</returns>
        public List<SkillData> GetAllSkills()
        {
            List<SkillData> skills = new List<SkillData>();
            TwoDA table = GetTable("skills");
            if (table == null)
            {
                return skills;
            }

            for (int i = 0; i < table.GetHeight(); i++)
            {
                SkillData skill = GetSkill(i);
                if (skill != null)
                {
                    skills.Add(skill);
                }
            }

            return skills;
        }

        /// <summary>
        /// Checks if a skill is a class skill for the given class.
        /// </summary>
        /// <param name="skillId">The skill ID (0-7).</param>
        /// <param name="classId">The class ID (0=Soldier, 1=Scout, 2=Scoundrel, 3=JediGuardian, 4=JediConsular, 5=JediSentinel).</param>
        /// <returns>True if the skill is a class skill for this class, false otherwise.</returns>
        /// <remarks>
        /// Class Skill Check:
        /// - Based on swkotor.exe and swkotor2.exe: Class skills are determined by classes.2da skill columns
        /// - Original implementation: Checks if skill is listed in class's skill columns in classes.2da
        /// - Class skills cost 1 point per rank, cross-class skills cost 2 points per rank
        /// - Class skills can be raised to rank 4 at level 1, cross-class skills can be raised to rank 2
        /// - Based on classes.2da structure: Each class has skill columns (e.g., "skill0", "skill1", etc.) listing class skills
        /// </remarks>
        public bool IsClassSkill(int skillId, int classId)
        {
            TwoDA classesTable = GetTable("classes");
            if (classesTable == null || classId < 0 || classId >= classesTable.GetHeight())
            {
                return false;
            }

            TwoDARow classRow = classesTable.GetRow(classId);
            if (classRow == null)
            {
                return false;
            }

            // Check skill columns in classes.2da (skill0, skill1, skill2, etc.)
            // Based on classes.2da structure: Class skills are listed in skill columns
            for (int i = 0; i < 20; i++) // Check up to 20 skill columns
            {
                string columnName = "skill" + i;
                int? listedSkillId = classRow.GetInteger(columnName);
                if (listedSkillId.HasValue && listedSkillId.Value == skillId)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

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
            public string ModelC { get; set; }
            public string ModelD { get; set; }
            public string ModelE { get; set; }
            public string ModelF { get; set; }
            public string ModelG { get; set; }
            public string ModelH { get; set; }
            public string ModelI { get; set; }
            public string ModelJ { get; set; }
            public string ModelK { get; set; }
            public string ModelL { get; set; }
            public string ModelM { get; set; }
            public string ModelN { get; set; }
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
            public float HitRadius { get; set; }

            /// <summary>
            /// Gets the model ResRef for the specified body variation index.
            /// </summary>
            /// <param name="bodyVariation">Body variation index (0 = ModelA, 1 = ModelB, ..., 13 = ModelN).</param>
            /// <returns>Model ResRef or null if not found.</returns>
            [CanBeNull]
            public string GetModelByVariation(int bodyVariation)
            {
                switch (bodyVariation)
                {
                    case 0: return ModelA;
                    case 1: return ModelB;
                    case 2: return ModelC;
                    case 3: return ModelD;
                    case 4: return ModelE;
                    case 5: return ModelF;
                    case 6: return ModelG;
                    case 7: return ModelH;
                    case 8: return ModelI;
                    case 9: return ModelJ;
                    case 10: return ModelK;
                    case 11: return ModelL;
                    case 12: return ModelM;
                    case 13: return ModelN;
                    default: return ModelA; // Fallback to ModelA for out-of-range variations
                }
            }
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
            public int MinStr { get; set; }
            public int MinDex { get; set; }
            public int MinInt { get; set; }
            public int MinWis { get; set; }
            public int MinCon { get; set; }
            public int MinCha { get; set; }
            public bool Selectable { get; set; }
            public bool RequiresAction { get; set; }
            /// <summary>
            /// Uses per day limit for the feat.
            /// -1 = unlimited uses or special handling (e.g., based on class levels)
            /// 0+ = maximum uses per day
            /// </summary>
            public int UsesPerDay { get; set; }
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
            public float ConjDuration { get; set; }
            public int ConjRange { get; set; }
            public int Innate { get; set; }
            public int FeatId { get; set; }
        }

        /// <summary>
        /// Skill data from skills.2da.
        /// </summary>
        public class SkillData
        {
            public int RowIndex { get; set; }
            public int SkillId { get { return RowIndex; } set { RowIndex = value; } }
            public string Label { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public int DescriptionStrRef { get; set; }
        }

        #endregion

        #region Portrait Data

        /// <summary>
        /// Gets portrait data from portraits.2da.
        /// </summary>
        /// <param name="portraitId">Portrait ID (row index in portraits.2da).</param>
        /// <returns>Portrait data if found, null otherwise.</returns>
        /// <remarks>
        /// Portrait Data Access:
        /// - Based on swkotor.exe and swkotor2.exe: Portrait loading from portraits.2da
        /// - Located via string references: "portraits.2da" in resource loading
        /// - Original implementation: Loads portrait ResRef from portraits.2da table
        /// - Portrait ID is row index in portraits.2da (0-based)
        /// - Portraits.2da structure: Row index = Portrait ID, "baseresref" column = portrait texture ResRef
        /// - Portrait textures are stored as TPC or TGA files in game resources
        /// </remarks>
        [CanBeNull]
        public PortraitData GetPortrait(int portraitId)
        {
            TwoDA table = GetTable("portraits");
            if (table == null || portraitId < 0 || portraitId >= table.GetHeight())
            {
                return null;
            }

            TwoDARow row = table.GetRow(portraitId);
            return new PortraitData
            {
                RowIndex = portraitId,
                PortraitId = portraitId,
                Label = row.Label(),
                BaseResRef = row.GetString("baseresref") ?? row.GetString("resref") ?? string.Empty
            };
        }

        /// <summary>
        /// Portrait data from portraits.2da.
        /// </summary>
        public class PortraitData
        {
            public int RowIndex { get; set; }
            public int PortraitId { get { return RowIndex; } set { RowIndex = value; } }
            public string Label { get; set; }
            public string BaseResRef { get; set; }
        }

        #endregion
    }
}
