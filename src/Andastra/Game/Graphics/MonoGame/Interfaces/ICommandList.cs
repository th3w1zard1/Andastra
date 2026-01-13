using System;
using System.Numerics;

namespace Andastra.Game.Graphics.MonoGame.Interfaces
{
    /// <summary>
    /// Command list interface for recording rendering commands.
    /// Follows NVRHI patterns for cross-API compatibility.
    ///
    /// Usage:
    /// 1. Open() - begin recording
    /// 2. Record commands (set state, draw, dispatch, etc.)
    /// 3. Close() - end recording
    /// 4. Submit to device via ExecuteCommandList()
    /// </summary>
    public interface ICommandList : IDisposable
    {
        /// <summary>
        /// Opens the command list for recording.
        /// </summary>
        void Open();

        /// <summary>
        /// Closes the command list after recording.
        /// </summary>
        void Close();

        #region Resource Operations

        /// <summary>
        /// Writes data to a buffer.
        /// </summary>
        void WriteBuffer(IBuffer buffer, byte[] data, int destOffset = 0);

        /// <summary>
        /// Writes typed data to a buffer.
        /// </summary>
        void WriteBuffer<T>(IBuffer buffer, T[] data, int destOffset = 0) where T : unmanaged;

        /// <summary>
        /// Writes texture data.
        /// </summary>
        void WriteTexture(ITexture texture, int mipLevel, int arraySlice, byte[] data);

        /// <summary>
        /// Copies a buffer region.
        /// </summary>
        void CopyBuffer(IBuffer dest, int destOffset, IBuffer src, int srcOffset, int size);

        /// <summary>
        /// Copies a texture.
        /// </summary>
        void CopyTexture(ITexture dest, ITexture src);

        /// <summary>
        /// Clears a color attachment.
        /// </summary>
        void ClearColorAttachment(IFramebuffer framebuffer, int attachmentIndex, Vector4 color);

        /// <summary>
        /// Clears depth/stencil attachment.
        /// </summary>
        void ClearDepthStencilAttachment(IFramebuffer framebuffer, float depth, byte stencil, bool clearDepth = true, bool clearStencil = true);

        /// <summary>
        /// Clears an unordered access view (UAV) to a float value.
        /// </summary>
        void ClearUAVFloat(ITexture texture, Vector4 value);

        /// <summary>
        /// Clears an unordered access view (UAV) to an integer value.
        /// </summary>
        void ClearUAVUint(ITexture texture, uint value);

        #endregion

        #region Resource State Transitions

        /// <summary>
        /// Transitions a texture to a new resource state.
        /// </summary>
        void SetTextureState(ITexture texture, ResourceState state);

        /// <summary>
        /// Transitions a buffer to a new resource state.
        /// </summary>
        void SetBufferState(IBuffer buffer, ResourceState state);

        /// <summary>
        /// Commits pending resource barriers.
        /// </summary>
        void CommitBarriers();

        /// <summary>
        /// Inserts a UAV barrier.
        /// </summary>
        void UAVBarrier(ITexture texture);

        /// <summary>
        /// Inserts a UAV barrier for a buffer.
        /// </summary>
        void UAVBarrier(IBuffer buffer);

        #endregion

        #region Graphics State

        /// <summary>
        /// Sets the complete graphics state.
        /// </summary>
        void SetGraphicsState(GraphicsState state);

        /// <summary>
        /// Sets the viewport.
        /// </summary>
        void SetViewport(Viewport viewport);

        /// <summary>
        /// Sets multiple viewports.
        /// </summary>
        void SetViewports(Viewport[] viewports);

        /// <summary>
        /// Sets the scissor rectangle.
        /// </summary>
        void SetScissor(Rectangle scissor);

        /// <summary>
        /// Sets multiple scissor rectangles.
        /// </summary>
        void SetScissors(Rectangle[] scissors);

        /// <summary>
        /// Sets the blend constant color.
        /// </summary>
        void SetBlendConstant(Vector4 color);

        /// <summary>
        /// Sets the stencil reference value.
        /// </summary>
        void SetStencilRef(uint reference);

        #endregion

        #region Draw Commands

