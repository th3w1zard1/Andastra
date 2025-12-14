using System.Collections.Generic;
using Odyssey.Content.Interfaces;
using Odyssey.Scripting.EngineApi;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Kotor.Profiles
{
    /// <summary>
    /// Game profile for Star Wars: Knights of the Old Republic (K1).
    /// </summary>
    public class K1GameProfile : IGameProfile
    {
        private readonly K1ResourceConfig _resourceConfig;
        private readonly K1TableConfig _tableConfig;
        
        public K1GameProfile()
        {
            _resourceConfig = new K1ResourceConfig();
            _tableConfig = new K1TableConfig();
        }
        
        public GameType GameType { get { return GameType.K1; } }
        
        public string Name { get { return "Star Wars: Knights of the Old Republic"; } }
        
        public EngineFamily EngineFamily { get { return EngineFamily.Odyssey; } }
        
        public IEngineApi CreateEngineApi()
        {
            return new K1EngineApi();
        }
        
        public IResourceConfig ResourceConfig { get { return _resourceConfig; } }
        
        public ITableConfig TableConfig { get { return _tableConfig; } }
        
        public bool SupportsFeature(GameFeature feature)
        {
            switch (feature)
            {
                case GameFeature.Pazaak:
                case GameFeature.SwoopRacing:
                case GameFeature.Turret:
                case GameFeature.AlignmentDialogue:
                case GameFeature.PartyDeathHandling:
                    return true;
                    
                case GameFeature.Influence:
                case GameFeature.ItemCrafting:
                case GameFeature.CombatForms:
                case GameFeature.PrestigeClasses:
                case GameFeature.RemoteControl:
                    return false;
                    
                default:
                    return false;
            }
        }
    }
    
    internal class K1ResourceConfig : IResourceConfig
    {
        private static readonly List<string> _texturePackFiles = new List<string>
        {
            "swpc_tex_tpa.erf",
            "swpc_tex_tpb.erf",
            "swpc_tex_tpc.erf"
        };
        
        public string ChitinKeyFile { get { return "chitin.key"; } }
        
        public IReadOnlyList<string> TexturePackFiles { get { return _texturePackFiles; } }
        
        public string DialogTlkFile { get { return "dialog.tlk"; } }
        
        public string ModulesDirectory { get { return "modules"; } }
        
        public string OverrideDirectory { get { return "override"; } }
        
        public string SavesDirectory { get { return "saves"; } }
    }
    
    internal class K1TableConfig : ITableConfig
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
        
        public IReadOnlyList<string> RequiredTables { get { return _requiredTables; } }
        
        public IReadOnlyDictionary<string, string> AppearanceColumns { get { return _appearanceColumns; } }
        
        public IReadOnlyDictionary<string, string> BaseItemsColumns { get { return _baseItemsColumns; } }
    }
}
