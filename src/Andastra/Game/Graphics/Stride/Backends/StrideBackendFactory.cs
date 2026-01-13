using System;
using Andastra.Runtime.Graphics.Common.Backends;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Stride.Graphics;
using Stride.Engine;
using Stride.Graphics;

namespace Andastra.Game.Stride.Backends
{
    /// <summary>
    /// Factory for creating Stride graphics backends.
    /// Selects appropriate backend based on platform and settings.
    ///
    /// Supports:
    /// - DirectX 11 (Windows default)
    /// - DirectX 12 (Windows, with raytracing)
    /// - Vulkan (Windows, Linux, with raytracing)
    /// </summary>
    /// <remarks>
    /// Graphics Backend Factory:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) graphics initialization system
    /// - Original game uses DirectX 8/9 for rendering (D3D8.dll, D3D9.dll)
    /// - Located via string references: "Graphics Options" @ 0x007b56a8, "BTN_GRAPHICS" @ 0x007d0d8c, "optgraphics_p" @ 0x007d2064
    /// - "2D3DBias" @ 0x007c612c, "2D3D Bias" @ 0x007c71f8 (graphics settings)
    /// - Original implementation: Initializes DirectX device, sets up rendering pipeline
    /// - This implementation: Modern graphics backend factory supporting DirectX 11/12, Vulkan, and modern enhancements (DLSS, FSR, raytracing)
    /// - Note: DirectX 11/12, Vulkan, DLSS, FSR, and raytracing are modern enhancements not present in original game
    /// - Original game rendering: DirectX 8/9 fixed-function pipeline, no modern post-processing or upscaling
    /// </remarks>
    public static class StrideBackendFactory
    {
        /// <summary>
        /// Creates the appropriate backend for the current platform and settings.
        /// </summary>
        public static ILowLevelBackend Create(BioWare.NET.Common.BioWareGame game, RenderSettings settings)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            if (settings == null)
            {
                settings = new RenderSettings();
            }

            var preferredBackend = settings.PreferredBackend;

            // Auto-select based on platform and raytracing requirements
            if (preferredBackend == GraphicsBackendType.Auto)
            {
                preferredBackend = SelectOptimalBackend(settings);
            }

            return CreateBackend(game, preferredBackend, settings);
        }

        /// <summary>
        /// Selects the optimal backend based on settings and hardware.
        /// </summary>
        private static GraphicsBackendType SelectOptimalBackend(RenderSettings settings)
        {
            // If raytracing is requested, prefer DX12 or Vulkan
            if (settings.Raytracing != RaytracingLevel.Disabled)
            {
                // Try DX12 first on Windows (better DXR support)
                if (IsWindows() && IsDx12Available())
                {
                    return GraphicsBackendType.Direct3D12;
                }

                // Fall back to Vulkan if available
                if (IsVulkanAvailable())
                {
                    return GraphicsBackendType.Vulkan;
                }
            }

            // If Remix is requested, we need DX9 compatibility
            if (settings.RemixCompatibility)
            {
                return GraphicsBackendType.Direct3D9Remix;
            }

            // Default selection based on platform
            if (IsWindows())
            {
                // DX11 for best compatibility on Windows
                return GraphicsBackendType.Direct3D11;
            }

            // Vulkan for non-Windows platforms
            return GraphicsBackendType.Vulkan;
        }

        /// <summary>
        /// Creates a specific backend type.
        /// </summary>
        private static ILowLevelBackend CreateBackend(BioWare.NET.Common.BioWareGame game, GraphicsBackendType backendType, RenderSettings settings)
        {
            Console.WriteLine($"[StrideBackendFactory] Creating {backendType} backend");

            // Create a Stride.Engine.Game instance for the backends
            // The backends need a game instance, not the BioWareGame enum
            var strideGame = new StrideGameWrapper();

            switch (backendType)
            {
                case GraphicsBackendType.Direct3D12:
                    return new StrideDirect3D12Backend(strideGame);

                case GraphicsBackendType.Vulkan:
                    return new StrideVulkanBackend(strideGame);

                case GraphicsBackendType.Direct3D11:
                default:
                    return new StrideDirect3D11Backend(strideGame);
            }
        }

        #region Platform Detection

        private static bool IsWindows()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }

        private static bool IsDx12Available()
        {
            // Check Windows 10 or later
            var version = Environment.OSVersion.Version;
            return version.Major >= 10;
        }

        private static bool IsVulkanAvailable()
        {
            // Would check for Vulkan SDK/runtime availability
            return true;
        }

        #endregion

        /// <summary>
        /// Gets supported backends for the current platform.
        /// </summary>
        public static GraphicsBackendType[] GetSupportedBackends()
        {
            if (IsWindows())
            {
                return new[]
                {
                    GraphicsBackendType.Direct3D11,
                    GraphicsBackendType.Direct3D12,
                    GraphicsBackendType.Vulkan
                };
            }

            // Linux/macOS
            return new[]
            {
                GraphicsBackendType.Vulkan
            };
        }

        /// <summary>
        /// Gets backends that support raytracing.
        /// </summary>
        public static GraphicsBackendType[] GetRaytracingBackends()
        {
            return new[]
            {
                GraphicsBackendType.Direct3D12,
                GraphicsBackendType.Vulkan
            };
        }
    }
}

