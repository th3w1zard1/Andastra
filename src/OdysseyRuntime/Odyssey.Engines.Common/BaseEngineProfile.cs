using System.Collections.Generic;
using BioWareEngines.Content.Interfaces;
using BioWareEngines.Scripting.Interfaces;

namespace BioWareEngines.Engines.Common
{
    /// <summary>
    /// Abstract base class for game profiles across all engines.
    /// </summary>
    /// <remarks>
    /// Base Engine Profile:
    /// - Based on swkotor2.exe game profile system
    /// - Located via string references: "config.txt" @ 0x007b5750 (configuration file loading)
    /// - Game profile determines game-specific behavior (K1 vs K2) through resource configs and table configs
    /// - Resource config: Defines resource file locations (chitin.key, dialog.tlk, modules, override, saves), keyfile handling, resource precedence
    /// - Table config: Defines 2DA table locations (appearance.2da, baseitems.2da, etc.), table loading behavior
    /// - Original implementation: Game profiles configure engine behavior for specific games (KOTOR, KOTOR2, etc.)
    /// - Engine initialization: FUN_00404250 @ 0x00404250 loads game configuration based on profile
    /// - Resource loading: FUN_00633270 @ 0x00633270 handles resource path resolution based on profile
    /// - Note: This is an abstraction layer for multiple BioWare engines (Odyssey, Aurora, Eclipse)
    /// </remarks>
    public abstract class BaseEngineProfile : IEngineProfile
    {
        protected readonly IResourceConfig _resourceConfig;
        protected readonly ITableConfig _tableConfig;
        protected readonly HashSet<string> _supportedFeatures;

        protected BaseEngineProfile()
        {
            _resourceConfig = CreateResourceConfig();
            _tableConfig = CreateTableConfig();
            _supportedFeatures = new HashSet<string>();
            InitializeSupportedFeatures();
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


