using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Rendering;

namespace Andastra.Runtime.MonoGame.Backends
{
    /// <summary>
    /// OpenGL ES graphics backend implementation.
    ///
    /// Provides:
    /// - OpenGL ES 3.2 rendering
    /// - EGL-based context creation (cross-platform)
    /// - Mobile and embedded platform support
    /// - Shader-based rendering (no fixed-function pipeline)
    ///
    /// OpenGL ES differences from OpenGL:
    /// - No fixed-function pipeline (must use shaders)
    /// - Removed deprecated functions (glBegin/glEnd, immediate mode, etc.)
    /// - Limited texture formats and capabilities
    /// - Different GLSL precision requirements
    /// - No geometry shaders (ES 2.0/3.0), no tessellation (ES 2.0/3.0)
    /// - Compute shaders available in ES 3.1+
    /// - Vertex buffer objects required (no immediate mode)
    /// - EGL used for context management instead of platform-specific APIs
    ///
    /// Based on OpenGL ES 3.2 specification:
    /// https://www.khronos.org/registry/OpenGL/specs/es/3.2/es_spec_3.2.pdf
    /// https://www.khronos.org/egl
    /// </summary>
    public class OpenGLESBackend : IGraphicsBackend
    {
        private bool _initialized;
        private GraphicsCapabilities _capabilities;
        private RenderSettings _settings;

        // EGL context and surface
        private IntPtr _eglDisplay;
        private IntPtr _eglContext;
        private IntPtr _eglSurface;
        private IntPtr _eglConfig;
        private IntPtr _windowHandle;
        private bool _isPBufferSurface; // True if using pbuffer, false if using window surface

        // Framebuffer objects
        private uint _defaultFramebuffer;
        private uint _colorAttachment;
        private uint _depthStencilAttachment;

        // Resource tracking
        private readonly Dictionary<IntPtr, ResourceInfo> _resources;
        private uint _nextResourceHandle;

        // OpenGL ES version info
        private int _majorVersion;
        private int _minorVersion;
        private string _glslVersion;

        // Frame statistics
        private FrameStatistics _lastFrameStats;

        // OpenGL ES function delegates (loaded dynamically via eglGetProcAddress)
        private delegate void GlGenTexturesDelegate(int n, ref uint textures);
        private delegate void GlBindTextureDelegate(uint target, uint texture);
        private delegate void GlTexImage2DDelegate(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, IntPtr pixels);
        private delegate void GlCompressedTexImage2DDelegate(uint target, int level, uint internalformat, int width, int height, int border, int imageSize, IntPtr data);
        private delegate void GlTexParameteriDelegate(uint target, uint pname, int param);
        private delegate uint GlGetErrorDelegate();
        private delegate void GlDeleteTexturesDelegate(int n, ref uint textures);
        private delegate void GlViewportDelegate(int x, int y, int width, int height);
        private delegate void GlGetIntegervDelegate(uint pname, ref int data);
        private delegate IntPtr GlGetStringDelegate(uint name);
        private delegate void GlClearColorDelegate(float red, float green, float blue, float alpha);
        private delegate void GlClearDelegate(uint mask);
        private delegate void GlEnableDelegate(uint cap);
        private delegate void GlDisableDelegate(uint cap);
        private delegate void GlDepthFuncDelegate(uint func);
        private delegate void GlCullFaceDelegate(uint mode);
        private delegate void GlFrontFaceDelegate(uint mode);
        private delegate void GlBlendFuncDelegate(uint sfactor, uint dfactor);
        private delegate void GlGenBuffersDelegate(int n, ref uint buffers);
        private delegate void GlBindBufferDelegate(uint target, uint buffer);
        private delegate void GlBufferDataDelegate(uint target, int size, IntPtr data, uint usage);
        private delegate void GlDeleteBuffersDelegate(int n, ref uint buffers);
        private delegate uint GlCreateShaderDelegate(uint shaderType);
        private delegate void GlShaderSourceDelegate(uint shader, int count, IntPtr source, IntPtr length);
        private delegate void GlCompileShaderDelegate(uint shader);
        private delegate void GlGetShaderivDelegate(uint shader, uint pname, ref int param);
        private delegate void GlGetShaderInfoLogDelegate(uint shader, int bufSize, ref int length, System.Text.StringBuilder infoLog);
        private delegate void GlDeleteShaderDelegate(uint shader);
        private delegate uint GlCreateProgramDelegate();
        private delegate void GlAttachShaderDelegate(uint program, uint shader);
        private delegate void GlLinkProgramDelegate(uint program);
        private delegate void GlGetProgramivDelegate(uint program, uint pname, ref int param);
        private delegate void GlGetProgramInfoLogDelegate(uint program, int bufSize, ref int length, System.Text.StringBuilder infoLog);
        private delegate void GlDeleteProgramDelegate(uint program);
        private delegate void GlUseProgramDelegate(uint program);
        private delegate void GlGenVertexArraysDelegate(int n, ref uint arrays);
        private delegate void GlBindVertexArrayDelegate(uint array);
        private delegate void GlDeleteVertexArraysDelegate(int n, ref uint arrays);
        private delegate void GlVertexAttribPointerDelegate(uint index, int size, uint type, bool normalized, int stride, IntPtr pointer);
        private delegate void GlEnableVertexAttribArrayDelegate(uint index);
        private delegate void GlBindFramebufferDelegate(uint target, uint framebuffer);
        private delegate void GlGenFramebuffersDelegate(int n, ref uint framebuffers);
        private delegate void GlDeleteFramebuffersDelegate(int n, ref uint framebuffers);
        private delegate void GlFramebufferTexture2DDelegate(uint target, uint attachment, uint textarget, uint texture, int level);
        private delegate uint GlCheckFramebufferStatusDelegate(uint target);
        private delegate void GlGenerateMipmapDelegate(uint target);
        private delegate int GlGetStringiDelegate(uint name, uint index);

        // Loaded function delegates
        private GlGenTexturesDelegate _glGenTextures;
        private GlBindTextureDelegate _glBindTexture;
        private GlTexImage2DDelegate _glTexImage2D;
        private GlCompressedTexImage2DDelegate _glCompressedTexImage2D;
        private GlTexParameteriDelegate _glTexParameteri;
        private GlGetErrorDelegate _glGetError;
        private GlDeleteTexturesDelegate _glDeleteTextures;
        private GlViewportDelegate _glViewport;
        private GlGetIntegervDelegate _glGetIntegerv;
        private GlGetStringDelegate _glGetString;
        private GlClearColorDelegate _glClearColor;
        private GlClearDelegate _glClear;
        private GlEnableDelegate _glEnable;
        private GlDisableDelegate _glDisable;
        private GlDepthFuncDelegate _glDepthFunc;
        private GlCullFaceDelegate _glCullFace;
        private GlFrontFaceDelegate _glFrontFace;
        private GlBlendFuncDelegate _glBlendFunc;
        private GlGenBuffersDelegate _glGenBuffers;
        private GlBindBufferDelegate _glBindBuffer;
        private GlBufferDataDelegate _glBufferData;
        private GlDeleteBuffersDelegate _glDeleteBuffers;
        private GlCreateShaderDelegate _glCreateShader;
        private GlShaderSourceDelegate _glShaderSource;
        private GlCompileShaderDelegate _glCompileShader;
        private GlGetShaderivDelegate _glGetShaderiv;
        private GlGetShaderInfoLogDelegate _glGetShaderInfoLog;
        private GlDeleteShaderDelegate _glDeleteShader;
        private GlCreateProgramDelegate _glCreateProgram;
        private GlAttachShaderDelegate _glAttachShader;
        private GlLinkProgramDelegate _glLinkProgram;
        private GlGetProgramivDelegate _glGetProgramiv;
        private GlGetProgramInfoLogDelegate _glGetProgramInfoLog;
        private GlDeleteProgramDelegate _glDeleteProgram;
        private GlUseProgramDelegate _glUseProgram;
        private GlGenVertexArraysDelegate _glGenVertexArrays;
        private GlBindVertexArrayDelegate _glBindVertexArray;
        private GlDeleteVertexArraysDelegate _glDeleteVertexArrays;
        private GlVertexAttribPointerDelegate _glVertexAttribPointer;
        private GlEnableVertexAttribArrayDelegate _glEnableVertexAttribArray;
        private GlBindFramebufferDelegate _glBindFramebuffer;
        private GlGenFramebuffersDelegate _glGenFramebuffers;
        private GlDeleteFramebuffersDelegate _glDeleteFramebuffers;
        private GlFramebufferTexture2DDelegate _glFramebufferTexture2D;
        private GlCheckFramebufferStatusDelegate _glCheckFramebufferStatus;
        private GlGenerateMipmapDelegate _glGenerateMipmap;
        private GlGetStringiDelegate _glGetStringi;

