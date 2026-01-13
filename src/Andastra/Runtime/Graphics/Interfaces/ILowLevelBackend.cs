using System;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Runtime.Graphics.Common.Interfaces
{
    /// <summary>
    /// Low-level graphics backend interface for API-specific implementations.
    /// This is the core abstraction for DirectX 9/10/11/12, Vulkan, OpenGL, Metal.
    /// 
    /// Based on modern graphics API patterns:
    /// - DirectX 12: https://docs.microsoft.com/en-us/windows/win32/direct3d12/
    /// - Vulkan: https://www.khronos.org/vulkan/
    /// - Metal: https://developer.apple.com/metal/
    /// </summary>
    /// <remarks>
    /// Low-Level Backend Interface:
    /// - This is an abstraction layer for modern graphics APIs (DirectX 11/12, Vulkan, OpenGL, Metal)
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Graphics initialization: 0x00404250 @ 0x00404250 (main game loop, WinMain equivalent) handles graphics setup
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8, "2D3DBias" @ 0x007c612c
    /// - Original game graphics device: glClear @ 0x0080a9c0, glViewport @ 0x0080a9d8, glDrawArrays @ 0x0080aab6, glDrawElements @ 0x0080aafe
    /// - This interface: Provides unified abstraction for modern graphics APIs, not directly mapped to swkotor2.exe functions
    /// </remarks>
    public interface ILowLevelBackend : IDisposable
    {
        /// <summary>
        /// Gets the active backend type.
        /// </summary>
        GraphicsBackendType BackendType { get; }

        /// <summary>
        /// Gets the hardware capabilities.
        /// </summary>
        GraphicsCapabilities Capabilities { get; }

        /// <summary>
        /// Gets whether the backend is initialized and ready.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Queries whether raytracing is available and enabled.
        /// </summary>
        bool IsRaytracingEnabled { get; }

        /// <summary>
        /// Initializes the graphics backend with the specified settings.
        /// </summary>
        /// <param name="settings">Renderer settings.</param>
        /// <returns>True if initialization succeeded.</returns>
        bool Initialize(RenderSettings settings);

        /// <summary>
        /// Shuts down the graphics backend.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Begins a new frame for rendering.
        /// </summary>
        void BeginFrame();

        /// <summary>
        /// Ends the current frame and presents to screen.
        /// </summary>
        void EndFrame();

        /// <summary>
        /// Resizes the swap chain / render targets.
        /// </summary>
        /// <param name="width">New width in pixels.</param>
        /// <param name="height">New height in pixels.</param>
        void Resize(int width, int height);

        /// <summary>
        /// Creates a texture resource.
        /// </summary>
        /// <param name="desc">Texture description.</param>
        /// <returns>Handle to the created texture.</returns>
        IntPtr CreateTexture(TextureDescription desc);

        /// <summary>
        /// Creates a buffer resource.
        /// </summary>
        /// <param name="desc">Buffer description.</param>
        /// <returns>Handle to the created buffer.</returns>
        IntPtr CreateBuffer(BufferDescription desc);

        /// <summary>
        /// Creates a shader pipeline.
        /// </summary>
        /// <param name="desc">Pipeline description.</param>
        /// <returns>Handle to the created pipeline.</returns>
        IntPtr CreatePipeline(PipelineDescription desc);

        /// <summary>
        /// Destroys a resource by handle.
        /// </summary>
        /// <param name="handle">Resource handle.</param>
        void DestroyResource(IntPtr handle);

        /// <summary>
        /// Enables or disables raytracing features.
        /// </summary>
        /// <param name="level">Raytracing feature level.</param>
        void SetRaytracingLevel(RaytracingLevel level);

        /// <summary>
        /// Gets performance statistics for the last frame.
        /// </summary>
        FrameStatistics GetFrameStatistics();
    }

    /// <summary>
    /// Extended backend interface for raytracing-capable APIs (DX12, Vulkan RT).
    /// </summary>
    public interface IRaytracingBackend : ILowLevelBackend
    {
        /// <summary>
        /// Creates a bottom-level acceleration structure for raytracing.
        /// </summary>
        IntPtr CreateBlas(MeshGeometry geometry);

        /// <summary>
        /// Creates a top-level acceleration structure.
        /// </summary>
        IntPtr CreateTlas(int maxInstances);

        /// <summary>
        /// Creates a raytracing pipeline state object.
        /// </summary>
        IntPtr CreateRaytracingPso(RaytracingPipelineDesc desc);

        /// <summary>
        /// Dispatches raytracing work.
        /// </summary>
        void DispatchRays(DispatchRaysDesc desc);

        /// <summary>
        /// Updates an instance transform in the TLAS.
        /// </summary>
        void UpdateTlasInstance(IntPtr tlas, int instanceIndex, System.Numerics.Matrix4x4 transform);
    }

    /// <summary>
    /// Extended backend interface for compute-capable APIs.
    /// </summary>
    public interface IComputeBackend : ILowLevelBackend
    {
        /// <summary>
        /// Dispatches compute shader work.
        /// Based on D3D11/D3D12/Vulkan compute dispatch.
        /// </summary>
        void Dispatch(int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ);

        /// <summary>
        /// Creates a structured buffer for compute shaders.
        /// </summary>
        IntPtr CreateStructuredBuffer(int elementCount, int elementStride, bool cpuWritable);

        /// <summary>
        /// Maps a buffer for CPU access.
        /// </summary>
        IntPtr MapBuffer(IntPtr bufferHandle, MapType mapType);

        /// <summary>
        /// Unmaps a previously mapped buffer.
        /// </summary>
        void UnmapBuffer(IntPtr bufferHandle);
    }

    /// <summary>
    /// Raytracing pipeline description.
    /// </summary>
    public struct RaytracingPipelineDesc
    {
        public byte[] RayGenShader;
        public byte[] MissShader;
        public byte[] ClosestHitShader;
        public byte[] AnyHitShader;
        public int MaxPayloadSize;
        public int MaxAttributeSize;
        public int MaxRecursionDepth;
        public string DebugName;
    }

    /// <summary>
    /// Dispatch rays description.
    /// </summary>
    public struct DispatchRaysDesc
    {
        public IntPtr RayGenShaderTable;
        public IntPtr MissShaderTable;
        public IntPtr HitGroupTable;
        public int Width;
        public int Height;
        public int Depth;
    }
}

