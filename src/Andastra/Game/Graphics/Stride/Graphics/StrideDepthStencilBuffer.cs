using System;
using System.Reflection;
using Andastra.Runtime.Graphics;

namespace Andastra.Game.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IDepthStencilBuffer.
    /// </summary>
    public class StrideDepthStencilBuffer : IDepthStencilBuffer
    {
        private readonly global::Stride.Graphics.Texture _depthBuffer;

        public StrideDepthStencilBuffer(global::Stride.Graphics.Texture depthBuffer)
        {
            _depthBuffer = depthBuffer ?? throw new ArgumentNullException(nameof(depthBuffer));
        }

        public int Width => _depthBuffer.Width;

        public int Height => _depthBuffer.Height;

        public IntPtr NativeHandle
        {
            get
            {
                if (_depthBuffer == null)
                    return IntPtr.Zero;

                // Stride Texture native resource access via reflection
                // Different backends expose native handles through different properties
                var textureType = _depthBuffer.GetType();

                // Try NativePointer first (used by D3D12 and Vulkan backends)
                var nativePointerProperty = textureType.GetProperty("NativePointer",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nativePointerProperty != null)
                {
                    var value = nativePointerProperty.GetValue(_depthBuffer);
                    if (value is IntPtr ptr && ptr != IntPtr.Zero)
                    {
                        return ptr;
                    }
                }

                // Try NativeDeviceTexture (used by D3D11 backend)
                var nativeDeviceTextureProperty = textureType.GetProperty("NativeDeviceTexture",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nativeDeviceTextureProperty != null)
                {
                    var value = nativeDeviceTextureProperty.GetValue(_depthBuffer);
                    if (value is IntPtr ptr && ptr != IntPtr.Zero)
                    {
                        return ptr;
                    }
                }

                // Try NativeResource (alternative property name)
                var nativeResourceProperty = textureType.GetProperty("NativeResource",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nativeResourceProperty != null)
                {
                    var value = nativeResourceProperty.GetValue(_depthBuffer);
                    if (value is IntPtr ptr && ptr != IntPtr.Zero)
                    {
                        return ptr;
                    }
                }

                // Alternative: Check for Resource property (Stride internal)
                var resourceProperty = textureType.GetProperty("Resource",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (resourceProperty != null)
                {
                    var resource = resourceProperty.GetValue(_depthBuffer);
                    if (resource != null)
                    {
                        var resourceType = resource.GetType();
                        var nativePtrProperty = resourceType.GetProperty("NativePointer",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (nativePtrProperty != null)
                        {
                            var value = nativePtrProperty.GetValue(resource);
                            if (value is IntPtr ptr && ptr != IntPtr.Zero)
                            {
                                return ptr;
                            }
                        }
                    }
                }

                return IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            _depthBuffer?.Dispose();
        }
    }
}

