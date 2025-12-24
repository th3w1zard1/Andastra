using System.Collections.Generic;
using Andastra.Runtime.Engines.Common;
using Andastra.Runtime.Engines.Odyssey.EngineApi;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Runtime.Engines.Odyssey.Profiles
{
    /// <summary>
    /// Game profile for Star Wars: Knights of the Old Republic (K1).
    /// </summary>
    /// <remarks>
    /// KOTOR 1 Game Profile:
    /// - Based on swkotor.exe game profile system
    /// - Located via string references: Game version checking, resource path resolution
    /// - Resource files: "chitin.key" @ 0x007c6bcc (keyfile), "dialog.tlk" @ 0x007c6bd0 (dialogue file)
    /// - Directory paths: ".\modules" @ 0x007c6bcc, ".\override" @ 0x007c6bd4, ".\saves" @ 0x007c6b0c
    /// - "MODULES:" @ 0x007b58b4, ":MODULES" @ 0x007be258, "MODULES" @ 0x007c6bc4 (module directory paths)
    /// - "d:\modules" @ 0x007c6bd8, ":modules" @ 0x007cc0d8 (module directory variants)
    /// - Original implementation: Defines KOTOR 1 specific configuration (resource paths, 2DA tables, NWScript functions)
    /// - NWScript functions: K1 has ~850 engine functions (function IDs 0-849)
    /// - Resource paths: Uses K1-specific texture pack files (swpc_tex_tpa.erf, swpc_tex_tpb.erf, swpc_tex_tpc.erf)
    /// - Feature support: Pazaak, Swoop Racing, Turret minigames supported in K1 (not in K2)
    /// - Feature differences: K1 does not support Influence system, Prestige Classes, Combat Forms, Item Crafting
    /// - Based on swkotor.exe game version detection and resource loading
    /// - FUN_00633270 @ 0x00633270 sets up all game directories including MODULES, OVERRIDE, SAVES
    /// </remarks>
    public class OdysseyKotor1GameProfile : BaseEngineProfile
    {
        public override string GameType
        {
            get { return "K1"; }
        }

        public override string Name
        {
            get { return "Star Wars: Knights of the Old Republic"; }
        }

        public override Andastra.Runtime.Engines.Common.EngineFamily EngineFamily
        {
            get { return Andastra.Runtime.Engines.Common.EngineFamily.Odyssey; }
        }

        public override IEngineApi CreateEngineApi()
        {
            return new EngineApi.Kotor1();
        }

        protected override Andastra.Runtime.Engines.Common.IResourceConfig CreateResourceConfig()
        {
            return new Kotor1ResourceConfig();
        }

        protected override Andastra.Runtime.Engines.Common.ITableConfig CreateTableConfig()
        {
            return new Kotor1TableConfig();
        }

        protected override void InitializeSupportedFeatures()
        {
            _supportedFeatures.Add("Pazaak");
            _supportedFeatures.Add("SwoopRacing");
            _supportedFeatures.Add("Turret");
            _supportedFeatures.Add("AlignmentDialogue");
            _supportedFeatures.Add("PartyDeathHandling");
        }

        private class Kotor1ResourceConfig : OdysseyResourceConfigBase
        {
            private static readonly List<string> _texturePackFiles = new List<string>
            {
                "swpc_tex_tpa.erf",
                "swpc_tex_tpb.erf",
                "swpc_tex_tpc.erf"
            };

            public override IReadOnlyList<string> TexturePackFiles
            {
                get { return _texturePackFiles; }
            }
        }

        private class Kotor1TableConfig : Andastra.Runtime.Engines.Common.ITableConfig
        {
            private static readonly List<string> _requiredTables = new List<string>
            {
                "appearance",
                "baseitems",
                "classes",
                "feat",
                "skills",
                "spells",
                "surfacemat",
                "placeables",
                "genericdoors",
                "heads",
                "portraits"
            };

            private static readonly Dictionary<string, string> _appearanceColumns = new Dictionary<string, string>
            {
                { "label", "label" },
                { "model_type", "model_type" },
                { "racetex", "racetex" },
                { "walkdist", "walkdist" },
                { "rundist", "rundist" }
            };

            private static readonly Dictionary<string, string> _baseItemsColumns = new Dictionary<string, string>
            {
                { "label", "label" },
                { "equipableslots", "equipableslots" },
                { "defaulticon", "defaulticon" },
                { "defaultmodel", "defaultmodel" }
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

