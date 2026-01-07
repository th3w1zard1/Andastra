using System;

namespace Andastra.Runtime.Graphics.Common.Enums
{
    /// <summary>
    /// Shader stages in the graphics pipeline.
    /// Based on D3D12_SHADER_STAGE and Vulkan VkShaderStageFlagBits.
    /// </summary>
    /// <remarks>
    /// Shader Stage Enumeration:
    /// - This enum represents modern shader pipeline stages
    /// - Original game graphics system: Primarily DirectX 9 (d3d9.dll @ 0x0080a6c0) or OpenGL (OPENGL32.dll @ 0x00809ce2)
    /// - Located via string references: "Render Window" @ 0x007b5680, "Graphics Options" @ 0x007b56a8
    /// - Original game shaders: Vertex shaders (!!VP1.0 @ 0x0081c228), pixel shaders (fixed-function and programmable)
    /// - Original game did not support geometry shaders, tessellation, compute shaders, or raytracing shaders
    /// - This enum: Represents modern shader stages for advanced graphics APIs, not directly mapped to swkotor2.exe functions
    /// </remarks>
    [Flags]
    public enum ShaderStage
    {
        /// <summary>
        /// No shader stage.
        /// </summary>
        None = 0,

        /// <summary>
        /// Vertex shader stage.
        /// </summary>
        Vertex = 1 << 0,

        /// <summary>
        /// Hull shader stage (tessellation control).
        /// </summary>
        Hull = 1 << 1,

        /// <summary>
        /// Domain shader stage (tessellation evaluation).
        /// </summary>
        Domain = 1 << 2,

        /// <summary>
        /// Geometry shader stage.
        /// </summary>
        Geometry = 1 << 3,

        /// <summary>
        /// Pixel shader stage (fragment shader).
        /// </summary>
        Pixel = 1 << 4,

        /// <summary>
        /// Compute shader stage.
        /// </summary>
        Compute = 1 << 5,

        /// <summary>
        /// Amplification shader stage (mesh shader pipeline).
        /// </summary>
        Amplification = 1 << 6,

        /// <summary>
        /// Mesh shader stage (mesh shader pipeline).
        /// </summary>
        Mesh = 1 << 7,

        /// <summary>
        /// Ray generation shader (raytracing).
        /// </summary>
        RayGeneration = 1 << 8,

        /// <summary>
        /// Intersection shader (raytracing).
        /// </summary>
        Intersection = 1 << 9,

        /// <summary>
        /// Any hit shader (raytracing).
        /// </summary>
        AnyHit = 1 << 10,

        /// <summary>
        /// Closest hit shader (raytracing).
        /// </summary>
        ClosestHit = 1 << 11,

        /// <summary>
        /// Miss shader (raytracing).
        /// </summary>
        Miss = 1 << 12,

        /// <summary>
        /// Callable shader (raytracing).
        /// </summary>
        Callable = 1 << 13,

        /// <summary>
        /// All graphics stages (vertex, hull, domain, geometry, pixel).
        /// </summary>
        AllGraphics = Vertex | Hull | Domain | Geometry | Pixel,

        /// <summary>
        /// All shader stages.
        /// </summary>
        All = AllGraphics | Compute | Amplification | Mesh | RayGeneration | Intersection | AnyHit | ClosestHit | Miss | Callable
    }
}

