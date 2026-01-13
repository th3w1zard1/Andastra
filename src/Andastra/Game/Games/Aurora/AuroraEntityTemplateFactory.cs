using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Aurora.Components;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Common.Components;
using JetBrains.Annotations;
using ResRef = BioWare.NET.Common.ResRef;
using ScriptEvent = Andastra.Runtime.Core.Enums.ScriptEvent;

namespace Andastra.Game.Games.Aurora
{
    /// <summary>
    /// Aurora engine family implementation of entity template factory.
    /// Creates entities from templates using Aurora-specific loading mechanisms.
    /// </summary>
    /// <remarks>
    /// Entity Template Factory (Aurora Engine Family):
    /// - Common template factory implementation for Aurora engine games (Neverwinter Nights, Neverwinter Nights 2)
    /// - Based on nwmain.exe entity creation system
    /// - Located via string references: "TemplateResRef" @ 0x140dddee8 (nwmain.exe)
    /// - Template loading: CNWSCreature::LoadCreature @ 0x1403975e0 (nwmain.exe) reads TemplateResRef from GFF
    /// - CNWSArea::LoadCreatures @ 0x140360570 (nwmain.exe) loads creatures from GIT and applies templates
    /// - Original implementation: Loads templates from GFF files, creates entities with Aurora-specific components
    /// - Module is required for template resource loading (template files from module archives or HAK files)
    /// - Both games use similar template loading mechanism
    /// 
    /// Reverse Engineered from Ghidra MCP Analysis (nwmain.exe):
    /// - LoadCreature @ 0x1403975e0: Reads TemplateResRef field from GFF structure using CResGFF::ReadFieldCResRef
    /// - TemplateResRef string @ 0x140dddee8: Referenced by LoadCreature, SaveCreature, LoadTrigger, SaveDoor, SavePlaceable, SaveItem, SaveTrigger
    /// - LoadCreatures @ 0x140360570: Loads creature list from GIT, creates CNWSCreature instances, calls LoadCreature for each
    /// - Template loading pattern: Read TemplateResRef → Load UTC GFF file → Apply template data to creature entity
    /// - Error handling: "Creature template %s doesn't exist.\n" @ 0x140dddad0 (error message string)
    /// </remarks>
    [PublicAPI]
    public class AuroraEntityTemplateFactory : BaseEntityTemplateFactory
    {
        private readonly IGameResourceProvider _resourceProvider;
        private uint _nextObjectId = 1000000;

        /// <summary>
        /// Creates a new AuroraEntityTemplateFactory.
        /// </summary>
        /// <param name="resourceProvider">The resource provider for loading template files.</param>
        /// <exception cref="ArgumentNullException">Thrown if resourceProvider is null.</exception>
        public AuroraEntityTemplateFactory(IGameResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
        }

        /// <summary>
        /// Gets the next available object ID.
        /// Uses high range to avoid conflicts with Entity's static counter (used by World.CreateEntity).
        /// </summary>
        private uint GetNextObjectId()
        {
            return _nextObjectId++;
        }

