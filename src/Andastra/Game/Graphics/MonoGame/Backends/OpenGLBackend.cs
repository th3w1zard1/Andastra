using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Game.Graphics.MonoGame.Backends
{
    /// <summary>
    /// OpenGL graphics backend implementation.
    ///
    /// Provides:
    /// - OpenGL 4.5+ Core Profile rendering
    /// - Cross-platform support (Windows, Linux, macOS)
    /// - Compute shaders (OpenGL 4.3+)
    /// - Tessellation (OpenGL 4.0+)
    /// - AZDO (Approaching Zero Driver Overhead) techniques
    ///
    /// This is the primary cross-platform rendering backend,
    /// used as fallback when Vulkan is unavailable.
    /// </summary>
    public class OpenGLBackend : IGraphicsBackend
    {
        private bool _initialized;
        private GraphicsCapabilities _capabilities;
        private RenderSettings _settings;

        // OpenGL context
        private IntPtr _context;
        private IntPtr _windowHandle;

        // Framebuffer objects
        private uint _defaultFramebuffer;
        private uint _colorAttachment;
        private uint _depthStencilAttachment;

        // Resource tracking
        private readonly Dictionary<IntPtr, ResourceInfo> _resources;
        private uint _nextResourceHandle;

        // OpenGL version info
        private int _majorVersion;
        private int _minorVersion;
        private string _glslVersion;

        // Frame statistics
        private FrameStatistics _lastFrameStats;

        public GraphicsBackend BackendType
        {
            get { return GraphicsBackend.OpenGL; }
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
            // OpenGL does not support hardware raytracing
            get { return false; }
        }

        /// <summary>
        /// Gets the OpenGL version string.
        /// </summary>
        public string GLVersion
        {
            get { return _majorVersion + "." + _minorVersion; }
        }

        /// <summary>
        /// Gets the GLSL version string.
        /// </summary>
        public string GLSLVersion
        {
            get { return _glslVersion; }
        }

        public OpenGLBackend()
        {
            _resources = new Dictionary<IntPtr, ResourceInfo>();
            _nextResourceHandle = 1;
        }

        /// <summary>
        /// Initializes the OpenGL backend.
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

            // Initialize GLAD or GL loader
            if (!InitializeGLLoader())
            {
                Console.WriteLine("[OpenGLBackend] Failed to initialize OpenGL loader");
                return false;
            }

            // Query OpenGL version
            QueryGLVersion();

            // Check for required extensions
            if (!CheckRequiredExtensions())
            {
                Console.WriteLine("[OpenGLBackend] Required OpenGL extensions not available");
                return false;
            }

            // Query capabilities
            QueryCapabilities();

            // Create default framebuffer
            CreateDefaultFramebuffer();

            // Set default GL state
            SetDefaultState();

            _initialized = true;
            Console.WriteLine("[OpenGLBackend] Initialized successfully");
            Console.WriteLine("[OpenGLBackend] OpenGL Version: " + GLVersion);
            Console.WriteLine("[OpenGLBackend] GLSL Version: " + _glslVersion);
            Console.WriteLine("[OpenGLBackend] Renderer: " + _capabilities.DeviceName);

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

            // Delete framebuffer
            // glDeleteFramebuffers(1, &_defaultFramebuffer)
            // glDeleteTextures(1, &_colorAttachment)
            // glDeleteRenderbuffers(1, &_depthStencilAttachment)

            // Destroy context (platform-specific)

            _initialized = false;
            Console.WriteLine("[OpenGLBackend] Shutdown complete");
        }

        public void BeginFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Bind default framebuffer
            // glBindFramebuffer(GL_FRAMEBUFFER, _defaultFramebuffer)

            // Clear buffers
            // glClearColor(0.0f, 0.0f, 0.0f, 1.0f)
            // glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT)

            _lastFrameStats = new FrameStatistics();
        }

        public void EndFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Swap buffers (platform-specific)
            // SwapBuffers(_windowHandle) or SDL_GL_SwapWindow() etc.

            // Check for GL errors in debug builds
            // GLenum error = glGetError()
        }

        public void Resize(int width, int height)
        {
            if (!_initialized)
            {
                return;
            }

            // Update viewport
            // glViewport(0, 0, width, height)

            // Recreate framebuffer attachments if using FBO
            // glBindTexture(GL_TEXTURE_2D, _colorAttachment)
            // glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, width, height, 0, GL_RGBA, GL_UNSIGNED_BYTE, NULL)

            // glBindRenderbuffer(GL_RENDERBUFFER, _depthStencilAttachment)
            // glRenderbufferStorage(GL_RENDERBUFFER, GL_DEPTH24_STENCIL8, width, height)

            _settings.Width = width;
            _settings.Height = height;
        }

        public IntPtr CreateTexture(TextureDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // GLuint texture;
            // glGenTextures(1, &texture)
            // glBindTexture(GL_TEXTURE_2D, texture)
            // glTexImage2D(GL_TEXTURE_2D, 0, internalFormat, width, height, 0, format, type, NULL)
            // glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR)
            // glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR)
            // if (desc.MipLevels > 1) glGenerateMipmap(GL_TEXTURE_2D)

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Texture,
                Handle = handle,
                GLHandle = 0, // Would be actual GL handle
                DebugName = desc.DebugName,
                TextureDesc = desc
            };

            return handle;
        }

        /// <summary>
        /// Uploads texture pixel data to a previously created texture.
        /// Matches original engine behavior: swkotor.exe and swkotor2.exe use glTexImage2D/glCompressedTexImage2D
        /// to upload texture data after creating the texture object.
        /// Based on swkotor.exe: FUN_00427c90 @ 0x00427c90 and swkotor2.exe equivalent functions.
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
                Console.WriteLine("[OpenGLBackend] UploadTextureData: Invalid texture handle");
                return false;
            }

            if (info.Type != ResourceType.Texture)
            {
                Console.WriteLine("[OpenGLBackend] UploadTextureData: Handle is not a texture");
                return false;
            }

            if (data.Mipmaps == null || data.Mipmaps.Length == 0)
            {
                Console.WriteLine("[OpenGLBackend] UploadTextureData: No mipmap data provided");
                return false;
            }

            try
            {
                // For OpenGL, we use glTexImage2D/glCompressedTexImage2D to upload texture data for each mipmap level
                // This matches the original engine's pattern exactly:
                // 1. Generate texture if not already created: glGenTextures(1, &textureId)
                // 2. Bind the texture: glBindTexture(GL_TEXTURE_2D, textureId)
                // 3. For each mipmap level: glTexImage2D(GL_TEXTURE_2D, level, internalFormat, width, height, 0, format, type, data)
                //    OR glCompressedTexImage2D for compressed formats
                // 4. Set texture parameters: glTexParameteri
                // Original engine: swkotor.exe: FUN_00427c90 @ 0x00427c90 and swkotor2.exe equivalent functions

                // Step 1: Generate texture if not already created
                if (info.GLHandle == 0)
                {
                    uint textureId = 0;
                    glGenTextures(1, ref textureId);
                    if (textureId == 0)
                    {
                        Console.WriteLine($"[OpenGLBackend] UploadTextureData: glGenTextures failed for texture {info.DebugName}");
                        return false;
                    }
                    info.GLHandle = textureId;
                }

                // Step 2: Bind texture
                glBindTexture(GL_TEXTURE_2D, info.GLHandle);

                // Step 3: Get format conversions
                uint format = ConvertTextureFormatToOpenGL(data.Format);
                uint internalFormat = GetOpenGLInternalFormat(data.Format);
                uint dataType = GetOpenGLDataType(data.Format);
                bool isCompressed = IsCompressedFormat(data.Format);

                // Step 4: Validate texture format matches upload data format
                if (info.TextureDesc.Format != data.Format)
                {
                    Console.WriteLine($"[OpenGLBackend] UploadTextureData: Texture format mismatch. Expected {info.TextureDesc.Format}, got {data.Format}");
                    glBindTexture(GL_TEXTURE_2D, 0);
                    return false;
                }

                // Step 5: Upload each mipmap level
                for (int i = 0; i < data.Mipmaps.Length; i++)
                {
                    TextureMipmapData mipmap = data.Mipmaps[i];

                    // Validate mipmap data
                    if (mipmap.Data == null || mipmap.Data.Length == 0)
                    {
                        Console.WriteLine($"[OpenGLBackend] UploadTextureData: Mipmap {i} has no data for texture {info.DebugName}");
                        glBindTexture(GL_TEXTURE_2D, 0);
                        return false;
                    }

                    // Validate mipmap dimensions
                    if (mipmap.Width <= 0 || mipmap.Height <= 0)
                    {
                        Console.WriteLine($"[OpenGLBackend] UploadTextureData: Invalid mipmap dimensions {mipmap.Width}x{mipmap.Height} for mipmap {i}");
                        glBindTexture(GL_TEXTURE_2D, 0);
                        return false;
                    }

                    // Validate mipmap level matches index
                    if (mipmap.Level != i)
                    {
                        Console.WriteLine($"[OpenGLBackend] UploadTextureData: Mipmap level mismatch. Expected {i}, got {mipmap.Level}");
                        glBindTexture(GL_TEXTURE_2D, 0);
                        return false;
                    }

                    // Prepare data for upload (handle BGRA conversion if needed)
                    byte[] uploadData = mipmap.Data;
                    uint uploadFormat = format;

                    // Convert BGRA to RGBA for OpenGL compatibility (matches original engine behavior)
                    // Check source texture format enum to determine if conversion is needed
                    if (data.Format == TextureFormat.B8G8R8A8_UNorm || data.Format == TextureFormat.B8G8R8A8_UNorm_SRGB)
                    {
                        uploadData = ConvertBGRAToRGBA(mipmap.Data);
                        uploadFormat = GL_RGBA;
                    }

                    // Pin data for P/Invoke
                    GCHandle pinnedData = GCHandle.Alloc(uploadData, GCHandleType.Pinned);
                    try
                    {
                        IntPtr dataPtr = pinnedData.AddrOfPinnedObject();

                        if (isCompressed)
                        {
                            // Upload compressed texture data
                            // Calculate expected compressed size
                            int expectedSize = CalculateCompressedTextureSize(data.Format, mipmap.Width, mipmap.Height);
                            if (uploadData.Length < expectedSize)
                            {
                                Console.WriteLine($"[OpenGLBackend] UploadTextureData: Compressed mipmap {i} data size mismatch. Expected {expectedSize} bytes, got {uploadData.Length}");
                                glBindTexture(GL_TEXTURE_2D, 0);
                                return false;
                            }

                            // Upload compressed texture using glCompressedTexImage2D
                            // Matches original engine: swkotor.exe uses glCompressedTexImage2D for DXT/S3TC textures
                            glCompressedTexImage2D(GL_TEXTURE_2D, mipmap.Level, internalFormat, mipmap.Width, mipmap.Height, 0, expectedSize, dataPtr);
                        }
                        else
                        {
                            // Upload uncompressed texture data
                            // Matches original engine: swkotor.exe: FUN_00427c90 @ 0x00427c90 uses glTexImage2D
                            glTexImage2D(GL_TEXTURE_2D, mipmap.Level, (int)internalFormat, mipmap.Width, mipmap.Height, 0, uploadFormat, dataType, dataPtr);
                        }

                        // Check for OpenGL errors
                        uint error = glGetError();
                        if (error != GL_NO_ERROR)
                        {
                            string errorName = GetGLErrorName(error);
                            Console.WriteLine($"[OpenGLBackend] UploadTextureData: OpenGL error {errorName} (0x{error:X}) uploading mipmap {i} for texture {info.DebugName}");
                            glBindTexture(GL_TEXTURE_2D, 0);
                            return false;
                        }
                    }
                    finally
                    {
                        pinnedData.Free();
                    }
                }

                // Step 6: Set texture parameters
                // Matches original engine: swkotor.exe sets texture filtering parameters
                if (data.Mipmaps.Length > 1)
                {
                    // Use mipmap filtering if multiple mip levels
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR_MIPMAP_LINEAR);
                }
                else
                {
                    // Use linear filtering for single mip level
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                }
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);

                // Step 7: Unbind texture
                glBindTexture(GL_TEXTURE_2D, 0);

                // Step 8: Store upload data for reference (may be needed for texture recreation)
                info.UploadData = data;
                _resources[handle] = info;

                Console.WriteLine($"[OpenGLBackend] UploadTextureData: Successfully uploaded {data.Mipmaps.Length} mipmap levels for texture {info.DebugName} (GL handle: {info.GLHandle})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpenGLBackend] UploadTextureData: Exception uploading texture: {ex.Message}");
                return false;
            }
        }

        public IntPtr CreateBuffer(BufferDescription desc)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // GLuint buffer;
            // glGenBuffers(1, &buffer)
            // glBindBuffer(target, buffer)
            // glBufferData(target, desc.ByteSize, NULL, usage)

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
                GLHandle = 0,
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

            // Create shaders
            // GLuint vs = glCreateShader(GL_VERTEX_SHADER)
            // glShaderSource(vs, 1, &vertexSource, NULL)
            // glCompileShader(vs)
            // ... repeat for other shader stages

            // Create program
            // GLuint program = glCreateProgram()
            // glAttachShader(program, vs)
            // glAttachShader(program, fs)
            // glLinkProgram(program)

            // Create VAO for input layout
            // GLuint vao;
            // glGenVertexArrays(1, &vao)
            // glBindVertexArray(vao)
            // ... configure vertex attributes

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Pipeline,
                Handle = handle,
                GLHandle = 0,
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
            // OpenGL does not support hardware raytracing
            if (level != RaytracingLevel.Disabled)
            {
                Console.WriteLine("[OpenGLBackend] Raytracing not supported in OpenGL. Use Vulkan or DirectX 12 for raytracing.");
            }
        }

        public FrameStatistics GetFrameStatistics()
        {
            return _lastFrameStats;
        }

        public IDevice GetDevice()
        {
            // OpenGL does not support hardware raytracing
            // Return null as per interface documentation
            return null;
        }

        #region OpenGL Specific Methods

        /// <summary>
        /// Dispatches compute shader work.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glDispatchCompute.xhtml
        /// Requires OpenGL 4.3+
        /// </summary>
        public void DispatchCompute(int numGroupsX, int numGroupsY, int numGroupsZ)
        {
            if (!_initialized || !_capabilities.SupportsComputeShaders)
            {
                return;
            }

            // glDispatchCompute(numGroupsX, numGroupsY, numGroupsZ)
            // glMemoryBarrier(GL_SHADER_STORAGE_BARRIER_BIT)
        }

        /// <summary>
        /// Sets the viewport.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glViewport.xhtml
        /// </summary>
        public void SetViewport(int x, int y, int width, int height)
        {
            if (!_initialized)
            {
                return;
            }

            // glViewport(x, y, width, height)
        }

        /// <summary>
        /// Draws non-indexed geometry using VAO.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glDrawArrays.xhtml
        /// </summary>
        public void DrawArrays(GLPrimitiveType mode, int first, int count)
        {
            if (!_initialized)
            {
                return;
            }

            // glDrawArrays(mode, first, count)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += count / 3;
        }

        /// <summary>
        /// Draws indexed geometry using VAO.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glDrawElements.xhtml
        /// </summary>
        public void DrawElements(GLPrimitiveType mode, int count, GLIndexType type, int offset)
        {
            if (!_initialized)
            {
                return;
            }

            // glDrawElements(mode, count, type, (void*)offset)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += count / 3;
        }

        /// <summary>
        /// Draws instanced geometry.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glDrawElementsInstanced.xhtml
        /// </summary>
        public void DrawElementsInstanced(GLPrimitiveType mode, int count, GLIndexType type, int offset, int instanceCount)
        {
            if (!_initialized)
            {
                return;
            }

            // glDrawElementsInstanced(mode, count, type, (void*)offset, instanceCount)
            _lastFrameStats.DrawCalls++;
            _lastFrameStats.TrianglesRendered += (count / 3) * instanceCount;
        }

        /// <summary>
        /// Multi-draw indirect for AZDO rendering.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glMultiDrawElementsIndirect.xhtml
        /// Requires OpenGL 4.3+
        /// </summary>
        public void MultiDrawElementsIndirect(GLPrimitiveType mode, GLIndexType type, IntPtr indirectBuffer, int drawCount, int stride)
        {
            if (!_initialized || _majorVersion < 4 || (_majorVersion == 4 && _minorVersion < 3))
            {
                return;
            }

            // glMultiDrawElementsIndirect(mode, type, indirectBuffer, drawCount, stride)
            _lastFrameStats.DrawCalls++;
        }

        /// <summary>
        /// Creates a shader storage buffer object (SSBO).
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glBufferData.xhtml
        /// Requires OpenGL 4.3+
        /// </summary>
        public IntPtr CreateShaderStorageBuffer(int sizeInBytes)
        {
            if (!_initialized || !_capabilities.SupportsComputeShaders)
            {
                return IntPtr.Zero;
            }

            // GLuint ssbo;
            // glGenBuffers(1, &ssbo)
            // glBindBuffer(GL_SHADER_STORAGE_BUFFER, ssbo)
            // glBufferData(GL_SHADER_STORAGE_BUFFER, sizeInBytes, NULL, GL_DYNAMIC_COPY)

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Buffer,
                Handle = handle,
                GLHandle = 0,
                DebugName = "SSBO"
            };

            return handle;
        }

        /// <summary>
        /// Binds a shader storage buffer to a binding point.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glBindBufferBase.xhtml
        /// </summary>
        public void BindShaderStorageBuffer(IntPtr bufferHandle, int bindingPoint)
        {
            if (!_initialized)
            {
                return;
            }

            // glBindBufferBase(GL_SHADER_STORAGE_BUFFER, bindingPoint, glHandle)
        }

        /// <summary>
        /// Creates an immutable texture storage for bindless textures.
        /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glTexStorage2D.xhtml
        /// </summary>
        public IntPtr CreateImmutableTexture2D(int width, int height, int levels, GLInternalFormat internalFormat)
        {
            if (!_initialized)
            {
                return IntPtr.Zero;
            }

            // GLuint texture;
            // glGenTextures(1, &texture)
            // glBindTexture(GL_TEXTURE_2D, texture)
            // glTexStorage2D(GL_TEXTURE_2D, levels, internalFormat, width, height)

            IntPtr handle = new IntPtr(_nextResourceHandle++);
            _resources[handle] = new ResourceInfo
            {
                Type = ResourceType.Texture,
                Handle = handle,
                GLHandle = 0,
                DebugName = "ImmutableTexture2D"
            };

            return handle;
        }

        #endregion

        #region OpenGL P/Invoke Declarations

        // OpenGL texture functions
        // Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glGenTextures.xhtml
        // Matches original engine: swkotor.exe and swkotor2.exe use these exact functions
        [DllImport("opengl32.dll", EntryPoint = "glGenTextures")]
        private static extern void glGenTextures(int n, ref uint textures);

        [DllImport("opengl32.dll", EntryPoint = "glBindTexture")]
        private static extern void glBindTexture(uint target, uint texture);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D")]
        private static extern void glTexImage2D(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, IntPtr pixels);

        [DllImport("opengl32.dll", EntryPoint = "glCompressedTexImage2D")]
        private static extern void glCompressedTexImage2D(uint target, int level, uint internalformat, int width, int height, int border, int imageSize, IntPtr data);

        [DllImport("opengl32.dll", EntryPoint = "glTexParameteri")]
        private static extern void glTexParameteri(uint target, uint pname, int param);

        [DllImport("opengl32.dll", EntryPoint = "glGetError")]
        private static extern uint glGetError();

        [DllImport("opengl32.dll", EntryPoint = "glDeleteTextures")]
        private static extern void glDeleteTextures(int n, ref uint textures);

        // OpenGL constants
        // Based on OpenGL API specification and original engine usage
        private const uint GL_TEXTURE_2D = 0x0DE1;
        private const uint GL_TEXTURE_MIN_FILTER = 0x2801;
        private const uint GL_TEXTURE_MAG_FILTER = 0x2800;
        private const uint GL_TEXTURE_WRAP_S = 0x2802;
        private const uint GL_TEXTURE_WRAP_T = 0x2803;
        private const uint GL_LINEAR = 0x2601;
        private const uint GL_LINEAR_MIPMAP_LINEAR = 0x2703;
        private const uint GL_NEAREST = 0x2600;
        private const uint GL_CLAMP_TO_EDGE = 0x812F;
        private const uint GL_RGBA = 0x1908;
        private const uint GL_RGB = 0x1907;
        private const uint GL_RED = 0x1903;
        private const uint GL_RG = 0x8227;
        private const uint GL_BGRA = 0x80E1;
        private const uint GL_BGR = 0x80E0;
        private const uint GL_UNSIGNED_BYTE = 0x1401;
        private const uint GL_FLOAT = 0x1406;
        private const uint GL_HALF_FLOAT = 0x140B;
        private const uint GL_UNSIGNED_SHORT = 0x1403;
        private const uint GL_UNSIGNED_INT = 0x1405;

        // Internal format constants (matching GLInternalFormat enum values)
        private const uint GL_RGBA8 = 0x8058;
        private const uint GL_RGB8 = 0x8051;
        private const uint GL_RG8 = 0x822B;
        private const uint GL_R8 = 0x8229;
        private const uint GL_RGBA16F = 0x881A;
        private const uint GL_RGB16F = 0x881B;
        private const uint GL_RG16F = 0x822F;
        private const uint GL_R16F = 0x822D;
        private const uint GL_RGBA32F = 0x8814;
        private const uint GL_RGB32F = 0x8815;
        private const uint GL_RG32F = 0x8230;
        private const uint GL_R32F = 0x822E;
        private const uint GL_SRGB8_ALPHA8 = 0x8C43;
        private const uint GL_SRGB8 = 0x8C41;

        // Compressed texture formats (S3TC/DXT)
        // Based on original engine: swkotor.exe and swkotor2.exe use S3TC compression
        private const uint GL_COMPRESSED_RGB_S3TC_DXT1_EXT = 0x83F0;
        private const uint GL_COMPRESSED_RGBA_S3TC_DXT1_EXT = 0x83F1;
        private const uint GL_COMPRESSED_RGBA_S3TC_DXT3_EXT = 0x83F2;
        private const uint GL_COMPRESSED_RGBA_S3TC_DXT5_EXT = 0x83F3;

        // Error codes
        private const uint GL_NO_ERROR = 0x0000;
        private const uint GL_INVALID_ENUM = 0x0500;
        private const uint GL_INVALID_VALUE = 0x0501;
        private const uint GL_INVALID_OPERATION = 0x0502;
        private const uint GL_OUT_OF_MEMORY = 0x0505;

        #endregion

        #region Texture Format Conversion

        /// <summary>
        /// Converts TextureFormat to OpenGL format constant.
        /// Matches original engine behavior: swkotor.exe and swkotor2.exe use these format mappings.
        /// Based on swkotor.exe: FUN_00427c90 @ 0x00427c90 texture format conversion.
        /// </summary>
        private uint ConvertTextureFormatToOpenGL(TextureFormat format)
        {
            switch (format)
            {
                // Uncompressed formats
                case TextureFormat.R8_UNorm:
                    return GL_RED;
                case TextureFormat.R8G8_UNorm:
                    return GL_RG;
                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                    return GL_RGBA;
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                    // BGRA is converted to RGBA during upload (see UploadTextureData)
                    // Return RGBA here since that's the format OpenGL will receive
                    return GL_RGBA;
                case TextureFormat.R16_Float:
                    return GL_RED;
                case TextureFormat.R16G16_Float:
                    return GL_RG;
                case TextureFormat.R16G16B16A16_Float:
                    return GL_RGBA;
                case TextureFormat.R32_Float:
                    return GL_RED;
                case TextureFormat.R32G32_Float:
                    return GL_RG;
                case TextureFormat.R32G32B32_Float:
                    return GL_RGB;
                case TextureFormat.R32G32B32A32_Float:
                    return GL_RGBA;
                case TextureFormat.R11G11B10_Float:
                    return GL_RGB;
                case TextureFormat.R10G10B10A2_UNorm:
                    return GL_RGBA;
                // Compressed formats (returned format is not used for compressed, but we return RGBA for consistency)
                case TextureFormat.BC1_UNorm:
                case TextureFormat.BC1_UNorm_SRGB:
                case TextureFormat.BC2_UNorm:
                case TextureFormat.BC2_UNorm_SRGB:
                case TextureFormat.BC3_UNorm:
                case TextureFormat.BC3_UNorm_SRGB:
                case TextureFormat.BC4_UNorm:
                case TextureFormat.BC5_UNorm:
                case TextureFormat.BC6H_UFloat:
                case TextureFormat.BC7_UNorm:
                case TextureFormat.BC7_UNorm_SRGB:
                    return GL_RGBA; // Not used for compressed, but required for function signature
                // Depth formats
                case TextureFormat.D16_UNorm:
                case TextureFormat.D24_UNorm_S8_UInt:
                case TextureFormat.D32_Float:
                case TextureFormat.D32_Float_S8_UInt:
                    return GL_RED; // Depth formats use special internal formats
                default:
                    Console.WriteLine($"[OpenGLBackend] ConvertTextureFormatToOpenGL: Unknown format {format}, defaulting to GL_RGBA");
                    return GL_RGBA;
            }
        }

        /// <summary>
        /// Gets OpenGL internal format constant for a texture format.
        /// Matches original engine behavior: swkotor.exe and swkotor2.exe use these internal format mappings.
        /// Based on swkotor.exe: FUN_00427c90 @ 0x00427c90 internal format selection.
        /// </summary>
        private uint GetOpenGLInternalFormat(TextureFormat format)
        {
            switch (format)
            {
                // Uncompressed 8-bit formats
                case TextureFormat.R8_UNorm:
                    return GL_R8;
                case TextureFormat.R8G8_UNorm:
                    return GL_RG8;
                case TextureFormat.R8G8B8A8_UNorm:
                    return GL_RGBA8;
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                    return GL_SRGB8_ALPHA8;
                case TextureFormat.B8G8R8A8_UNorm:
                    // BGRA converted to RGBA8 internally
                    return GL_RGBA8;
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                    return GL_SRGB8_ALPHA8;
                // 16-bit float formats
                case TextureFormat.R16_Float:
                    return GL_R16F;
                case TextureFormat.R16G16_Float:
                    return GL_RG16F;
                case TextureFormat.R16G16B16A16_Float:
                    return GL_RGBA16F;
                // 32-bit float formats
                case TextureFormat.R32_Float:
                    return GL_R32F;
                case TextureFormat.R32G32_Float:
                    return GL_RG32F;
                case TextureFormat.R32G32B32_Float:
                    return GL_RGB32F;
                case TextureFormat.R32G32B32A32_Float:
                    return GL_RGBA32F;
                // Packed formats
                case TextureFormat.R11G11B10_Float:
                    return GL_R11F_G11F_B10F; // 0x8C3A (not in our constants, but used)
                case TextureFormat.R10G10B10A2_UNorm:
                    return GL_RGB10_A2; // 0x8059 (not in our constants, but used)
                // Compressed formats (S3TC/DXT)
                case TextureFormat.BC1_UNorm:
                    return GL_COMPRESSED_RGB_S3TC_DXT1_EXT;
                case TextureFormat.BC1_UNorm_SRGB:
                    return GL_COMPRESSED_RGB_S3TC_DXT1_EXT; // SRGB handled via extension
                case TextureFormat.BC2_UNorm:
                    return GL_COMPRESSED_RGBA_S3TC_DXT3_EXT;
                case TextureFormat.BC2_UNorm_SRGB:
                    return GL_COMPRESSED_RGBA_S3TC_DXT3_EXT;
                case TextureFormat.BC3_UNorm:
                    return GL_COMPRESSED_RGBA_S3TC_DXT5_EXT;
                case TextureFormat.BC3_UNorm_SRGB:
                    return GL_COMPRESSED_RGBA_S3TC_DXT5_EXT;
                case TextureFormat.BC4_UNorm:
                    return GL_COMPRESSED_RED_RGTC1; // 0x8DBB (not in our constants)
                case TextureFormat.BC5_UNorm:
                    return GL_COMPRESSED_RG_RGTC2; // 0x8DBD (not in our constants)
                // Depth formats
                case TextureFormat.D16_UNorm:
                    return GL_DEPTH_COMPONENT16; // 0x81A5
                case TextureFormat.D24_UNorm_S8_UInt:
                    return GL_DEPTH24_STENCIL8; // 0x88F0
                case TextureFormat.D32_Float:
                    return GL_DEPTH_COMPONENT32F; // 0x8CAC
                case TextureFormat.D32_Float_S8_UInt:
                    return GL_DEPTH32F_STENCIL8; // 0x8CAD
                default:
                    Console.WriteLine($"[OpenGLBackend] GetOpenGLInternalFormat: Unknown format {format}, defaulting to GL_RGBA8");
                    return GL_RGBA8;
            }
        }

        /// <summary>
        /// Gets OpenGL data type for a texture format.
        /// </summary>
        private uint GetOpenGLDataType(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.R16_Float:
                case TextureFormat.R16G16_Float:
                case TextureFormat.R16G16B16A16_Float:
                    return GL_HALF_FLOAT;
                case TextureFormat.R32_Float:
                case TextureFormat.R32G32_Float:
                case TextureFormat.R32G32B32_Float:
                case TextureFormat.R32G32B32A32_Float:
                case TextureFormat.R11G11B10_Float:
                case TextureFormat.D32_Float:
                case TextureFormat.D32_Float_S8_UInt:
                    return GL_FLOAT;
                default:
                    return GL_UNSIGNED_BYTE;
            }
        }

        /// <summary>
        /// Checks if a texture format is compressed.
        /// </summary>
        private bool IsCompressedFormat(TextureFormat format)
        {
            return format == TextureFormat.BC1_UNorm ||
                   format == TextureFormat.BC1_UNorm_SRGB ||
                   format == TextureFormat.BC2_UNorm ||
                   format == TextureFormat.BC2_UNorm_SRGB ||
                   format == TextureFormat.BC3_UNorm ||
                   format == TextureFormat.BC3_UNorm_SRGB ||
                   format == TextureFormat.BC4_UNorm ||
                   format == TextureFormat.BC5_UNorm ||
                   format == TextureFormat.BC6H_UFloat ||
                   format == TextureFormat.BC7_UNorm ||
                   format == TextureFormat.BC7_UNorm_SRGB;
        }

        /// <summary>
        /// Calculates the expected data size for a compressed texture mipmap.
        /// Based on S3TC/DXT block compression: 4x4 pixel blocks.
        /// </summary>
        private int CalculateCompressedTextureSize(TextureFormat format, int width, int height)
        {
            // S3TC/DXT formats use 4x4 pixel blocks
            int blockWidth = (width + 3) / 4;
            int blockHeight = (height + 3) / 4;
            int blockSize = 0;

            switch (format)
            {
                case TextureFormat.BC1_UNorm:
                case TextureFormat.BC1_UNorm_SRGB:
                    blockSize = 8; // DXT1: 8 bytes per block
                    break;
                case TextureFormat.BC2_UNorm:
                case TextureFormat.BC2_UNorm_SRGB:
                case TextureFormat.BC3_UNorm:
                case TextureFormat.BC3_UNorm_SRGB:
                    blockSize = 16; // DXT3/DXT5: 16 bytes per block
                    break;
                case TextureFormat.BC4_UNorm:
                    blockSize = 8; // BC4: 8 bytes per block
                    break;
                case TextureFormat.BC5_UNorm:
                    blockSize = 16; // BC5: 16 bytes per block
                    break;
                default:
                    // For other formats, calculate uncompressed size
                    return width * height * GetBytesPerPixel(format);
            }

            return blockWidth * blockHeight * blockSize;
        }

        /// <summary>
        /// Gets the number of bytes per pixel for an uncompressed format.
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
                    return 2;
                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                case TextureFormat.R8G8B8A8_UInt:
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                    return 4;
                case TextureFormat.R16_Float:
                case TextureFormat.R16_UNorm:
                case TextureFormat.R16_UInt:
                case TextureFormat.R16_SInt:
                    return 2;
                case TextureFormat.R16G16_Float:
                case TextureFormat.R16G16_UInt:
                    return 4;
                case TextureFormat.R16G16B16A16_Float:
                case TextureFormat.R16G16B16A16_UInt:
                    return 8;
                case TextureFormat.R32_Float:
                case TextureFormat.R32_UInt:
                case TextureFormat.R32_SInt:
                    return 4;
                case TextureFormat.R32G32_Float:
                case TextureFormat.R32G32_UInt:
                    return 8;
                case TextureFormat.R32G32B32_Float:
                    return 12;
                case TextureFormat.R32G32B32A32_Float:
                case TextureFormat.R32G32B32A32_UInt:
                    return 16;
                case TextureFormat.R11G11B10_Float:
                    return 4;
                case TextureFormat.R10G10B10A2_UNorm:
                case TextureFormat.R10G10B10A2_UInt:
                    return 4;
                default:
                    return 4; // Default to RGBA
            }
        }

        /// <summary>
        /// Converts BGRA pixel data to RGBA.
        /// Matches original engine behavior: swkotor.exe converts BGRA to RGBA for OpenGL.
        /// Based on swkotor.exe: FUN_00427c90 @ 0x00427c90 BGRA conversion.
        ///
        /// BGRA format: [B, G, R, A] per pixel (4 bytes)
        /// RGBA format: [R, G, B, A] per pixel (4 bytes)
        /// Conversion: Swap B and R channels, keep G and A unchanged.
        /// </summary>
        private byte[] ConvertBGRAToRGBA(byte[] bgraData)
        {
            if (bgraData == null || bgraData.Length == 0)
            {
                return bgraData;
            }

            // Validate data length is a multiple of 4 (BGRA = 4 bytes per pixel)
            if (bgraData.Length % 4 != 0)
            {
                Console.WriteLine($"[OpenGLBackend] ConvertBGRAToRGBA: Warning - data length {bgraData.Length} is not a multiple of 4. This may indicate invalid BGRA texture data.");
            }

            byte[] rgbaData = new byte[bgraData.Length];

            // Process complete 4-byte pixels
            int pixelCount = bgraData.Length / 4;
            for (int i = 0; i < pixelCount; i++)
            {
                int offset = i * 4;
                // BGRA -> RGBA: swap R and B channels
                rgbaData[offset] = bgraData[offset + 2];     // R (from B position)
                rgbaData[offset + 1] = bgraData[offset + 1]; // G (unchanged)
                rgbaData[offset + 2] = bgraData[offset];     // B (from R position)
                rgbaData[offset + 3] = bgraData[offset + 3]; // A (unchanged)
            }

            // Copy any remaining bytes (shouldn't happen for valid texture data, but handle gracefully)
            int remainingBytes = bgraData.Length % 4;
            if (remainingBytes > 0)
            {
                int startOffset = pixelCount * 4;
                for (int i = 0; i < remainingBytes; i++)
                {
                    rgbaData[startOffset + i] = bgraData[startOffset + i];
                }
                Console.WriteLine($"[OpenGLBackend] ConvertBGRAToRGBA: Warning - {remainingBytes} leftover bytes copied without conversion. This may indicate invalid BGRA texture data.");
            }

            return rgbaData;
        }

        // Additional OpenGL constants for internal formats not in enum
        private const uint GL_R11F_G11F_B10F = 0x8C3A;
        private const uint GL_RGB10_A2 = 0x8059;
        private const uint GL_COMPRESSED_RED_RGTC1 = 0x8DBB;
        private const uint GL_COMPRESSED_RG_RGTC2 = 0x8DBD;
        private const uint GL_DEPTH_COMPONENT16 = 0x81A5;
        private const uint GL_DEPTH_COMPONENT32F = 0x8CAC;
        private const uint GL_DEPTH24_STENCIL8 = 0x88F0;
        private const uint GL_DEPTH32F_STENCIL8 = 0x8CAD;

        /// <summary>
        /// Gets the name of an OpenGL error code for debugging.
        /// </summary>
        private string GetGLErrorName(uint error)
        {
            switch (error)
            {
                case GL_NO_ERROR:
                    return "GL_NO_ERROR";
                case GL_INVALID_ENUM:
                    return "GL_INVALID_ENUM";
                case GL_INVALID_VALUE:
                    return "GL_INVALID_VALUE";
                case GL_INVALID_OPERATION:
                    return "GL_INVALID_OPERATION";
                case GL_OUT_OF_MEMORY:
                    return "GL_OUT_OF_MEMORY";
                default:
                    return $"Unknown (0x{error:X})";
            }
        }

        #endregion

        private bool InitializeGLLoader()
        {
            // Load OpenGL functions via GLAD, GLEW, or similar
            // This would typically be done by MonoGame's internal initialization
            return true;
        }

        private void QueryGLVersion()
        {
            // glGetIntegerv(GL_MAJOR_VERSION, &_majorVersion)
            // glGetIntegerv(GL_MINOR_VERSION, &_minorVersion)
            // const char* glslVersion = glGetString(GL_SHADING_LANGUAGE_VERSION)

            _majorVersion = 4;
            _minorVersion = 5;
            _glslVersion = "450";
        }

        private bool CheckRequiredExtensions()
        {
            // Check for required extensions
            // const char* extensions = glGetString(GL_EXTENSIONS)
            // or use glGetStringi for GL 3.0+

            // Required: GL_ARB_direct_state_access (or GL 4.5)
            // Required: GL_ARB_buffer_storage (or GL 4.4)
            // Optional: GL_ARB_compute_shader (or GL 4.3)
            // Optional: GL_ARB_tessellation_shader (or GL 4.0)
            // Optional: GL_ARB_bindless_texture

            return true;
        }

        private void QueryCapabilities()
        {
            // GLint maxTextureSize;
            // glGetIntegerv(GL_MAX_TEXTURE_SIZE, &maxTextureSize)

            // GLint maxColorAttachments;
            // glGetIntegerv(GL_MAX_COLOR_ATTACHMENTS, &maxColorAttachments)

            // const char* renderer = glGetString(GL_RENDERER)
            // const char* vendor = glGetString(GL_VENDOR)

            bool supportsCompute = _majorVersion > 4 || (_majorVersion == 4 && _minorVersion >= 3);
            bool supportsTessellation = _majorVersion >= 4;

            _capabilities = new GraphicsCapabilities
            {
                MaxTextureSize = 16384,
                MaxRenderTargets = 8,
                MaxAnisotropy = 16,
                SupportsComputeShaders = supportsCompute,
                SupportsGeometryShaders = true,
                SupportsTessellation = supportsTessellation,
                SupportsRaytracing = false,
                SupportsMeshShaders = false,
                SupportsVariableRateShading = false,
                DedicatedVideoMemory = 0, // Can query via GL_NVX_gpu_memory_info
                SharedSystemMemory = 0,
                VendorName = "Unknown",
                DeviceName = "OpenGL Renderer",
                DriverVersion = GLVersion,
                ActiveBackend = GraphicsBackend.OpenGL,
                ShaderModelVersion = _majorVersion >= 4 ? 5.0f : 4.0f,
                RemixAvailable = false,
                DlssAvailable = false,
                FsrAvailable = true // FSR can work via compute shaders
            };
        }

        private void CreateDefaultFramebuffer()
        {
            // For rendering to window, use default framebuffer (0)
            // For off-screen rendering:
            // glGenFramebuffers(1, &_defaultFramebuffer)
            // glBindFramebuffer(GL_FRAMEBUFFER, _defaultFramebuffer)
            // ... create and attach color/depth textures
        }

        private void SetDefaultState()
        {
            // glEnable(GL_DEPTH_TEST)
            // glDepthFunc(GL_LESS)
            // glEnable(GL_CULL_FACE)
            // glCullFace(GL_BACK)
            // glFrontFace(GL_CCW)
            // glEnable(GL_BLEND)
            // glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA)
        }

        private void DestroyResourceInternal(ResourceInfo info)
        {
            switch (info.Type)
            {
                case ResourceType.Texture:
                    // Delete OpenGL texture
                    // Matches original engine: swkotor.exe uses glDeleteTextures to clean up textures
                    if (info.GLHandle != 0)
                    {
                        uint textureId = info.GLHandle;
                        glDeleteTextures(1, ref textureId);
                        // Check for errors (optional, but good for debugging)
                        uint error = glGetError();
                        if (error != GL_NO_ERROR)
                        {
                            Console.WriteLine($"[OpenGLBackend] DestroyResourceInternal: OpenGL error {GetGLErrorName(error)} deleting texture {info.DebugName}");
                        }
                    }
                    break;
                case ResourceType.Buffer:
                    // glDeleteBuffers(1, &info.GLHandle)
                    break;
                case ResourceType.Pipeline:
                    // glDeleteProgram(info.GLHandle)
                    // glDeleteVertexArrays(1, &vao)
                    break;
            }
        }

        public void Dispose()
        {
            Shutdown();
        }

        private struct ResourceInfo
        {
            public ResourceType Type;
            public IntPtr Handle;
            public uint GLHandle;
            public string DebugName;
            public TextureDescription TextureDesc;
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
    /// OpenGL primitive types.
    /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glDrawElements.xhtml
    /// </summary>
    public enum GLPrimitiveType
    {
        Points = 0x0000,
        Lines = 0x0001,
        LineLoop = 0x0002,
        LineStrip = 0x0003,
        Triangles = 0x0004,
        TriangleStrip = 0x0005,
        TriangleFan = 0x0006,
        LinesAdjacency = 0x000A,
        LineStripAdjacency = 0x000B,
        TrianglesAdjacency = 0x000C,
        TriangleStripAdjacency = 0x000D,
        Patches = 0x000E
    }

    /// <summary>
    /// OpenGL index types.
    /// </summary>
    public enum GLIndexType
    {
        UnsignedByte = 0x1401,
        UnsignedShort = 0x1403,
        UnsignedInt = 0x1405
    }

    /// <summary>
    /// OpenGL internal texture formats.
    /// Based on OpenGL API: https://www.khronos.org/registry/OpenGL-Refpages/gl4/html/glTexStorage2D.xhtml
    /// </summary>
    public enum GLInternalFormat
    {
        R8 = 0x8229,
        R8SNorm = 0x8F94,
        R16 = 0x822A,
        R16SNorm = 0x8F98,
        RG8 = 0x822B,
        RG8SNorm = 0x8F95,
        RG16 = 0x822C,
        RG16SNorm = 0x8F99,
        RGB8 = 0x8051,
        RGBA8 = 0x8058,
        RGBA8SNorm = 0x8F97,
        SRGB8 = 0x8C41,
        SRGB8Alpha8 = 0x8C43,
        R16F = 0x822D,
        RG16F = 0x822F,
        RGB16F = 0x881B,
        RGBA16F = 0x881A,
        R32F = 0x822E,
        RG32F = 0x8230,
        RGB32F = 0x8815,
        RGBA32F = 0x8814,
        Depth16 = 0x81A5,
        Depth24 = 0x81A6,
        Depth32F = 0x8CAC,
        Depth24Stencil8 = 0x88F0,
        Depth32FStencil8 = 0x8CAD,
        // Compressed formats
        CompressedRGBS3tcDxt1 = 0x83F0,
        CompressedRGBAS3tcDxt1 = 0x83F1,
        CompressedRGBAS3tcDxt3 = 0x83F2,
        CompressedRGBAS3tcDxt5 = 0x83F3,
        CompressedRGBBptcSigned = 0x8E8E,
        CompressedRGBBptcUnsigned = 0x8E8F,
        CompressedRGBABptcUNorm = 0x8E8C,
        CompressedSRGBABptcUNorm = 0x8E8D
    }
}

