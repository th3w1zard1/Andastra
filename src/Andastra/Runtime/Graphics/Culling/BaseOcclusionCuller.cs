using System;
using System.Collections.Generic;
using System.Numerics;

namespace Andastra.Runtime.Graphics.Common.Culling
{
    /// <summary>
    /// Base class for occlusion culling systems using Hi-Z (Hierarchical-Z) buffer.
    ///
    /// Occlusion culling determines which objects are hidden behind other objects,
    /// allowing us to skip rendering entirely hidden geometry.
    ///
    /// Common functionality across all graphics backends:
    /// - Hi-Z buffer generation from depth buffer
    /// - Temporal coherence (objects stay occluded for multiple frames)
    /// - AABB projection to screen space
    /// - Mip level selection based on screen space size
    /// - Occlusion statistics tracking
    /// </summary>
    /// <remarks>
    /// Occlusion Culling System (Modern Enhancement):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) rendering system architecture
    /// - Located via string references: Original engine uses VIS file-based room visibility culling
    /// - VIS file format: "%s/%s.VIS" @ 0x007b972c (VIS file path format), "visasmarr" @ 0x007bf720 (VIS file reference)
    /// - Original implementation: KOTOR uses VIS (visibility) files for room-based occlusion culling
    /// - VIS files: Pre-computed room-to-room visibility relationships for efficient occlusion culling
    /// - Original occlusion culling: Room-based visibility from VIS files combined with frustum culling
    /// - This is a modernization feature: Hi-Z buffer provides GPU-accelerated pixel-accurate occlusion testing
    /// - Modern enhancement: More accurate than VIS files for dynamic objects, requires modern GPU features
    /// - Original engine: DirectX 8/9 era, Hi-Z buffers not available, relied on VIS file pre-computation
    /// - Combined approaches: Modern renderer can use both VIS-based room culling + Hi-Z for best performance
    ///
    /// Inheritance Structure:
    /// - BaseOcclusionCuller (this class) - Common occlusion culling logic
    ///   - MonoGameOcclusionCuller : BaseOcclusionCuller (Runtime.MonoGame.Culling) - MonoGame-specific implementation
    ///   - StrideOcclusionCuller : BaseOcclusionCuller (Runtime.Stride.Culling) - Stride-specific implementation
    /// </remarks>
    public abstract class BaseOcclusionCuller : IDisposable
    {
        protected int _width;
        protected int _height;

        // Temporal occlusion cache (frame-based)
        protected readonly Dictionary<uint, OcclusionInfo> _occlusionCache;
        protected int _currentFrame;

        // View and projection matrices for screen space projection
        protected Matrix4x4 _viewMatrix;
        protected Matrix4x4 _projectionMatrix;
        protected bool _matricesValid;

        // Cached Hi-Z buffer data for CPU-side sampling (updated on demand)
        protected float[] _hiZBufferData;
        protected int _hiZBufferDataMipLevel;
        protected bool _hiZBufferDataValid;

        // CPU-side mip level cache for proper max depth calculations
        // Graphics backends may not support writing to specific mip levels,
        // so we maintain a CPU-side cache of all mip levels with proper max depth values
        protected float[][] _mipLevelCache;
        protected bool _mipLevelCacheValid;

        // Statistics
        protected OcclusionStats _stats;

        /// <summary>
        /// Gets occlusion statistics.
        /// </summary>
        public OcclusionStats Stats
        {
            get { return _stats; }
        }

        /// <summary>
        /// Gets or sets whether occlusion culling is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum object size to test (smaller objects skip occlusion test).
        /// </summary>
        public float MinTestSize { get; set; } = 1.0f;

        /// <summary>
        /// Gets or sets the temporal cache lifetime in frames.
        /// </summary>
        public int CacheLifetime { get; set; } = 3;

        /// <summary>
        /// Initializes a new occlusion culler.
        /// </summary>
        /// <param name="width">Buffer width. Must be greater than zero.</param>
        /// <param name="height">Buffer height. Must be greater than zero.</param>
        /// <exception cref="ArgumentException">Thrown if width or height is less than or equal to zero.</exception>
        protected BaseOcclusionCuller(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentException("Width must be greater than zero.", nameof(width));
            }
            if (height <= 0)
            {
                throw new ArgumentException("Height must be greater than zero.", nameof(height));
            }

            _width = width;
            _height = height;
            _occlusionCache = new Dictionary<uint, OcclusionInfo>();
            _stats = new OcclusionStats();
        }

        /// <summary>
        /// Checks if Hi-Z buffer is available.
        /// </summary>
        /// <returns>True if Hi-Z buffer exists, false otherwise.</returns>
        protected abstract bool HasHiZBuffer();

