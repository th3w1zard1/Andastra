using System.Collections.Generic;
using System.Numerics;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Represents a game area (module area) with rooms and objects.
    /// </summary>
    /// <remarks>
    /// Common Area Interface across all BioWare engines:
    ///
    /// Cross-Engine Analysis (Ghidra reverse engineering):
    ///
    /// Odyssey Engine (swkotor.exe, swkotor2.exe):
    /// - String references: "Area" @ 0x007be340, "AreaName" @ 0x007be340, "AREANAME" @ 0x007be1dc
    /// - "AreaId" @ 0x007bef48, "AreaMap" @ 0x007bd118, "AreaMapResX" @ 0x007bd10c, "AreaMapResY" @ 0x007bd100
    /// - "AreaProperties" @ 0x007bd228, "AreaEffectList" @ 0x007bd0d4, "AreaList" @ 0x007c0b7c
    /// - "EVENT_AREA_TRANSITION" @ 0x007bcbdc, "EVENT_REMOVE_FROM_AREA" @ 0x007bcddc
    /// - Error messages: "Area %s is not a valid area." @ 0x007c22bc, "Area %s not valid." @ 0x007c22dc
    /// - Coordinate validation: "X co-ordinate outside of area, should be in [%f, %f]" @ 0x007c224c
    /// - "Y co-ordinate outside of area, should be in [%f, %f]" @ 0x007c2284
    /// - Event dispatching: 0x004dcfb0 @ 0x004dcfb0 handles area events including EVENT_AREA_TRANSITION (case 0x1a) and EVENT_REMOVE_FROM_AREA (case 4)
    /// - Save game integration: 0x004eb750 @ 0x004eb750 saves AREANAME to save game NFO file
    /// - Pathfinding module: "?nwsareapathfind.cpp" @ 0x007be3ff indicates area pathfinding implementation
    /// - Area number tracking: "AreaNumber" @ 0x007c7324 used for area identification
    /// - LoadAreaProperties @ 0x004e26d0 reads area properties from ARE file
    /// - SaveAreaProperties @ 0x004e11d0 writes area properties to ARE file
    ///
    /// Aurora Engine (nwmain.exe):
    /// - CNWSArea::LoadArea @ 0x14035f4e0: Loads ARE file, calls LoadAreaHeader, LoadTileSetInfo, LoadGIT
    /// - CNWSArea::UnloadArea @ 0x1403681f0: Unloads area and cleans up resources
    /// - CNWSArea::LoadAreaEffects @ 0x14035f780: Loads area effects from ARE file
    /// - CNWSArea::LoadAreaHeader @ 0x14035faf0: Loads area header information
    /// - String references: "Area_Destroyed" @ 0x140dca3f0, "Area_SetName" @ 0x140dca3e0
    /// - "Area_Weather" @ 0x140dca330, "Area_UpdateSkyBox" @ 0x140dca378
    /// - "Area_UpdateFogColor" @ 0x140dca390, "Area_UpdateFogAmount" @ 0x140dca3a8
    /// - "Area_RecomputeStaticLighting" @ 0x140dca340, "Area_ChangeDayNight" @ 0x140dca360
    /// - Tile-based area construction with enhanced weather and environmental systems
    ///
    /// Eclipse Engine (daorigins.exe, DragonAge2.exe, , ):
    /// - CArea class: Area management with advanced physics and lighting systems
    /// - String references: "CArea" @ 0x00b0d500, "AreaTransition" @ 0x00b1ad60
    /// - "Mod_Entry_Area" @ 0x00ae821c, "AreaEffectList" @ 0x00af4ea0
    /// - "Area_Name" @ 0x00afbbf8, "Area_ID" @ 0x00afbc04, "Area_List" @ 0x00afbc0c
    /// - "Area Transition - Start" @ 0x00af8188, "Area Transition - End" @ 0x00af8150
    /// - "Area List Swap - Start" @ 0x00af81e4, "Area List Swap - End" @ 0x00af81b8
    /// - Advanced physics integration, destructible environments, dynamic lighting
    ///
    /// Common Functionality (all engines):
    /// - Areas loaded from ARE (area properties) and GIT (instances) file formats
    /// - ARE file format: GFF with "ARE " signature containing area static properties (lighting, fog, grass)
    /// - GIT file format: GFF with "GIT " signature containing dynamic object instances
    /// - Areas contain entities (creatures, doors, placeables, triggers, waypoints, sounds)
    /// - Navigation mesh (walkmesh) provides pathfinding and collision detection
    /// - Area transitions: Entities can move between areas with position projection to walkmesh
    /// - Unescapable flag: Prevents players from leaving area (common across all engines)
    ///
    /// Engine-Specific Features:
    /// - Odyssey: Stealth XP system (StealthXPEnabled property)
    /// - Aurora: Tile-based areas, weather simulation, enhanced area effects
    /// - Eclipse: Physics simulation, destructible environments, advanced lighting, dynamic area modifications
    ///
    /// Inheritance Structure:
    /// - Base Class: BaseArea (Runtime.Games.Common) - Contains common functionality
    ///   - Odyssey: OdysseyArea : BaseArea (swkotor.exe, swkotor2.exe)
    ///   - Aurora: AuroraArea : BaseArea (nwmain.exe)
    ///   - Eclipse: EclipseArea : BaseArea (daorigins.exe, DragonAge2.exe, , )
    ///
    /// Based on ARE/GIT file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public interface IArea
    {
        /// <summary>
        /// The resource reference name of this area.
        /// </summary>
        string ResRef { get; }

        /// <summary>
        /// The display name of the area.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// The tag of the area.
        /// </summary>
        string Tag { get; }

        /// <summary>
        /// All creatures in this area.
        /// </summary>
        IEnumerable<IEntity> Creatures { get; }

        /// <summary>
        /// All placeables in this area.
        /// </summary>
        IEnumerable<IEntity> Placeables { get; }

        /// <summary>
        /// All doors in this area.
        /// </summary>
        IEnumerable<IEntity> Doors { get; }

        /// <summary>
        /// All triggers in this area.
        /// </summary>
        IEnumerable<IEntity> Triggers { get; }

        /// <summary>
        /// All waypoints in this area.
        /// </summary>
        IEnumerable<IEntity> Waypoints { get; }

        /// <summary>
        /// All sounds in this area.
        /// </summary>
        IEnumerable<IEntity> Sounds { get; }

        /// <summary>
        /// Gets an object by tag within this area.
        /// </summary>
        IEntity GetObjectByTag(string tag, int nth = 0);

        /// <summary>
        /// Gets the walkmesh navigation system for this area.
        /// </summary>
        INavigationMesh NavigationMesh { get; }

        /// <summary>
        /// Tests if a point is on walkable ground.
        /// </summary>
        bool IsPointWalkable(Vector3 point);

        /// <summary>
        /// Projects a point onto the walkmesh.
        /// </summary>
        bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height);

        /// <summary>
        /// Gets or sets whether the area is unescapable (players cannot leave).
        /// TRUE means the area cannot be escaped, FALSE means it can be escaped.
        /// </summary>
        bool IsUnescapable { get; set; }

        /// <summary>
        /// Gets or sets whether stealth XP is enabled for this area.
        /// TRUE means stealth XP is enabled, FALSE means it is disabled.
        /// </summary>
        /// <remarks>
        /// Odyssey Engine Specific (swkotor.exe, swkotor2.exe):
        /// - StealthXPEnabled stored in AreaProperties GFF nested struct
        /// - Located via string references: "StealthXPEnabled" @ 0x007bd1b4 (swkotor2.exe)
        /// - LoadAreaProperties @ 0x004e26d0 (swkotor2.exe) reads from ARE file
        /// - SaveAreaProperties @ 0x004e11d0 (swkotor2.exe) writes to ARE file
        /// - Not present in Aurora or Eclipse engines (always returns false in those implementations)
        /// </remarks>
        bool StealthXPEnabled { get; set; }

        /// <summary>
        /// Updates the area state (area effects, lighting, weather, entity spawning/despawning).
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        void Update(float deltaTime);
    }
}

