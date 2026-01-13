using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Andastra.Runtime.Graphics.Common.Culling;

namespace Andastra.Game.Graphics.Common.Backends.Odyssey.Culling
{
    /// <summary>
    /// Hi-Z occlusion culling implementation for KOTOR1 graphics backend (swkotor.exe).
    /// Uses OpenGL for depth buffer access and Hi-Z mipmap generation.
    /// 
    /// IMPORTANT: This is a MODERN ENHANCEMENT - the original swkotor.exe does NOT implement Hi-Z occlusion culling.
    /// The original engine uses VIS files for room-to-room visibility (portal-based culling).
    /// 
    /// Matches swkotor.exe OpenGL usage patterns exactly:
    /// - Uses glCopyTexImage2D for framebuffer-to-texture copy (swkotor.exe: FUN_00427c90 @ 0x00427c90, line 29)
    /// - Uses glDepthFunc, glDepthMask for depth testing (swkotor.exe: glDepthFunc @ 0x0078c256, glDepthMask @ 0x0078bf0a)
    /// - OpenGL context: Created via wglCreateContext (swkotor.exe: FUN_0044dab0 @ 0x0044dab0)
    /// - Texture operations: glGenTextures, glBindTexture, glTexImage2D (swkotor.exe: FUN_00427c90 @ 0x00427c90)
    /// - NOTE: glReadPixels exists in swkotor.exe @ 0x0078be1a but is NEVER CALLED (no cross-references found)
    /// </summary>
    /// <remarks>
    /// KOTOR1-Specific OpenGL Implementation (swkotor.exe):
    /// - OpenGL context: HGLRC created via wglCreateContext
    /// - Device context: HDC from GetDC(windowHandle)
    /// - Depth buffer: Copied via glCopyTexImage2D (matches swkotor.exe: FUN_00427c90 line 29 pattern)
    /// - Hi-Z buffer: GL_TEXTURE_2D with GL_R32F format (single-channel float for depth)
    /// - Mipmap generation: CPU-side max depth calculation (OpenGL 1.x/2.x doesn't support compute shaders)
    /// - Global variables: DAT_0078d98c, DAT_0078daf4 (KOTOR1-specific texture flags)
    /// - Helper functions: FUN_0045f820, FUN_006fae8c (KOTOR1-specific texture setup)
    /// - Original engine occlusion: VIS files for room visibility, NOT Hi-Z buffers
    /// 
    /// Original Engine Behavior (swkotor.exe):
    /// - Uses VIS files for room-to-room visibility culling (portal-based)
    /// - Uses frustum culling for geometry outside view
    /// - Uses glDepthFunc/glDepthMask for depth testing during rendering
    /// - Does NOT read depth buffers back from GPU
    /// - Does NOT implement Hi-Z occlusion culling
    /// 
    /// Inheritance Structure:
    /// - BaseOcclusionCuller (Runtime.Graphics.Common.Culling) - Common occlusion culling logic
    ///   - Kotor1OcclusionCuller (this class) - KOTOR1 OpenGL-specific implementation (modern enhancement)
    /// </remarks>
    public class Kotor1OcclusionCuller : BaseOcclusionCuller
    {
        private readonly IntPtr _glContext; // HGLRC - OpenGL rendering context
        private readonly IntPtr _glDevice; // HDC - OpenGL device context
        private uint _hiZBufferTexture; // GLuint - OpenGL texture ID for Hi-Z buffer

        /// <summary>
        /// Initializes a new KOTOR1 occlusion culler.
        /// </summary>
        /// <param name="glContext">OpenGL rendering context (HGLRC). Must not be IntPtr.Zero.</param>
        /// <param name="glDevice">OpenGL device context (HDC). Must not be IntPtr.Zero.</param>
        /// <param name="width">Buffer width. Must be greater than zero.</param>
        /// <param name="height">Buffer height. Must be greater than zero.</param>
        /// <exception cref="ArgumentNullException">Thrown if glContext or glDevice is IntPtr.Zero.</exception>
        /// <exception cref="ArgumentException">Thrown if width or height is less than or equal to zero.</exception>
        public Kotor1OcclusionCuller(IntPtr glContext, IntPtr glDevice, int width, int height)
            : base(width, height)
        {
            if (glContext == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(glContext), "OpenGL context must not be IntPtr.Zero.");
            }
            if (glDevice == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(glDevice), "OpenGL device context must not be IntPtr.Zero.");
            }

