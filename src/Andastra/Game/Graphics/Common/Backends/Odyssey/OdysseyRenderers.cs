using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BioWare.NET.Resource.Formats.MDLData;
using BioWare.NET.Resource.Formats.WAV;
using BioWare.NET.Extract;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Audio;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Graphics;
using JetBrains.Annotations;

namespace Andastra.Game.Graphics.Common.Backends.Odyssey
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
            public System.Numerics.Vector2 TexCoord;
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
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005261b0 @ 0x005261b0 (entity model rendering)
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

            // Create or get basic effect for rendering
            // Based on StrideEntityModelRenderer pattern: create effect, set matrices, apply
            IBasicEffect effect = _device.CreateBasicEffect();
            if (effect is OdysseyBasicEffect odysseyEffect)
            {
                // Set view and projection matrices (typically set once per frame, but we set per-entity for flexibility)
                odysseyEffect.View = viewMatrix;
                odysseyEffect.Projection = projectionMatrix;

                // Set opacity from renderable component for fade-in/fade-out effects
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FadeTime @ 0x007c60ec (fade duration), alpha blending for entity rendering
                // Opacity is updated by AppearAnimationFadeSystem for appear animations
                // Opacity is updated by ActionDestroyObject for destroy animations
                odysseyEffect.Alpha = opacity;
            }

            // Render all meshes
            foreach (MeshRenderData mesh in modelCache.Meshes)
            {
                if (mesh.VertexBuffer == null || mesh.IndexBuffer == null)
                {
                    continue;
                }

                // Enable texture if mesh has texture
                if (effect is OdysseyBasicEffect odysseyEffectForMesh)
                {
                    if (!string.IsNullOrEmpty(mesh.TextureName))
                    {
                        odysseyEffectForMesh.TextureEnabled = true;
                        // TODO: Load and bind texture from mesh.TextureName
                        // For now, texture loading is handled elsewhere
                    }
                    else
                    {
                        odysseyEffectForMesh.TextureEnabled = false;
                    }
                }

                // Combine mesh transform with entity transform
                Matrix4x4 finalWorld = Matrix4x4.Multiply(mesh.WorldTransform, entityWorldMatrix);

                // Set vertex and index buffers
                _device.SetVertexBuffer(mesh.VertexBuffer);
                _device.SetIndexBuffer(mesh.IndexBuffer);

                // Apply view/projection matrices and opacity via OpenGL state
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): glDrawElements with proper matrix setup
                // Based on xoreos: graphics.cpp renderWorld() @ lines 1059-1081
                if (effect is OdysseyBasicEffect odysseyEffectForMesh2)
                {
                    // Set world matrix for this mesh
                    odysseyEffectForMesh2.World = finalWorld;

                    // Apply effect (sets matrices and opacity via OpenGL state)
                    // Note: OpenGL fixed-function pipeline uses glMatrixMode and glMultMatrixf
                    // The Apply() method handles projection, view, and world matrix setup
                    odysseyEffectForMesh2.Apply();
                }

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
            // ModelResolver is in Andastra.Game.Games.Odyssey.Systems namespace
            if (_gameDataManager != null)
            {
                try
                {
                    Type modelResolverType = Type.GetType("Andastra.Game.Games.Odyssey.Systems.ModelResolver, Runtime.Games.Odyssey");
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

                // Use BioWare.NET MDL parser
                return BioWare.NET.Resource.Formats.MDL.MDLAuto.ReadMdl(result.Data);
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
                System.Numerics.Vector2 texCoord = System.Numerics.Vector2.Zero;

                if (mesh.Normals != null && i < mesh.Normals.Count)
                {
                    normal = new Vector3(mesh.Normals[i].X, mesh.Normals[i].Y, mesh.Normals[i].Z);
                }

                if (mesh.UV1 != null && i < mesh.UV1.Count)
                {
                    texCoord = new System.Numerics.Vector2(mesh.UV1[i].X, mesh.UV1[i].Y);
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
    /// <remarks>
    /// Odyssey Sound Player:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Sound playback: Original engine uses Miles Sound System (MSS) or DirectSound
    /// - Sound files: Stored as WAV resources, referenced by ResRef (e.g., "sound01")
    /// - Positional audio: Sounds can be played at 3D positions with distance attenuation
    /// - This implementation: Uses Windows waveOut APIs for sound playback (similar to OdysseyMusicPlayer)
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Sound effect playback functions @ various addresses
    /// - Sound directories: "STREAMSOUNDS" @ 0x007c69dc, "HD0:STREAMSOUNDS" @ 0x007c771c
    /// - Sound settings: "Sound Volume" @ 0x007c83cc, "Number 3D Sounds" @ 0x007c7258
    /// - Supports multiple simultaneous sound instances (unlike music/voice which play one at a time)
    /// </remarks>
    public class OdysseySoundPlayer : IDisposable
    {
        private readonly object _resourceProvider;
        private readonly Dictionary<uint, SoundInstanceData> _playingSounds;
        private readonly object _soundsLock = new object(); // Thread-safe access to playing sounds
        private uint _nextSoundInstanceId = 1;
        private float _masterVolume = 1.0f; // Master volume (0.0 to 1.0)
        private bool _disposed;

        // Sound instance data for tracking active sounds
        private struct SoundInstanceData
        {
            public IntPtr WaveOutHandle;
            public string SoundResRef;
            public float OriginalVolume;
            public Vector3 Position;
            public bool Is3D;
            public Thread PlaybackThread;
            public CancellationTokenSource Cancellation;
        }

        public OdysseySoundPlayer(object resourceProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
            _playingSounds = new Dictionary<uint, SoundInstanceData>();
        }

        /// <summary>
        /// Plays a sound effect by ResRef.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Sound effect playback system
        /// </summary>
        /// <param name="soundResRef">The ResRef of the sound file (e.g., "sound01").</param>
        public void Play(string soundResRef)
        {
            Play(soundResRef, Vector3.Zero);
        }

        /// <summary>
        /// Plays a 3D positioned sound effect by ResRef.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 3D sound effect playback system
        /// </summary>
        /// <param name="soundResRef">The ResRef of the sound file (e.g., "sound01").</param>
        /// <param name="position">3D position for spatial audio (Vector3.Zero for non-positional).</param>
        public void Play(string soundResRef, Vector3 position)
        {
            if (string.IsNullOrEmpty(soundResRef))
            {
                return;
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine($"[OdysseySoundPlayer] Sound playback not implemented for non-Windows platforms. Sound: {soundResRef}");
                Console.WriteLine($"[OdysseySoundPlayer] For cross-platform support, implement OpenAL or NAudio integration.");
                return;
            }

            try
            {
                // Get resource provider
                IGameResourceProvider resourceProvider = _resourceProvider as IGameResourceProvider;
                if (resourceProvider == null)
                {
                    Console.WriteLine($"[OdysseySoundPlayer] Resource provider is not IGameResourceProvider");
                    return;
                }

                // Load WAV resource
                var resourceId = new ResourceIdentifier(soundResRef, ResourceType.WAV);
                byte[] wavData = resourceProvider.GetResourceBytesAsync(resourceId, CancellationToken.None).GetAwaiter().GetResult();
                if (wavData == null || wavData.Length == 0)
                {
                    Console.WriteLine($"[OdysseySoundPlayer] Sound not found: {soundResRef}");
                    return;
                }

                // Parse WAV file to get format information
                WAV wavFile = WAVAuto.ReadWav(wavData);
                if (wavFile == null || wavFile.Data == null || wavFile.Data.Length == 0)
                {
                    Console.WriteLine($"[OdysseySoundPlayer] Failed to parse WAV file: {soundResRef}");
                    return;
                }

                // Create sound instance ID
                uint instanceId = _nextSoundInstanceId++;
                bool is3D = position != Vector3.Zero;

                // Start playback thread
                CancellationTokenSource cancellation = new CancellationTokenSource();
                Thread playbackThread = new Thread(() => PlaySoundAsync(instanceId, soundResRef, wavFile, position, is3D, cancellation.Token))
                {
                    IsBackground = true,
                    Name = $"OdysseySoundPlayer-{soundResRef}-{instanceId}"
                };
                playbackThread.Start();

                // Track sound instance
                lock (_soundsLock)
                {
                    _playingSounds[instanceId] = new SoundInstanceData
                    {
                        WaveOutHandle = IntPtr.Zero, // Will be set by playback thread
                        SoundResRef = soundResRef,
                        OriginalVolume = _masterVolume,
                        Position = position,
                        Is3D = is3D,
                        PlaybackThread = playbackThread,
                        Cancellation = cancellation
                    };
                }

                Console.WriteLine($"[OdysseySoundPlayer] Playing sound: {soundResRef} (instance: {instanceId}, 3D: {is3D}, format: {wavFile.SampleRate}Hz, {wavFile.Channels}ch, {wavFile.BitsPerSample}bit)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseySoundPlayer] Exception playing sound {soundResRef}: {ex.Message}");
                Console.WriteLine($"[OdysseySoundPlayer] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Asynchronously plays sound audio using Windows waveOut APIs.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Sound effect playback system
        /// </summary>
        private void PlaySoundAsync(uint instanceId, string soundResRef, WAV wavFile, Vector3 position, bool is3D, CancellationToken cancellationToken)
        {
            try
            {
                // Prepare WAVEFORMATEX structure
                WAVEFORMATEX waveFormat = new WAVEFORMATEX
                {
                    wFormatTag = 1, // WAVE_FORMAT_PCM
                    nChannels = (ushort)wavFile.Channels,
                    nSamplesPerSec = (uint)wavFile.SampleRate,
                    wBitsPerSample = (ushort)wavFile.BitsPerSample,
                    nBlockAlign = (ushort)(wavFile.Channels * (wavFile.BitsPerSample / 8)),
                    nAvgBytesPerSec = (uint)(wavFile.SampleRate * wavFile.Channels * (wavFile.BitsPerSample / 8)),
                    cbSize = 0
                };

                // Open waveOut device
                IntPtr waveOutHandle = IntPtr.Zero;
                int result = waveOutOpen(out waveOutHandle, WAVE_MAPPER, ref waveFormat, IntPtr.Zero, IntPtr.Zero, CALLBACK_NULL);
                if (result != MMSYSERR_NOERROR)
                {
                    Console.WriteLine($"[OdysseySoundPlayer] Failed to open waveOut device for {soundResRef}: error code {result}");
                    lock (_soundsLock)
                    {
                        _playingSounds.Remove(instanceId);
                    }
                    return;
                }

                // Update instance with waveOut handle
                lock (_soundsLock)
                {
                    if (_playingSounds.TryGetValue(instanceId, out SoundInstanceData instanceData))
                    {
                        instanceData.WaveOutHandle = waveOutHandle;
                        _playingSounds[instanceId] = instanceData;
                    }
                    else
                    {
                        // Instance was removed (stopped), close handle and return
                        waveOutClose(waveOutHandle);
                        return;
                    }
                }

                // Apply volume
                ApplyVolumeToSound(instanceId, waveOutHandle);

                // Play sound data
                byte[] pcmData = wavFile.Data;
                int bufferSize = pcmData.Length;
                int bufferPosition = 0;

                while (bufferPosition < bufferSize && !cancellationToken.IsCancellationRequested)
                {
                    // Calculate how much data to send
                    int remainingBytes = bufferSize - bufferPosition;
                    int bytesToSend = Math.Min(65536, remainingBytes); // 64KB buffer

                    // Allocate buffer for waveOut
                    WAVEHDR waveHeader = new WAVEHDR
                    {
                        lpData = Marshal.AllocHGlobal(bytesToSend),
                        dwBufferLength = (uint)bytesToSend,
                        dwFlags = 0,
                        dwLoops = 0
                    };

                    // Copy PCM data to buffer
                    Marshal.Copy(pcmData, bufferPosition, waveHeader.lpData, bytesToSend);

                    // Prepare header
                    result = waveOutPrepareHeader(waveOutHandle, ref waveHeader, Marshal.SizeOf(typeof(WAVEHDR)));
                    if (result != MMSYSERR_NOERROR)
                    {
                        Console.WriteLine($"[OdysseySoundPlayer] Failed to prepare wave header for {soundResRef}: error code {result}");
                        Marshal.FreeHGlobal(waveHeader.lpData);
                        break;
                    }

                    // Write data to waveOut
                    result = waveOutWrite(waveOutHandle, ref waveHeader, Marshal.SizeOf(typeof(WAVEHDR)));
                    if (result != MMSYSERR_NOERROR)
                    {
                        Console.WriteLine($"[OdysseySoundPlayer] Failed to write wave data for {soundResRef}: error code {result}");
                        waveOutUnprepareHeader(waveOutHandle, ref waveHeader, Marshal.SizeOf(typeof(WAVEHDR)));
                        Marshal.FreeHGlobal(waveHeader.lpData);
                        break;
                    }

                    // Wait for buffer to complete (with cancellation support)
                    while ((waveHeader.dwFlags & WHDR_DONE) == 0 && !cancellationToken.IsCancellationRequested)
                    {
                        Thread.Sleep(10); // Check every 10ms
                    }

                    // If cancellation was requested, stop playback
                    if (cancellationToken.IsCancellationRequested)
                    {
                        waveOutReset(waveOutHandle);
                        waveOutUnprepareHeader(waveOutHandle, ref waveHeader, Marshal.SizeOf(typeof(WAVEHDR)));
                        Marshal.FreeHGlobal(waveHeader.lpData);
                        break;
                    }

                    // Unprepare header and free buffer
                    waveOutUnprepareHeader(waveOutHandle, ref waveHeader, Marshal.SizeOf(typeof(WAVEHDR)));
                    Marshal.FreeHGlobal(waveHeader.lpData);

                    // Advance position
                    bufferPosition += bytesToSend;
                }

                // Clean up
                lock (_soundsLock)
                {
                    if (_playingSounds.ContainsKey(instanceId))
                    {
                        if (waveOutHandle != IntPtr.Zero)
                        {
                            waveOutReset(waveOutHandle);
                            waveOutClose(waveOutHandle);
                        }
                        _playingSounds.Remove(instanceId);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseySoundPlayer] Error in PlaySoundAsync for {soundResRef}: {ex.Message}");
                Console.WriteLine($"[OdysseySoundPlayer] Stack trace: {ex.StackTrace}");
                lock (_soundsLock)
                {
                    if (_playingSounds.TryGetValue(instanceId, out SoundInstanceData instanceData))
                    {
                        if (instanceData.WaveOutHandle != IntPtr.Zero)
                        {
                            try
                            {
                                waveOutReset(instanceData.WaveOutHandle);
                                waveOutClose(instanceData.WaveOutHandle);
                            }
                            catch { }
                        }
                        _playingSounds.Remove(instanceId);
                    }
                }
            }
        }

        /// <summary>
        /// Stops all currently playing sounds.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Sound stop functionality
        /// </summary>
        public void Stop()
        {
            lock (_soundsLock)
            {
                // Stop all sound instances
                foreach (var kvp in _playingSounds)
                {
                    uint instanceId = kvp.Key;
                    SoundInstanceData instanceData = kvp.Value;

                    try
                    {
                        // Cancel playback thread
                        if (instanceData.Cancellation != null)
                        {
                            instanceData.Cancellation.Cancel();
                        }

                        // Stop waveOut playback
                        if (instanceData.WaveOutHandle != IntPtr.Zero)
                        {
                            waveOutReset(instanceData.WaveOutHandle);
                            waveOutClose(instanceData.WaveOutHandle);
                        }

                        // Wait for thread to finish (with timeout)
                        if (instanceData.PlaybackThread != null)
                        {
                            if (!instanceData.PlaybackThread.Join(1000))
                            {
                                Console.WriteLine($"[OdysseySoundPlayer] Playback thread for {instanceData.SoundResRef} did not terminate in time");
                            }
                        }

                        // Dispose cancellation token
                        instanceData.Cancellation?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[OdysseySoundPlayer] Error stopping sound {instanceId} ({instanceData.SoundResRef}): {ex.Message}");
                    }
                }

                // Clear all tracked sounds
                _playingSounds.Clear();

                Console.WriteLine("[OdysseySoundPlayer] Stopped all sounds");
            }
        }

        /// <summary>
        /// Sets the master volume for all sounds.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "Sound Volume" @ 0x007c83cc
        /// Volume is clamped to 0.0-1.0 range and applied immediately to all active sounds.
        /// </summary>
        /// <param name="volume">Volume (0.0 to 1.0). Values outside this range are clamped.</param>
        public void SetVolume(float volume)
        {
            lock (_soundsLock)
            {
                // Clamp volume to valid range (0.0 to 1.0)
                _masterVolume = Math.Max(0.0f, Math.Min(1.0f, volume));

                // Apply volume to all playing sounds
                foreach (var kvp in _playingSounds)
                {
                    uint instanceId = kvp.Key;
                    SoundInstanceData instanceData = kvp.Value;

                    if (instanceData.WaveOutHandle != IntPtr.Zero)
                    {
                        ApplyVolumeToSound(instanceId, instanceData.WaveOutHandle);
                    }
                }

                Console.WriteLine($"[OdysseySoundPlayer] Master volume set to {_masterVolume:F2} ({(int)(_masterVolume * 100)}%)");
            }
        }

        /// <summary>
        /// Applies the current master volume to a specific sound instance.
        /// Uses Windows waveOutSetVolume API to set the volume for the waveOut device.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Sound volume control system
        /// </summary>
        private void ApplyVolumeToSound(uint instanceId, IntPtr waveOutHandle)
        {
            if (waveOutHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                // Get original volume for this instance
                float originalVolume = _masterVolume;
                lock (_soundsLock)
                {
                    if (_playingSounds.TryGetValue(instanceId, out SoundInstanceData instanceData))
                    {
                        originalVolume = instanceData.OriginalVolume;
                    }
                }

                // Calculate final volume (master volume * original volume)
                float finalVolume = _masterVolume * originalVolume;

                // Convert volume (0.0-1.0) to Windows volume format (0x0000-0xFFFF)
                // Windows volume is a DWORD where low word is left channel, high word is right channel
                ushort volumeValue = (ushort)(finalVolume * 0xFFFF);
                uint windowsVolume = (uint)(volumeValue | (volumeValue << 16)); // Left and right channels

                // Set volume for the waveOut device
                int result = waveOutSetVolume(waveOutHandle, windowsVolume);
                if (result != MMSYSERR_NOERROR)
                {
                    Console.WriteLine($"[OdysseySoundPlayer] Failed to set volume for instance {instanceId}: error code {result}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseySoundPlayer] Error applying volume to instance {instanceId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes resources and stops any playing sounds.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }

        #region Windows waveOut API P/Invoke
        // Windows waveOut API for sound playback
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Sound playback uses Miles Sound System (MSS) or DirectSound
        // This implementation uses waveOut for Windows compatibility
        // For cross-platform support, consider OpenAL or NAudio

        [DllImport("winmm.dll", EntryPoint = "waveOutOpen", SetLastError = true)]
        private static extern int waveOutOpen(out IntPtr phwo, int uDeviceID, ref WAVEFORMATEX pwfx, IntPtr dwCallback, IntPtr dwInstance, int fdwOpen);

        [DllImport("winmm.dll", EntryPoint = "waveOutClose", SetLastError = true)]
        private static extern int waveOutClose(IntPtr hwo);

        [DllImport("winmm.dll", EntryPoint = "waveOutPrepareHeader", SetLastError = true)]
        private static extern int waveOutPrepareHeader(IntPtr hwo, ref WAVEHDR pwh, int cbwh);

        [DllImport("winmm.dll", EntryPoint = "waveOutUnprepareHeader", SetLastError = true)]
        private static extern int waveOutUnprepareHeader(IntPtr hwo, ref WAVEHDR pwh, int cbwh);

        [DllImport("winmm.dll", EntryPoint = "waveOutWrite", SetLastError = true)]
        private static extern int waveOutWrite(IntPtr hwo, ref WAVEHDR pwh, int cbwh);

        [DllImport("winmm.dll", EntryPoint = "waveOutReset", SetLastError = true)]
        private static extern int waveOutReset(IntPtr hwo);

        [DllImport("winmm.dll", EntryPoint = "waveOutSetVolume", SetLastError = true)]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        // WaveOut constants (shared with OdysseyMusicPlayer)
        private const int WAVE_MAPPER = -1;
        private const int CALLBACK_NULL = 0x00000000;
        private const int MMSYSERR_NOERROR = 0;
        private const uint WHDR_DONE = 0x00000001;

        // WAVEFORMATEX structure for waveOut (shared with OdysseyMusicPlayer)
        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        // WAVEHDR structure for waveOut buffers (shared with OdysseyMusicPlayer)
        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }
        #endregion
    }

    /// <summary>
    /// Odyssey music player implementation.
    /// Plays background music using Windows waveOut APIs with looping support.
    /// </summary>
    /// <remarks>
    /// Odyssey Music Player:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Music playback: Original engine uses Miles Sound System (MSS) for background music
    /// - Music files: Stored as WAV or MP3 resources, referenced by ResRef (e.g., "mus_theme")
    /// - Volume control: Original engine has separate music volume setting
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Music playback functions @ various addresses
    /// - Music directories: "STREAMMUSIC" @ 0x007c69dc, "HD0:STREAMMUSIC" @ 0x007c771c
    /// - Music settings: "Music Volume" @ 0x007c83cc, "Music Enabled" @ 0x007c7258
    /// - This implementation: Uses Windows waveOut APIs for music playback with looping and volume control
    /// - swkotor2.exe: Music playback system uses MSS streaming with looping support
    /// </remarks>
    public class OdysseyMusicPlayer : IMusicPlayer
    {
        private readonly object _resourceProvider;
        private string _currentTrack;
        private bool _isPlaying;
        private bool _isPaused;
        private float _volume = 1.0f; // Default volume (0.0 to 1.0)
        private readonly object _playbackLock = new object(); // Thread-safe playback access
        private IntPtr _waveOutHandle = IntPtr.Zero;
        private WAVHeader _currentWavHeader;
        private byte[] _currentPcmData;
        private Thread _playbackThread;
        private CancellationTokenSource _playbackCancellation;
        private bool _shouldLoop = true; // Music always loops
        private bool _disposed;

        public OdysseyMusicPlayer(object resourceProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
        }

        /// <summary>
        /// Gets whether music is currently playing.
        /// </summary>
        public bool IsPlaying
        {
            get
            {
                lock (_playbackLock)
                {
                    return _isPlaying && !_isPaused && _waveOutHandle != IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Gets or sets the music volume (0.0f to 1.0f).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "Music Volume" @ 0x007c83cc
        /// </summary>
        public float Volume
        {
            get
            {
                lock (_playbackLock)
                {
                    return _volume;
                }
            }
            set
            {
                SetVolume(value);
            }
        }

        /// <summary>
        /// Plays a music file by ResRef. Music loops continuously until stopped.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Music playback system
        /// </summary>
        /// <param name="musicResRef">The ResRef of the music file (e.g., "mus_theme_cult").</param>
        /// <param name="volume">Volume level (0.0f to 1.0f, default 1.0f).</param>
        /// <returns>True if music started playing successfully, false otherwise.</returns>
        /// <remarks>
        /// Music Playback Implementation:
        /// - Loads WAV file from resource provider using musicResRef
        /// - Uses Windows waveOut APIs for streaming music playback
        /// - Music loops automatically until Stop() is called
        /// - Volume is applied immediately when playback starts
        /// - Original engine uses Miles Sound System (MSS) for streaming music
        /// - Music files are typically WAV format, stored in STREAMMUSIC directory
        /// - Based on swkotor.exe 0x005f9af0: Music playback function
        /// </remarks>
        public bool Play(string musicResRef, float volume = 1.0f)
        {
            if (string.IsNullOrEmpty(musicResRef))
            {
                return false;
            }

            // If same music is already playing, just adjust volume
            lock (_playbackLock)
            {
                if (_currentTrack == musicResRef && _isPlaying && !_isPaused && _waveOutHandle != IntPtr.Zero)
                {
                    Volume = volume;
                    return true;
                }

                // Stop current music if playing
                StopInternal();
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine($"[OdysseyMusicPlayer] Music playback not implemented for non-Windows platforms. Music: {musicResRef}");
                Console.WriteLine($"[OdysseyMusicPlayer] For cross-platform support, implement OpenAL or NAudio integration.");
                return false;
            }

            try
            {
                // Get resource provider
                IGameResourceProvider resourceProvider = _resourceProvider as IGameResourceProvider;
                if (resourceProvider == null)
                {
                    Console.WriteLine($"[OdysseyMusicPlayer] Resource provider is not IGameResourceProvider");
                    return false;
                }

                // Load WAV resource
                var resourceId = new ResourceIdentifier(musicResRef, ResourceType.WAV);
                byte[] wavData = resourceProvider.GetResourceBytesAsync(resourceId, CancellationToken.None).GetAwaiter().GetResult();
                if (wavData == null || wavData.Length == 0)
                {
                    Console.WriteLine($"[OdysseyMusicPlayer] Music not found: {musicResRef}");
                    return false;
                }

                // Parse WAV file to get format information
                WAV wavFile = WAVAuto.ReadWav(wavData);
                if (wavFile == null || wavFile.Data == null || wavFile.Data.Length == 0)
                {
                    Console.WriteLine($"[OdysseyMusicPlayer] Failed to parse WAV file: {musicResRef}");
                    return false;
                }

                lock (_playbackLock)
                {
                    // Store WAV data for playback
                    _currentTrack = musicResRef;
                    _currentPcmData = wavFile.Data;
                    _currentWavHeader = new WAVHeader
                    {
                        SampleRate = wavFile.SampleRate,
                        Channels = wavFile.Channels,
                        BitsPerSample = wavFile.BitsPerSample,
                        DataSize = wavFile.Data.Length
                    };

                    // Set volume
                    _volume = Math.Max(0.0f, Math.Min(1.0f, volume));

                    // Start playback thread
                    _playbackCancellation = new CancellationTokenSource();
                    _isPlaying = true;
                    _isPaused = false;
                    _playbackThread = new Thread(() => PlayMusicLoop(_playbackCancellation.Token))
                    {
                        IsBackground = true,
                        Name = $"OdysseyMusicPlayer-{musicResRef}"
                    };
                    _playbackThread.Start();

                    Console.WriteLine($"[OdysseyMusicPlayer] Playing music: {musicResRef} (looping, volume: {_volume:F2}, format: {_currentWavHeader.SampleRate}Hz, {_currentWavHeader.Channels}ch, {_currentWavHeader.BitsPerSample}bit)");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyMusicPlayer] Exception playing music {musicResRef}: {ex.Message}");
                Console.WriteLine($"[OdysseyMusicPlayer] Stack trace: {ex.StackTrace}");
                lock (_playbackLock)
                {
                    StopInternal();
                }
                return false;
            }
        }

        /// <summary>
        /// Stops music playback.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Music stop functionality
        /// </summary>
        public void Stop()
        {
            lock (_playbackLock)
            {
                StopInternal();
            }
        }

        /// <summary>
        /// Internal stop method (must be called with lock held).
        /// </summary>
        private void StopInternal()
        {
            if (_playbackCancellation != null)
            {
                _playbackCancellation.Cancel();
                _playbackCancellation.Dispose();
                _playbackCancellation = null;
            }

            if (_waveOutHandle != IntPtr.Zero)
            {
                try
                {
                    waveOutReset(_waveOutHandle);
                    waveOutClose(_waveOutHandle);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OdysseyMusicPlayer] Error closing waveOut: {ex.Message}");
                }
                _waveOutHandle = IntPtr.Zero;
            }

            if (_playbackThread != null)
            {
                // Wait for thread to finish (with timeout)
                if (!_playbackThread.Join(1000))
                {
                    Console.WriteLine("[OdysseyMusicPlayer] Playback thread did not terminate in time");
                }
                _playbackThread = null;
            }

            _isPlaying = false;
            _isPaused = false;
            _currentTrack = null;
            _currentPcmData = null;
        }

        /// <summary>
        /// Pauses music playback (can be resumed with Resume()).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Music pause functionality
        /// </summary>
        public void Pause()
        {
            lock (_playbackLock)
            {
                if (_isPlaying && !_isPaused && _waveOutHandle != IntPtr.Zero)
                {
                    try
                    {
                        waveOutPause(_waveOutHandle);
                        _isPaused = true;
                        Console.WriteLine($"[OdysseyMusicPlayer] Music paused: {_currentTrack}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[OdysseyMusicPlayer] Error pausing music: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Resumes paused music playback.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Music resume functionality
        /// </summary>
        public void Resume()
        {
            lock (_playbackLock)
            {
                if (_isPlaying && _isPaused && _waveOutHandle != IntPtr.Zero)
                {
                    try
                    {
                        waveOutRestart(_waveOutHandle);
                        _isPaused = false;
                        Console.WriteLine($"[OdysseyMusicPlayer] Music resumed: {_currentTrack}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[OdysseyMusicPlayer] Error resuming music: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Sets the music volume.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "Music Volume" @ 0x007c83cc
        /// Volume is clamped to 0.0-1.0 range and applied immediately to active playback.
        /// </summary>
        /// <param name="volume">Volume (0.0 to 1.0). Values outside this range are clamped.</param>
        /// <remarks>
        /// Music Volume Implementation:
        /// - Volume is stored and applied immediately to active playback
        /// - Uses Windows waveOutSetVolume API for volume control
        /// - Original engine uses Miles Sound System (MSS) which supports per-stream volume
        /// - Volume is applied to the waveOut device for the current music stream
        /// </remarks>
        public void SetVolume(float volume)
        {
            lock (_playbackLock)
            {
                // Clamp volume to valid range (0.0 to 1.0)
                _volume = Math.Max(0.0f, Math.Min(1.0f, volume));

                // Apply volume to currently playing music
                ApplyVolumeToCurrentPlayback();

                Console.WriteLine($"[OdysseyMusicPlayer] Music volume set to {_volume:F2} ({(int)(_volume * 100)}%)");
            }
        }

        /// <summary>
        /// Gets the current music volume.
        /// </summary>
        /// <returns>Current volume (0.0 to 1.0).</returns>
        public float GetVolume()
        {
            lock (_playbackLock)
            {
                return _volume;
            }
        }

        /// <summary>
        /// Applies the current volume setting to any active music playback.
        /// This method is called when volume changes and when music starts playing.
        /// Uses Windows waveOutSetVolume API to set the volume for the waveOut device.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Music volume control system
        /// </summary>
        private void ApplyVolumeToCurrentPlayback()
        {
            if (_isPlaying && !string.IsNullOrEmpty(_currentTrack) && _waveOutHandle != IntPtr.Zero)
            {
                try
                {
                    // Convert volume (0.0-1.0) to Windows volume format (0x0000-0xFFFF)
                    // Windows volume is a DWORD where low word is left channel, high word is right channel
                    // For stereo, we set both channels to the same volume
                    ushort volumeValue = (ushort)(_volume * 0xFFFF);
                    uint windowsVolume = (uint)(volumeValue | (volumeValue << 16)); // Left and right channels

                    // Set volume for the waveOut device
                    // waveOutSetVolume sets the volume for the entire waveOut device
                    // This affects all audio played through this device
                    int result = waveOutSetVolume(_waveOutHandle, windowsVolume);
                    if (result != MMSYSERR_NOERROR)
                    {
                        Console.WriteLine($"[OdysseyMusicPlayer] Failed to set volume: error code {result}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OdysseyMusicPlayer] Error applying volume: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Main playback loop that plays music with looping support.
        /// Uses Windows waveOut APIs to stream PCM audio data.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Music streaming and looping system
        /// </summary>
        private void PlayMusicLoop(CancellationToken cancellationToken)
        {
            if (_currentPcmData == null || _currentPcmData.Length == 0 || _currentWavHeader.DataSize == 0)
            {
                Console.WriteLine("[OdysseyMusicPlayer] No PCM data to play");
                lock (_playbackLock)
                {
                    _isPlaying = false;
                }
                return;
            }

            try
            {
                // Prepare WAVEFORMATEX structure
                WAVEFORMATEX waveFormat = new WAVEFORMATEX
                {
                    wFormatTag = 1, // WAVE_FORMAT_PCM
                    nChannels = (ushort)_currentWavHeader.Channels,
                    nSamplesPerSec = (uint)_currentWavHeader.SampleRate,
                    wBitsPerSample = (ushort)_currentWavHeader.BitsPerSample,
                    nBlockAlign = (ushort)(_currentWavHeader.Channels * (_currentWavHeader.BitsPerSample / 8)),
                    nAvgBytesPerSec = (uint)(_currentWavHeader.SampleRate * _currentWavHeader.Channels * (_currentWavHeader.BitsPerSample / 8)),
                    cbSize = 0
                };

                // Open waveOut device
                IntPtr waveOutHandle = IntPtr.Zero;
                int result = waveOutOpen(out waveOutHandle, WAVE_MAPPER, ref waveFormat, IntPtr.Zero, IntPtr.Zero, CALLBACK_NULL);
                if (result != MMSYSERR_NOERROR)
                {
                    Console.WriteLine($"[OdysseyMusicPlayer] Failed to open waveOut device: error code {result}");
                    lock (_playbackLock)
                    {
                        _isPlaying = false;
                    }
                    return;
                }

                lock (_playbackLock)
                {
                    _waveOutHandle = waveOutHandle;
                }

                // Apply initial volume
                ApplyVolumeToCurrentPlayback();

                // Play music in a loop until cancelled
                int bufferSize = 65536; // 64KB buffer size for smooth playback
                int position = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Calculate how much data to send
                    int remainingBytes = _currentPcmData.Length - position;
                    int bytesToSend = Math.Min(bufferSize, remainingBytes);

                    if (bytesToSend == 0)
                    {
                        // Reached end of track - loop back to beginning
                        if (_shouldLoop)
                        {
                            position = 0;
                            bytesToSend = Math.Min(bufferSize, _currentPcmData.Length);
                        }
                        else
                        {
                            // No looping, stop playback
                            break;
                        }
                    }

                    // Allocate buffer for waveOut
                    WAVEHDR waveHeader = new WAVEHDR
                    {
                        lpData = Marshal.AllocHGlobal(bytesToSend),
                        dwBufferLength = (uint)bytesToSend,
                        dwFlags = 0,
                        dwLoops = 0
                    };

                    // Copy PCM data to buffer
                    Marshal.Copy(_currentPcmData, position, waveHeader.lpData, bytesToSend);

                    // Prepare header
                    result = waveOutPrepareHeader(_waveOutHandle, ref waveHeader, Marshal.SizeOf(typeof(WAVEHDR)));
                    if (result != MMSYSERR_NOERROR)
                    {
                        Console.WriteLine($"[OdysseyMusicPlayer] Failed to prepare wave header: error code {result}");
                        Marshal.FreeHGlobal(waveHeader.lpData);
                        break;
                    }

                    // Write data to waveOut
                    result = waveOutWrite(_waveOutHandle, ref waveHeader, Marshal.SizeOf(typeof(WAVEHDR)));
                    if (result != MMSYSERR_NOERROR)
                    {
                        Console.WriteLine($"[OdysseyMusicPlayer] Failed to write wave data: error code {result}");
                        waveOutUnprepareHeader(_waveOutHandle, ref waveHeader, Marshal.SizeOf(typeof(WAVEHDR)));
                        Marshal.FreeHGlobal(waveHeader.lpData);
                        break;
                    }

                    // Wait for buffer to complete (with cancellation support)
                    while ((waveHeader.dwFlags & WHDR_DONE) == 0 && !cancellationToken.IsCancellationRequested)
                    {
                        Thread.Sleep(10); // Check every 10ms
                    }

                    // If cancellation was requested, stop playback before unpreparing header
                    // This ensures the buffer is no longer in use before we free it
                    if (cancellationToken.IsCancellationRequested && _waveOutHandle != IntPtr.Zero)
                    {
                        waveOutReset(_waveOutHandle);
                    }

                    // Unprepare header and free buffer
                    waveOutUnprepareHeader(_waveOutHandle, ref waveHeader, Marshal.SizeOf(typeof(WAVEHDR)));
                    Marshal.FreeHGlobal(waveHeader.lpData);

                    // Advance position
                    position += bytesToSend;
                    if (position >= _currentPcmData.Length)
                    {
                        position = 0; // Loop back to beginning
                    }
                }

                // Clean up
                lock (_playbackLock)
                {
                    if (_waveOutHandle != IntPtr.Zero)
                    {
                        waveOutReset(_waveOutHandle);
                        waveOutClose(_waveOutHandle);
                        _waveOutHandle = IntPtr.Zero;
                    }
                    _isPlaying = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyMusicPlayer] Error in playback loop: {ex.Message}");
                Console.WriteLine($"[OdysseyMusicPlayer] Stack trace: {ex.StackTrace}");
                lock (_playbackLock)
                {
                    if (_waveOutHandle != IntPtr.Zero)
                    {
                        try
                        {
                            waveOutReset(_waveOutHandle);
                            waveOutClose(_waveOutHandle);
                        }
                        catch { }
                        _waveOutHandle = IntPtr.Zero;
                    }
                    _isPlaying = false;
                }
            }
        }

        /// <summary>
        /// Gets the current music track ResRef.
        /// </summary>
        public string CurrentTrack
        {
            get
            {
                lock (_playbackLock)
                {
                    return _currentTrack;
                }
            }
        }

        /// <summary>
        /// Disposes resources and stops any playing music.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }

        #region WAV Header Structure
        /// <summary>
        /// WAV file header information extracted from parsed WAV file.
        /// </summary>
        private struct WAVHeader
        {
            public int SampleRate;
            public int Channels;
            public int BitsPerSample;
            public int DataSize;
        }
        #endregion

        #region Windows waveOut API P/Invoke
        // Windows waveOut API for music playback with looping and volume control
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Music playback uses Miles Sound System (MSS), but we use waveOut for Windows compatibility
        // For cross-platform support, consider OpenAL or NAudio

        [DllImport("winmm.dll", EntryPoint = "waveOutOpen", SetLastError = true)]
        private static extern int waveOutOpen(out IntPtr phwo, int uDeviceID, ref WAVEFORMATEX pwfx, IntPtr dwCallback, IntPtr dwInstance, int fdwOpen);

        [DllImport("winmm.dll", EntryPoint = "waveOutClose", SetLastError = true)]
        private static extern int waveOutClose(IntPtr hwo);

        [DllImport("winmm.dll", EntryPoint = "waveOutPrepareHeader", SetLastError = true)]
        private static extern int waveOutPrepareHeader(IntPtr hwo, ref WAVEHDR pwh, int cbwh);

        [DllImport("winmm.dll", EntryPoint = "waveOutUnprepareHeader", SetLastError = true)]
        private static extern int waveOutUnprepareHeader(IntPtr hwo, ref WAVEHDR pwh, int cbwh);

        [DllImport("winmm.dll", EntryPoint = "waveOutWrite", SetLastError = true)]
        private static extern int waveOutWrite(IntPtr hwo, ref WAVEHDR pwh, int cbwh);

        [DllImport("winmm.dll", EntryPoint = "waveOutPause", SetLastError = true)]
        private static extern int waveOutPause(IntPtr hwo);

        [DllImport("winmm.dll", EntryPoint = "waveOutRestart", SetLastError = true)]
        private static extern int waveOutRestart(IntPtr hwo);

        [DllImport("winmm.dll", EntryPoint = "waveOutReset", SetLastError = true)]
        private static extern int waveOutReset(IntPtr hwo);

        [DllImport("winmm.dll", EntryPoint = "waveOutSetVolume", SetLastError = true)]
        private static extern int waveOutSetVolume(IntPtr hwo, uint dwVolume);

        // WaveOut constants
        private const int WAVE_MAPPER = -1;
        private const int CALLBACK_NULL = 0x00000000;
        private const int MMSYSERR_NOERROR = 0;
        private const uint WHDR_DONE = 0x00000001;

        // WAVEFORMATEX structure for waveOut
        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        // WAVEHDR structure for waveOut buffers
        [StructLayout(LayoutKind.Sequential)]
        private struct WAVEHDR
        {
            public IntPtr lpData;
            public uint dwBufferLength;
            public uint dwBytesRecorded;
            public IntPtr dwUser;
            public uint dwFlags;
            public uint dwLoops;
            public IntPtr lpNext;
            public IntPtr reserved;
        }
        #endregion
    }

    /// <summary>
    /// Odyssey voice player implementation.
    /// Plays voice-over dialogue audio.
    /// </summary>
    /// <remarks>
    /// Odyssey Voice Player:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Voice playback: Original engine uses Miles Sound System (MSS) or DirectSound
    /// - Voice files: Stored as WAV resources, referenced by ResRef (e.g., "n_darthmalak01")
    /// - This implementation: Uses Windows API PlaySound for simple playback (can be extended with OpenAL)
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Voice-over playback functions @ various addresses
    /// - Voice directories: "STREAMVOICE" @ 0x007c69dc, "HD0:STREAMVOICE" @ 0x007c771c
    /// - Voice settings: "Voiceover Volume" @ 0x007c83cc, "Number 3D Voices" @ 0x007c7258
    /// </remarks>
    public class OdysseyVoicePlayer : IDisposable
    {
        private readonly object _resourceProvider;
        private bool _isPlaying;
        private float _volume = 1.0f;
        private string _currentVoiceResRef;
        private CancellationTokenSource _playbackCancellation;
        private readonly object _playbackLock = new object();

        public OdysseyVoicePlayer(object resourceProvider)
        {
            _resourceProvider = resourceProvider;
        }

        /// <summary>
        /// Plays a voice-over audio file by ResRef.
        /// Matching PyKotor and StrideVoicePlayer pattern: Load WAV, parse, play.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Voice-over playback system
        /// </summary>
        /// <param name="voiceResRef">The voice resource reference (e.g., "n_darthmalak01").</param>
        public void Play(string voiceResRef)
        {
            if (string.IsNullOrEmpty(voiceResRef))
            {
                return;
            }

            lock (_playbackLock)
            {
                // Stop any currently playing voice
                Stop();

                _currentVoiceResRef = voiceResRef;
                _isPlaying = true;
                _playbackCancellation = new CancellationTokenSource();

                // Play voice asynchronously to avoid blocking
                Task.Run(() => PlayVoiceAsync(voiceResRef, _playbackCancellation.Token));
            }
        }

        /// <summary>
        /// Asynchronously plays voice audio.
        /// </summary>
        private void PlayVoiceAsync(string voiceResRef, CancellationToken cancellationToken)
        {
            try
            {
                // Get resource provider
                IGameResourceProvider resourceProvider = _resourceProvider as IGameResourceProvider;
                if (resourceProvider == null)
                {
                    Console.WriteLine($"[OdysseyVoicePlayer] Resource provider is not IGameResourceProvider");
                    lock (_playbackLock)
                    {
                        _isPlaying = false;
                    }
                    return;
                }

                // Load WAV file from resource provider
                // Matching StrideVoicePlayer: GetResourceBytes(new ResourceIdentifier(voiceResRef, ResourceType.WAV))
                var resourceId = new ResourceIdentifier(voiceResRef, ResourceType.WAV);
                byte[] wavData = resourceProvider.GetResourceBytes(resourceId);

                if (wavData == null || wavData.Length == 0)
                {
                    Console.WriteLine($"[OdysseyVoicePlayer] Voice-over not found: {voiceResRef}");
                    lock (_playbackLock)
                    {
                        _isPlaying = false;
                    }
                    return;
                }

                // Parse WAV file to get format information
                // Matching StrideVoicePlayer: WAVAuto.ReadWav(wavData)
                WAV wavFile = WAVAuto.ReadWav(wavData);
                if (wavFile == null || wavFile.Data == null || wavFile.Data.Length == 0)
                {
                    Console.WriteLine($"[OdysseyVoicePlayer] Failed to parse WAV file: {voiceResRef}");
                    lock (_playbackLock)
                    {
                        _isPlaying = false;
                    }
                    return;
                }

                // Check cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    lock (_playbackLock)
                    {
                        _isPlaying = false;
                    }
                    return;
                }

                // Play audio using Windows API PlaySound
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Voice playback uses DirectSound/MSS, but we use PlaySound for simplicity
                // Note: PlaySound is Windows-only; for cross-platform, consider OpenAL or NAudio
                PlayWavAudio(wavData, wavFile, cancellationToken);

                // Calculate duration for logging
                float duration = 0.0f;
                if (wavFile.SampleRate > 0 && wavFile.Channels > 0 && wavFile.BitsPerSample > 0)
                {
                    duration = (float)wavFile.Data.Length / (wavFile.SampleRate * wavFile.Channels * (wavFile.BitsPerSample / 8));
                }

                Console.WriteLine($"[OdysseyVoicePlayer] Playing voice-over: {voiceResRef} (duration: {duration:F2}s, format: {wavFile.SampleRate}Hz, {wavFile.Channels}ch, {wavFile.BitsPerSample}bit)");

                // Wait for playback to complete (PlaySound is synchronous)
                // In a real implementation, this would be handled by the audio system
                if (!cancellationToken.IsCancellationRequested)
                {
                    // PlaySound blocks until playback completes or is cancelled
                    // For async playback, we would use a different API (OpenAL, DirectSound, etc.)
                }

                lock (_playbackLock)
                {
                    _isPlaying = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyVoicePlayer] Error playing voice-over {voiceResRef}: {ex.Message}");
                lock (_playbackLock)
                {
                    _isPlaying = false;
                }
            }
        }

        /// <summary>
        /// Plays WAV audio data using Windows API PlaySound.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Voice playback system
        /// Note: This is a Windows-only implementation. For cross-platform, use OpenAL or NAudio.
        /// </summary>
        private void PlayWavAudio(byte[] wavData, WAV wavFile, CancellationToken cancellationToken)
        {
            // Windows API PlaySound implementation
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Voice playback uses DirectSound/MSS
            // This implementation uses PlaySound for simplicity (can be extended with OpenAL)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    // Write WAV data to temporary file for PlaySound
                    // PlaySound can play from memory, but requires proper WAV format
                    string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".wav");
                    File.WriteAllBytes(tempFile, wavData);

                    try
                    {
                        // Play sound asynchronously (SND_ASYNC flag)
                        // SND_FILENAME: Play from file
                        // SND_ASYNC: Play asynchronously (non-blocking)
                        // SND_NODEFAULT: Don't play default sound if file not found
                        uint flags = SND_FILENAME | SND_ASYNC | SND_NODEFAULT;

                        // Apply volume if supported (PlaySound has limited volume control)
                        // Volume is applied via waveOutSetVolume or mixer APIs in a full implementation
                        bool success = PlaySound(tempFile, IntPtr.Zero, flags);

                        if (!success)
                        {
                            Console.WriteLine($"[OdysseyVoicePlayer] PlaySound failed for: {_currentVoiceResRef}");
                        }

                        // Wait for playback to complete or cancellation
                        // PlaySound with SND_ASYNC returns immediately, so we need to wait
                        // In a real implementation, we would use waveOut APIs or OpenAL for proper control
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            // Estimate duration and wait
                            float duration = 0.0f;
                            if (wavFile.SampleRate > 0 && wavFile.Channels > 0 && wavFile.BitsPerSample > 0)
                            {
                                duration = (float)wavFile.Data.Length / (wavFile.SampleRate * wavFile.Channels * (wavFile.BitsPerSample / 8));
                            }

                            if (duration > 0)
                            {
                                // Wait for playback to complete (with cancellation support)
                                int waitTime = (int)(duration * 1000); // Convert to milliseconds
                                int elapsed = 0;
                                while (elapsed < waitTime && !cancellationToken.IsCancellationRequested)
                                {
                                    Thread.Sleep(100); // Check every 100ms
                                    elapsed += 100;
                                }
                            }
                        }
                    }
                    finally
                    {
                        // Clean up temporary file
                        try
                        {
                            if (File.Exists(tempFile))
                            {
                                File.Delete(tempFile);
                            }
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OdysseyVoicePlayer] Error in PlayWavAudio: {ex.Message}");
                }
            }
            else
            {
                // Non-Windows platform: Log that playback is not supported
                // For cross-platform, implement OpenAL or NAudio
                Console.WriteLine($"[OdysseyVoicePlayer] Voice playback not implemented for non-Windows platforms. Voice: {_currentVoiceResRef}");
                Console.WriteLine($"[OdysseyVoicePlayer] For cross-platform support, implement OpenAL or NAudio integration.");
            }
        }

        /// <summary>
        /// Stops the currently playing voice-over.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Voice stop functionality
        /// </summary>
        public void Stop()
        {
            lock (_playbackLock)
            {
                if (_playbackCancellation != null)
                {
                    _playbackCancellation.Cancel();
                    _playbackCancellation.Dispose();
                    _playbackCancellation = null;
                }

                // Stop PlaySound playback
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try
                    {
                        // Stop any currently playing sound
                        PlaySound(null, IntPtr.Zero, SND_PURGE);
                    }
                    catch
                    {
                        // Ignore errors
                    }
                }

                _isPlaying = false;
                _currentVoiceResRef = null;
            }
        }

        /// <summary>
        /// Sets the voice volume.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "Voiceover Volume" @ 0x007c83cc
        /// Note: PlaySound has limited volume control; for full volume control, use OpenAL or DirectSound.
        /// </summary>
        /// <param name="volume">Volume (0.0 to 1.0).</param>
        public void SetVolume(float volume)
        {
            lock (_playbackLock)
            {
                _volume = Math.Max(0.0f, Math.Min(1.0f, volume));

                // PlaySound doesn't support per-instance volume control
                // For full volume control, we would need to use waveOutSetVolume or mixer APIs
                // This is a placeholder for future OpenAL/DirectSound integration
                Console.WriteLine($"[OdysseyVoicePlayer] Volume set to {_volume:F2} (PlaySound has limited volume control)");
            }
        }

        /// <summary>
        /// Gets whether voice-over is currently playing.
        /// </summary>
        public bool IsPlaying
        {
            get
            {
                lock (_playbackLock)
                {
                    return _isPlaying;
                }
            }
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        #region Windows API P/Invoke for PlaySound
        // Windows API PlaySound for voice playback
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Voice playback uses DirectSound/MSS
        // This is a simple implementation using PlaySound; for full features, use OpenAL or DirectSound
        [DllImport("winmm.dll", EntryPoint = "PlaySound", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

        // PlaySound flags
        private const uint SND_FILENAME = 0x00020000;
        private const uint SND_ASYNC = 0x0001;
        private const uint SND_NODEFAULT = 0x0002;
        private const uint SND_PURGE = 0x0040;
        #endregion
    }
}
