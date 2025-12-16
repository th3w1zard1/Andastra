using System;
using Stride.Graphics;
using Odyssey.Graphics.Common.Backends;
using Odyssey.Graphics.Common.Enums;
using Odyssey.Graphics.Common.Structs;

namespace Odyssey.Stride.Backends
{
    /// <summary>
    /// Stride implementation of DirectX 11 backend.
    /// Inherits all shared D3D11 logic from BaseDirect3D11Backend.
    ///
    /// Based on Stride Graphics API: https://doc.stride3d.net/latest/en/manual/graphics/
    /// Stride supports DirectX 11 as one of its primary backends.
    /// </summary>
    public class StrideDirect3D11Backend : BaseDirect3D11Backend
    {
        private global::Stride.Engine.Game _game;
        private GraphicsDevice _strideDevice;
        private CommandList _commandList;

        public StrideDirect3D11Backend(global::Stride.Engine.Game game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        #region BaseGraphicsBackend Implementation

        protected override bool CreateDeviceResources()
        {
            if (_game.GraphicsDevice == null)
            {
                Console.WriteLine("[StrideDX11] GraphicsDevice not available");
                return false;
            }

            _strideDevice = _game.GraphicsDevice;

            // Get native D3D11 handles from Stride
            _device = _strideDevice.NativeDevice;
            _immediateContext = IntPtr.Zero; // Stride manages context internally

            // Determine feature level based on Stride device
            _featureLevel = DetermineFeatureLevel();

            return true;
        }

        protected override bool CreateSwapChainResources()
        {
            // Stride manages swap chain internally
            // We just need to get the command list for rendering
            _commandList = _game.GraphicsContext.CommandList;
            return _commandList != null;
        }

        protected override void DestroyDeviceResources()
        {
            // Stride manages device lifetime
            _strideDevice = null;
            _device = IntPtr.Zero;
        }

        protected override void DestroySwapChainResources()
        {
            // Stride manages swap chain lifetime
            _commandList = null;
        }

        protected override ResourceInfo CreateTextureInternal(TextureDescription desc, IntPtr handle)
        {
            var strideDesc = new global::Stride.Graphics.TextureDescription
            {
                Width = desc.Width,
                Height = desc.Height,
                Depth = desc.Depth,
                MipLevels = desc.MipLevels,
                ArraySize = desc.ArraySize,
                Dimension = TextureDimension.Texture2D,
                Format = ConvertFormat(desc.Format),
                Flags = ConvertUsage(desc.Usage)
            };

            var texture = Texture.New(_strideDevice, strideDesc);

            return new ResourceInfo
            {
                Type = ResourceType.Texture,
                Handle = handle,
                NativeHandle = texture?.NativeDeviceTexture ?? IntPtr.Zero,
                DebugName = desc.DebugName,
                SizeInBytes = desc.Width * desc.Height * GetFormatSize(desc.Format)
            };
        }

        protected override ResourceInfo CreateBufferInternal(BufferDescription desc, IntPtr handle)
        {
            BufferFlags flags = BufferFlags.None;
            if ((desc.Usage & BufferUsage.Vertex) != 0) flags |= BufferFlags.VertexBuffer;
            if ((desc.Usage & BufferUsage.Index) != 0) flags |= BufferFlags.IndexBuffer;
            if ((desc.Usage & BufferUsage.Constant) != 0) flags |= BufferFlags.ConstantBuffer;
            if ((desc.Usage & BufferUsage.Structured) != 0) flags |= BufferFlags.StructuredBuffer;

            var buffer = Buffer.New(_strideDevice, desc.SizeInBytes, flags);

            return new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
                NativeHandle = buffer?.NativeBuffer ?? IntPtr.Zero,
                DebugName = desc.DebugName,
                SizeInBytes = desc.SizeInBytes
            };
        }

        protected override ResourceInfo CreatePipelineInternal(PipelineDescription desc, IntPtr handle)
        {
            // Stride uses effect-based pipeline
            // Would need to compile shaders and create pipeline state
            return new ResourceInfo
            {
                Type = ResourceType.Pipeline,
                Handle = handle,
                NativeHandle = IntPtr.Zero,
                DebugName = desc.DebugName
            };
        }

        protected override void DestroyResourceInternal(ResourceInfo info)
        {
            // Resources tracked by Stride's garbage collection
            // Would dispose IDisposable resources here
        }

        #endregion

        #region BaseDirect3D11Backend Implementation

        protected override void OnDispatch(int x, int y, int z)
        {
            _commandList?.Dispatch(x, y, z);
        }

        protected override void OnSetViewport(int x, int y, int w, int h, float minD, float maxD)
        {
            _commandList?.SetViewport(new Viewport(x, y, w, h, minD, maxD));
        }

