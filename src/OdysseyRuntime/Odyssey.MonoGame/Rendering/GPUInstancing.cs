using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Odyssey.MonoGame.Rendering
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
    public class GPUInstancing : IDisposable
    {
        /// <summary>
        /// Instance data structure for GPU instancing.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct InstanceData
        {
            /// <summary>
            /// World transformation matrix (row-major).
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
                throw new ArgumentNullException("graphicsDevice");
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
        /// <param name="instances">Array of instance data.</param>
        public void AddInstances(IntPtr meshHandle, InstanceData[] instances)
        {
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
        /// <param name="vertexBuffer">Vertex buffer.</param>
        /// <param name="indexBuffer">Index buffer.</param>
        /// <param name="vertexCount">Number of vertices.</param>
        /// <param name="primitiveCount">Number of primitives.</param>
        public void DrawInstanced(
            IntPtr mesh,
            PrimitiveType primitiveType,
            VertexBuffer vertexBuffer,
            IndexBuffer indexBuffer,
            int vertexCount,
            int primitiveCount)
        {
            InstanceBuffer buffer;
            if (!_instanceBuffers.TryGetValue(mesh, out buffer) || buffer.InstanceCount == 0)
            {
                return;
            }

            // Set vertex buffer
            _graphicsDevice.SetVertexBuffers(
                new VertexBufferBinding(vertexBuffer, 0, 0),
                new VertexBufferBinding(buffer.Buffer, 0, 1) // Instance buffer at slot 1
            );
            _graphicsDevice.Indices = indexBuffer;

            // Draw instanced primitives
            // Note: MonoGame doesn't have DrawInstancedPrimitives directly,
            // would need custom shader with instancing support
            // For now, this is a placeholder structure
            // In actual implementation, would use:
            // _graphicsDevice.DrawInstancedPrimitives(primitiveType, 0, 0, primitiveCount, 0, buffer.InstanceCount);
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

