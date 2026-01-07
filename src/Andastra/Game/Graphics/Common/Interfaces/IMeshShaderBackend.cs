using System;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Runtime.Graphics.Common.Interfaces
{
    /// <summary>
    /// Interface for graphics backends that support mesh shaders (DirectX 12 Ultimate, Vulkan Mesh Shaders).
    /// Mesh shaders replace the traditional vertex/geometry shader pipeline with a more flexible compute-like model.
    ///
    /// Based on DirectX 12 Mesh Shaders: https://devblogs.microsoft.com/directx/coming-to-directx-12-mesh-shaders-and-amplification-shaders-reinventing-the-geometry-pipeline/
    /// </summary>
    public interface IMeshShaderBackend : ILowLevelBackend
    {
        /// <summary>
        /// Whether mesh shaders are available and supported.
        /// </summary>
        bool MeshShadersAvailable { get; }

        /// <summary>
        /// Creates a mesh shader pipeline state object.
        /// </summary>
        /// <param name="amplificationShader">Amplification shader bytecode (optional, can be null).</param>
        /// <param name="meshShader">Mesh shader bytecode.</param>
        /// <param name="pixelShader">Pixel shader bytecode.</param>
        /// <param name="desc">Additional pipeline state description.</param>
        /// <returns>Handle to the created pipeline.</returns>
        IntPtr CreateMeshShaderPipeline(byte[] amplificationShader, byte[] meshShader,
            byte[] pixelShader, MeshPipelineDescription desc);

        /// <summary>
        /// Dispatches mesh shaders (amplification + mesh + pixel pipeline).
        /// </summary>
        /// <param name="pipeline">Mesh shader pipeline handle.</param>
        /// <param name="threadGroupCountX">Number of thread groups in X dimension.</param>
        /// <param name="threadGroupCountY">Number of thread groups in Y dimension.</param>
        /// <param name="threadGroupCountZ">Number of thread groups in Z dimension.</param>
        void DispatchMesh(int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ);

        /// <summary>
        /// Dispatches mesh shaders with indirect dispatch (GPU-driven).
        /// </summary>
        /// <param name="pipeline">Mesh shader pipeline handle.</param>
        /// <param name="indirectBuffer">Buffer containing indirect dispatch arguments.</param>
        /// <param name="offset">Offset into the indirect buffer.</param>
        void DispatchMeshIndirect(IntPtr indirectBuffer, int offset);
    }

    /// <summary>
    /// Mesh shader pipeline description.
    /// </summary>
    public struct MeshPipelineDescription
    {
        public BlendStateDesc BlendState;
        public RasterizerStateDesc RasterizerState;
        public DepthStencilStateDesc DepthStencilState;
        public PrimitiveTopology OutputTopology;
        public int MaxVertexCount;
        public int MaxPrimitiveCount;
        public int PayloadSizeInBytes;
        public string DebugName;
    }
}

