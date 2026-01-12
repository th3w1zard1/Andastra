using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Microsoft.Xna.Framework;
using XnaVector3 = Microsoft.Xna.Framework.Vector3;
using XnaVector4 = Microsoft.Xna.Framework.Vector4;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// GPU-driven indirect rendering using command buffers.
    ///
    /// Indirect rendering allows the GPU to generate draw commands,
    /// enabling:
    /// - GPU-side culling (compute shader based)
    /// - Automatic LOD selection on GPU
    /// - Massive scalability (millions of objects)
    /// - Reduced CPU overhead
    ///
    /// Based on modern AAA game GPU-driven rendering techniques.
    /// Implementation follows GPU-driven rendering patterns from:
    /// - Unreal Engine 5 Nanite
    /// - Unity DOTS/ECS rendering
    /// - Frostbite engine GPU culling
    /// </summary>
    public class IndirectRenderer : IDisposable
    {
        /// <summary>
        /// Indirect draw command structure matching D3D12/Vulkan indirect draw command format.
        /// Based on D3D12_DRAW_INDEXED_ARGUMENTS and VkDrawIndexedIndirectCommand.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct IndirectDrawCommand
        {
            /// <summary>
            /// Index count per instance.
            /// </summary>
            public uint IndexCountPerInstance;

            /// <summary>
            /// Instance count.
            /// </summary>
            public uint InstanceCount;

            /// <summary>
            /// Start index location.
            /// </summary>
            public uint StartIndexLocation;

            /// <summary>
            /// Base vertex location.
            /// </summary>
            public int BaseVertexLocation;

            /// <summary>
            /// Start instance location.
            /// </summary>
            public uint StartInstanceLocation;
        }

        /// <summary>
        /// Object data for GPU culling.
        /// Packed for efficient GPU access (16-byte aligned).
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        public struct ObjectData
        {
            /// <summary>
            /// World matrix (row-major, 4x4 = 16 floats = 64 bytes).
            /// </summary>
            public Matrix WorldMatrix;

            /// <summary>
            /// Bounding sphere center.
            /// </summary>
            public Vector3 BoundingCenter;

            /// <summary>
            /// Bounding sphere radius.
            /// </summary>
            public float BoundingRadius;

            /// <summary>
            /// Material ID.
            /// </summary>
            public uint MaterialId;

            /// <summary>
            /// Mesh ID.
            /// </summary>
            public uint MeshId;

            /// <summary>
            /// LOD level (0 = highest detail, higher = lower detail).
            /// </summary>
            public uint LODLevel;

            /// <summary>
            /// Visibility flags (set by GPU culling: 0 = culled, 1 = visible).
            /// </summary>
            public uint Visible;
        }

        /// <summary>
        /// Constant buffer data for GPU culling compute shader.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        private struct CullingConstants
        {
            /// <summary>
            /// View matrix (row-major, 4x4 = 16 floats).
            /// </summary>
            public Matrix ViewMatrix;

            /// <summary>
            /// Projection matrix (row-major, 4x4 = 16 floats).
            /// </summary>
            public Matrix ProjectionMatrix;

            /// <summary>
            /// View-projection matrix (row-major, 4x4 = 16 floats).
            /// </summary>
            public Matrix ViewProjectionMatrix;

            /// <summary>
            /// Camera position in world space.
            /// </summary>
            public Vector3 CameraPosition;

            /// <summary>
            /// Number of objects to process.
            /// </summary>
            public uint ObjectCount;

            /// <summary>
            /// LOD distance thresholds (4 levels: near, mid, far, very far).
            /// </summary>
            public Vector4 LODDistances;

            /// <summary>
            /// Frustum planes (6 planes, each Vector4: normal.x, normal.y, normal.z, distance).
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public Vector4[] FrustumPlanes;
        }

        private readonly IDevice _device;
        private IBuffer _objectDataBuffer;
        private IBuffer _indirectCommandBuffer;
        private IBuffer _culledObjectBuffer;
        private IBuffer _constantBuffer;
        private IShader _computeShader;
        private IComputePipeline _computePipeline;
        private IBindingLayout _bindingLayout;
        private IBindingSet _bindingSet;
        private int _maxObjects;
        private int _currentObjectCount;
        private const int ThreadGroupSize = 64; // Threads per compute shader thread group

        /// <summary>
        /// Gets or sets the maximum number of objects.
        /// </summary>
        public int MaxObjects
        {
            get { return _maxObjects; }
            set
            {
                _maxObjects = Math.Max(1, value);
                RecreateBuffers();
            }
        }

        /// <summary>
        /// Gets the current number of objects in the buffer.
        /// </summary>
        public int CurrentObjectCount
        {
            get { return _currentObjectCount; }
        }

        /// <summary>
        /// Initializes a new indirect renderer.
        /// </summary>
        /// <param name="device">Graphics device interface for creating resources and dispatching compute shaders.</param>
        /// <param name="maxObjects">Maximum number of objects to support (default: 100000).</param>
        public IndirectRenderer(IDevice device, int maxObjects = 100000)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            _device = device;
            _maxObjects = maxObjects;
            _currentObjectCount = 0;

            InitializeComputeShader();
            RecreateBuffers();
        }

        /// <summary>
        /// Updates object data buffer with current objects.
        /// </summary>
        /// <param name="objects">Array of object data. Can be null or empty (no-op).</param>
        public void UpdateObjectData(ObjectData[] objects)
        {
            if (objects == null || objects.Length == 0)
            {
                _currentObjectCount = 0;
                return;
            }

            if (objects.Length > _maxObjects)
            {
                Array.Resize(ref objects, _maxObjects);
            }

            _currentObjectCount = objects.Length;

            if (_objectDataBuffer == null)
            {
                return;
            }

            // Upload object data to GPU buffer
            ICommandList commandList = _device.CreateCommandList(CommandListType.Copy);
            commandList.Open();

            // Convert ObjectData array to byte array for upload
            int objectDataSize = Marshal.SizeOf(typeof(ObjectData));
            byte[] objectDataBytes = new byte[objects.Length * objectDataSize];
            IntPtr ptr = Marshal.AllocHGlobal(objectDataSize);
            try
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    Marshal.StructureToPtr(objects[i], ptr, false);
                    Marshal.Copy(ptr, objectDataBytes, i * objectDataSize, objectDataSize);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            commandList.WriteBuffer(_objectDataBuffer, objectDataBytes, 0);
            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();
        }

        /// <summary>
        /// Executes GPU culling compute shader.
        /// Performs frustum culling, LOD selection, and generates indirect draw commands.
        /// Based on GPU-driven rendering techniques from modern game engines.
        /// </summary>
        /// <param name="viewMatrix">View matrix.</param>
        /// <param name="projectionMatrix">Projection matrix.</param>
        /// <param name="frustumPlanes">Frustum planes for culling (6 planes: left, right, bottom, top, near, far).</param>
        /// <param name="cameraPosition">Camera position in world space for distance-based LOD.</param>
        /// <param name="lodDistances">LOD distance thresholds (Vector4: near, mid, far, very far).</param>
        public void ExecuteGPUCulling(Matrix viewMatrix, Matrix projectionMatrix, Vector4[] frustumPlanes, Vector3 cameraPosition, Vector4 lodDistances)
        {
            if (_currentObjectCount == 0 || _computePipeline == null || _bindingSet == null)
            {
                return;
            }

            if (frustumPlanes == null || frustumPlanes.Length < 6)
            {
                throw new ArgumentException("frustumPlanes must contain at least 6 planes", nameof(frustumPlanes));
            }

            // Prepare constant buffer data
            Matrix viewProjectionMatrix = viewMatrix * projectionMatrix;
            CullingConstants constants = new CullingConstants
            {
                ViewMatrix = viewMatrix,
                ProjectionMatrix = projectionMatrix,
                ViewProjectionMatrix = viewProjectionMatrix,
                CameraPosition = cameraPosition,
                ObjectCount = (uint)_currentObjectCount,
                LODDistances = lodDistances,
                FrustumPlanes = new Vector4[6]
            };
            Array.Copy(frustumPlanes, constants.FrustumPlanes, Math.Min(6, frustumPlanes.Length));

            // Upload constant buffer
            int constantBufferSize = Marshal.SizeOf(typeof(CullingConstants));
            byte[] constantBufferBytes = new byte[constantBufferSize];
            IntPtr constantsPtr = Marshal.AllocHGlobal(constantBufferSize);
            try
            {
                Marshal.StructureToPtr(constants, constantsPtr, false);
                Marshal.Copy(constantsPtr, constantBufferBytes, 0, constantBufferSize);
            }
            finally
            {
                Marshal.FreeHGlobal(constantsPtr);
            }

            // Clear indirect command buffer (set all commands to zero initially)
            ICommandList clearCommandList = _device.CreateCommandList(CommandListType.Copy);
            clearCommandList.Open();
            byte[] zeroBytes = new byte[Marshal.SizeOf(typeof(IndirectDrawCommand)) * _maxObjects];
            clearCommandList.WriteBuffer(_indirectCommandBuffer, zeroBytes, 0);
            clearCommandList.Close();
            _device.ExecuteCommandList(clearCommandList);
            clearCommandList.Dispose();

            // Upload constants
            ICommandList uploadCommandList = _device.CreateCommandList(CommandListType.Copy);
            uploadCommandList.Open();
            uploadCommandList.WriteBuffer(_constantBuffer, constantBufferBytes, 0);
            uploadCommandList.Close();
            _device.ExecuteCommandList(uploadCommandList);
            uploadCommandList.Dispose();

            // Transition buffers to appropriate states for compute shader
            ICommandList computeCommandList = _device.CreateCommandList(CommandListType.Compute);
            computeCommandList.Open();

            // Transition buffers to UnorderedAccess state for compute shader writes
            computeCommandList.SetBufferState(_objectDataBuffer, ResourceState.UnorderedAccess);
            computeCommandList.SetBufferState(_indirectCommandBuffer, ResourceState.UnorderedAccess);
            computeCommandList.SetBufferState(_culledObjectBuffer, ResourceState.UnorderedAccess);
            computeCommandList.SetBufferState(_constantBuffer, ResourceState.ShaderResource);
            computeCommandList.CommitBarriers();

            // Set compute state and dispatch
            ComputeState computeState = new ComputeState
            {
                Pipeline = _computePipeline,
                BindingSets = new IBindingSet[] { _bindingSet }
            };
            computeCommandList.SetComputeState(computeState);

            // Calculate thread group count (one thread per object, rounded up)
            int threadGroupCountX = (_currentObjectCount + ThreadGroupSize - 1) / ThreadGroupSize;
            computeCommandList.Dispatch(threadGroupCountX, 1, 1);

            // Transition indirect command buffer to IndirectArgument state for drawing
            computeCommandList.SetBufferState(_indirectCommandBuffer, ResourceState.IndirectArgument);
            computeCommandList.CommitBarriers();

            computeCommandList.Close();
            _device.ExecuteCommandList(computeCommandList);
            computeCommandList.Dispose();
        }

        /// <summary>
        /// Executes indirect draw commands generated by GPU culling.
        /// Uses DrawIndexedIndirect to render visible objects.
        /// Based on D3D12 DrawIndexedInstancedIndirect and Vulkan vkCmdDrawIndexedIndirect.
        /// </summary>
        /// <param name="indexBuffer">Index buffer for indexed drawing.</param>
        /// <param name="vertexBuffers">Vertex buffers for drawing.</param>
        /// <param name="graphicsPipeline">Graphics pipeline state for rendering.</param>
        /// <param name="framebuffer">Framebuffer to render to.</param>
        /// <param name="viewport">Viewport for rendering.</param>
        public void ExecuteIndirectDraws(IBuffer indexBuffer, IBuffer[] vertexBuffers, IGraphicsPipeline graphicsPipeline, IFramebuffer framebuffer, Viewport viewport)
        {
            if (_currentObjectCount == 0 || _indirectCommandBuffer == null || indexBuffer == null || graphicsPipeline == null)
            {
                return;
            }

            ICommandList commandList = _device.CreateCommandList(CommandListType.Graphics);
            commandList.Open();

            // Set graphics state
            GraphicsState graphicsState = new GraphicsState
            {
                Pipeline = graphicsPipeline,
                Framebuffer = framebuffer,
                Viewport = new ViewportState
                {
                    Viewports = new Viewport[] { viewport },
                    Scissors = new Andastra.Runtime.MonoGame.Interfaces.Rectangle[] { new Andastra.Runtime.MonoGame.Interfaces.Rectangle { X = (int)viewport.X, Y = (int)viewport.Y, Width = (int)viewport.Width, Height = (int)viewport.Height } }
                },
                IndexBuffer = indexBuffer
            };

            if (vertexBuffers != null && vertexBuffers.Length > 0)
            {
                graphicsState.VertexBuffers = vertexBuffers;
            }

            commandList.SetGraphicsState(graphicsState);

            // Execute indirect indexed draw
            // DrawIndexedIndirect reads draw commands from _indirectCommandBuffer
            // Each command specifies: index count, instance count, start index, base vertex, start instance
            commandList.DrawIndexedIndirect(_indirectCommandBuffer, 0, _currentObjectCount, Marshal.SizeOf(typeof(IndirectDrawCommand)));

            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();
        }

        /// <summary>
        /// Initializes compute shader and pipeline for GPU culling.
        /// </summary>
        private void InitializeComputeShader()
        {
            if (_device == null)
            {
                return;
            }

            // Try to load compute shader bytecode
            byte[] shaderBytecode = LoadComputeShaderBytecode("IndirectRendererCulling");

            if (shaderBytecode == null || shaderBytecode.Length == 0)
            {
                // Generate minimal placeholder compute shader bytecode
                // In production, this should be replaced with actual compiled shader bytecode
                shaderBytecode = GeneratePlaceholderComputeShaderBytecode();

                if (shaderBytecode == null || shaderBytecode.Length == 0)
                {
                    Console.WriteLine("[IndirectRenderer] Warning: Could not load or generate compute shader bytecode. GPU culling will not function.");
                    return;
                }

                Console.WriteLine("[IndirectRenderer] Warning: Using placeholder compute shader bytecode. For production, provide pre-compiled shader bytecode.");
            }

            // Create compute shader
            _computeShader = _device.CreateShader(new ShaderDesc
            {
                Type = ShaderType.Compute,
                Bytecode = shaderBytecode,
                EntryPoint = "CullObjects",
                DebugName = "IndirectRendererCulling"
            });

            if (_computeShader == null)
            {
                Console.WriteLine("[IndirectRenderer] Error: Failed to create compute shader.");
                return;
            }

            // Create binding layout for compute shader resources
            _bindingLayout = _device.CreateBindingLayout(new BindingLayoutDesc
            {
                Items = new BindingLayoutItem[]
                {
                    // Constant buffer (slot 0)
                    new BindingLayoutItem
                    {
                        Type = BindingType.ConstantBuffer,
                        Slot = 0,
                        Stages = ShaderStageFlags.Compute
                    },
                    // Object data buffer (UAV, slot 0)
                    new BindingLayoutItem
                    {
                        Type = BindingType.RWBuffer,
                        Slot = 0,
                        Stages = ShaderStageFlags.Compute
                    },
                    // Indirect command buffer (UAV, slot 1)
                    new BindingLayoutItem
                    {
                        Type = BindingType.RWBuffer,
                        Slot = 1,
                        Stages = ShaderStageFlags.Compute
                    },
                    // Culled object buffer (UAV, slot 2)
                    new BindingLayoutItem
                    {
                        Type = BindingType.RWBuffer,
                        Slot = 2,
                        Stages = ShaderStageFlags.Compute
                    }
                },
                IsPushDescriptor = false
            });

            // Create compute pipeline
            _computePipeline = _device.CreateComputePipeline(new ComputePipelineDesc
            {
                ComputeShader = _computeShader,
                BindingLayouts = new IBindingLayout[] { _bindingLayout }
            });

            if (_computePipeline == null)
            {
                Console.WriteLine("[IndirectRenderer] Error: Failed to create compute pipeline.");
                return;
            }
        }

        /// <summary>
        /// Loads compute shader bytecode from resources or file system.
        /// </summary>
        private byte[] LoadComputeShaderBytecode(string shaderName)
        {
            // Try embedded resources first
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string resourceName = $"Andastra.Runtime.MonoGame.Shaders.{shaderName}.cso";
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        byte[] bytecode = new byte[stream.Length];
                        stream.Read(bytecode, 0, bytecode.Length);
                        return bytecode;
                    }
                }
            }
            catch
            {
                // Ignore errors, try file system next
            }

            // Try file system
            try
            {
                string filePath = System.IO.Path.Combine("Shaders", $"{shaderName}.cso");
                if (System.IO.File.Exists(filePath))
                {
                    return System.IO.File.ReadAllBytes(filePath);
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        /// <summary>
        /// Generates minimal placeholder compute shader bytecode.
        /// This is a placeholder that should be replaced with actual compiled shader bytecode.
        /// </summary>
        private byte[] GeneratePlaceholderComputeShaderBytecode()
        {
            // This is a minimal placeholder - in production, actual shader bytecode should be provided
            // The compute shader should:
            // 1. Read object data from structured buffer
            // 2. Test bounding sphere against frustum planes
            // 3. Calculate distance from camera for LOD selection
            // 4. Write indirect draw command if visible
            // 5. Compact visible objects into culled buffer

            // For now, return empty array - actual implementation requires compiled shader bytecode
            // Shader source would be in HLSL/GLSL and compiled offline
            return new byte[0];
        }

        /// <summary>
        /// Recreates all GPU buffers when MaxObjects changes.
        /// </summary>
        private void RecreateBuffers()
        {
            DisposeBuffers();

            if (_device == null)
            {
                return;
            }

            int objectDataSize = Marshal.SizeOf(typeof(ObjectData));
            int indirectCommandSize = Marshal.SizeOf(typeof(IndirectDrawCommand));
            int constantBufferSize = Marshal.SizeOf(typeof(CullingConstants));

            // Create object data buffer (structured buffer, UAV)
            _objectDataBuffer = _device.CreateBuffer(new BufferDesc
            {
                ByteSize = objectDataSize * _maxObjects,
                StructStride = objectDataSize,
                Usage = BufferUsageFlags.UnorderedAccess | BufferUsageFlags.ShaderResource,
                InitialState = ResourceState.UnorderedAccess,
                KeepInitialState = false,
                CanHaveRawViews = false,
                IsAccelStructBuildInput = false,
                HeapType = BufferHeapType.Default,
                DebugName = "IndirectRenderer_ObjectDataBuffer"
            });

            // Create indirect command buffer (UAV for writing, IndirectArgument for reading)
            _indirectCommandBuffer = _device.CreateBuffer(new BufferDesc
            {
                ByteSize = indirectCommandSize * _maxObjects,
                StructStride = indirectCommandSize,
                Usage = BufferUsageFlags.UnorderedAccess | BufferUsageFlags.IndirectArgument,
                InitialState = ResourceState.UnorderedAccess,
                KeepInitialState = false,
                CanHaveRawViews = false,
                IsAccelStructBuildInput = false,
                HeapType = BufferHeapType.Default,
                DebugName = "IndirectRenderer_IndirectCommandBuffer"
            });

            // Create culled object buffer (structured buffer, UAV for compacting visible objects)
            _culledObjectBuffer = _device.CreateBuffer(new BufferDesc
            {
                ByteSize = objectDataSize * _maxObjects,
                StructStride = objectDataSize,
                Usage = BufferUsageFlags.UnorderedAccess | BufferUsageFlags.ShaderResource,
                InitialState = ResourceState.UnorderedAccess,
                KeepInitialState = false,
                CanHaveRawViews = false,
                IsAccelStructBuildInput = false,
                HeapType = BufferHeapType.Default,
                DebugName = "IndirectRenderer_CulledObjectBuffer"
            });

            // Create constant buffer for culling parameters
            _constantBuffer = _device.CreateBuffer(new BufferDesc
            {
                ByteSize = constantBufferSize,
                StructStride = 0,
                Usage = BufferUsageFlags.ConstantBuffer,
                InitialState = ResourceState.ShaderResource,
                KeepInitialState = false,
                CanHaveRawViews = false,
                IsAccelStructBuildInput = false,
                HeapType = BufferHeapType.Upload, // CPU-writable for frequent updates
                DebugName = "IndirectRenderer_ConstantBuffer"
            });

            // Recreate binding set with new buffers
            if (_bindingLayout != null)
            {
                _bindingSet = _device.CreateBindingSet(_bindingLayout, new BindingSetDesc
                {
                    Items = new BindingSetItem[]
                    {
                        new BindingSetItem
                        {
                            Type = BindingType.ConstantBuffer,
                            Slot = 0,
                            Buffer = _constantBuffer
                        },
                        new BindingSetItem
                        {
                            Type = BindingType.RWBuffer,
                            Slot = 0,
                            Buffer = _objectDataBuffer
                        },
                        new BindingSetItem
                        {
                            Type = BindingType.RWBuffer,
                            Slot = 1,
                            Buffer = _indirectCommandBuffer
                        },
                        new BindingSetItem
                        {
                            Type = BindingType.RWBuffer,
                            Slot = 2,
                            Buffer = _culledObjectBuffer
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Disposes all GPU buffers.
        /// </summary>
        private void DisposeBuffers()
        {
            _objectDataBuffer?.Dispose();
            _objectDataBuffer = null;
            _indirectCommandBuffer?.Dispose();
            _indirectCommandBuffer = null;
            _culledObjectBuffer?.Dispose();
            _culledObjectBuffer = null;
            _constantBuffer?.Dispose();
            _constantBuffer = null;
            _bindingSet?.Dispose();
            _bindingSet = null;
        }

        /// <summary>
        /// Disposes all resources.
        /// </summary>
        public void Dispose()
        {
            DisposeBuffers();
            _computePipeline?.Dispose();
            _computePipeline = null;
            _computeShader?.Dispose();
            _computeShader = null;
            _bindingLayout?.Dispose();
            _bindingLayout = null;
        }
    }
}

