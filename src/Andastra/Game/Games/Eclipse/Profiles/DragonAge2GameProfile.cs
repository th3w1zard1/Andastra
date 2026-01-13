using System.Collections.Generic;
using Andastra.Runtime.Engines.Common;
using Andastra.Runtime.Engines.Eclipse.EngineApi;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Game.Engines.Eclipse.Profiles
{
    /// <summary>
    /// Game profile for Dragon Age 2 (DA2).
    /// </summary>
    /// <remarks>
    /// Dragon Age 2 Game Profile:
    /// - Based on DragonAge2.exe game profile system
    /// - Located via string references: Game version checking, resource path resolution
    /// - Game identification: "DragonAge2.exe" @ executable name, "Dragon Age II" @ registry/INI
    /// - Resource files: Eclipse Engine uses RIM files (similar to DA:O but with DA2-specific packages)
    /// - Directory paths: "packages\\core\\" @ 0x00bf5d10, "packages\\core\\override\\" for override resources
    /// - "packages\\" @ 0x00bf5d10, "DragonAge::Streaming" for streaming resources
    /// - Original implementation: Defines Dragon Age 2 specific configuration (resource paths, 2DA tables, script functions)
    /// - Script functions: DA2 uses UnrealScript with Eclipse Engine API (~500 engine functions, same as DA:O)
    /// - Resource paths: Uses DA2-specific package structure (packages/core with RIM files)
    /// - Feature support: Tactical combat, party system, crafting, dialogue system, journal system
    /// - DA2-specific features: Improved combat system, companion system, resource gathering, crafting improvements
    /// - Based on DragonAge2.exe game version detection and resource loading
    /// - DragonAge2.exe: Resource Manager initialization sets up all game directories
    /// </remarks>
    public class DragonAge2GameProfile : BaseEngineProfile
    {
        public override string GameType
        {
            get { return "DA2"; }
        }

        public override string Name
        {
            get { return "Dragon Age II"; }
        }

        public override EngineFamily EngineFamily
        {
            get { return EngineFamily.Eclipse; }
        }

        public override IEngineApi CreateEngineApi()
        {
            return new DragonAge2EngineApi();
        }

        protected override IResourceConfig CreateResourceConfig()
        {
            return new DragonAge2ResourceConfig();
        }

        protected override ITableConfig CreateTableConfig()
        {
            return new DragonAge2TableConfig();
        }

        protected override void InitializeSupportedFeatures()
        {
            // Core features supported by Dragon Age 2
            _supportedFeatures.Add("DialogSystem");
            _supportedFeatures.Add("JournalSystem");
            _supportedFeatures.Add("PartySystem");
            _supportedFeatures.Add("InventorySystem");
            _supportedFeatures.Add("CombatSystem");
            _supportedFeatures.Add("LevelingSystem");
            _supportedFeatures.Add("CraftingSystem");
            _supportedFeatures.Add("TacticalCamera");
            _supportedFeatures.Add("PartyAI");
            _supportedFeatures.Add("CompanionSystem");
            _supportedFeatures.Add("ResourceGathering");
            _supportedFeatures.Add("SaveSystem");
            _supportedFeatures.Add("ModuleSystem");
        }

        private class DragonAge2ResourceConfig : EclipseResourceConfigBase
        {
            private static readonly List<string> _texturePackFiles = new List<string>();

            public override IReadOnlyList<string> TexturePackFiles
            {
                get { return _texturePackFiles; }
            }
        }

        private class DragonAge2TableConfig : ITableConfig
        {
            private static readonly List<string> _requiredTables = new List<string>
            {
                "appearance",
                "baseitems",
                "classes",
                "feats",
                "skills",
                "spells",
                "portraits",
                "placeables",
                "soundset",
                "visualeffects",
                "racialtypes",
                "creaturespeed",
                "upgrade",
                "itemcreate"
            };

            private static readonly Dictionary<string, string> _appearanceColumns = new Dictionary<string, string>
            {
                { "label", "label" },
                { "race", "race" },
                { "racetex", "racetex" },
                { "modeltype", "modeltype" },
                { "modela", "modela" },
                { "texturea", "texturea" },
                { "modelb", "modelb" },
                { "textureb", "textureb" },
                { "normalhead", "normalhead" },
                { "backuphead", "backuphead" },
                { "portrait", "portrait" }
            };

            private static readonly Dictionary<string, string> _baseItemsColumns = new Dictionary<string, string>
            {
                { "label", "label" },
                { "name", "name" },
                { "equipableslots", "equipableslots" },
                { "itemclass", "itemclass" },
                { "stacking", "stacking" },
                { "modeltype", "modeltype" },
                { "itemtype", "itemtype" },
                { "baseac", "baseac" },
                { "dieroll", "dieroll" },
                { "numdie", "numdie" }
            };

            public IReadOnlyList<string> RequiredTables
            {
                get { return _requiredTables; }
            }

            public IReadOnlyDictionary<string, string> AppearanceColumns
            {
                get { return _appearanceColumns; }
            }

            public IReadOnlyDictionary<string, string> BaseItemsColumns
            {
                get { return _baseItemsColumns; }
            }
        }
    }
}

