using System;

namespace Odyssey.Graphics.Common.Enums
{
    /// <summary>
    /// Shader stages in the graphics pipeline.
    /// Based on D3D12_SHADER_STAGE and Vulkan VkShaderStageFlagBits.
    /// </summary>
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

