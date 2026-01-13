using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.VIS;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource.Formats.GFF.Generics.ARE;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Scene;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Eclipse.Scene
{
    /// <summary>
    /// Eclipse engine (Dragon Age Origins, Dragon Age 2) scene builder (graphics-backend agnostic).
    /// Builds abstract rendering structures from ARE (area) files with advanced features.
    /// Works with both MonoGame and Stride backends.
    /// </summary>
    /// <remarks>
    /// Eclipse Scene Builder:
    /// - Based on daorigins.exe, DragonAge2.exe area loading system
    /// - Original implementation: Builds rendering structures from ARE with room-based sections
    /// - ARE file format: Contains area properties, room definitions (audio zones), environmental effects
    /// - Scene building: Parses ARE data, creates area sections from rooms, sets up visibility culling
    /// - Areas: Complex 3D environments with room-based sections, dynamic visibility, physics-based culling
    /// - Graphics-agnostic: Works with any graphics backend (MonoGame, Stride, etc.)
    ///
    /// Room-Based System:
    /// - Eclipse uses rooms (ARERoom list) from ARE files to define area sections
    /// - Rooms define audio zones and weather regions within the area
    /// - Room geometry comes from separate model files (not stored in ARE)
    /// - Room names are used as section identifiers for visibility culling
    ///
    /// Based on reverse engineering of Eclipse engine area loading:
    /// - daorigins.exe: Area loading system with room-based sections
    /// - DragonAge2.exe: Enhanced area loading with advanced features
    /// - ARE file format: Same GFF structure as Odyssey/Aurora engines
    ///
    /// Inheritance:
    /// - BaseSceneBuilder (Runtime.Graphics.Common.Scene) - Common scene building patterns
    ///   - EclipseSceneBuilder (this class) - Eclipse-specific ARE room-based features
    /// </remarks>
    public class EclipseSceneBuilder : BaseSceneBuilder
    {
        private readonly IGameResourceProvider _resourceProvider;

        public EclipseSceneBuilder([NotNull] IGameResourceProvider resourceProvider)
        {
            if (resourceProvider == null)
            {
                throw new ArgumentNullException("resourceProvider");
            }

            _resourceProvider = resourceProvider;
        }

        /// <summary>
        /// Builds a scene from ARE area data (Eclipse-specific).
        /// </summary>
        /// <param name="areData">ARE area data containing advanced features. Can be byte[] (raw ARE file), GFF object, or ARE object.</param>
        /// <returns>Scene data structure with all area sections configured for rendering.</returns>
        /// <remarks>
        /// Scene Building Process (Eclipse engines - daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine area loading system
        /// - Original implementation: Builds rendering structures from ARE with room-based sections
        /// - Process:
        ///   1. Parse ARE file (GFF format with "ARE " signature)
        ///   2. Extract Rooms list from ARE root struct
        ///   3. Create AreaSection objects for each room (audio zones)
        ///   4. Set up room identifiers for visibility culling
        ///   5. Organize sections into scene hierarchy for efficient rendering
        /// - Room-based system: Eclipse uses rooms (audio zones) rather than tiles (Aurora) or LYT rooms (Odyssey)
        /// - Room identifiers: Used for visibility culling and audio zone management
        /// - Advanced features: Dynamic geometry, physics meshes, environmental effects
        ///
        /// ARE file format (GFF with "ARE " signature):
        /// - Root struct contains Rooms (GFFList) with room definitions
        /// - Rooms list contains ARERoom structs with: RoomName (String), EnvAudio (Int32),
        ///   AmbientScale (Single), DisableWeather (UInt8), ForceRating (Int32)
        /// - Rooms define audio zones and weather regions within the area
        /// - Room geometry comes from separate model files (not stored in ARE)
        ///
        /// Based on official BioWare ARE format specification:
        /// - vendor/PyKotor/wiki/Bioware-Aurora-AreaFile.md (Section 2.6: Rooms)
        /// - All engines (Odyssey, Aurora, Eclipse) use the same ARE file format structure
        /// - Eclipse-specific: Rooms used for audio zones and environmental effects
        /// </remarks>
        public EclipseSceneData BuildScene([NotNull] object areData)
        {
            if (areData == null)
            {
                throw new ArgumentNullException("areData");
            }

            // Parse ARE data - handle byte[], GFF, and ARE objects
            ARE are = null;
            if (areData is byte[] areBytes)
            {
                // Parse GFF from byte array, then construct ARE object
                // Based on ResourceAutoHelpers.ReadAre implementation
                GFF gff = GFF.FromBytes(areBytes);
                if (gff == null)
                {
                    throw new ArgumentException("Invalid ARE file: failed to parse GFF", "areData");
                }
                are = AREHelpers.ConstructAre(gff);
            }
            else if (areData is GFF areGff)
            {
                // Use GFF object directly, construct ARE object
                are = AREHelpers.ConstructAre(areGff);
            }
            else if (areData is ARE areObj)
            {
                // Use ARE object directly
                are = areObj;
            }
            else
            {
                throw new ArgumentException("areData must be byte[], GFF, or ARE object", "areData");
            }

            // Create scene data structure
            var sceneData = new EclipseSceneData();
            sceneData.AreaSections = new List<AreaSection>();

            // Handle null or empty ARE data
            if (are == null)
            {
                // Empty scene - no sections
                RootEntity = sceneData;
                return sceneData;
            }

            // Extract rooms from ARE file
            // Based on ARE format: Rooms is GFFList containing ARERoom structs
            // Each room defines an audio zone and weather region
            // Rooms are referenced by VIS files for visibility culling
            if (are.Rooms == null || are.Rooms.Count == 0)
            {
                // No rooms defined - create empty scene
                // Eclipse areas may not always have rooms defined
                RootEntity = sceneData;
                return sceneData;
            }

            // Create AreaSection for each room
            // Based on Eclipse engine: Rooms define area sections for audio zones
            // Room geometry comes from separate model files (not in ARE)
            // Room names are used as section identifiers for visibility culling and audio zones
            // ModelResRef is determined from room name using naming convention or from VIS/layout data when available
            foreach (ARERoom room in are.Rooms)
            {
                if (room == null || string.IsNullOrEmpty(room.Name))
                {
                    // Skip rooms without names (invalid room data)
                    continue;
                }

                // Determine ModelResRef for this room
                // Based on Eclipse engine: Model references are typically derived from room names
                // Eclipse areas use naming conventions where room names often correspond to model file names
                // ModelResRef can be updated later when VIS files or layout data is loaded
                string modelResRef = DetermineModelResRefFromRoomName(room.Name);

                // Create area section for this room
                // Based on Eclipse engine: Room names are used as section identifiers
                // Position is set to zero initially - actual geometry comes from model files
                // ModelResRef is determined from room name (can be updated from VIS/layout data later)
                // RoomName stores the original ARE room name separately from ModelResRef
                AreaSection section = new AreaSection
                {
                    RoomName = room.Name, // Original ARE room name (used for VIS visibility lookups)
                    ModelResRef = modelResRef, // Model reference determined from room name (can be updated from VIS/layout data)
                    Position = Vector3.Zero, // Position determined from model geometry or layout files
                    IsVisible = true, // All sections visible initially, visibility updated by SetCurrentArea
                    MeshData = null // Mesh data loaded on demand by graphics backend
                };

                // Room name is now stored separately from ModelResRef
                // VIS files use room names for visibility calculations
                // ModelResRef is used for loading model files and may differ from room name
                // When VIS data is loaded, ModelResRef may be updated from VIS room names (which are model ResRefs)
                // RoomName always preserves the original ARE room name for identification

                // Add section to scene
                sceneData.AreaSections.Add(section);
            }

            // Set root entity and return scene data
            RootEntity = sceneData;
            return sceneData;
        }

        /// <summary>
        /// Gets the visibility of an area section from the current section (Eclipse-specific).
        /// </summary>
        /// <param name="currentArea">Current area section identifier (model ResRef for VIS lookup, or room name).</param>
        /// <param name="targetArea">Target area section identifier (model ResRef for VIS lookup, or room name) to check visibility for.</param>
        /// <returns>True if the target section is visible from the current section.</returns>
        /// <remarks>
        /// Area Section Visibility (Eclipse engines - daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine room-based visibility system
        /// - Rooms are audio zones that can have visibility relationships
        /// - Uses VIS files (room visibility graph) for static visibility determination
        /// - Falls back to all visible if VIS data is unavailable (physics-based culling can be added later)
        /// - Based on Eclipse engine: Room visibility determined by VIS files or dynamic obstacles
        ///
        /// Implementation details:
        /// - If VIS data is available, uses VIS.GetVisible() to check room-to-room visibility
        /// - VIS room names are model ResRefs (VIS room name = model file name)
        /// - Parameters should be model ResRefs for VIS lookups (VIS.GetVisible() expects model ResRefs)
        /// - Room names are matched case-insensitively (VIS stores lowercase)
        /// - If VIS data is unavailable or rooms don't exist in VIS, defaults to visible (render all)
        /// - Physics-based culling for dynamic obstacles can be added as enhancement
        ///
        /// Note: RoomName and ModelResRef are now separate:
        /// - RoomName: Original ARE room name (for identification)
        /// - ModelResRef: Model file reference (for loading models and VIS lookups)
        /// - VIS room names are model ResRefs, so ModelResRef should be used for VIS lookups
        /// </remarks>
        public override bool IsAreaVisible(string currentArea, string targetArea)
        {
            if (string.IsNullOrEmpty(currentArea) || string.IsNullOrEmpty(targetArea))
            {
                return false;
            }

            // Check VIS data for visibility (if available)
            // VIS.GetVisible() expects model ResRefs (VIS room names are model ResRefs)
            // Parameters should be model ResRefs, but can also be room names (will be matched case-insensitively)
            if (RootEntity is EclipseSceneData sceneData && sceneData.VisibilityGraph != null)
            {
                try
                {
                    // VIS.GetVisible() throws if rooms don't exist, catch and default to visible
                    // VIS room names are model ResRefs, so parameters should be model ResRefs
                    return sceneData.VisibilityGraph.GetVisible(currentArea, targetArea);
                }
                catch (ArgumentException)
                {
                    // Room doesn't exist in VIS data - default to visible (render all)
                    return true;
                }
            }

            // No VIS data available - default to visible (all sections rendered)
            // Future enhancement: Add physics-based culling for dynamic obstacles
            return true;
        }

        /// <summary>
        /// Sets the current area section for visibility culling (Eclipse-specific).
        /// </summary>
        /// <param name="areaIdentifier">Area section identifier (room name) for visibility determination.</param>
        /// <remarks>
        /// Area Section Visibility Culling (Eclipse engines - daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine room-based visibility system
        /// - Sets current section and updates visibility for all sections
        /// - Uses VIS files (room visibility graph) for static visibility determination
        /// - Falls back to all visible if VIS data is unavailable (physics-based culling can be added later)
        ///
        /// Process:
        ///   1. Set current section identifier
        ///   2. If VIS data is available, iterate through all sections in scene
        ///   3. Mark sections as visible if they are visible from current section (using VIS.GetVisible())
        ///   4. Mark all other sections as not visible
        ///   5. If VIS data is unavailable, mark all sections as visible (render all)
        ///
        /// Based on Eclipse engine: Room visibility determined by VIS files or dynamic obstacles
        /// - VIS files define static room-to-room visibility relationships
        /// - Physics-based culling for dynamic obstacles can be added as enhancement
        /// </remarks>
        public override void SetCurrentArea(string areaIdentifier)
        {
            if (RootEntity is EclipseSceneData sceneData)
            {
                sceneData.CurrentSection = areaIdentifier;

                // Update section visibility based on VIS data (if available)
                if (sceneData.AreaSections != null)
                {
                    // If VIS data is available, use it for visibility culling
                    if (sceneData.VisibilityGraph != null && !string.IsNullOrEmpty(areaIdentifier))
                    {
                        // Use VIS data to determine which sections are visible from current section
                        // VIS files use model ResRefs as room names (VIS room names = model ResRefs)
                        // ModelResRef is used for VIS lookups since VIS room names are model ResRefs
                        // RoomName stores the original ARE room name for identification (separate from model ResRef)
                        foreach (var section in sceneData.AreaSections)
                        {
                            if (string.IsNullOrEmpty(section.ModelResRef))
                            {
                                // Skip sections without model reference (invalid section)
                                section.IsVisible = false;
                                continue;
                            }

                            // Check visibility using VIS data
                            // VIS.GetVisible() uses model ResRefs (VIS room names = model ResRefs)
                            // Try ModelResRef first since VIS room names are model ResRefs
                            // If ModelResRef doesn't exist in VIS, try RoomName as fallback (in case ARE room name matches VIS room name)
                            // VIS.GetVisible() throws if rooms don't exist, catch and default to visible
                            try
                            {
                                // Use ModelResRef for VIS lookup (VIS room names are model ResRefs)
                                section.IsVisible = sceneData.VisibilityGraph.GetVisible(areaIdentifier, section.ModelResRef);
                            }
                            catch (ArgumentException)
                            {
                                // ModelResRef doesn't exist in VIS - try RoomName as fallback
                                // In some cases, ARE room name might match VIS room name (model ResRef)
                                try
                                {
                                    if (!string.IsNullOrEmpty(section.RoomName))
                                    {
                                        section.IsVisible = sceneData.VisibilityGraph.GetVisible(areaIdentifier, section.RoomName);
                                    }
                                    else
                                    {
                                        // No RoomName either - default to visible (render section)
                                        section.IsVisible = true;
                                    }
                                }
                                catch (ArgumentException)
                                {
                                    // Neither ModelResRef nor RoomName exists in VIS data - default to visible (render section)
                                    section.IsVisible = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        // No VIS data available - mark all sections as visible (render all)
                        // Future enhancement: Add physics-based culling for dynamic obstacles
                        foreach (var section in sceneData.AreaSections)
                        {
                            section.IsVisible = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clears the current scene and disposes resources (Eclipse-specific).
        /// </summary>
        public override void Clear()
        {
            ClearRoomMeshData();
            RootEntity = null;
        }

        /// <summary>
        /// Gets the list of area sections for rendering.
        /// </summary>
        protected override IList<ISceneRoom> GetSceneRooms()
        {
            if (RootEntity is EclipseSceneData sceneData)
            {
                return sceneData.AreaSections.Cast<ISceneRoom>().ToList();
            }
            return null;
        }

        /// <summary>
        /// Builds a scene from area data (internal implementation).
        /// </summary>
        protected override void BuildSceneInternal(object areaData)
        {
            BuildScene(areaData);
        }

        /// <summary>
        /// Builds a scene from ARE area data with optional VIS visibility data (Eclipse-specific).
        /// </summary>
        /// <param name="areData">ARE area data containing advanced features. Can be byte[] (raw ARE file), GFF object, or ARE object.</param>
        /// <param name="visData">Optional VIS visibility data for room-to-room visibility culling. If null, all rooms are visible.</param>
        /// <returns>Scene data structure with all area sections configured for rendering.</returns>
        /// <remarks>
        /// Enhanced scene building with VIS data support (Eclipse engines - daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine area loading system with VIS culling support
        /// - VIS data defines static room-to-room visibility relationships
        /// - If VIS data is provided, visibility culling will be used in SetCurrentArea()
        /// - If VIS data is null, all sections remain visible (no culling)
        ///
        /// This overload allows explicit VIS data to be provided, similar to OdysseySceneBuilder pattern.
        /// </remarks>
        public EclipseSceneData BuildScene([NotNull] object areData, [CanBeNull] VIS visData)
        {
            EclipseSceneData sceneData = BuildScene(areData);

            // Set VIS data for visibility culling
            if (sceneData != null)
            {
                sceneData.VisibilityGraph = visData;
            }

            return sceneData;
        }

        /// <summary>
        /// Attempts to load VIS file for the specified area resref and sets it in the current scene data.
        /// </summary>
        /// <param name="areaResRef">Area resource reference (resref) to load VIS file for. VIS file is typically named "{areaResRef}.vis".</param>
        /// <remarks>
        /// VIS File Loading (Eclipse engines - daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine VIS file loading system
        /// - VIS files define static room-to-room visibility relationships
        /// - VIS file naming convention: "{areaResRef}.vis" (e.g., "ar_m01aa.vis")
        /// - If VIS file cannot be loaded, scene continues without VIS data (all rooms visible)
        /// - This method is asynchronous and should be awaited
        ///
        /// Usage:
        /// - Call this method after BuildScene() to load VIS data if available
        /// - If VIS loading fails, visibility culling falls back to all visible
        /// </remarks>
        public async Task LoadVISDataAsync(string areaResRef)
        {
            if (string.IsNullOrEmpty(areaResRef) || _resourceProvider == null)
            {
                return;
            }

            if (!(RootEntity is EclipseSceneData sceneData))
            {
                return;
            }

            try
            {
                // Attempt to load VIS file from resource provider
                ResourceIdentifier visResourceId = new ResourceIdentifier(areaResRef, ResourceType.VIS);
                bool exists = await _resourceProvider.ExistsAsync(visResourceId, System.Threading.CancellationToken.None);

                if (exists)
                {
                    using (var stream = await _resourceProvider.OpenResourceAsync(visResourceId, System.Threading.CancellationToken.None))
                    {
                        if (stream != null && stream.Length > 0)
                        {
                            // Read entire VIS file data into memory
                            // Use MemoryStream for efficient reading
                            using (var memoryStream = new MemoryStream())
                            {
                                await stream.CopyToAsync(memoryStream);
                                byte[] visData = memoryStream.ToArray();

                                if (visData.Length > 0)
                                {
                                    // Parse VIS file using VISAuto
                                    VIS vis = VISAuto.ReadVis(visData);
                                    if (vis != null)
                                    {
                                        sceneData.VisibilityGraph = vis;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // VIS file loading failed - continue without VIS data (all rooms will be visible)
                // This is acceptable behavior - not all areas have VIS files
            }
        }

        /// <summary>
        /// Sets the VIS visibility data for the current scene.
        /// </summary>
        /// <param name="visData">VIS visibility data for room-to-room visibility culling. Can be null to disable VIS culling.</param>
        /// <remarks>
        /// Sets VIS data for visibility culling (Eclipse engines - daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine VIS data management
        /// - VIS data defines static room-to-room visibility relationships
        /// - If VIS data is set, visibility culling will be used in SetCurrentArea()
        /// - If VIS data is null, all sections remain visible (no culling)
        ///
        /// This method allows VIS data to be set manually (e.g., from ModuleLoader or cached data).
        /// </remarks>
        public void SetVISData([CanBeNull] VIS visData)
        {
            if (RootEntity is EclipseSceneData sceneData)
            {
                sceneData.VisibilityGraph = visData;

                // Update ModelResRef from VIS data if available
                // VIS files may contain model references that can be used to update section ModelResRef
                if (visData != null && sceneData.AreaSections != null)
                {
                    UpdateModelResRefFromVIS(visData, sceneData.AreaSections);
                }
            }
        }

        /// <summary>
        /// Determines the model resource reference from a room name.
        /// Based on Eclipse engine: Room names typically correspond to model file names.
        /// Eclipse areas use naming conventions where room names often match model file names.
        /// </summary>
        /// <param name="roomName">Room name identifier from ARE file.</param>
        /// <returns>Model resource reference (typically the room name, but can be transformed if needed).</returns>
        /// <remarks>
        /// Model Reference Determination (Eclipse engines - daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine room-to-model mapping conventions
        /// - Room names are typically used directly as model references
        /// - Some areas may use naming conventions (e.g., "room_01" -> "ar_m01aa_room_01")
        /// - Model references can be updated later from VIS files or layout data
        ///
        /// Current implementation: Uses room name directly as model reference
        /// This matches Eclipse engine behavior where room names correspond to model file names
        /// </remarks>
        private string DetermineModelResRefFromRoomName(string roomName)
        {
            if (string.IsNullOrEmpty(roomName))
            {
                return string.Empty;
            }

            // Based on Eclipse engine: Room names are typically used directly as model references
            // Eclipse areas use naming conventions where room names match model file names
            // Example: Room name "room_01" corresponds to model file "room_01.mdl" or similar
            //
            // Some areas may use prefixes or suffixes, but the base name is typically the room name
            // Model references can be updated later from VIS files or layout data if different naming is used

            // Return room name as model reference (standard Eclipse convention)
            return roomName;
        }

        /// <summary>
        /// Updates ModelResRef for area sections based on VIS file data.
        /// VIS files contain room names that correspond to model ResRefs (MDL file names).
        /// </summary>
        /// <param name="visData">VIS visibility data containing room names (model ResRefs).</param>
        /// <param name="sections">List of area sections to update.</param>
        /// <remarks>
        /// VIS-Based Model Reference Update (Eclipse engines - daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine VIS file format
        /// - VIS files contain room names that ARE the model ResRefs (MDL file names)
        /// - ARE room names may differ from model ResRefs in some cases
        /// - When VIS data is available, use VIS room names as authoritative source for model ResRefs
        /// - daorigins.exe: VIS room names correspond to MDL model file names
        /// - DragonAge2.exe: VIS room names correspond to model file names (GR2/MMH format)
        ///
        /// Implementation:
        /// 1. Get all room names from VIS file (these are the model ResRefs)
        /// 2. For each section, check if its current ModelResRef exists in VIS
        /// 3. If ModelResRef exists in VIS, it's correct (keep it)
        /// 4. If ModelResRef doesn't exist in VIS, try to find matching VIS room by name comparison
        /// 5. If match found, update ModelResRef to VIS room name (authoritative model ResRef)
        /// 6. If no match found, keep original ModelResRef (may be valid but not in VIS)
        ///
        /// Based on VIS file format specification:
        /// - vendor/PyKotor/wiki/VIS-File-Format.md
        /// - VIS room names are typically the MDL ResRef of the room
        /// - Room names in VIS are stored lowercase for case-insensitive comparison
        /// </remarks>
        private void UpdateModelResRefFromVIS([NotNull] VIS visData, [NotNull] List<AreaSection> sections)
        {
            if (visData == null || sections == null)
            {
                return;
            }

            // Get all room names from VIS file (these are the model ResRefs)
            // Based on VIS file format: Room names in VIS are the model ResRefs (MDL file names)
            // daorigins.exe: VIS room names correspond to MDL model file names
            // DragonAge2.exe: VIS room names correspond to model file names
            HashSet<string> visRooms = visData.AllRooms();
            if (visRooms == null || visRooms.Count == 0)
            {
                // VIS file has no rooms - cannot update ModelResRef
                return;
            }

            // Create case-insensitive lookup dictionary for efficient matching
            // VIS stores room names lowercase, but we need to match against section ModelResRef
            // which may be in different case
            var visRoomLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string visRoom in visRooms)
            {
                if (!string.IsNullOrEmpty(visRoom))
                {
                    // Store both lowercase and original case for matching
                    visRoomLookup[visRoom] = visRoom;
                }
            }

            // Update ModelResRef for each section based on VIS data
            // Based on Eclipse engine: VIS room names are authoritative model ResRefs
            // RoomName is preserved (not updated) - it always stores the original ARE room name
            // Only ModelResRef is updated from VIS data (VIS room names are model ResRefs)
            foreach (AreaSection section in sections)
            {
                if (section == null || string.IsNullOrEmpty(section.ModelResRef))
                {
                    // Skip sections without ModelResRef
                    continue;
                }

                string currentModelResRef = section.ModelResRef;

                // Check if current ModelResRef exists in VIS (case-insensitive)
                // If it exists, it's correct (VIS room names are model ResRefs)
                if (visRoomLookup.ContainsKey(currentModelResRef))
                {
                    // ModelResRef exists in VIS - it's correct, keep it
                    // Optionally normalize to VIS case (use VIS room name as authoritative)
                    string visRoomName = visRoomLookup[currentModelResRef];
                    if (!string.Equals(currentModelResRef, visRoomName, StringComparison.Ordinal))
                    {
                        // Update to VIS room name (authoritative case)
                        section.ModelResRef = visRoomName;
                    }
                    continue;
                }

                // Current ModelResRef doesn't exist in VIS - try to find matching VIS room
                // This handles cases where ARE room name differs from model ResRef
                // Search for VIS room that matches the section's ModelResRef (case-insensitive)
                string matchingVisRoom = null;
                foreach (string visRoom in visRooms)
                {
                    if (string.Equals(visRoom, currentModelResRef, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingVisRoom = visRoom;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(matchingVisRoom))
                {
                    // Found matching VIS room - update ModelResRef to VIS room name
                    // VIS room name is the authoritative model ResRef
                    section.ModelResRef = matchingVisRoom;
                }
                // If no match found, keep original ModelResRef
                // It may be valid but not present in VIS (some areas may not have all rooms in VIS)
            }
        }
    }

    /// <summary>
    /// Scene data for Eclipse engine (daorigins.exe, DragonAge2.exe, , ).
    /// Contains area sections and current section tracking.
    /// Graphics-backend agnostic.
    /// </summary>
    /// <remarks>
    /// Eclipse Scene Data Structure:
    /// - Based on Eclipse engine area structure
    /// - AreaSections: Complex 3D area sections with dynamic features
    /// - CurrentSection: Currently active section for visibility determination
    /// - VisibilityGraph: VIS data for room-to-room visibility culling (optional)
    /// - Graphics-agnostic: Can be rendered by any graphics backend
    /// </remarks>
    public class EclipseSceneData
    {
        /// <summary>
        /// Gets or sets the list of area sections in the scene.
        /// </summary>
        public List<AreaSection> AreaSections { get; set; }

        /// <summary>
        /// Gets or sets the current area section identifier for visibility culling.
        /// </summary>
        [CanBeNull]
        public string CurrentSection { get; set; }

        /// <summary>
        /// Gets or sets the visibility graph (VIS data) for room-to-room visibility culling.
        /// </summary>
        /// <remarks>
        /// VIS Data (Eclipse engines - daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine VIS file format
        /// - Defines static room-to-room visibility relationships
        /// - Used by SetCurrentArea() to determine which sections are visible
        /// - If null, all sections remain visible (no culling)
        /// - VIS files are optional - not all areas have VIS data
        /// </remarks>
        [CanBeNull]
        public VIS VisibilityGraph { get; set; }
    }

    /// <summary>
    /// Area section data for rendering (Eclipse-specific).
    /// Graphics-backend agnostic.
    /// </summary>
    /// <remarks>
    /// Area Section:
    /// - Based on Eclipse engine area structure
    /// - RoomName: Original ARE room name identifier (used for VIS visibility lookups)
    /// - ModelResRef: Area section model reference (used for loading model files)
    /// - Position: World position
    /// - IsVisible: Visibility flag updated by physics culling
    /// - MeshData: Abstract mesh data loaded by graphics backend
    ///
    /// Room Name vs Model ResRef:
    /// - RoomName: Original room name from ARE file, used for VIS visibility calculations
    /// - ModelResRef: Model file reference (MDL/GR2/MMH), used for loading geometry
    /// - These may differ: ARE room names are identifiers, model ResRefs are file names
    /// - VIS files use model ResRefs (room names in VIS = model file names)
    /// - When VIS data is loaded, ModelResRef may be updated from VIS room names
    /// - RoomName always preserves the original ARE room name for identification
    ///
    /// Based on Eclipse engine (daorigins.exe, DragonAge2.exe):
    /// - daorigins.exe: ARE rooms have names, VIS files contain model ResRefs
    /// - DragonAge2.exe: Same structure, model ResRefs may differ from ARE room names
    /// </remarks>
    public class AreaSection : ISceneRoom
    {
        /// <summary>
        /// Gets or sets the original ARE room name identifier.
        /// Used for VIS visibility lookups and room identification.
        /// This is the room name from the ARE file, separate from the model resource reference.
        /// </summary>
        /// <remarks>
        /// Room Name (Eclipse engines - daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine ARE room structure
        /// - Original room name from ARE file Rooms list
        /// - Used for VIS visibility calculations (VIS.GetVisible() uses room names)
        /// - Preserved even when ModelResRef is updated from VIS data
        /// - Case-sensitive: Stored as-is from ARE file
        /// - VIS lookups: Converted to lowercase for case-insensitive matching
        ///
        /// Based on ARE file format:
        /// - vendor/PyKotor/wiki/Bioware-Aurora-AreaFile.md (Section 2.6: Rooms)
        /// - ARE rooms have RoomName field (String) that identifies the room
        /// - VIS files reference rooms by name for visibility calculations
        /// </remarks>
        public string RoomName { get; set; }

        /// <summary>
        /// Gets or sets the model resource reference for this area section.
        /// Used for loading model files (MDL/GR2/MMH format).
        /// May differ from RoomName - model ResRefs are file names, room names are identifiers.
        /// </summary>
        /// <remarks>
        /// Model ResRef (Eclipse engines - daorigins.exe, DragonAge2.exe):
        /// - Based on Eclipse engine model loading system
        /// - Model file reference (MDL for daorigins.exe, GR2/MMH for DragonAge2.exe)
        /// - Used for loading geometry from resource files
        /// - May be updated from VIS data when VIS files are loaded
        /// - VIS room names are model ResRefs (VIS room name = model file name)
        /// - Initially derived from ARE room name, but can be updated from VIS/layout data
        ///
        /// Based on Eclipse engine model loading:
        /// - daorigins.exe: Model ResRefs are MDL file names
        /// - DragonAge2.exe: Model ResRefs are GR2/MMH file names
        /// - VIS files contain model ResRefs as room names
        /// </remarks>
        public string ModelResRef { get; set; }
        public Vector3 Position { get; set; }
        public bool IsVisible { get; set; }

        /// <summary>
        /// Area section mesh data loaded from model. Null until loaded on demand by graphics backend.
        /// </summary>
        [CanBeNull]
        public IRoomMeshData MeshData { get; set; }
    }
}

