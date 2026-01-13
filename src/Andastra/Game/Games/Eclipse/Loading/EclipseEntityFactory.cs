using System;
using System.Collections.Generic;
using System.Numerics;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Extract;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Eclipse;
using Andastra.Game.Games.Eclipse.Components;
using JetBrains.Annotations;
using Gender = BioWare.NET.Common.Gender;
using Language = BioWare.NET.Common.Language;
using ObjectType = Andastra.Runtime.Core.Enums.ObjectType;
using ScriptEvent = Andastra.Runtime.Core.Enums.ScriptEvent;

namespace Andastra.Game.Games.Eclipse.Loading
{
    /// <summary>
    /// Factory for creating runtime entities from GFF templates in Eclipse engine.
    /// </summary>
    /// <remarks>
    /// Eclipse Entity Factory System:
    /// - Based on daorigins.exe and DragonAge2.exe entity creation system
    /// - Located via string references: "TemplateResRef" @ 0x00af4f00 (daorigins.exe), "TemplateResRef" @ 0x00bf2538 (DragonAge2.exe)
    /// - Template loading: Eclipse uses UTC GFF templates (same format as Odyssey/Aurora)
    /// - Original implementation: Creates runtime entities from UTC GFF templates
    /// - Entities created from templates with position/facing specified
    /// - Template data applied to entity components (stats, scripts, appearance, etc.)
    /// - ObjectId assignment: Sequential uint32 starting from 1000000 (high range to avoid conflicts with World.CreateEntity counter)
    /// - Based on daorigins.exe/DragonAge2.exe: ObjectIds are assigned sequentially, starting from 1
    /// - OBJECT_INVALID = 0x7F000000, OBJECT_SELF = 0x7F000001
    ///
    /// GFF Template Types (GFF signatures):
    /// - UTC → Creature (Appearance_Type, Faction, HP, Attributes, Scripts)
    /// - UTP → Placeable (Appearance, Useable, Locked, OnUsed)
    /// - UTD → Door (GenericType, Locked, OnOpen, OnClose, LinkedToModule, LinkedTo)
    /// - UTT → Trigger (Geometry polygon, OnEnter, OnExit, LinkedToModule)
    /// - UTW → Waypoint (Tag, position, MapNote, MapNoteEnabled)
    /// - UTS → Sound (Active, Looping, Positional, ResRef)
    /// - UTE → Encounter (Creature list, spawn conditions, Geometry, SpawnPointList)
    /// - UTI → Item (BaseItem, Properties, Charges)
    /// - UTM → Store (merchant inventory)
    ///
    /// Eclipse-specific differences from Odyssey:
    /// - Uses EclipseEntity instead of Entity
    /// - Uses EclipseStatsComponent (Health/Stamina instead of HP/FP)
    /// - Uses Eclipse-specific components (EclipseFactionComponent, EclipseInventoryComponent, etc.)
    /// - Template format is identical (UTC GFF), but component initialization differs
    /// </remarks>
    public class EclipseEntityFactory
    {
        // Based on daorigins.exe/DragonAge2.exe: ObjectId assignment system
        // Located via string references: "ObjectId" @ 0x00af4e74 (daorigins.exe), "ObjectId" @ 0x00bf1a3c (DragonAge2.exe)
        // Original implementation: ObjectIds are assigned sequentially, starting from 1
        // ObjectIds should be unique across all entities. Use high range (1000000+) to avoid conflicts with World.CreateEntity counter
        private uint _nextObjectId = 1000000;

        // TLK (talk table) files for LocalizedString resolution
        // Based on daorigins.exe/DragonAge2.exe: TLK lookup system for localized strings
        // Eclipse engine uses TLK format compatible with Odyssey/Aurora engines
        // Base TLK: dialog.tlk (main talk table)
        // Custom TLK: Custom entries start at 0x01000000 (high bit set)
        // Located via string references: TLK lookup in entity template loading
        // Original implementation: Eclipse engine resolves LocalizedString.StringRef via TLK files
        private TLK _baseTlk;
        private TLK _customTlk;

