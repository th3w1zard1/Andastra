using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Engines.Odyssey.Profiles;
using AppearanceData = Andastra.Runtime.Engines.Odyssey.Data.GameDataManager.AppearanceData;
using BaseItemData = Andastra.Runtime.Engines.Odyssey.Data.GameDataManager.BaseItemData;
using FeatData = Andastra.Runtime.Engines.Odyssey.Data.GameDataManager.FeatData;
using SpellData = Andastra.Runtime.Engines.Odyssey.Data.GameDataManager.SpellData;

namespace Andastra.Game.Engines.Odyssey.Data
{
    /// <summary>
    /// Manages loading and caching of 2DA tables for game data lookup.
    /// </summary>
    /// <remarks>
    /// 2DA Table Manager (Odyssey-specific):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005edd20 @ 0x005edd20 (2DA table loading)
    /// - Located via string references: "2DAName" @ 0x007c3980, " 2DA file" @ 0x007c4674
    /// - Error messages: "CSWClass::LoadFeatGain: can't load featgain.2da" @ 0x007c46bc, "CSWClass::LoadFeatTable: Can't load feat.2da" @ 0x007c4720
    /// - Cross-engine analysis:
    ///   - Aurora (nwmain.exe): C2DA::Load2DArray @ 0x1401a73a0, Load2DArrays @ 0x1402b3920 - loads 2DA files via C2DA class
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, ): 2DA system is UnrealScript-based (different architecture), uses "Get2DAValue", "GetNum2DARows" functions
    /// - Inheritance: Base class BaseTwoDATableManager (Runtime.Games.Common) - abstract 2DA loading, Odyssey override (Runtime.Games.Odyssey) - resource-based 2DA loading
    /// - Original implementation: Loads 2DA files from chitin.key or module archives via resource system, caches loaded tables for performance
    /// - Resource precedence: override → module → chitin (via IGameResourceProvider)
    /// - Table lookup: Uses row label (string) or row index (int) to access data
    /// - Column access: Column names are case-insensitive (e.g., "ModelA", "modela" both work)
    /// - Based on BioWare.NET.Resource.Formats.TwoDA.TwoDA for parsing
    /// </remarks>
    public class TwoDATableManager
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly IGameProfile _gameProfile;
        private readonly Dictionary<string, TwoDA> _cachedTables;
        private readonly SemaphoreSlim _loadSemaphore;

        public TwoDATableManager(IGameResourceProvider resourceProvider, IGameProfile gameProfile)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
            _gameProfile = gameProfile ?? throw new ArgumentNullException("gameProfile");
            _cachedTables = new Dictionary<string, TwoDA>(StringComparer.OrdinalIgnoreCase);
            _loadSemaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Loads a 2DA table by name (e.g., "appearance", "baseitems").
        /// </summary>
        public async Task<TwoDA> LoadTableAsync(string tableName, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", "tableName");
            }

            // Check cache first
            TwoDA cached;
            if (_cachedTables.TryGetValue(tableName, out cached))
            {
                return cached;
            }

            // Load table (with semaphore to prevent concurrent loads of same table)
            await _loadSemaphore.WaitAsync(ct);
            try
            {
                // Double-check cache after acquiring lock
                if (_cachedTables.TryGetValue(tableName, out cached))
                {
                    return cached;
                }

                // Load from resource provider
                string resRef = tableName.ToLowerInvariant();
                ResourceIdentifier resourceId = new ResourceIdentifier(resRef, ResourceType.TwoDA);

                Stream stream = await _resourceProvider.OpenResourceAsync(resourceId, ct);
                if (stream == null)
                {
                    throw new FileNotFoundException($"2DA table '{tableName}' not found in resource provider");
                }

                try
                {
                    // Read stream into byte array
                    byte[] data;
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        data = memoryStream.ToArray();
                    }

                    // Parse 2DA file using BioWare.NET
                    TwoDABinaryReader reader = new TwoDABinaryReader(data);
                    TwoDA table = reader.Load();

                    // Cache the loaded table
                    _cachedTables[tableName] = table;
                    return table;
                }
                finally
                {
                    stream.Dispose();
                }
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets a cached table, or loads it if not cached.
        /// </summary>
        public TwoDA GetTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("Table name cannot be null or empty", "tableName");
            }

            TwoDA cached;
            if (_cachedTables.TryGetValue(tableName, out cached))
            {
                return cached;
            }

            // Synchronous load (blocks until loaded)
            // Note: In production, prefer async methods
            return LoadTableAsync(tableName).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets a row from a 2DA table by row index.
        /// </summary>
        public TwoDARow GetRow(string tableName, int rowIndex)
        {
            TwoDA table = GetTable(tableName);
            return table.GetRow(rowIndex);
        }

        /// <summary>
        /// Gets a row from a 2DA table by row label.
        /// </summary>
        public TwoDARow GetRowByLabel(string tableName, string rowLabel)
        {
            TwoDA table = GetTable(tableName);
            return table.FindRow(rowLabel);
        }

        /// <summary>
        /// Gets a value from a 2DA table cell.
        /// </summary>
        public string GetCellValue(string tableName, int rowIndex, string columnName)
        {
            TwoDARow row = GetRow(tableName, rowIndex);
            return row.GetString(columnName);
        }

        /// <summary>
        /// Gets an integer value from a 2DA table cell.
        /// </summary>
        public int? GetCellInt(string tableName, int rowIndex, string columnName, int? defaultValue = null)
        {
            TwoDARow row = GetRow(tableName, rowIndex);
            return row.GetInteger(columnName, defaultValue);
        }

        /// <summary>
        /// Gets a float value from a 2DA table cell.
        /// </summary>
        public float? GetCellFloat(string tableName, int rowIndex, string columnName, float? defaultValue = null)
        {
            TwoDARow row = GetRow(tableName, rowIndex);
            return row.GetFloat(columnName, defaultValue);
        }

        /// <summary>
        /// Gets a boolean value from a 2DA table cell.
        /// </summary>
        public bool? GetCellBool(string tableName, int rowIndex, string columnName, bool? defaultValue = null)
        {
            TwoDARow row = GetRow(tableName, rowIndex);
            return row.GetBoolean(columnName, defaultValue);
        }

        #region Convenience Methods for Common Tables

        /// <summary>
        /// Gets appearance data for a creature appearance ID.
        /// </summary>
        public AppearanceData GetAppearance(int appearanceId)
        {
            TwoDARow row = GetRow("appearance", appearanceId);
            if (row == null)
            {
                return null;
            }

            return new AppearanceData
            {
                AppearanceId = appearanceId,
                Label = row.GetString("label", ""),
                ModelType = row.GetString("modeltype", ""),
                ModelA = row.GetString("modela", ""),
                ModelB = row.GetString("modelb", ""),
                TexA = row.GetString("texa", ""),
                TexB = row.GetString("texb", ""),
                Race = row.GetString("race", ""),
                RacialType = row.GetInteger("racialtype", 0) ?? 0,
                Height = row.GetFloat("height", 1.0f) ?? 1.0f,
                WalkDist = row.GetFloat("walkdist", 0.0f) ?? 0.0f,
                RunDist = row.GetFloat("rundist", 0.0f) ?? 0.0f,
                PerceptionDist = row.GetInteger("perceptiondist", 20) ?? 20,
                SizeCategory = row.GetInteger("sizecategory", 0) ?? 0,
                HitRadius = row.GetFloat("hitradius", 0.5f) ?? 0.5f
            };
        }

        /// <summary>
        /// Gets base item data for an item base item ID.
        /// </summary>
        public BaseItemData GetBaseItem(int baseItemId)
        {
            TwoDARow row = GetRow("baseitems", baseItemId);
            if (row == null)
            {
                return null;
            }

            return new BaseItemData
            {
                BaseItemId = baseItemId,
                Label = row.GetString("label", ""),
                EquipableSlots = row.GetInteger("equipableslots", 0) ?? 0,
                DefaultIcon = row.GetString("defaulticon", ""),
                DefaultModel = row.GetString("defaultmodel", ""),
                WeaponType = row.GetInteger("weapontype", 0) ?? 0,
                WeaponWield = row.GetInteger("weaponwield", 0) ?? 0,
                DamageFlags = row.GetInteger("damageflags", 0) ?? 0,
                WeaponSize = row.GetInteger("weaponsize", 0) ?? 0,
                RangedWeapon = row.GetBoolean("rangedweapon", false) ?? false,
                MaxRange = row.GetInteger("maxrange", 0) ?? 0,
                MinRange = row.GetInteger("minrange", 0) ?? 0
            };
        }

        /// <summary>
        /// Gets feat data for a feat ID.
        /// </summary>
        public FeatData GetFeat(int featId)
        {
            TwoDARow row = GetRow("feat", featId);
            if (row == null)
            {
                return null;
            }

            return new FeatData
            {
                FeatId = featId,
                Label = row.GetString("label", ""),
                DescriptionStrRef = row.GetInteger("description", 0) ?? 0, // StrRef
                Icon = row.GetString("icon", ""),
                FeatCategory = row.GetInteger("featcategory", 0) ?? 0,
                MaxRanks = row.GetInteger("maxranks", 0) ?? 0,
                MinLevel = row.GetInteger("minlevel", 0) ?? 0,
                MinLevelClass = row.GetInteger("minlevelclass", 0) ?? 0,
                RequiresAction = row.GetBoolean("requiresaction", false) ?? false
            };
        }

        /// <summary>
        /// Gets spell data for a spell ID.
        /// </summary>
        public SpellData GetSpell(int spellId)
        {
            TwoDARow row = GetRow("spells", spellId);
            if (row == null)
            {
                return null;
            }

            return new SpellData
            {
                SpellId = spellId,
                Label = row.GetString("label", ""),
                Name = row.GetInteger("name", 0) ?? 0, // StrRef
                Icon = row.GetString("icon", ""),
                School = row.GetInteger("school", 0) ?? 0,
                Range = row.GetInteger("range", 0) ?? 0,
                TargetType = row.GetInteger("targettype", 0) ?? 0,
                ImpactScript = row.GetString("impactscript", ""),
                CastingTime = row.GetFloat("castingtime", 0.0f) ?? 0.0f,
                SpellLevel = row.GetInteger("spelllevel", 0) ?? 0
            };
        }

        /// <summary>
        /// Preloads all required tables for the game profile.
        /// </summary>
        public async Task PreloadTablesAsync(CancellationToken ct = default)
        {
            ITableConfig tableConfig = _gameProfile.TableConfig;
            if (tableConfig == null)
            {
                return;
            }

            foreach (string tableName in tableConfig.RequiredTables)
            {
                await LoadTableAsync(tableName, ct);
            }
        }

        /// <summary>
        /// Clears the table cache.
        /// </summary>
        public void ClearCache()
        {
            _cachedTables.Clear();
        }

        /// <summary>
        /// Clears a specific table from the cache.
        /// </summary>
        public void ClearTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                return;
            }

            _cachedTables.Remove(tableName);
        }

        #endregion
    }
}

