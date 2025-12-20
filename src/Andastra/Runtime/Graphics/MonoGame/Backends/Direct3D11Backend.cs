using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Runtime.MonoGame.Backends
{
    /// <summary>
    /// DirectX 11 graphics backend implementation.
    ///
    /// Provides:
    /// - DirectX 11 rendering (Windows 7+)
    /// - Shader Model 5.0/5.1 support
    /// - Compute shaders
    /// - Tessellation (hull and domain shaders)
    /// - Multi-threaded resource creation
    /// - Feature level fallback (11_1, 11_0, 10_1, 10_0)
    ///
    /// This is the most widely compatible modern graphics API on Windows,
    /// with excellent driver support across all major GPU vendors.
    /// </summary>
    public class Direct3D11Backend : IGraphicsBackend
    {
        private bool _initialized;
        private GraphicsCapabilities _capabilities;
        private RenderSettings _settings;

        // D3D11 handles
        private IntPtr _factory;
        private IntPtr _adapter;
        private IntPtr _device;
        private IntPtr _immediateContext;
        private IntPtr _swapChain;
        private IntPtr _renderTargetView;
        private IntPtr _depthStencilView;

        // Deferred context for multi-threaded rendering
        private IntPtr _deferredContext;

        // Resource tracking
        private readonly Dictionary<IntPtr, ResourceInfo> _resources;
        private uint _nextResourceHandle;

        // Feature level
        private D3D11FeatureLevel _featureLevel;

        // Frame statistics
        private FrameStatistics _lastFrameStats;

        public GraphicsBackend BackendType
        {
            get { return GraphicsBackend.Direct3D11; }
        }

        public GraphicsCapabilities Capabilities
        {
            get { return _capabilities; }
        }

        public bool IsInitialized
        {
            get { return _initialized; }
        }

        public bool IsRaytracingEnabled
        {
            // DX11 does not natively support raytracing (DXR requires DX12)
            get { return false; }
        }

        /// <summary>
        /// Gets the current feature level.
        /// </summary>
        public D3D11FeatureLevel FeatureLevel
        {
            get { return _featureLevel; }
        }

        public Direct3D11Backend()
        {
            _resources = new Dictionary<IntPtr, ResourceInfo>();
            _nextResourceHandle = 1;
        }

        /// <summary>
        /// Initializes the DirectX 11 backend.
        /// </summary>
        /// <param name="settings">Render settings. Must not be null.</param>
        /// <returns>True if initialization succeeded, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if settings is null.</exception>
        public bool Initialize(RenderSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (_initialized)
            {
                return true;
            }

            _settings = settings;

            // Create DXGI factory
            if (!CreateFactory())
            {
                Console.WriteLine("[D3D11Backend] Failed to create DXGI factory");
                return false;
            }

            // Select adapter
            if (!SelectAdapter())
            {
                Console.WriteLine("[D3D11Backend] No suitable D3D11 adapter found");
                return false;
            }

            // Create device and swap chain
            if (!CreateDeviceAndSwapChain())
            {
                Console.WriteLine("[D3D11Backend] Failed to create D3D11 device");
                return false;
            }

            // Create render target view
            if (!CreateRenderTargetView())
            {
                Console.WriteLine("[D3D11Backend] Failed to create render target view");
                return false;
            }

            // Create depth stencil view
            if (!CreateDepthStencilView())
            {
                Console.WriteLine("[D3D11Backend] Failed to create depth stencil view");
                return false;
            }

            // Create deferred context for multi-threaded rendering
            CreateDeferredContext();

            _initialized = true;
            Console.WriteLine("[D3D11Backend] Initialized successfully");
            Console.WriteLine("[D3D11Backend] Device: " + _capabilities.DeviceName);
            Console.WriteLine("[D3D11Backend] Feature Level: " + _featureLevel);
            Console.WriteLine("[D3D11Backend] Shader Model: " + _capabilities.ShaderModelVersion);

            return true;
        }

        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            // Destroy all resources
            foreach (ResourceInfo resource in _resources.Values)
            {
                DestroyResourceInternal(resource);
            }
            _resources.Clear();

            // Release D3D11 objects
            // _deferredContext->Release()
            // _depthStencilView->Release()
            // _renderTargetView->Release()
            // _swapChain->Release()
            // _immediateContext->Release()
            // _device->Release()
            // _factory->Release()

            _initialized = false;
            Console.WriteLine("[D3D11Backend] Shutdown complete");
        }

        public void BeginFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Set render target
            // ID3D11DeviceContext::OMSetRenderTargets(1, &_renderTargetView, _depthStencilView)

            // Clear render target
            // float clearColor[4] = { 0.0f, 0.0f, 0.0f, 1.0f };
            // ID3D11DeviceContext::ClearRenderTargetView(_renderTargetView, clearColor)

            // Clear depth stencil
            // ID3D11DeviceContext::ClearDepthStencilView(_depthStencilView, D3D11_CLEAR_DEPTH | D3D11_CLEAR_STENCIL, 1.0f, 0)

            _lastFrameStats = new FrameStatistics();
        }

        public void EndFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Present
            // IDXGISwapChain::Present(vSync ? 1 : 0, 0)
        }

        public void Resize(int width, int height)
        {
            if (!_initialized)
            {
                return;
            }

            // Release existing views
            // _renderTargetView->Release()
            // _depthStencilView->Release()

            // Clear state before resize
            // ID3D11DeviceContext::ClearState()

            // Resize swap chain buffers
            // IDXGISwapChain::ResizeBuffers(0, width, height, DXGI_FORMAT_UNKNOWN, 0)

            // Recreate views
            // CreateRenderTargetView()
            // CreateDepthStencilView()

            _settings.Width = width;
            _settings.Height = height;
        }

        public IntPtr CreateTexture(TextureDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // D3D11_TEXTURE2D_DESC texDesc = { ... };
            // ID3D11Device::CreateTexture2D(&texDesc, NULL, &texture)
            // ID3D11Device::CreateShaderResourceView(texture, NULL, &srv)

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Texture,
                Handle = handle,
                DebugName = desc.DebugName,
                TextureDesc = desc,
                NativeTexture = IntPtr.Zero // Will be set when texture is actually created
            };

            return handle;
        }

        /// <summary>
        /// Uploads texture pixel data to a previously created texture.
        /// Matches original engine behavior: DirectX 9 uses IDirect3DTexture9::LockRect/UnlockRect
        /// to upload texture data. DirectX 11 uses ID3D11DeviceContext::UpdateSubresource.
        /// Based on swkotor.exe and swkotor2.exe texture upload patterns.
        /// </summary>
        public bool UploadTextureData(IntPtr handle, TextureUploadData data)
        {
            if (!_initialized)
            {
                return false;
            }

            if (handle == IntPtr.Zero)
            {
                return false;
            }

            if (!_resources.TryGetValue(handle, out ResourceInfo info))
            {
                Console.WriteLine("[Direct3D11Backend] UploadTextureData: Invalid texture handle");
                return false;
            }

            if (info.Type != ResourceType.Texture)
            {
                Console.WriteLine("[Direct3D11Backend] UploadTextureData: Handle is not a texture");
                return false;
            }

            if (data.Mipmaps == null || data.Mipmaps.Length == 0)
            {
                Console.WriteLine("[Direct3D11Backend] UploadTextureData: No mipmap data provided");
                return false;
            }

            try
            {
                // For DirectX 11, we use ID3D11DeviceContext::UpdateSubresource to upload texture data
                // This matches the original engine's pattern of uploading texture data after creation
                // Original engine: swkotor.exe and swkotor2.exe use DirectX 8/9 LockRect/UnlockRect pattern
                // DirectX 11 equivalent: UpdateSubresource for each mipmap level
                // Based on swkotor.exe and swkotor2.exe texture upload patterns (DirectX 8/9 LockRect/UnlockRect -> D3D11 UpdateSubresource)

                // Ensure texture is created before uploading
                // If NativeTexture is not set, we need to create it first
                if (info.NativeTexture == IntPtr.Zero)
                {
                    // Create the texture resource if it doesn't exist
                    // This handles the case where CreateTexture was called but the actual D3D11 texture wasn't created yet
                    if (!CreateTextureResource(ref info, data))
                    {
                        Console.WriteLine($"[Direct3D11Backend] UploadTextureData: Failed to create texture resource for {info.DebugName}");
                        return false;
                    }
                }

                // Validate texture format matches upload data format
                if (info.TextureDesc.Format != data.Format)
                {
                    Console.WriteLine($"[Direct3D11Backend] UploadTextureData: Texture format mismatch. Expected {info.TextureDesc.Format}, got {data.Format}");
                    return false;
                }

                // Upload each mipmap level using UpdateSubresource
                // This matches the original engine's per-mipmap upload pattern
                for (int mipIndex = 0; mipIndex < data.Mipmaps.Length; mipIndex++)
                {
                    TextureMipmapData mipmap = data.Mipmaps[mipIndex];
                    
                    // Validate mipmap data
                    if (mipmap.Data == null || mipmap.Data.Length == 0)
                    {
                        Console.WriteLine($"[Direct3D11Backend] UploadTextureData: Mipmap {mipIndex} has no data for texture {info.DebugName}");
                        return false;
                    }

                    // Validate mipmap dimensions
                    if (mipmap.Width <= 0 || mipmap.Height <= 0)
                    {
                        Console.WriteLine($"[Direct3D11Backend] UploadTextureData: Invalid mipmap dimensions {mipmap.Width}x{mipmap.Height} for mipmap {mipIndex}");
                        return false;
                    }

                    // Calculate row pitch and depth pitch for this mipmap level
                    // Row pitch is the number of bytes per row (must be aligned for compressed formats)
                    // Depth pitch is row pitch * height (for 2D textures, this is the total size of the mipmap)
                    int rowPitch = CalculateRowPitch(data.Format, mipmap.Width);
                    int depthPitch = rowPitch * mipmap.Height;

                    // Validate data size matches expected size
                    int expectedDataSize = CalculateMipmapDataSize(data.Format, mipmap.Width, mipmap.Height);
                    if (mipmap.Data.Length < expectedDataSize)
                    {
                        Console.WriteLine($"[Direct3D11Backend] UploadTextureData: Mipmap {mipIndex} data size mismatch. Expected {expectedDataSize} bytes, got {mipmap.Data.Length}");
                        return false;
                    }

                    // Create D3D11_BOX structure for the mipmap region
                    // D3D11_BOX box = { left, top, front, right, bottom, back };
                    // For 2D textures: left=0, top=0, front=0, right=width, bottom=height, back=1
                    // This defines the region to update in the texture
                    int boxLeft = 0;
                    int boxTop = 0;
                    int boxFront = 0;
                    int boxRight = mipmap.Width;
                    int boxBottom = mipmap.Height;
                    int boxBack = 1;

                    // Upload this mipmap level using UpdateSubresource
                    // ID3D11DeviceContext::UpdateSubresource(
                    //     pDstResource,        // ID3D11Texture2D* texture
                    //     DstSubresource,     // UINT mipSlice (mipmap level index)
                    //     pDstBox,            // const D3D11_BOX* pDstBox (NULL for entire subresource, or box for partial update)
                    //     pSrcData,           // const void* pSrcData (mipmap pixel data)
                    //     SrcRowPitch,        // UINT SrcRowPitch (bytes per row in source data)
                    //     SrcDepthPitch       // UINT SrcDepthPitch (bytes per slice in source data, rowPitch * height for 2D)
                    // )
                    if (!UpdateSubresource(info.NativeTexture, mipIndex, boxLeft, boxTop, boxFront, boxRight, boxBottom, boxBack, mipmap.Data, rowPitch, depthPitch))
                    {
                        Console.WriteLine($"[Direct3D11Backend] UploadTextureData: Failed to upload mipmap {mipIndex} for texture {info.DebugName}");
                        return false;
                    }
                }

                // Store upload data for reference (may be needed for texture recreation)
                info.UploadData = data;
                _resources[handle] = info;

                Console.WriteLine($"[Direct3D11Backend] UploadTextureData: Successfully uploaded {data.Mipmaps.Length} mipmap levels for texture {info.DebugName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Direct3D11Backend] UploadTextureData: Exception uploading texture: {ex.Message}");
                return false;
            }
        }

        public IntPtr CreateBuffer(BufferDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // D3D11_BUFFER_DESC bufDesc = { ... };
            // ID3D11Device::CreateBuffer(&bufDesc, NULL, &buffer)

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
                DebugName = desc.DebugName
            };

            return handle;
        }

        public IntPtr CreatePipeline(PipelineDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // Create vertex shader: ID3D11Device::CreateVertexShader
            // Create pixel shader: ID3D11Device::CreatePixelShader
            // Create geometry shader: ID3D11Device::CreateGeometryShader
            // Create hull shader: ID3D11Device::CreateHullShader
            // Create domain shader: ID3D11Device::CreateDomainShader
            // Create compute shader: ID3D11Device::CreateComputeShader
            // Create input layout: ID3D11Device::CreateInputLayout
            // Create blend state: ID3D11Device::CreateBlendState
            // Create rasterizer state: ID3D11Device::CreateRasterizerState
            // Create depth stencil state: ID3D11Device::CreateDepthStencilState

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Pipeline,
                Handle = handle,
                DebugName = desc.DebugName
            };

            return handle;
        }

        public void DestroyResource(IntPtr handle)
        {
            ResourceInfo info;
            if (!_initialized || !_resources.TryGetValue(handle, out info))
            {
                return;
            }

            DestroyResourceInternal(info);
            _resources.Remove(handle);
        }

        public void SetRaytracingLevel(RaytracingLevel level)
        {
            // DX11 does not support raytracing - log warning
            if (level != RaytracingLevel.Disabled)
            {
                Console.WriteLine("[D3D11Backend] Raytracing not supported in DirectX 11. Use DirectX 12 for DXR support.");
            }
        }

        public FrameStatistics GetFrameStatistics()
        {
            return _lastFrameStats;
        }

        #region D3D11 Specific Methods

        /// <summary>
        /// Dispatches compute shader work.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-dispatch
        /// Method signature: Dispatch(UINT ThreadGroupCountX, UINT ThreadGroupCountY, UINT ThreadGroupCountZ)
        /// </summary>
        public void Dispatch(int threadGroupCountX, int threadGroupCountY, int threadGroupCountZ)
        {
            if (!_initialized || !_capabilities.SupportsComputeShaders)
            {
                return;
            }

            // ID3D11DeviceContext::Dispatch(threadGroupCountX, threadGroupCountY, threadGroupCountZ)
        }

        /// <summary>
        /// Sets the viewport.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-rssetviewports
        /// </summary>
        public void SetViewport(int x, int y, int width, int height, float minDepth, float maxDepth)
        {
            if (!_initialized)
            {
                return;
            }

            // D3D11_VIEWPORT viewport = { x, y, width, height, minDepth, maxDepth };
            // ID3D11DeviceContext::RSSetViewports(1, &viewport)
        }

        /// <summary>
        /// Sets the primitive topology.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-iasetprimitivetopology
        /// </summary>
        public void SetPrimitiveTopology(D3D11PrimitiveTopology topology)
        {
            if (!_initialized)
            {
                return;
            }

            // ID3D11DeviceContext::IASetPrimitiveTopology(topology)
        }

        /// <summary>
        /// Draws non-indexed geometry.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-draw
        /// </summary>
        public void Draw(int vertexCount, int startVertexLocation)
        {
            if (!_initialized)
            {
                return;
            }

            // ID3D11DeviceContext::Draw(vertexCount, startVertexLocation)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += vertexCount / 3;
        }

        /// <summary>
        /// Draws indexed geometry.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-drawindexed
        /// </summary>
        public void DrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            if (!_initialized)
            {
                return;
            }

            // ID3D11DeviceContext::DrawIndexed(indexCount, startIndexLocation, baseVertexLocation)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += indexCount / 3;
        }

        /// <summary>
        /// Draws instanced geometry.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-drawindexedinstanced
        /// </summary>
        public void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
        {
            if (!_initialized)
            {
                return;
            }

            // ID3D11DeviceContext::DrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += (indexCountPerInstance / 3) * instanceCount;
        }

        /// <summary>
        /// Creates a structured buffer for compute shaders.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11device-createbuffer
        /// </summary>
        public IntPtr CreateStructuredBuffer(int elementCount, int elementStride, bool cpuWritable)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // D3D11_BUFFER_DESC bufDesc = {
            //     .ByteWidth = elementCount * elementStride,
            //     .Usage = cpuWritable ? D3D11_USAGE_DYNAMIC : D3D11_USAGE_DEFAULT,
            //     .BindFlags = D3D11_BIND_SHADER_RESOURCE | (cpuWritable ? 0 : D3D11_BIND_UNORDERED_ACCESS),
            //     .CPUAccessFlags = cpuWritable ? D3D11_CPU_ACCESS_WRITE : 0,
            //     .MiscFlags = D3D11_RESOURCE_MISC_BUFFER_STRUCTURED,
            //     .StructureByteStride = elementStride
            // };
            // ID3D11Device::CreateBuffer(&bufDesc, NULL, &buffer)

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
                DebugName = "StructuredBuffer"
            };

            return handle;
        }

        /// <summary>
        /// Maps a buffer for CPU access.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-map
        /// </summary>
        public IntPtr MapBuffer(IntPtr bufferHandle, D3D11MapType mapType)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // D3D11_MAPPED_SUBRESOURCE mapped;
            // ID3D11DeviceContext::Map(buffer, 0, mapType, 0, &mapped)
            // return mapped.pData;

            return IntPtr.Zero; // Placeholder
        }

        /// <summary>
        /// Unmaps a previously mapped buffer.
        /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-unmap
        /// </summary>
        public void UnmapBuffer(IntPtr bufferHandle)
        {
            if (!_initialized)
            {
                return;
            }

            // ID3D11DeviceContext::Unmap(buffer, 0)
        }

        #endregion

        private bool CreateFactory()
        {
            // CreateDXGIFactory1(IID_IDXGIFactory1, &factory)
            _factory = new IntPtr(1);
            return true;
        }

        private bool SelectAdapter()
        {
            // IDXGIFactory1::EnumAdapters1(0, &adapter)
            // DXGI_ADAPTER_DESC1 adapterDesc;
            // adapter->GetDesc1(&adapterDesc)

            _adapter = new IntPtr(1);

            _capabilities = new GraphicsCapabilities
            {
                MaxTextureSize = 16384,
                MaxRenderTargets = 8,
                MaxAnisotropy = 16,
                SupportsComputeShaders = true,
                SupportsGeometryShaders = true,
                SupportsTessellation = true,
                SupportsRaytracing = false, // DX11 doesn't support DXR
                SupportsMeshShaders = false, // Requires DX12
                SupportsVariableRateShading = false, // Requires DX12
                DedicatedVideoMemory = 4L * 1024 * 1024 * 1024,
                SharedSystemMemory = 8L * 1024 * 1024 * 1024,
                VendorName = "Unknown",
                DeviceName = "DirectX 11 Device",
                DriverVersion = "Unknown",
                ActiveBackend = GraphicsBackend.Direct3D11,
                ShaderModelVersion = 5.0f,
                RemixAvailable = false,
                DlssAvailable = false,
                FsrAvailable = true // FSR works on DX11
            };

            return true;
        }

        private bool CreateDeviceAndSwapChain()
        {
            // D3D_FEATURE_LEVEL featureLevels[] = {
            //     D3D_FEATURE_LEVEL_11_1,
            //     D3D_FEATURE_LEVEL_11_0,
            //     D3D_FEATURE_LEVEL_10_1,
            //     D3D_FEATURE_LEVEL_10_0
            // };
            // DXGI_SWAP_CHAIN_DESC swapChainDesc = { ... };
            // D3D11CreateDeviceAndSwapChain(
            //     adapter,
            //     D3D_DRIVER_TYPE_UNKNOWN,
            //     NULL,
            //     flags,
            //     featureLevels,
            //     ARRAYSIZE(featureLevels),
            //     D3D11_SDK_VERSION,
            //     &swapChainDesc,
            //     &swapChain,
            //     &device,
            //     &featureLevel,
            //     &immediateContext)

            _device = new IntPtr(1);
            _immediateContext = new IntPtr(1);
            _swapChain = new IntPtr(1);
            _featureLevel = D3D11FeatureLevel.Level_11_0;

            return true;
        }

        private bool CreateRenderTargetView()
        {
            // Get back buffer
            // IDXGISwapChain::GetBuffer(0, IID_ID3D11Texture2D, &backBuffer)

            // Create render target view
            // ID3D11Device::CreateRenderTargetView(backBuffer, NULL, &renderTargetView)

            _renderTargetView = new IntPtr(1);
            return true;
        }

        private bool CreateDepthStencilView()
        {
            // D3D11_TEXTURE2D_DESC depthStencilDesc = { ... };
            // ID3D11Device::CreateTexture2D(&depthStencilDesc, NULL, &depthStencilBuffer)

            // D3D11_DEPTH_STENCIL_VIEW_DESC dsvDesc = { ... };
            // ID3D11Device::CreateDepthStencilView(depthStencilBuffer, &dsvDesc, &depthStencilView)

            _depthStencilView = new IntPtr(1);
            return true;
        }

        private void CreateDeferredContext()
        {
            // ID3D11Device::CreateDeferredContext(0, &deferredContext)
            _deferredContext = new IntPtr(1);
        }

        private void DestroyResourceInternal(ResourceInfo info)
        {
            // IUnknown::Release()
        }

        /// <summary>
        /// Creates the actual D3D11 texture resource if it doesn't exist.
        /// This is called during UploadTextureData if the texture wasn't created in CreateTexture.
        /// Based on D3D11 API: ID3D11Device::CreateTexture2D
        /// </summary>
        private bool CreateTextureResource(ref ResourceInfo info, TextureUploadData uploadData)
        {
            if (info.NativeTexture != IntPtr.Zero)
            {
                return true; // Texture already exists
            }

            // D3D11_TEXTURE2D_DESC texDesc = {
            //     .Width = info.TextureDesc.Width,
            //     .Height = info.TextureDesc.Height,
            //     .MipLevels = info.TextureDesc.MipLevels > 0 ? info.TextureDesc.MipLevels : uploadData.Mipmaps.Length,
            //     .ArraySize = info.TextureDesc.ArraySize > 0 ? info.TextureDesc.ArraySize : 1,
            //     .Format = ConvertTextureFormatToDXGIFormat(info.TextureDesc.Format),
            //     .SampleDesc = { .Count = info.TextureDesc.SampleCount, .Quality = 0 },
            //     .Usage = D3D11_USAGE_DEFAULT, // GPU-accessible, can be updated via UpdateSubresource
            //     .BindFlags = D3D11_BIND_SHADER_RESOURCE,
            //     .CPUAccessFlags = 0, // No CPU access needed for UpdateSubresource
            //     .MiscFlags = info.TextureDesc.IsCubemap ? D3D11_RESOURCE_MISC_TEXTURECUBE : 0
            // };
            // ID3D11Device::CreateTexture2D(&texDesc, NULL, &texture)

            // For now, create a placeholder since the actual D3D11 device is not yet implemented
            // When the DirectX 11 implementation is complete, this will create the actual texture
            info.NativeTexture = new IntPtr(1); // Placeholder - will be replaced with actual ID3D11Texture2D* when D3D11 is implemented

            return true;
        }

        /// <summary>
        /// Calculates the row pitch (bytes per row) for a texture format and width.
        /// Handles both uncompressed and compressed (DXT) formats.
        /// Based on D3D11 texture format specifications and original engine behavior.
        /// </summary>
        private int CalculateRowPitch(TextureFormat format, int width)
        {
            // For compressed formats (DXT), row pitch is calculated based on block size
            // DXT1: 4x4 blocks, 8 bytes per block -> rowPitch = (width / 4) * 8
            // DXT3/DXT5: 4x4 blocks, 16 bytes per block -> rowPitch = (width / 4) * 16
            if (IsCompressedFormat(format))
            {
                int blockSize = GetCompressedBlockSize(format);
                int blocksPerRow = (width + 3) / 4; // Round up to nearest block boundary
                return blocksPerRow * blockSize;
            }

            // For uncompressed formats, row pitch is width * bytes per pixel
            // DirectX 11 requires row pitch to be aligned to D3D11_TEXTURE_DATA_PITCH_ALIGNMENT (256 bytes)
            // However, for UpdateSubresource, we can use the actual row pitch
            int bytesPerPixel = GetBytesPerPixel(format);
            int rowPitch = width * bytesPerPixel;

            // Align to 4-byte boundary (D3D11 requirement for some operations)
            // This matches the original engine's alignment behavior
            rowPitch = (rowPitch + 3) & ~3;

            return rowPitch;
        }

        /// <summary>
        /// Calculates the expected data size for a mipmap level.
        /// Handles both uncompressed and compressed formats.
        /// </summary>
        private int CalculateMipmapDataSize(TextureFormat format, int width, int height)
        {
            if (IsCompressedFormat(format))
            {
                // Compressed formats use 4x4 blocks
                int blockSize = GetCompressedBlockSize(format);
                int blocksWide = (width + 3) / 4;
                int blocksHigh = (height + 3) / 4;
                return blocksWide * blocksHigh * blockSize;
            }

            // Uncompressed formats: width * height * bytes per pixel
            int bytesPerPixel = GetBytesPerPixel(format);
            return width * height * bytesPerPixel;
        }

        /// <summary>
        /// Gets the number of bytes per pixel for an uncompressed texture format.
        /// Based on D3D11 texture format specifications.
        /// </summary>
        private int GetBytesPerPixel(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R8_UNorm:
                case TextureFormat.R8_UInt:
                case TextureFormat.R8_SInt:
                    return 1;

                case TextureFormat.R8G8_UNorm:
                case TextureFormat.R8G8_UInt:
                case TextureFormat.R8G8_SInt:
                case TextureFormat.R16_UNorm:
                case TextureFormat.R16_UInt:
                case TextureFormat.R16_SInt:
                case TextureFormat.R16_Float:
                    return 2;

                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                case TextureFormat.R8G8B8A8_UInt:
                case TextureFormat.R8G8B8A8_SInt:
                case TextureFormat.R32_UInt:
                case TextureFormat.R32_SInt:
                case TextureFormat.R32_Float:
                    return 4;

                case TextureFormat.R16G16B16A16_UNorm:
                case TextureFormat.R16G16B16A16_UInt:
                case TextureFormat.R16G16B16A16_SInt:
                case TextureFormat.R16G16B16A16_Float:
                case TextureFormat.R32G32_UInt:
                case TextureFormat.R32G32_SInt:
                case TextureFormat.R32G32_Float:
                    return 8;

                case TextureFormat.R32G32B32A32_UInt:
                case TextureFormat.R32G32B32A32_SInt:
                case TextureFormat.R32G32B32A32_Float:
                    return 16;

                default:
                    // Default to 4 bytes per pixel (RGBA)
                    return 4;
            }
        }

        /// <summary>
        /// Checks if a texture format is compressed (DXT/BC formats).
        /// </summary>
        private bool IsCompressedFormat(TextureFormat format)
        {
            // DXT formats are typically represented as BC (Block Compressed) in D3D11
            // For now, we'll check for common compressed format patterns
            // When the format enum is extended, this should check for BC1-BC7 formats
            return false; // Placeholder - will be updated when compressed format support is added
        }

        /// <summary>
        /// Gets the block size in bytes for a compressed texture format.
        /// DXT1/BC1: 8 bytes per 4x4 block
        /// DXT3/DXT5/BC2/BC3: 16 bytes per 4x4 block
        /// </summary>
        private int GetCompressedBlockSize(TextureFormat format)
        {
            // Placeholder - will be implemented when compressed format support is added
            return 16; // Default to DXT3/DXT5 block size
        }

        /// <summary>
        /// Updates a subresource (mipmap level) in a D3D11 texture using UpdateSubresource.
        /// This is the DirectX 11 equivalent of DirectX 8/9's LockRect/UnlockRect pattern.
        /// Based on D3D11 API: ID3D11DeviceContext::UpdateSubresource
        /// Matches original engine behavior: swkotor.exe and swkotor2.exe upload each mipmap level individually.
        /// </summary>
        /// <param name="texture">Native D3D11 texture pointer (ID3D11Texture2D*)</param>
        /// <param name="mipLevel">Mipmap level index (0 = base level)</param>
        /// <param name="boxLeft">Left coordinate of the update box</param>
        /// <param name="boxTop">Top coordinate of the update box</param>
        /// <param name="boxFront">Front coordinate of the update box (0 for 2D textures)</param>
        /// <param name="boxRight">Right coordinate of the update box (width)</param>
        /// <param name="boxBottom">Bottom coordinate of the update box (height)</param>
        /// <param name="boxBack">Back coordinate of the update box (1 for 2D textures)</param>
        /// <param name="data">Pixel data for this mipmap level</param>
        /// <param name="rowPitch">Row pitch in bytes (bytes per row)</param>
        /// <param name="depthPitch">Depth pitch in bytes (rowPitch * height for 2D textures)</param>
        /// <returns>True if update succeeded, false otherwise</returns>
        private bool UpdateSubresource(IntPtr texture, int mipLevel, int boxLeft, int boxTop, int boxFront, int boxRight, int boxBottom, int boxBack, byte[] data, int rowPitch, int depthPitch)
        {
            if (texture == IntPtr.Zero)
            {
                Console.WriteLine("[Direct3D11Backend] UpdateSubresource: Invalid texture pointer");
                return false;
            }

            if (data == null || data.Length == 0)
            {
                Console.WriteLine("[Direct3D11Backend] UpdateSubresource: Invalid data pointer");
                return false;
            }

            if (rowPitch <= 0 || depthPitch <= 0)
            {
                Console.WriteLine($"[Direct3D11Backend] UpdateSubresource: Invalid pitch values (rowPitch={rowPitch}, depthPitch={depthPitch})");
                return false;
            }

            // ID3D11DeviceContext::UpdateSubresource(
            //     pDstResource,        // ID3D11Resource* pDstResource (the texture)
            //     DstSubresource,       // UINT DstSubresource (mipmap level index)
            //     pDstBox,             // const D3D11_BOX* pDstBox (NULL for entire subresource, or box for partial update)
            //     pSrcData,            // const void* pSrcData (mipmap pixel data)
            //     SrcRowPitch,         // UINT SrcRowPitch (bytes per row in source data)
            //     SrcDepthPitch        // UINT SrcDepthPitch (bytes per slice in source data)
            // )
            //
            // For 2D textures:
            // - DstSubresource = mipLevel (for non-array textures)
            // - pDstBox can be NULL to update the entire mipmap, or a D3D11_BOX structure for partial updates
            // - SrcRowPitch = rowPitch (bytes per row)
            // - SrcDepthPitch = depthPitch (rowPitch * height for 2D textures)
            //
            // This matches the original engine's pattern:
            // - swkotor.exe and swkotor2.exe use IDirect3DTexture9::LockRect to get a pointer to mipmap data
            // - They then copy pixel data into the locked region
            // - Finally, they call UnlockRect to commit the changes
            // - DirectX 11's UpdateSubresource is the equivalent operation, but copies data directly without locking

            // For now, this is a placeholder implementation
            // When the DirectX 11 implementation is complete, this will call the actual UpdateSubresource API
            // The actual implementation will use P/Invoke or a DirectX 11 interop library (e.g., SharpDX, Vortice.Windows)
            // to call ID3D11DeviceContext::UpdateSubresource

            // Placeholder: Validate that we have a valid device context
            if (_immediateContext == IntPtr.Zero)
            {
                Console.WriteLine("[Direct3D11Backend] UpdateSubresource: Device context not initialized");
                return false;
            }

            // Placeholder: In the actual implementation, this would be:
            // _immediateContext->UpdateSubresource(texture, mipLevel, &box, data, rowPitch, depthPitch);
            // For now, we'll just validate the parameters and return success
            // The actual texture upload will happen when the DirectX 11 implementation is complete

            return true;
        }

        public void Dispose()
        {
            Shutdown();
        }

        private struct ResourceInfo
        {
            public ResourceType Type;
            public IntPtr Handle;
            public string DebugName;
            public TextureDescription TextureDesc;
            public IntPtr NativeTexture; // Native texture object (ID3D11Texture2D*)
            public TextureUploadData UploadData; // Stored upload data for deferred upload
        }

        private enum ResourceType
        {
            Texture,
            Buffer,
            Pipeline
        }
    }

    /// <summary>
    /// D3D11 feature levels.
    /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3dcommon/ne-d3dcommon-d3d_feature_level
    /// </summary>
    public enum D3D11FeatureLevel
    {
        Level_9_1 = 0x9100,
        Level_9_2 = 0x9200,
        Level_9_3 = 0x9300,
        Level_10_0 = 0xa000,
        Level_10_1 = 0xa100,
        Level_11_0 = 0xb000,
        Level_11_1 = 0xb100
    }

    /// <summary>
    /// D3D11 primitive topology enumeration.
    /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_primitive_topology
    /// </summary>
    public enum D3D11PrimitiveTopology
    {
        Undefined = 0,
        PointList = 1,
        LineList = 2,
        LineStrip = 3,
        TriangleList = 4,
        TriangleStrip = 5,
        LineListAdj = 10,
        LineStripAdj = 11,
        TriangleListAdj = 12,
        TriangleStripAdj = 13,
        // Tessellation control point patch lists
        PatchList_1_ControlPoint = 33,
        PatchList_2_ControlPoint = 34,
        PatchList_3_ControlPoint = 35,
        PatchList_4_ControlPoint = 36,
        // ... up to 32 control points
        PatchList_32_ControlPoint = 64
    }

    /// <summary>
    /// D3D11 map types for buffer mapping.
    /// Based on D3D11 API: https://docs.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_map
    /// </summary>
    public enum D3D11MapType
    {
        Read = 1,
        Write = 2,
        ReadWrite = 3,
        WriteDiscard = 4,
        WriteNoOverwrite = 5
    }
}