        /// <summary>
        /// Generates Hi-Z buffer from depth buffer.
        /// Must be called after depth pre-pass or main depth rendering.
        /// </summary>
        /// <param name="depthBuffer">Depth buffer to downsample. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if depthBuffer is null.</exception>
        public abstract void GenerateHiZBuffer(object depthBuffer);

        /// <summary>
        /// Tests if an AABB is occluded using Hi-Z buffer.
        /// </summary>
        /// <param name="minPoint">Minimum corner of AABB.</param>
        /// <param name="maxPoint">Maximum corner of AABB.</param>
        /// <param name="objectId">Unique ID for temporal caching.</param>
        /// <returns>True if object is occluded (should be culled).</returns>
        public bool IsOccluded(Vector3 minPoint, Vector3 maxPoint, uint objectId)
        {
            if (!Enabled)
            {
                return false;
            }

            // Check temporal cache first
            OcclusionInfo cached;
            if (_occlusionCache.TryGetValue(objectId, out cached))
            {
                if (_currentFrame - cached.LastFrame <= CacheLifetime)
                {
                    _stats.CacheHits++;
                    return cached.Occluded;
                }
                // Cache expired
                _occlusionCache.Remove(objectId);
            }

            // Test against Hi-Z buffer
            bool occluded = TestOcclusionHiZ(minPoint, maxPoint);

            // Cache result
            _occlusionCache[objectId] = new OcclusionInfo
            {
                Occluded = occluded,
                LastFrame = _currentFrame
            };

            if (occluded)
            {
                _stats.OccludedObjects++;
            }
            else
            {
                _stats.VisibleObjects++;
            }
            _stats.TotalTests++;

            return occluded;
        }

