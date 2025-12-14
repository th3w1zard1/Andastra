using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using CSharpKOTOR.Formats.BWM;
using CSharpKOTOR.Formats.LYT;
using CSharpKOTOR.Formats.VIS;
using CSharpKOTOR.Resources;
using Odyssey.Content.Converters;
using Odyssey.Content.Interfaces;
using Odyssey.Core.Navigation;

namespace Odyssey.Content.Loaders
{
    /// <summary>
    /// Loads module data from KOTOR game files.
    /// </summary>
    /// <remarks>
    /// Module loading pipeline:
    /// 1. Load module.ifo to get area and entry point info
    /// 2. Load area.are for area properties
    /// 3. Load area.git for entity instance spawning
    /// 4. Load area.lyt for room layout
    /// 5. Load area.vis for room visibility
    /// 6. Load room walkmeshes for navigation
    /// </remarks>
    public class ModuleLoader
    {
        private readonly IGameResourceProvider _resourceProvider;

        public ModuleLoader(IGameResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
        }

        /// <summary>
        /// Loads the layout (LYT) for a module.
        /// </summary>
        public async Task<LYT> LoadLayoutAsync(string moduleResRef, CancellationToken ct = default(CancellationToken))
        {
            var id = new ResourceIdentifier(moduleResRef, ResourceType.LYT);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var reader = new LYTAsciiReader(data))
            {
                return reader.Load();
            }
        }

        /// <summary>
        /// Loads the visibility (VIS) data for a module.
        /// </summary>
        public async Task<VIS> LoadVisibilityAsync(string moduleResRef, CancellationToken ct = default(CancellationToken))
        {
            var id = new ResourceIdentifier(moduleResRef, ResourceType.VIS);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var reader = new VISAsciiReader(data))
            {
                return reader.Load();
            }
        }

        /// <summary>
        /// Loads a walkmesh (WOK/BWM) for a room.
        /// </summary>
        public async Task<NavigationMesh> LoadWalkmeshAsync(string roomModel, CancellationToken ct = default(CancellationToken))
        {
            var id = new ResourceIdentifier(roomModel, ResourceType.WOK);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var bwm = BWMBinaryReader.Read(stream);
                return BwmToNavigationMeshConverter.Convert(bwm);
            }
        }

        /// <summary>
        /// Loads a walkmesh with an offset (for positioning in world space).
        /// </summary>
        public async Task<NavigationMesh> LoadWalkmeshWithOffsetAsync(
            string roomModel,
            Vector3 offset,
            CancellationToken ct = default(CancellationToken))
        {
            var id = new ResourceIdentifier(roomModel, ResourceType.WOK);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var bwm = BWMBinaryReader.Read(stream);
                return BwmToNavigationMeshConverter.ConvertWithOffset(bwm, offset);
            }
        }

        /// <summary>
        /// Loads all room walkmeshes for a module and merges them.
        /// </summary>
        public async Task<NavigationMesh> LoadAreaNavigationAsync(LYT layout, CancellationToken ct = default(CancellationToken))
        {
            if (layout == null || layout.Rooms.Count == 0)
            {
                return new NavigationMesh(
                    new Vector3[0],
                    new int[0],
                    new int[0],
                    new int[0],
                    null);
            }

            var meshes = new List<NavigationMesh>();

            foreach (var room in layout.Rooms)
            {
                ct.ThrowIfCancellationRequested();

                Vector3 offset = new Vector3(room.Position.X, room.Position.Y, room.Position.Z);
                var mesh = await LoadWalkmeshWithOffsetAsync(room.Model, offset, ct);
                if (mesh != null)
                {
                    meshes.Add(mesh);
                }
            }

            return BwmToNavigationMeshConverter.Merge(meshes);
        }

        /// <summary>
        /// Loads a door walkmesh (DWK).
        /// </summary>
        public async Task<NavigationMesh> LoadDoorWalkmeshAsync(string doorTemplate, CancellationToken ct = default(CancellationToken))
        {
            var id = new ResourceIdentifier(doorTemplate, ResourceType.DWK);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var bwm = BWMBinaryReader.Read(stream);
                return BwmToNavigationMeshConverter.Convert(bwm);
            }
        }

        /// <summary>
        /// Loads a placeable walkmesh (PWK).
        /// </summary>
        public async Task<NavigationMesh> LoadPlaceableWalkmeshAsync(string placeableTemplate, CancellationToken ct = default(CancellationToken))
        {
            var id = new ResourceIdentifier(placeableTemplate, ResourceType.PWK);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var bwm = BWMBinaryReader.Read(stream);
                return BwmToNavigationMeshConverter.Convert(bwm);
            }
        }

    }
}
