using System;
using System.Collections.Generic;
using CSharpKOTOR.Common;
using Odyssey.Kotor.Profiles;
using Odyssey.Scripting.EngineApi;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Kotor.Profiles
{
    /// <summary>
    /// Game profile for KOTOR 2: The Sith Lords (TSL).
    /// Defines TSL-specific resource paths, table configurations, and feature support.
    /// </summary>
    public class K2GameProfile : IGameProfile
    {
        private readonly K2ResourceConfig _resourceConfig;
        private readonly K2TableConfig _tableConfig;
        
        public K2GameProfile()
        {
            _resourceConfig = new K2ResourceConfig();
            _tableConfig = new K2TableConfig();
        }
        
        public GameType GameType
        {
            get { return GameType.TSL; }
        }
        
        public string Name
        {
            get { return "Star Wars: Knights of the Old Republic II - The Sith Lords"; }
        }
        
        public EngineFamily EngineFamily
        {
            get { return EngineFamily.Odyssey; }
        }
        
        public IEngineApi CreateEngineApi()
        {
            return new K2EngineApi();
        }
        
        public IResourceConfig ResourceConfig
        {
            get { return _resourceConfig; }
        }
        
        public ITableConfig TableConfig
        {
            get { return _tableConfig; }
        }
        
        public bool SupportsFeature(GameFeature feature)
        {
            switch (feature)
            {
                // Core features supported by both games
                case GameFeature.DialogSystem:
                case GameFeature.JournalSystem:
                case GameFeature.PartySystem:
                case GameFeature.InventorySystem:
                case GameFeature.CombatSystem:
                case GameFeature.LevelingSystem:
                case GameFeature.ForceSystem:
                case GameFeature.CraftingSystem:
                case GameFeature.MiniGames:
                    return true;
                    
                // TSL-specific features
                case GameFeature.InfluenceSystem:
                case GameFeature.PrestigeClasses:
                case GameFeature.CombatForms:
                case GameFeature.Workbench:
                case GameFeature.LabStation:
                case GameFeature.ItemBreakdown:
                    return true;
                    
                // K1-only features
                case GameFeature.PazaakDen:
                    return false;
                    
                default:
                    return false;
            }
        }
        
        #region K2ResourceConfig
        
        private class K2ResourceConfig : IResourceConfig
        {
            public string ChitinKeyPath
            {
                get { return "chitin.key"; }
            }
            
            public IReadOnlyList<string> TexturePackPaths
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
            
            public string DialogTlkPath
            {
                get { return "dialog.tlk"; }
            }
            
            public string ModulesDirectory
            {
                get { return "modules"; }
            }
            
            public string OverrideDirectory
            {
                get { return "override"; }
            }
            
            public string SavesDirectory
            {
                get { return "saves"; }
            }
            
            public IReadOnlyList<string> LipSyncPaths
            {
                get { return new[] { "lips" }; }
            }
            
            public IReadOnlyList<string> StreamMusicPaths
            {
                get { return new[] { "streammusic" }; }
            }
            
            public IReadOnlyList<string> StreamSoundPaths
            {
                get { return new[] { "streamsounds" }; }
            }
            
            public IReadOnlyList<string> StreamVoicePaths
            {
                get { return new[] { "streamvoice", "streamwaves/vo" }; }
            }
        }
        
        #endregion
        
        #region K2TableConfig
        
        private class K2TableConfig : ITableConfig
        {
            private readonly Dictionary<string, string[]> _tables;
            
            public K2TableConfig()
            {
                _tables = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    // Core appearance/template tables
                    { "appearance", new[] { "label", "race", "racetex", "modeltype", "modela", "texturea", "modelb", "textureb", "normalhead", "backuphead", "portrait", "perspace", "creperspace", "height", "hitdist", "prefatckdist", "targetheight", "abortonparry", "ratefiremod", "envmap", "bloodcolr", "weaponscale", "wing_tail_scale", "footsteptype" } },
                    { "baseitems", new[] { "label", "name", "equipableslots", "itemclass", "stacking", "modeltype", "itemtype", "baseac", "dieroll", "numdie", "critth", "crithitmult", "rangeincrement", "weaponsize", "cost", "costmod", "maxattacks", "preferredatk", "damageflags" } },
                    { "classes", new[] { "label", "name", "description", "icon", "hitdie", "attackbonustable", "savingthrowbonus", "skillstable", "featstable", "spellgaintable", "spelllist", "skillpointbase", "spellcaster", "arcanespellfail", "forcedie", "maxforcepowerlevel" } },
                    { "feats", new[] { "label", "name", "description", "icon", "minattackbonus", "minstr", "mindex", "mincon", "minint", "minwis", "mincha", "minspelllvl", "prereqfeat1", "prereqfeat2", "allclassescanuse", "category" } },
                    { "skills", new[] { "label", "name", "description", "icon", "keyability", "armorcheckpenalty", "trainedonly", "untrained" } },
                    { "spells", new[] { "label", "name", "spelldesc", "iconresref", "school", "range", "vs", "impactscript", "conjontime", "itemcastsound", "usertype", "forcehostile" } },
                    
                    // Combat and gameplay tables
                    { "weapontypes", new[] { "label", "damageflags", "preferredatk", "dieroll", "numdie" } },
                    { "damageflags", new[] { "label", "damagecap" } },
                    { "ranges", new[] { "label", "primary", "secondary" } },
                    
                    // TSL-specific tables
                    { "influence", new[] { "label", "npcindex", "basevalue", "minvalue", "maxvalue" } },
                    { "combatfeat", new[] { "label", "prereq1", "prereq2", "forcepoints", "icon", "name", "description" } },
                    { "prestigeclass", new[] { "label", "name", "description", "alignment", "prereq1", "prereq2", "prereq3" } },
                    { "upgrade", new[] { "label", "slot", "type", "cost", "item" } },
                    { "itemcreate", new[] { "label", "template", "cost", "category" } },
                    { "iprp_abilities", new[] { "label", "name", "costvalue", "costtable" } },
                    
                    // Dialog and journal tables
                    { "dialog", new[] { "label", "listenerid", "speakerid", "text", "vo_resref", "script_listener", "script_speaker", "emotion", "facialanimation" } },
                    { "journal", new[] { "label", "tag", "name", "priority", "comment" } },
                    { "portraits", new[] { "label", "baseresref", "sex", "race" } },
                    
                    // Area and module tables
                    { "modulesave", new[] { "label", "area", "moduleid", "name" } },
                    { "loadhints", new[] { "label", "hint", "gameversion" } },
                    { "placeables", new[] { "label", "modelname", "lightcolor", "soundapptype" } },
                    { "soundset", new[] { "label", "resref" } },
                    
                    // Visual effect tables
                    { "visualeffects", new[] { "label", "progfx_impact", "type_fd", "model", "soundimpact", "progfx_duration", "progfx_emitter" } },
                    { "videofx", new[] { "label", "resref", "stretch" } },
                    
                    // Creature AI tables
                    { "creaturespeed", new[] { "label", "walkrate", "runrate" } },
                    { "racialtypes", new[] { "label", "name", "description", "playable", "appearance", "str", "dex", "con", "int", "wis", "cha" } },
                    { "phenotypes", new[] { "label", "name" } },
                    
                    // Party member tables (TSL specific)
                    { "partymapt", new[] { "label", "name", "portrait", "baseresref", "localstring", "voiceid" } },
                    { "partytable", new[] { "label", "npc", "resref", "canselect" } }
                };
            }
            
            public IReadOnlyList<string> RequiredTables
            {
                get
                {
                    return new[]
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
                        "influence",         // TSL-specific
                        "combatfeat",        // TSL-specific
                        "upgrade",           // TSL-specific
                        "itemcreate"         // TSL-specific
                    };
                }
            }
            
            public IReadOnlyList<string> GetRelevantColumns(string tableName)
            {
                if (_tables.TryGetValue(tableName, out string[] columns))
                {
                    return columns;
                }
                return new string[0];
            }
        }
        
        #endregion
    }
}