        // Platform-specific library handle
        private IntPtr _glesLibraryHandle;

        public GraphicsBackend BackendType
        {
            get { return GraphicsBackend.OpenGLES; }
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
            // OpenGL ES does not support hardware raytracing
            get { return false; }
        }

        /// <summary>
        /// Gets the OpenGL ES version string.
        /// </summary>
        public string GLVersion
        {
            get { return _majorVersion + "." + _minorVersion + " ES"; }
        }

        /// <summary>
        /// Gets the GLSL ES version string.
        /// </summary>
        public string GLSLVersion
        {
            get { return _glslVersion; }
        }

        public OpenGLESBackend()
        {
            _resources = new Dictionary<IntPtr, ResourceInfo>();
            _nextResourceHandle = 1;
        }

        /// <summary>
        /// Initializes the OpenGL ES backend with EGL context creation.
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

            // Step 1: Initialize EGL display
            if (!InitializeEGLDisplay())
            {
                Console.WriteLine("[OpenGLESBackend] Failed to initialize EGL display");
                return false;
            }

            // Step 2: Choose EGL configuration
            if (!ChooseEGLConfig())
            {
                Console.WriteLine("[OpenGLESBackend] Failed to choose EGL configuration");
                CleanupEGL();
                return false;
            }

            // Step 3: Create EGL context
            if (!CreateEGLContext())
            {
                Console.WriteLine("[OpenGLESBackend] Failed to create EGL context");
                CleanupEGL();
                return false;
            }

            // Step 4: Create EGL surface (window or pbuffer)
            if (!CreateEGLSurface(IntPtr.Zero))
            {
                Console.WriteLine("[OpenGLESBackend] Failed to create EGL surface");
                CleanupEGL();
                return false;
            }

            // Step 5: Make context current
            if (!eglMakeCurrent(_eglDisplay, _eglSurface, _eglSurface, _eglContext))
            {
                Console.WriteLine("[OpenGLESBackend] Failed to make EGL context current");
                CleanupEGL();
                return false;
            }

            // Step 6: Load OpenGL ES functions
            if (!InitializeGLESLoader())
            {
                Console.WriteLine("[OpenGLESBackend] Failed to initialize OpenGL ES loader");
                CleanupEGL();
                return false;
            }

            // Step 7: Query OpenGL ES version
            QueryGLESVersion();

            // Step 8: Check for required extensions
            if (!CheckRequiredExtensions())
            {
                Console.WriteLine("[OpenGLESBackend] Required OpenGL ES extensions not available");
                CleanupEGL();
                return false;
            }

            // Step 9: Query capabilities
            QueryCapabilities();

            // Step 10: Create default framebuffer (for ES, default framebuffer is 0 when rendering to window)
            CreateDefaultFramebuffer();

            // Step 11: Set default GL state
            SetDefaultState();

            _initialized = true;
            Console.WriteLine("[OpenGLESBackend] Initialized successfully");
            Console.WriteLine("[OpenGLESBackend] OpenGL ES Version: " + GLVersion);
            Console.WriteLine("[OpenGLESBackend] GLSL ES Version: " + _glslVersion);
            Console.WriteLine("[OpenGLESBackend] Renderer: " + _capabilities.DeviceName);

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

            // Cleanup EGL
            CleanupEGL();

