using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
{
    /// <summary>
    /// GPU instancing system for rendering multiple copies of the same mesh efficiently.
    /// 
    /// GPU instancing uses hardware instancing to render many instances of the same
    /// mesh in a single draw call, dramatically reducing draw call overhead.
    /// 
    /// Use cases:
    /// - Repeated geometry (trees, rocks, props)
    /// - Particle systems
    /// - Crowd rendering
    /// - Architectural elements (columns, windows, etc.)
    /// </summary>
    /// <remarks>
    /// GPU Instancing (Modern Enhancement):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) rendering system architecture
    /// - Original implementation: KOTOR renders each object with individual draw calls
    /// - Original engine: DirectX 8/9 era, hardware instancing not widely available/supported
    /// - Original rendering: Each entity/model rendered separately with individual draw calls
    /// - This is a modernization feature: GPU instancing dramatically reduces draw calls for repeated geometry
    /// - Modern enhancement: Uses modern GPU instancing APIs to render many instances in one draw call
    /// - Original behavior: Every repeated prop (trees, rocks) = separate draw call
    /// - Modern benefit: Hundreds of instances can be rendered in a single draw call
    /// </remarks>
    public class GPUInstancing : IDisposable
    {
        /// <summary>
        /// Instance data structure for GPU instancing.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct InstanceData : IVertexType
        {
            /// <summary>
            /// World transformation matrix (row-major).
            /// Matrix is stored as 4 Vector4 rows (16 floats = 64 bytes).
            /// </summary>
            public Matrix WorldMatrix;

            /// <summary>
            /// Color tint (RGBA).
            /// </summary>
            public Vector4 Color;

            /// <summary>
            /// Additional instance-specific parameters.
            /// </summary>
            public Vector4 Parameters;

            /// <summary>
            /// Vertex declaration for instance data.
            /// Matrix is split into 4 Vector4 elements for vertex shader input.
            /// </summary>
            public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
                // Matrix row 0 (offset 0, 16 bytes)
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                // Matrix row 1 (offset 16, 16 bytes)
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
                // Matrix row 2 (offset 32, 16 bytes)
                new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
                // Matrix row 3 (offset 48, 16 bytes)
                new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
                // Color (offset 64, 16 bytes)
                new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.Color, 1),
                // Parameters (offset 80, 16 bytes)
                new VertexElement(80, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5)
            );

            /// <summary>
            /// Gets the vertex declaration for instance data.
            /// </summary>
            VertexDeclaration IVertexType.VertexDeclaration
            {
                get { return VertexDeclaration; }
            }
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<IntPtr, InstanceBuffer> _instanceBuffers;
        private readonly Dictionary<IntPtr, List<InstanceData>> _pendingInstances;
        private int _maxInstancesPerDraw;

        /// <summary>
        /// Gets or sets the maximum instances per draw call.
        /// </summary>
        public int MaxInstancesPerDraw
        {
            get { return _maxInstancesPerDraw; }
            set { _maxInstancesPerDraw = Math.Max(1, value); }
        }

        /// <summary>
        /// Initializes a new GPU instancing system.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device.</param>
        public GPUInstancing(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _instanceBuffers = new Dictionary<IntPtr, InstanceBuffer>();
            _pendingInstances = new Dictionary<IntPtr, List<InstanceData>>();
            _maxInstancesPerDraw = 1024; // Default: 1024 instances per draw
        }

        /// <summary>
        /// Adds instances for a mesh.
        /// </summary>
        /// <param name="meshHandle">Mesh handle/identifier.</param>
        /// <param name="instances">Array of instance data. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if instances is null.</exception>
        public void AddInstances(IntPtr meshHandle, InstanceData[] instances)
        {
            if (instances == null)
            {
                throw new ArgumentNullException(nameof(instances));
            }

            List<InstanceData> list;
            if (!_pendingInstances.TryGetValue(meshHandle, out list))
            {
                list = new List<InstanceData>();
                _pendingInstances[meshHandle] = list;
            }

            list.AddRange(instances);
        }

        /// <summary>
        /// Prepares instance buffers for rendering.
        /// Must be called before DrawInstanced.
        /// </summary>
        public void PrepareBuffers()
        {
            foreach (var kvp in _pendingInstances)
            {
                IntPtr meshHandle = kvp.Key;
                List<InstanceData> instances = kvp.Value;

                if (instances.Count == 0)
                {
                    continue;
                }

                // Get or create instance buffer
                InstanceBuffer buffer;
                if (!_instanceBuffers.TryGetValue(meshHandle, out buffer))
                {
                    buffer = new InstanceBuffer
                    {
                        MeshHandle = meshHandle,
                        Buffer = new DynamicVertexBuffer(
                            _graphicsDevice,
                            typeof(InstanceData),
                            _maxInstancesPerDraw,
                            BufferUsage.WriteOnly
                        )
                    };
                    _instanceBuffers[meshHandle] = buffer;
                }

                // Update buffer data
                InstanceData[] data = instances.ToArray();
                if (data.Length > _maxInstancesPerDraw)
                {
                    // Truncate if too many instances
                    Array.Resize(ref data, _maxInstancesPerDraw);
                }

                buffer.Buffer.SetData(data, 0, data.Length, SetDataOptions.Discard);
                buffer.InstanceCount = data.Length;

                // Clear pending instances
                instances.Clear();
            }
        }

        /// <summary>
        /// Draws all prepared instances.
        /// </summary>
        /// <param name="mesh">Mesh to instance.</param>
        /// <param name="primitiveType">Primitive type.</param>
        /// <param name="vertexBuffer">Vertex buffer. Must not be null.</param>
        /// <param name="indexBuffer">Index buffer. Must not be null.</param>
        /// <param name="vertexCount">Number of vertices. Must be non-negative.</param>
        /// <param name="primitiveCount">Number of primitives. Must be non-negative.</param>
        /// <exception cref="ArgumentNullException">Thrown if vertexBuffer or indexBuffer is null.</exception>
        /// <exception cref="ArgumentException">Thrown if vertexCount or primitiveCount is negative.</exception>
        public void DrawInstanced(
            IntPtr mesh,
            PrimitiveType primitiveType,
            VertexBuffer vertexBuffer,
            IndexBuffer indexBuffer,
            int vertexCount,
            int primitiveCount)
        {
            if (vertexBuffer == null)
            {
                throw new ArgumentNullException(nameof(vertexBuffer));
            }
            if (indexBuffer == null)
            {
                throw new ArgumentNullException(nameof(indexBuffer));
            }
            if (vertexCount < 0)
            {
                throw new ArgumentException("Vertex count must be non-negative.", nameof(vertexCount));
            }
            if (primitiveCount < 0)
            {
                throw new ArgumentException("Primitive count must be non-negative.", nameof(primitiveCount));
            }

            InstanceBuffer buffer;
            if (!_instanceBuffers.TryGetValue(mesh, out buffer) || buffer.InstanceCount == 0)
            {
                return;
            }

            // Set vertex buffers with instancing support
            // Slot 0: Per-vertex data (instanceFrequency = 0 means per-vertex)
            // Slot 1: Per-instance data (instanceFrequency = 1 means per-instance)
            _graphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(vertexBuffer, 0, 0),
                new VertexBufferBinding(buffer.Buffer, 0, 1) // Instance buffer at slot 1, instanceFrequency = 1
            );
            _graphicsDevice.Indices = indexBuffer;

            // Draw instanced primitives
            // baseVertex: 0 (no vertex offset)
            // startIndex: 0 (start from beginning of index buffer)
            // primitiveCount: number of primitives per instance
            // instanceCount: number of instances to render
            _graphicsDevice.DrawInstancedPrimitives(
                primitiveType,
                0,                    // baseVertex
                0,                    // startIndex
                primitiveCount,       // primitiveCount per instance
                buffer.InstanceCount  // instanceCount
            );
        }

        /// <summary>
        /// Clears all pending instances.
        /// </summary>
        public void Clear()
        {
            foreach (List<InstanceData> instances in _pendingInstances.Values)
            {
                instances.Clear();
            }
        }

        public void Dispose()
        {
            foreach (InstanceBuffer buffer in _instanceBuffers.Values)
            {
                if (buffer.Buffer != null)
                {
                    buffer.Buffer.Dispose();
                }
            }
            _instanceBuffers.Clear();
            _pendingInstances.Clear();
        }

        private struct InstanceBuffer
        {
            public IntPtr MeshHandle;
            public DynamicVertexBuffer Buffer;
            public int InstanceCount;
        }
    }
}