        /// <summary>
        /// Draws non-indexed primitives.
        /// </summary>
        void Draw(DrawArguments args);

        /// <summary>
        /// Draws indexed primitives.
        /// </summary>
        void DrawIndexed(DrawArguments args);

        /// <summary>
        /// Draws indirect (arguments from buffer).
        /// </summary>
        void DrawIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride);

        /// <summary>
        /// Draws indexed indirect (arguments from buffer).
        /// </summary>
        void DrawIndexedIndirect(IBuffer argumentBuffer, int offset, int drawCount, int stride);

        #endregion

        #region Compute State

        /// <summary>
        /// Sets the compute state.
        /// </summary>
        void SetComputeState(ComputeState state);

        /// <summary>
        /// Dispatches compute work.
        /// </summary>
        void Dispatch(int groupCountX, int groupCountY = 1, int groupCountZ = 1);

        /// <summary>
        /// Dispatches compute work with indirect arguments.
        /// </summary>
        void DispatchIndirect(IBuffer argumentBuffer, int offset);

        #endregion

        #region Raytracing Commands

        /// <summary>
        /// Sets the raytracing state.
        /// </summary>
        void SetRaytracingState(RaytracingState state);

        /// <summary>
        /// Dispatches rays.
        /// </summary>
        void DispatchRays(DispatchRaysArguments args);

        /// <summary>
        /// Builds a bottom-level acceleration structure.
        /// </summary>
        void BuildBottomLevelAccelStruct(IAccelStruct accelStruct, GeometryDesc[] geometries);

        /// <summary>
        /// Builds a top-level acceleration structure.
        /// </summary>
        void BuildTopLevelAccelStruct(IAccelStruct accelStruct, AccelStructInstance[] instances);

        /// <summary>
        /// Compacts an acceleration structure.
        /// </summary>
        void CompactBottomLevelAccelStruct(IAccelStruct dest, IAccelStruct src);

        /// <summary>
        /// Pushes descriptor set bindings directly into the command buffer without creating a binding set.
        /// This is more efficient than creating and binding descriptor sets when descriptors change frequently.
        /// Only works if the binding layout was created with IsPushDescriptor = true.
        ///
        /// Based on Vulkan VK_KHR_push_descriptor extension:
        /// https://www.khronos.org/registry/vulkan/specs/1.3-extensions/man/html/vkCmdPushDescriptorSetKHR.html
        ///
        /// DirectX 12 equivalent: Uses root descriptors or updates descriptor tables directly.
        /// </summary>
        /// <param name="bindingLayout">The binding layout that describes the descriptor set layout. Must have IsPushDescriptor = true.</param>
        /// <param name="setIndex">The descriptor set index to push (typically 0 for the first set).</param>
        /// <param name="items">The binding items to push. These will be bound directly without creating a descriptor set.</param>
        void PushDescriptorSet(IBindingLayout bindingLayout, int setIndex, BindingSetItem[] items);

        #endregion

        #region Debug

        /// <summary>
        /// Begins a debug event region.
        /// </summary>
        void BeginDebugEvent(string name, Vector4 color);

        /// <summary>
        /// Ends a debug event region.
        /// </summary>
        void EndDebugEvent();

        /// <summary>
        /// Inserts a debug marker.
        /// </summary>
        void InsertDebugMarker(string name, Vector4 color);