        /// <summary>
        /// Tests occlusion using Hi-Z buffer hierarchical depth test.
        /// Projects AABB to screen space, samples Hi-Z buffer at appropriate mip level, and compares depths.
        ///
        /// Implementation based on Hi-Z occlusion culling algorithm:
        /// 1. Project AABB corners to screen space using view/projection matrices
        /// 2. Calculate screen space bounding rectangle
        /// 3. Find appropriate mip level based on screen space size
        /// 4. Sample Hi-Z buffer at mip level to get maximum depth in region
        /// 5. Compare AABB minimum depth against Hi-Z maximum depth
        /// 6. If AABB min depth > Hi-Z max depth, object is occluded
        /// </summary>
        protected bool TestOcclusionHiZ(Vector3 minPoint, Vector3 maxPoint)
        {
            if (!HasHiZBuffer())
            {
                return false; // No Hi-Z buffer available, assume visible
            }

            if (!_matricesValid)
            {
                return false; // Matrices not set, assume visible
            }

            // Calculate AABB size in world space
            Vector3 aabbSize = maxPoint - minPoint;
            float aabbSizeMax = Math.Max(Math.Max(aabbSize.X, aabbSize.Y), aabbSize.Z);

            // Skip occlusion test for objects smaller than minimum test size
            if (aabbSizeMax < MinTestSize)
            {
                return false;
            }

            // Project AABB corners to screen space
            // AABB has 8 corners: all combinations of min/max for X, Y, Z
            Vector3[] aabbCorners = new Vector3[8];
            aabbCorners[0] = new Vector3(minPoint.X, minPoint.Y, minPoint.Z);
            aabbCorners[1] = new Vector3(maxPoint.X, minPoint.Y, minPoint.Z);
            aabbCorners[2] = new Vector3(minPoint.X, maxPoint.Y, minPoint.Z);
            aabbCorners[3] = new Vector3(maxPoint.X, maxPoint.Y, minPoint.Z);
            aabbCorners[4] = new Vector3(minPoint.X, minPoint.Y, maxPoint.Z);
            aabbCorners[5] = new Vector3(maxPoint.X, minPoint.Y, maxPoint.Z);
            aabbCorners[6] = new Vector3(minPoint.X, maxPoint.Y, maxPoint.Z);
            aabbCorners[7] = new Vector3(maxPoint.X, maxPoint.Y, maxPoint.Z);

            // Project all corners to screen space and find bounding rectangle
            float minScreenX = float.MaxValue;
            float maxScreenX = float.MinValue;
            float minScreenY = float.MaxValue;
            float maxScreenY = float.MinValue;
            float minDepth = float.MaxValue;
            bool anyVisible = false;

            // Combine view and projection matrices for efficiency
            Matrix4x4 viewProj = _viewMatrix * _projectionMatrix;

            for (int i = 0; i < 8; i++)
            {
                // Transform to view space
                Vector4 viewPos = Vector4.Transform(
                    new Vector4(aabbCorners[i].X, aabbCorners[i].Y, aabbCorners[i].Z, 1.0f),
                    _viewMatrix);

                // Project to clip space
                Vector4 clipPos = Vector4.Transform(viewPos, _projectionMatrix);

                // Perspective divide
                if (Math.Abs(clipPos.W) > 1e-6f)
                {
                    clipPos.X /= clipPos.W;
                    clipPos.Y /= clipPos.W;
                    clipPos.Z /= clipPos.W;
                }

                // Check if corner is behind camera (Z > 1 in clip space means behind far plane, Z < -1 means behind camera)
                if (clipPos.Z < -1.0f || clipPos.Z > 1.0f)
                {
                    continue; // Corner is outside view frustum
                }

                anyVisible = true;

                // Convert to screen space (0 to width/height)
                float screenX = (clipPos.X * 0.5f + 0.5f) * _width;
                float screenY = (1.0f - (clipPos.Y * 0.5f + 0.5f)) * _height;

                // Clamp to viewport bounds
                screenX = Math.Max(0, Math.Min(screenX, _width - 1));
                screenY = Math.Max(0, Math.Min(screenY, _height - 1));

                minScreenX = Math.Min(minScreenX, screenX);
                maxScreenX = Math.Max(maxScreenX, screenX);
                minScreenY = Math.Min(minScreenY, screenY);
                maxScreenY = Math.Max(maxScreenY, screenY);

                // Track minimum depth (closest to camera) for occlusion test
                // In clip space, Z ranges from -1 (near) to 1 (far), but we need depth in view space
                // For occlusion testing, we use the view space Z (depth from camera)
                float viewSpaceDepth = viewPos.Z;
                minDepth = Math.Min(minDepth, viewSpaceDepth);
            }

            // If no corners are visible, object is outside frustum (not occluded, just culled by frustum)
            if (!anyVisible)
            {
                return false;
            }

            // Calculate screen space bounding rectangle
            int screenMinX = (int)Math.Floor(minScreenX);
            int screenMaxX = (int)Math.Ceiling(maxScreenX);
            int screenMinY = (int)Math.Floor(minScreenY);
            int screenMaxY = (int)Math.Ceiling(maxScreenY);

            // Clamp to viewport
            screenMinX = Math.Max(0, Math.Min(screenMinX, _width - 1));
            screenMaxX = Math.Max(0, Math.Min(screenMaxX, _width - 1));
            screenMinY = Math.Max(0, Math.Min(screenMinY, _height - 1));
            screenMaxY = Math.Max(0, Math.Min(screenMaxY, _height - 1));

            // Calculate screen space size for mip level selection
            float screenWidth = screenMaxX - screenMinX + 1;
            float screenHeight = screenMaxY - screenMinY + 1;
            float screenSize = Math.Max(screenWidth, screenHeight);

            // Find appropriate mip level based on screen space size
            // Higher mip levels for smaller screen space objects (more aggressive culling)
            int mipLevel = CalculateMipLevel(screenSize);

            // Sample Hi-Z buffer at calculated mip level
            // Get maximum depth in the screen space region
            float hiZMaxDepth = SampleHiZBufferMaxDepth(screenMinX, screenMinY, screenMaxX, screenMaxY, mipLevel);

            // If Hi-Z max depth is invalid (no data), assume visible
            if (hiZMaxDepth <= 0.0f || float.IsInfinity(hiZMaxDepth) || float.IsNaN(hiZMaxDepth))
            {
                return false;
            }

            // Compare AABB minimum depth against Hi-Z maximum depth
            // In view space, larger Z values are farther from camera
            // If the AABB's closest point (minDepth) is farther than the Hi-Z max depth,
            // the entire AABB is behind occluders and can be culled
            // Note: We add a small bias to prevent false positives from floating point precision
            const float depthBias = 0.01f;
            bool occluded = minDepth > (hiZMaxDepth + depthBias);

            return occluded;
        }

        /// <summary>
        /// Calculates the appropriate mip level based on screen space size.
        /// </summary>
        /// <param name="screenSize">Screen space size of the object.</param>
        /// <returns>Mip level to use for sampling.</returns>
        protected abstract int CalculateMipLevel(float screenSize);

        /// <summary>
        /// Samples Hi-Z buffer to get maximum depth in a screen space region.
        /// Uses CPU-side texture readback for sampling.
        /// </summary>
        /// <param name="minX">Minimum X coordinate in screen space.</param>
        /// <param name="minY">Minimum Y coordinate in screen space.</param>
        /// <param name="maxX">Maximum X coordinate in screen space.</param>
        /// <param name="maxY">Maximum Y coordinate in screen space.</param>
        /// <param name="mipLevel">Mip level to sample from.</param>
        /// <returns>Maximum depth value in the region, or 0 if sampling fails.</returns>
        protected abstract float SampleHiZBufferMaxDepth(int minX, int minY, int maxX, int maxY, int mipLevel);

