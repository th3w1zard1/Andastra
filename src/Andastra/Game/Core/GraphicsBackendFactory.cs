using System;
using Andastra.Runtime.Core;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Backends.Odyssey;
using Andastra.Runtime.Graphics.Common.Enums;

namespace Andastra.Game.Game.Core
{
    /// <summary>
    /// Factory for creating graphics backend instances.
    /// Located in Odyssey.Game to avoid circular dependencies in the abstraction layer.
    /// </summary>
    /// <remarks>
    /// Graphics Backend Factory:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) graphics initialization system
    /// - Original game uses DirectX 8/9 for rendering (D3D8.dll, D3D9.dll)
    /// - Located via string references: "Graphics Options" @ 0x007b56a8, "BTN_GRAPHICS" @ 0x007d0d8c, "optgraphics_p" @ 0x007d2064
    /// - "2D3DBias" @ 0x007c612c, "2D3D Bias" @ 0x007c71f8 (graphics settings)
    /// - Original implementation: Initializes DirectX device, sets up rendering pipeline
    /// - This implementation: Factory for creating modern graphics backends (MonoGame, Stride) and original engine backends (OdysseyEngine)
    /// - Note: MonoGame and Stride are modern graphics frameworks, not present in original game
    /// - Original game rendering: DirectX 8/9 fixed-function pipeline
    /// - OdysseyEngine: Matches original game rendering exactly 1:1 (Kotor1GraphicsBackend for K1, Kotor2GraphicsBackend for K2)
    /// </remarks>
    public static class GraphicsBackendFactory
    {
        /// <summary>
        /// Creates a graphics backend of the specified type.
        /// </summary>
        /// <param name="backendType">The backend type to create.</param>
        /// <param name="gameType">Optional game type for OdysseyEngine backend (K1 or K2). Required when backendType is OdysseyEngine.</param>
        /// <returns>An instance of the graphics backend.</returns>
        public static IGraphicsBackend CreateBackend(GraphicsBackendType backendType, KotorGame? gameType = null)
        {
            try
            {
                switch (backendType)
                {
                    case GraphicsBackendType.MonoGame:
                        return new MonoGame.Graphics.MonoGameGraphicsBackend();
                    case GraphicsBackendType.Stride:
                        return new Stride.Graphics.StrideGraphicsBackend();
                    case GraphicsBackendType.OdysseyEngine:
                        if (!gameType.HasValue)
                        {
                            throw new ArgumentException("Game type (K1 or K2) is required when creating OdysseyEngine backend", nameof(gameType));
                        }
                        if (gameType.Value == KotorGame.K1)
                        {
                            return new Kotor1GraphicsBackend();
                        }
                        else if (gameType.Value == KotorGame.K2)
                        {
                            return new Kotor2GraphicsBackend();
                        }
                        else
                        {
                            throw new ArgumentException("OdysseyEngine backend only supports K1 or K2 game types", nameof(gameType));
                        }
                    default:
                        throw new ArgumentException("Unknown graphics backend type: " + backendType, nameof(backendType));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create {backendType} backend: {ex.Message}");
                Console.WriteLine("Falling back to MonoGame backend...");

                // Fallback to MonoGame
                try
                {
                    return new Andastra.Runtime.MonoGame.Graphics.MonoGameGraphicsBackend();
                }
                catch (Exception fallbackEx)
                {
                    Console.WriteLine($"Fallback to MonoGame also failed: {fallbackEx.Message}");
                    throw new InvalidOperationException("No graphics backend available", fallbackEx);
                }
            }
        }
    }
}

