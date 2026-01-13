using System;
using System.Collections.Generic;
using BioWare.NET.Common;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Odyssey.EngineApi;
using Andastra.Game.Scripting.Interfaces;
using Andastra.Game.Games.Common;

namespace Andastra.Game.Games.Odyssey.Profiles
{
    /// <summary>
    /// Game profile for KOTOR 2: The Sith Lords (TSL).
    /// Defines TSL-specific resource paths, table configurations, and feature support.
    /// </summary>
    /// <remarks>
    /// KOTOR 2 Game Profile:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) game profile system
    /// - Located via string references: Game version checking, resource path resolution
    /// - Game identification: "swkotor2" @ 0x007b575c, "swKotor2.ini" @ 0x007b5740, ".\swkotor2.ini" @ 0x007b5644
    /// - "KotOR2" @ 0x0080c210, "KotorCin" @ 0x007b5630
    /// - Resource files: "chitin.key" @ 0x007c6bcc (keyfile), "dialog.tlk" @ 0x007c6bd0 (dialogue file)
    /// - Directory paths: ".\modules" @ 0x007c6bcc, ".\override" @ 0x007c6bd4, ".\saves" @ 0x007c6b0c
    /// - "MODULES:" @ 0x007b58b4, ":MODULES" @ 0x007be258, "MODULES" @ 0x007c6bc4 (module directory paths)
    /// - "d:\modules" @ 0x007c6bd8, ":modules" @ 0x007cc0d8 (module directory variants)
    /// - "LIVE%d:MODULES\" @ (K1: 0x007458c4, TSL: 0x007be680) (live module directory format)
    /// - Original implementation: Defines KOTOR 2 specific configuration (resource paths, 2DA tables, NWScript functions)
    /// - NWScript functions: K2 has ~950 engine functions (function IDs 0-949)
    /// - Resource paths: Uses K2-specific texture pack files (swpc_tex_gui.erf, swpc_tex_tpa.erf)
    /// - Feature support: Influence system, Prestige Classes, Combat Forms, Item Crafting supported in K2 (not in K1)
    /// - K2-specific features: Workbench, Lab Station, Item Breakdown (crafting system)
    /// - K2 does not support Pazaak Den (replaced with Pazaak cards), supports Pazaak minigame
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) game version detection and resource loading
    /// - 0x00633270 @ 0x00633270 sets up all game directories including MODULES, OVERRIDE, SAVES
    /// </remarks>
    public class OdysseyTheSithLordsGameProfile : BaseEngineProfile
    {
        public override string GameType
        {
            get { return "K2"; }
        }

        public override string Name
        {
            get { return "Star Wars: Knights of the Old Republic II - The Sith Lords"; }
        }

        public override Andastra.Game.Games.Common.EngineFamily EngineFamily
        {
            get { return Andastra.Game.Games.Common.EngineFamily.Odyssey; }
        }

        public override IEngineApi CreateEngineApi()
        {
            return new EngineApi.OdysseyEngineApi(BioWareGame.K2);
        }

        protected override Andastra.Game.Games.Common.IResourceConfig CreateResourceConfig()
        {
            return new TheSithLordsResourceConfig();
        }

        protected override Andastra.Game.Games.Common.ITableConfig CreateTableConfig()
        {
            return new TheSithLordsTableConfig();
        }

        protected override void InitializeSupportedFeatures()
        {
            // Core features supported by both games
            _supportedFeatures.Add("DialogSystem");
            _supportedFeatures.Add("JournalSystem");
            _supportedFeatures.Add("PartySystem");
            _supportedFeatures.Add("InventorySystem");
            _supportedFeatures.Add("CombatSystem");
            _supportedFeatures.Add("LevelingSystem");
            _supportedFeatures.Add("ForceSystem");
            _supportedFeatures.Add("CraftingSystem");
            _supportedFeatures.Add("MiniGames");

            // TSL-specific features
            _supportedFeatures.Add("InfluenceSystem");
            _supportedFeatures.Add("PrestigeClasses");
            _supportedFeatures.Add("CombatForms");
            _supportedFeatures.Add("Workbench");
            _supportedFeatures.Add("LabStation");
            _supportedFeatures.Add("ItemBreakdown");
        }

        private class TheSithLordsResourceConfig : OdysseyResourceConfigBase
        {
            public override IReadOnlyList<string> TexturePackFiles
            {
                get
                {
                    return new[]
                    {
                        "TexturePacks/swpc_tex_gui.erf",
                        "TexturePacks/swpc_tex_tpa.erf"
                    };
                }
            }
        }

        private class TheSithLordsTableConfig : Andastra.Game.Games.Common.ITableConfig
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
                "phenotypes",
                "creaturespeed",
                "influence",
                "combatfeat",
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