        /// <summary>
        /// Generates all mip levels by calculating maximum depth from 2x2 regions.
        /// Uses CPU-side calculation for accurate max depth operations.
        /// </summary>
        /// <param name="maxMipLevels">Maximum number of mip levels to generate.</param>
        protected void GenerateMipLevelsWithMaxDepth(int maxMipLevels)
        {
            _mipLevelCacheValid = false;

            // Initialize mip level cache if needed
            if (_mipLevelCache == null)
            {
                _mipLevelCache = new float[maxMipLevels][];

                // Read mip level 0 (full resolution) from Hi-Z buffer
                float[] mip0Data = ReadMipLevel0();
                if (mip0Data != null)
                {
                    _mipLevelCache[0] = mip0Data;
                }
            }

            // Generate each mip level from previous level
            for (int mip = 1; mip < maxMipLevels; mip++)
            {
                int mipWidth = Math.Max(1, _width >> mip);
                int mipHeight = Math.Max(1, _height >> mip);
                int prevMipWidth = Math.Max(1, _width >> (mip - 1));
                int prevMipHeight = Math.Max(1, _height >> (mip - 1));

                // Get previous mip level data from cache
                float[] prevMipData = _mipLevelCache[mip - 1];
                if (prevMipData == null)
                {
                    // Previous mip level not cached, need to generate it first
                    // This shouldn't happen if we generate mip levels in order, but handle it gracefully
                    continue;
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

                // Store in cache for future use
                _mipLevelCache[mip] = mipData;
            }

            _mipLevelCacheValid = true;
        }

        /// <summary>
        /// Reads mip level 0 (full resolution) from the Hi-Z buffer.
        /// </summary>
        /// <returns>Full resolution depth data, or null if read fails.</returns>
        protected abstract float[] ReadMipLevel0();

        /// <summary>
        /// Updates view and projection matrices for screen space projection.
        /// Must be called each frame before occlusion testing.
        /// </summary>
        /// <param name="viewMatrix">View matrix (world to camera space).</param>
        /// <param name="projectionMatrix">Projection matrix (camera to clip space).</param>
        public void UpdateMatrices(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            _viewMatrix = viewMatrix;
            _projectionMatrix = projectionMatrix;
            _matricesValid = true;
            _hiZBufferDataValid = false; // Invalidate cached Hi-Z data when matrices change
            _mipLevelCacheValid = false; // Invalidate mip level cache when matrices change
        }

        /// <summary>
        /// Starts a new frame, clearing expired cache entries.
        /// </summary>
        public void BeginFrame()
        {
            _currentFrame++;
            _stats.Reset();

            // Clean up expired cache entries
            if (_occlusionCache.Count > 10000) // Prevent unbounded growth
            {
                var toRemove = new List<uint>();
                foreach (var kvp in _occlusionCache)
                {
                    if (_currentFrame - kvp.Value.LastFrame > CacheLifetime)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
                foreach (uint id in toRemove)
                {
                    _occlusionCache.Remove(id);
                }
            }
        }

        /// <summary>
        /// Resizes the occlusion culler for new resolution.
        /// </summary>
        /// <param name="width">New buffer width. Must be greater than zero.</param>
        /// <param name="height">New buffer height. Must be greater than zero.</param>
        /// <exception cref="ArgumentException">Thrown if width or height is less than or equal to zero.</exception>
        public virtual void Resize(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentException("Width must be greater than zero.", nameof(width));
            }
            if (height <= 0)
            {
                throw new ArgumentException("Height must be greater than zero.", nameof(height));
            }

            // Update width and height fields for dynamic resizing
            _width = width;
            _height = height;

            // Invalidate mip level cache when resizing
            _mipLevelCache = null;
            _mipLevelCacheValid = false;
        }

        public abstract void Dispose();

        protected struct OcclusionInfo
        {
            public bool Occluded;
            public int LastFrame;
        }
    }

    /// <summary>
    /// Statistics for occlusion culling.
    /// </summary>
    public class OcclusionStats
    {
        /// <summary>
        /// Total occlusion tests performed.
        /// </summary>
        public int TotalTests { get; set; }

        /// <summary>
        /// Objects found to be occluded.
        /// </summary>
        public int OccludedObjects { get; set; }

        /// <summary>
        /// Objects found to be visible.
        /// </summary>
        public int VisibleObjects { get; set; }

        /// <summary>
        /// Cache hits (temporal coherence).
        /// </summary>
        public int CacheHits { get; set; }

        /// <summary>
        /// Gets the occlusion rate (percentage of objects occluded).
        /// </summary>
        public float OcclusionRate
        {
            get
            {
                if (TotalTests == 0)
                {
                    return 0.0f;
                }
                return (OccludedObjects / (float)TotalTests) * 100.0f;
            }
        }

        /// <summary>
        /// Resets statistics for a new frame.
        /// </summary>
        public void Reset()
        {
            TotalTests = 0;
            OccludedObjects = 0;
            VisibleObjects = 0;
            CacheHits = 0;
        }
    }
}

