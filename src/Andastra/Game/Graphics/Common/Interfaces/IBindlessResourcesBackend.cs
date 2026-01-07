using System;
using Andastra.Runtime.Graphics.Common.Enums;

namespace Andastra.Runtime.Graphics.Common.Interfaces
{
    /// <summary>
    /// Interface for graphics backends that support bindless resources (DirectX 12, Vulkan descriptor indexing).
    /// Bindless resources allow shaders to access large arrays of textures/samplers without explicit binding,
    /// enabling GPU-driven rendering and more flexible resource management.
    ///
    /// Based on DirectX 12 Bindless Resources: https://devblogs.microsoft.com/directx/in-the-works-hlsl-shader-model-6-6/
    /// Based on Vulkan Descriptor Indexing: https://www.khronos.org/registry/vulkan/specs/1.3-extensions/html/vkspec.html#descriptorsets-updates
    /// </summary>
    /// <remarks>
    /// Bindless Resources Backend Interface:
    /// - This is a modern graphics API feature (DirectX 12, Vulkan)
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8
    /// - Original game did not support bindless resources; this is a modern enhancement for GPU-driven rendering
    /// - This interface: Provides bindless resources abstraction for modern graphics APIs, not directly mapped to swkotor2.exe functions
    /// </remarks>
    public interface IBindlessResourcesBackend : ILowLevelBackend
    {
        /// <summary>
        /// Whether bindless resources are available and supported.
        /// </summary>
        bool BindlessResourcesAvailable { get; }

        /// <summary>
        /// Maximum number of bindless resources that can be accessed by a shader.
        /// </summary>
        int MaxBindlessResources { get; }

        /// <summary>
        /// Creates a bindless resource heap/descriptor set for textures.
        /// </summary>
        /// <param name="capacity">Maximum number of textures that can be stored.</param>
        /// <returns>Handle to the created bindless heap.</returns>
        IntPtr CreateBindlessTextureHeap(int capacity);

        /// <summary>
        /// Creates a bindless resource heap/descriptor set for samplers.
        /// </summary>
        /// <param name="capacity">Maximum number of samplers that can be stored.</param>
        /// <returns>Handle to the created bindless heap.</returns>
        IntPtr CreateBindlessSamplerHeap(int capacity);

        /// <summary>
        /// Adds a texture to the bindless heap and returns its index.
        /// </summary>
        /// <param name="heap">Bindless texture heap handle.</param>
        /// <param name="texture">Texture handle to add.</param>
        /// <returns>Index in the heap that can be used in shaders.</returns>
        int AddBindlessTexture(IntPtr heap, IntPtr texture);

        /// <summary>
        /// Adds a sampler to the bindless heap and returns its index.
        /// </summary>
        /// <param name="heap">Bindless sampler heap handle.</param>
        /// <param name="sampler">Sampler handle to add.</param>
        /// <returns>Index in the heap that can be used in shaders.</returns>
        int AddBindlessSampler(IntPtr heap, IntPtr sampler);

        /// <summary>
        /// Removes a texture from the bindless heap (marks the slot as invalid).
        /// </summary>
        /// <param name="heap">Bindless texture heap handle.</param>
        /// <param name="index">Index in the heap to remove.</param>
        void RemoveBindlessTexture(IntPtr heap, int index);

        /// <summary>
        /// Removes a sampler from the bindless heap (marks the slot as invalid).
        /// </summary>
        /// <param name="heap">Bindless sampler heap handle.</param>
        /// <param name="index">Index in the heap to remove.</param>
        void RemoveBindlessSampler(IntPtr heap, int index);

        /// <summary>
        /// Binds a bindless heap to a shader stage for use in shaders.
        /// </summary>
        /// <param name="heap">Bindless heap handle (texture or sampler).</param>
        /// <param name="slot">Root parameter/descriptor set slot.</param>
        /// <param name="stage">Shader stage to bind to.</param>
        void SetBindlessHeap(IntPtr heap, int slot, ShaderStage stage);
    }
}

