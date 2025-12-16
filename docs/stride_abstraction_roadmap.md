# Stride Abstraction Layer Roadmap

This document tracks progress on adding Stride engine support alongside MonoGame through a comprehensive abstraction layer.

## Goal

Create a unified abstraction layer that allows OdysseyRuntime to run on either MonoGame or Stride, with minimal code duplication and full feature parity.

## Architecture

```
Odyssey.Core (no rendering dependencies)
    ↑
Odyssey.Graphics (abstraction interfaces)
    ↑
Odyssey.MonoGame (MonoGame implementations)
Odyssey.Stride (Stride implementations)
    ↑
Odyssey.Game (uses abstraction, selects backend)
```

## Phase 1: Core Abstraction Interfaces ✅

- [x] IGraphicsDevice - Graphics device abstraction
- [x] ITexture2D - Texture resource abstraction
- [x] IVertexBuffer, IIndexBuffer - Buffer resource abstraction
- [x] IRenderTarget, IDepthStencilBuffer - Render target abstraction
- [x] IGraphicsBackend - Backend selection
- [x] ISpriteBatch - 2D sprite rendering
- [x] IFont - Font rendering
- [x] IContentManager - Content loading
- [x] IWindow - Window management
- [x] IInputManager - Input handling (keyboard, mouse, gamepad)
- [x] ISoundPlayer - Audio playback (already exists in Core)
- [x] IVoicePlayer - Voice playback (already exists in Core)
- [ ] ISpatialAudio - 3D spatial audio (needs abstraction)

## Phase 2: MonoGame Abstraction Implementation ✅

- [x] MonoGameGraphicsDevice - IGraphicsDevice implementation
- [x] MonoGameTexture2D - ITexture2D implementation
- [x] MonoGameVertexBuffer, MonoGameIndexBuffer - Buffer implementations
- [x] MonoGameRenderTarget, MonoGameDepthStencilBuffer - Render target implementations
- [x] MonoGameGraphicsBackend - IGraphicsBackend implementation
- [x] MonoGameSpriteBatch - ISpriteBatch implementation
- [x] MonoGameFont - IFont implementation
- [x] MonoGameContentManager - IContentManager implementation
- [x] MonoGameWindow - IWindow implementation
- [x] MonoGameInputManager - IInputManager implementation
- [x] MonoGameSpatialAudio - ISpatialAudio implementation (already exists)

## Phase 3: Stride Implementation ✅

- [x] StrideGraphicsDevice - IGraphicsDevice implementation
- [x] StrideTexture2D - ITexture2D implementation
- [x] StrideVertexBuffer, StrideIndexBuffer - Buffer implementations
- [x] StrideRenderTarget, StrideDepthStencilBuffer - Render target implementations
- [x] StrideGraphicsBackend - IGraphicsBackend implementation
- [x] StrideSpriteBatch - ISpriteBatch implementation
- [x] StrideFont - IFont implementation
- [x] StrideContentManager - IContentManager implementation
- [x] StrideWindow - IWindow implementation
- [x] StrideInputManager - IInputManager implementation
- [ ] StrideSpatialAudio - ISpatialAudio implementation (needs abstraction first)

## Phase 4: Refactor Existing Code (IN PROGRESS)

- [x] Update Odyssey.Game.csproj to reference Odyssey.Graphics instead of direct MonoGame
- [x] Refactor OdysseyGame to use IGraphicsBackend instead of inheriting from MonoGame.Game
- [x] Replace GraphicsDevice with IGraphicsDevice throughout OdysseyGame
- [x] Replace SpriteBatch with ISpriteBatch throughout OdysseyGame
- [x] Replace SpriteFont with IFont throughout OdysseyGame
- [x] Replace Texture2D with ITexture2D throughout OdysseyGame
- [x] Replace Content.Load with IContentManager.Load
- [x] Replace Keyboard/Mouse state with IInputManager
- [x] Refactor MenuRenderer.cs to use abstraction (12 MonoGame references)
- [x] Refactor SaveLoadMenu.cs to use abstraction (8 MonoGame references)
- [x] Refactor OdysseyGame.cs to use abstraction (63 MonoGame references)
- [x] Update Program.cs to use GraphicsBackendFactory for backend selection

## Phase 5: Feature Parity Verification

- [ ] Verify all MonoGame features work in Stride:
  - [ ] Texture loading and rendering
  - [ ] Model rendering
  - [ ] Shader compilation and execution
  - [ ] Sprite batch rendering
  - [ ] Font rendering
  - [ ] Audio playback
  - [ ] Input handling
  - [ ] Window management
  - [ ] Render targets
  - [ ] Depth/stencil buffers
  - [ ] Blend states
  - [ ] Rasterizer states
  - [ ] Compute shaders
  - [ ] Raytracing (if supported)

## Phase 6: Testing and Validation

- [ ] Unit tests for abstraction layer
- [ ] Integration tests for both backends
- [ ] Performance comparison
- [ ] Visual parity verification
- [ ] Audio parity verification

## Current Status

**Phase 1**: ✅ Complete - All core abstraction interfaces created
**Phase 2**: ✅ Complete - All MonoGame implementations created
**Phase 3**: ✅ Complete - All Stride implementations created (may need API adjustments)
**Phase 4**: In progress - Need to refactor OdysseyGame and existing code
**Phase 5**: Not started
**Phase 6**: Not started

## Notes

- Keep C# 7.3 compatibility
- Minimize code duplication between MonoGame and Stride implementations
- Ensure feature parity - all MonoGame features must work in Stride
- Use factory pattern for backend selection
- Support runtime backend switching (future enhancement)