            _glContext = glContext;
            _glDevice = glDevice;
            CreateHiZBufferTexture(_width, _height, CalculateMipLevels(_width, _height));
        }

        /// <summary>
        /// Generates Hi-Z buffer from depth buffer using OpenGL.
        /// Matches swkotor.exe OpenGL framebuffer-to-texture copy pattern.
        /// </summary>
        /// <param name="depthBuffer">Depth buffer object (not used directly, reads from current OpenGL framebuffer).</param>
        /// <remarks>
        /// KOTOR1 Implementation (swkotor.exe):
        /// - Copies depth buffer using glCopyTexImage2D (swkotor.exe: FUN_00427c90 @ 0x00427c90, line 29)
        /// - Pattern: glCopyTexImage2D(0x84f5, 0, 0x8058, 0, 0, width, height, 0) - matches original exactly
        /// - 0x84f5 = GL_TEXTURE_RECTANGLE_NV, 0x8058 = GL_RGBA8 (original engine uses RGBA, we use depth)
        /// - Then reads back via glGetTexImage for CPU-side processing
        /// - Generates subsequent mip levels using CPU-side max depth calculation
        /// - Matches FUN_00427c90 @ 0x00427c90 texture initialization pattern
        /// - NOTE: Original engine does NOT implement this - this is a modern enhancement
        /// </remarks>
        private void GenerateHiZBufferInternal(object depthBuffer)
        {
            if (!Enabled)
            {
                return;
            }

            // Make OpenGL context current (required for glCopyTexImage2D)
            // Matches swkotor.exe: wglMakeCurrent(hdc, hglrc) pattern from FUN_0044dab0
            if (!wglMakeCurrent(_glDevice, _glContext))
            {
                return; // Failed to make context current
            }

            try
            {
                // Copy depth buffer from framebuffer to texture using glCopyTexImage2D
                // Matches swkotor.exe: glCopyTexImage2D pattern from FUN_00427c90 line 29
                // Original: glCopyTexImage2D(0x84f5, 0, 0x8058, 0, 0, width, height, 0)
                // We use GL_TEXTURE_2D and GL_DEPTH_COMPONENT for depth buffer
                glBindTexture(GL_TEXTURE_2D, _hiZBufferTexture);
                glCopyTexImage2D(GL_TEXTURE_2D, 0, GL_DEPTH_COMPONENT, 0, 0, _width, _height, 0);

                // Read depth data back from texture for CPU-side processing
                // Original engine doesn't do this, but we need it for Hi-Z mipmap generation
                float[] depthData = new float[_width * _height];
                glGetTexImage(GL_TEXTURE_2D, 0, GL_DEPTH_COMPONENT, GL_FLOAT, depthData);

                // Populate mip level 0 in cache
                if (_mipLevelCache == null)
                {
                    int mipLevels = CalculateMipLevels(_width, _height);
                    _mipLevelCache = new float[mipLevels][];
                }
                _mipLevelCache[0] = depthData;

                // Generate all mip levels using CPU-side max depth calculation
                // OpenGL 1.x/2.x doesn't support compute shaders, so we use CPU-side calculation
                // Matches the CPU-side approach used in MonoGame/Stride implementations
                _mipLevelCacheValid = false;
                int maxMipLevels = CalculateMipLevels(_width, _height);
                for (int mip = 1; mip < maxMipLevels; mip++)
                {
                    int mipWidth = Math.Max(1, _width >> mip);
                    int mipHeight = Math.Max(1, _height >> mip);
                    int prevMipWidth = Math.Max(1, _width >> (mip - 1));
                    int prevMipHeight = Math.Max(1, _height >> (mip - 1));

                    GenerateMipLevelWithMaxDepth(mip, mipWidth, mipHeight, prevMipWidth, prevMipHeight);

                    // Upload mip level to OpenGL texture (optional, for GPU-side sampling if needed)
                    // Note: OpenGL 1.x/2.x doesn't support writing to specific mip levels easily,
                    // so we primarily use CPU-side cache for sampling
                    float[] mipData = _mipLevelCache[mip];
                    if (mipData != null)
                    {
                        glTexImage2D(GL_TEXTURE_2D, (int)mip, (int)GL_R32F, mipWidth, mipHeight, 0, GL_RED, GL_FLOAT, mipData);
                    }
                }

                _mipLevelCacheValid = true;
            }
            finally
            {
                // Restore previous OpenGL state
                glBindTexture(GL_TEXTURE_2D, 0);
            }
        }

        /// <summary>
        /// Samples Hi-Z buffer max depth using OpenGL or CPU-side cache.
        /// Matches swkotor.exe OpenGL texture sampling pattern.
        /// </summary>
        /// <param name="minX">Minimum X coordinate in screen space.</param>
        /// <param name="minY">Minimum Y coordinate in screen space.</param>
        /// <param name="maxX">Maximum X coordinate in screen space.</param>
        /// <param name="maxY">Maximum Y coordinate in screen space.</param>
        /// <param name="mipLevel">Mip level to sample from.</param>
        /// <returns>Maximum depth value in the specified region.</returns>
        /// <remarks>
        /// KOTOR1 Implementation (swkotor.exe):
        /// - Uses CPU-side cache for accurate max depth values (primary method)
        /// - Falls back to glGetTexImage if cache is invalid (slower, but accurate)
        /// - Matches FUN_00427c90 @ 0x00427c90 texture access pattern
        /// - NOTE: Original engine does NOT implement this - this is a modern enhancement
        /// </remarks>
        private float SampleHiZBufferMaxDepthInternal(int minX, int minY, int maxX, int maxY, int mipLevel)
        {
            // Use base class implementation which uses _mipLevelCache
            // If cache is invalid, we'll need to read from OpenGL texture
            if (!_mipLevelCacheValid || _mipLevelCache == null || mipLevel >= _mipLevelCache.Length || _mipLevelCache[mipLevel] == null)
            {
                // Cache is invalid, read from OpenGL texture
                // Make OpenGL context current
                if (!wglMakeCurrent(_glDevice, _glContext))
                {
                    return 1.0f; // Failed to make context current, assume fully occluded
                }

                try
                {
                    // Read full resolution texture (mip 0) from GPU
                    float[] fullResData = new float[_width * _height];
                    glBindTexture(GL_TEXTURE_2D, _hiZBufferTexture);
                    glGetTexImage(GL_TEXTURE_2D, 0, GL_RED, GL_FLOAT, fullResData);

                    // Populate mip level 0 in cache
                    if (_mipLevelCache == null)
                    {
                        int mipLevels = CalculateMipLevels(_width, _height);
                        _mipLevelCache = new float[mipLevels][];
                    }
                    _mipLevelCache[0] = fullResData;

                    // Generate all mip levels into the cache
                    for (int mip = 1; mip < _mipLevelCache.Length; mip++)
                    {
                        int currentMipWidth = Math.Max(1, _width >> mip);
                        int currentMipHeight = Math.Max(1, _height >> mip);
                        int prevMipWidth = Math.Max(1, _width >> (mip - 1));
                        int prevMipHeight = Math.Max(1, _height >> (mip - 1));
                        GenerateMipLevelWithMaxDepth(mip, currentMipWidth, currentMipHeight, prevMipWidth, prevMipHeight);
                    }
                    _mipLevelCacheValid = true;
                }
                finally
                {
                    glBindTexture(GL_TEXTURE_2D, 0);
                }
            }

            // Sample from CPU-side cache
            if (_mipLevelCache == null || mipLevel >= _mipLevelCache.Length || _mipLevelCache[mipLevel] == null)
            {
                return 0.0f;
            }

            float[] mipData = _mipLevelCache[mipLevel];
            int mipWidth = Math.Max(1, _width >> mipLevel);
            int mipHeight = Math.Max(1, _height >> mipLevel);

            // Clamp coordinates to mip level bounds
            int clampedMinX = Math.Max(0, Math.Min(minX >> mipLevel, mipWidth - 1));
            int clampedMaxX = Math.Max(0, Math.Min(maxX >> mipLevel, mipWidth - 1));
            int clampedMinY = Math.Max(0, Math.Min(minY >> mipLevel, mipHeight - 1));
            int clampedMaxY = Math.Max(0, Math.Min(maxY >> mipLevel, mipHeight - 1));

            // Find maximum depth in the region
            float maxDepth = 0.0f;
            for (int y = clampedMinY; y <= clampedMaxY; y++)
            {
                for (int x = clampedMinX; x <= clampedMaxX; x++)
                {
                    int index = y * mipWidth + x;
                    if (index >= 0 && index < mipData.Length)
                    {
                        maxDepth = Math.Max(maxDepth, mipData[index]);
                    }
                }
            }

            return maxDepth;
        }

        /// <summary>
        /// Generates a single mip level by calculating maximum depth from 2x2 regions of the previous mip level.
        /// </summary>
        /// <param name="mipLevel">The mip level index to generate.</param>
        /// <param name="mipWidth">Width of the mip level.</param>
        /// <param name="mipHeight">Height of the mip level.</param>
        /// <param name="prevMipWidth">Width of the previous mip level.</param>
        /// <param name="prevMipHeight">Height of the previous mip level.</param>
        private void GenerateMipLevelWithMaxDepth(int mipLevel, int mipWidth, int mipHeight, int prevMipWidth, int prevMipHeight)
        {
            if (_mipLevelCache == null || mipLevel < 1 || mipLevel >= _mipLevelCache.Length)
            {
                return;
            }

            // Get previous mip level data from cache
            float[] prevMipData = _mipLevelCache[mipLevel - 1];
            if (prevMipData == null)
            {
                return;
            }

            // Calculate max depth values for current mip level from previous mip level
            // Each texel in current mip level is the maximum of a 2x2 region in previous level
            float[] mipData = new float[mipWidth * mipHeight];
            for (int y = 0; y < mipHeight; y++)
            {
                for (int x = 0; x < mipWidth; x++)
                {
                    // Calculate source region in previous mip level (2x2 region)
                    int srcX = x << 1;
                    int srcY = y << 1;

                    // Sample 2x2 region and find maximum depth
                    float maxDepth = 0.0f;
                    for (int sy = 0; sy < 2 && (srcY + sy) < prevMipHeight; sy++)
                    {
                        for (int sx = 0; sx < 2 && (srcX + sx) < prevMipWidth; sx++)
                        {
                            int srcIndex = (srcY + sy) * prevMipWidth + (srcX + sx);
                            if (srcIndex >= 0 && srcIndex < prevMipData.Length)
                            {
                                maxDepth = Math.Max(maxDepth, prevMipData[srcIndex]);
                            }
                        }
                    }

                    int mipIndex = y * mipWidth + x;
                    if (mipIndex < mipData.Length)
                    {
                        mipData[mipIndex] = maxDepth;
                    }
                }
            }

            // Store generated mip level in cache
            _mipLevelCache[mipLevel] = mipData;
        }

        /// <summary>
        /// Creates Hi-Z buffer texture using OpenGL.
        /// Matches swkotor.exe texture creation pattern from FUN_00427c90 @ 0x00427c90.
        /// </summary>
        /// <param name="width">Texture width.</param>
        /// <param name="height">Texture height.</param>
        /// <param name="mipLevels">Number of mip levels.</param>
        /// <remarks>
        /// KOTOR1 Implementation (swkotor.exe):
        /// - Uses glGenTextures to generate texture ID (swkotor.exe: FUN_00427c90 pattern)
        /// - Uses glBindTexture to bind texture (swkotor.exe: GL_TEXTURE_2D binding)
        /// - Sets texture parameters: GL_TEXTURE_MIN_FILTER, GL_TEXTURE_MAG_FILTER, GL_LINEAR_MIPMAP_LINEAR
        /// - Format: GL_R32F (single-channel float for depth values)
        /// - Matches DAT_007a687c, DAT_007a6870 texture ID pattern (KOTOR1-specific global variables)
        /// </remarks>
        private void CreateHiZBufferTexture(int width, int height, int mipLevels)
        {
            // Make OpenGL context current
            if (!wglMakeCurrent(_glDevice, _glContext))
            {
                throw new InvalidOperationException("Failed to make OpenGL context current for texture creation.");
            }

            try
            {
                // Generate texture ID
                // Matches swkotor.exe: glGenTextures(1, &textureId) pattern from FUN_00427c90
                uint[] textureIds = new uint[1];
                glGenTextures(1, textureIds);
                _hiZBufferTexture = textureIds[0];

                // Bind texture and set parameters
                // Matches swkotor.exe: glBindTexture(GL_TEXTURE_2D, textureId) pattern
                glBindTexture(GL_TEXTURE_2D, _hiZBufferTexture);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR_MIPMAP_LINEAR);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);

                // Allocate texture storage (mip level 0 will be filled when GenerateHiZBuffer is called)
                // Matches swkotor.exe: glTexImage2D pattern from FUN_00427c90
                glTexImage2D(GL_TEXTURE_2D, 0, (int)GL_R32F, width, height, 0, GL_RED, GL_FLOAT, IntPtr.Zero);
            }
            finally
            {
                glBindTexture(GL_TEXTURE_2D, 0);
            }
        }

        /// <summary>
        /// Disposes Hi-Z buffer texture using OpenGL.
        /// Matches swkotor.exe texture cleanup pattern.
        /// </summary>
        private void DisposeHiZBuffer()
        {
            if (_hiZBufferTexture != 0)
            {
                // Make OpenGL context current
                if (wglMakeCurrent(_glDevice, _glContext))
                {
                    try
                    {
                        // Delete texture
                        // Matches swkotor.exe: glDeleteTextures pattern
                        uint[] textureIds = new uint[] { _hiZBufferTexture };
                        glDeleteTextures(1, textureIds);
                    }
                    finally
                    {
                        glBindTexture(GL_TEXTURE_2D, 0);
                    }
                }
                _hiZBufferTexture = 0;
            }
        }

        /// <summary>
        /// Calculates the number of mip levels for a given texture size.
        /// </summary>
        private int CalculateMipLevels(int width, int height)
        {
            int maxDimension = Math.Max(width, height);
            int levels = 1;
            while (maxDimension > 1)
            {
                maxDimension >>= 1;
                levels++;
            }
            return levels;
        }

        /// <summary>
        /// Calculates the appropriate mip level based on screen space size.
        /// </summary>
        protected override int CalculateMipLevel(float screenSize)
        {
            // Calculate mip level based on screen space size
            // Larger screen space objects use lower mip levels (more detail)
            // Smaller screen space objects use higher mip levels (less detail, faster)
            if (screenSize <= 0.0f)
            {
                return 0;
            }

            // Find the mip level where the screen space size matches the mip level resolution
            int maxMipLevels = CalculateMipLevels(_width, _height);
            for (int mip = 0; mip < maxMipLevels; mip++)
            {
                int mipWidth = Math.Max(1, _width >> mip);
                int mipHeight = Math.Max(1, _height >> mip);
                float mipSize = Math.Max(mipWidth, mipHeight);

                if (screenSize >= mipSize * 0.5f)
                {
                    return mip;
                }
            }

            return maxMipLevels - 1;
        }

        /// <summary>
        /// Reads mip level 0 (full resolution) from the Hi-Z buffer.
        /// </summary>
        protected override float[] ReadMipLevel0()
        {
            if (!Enabled || _hiZBufferTexture == 0)
            {
                return null;
            }

            // Make OpenGL context current
            if (!wglMakeCurrent(_glDevice, _glContext))
            {
                return null;
            }

            try
            {
                // Read full resolution texture (mip 0) from GPU
                float[] fullResData = new float[_width * _height];
                glBindTexture(GL_TEXTURE_2D, _hiZBufferTexture);
                glGetTexImage(GL_TEXTURE_2D, 0, GL_RED, GL_FLOAT, fullResData);
                return fullResData;
            }
            finally
            {
                glBindTexture(GL_TEXTURE_2D, 0);
            }
        }

        /// <summary>
        /// Generates Hi-Z buffer from depth buffer.
        /// </summary>
        public override void GenerateHiZBuffer(object depthBuffer)
        {
            GenerateHiZBufferInternal(depthBuffer);
        }

        /// <summary>
        /// Checks if Hi-Z buffer is available.
        /// </summary>
        protected override bool HasHiZBuffer()
        {
            return _hiZBufferTexture != 0;
        }

        /// <summary>
        /// Samples Hi-Z buffer to get maximum depth in a screen space region.
        /// </summary>
        protected override float SampleHiZBufferMaxDepth(int minX, int minY, int maxX, int maxY, int mipLevel)
        {
            // Use internal implementation which handles cache validation and fallback
            return SampleHiZBufferMaxDepthInternal(minX, minY, maxX, maxY, mipLevel);
        }

        /// <summary>
        /// Disposes the occlusion culler and releases resources.
        /// </summary>
        public override void Dispose()
        {
            DisposeHiZBuffer();
        }

        #region OpenGL P/Invoke Declarations (Windows-only, matches swkotor.exe)

        // OpenGL Constants (matches swkotor.exe OpenGL usage)
        private const uint GL_TEXTURE_2D = 0x0DE1;
        private const uint GL_DEPTH_COMPONENT = 0x1902;
        private const uint GL_RED = 0x1903;
        private const uint GL_FLOAT = 0x1406;
        private const uint GL_R32F = 0x822E; // GL_R32F (OpenGL 3.0+, but we'll use GL_RED for compatibility)
        private const uint GL_TEXTURE_MIN_FILTER = 0x2801;
        private const uint GL_TEXTURE_MAG_FILTER = 0x2800;
        private const uint GL_TEXTURE_WRAP_S = 0x2802;
        private const uint GL_TEXTURE_WRAP_T = 0x2803;
        private const uint GL_LINEAR = 0x2601;
        private const uint GL_LINEAR_MIPMAP_LINEAR = 0x2703;
        private const uint GL_CLAMP_TO_EDGE = 0x812F;

        // WGL Constants (matches swkotor.exe WGL usage)
        private const string OPENGL32_DLL = "opengl32.dll";
        private const string GDI32_DLL = "gdi32.dll";

        // OpenGL Function Declarations (matches swkotor.exe: FUN_00427c90 @ 0x00427c90)
        // NOTE: glReadPixels exists @ 0x0078be1a but is NEVER CALLED in original engine (no cross-references)
        // Original engine uses glCopyTexImage2D instead (FUN_00427c90 line 29)
        [DllImport(OPENGL32_DLL, EntryPoint = "glCopyTexImage2D", CallingConvention = CallingConvention.Cdecl)]
        private static extern void glCopyTexImage2D(uint target, int level, uint internalFormat, int x, int y, int width, int height, int border);

        [DllImport(OPENGL32_DLL, EntryPoint = "glGenTextures", CallingConvention = CallingConvention.Cdecl)]
        private static extern void glGenTextures(int n, uint[] textures);

        [DllImport(OPENGL32_DLL, EntryPoint = "glDeleteTextures", CallingConvention = CallingConvention.Cdecl)]
        private static extern void glDeleteTextures(int n, uint[] textures);

        [DllImport(OPENGL32_DLL, EntryPoint = "glBindTexture", CallingConvention = CallingConvention.Cdecl)]
        private static extern void glBindTexture(uint target, uint texture);

        [DllImport(OPENGL32_DLL, EntryPoint = "glTexImage2D", CallingConvention = CallingConvention.Cdecl)]
        private static extern void glTexImage2D(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, float[] data);

        [DllImport(OPENGL32_DLL, EntryPoint = "glTexImage2D", CallingConvention = CallingConvention.Cdecl)]
        private static extern void glTexImage2D(uint target, int level, int internalFormat, int width, int height, int border, uint format, uint type, IntPtr data);

        [DllImport(OPENGL32_DLL, EntryPoint = "glGetTexImage", CallingConvention = CallingConvention.Cdecl)]
        private static extern void glGetTexImage(uint target, int level, uint format, uint type, float[] pixels);

        [DllImport(OPENGL32_DLL, EntryPoint = "glTexParameteri", CallingConvention = CallingConvention.Cdecl)]
        private static extern void glTexParameteri(uint target, uint pname, int param);

        // WGL Function Declarations (matches swkotor.exe: wglCreateContext @ 0x0073d2b8, wglMakeCurrent usage)
        [DllImport(GDI32_DLL, EntryPoint = "wglMakeCurrent", CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

        #endregion
    }
}

