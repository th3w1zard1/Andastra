using System;
using System.Collections.Generic;
using CSharpKOTOR.Formats.LYT;
using CSharpKOTOR.Formats.VIS;
using CSharpKOTOR.Formats.MDLData;
using Odyssey.Content.Interfaces;
using Odyssey.Core.Interfaces;
using Odyssey.Stride.Converters;
using StrideEngine = Stride.Engine;
using Stride.Rendering;
using Stride.Graphics;
using JetBrains.Annotations;
using StrideMath = Stride.Core.Mathematics;

namespace Odyssey.Stride.Scene
{
    /// <summary>
    /// Builds Stride scene graph from KOTOR module data.
    /// Handles room instantiation, entity visuals, and VIS-based culling.
    /// </summary>
    public class SceneBuilder
    {
        private readonly GraphicsDevice _device;
        private readonly IGameResourceProvider _resourceProvider;
        private readonly MdlToStrideModelConverter _modelConverter;
        private readonly Dictionary<string, Texture> _textureCache;
        private readonly Dictionary<string, MdlToStrideModelConverter.ConversionResult> _modelCache;
        private readonly Dictionary<string, StrideEngine.Entity> _roomEntities;
        private readonly StrideEngine.Entity _rootEntity;

        private VIS _visibility;
        private string _currentRoom;

        /// <summary>
        /// Gets the root entity containing all scene objects.
        /// </summary>
        public StrideEngine.Entity RootEntity
        {
            get { return _rootEntity; }
        }

        /// <summary>
        /// Gets or sets the current room for visibility culling.
        /// </summary>
        public string CurrentRoom
        {
            get { return _currentRoom; }
            set
            {
                if (_currentRoom != value)
                {
                    _currentRoom = value;
                    UpdateVisibility();
                }
            }
        }

        /// <summary>
        /// Creates a new scene builder.
        /// </summary>
        /// <param name="device">Graphics device.</param>
        /// <param name="resourceProvider">Resource provider for loading assets.</param>
        public SceneBuilder([NotNull] GraphicsDevice device, [NotNull] IGameResourceProvider resourceProvider)
        {
            _device = device ?? throw new ArgumentNullException("device");
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");

            _modelConverter = new MdlToStrideModelConverter(device);
            _textureCache = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
            _modelCache = new Dictionary<string, MdlToStrideModelConverter.ConversionResult>(StringComparer.OrdinalIgnoreCase);
            _roomEntities = new Dictionary<string, StrideEngine.Entity>(StringComparer.OrdinalIgnoreCase);

            _rootEntity = new StrideEngine.Entity("AreaRoot");
        }

        /// <summary>
        /// Builds the scene from module layout and visibility data.
        /// </summary>
        /// <param name="layout">Area layout (LYT).</param>
        /// <param name="visibility">Room visibility (VIS).</param>
        /// <param name="loadModel">Function to load MDL model by resref.</param>
        /// <param name="loadTexture">Function to load TPC texture by resref.</param>
        public void BuildScene(
            [NotNull] LYT layout,
            VIS visibility,
            Func<string, MDL> loadModel,
            Func<string, CSharpKOTOR.Formats.TPC.TPC> loadTexture)
        {
            if (layout == null)
            {
                throw new ArgumentNullException("layout");
            }

            _visibility = visibility;
            ClearScene();

            // Build rooms from layout
            foreach (var room in layout.Rooms)
            {
                BuildRoom(room, loadModel, loadTexture);
            }

            // Build door hooks from layout
            foreach (var doorHook in layout.Doorhooks)
            {
                // Door hooks are placeholders for door positions
                // Actual door entities are spawned from GIT
                CreateDoorHookMarker(doorHook);
            }

            // Set initial visibility (all visible until we know player position)
            if (visibility == null)
            {
                SetAllRoomsVisible(true);
            }
        }

        /// <summary>
        /// Clears all scene objects.
        /// </summary>
        public void ClearScene()
        {
            foreach (var room in _roomEntities.Values)
            {
                _rootEntity.Transform.Children.Remove(room.Transform);
            }
            _roomEntities.Clear();
            _textureCache.Clear();
            _modelCache.Clear();
            _currentRoom = null;
        }

