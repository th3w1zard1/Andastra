using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Andastra.Parsing.Formats.MDLData;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Graphics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Graphics.Common.Backends.Odyssey
{
    /// <summary>
    /// Odyssey room mesh renderer implementation.
    /// Renders area/room geometry using OpenGL.
    /// </summary>
    /// <remarks>
    /// Odyssey Room Mesh Renderer:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Room rendering: Original engine renders WOK/LYT/VIS based area geometry
    /// - Uses OpenGL fixed-function pipeline for rendering
    /// - This implementation: OpenGL rendering for room meshes
    /// </remarks>
    public class OdysseyRoomMeshRenderer : IRoomMeshRenderer
    {
        private readonly OdysseyGraphicsDevice _device;
        private readonly Dictionary<string, OdysseyRoomMeshData> _meshCache;
        private bool _disposed;
        
        public OdysseyRoomMeshRenderer(OdysseyGraphicsDevice device)
        {
            _device = device;
            _meshCache = new Dictionary<string, OdysseyRoomMeshData>(StringComparer.OrdinalIgnoreCase);
        }
        
        public IRoomMeshData LoadRoomMesh(string modelResRef, MDL mdl)
        {
            if (string.IsNullOrEmpty(modelResRef) || mdl == null)
            {
                return null;
            }
            
            // Check cache first
            if (_meshCache.TryGetValue(modelResRef, out OdysseyRoomMeshData cached))
            {
                return cached;
            }
            
            // TODO: STUB - Load and convert MDL mesh data to OpenGL buffers
            // For now, return a placeholder mesh data
            var meshData = new OdysseyRoomMeshData();
            _meshCache[modelResRef] = meshData;
            
            return meshData;
        }
        
        public void Clear()
        {
            foreach (var mesh in _meshCache.Values)
            {
                mesh.Dispose();
            }
            _meshCache.Clear();
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                Clear();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Odyssey room mesh data implementation.
    /// </summary>
    public class OdysseyRoomMeshData : IRoomMeshData, IDisposable
    {
        private IVertexBuffer _vertexBuffer;
        private IIndexBuffer _indexBuffer;
        private int _indexCount;
        
        public IVertexBuffer VertexBuffer => _vertexBuffer;
        public IIndexBuffer IndexBuffer => _indexBuffer;
        public int IndexCount => _indexCount;
        
        public void Dispose()
        {
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }
    }
    
    /// <summary>
    /// Odyssey entity model renderer implementation.
    /// Renders character and object models using OpenGL.
    /// </summary>
    /// <remarks>
    /// Odyssey Entity Model Renderer:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Model rendering: Original engine renders MDL/MDX model files
    /// - Skeleton animation: Uses bone transforms for skeletal animation
    /// - This implementation: OpenGL rendering for entity models
    /// </remarks>
    public class OdysseyEntityModelRenderer : IEntityModelRenderer
    {
        private readonly OdysseyGraphicsDevice _device;
        private readonly object _gameDataManager;
        private readonly Installation _installation;
        private readonly Dictionary<string, EntityModelCache> _modelCache;
        
        // Vertex structure for entity rendering
        private struct VertexPositionNormalTexture
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector2 TexCoord;
        }
        
        // Cached model data
        private class EntityModelCache
        {
            public List<MeshRenderData> Meshes { get; set; }
            public string ModelName { get; set; }
        }
        
        // Mesh render data
        private class MeshRenderData
        {
            public IVertexBuffer VertexBuffer { get; set; }
            public IIndexBuffer IndexBuffer { get; set; }
            public int IndexCount { get; set; }
            public Matrix4x4 WorldTransform { get; set; }
            public string TextureName { get; set; }
        }
        
        public OdysseyEntityModelRenderer(OdysseyGraphicsDevice device, object gameDataManager = null, object installation = null)
        {
            _device = device;
            _gameDataManager = gameDataManager;
            _installation = installation as Installation;
            _modelCache = new Dictionary<string, EntityModelCache>(StringComparer.OrdinalIgnoreCase);
        }
        
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/renderer/model.py:103-141
        // Original: def paintGL(self): - entity rendering in OpenGL scene
        // Based on swkotor2.exe: FUN_005261b0 @ 0x005261b0 (entity model rendering)
        public void RenderEntity([NotNull] IEntity entity, Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (entity == null)
            {
                return;
            }
            
            // Check visibility
            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            if (renderable != null && !renderable.Visible)
            {
                return;
            }
            
            // Resolve model ResRef
            string modelResRef = ResolveEntityModel(entity);
            if (string.IsNullOrEmpty(modelResRef))
            {
                return;
            }
            
            // Get or load model
            EntityModelCache modelCache = GetOrLoadModel(modelResRef);
            if (modelCache == null || modelCache.Meshes == null || modelCache.Meshes.Count == 0)
            {
                return;
            }
            
            // Get entity transform
            ITransformComponent transform = entity.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }
            
            // Build world matrix from entity transform
            // Y-up system: facing is rotation around Y axis
            Matrix4x4 rotation = Matrix4x4.CreateRotationY(transform.Facing);
            Matrix4x4 translation = Matrix4x4.CreateTranslation(
                transform.Position.X,
                transform.Position.Y,
                transform.Position.Z
            );
            Matrix4x4 entityWorldMatrix = Matrix4x4.Multiply(rotation, translation);
            
            // Get opacity from renderable component
            float opacity = 1.0f;
            if (renderable != null)
            {
                opacity = renderable.Opacity;
            }
            
            // Render all meshes
            foreach (MeshRenderData mesh in modelCache.Meshes)
            {
                if (mesh.VertexBuffer == null || mesh.IndexBuffer == null)
                {
                    continue;
                }
                
                // Combine mesh transform with entity transform
                Matrix4x4 finalWorld = Matrix4x4.Multiply(mesh.WorldTransform, entityWorldMatrix);
                
                // Set vertex and index buffers
                _device.SetVertexBuffer(mesh.VertexBuffer);
                _device.SetIndexBuffer(mesh.IndexBuffer);
                
                // TODO: Apply view/projection matrices and opacity via OpenGL state
                // For now, basic rendering is set up - full shader/material system would go here
                // Based on swkotor2.exe: glDrawElements with proper matrix setup
                
                // Draw indexed primitives
                int primitiveCount = mesh.IndexCount / 3; // Triangles
                if (primitiveCount > 0)
                {
                    _device.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0, // baseVertex
                        0, // minVertexIndex
                        mesh.VertexBuffer.VertexCount, // numVertices
                        0, // startIndex
                        primitiveCount
                    );
                }
            }
        }
        
        // Resolves model ResRef from entity using ModelResolver (matching MonoGame/Stride pattern)
        [CanBeNull]
        private string ResolveEntityModel([NotNull] IEntity entity)
        {
            // Check renderable component first
            IRenderableComponent renderable = entity.GetComponent<IRenderableComponent>();
            if (renderable != null && !string.IsNullOrEmpty(renderable.ModelResRef))
            {
                return renderable.ModelResRef;
            }
            
            // Resolve model from appearance using reflection to avoid circular dependency
            // ModelResolver is in Andastra.Runtime.Engines.Odyssey.Systems namespace
            if (_gameDataManager != null)
            {
                try
                {
                    Type modelResolverType = Type.GetType("Andastra.Runtime.Engines.Odyssey.Systems.ModelResolver, Andastra.Runtime.Games.Odyssey");
                    if (modelResolverType != null)
                    {
                        MethodInfo resolveMethod = modelResolverType.GetMethod("ResolveEntityModel", BindingFlags.Public | BindingFlags.Static);
                        if (resolveMethod != null)
                        {
                            object result = resolveMethod.Invoke(null, new object[] { _gameDataManager, entity });
                            return result as string;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[OdysseyEntityModelRenderer] Error resolving model: " + ex.Message);
                }
            }
            
            return null;
        }
        
        // Gets or loads model from cache
        [CanBeNull]
        private EntityModelCache GetOrLoadModel([NotNull] string modelResRef)
        {
            // Check cache
            if (_modelCache.TryGetValue(modelResRef, out EntityModelCache cached))
            {
                return cached;
            }
            
            // Load MDL model
            MDL mdl = LoadMDLModel(modelResRef);
            if (mdl == null)
            {
                return null;
            }
            
            // Convert MDL to renderable meshes
            EntityModelCache modelCache = ConvertMDLToCache(mdl, modelResRef);
            if (modelCache != null && modelCache.Meshes.Count > 0)
            {
                _modelCache[modelResRef] = modelCache;
            }
            
            return modelCache;
        }
        
        // Loads MDL model from installation
        [CanBeNull]
        private MDL LoadMDLModel([NotNull] string modelResRef)
        {
            if (string.IsNullOrEmpty(modelResRef) || _installation == null)
            {
                return null;
            }
            
            try
            {
                ResourceResult result = _installation.Resources.LookupResource(modelResRef, ResourceType.MDL);
                if (result == null || result.Data == null)
                {
                    return null;
                }
                
                // Use Andastra.Parsing MDL parser
                return Andastra.Parsing.Formats.MDL.MDLAuto.ReadMdl(result.Data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[OdysseyEntityModelRenderer] Error loading MDL " + modelResRef + ": " + ex.Message);
                return null;
            }
        }
        
        // Converts MDL to cached renderable meshes (matching MonoGame/Stride converter pattern)
        [CanBeNull]
        private EntityModelCache ConvertMDLToCache([NotNull] MDL mdl, [NotNull] string modelResRef)
        {
            if (mdl == null)
            {
                return null;
            }
            
            var result = new EntityModelCache
            {
                ModelName = mdl.Name ?? modelResRef,
                Meshes = new List<MeshRenderData>()
            };
            
            // Traverse node hierarchy and convert meshes
            if (mdl.Root != null)
            {
                ConvertNodeHierarchy(mdl.Root, Matrix4x4.Identity, result.Meshes);
            }
            
            Console.WriteLine("[OdysseyEntityModelRenderer] Converted model: " + result.ModelName +
                " with " + result.Meshes.Count + " mesh parts");
            
            return result;
        }
        
        // Converts MDL node hierarchy to renderable meshes
        private void ConvertNodeHierarchy([NotNull] MDLNode node, Matrix4x4 parentTransform, [NotNull] List<MeshRenderData> meshes)
        {
            if (node == null)
            {
                return;
            }
            
            // Calculate local transform
            Matrix4x4 localTransform = CalculateNodeTransform(node);
            Matrix4x4 worldTransform = Matrix4x4.Multiply(localTransform, parentTransform);
            
            // Convert mesh if present
            if (node.Mesh != null)
            {
                MeshRenderData meshData = ConvertMesh(node.Mesh, worldTransform);
                if (meshData != null)
                {
                    meshes.Add(meshData);
                }
            }
            
            // Process children
            if (node.Children != null)
            {
                foreach (MDLNode child in node.Children)
                {
                    ConvertNodeHierarchy(child, worldTransform, meshes);
                }
            }
        }
        
        // Calculates transform matrix for MDL node
        private Matrix4x4 CalculateNodeTransform([NotNull] MDLNode node)
        {
            // Create rotation from quaternion
            System.Numerics.Quaternion rotation;
            if (node.Orientation != null && 
                (node.Orientation.X != 0 || node.Orientation.Y != 0 || node.Orientation.Z != 0 || node.Orientation.W != 0))
            {
                rotation = new System.Numerics.Quaternion(
                    node.Orientation.X,
                    node.Orientation.Y,
                    node.Orientation.Z,
                    node.Orientation.W
                );
            }
            else
            {
                rotation = System.Numerics.Quaternion.Identity;
            }
            
            // Create translation
            Vector3 translation = Vector3.Zero;
            if (node.Position != null)
            {
                translation = new Vector3(
                    node.Position.X,
                    node.Position.Y,
                    node.Position.Z
                );
            }
            
            // Create rotation matrix from quaternion
            Matrix4x4 rotationMatrix = Matrix4x4.CreateFromQuaternion(rotation);
            Matrix4x4 translationMatrix = Matrix4x4.CreateTranslation(translation);
            
            // Combine: Translation * Rotation (MDL uses this order)
            return Matrix4x4.Multiply(translationMatrix, rotationMatrix);
        }
        
        // Converts MDL mesh to renderable mesh data
        [CanBeNull]
        private MeshRenderData ConvertMesh([NotNull] MDLMesh mesh, Matrix4x4 worldTransform)
        {
            if (mesh.Vertices == null || mesh.Vertices.Count == 0)
            {
                return null;
            }
            
            if (mesh.Faces == null || mesh.Faces.Count == 0)
            {
                return null;
            }
            
            // Build vertex array
            var vertices = new VertexPositionNormalTexture[mesh.Vertices.Count];
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                Vector3 pos = new Vector3(mesh.Vertices[i].X, mesh.Vertices[i].Y, mesh.Vertices[i].Z);
                Vector3 normal = Vector3.UnitY; // Default to up if no normal
                Vector2 texCoord = Vector2.Zero;
                
                if (mesh.Normals != null && i < mesh.Normals.Count)
                {
                    normal = new Vector3(mesh.Normals[i].X, mesh.Normals[i].Y, mesh.Normals[i].Z);
                }
                
                if (mesh.UV1 != null && i < mesh.UV1.Count)
                {
                    texCoord = new Vector2(mesh.UV1[i].X, mesh.UV1[i].Y);
                }
                
                vertices[i] = new VertexPositionNormalTexture
                {
                    Position = pos,
                    Normal = normal,
                    TexCoord = texCoord
                };
            }
            
            // Build index array
            var indices = new int[mesh.Faces.Count * 3];
            for (int i = 0; i < mesh.Faces.Count; i++)
            {
                indices[i * 3 + 0] = mesh.Faces[i].V1;
                indices[i * 3 + 1] = mesh.Faces[i].V2;
                indices[i * 3 + 2] = mesh.Faces[i].V3;
            }
            
            // Create GPU buffers
            IVertexBuffer vertexBuffer = _device.CreateVertexBuffer(vertices);
            IIndexBuffer indexBuffer = _device.CreateIndexBuffer(indices, true); // Use 16-bit indices
            
            // Get texture name
            string textureName = null;
            if (!string.IsNullOrEmpty(mesh.Texture1) &&
                mesh.Texture1.ToLowerInvariant() != "null" &&
                mesh.Texture1.ToLowerInvariant() != "none")
            {
                textureName = mesh.Texture1.ToLowerInvariant();
            }
            
            return new MeshRenderData
            {
                VertexBuffer = vertexBuffer,
                IndexBuffer = indexBuffer,
                IndexCount = indices.Length,
                WorldTransform = worldTransform,
                TextureName = textureName
            };
        }
        
        // Clears the model cache
        public void ClearCache()
        {
            foreach (EntityModelCache cache in _modelCache.Values)
            {
                if (cache != null && cache.Meshes != null)
                {
                    foreach (MeshRenderData mesh in cache.Meshes)
                    {
                        mesh.VertexBuffer?.Dispose();
                        mesh.IndexBuffer?.Dispose();
                    }
                }
            }
            _modelCache.Clear();
        }
    }
    
    /// <summary>
    /// Odyssey spatial audio implementation.
    /// Uses OpenAL or DirectSound for 3D positional audio.
    /// </summary>
    /// <remarks>
    /// Odyssey Spatial Audio:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Audio system: Original engine uses Miles Sound System (MSS)
    /// - 3D audio: Positional audio with distance attenuation
    /// - This implementation: Placeholder for spatial audio
    /// </remarks>
    public class OdysseySpatialAudio : ISpatialAudio, IDisposable
    {
        private Vector3 _listenerPosition;
        private Vector3 _listenerForward;
        private Vector3 _listenerUp;
        private Vector3 _listenerVelocity;
        private float _dopplerFactor = 1.0f;
        private float _speedOfSound = 343.3f; // m/s at room temperature
        private Dictionary<uint, EmitterData> _emitters;
        private uint _nextEmitterId = 1;
        
        private struct EmitterData
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Volume;
            public float MinDistance;
            public float MaxDistance;
        }
        
        public OdysseySpatialAudio()
        {
            _listenerPosition = Vector3.Zero;
            _listenerForward = new Vector3(0, 0, -1);
            _listenerUp = new Vector3(0, 1, 0);
            _listenerVelocity = Vector3.Zero;
            _emitters = new Dictionary<uint, EmitterData>();
        }
        
        public float DopplerFactor
        {
            get { return _dopplerFactor; }
            set { _dopplerFactor = value; }
        }
        
        public float SpeedOfSound
        {
            get { return _speedOfSound; }
            set { _speedOfSound = value; }
        }
        
        public void SetListener(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity)
        {
            _listenerPosition = position;
            _listenerForward = forward;
            _listenerUp = up;
            _listenerVelocity = velocity;
        }
        
        public uint CreateEmitter(Vector3 position, Vector3 velocity, float volume, float minDistance, float maxDistance)
        {
            uint id = _nextEmitterId++;
            _emitters[id] = new EmitterData
            {
                Position = position,
                Velocity = velocity,
                Volume = volume,
                MinDistance = minDistance,
                MaxDistance = maxDistance
            };
            return id;
        }
        
        public void UpdateEmitter(uint emitterId, Vector3 position, Vector3 velocity, float volume, float minDistance, float maxDistance)
        {
            if (_emitters.ContainsKey(emitterId))
            {
                _emitters[emitterId] = new EmitterData
                {
                    Position = position,
                    Velocity = velocity,
                    Volume = volume,
                    MinDistance = minDistance,
                    MaxDistance = maxDistance
                };
            }
        }
        
        public Audio3DParameters Calculate3DParameters(uint emitterId)
        {
            var result = new Audio3DParameters();
            
            if (!_emitters.TryGetValue(emitterId, out EmitterData emitter))
            {
                return result;
            }
            
            // Calculate distance
            Vector3 direction = emitter.Position - _listenerPosition;
            float distance = direction.Length();
            result.Distance = distance;
            
            // Calculate volume based on distance attenuation
            if (distance <= emitter.MinDistance)
            {
                result.Volume = emitter.Volume;
            }
            else if (distance >= emitter.MaxDistance)
            {
                result.Volume = 0.0f;
            }
            else
            {
                // Linear attenuation
                float t = (distance - emitter.MinDistance) / (emitter.MaxDistance - emitter.MinDistance);
                result.Volume = emitter.Volume * (1.0f - t);
            }
            
            // Calculate pan (simplified left/right panning)
            if (distance > 0.001f)
            {
                Vector3 normalizedDirection = Vector3.Normalize(direction);
                Vector3 right = Vector3.Cross(_listenerForward, _listenerUp);
                result.Pan = Vector3.Dot(normalizedDirection, right);
            }
            else
            {
                result.Pan = 0.0f;
            }
            
            // Calculate Doppler shift
            result.DopplerShift = 1.0f;
            if (_dopplerFactor > 0.0f && _speedOfSound > 0.0f && distance > 0.001f)
            {
                Vector3 normalizedDirection = Vector3.Normalize(direction);
                float listenerSpeed = Vector3.Dot(_listenerVelocity, normalizedDirection);
                float emitterSpeed = Vector3.Dot(emitter.Velocity, normalizedDirection);
                
                float denom = _speedOfSound + (emitterSpeed * _dopplerFactor);
                if (Math.Abs(denom) > 0.001f)
                {
                    result.DopplerShift = (_speedOfSound - (listenerSpeed * _dopplerFactor)) / denom;
                }
            }
            
            return result;
        }
        
        public void RemoveEmitter(uint emitterId)
        {
            _emitters.Remove(emitterId);
        }
        
        public void Dispose()
        {
            _emitters.Clear();
        }
    }
    
    /// <summary>
    /// Odyssey sound player implementation.
    /// Plays sound effects using the original engine's audio system pattern.
    /// </summary>
    public class OdysseySoundPlayer
    {
        private readonly object _resourceProvider;
        
        public OdysseySoundPlayer(object resourceProvider)
        {
            _resourceProvider = resourceProvider;
        }
        
        public void Play(string soundResRef)
        {
            // TODO: STUB - Play sound by ResRef
        }
        
        public void Play(string soundResRef, Vector3 position)
        {
            // TODO: STUB - Play 3D positioned sound
        }
        
        public void Stop()
        {
            // TODO: STUB - Stop all sounds
        }
        
        public void SetVolume(float volume)
        {
            // TODO: STUB - Set sound volume
        }
    }
    
    /// <summary>
    /// Odyssey music player implementation.
    /// Plays background music using streaming audio.
    /// </summary>
    public class OdysseyMusicPlayer
    {
        private readonly object _resourceProvider;
        private string _currentTrack;
        private bool _isPlaying;
        
        public OdysseyMusicPlayer(object resourceProvider)
        {
            _resourceProvider = resourceProvider;
        }
        
        public void Play(string musicResRef)
        {
            _currentTrack = musicResRef;
            _isPlaying = true;
            // TODO: STUB - Play background music
        }
        
        public void Stop()
        {
            _isPlaying = false;
            // TODO: STUB - Stop music
        }
        
        public void Pause()
        {
            // TODO: STUB - Pause music
        }
        
        public void Resume()
        {
            // TODO: STUB - Resume music
        }
        
        public void SetVolume(float volume)
        {
            // TODO: STUB - Set music volume
        }
        
        public bool IsPlaying => _isPlaying;
        public string CurrentTrack => _currentTrack;
    }
    
    /// <summary>
    /// Odyssey voice player implementation.
    /// Plays voice-over dialogue audio.
    /// </summary>
    public class OdysseyVoicePlayer
    {
        private readonly object _resourceProvider;
        private bool _isPlaying;
        
        public OdysseyVoicePlayer(object resourceProvider)
        {
            _resourceProvider = resourceProvider;
        }
        
        public void Play(string voiceResRef)
        {
            _isPlaying = true;
            // TODO: STUB - Play voice audio
        }
        
        public void Stop()
        {
            _isPlaying = false;
            // TODO: STUB - Stop voice
        }
        
        public void SetVolume(float volume)
        {
            // TODO: STUB - Set voice volume
        }
        
        public bool IsPlaying => _isPlaying;
    }
}