        /// <summary>
        /// Gets the next available object ID.
        /// Uses high range to avoid conflicts with Entity's static counter (used by World.CreateEntity).
        /// </summary>
        private uint GetNextObjectId()
        {
            return _nextObjectId++;
        }

        /// <summary>
        /// Sets the talk tables for LocalizedString resolution.
        /// </summary>
        /// <param name="baseTlk">The base TLK (dialog.tlk) for string lookups.</param>
        /// <param name="customTlk">The custom TLK for custom string entries (optional).</param>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: TLK lookup system
        /// - Eclipse engine uses TLK format compatible with Odyssey/Aurora engines
        /// - Base TLK: dialog.tlk contains main string table
        /// - Custom TLK: Custom entries start at 0x01000000 (high bit set)
        /// - Located via string references: TLK lookup in entity template loading
        /// - Original implementation: Eclipse engine resolves LocalizedString.StringRef via TLK files
        /// - Similar to Odyssey DialogueManager.SetTalkTables and EclipseJRLLoader.SetTalkTables
        /// </remarks>
        public void SetTalkTables(object baseTlk, object customTlk = null)
        {
            _baseTlk = baseTlk as TLK;
            _customTlk = customTlk as TLK;
        }

        /// <summary>
        /// Looks up a string by string reference in the talk tables.
        /// </summary>
        /// <param name="stringRef">The string reference to look up.</param>
        /// <returns>The string text from TLK, or empty string if not found.</returns>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: TLK string lookup system
        /// - Eclipse engine uses TLK format compatible with Odyssey/Aurora engines
        /// - Custom TLK entries start at 0x01000000 (high bit set)
        /// - Base TLK: dialog.tlk contains main string table (0x00000000 - 0x00FFFFFF)
        /// - Custom TLK: Custom entries (0x01000000+)
        /// - Located via string references: TLK lookup in entity template loading
        /// - Original implementation: Eclipse engine resolves LocalizedString.StringRef via TLK files
        /// - Similar to Odyssey DialogueManager.LookupString implementation
        /// </remarks>
        private string LookupString(int stringRef)
        {
            if (stringRef < 0)
            {
                return string.Empty;
            }

            // Custom TLK entries start at 0x01000000 (high bit set)
            // Based on Odyssey engine: Custom TLK entries use high bit to distinguish from base TLK
            const int CUSTOM_TLK_START = 0x01000000;

            if (stringRef >= CUSTOM_TLK_START)
            {
                // Look up in custom TLK
                if (_customTlk != null)
                {
                    int customRef = stringRef - CUSTOM_TLK_START;
                    string text = _customTlk.String(customRef);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
            }
            else
            {
                // Look up in base TLK
                if (_baseTlk != null)
                {
                    string text = _baseTlk.String(stringRef);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Creates a creature from a template ResRef at a specific position.
        /// </summary>
        /// <param name="module">The module to load templates from.</param>
        /// <param name="templateResRef">The template resource reference (e.g., "n_darthmalak").</param>
        /// <param name="position">The spawn position.</param>
        /// <param name="facing">The facing direction in radians.</param>
        /// <returns>The created entity, or null if template not found or creation failed.</returns>
        /// <remarks>
        /// Eclipse engine family implementation:
        /// - Validates template ResRef parameter (null/empty check)
        /// - Loads UTC GFF template from module
        /// - Creates EclipseEntity with ObjectId, ObjectType.Creature, position, facing
        /// - Applies template data to entity components (stats, scripts, appearance, etc.)
        /// - Returns null on failure (template not found, invalid data, creation error)
        /// - Based on daorigins.exe and DragonAge2.exe: Template loading system
        /// - Located via string references: "TemplateResRef" @ 0x00af4f00 (daorigins.exe), "TemplateResRef" @ 0x00bf2538 (DragonAge2.exe)
        /// - Original implementation: Loads UTC GFF, reads creature properties, creates entity with components
        /// </remarks>
        [CanBeNull]
        public IEntity CreateCreatureFromTemplate(Module module, string templateResRef, Vector3 position, float facing)
        {
            if (module == null || string.IsNullOrEmpty(templateResRef))
            {
                return null;
            }

            var entity = new EclipseEntity(GetNextObjectId(), ObjectType.Creature, null);

            // Set position and facing
            var transformComponent = entity.GetComponent<ITransformComponent>();
            if (transformComponent != null)
            {
                transformComponent.Position = position;
                transformComponent.Facing = facing;
            }

            LoadCreatureTemplate(entity, module, templateResRef);

            return entity;
        }

        /// <summary>
        /// Loads creature template from UTC GFF and applies to entity.
        /// </summary>
        /// <param name="entity">The entity to apply template data to.</param>
        /// <param name="module">The module to load templates from.</param>
        /// <param name="templateResRef">The template resource reference.</param>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: Template loading system
        /// - Loads UTC GFF structure, reads fields in specific order:
        ///   - FirstName, LastName, Description (localized strings)
        ///   - IsPC, Tag, Conversation (ResRef)
        ///   - Appearance_Type, BodyVariation, TextureVar, Portrait, SoundSetFile
        ///   - CurrentHitPoints, MaxHitPoints (maps to CurrentHealth/MaxHealth in EclipseStatsComponent)
        ///   - Str, Dex, Con, Int, Wis, Cha (ability scores)
        ///   - NaturalAC, FortitudeSave, ReflexSave, WillSave
        ///   - FactionID
        ///   - Script hooks (ScriptHeartbeat, ScriptOnNotice, ScriptAttacked, ScriptDamaged, ScriptDeath, etc.)
        /// - Applies template data to EclipseEntity components:
        ///   - EclipseStatsComponent: HP (maps to Health), Attributes, Saves
        ///   - EclipseFactionComponent: FactionID
        ///   - EclipseScriptHooksComponent: Script event hooks
        ///   - BaseRenderableComponent: Appearance_Type
        /// - Located via string references: "TemplateResRef" @ 0x00af4f00 (daorigins.exe), "TemplateResRef" @ 0x00bf2538 (DragonAge2.exe)
        /// - Original implementation: Loads UTC GFF, reads creature properties, creates entity with components
        /// </remarks>
        private void LoadCreatureTemplate(EclipseEntity entity, Module module, string templateResRef)
        {
            // Load UTC resource from module
            ModuleResource utcResource = module.Creature(templateResRef);
            if (utcResource == null)
            {
                return;
            }

            object utcData = utcResource.Resource();
            if (utcData == null)
            {
                return;
            }

            GFF utcGff = utcData as GFF;
            if (utcGff == null)
            {
                return;
            }

            GFFStruct root = utcGff.Root;

            // Tag (if not set from GIT)
            if (string.IsNullOrEmpty(entity.Tag))
            {
                entity.Tag = GetStringField(root, "Tag");
            }

            // Store template data for components
            entity.SetData("TemplateResRef", templateResRef);
            entity.SetData("FirstName", GetLocStringField(root, "FirstName"));
            entity.SetData("LastName", GetLocStringField(root, "LastName"));
            entity.SetData("RaceId", GetIntField(root, "Race", 0)); // Race field in UTC
            entity.SetData("Appearance_Type", GetIntField(root, "Appearance_Type", 0));
            entity.SetData("FactionID", GetIntField(root, "FactionID", 0));

            // HP (maps to Health in EclipseStatsComponent)
            int currentHP = GetIntField(root, "CurrentHitPoints", 1);
            int maxHP = GetIntField(root, "MaxHitPoints", 1);
            entity.SetData("CurrentHitPoints", currentHP);
            entity.SetData("MaxHitPoints", maxHP);

            // Attributes
            entity.SetData("Str", GetIntField(root, "Str", 10));
            entity.SetData("Dex", GetIntField(root, "Dex", 10));
            entity.SetData("Con", GetIntField(root, "Con", 10));
            entity.SetData("Int", GetIntField(root, "Int", 10));
            entity.SetData("Wis", GetIntField(root, "Wis", 10));
            entity.SetData("Cha", GetIntField(root, "Cha", 10));

            // Saves
            entity.SetData("FortitudeSave", GetIntField(root, "fortbonus", 0));
            entity.SetData("ReflexSave", GetIntField(root, "refbonus", 0));
            entity.SetData("WillSave", GetIntField(root, "willbonus", 0));
            entity.SetData("NaturalAC", GetIntField(root, "NaturalAC", 0));

            // Apply template data to components
            ApplyTemplateToComponents(entity);

            // Scripts
            SetEntityScripts(entity, root, new Dictionary<string, ScriptEvent>
            {
                { "ScriptAttacked", ScriptEvent.OnPhysicalAttacked },
                { "ScriptDamaged", ScriptEvent.OnDamaged },
                { "ScriptDeath", ScriptEvent.OnDeath },
                { "ScriptDialogue", ScriptEvent.OnConversation },
                { "ScriptDisturbed", ScriptEvent.OnDisturbed },
                { "ScriptEndRound", ScriptEvent.OnEndCombatRound },
                { "ScriptHeartbeat", ScriptEvent.OnHeartbeat },
                { "ScriptOnBlocked", ScriptEvent.OnBlocked },
                { "ScriptOnNotice", ScriptEvent.OnPerception },
                { "ScriptRested", ScriptEvent.OnRested },
                { "ScriptSpawn", ScriptEvent.OnSpawn },
                { "ScriptSpellAt", ScriptEvent.OnSpellCastAt },
                { "ScriptUserDefine", ScriptEvent.OnUserDefined }
            });

            // Load ScriptDialogue as Conversation property (dialogue ResRef for BeginConversation)
            // Based on daorigins.exe/DragonAge2.exe: ScriptDialogue field from UTC template
            // ScriptDialogue ResRef is the dialogue file (DLG) used for conversations with this creature
            string scriptDialogue = GetResRefField(root, "ScriptDialogue");
            if (!string.IsNullOrEmpty(scriptDialogue))
            {
                entity.SetData("Conversation", scriptDialogue);
                entity.SetData("ScriptDialogue", scriptDialogue);
            }
        }

        /// <summary>
        /// Applies template data stored in entity to components.
        /// </summary>
        /// <param name="entity">The entity with template data to apply.</param>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: Component initialization from templates
        /// - Applies template data to EclipseStatsComponent (HP, Attributes, Saves)
        /// - Applies template data to EclipseFactionComponent (FactionID)
        /// - Applies template data to BaseRenderableComponent (Appearance_Type)
        /// - Located via string references: Component initialization from template data
        /// - Original implementation: Components read template data from entity data storage
        /// </remarks>
        private void ApplyTemplateToComponents(EclipseEntity entity)
        {
            // Apply stats to EclipseStatsComponent
            IStatsComponent statsComponent = entity.GetComponent<IStatsComponent>();
            if (statsComponent != null)
            {
                // HP (maps to Health in EclipseStatsComponent)
                if (entity.GetData("CurrentHitPoints") is int currentHP)
                {
                    statsComponent.CurrentHP = currentHP;
                }
                if (entity.GetData("MaxHitPoints") is int maxHP)
                {
                    statsComponent.MaxHP = maxHP;
                }

                // Attributes
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

                // Saves - use SetBaseSaves method if available
                var eclipseStats = statsComponent as Components.EclipseStatsComponent;
                if (eclipseStats != null)
                {
                    int fortSave = entity.GetData<int>("FortitudeSave", 0);
                    int refSave = entity.GetData<int>("ReflexSave", 0);
                    int willSave = entity.GetData<int>("WillSave", 0);
                    if (fortSave != 0 || refSave != 0 || willSave != 0)
                    {
                        eclipseStats.SetBaseSaves(fortSave, refSave, willSave);
                    }
                    if (entity.GetData("NaturalAC") is int naturalAC)
                    {
                        eclipseStats.NaturalArmor = naturalAC;
                    }
                }
            }

            // Apply faction to EclipseFactionComponent
            IFactionComponent factionComponent = entity.GetComponent<IFactionComponent>();
            if (factionComponent != null)
            {
                if (entity.GetData("FactionID") is int factionId)
                {
                    factionComponent.FactionId = factionId;
                }
            }

            // Apply appearance to BaseRenderableComponent
            IRenderableComponent renderableComponent = entity.GetComponent<IRenderableComponent>();
            if (renderableComponent != null)
            {
                if (entity.GetData("Appearance_Type") is int appearanceType)
                {
                    renderableComponent.AppearanceRow = appearanceType;
                }
            }
        }

        /// <summary>
        /// Sets entity script hooks from GFF template.
        /// </summary>
        /// <param name="entity">The entity to set scripts on.</param>
        /// <param name="root">The GFF root struct.</param>
        /// <param name="scriptMappings">Mapping of GFF field names to ScriptEvent enum values.</param>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: Script hooks system
        /// - Script hooks stored in entity templates and can be set/modified at runtime
        /// - Event system: "EventListeners" @ 0x00ae8194 (daorigins.exe), 0x00bf543c (DragonAge2.exe)
        /// - Event scripts: "EventScripts" @ 0x00ae81bc (daorigins.exe), 0x00bf5464 (DragonAge2.exe)
        /// - Maps script events to script resource references (ResRef strings)
        /// - Scripts are executed by UnrealScript VM when events fire (OnHeartbeat, OnPerception, OnAttacked, etc.)
        /// - Located via string references: Script hooks from template data
        /// - Original implementation: Sets script hooks in EclipseScriptHooksComponent from template data
        /// </remarks>
        private void SetEntityScripts(EclipseEntity entity, GFFStruct root, Dictionary<string, ScriptEvent> scriptMappings)
        {
            IScriptHooksComponent scriptHooksComponent = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooksComponent == null)
            {
                return;
            }

            foreach (KeyValuePair<string, ScriptEvent> mapping in scriptMappings)
            {
                string scriptResRef = GetResRefField(root, mapping.Key);
                if (!string.IsNullOrEmpty(scriptResRef))
                {
                    scriptHooksComponent.SetScript(mapping.Value, scriptResRef);
                }
            }
        }

        #region GFF Field Helpers

        /// <summary>
        /// Gets a string field from a GFF struct.
        /// </summary>
        private string GetStringField(GFFStruct gffStruct, string name)
        {
            if (gffStruct.Exists(name))
            {
                return gffStruct.GetString(name) ?? string.Empty;
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets an integer field from a GFF struct.
        /// </summary>
        private int GetIntField(GFFStruct gffStruct, string name, int defaultValue)
        {
            if (gffStruct.Exists(name))
            {
                return gffStruct.GetInt32(name);
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets a ResRef field from a GFF struct.
        /// </summary>
        private string GetResRefField(GFFStruct gffStruct, string name)
        {
            if (gffStruct.Exists(name))
            {
                ResRef resRef = gffStruct.GetResRef(name);
                if (resRef != null && !resRef.IsBlank())
                {
                    return resRef.ToString();
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets a localized string field from a GFF struct.
        /// </summary>
        private string GetLocStringField(GFFStruct gffStruct, string name)
        {
            if (!gffStruct.Exists(name))
            {
                return string.Empty;
            }

            // Get LocalizedString from GFF field
            LocalizedString locString;
            if (!gffStruct.TryGetLocString(name, out locString))
            {
                return string.Empty;
            }

            // If StringRef is valid (>= 0), look up in TLK (if available)
            // Based on daorigins.exe/DragonAge2.exe: LocalizedString resolution
            // Eclipse engine uses TLK format compatible with Odyssey/Aurora engines
            // Located via string references: TLK lookup in entity template loading
            // Original implementation: Eclipse engine resolves LocalizedString.StringRef via TLK files
            if (locString.StringRef >= 0)
            {
                // Look up string in TLK tables (base or custom)
                string tlkText = LookupString(locString.StringRef);
                if (!string.IsNullOrEmpty(tlkText))
                {
                    return tlkText;
                }
            }

            // Use embedded substrings from GFF (default to English/Male)
            string result = locString.Get(Language.English, Gender.Male, useFallback: true);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }

            // Fallback: return first available substring
            foreach ((Language _, Gender _, string text) in locString)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        #endregion
    }
}