        /// <summary>
        /// Creates a Stride entity for a runtime entity.
        /// </summary>
        /// <param name="entity">Runtime entity.</param>
        /// <param name="modelResRef">Model resref to load.</param>
        /// <param name="loadModel">Model loader function.</param>
        /// <param name="loadTexture">Texture loader function.</param>
        /// <returns>Stride entity with visual representation.</returns>
        public StrideEngine.Entity CreateEntityVisual(
            IEntity entity,
            string modelResRef,
            Func<string, MDL> loadModel,
            Func<string, CSharpKOTOR.Formats.TPC.TPC> loadTexture)
        {
            if (entity == null || string.IsNullOrEmpty(modelResRef))
            {
                return null;
            }

            // Load model
            var modelData = LoadModel(modelResRef, loadModel, loadTexture);
            if (modelData == null || modelData.Meshes.Count == 0)
            {
                return null;
            }

            // Create entity
            var strideEntity = new StrideEngine.Entity(entity.Tag ?? modelResRef);

            // Get position from transform component
            var transform = entity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                strideEntity.Transform.Position = new StrideMath.Vector3(
                    transform.Position.X,
                    transform.Position.Y,
                    transform.Position.Z);

                // Convert facing (radians around Y) to quaternion
                float facing = transform.Facing;
                strideEntity.Transform.Rotation = global::Stride.Core.Mathematics.Quaternion.RotationY(facing);
            }

            // Add mesh components
            AddMeshesToEntity(strideEntity, modelData);

            // Add to scene
            _rootEntity.Transform.Children.Add(strideEntity.Transform);

            return strideEntity;
        }