        #endregion
    }

    /// <summary>
    /// Graphics state for draw commands.
    /// </summary>
    public struct GraphicsState
    {
        public IGraphicsPipeline Pipeline;
        public IFramebuffer Framebuffer;
        public ViewportState Viewport;
        public IBindingSet[] BindingSets;
        public IBuffer[] VertexBuffers;
        public IBuffer IndexBuffer;
        public TextureFormat IndexFormat;

        public GraphicsState SetPipeline(IGraphicsPipeline p) { Pipeline = p; return this; }
        public GraphicsState SetFramebuffer(IFramebuffer f) { Framebuffer = f; return this; }
        public GraphicsState SetViewport(ViewportState v) { Viewport = v; return this; }
        public GraphicsState AddBindingSet(IBindingSet b)
        {
            if (BindingSets == null) BindingSets = new IBindingSet[] { b };
            else
            {
                var newArr = new IBindingSet[BindingSets.Length + 1];
                Array.Copy(BindingSets, newArr, BindingSets.Length);
                newArr[BindingSets.Length] = b;
                BindingSets = newArr;
            }
            return this;
        }
        public GraphicsState AddVertexBuffer(IBuffer b)
        {
            if (VertexBuffers == null) VertexBuffers = new IBuffer[] { b };
            else
            {
                var newArr = new IBuffer[VertexBuffers.Length + 1];
                Array.Copy(VertexBuffers, newArr, VertexBuffers.Length);
                newArr[VertexBuffers.Length] = b;
                VertexBuffers = newArr;
            }
            return this;
        }
        public GraphicsState SetIndexBuffer(IBuffer b, TextureFormat format = TextureFormat.R32_UInt)
        {
            IndexBuffer = b;
            IndexFormat = format;
            return this;
        }
    }

    /// <summary>
    /// Compute state for dispatch commands.
    /// </summary>
    public struct ComputeState
    {
        public IComputePipeline Pipeline;
        public IBindingSet[] BindingSets;

        public ComputeState SetPipeline(IComputePipeline p) { Pipeline = p; return this; }
        public ComputeState AddBindingSet(IBindingSet b)
        {
            if (BindingSets == null) BindingSets = new IBindingSet[] { b };
            else
            {
                var newArr = new IBindingSet[BindingSets.Length + 1];
                Array.Copy(BindingSets, newArr, BindingSets.Length);
                newArr[BindingSets.Length] = b;
                BindingSets = newArr;
            }
            return this;
        }
    }

    /// <summary>
    /// Raytracing state for dispatch rays commands.
    /// </summary>
    public struct RaytracingState
    {
        public IRaytracingPipeline Pipeline;
        public IBindingSet[] BindingSets;
        public ShaderBindingTable ShaderTable;
    }

    /// <summary>
    /// Viewport state combining viewport and scissor.
    /// </summary>
    public struct ViewportState
    {
        public Viewport[] Viewports;
        public Rectangle[] Scissors;

        public ViewportState AddViewport(Viewport v)
        {
            if (Viewports == null) Viewports = new Viewport[] { v };
            else
            {
                var newArr = new Viewport[Viewports.Length + 1];
                Array.Copy(Viewports, newArr, Viewports.Length);
                newArr[Viewports.Length] = v;
                Viewports = newArr;
            }
            return this;
        }

        public ViewportState AddViewportAndScissorRect(Viewport v)
        {
            AddViewport(v);
            if (Scissors == null) Scissors = new Rectangle[] { new Rectangle { X = (int)v.X, Y = (int)v.Y, Width = (int)v.Width, Height = (int)v.Height } };
            else
            {
                var newArr = new Rectangle[Scissors.Length + 1];
                Array.Copy(Scissors, newArr, Scissors.Length);
                newArr[Scissors.Length] = new Rectangle { X = (int)v.X, Y = (int)v.Y, Width = (int)v.Width, Height = (int)v.Height };
                Scissors = newArr;
            }
            return this;
        }
    }

    /// <summary>
    /// Viewport definition.
    /// </summary>
    public struct Viewport
    {
        public float X, Y, Width, Height, MinDepth, MaxDepth;

        public Viewport(float width, float height)
        {
            X = 0; Y = 0; Width = width; Height = height; MinDepth = 0; MaxDepth = 1;
        }

        public Viewport(float x, float y, float width, float height, float minDepth = 0, float maxDepth = 1)
        {
            X = x; Y = y; Width = width; Height = height; MinDepth = minDepth; MaxDepth = maxDepth;
        }
    }

    /// <summary>
    /// Rectangle for scissor testing.
    /// </summary>
    public struct Rectangle
    {
        public int X, Y, Width, Height;
    }

    /// <summary>
    /// Draw call arguments.
    /// </summary>
    public struct DrawArguments
    {
        public int VertexCount;
        public int InstanceCount;
        public int StartVertexLocation;
        public int StartInstanceLocation;
        public int StartIndexLocation;
        public int BaseVertexLocation;

        public DrawArguments SetVertexCount(int n) { VertexCount = n; return this; }
        public DrawArguments SetInstanceCount(int n) { InstanceCount = n; return this; }
        public DrawArguments SetStartVertex(int n) { StartVertexLocation = n; return this; }
        public DrawArguments SetStartInstance(int n) { StartInstanceLocation = n; return this; }
        public DrawArguments SetStartIndex(int n) { StartIndexLocation = n; return this; }
        public DrawArguments SetBaseVertex(int n) { BaseVertexLocation = n; return this; }
    }

    /// <summary>
    /// Dispatch rays arguments.
    /// </summary>
    public struct DispatchRaysArguments
    {
        public int Width;
        public int Height;
        public int Depth;
    }

    /// <summary>
    /// Shader binding table for raytracing.
    /// </summary>
    public struct ShaderBindingTable
    {
        public IBuffer Buffer;
        public ulong RayGenOffset;
        public ulong RayGenSize;
        public ulong MissOffset;
        public ulong MissStride;
        public ulong MissSize;
        public ulong HitGroupOffset;
        public ulong HitGroupStride;
        public ulong HitGroupSize;
        public ulong CallableOffset;
        public ulong CallableStride;
        public ulong CallableSize;
    }

    /// <summary>
    /// Acceleration structure instance for TLAS building.
    /// Matches VkAccelerationStructureInstanceKHR layout.
    /// </summary>
    public struct AccelStructInstance
    {
        /// <summary>
        /// 3x4 row-major transform matrix.
        /// </summary>
        public Matrix3x4 Transform;

        /// <summary>
        /// 24-bit instance custom index for ray shaders.
        /// </summary>
        public uint InstanceCustomIndex;

        /// <summary>
        /// 8-bit visibility mask.
        /// </summary>
        public byte Mask;

        /// <summary>
        /// 24-bit shader binding table offset.
        /// </summary>
        public uint InstanceShaderBindingTableRecordOffset;

        /// <summary>
        /// 8-bit instance flags.
        /// </summary>
        public AccelStructInstanceFlags Flags;

        /// <summary>
        /// Device address of the referenced BLAS.
        /// </summary>
        public ulong AccelerationStructureReference;
    }

    /// <summary>
    /// 3x4 transform matrix matching Vulkan's VkTransformMatrixKHR.
    /// Row-major, 3 rows x 4 columns.
    /// </summary>
    public struct Matrix3x4
    {
        public float M11, M12, M13, M14;
        public float M21, M22, M23, M24;
        public float M31, M32, M33, M34;

        public static Matrix3x4 Identity
        {
            get
            {
                return new Matrix3x4
                {
                    M11 = 1,
                    M12 = 0,
                    M13 = 0,
                    M14 = 0,
                    M21 = 0,
                    M22 = 1,
                    M23 = 0,
                    M24 = 0,
                    M31 = 0,
                    M32 = 0,
                    M33 = 1,
                    M34 = 0
                };
            }
        }

        public static Matrix3x4 FromMatrix4x4(Matrix4x4 m)
        {
            return new Matrix3x4
            {
                M11 = m.M11,
                M12 = m.M12,
                M13 = m.M13,
                M14 = m.M14,
                M21 = m.M21,
                M22 = m.M22,
                M23 = m.M23,
                M24 = m.M24,
                M31 = m.M31,
                M32 = m.M32,
                M33 = m.M33,
                M34 = m.M34
            };
        }
    }

    /// <summary>
    /// Acceleration structure instance flags.
    /// </summary>
    [Flags]
    public enum AccelStructInstanceFlags : byte
    {
        None = 0,
        TriangleFacingCullDisable = 1,
        TriangleFrontCounterClockwise = 2,
        ForceOpaque = 4,
        ForceNoOpaque = 8
    }

    /// <summary>
    /// Additional texture format for R32_UInt index buffers.
    /// </summary>
    public static class TextureFormatExtensions
    {
        public const TextureFormat R32_UInt = (TextureFormat)1000;
        public const TextureFormat R16_UInt = (TextureFormat)1001;
    }
}

