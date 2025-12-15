using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;
using Odyssey.Content.Interfaces;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Content.Loaders
{
    /// <summary>
    /// Loads entity templates from GFF files (UTC, UTP, UTD, UTT, UTW, UTS, UTE, UTM).
    /// </summary>
    /// <remarks>
    /// Template Loader:
    /// - Based on swkotor2.exe template loading system
    /// - Template formats: GFF files with signatures "UTC ", "UTP ", "UTD ", "UTT ", "UTW ", "UTS ", "UTE ", "UTM "
    /// - Located via string references: "TemplateResRef" @ 0x007bd00c (template reference field in GIT instances)
    /// - "TemplateResRef: " @ 0x007caedc (template debug message), "template" @ 0x007d0470, "Template" @ 0x007d05cc
    /// - "UseTemplates" @ 0x007bd930 (template usage flag)
    /// - Template directory: "HD0:DATAXBOX\templates" @ 0x007c7590 (Xbox template directory)
    /// - Template loading error messages:
    ///   - "Creature template '%s' doesn't exist.\n" @ 0x007bf78c
    ///   - "Encounter template %s doesn't exist.\n" @ 0x007c0df0
    ///   - "Waypoint template %s doesn't exist.\n" @ 0x007c0f24
    ///   - "Store template %s doesn't exist.\n" @ 0x007c1228
    ///   - "Item template %s doesn't exist.\n" @ 0x007c2028
    /// - Original implementation: FUN_005261b0 @ 0x005261b0 loads creature templates from UTC GFF files
    ///   - Loads UTC GFF with "UTC " signature, falls back to "NW_BADGER" if template not found
    ///   - Calls FUN_005fb0f0 to load creature data from template
    ///   - Calls FUN_0050c510 to load creature scripts (ScriptHeartbeat, ScriptOnNotice, ScriptSpellAt, etc.)
    ///   - Loads position/orientation: XPosition, YPosition, ZPosition, XOrientation, YOrientation, ZOrientation
    ///   - Loads JoiningXP, PM_IsDisguised, PM_Appearance, StealthMode flags
    ///   - Sets creature position and orientation, initializes creature state
    /// - FUN_0050c510 @ 0x0050c510 loads creature script hooks from UTC template
    ///   - Loads script ResRefs: ScriptHeartbeat, ScriptOnNotice, ScriptSpellAt, ScriptAttacked, ScriptDamaged,
    ///     ScriptDisturbed, ScriptEndRound, ScriptDialogue, ScriptSpawn, ScriptRested, ScriptDeath, ScriptUserDefine,
    ///     ScriptOnBlocked, ScriptEndDialogue
    ///   - Stores scripts in creature object at specific offsets (0x270, 0x278, 0x280, etc.)
    /// - FUN_00580ed0 @ 0x00580ed0 loads door properties from UTD template
    ///   - Loads door appearance, generic type, open state, bearing, faction, saves (Fort, Will, Ref), HP
    ///   - Loads flags: Invulnerable, Plot, Static, NotBlastable, Min1HP, AutoRemoveKey
    ///   - Loads lock properties: KeyName, KeyRequired, OpenLockDC, CloseLockDC, SecretDoorDC, OpenLockDiff, OpenLockDiffMod
    ///   - Loads trap properties: TrapType, TrapDisarmable, TrapDetectable, DisarmDC, TrapDetectDC, TrapFlag, TrapOneShot
    ///   - Loads script hooks: OnClosed, OnDamaged, OnDeath, OnDisarm, OnHeartbeat, OnLock, OnMeleeAttacked, OnOpen,
    ///     OnSpellCastAt, OnTrapTriggered, OnUnlock, OnUserDefined, OnClick, OnFailToOpen, OnDialog
    ///   - Loads transition data: LinkedTo, LinkedToFlags, LinkedToModule, TransitionDestination, LoadScreenID
    ///   - Loads model data: Appearance (for generic doors), ModelName/Model (for specific doors), VisibleModel
    ///   - Loads portrait: PortraitId or Portrait string
    ///   - Loads localization: LocName, Description
    ///   - Loads conversation: Conversation ResRef
    ///   - Creates door walkmesh instances for each walkmesh type
    /// - FUN_004e08e0 @ 0x004e08e0 loads placeable/door templates from UTP/UTD GFF files
    /// - FUN_005838d0 @ 0x005838d0 loads door template and transition data
    /// - Loads GFF template files, parses entity data, creates template objects
    /// - Templates define base properties for entities (stats, appearance, scripts, etc.)
    /// - Template GFF fields: Tag, TemplateResRef, Appearance_Type, ScriptHeartbeat, ScriptOnNotice, etc.
    /// - UTC = Creature template (creature stats, appearance, classes, feats, scripts)
    /// - UTP = Placeable template (placeable appearance, scripts, inventory flag, lock state)
    /// - UTD = Door template (door appearance, lock state, transition data, scripts)
    /// - UTT = Trigger template (trigger geometry, trap flags, scripts)
    /// - UTW = Waypoint template (waypoint appearance, map note data)
    /// - UTS = Sound template (sound properties, volume, distance, looping)
    /// - UTE = Encounter template (spawn points, creature lists, difficulty)
    /// - UTM = Store template (merchant markup rates, item lists)
    /// - Script fields in templates: ScriptHeartbeat, ScriptOnNotice, ScriptDamaged, ScriptDeath, etc. (mapped to ScriptEvent enum)
    /// - Based on template file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class TemplateLoader
    {
        private readonly IGameResourceProvider _resourceProvider;

        public TemplateLoader(IGameResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
        }

        /// <summary>
        /// Loads a creature template (UTC).
        /// </summary>
        public async Task<CreatureTemplate> LoadCreatureTemplateAsync(
            string templateResRef,
            CancellationToken ct = default(CancellationToken))
        {
            var id = new CSharpKOTOR.Resources.ResourceIdentifier(templateResRef, CSharpKOTOR.Resources.ResourceType.UTC);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var reader = new GFFBinaryReader(stream);
                GFF gff = reader.Load();
                return ParseCreatureTemplate(gff.Root);
            }
        }

        /// <summary>
        /// Loads a placeable template (UTP).
        /// </summary>
        public async Task<PlaceableTemplate> LoadPlaceableTemplateAsync(
            string templateResRef,
            CancellationToken ct = default(CancellationToken))
        {
            var id = new CSharpKOTOR.Resources.ResourceIdentifier(templateResRef, CSharpKOTOR.Resources.ResourceType.UTP);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var reader = new GFFBinaryReader(stream);
                GFF gff = reader.Load();
                return ParsePlaceableTemplate(gff.Root);
            }
        }

        /// <summary>
        /// Loads a door template (UTD).
        /// </summary>
        public async Task<DoorTemplate> LoadDoorTemplateAsync(
            string templateResRef,
            CancellationToken ct = default(CancellationToken))
        {
            var id = new CSharpKOTOR.Resources.ResourceIdentifier(templateResRef, CSharpKOTOR.Resources.ResourceType.UTD);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var reader = new GFFBinaryReader(stream);
                GFF gff = reader.Load();
                return ParseDoorTemplate(gff.Root);
            }
        }

        /// <summary>
        /// Loads a trigger template (UTT).
        /// </summary>
        public async Task<TriggerTemplate> LoadTriggerTemplateAsync(
            string templateResRef,
            CancellationToken ct = default(CancellationToken))
        {
            var id = new CSharpKOTOR.Resources.ResourceIdentifier(templateResRef, CSharpKOTOR.Resources.ResourceType.UTT);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var reader = new GFFBinaryReader(stream);
                GFF gff = reader.Load();
                return ParseTriggerTemplate(gff.Root);
            }
        }

        /// <summary>
        /// Loads a waypoint template (UTW).
        /// </summary>
        public async Task<WaypointTemplate> LoadWaypointTemplateAsync(
            string templateResRef,
            CancellationToken ct = default(CancellationToken))
        {
            var id = new CSharpKOTOR.Resources.ResourceIdentifier(templateResRef, CSharpKOTOR.Resources.ResourceType.UTW);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var reader = new GFFBinaryReader(stream);
                GFF gff = reader.Load();
                return ParseWaypointTemplate(gff.Root);
            }
        }

        /// <summary>
        /// Loads a sound template (UTS).
        /// </summary>
        public async Task<SoundTemplate> LoadSoundTemplateAsync(
            string templateResRef,
            CancellationToken ct = default(CancellationToken))
        {
            var id = new CSharpKOTOR.Resources.ResourceIdentifier(templateResRef, CSharpKOTOR.Resources.ResourceType.UTS);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var reader = new GFFBinaryReader(stream);
                GFF gff = reader.Load();
                return ParseSoundTemplate(gff.Root);
            }
        }

        /// <summary>
        /// Loads an encounter template (UTE).
        /// </summary>
        public async Task<EncounterTemplate> LoadEncounterTemplateAsync(
            string templateResRef,
            CancellationToken ct = default(CancellationToken))
        {
            var id = new CSharpKOTOR.Resources.ResourceIdentifier(templateResRef, CSharpKOTOR.Resources.ResourceType.UTE);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var reader = new GFFBinaryReader(stream);
                GFF gff = reader.Load();
                return ParseEncounterTemplate(gff.Root);
            }
        }

        /// <summary>
        /// Loads a store/merchant template (UTM).
        /// </summary>
        public async Task<StoreTemplate> LoadStoreTemplateAsync(
            string templateResRef,
            CancellationToken ct = default(CancellationToken))
        {
            var id = new CSharpKOTOR.Resources.ResourceIdentifier(templateResRef, CSharpKOTOR.Resources.ResourceType.UTM);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var reader = new GFFBinaryReader(stream);
                GFF gff = reader.Load();
                return ParseStoreTemplate(gff.Root);
            }
        }

        #region Template Parsing

        private CreatureTemplate ParseCreatureTemplate(GFFStruct root)
        {
            var template = new CreatureTemplate();

            // Basic info
            template.TemplateResRef = GetString(root, "TemplateResRef");
            template.Tag = GetString(root, "Tag");
            template.FirstName = GetLocalizedString(root, "FirstName");
            template.LastName = GetLocalizedString(root, "LastName");

            // Appearance
            template.Appearance = GetInt(root, "Appearance_Type");
            template.BodyVariation = GetByte(root, "BodyVariation");
            template.TextureVar = GetByte(root, "TextureVar");
            template.Portrait = GetString(root, "Portrait");
            template.Soundset = GetInt(root, "SoundSetFile");

            // Stats
            template.CurrentHP = GetShort(root, "CurrentHitPoints");
            template.MaxHP = GetShort(root, "MaxHitPoints");
            template.CurrentFP = GetShort(root, "CurrentForce");
            template.MaxFP = GetShort(root, "MaxForce");

            // Attributes
            template.Strength = GetByte(root, "Str");
            template.Dexterity = GetByte(root, "Dex");
            template.Constitution = GetByte(root, "Con");
            template.Intelligence = GetByte(root, "Int");
            template.Wisdom = GetByte(root, "Wis");
            template.Charisma = GetByte(root, "Cha");

            // Combat
            template.NaturalAC = GetByte(root, "NaturalAC");
            template.FortitudeSave = GetByte(root, "fortbonus");
            template.ReflexSave = GetByte(root, "refbonus");
            template.WillSave = GetByte(root, "willbonus");

            // Flags
            template.IsPC = GetByte(root, "IsPC") != 0;
            template.NoPermDeath = GetByte(root, "NoPermDeath") != 0;
            template.Plot = GetByte(root, "Plot") != 0;
            template.Interruptable = GetByte(root, "Interruptable") != 0;
            template.DisarmableDet = GetByte(root, "DisarmableDet") != 0;

            // Faction
            template.FactionID = GetInt(root, "FactionID");

            // Scripts
            template.OnSpawn = GetString(root, "ScriptSpawn");
            template.OnDeath = GetString(root, "ScriptDeath");
            template.OnHeartbeat = GetString(root, "ScriptHeartbeat");
            template.OnPerception = GetString(root, "ScriptOnNotice");
            template.OnDamaged = GetString(root, "ScriptDamaged");
            template.OnAttacked = GetString(root, "ScriptAttacked");
            template.OnEndRound = GetString(root, "ScriptEndRound");
            template.OnDialogue = GetString(root, "ScriptDialogue");
            template.OnDisturbed = GetString(root, "ScriptDisturbed");
            template.OnBlocked = GetString(root, "ScriptOnBlocked");
            template.OnUserDefined = GetString(root, "ScriptUserDefine");

            // Conversation
            template.Conversation = GetString(root, "Conversation");

            return template;
        }

        private PlaceableTemplate ParsePlaceableTemplate(GFFStruct root)
        {
            var template = new PlaceableTemplate();

            template.TemplateResRef = GetString(root, "TemplateResRef");
            template.Tag = GetString(root, "Tag");
            template.LocalizedName = GetLocalizedString(root, "LocName");
            template.Appearance = GetInt(root, "Appearance");
            template.Description = GetLocalizedString(root, "Description");

            // State
            template.Static = GetByte(root, "Static") != 0;
            template.Useable = GetByte(root, "Useable") != 0;
            template.HasInventory = GetByte(root, "HasInventory") != 0;
            template.Plot = GetByte(root, "Plot") != 0;
            template.Locked = GetByte(root, "Locked") != 0;
            template.LockDC = GetByte(root, "OpenLockDC");
            template.KeyRequired = GetByte(root, "KeyRequired") != 0;
            template.KeyName = GetString(root, "KeyName");
            template.Trapable = GetByte(root, "TrapDetectable") != 0;
            template.AnimationState = GetByte(root, "AnimationState");

            // Scripts
            template.OnUsed = GetString(root, "OnUsed");
            template.OnHeartbeat = GetString(root, "OnHeartbeat");
            template.OnInvDisturbed = GetString(root, "OnInvDisturbed");
            template.OnOpen = GetString(root, "OnOpen");
            template.OnClosed = GetString(root, "OnClosed");
            template.OnLock = GetString(root, "OnLock");
            template.OnUnlock = GetString(root, "OnUnlock");
            template.OnDamaged = GetString(root, "OnDamaged");
            template.OnDeath = GetString(root, "OnDeath");
            template.OnUserDefined = GetString(root, "OnUserDefined");
            template.OnEndDialogue = GetString(root, "OnEndDialogue");
            template.OnTrapTriggered = GetString(root, "OnTrapTriggered");
            template.OnDisarm = GetString(root, "OnDisarm");
            template.OnMeleeAttacked = GetString(root, "OnMeleeAttacked");

            // Conversation
            template.Conversation = GetString(root, "Conversation");

            return template;
        }

        private DoorTemplate ParseDoorTemplate(GFFStruct root)
        {
            var template = new DoorTemplate();

            template.TemplateResRef = GetString(root, "TemplateResRef");
            template.Tag = GetString(root, "Tag");
            template.LocalizedName = GetLocalizedString(root, "LocName");
            template.Description = GetLocalizedString(root, "Description");
            template.GenericType = GetInt(root, "GenericType");

            // State
            template.Static = GetByte(root, "Static") != 0;
            template.Plot = GetByte(root, "Plot") != 0;
            template.Locked = GetByte(root, "Locked") != 0;
            template.LockDC = GetByte(root, "OpenLockDC");
            template.KeyRequired = GetByte(root, "KeyRequired") != 0;
            template.KeyName = GetString(root, "KeyName");
            template.Trapable = GetByte(root, "TrapDetectable") != 0;
            template.CurrentHP = GetShort(root, "CurrentHP");
            template.Hardness = GetByte(root, "Hardness");
            template.AnimationState = GetByte(root, "AnimationState");

            // Transition
            template.LinkedTo = GetString(root, "LinkedTo");
            template.LinkedToFlags = GetByte(root, "LinkedToFlags");
            template.LinkedToModule = GetString(root, "LinkedToModule");

            // Scripts
            template.OnClick = GetString(root, "OnClick");
            template.OnClosed = GetString(root, "OnClosed");
            template.OnDamaged = GetString(root, "OnDamaged");
            template.OnDeath = GetString(root, "OnDeath");
            template.OnFailToOpen = GetString(root, "OnFailToOpen");
            template.OnHeartbeat = GetString(root, "OnHeartbeat");
            template.OnLock = GetString(root, "OnLock");
            template.OnMeleeAttacked = GetString(root, "OnMeleeAttacked");
            template.OnOpen = GetString(root, "OnOpen");
            template.OnUnlock = GetString(root, "OnUnlock");
            template.OnUserDefined = GetString(root, "OnUserDefined");

            // Conversation
            template.Conversation = GetString(root, "Conversation");

            return template;
        }

        private TriggerTemplate ParseTriggerTemplate(GFFStruct root)
        {
            var template = new TriggerTemplate();

            template.TemplateResRef = GetString(root, "TemplateResRef");
            template.Tag = GetString(root, "Tag");
            template.LocalizedName = GetLocalizedString(root, "LocalizedName");
            template.Type = GetInt(root, "Type");
            template.Faction = GetInt(root, "Faction");

            // Flags
            template.Trapable = GetByte(root, "TrapDetectable") != 0;
            template.TrapDisarmable = GetByte(root, "TrapDisarmable") != 0;
            template.TrapOneShot = GetByte(root, "TrapOneShot") != 0;
            template.TrapType = GetByte(root, "TrapType");
            template.DisarmDC = GetByte(root, "DisarmDC");
            template.DetectDC = GetByte(root, "TrapDetectDC");

            // Scripts
            template.OnEnter = GetString(root, "ScriptOnEnter");
            template.OnExit = GetString(root, "ScriptOnExit");
            template.OnHeartbeat = GetString(root, "ScriptHeartbeat");
            template.OnUserDefined = GetString(root, "ScriptUserDefine");
            template.OnTrapTriggered = GetString(root, "OnTrapTriggered");
            template.OnDisarm = GetString(root, "OnDisarm");

            return template;
        }

        private WaypointTemplate ParseWaypointTemplate(GFFStruct root)
        {
            var template = new WaypointTemplate();

            template.TemplateResRef = GetString(root, "TemplateResRef");
            template.Tag = GetString(root, "Tag");
            template.LocalizedName = GetLocalizedString(root, "LocalizedName");
            template.Description = GetLocalizedString(root, "Description");
            template.Appearance = GetByte(root, "Appearance");
            template.MapNote = GetLocalizedString(root, "MapNote");
            template.MapNoteEnabled = GetByte(root, "MapNoteEnabled") != 0;
            template.HasMapNote = GetByte(root, "HasMapNote") != 0;

            return template;
        }

        private SoundTemplate ParseSoundTemplate(GFFStruct root)
        {
            var template = new SoundTemplate();

            template.TemplateResRef = GetString(root, "TemplateResRef");
            template.Tag = GetString(root, "Tag");
            template.LocalizedName = GetLocalizedString(root, "LocName");
            template.Active = GetByte(root, "Active") != 0;
            template.Continuous = GetByte(root, "Continuous") != 0;
            template.Looping = GetByte(root, "Looping") != 0;
            template.Positional = GetByte(root, "Positional") != 0;
            template.Random = GetByte(root, "Random") != 0;

            template.Volume = GetByte(root, "Volume");
            template.VolumeVariation = GetByte(root, "VolumeVrtn");
            template.Interval = GetInt(root, "Interval");
            template.IntervalVariation = GetInt(root, "IntervalVrtn");
            template.MaxDistance = GetFloat(root, "MaxDistance");
            template.MinDistance = GetFloat(root, "MinDistance");
            template.Elevation = GetFloat(root, "Elevation");

            return template;
        }

        private EncounterTemplate ParseEncounterTemplate(GFFStruct root)
        {
            var template = new EncounterTemplate();

            template.TemplateResRef = GetString(root, "TemplateResRef");
            template.Tag = GetString(root, "Tag");
            template.LocalizedName = GetLocalizedString(root, "LocalizedName");
            template.Faction = GetInt(root, "Faction");
            template.Active = GetByte(root, "Active") != 0;
            template.DifficultyIndex = GetInt(root, "DifficultyIndex");
            template.SpawnOption = GetInt(root, "SpawnOption");
            template.MaxCreatures = GetInt(root, "MaxCreatures");
            template.RecCreatures = GetInt(root, "RecCreatures");
            template.ResetTime = GetInt(root, "ResetTime");

            // Scripts
            template.OnEntered = GetString(root, "OnEntered");
            template.OnExhausted = GetString(root, "OnExhausted");
            template.OnExit = GetString(root, "OnExit");
            template.OnHeartbeat = GetString(root, "OnHeartbeat");
            template.OnUserDefined = GetString(root, "OnUserDefined");

            return template;
        }

        private StoreTemplate ParseStoreTemplate(GFFStruct root)
        {
            var template = new StoreTemplate();

            template.TemplateResRef = GetString(root, "ResRef");
            template.Tag = GetString(root, "Tag");
            template.ID = GetInt(root, "ID");
            template.MarkUp = GetInt(root, "MarkUp");
            template.MarkUpRate = GetInt(root, "MarkUpRate");
            template.MarkDown = GetInt(root, "MarkDown");
            template.MarkDownRate = GetInt(root, "MarkDownRate");

            // Scripts
            template.OnOpenStore = GetString(root, "OnOpenStore");
            template.OnStoreClosed = GetString(root, "OnStoreClosed");

            return template;
        }

        #endregion

        #region GFF Helpers

        private string GetString(GFFStruct gffStruct, string name)
        {
            if (gffStruct.Exists(name))
            {
                return gffStruct.GetString(name) ?? string.Empty;
            }
            return string.Empty;
        }

        private int GetInt(GFFStruct gffStruct, string name)
        {
            if (gffStruct.Exists(name))
            {
                return gffStruct.GetInt32(name);
            }
            return 0;
        }

        private short GetShort(GFFStruct gffStruct, string name)
        {
            if (gffStruct.Exists(name))
            {
                return gffStruct.GetInt16(name);
            }
            return 0;
        }

        private byte GetByte(GFFStruct gffStruct, string name)
        {
            if (gffStruct.Exists(name))
            {
                return gffStruct.GetUInt8(name);
            }
            return 0;
        }

        private float GetFloat(GFFStruct gffStruct, string name)
        {
            if (gffStruct.Exists(name))
            {
                return gffStruct.GetSingle(name);
            }
            return 0f;
        }

        private string GetLocalizedString(GFFStruct gffStruct, string name)
        {
            if (gffStruct.Exists(name))
            {
                // GFF LocalizedString handling - try to get the string reference first
                // For now, return empty as localization requires TLK lookup
                return string.Empty;
            }
            return string.Empty;
        }

        #endregion
    }

    #region Template Classes

    /// <summary>
    /// Base template class for all GFF templates.
    /// </summary>
    public abstract class BaseTemplate
    {
        public string TemplateResRef { get; set; }
        public string Tag { get; set; }
    }

    /// <summary>
    /// Creature template (UTC).
    /// </summary>
    public class CreatureTemplate : BaseTemplate
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }

        // Appearance
        public int Appearance { get; set; }
        public byte BodyVariation { get; set; }
        public byte TextureVar { get; set; }
        public string Portrait { get; set; }
        public int Soundset { get; set; }

        // Stats
        public short CurrentHP { get; set; }
        public short MaxHP { get; set; }
        public short CurrentFP { get; set; }
        public short MaxFP { get; set; }

        // Attributes
        public byte Strength { get; set; }
        public byte Dexterity { get; set; }
        public byte Constitution { get; set; }
        public byte Intelligence { get; set; }
        public byte Wisdom { get; set; }
        public byte Charisma { get; set; }

        // Combat
        public byte NaturalAC { get; set; }
        public byte FortitudeSave { get; set; }
        public byte ReflexSave { get; set; }
        public byte WillSave { get; set; }

        // Flags
        public bool IsPC { get; set; }
        public bool NoPermDeath { get; set; }
        public bool Plot { get; set; }
        public bool Interruptable { get; set; }
        public bool DisarmableDet { get; set; }

        // Faction
        public int FactionID { get; set; }

        // Scripts
        public string OnSpawn { get; set; }
        public string OnDeath { get; set; }
        public string OnHeartbeat { get; set; }
        public string OnPerception { get; set; }
        public string OnDamaged { get; set; }
        public string OnAttacked { get; set; }
        public string OnEndRound { get; set; }
        public string OnDialogue { get; set; }
        public string OnDisturbed { get; set; }
        public string OnBlocked { get; set; }
        public string OnUserDefined { get; set; }

        // Conversation
        public string Conversation { get; set; }
    }

    /// <summary>
    /// Placeable template (UTP).
    /// </summary>
    public class PlaceableTemplate : BaseTemplate
    {
        public string LocalizedName { get; set; }
        public int Appearance { get; set; }
        public string Description { get; set; }

        // State
        public bool Static { get; set; }
        public bool Useable { get; set; }
        public bool HasInventory { get; set; }
        public bool Plot { get; set; }
        public bool Locked { get; set; }
        public byte LockDC { get; set; }
        public bool KeyRequired { get; set; }
        public string KeyName { get; set; }
        public bool Trapable { get; set; }
        public byte AnimationState { get; set; }

        // Scripts
        public string OnUsed { get; set; }
        public string OnHeartbeat { get; set; }
        public string OnInvDisturbed { get; set; }
        public string OnOpen { get; set; }
        public string OnClosed { get; set; }
        public string OnLock { get; set; }
        public string OnUnlock { get; set; }
        public string OnDamaged { get; set; }
        public string OnDeath { get; set; }
        public string OnUserDefined { get; set; }
        public string OnEndDialogue { get; set; }
        public string OnTrapTriggered { get; set; }
        public string OnDisarm { get; set; }
        public string OnMeleeAttacked { get; set; }

        // Conversation
        public string Conversation { get; set; }
    }

    /// <summary>
    /// Door template (UTD).
    /// </summary>
    public class DoorTemplate : BaseTemplate
    {
        public string LocalizedName { get; set; }
        public string Description { get; set; }
        public int GenericType { get; set; }

        // State
        public bool Static { get; set; }
        public bool Plot { get; set; }
        public bool Locked { get; set; }
        public byte LockDC { get; set; }
        public bool KeyRequired { get; set; }
        public string KeyName { get; set; }
        public bool Trapable { get; set; }
        public short CurrentHP { get; set; }
        public byte Hardness { get; set; }
        public byte AnimationState { get; set; }

        // Transition
        public string LinkedTo { get; set; }
        public byte LinkedToFlags { get; set; }
        public string LinkedToModule { get; set; }

        // Scripts
        public string OnClick { get; set; }
        public string OnClosed { get; set; }
        public string OnDamaged { get; set; }
        public string OnDeath { get; set; }
        public string OnFailToOpen { get; set; }
        public string OnHeartbeat { get; set; }
        public string OnLock { get; set; }
        public string OnMeleeAttacked { get; set; }
        public string OnOpen { get; set; }
        public string OnUnlock { get; set; }
        public string OnUserDefined { get; set; }

        // Conversation
        public string Conversation { get; set; }
    }

    /// <summary>
    /// Trigger template (UTT).
    /// </summary>
    public class TriggerTemplate : BaseTemplate
    {
        public string LocalizedName { get; set; }
        public int Type { get; set; }
        public int Faction { get; set; }

        // Trap flags
        public bool Trapable { get; set; }
        public bool TrapDisarmable { get; set; }
        public bool TrapOneShot { get; set; }
        public byte TrapType { get; set; }
        public byte DisarmDC { get; set; }
        public byte DetectDC { get; set; }

        // Scripts
        public string OnEnter { get; set; }
        public string OnExit { get; set; }
        public string OnHeartbeat { get; set; }
        public string OnUserDefined { get; set; }
        public string OnTrapTriggered { get; set; }
        public string OnDisarm { get; set; }
    }

    /// <summary>
    /// Waypoint template (UTW).
    /// </summary>
    public class WaypointTemplate : BaseTemplate
    {
        public string LocalizedName { get; set; }
        public string Description { get; set; }
        public byte Appearance { get; set; }
        public string MapNote { get; set; }
        public bool MapNoteEnabled { get; set; }
        public bool HasMapNote { get; set; }
    }

    /// <summary>
    /// Sound template (UTS).
    /// </summary>
    public class SoundTemplate : BaseTemplate
    {
        public string LocalizedName { get; set; }
        public bool Active { get; set; }
        public bool Continuous { get; set; }
        public bool Looping { get; set; }
        public bool Positional { get; set; }
        public bool Random { get; set; }

        public byte Volume { get; set; }
        public byte VolumeVariation { get; set; }
        public int Interval { get; set; }
        public int IntervalVariation { get; set; }
        public float MaxDistance { get; set; }
        public float MinDistance { get; set; }
        public float Elevation { get; set; }
    }

    /// <summary>
    /// Encounter template (UTE).
    /// </summary>
    public class EncounterTemplate : BaseTemplate
    {
        public string LocalizedName { get; set; }
        public int Faction { get; set; }
        public bool Active { get; set; }
        public int DifficultyIndex { get; set; }
        public int SpawnOption { get; set; }
        public int MaxCreatures { get; set; }
        public int RecCreatures { get; set; }
        public int ResetTime { get; set; }

        // Scripts
        public string OnEntered { get; set; }
        public string OnExhausted { get; set; }
        public string OnExit { get; set; }
        public string OnHeartbeat { get; set; }
        public string OnUserDefined { get; set; }
    }

    /// <summary>
    /// Store template (UTM).
    /// </summary>
    public class StoreTemplate : BaseTemplate
    {
        public int ID { get; set; }
        public int MarkUp { get; set; }
        public int MarkUpRate { get; set; }
        public int MarkDown { get; set; }
        public int MarkDownRate { get; set; }

        // Scripts
        public string OnOpenStore { get; set; }
        public string OnStoreClosed { get; set; }
    }

    #endregion
}