        /// <summary>
        /// Updates entity visual position to match runtime entity.
        /// </summary>
        public void SyncEntityPosition(StrideEngine.Entity strideEntity, IEntity runtimeEntity)
        {
            if (strideEntity == null || runtimeEntity == null)
            {
                return;
            }

            var transform = runtimeEntity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                strideEntity.Transform.Position = new StrideMath.Vector3(
                    transform.Position.X,
                    transform.Position.Y,
                    transform.Position.Z);

                strideEntity.Transform.Rotation = global::Stride.Core.Mathematics.Quaternion.RotationY(transform.Facing);
            }
        }

        private void BuildRoom(LYTRoom room, Func<string, MDL> loadModel, Func<string, CSharpKOTOR.Formats.TPC.TPC> loadTexture)
        {
            string modelName = room.Model.ToLowerInvariant();

            // Load and convert model
            var modelData = LoadModel(modelName, loadModel, loadTexture);
            if (modelData == null)
            {
                Console.WriteLine("[SceneBuilder] Failed to load room model: " + modelName);
                return;
            }

            // Create room entity
            var roomEntity = new StrideEngine.Entity(modelName);

            // Position from LYT
            roomEntity.Transform.Position = new StrideMath.Vector3(
                room.Position.X,
                room.Position.Y,
                room.Position.Z);

            // Add mesh components
            AddMeshesToEntity(roomEntity, modelData);

            // Store for visibility management
            _roomEntities[modelName] = roomEntity;

            // Add to scene root
            _rootEntity.Transform.Children.Add(roomEntity.Transform);
        }

        private void AddMeshesToEntity(StrideEngine.Entity entity, MdlToStrideModelConverter.ConversionResult modelData)
        {
            foreach (var meshData in modelData.Meshes)
            {
                AddMeshToEntity(entity, meshData);
            }
        }

        private void AddMeshToEntity(StrideEngine.Entity parentEntity, MdlToStrideModelConverter.MeshData meshData)
        {
            if (!meshData.Render || meshData.MeshDraw == null)
            {
                return;
            }

            // Create child entity for this mesh
            var meshEntity = new StrideEngine.Entity(meshData.Name ?? "mesh");

            // Local transform
            meshEntity.Transform.Position = new StrideMath.Vector3(
                meshData.Position.X,
                meshData.Position.Y,
                meshData.Position.Z);

            meshEntity.Transform.Rotation = new global::Stride.Core.Mathematics.Quaternion(
                meshData.Orientation.X,
                meshData.Orientation.Y,
                meshData.Orientation.Z,
                meshData.Orientation.W);

            // Create mesh component
            var mesh = new global::Stride.Rendering.Mesh
            {
                Draw = meshData.MeshDraw
            };

            // Create model component
            var model = new Model();
            model.Add(mesh);

            var modelComponent = new StrideEngine.ModelComponent
            {
                Model = model
            };

            meshEntity.Add(modelComponent);

            // Add to parent
            parentEntity.Transform.Children.Add(meshEntity.Transform);

            // Process children
            foreach (var child in meshData.Children)
            {
                AddMeshToEntity(meshEntity, child);
            }
        }

        private void CreateDoorHookMarker(LYTDoorHook doorHook)
        {
            // Create invisible marker for door position
            // Actual door model is added when door entity is created from GIT
            var marker = new StrideEngine.Entity("doorhook_" + doorHook.Room + "_" + doorHook.Door);
            marker.Transform.Position = new StrideMath.Vector3(
                doorHook.Position.X,
                doorHook.Position.Y,
                doorHook.Position.Z);

            // Convert quaternion rotation
            marker.Transform.Rotation = new global::Stride.Core.Mathematics.Quaternion(
                doorHook.Orientation.X,
                doorHook.Orientation.Y,
                doorHook.Orientation.Z,
                doorHook.Orientation.W);

            _rootEntity.Transform.Children.Add(marker.Transform);
        }

        private MdlToStrideModelConverter.ConversionResult LoadModel(
            string modelName,
            Func<string, MDL> loadModel,
            Func<string, CSharpKOTOR.Formats.TPC.TPC> loadTexture)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                return null;
            }

            string key = modelName.ToLowerInvariant();

            // Check cache
            if (_modelCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            // Load MDL
            MDL mdl;
            try
            {
                mdl = loadModel(key);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SceneBuilder] Error loading model " + key + ": " + ex.Message);
                return null;
            }

            if (mdl == null)
            {
                return null;
            }

            // Convert to Stride
            var result = _modelConverter.Convert(mdl);

            // Pre-load textures
            foreach (var texName in result.TextureReferences)
            {
                LoadTexture(texName, loadTexture);
            }

            // Cache result
            _modelCache[key] = result;

            return result;
        }

        private Texture LoadTexture(string textureName, Func<string, CSharpKOTOR.Formats.TPC.TPC> loadTexture)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return null;
            }

            string key = textureName.ToLowerInvariant();

            // Check cache
            if (_textureCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            // Load TPC
            CSharpKOTOR.Formats.TPC.TPC tpc;
            try
            {
                tpc = loadTexture(key);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SceneBuilder] Error loading texture " + key + ": " + ex.Message);
                return null;
            }

            if (tpc == null)
            {
                return null;
            }

            // Convert to Stride
            Texture strideTexture;
            try
            {
                strideTexture = TpcToStrideTextureConverter.Convert(tpc, _device);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SceneBuilder] Error converting texture " + key + ": " + ex.Message);
                return null;
            }

            // Cache result
            _textureCache[key] = strideTexture;

            return strideTexture;
        }

        private void UpdateVisibility()
        {
            if (_visibility == null || string.IsNullOrEmpty(_currentRoom))
            {
                SetAllRoomsVisible(true);
                return;
            }

            // Get visible rooms from current room
            HashSet<string> visibleRooms = new HashSet<string>();
            foreach (var kvp in _visibility.GetEnumerator())
            {
                if (kvp.Item1.Equals(_currentRoom, StringComparison.OrdinalIgnoreCase))
                {
                    visibleRooms = kvp.Item2;
                    break;
                }
            }

            if (visibleRooms.Count == 0)
            {
                // No visibility data - show all
                SetAllRoomsVisible(true);
                return;
            }

            // Update room visibility
            foreach (var kvp in _roomEntities)
            {
                string roomName = kvp.Key;
                var entity = kvp.Value;

                // Room is visible if it's in the visible set or is the current room
                bool isVisible = roomName.Equals(_currentRoom, StringComparison.OrdinalIgnoreCase) ||
                                 visibleRooms.Contains(roomName.ToLowerInvariant());

                SetEntityEnabled(entity, isVisible);
            }
        }

        private void SetAllRoomsVisible(bool visible)
        {
            foreach (var entity in _roomEntities.Values)
            {
                SetEntityEnabled(entity, visible);
            }
        }

        private void SetEntityEnabled(StrideEngine.Entity entity, bool enabled)
        {
            // Enable/disable all model components in this entity and children
            var modelComponent = entity.Get<StrideEngine.ModelComponent>();
            if (modelComponent != null)
            {
                modelComponent.Enabled = enabled;
            }

            foreach (var childTransform in entity.Transform.Children)
            {
                SetEntityEnabled(childTransform.Entity, enabled);
            }
        }

        /// <summary>
        /// Gets the room name at a given position based on room bounds.
        /// </summary>
        public string GetRoomAtPosition(StrideMath.Vector3 position)
        {
            foreach (var kvp in _roomEntities)
            {
                var roomEntity = kvp.Value;
                var roomPos = roomEntity.Transform.Position;

                // Simple distance check - could be improved with actual room bounds
                float dist = StrideMath.Vector3.Distance(
                    new StrideMath.Vector3(position.X, position.Y, 0),
                    new StrideMath.Vector3(roomPos.X, roomPos.Y, 0));

                if (dist < 50f) // Rough check
                {
                    return kvp.Key;
                }
            }

            return _currentRoom;
        }
    }
}

