using System;

namespace BioWareEngines.Graphics
{
    /// <summary>
    /// Graphics backend type enumeration.
    /// </summary>
    /// <remarks>
    /// Graphics Backend Type:
    /// - Based on swkotor2.exe graphics initialization system
    /// - Located via string references: "Graphics Options" @ 0x007b56a8, "BTN_GRAPHICS" @ 0x007d0d8c, "optgraphics_p" @ 0x007d2064
    /// - "2D3DBias" @ 0x007c612c, "2D3D Bias" @ 0x007c71f8 (graphics settings)
    /// - Original game uses DirectX 8/9 for rendering (D3D8.dll, D3D9.dll)
    /// - This enumeration: Defines modern graphics backend types (MonoGame, Stride) for abstraction layer
    /// - Note: MonoGame and Stride are modern graphics frameworks, not present in original game
    /// - Original game rendering: DirectX 8/9 fixed-function pipeline
    /// </remarks>
    public enum GraphicsBackendType
    {
        /// <summary>
        /// MonoGame backend (DesktopGL, DirectX, etc.)
        /// </summary>
        MonoGame,

        /// <summary>
        /// Stride 3D engine backend.
        /// </summary>
        Stride
    }

}

