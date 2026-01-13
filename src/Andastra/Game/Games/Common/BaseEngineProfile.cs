using System;
using System.Collections.Generic;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Game.Scripting.Interfaces;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Abstract base class for game profiles across all engines.
    /// </summary>
    /// <remarks>
    /// Base Engine Profile:
    /// - Common functionality shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (OdysseyEngineProfile, AuroraEngineProfile, EclipseEngineProfile)
    ///
    /// Common Profile Patterns (Reverse Engineered):
    /// - Resource Path Initialization: All engines initialize resource paths in constructor
    ///   - Pattern: All engines use similar resource path setup functions that register directory aliases
    ///   - Common directories: modules, override, saves (present in all engines)
    /// - Profile Initialization Pattern: All engines follow same initialization sequence
    ///   1. Create resource configuration (game-specific file names, paths)
    ///   2. Create table configuration (2DA table schemas)
    ///   3. Initialize supported features set (game-specific feature flags)
    /// - Feature Detection: All engines support feature queries via SupportsFeature()
    ///   - Pattern: HashSet-based feature lookup with null/empty string validation
    ///   - Used to determine game-specific capabilities (e.g., Influence system in K2, Pazaak in K1)
    /// - Game Type Identification: All engines identify themselves via GameType property
    ///   - Pattern: Game identification via executable name, INI file names, or registry keys
    ///
    /// Inheritance Structure:
    /// - BaseEngineProfile (this class): Common initialization, feature detection, resource/table config access
    /// - OdysseyEngineProfile: Odyssey-specific resource paths (chitin.key, dialog.tlk, texture packs)
    ///   - OdysseyKotor1GameProfile: K1-specific features (Pazaak, Swoop Racing, Turret)
    ///   - OdysseyTheSithLordsGameProfile: K2-specific features (Influence, Prestige Classes, Crafting)
    /// - AuroraEngineProfile: Aurora-specific resource paths (hak files, module files)
    /// - EclipseEngineProfile: Eclipse-specific resource paths (packages/core/override structure)
    /// - Profile: Infinity-specific resource paths (CHITIN.KEY, dialog.tlk variants)
    /// </remarks>
    public abstract class BaseEngineProfile : IEngineProfile
    {
        protected readonly IResourceConfig _resourceConfig;
        protected readonly ITableConfig _tableConfig;
        protected readonly HashSet<string> _supportedFeatures;

        /// <summary>
        /// Initializes a new instance of the BaseEngineProfile class.
        /// </summary>
        /// <remarks>
        /// Initialization Pattern (Common across all engines):
        /// - Pattern: All engines follow this initialization sequence:
        ///   1. Create resource configuration (game-specific file names, paths)
        ///   2. Create table configuration (2DA table schemas)
        ///   3. Initialize supported features set (game-specific feature flags)
        /// </remarks>
        protected BaseEngineProfile()
        {
            _resourceConfig = CreateResourceConfig();
            _tableConfig = CreateTableConfig();
            _supportedFeatures = new HashSet<string>();
            InitializeSupportedFeatures();

            // Common validation: Ensure resource and table configs are not null
            if (_resourceConfig == null)
            {
                throw new InvalidOperationException("CreateResourceConfig() must not return null");
            }

            if (_tableConfig == null)
            {
                throw new InvalidOperationException("CreateTableConfig() must not return null");
            }
        }

        public abstract string GameType { get; }

        public abstract string Name { get; }

        public abstract EngineFamily EngineFamily { get; }

        public abstract IEngineApi CreateEngineApi();

        public IResourceConfig ResourceConfig
        {
            get { return _resourceConfig; }
        }

        public ITableConfig TableConfig
        {
            get { return _tableConfig; }
        }

        public bool SupportsFeature(string feature)
        {
            if (string.IsNullOrEmpty(feature))
            {
                return false;
            }

            return _supportedFeatures.Contains(feature);
        }

        protected abstract IResourceConfig CreateResourceConfig();

        protected abstract ITableConfig CreateTableConfig();

        protected abstract void InitializeSupportedFeatures();
    }
}
