using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Games.Common.Components
{
    /// <summary>
    /// Base implementation of waypoint component functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Waypoint Component Implementation:
    /// - Common waypoint functionality shared across all engines
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (if any differences exist)
    /// - Cross-engine analysis:
    ///   - Odyssey: swkotor.exe, swkotor2.exe
    ///     - swkotor2.exe: FUN_004e08e0 @ 0x004e08e0 loads waypoint instances from GIT
    ///     - String references: "WaypointList" @ 0x007bd288, "Waypoint" @ 0x007bc510, "STARTWAYPOINT" @ 0x007be034
    ///     - "MapNote" @ 0x007bd10c, "MapNoteEnabled" @ 0x007bd118
    ///   - Aurora: nwmain.exe
    ///     - CNWSWaypoint constructor @ 0x140508d60, LoadWaypoint @ 0x140509f80, SaveWaypoint @ 0x14050a4d0
    ///     - String references: "WaypointList" @ 0x140ddb7a0, "Tag" @ 0x140dca0f0, "LocalizedName", "MapNote", "MapNoteEnabled"
    ///     - LoadWaypoint loads: Tag (CExoString at this+0x20), LocalizedName (CExoLocString at this+800), Position, Orientation, HasMapNote (bool at this+0x308), MapNoteEnabled (bool at this+0x30c), MapNote (CExoLocString at this+0x310)
    ///   - Eclipse: daorigins.exe, DragonAge2.exe (waypoint system similar, needs verification)
    ///   - Infinity: MassEffect.exe, MassEffect2.exe (waypoint system similar, needs verification)
    ///
    /// Common functionality across all engines:
    /// - Template resource reference: TemplateResRef identifies the UTW template used
    /// - Map note text: MapNote contains localized text for minimap display
    /// - Map note enabled flag: MapNoteEnabled controls whether map note is displayed
    /// - Has map note flag: HasMapNote indicates if waypoint has a map note configured
    /// - Waypoints are invisible markers used for scripting and navigation
    /// - GetWaypointByTag functions find waypoints by tag (searches all waypoints in current area)
    /// - Waypoints used for: Module transitions (LinkedTo field), script positioning, area navigation, party spawning
    /// - STARTWAYPOINT: Special waypoint tag used for module entry positioning (party spawns at STARTWAYPOINT if no TransitionDestination)
    ///
    /// Engine-specific differences (handled in subclasses):
    /// - Odyssey: Appearance (int), Description (int - localized string reference)
    /// - Aurora: LocalizedName (CExoLocString) - handled via entity DisplayName property
    /// - Eclipse/Infinity: May have different properties (to be determined via reverse engineering)
    /// </remarks>
    public class BaseWaypointComponent : IComponent, IWaypointComponent
    {
        private string _templateResRef;
        private string _mapNote;
        private bool _mapNoteEnabled;
        private bool _hasMapNote;

        public IEntity Owner { get; set; }

        public virtual void OnAttach() { }
        public virtual void OnDetach() { }

        public BaseWaypointComponent()
        {
            _templateResRef = string.Empty;
            _mapNote = string.Empty;
            _mapNoteEnabled = false;
            _hasMapNote = false;
        }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Identifies the UTW template resource used for this waypoint.
        /// Based on TemplateResRef field in waypoint GFF structures.
        /// </remarks>
        public virtual string TemplateResRef
        {
            get => _templateResRef;
            set => _templateResRef = value ?? string.Empty;
        }

        /// <summary>
        /// Map note text.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Localized text displayed on minimap when MapNoteEnabled is true.
        /// Based on MapNote field in waypoint GFF structures (CExoLocString in Aurora, string in Odyssey).
        /// </remarks>
        public virtual string MapNote
        {
            get => _mapNote;
            set => _mapNote = value ?? string.Empty;
        }

        /// <summary>
        /// Whether the map note is enabled.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Controls whether map note is displayed on minimap.
        /// Based on MapNoteEnabled field in waypoint GFF structures.
        /// </remarks>
        public virtual bool MapNoteEnabled
        {
            get => _mapNoteEnabled;
            set => _mapNoteEnabled = value;
        }

        /// <summary>
        /// Whether this waypoint has a map note.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Indicates if waypoint has a map note configured.
        /// Based on HasMapNote field in waypoint GFF structures.
        /// </remarks>
        public virtual bool HasMapNote
        {
            get => _hasMapNote;
            set => _hasMapNote = value;
        }
    }
}

