using System.Collections.Generic;
using Odyssey.Content.Interfaces;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Engines.Common
{
    /// <summary>
    /// Abstract base class for game profiles across all engines.
    /// </summary>
    /// <remarks>
    /// Base Engine Profile:
    /// - Based on swkotor2.exe game profile system
    /// - Located via string references: Game profile determines game-specific behavior (K1 vs K2)
    /// - Game profiles: Define resource configs, table configs, supported features, engine API creation
    /// - Resource config: Defines resource file locations, keyfile handling, resource precedence
    /// - Table config: Defines 2DA table locations, table loading behavior
    /// - Original implementation: Game profiles configure engine behavior for specific games (KOTOR, KOTOR2, etc.)
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


