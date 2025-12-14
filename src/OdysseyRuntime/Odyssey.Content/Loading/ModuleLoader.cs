using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Odyssey.Content.Interfaces;
using Odyssey.Core.Enums;
using Odyssey.Core.Module;
using Odyssey.Core.Templates;

namespace Odyssey.Content.Loading
{
    /// <summary>
    /// Module loading service - loads KOTOR modules from game resources.
    /// Handles IFO, ARE, GIT, LYT, VIS parsing and entity spawning.
    /// </summary>
    /// <remarks>
    /// Module Loading Pipeline:
    /// 1. Load IFO - Module metadata, entry point, scripts
    /// 2. Load ARE - Area properties (lighting, fog, grass)
    /// 3. Load LYT - Room layout and doorhook positions  
    /// 4. Load VIS - Room visibility graph for culling
    /// 5. Load GIT - Entity spawn lists
    /// 6. Load walkmeshes (WOK) - Navigation and collision
    /// 7. Spawn entities from templates
    /// </remarks>
    public class ModuleLoader
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly ITemplateLoader _templateLoader;

        public ModuleLoader(IGameResourceProvider resourceProvider, ITemplateLoader templateLoader)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
            _templateLoader = templateLoader ?? throw new ArgumentNullException("templateLoader");
        }

        /// <summary>
        /// Loads a module by its resource reference name.
        /// </summary>
        /// <param name="moduleResRef">The module resref (without extension).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The loaded runtime module.</returns>
        public async Task<RuntimeModule> LoadModuleAsync(string moduleResRef, CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(moduleResRef))
            {
                throw new ArgumentException("Module resref cannot be null or empty", "moduleResRef");
            }

            // Create runtime module
            var module = new RuntimeModule();
            module.ResRef = moduleResRef;

            // 1. Load IFO (module info)
            await LoadIfoAsync(module, moduleResRef, ct);

            // 2. Load entry area (first area listed in IFO)
            if (!string.IsNullOrEmpty(module.EntryArea))
            {
                var entryArea = await LoadAreaAsync(module.EntryArea, ct);
                if (entryArea != null)
                {
                    module.AddArea(entryArea);
                }
            }

            return module;
        }

        /// <summary>
        /// Loads an area by its resource reference.
        /// </summary>
        public async Task<RuntimeArea> LoadAreaAsync(string areaResRef, CancellationToken ct = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(areaResRef))
            {
                return null;
            }

            var area = new RuntimeArea();
            area.ResRef = areaResRef;

            // Load ARE (area properties)
            await LoadAreAsync(area, areaResRef, ct);

            // Load LYT (room layout)
            await LoadLytAsync(area, areaResRef, ct);

            // Load VIS (visibility)
            await LoadVisAsync(area, areaResRef, ct);

            // Load GIT (entity instances)
            await LoadGitAsync(area, areaResRef, ct);

            // Load walkmeshes
            await LoadWalkmeshesAsync(area, ct);

            return area;
        }

        #region IFO Loading

        private async Task LoadIfoAsync(RuntimeModule module, string moduleResRef, CancellationToken ct)
        {
            var ifoId = new ResourceIdentifier(moduleResRef, ResourceType.IFO);
            byte[] ifoData = await _resourceProvider.GetResourceBytesAsync(ifoId, ct);

            if (ifoData == null || ifoData.Length == 0)
            {
                return;
            }

            // Parse IFO using CSharpKOTOR format reader
            // The actual parsing is delegated to the template loader or inline parsing
            ParseIfoData(module, ifoData);
        }

        private void ParseIfoData(RuntimeModule module, byte[] data)
        {
            // IFO is a GFF file - parse using CSharpKOTOR
            // For now, use a basic reader that extracts key fields

            // GFF header validation
            if (data.Length < 56)
            {
                return;
            }

            // Check for GFF signature
            if (data[0] != 'G' || data[1] != 'F' || data[2] != 'F')
            {
                return;
            }

            // Parse as GFF structure
            // This would integrate with CSharpKOTOR.Formats.GFF
            // For the initial implementation, extract common fields

            // Note: Full integration with CSharpKOTOR.Resource.Generics.IFOHelpers
            // would be done here when project references are set up
        }

        #endregion

        #region ARE Loading

        private async Task LoadAreAsync(RuntimeArea area, string areaResRef, CancellationToken ct)
        {
            var areId = new ResourceIdentifier(areaResRef, ResourceType.ARE);
            byte[] areData = await _resourceProvider.GetResourceBytesAsync(areId, ct);

            if (areData == null || areData.Length == 0)
            {
                return;
            }

            ParseAreData(area, areData);
        }

        private void ParseAreData(RuntimeArea area, byte[] data)
        {
            // ARE is a GFF file containing area properties
            // Parse lighting, fog, grass, weather settings
            // Integration with CSharpKOTOR.Resource.Generics.AREHelpers
        }

        #endregion

        #region LYT Loading

        private async Task LoadLytAsync(RuntimeArea area, string areaResRef, CancellationToken ct)
        {
            var lytId = new ResourceIdentifier(areaResRef, ResourceType.LYT);
            byte[] lytData = await _resourceProvider.GetResourceBytesAsync(lytId, ct);

            if (lytData == null || lytData.Length == 0)
            {
                return;
            }

            ParseLytData(area, lytData);
        }

        private void ParseLytData(RuntimeArea area, byte[] data)
        {
            // LYT is ASCII format
            // Format: beginlayout ... donelayout
            // Sections: roomcount, trackcount, obstaclecount, doorhookcount

            string text;
            using (var stream = new MemoryStream(data))
            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int lineIndex = 0;

            // Skip until beginlayout
            while (lineIndex < lines.Length && !lines[lineIndex].Trim().Equals("beginlayout", StringComparison.OrdinalIgnoreCase))
            {
                lineIndex++;
            }
            lineIndex++; // Skip beginlayout line

            while (lineIndex < lines.Length)
            {
                string line = lines[lineIndex].Trim();

                if (line.Equals("donelayout", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                // Parse roomcount section
                if (line.StartsWith("roomcount", StringComparison.OrdinalIgnoreCase))
                {
                    int roomCount;
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out roomCount))
                    {
                        for (int i = 0; i < roomCount && lineIndex + 1 + i < lines.Length; i++)
                        {
                            string roomLine = lines[lineIndex + 1 + i].Trim();
                            ParseRoomLine(area, roomLine);
                        }
                        lineIndex += roomCount;
                    }
                }
                // Parse doorhookcount section
                else if (line.StartsWith("doorhookcount", StringComparison.OrdinalIgnoreCase))
                {
                    int hookCount;
                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && int.TryParse(parts[1], out hookCount))
                    {
                        for (int i = 0; i < hookCount && lineIndex + 1 + i < lines.Length; i++)
                        {
                            string hookLine = lines[lineIndex + 1 + i].Trim();
                            // Parse doorhook: room_name door_name x y z qx qy qz qw
                            // Doorhooks define where doors are placed in the layout
                        }
                        lineIndex += hookCount;
                    }
                }

                lineIndex++;
            }
        }

        private void ParseRoomLine(RuntimeArea area, string line)
        {
            // Format: model_name x y z
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
            {
                var room = new RoomInfo();
                room.ModelName = parts[0];

                float x, y, z;
                if (float.TryParse(parts[1], out x) &&
                    float.TryParse(parts[2], out y) &&
                    float.TryParse(parts[3], out z))
                {
                    room.Position = new Vector3(x, y, z);
                }

                area.Rooms.Add(room);
            }
        }

        #endregion

        #region VIS Loading

        private async Task LoadVisAsync(RuntimeArea area, string areaResRef, CancellationToken ct)
        {
            var visId = new ResourceIdentifier(areaResRef, ResourceType.VIS);
            byte[] visData = await _resourceProvider.GetResourceBytesAsync(visId, ct);

            if (visData == null || visData.Length == 0)
            {
                return;
            }

            ParseVisData(area, visData);
        }

        private void ParseVisData(RuntimeArea area, byte[] data)
        {
            // VIS is ASCII format defining room visibility
            // Each room lists which other rooms are visible from it

            string text;
            using (var stream = new MemoryStream(data))
            using (var reader = new StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Build room name to index mapping
            var roomIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < area.Rooms.Count; i++)
            {
                roomIndices[area.Rooms[i].ModelName] = i;
            }

            // Parse visibility entries
            int lineIndex = 0;
            while (lineIndex < lines.Length)
            {
                string line = lines[lineIndex].Trim();

                // Room entry format: room_name visible_count
                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string roomName = parts[0];
                    int visibleCount;
                    if (int.TryParse(parts[1], out visibleCount))
                    {
                        int roomIndex;
                        if (roomIndices.TryGetValue(roomName, out roomIndex))
                        {
                            var room = area.Rooms[roomIndex];
                            room.VisibleRooms = new List<int>();

                            // Read visible room names
                            for (int i = 0; i < visibleCount && lineIndex + 1 + i < lines.Length; i++)
                            {
                                string visibleRoom = lines[lineIndex + 1 + i].Trim();
                                int visibleIndex;
                                if (roomIndices.TryGetValue(visibleRoom, out visibleIndex))
                                {
                                    room.VisibleRooms.Add(visibleIndex);
                                }
                            }
                            lineIndex += visibleCount;
                        }
                    }
                }
                lineIndex++;
            }
        }

        #endregion

        #region GIT Loading

        private async Task LoadGitAsync(RuntimeArea area, string areaResRef, CancellationToken ct)
        {
            var gitId = new ResourceIdentifier(areaResRef, ResourceType.GIT);
            byte[] gitData = await _resourceProvider.GetResourceBytesAsync(gitId, ct);

            if (gitData == null || gitData.Length == 0)
            {
                return;
            }

            await ParseGitDataAsync(area, gitData, ct);
        }

        private async Task ParseGitDataAsync(RuntimeArea area, byte[] data, CancellationToken ct)
        {
            // GIT is a GFF file containing entity instance lists
            // Integration with CSharpKOTOR.Resource.Generics.GITHelpers.ConstructGit

            // For initial implementation, this would:
            // 1. Parse the GFF structure
            // 2. Extract creature/door/placeable/trigger/waypoint/sound lists
            // 3. Load templates for each entity
            // 4. Spawn entities into the area

            // This is a placeholder - full integration requires CSharpKOTOR reference
        }

        #endregion

        #region Walkmesh Loading

        private async Task LoadWalkmeshesAsync(RuntimeArea area, CancellationToken ct)
        {
            // Load walkmeshes for each room
            foreach (var room in area.Rooms)
            {
                if (string.IsNullOrEmpty(room.ModelName))
                {
                    continue;
                }

                // WOK files are walkmeshes for room models
                var wokId = new ResourceIdentifier(room.ModelName, ResourceType.WOK);
                byte[] wokData = await _resourceProvider.GetResourceBytesAsync(wokId, ct);

                if (wokData != null && wokData.Length > 0)
                {
                    // Parse BWM format - integration with CSharpKOTOR.Formats.BWM
                    // Build navigation mesh from walkmesh data
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Interface for loading entity templates from GFF files.
    /// </summary>
    public interface ITemplateLoader
    {
        /// <summary>
        /// Loads a creature template from UTC data.
        /// </summary>
        Task<CreatureTemplate> LoadCreatureTemplateAsync(string resRef, CancellationToken ct);

        /// <summary>
        /// Loads a door template from UTD data.
        /// </summary>
        Task<DoorTemplate> LoadDoorTemplateAsync(string resRef, CancellationToken ct);

        /// <summary>
        /// Loads a placeable template from UTP data.
        /// </summary>
        Task<PlaceableTemplate> LoadPlaceableTemplateAsync(string resRef, CancellationToken ct);

        /// <summary>
        /// Loads a trigger template from UTT data.
        /// </summary>
        Task<TriggerTemplate> LoadTriggerTemplateAsync(string resRef, CancellationToken ct);

        /// <summary>
        /// Loads a waypoint template from UTW data.
        /// </summary>
        Task<WaypointTemplate> LoadWaypointTemplateAsync(string resRef, CancellationToken ct);

        /// <summary>
        /// Loads a sound template from UTS data.
        /// </summary>
        Task<SoundTemplate> LoadSoundTemplateAsync(string resRef, CancellationToken ct);

        /// <summary>
        /// Loads an encounter template from UTE data.
        /// </summary>
        Task<EncounterTemplate> LoadEncounterTemplateAsync(string resRef, CancellationToken ct);

        /// <summary>
        /// Loads a store template from UTM data.
        /// </summary>
        Task<StoreTemplate> LoadStoreTemplateAsync(string resRef, CancellationToken ct);
    }
}
