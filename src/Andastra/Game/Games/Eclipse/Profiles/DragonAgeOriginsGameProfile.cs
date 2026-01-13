using System.Collections.Generic;
using Andastra.Runtime.Engines.Common;
using Andastra.Runtime.Engines.Eclipse.EngineApi;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Game.Engines.Eclipse.Profiles
{
    /// <summary>
    /// Game profile for Dragon Age: Origins (DA:O).
    /// </summary>
    /// <remarks>
    /// Dragon Age: Origins Game Profile:
    /// - Based on daorigins.exe game profile system
    /// - Located via string references: Game version checking, resource path resolution
    /// - Game identification: "daorigins.exe" @ executable name, "Dragon Age: Origins" @ registry/INI
    /// - Resource files: Eclipse Engine uses RIM files (global.rim, globalvfx.rim, guiglobal.rim, designerscripts.rim)
    /// - Directory paths: "packages\\core\\" @ 0x00ad9798, "packages\\core\\override\\" for override resources
    /// - "packages\\" @ 0x00ad9810, "DragonAge::Streaming" @ 0x00ad7a34 for streaming resources
    /// - Original implementation: Defines Dragon Age: Origins specific configuration (resource paths, 2DA tables, script functions)
    /// - Script functions: DA:O uses UnrealScript with Eclipse Engine API (~500 engine functions)
    /// - Resource paths: Uses DA:O-specific package structure (packages/core with RIM files)
    /// - Feature support: Tactical combat, party system, crafting, dialogue system, journal system
    /// - DA:O-specific features: Origin stories, tactical camera, party AI, crafting system
    /// - Based on daorigins.exe game version detection and resource loading
    /// - daorigins.exe: "Initialize - Resource Manager" @ 0x00ad947c sets up all game directories
    /// </remarks>
    public class DragonAgeOriginsGameProfile : BaseEngineProfile
    {
        public override string GameType
        {
            get { return "DAO"; }
        }

        public override string Name
        {
            get { return "Dragon Age: Origins"; }
        }

        public override EngineFamily EngineFamily
        {
            get { return EngineFamily.Eclipse; }
        }

        public override IEngineApi CreateEngineApi()
        {
            return new DragonAgeOriginsEngineApi();
        }

        protected override IResourceConfig CreateResourceConfig()
        {
            return new DragonAgeOriginsResourceConfig();
        }

        protected override ITableConfig CreateTableConfig()
        {
            return new DragonAgeOriginsTableConfig();
        }

        protected override void InitializeSupportedFeatures()
        {
            // Core features supported by Dragon Age: Origins
            _supportedFeatures.Add("DialogSystem");
            _supportedFeatures.Add("JournalSystem");
            _supportedFeatures.Add("PartySystem");
            _supportedFeatures.Add("InventorySystem");
            _supportedFeatures.Add("CombatSystem");
            _supportedFeatures.Add("LevelingSystem");
            _supportedFeatures.Add("CraftingSystem");
            _supportedFeatures.Add("TacticalCamera");
            _supportedFeatures.Add("PartyAI");
            _supportedFeatures.Add("OriginStories");
            _supportedFeatures.Add("SaveSystem");
            _supportedFeatures.Add("ModuleSystem");
        }

        private class DragonAgeOriginsResourceConfig : EclipseResourceConfigBase
        {
            private static readonly List<string> _texturePackFiles = new List<string>();

            public override IReadOnlyList<string> TexturePackFiles
            {
                get { return _texturePackFiles; }
            }
        }

        private class DragonAgeOriginsTableConfig : ITableConfig
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

