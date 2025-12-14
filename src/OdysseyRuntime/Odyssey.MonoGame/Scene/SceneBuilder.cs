using System;
using System.Collections.Generic;
using CSharpKOTOR.Formats.LYT;
using CSharpKOTOR.Formats.VIS;
using CSharpKOTOR.Formats.MDLData;
using Odyssey.Content.Interfaces;
using Odyssey.Core.Interfaces;
using Odyssey.MonoGame.Converters;
using StrideEngine = Stride.Engine;
using Stride.Rendering;
using Stride.Graphics;
using JetBrains.Annotations;
using StrideMath = Stride.Core.Mathematics;

namespace Odyssey.MonoGame.Scene
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
        // Initialize scene builder with graphics device and resource provider
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.GraphicsDevice.html
        // GraphicsDevice provides access to graphics hardware for creating resources
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.Texture.html
        // Texture represents image data, cached for reuse
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
        // Entity(string) constructor creates root entity for the scene hierarchy
        // Source: https://doc.stride3d.net/latest/en/manual/entities/index.html
        public SceneBuilder([NotNull] GraphicsDevice device, [NotNull] IGameResourceProvider resourceProvider)
        {
            _device = device ?? throw new ArgumentNullException("device");
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");

            _modelConverter = new MdlToStrideModelConverter(device);
            _textureCache = new Dictionary<string, Texture>(StringComparer.OrdinalIgnoreCase);
            _modelCache = new Dictionary<string, MdlToStrideModelConverter.ConversionResult>(StringComparer.OrdinalIgnoreCase);
            _roomEntities = new Dictionary<string, StrideEngine.Entity>(StringComparer.OrdinalIgnoreCase);

            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
            // Entity(string) constructor creates root entity for scene hierarchy
            // All scene entities will be children of this root entity
            // Source: https://doc.stride3d.net/latest/en/manual/entities/index.html
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

            // Create entity for visual representation
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
            // Entity(string) constructor creates a new entity with the specified name
            // Source: https://doc.stride3d.net/latest/en/manual/entities/index.html
            var strideEntity = new StrideEngine.Entity(entity.Tag ?? modelResRef);

            // Get position from transform component
            var transform = entity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
                // Transform.Position sets the world position of the entity
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
                // Vector3(float x, float y, float z) constructor creates position vector
                // Source: https://doc.stride3d.net/latest/en/manual/entities/transforms/index.html
                strideEntity.Transform.Position = new StrideMath.Vector3(
                    transform.Position.X,
                    transform.Position.Y,
                    transform.Position.Z);

                // Convert facing (radians around Y) to quaternion
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Quaternion.html
                // Quaternion.RotationY(float) creates a quaternion representing rotation around Y axis
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
                // Transform.Rotation sets the entity's rotation
                // Source: https://doc.stride3d.net/latest/en/manual/entities/transforms/index.html
                float facing = transform.Facing;
                strideEntity.Transform.Rotation = global::Stride.Core.Mathematics.Quaternion.RotationY(facing);
            }

            // Add mesh components
            AddMeshesToEntity(strideEntity, modelData);

            // Add to scene root entity hierarchy
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Children collection contains child transforms
            // Add(TransformComponent) adds entity as child of root, creating scene hierarchy
            // Source: https://doc.stride3d.net/latest/en/manual/entities/transforms/index.html
            _rootEntity.Transform.Children.Add(strideEntity.Transform);

            return strideEntity;
        }

        /// <summary>
        /// Updates entity visual position to match runtime entity.
        /// </summary>
        // Synchronize Stride entity transform with runtime entity transform
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
        // Transform.Position and Transform.Rotation properties update entity world transform
        // Source: https://doc.stride3d.net/latest/en/manual/entities/transforms/index.html
        public void SyncEntityPosition(StrideEngine.Entity strideEntity, IEntity runtimeEntity)
        {
            if (strideEntity == null || runtimeEntity == null)
            {
                return;
            }

            var transform = runtimeEntity.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
                // Transform.Position property sets the world position
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
                // Vector3 constructor creates position from runtime entity coordinates
                strideEntity.Transform.Position = new StrideMath.Vector3(
                    transform.Position.X,
                    transform.Position.Y,
                    transform.Position.Z);

                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Quaternion.html
                // Quaternion.RotationY(float) creates rotation quaternion from facing angle
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
                // Transform.Rotation property sets the entity's rotation
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
            // Entity(string) - Creates a new entity with the specified name
            var roomEntity = new StrideEngine.Entity(modelName);

            // Position from LYT
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Position - Gets or sets the world position of the entity
            // Vector3 - 3D position (X, Y, Z coordinates)
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Position - Sets the local position relative to parent entity
            meshEntity.Transform.Position = new StrideMath.Vector3(
                meshData.Position.X,
                meshData.Position.Y,
                meshData.Position.Z);

            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Rotation - Sets the local rotation as a quaternion
            // Quaternion(X, Y, Z, W) - Represents rotation (X, Y, Z are imaginary parts, W is real part)
            meshEntity.Transform.Rotation = new global::Stride.Core.Mathematics.Quaternion(
                meshData.Orientation.X,
                meshData.Orientation.Y,
                meshData.Orientation.Z,
                meshData.Orientation.W);

            // Create mesh component
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Mesh.html
            // Mesh - Represents a single mesh with draw data
            // Draw property contains MeshDraw with vertex/index buffers and topology
            var mesh = new global::Stride.Rendering.Mesh
            {
                Draw = meshData.MeshDraw
            };

            // Create model component
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Model.html
            // Model() constructor creates a new model container
            // Model - Container for one or more meshes
            // Method signature: Model()
            // Add(Mesh) - Adds a mesh to the model
            // Method signature: void Add(Mesh mesh)
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/models/index.html
            var model = new Model();
            model.Add(mesh);

            // Create ModelComponent to attach model to entity
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.ModelComponent.html
            // ModelComponent - Component that renders a Model on an Entity
            // Model property - The model to render
            var modelComponent = new StrideEngine.ModelComponent
            {
                Model = model
            };

            // Add ModelComponent to entity
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
            // Entity.Add(EntityComponent) - Adds a component to the entity
            meshEntity.Add(modelComponent);

            // Add to parent entity hierarchy
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Children - Collection of child transforms (creates parent-child relationship)
            // Add(TransformComponent) - Adds a child transform, making the entity a child of the parent
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
            // Entity(string) constructor creates a marker entity for door hook position
            // Source: https://doc.stride3d.net/latest/en/manual/entities/index.html
            var marker = new StrideEngine.Entity("doorhook_" + doorHook.Room + "_" + doorHook.Door);
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Position sets the world position of the door hook marker
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
            // Vector3 constructor creates position from LYT door hook coordinates
            // Source: https://doc.stride3d.net/latest/en/manual/entities/transforms/index.html
            marker.Transform.Position = new StrideMath.Vector3(
                doorHook.Position.X,
                doorHook.Position.Y,
                doorHook.Position.Z);

            // Convert quaternion rotation from LYT to Stride
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Quaternion.html
            // Quaternion(float x, float y, float z, float w) constructor creates rotation from LYT orientation
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Rotation sets the entity's rotation
            // Source: https://doc.stride3d.net/latest/en/manual/entities/transforms/index.html
            marker.Transform.Rotation = new global::Stride.Core.Mathematics.Quaternion(
                doorHook.Orientation.X,
                doorHook.Orientation.Y,
                doorHook.Orientation.Z,
                doorHook.Orientation.W);

            // Add marker to scene root hierarchy
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Children.Add(TransformComponent) adds marker as child of root entity
            // Source: https://doc.stride3d.net/latest/en/manual/entities/transforms/index.html
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
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
            // Entity.Get<T>() - Gets a component of type T from the entity
            // Returns null if component doesn't exist
            var modelComponent = entity.Get<StrideEngine.ModelComponent>();
            if (modelComponent != null)
            {
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.EntityComponent.html
                // EntityComponent.Enabled - Gets or sets whether the component is enabled
                // When disabled, the component is not processed during update/render
                modelComponent.Enabled = enabled;
            }

            // Recursively enable/disable child entities
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // Transform.Children - Collection of child transforms
            // Transform.Entity - Gets the entity that owns this transform
            foreach (var childTransform in entity.Transform.Children)
            {
                SetEntityEnabled(childTransform.Entity, enabled);
            }
        }

        /// <summary>
        /// Gets the room name at a given position based on room bounds.
        /// </summary>
        // Get room name at given world position using distance check
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
        // Transform.Position property gets the world position of room entities
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
        // Vector3.Distance(Vector3, Vector3) calculates distance between two 3D points
        // Method signature: static float Distance(Vector3 value1, Vector3 value2)
        // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
        public string GetRoomAtPosition(StrideMath.Vector3 position)
        {
            foreach (var kvp in _roomEntities)
            {
                var roomEntity = kvp.Value;
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
                // Transform.Position gets the world position of the room entity
                var roomPos = roomEntity.Transform.Position;

                // Simple distance check - could be improved with actual room bounds
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
                // Vector3.Distance calculates 2D distance (ignoring Z) for room detection
                // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
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

