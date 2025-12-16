using System.Collections.Generic;
using AuroraEngine.Common;
using AuroraEngine.Common.Resources;
using JetBrains.Annotations;

namespace BioWareEngines.Engines.Odyssey.Templates
{
    // Moved from AuroraEngine.Common.Resource.Generics.UTE to Odyssey.Engines.Odyssey.Templates
    // This is KOTOR/Odyssey-specific GFF template structure
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ute.py:15
    /// <summary>
    /// Stores encounter data.
    ///
    /// UTE files are GFF-based format files that store encounter definitions including
    /// creature spawn lists, difficulty, respawn settings, and script hooks.
    /// </summary>
    /// <remarks>
    /// UTE (Encounter Template) Format:
    /// - Based on swkotor2.exe encounter template system
    /// - Located via string references: "Encounter" @ 0x007bc544, "EncounterList" @ 0x007c0c80, "Encounter template '%s' doesn't exist.\n" @ 0x007bf78c
    /// - Encounter loading: FUN_005223a0 @ 0x005223a0 loads encounter from GFF (construct_ute equivalent)
    /// - Encounter saving: FUN_005226d0 @ 0x005226d0 saves encounter to GFF (dismantle_ute equivalent)
    /// - Original implementation: UTE files are GFF with "UTE " signature containing encounter template data
    /// - GFF fields: TemplateResRef, Tag, Active, DifficultyIndex, Difficulty, Faction, MaxCreatures, RecCreatures, Respawns, SpawnOption, etc.
    /// - Creature spawns: CreatureList contains creature spawn definitions (ResRef, Appearance, CR, SingleSpawn, GuaranteedCount)
    /// - Script hooks: OnEntered, OnExit, OnExhausted, OnHeartbeat, OnUserDefined
    /// - Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ute.py:15
    /// </remarks>
    [PublicAPI]
    public sealed class UTE
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ute.py:15
        // Original: BINARY_TYPE = ResourceType.UTE
        public static readonly ResourceType BinaryType = ResourceType.UTE;

        // Basic UTE properties
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ute.py:144-175
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public string Tag { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public bool Active { get; set; }
        public int DifficultyId { get; set; }
        public int DifficultyIndex { get; set; }
        public int Faction { get; set; }

        // Alias for Faction to match Python naming
        public int FactionId
        {
            get { return Faction; }
            set { Faction = value; }
        }

        public int MaxCreatures { get; set; }
        public int RecCreatures { get; set; }
        public int Respawn { get; set; }

        // Alias for Respawn to match Python naming (can be -1 for infinite)
        public int Respawns
        {
            get { return Respawn; }
            set { Respawn = value; }
        }

        public int RespawnTime { get; set; }
        public int Reset { get; set; }
        public int ResetTime { get; set; }
        public int PlayerOnly { get; set; }
        public int SingleSpawn { get; set; }

        // Alias for SingleSpawn to match Python naming (bool in Python, int in C#)
        // Matching PyKotor implementation: self.single_shot: bool = False
        // Original: single_shot: "SpawnOption" field. Whether encounter spawns only once.
        public bool SingleShot
        {
            get { return SingleSpawn != 0; }
            set { SingleSpawn = value ? 1 : 0; }
        }

        // Matching PyKotor implementation: self.on_entered: ResRef = ResRef.from_blank()
        // Original: on_entered: "OnEntered" field. Script to run when encounter area is entered.
        public ResRef OnEnteredScript { get; set; } = ResRef.FromBlank();

        // Alias to match Python naming
        public ResRef OnEntered
        {
            get { return OnEnteredScript; }
            set { OnEnteredScript = value; }
        }

        // Matching PyKotor implementation: self.on_exit: ResRef = ResRef.from_blank()
        // Original: on_exit: "OnExit" field. Script to run when leaving encounter area.
        public ResRef OnExitScript { get; set; } = ResRef.FromBlank();

        // Alias to match Python naming
        public ResRef OnExit
        {
            get { return OnExitScript; }
            set { OnExitScript = value; }
        }

        // Matching PyKotor implementation: self.on_exhausted: ResRef = ResRef.from_blank()
        // Original: on_exhausted: "OnExhausted" field. Script to run when encounter is exhausted.
        public ResRef OnExhaustedScript { get; set; } = ResRef.FromBlank();

        // Alias to match Python naming
        public ResRef OnExhausted
        {
            get { return OnExhaustedScript; }
            set { OnExhaustedScript = value; }
        }

        // Matching PyKotor implementation: self.on_heartbeat: ResRef = ResRef.from_blank()
        // Original: on_heartbeat: "OnHeartbeat" field. Script to run on heartbeat.
        public ResRef OnHeartbeatScript { get; set; } = ResRef.FromBlank();

        // Alias to match Python naming
        public ResRef OnHeartbeat
        {
            get { return OnHeartbeatScript; }
            set { OnHeartbeatScript = value; }
        }

        // Matching PyKotor implementation: self.on_user_defined: ResRef = ResRef.from_blank()
        // Original: on_user_defined: "OnUserDefined" field. Script to run on user-defined event.
        public ResRef OnUserDefinedScript { get; set; } = ResRef.FromBlank();

        // Alias to match Python naming
        public ResRef OnUserDefined
        {
            get { return OnUserDefinedScript; }
            set { OnUserDefinedScript = value; }
        }

        // Matching PyKotor implementation: self.name: LocalizedString = LocalizedString.from_invalid()
        // Original: name: "LocalizedName" field. Localized name. Not used by the game engine.
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();

        // Matching PyKotor implementation: self.palette_id: int = 0
        // Original: palette_id: "PaletteID" field. Palette identifier. Used in toolset only.
        public int PaletteId { get; set; }

        // Creature spawn list
        public List<UTECreature> Creatures { get; set; } = new List<UTECreature>();

        public UTE()
        {
        }
    }

    /// <summary>
    /// Represents a creature spawn in an encounter.
    /// </summary>
    [PublicAPI]
    public sealed class UTECreature
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/ute.py:215-222
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public int Appearance { get; set; }

        // Alias for Appearance to match Python naming
        public int AppearanceId
        {
            get { return Appearance; }
            set { Appearance = value; }
        }

        public int SingleSpawn { get; set; }

        // Alias for SingleSpawn to match Python naming (bool in Python, int in C#)
        public bool SingleSpawnBool
        {
            get { return SingleSpawn != 0; }
            set { SingleSpawn = value ? 1 : 0; }
        }

        public int CR { get; set; }

        // Alias for CR to match Python naming (float in Python, int in C#)
        public float ChallengeRating
        {
            get { return CR; }
            set { CR = (int)value; }
        }

        public int GuaranteedCount { get; set; }

        public UTECreature()
        {
        }
    }
}

