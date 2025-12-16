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
- [x] ITexture - Texture resource abstraction
- [x] IBuffer - Buffer resource abstraction
- [x] IShader - Shader abstraction
- [x] IGraphicsBackend - Backend selection
- [ ] IGraphicsContext - Rendering context (command lists, state)
- [ ] ISpriteBatch - 2D sprite rendering
- [ ] IFont - Font rendering
- [ ] IContentManager - Content loading
- [ ] IWindow - Window management
- [ ] IInputManager - Input handling (keyboard, mouse, gamepad)
- [ ] ISoundPlayer - Audio playback (already exists in Core)
- [ ] IVoicePlayer - Voice playback (already exists in Core)
- [ ] ISpatialAudio - 3D spatial audio

## Phase 2: MonoGame Abstraction Implementation

- [ ] MonoGameGraphicsDevice - IGraphicsDevice implementation
- [ ] MonoGameTexture - ITexture implementation
- [ ] MonoGameBuffer - IBuffer implementation
- [ ] MonoGameShader - IShader implementation
- [ ] MonoGameGraphicsContext - IGraphicsContext implementation
- [ ] MonoGameSpriteBatch - ISpriteBatch implementation
- [ ] MonoGameFont - IFont implementation
- [ ] MonoGameContentManager - IContentManager implementation
- [ ] MonoGameWindow - IWindow implementation
- [ ] MonoGameInputManager - IInputManager implementation
- [ ] MonoGameSpatialAudio - ISpatialAudio implementation (already exists)

## Phase 3: Stride Implementation

- [ ] StrideGraphicsDevice - IGraphicsDevice implementation
- [ ] StrideTexture - ITexture implementation
- [ ] StrideBuffer - IBuffer implementation
- [ ] StrideShader - IShader implementation
- [ ] StrideGraphicsContext - IGraphicsContext implementation
- [ ] StrideSpriteBatch - ISpriteBatch implementation
- [ ] StrideFont - IFont implementation
- [ ] StrideContentManager - IContentManager implementation
- [ ] StrideWindow - IWindow implementation
- [ ] StrideInputManager - IInputManager implementation
- [ ] StrideSpatialAudio - ISpatialAudio implementation

## Phase 4: Refactor Existing Code

- [ ] Refactor OdysseyGame to use IGraphicsDevice instead of GraphicsDevice
- [ ] Refactor all rendering code to use abstraction interfaces
- [ ] Refactor UI components to use ISpriteBatch/IFont
- [ ] Refactor audio code to use abstraction (already partially done)
- [ ] Refactor input handling to use IInputManager
- [ ] Refactor content loading to use IContentManager

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

**Phase 1**: In progress - Core interfaces partially exist (IGraphicsBackend, IDevice) but need expansion
**Phase 2**: Not started
**Phase 3**: Not started
**Phase 4**: Not started
**Phase 5**: Not started
**Phase 6**: Not started

## Notes

- Keep C# 7.3 compatibility
- Minimize code duplication between MonoGame and Stride implementations
- Ensure feature parity - all MonoGame features must work in Stride
- Use factory pattern for backend selection
- Support runtime backend switching (future enhancement)