        protected override void OnSetPrimitiveTopology(PrimitiveTopology topology)
        {
            // Stride sets topology per draw call
        }

        protected override void OnDraw(int vertexCount, int startVertexLocation)
        {
            _commandList?.Draw(vertexCount, startVertexLocation);
        }

        protected override void OnDrawIndexed(int indexCount, int startIndexLocation, int baseVertexLocation)
        {
            _commandList?.DrawIndexed(indexCount, startIndexLocation, baseVertexLocation);
        }

        protected override void OnDrawIndexedInstanced(int indexCountPerInstance, int instanceCount,
            int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
        {
            _commandList?.DrawIndexedInstanced(indexCountPerInstance, instanceCount,
                startIndexLocation, baseVertexLocation, startInstanceLocation);
        }

        protected override ResourceInfo CreateStructuredBufferInternal(int elementCount, int elementStride,
            bool cpuWritable, IntPtr handle)
        {
            var flags = BufferFlags.StructuredBuffer | BufferFlags.ShaderResource;
            if (!cpuWritable) flags |= BufferFlags.UnorderedAccess;

            var buffer = Buffer.Structured.New(_strideDevice, elementCount, elementStride,
                cpuWritable);

            return new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
                NativeHandle = buffer?.NativeBuffer ?? IntPtr.Zero,
                DebugName = "StructuredBuffer",
                SizeInBytes = elementCount * elementStride
            };
        }

        public override IntPtr MapBuffer(IntPtr bufferHandle, MapType mapType)
        {
            // Stride buffer mapping would go here
            return IntPtr.Zero;
        }

        public override void UnmapBuffer(IntPtr bufferHandle)
        {
            // Stride buffer unmapping
        }

        #endregion

        #region Utility Methods

        private D3D11FeatureLevel DetermineFeatureLevel()
        {
            // Stride typically uses DX11.0 or DX11.1
            return D3D11FeatureLevel.Level_11_0;
        }

        private PixelFormat ConvertFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R8G8B8A8_UNorm: return PixelFormat.R8G8B8A8_UNorm;
                case TextureFormat.R8G8B8A8_UNorm_SRGB: return PixelFormat.R8G8B8A8_UNorm_SRgb;
                case TextureFormat.B8G8R8A8_UNorm: return PixelFormat.B8G8R8A8_UNorm;
                case TextureFormat.R16G16B16A16_Float: return PixelFormat.R16G16B16A16_Float;
                case TextureFormat.R32G32B32A32_Float: return PixelFormat.R32G32B32A32_Float;
                case TextureFormat.D24_UNorm_S8_UInt: return PixelFormat.D24_UNorm_S8_UInt;
                case TextureFormat.D32_Float: return PixelFormat.D32_Float;
                case TextureFormat.BC1_UNorm: return PixelFormat.BC1_UNorm;
                case TextureFormat.BC3_UNorm: return PixelFormat.BC3_UNorm;
                case TextureFormat.BC7_UNorm: return PixelFormat.BC7_UNorm;
                default: return PixelFormat.R8G8B8A8_UNorm;
            }
        }

        private TextureFlags ConvertUsage(TextureUsage usage)
        {
            TextureFlags flags = TextureFlags.None;
            if ((usage & TextureUsage.ShaderResource) != 0) flags |= TextureFlags.ShaderResource;
            if ((usage & TextureUsage.RenderTarget) != 0) flags |= TextureFlags.RenderTarget;
            if ((usage & TextureUsage.DepthStencil) != 0) flags |= TextureFlags.DepthStencil;
            if ((usage & TextureUsage.UnorderedAccess) != 0) flags |= TextureFlags.UnorderedAccess;
            return flags;
        }

        private int GetFormatSize(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R8_UNorm:
                case TextureFormat.R8_UInt:
                    return 1;
                case TextureFormat.R8G8_UNorm:
                case TextureFormat.R16_Float:
                    return 2;
                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.R32_Float:
                    return 4;
                case TextureFormat.R16G16B16A16_Float:
                case TextureFormat.R32G32_Float:
                    return 8;
                case TextureFormat.R32G32B32A32_Float:
                    return 16;
                default:
                    return 4;
            }
        }

        #endregion

        protected override long QueryVideoMemory()
        {
            return _strideDevice?.Adapter?.Description?.DedicatedVideoMemory ?? 4L * 1024 * 1024 * 1024;
        }

        protected override string QueryVendorName()
        {
            return _strideDevice?.Adapter?.Description?.VendorId.ToString() ?? "Unknown";
        }

        protected override string QueryDeviceName()
        {
            return _strideDevice?.Adapter?.Description?.Description ?? "Stride DirectX 11 Device";
        }
    }
}