        /// <summary>
        /// Creates a creature entity from a template ResRef at the specified position.
        /// </summary>
        /// <param name="templateResRef">The template resource reference.</param>
        /// <param name="position">The spawn position.</param>
        /// <param name="facing">The facing direction in radians.</param>
        /// <returns>The created entity, or null if template not found or creation failed.</returns>
        /// <remarks>
        /// Aurora engine family implementation:
        /// - Validates template ResRef using base class validation
        /// - Loads UTC template from AuroraResourceProvider (searches override → module → HAK → base game)
        /// - Based on nwmain.exe: CNWSCreature::LoadCreature @ 0x1403975e0 reads TemplateResRef from GFF
        /// - Located via string references: "TemplateResRef" @ 0x140dddee8 (nwmain.exe)
        /// - Original implementation: Loads template GFF, reads creature properties, creates entity with Aurora components
        /// - Template loading follows nwmain.exe pattern: Load UTC GFF → Parse structure → Apply to entity
        /// - Error handling: Returns null if template not found (matches "Creature template %s doesn't exist.\n" behavior)
        /// </remarks>
        public override IEntity CreateCreatureFromTemplate(string templateResRef, Vector3 position, float facing)
        {
            if (!IsValidTemplateResRef(templateResRef))
            {
                return null;
            }

            // Load UTC template from resource provider
            // Based on nwmain.exe: Template loading searches override → module → HAK → base game
            // AuroraResourceProvider.LookupResource follows precedence: Override → Module → HAK → Base Game → Hardcoded
            var resourceId = new ResourceIdentifier(templateResRef, ResourceType.UTC);
            byte[] templateData = _resourceProvider.GetResourceBytesAsync(resourceId, default).GetAwaiter().GetResult();

            if (templateData == null || templateData.Length == 0)
            {
                // Template not found - matches nwmain.exe error handling: "Creature template %s doesn't exist.\n" @ 0x140dddad0
                return null;
            }

            // Parse UTC GFF structure
            // Based on nwmain.exe: UTC files are GFF format with "UTC " signature
            GFF utcGff = GFF.FromBytes(templateData);
            if (utcGff == null || utcGff.Root == null)
            {
                return null;
            }

            GFFStruct root = utcGff.Root;

            // Create AuroraEntity with ObjectId and ObjectType
            // Based on nwmain.exe: CNWSCreature constructor creates creature with ObjectId
            // ObjectId assignment: Sequential uint32 starting from 1000000 (high range to avoid conflicts)
            uint objectId = GetNextObjectId();
            var entity = new AuroraEntity(objectId, ObjectType.Creature);

            // Set position and facing
            // Based on nwmain.exe: Position stored as XPosition, YPosition, ZPosition in GIT
            // Orientation stored as XOrientation, YOrientation, ZOrientation in GIT
            // Transform component is attached automatically by AuroraEntity.Initialize()
            var transformComponent = entity.GetComponent<ITransformComponent>();
            if (transformComponent != null)
            {
                transformComponent.Position = position;
                transformComponent.Facing = facing;
            }

            // Load Tag (if not set from GIT)
            // Based on nwmain.exe: Tag is CExoString in original, stored as string here
            if (string.IsNullOrEmpty(entity.Tag))
            {
                entity.Tag = GetStringField(root, "Tag");
            }

            // Store template data for components
            entity.SetData("TemplateResRef", templateResRef);
            entity.SetData("FirstName", GetLocStringField(root, "FirstName"));
            entity.SetData("LastName", GetLocStringField(root, "LastName"));
            entity.SetData("RaceId", GetIntField(root, "Race", 0));
            entity.SetData("Appearance_Type", GetIntField(root, "Appearance_Type", 0));
            entity.SetData("FactionID", GetIntField(root, "FactionID", 0));
            entity.SetData("CurrentHitPoints", GetIntField(root, "CurrentHitPoints", 1));
            entity.SetData("MaxHitPoints", GetIntField(root, "MaxHitPoints", 1));

            // Load class list from UTC Classes field
            // Based on nwmain.exe: ClassList is GFFList containing class entries
            // Each entry has Class (class ID) and ClassLevel (level in that class)
            if (root.Exists("ClassList"))
            {
                GFFList classList = root.GetList("ClassList");
                if (classList != null)
                {
                    var classes = new List<Components.AuroraCreatureClass>();
                    for (int i = 0; i < classList.Count; i++)
                    {
                        GFFStruct classStruct = classList[i];
                        if (classStruct != null)
                        {
                            int classId = GetIntField(classStruct, "Class", 0);
                            int classLevel = GetIntField(classStruct, "ClassLevel", 1);
                            classes.Add(new Components.AuroraCreatureClass { ClassId = classId, Level = classLevel });
                        }
                    }
                    entity.SetData("ClassList", classes);
                }
            }

            // Attributes
            // Based on nwmain.exe: Ability scores stored in UTC root structure
            entity.SetData("Str", GetIntField(root, "Str", 10));
            entity.SetData("Dex", GetIntField(root, "Dex", 10));
            entity.SetData("Con", GetIntField(root, "Con", 10));
            entity.SetData("Int", GetIntField(root, "Int", 10));
            entity.SetData("Wis", GetIntField(root, "Wis", 10));
            entity.SetData("Cha", GetIntField(root, "Cha", 10));

            // Apply template data to StatsComponent
            // Based on nwmain.exe: CNWSCreatureStats::LoadStats loads stats from GFF
            var statsComponent = entity.GetComponent<IStatsComponent>();
            if (statsComponent != null)
            {
                // Set ability scores
                if (entity.GetData("Str") is int str)
                {
                    statsComponent.SetAbility(Ability.Strength, str);
                }
                if (entity.GetData("Dex") is int dex)
                {
                    statsComponent.SetAbility(Ability.Dexterity, dex);
                }
                if (entity.GetData("Con") is int con)
                {
                    statsComponent.SetAbility(Ability.Constitution, con);
                }
                if (entity.GetData("Int") is int intel)
                {
                    statsComponent.SetAbility(Ability.Intelligence, intel);
                }
                if (entity.GetData("Wis") is int wis)
                {
                    statsComponent.SetAbility(Ability.Wisdom, wis);
                }
                if (entity.GetData("Cha") is int cha)
                {
                    statsComponent.SetAbility(Ability.Charisma, cha);
                }

                // Set HP
                if (entity.GetData("CurrentHitPoints") is int currentHP)
                {
                    statsComponent.CurrentHP = currentHP;
                }
                if (entity.GetData("MaxHitPoints") is int maxHP)
                {
                    statsComponent.MaxHP = maxHP;
                }

                // Set save bonuses
                // Based on nwmain.exe: Save bonuses stored in UTC as fortbonus, refbonus, willbonus
                int fortBonus = GetIntField(root, "fortbonus", 0);
                int refBonus = GetIntField(root, "refbonus", 0);
                int willBonus = GetIntField(root, "willbonus", 0);
                entity.SetData("FortitudeBonus", fortBonus);
                entity.SetData("ReflexBonus", refBonus);
                entity.SetData("WillBonus", willBonus);
            }

            // Apply template data to CreatureComponent
            // Based on nwmain.exe: CNWSCreatureStats contains creature-specific data
            var creatureComponent = entity.GetComponent<AuroraCreatureComponent>();
            if (creatureComponent != null)
            {
                creatureComponent.TemplateResRef = templateResRef;
                creatureComponent.Tag = entity.Tag;

                // Set appearance
                if (entity.GetData("Appearance_Type") is int appearanceType)
                {
                    creatureComponent.AppearanceType = appearanceType;
                }
                if (entity.GetData("RaceId") is int raceId)
                {
                    creatureComponent.RaceId = raceId;
                }

                // Load class list
                if (entity.GetData("ClassList") is List<Components.AuroraCreatureClass> classes)
                {
                    creatureComponent.ClassList = classes;
                }

                // Load feats from FeatList
                // Based on nwmain.exe: FeatList is GFFList containing feat IDs
                if (root.Exists("FeatList"))
                {
                    GFFList featList = root.GetList("FeatList");
                    if (featList != null)
                    {
                        var feats = new List<int>();
                        for (int i = 0; i < featList.Count; i++)
                        {
                            GFFStruct featStruct = featList[i];
                            if (featStruct != null)
                            {
                                int featId = GetIntField(featStruct, "Feat", 0);
                                if (featId != 0)
                                {
                                    feats.Add(featId);
                                }
                            }
                        }
                        creatureComponent.FeatList = feats;
                    }
                }

                // Load Conversation field (dialogue ResRef for BeginConversation)
                // Based on nwmain.exe: Conversation field is ResRef to DLG file
                string conversation = GetResRefField(root, "Conversation");
                if (!string.IsNullOrEmpty(conversation))
                {
                    creatureComponent.Conversation = conversation;
                    entity.SetData("Conversation", conversation);
                }
            }

            // Apply template data to FactionComponent
            // Based on nwmain.exe: FactionID loaded from creature template GFF
            var factionComponent = entity.GetComponent<IFactionComponent>();
            if (factionComponent != null && entity.GetData("FactionID") is int factionId)
            {
                factionComponent.FactionId = factionId;
            }

            // Load script hooks
            // Based on nwmain.exe: Script hooks stored as ResRef fields in UTC
            // Aurora uses: ScriptHeartbeat, ScriptOnNotice, ScriptSpellAt, ScriptAttacked, ScriptDamaged, etc.
            SetEntityScripts(entity, root, new Dictionary<string, ScriptEvent>
            {
                { "ScriptAttacked", ScriptEvent.OnAttacked },
                { "ScriptDamaged", ScriptEvent.OnDamaged },
                { "ScriptDeath", ScriptEvent.OnDeath },
                { "ScriptDialogue", ScriptEvent.OnDialogue },
                { "ScriptDisturbed", ScriptEvent.OnDisturbed },
                { "ScriptEndRound", ScriptEvent.OnEndRound },
                { "ScriptHeartbeat", ScriptEvent.OnHeartbeat },
                { "ScriptOnBlocked", ScriptEvent.OnBlocked },
                { "ScriptOnNotice", ScriptEvent.OnNotice },
                { "ScriptRested", ScriptEvent.OnRested },
                { "ScriptSpawn", ScriptEvent.OnSpawn },
                { "ScriptSpellAt", ScriptEvent.OnSpellAt },
                { "ScriptUserDefine", ScriptEvent.OnUserDefined }
            });

            // Load additional UTC fields
            // Based on nwmain.exe: Additional creature properties from UTC template
            entity.SetData("Gender", GetIntField(root, "Gender", 0));
            entity.SetData("PortraitId", GetIntField(root, "PortraitId", 0));
            entity.SetData("SubraceIndex", GetIntField(root, "SubraceIndex", 0));
            entity.SetData("BodyVariation", GetIntField(root, "BodyVariation", 0));
            entity.SetData("TextureVar", GetIntField(root, "TextureVar", 0));
            entity.SetData("SoundSetFile", GetIntField(root, "SoundSetFile", 0));
            entity.SetData("NaturalAC", GetIntField(root, "NaturalAC", 0));
            entity.SetData("ChallengeRating", GetIntField(root, "ChallengeRating", 0));
            entity.SetData("IsPC", GetIntField(root, "IsPC", 0) != 0);
            entity.SetData("Plot", GetIntField(root, "Plot", 0) != 0);
            entity.SetData("Interruptable", GetIntField(root, "Interruptable", 0) != 0);
            entity.SetData("Disarmable", GetIntField(root, "Disarmable", 0) != 0);
            entity.SetData("Min1HP", GetIntField(root, "Min1HP", 0) != 0);
            entity.SetData("NotReorienting", GetIntField(root, "NotReorienting", 0) != 0);
            entity.SetData("PartyInteract", GetIntField(root, "PartyInteract", 0) != 0);
            entity.SetData("NoPermDeath", GetIntField(root, "NoPermDeath", 0) != 0);
            entity.SetData("WalkRate", GetIntField(root, "WalkRate", 0));
            entity.SetData("GoodEvil", GetIntField(root, "GoodEvil", 0));
            entity.SetData("PerceptionRange", GetIntField(root, "PerceptionRange", 0));

            return entity;
        }