            // Unload OpenGL ES library if loaded
            if (_glesLibraryHandle != IntPtr.Zero)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // FreeLibrary on Windows
                    FreeLibrary(_glesLibraryHandle);
                }
                // On Linux/OSX, dlclose would be needed but we don't typically unload libraries
                _glesLibraryHandle = IntPtr.Zero;
            }

            // Clear function delegates
            _glGenTextures = null;
            _glBindTexture = null;
            _glTexImage2D = null;
            _glCompressedTexImage2D = null;
            _glTexParameteri = null;
            _glGetError = null;
            _glDeleteTextures = null;
            _glViewport = null;
            _glGetIntegerv = null;
            _glGetString = null;
            _glClearColor = null;
            _glClear = null;
            _glEnable = null;
            _glDisable = null;
            _glDepthFunc = null;
            _glCullFace = null;
            _glFrontFace = null;
            _glBlendFunc = null;
            _glGenBuffers = null;
            _glBindBuffer = null;
            _glBufferData = null;
            _glDeleteBuffers = null;
            _glCreateShader = null;
            _glShaderSource = null;
            _glCompileShader = null;
            _glGetShaderiv = null;
            _glGetShaderInfoLog = null;
            _glDeleteShader = null;
            _glCreateProgram = null;
            _glAttachShader = null;
            _glLinkProgram = null;
            _glGetProgramiv = null;
            _glGetProgramInfoLog = null;
            _glDeleteProgram = null;
            _glUseProgram = null;
            _glGenVertexArrays = null;
            _glBindVertexArray = null;
            _glDeleteVertexArrays = null;
            _glVertexAttribPointer = null;
            _glEnableVertexAttribArray = null;
            _glBindFramebuffer = null;
            _glGenFramebuffers = null;
            _glDeleteFramebuffers = null;
            _glFramebufferTexture2D = null;
            _glCheckFramebufferStatus = null;
            _glGenerateMipmap = null;
            _glGetStringi = null;

            _initialized = false;
            Console.WriteLine("[OpenGLESBackend] Shutdown complete");
        }

        public void BeginFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Make sure context is current
            if (!eglMakeCurrent(_eglDisplay, _eglSurface, _eglSurface, _eglContext))
            {
                Console.WriteLine("[OpenGLESBackend] Warning: Failed to make context current in BeginFrame");
                return;
            }

            // Bind default framebuffer (0 for window rendering in ES)
            if (_glBindFramebuffer != null)
            {
                const uint GL_FRAMEBUFFER = 0x8D40;
                _glBindFramebuffer(GL_FRAMEBUFFER, _defaultFramebuffer);
            }

            // Clear buffers
            if (_glClearColor != null && _glClear != null)
            {
                _glClearColor(0.0f, 0.0f, 0.0f, 1.0f);
                const uint GL_COLOR_BUFFER_BIT = 0x00004000;
                const uint GL_DEPTH_BUFFER_BIT = 0x00000100;
                const uint GL_STENCIL_BUFFER_BIT = 0x00000400;
                _glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT);
            }

            _lastFrameStats = new FrameStatistics();
        }

        public void EndFrame()
        {
            if (!_initialized)
            {
                return;
            }

            // Swap buffers via EGL
            if (_eglDisplay != IntPtr.Zero && _eglSurface != IntPtr.Zero)
            {
                eglSwapBuffers(_eglDisplay, _eglSurface);
            }

            // Check for GL errors in debug builds
            if (_glGetError != null)
            {
                uint error = _glGetError();
                if (error != GL_NO_ERROR)
                {
                    Console.WriteLine($"[OpenGLESBackend] EndFrame: OpenGL ES error {GetGLESErrorName(error)} (0x{error:X})");
                }
            }
        }

        /// <summary>
        /// Resizes the rendering surface and updates the viewport.
        ///
        /// For EGL window surfaces: The native window typically handles resizing automatically,
        /// but we query the actual surface size and update the viewport accordingly.
        /// For EGL pbuffer surfaces: The surface must be destroyed and recreated with new dimensions
        /// since pbuffers have fixed sizes.
        ///
        /// Based on EGL 1.5 specification and OpenGL ES 3.2 surface management requirements.
        /// </summary>
        public void Resize(int width, int height)
        {
            if (!_initialized)
            {
                return;
            }

            // Validate dimensions
            if (width <= 0 || height <= 0)
            {
                Console.WriteLine($"[OpenGLESBackend] Resize: Invalid dimensions {width}x{height}, minimum is 1x1");
                width = Math.Max(1, width);
                height = Math.Max(1, height);
            }

            // Make sure context is current before resizing
            if (!eglMakeCurrent(_eglDisplay, _eglSurface, _eglSurface, _eglContext))
            {
                Console.WriteLine("[OpenGLESBackend] Resize: Warning - Failed to make context current");
                return;
            }

            // Handle surface resizing based on surface type
            if (_isPBufferSurface)
            {
                // Pbuffer surfaces have fixed dimensions and must be recreated
                // Save reference to old surface before replacing it
                IntPtr oldSurface = _eglSurface;

                // Update settings with new dimensions
                _settings.Width = width;
                _settings.Height = height;

                // Create new pbuffer surface with new dimensions
                // Note: We create the new surface before destroying the old one to allow recovery on failure
                int[] pbufferAttribs = new int[]
                {
                    EGL_WIDTH, width,
                    EGL_HEIGHT, height,
                    EGL_NONE
                };

                IntPtr newSurface = eglCreatePbufferSurface(_eglDisplay, _eglConfig, pbufferAttribs);
                if (newSurface == IntPtr.Zero || newSurface == new IntPtr(-1))
                {
                    uint error = eglGetError();
                    Console.WriteLine($"[OpenGLESBackend] Resize: Failed to create new pbuffer surface with error 0x{error:X}, keeping old surface");
                    // Old surface is still valid, context is still current with it
                    return;
                }

                // New surface created successfully, now we can safely replace the old one
                // Make context non-current before destroying old surface
                eglMakeCurrent(_eglDisplay, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                // Destroy old pbuffer surface
                if (oldSurface != IntPtr.Zero)
                {
                    eglDestroySurface(_eglDisplay, oldSurface);
                }

                // Update surface reference to new surface
                _eglSurface = newSurface;

                // Make context current with new surface
                if (!eglMakeCurrent(_eglDisplay, _eglSurface, _eglSurface, _eglContext))
                {
                    Console.WriteLine("[OpenGLESBackend] Resize: Failed to make context current with new pbuffer surface");
                    // Surface is created but context is not current - this is an error state
                    // The surface will be cleaned up on shutdown
                    return;
                }
            }
            else
            {
                // Window surfaces resize automatically with the native window
                // Query the actual surface size to ensure it matches (some implementations
                // may report slightly different sizes due to DPI scaling or rounding)
                int surfaceWidth = 0;
                int surfaceHeight = 0;

                if (eglQuerySurface(_eglDisplay, _eglSurface, EGL_WIDTH, ref surfaceWidth) &&
                    eglQuerySurface(_eglDisplay, _eglSurface, EGL_HEIGHT, ref surfaceHeight))
                {
                    // Use the actual surface dimensions if they differ from requested
                    // This handles cases where the window manager enforces minimum sizes,
                    // DPI scaling, or other platform-specific constraints
                    if (surfaceWidth > 0 && surfaceHeight > 0)
                    {
                        width = surfaceWidth;
                        height = surfaceHeight;
                    }
                }
                else
                {
                    uint error = eglGetError();
                    Console.WriteLine($"[OpenGLESBackend] Resize: Failed to query window surface size, using requested dimensions. Error: 0x{error:X}");
                }

                // Update settings with actual surface dimensions
                _settings.Width = width;
                _settings.Height = height;
            }

            // Update OpenGL ES viewport to match new surface size
            if (_glViewport != null)
            {
                _glViewport(0, 0, width, height);
            }

            // Check for OpenGL ES errors
            uint glError = (_glGetError != null) ? _glGetError() : GL_NO_ERROR;
            if (glError != GL_NO_ERROR)
            {
                string errorName = GetGLESErrorName(glError);
                Console.WriteLine($"[OpenGLESBackend] Resize: OpenGL ES error {errorName} (0x{glError:X}) setting viewport");
            }

            Console.WriteLine($"[OpenGLESBackend] Resize: Surface resized to {width}x{height} (surface type: {(_isPBufferSurface ? "pbuffer" : "window")})");
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
                Console.WriteLine("[OpenGLESBackend] UploadTextureData: Invalid texture handle");
                return false;
            }

            if (info.Type != ResourceType.Texture)
            {
                Console.WriteLine("[OpenGLESBackend] UploadTextureData: Handle is not a texture");
                return false;
            }

            if (data.Mipmaps == null || data.Mipmaps.Length == 0)
            {
                Console.WriteLine("[OpenGLESBackend] UploadTextureData: No mipmap data provided");
                return false;
            }

            try
            {
                // Generate texture if not already created
                if (info.GLHandle == 0)
                {
                    if (_glGenTextures == null)
                    {
                        Console.WriteLine("[OpenGLESBackend] UploadTextureData: glGenTextures not loaded");
                        return false;
                    }
                    uint textureId = 0;
                    _glGenTextures(1, ref textureId);
                    if (textureId == 0)
                    {
                        Console.WriteLine($"[OpenGLESBackend] UploadTextureData: glGenTextures failed for texture {info.DebugName}");
                        return false;
                    }
                    info.GLHandle = textureId;
                }

                // Bind texture
                if (_glBindTexture == null)
                {
                    Console.WriteLine("[OpenGLESBackend] UploadTextureData: glBindTexture not loaded");
                    return false;
                }
                _glBindTexture(GL_TEXTURE_2D, info.GLHandle);

                // Get format conversions (OpenGL ES has limited format support)
                uint format = ConvertTextureFormatToGLES(data.Format);
                uint internalFormat = GetGLESInternalFormat(data.Format);
                uint dataType = GetGLESDataType(data.Format);
                bool isCompressed = IsCompressedFormat(data.Format);

                // Validate texture format matches upload data format
                if (info.TextureDesc.Format != data.Format)
                {
                    Console.WriteLine($"[OpenGLESBackend] UploadTextureData: Texture format mismatch. Expected {info.TextureDesc.Format}, got {data.Format}");
                    if (_glBindTexture != null)
                    {
                        _glBindTexture(GL_TEXTURE_2D, 0);
                    }
                    return false;
                }

                // Upload each mipmap level
                for (int i = 0; i < data.Mipmaps.Length; i++)
                {
                    TextureMipmapData mipmap = data.Mipmaps[i];

                    if (mipmap.Data == null || mipmap.Data.Length == 0)
                    {
                        Console.WriteLine($"[OpenGLESBackend] UploadTextureData: Mipmap {i} has no data for texture {info.DebugName}");
                        if (_glBindTexture != null)
                        {
                            _glBindTexture(GL_TEXTURE_2D, 0);
                        }
                        return false;
                    }

                    if (mipmap.Width <= 0 || mipmap.Height <= 0)
                    {
                        Console.WriteLine($"[OpenGLESBackend] UploadTextureData: Invalid mipmap dimensions {mipmap.Width}x{mipmap.Height} for mipmap {i}");
                        if (_glBindTexture != null)
                        {
                            _glBindTexture(GL_TEXTURE_2D, 0);
                        }
                        return false;
                    }

                    if (mipmap.Level != i)
                    {
                        Console.WriteLine($"[OpenGLESBackend] UploadTextureData: Mipmap level mismatch. Expected {i}, got {mipmap.Level}");
                        if (_glBindTexture != null)
                        {
                            _glBindTexture(GL_TEXTURE_2D, 0);
                        }
                        return false;
                    }

                    // Prepare data for upload
                    byte[] uploadData = mipmap.Data;
                    uint uploadFormat = format;

                    // OpenGL ES does not support BGRA format - must convert to RGBA
                    // Check if format indicates BGRA (B8G8R8A8 formats)
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
                            int expectedSize = CalculateCompressedTextureSize(data.Format, mipmap.Width, mipmap.Height);
                            if (uploadData.Length < expectedSize)
                            {
                                Console.WriteLine($"[OpenGLESBackend] UploadTextureData: Compressed mipmap {i} data size mismatch. Expected {expectedSize} bytes, got {uploadData.Length}");
                                if (_glBindTexture != null)
                                {
                                    _glBindTexture(GL_TEXTURE_2D, 0);
                                }
                                return false;
                            }

                            if (_glCompressedTexImage2D != null)
                            {
                                _glCompressedTexImage2D(GL_TEXTURE_2D, mipmap.Level, internalFormat, mipmap.Width, mipmap.Height, 0, expectedSize, dataPtr);
                            }
                            else
                            {
                                Console.WriteLine("[OpenGLESBackend] UploadTextureData: glCompressedTexImage2D not loaded");
                                if (_glBindTexture != null)
                                {
                                    _glBindTexture(GL_TEXTURE_2D, 0);
                                }
                                return false;
                            }
                        }
                        else
                        {
                            // Upload uncompressed texture data
                            if (_glTexImage2D != null)
                            {
                                _glTexImage2D(GL_TEXTURE_2D, mipmap.Level, (int)internalFormat, mipmap.Width, mipmap.Height, 0, uploadFormat, dataType, dataPtr);
                            }
                            else
                            {
                                Console.WriteLine("[OpenGLESBackend] UploadTextureData: glTexImage2D not loaded");
                                if (_glBindTexture != null)
                                {
                                    _glBindTexture(GL_TEXTURE_2D, 0);
                                }
                                return false;
                            }
                        }

                        // Check for OpenGL ES errors
                        uint error = (_glGetError != null) ? _glGetError() : GL_NO_ERROR;
                        if (error != GL_NO_ERROR)
                        {
                            string errorName = GetGLESErrorName(error);
                            Console.WriteLine($"[OpenGLESBackend] UploadTextureData: OpenGL ES error {errorName} (0x{error:X}) uploading mipmap {i} for texture {info.DebugName}");
                            if (_glBindTexture != null)
                            {
                                _glBindTexture(GL_TEXTURE_2D, 0);
                            }
                            return false;
                        }
                    }
                    finally
                    {
                        pinnedData.Free();
                    }
                }

                // Set texture parameters
                if (_glTexParameteri != null)
                {
                    if (data.Mipmaps.Length > 1)
                    {
                        _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR_MIPMAP_LINEAR);
                    }
                    else
                    {
                        _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                    }
                    _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                    _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                    _glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                }

                // Unbind texture
                if (_glBindTexture != null)
                {
                    _glBindTexture(GL_TEXTURE_2D, 0);
                }

                // Store upload data for reference
                info.UploadData = data;
                _resources[handle] = info;

                Console.WriteLine($"[OpenGLESBackend] UploadTextureData: Successfully uploaded {data.Mipmaps.Length} mipmap levels for texture {info.DebugName} (GL handle: {info.GLHandle})");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpenGLESBackend] UploadTextureData: Exception uploading texture: {ex.Message}");
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

            // Create shaders (OpenGL ES requires shaders - no fixed-function pipeline)
            // GLuint vs = glCreateShader(GL_VERTEX_SHADER)
            // glShaderSource(vs, 1, &vertexSource, NULL)
            // glCompileShader(vs)
            // ... repeat for fragment shader (required in ES)

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
            // OpenGL ES does not support hardware raytracing
            if (level != RaytracingLevel.Disabled)
            {
                Console.WriteLine("[OpenGLESBackend] Raytracing not supported in OpenGL ES. Use Vulkan or DirectX 12 for raytracing.");
            }
        }

        public FrameStatistics GetFrameStatistics()
        {
            return _lastFrameStats;
        }

        public IDevice GetDevice()
        {
            // OpenGL ES does not support hardware raytracing
            // Return null as per interface documentation
            return null;
        }

        #region EGL Context Management

        /// <summary>
        /// Initializes EGL display connection.
        /// </summary>
        private bool InitializeEGLDisplay()
        {
            // Get default display
            // On Windows: eglGetDisplay(EGL_DEFAULT_DISPLAY) -> typically returns native display handle
            // On Linux: eglGetDisplay(EGL_DEFAULT_DISPLAY) -> X11 display
            // On Android: eglGetDisplay(EGL_DEFAULT_DISPLAY) -> native window
            // On iOS: Uses different APIs (CGL/EAGL), but can use EGL with some workarounds

            _eglDisplay = eglGetDisplay(EGL_DEFAULT_DISPLAY);
            if (_eglDisplay == IntPtr.Zero || _eglDisplay == new IntPtr(-1))
            {
                Console.WriteLine("[OpenGLESBackend] eglGetDisplay failed");
                return false;
            }

            // Initialize EGL
            int major = 0;
            int minor = 0;
            if (!eglInitialize(_eglDisplay, ref major, ref minor))
            {
                Console.WriteLine("[OpenGLESBackend] eglInitialize failed");
                return false;
            }

            Console.WriteLine($"[OpenGLESBackend] EGL initialized: version {major}.{minor}");
            return true;
        }

        /// <summary>
        /// Chooses EGL configuration matching render settings.
        /// </summary>
        private bool ChooseEGLConfig()
        {
            // EGL configuration attributes
            // Request OpenGL ES 3.2 context
            int[] attribs = new int[]
            {
                EGL_RENDERABLE_TYPE, EGL_OPENGL_ES3_BIT,
                EGL_SURFACE_TYPE, EGL_WINDOW_BIT,
                EGL_RED_SIZE, 8,
                EGL_GREEN_SIZE, 8,
                EGL_BLUE_SIZE, 8,
                EGL_ALPHA_SIZE, 8,
                EGL_DEPTH_SIZE, 24,
                EGL_STENCIL_SIZE, 8,
                EGL_SAMPLE_BUFFERS, 0,
                EGL_SAMPLES, 0,
                EGL_NONE
            };

            int numConfigs = 0;
            IntPtr configs = IntPtr.Zero;
            if (!eglChooseConfig(_eglDisplay, attribs, ref configs, 1, ref numConfigs))
            {
                Console.WriteLine("[OpenGLESBackend] eglChooseConfig failed");
                return false;
            }

            if (numConfigs == 0)
            {
                Console.WriteLine("[OpenGLESBackend] No EGL configurations found");
                return false;
            }

            // Use first matching configuration
            // In a production system, we'd iterate and select the best match
            _eglConfig = configs;

            return true;
        }

        /// <summary>
        /// Creates EGL context with OpenGL ES 3.2.
        /// </summary>
        private bool CreateEGLContext()
        {
            // Context attributes for OpenGL ES 3.2
            int[] contextAttribs = new int[]
            {
                EGL_CONTEXT_MAJOR_VERSION, 3,
                EGL_CONTEXT_MINOR_VERSION, 2,
                EGL_NONE
            };

            _eglContext = eglCreateContext(_eglDisplay, _eglConfig, IntPtr.Zero, contextAttribs);
            if (_eglContext == IntPtr.Zero || _eglContext == new IntPtr(-1))
            {
                uint error = eglGetError();
                Console.WriteLine($"[OpenGLESBackend] eglCreateContext failed with error 0x{error:X}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates EGL surface (window or pbuffer).
        /// </summary>
        private bool CreateEGLSurface(IntPtr nativeWindow)
        {
            // For window rendering, create window surface
            // For headless rendering, create pbuffer surface
            if (nativeWindow == IntPtr.Zero)
            {
                // Create pbuffer surface for headless rendering
                int[] pbufferAttribs = new int[]
                {
                    EGL_WIDTH, _settings.Width,
                    EGL_HEIGHT, _settings.Height,
                    EGL_NONE
                };

                _eglSurface = eglCreatePbufferSurface(_eglDisplay, _eglConfig, pbufferAttribs);
                _isPBufferSurface = true;
                _windowHandle = IntPtr.Zero;
            }
            else
            {
                // Create window surface
                int[] windowAttribs = new int[]
                {
                    EGL_NONE
                };

                _eglSurface = eglCreateWindowSurface(_eglDisplay, _eglConfig, nativeWindow, windowAttribs);
                _isPBufferSurface = false;
                _windowHandle = nativeWindow;
            }

            if (_eglSurface == IntPtr.Zero || _eglSurface == new IntPtr(-1))
            {
                uint error = eglGetError();
                Console.WriteLine($"[OpenGLESBackend] eglCreateWindowSurface/eglCreatePbufferSurface failed with error 0x{error:X}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cleans up EGL resources.
        /// </summary>
        private void CleanupEGL()
        {
            if (_eglDisplay != IntPtr.Zero)
            {
                // Make no context current
                eglMakeCurrent(_eglDisplay, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

                // Destroy surface
                if (_eglSurface != IntPtr.Zero)
                {
                    eglDestroySurface(_eglDisplay, _eglSurface);
                    _eglSurface = IntPtr.Zero;
                }

                // Destroy context
                if (_eglContext != IntPtr.Zero)
                {
                    eglDestroyContext(_eglDisplay, _eglContext);
                    _eglContext = IntPtr.Zero;
                }

                // Terminate display
                eglTerminate(_eglDisplay);
                _eglDisplay = IntPtr.Zero;
            }
        }

        #endregion

        #region OpenGL ES Initialization

        /// <summary>
        /// Initializes OpenGL ES function loader.
        /// Dynamically loads all OpenGL ES functions using eglGetProcAddress.
        /// This is required because OpenGL ES functions must be loaded at runtime,
        /// and extension functions are only available after context creation.
        /// </summary>
        private bool InitializeGLESLoader()
        {
            // Ensure EGL context is current before loading functions
            if (_eglContext == IntPtr.Zero || _eglDisplay == IntPtr.Zero)
            {
                Console.WriteLine("[OpenGLESBackend] InitializeGLESLoader: EGL context not created");
                return false;
            }

            // Load platform-specific OpenGL ES library
            if (!LoadGLESLibrary())
            {
                Console.WriteLine("[OpenGLESBackend] InitializeGLESLoader: Failed to load OpenGL ES library");
                return false;
            }

            // Load core OpenGL ES 3.2 functions
            // Core functions that are always available in ES 3.2
            _glGenTextures = LoadFunction<GlGenTexturesDelegate>("glGenTextures");
            _glBindTexture = LoadFunction<GlBindTextureDelegate>("glBindTexture");
            _glTexImage2D = LoadFunction<GlTexImage2DDelegate>("glTexImage2D");
            _glCompressedTexImage2D = LoadFunction<GlCompressedTexImage2DDelegate>("glCompressedTexImage2D");
            _glTexParameteri = LoadFunction<GlTexParameteriDelegate>("glTexParameteri");
            _glGetError = LoadFunction<GlGetErrorDelegate>("glGetError");
            _glDeleteTextures = LoadFunction<GlDeleteTexturesDelegate>("glDeleteTextures");
            _glViewport = LoadFunction<GlViewportDelegate>("glViewport");
            _glGetIntegerv = LoadFunction<GlGetIntegervDelegate>("glGetIntegerv");
            _glGetString = LoadFunction<GlGetStringDelegate>("glGetString");
            _glClearColor = LoadFunction<GlClearColorDelegate>("glClearColor");
            _glClear = LoadFunction<GlClearDelegate>("glClear");
            _glEnable = LoadFunction<GlEnableDelegate>("glEnable");
            _glDisable = LoadFunction<GlDisableDelegate>("glDisable");
            _glDepthFunc = LoadFunction<GlDepthFuncDelegate>("glDepthFunc");
            _glCullFace = LoadFunction<GlCullFaceDelegate>("glCullFace");
            _glFrontFace = LoadFunction<GlFrontFaceDelegate>("glFrontFace");
            _glBlendFunc = LoadFunction<GlBlendFuncDelegate>("glBlendFunc");
            _glGenBuffers = LoadFunction<GlGenBuffersDelegate>("glGenBuffers");
            _glBindBuffer = LoadFunction<GlBindBufferDelegate>("glBindBuffer");
            _glBufferData = LoadFunction<GlBufferDataDelegate>("glBufferData");
            _glDeleteBuffers = LoadFunction<GlDeleteBuffersDelegate>("glDeleteBuffers");
            _glCreateShader = LoadFunction<GlCreateShaderDelegate>("glCreateShader");
            _glShaderSource = LoadFunction<GlShaderSourceDelegate>("glShaderSource");
            _glCompileShader = LoadFunction<GlCompileShaderDelegate>("glCompileShader");
            _glGetShaderiv = LoadFunction<GlGetShaderivDelegate>("glGetShaderiv");
            _glGetShaderInfoLog = LoadFunction<GlGetShaderInfoLogDelegate>("glGetShaderInfoLog");
            _glDeleteShader = LoadFunction<GlDeleteShaderDelegate>("glDeleteShader");
            _glCreateProgram = LoadFunction<GlCreateProgramDelegate>("glCreateProgram");
            _glAttachShader = LoadFunction<GlAttachShaderDelegate>("glAttachShader");
            _glLinkProgram = LoadFunction<GlLinkProgramDelegate>("glLinkProgram");
            _glGetProgramiv = LoadFunction<GlGetProgramivDelegate>("glGetProgramiv");
            _glGetProgramInfoLog = LoadFunction<GlGetProgramInfoLogDelegate>("glGetProgramInfoLog");
            _glDeleteProgram = LoadFunction<GlDeleteProgramDelegate>("glDeleteProgram");
            _glUseProgram = LoadFunction<GlUseProgramDelegate>("glUseProgram");
            _glGenVertexArrays = LoadFunction<GlGenVertexArraysDelegate>("glGenVertexArrays");
            _glBindVertexArray = LoadFunction<GlBindVertexArrayDelegate>("glBindVertexArray");
            _glDeleteVertexArrays = LoadFunction<GlDeleteVertexArraysDelegate>("glDeleteVertexArrays");
            _glVertexAttribPointer = LoadFunction<GlVertexAttribPointerDelegate>("glVertexAttribPointer");
            _glEnableVertexAttribArray = LoadFunction<GlEnableVertexAttribArrayDelegate>("glEnableVertexAttribArray");
            _glBindFramebuffer = LoadFunction<GlBindFramebufferDelegate>("glBindFramebuffer");
            _glGenFramebuffers = LoadFunction<GlGenFramebuffersDelegate>("glGenFramebuffers");
            _glDeleteFramebuffers = LoadFunction<GlDeleteFramebuffersDelegate>("glDeleteFramebuffers");
            _glFramebufferTexture2D = LoadFunction<GlFramebufferTexture2DDelegate>("glFramebufferTexture2D");
            _glCheckFramebufferStatus = LoadFunction<GlCheckFramebufferStatusDelegate>("glCheckFramebufferStatus");
            _glGenerateMipmap = LoadFunction<GlGenerateMipmapDelegate>("glGenerateMipmap");
            _glGetStringi = LoadFunction<GlGetStringiDelegate>("glGetStringi");

            // Validate that all critical functions were loaded
            if (_glGetError == null || _glGetString == null || _glGetIntegerv == null)
            {
                Console.WriteLine("[OpenGLESBackend] InitializeGLESLoader: Failed to load critical OpenGL ES functions");
                return false;
            }

            // Test that functions work by checking for errors
            uint error = _glGetError();
            if (error != GL_NO_ERROR)
            {
                Console.WriteLine($"[OpenGLESBackend] InitializeGLESLoader: OpenGL ES error detected during initialization: 0x{error:X}");
            }

            Console.WriteLine("[OpenGLESBackend] InitializeGLESLoader: Successfully loaded OpenGL ES functions");
            return true;
        }

        /// <summary>
        /// Loads a function pointer from OpenGL ES using eglGetProcAddress.
        /// </summary>
        private T LoadFunction<T>(string functionName) where T : class
        {
            IntPtr procAddress = eglGetProcAddress(functionName);
            if (procAddress == IntPtr.Zero || procAddress == new IntPtr(-1))
            {
                // Try loading from static library as fallback (for core functions on some platforms)
                procAddress = GetProcAddress(_glesLibraryHandle, functionName);
                if (procAddress == IntPtr.Zero)
                {
                    Console.WriteLine($"[OpenGLESBackend] LoadFunction: Failed to load {functionName}");
                    return null;
                }
            }

            try
            {
                return Marshal.GetDelegateForFunctionPointer<T>(procAddress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpenGLESBackend] LoadFunction: Exception loading {functionName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads the platform-specific OpenGL ES library.
        /// </summary>
        private bool LoadGLESLibrary()
        {
            // Platform-specific library names
            string[] libraryNames;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                libraryNames = new string[] { "libGLESv2.dll", "GLESv2.dll" };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                libraryNames = new string[] { "libGLESv2.so.2", "libGLESv2.so" };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                libraryNames = new string[] { "libGLESv2.dylib" };
            }
            else
            {
                Console.WriteLine("[OpenGLESBackend] LoadGLESLibrary: Unsupported platform");
                return false;
            }

            // Try to load library
            foreach (string libName in libraryNames)
            {
                _glesLibraryHandle = LoadLibrary(libName);
                if (_glesLibraryHandle != IntPtr.Zero)
                {
                    Console.WriteLine($"[OpenGLESBackend] LoadGLESLibrary: Loaded {libName}");
                    return true;
                }
            }

            // Library loading is optional - functions can be loaded via eglGetProcAddress alone
            // Some platforms (like Android) don't require explicit library loading
            Console.WriteLine("[OpenGLESBackend] LoadGLESLibrary: Could not load OpenGL ES library, will use eglGetProcAddress only");
            return true; // Continue anyway - eglGetProcAddress may work without explicit library load
        }

        /// <summary>
        /// Queries OpenGL ES version.
        /// </summary>
        private void QueryGLESVersion()
        {
            if (_glGetIntegerv == null || _glGetString == null)
            {
                Console.WriteLine("[OpenGLESBackend] QueryGLESVersion: Functions not loaded, using defaults");
                _majorVersion = 3;
                _minorVersion = 2;
                _glslVersion = "320 es";
                return;
            }

            // Query major version (OpenGL ES 3.0+)
            const uint GL_MAJOR_VERSION = 0x821B;
            _majorVersion = 0;
            _glGetIntegerv(GL_MAJOR_VERSION, ref _majorVersion);

            // Query minor version (OpenGL ES 3.0+)
            const uint GL_MINOR_VERSION = 0x821C;
            _minorVersion = 0;
            _glGetIntegerv(GL_MINOR_VERSION, ref _minorVersion);

            // Query GLSL ES version string
            const uint GL_SHADING_LANGUAGE_VERSION = 0x8B8C;
            IntPtr glslVersionPtr = _glGetString(GL_SHADING_LANGUAGE_VERSION);
            if (glslVersionPtr != IntPtr.Zero)
            {
                _glslVersion = Marshal.PtrToStringAnsi(glslVersionPtr);
            }
            else
            {
                // Fallback: construct version string from major/minor
                _glslVersion = _majorVersion + "" + _minorVersion + "0 es";
            }

            // Validate version
            if (_majorVersion == 0)
            {
                Console.WriteLine("[OpenGLESBackend] QueryGLESVersion: Failed to query version, using defaults");
                _majorVersion = 3;
                _minorVersion = 2;
                _glslVersion = "320 es";
            }
        }

        /// <summary>
        /// Checks for required OpenGL ES extensions.
        /// </summary>
        private bool CheckRequiredExtensions()
        {
            if (_glGetString == null || _glGetIntegerv == null)
            {
                Console.WriteLine("[OpenGLESBackend] CheckRequiredExtensions: Functions not loaded");
                return false;
            }

            // OpenGL ES 3.2 core features should be available
            // Check that we have at least ES 3.0 (required for our features)
            if (_majorVersion < 3)
            {
                Console.WriteLine($"[OpenGLESBackend] CheckRequiredExtensions: OpenGL ES version {_majorVersion}.{_minorVersion} is too old, requires ES 3.0+");
                return false;
            }

            // Check for optional extensions that we might use
            // For ES 3.0+, we can use glGetStringi to query extensions
            if (_glGetStringi != null && _majorVersion >= 3)
            {
                const uint GL_NUM_EXTENSIONS = 0x821D;
                int numExtensions = 0;
                _glGetIntegerv(GL_NUM_EXTENSIONS, ref numExtensions);

                // Check for specific extensions we might need
                const uint GL_EXTENSIONS = 0x1F03;
                bool hasCompressedTextures = false;
                bool hasTextureRG = false;

                for (uint i = 0; i < numExtensions; i++)
                {
                    IntPtr extPtr = _glGetStringi(GL_EXTENSIONS, i);
                    if (extPtr != IntPtr.Zero)
                    {
                        string ext = Marshal.PtrToStringAnsi(extPtr);
                        if (ext != null)
                        {
                            if (ext.Contains("GL_EXT_texture_compression_s3tc") || ext.Contains("GL_OES_texture_compression_S3TC"))
                            {
                                hasCompressedTextures = true;
                            }
                            if (ext.Contains("GL_EXT_texture_rg") || _majorVersion >= 3)
                            {
                                hasTextureRG = true; // Core in ES 3.0+
                            }
                        }
                    }
                }

                // Log extension availability
                Console.WriteLine($"[OpenGLESBackend] CheckRequiredExtensions: Found {numExtensions} extensions");
                Console.WriteLine($"[OpenGLESBackend] CheckRequiredExtensions: Compressed textures: {hasCompressedTextures}, Texture RG: {hasTextureRG}");
            }
            else
            {
                // Fallback for ES 2.0: use glGetString(GL_EXTENSIONS)
                const uint GL_EXTENSIONS = 0x1F03;
                IntPtr extensionsPtr = _glGetString(GL_EXTENSIONS);
                if (extensionsPtr != IntPtr.Zero)
                {
                    string extensions = Marshal.PtrToStringAnsi(extensionsPtr);
                    Console.WriteLine($"[OpenGLESBackend] CheckRequiredExtensions: Extensions: {extensions}");
                }
            }

            return true;
        }

        /// <summary>
        /// Queries OpenGL ES capabilities.
        /// </summary>
        private void QueryCapabilities()
        {
            if (_glGetIntegerv == null || _glGetString == null)
            {
                Console.WriteLine("[OpenGLESBackend] QueryCapabilities: Functions not loaded, using defaults");
                bool defaultSupportsCompute = _majorVersion > 3 || (_majorVersion == 3 && _minorVersion >= 1);
                _capabilities = new GraphicsCapabilities
                {
                    MaxTextureSize = 4096,
                    MaxRenderTargets = 4,
                    MaxAnisotropy = 16,
                    SupportsComputeShaders = defaultSupportsCompute,
                    SupportsGeometryShaders = false,
                    SupportsTessellation = false,
                    SupportsRaytracing = false,
                    SupportsMeshShaders = false,
                    SupportsVariableRateShading = false,
                    DedicatedVideoMemory = 0,
                    SharedSystemMemory = 0,
                    VendorName = "Unknown",
                    DeviceName = "OpenGL ES Renderer",
                    DriverVersion = GLVersion,
                    ActiveBackend = GraphicsBackend.OpenGLES,
                    ShaderModelVersion = 3.2f,
                    RemixAvailable = false,
                    DlssAvailable = false,
                    FsrAvailable = defaultSupportsCompute
                };
                return;
            }

            // Query maximum texture size
            const uint GL_MAX_TEXTURE_SIZE = 0x0D33;
            int maxTextureSize = 4096; // Default
            _glGetIntegerv(GL_MAX_TEXTURE_SIZE, ref maxTextureSize);

            // Query maximum color attachments (ES 3.0+)
            const uint GL_MAX_COLOR_ATTACHMENTS = 0x8CDF;
            int maxColorAttachments = 4; // Default for ES 3.0+
            if (_majorVersion >= 3)
            {
                _glGetIntegerv(GL_MAX_COLOR_ATTACHMENTS, ref maxColorAttachments);
            }

            // Query renderer string
            const uint GL_RENDERER = 0x1F01;
            IntPtr rendererPtr = _glGetString(GL_RENDERER);
            string deviceName = "OpenGL ES Renderer";
            if (rendererPtr != IntPtr.Zero)
            {
                deviceName = Marshal.PtrToStringAnsi(rendererPtr);
                if (string.IsNullOrEmpty(deviceName))
                {
                    deviceName = "OpenGL ES Renderer";
                }
            }

            // Query vendor string
            const uint GL_VENDOR = 0x1F00;
            IntPtr vendorPtr = _glGetString(GL_VENDOR);
            string vendorName = "Unknown";
            if (vendorPtr != IntPtr.Zero)
            {
                vendorName = Marshal.PtrToStringAnsi(vendorPtr);
                if (string.IsNullOrEmpty(vendorName))
                {
                    vendorName = "Unknown";
                }
            }

            // Determine feature support based on version
            bool supportsCompute = _majorVersion > 3 || (_majorVersion == 3 && _minorVersion >= 1);

            _capabilities = new GraphicsCapabilities
            {
                MaxTextureSize = maxTextureSize,
                MaxRenderTargets = maxColorAttachments,
                MaxAnisotropy = 16, // Typical ES limit, could query GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT if extension available
                SupportsComputeShaders = supportsCompute, // ES 3.1+
                SupportsGeometryShaders = false, // ES does not support geometry shaders
                SupportsTessellation = false, // ES does not support tessellation
                SupportsRaytracing = false,
                SupportsMeshShaders = false,
                SupportsVariableRateShading = false,
                DedicatedVideoMemory = 0, // ES doesn't expose memory info
                SharedSystemMemory = 0,
                VendorName = vendorName,
                DeviceName = deviceName,
                DriverVersion = GLVersion,
                ActiveBackend = GraphicsBackend.OpenGLES,
                ShaderModelVersion = _majorVersion + _minorVersion * 0.1f, // e.g., 3.2
                RemixAvailable = false,
                DlssAvailable = false,
                FsrAvailable = supportsCompute // FSR can work via compute shaders in ES 3.1+
            };
        }

        /// <summary>
        /// Creates default framebuffer.
        /// </summary>
        private void CreateDefaultFramebuffer()
        {
            // For window rendering in OpenGL ES, default framebuffer is 0
            // For off-screen rendering: create FBO
            _defaultFramebuffer = 0;
        }

        /// <summary>
        /// Sets default OpenGL ES state.
        /// </summary>
        private void SetDefaultState()
        {
            if (_glEnable == null || _glDepthFunc == null || _glCullFace == null ||
                _glFrontFace == null || _glBlendFunc == null || _glClearColor == null)
            {
                Console.WriteLine("[OpenGLESBackend] SetDefaultState: Functions not loaded, skipping state setup");
                return;
            }

            // Enable depth testing
            const uint GL_DEPTH_TEST = 0x0B71;
            _glEnable(GL_DEPTH_TEST);
            const uint GL_LESS = 0x0201;
            _glDepthFunc(GL_LESS);

            // Enable face culling
            const uint GL_CULL_FACE = 0x0B44;
            _glEnable(GL_CULL_FACE);
            const uint GL_BACK = 0x0405;
            _glCullFace(GL_BACK);
            const uint GL_CCW = 0x0901;
            _glFrontFace(GL_CCW);

            // Enable blending
            const uint GL_BLEND = 0x0BE2;
            _glEnable(GL_BLEND);
            const uint GL_SRC_ALPHA = 0x0302;
            const uint GL_ONE_MINUS_SRC_ALPHA = 0x0303;
            _glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

            // Set clear color to black
            _glClearColor(0.0f, 0.0f, 0.0f, 1.0f);

            // Check for errors
            if (_glGetError != null)
            {
                uint error = _glGetError();
                if (error != GL_NO_ERROR)
                {
                    Console.WriteLine($"[OpenGLESBackend] SetDefaultState: OpenGL ES error {GetGLESErrorName(error)} (0x{error:X})");
                }
            }
        }

        #endregion

        #region Texture Format Conversion (OpenGL ES specific)

        /// <summary>
        /// Converts TextureFormat to OpenGL ES format constant.
        /// OpenGL ES has more limited format support than desktop OpenGL.
        /// </summary>
        private uint ConvertTextureFormatToGLES(TextureFormat format)
        {
            switch (format)
            {
                // Uncompressed formats (ES supports basic formats)
                case TextureFormat.R8_UNorm:
                    return GL_RED_EXT; // ES 2.0 requires GL_EXT_texture_rg extension, ES 3.0+ has GL_RED
                case TextureFormat.R8G8_UNorm:
                    return GL_RG_EXT; // ES 2.0 requires extension, ES 3.0+ has GL_RG
                case TextureFormat.R8G8B8A8_UNorm:
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                    return GL_RGBA;
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                    // BGRA not directly supported in ES - must convert
                    return GL_RGBA;
                case TextureFormat.R16_Float:
                case TextureFormat.R16G16_Float:
                case TextureFormat.R16G16B16A16_Float:
                    return GL_RGBA; // Half float formats
                case TextureFormat.R32_Float:
                case TextureFormat.R32G32_Float:
                case TextureFormat.R32G32B32_Float:
                case TextureFormat.R32G32B32A32_Float:
                    return GL_RGBA; // Float formats
                // Compressed formats
                case TextureFormat.BC1_UNorm:
                case TextureFormat.BC1_UNorm_SRGB:
                case TextureFormat.BC2_UNorm:
                case TextureFormat.BC2_UNorm_SRGB:
                case TextureFormat.BC3_UNorm:
                case TextureFormat.BC3_UNorm_SRGB:
                    return GL_RGBA; // Compressed formats use special internal formats
                // Depth formats
                case TextureFormat.D16_UNorm:
                case TextureFormat.D24_UNorm_S8_UInt:
                case TextureFormat.D32_Float:
                case TextureFormat.D32_Float_S8_UInt:
                    return GL_DEPTH_COMPONENT; // Depth formats
                default:
                    Console.WriteLine($"[OpenGLESBackend] ConvertTextureFormatToGLES: Unknown format {format}, defaulting to GL_RGBA");
                    return GL_RGBA;
            }
        }

        /// <summary>
        /// Gets OpenGL ES internal format constant for a texture format.
        /// </summary>
        private uint GetGLESInternalFormat(TextureFormat format)
        {
            switch (format)
            {
                // Uncompressed 8-bit formats
                case TextureFormat.R8_UNorm:
                    return GL_R8; // ES 3.0+
                case TextureFormat.R8G8_UNorm:
                    return GL_RG8; // ES 3.0+
                case TextureFormat.R8G8B8A8_UNorm:
                    return GL_RGBA8;
                case TextureFormat.R8G8B8A8_UNorm_SRGB:
                    return GL_SRGB8_ALPHA8;
                case TextureFormat.B8G8R8A8_UNorm:
                case TextureFormat.B8G8R8A8_UNorm_SRGB:
                    // BGRA converted to RGBA8 internally
                    return GL_RGBA8;
                // 16-bit float formats
                case TextureFormat.R16_Float:
                    return GL_R16F; // ES 3.0+
                case TextureFormat.R16G16_Float:
                    return GL_RG16F; // ES 3.0+
                case TextureFormat.R16G16B16A16_Float:
                    return GL_RGBA16F;
                // 32-bit float formats
                case TextureFormat.R32_Float:
                    return GL_R32F; // ES 3.0+
                case TextureFormat.R32G32_Float:
                    return GL_RG32F; // ES 3.0+
                case TextureFormat.R32G32B32_Float:
                    return GL_RGB32F;
                case TextureFormat.R32G32B32A32_Float:
                    return GL_RGBA32F;
                // Compressed formats (S3TC/DXT) - ES supports via extension
                case TextureFormat.BC1_UNorm:
                case TextureFormat.BC1_UNorm_SRGB:
                    return GL_COMPRESSED_RGB_S3TC_DXT1_EXT;
                case TextureFormat.BC2_UNorm:
                case TextureFormat.BC2_UNorm_SRGB:
                    return GL_COMPRESSED_RGBA_S3TC_DXT3_EXT;
                case TextureFormat.BC3_UNorm:
                case TextureFormat.BC3_UNorm_SRGB:
                    return GL_COMPRESSED_RGBA_S3TC_DXT5_EXT;
                // Depth formats
                case TextureFormat.D16_UNorm:
                    return GL_DEPTH_COMPONENT16;
                case TextureFormat.D24_UNorm_S8_UInt:
                    return GL_DEPTH24_STENCIL8; // ES 3.0+
                case TextureFormat.D32_Float:
                    return GL_DEPTH_COMPONENT32F; // ES 3.0+
                case TextureFormat.D32_Float_S8_UInt:
                    return GL_DEPTH32F_STENCIL8; // ES 3.0+
                default:
                    Console.WriteLine($"[OpenGLESBackend] GetGLESInternalFormat: Unknown format {format}, defaulting to GL_RGBA8");
                    return GL_RGBA8;
            }
        }

        /// <summary>
        /// Gets OpenGL ES data type for a texture format.
        /// </summary>
        private uint GetGLESDataType(TextureFormat format)
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
                    return width * height * 4; // Default to RGBA
            }

            return blockWidth * blockHeight * blockSize;
        }

        /// <summary>
        /// Converts BGRA pixel data to RGBA (required for OpenGL ES).
        /// </summary>
        private byte[] ConvertBGRAToRGBA(byte[] bgraData)
        {
            if (bgraData == null || bgraData.Length == 0)
            {
                return bgraData;
            }

            byte[] rgbaData = new byte[bgraData.Length];
            for (int i = 0; i < bgraData.Length; i += 4)
            {
                if (i + 3 < bgraData.Length)
                {
                    // BGRA -> RGBA: swap R and B channels
                    rgbaData[i] = bgraData[i + 2];     // R
                    rgbaData[i + 1] = bgraData[i + 1]; // G
                    rgbaData[i + 2] = bgraData[i];     // B
                    rgbaData[i + 3] = bgraData[i + 3]; // A
                }
            }
            return rgbaData;
        }

        /// <summary>
        /// Gets the name of an OpenGL ES error code for debugging.
        /// </summary>
        private string GetGLESErrorName(uint error)
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

        #region Resource Management

        private void DestroyResourceInternal(ResourceInfo info)
        {
            switch (info.Type)
            {
                case ResourceType.Texture:
                    if (info.GLHandle != 0 && _glDeleteTextures != null)
                    {
                        uint textureId = info.GLHandle;
                        _glDeleteTextures(1, ref textureId);
                        uint error = (_glGetError != null) ? _glGetError() : GL_NO_ERROR;
                        if (error != GL_NO_ERROR)
                        {
                            Console.WriteLine($"[OpenGLESBackend] DestroyResourceInternal: OpenGL ES error {GetGLESErrorName(error)} deleting texture {info.DebugName}");
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

        #endregion

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
            public TextureUploadData UploadData;
        }

        private enum ResourceType
        {
            Texture,
            Buffer,
            Pipeline
        }

        #region EGL P/Invoke Declarations

        // EGL constants
        private const int EGL_DEFAULT_DISPLAY = 0;
        private const int EGL_NONE = 0x3038;
        private const int EGL_FALSE = 0;
        private const int EGL_TRUE = 1;

        // EGL configuration attributes
        private const int EGL_RED_SIZE = 0x3024;
        private const int EGL_GREEN_SIZE = 0x3023;
        private const int EGL_BLUE_SIZE = 0x3022;
        private const int EGL_ALPHA_SIZE = 0x3021;
        private const int EGL_DEPTH_SIZE = 0x3025;
        private const int EGL_STENCIL_SIZE = 0x3026;
        private const int EGL_SURFACE_TYPE = 0x3033;
        private const int EGL_RENDERABLE_TYPE = 0x3040;
        private const int EGL_WINDOW_BIT = 0x0004;
        private const int EGL_PBUFFER_BIT = 0x0001;
        private const int EGL_OPENGL_ES_BIT = 0x0001;
        private const int EGL_OPENGL_ES2_BIT = 0x0004;
        private const int EGL_OPENGL_ES3_BIT = 0x0040;
        private const int EGL_SAMPLE_BUFFERS = 0x3032;
        private const int EGL_SAMPLES = 0x3031;
        private const int EGL_WIDTH = 0x3057;
        private const int EGL_HEIGHT = 0x3056;

        // EGL context attributes
        private const int EGL_CONTEXT_MAJOR_VERSION = 0x3098;
        private const int EGL_CONTEXT_MINOR_VERSION = 0x30FB;

        // EGL functions
        [DllImport("libEGL.dll", EntryPoint = "eglGetDisplay")]
        private static extern IntPtr eglGetDisplay(int displayId);

        [DllImport("libEGL.dll", EntryPoint = "eglInitialize")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool eglInitialize(IntPtr display, ref int major, ref int minor);

        [DllImport("libEGL.dll", EntryPoint = "eglChooseConfig")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool eglChooseConfig(IntPtr display, int[] attribList, ref IntPtr configs, int configSize, ref int numConfigs);

        [DllImport("libEGL.dll", EntryPoint = "eglCreateContext")]
        private static extern IntPtr eglCreateContext(IntPtr display, IntPtr config, IntPtr shareContext, int[] attribList);

        [DllImport("libEGL.dll", EntryPoint = "eglCreateWindowSurface")]
        private static extern IntPtr eglCreateWindowSurface(IntPtr display, IntPtr config, IntPtr win, int[] attribList);

        [DllImport("libEGL.dll", EntryPoint = "eglCreatePbufferSurface")]
        private static extern IntPtr eglCreatePbufferSurface(IntPtr display, IntPtr config, int[] attribList);

        [DllImport("libEGL.dll", EntryPoint = "eglMakeCurrent")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool eglMakeCurrent(IntPtr display, IntPtr draw, IntPtr read, IntPtr context);

        [DllImport("libEGL.dll", EntryPoint = "eglSwapBuffers")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool eglSwapBuffers(IntPtr display, IntPtr surface);

        [DllImport("libEGL.dll", EntryPoint = "eglDestroySurface")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool eglDestroySurface(IntPtr display, IntPtr surface);

        [DllImport("libEGL.dll", EntryPoint = "eglDestroyContext")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool eglDestroyContext(IntPtr display, IntPtr context);

        [DllImport("libEGL.dll", EntryPoint = "eglTerminate")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool eglTerminate(IntPtr display);

        [DllImport("libEGL.dll", EntryPoint = "eglGetError")]
        private static extern uint eglGetError();

        [DllImport("libEGL.dll", EntryPoint = "eglQuerySurface")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool eglQuerySurface(IntPtr display, IntPtr surface, int attribute, ref int value);

        [DllImport("libEGL.dll", EntryPoint = "eglGetProcAddress")]
        private static extern IntPtr eglGetProcAddress(string procname);

        #endregion

        #region Platform-Specific Library Loading

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibraryWindows(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddressWindows(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.so.2", EntryPoint = "dlsym")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        // Cross-platform LoadLibrary wrapper
        private static IntPtr LoadLibrary(string libraryName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return LoadLibraryWindows(libraryName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // RTLD_LAZY = 1, RTLD_NOW = 2
                return dlopen(libraryName, 1); // RTLD_LAZY
            }
            return IntPtr.Zero;
        }

        // Cross-platform GetProcAddress wrapper
        private static IntPtr GetProcAddress(IntPtr libraryHandle, string functionName)
        {
            if (libraryHandle == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetProcAddressWindows(libraryHandle, functionName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return dlsym(libraryHandle, functionName);
            }
            return IntPtr.Zero;
        }

        #endregion

        #region OpenGL ES P/Invoke Declarations (Legacy - kept for fallback)

        // OpenGL ES texture functions
        [DllImport("libGLESv2.dll", EntryPoint = "glGenTextures")]
        private static extern void glGenTextures(int n, ref uint textures);

        [DllImport("libGLESv2.dll", EntryPoint = "glBindTexture")]
        private static extern void glBindTexture(uint target, uint texture);

        [DllImport("libGLESv2.dll", EntryPoint = "glTexImage2D")]
        private static extern void glTexImage2D(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, IntPtr pixels);

        [DllImport("libGLESv2.dll", EntryPoint = "glCompressedTexImage2D")]
        private static extern void glCompressedTexImage2D(uint target, int level, uint internalformat, int width, int height, int border, int imageSize, IntPtr data);

        [DllImport("libGLESv2.dll", EntryPoint = "glTexParameteri")]
        private static extern void glTexParameteri(uint target, uint pname, int param);

        [DllImport("libGLESv2.dll", EntryPoint = "glGetError")]
        private static extern uint glGetError();

        [DllImport("libGLESv2.dll", EntryPoint = "glDeleteTextures")]
        private static extern void glDeleteTextures(int n, ref uint textures);

        [DllImport("libGLESv2.dll", EntryPoint = "glViewport")]
        private static extern void glViewport(int x, int y, int width, int height);

        // OpenGL ES constants
        private const uint GL_TEXTURE_2D = 0x0DE1;
        private const uint GL_TEXTURE_MIN_FILTER = 0x2801;
        private const uint GL_TEXTURE_MAG_FILTER = 0x2800;
        private const uint GL_TEXTURE_WRAP_S = 0x2802;
        private const uint GL_TEXTURE_WRAP_T = 0x2803;
        private const uint GL_LINEAR = 0x2601;
        private const uint GL_LINEAR_MIPMAP_LINEAR = 0x2703;
        private const uint GL_CLAMP_TO_EDGE = 0x812F;
        private const uint GL_RGBA = 0x1908;
        private const uint GL_RGB = 0x1907;
        private const uint GL_RED_EXT = 0x1903; // ES 2.0 extension, ES 3.0+ has GL_RED
        private const uint GL_RG_EXT = 0x8227; // ES 2.0 extension, ES 3.0+ has GL_RG
        private const uint GL_BGRA = 0x80E1; // Not in ES core, but may be available via extension
        private const uint GL_BGRA_EXT = 0x80E1;
        private const uint GL_DEPTH_COMPONENT = 0x1902;
        private const uint GL_UNSIGNED_BYTE = 0x1401;
        private const uint GL_FLOAT = 0x1406;
        private const uint GL_HALF_FLOAT = 0x140B;

        // Internal format constants
        private const uint GL_R8 = 0x8229; // ES 3.0+
        private const uint GL_RG8 = 0x822B; // ES 3.0+
        private const uint GL_RGBA8 = 0x8058;
        private const uint GL_SRGB8_ALPHA8 = 0x8C43;
        private const uint GL_R16F = 0x822D; // ES 3.0+
        private const uint GL_RG16F = 0x822F; // ES 3.0+
        private const uint GL_RGBA16F = 0x881A;
        private const uint GL_R32F = 0x822E; // ES 3.0+
        private const uint GL_RG32F = 0x8230; // ES 3.0+
        private const uint GL_RGB32F = 0x8815;
        private const uint GL_RGBA32F = 0x8814;

        // Compressed texture formats
        private const uint GL_COMPRESSED_RGB_S3TC_DXT1_EXT = 0x83F0;
        private const uint GL_COMPRESSED_RGBA_S3TC_DXT1_EXT = 0x83F1;
        private const uint GL_COMPRESSED_RGBA_S3TC_DXT3_EXT = 0x83F2;
        private const uint GL_COMPRESSED_RGBA_S3TC_DXT5_EXT = 0x83F3;

        // Depth formats
        private const uint GL_DEPTH_COMPONENT16 = 0x81A5;
        private const uint GL_DEPTH_COMPONENT32F = 0x8CAC; // ES 3.0+
        private const uint GL_DEPTH24_STENCIL8 = 0x88F0; // ES 3.0+
        private const uint GL_DEPTH32F_STENCIL8 = 0x8CAD; // ES 3.0+

        // Error codes
        private const uint GL_NO_ERROR = 0x0000;
        private const uint GL_INVALID_ENUM = 0x0500;
        private const uint GL_INVALID_VALUE = 0x0501;
        private const uint GL_INVALID_OPERATION = 0x0502;
        private const uint GL_OUT_OF_MEMORY = 0x0505;

        #endregion
    }
}