        #region Helper Methods

        private static string GetResRefField(GFFStruct root, string fieldName)
        {
            if (root.Exists(fieldName))
            {
                ResRef resRef = root.GetResRef(fieldName);
                if (resRef != null)
                {
                    return resRef.ToString();
                }
            }
            return string.Empty;
        }

        private static string GetStringField(GFFStruct root, string fieldName)
        {
            if (root.Exists(fieldName))
            {
                return root.GetString(fieldName) ?? string.Empty;
            }
            return string.Empty;
        }

        private static string GetLocStringField(GFFStruct root, string fieldName)
        {
            if (root.Exists(fieldName))
            {
                LocalizedString locStr = root.GetLocString(fieldName);
                if (locStr != null)
                {
                    return locStr.ToString();
                }
            }
            return string.Empty;
        }

        private static int GetIntField(GFFStruct root, string fieldName, int defaultValue = 0)
        {
            if (root.Exists(fieldName))
            {
                return root.GetInt32(fieldName);
            }
            return defaultValue;
        }

        private static void SetEntityScripts(AuroraEntity entity, GFFStruct root, Dictionary<string, ScriptEvent> mappings)
        {
            var scriptHooksComponent = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooksComponent == null)
            {
                return;
            }

            foreach (KeyValuePair<string, ScriptEvent> mapping in mappings)
            {
                if (root.Exists(mapping.Key))
                {
                    ResRef scriptRef = root.GetResRef(mapping.Key);
                    if (scriptRef != null && !string.IsNullOrEmpty(scriptRef.ToString()))
                    {
                        scriptHooksComponent.SetScript(mapping.Value, scriptRef.ToString());
                    }
                }
            }
        }

        #endregion
    }
}

