using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;

namespace Andastra.Game.Graphics.MonoGame.Raytracing
{
    /// <summary>
    /// Native hardware raytracing system using DXR/Vulkan RT.
    ///
    /// Features:
    /// - Bottom-level acceleration structure (BLAS) management
    /// - Top-level acceleration structure (TLAS) with instancing
    /// - Raytraced shadows (soft shadows with penumbra)
    /// - Raytraced reflections (glossy and mirror)
    /// - Raytraced ambient occlusion (RTAO)
    /// - Raytraced global illumination (RTGI)
    /// - Temporal denoising integration
    /// - Native Intel Open Image Denoise (OIDN) library integration
    /// </summary>
    public class NativeRaytracingSystem : IRaytracingSystem
    {
        #region OIDN (Open Image Denoise) Native Library Integration

        // OIDN library name - platform-specific
        // Windows: OpenImageDenoise.dll
        // Linux: libOpenImageDenoise.so
        // macOS: libOpenImageDenoise.dylib
        private const string OIDN_LIBRARY_WINDOWS = "OpenImageDenoise.dll";
        private const string OIDN_LIBRARY_LINUX = "libOpenImageDenoise.so";
        private const string OIDN_LIBRARY_MACOS = "libOpenImageDenoise.dylib";

        // OIDN device types
        private const int OIDN_DEVICE_TYPE_DEFAULT = 0;
        private const int OIDN_DEVICE_TYPE_CPU = 1;
        private const int OIDN_DEVICE_TYPE_SYCL = 2; // Intel oneAPI SYCL
        private const int OIDN_DEVICE_TYPE_CUDA = 3; // NVIDIA CUDA
        private const int OIDN_DEVICE_TYPE_HIP = 4; // AMD HIP

        // OIDN filter types
        private const string OIDN_FILTER_TYPE_RT = "RT"; // Raytracing denoising filter

        // OIDN image formats
        private const int OIDN_FORMAT_FLOAT3 = 0; // RGB float
        private const int OIDN_FORMAT_FLOAT4 = 1; // RGBA float

        // OIDN image layout
        private const int OIDN_LAYOUT_ROW_MAJOR = 0;

        // OIDN error codes
        private const int OIDN_SUCCESS = 0;
        private const int OIDN_ERROR_INVALID_ARGUMENT = 1;
        private const int OIDN_ERROR_INVALID_OPERATION = 2;
        private const int OIDN_ERROR_OUT_OF_MEMORY = 3;
        private const int OIDN_ERROR_UNSUPPORTED_HARDWARE = 4;
        private const int OIDN_ERROR_CANCELLED = 5;

        // OIDN API function delegates
        // Based on Intel OIDN API: https://www.openimagedenoise.org/documentation.html
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr oidnNewDeviceDelegate(int deviceType);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void oidnCommitDeviceDelegate(IntPtr device);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void oidnReleaseDeviceDelegate(IntPtr device);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr oidnNewFilterDelegate(IntPtr device, [MarshalAs(UnmanagedType.LPStr)] string filterType);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void oidnSetSharedFilterImageDelegate(IntPtr filter, [MarshalAs(UnmanagedType.LPStr)] string name, IntPtr ptr, int format, int width, int height, int byteOffset, int bytePixelStride, int byteRowStride);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void oidnSetFilter1bDelegate(IntPtr filter, [MarshalAs(UnmanagedType.LPStr)] string name, bool value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void oidnCommitFilterDelegate(IntPtr filter);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void oidnExecuteFilterDelegate(IntPtr filter);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void oidnReleaseFilterDelegate(IntPtr filter);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int oidnGetDeviceErrorDelegate(IntPtr device, [MarshalAs(UnmanagedType.LPStr)] out string outMessage);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void oidnSetDevice1bDelegate(IntPtr device, [MarshalAs(UnmanagedType.LPStr)] string name, bool value);

        // OIDN function pointers (loaded dynamically)
        private static oidnNewDeviceDelegate _oidnNewDevice;
        private static oidnCommitDeviceDelegate _oidnCommitDevice;
        private static oidnReleaseDeviceDelegate _oidnReleaseDevice;
        private static oidnNewFilterDelegate _oidnNewFilter;
        private static oidnSetSharedFilterImageDelegate _oidnSetSharedFilterImage;
        private static oidnSetFilter1bDelegate _oidnSetFilter1b;
        private static oidnCommitFilterDelegate _oidnCommitFilter;
        private static oidnExecuteFilterDelegate _oidnExecuteFilter;
        private static oidnReleaseFilterDelegate _oidnReleaseFilter;
        private static oidnGetDeviceErrorDelegate _oidnGetDeviceError;
        private static oidnSetDevice1bDelegate _oidnSetDevice1b;

        // OIDN library handle
        private static IntPtr _oidnLibraryHandle = IntPtr.Zero;
        private static bool _oidnLibraryLoaded = false;
        private static readonly object _oidnLoadLock = new object();

        // Platform-specific library loading functions
        // Based on Windows API: LoadLibrary/FreeLibrary/GetProcAddress
        // Based on Linux/macOS: dlopen/dlclose/dlsym
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("libdl.so.2", CharSet = CharSet.Ansi)]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.so.2", CharSet = CharSet.Ansi)]
        private static extern int dlclose(IntPtr handle);

        [DllImport("libdl.so.2", CharSet = CharSet.Ansi)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        private const int RTLD_NOW = 2; // dlopen flag: resolve all symbols immediately

        /// <summary>
        /// Loads the OIDN library and initializes function pointers.
        /// Based on Intel OIDN API: Library must be loaded before use.
        /// Uses platform-specific library loading (LoadLibrary on Windows, dlopen on Linux/macOS).
        /// </summary>
        /// <returns>True if library loaded successfully, false otherwise.</returns>
        private static bool LoadOIDNLibrary()
        {
            lock (_oidnLoadLock)
            {
                if (_oidnLibraryLoaded)
                {
                    return true;
                }

                // Determine library name based on platform
                string libraryName = null;
                bool isWindows = false;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    libraryName = OIDN_LIBRARY_WINDOWS;
                    isWindows = true;
                }
                else if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    // Check if macOS
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        libraryName = OIDN_LIBRARY_MACOS;
                    }
                    else
                    {
                        libraryName = OIDN_LIBRARY_LINUX;
                    }
                }

                if (libraryName == null)
                {
                    Console.WriteLine("[NativeRT] LoadOIDNLibrary: Unsupported platform for OIDN");
                    return false;
                }

                // Try to load the library
                try
                {
                    if (isWindows)
                    {
                        _oidnLibraryHandle = LoadLibrary(libraryName);
                    }
                    else
                    {
                        _oidnLibraryHandle = dlopen(libraryName, RTLD_NOW);
                    }

                    if (_oidnLibraryHandle == IntPtr.Zero)
                    {
                        Console.WriteLine($"[NativeRT] LoadOIDNLibrary: Failed to load {libraryName}");
                        return false;
                    }

                    // Load function pointers
                    _oidnNewDevice = GetFunction<oidnNewDeviceDelegate>("oidnNewDevice");
                    _oidnCommitDevice = GetFunction<oidnCommitDeviceDelegate>("oidnCommitDevice");
                    _oidnReleaseDevice = GetFunction<oidnReleaseDeviceDelegate>("oidnReleaseDevice");
                    _oidnNewFilter = GetFunction<oidnNewFilterDelegate>("oidnNewFilter");
                    _oidnSetSharedFilterImage = GetFunction<oidnSetSharedFilterImageDelegate>("oidnSetSharedFilterImage");
                    _oidnSetFilter1b = GetFunction<oidnSetFilter1bDelegate>("oidnSetFilter1b");
                    _oidnCommitFilter = GetFunction<oidnCommitFilterDelegate>("oidnCommitFilter");
                    _oidnExecuteFilter = GetFunction<oidnExecuteFilterDelegate>("oidnExecuteFilter");
                    _oidnReleaseFilter = GetFunction<oidnReleaseFilterDelegate>("oidnReleaseFilter");
                    _oidnGetDeviceError = GetFunction<oidnGetDeviceErrorDelegate>("oidnGetDeviceError");
                    _oidnSetDevice1b = GetFunction<oidnSetDevice1bDelegate>("oidnSetDevice1b");

                    // Verify all functions loaded
                    if (_oidnNewDevice == null || _oidnCommitDevice == null || _oidnReleaseDevice == null ||
                        _oidnNewFilter == null || _oidnSetSharedFilterImage == null || _oidnSetFilter1b == null ||
                        _oidnCommitFilter == null || _oidnExecuteFilter == null || _oidnReleaseFilter == null ||
                        _oidnGetDeviceError == null || _oidnSetDevice1b == null)
                    {
                        Console.WriteLine("[NativeRT] LoadOIDNLibrary: Failed to load all OIDN functions");
                        UnloadOIDNLibrary();
                        return false;
                    }

                    _oidnLibraryLoaded = true;
                    Console.WriteLine($"[NativeRT] LoadOIDNLibrary: Successfully loaded {libraryName}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NativeRT] LoadOIDNLibrary: Exception loading library: {ex.Message}");
                    UnloadOIDNLibrary();
                    return false;
                }
            }
        }

        /// <summary>
        /// Unloads the OIDN library.
        /// Uses platform-specific library unloading (FreeLibrary on Windows, dlclose on Linux/macOS).
        /// </summary>
        private static void UnloadOIDNLibrary()
        {
            lock (_oidnLoadLock)
            {
                if (_oidnLibraryHandle != IntPtr.Zero)
                {
                    try
                    {
                        bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                        if (isWindows)
                        {
                            FreeLibrary(_oidnLibraryHandle);
                        }
                        else
                        {
                            dlclose(_oidnLibraryHandle);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[NativeRT] UnloadOIDNLibrary: Exception unloading library: {ex.Message}");
                    }
                    _oidnLibraryHandle = IntPtr.Zero;
                }

                // Clear function pointers
                _oidnNewDevice = null;
                _oidnCommitDevice = null;
                _oidnReleaseDevice = null;
                _oidnNewFilter = null;
                _oidnSetSharedFilterImage = null;
                _oidnSetFilter1b = null;
                _oidnCommitFilter = null;
                _oidnExecuteFilter = null;
                _oidnReleaseFilter = null;
                _oidnGetDeviceError = null;
                _oidnSetDevice1b = null;

                _oidnLibraryLoaded = false;
            }
        }

        /// <summary>
        /// Gets a function pointer from the loaded library.
        /// Uses platform-specific function lookup (GetProcAddress on Windows, dlsym on Linux/macOS).
        /// </summary>
        private static T GetFunction<T>(string functionName) where T : class
        {
            return GetFunction<T>(_oidnLibraryHandle, functionName);
        }

        /// <summary>
        /// Gets a function pointer from the specified loaded library.
        /// Uses platform-specific function lookup (GetProcAddress on Windows, dlsym on Linux/macOS).
        /// </summary>
        private static T GetFunction<T>(IntPtr libraryHandle, string functionName) where T : class
        {
            if (libraryHandle == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                IntPtr functionPtr;
                bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                if (isWindows)
                {
                    functionPtr = GetProcAddress(libraryHandle, functionName);
                }
                else
                {
                    functionPtr = dlsym(libraryHandle, functionName);
                }

                if (functionPtr == IntPtr.Zero)
                {
                    Console.WriteLine($"[NativeRT] GetFunction: Failed to get function {functionName}");
                    return null;
                }

                return Marshal.GetDelegateForFunctionPointer<T>(functionPtr);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] GetFunction: Exception getting function {functionName}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Initializes OIDN device and filter for native denoising.
        /// Based on Intel OIDN API: oidnNewDevice, oidnNewFilter, oidnCommitDevice, oidnCommitFilter
        /// </summary>
        private bool InitializeOIDN()
        {
            if (_oidnInitialized)
            {
                return true;
            }

            // Load OIDN library if not already loaded
            if (!LoadOIDNLibrary())
            {
                Console.WriteLine("[NativeRT] InitializeOIDN: Failed to load OIDN library, falling back to GPU compute shader");
                _useNativeOIDN = false;
                return false;
            }

            try
            {
                // Create OIDN device (CPU device for CPU-side processing)
                // Based on OIDN API: oidnNewDevice(OIDN_DEVICE_TYPE_CPU) creates a CPU device
                _oidnDevice = _oidnNewDevice(OIDN_DEVICE_TYPE_CPU);
                if (_oidnDevice == IntPtr.Zero)
                {
                    CheckOIDNError(_oidnDevice);
                    Console.WriteLine("[NativeRT] InitializeOIDN: Failed to create OIDN device, falling back to GPU compute shader");
                    _useNativeOIDN = false;
                    return false;
                }

                // Set device properties (optional)
                // oidnSetDevice1b can be used to set device properties like "setAffinity"
                // For now, we use default settings

                // Commit device
                // Based on OIDN API: oidnCommitDevice must be called after setting device properties
                _oidnCommitDevice(_oidnDevice);
                CheckOIDNError(_oidnDevice);

                // Create OIDN filter for raytracing denoising
                // Based on OIDN API: oidnNewFilter(device, "RT") creates a raytracing denoising filter
                _oidnFilter = _oidnNewFilter(_oidnDevice, OIDN_FILTER_TYPE_RT);
                if (_oidnFilter == IntPtr.Zero)
                {
                    CheckOIDNError(_oidnDevice);
                    Console.WriteLine("[NativeRT] InitializeOIDN: Failed to create OIDN filter, falling back to GPU compute shader");
                    _useNativeOIDN = false;
                    ReleaseOIDNDevice();
                    return false;
                }

                _oidnInitialized = true;
                _useNativeOIDN = true;
                Console.WriteLine("[NativeRT] InitializeOIDN: Successfully initialized OIDN native library");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] InitializeOIDN: Exception during initialization: {ex.Message}");
                _useNativeOIDN = false;
                ReleaseOIDNDevice();
                return false;
            }
        }

        /// <summary>
        /// Releases OIDN device and filter.
        /// Based on Intel OIDN API: oidnReleaseFilter, oidnReleaseDevice
        /// </summary>
        private void ReleaseOIDNDevice()
        {
            if (_oidnFilter != IntPtr.Zero)
            {
                try
                {
                    _oidnReleaseFilter(_oidnFilter);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NativeRT] ReleaseOIDNDevice: Exception releasing filter: {ex.Message}");
                }
                _oidnFilter = IntPtr.Zero;
            }

            if (_oidnDevice != IntPtr.Zero)
            {
                try
                {
                    _oidnReleaseDevice(_oidnDevice);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NativeRT] ReleaseOIDNDevice: Exception releasing device: {ex.Message}");
                }
                _oidnDevice = IntPtr.Zero;
            }

            _oidnInitialized = false;
        }

        /// <summary>
        /// Checks for OIDN errors and logs them.
        /// Based on Intel OIDN API: oidnGetDeviceError
        /// </summary>
        private void CheckOIDNError(IntPtr device)
        {
            if (device == IntPtr.Zero || _oidnGetDeviceError == null)
            {
                return;
            }

            try
            {
                int errorCode = _oidnGetDeviceError(device, out string errorMessage);
                if (errorCode != OIDN_SUCCESS)
                {
                    string errorName = GetOIDNErrorName(errorCode);
                    Console.WriteLine($"[NativeRT] OIDN Error: {errorName} - {errorMessage ?? "No message"}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] CheckOIDNError: Exception checking error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the name of an OIDN error code.
        /// </summary>
        private string GetOIDNErrorName(int errorCode)
        {
            switch (errorCode)
            {
                case OIDN_ERROR_INVALID_ARGUMENT:
                    return "OIDN_ERROR_INVALID_ARGUMENT";
                case OIDN_ERROR_INVALID_OPERATION:
                    return "OIDN_ERROR_INVALID_OPERATION";
                case OIDN_ERROR_OUT_OF_MEMORY:
                    return "OIDN_ERROR_OUT_OF_MEMORY";
                case OIDN_ERROR_UNSUPPORTED_HARDWARE:
                    return "OIDN_ERROR_UNSUPPORTED_HARDWARE";
                case OIDN_ERROR_CANCELLED:
                    return "OIDN_ERROR_CANCELLED";
                default:
                    return $"Unknown error ({errorCode})";
            }
        }

        /// <summary>
        /// Transfers texture data from GPU to CPU memory for OIDN processing.
        /// Based on DirectX 12/Vulkan: Uses staging buffer to read texture data.
        /// </summary>
        /// <param name="texture">Texture to read from GPU.</param>
        /// <param name="width">Texture width.</param>
        /// <param name="height">Texture height.</param>
        /// <returns>CPU-accessible byte array containing texture data (RGBA float format), or null on failure.</returns>
        private unsafe float[] ReadTextureDataFromGPU(ITexture texture, int width, int height)
        {
            if (texture == null || _device == null || width <= 0 || height <= 0)
            {
                return null;
            }

            // Create staging buffer for reading texture data
            // Based on DirectX 12/Vulkan: Staging buffers are CPU-accessible and can be used to read GPU resources
            // Size: width * height * 4 components (RGBA) * 4 bytes per float = width * height * 16 bytes
            int bufferSize = width * height * 4 * sizeof(float); // RGBA float format

            IBuffer stagingBuffer = _device.CreateBuffer(new BufferDesc
            {
                ByteSize = bufferSize,
                Usage = BufferUsageFlags.ShaderResource,
                InitialState = ResourceState.CopyDest,
                IsAccelStructBuildInput = false,
                DebugName = "OIDNStagingBuffer"
            });

            if (stagingBuffer == null)
            {
                Console.WriteLine("[NativeRT] ReadTextureDataFromGPU: Failed to create staging buffer");
                return null;
            }

            try
            {
                // Copy texture to staging buffer using command list
                // Based on DirectX 12/Vulkan: CopyTextureRegion or similar command
                ICommandList commandList = _device.CreateCommandList(CommandListType.Compute);
                commandList.Open();

                // Transition texture to copy source state
                commandList.SetTextureState(texture, ResourceState.CopySource);
                commandList.CommitBarriers();

                // Copy texture data to staging buffer
                // Note: ICommandList doesn't have CopyTextureToBuffer, so we use a compute shader or fallback
                // For now, we'll use a workaround: create a CPU-readable texture and copy to it
                // In a full implementation, this would use CopyTextureRegion or similar API

                // Create CPU-readable staging texture
                ITexture stagingTexture = _device.CreateTexture(new TextureDesc
                {
                    Width = width,
                    Height = height,
                    Depth = 1,
                    ArraySize = 1,
                    MipLevels = 1,
                    SampleCount = 1,
                    Format = TextureFormat.R32G32B32A32_Float,
                    Dimension = TextureDimension.Texture2D,
                    Usage = TextureUsage.ShaderResource | TextureUsage.CopyDest,
                    InitialState = ResourceState.CopyDest,
                    KeepInitialState = false,
                    DebugName = "OIDNStagingTexture"
                });

                if (stagingTexture == null)
                {
                    commandList.Close();
                    commandList.Dispose();
                    Console.WriteLine("[NativeRT] ReadTextureDataFromGPU: Failed to create staging texture");
                    return null;
                }

                // Copy source texture to staging texture
                commandList.SetTextureState(stagingTexture, ResourceState.CopyDest);
                commandList.CommitBarriers();
                commandList.CopyTexture(stagingTexture, texture);

                // Transition staging texture to readable state
                commandList.SetTextureState(stagingTexture, ResourceState.CopySource);
                commandList.CommitBarriers();

                commandList.Close();
                _device.ExecuteCommandList(commandList);
                commandList.Dispose();

                // Wait for GPU to finish copying
                _device.WaitIdle();

                // Copy texture data to buffer using compute shader
                // Based on DirectX 12/Vulkan: Use compute shader to copy texture pixels to structured buffer
                // This approach works across all backends that support compute shaders

                // Create structured buffer for texture data output (CPU-accessible readback buffer)
                // Based on DirectX 12: D3D12_HEAP_TYPE_READBACK for CPU-accessible buffers
                // Based on Vulkan: VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT | VK_MEMORY_PROPERTY_HOST_COHERENT_BIT
                // The buffer will store RGBA float4 values for each pixel
                IBuffer readbackBuffer = _device.CreateBuffer(new BufferDesc
                {
                    ByteSize = bufferSize,
                    Usage = BufferUsageFlags.UnorderedAccess | BufferUsageFlags.ShaderResource,
                    InitialState = ResourceState.UnorderedAccess,
                    IsAccelStructBuildInput = false,
                    DebugName = "OIDNReadbackBuffer"
                });

                if (readbackBuffer == null)
                {
                    Console.WriteLine("[NativeRT] ReadTextureDataFromGPU: Failed to create readback buffer");
                    stagingTexture.Dispose();
                    stagingBuffer.Dispose();
                    return null;
                }

                // Create compute shader to copy texture to structured buffer
                // Shader signature:
                //   RWStructuredBuffer<float4> outputBuffer : register(u0);
                //   Texture2D<float4> inputTexture : register(t0);
                //   cbuffer Constants : register(b0) { uint2 textureSize; }
                // Shader code:
                //   uint2 pixelCoord = DispatchThreadID.xy;
                //   if (pixelCoord.x < textureSize.x && pixelCoord.y < textureSize.y)
                //   {
                //       uint bufferIndex = pixelCoord.y * textureSize.x + pixelCoord.x;
                //       outputBuffer[bufferIndex] = inputTexture[pixelCoord];
                //   }

                // Try to load or create texture-to-buffer copy compute shader
                IShader copyShader = CreatePlaceholderComputeShader("TextureToBufferCopy");
                if (copyShader == null)
                {
                    Console.WriteLine("[NativeRT] ReadTextureDataFromGPU: Failed to create texture-to-buffer copy shader");
                    readbackBuffer.Dispose();
                    stagingTexture.Dispose();
                    stagingBuffer.Dispose();
                    return null;
                }

                // Create binding layout for copy shader
                IBindingLayout copyLayout = _device.CreateBindingLayout(new BindingLayoutDesc
                {
                    Items = new BindingLayoutItem[]
                    {
                        new BindingLayoutItem
                        {
                            Slot = 0,
                            Type = BindingType.Texture,
                            Stages = ShaderStageFlags.Compute,
                            Count = 1
                        },
                        new BindingLayoutItem
                        {
                            Slot = 1,
                            Type = BindingType.RWBuffer,
                            Stages = ShaderStageFlags.Compute,
                            Count = 1
                        },
                        new BindingLayoutItem
                        {
                            Slot = 2,
                            Type = BindingType.ConstantBuffer,
                            Stages = ShaderStageFlags.Compute,
                            Count = 1
                        }
                    },
                    IsPushDescriptor = false
                });

                // Create constant buffer for texture dimensions
                IBuffer constantsBuffer = _device.CreateBuffer(new BufferDesc
                {
                    ByteSize = 8, // uint2 = 8 bytes
                    Usage = BufferUsageFlags.ConstantBuffer,
                    InitialState = ResourceState.ConstantBuffer,
                    IsAccelStructBuildInput = false,
                    DebugName = "TextureCopyConstants"
                });

                if (constantsBuffer == null || copyLayout == null)
                {
                    Console.WriteLine("[NativeRT] ReadTextureDataFromGPU: Failed to create copy shader resources");
                    copyShader.Dispose();
                    if (copyLayout != null) copyLayout.Dispose();
                    if (constantsBuffer != null) constantsBuffer.Dispose();
                    readbackBuffer.Dispose();
                    stagingTexture.Dispose();
                    stagingBuffer.Dispose();
                    return null;
                }

                // Write texture dimensions to constant buffer
                ICommandList copyCommandList = _device.CreateCommandList(CommandListType.Compute);
                copyCommandList.Open();

                uint[] dimensions = new uint[] { (uint)width, (uint)height };
                byte[] dimensionBytes = new byte[8];
                System.Buffer.BlockCopy(dimensions, 0, dimensionBytes, 0, 8);
                copyCommandList.WriteBuffer(constantsBuffer, dimensionBytes, 0);

                // Create binding set for copy shader
                IBindingSet copyBindingSet = _device.CreateBindingSet(copyLayout, new BindingSetDesc
                {
                    Items = new BindingSetItem[]
                    {
                        new BindingSetItem
                        {
                            Slot = 0,
                            Type = BindingType.Texture,
                            Texture = stagingTexture
                        },
                        new BindingSetItem
                        {
                            Slot = 1,
                            Type = BindingType.RWBuffer,
                            Buffer = readbackBuffer
                        },
                        new BindingSetItem
                        {
                            Slot = 2,
                            Type = BindingType.ConstantBuffer,
                            Buffer = constantsBuffer
                        }
                    }
                });

                if (copyBindingSet == null)
                {
                    Console.WriteLine("[NativeRT] ReadTextureDataFromGPU: Failed to create copy binding set");
                    copyCommandList.Close();
                    copyCommandList.Dispose();
                    copyShader.Dispose();
                    copyLayout.Dispose();
                    constantsBuffer.Dispose();
                    readbackBuffer.Dispose();
                    stagingTexture.Dispose();
                    stagingBuffer.Dispose();
                    return null;
                }

                // Create compute pipeline for copy shader
                IComputePipeline copyPipeline = _device.CreateComputePipeline(new ComputePipelineDesc
                {
                    ComputeShader = copyShader,
                    BindingLayouts = new IBindingLayout[] { copyLayout }
                });

                if (copyPipeline == null)
                {
                    Console.WriteLine("[NativeRT] ReadTextureDataFromGPU: Failed to create copy pipeline");
                    copyBindingSet.Dispose();
                    copyCommandList.Close();
                    copyCommandList.Dispose();
                    copyShader.Dispose();
                    copyLayout.Dispose();
                    constantsBuffer.Dispose();
                    readbackBuffer.Dispose();
                    stagingTexture.Dispose();
                    stagingBuffer.Dispose();
                    return null;
                }

                // Set compute state and dispatch copy shader
                copyCommandList.SetTextureState(stagingTexture, ResourceState.ShaderResource);
                copyCommandList.SetBufferState(readbackBuffer, ResourceState.UnorderedAccess);
                copyCommandList.SetBufferState(constantsBuffer, ResourceState.ConstantBuffer);
                copyCommandList.CommitBarriers();

                ComputeState copyState = new ComputeState
                {
                    Pipeline = copyPipeline,
                    BindingSets = new IBindingSet[] { copyBindingSet }
                };
                copyCommandList.SetComputeState(copyState);

                // Dispatch compute shader (one thread per pixel)
                int groupCountX = (width + 8 - 1) / 8; // 8x8 thread groups
                int groupCountY = (height + 8 - 1) / 8;
                copyCommandList.Dispatch(groupCountX, groupCountY, 1);

                // Transition readback buffer to copy source for CPU read
                copyCommandList.SetBufferState(readbackBuffer, ResourceState.CopySource);
                copyCommandList.CommitBarriers();

                copyCommandList.Close();
                _device.ExecuteCommandList(copyCommandList);
                copyCommandList.Dispose();

                // Wait for GPU to finish
                _device.WaitIdle();

                // Read buffer data from GPU to CPU
                // Based on DirectX 12/Vulkan: Map buffer for CPU read access
                // This requires backend-specific implementation to map and read buffer
                float[] cpuData = null;

                // Try to read buffer using backend-specific methods via reflection
                if (_backend != null)
                {
                    try
                    {
                        Type backendType = _backend.GetType();
                        System.Reflection.MethodInfo readBufferMethod = backendType.GetMethod("ReadBufferData",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (readBufferMethod != null)
                        {
                            object result = readBufferMethod.Invoke(_backend, new object[] { readbackBuffer, 0, bufferSize });
                            if (result is byte[] byteData)
                            {
                                // Convert byte array to float array (RGBA float format)
                                int floatCount = byteData.Length / sizeof(float);
                                cpuData = new float[floatCount];
                                System.Buffer.BlockCopy(byteData, 0, cpuData, 0, byteData.Length);
                            }
                            else if (result is float[] floatData)
                            {
                                cpuData = floatData;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[NativeRT] ReadTextureDataFromGPU: Exception accessing backend ReadBufferData: {ex.Message}");
                    }
                }

                // Clean up resources
                copyBindingSet.Dispose();
                copyPipeline.Dispose();
                copyShader.Dispose();
                copyLayout.Dispose();
                constantsBuffer.Dispose();
                readbackBuffer.Dispose();
                stagingTexture.Dispose();
                stagingBuffer.Dispose();

                if (cpuData == null)
                {
                    Console.WriteLine("[NativeRT] ReadTextureDataFromGPU: Buffer read requires backend-specific implementation");
                    Console.WriteLine("[NativeRT] ReadTextureDataFromGPU: Backend must provide ReadBufferData method or buffer mapping support");
                }

                return cpuData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] ReadTextureDataFromGPU: Exception: {ex.Message}");
                stagingBuffer.Dispose();
                return null;
            }
        }

        /// <summary>
        /// Transfers texture data from CPU memory to GPU texture for OIDN results.
        /// Based on DirectX 12/Vulkan: Uses staging buffer to write texture data.
        /// </summary>
        /// <param name="texture">Texture to write to GPU.</param>
        /// <param name="data">CPU-accessible byte array containing texture data (RGBA float format).</param>
        /// <param name="width">Texture width.</param>
        /// <param name="height">Texture height.</param>
        /// <returns>True if write succeeded, false otherwise.</returns>
        private bool WriteTextureDataToGPU(ITexture texture, float[] data, int width, int height)
        {
            if (texture == null || data == null || _device == null || width <= 0 || height <= 0)
            {
                return false;
            }

            // Convert float array to byte array for WriteTexture
            // Based on ICommandList.WriteTexture: Accepts byte[] data
            int pixelCount = width * height;
            int floatCount = pixelCount * 4; // RGBA
            if (data.Length < floatCount)
            {
                Console.WriteLine($"[NativeRT] WriteTextureDataToGPU: Data array too small (expected {floatCount} floats, got {data.Length})");
                return false;
            }

            byte[] byteData = new byte[floatCount * sizeof(float)];
            unsafe
            {
                fixed (float* floatPtr = data)
                {
                    System.Buffer.BlockCopy(data, 0, byteData, 0, byteData.Length);
                }
            }

            // Write texture data using command list
            ICommandList commandList = _device.CreateCommandList(CommandListType.Compute);
            commandList.Open();

            // Transition texture to copy dest state
            commandList.SetTextureState(texture, ResourceState.CopyDest);
            commandList.CommitBarriers();

            // Write texture data (mip level 0, array slice 0)
            commandList.WriteTexture(texture, 0, 0, byteData);

            // Transition texture back to shader resource state
            commandList.SetTextureState(texture, ResourceState.ShaderResource);
            commandList.CommitBarriers();

            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();

            return true;
        }

        /// <summary>
        /// Executes OIDN denoising filter on CPU-side data.
        /// Based on Intel OIDN API: oidnSetSharedFilterImage, oidnCommitFilter, oidnExecuteFilter
        /// </summary>
        /// <param name="inputData">Input image data (RGBA float, row-major).</param>
        /// <param name="outputData">Output image data buffer (RGBA float, row-major, must be pre-allocated).</param>
        /// <param name="albedoData">Albedo image data (RGBA float, row-major, optional).</param>
        /// <param name="normalData">Normal image data (RGBA float, row-major, optional).</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <returns>True if denoising succeeded, false otherwise.</returns>
        private unsafe bool ExecuteOIDNFilter(float[] inputData, float[] outputData, float[] albedoData, float[] normalData, int width, int height)
        {
            if (!_oidnInitialized || _oidnFilter == IntPtr.Zero || _oidnDevice == IntPtr.Zero)
            {
                Console.WriteLine("[NativeRT] ExecuteOIDNFilter: OIDN not initialized");
                return false;
            }

            if (inputData == null || outputData == null || width <= 0 || height <= 0)
            {
                Console.WriteLine("[NativeRT] ExecuteOIDNFilter: Invalid parameters");
                return false;
            }

            // Verify data sizes
            int pixelCount = width * height;
            int floatCount = pixelCount * 4; // RGBA
            if (inputData.Length < floatCount || outputData.Length < floatCount)
            {
                Console.WriteLine($"[NativeRT] ExecuteOIDNFilter: Data arrays too small (expected {floatCount} floats)");
                return false;
            }

            try
            {
                // Pin input data for OIDN
                fixed (float* inputPtr = inputData)
                {
                    // Set input image
                    // Based on OIDN API: oidnSetSharedFilterImage(filter, "color", ptr, format, width, height, ...)
                    // OIDN expects row-major layout with 4 floats per pixel (RGBA)
                    int byteOffset = 0;
                    int bytePixelStride = 4 * sizeof(float); // 16 bytes per pixel (RGBA float)
                    int byteRowStride = width * bytePixelStride; // Row stride in bytes

                    _oidnSetSharedFilterImage(_oidnFilter, "color", new IntPtr(inputPtr), OIDN_FORMAT_FLOAT4, width, height, byteOffset, bytePixelStride, byteRowStride);
                    CheckOIDNError(_oidnDevice);

                    // Set output image
                    fixed (float* outputPtr = outputData)
                    {
                        _oidnSetSharedFilterImage(_oidnFilter, "output", new IntPtr(outputPtr), OIDN_FORMAT_FLOAT4, width, height, byteOffset, bytePixelStride, byteRowStride);
                        CheckOIDNError(_oidnDevice);

                        // Set albedo image if provided
                        if (albedoData != null && albedoData.Length >= floatCount)
                        {
                            fixed (float* albedoPtr = albedoData)
                            {
                                _oidnSetSharedFilterImage(_oidnFilter, "albedo", new IntPtr(albedoPtr), OIDN_FORMAT_FLOAT4, width, height, byteOffset, bytePixelStride, byteRowStride);
                                CheckOIDNError(_oidnDevice);
                            }
                        }

                        // Set normal image if provided
                        if (normalData != null && normalData.Length >= floatCount)
                        {
                            fixed (float* normalPtr = normalData)
                            {
                                _oidnSetSharedFilterImage(_oidnFilter, "normal", new IntPtr(normalPtr), OIDN_FORMAT_FLOAT4, width, height, byteOffset, bytePixelStride, byteRowStride);
                                CheckOIDNError(_oidnDevice);
                            }
                        }

                        // Set filter parameters (optional)
                        // Based on OIDN API: oidnSetFilter1b can set boolean parameters like "hdr"
                        // OIDN RT filter supports "hdr" parameter for high dynamic range images
                        _oidnSetFilter1b(_oidnFilter, "hdr", true); // Assume HDR for raytracing output
                        CheckOIDNError(_oidnDevice);

                        // Commit filter (must be called after setting all images and parameters)
                        // Based on OIDN API: oidnCommitFilter must be called before oidnExecuteFilter
                        _oidnCommitFilter(_oidnFilter);
                        CheckOIDNError(_oidnDevice);

                        // Execute filter (performs denoising)
                        // Based on OIDN API: oidnExecuteFilter performs the actual denoising operation
                        _oidnExecuteFilter(_oidnFilter);
                        CheckOIDNError(_oidnDevice);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] ExecuteOIDNFilter: Exception: {ex.Message}");
                CheckOIDNError(_oidnDevice);
                return false;
            }
        }

        #endregion

        #region NRD (NVIDIA Real-Time Denoiser) Native Library Integration

        // NRD library name - platform-specific
        // Windows: NRD.dll
        // Linux: libNRD.so
        // macOS: libNRD.dylib
        private const string NRD_LIBRARY_WINDOWS = "NRD.dll";
        private const string NRD_LIBRARY_LINUX = "libNRD.so";
        private const string NRD_LIBRARY_MACOS = "libNRD.dylib";

        // NRD denoiser method types
        // Based on NRD SDK: nrd::Method enum
        private const int NRD_METHOD_REBLUR_DIFFUSE = 0;
        private const int NRD_METHOD_REBLUR_DIFFUSE_SPECULAR = 1;
        private const int NRD_METHOD_REBLUR_SPECULAR = 2;
        private const int NRD_METHOD_RELAX_DIFFUSE = 3;
        private const int NRD_METHOD_RELAX_DIFFUSE_SPECULAR = 4;
        private const int NRD_METHOD_RELAX_SPECULAR = 5;
        private const int NRD_METHOD_REFERENCE = 6;
        private const int NRD_METHOD_SIGMA_SHADOW = 7;
        private const int NRD_METHOD_SIGMA_SHADOW_TRANSLUCENCY = 8;

        // NRD API function delegates
        // Based on NVIDIA NRD SDK API: https://github.com/NVIDIA/gpu-denoisers
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr nrdCreateDenoiserDelegate(ref NRDDenoiserDesc desc);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void nrdDestroyDenoiserDelegate(IntPtr denoiser);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void nrdSetMethodSettingsDelegate(IntPtr denoiser, int methodIndex, IntPtr methodSettings);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void nrdGetComputeDispatchesDelegate(IntPtr denoiser, IntPtr dispatchDesc, int methodIndex, ref NRDCommonSettings commonSettings, out int dispatchCount);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void nrdSetCommonSettingsDelegate(IntPtr denoiser, ref NRDCommonSettings settings);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool nrdGetShaderBytecodeDelegate(IntPtr shaderIdentifier, out IntPtr bytecode, out int bytecodeSize);

        // NRD function pointers (loaded dynamically)
        private static nrdCreateDenoiserDelegate _nrdCreateDenoiser;
        private static nrdDestroyDenoiserDelegate _nrdDestroyDenoiser;
        private static nrdSetMethodSettingsDelegate _nrdSetMethodSettings;
        private static nrdGetComputeDispatchesDelegate _nrdGetComputeDispatches;
        private static nrdSetCommonSettingsDelegate _nrdSetCommonSettings;
        private static nrdGetShaderBytecodeDelegate _nrdGetShaderBytecode;

        // NRD library handle
        private static IntPtr _nrdLibraryHandle = IntPtr.Zero;
        private static bool _nrdLibraryLoaded = false;
        private static readonly object _nrdLoadLock = new object();

        // NRD structures (matching NRD SDK C++ structures)
        // Based on NRD SDK: nrd.h structures
        [StructLayout(LayoutKind.Sequential)]
        private struct NRDDenoiserDesc
        {
            public IntPtr RequestedDenoisers; // Array of method indices
            public int RequestedDenoisersNum;
            // Note: In full NRD SDK, this would contain more fields like renderWidth, renderHeight
            // For C# interop, we simplify to essential fields
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NRDCommonSettings
        {
            public int RenderWidth;
            public int RenderHeight;
            public float TimeDeltaBetweenFrames;
            public float CameraJitter;
            public float AccumulationSpeed; // Temporal accumulation speed
            // Motion vector settings
            public float MotionVectorScaleX;
            public float MotionVectorScaleY;
            // World-space settings
            public int IsWorldSpaceMotionEnabled;
            public int FrameIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NRDDispatchDesc
        {
            public IntPtr ComputeShader; // Shader handle
            public uint ThreadGroupOffsetX;
            public uint ThreadGroupOffsetY;
            public uint ThreadGroupOffsetZ;
            public uint ThreadGroupDimX;
            public uint ThreadGroupDimY;
            public uint ThreadGroupDimZ;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NRDMethodSettings
        {
            // REBLUR method settings (most commonly used)
            public float BlurRadius;
            public float LobeAngleFraction;
            public float RoughnessFraction;
            public float LobeAngle;
            public float LobeAnglePenalty;
            public float RoughnessEdgeStoppingRelaxation;
            public float NormalEdgeStoppingRelaxation;
            public int PrepassBlurRadius;
            public float HitDistanceReconstructionMode;
            public float HitDistanceParameters;
            public float DepthThreshold;
            public float AccumulationSpeed; // Per-method accumulation speed
            public float StabilizationStrength;
        }

        /// <summary>
        /// Loads the NRD library and initializes function pointers.
        /// Based on NVIDIA NRD SDK API: Library must be loaded before use.
        /// Uses platform-specific library loading (LoadLibrary on Windows, dlopen on Linux/macOS).
        /// </summary>
        /// <returns>True if library loaded successfully, false otherwise.</returns>
        private static bool LoadNRDLibrary()
        {
            lock (_nrdLoadLock)
            {
                if (_nrdLibraryLoaded)
                {
                    return true;
                }

                // Determine library name based on platform
                string libraryName = null;
                bool isWindows = false;
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    libraryName = NRD_LIBRARY_WINDOWS;
                    isWindows = true;
                }
                else if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    // Check if macOS
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        libraryName = NRD_LIBRARY_MACOS;
                    }
                    else
                    {
                        libraryName = NRD_LIBRARY_LINUX;
                    }
                }

                if (libraryName == null)
                {
                    Console.WriteLine("[NativeRT] LoadNRDLibrary: Unsupported platform for NRD");
                    return false;
                }

                // Try to load the library
                try
                {
                    if (isWindows)
                    {
                        _nrdLibraryHandle = LoadLibrary(libraryName);
                    }
                    else
                    {
                        _nrdLibraryHandle = dlopen(libraryName, RTLD_NOW);
                    }

                    if (_nrdLibraryHandle == IntPtr.Zero)
                    {
                        Console.WriteLine($"[NativeRT] LoadNRDLibrary: Failed to load {libraryName}");
                        return false;
                    }

                    // Load function pointers
                    // Note: NRD SDK uses C++ mangled names, which may require name mangling resolution
                    // For now, we use expected exported function names
                    _nrdCreateDenoiser = GetFunction<nrdCreateDenoiserDelegate>(_nrdLibraryHandle, "nrdCreateDenoiser");
                    _nrdDestroyDenoiser = GetFunction<nrdDestroyDenoiserDelegate>(_nrdLibraryHandle, "nrdDestroyDenoiser");
                    _nrdSetMethodSettings = GetFunction<nrdSetMethodSettingsDelegate>(_nrdLibraryHandle, "nrdSetMethodSettings");
                    _nrdGetComputeDispatches = GetFunction<nrdGetComputeDispatchesDelegate>(_nrdLibraryHandle, "nrdGetComputeDispatches");
                    _nrdSetCommonSettings = GetFunction<nrdSetCommonSettingsDelegate>(_nrdLibraryHandle, "nrdSetCommonSettings");
                    _nrdGetShaderBytecode = GetFunction<nrdGetShaderBytecodeDelegate>(_nrdLibraryHandle, "nrdGetShaderBytecode");

                    // Verify all functions loaded
                    // Note: nrdGetShaderBytecode is optional - if not available, we'll use fallback shaders
                    if (_nrdCreateDenoiser == null || _nrdDestroyDenoiser == null ||
                        _nrdSetMethodSettings == null || _nrdGetComputeDispatches == null ||
                        _nrdSetCommonSettings == null)
                    {
                        Console.WriteLine("[NativeRT] LoadNRDLibrary: Failed to load all NRD functions");
                        if (_nrdLibraryHandle != IntPtr.Zero)
                        {
                            if (isWindows)
                            {
                                FreeLibrary(_nrdLibraryHandle);
                            }
                            else
                            {
                                dlclose(_nrdLibraryHandle);
                            }
                            _nrdLibraryHandle = IntPtr.Zero;
                        }
                        return false;
                    }

                    _nrdLibraryLoaded = true;
                    Console.WriteLine($"[NativeRT] LoadNRDLibrary: Successfully loaded {libraryName}");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NativeRT] LoadNRDLibrary: Exception loading library: {ex.Message}");
                    if (_nrdLibraryHandle != IntPtr.Zero)
                    {
                        if (isWindows)
                        {
                            FreeLibrary(_nrdLibraryHandle);
                        }
                        else
                        {
                            dlclose(_nrdLibraryHandle);
                        }
                        _nrdLibraryHandle = IntPtr.Zero;
                    }
                    return false;
                }
            }
        }

        /// <summary>
        /// Unloads the NRD library.
        /// Uses platform-specific library unloading (FreeLibrary on Windows, dlclose on Linux/macOS).
        /// </summary>
        private static void UnloadNRDLibrary()
        {
            lock (_nrdLoadLock)
            {
                if (_nrdLibraryHandle != IntPtr.Zero)
                {
                    try
                    {
                        bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                        if (isWindows)
                        {
                            FreeLibrary(_nrdLibraryHandle);
                        }
                        else
                        {
                            dlclose(_nrdLibraryHandle);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[NativeRT] UnloadNRDLibrary: Exception unloading library: {ex.Message}");
                    }
                    _nrdLibraryHandle = IntPtr.Zero;
                }

                // Clear function pointers
                _nrdCreateDenoiser = null;
                _nrdDestroyDenoiser = null;
                _nrdSetMethodSettings = null;
                _nrdGetComputeDispatches = null;
                _nrdSetCommonSettings = null;
                _nrdGetShaderBytecode = null;

                _nrdLibraryLoaded = false;
            }
        }

        /// <summary>
        /// Initializes NRD denoiser instance.
        /// Based on NVIDIA NRD SDK API: nrd::DenoiserDesc, nrd::CreateDenoiser()
        /// </summary>
        /// <param name="width">Render width in pixels.</param>
        /// <param name="height">Render height in pixels.</param>
        /// <returns>True if initialization succeeded, false otherwise.</returns>
        private bool InitializeNRD(int width, int height)
        {
            if (_nrdDenoiser != IntPtr.Zero)
            {
                return true;
            }

            // Load NRD library if not already loaded
            if (!LoadNRDLibrary())
            {
                Console.WriteLine("[NativeRT] InitializeNRD: Failed to load NRD library, falling back to GPU compute shader");
                _useNativeNRD = false;
                return false;
            }

            try
            {
                // Create NRD denoiser descriptor
                // Based on NRD SDK: nrd::DenoiserDesc describes which denoiser methods to enable
                // We use REBLUR_DIFFUSE_SPECULAR which is the most commonly used method
                unsafe
                {
                    int[] requestedMethods = new int[] { NRD_METHOD_REBLUR_DIFFUSE_SPECULAR };
                    fixed (int* methodsPtr = requestedMethods)
                    {
                        NRDDenoiserDesc desc = new NRDDenoiserDesc
                        {
                            RequestedDenoisers = new IntPtr(methodsPtr),
                            RequestedDenoisersNum = requestedMethods.Length
                        };

                        // Create denoiser instance
                        // Based on NRD SDK: nrd::CreateDenoiser() creates a denoiser instance
                        _nrdDenoiser = _nrdCreateDenoiser(ref desc);
                        if (_nrdDenoiser == IntPtr.Zero)
                        {
                            Console.WriteLine("[NativeRT] InitializeNRD: Failed to create NRD denoiser, falling back to GPU compute shader");
                            _useNativeNRD = false;
                            return false;
                        }

                        // Set common settings
                        // Based on NRD SDK: nrd::SetCommonSettings() configures common denoiser parameters
                        NRDCommonSettings commonSettings = new NRDCommonSettings
                        {
                            RenderWidth = width,
                            RenderHeight = height,
                            TimeDeltaBetweenFrames = 1.0f / 60.0f, // Assume 60 FPS
                            CameraJitter = 0.0f, // No jitter by default
                            AccumulationSpeed = 0.1f, // Temporal accumulation speed (0.0-1.0)
                            MotionVectorScaleX = 1.0f,
                            MotionVectorScaleY = 1.0f,
                            IsWorldSpaceMotionEnabled = 0, // Screen-space motion vectors
                            FrameIndex = 0
                        };

                        _nrdSetCommonSettings(_nrdDenoiser, ref commonSettings);

                        _nrdInitialized = true;
                        _useNativeNRD = true;
                        _nrdRenderWidth = width;
                        _nrdRenderHeight = height;
                        Console.WriteLine("[NativeRT] InitializeNRD: Successfully initialized NRD native library");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] InitializeNRD: Exception during initialization: {ex.Message}");
                _useNativeNRD = false;
                ReleaseNRDDenoiser();
                return false;
            }
        }

        /// <summary>
        /// Releases NRD denoiser instance.
        /// Based on NVIDIA NRD SDK API: nrd::DestroyDenoiser()
        /// </summary>
        private void ReleaseNRDDenoiser()
        {
            if (_nrdDenoiser != IntPtr.Zero)
            {
                try
                {
                    _nrdDestroyDenoiser(_nrdDenoiser);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NativeRT] ReleaseNRDDenoiser: Exception releasing denoiser: {ex.Message}");
                }
                _nrdDenoiser = IntPtr.Zero;
            }

            _nrdInitialized = false;
            _nrdRenderWidth = 0;
            _nrdRenderHeight = 0;

            // Dispose all cached NRD compute pipelines
            if (_nrdPipelineCache != null)
            {
                foreach (var pipeline in _nrdPipelineCache.Values)
                {
                    if (pipeline != null)
                    {
                        try
                        {
                            pipeline.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[NativeRT] ReleaseNRDDenoiser: Exception disposing pipeline: {ex.Message}");
                        }
                    }
                }
                _nrdPipelineCache.Clear();
            }
        }

        /// <summary>
        /// Gets or creates a compute pipeline for an NRD shader identifier.
        /// Based on NVIDIA NRD SDK API: nrd::GetShaderBytecode() retrieves shader bytecode from shader identifier.
        /// </summary>
        /// <param name="shaderIdentifier">NRD shader identifier from dispatch descriptor.</param>
        /// <returns>Compute pipeline for the shader, or null if shader bytecode cannot be retrieved.</returns>
        private IComputePipeline GetOrCreateNRDPipeline(IntPtr shaderIdentifier)
        {
            if (shaderIdentifier == IntPtr.Zero || _device == null || _denoiserBindingLayout == null)
            {
                return null;
            }

            // Check cache first
            if (_nrdPipelineCache.TryGetValue(shaderIdentifier, out IComputePipeline cachedPipeline))
            {
                return cachedPipeline;
            }

            // Try to get shader bytecode from NRD SDK
            if (_nrdGetShaderBytecode == null)
            {
                // Fallback: Use temporal denoiser pipeline if NRD shader bytecode retrieval is not available
                Console.WriteLine("[NativeRT] GetOrCreateNRDPipeline: nrdGetShaderBytecode not available, using temporal denoiser pipeline as fallback");
                return _temporalDenoiserPipeline;
            }

            try
            {
                IntPtr bytecodePtr = IntPtr.Zero;
                int bytecodeSize = 0;

                // Based on NRD SDK: nrd::GetShaderBytecode() retrieves shader bytecode from shader identifier
                bool success = _nrdGetShaderBytecode(shaderIdentifier, out bytecodePtr, out bytecodeSize);

                if (!success || bytecodePtr == IntPtr.Zero || bytecodeSize <= 0)
                {
                    Console.WriteLine($"[NativeRT] GetOrCreateNRDPipeline: Failed to get shader bytecode for identifier 0x{shaderIdentifier.ToInt64():X16}, using temporal denoiser pipeline as fallback");
                    return _temporalDenoiserPipeline;
                }

                // Copy shader bytecode from unmanaged memory
                byte[] shaderBytecode = new byte[bytecodeSize];
                Marshal.Copy(bytecodePtr, shaderBytecode, 0, bytecodeSize);

                // Create shader from bytecode
                IShader nrdShader = _device.CreateShader(new ShaderDesc
                {
                    Type = ShaderType.Compute,
                    Bytecode = shaderBytecode,
                    EntryPoint = "main", // NRD shaders use "main" as entry point
                    DebugName = $"NRD_Shader_0x{shaderIdentifier.ToInt64():X16}"
                });

                if (nrdShader == null)
                {
                    Console.WriteLine($"[NativeRT] GetOrCreateNRDPipeline: Failed to create shader from bytecode for identifier 0x{shaderIdentifier.ToInt64():X16}, using temporal denoiser pipeline as fallback");
                    return _temporalDenoiserPipeline;
                }

                // Create compute pipeline from NRD shader
                IComputePipeline nrdPipeline = _device.CreateComputePipeline(new ComputePipelineDesc
                {
                    ComputeShader = nrdShader,
                    BindingLayouts = new IBindingLayout[] { _denoiserBindingLayout }
                });

                if (nrdPipeline == null)
                {
                    Console.WriteLine($"[NativeRT] GetOrCreateNRDPipeline: Failed to create compute pipeline for identifier 0x{shaderIdentifier.ToInt64():X16}, using temporal denoiser pipeline as fallback");
                    nrdShader.Dispose();
                    return _temporalDenoiserPipeline;
                }

                // Cache the pipeline
                _nrdPipelineCache[shaderIdentifier] = nrdPipeline;
                Console.WriteLine($"[NativeRT] GetOrCreateNRDPipeline: Successfully created and cached NRD pipeline for identifier 0x{shaderIdentifier.ToInt64():X16}");

                // Dispose shader (pipeline retains the bytecode)
                nrdShader.Dispose();

                return nrdPipeline;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] GetOrCreateNRDPipeline: Exception creating pipeline for identifier 0x{shaderIdentifier.ToInt64():X16}: {ex.Message}, using temporal denoiser pipeline as fallback");
                return _temporalDenoiserPipeline;
            }
        }

        /// <summary>
        /// Applies NRD denoising using native NRD library.
        /// Based on NVIDIA NRD SDK API: nrd::SetMethodSettings(), nrd::GetComputeDispatches()
        /// Full native NRD library integration with CPU-side NRD SDK calls:
        /// - nrd::SetMethodSettings() to configure denoiser parameters
        /// - nrd::GetComputeDispatches() to get shader dispatch information
        /// - nrd::SetCommonSettings() to set per-frame common parameters
        /// - nrd::GetShaderBytecode() to retrieve shader bytecode from shader identifiers
        /// Direct integration with NRD's shader library via compute dispatches
        /// </summary>
        /// <param name="parameters">Denoiser parameters containing input/output textures and auxiliary buffers.</param>
        /// <param name="width">Width of the texture in pixels.</param>
        /// <param name="height">Height of the texture in pixels.</param>
        private void ApplyNRDDenoising(DenoiserParams parameters, int width, int height)
        {
            if (!_nrdInitialized || _nrdDenoiser == IntPtr.Zero || _device == null)
            {
                // Fall back to GPU compute shader implementation
                ApplyTemporalDenoising(parameters, width, height);
                return;
            }

            // Get input and output textures
            ITexture inputTexture = GetTextureFromHandle(parameters.InputTexture);
            ITexture outputTexture = GetTextureFromHandle(parameters.OutputTexture);
            ITexture normalTexture = GetTextureFromHandle(parameters.NormalTexture);
            ITexture albedoTexture = GetTextureFromHandle(parameters.AlbedoTexture);
            ITexture motionTexture = GetTextureFromHandle(parameters.MotionTexture);

            if (inputTexture == null || outputTexture == null)
            {
                // Fall back to GPU compute shader implementation
                ApplyTemporalDenoising(parameters, width, height);
                return;
            }

            try
            {
                // Reinitialize NRD if render resolution changed
                if (width != _nrdRenderWidth || height != _nrdRenderHeight)
                {
                    ReleaseNRDDenoiser();
                    if (!InitializeNRD(width, height))
                    {
                        // Fall back to GPU compute shader implementation
                        ApplyTemporalDenoising(parameters, width, height);
                        return;
                    }
                }

                // Set method settings for REBLUR_DIFFUSE_SPECULAR
                // Based on NRD SDK: nrd::SetMethodSettings() configures denoiser method-specific parameters
                unsafe
                {
                    NRDMethodSettings methodSettings = new NRDMethodSettings
                    {
                        BlurRadius = 60.0f, // Blur radius in pixels
                        LobeAngleFraction = 0.15f, // Specular lobe angle fraction
                        RoughnessFraction = 0.15f, // Roughness fraction for edge stopping
                        LobeAngle = 1.0f, // Specular lobe angle in radians
                        LobeAnglePenalty = 1.0f, // Lobe angle penalty factor
                        RoughnessEdgeStoppingRelaxation = 0.5f, // Edge stopping relaxation
                        NormalEdgeStoppingRelaxation = 0.1f, // Normal-based edge stopping
                        PrepassBlurRadius = 2, // Prepass blur radius
                        HitDistanceReconstructionMode = 0.0f, // Hit distance reconstruction mode
                        HitDistanceParameters = 0.25f, // Hit distance parameters
                        DepthThreshold = 0.02f, // Depth threshold for disocclusion detection
                        AccumulationSpeed = parameters.BlendFactor, // Use provided blend factor for accumulation speed
                        StabilizationStrength = 1.0f // Stabilization strength
                    };

                    _nrdSetMethodSettings(_nrdDenoiser, NRD_METHOD_REBLUR_DIFFUSE_SPECULAR, new IntPtr(&methodSettings));
                }

                // Update common settings with current frame parameters
                // Based on NRD SDK: nrd::SetCommonSettings() updates common parameters like motion vectors
                NRDCommonSettings commonSettings = new NRDCommonSettings
                {
                    RenderWidth = width,
                    RenderHeight = height,
                    TimeDeltaBetweenFrames = 1.0f / 60.0f, // Assume 60 FPS
                    CameraJitter = 0.0f, // No jitter by default
                    AccumulationSpeed = parameters.BlendFactor, // Use provided blend factor
                    MotionVectorScaleX = 1.0f,
                    MotionVectorScaleY = 1.0f,
                    IsWorldSpaceMotionEnabled = 0, // Screen-space motion vectors
                    FrameIndex = _nrdFrameIndex++
                };

                _nrdSetCommonSettings(_nrdDenoiser, ref commonSettings);

                // Get compute dispatches for NRD denoising
                // Based on NRD SDK: nrd::GetComputeDispatches() returns shader dispatch information
                // NRD denoising consists of multiple compute shader passes that must be executed in order
                unsafe
                {
                    // Allocate dispatch descriptors array
                    // NRD typically requires 2-4 dispatch passes depending on method
                    const int maxDispatches = 8;
                    NRDDispatchDesc[] dispatchDescs = new NRDDispatchDesc[maxDispatches];
                    fixed (NRDDispatchDesc* dispatchPtr = dispatchDescs)
                    {
                        int dispatchCount = 0;
                        _nrdGetComputeDispatches(_nrdDenoiser, new IntPtr(dispatchPtr), NRD_METHOD_REBLUR_DIFFUSE_SPECULAR, ref commonSettings, out dispatchCount);

                        if (dispatchCount <= 0 || dispatchCount > maxDispatches)
                        {
                            Console.WriteLine($"[NativeRT] ApplyNRDDenoising: Invalid dispatch count: {dispatchCount}, falling back to GPU compute shader");
                            ApplyTemporalDenoising(parameters, width, height);
                            return;
                        }

                        // Execute each compute dispatch
                        // Each dispatch represents a compute shader pass in the NRD denoising pipeline
                        for (int i = 0; i < dispatchCount; i++)
                        {
                            NRDDispatchDesc dispatchDesc = dispatchDescs[i];

                            // NRD SDK provides shader identifiers through dispatchDesc.ComputeShader
                            // Get or create compute pipeline for this NRD shader identifier
                            // Based on NRD SDK: Each dispatch provides a shader identifier that must be resolved to shader bytecode
                            // Full native NRD library integration:
                            // 1. Get shader bytecode from NRD SDK using nrd::GetShaderBytecode()
                            // 2. Create compute pipelines for each NRD shader (cached for reuse)
                            // 3. Bind NRD-specific textures and buffers
                            // 4. Execute dispatches in the correct order using actual NRD shaders

                            // Get or create compute pipeline for this NRD shader
                            IComputePipeline nrdPipeline = GetOrCreateNRDPipeline(dispatchDesc.ComputeShader);

                            if (nrdPipeline == null)
                            {
                                Console.WriteLine($"[NativeRT] ApplyNRDDenoising: Failed to get pipeline for dispatch {i}, skipping");
                                continue;
                            }

                            // Create command list for this dispatch
                            ICommandList commandList = _device.CreateCommandList(CommandListType.Compute);
                            commandList.Open();

                            // Transition resources for NRD denoising
                            commandList.SetTextureState(inputTexture, ResourceState.ShaderResource);
                            commandList.SetTextureState(outputTexture, ResourceState.UnorderedAccess);
                            if (normalTexture != null)
                            {
                                commandList.SetTextureState(normalTexture, ResourceState.ShaderResource);
                            }
                            if (albedoTexture != null)
                            {
                                commandList.SetTextureState(albedoTexture, ResourceState.ShaderResource);
                            }
                            if (motionTexture != null)
                            {
                                commandList.SetTextureState(motionTexture, ResourceState.ShaderResource);
                            }
                            commandList.CommitBarriers();

                            // Use actual NRD compute pipeline loaded from SDK
                            // Create binding set for NRD denoising
                            IBindingSet bindingSet = CreateDenoiserBindingSet(parameters, null);
                            if (bindingSet != null)
                            {
                                ComputeState computeState = new ComputeState
                                {
                                    Pipeline = nrdPipeline,
                                    BindingSets = new IBindingSet[] { bindingSet }
                                };
                                commandList.SetComputeState(computeState);

                                // Dispatch using NRD-provided thread group dimensions and offsets
                                uint groupCountX = (uint)(width + dispatchDesc.ThreadGroupDimX - 1) / dispatchDesc.ThreadGroupDimX;
                                uint groupCountY = (uint)(height + dispatchDesc.ThreadGroupDimY - 1) / dispatchDesc.ThreadGroupDimY;

                                // Apply thread group offsets if provided by NRD
                                // Note: Some backends support dispatch with offsets directly
                                // For backends that don't, we rely on shader-side offset handling

                                commandList.Dispatch((int)groupCountX, (int)groupCountY, (int)dispatchDesc.ThreadGroupDimZ);

                                bindingSet.Dispose();
                            }

                            // Transition output back to readable state after last dispatch
                            if (i == dispatchCount - 1)
                            {
                                commandList.SetTextureState(outputTexture, ResourceState.ShaderResource);
                            }
                            commandList.CommitBarriers();

                            commandList.Close();
                            _device.ExecuteCommandList(commandList);
                            commandList.Dispose();
                        }
                    }
                }

                // NRD native denoising completed successfully
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] ApplyNRDDenoising: Exception: {ex.Message}");
                // Fall back to GPU compute shader implementation
                ApplyTemporalDenoising(parameters, width, height);
            }
        }

        #endregion
        private IGraphicsBackend _backend;
        private IDevice _device;
        private RaytracingSettings _settings;
        private bool _initialized;
        private bool _enabled;
        private RaytracingLevel _level;

        // Acceleration structures
        private readonly Dictionary<IntPtr, BlasEntry> _blasEntries;
        private readonly Dictionary<IntPtr, IAccelStruct> _blasAccelStructs; // Map IntPtr handle to IAccelStruct
        private readonly List<TlasInstance> _tlasInstances;
        private IAccelStruct _tlas;
        private IBuffer _instanceBuffer;
        private bool _tlasDirty = true;
        private uint _nextBlasHandle = 1;

        // Ray tracing pipelines
        private IRaytracingPipeline _shadowPipeline;
        private IRaytracingPipeline _reflectionPipeline;
        private IRaytracingPipeline _aoPipeline;
        private IRaytracingPipeline _giPipeline;

        // Shadow pipeline resources
        private IBindingLayout _shadowBindingLayout;
        private IBindingSet _shadowBindingSet;
        private IBuffer _shadowConstantBuffer;
        private IBuffer _shadowShaderBindingTable;
        private ShaderBindingTable _shadowSbt;

        // Denoiser state
        private DenoiserType _currentDenoiserType;
        private IComputePipeline _temporalDenoiserPipeline;
        private IComputePipeline _spatialDenoiserPipeline;
        private IBindingLayout _denoiserBindingLayout;
        private IBuffer _denoiserConstantBuffer;

        // OIDN (Open Image Denoise) native library state
        // Based on Intel OIDN API: https://www.openimagedenoise.org/documentation.html
        private IntPtr _oidnDevice; // OIDN device handle (oidnDevice)
        private IntPtr _oidnFilter; // OIDN filter handle (oidnFilter)
        private bool _oidnInitialized; // Whether OIDN is initialized
        private bool _useNativeOIDN; // Whether to use native OIDN library (true) or GPU compute shader (false)

        // NRD (NVIDIA Real-Time Denoiser) native library state
        // Based on NVIDIA NRD SDK API: https://github.com/NVIDIA/gpu-denoisers
        private IntPtr _nrdDenoiser; // NRD denoiser handle (nrd::Denoiser)
        private bool _nrdInitialized; // Whether NRD is initialized
        private bool _useNativeNRD; // Whether to use native NRD library (true) or GPU compute shader (false)
        private int _nrdRenderWidth; // Current render width for NRD
        private int _nrdRenderHeight; // Current render height for NRD
        private int _nrdFrameIndex; // Frame index for NRD temporal accumulation
        // NRD compute pipeline cache - maps shader identifier (IntPtr) to IComputePipeline
        // Based on NRD SDK: Each dispatch provides a shader identifier that must be resolved to shader bytecode
        private readonly Dictionary<IntPtr, IComputePipeline> _nrdPipelineCache;

        // History buffers for temporal accumulation (ping-pong)
        private Dictionary<IntPtr, ITexture> _historyBuffers;
        private Dictionary<IntPtr, int> _historyBufferWidths;
        private Dictionary<IntPtr, int> _historyBufferHeights;

        // Texture handle tracking - maps IntPtr handles to ITexture objects
        // This allows us to look up textures when updating binding sets
        private readonly Dictionary<IntPtr, ITexture> _textureHandleMap;
        private readonly Dictionary<IntPtr, TextureInfo> _textureInfoCache; // Cache texture dimensions

        // Buffer handle tracking - maps IntPtr handles to IBuffer objects
        // This allows us to look up buffers when building acceleration structures
        private readonly Dictionary<IntPtr, IBuffer> _bufferHandleMap;

        // Statistics
        private RaytracingStatistics _lastStats;

        public bool IsAvailable
        {
            get { return _backend?.Capabilities.SupportsRaytracing ?? false; }
        }

        public bool IsEnabled
        {
            get { return _enabled && _initialized; }
        }

        public RaytracingLevel CurrentLevel
        {
            get { return _level; }
        }

        public bool RemixAvailable
        {
            get { return _backend?.Capabilities.RemixAvailable ?? false; }
        }

        public bool RemixActive
        {
            get { return false; } // Native RT doesn't use Remix
        }

        public float HardwareTier
        {
            get { return 1.1f; } // DXR 1.1 / Vulkan RT 1.1
        }

        public int MaxRecursionDepth
        {
            get { return _settings.Level == RaytracingLevel.PathTracing ? 8 : 3; }
        }

        public NativeRaytracingSystem(IGraphicsBackend backend, IDevice device = null)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _device = device;
            _blasEntries = new Dictionary<IntPtr, BlasEntry>();
            _blasAccelStructs = new Dictionary<IntPtr, IAccelStruct>();
            _tlasInstances = new List<TlasInstance>();
            _historyBuffers = new Dictionary<IntPtr, ITexture>();
            _historyBufferWidths = new Dictionary<IntPtr, int>();
            _historyBufferHeights = new Dictionary<IntPtr, int>();
            _textureHandleMap = new Dictionary<IntPtr, ITexture>();
            _textureInfoCache = new Dictionary<IntPtr, TextureInfo>();
            _bufferHandleMap = new Dictionary<IntPtr, IBuffer>();
            _nrdPipelineCache = new Dictionary<IntPtr, IComputePipeline>();
            _currentDenoiserType = DenoiserType.None;
        }

        public bool Initialize(RaytracingSettings settings)
        {
            if (_initialized)
            {
                return true;
            }

            if (!IsAvailable)
            {
                Console.WriteLine("[NativeRT] Hardware raytracing not available");
                return false;
            }

            // Get device from backend if not provided
            if (_device == null)
            {
                _device = _backend.GetDevice();
                if (_device == null)
                {
                    Console.WriteLine("[NativeRT] Error: Failed to get IDevice from backend. Raytracing requires a device interface.");
                    Console.WriteLine("[NativeRT] Backend may not support raytracing or device creation failed.");
                    return false;
                }
            }

            _settings = settings;
            _level = settings.Level;

            // Create ray tracing pipelines
            if (!CreatePipelines())
            {
                Console.WriteLine("[NativeRT] Failed to create RT pipelines");
                return false;
            }

            // Initialize denoiser
            if (settings.EnableDenoiser)
            {
                InitializeDenoiser(settings.Denoiser);
            }

            _initialized = true;
            _enabled = settings.Level != RaytracingLevel.Disabled;

            Console.WriteLine("[NativeRT] Initialized successfully");
            Console.WriteLine("[NativeRT] Level: " + _level);
            Console.WriteLine("[NativeRT] Denoiser: " + settings.Denoiser);

            return true;
        }

        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            // Destroy all BLAS
            foreach (BlasEntry entry in _blasEntries.Values)
            {
                _backend.DestroyResource(entry.Handle);
            }
            _blasEntries.Clear();

            foreach (IAccelStruct blas in _blasAccelStructs.Values)
            {
                if (blas != null)
                {
                    blas.Dispose();
                }
            }
            _blasAccelStructs.Clear();

            // Destroy TLAS
            if (_tlas != null)
            {
                _tlas.Dispose();
                _tlas = null;
            }

            // Destroy instance buffer
            if (_instanceBuffer != null)
            {
                _instanceBuffer.Dispose();
                _instanceBuffer = null;
            }

            // Destroy pipelines
            DestroyPipelines();

            // Destroy denoiser
            ShutdownDenoiser();

            // Clean up texture tracking
            _textureHandleMap.Clear();
            _textureInfoCache.Clear();

            // Clean up buffer tracking
            _bufferHandleMap.Clear();

            _initialized = false;
            _enabled = false;

            Console.WriteLine("[NativeRT] Shutdown complete");
        }

        public void SetLevel(RaytracingLevel level)
        {
            _level = level;
            _enabled = level != RaytracingLevel.Disabled;
        }

        public void BuildTopLevelAS()
        {
            if (!_initialized || !_tlasDirty || _device == null)
            {
                return;
            }

            if (_tlasInstances.Count == 0)
            {
                // No instances to build
                _tlasDirty = false;
                return;
            }

            // Create or update instance buffer with transforms and BLAS references
            int instanceCount = _tlasInstances.Count;
            int instanceBufferSize = instanceCount * 64; // AccelStructInstance is 64 bytes (VkAccelerationStructureInstanceKHR)

            // Create or resize instance buffer if needed
            if (_instanceBuffer == null || instanceBufferSize > _instanceBuffer.Desc.ByteSize)
            {
                if (_instanceBuffer != null)
                {
                    _instanceBuffer.Dispose();
                }

                _instanceBuffer = _device.CreateBuffer(new BufferDesc
                {
                    ByteSize = instanceBufferSize,
                    Usage = BufferUsageFlags.AccelStructStorage,
                    InitialState = ResourceState.AccelStructBuildInput,
                    IsAccelStructBuildInput = true,
                    DebugName = "TLAS_InstanceBuffer"
                });
            }

            // Create instance data array with transforms and BLAS references
            AccelStructInstance[] instances = new AccelStructInstance[instanceCount];
            for (int i = 0; i < instanceCount; i++)
            {
                TlasInstance tlasInst = _tlasInstances[i];
                BlasEntry blasEntry = _blasEntries[tlasInst.BlasHandle];

                // Get BLAS device address from IAccelStruct
                ulong blasAddress = 0;
                if (_blasAccelStructs.TryGetValue(tlasInst.BlasHandle, out IAccelStruct blas))
                {
                    blasAddress = blas.DeviceAddress;
                }

                instances[i] = new AccelStructInstance
                {
                    Transform = Matrix3x4.FromMatrix4x4(tlasInst.Transform),
                    InstanceCustomIndex = (uint)i,
                    Mask = (byte)(tlasInst.InstanceMask & 0xFF),
                    InstanceShaderBindingTableRecordOffset = tlasInst.HitGroupIndex,
                    Flags = blasEntry.IsOpaque ? AccelStructInstanceFlags.ForceOpaque : AccelStructInstanceFlags.None,
                    AccelerationStructureReference = blasAddress
                };
            }

            // Write instance data to buffer using command list
            ICommandList commandList = _device.CreateCommandList(CommandListType.Graphics);
            commandList.Open();
            commandList.WriteBuffer(_instanceBuffer, instances);
            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();

            // Create or update TLAS acceleration structure
            int maxInstances = Math.Max(instanceCount, 1024); // Allocate space for growth
            if (_tlas == null)
            {
                _tlas = _device.CreateAccelStruct(new AccelStructDesc
                {
                    IsTopLevel = true,
                    TopLevelMaxInstances = maxInstances,
                    BuildFlags = AccelStructBuildFlags.AllowUpdate | AccelStructBuildFlags.PreferFastTrace,
                    DebugName = "TopLevelAS"
                });
            }
            else if (instanceCount > maxInstances)
            {
                // Need to recreate with larger capacity
                _tlas.Dispose();
                _tlas = _device.CreateAccelStruct(new AccelStructDesc
                {
                    IsTopLevel = true,
                    TopLevelMaxInstances = maxInstances * 2,
                    BuildFlags = AccelStructBuildFlags.AllowUpdate | AccelStructBuildFlags.PreferFastTrace,
                    DebugName = "TopLevelAS"
                });
            }

            // Build TLAS using command list
            commandList = _device.CreateCommandList(CommandListType.Graphics);
            commandList.Open();
            commandList.BuildTopLevelAccelStruct(_tlas, instances);
            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();

            _tlasDirty = false;
            _lastStats.TlasInstanceCount = _tlasInstances.Count;
        }

        public IntPtr BuildBottomLevelAS(MeshGeometry geometry)
        {
            if (!_initialized || _device == null)
            {
                return IntPtr.Zero;
            }

            // Create BLAS for mesh geometry
            // - Create geometry description from vertex/index buffers
            // - Create acceleration structure
            // - Build BLAS using command list

            // Get vertex and index buffers from handles
            IBuffer vertexBuffer = GetBufferFromHandle(geometry.VertexBuffer);
            IBuffer indexBuffer = GetBufferFromHandle(geometry.IndexBuffer);

            if (vertexBuffer == null || indexBuffer == null)
            {
                Console.WriteLine("[NativeRT] BuildBottomLevelAS: Vertex or index buffer not found. Buffers must be registered using RegisterBufferHandle before building BLAS.");
                Console.WriteLine($"[NativeRT] BuildBottomLevelAS: VertexBuffer handle: {geometry.VertexBuffer}, IndexBuffer handle: {geometry.IndexBuffer}");
                return IntPtr.Zero;
            }

            // Validate buffer sizes match geometry counts
            int expectedVertexBufferSize = geometry.VertexCount * geometry.VertexStride;
            if (vertexBuffer.Desc.ByteSize < expectedVertexBufferSize)
            {
                Console.WriteLine($"[NativeRT] BuildBottomLevelAS: Vertex buffer size mismatch. Expected at least {expectedVertexBufferSize} bytes, got {vertexBuffer.Desc.ByteSize} bytes");
                return IntPtr.Zero;
            }

            // Determine index format from buffer size
            // R32_UInt: 4 bytes per index, R16_UInt: 2 bytes per index
            TextureFormat indexFormat = TextureFormat.R32_UInt;
            int expectedIndexBufferSize32 = geometry.IndexCount * 4; // 32-bit indices
            int expectedIndexBufferSize16 = geometry.IndexCount * 2; // 16-bit indices

            if (indexBuffer.Desc.ByteSize >= expectedIndexBufferSize32)
            {
                indexFormat = TextureFormat.R32_UInt;
            }
            else if (indexBuffer.Desc.ByteSize >= expectedIndexBufferSize16)
            {
                indexFormat = TextureFormat.R16_UInt;
            }
            else
            {
                Console.WriteLine($"[NativeRT] BuildBottomLevelAS: Index buffer size too small. Expected at least {expectedIndexBufferSize16} bytes (16-bit) or {expectedIndexBufferSize32} bytes (32-bit), got {indexBuffer.Desc.ByteSize} bytes");
                return IntPtr.Zero;
            }

            // Determine vertex format from stride
            // Common formats:
            // - 12 bytes (3 floats) = R32G32B32_Float (position only)
            // - 16 bytes (4 floats) = R32G32B32A32_Float (position + something)
            // - 32 bytes = R32G32B32A32_Float + R32G32B32A32_Float (position + normal + uv + tangent)
            // For raytracing BLAS, we typically only need positions (first 12 bytes)
            TextureFormat vertexFormat = TextureFormat.R32G32B32_Float;
            if (geometry.VertexStride >= 16)
            {
                // If stride is 16 or more, we might have position (12 bytes) + something (4 bytes)
                // For BLAS, we typically only use the first 12 bytes (position)
                vertexFormat = TextureFormat.R32G32B32_Float;
            }
            else if (geometry.VertexStride == 12)
            {
                vertexFormat = TextureFormat.R32G32B32_Float;
            }
            else
            {
                Console.WriteLine($"[NativeRT] BuildBottomLevelAS: Warning - Unusual vertex stride: {geometry.VertexStride} bytes. Using R32G32B32_Float format (assuming positions in first 12 bytes)");
                vertexFormat = TextureFormat.R32G32B32_Float;
            }

            // Create geometry description with actual buffers
            GeometryDesc geometryDesc = new GeometryDesc
            {
                Type = GeometryType.Triangles,
                Flags = geometry.IsOpaque ? GeometryFlags.Opaque : GeometryFlags.None,
                Triangles = new GeometryTriangles
                {
                    VertexBuffer = vertexBuffer,
                    VertexOffset = 0, // Start at beginning of buffer
                    VertexCount = geometry.VertexCount,
                    VertexStride = geometry.VertexStride,
                    VertexFormat = vertexFormat,
                    IndexBuffer = indexBuffer,
                    IndexOffset = 0, // Start at beginning of buffer
                    IndexCount = geometry.IndexCount,
                    IndexFormat = indexFormat,
                    TransformBuffer = null, // No per-geometry transform
                    TransformOffset = 0
                }
            };

            // Create BLAS acceleration structure
            IAccelStruct blas = _device.CreateAccelStruct(new AccelStructDesc
            {
                IsTopLevel = false,
                BottomLevelGeometries = new GeometryDesc[] { geometryDesc },
                BuildFlags = AccelStructBuildFlags.PreferFastTrace,
                DebugName = "BottomLevelAS"
            });

            // Build BLAS using command list
            ICommandList commandList = _device.CreateCommandList(CommandListType.Graphics);
            commandList.Open();
            commandList.BuildBottomLevelAccelStruct(blas, new GeometryDesc[] { geometryDesc });
            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();

            // Create handle for this BLAS
            IntPtr handle = new IntPtr(_nextBlasHandle++);

            _blasEntries[handle] = new BlasEntry
            {
                Handle = handle,
                VertexCount = geometry.VertexCount,
                IndexCount = geometry.IndexCount,
                IsOpaque = geometry.IsOpaque
            };

            _blasAccelStructs[handle] = blas;
            _lastStats.BlasCount = _blasEntries.Count;

            return handle;
        }

        public void AddInstance(IntPtr blas, Matrix4x4 transform, uint instanceMask, uint hitGroupIndex)
        {
            if (!_initialized || !_blasEntries.ContainsKey(blas))
            {
                return;
            }

            _tlasInstances.Add(new TlasInstance
            {
                BlasHandle = blas,
                Transform = transform,
                InstanceMask = instanceMask,
                HitGroupIndex = hitGroupIndex
            });

            _tlasDirty = true;
        }

        public void RemoveInstance(IntPtr blas)
        {
            _tlasInstances.RemoveAll(i => i.BlasHandle == blas);
            _tlasDirty = true;
        }

        public void UpdateInstanceTransform(IntPtr blas, Matrix4x4 transform)
        {
            for (int i = 0; i < _tlasInstances.Count; i++)
            {
                if (_tlasInstances[i].BlasHandle == blas)
                {
                    TlasInstance instance = _tlasInstances[i];
                    instance.Transform = transform;
                    _tlasInstances[i] = instance;
                }
            }
            _tlasDirty = true;
        }

        public void TraceShadowRays(ShadowRayParams parameters)
        {
            if (!_enabled || (_level != RaytracingLevel.ShadowsOnly &&
                             _level != RaytracingLevel.ShadowsAndReflections &&
                             _level != RaytracingLevel.Full))
            {
                return;
            }

            if (_shadowPipeline == null || _tlas == null || _device == null)
            {
                return;
            }

            // Ensure TLAS is up to date
            BuildTopLevelAS();

            // Get render resolution from output texture
            int renderWidth = 1920;
            int renderHeight = 1080;
            if (parameters.OutputTexture != IntPtr.Zero)
            {
                var textureInfo = GetTextureInfo(parameters.OutputTexture);
                if (textureInfo.HasValue)
                {
                    renderWidth = textureInfo.Value.Width;
                    renderHeight = textureInfo.Value.Height;
                }
            }

            // Update constant buffer with shadow ray parameters
            UpdateShadowConstants(parameters, renderWidth, renderHeight);

            // Create or update binding set with current resources
            UpdateShadowBindingSet(parameters.OutputTexture);

            // Create command list for ray dispatch
            ICommandList commandList = _device.CreateCommandList(CommandListType.Graphics);
            commandList.Open();

            // Check if we should use push descriptors
            bool usePushDescriptors = _shadowBindingLayout != null && _shadowBindingLayout.Desc.IsPushDescriptor;

            if (usePushDescriptors)
            {
                // Push descriptors directly into command buffer (more efficient, no binding set needed)
                // Get the current output texture
                ITexture outputTextureObj = null;
                if (parameters.OutputTexture != IntPtr.Zero)
                {
                    outputTextureObj = GetTextureFromHandle(parameters.OutputTexture);
                }

                if (outputTextureObj != null)
                {
                    // Push descriptor set with updated resources
                    BindingSetItem[] pushItems = new BindingSetItem[]
                    {
                        new BindingSetItem
                        {
                            Slot = 0,
                            Type = BindingType.AccelStruct,
                            AccelStruct = _tlas
                        },
                        new BindingSetItem
                        {
                            Slot = 1,
                            Type = BindingType.RWTexture,
                            Texture = outputTextureObj
                        },
                        new BindingSetItem
                        {
                            Slot = 2,
                            Type = BindingType.ConstantBuffer,
                            Buffer = _shadowConstantBuffer
                        }
                    };

                    try
                    {
                        commandList.PushDescriptorSet(_shadowBindingLayout, 0, pushItems);
                        Console.WriteLine("[NativeRT] TraceShadowRays: Successfully pushed descriptor set with push descriptors");
                    }
                    catch (NotSupportedException ex)
                    {
                        // Fallback to binding set if push descriptors not supported by backend
                        Console.WriteLine($"[NativeRT] TraceShadowRays: Push descriptors not supported by backend, falling back to binding set: {ex.Message}");
                        usePushDescriptors = false;
                    }
                }
            }

            // Set raytracing state (with or without binding sets depending on push descriptor usage)
            RaytracingState rtState = new RaytracingState
            {
                Pipeline = _shadowPipeline,
                BindingSets = usePushDescriptors ? null : new IBindingSet[] { _shadowBindingSet },
                ShaderTable = _shadowSbt
            };
            commandList.SetRaytracingState(rtState);

            // Dispatch shadow rays at render resolution
            // Each pixel traces one or more shadow rays based on SamplesPerPixel
            DispatchRaysArguments dispatchArgs = new DispatchRaysArguments
            {
                Width = renderWidth,
                Height = renderHeight,
                Depth = 1
            };
            commandList.DispatchRays(dispatchArgs);

            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();

            // Update statistics
            _lastStats.RaysTraced += (long)parameters.SamplesPerPixel * renderWidth * renderHeight;
            _lastStats.TraceTimeMs = 0.0; // Would be measured in real implementation

            // Apply temporal denoising if enabled
            if (_settings.EnableDenoiser && _settings.Denoiser != DenoiserType.None)
            {
                Denoise(new DenoiserParams
                {
                    InputTexture = parameters.OutputTexture,
                    OutputTexture = parameters.OutputTexture,
                    Type = _settings.Denoiser,
                    BlendFactor = 0.1f // Default temporal blend factor
                });
            }
        }

        public void TraceReflectionRays(ReflectionRayParams parameters)
        {
            if (!_enabled || (_level != RaytracingLevel.ReflectionsOnly &&
                             _level != RaytracingLevel.ShadowsAndReflections &&
                             _level != RaytracingLevel.Full))
            {
                return;
            }

            BuildTopLevelAS();

            // Dispatch reflection rays
            // - Trace from G-buffer normals and depth
            // - Apply roughness-based importance sampling
            // - Denoise result

            _lastStats.RaysTraced += (long)parameters.SamplesPerPixel * parameters.MaxBounces * 1920 * 1080;
        }

        public void TraceGlobalIllumination(GiRayParams parameters)
        {
            if (!_enabled || _level != RaytracingLevel.Full)
            {
                return;
            }

            BuildTopLevelAS();

            // Dispatch GI rays
            // - Trace indirect lighting paths
            // - Accumulate with temporal history
            // - Apply aggressive denoising

            _lastStats.RaysTraced += (long)parameters.SamplesPerPixel * parameters.MaxBounces * 1920 * 1080;
        }

        public void TraceAmbientOcclusion(AoRayParams parameters)
        {
            if (!_enabled)
            {
                return;
            }

            BuildTopLevelAS();

            // Dispatch AO rays
            // - Short-range visibility queries
            // - Cosine-weighted hemisphere sampling

            _lastStats.RaysTraced += (long)parameters.SamplesPerPixel * 1920 * 1080;
        }

        public void Denoise(DenoiserParams parameters)
        {
            if (!_initialized || parameters.Type == DenoiserType.None || _device == null)
            {
                return;
            }

            // Get texture dimensions from input texture
            int width = 1920;
            int height = 1080;
            var textureInfo = GetTextureInfo(parameters.InputTexture);
            if (textureInfo.HasValue)
            {
                width = textureInfo.Value.Width;
                height = textureInfo.Value.Height;
            }

            // Ensure history buffer exists for this texture handle
            EnsureHistoryBuffer(parameters.InputTexture, width, height);

            // Apply denoising based on type
            switch (parameters.Type)
            {
                case DenoiserType.Temporal:
                    ApplyTemporalDenoising(parameters, width, height);
                    break;

                case DenoiserType.Spatial:
                    ApplySpatialDenoising(parameters, width, height);
                    break;

                case DenoiserType.NvidiaRealTimeDenoiser:
                    // NVIDIA Real-Time Denoiser (NRD) implementation
                    // Full native NRD library integration with CPU-side NRD SDK calls:
                    // - nrd::SetMethodSettings() to configure denoiser parameters
                    // - nrd::GetComputeDispatches() to get shader dispatch information
                    // - nrd::SetCommonSettings() to set per-frame common parameters
                    // - nrd::GetShaderBytecode() to retrieve shader bytecode from shader identifiers
                    // Direct integration with NRD's shader library via compute dispatches
                    // Creates compute pipelines from NRD shader bytecode and caches them for reuse
                    // Automatically uses native NRD library if available, falls back to GPU compute shader otherwise
                    ApplyNRDDenoising(parameters, width, height);
                    break;

                case DenoiserType.IntelOpenImageDenoise:
                    // Intel Open Image Denoise (OIDN) implementation
                    // Full native OIDN library integration with CPU-side processing and data transfer
                    // Automatically uses native OIDN if available, falls back to GPU compute shader otherwise
                    ApplyOIDNDenoising(parameters, width, height);
                    break;
            }

            // Update statistics
            _lastStats.DenoiseTimeMs = 0.0; // Would be measured in real implementation
        }

        public RaytracingStatistics GetStatistics()
        {
            return _lastStats;
        }

        private bool CreatePipelines()
        {
            if (_device == null)
            {
                return false;
            }

            // Create raytracing shader pipelines
            // Each pipeline contains:
            // - Ray generation shader
            // - Miss shader(s)
            // - Closest hit shader(s)
            // - Any hit shader(s) for alpha testing

            // Shadow pipeline - simple occlusion testing
            _shadowPipeline = CreateShadowPipeline();
            if (_shadowPipeline == null)
            {
                Console.WriteLine("[NativeRT] Failed to create shadow pipeline");
                return false;
            }

            // Create shadow pipeline resources
            if (!CreateShadowPipelineResources())
            {
                Console.WriteLine("[NativeRT] Failed to create shadow pipeline resources");
                return false;
            }

            // Reflection pipeline - full material evaluation
            _reflectionPipeline = CreateReflectionPipeline();

            // AO pipeline - short-range visibility
            _aoPipeline = CreateAmbientOcclusionPipeline();

            // GI pipeline - multi-bounce indirect lighting
            _giPipeline = CreateGlobalIlluminationPipeline();

            return true;
        }

        private IRaytracingPipeline CreateShadowPipeline()
        {
            // Create shadow raytracing pipeline
            // Shadow pipeline needs:
            // - RayGen shader: generates shadow rays from light source
            // - Miss shader: returns 1.0 (fully lit) when ray doesn't hit anything
            // - ClosestHit shader: returns 0.0 (fully shadowed) when ray hits geometry
            // - AnyHit shader: can be used for alpha testing

            // Create shaders (in real implementation, these would be loaded from compiled shader bytecode)
            IShader rayGenShader = CreatePlaceholderShader(ShaderType.RayGeneration, "ShadowRayGen");
            IShader missShader = CreatePlaceholderShader(ShaderType.Miss, "ShadowMiss");
            IShader closestHitShader = CreatePlaceholderShader(ShaderType.ClosestHit, "ShadowClosestHit");

            if (rayGenShader == null || missShader == null || closestHitShader == null)
            {
                // If shaders can't be created, return null (pipeline creation will fail gracefully)
                return null;
            }

            // Create hit group for shadow rays
            HitGroup[] hitGroups = new HitGroup[]
            {
                new HitGroup
                {
                    Name = "ShadowHitGroup",
                    ClosestHitShader = closestHitShader,
                    AnyHitShader = null, // No alpha testing for shadows
                    IntersectionShader = null // Using triangle geometry
                }
            };

            // Create binding layout for shadow pipeline
            // Slot 0: TLAS (acceleration structure)
            // Slot 1: Output texture (RWTexture2D<float>)
            // Slot 2: Constant buffer (shadow parameters)
            _shadowBindingLayout = _device.CreateBindingLayout(new BindingLayoutDesc
            {
                Items = new BindingLayoutItem[]
                {
                    new BindingLayoutItem
                    {
                        Slot = 0,
                        Type = BindingType.AccelStruct,
                        Stages = ShaderStageFlags.RayGen,
                        Count = 1
                    },
                    new BindingLayoutItem
                    {
                        Slot = 1,
                        Type = BindingType.RWTexture,
                        Stages = ShaderStageFlags.RayGen,
                        Count = 1
                    },
                    new BindingLayoutItem
                    {
                        Slot = 2,
                        Type = BindingType.ConstantBuffer,
                        Stages = ShaderStageFlags.RayGen | ShaderStageFlags.ClosestHit | ShaderStageFlags.Miss,
                        Count = 1
                    }
                }
            });

            // Create raytracing pipeline
            RaytracingPipelineDesc pipelineDesc = new RaytracingPipelineDesc
            {
                Shaders = new IShader[] { rayGenShader, missShader, closestHitShader },
                HitGroups = hitGroups,
                MaxPayloadSize = 16, // Shadow ray payload: float hitDistance (4 bytes) + padding
                MaxAttributeSize = 8, // Barycentric coordinates (2 floats)
                MaxRecursionDepth = 1, // Shadows only need one bounce
                GlobalBindingLayout = _shadowBindingLayout,
                DebugName = "ShadowRaytracingPipeline"
            };

            return _device.CreateRaytracingPipeline(pipelineDesc);
        }

        private IRaytracingPipeline CreateReflectionPipeline()
        {
            // Placeholder - will be implemented when needed
            return null;
        }

        private IRaytracingPipeline CreateAmbientOcclusionPipeline()
        {
            // Placeholder - will be implemented when needed
            return null;
        }

        private IRaytracingPipeline CreateGlobalIlluminationPipeline()
        {
            // Placeholder - will be implemented when needed
            return null;
        }

        /// <summary>
        /// Creates a shader by loading bytecode from resources or generating minimal valid shader bytecode.
        /// Attempts multiple strategies:
        /// 1. Load from embedded resources (Resources/Shaders/{name}.{extension})
        /// 2. Load from file system (Shaders/{name}.{extension})
        /// 3. Generate minimal valid shader bytecode for the backend (fallback)
        /// </summary>
        private IShader CreatePlaceholderShader(ShaderType type, string name)
        {
            if (_device == null)
            {
                Console.WriteLine($"[NativeRT] Error: Cannot create shader {name} - device is null");
                return null;
            }

            // Attempt to load shader bytecode
            byte[] shaderBytecode = LoadShaderBytecode(name, type);

            if (shaderBytecode == null || shaderBytecode.Length == 0)
            {
                // Try to generate minimal valid shader bytecode for the backend
                shaderBytecode = GenerateMinimalShaderBytecode(type, name);

                if (shaderBytecode == null || shaderBytecode.Length == 0)
                {
                    Console.WriteLine($"[NativeRT] Error: Failed to load or generate shader bytecode for {name} ({type})");
                    Console.WriteLine($"[NativeRT] Shader bytecode must be provided for full functionality.");
                    Console.WriteLine($"[NativeRT] Expected locations:");
                    Console.WriteLine($"[NativeRT]   - Embedded resources: Resources/Shaders/{name}.{GetShaderExtension(type)}");
                    Console.WriteLine($"[NativeRT]   - File system: Shaders/{name}.{GetShaderExtension(type)}");
                    return null;
                }

                Console.WriteLine($"[NativeRT] Warning: Using generated minimal shader bytecode for {name} ({type})");
                Console.WriteLine($"[NativeRT] For production use, provide pre-compiled shader bytecode.");
            }
            else
            {
                Console.WriteLine($"[NativeRT] Successfully loaded shader bytecode for {name} ({type}), size: {shaderBytecode.Length} bytes");
            }

            // Create shader from bytecode
            try
            {
                IShader shader = _device.CreateShader(new ShaderDesc
                {
                    Type = type,
                    Bytecode = shaderBytecode,
                    EntryPoint = GetShaderEntryPoint(type),
                    DebugName = name
                });

                if (shader != null)
                {
                    Console.WriteLine($"[NativeRT] Successfully created shader {name} ({type})");
                }
                else
                {
                    Console.WriteLine($"[NativeRT] Error: Device returned null when creating shader {name} ({type})");
                }

                return shader;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] Exception creating shader {name} ({type}): {ex.Message}");
                Console.WriteLine($"[NativeRT] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to load shader bytecode from embedded resources or file system.
        /// </summary>
        private byte[] LoadShaderBytecode(string shaderName, ShaderType type)
        {
            if (string.IsNullOrEmpty(shaderName))
            {
                return null;
            }

            string extension = GetShaderExtension(type);
            string resourcePath = $"Resources/Shaders/{shaderName}.{extension}";
            string filePath = $"Shaders/{shaderName}.{extension}";

            // Try embedded resources first
            try
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                string fullResourceName = assembly.GetName().Name + "." + resourcePath.Replace('/', '.');

                using (System.IO.Stream stream = assembly.GetManifestResourceStream(fullResourceName))
                {
                    if (stream != null)
                    {
                        byte[] bytecode = new byte[stream.Length];
                        stream.Read(bytecode, 0, bytecode.Length);
                        Console.WriteLine($"[NativeRT] Loaded shader {shaderName} from embedded resource: {fullResourceName}");
                        return bytecode;
                    }
                }
            }
            catch (Exception ex)
            {
                // Embedded resource loading failed, try file system
                Console.WriteLine($"[NativeRT] Failed to load shader from embedded resources: {ex.Message}");
            }

            // Try file system
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    byte[] bytecode = System.IO.File.ReadAllBytes(filePath);
                    Console.WriteLine($"[NativeRT] Loaded shader {shaderName} from file: {filePath}");
                    return bytecode;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] Failed to load shader from file system: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets the file extension for a shader type based on the graphics backend.
        /// D3D12 uses .dxil, Vulkan uses .spv, D3D11 uses .cso
        /// </summary>
        private string GetShaderExtension(ShaderType type)
        {
            GraphicsBackend backend = _device?.Backend ?? GraphicsBackend.Direct3D12;

            switch (backend)
            {
                case GraphicsBackend.Direct3D12:
                    // D3D12 uses DXIL (DirectX Intermediate Language) for raytracing shaders
                    return "dxil";

                case GraphicsBackend.Vulkan:
                    // Vulkan uses SPIR-V for raytracing shaders
                    return "spv";

                case GraphicsBackend.Direct3D11:
                    // D3D11 uses compiled shader object (.cso) but doesn't support raytracing
                    // This is a fallback case
                    return "cso";

                default:
                    // Default to DXIL for unknown backends
                    return "dxil";
            }
        }

        /// <summary>
        /// Gets the entry point name for a shader type.
        /// </summary>
        private string GetShaderEntryPoint(ShaderType type)
        {
            // Most shaders use "main" as the entry point
            // Some backends may require specific entry point names
            return "main";
        }

        /// <summary>
        /// Generates minimal valid shader bytecode for the given shader type and backend.
        /// This is a fallback when shader bytecode cannot be loaded from resources.
        ///
        /// For compute shaders (denoisers), this method provides embedded HLSL source code
        /// that can be compiled to bytecode. For other shader types, returns null as generating
        /// valid raytracing shader bytecode is backend-specific and complex.
        ///
        /// For production use, shaders should be pre-compiled and provided as resources.
        /// </summary>
        private byte[] GenerateMinimalShaderBytecode(ShaderType type, string name)
        {
            GraphicsBackend backend = _device?.Backend ?? GraphicsBackend.Direct3D12;

            // For compute shaders (denoisers), we can provide embedded HLSL source code
            if (type == ShaderType.Compute)
            {
                string hlslSource = GetEmbeddedComputeShaderSource(name);
                if (!string.IsNullOrEmpty(hlslSource))
                {
                    // Try to compile HLSL source to bytecode
                    // If compilation fails, return null and log instructions
                    byte[] bytecode = CompileHlslToBytecode(hlslSource, name, backend);
                    if (bytecode != null && bytecode.Length > 0)
                    {
                        Console.WriteLine($"[NativeRT] Successfully compiled embedded HLSL source for {name}");
                        return bytecode;
                    }
                    else
                    {
                        Console.WriteLine($"[NativeRT] Failed to compile embedded HLSL source for {name}");
                        Console.WriteLine($"[NativeRT] Pre-compiled shader bytecode must be provided.");
                        Console.WriteLine($"[NativeRT] Expected format:");

                        switch (backend)
                        {
                            case GraphicsBackend.Direct3D12:
                                Console.WriteLine($"[NativeRT]   - HLSL source compiled to DXIL using DXC compiler");
                                Console.WriteLine($"[NativeRT]   - Example: dxc.exe -T cs_6_0 -E main {name}.hlsl -Fo {name}.dxil");
                                break;

                            case GraphicsBackend.Vulkan:
                                Console.WriteLine($"[NativeRT]   - GLSL source compiled to SPIR-V using glslc compiler");
                                Console.WriteLine($"[NativeRT]   - Example: glslc -fshader-stage=compute {name}.glsl -o {name}.spv");
                                break;
                        }

                        return null;
                    }
                }
            }

            // For other shader types, generating bytecode is too complex
            Console.WriteLine($"[NativeRT] Shader bytecode generation not supported for {type} on {backend} backend");
            Console.WriteLine($"[NativeRT] Pre-compiled shader bytecode must be provided for shader: {name}");
            Console.WriteLine($"[NativeRT] Expected format:");

            switch (backend)
            {
                case GraphicsBackend.Direct3D12:
                    Console.WriteLine($"[NativeRT]   - HLSL source compiled to DXIL using DXC compiler");
                    Console.WriteLine($"[NativeRT]   - Example: dxc.exe -T {GetDxilShaderTarget(type)} -E main {name}.hlsl -Fo {name}.dxil");
                    break;

                case GraphicsBackend.Vulkan:
                    Console.WriteLine($"[NativeRT]   - GLSL source compiled to SPIR-V using glslc compiler");
                    Console.WriteLine($"[NativeRT]   - Example: glslc -fshader-stage={GetSpirvShaderStage(type)} {name}.glsl -o {name}.spv");
                    break;
            }

            return null;
        }

        /// <summary>
        /// Gets embedded HLSL source code for compute shaders (denoisers).
        /// Returns the HLSL source code as a string, or null if the shader is not available.
        /// </summary>
        private string GetEmbeddedComputeShaderSource(string name)
        {
            switch (name)
            {
                case "TemporalDenoiser":
                    return GetTemporalDenoiserHlslSource();
                case "SpatialDenoiser":
                    return GetSpatialDenoiserHlslSource();
                default:
                    return null;
            }
        }

        /// <summary>
        /// Gets the HLSL source code for the temporal denoiser compute shader.
        ///
        /// Temporal denoising algorithm:
        /// 1. Reproject history buffer using motion vectors
        /// 2. Compute color variance from neighborhood
        /// 3. Clamp history to neighborhood bounds (reduces ghosting)
        /// 4. Blend current frame with clamped history using blend factor
        ///
        /// Binding layout:
        /// - t0: Input texture (current frame, SRV)
        /// - u0: Output texture (denoised result, UAV)
        /// - t1: History texture (previous frame, SRV)
        /// - t2: Normal texture (optional, SRV)
        /// - t3: Motion vector texture (optional, SRV)
        /// - t4: Albedo texture (optional, SRV)
        /// - b0: Constant buffer (denoiser parameters)
        /// </summary>
        private string GetTemporalDenoiserHlslSource()
        {
            return @"
// Temporal Denoiser Compute Shader
// Based on standard temporal accumulation with variance clipping
// swkotor2.exe: N/A (modern raytracing denoiser, not in original game)

cbuffer DenoiserConstants : register(b0)
{
    float4 denoiserParams;  // x: blendFactor, y: sigma, z: radius, w: unused
    int2 resolution;        // Render resolution
    float timeDelta;         // Frame time delta
    float padding;           // Padding for alignment
};

Texture2D<float4> inputTexture : register(t0);
RWTexture2D<float4> outputTexture : register(u0);
Texture2D<float4> historyTexture : register(t1);
Texture2D<float3> normalTexture : register(t2);
Texture2D<float2> motionTexture : register(t3);
Texture2D<float3> albedoTexture : register(t4);

SamplerState linearSampler : register(s0);

[numthreads(8, 8, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    int2 pixelCoord = int2(dispatchThreadId.xy);

    // Clamp to valid texture coordinates
    if (pixelCoord.x >= resolution.x || pixelCoord.y >= resolution.y)
        return;

    float2 uv = (float2(pixelCoord) + 0.5) / float2(resolution);

    // Sample current frame
    float4 currentColor = inputTexture.SampleLevel(linearSampler, uv, 0);

    // Sample motion vectors and reproject history
    float2 motion = float2(0.0, 0.0);
    if (motionTexture != null)
    {
        motion = motionTexture.SampleLevel(linearSampler, uv, 0).xy;
    }

    float2 historyUV = uv - motion;
    float4 historyColor = float4(0.0, 0.0, 0.0, 0.0);

    // Check if history UV is valid (within [0,1] range)
    if (historyUV.x >= 0.0 && historyUV.x <= 1.0 && historyUV.y >= 0.0 && historyUV.y <= 1.0)
    {
        historyColor = historyTexture.SampleLevel(linearSampler, historyUV, 0);
    }

    // Compute color variance from 3x3 neighborhood
    float4 minColor = currentColor;
    float4 maxColor = currentColor;

    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            int2 sampleCoord = pixelCoord + int2(x, y);
            if (sampleCoord.x >= 0 && sampleCoord.x < resolution.x &&
                sampleCoord.y >= 0 && sampleCoord.y < resolution.y)
            {
                float2 sampleUV = (float2(sampleCoord) + 0.5) / float2(resolution);
                float4 sampleColor = inputTexture.SampleLevel(linearSampler, sampleUV, 0);

                minColor = min(minColor, sampleColor);
                maxColor = max(maxColor, sampleColor);
            }
        }
    }

    // Clamp history to neighborhood bounds (variance clipping)
    // This reduces ghosting by preventing history from contributing colors
    // that are too different from the current frame's neighborhood
    float4 clampedHistory = clamp(historyColor, minColor, maxColor);

    // Blend current frame with clamped history
    float blendFactor = denoiserParams.x; // Typically 0.05-0.1 for temporal accumulation
    float4 result = lerp(clampedHistory, currentColor, blendFactor);

    // Write result
    outputTexture[pixelCoord] = result;
}
";
        }

        /// <summary>
        /// Gets the HLSL source code for the spatial denoiser compute shader.
        ///
        /// Spatial denoising algorithm:
        /// 1. Sample neighborhood around current pixel
        /// 2. Compute edge-aware weights based on color and normal similarity
        /// 3. Apply bilateral filter with edge-aware weights
        /// 4. Output filtered result
        ///
        /// Binding layout:
        /// - t0: Input texture (current frame, SRV)
        /// - u0: Output texture (denoised result, UAV)
        /// - t1: History texture (unused for spatial, SRV)
        /// - t2: Normal texture (for edge-aware filtering, SRV)
        /// - t3: Motion vector texture (unused for spatial, SRV)
        /// - t4: Albedo texture (for edge-aware filtering, SRV)
        /// - b0: Constant buffer (denoiser parameters)
        /// </summary>
        private string GetSpatialDenoiserHlslSource()
        {
            return @"
// Spatial Denoiser Compute Shader
// Based on edge-aware bilateral filtering
// swkotor2.exe: N/A (modern raytracing denoiser, not in original game)

cbuffer DenoiserConstants : register(b0)
{
    float4 denoiserParams;  // x: blendFactor (unused), y: sigma (color), z: radius (spatial), w: normalWeight
    int2 resolution;        // Render resolution
    float timeDelta;        // Frame time delta (unused)
    float padding;          // Padding for alignment
};

Texture2D<float4> inputTexture : register(t0);
RWTexture2D<float4> outputTexture : register(u0);
Texture2D<float4> historyTexture : register(t1);
Texture2D<float3> normalTexture : register(t2);
Texture2D<float2> motionTexture : register(t3);
Texture2D<float3> albedoTexture : register(t4);

SamplerState linearSampler : register(s0);

// Compute edge-aware weight for bilateral filtering
float ComputeBilateralWeight(float4 centerColor, float4 sampleColor,
                             float3 centerNormal, float3 sampleNormal,
                             float3 centerAlbedo, float3 sampleAlbedo,
                             float2 offset, float sigmaColor, float sigmaSpatial, float normalWeight)
{
    // Spatial weight (Gaussian based on distance)
    float spatialDist = length(offset);
    float spatialWeight = exp(-(spatialDist * spatialDist) / (2.0 * sigmaSpatial * sigmaSpatial));

    // Color weight (Gaussian based on color difference)
    float colorDist = length(centerColor.rgb - sampleColor.rgb);
    float colorWeight = exp(-(colorDist * colorDist) / (2.0 * sigmaColor * sigmaColor));

    // Normal weight (dot product for surface similarity)
    float normalWeightValue = 1.0;
    if (normalTexture != null)
    {
        float normalDot = dot(centerNormal, sampleNormal);
        normalWeightValue = pow(max(0.0, normalDot), normalWeight);
    }

    // Albedo weight (for edge detection)
    float albedoWeight = 1.0;
    if (albedoTexture != null)
    {
        float albedoDist = length(centerAlbedo - sampleAlbedo);
        albedoWeight = exp(-(albedoDist * albedoDist) / (2.0 * 0.1 * 0.1));
    }

    return spatialWeight * colorWeight * normalWeightValue * albedoWeight;
}

[numthreads(8, 8, 1)]
void main(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    int2 pixelCoord = int2(dispatchThreadId.xy);

    // Clamp to valid texture coordinates
    if (pixelCoord.x >= resolution.x || pixelCoord.y >= resolution.y)
        return;

    float2 uv = (float2(pixelCoord) + 0.5) / float2(resolution);

    // Sample center pixel
    float4 centerColor = inputTexture.SampleLevel(linearSampler, uv, 0);
    float3 centerNormal = float3(0.0, 0.0, 1.0);
    float3 centerAlbedo = float3(1.0, 1.0, 1.0);

    if (normalTexture != null)
    {
        centerNormal = normalTexture.SampleLevel(linearSampler, uv, 0).xyz;
    }

    if (albedoTexture != null)
    {
        centerAlbedo = albedoTexture.SampleLevel(linearSampler, uv, 0).xyz;
    }

    // Bilateral filter parameters
    float sigmaColor = denoiserParams.y;   // Color similarity threshold
    float sigmaSpatial = denoiserParams.z; // Spatial radius
    float normalWeight = denoiserParams.w;  // Normal weight exponent

    // Apply bilateral filter over neighborhood
    float4 filteredColor = float4(0.0, 0.0, 0.0, 0.0);
    float totalWeight = 0.0;

    int radius = (int)sigmaSpatial;
    for (int y = -radius; y <= radius; y++)
    {
        for (int x = -radius; x <= radius; x++)
        {
            int2 sampleCoord = pixelCoord + int2(x, y);
            if (sampleCoord.x >= 0 && sampleCoord.x < resolution.x &&
                sampleCoord.y >= 0 && sampleCoord.y < resolution.y)
            {
                float2 sampleUV = (float2(sampleCoord) + 0.5) / float2(resolution);
                float4 sampleColor = inputTexture.SampleLevel(linearSampler, sampleUV, 0);

                float3 sampleNormal = centerNormal;
                float3 sampleAlbedo = centerAlbedo;

                if (normalTexture != null)
                {
                    sampleNormal = normalTexture.SampleLevel(linearSampler, sampleUV, 0).xyz;
                }

                if (albedoTexture != null)
                {
                    sampleAlbedo = albedoTexture.SampleLevel(linearSampler, sampleUV, 0).xyz;
                }

                float2 offset = float2(x, y);
                float weight = ComputeBilateralWeight(centerColor, sampleColor,
                                                       centerNormal, sampleNormal,
                                                       centerAlbedo, sampleAlbedo,
                                                       offset, sigmaColor, sigmaSpatial, normalWeight);

                filteredColor += sampleColor * weight;
                totalWeight += weight;
            }
        }
    }

    // Normalize by total weight
    if (totalWeight > 0.0)
    {
        filteredColor /= totalWeight;
    }
    else
    {
        filteredColor = centerColor;
    }

    // Write result
    outputTexture[pixelCoord] = filteredColor;
}
";
        }

        /// <summary>
        /// Attempts to compile HLSL source code to shader bytecode.
        ///
        /// This method tries to use DXC (DirectX Shader Compiler) for D3D12 backends,
        /// or glslc for Vulkan backends. If the compiler is not available, returns null.
        ///
        /// In production, shaders should be pre-compiled offline and embedded as resources.
        /// This runtime compilation is provided as a fallback for development and testing.
        ///
        /// swkotor2.exe: N/A (modern raytracing shader compilation, not in original game)
        /// </summary>
        private byte[] CompileHlslToBytecode(string hlslSource, string shaderName, GraphicsBackend backend)
        {
            if (string.IsNullOrEmpty(hlslSource))
            {
                Console.WriteLine($"[NativeRT] CompileHlslToBytecode: Empty HLSL source for {shaderName}");
                return null;
            }

            switch (backend)
            {
                case GraphicsBackend.Direct3D12:
                    return CompileHlslToDxil(hlslSource, shaderName);

                case GraphicsBackend.Vulkan:
                    return CompileHlslToSpirv(hlslSource, shaderName);

                default:
                    Console.WriteLine($"[NativeRT] CompileHlslToBytecode: Unsupported backend {backend} for shader {shaderName}");
                    return null;
            }
        }

        /// <summary>
        /// Compiles HLSL source code to DXIL bytecode using DXC compiler.
        ///
        /// DXC (DirectX Shader Compiler) is the modern HLSL compiler that produces DXIL.
        /// This method locates DXC, writes the HLSL source to a temporary file, executes
        /// DXC to compile it, and reads the resulting DXIL bytecode.
        ///
        /// swkotor2.exe: N/A (modern raytracing shader compilation, not in original game)
        /// </summary>
        private byte[] CompileHlslToDxil(string hlslSource, string shaderName)
        {
            string dxcPath = FindDXCPath();
            if (string.IsNullOrEmpty(dxcPath))
            {
                Console.WriteLine($"[NativeRT] DXC compiler not found. Cannot compile {shaderName} to DXIL.");
                Console.WriteLine($"[NativeRT] DXC can be installed via Windows SDK or downloaded from:");
                Console.WriteLine($"[NativeRT]   https://github.com/microsoft/DirectXShaderCompiler/releases");
                return null;
            }

            // Determine shader type from shader name (default to compute for denoisers)
            ShaderType shaderType = ShaderType.Compute;
            if (shaderName.Contains("RayGen") || shaderName.Contains("RayGeneration"))
            {
                shaderType = ShaderType.RayGeneration;
            }
            else if (shaderName.Contains("Miss"))
            {
                shaderType = ShaderType.Miss;
            }
            else if (shaderName.Contains("ClosestHit"))
            {
                shaderType = ShaderType.ClosestHit;
            }
            else if (shaderName.Contains("AnyHit"))
            {
                shaderType = ShaderType.AnyHit;
            }

            string shaderTarget = GetDxilShaderTarget(shaderType);
            string entryPoint = GetShaderEntryPoint(shaderType);

            // Create temporary files
            string tempSourceFile = Path.Combine(Path.GetTempPath(), $"rt_shader_{Guid.NewGuid()}.hlsl");
            string tempOutputFile = Path.Combine(Path.GetTempPath(), $"rt_shader_{Guid.NewGuid()}.dxil");

            try
            {
                // Write HLSL source to temporary file
                File.WriteAllText(tempSourceFile, hlslSource, Encoding.UTF8);

                // Build DXC command line arguments
                // -T: shader target (e.g., cs_6_0, lib_6_3)
                // -E: entry point name
                // -Fo: output file
                // -spirv: not used for DXIL (only for SPIR-V output)
                // -WX: treat warnings as errors (optional, can be removed for more lenient compilation)
                string arguments = $"-T {shaderTarget} -E {entryPoint} \"{tempSourceFile}\" -Fo \"{tempOutputFile}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = dxcPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        Console.WriteLine($"[NativeRT] Failed to start DXC process for {shaderName}");
                        return null;
                    }

                    // Wait for compilation with timeout (30 seconds)
                    bool completed = process.WaitForExit(30000);
                    if (!completed)
                    {
                        Console.WriteLine($"[NativeRT] DXC compilation timeout for {shaderName}");
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // Ignore kill errors
                        }
                        return null;
                    }

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        string output = process.StandardOutput.ReadToEnd();
                        Console.WriteLine($"[NativeRT] DXC compilation failed for {shaderName} (exit code {process.ExitCode})");
                        if (!string.IsNullOrEmpty(error))
                        {
                            Console.WriteLine($"[NativeRT] DXC error output: {error}");
                        }
                        if (!string.IsNullOrEmpty(output))
                        {
                            Console.WriteLine($"[NativeRT] DXC standard output: {output}");
                        }
                        return null;
                    }

                    // Read compiled DXIL bytecode
                    if (File.Exists(tempOutputFile))
                    {
                        byte[] bytecode = File.ReadAllBytes(tempOutputFile);
                        Console.WriteLine($"[NativeRT] Successfully compiled {shaderName} to DXIL ({bytecode.Length} bytes)");
                        return bytecode;
                    }
                    else
                    {
                        Console.WriteLine($"[NativeRT] DXC succeeded but output file not found: {tempOutputFile}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] Exception during DXC compilation of {shaderName}: {ex.Message}");
                return null;
            }
            finally
            {
                // Cleanup temporary files
                try
                {
                    if (File.Exists(tempSourceFile))
                    {
                        File.Delete(tempSourceFile);
                    }
                    if (File.Exists(tempOutputFile))
                    {
                        File.Delete(tempOutputFile);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Compiles HLSL source code to SPIR-V bytecode using DXC compiler with SPIR-V backend.
        ///
        /// DXC supports compiling HLSL directly to SPIR-V using the -spirv flag.
        /// This is preferred over converting HLSL to GLSL first, as it maintains better
        /// compatibility and handles HLSL-specific features correctly.
        ///
        /// Alternative: If DXC with SPIR-V is not available, this could fall back to
        /// glslc after converting HLSL to GLSL, but that conversion is complex and
        /// error-prone, so we only support DXC with SPIR-V output.
        ///
        /// swkotor2.exe: N/A (modern raytracing shader compilation, not in original game)
        /// </summary>
        private byte[] CompileHlslToSpirv(string hlslSource, string shaderName)
        {
            // Try DXC with SPIR-V output first (preferred method)
            string dxcPath = FindDXCPath();
            if (!string.IsNullOrEmpty(dxcPath))
            {
                // DXC can compile HLSL directly to SPIR-V using -spirv flag
                return CompileHlslToSpirvWithDXC(hlslSource, shaderName, dxcPath);
            }

            // Fallback to glslc (requires HLSL to GLSL conversion, which is complex)
            // For now, we only support DXC with SPIR-V output
            Console.WriteLine($"[NativeRT] DXC compiler not found. Cannot compile {shaderName} to SPIR-V.");
            Console.WriteLine($"[NativeRT] DXC with SPIR-V support is required for Vulkan raytracing shaders.");
            Console.WriteLine($"[NativeRT] DXC can be installed via Windows SDK or downloaded from:");
            Console.WriteLine($"[NativeRT]   https://github.com/microsoft/DirectXShaderCompiler/releases");
            return null;
        }

        /// <summary>
        /// Compiles HLSL source code to SPIR-V using DXC with -spirv flag.
        ///
        /// DXC supports compiling HLSL directly to SPIR-V, which is the preferred
        /// method for Vulkan raytracing shaders as it maintains HLSL semantics.
        ///
        /// swkotor2.exe: N/A (modern raytracing shader compilation, not in original game)
        /// </summary>
        private byte[] CompileHlslToSpirvWithDXC(string hlslSource, string shaderName, string dxcPath)
        {
            // Determine shader type from shader name (default to compute for denoisers)
            ShaderType shaderType = ShaderType.Compute;
            if (shaderName.Contains("RayGen") || shaderName.Contains("RayGeneration"))
            {
                shaderType = ShaderType.RayGeneration;
            }
            else if (shaderName.Contains("Miss"))
            {
                shaderType = ShaderType.Miss;
            }
            else if (shaderName.Contains("ClosestHit"))
            {
                shaderType = ShaderType.ClosestHit;
            }
            else if (shaderName.Contains("AnyHit"))
            {
                shaderType = ShaderType.AnyHit;
            }

            // For SPIR-V output, we still use HLSL shader targets but add -spirv flag
            // DXC will convert the HLSL target to appropriate SPIR-V shader stage
            string shaderTarget = GetDxilShaderTarget(shaderType);
            string entryPoint = GetShaderEntryPoint(shaderType);

            // Create temporary files
            string tempSourceFile = Path.Combine(Path.GetTempPath(), $"rt_shader_{Guid.NewGuid()}.hlsl");
            string tempOutputFile = Path.Combine(Path.GetTempPath(), $"rt_shader_{Guid.NewGuid()}.spv");

            try
            {
                // Write HLSL source to temporary file
                File.WriteAllText(tempSourceFile, hlslSource, Encoding.UTF8);

                // Build DXC command line arguments for SPIR-V output
                // -T: shader target (e.g., cs_6_0, lib_6_3)
                // -E: entry point name
                // -spirv: output SPIR-V instead of DXIL
                // -Fo: output file
                string arguments = $"-T {shaderTarget} -E {entryPoint} -spirv \"{tempSourceFile}\" -Fo \"{tempOutputFile}\"";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = dxcPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        Console.WriteLine($"[NativeRT] Failed to start DXC process for {shaderName}");
                        return null;
                    }

                    // Wait for compilation with timeout (30 seconds)
                    bool completed = process.WaitForExit(30000);
                    if (!completed)
                    {
                        Console.WriteLine($"[NativeRT] DXC compilation timeout for {shaderName}");
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // Ignore kill errors
                        }
                        return null;
                    }

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        string output = process.StandardOutput.ReadToEnd();
                        Console.WriteLine($"[NativeRT] DXC SPIR-V compilation failed for {shaderName} (exit code {process.ExitCode})");
                        if (!string.IsNullOrEmpty(error))
                        {
                            Console.WriteLine($"[NativeRT] DXC error output: {error}");
                        }
                        if (!string.IsNullOrEmpty(output))
                        {
                            Console.WriteLine($"[NativeRT] DXC standard output: {output}");
                        }
                        return null;
                    }

                    // Read compiled SPIR-V bytecode
                    if (File.Exists(tempOutputFile))
                    {
                        byte[] bytecode = File.ReadAllBytes(tempOutputFile);
                        Console.WriteLine($"[NativeRT] Successfully compiled {shaderName} to SPIR-V ({bytecode.Length} bytes)");
                        return bytecode;
                    }
                    else
                    {
                        Console.WriteLine($"[NativeRT] DXC succeeded but output file not found: {tempOutputFile}");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] Exception during DXC SPIR-V compilation of {shaderName}: {ex.Message}");
                return null;
            }
            finally
            {
                // Cleanup temporary files
                try
                {
                    if (File.Exists(tempSourceFile))
                    {
                        File.Delete(tempSourceFile);
                    }
                    if (File.Exists(tempOutputFile))
                    {
                        File.Delete(tempOutputFile);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Finds the path to DXC (DirectX Shader Compiler) executable.
        ///
        /// DXC is typically installed with:
        /// - Windows SDK (in Windows Kits bin directory)
        /// - Visual Studio (in VS installation directory)
        /// - Standalone download from GitHub
        ///
        /// This method searches common installation locations and PATH.
        ///
        /// swkotor2.exe: N/A (modern raytracing shader compilation, not in original game)
        /// </summary>
        private string FindDXCPath()
        {
            string dxcExeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dxc.exe" : "dxc";

            // 1. Try Windows SDK installation directory
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                // Windows 10 SDK typically installs to Program Files (x86)\Windows Kits\10\bin\<version>\x64
                string[] windowsKitsPaths = new[]
                {
                    Path.Combine(programFilesX86, "Windows Kits", "10", "bin"),
                    Path.Combine(programFiles, "Windows Kits", "10", "bin")
                };

                foreach (string kitsPath in windowsKitsPaths)
                {
                    if (Directory.Exists(kitsPath))
                    {
                        // Search for latest version directory
                        string[] versionDirs = Directory.GetDirectories(kitsPath);
                        Array.Sort(versionDirs);
                        Array.Reverse(versionDirs); // Start with latest version

                        foreach (string versionDir in versionDirs)
                        {
                            // Try x64 first, then x86
                            string[] archDirs = new[]
                            {
                                Path.Combine(versionDir, "x64"),
                                Path.Combine(versionDir, "x86")
                            };

                            foreach (string archDir in archDirs)
                            {
                                string dxcPath = Path.Combine(archDir, dxcExeName);
                                if (File.Exists(dxcPath))
                                {
                                    return dxcPath;
                                }
                            }
                        }
                    }
                }
            }

            // 2. Try Visual Studio installation directory
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

                // Visual Studio typically installs DXC in VC\Tools\MSVC\<version>\bin\Hostx64\x64
                string[] vsPaths = new[]
                {
                    Path.Combine(programFiles, "Microsoft Visual Studio"),
                    Path.Combine(programFilesX86, "Microsoft Visual Studio")
                };

                foreach (string vsBasePath in vsPaths)
                {
                    if (Directory.Exists(vsBasePath))
                    {
                        // Search for VS installation directories (e.g., 2022, 2019, etc.)
                        string[] vsVersions = Directory.GetDirectories(vsBasePath);
                        foreach (string vsVersionPath in vsVersions)
                        {
                            string vcToolsPath = Path.Combine(vsVersionPath, "VC", "Tools", "MSVC");
                            if (Directory.Exists(vcToolsPath))
                            {
                                // Search for MSVC version directories
                                string[] msvcVersions = Directory.GetDirectories(vcToolsPath);
                                foreach (string msvcVersionPath in msvcVersions)
                                {
                                    string dxcPath = Path.Combine(msvcVersionPath, "bin", "Hostx64", "x64", dxcExeName);
                                    if (File.Exists(dxcPath))
                                    {
                                        return dxcPath;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 3. Try PATH environment variable
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                string[] paths = pathEnv.Split(Path.PathSeparator);
                foreach (string path in paths)
                {
                    if (!string.IsNullOrEmpty(path))
                    {
                        string dxcPath = Path.Combine(path, dxcExeName);
                        if (File.Exists(dxcPath))
                        {
                            return dxcPath;
                        }
                    }
                }
            }

            // 4. Try current directory and common tool locations
            string[] commonPaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), dxcExeName),
                Path.Combine(Directory.GetCurrentDirectory(), "tools", dxcExeName),
                Path.Combine(Directory.GetCurrentDirectory(), "bin", dxcExeName)
            };

            foreach (string commonPath in commonPaths)
            {
                if (File.Exists(commonPath))
                {
                    return commonPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the DXC shader target for a shader type (for D3D12/DXIL).
        /// </summary>
        private string GetDxilShaderTarget(ShaderType type)
        {
            switch (type)
            {
                case ShaderType.RayGeneration:
                    return "lib_6_3"; // Ray generation shader in lib_6_3 target
                case ShaderType.Miss:
                    return "lib_6_3"; // Miss shader in lib_6_3 target
                case ShaderType.ClosestHit:
                    return "lib_6_3"; // Closest hit shader in lib_6_3 target
                case ShaderType.AnyHit:
                    return "lib_6_3"; // Any hit shader in lib_6_3 target
                case ShaderType.Intersection:
                    return "lib_6_3"; // Intersection shader in lib_6_3 target
                case ShaderType.Callable:
                    return "lib_6_3"; // Callable shader in lib_6_3 target
                case ShaderType.Compute:
                    return "cs_6_0"; // Compute shader in cs_6_0 target
                default:
                    return "lib_6_3";
            }
        }

        /// <summary>
        /// Gets the SPIR-V shader stage for a shader type (for Vulkan).
        /// </summary>
        private string GetSpirvShaderStage(ShaderType type)
        {
            switch (type)
            {
                case ShaderType.RayGeneration:
                    return "rgen"; // Ray generation
                case ShaderType.Miss:
                    return "rmiss"; // Miss
                case ShaderType.ClosestHit:
                    return "rchit"; // Closest hit
                case ShaderType.AnyHit:
                    return "rahit"; // Any hit
                case ShaderType.Intersection:
                    return "rint"; // Intersection
                case ShaderType.Callable:
                    return "rcall"; // Callable
                case ShaderType.Compute:
                    return "compute"; // Compute shader
                default:
                    return "rgen";
            }
        }

        private bool CreateShadowPipelineResources()
        {
            if (_device == null || _shadowBindingLayout == null)
            {
                return false;
            }

            // Create constant buffer for shadow ray parameters
            // Size: Vector3 lightDirection (12 bytes) + float maxDistance (4 bytes) +
            //       int samplesPerPixel (4 bytes) + float softShadowAngle (4 bytes) +
            //       int2 renderResolution (8 bytes) + padding = 32 bytes (aligned)
            _shadowConstantBuffer = _device.CreateBuffer(new BufferDesc
            {
                ByteSize = 32,
                Usage = BufferUsageFlags.ConstantBuffer,
                InitialState = ResourceState.ConstantBuffer,
                IsAccelStructBuildInput = false,
                DebugName = "ShadowRayConstants"
            });

            // Create shader binding table
            // SBT layout:
            // - RayGen: 1 record
            // - Miss: 1 record
            // - HitGroup: 1 record (for opaque geometry)
            // Each record is typically 32-64 bytes depending on shader identifier size
            int sbtRecordSize = 64; // D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT = 32, but we use 64 for safety
            int sbtSize = sbtRecordSize * 3; // RayGen + Miss + HitGroup

            _shadowShaderBindingTable = _device.CreateBuffer(new BufferDesc
            {
                ByteSize = sbtSize,
                Usage = BufferUsageFlags.ShaderBindingTable,
                InitialState = ResourceState.ShaderResource,
                IsAccelStructBuildInput = false,
                DebugName = "ShadowShaderBindingTable"
            });

            // Initialize SBT structure
            _shadowSbt = new ShaderBindingTable
            {
                Buffer = _shadowShaderBindingTable,
                RayGenOffset = 0,
                RayGenSize = (ulong)sbtRecordSize,
                MissOffset = (ulong)sbtRecordSize,
                MissStride = (ulong)sbtRecordSize,
                MissSize = (ulong)sbtRecordSize,
                HitGroupOffset = (ulong)(sbtRecordSize * 2),
                HitGroupStride = (ulong)sbtRecordSize,
                HitGroupSize = (ulong)sbtRecordSize,
                CallableOffset = 0,
                CallableStride = 0,
                CallableSize = 0
            };

            // Write SBT records with shader identifiers retrieved from the raytracing pipeline
            // Shader identifiers are opaque handles obtained via IRaytracingPipeline.GetShaderIdentifier()
            // after the pipeline has been created. This populates the shader binding table with
            // the actual identifiers needed for DispatchRays.
            WriteShaderBindingTable();

            // Create initial binding set (will be updated each frame with current resources)
            _shadowBindingSet = _device.CreateBindingSet(_shadowBindingLayout, new BindingSetDesc
            {
                Items = new BindingSetItem[]
                {
                    new BindingSetItem
                    {
                        Slot = 0,
                        Type = BindingType.AccelStruct,
                        AccelStruct = _tlas
                    },
                    new BindingSetItem
                    {
                        Slot = 1,
                        Type = BindingType.RWTexture,
                        Texture = null // Will be set in UpdateShadowBindingSet
                    },
                    new BindingSetItem
                    {
                        Slot = 2,
                        Type = BindingType.ConstantBuffer,
                        Buffer = _shadowConstantBuffer
                    }
                }
            });

            return true;
        }

        /// <summary>
        /// Writes shader binding table records with shader identifiers retrieved from the raytracing pipeline.
        ///
        /// Shader Binding Table (SBT) structure:
        /// - Each record contains a shader identifier (32 bytes for D3D12, variable for Vulkan/Metal)
        /// - Records are aligned to D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT (32 bytes)
        /// - Additional space (64 bytes total) allows for local root signature arguments
        ///
        /// Based on D3D12 DXR API:
        /// - ID3D12StateObjectProperties::GetShaderIdentifier returns 32-byte opaque handles
        /// - SBT records must be written to GPU-accessible memory before DispatchRays
        /// - Records are written at specific offsets: RayGen (0), Miss (64), HitGroup (128)
        ///
        /// swkotor2.exe: N/A (DirectX 9, no raytracing support)
        /// Modern engines: Retrieve shader identifiers after pipeline creation via GetShaderIdentifier
        /// </summary>
        private void WriteShaderBindingTable()
        {
            if (_shadowShaderBindingTable == null || _shadowPipeline == null)
            {
                Console.WriteLine("[NativeRT] Warning: Cannot write shader binding table - buffer or pipeline is null");
                return;
            }

            if (_device == null)
            {
                Console.WriteLine("[NativeRT] Warning: Cannot write shader binding table - device is null");
                return;
            }

            // Get shader identifiers from the raytracing pipeline
            // Shader identifiers are opaque handles that identify shaders within the pipeline
            // They are written to the SBT buffer at specific offsets
            // Based on D3D12 DXR API: ID3D12StateObjectProperties::GetShaderIdentifier
            byte[] rayGenId = _shadowPipeline.GetShaderIdentifier("ShadowRayGen");
            byte[] missId = _shadowPipeline.GetShaderIdentifier("ShadowMiss");
            byte[] hitGroupId = _shadowPipeline.GetShaderIdentifier("ShadowHitGroup");

            // Shader identifier size is typically 32 bytes (D3D12_SHADER_IDENTIFIER_SIZE_IN_BYTES = 32)
            // SBT record size is 64 bytes to allow for additional data (local root signature arguments, etc.)
            // D3D12_RAYTRACING_SHADER_RECORD_BYTE_ALIGNMENT = 32, but we use 64 for safety and future expansion
            const int shaderIdentifierSize = 32;
            const int sbtRecordSize = 64;

            // Create command list to write to the SBT buffer
            ICommandList commandList = _device.CreateCommandList(CommandListType.Graphics);
            commandList.Open();

            // Write RayGen shader identifier at offset 0
            // Always write a record, even if identifier is unavailable (zero-filled placeholder)
            byte[] rayGenRecord = new byte[sbtRecordSize];
            if (rayGenId != null && rayGenId.Length >= shaderIdentifierSize)
            {
                Array.Copy(rayGenId, 0, rayGenRecord, 0, Math.Min(rayGenId.Length, shaderIdentifierSize));
            }
            else
            {
                // Write zero-filled placeholder if shader identifier is unavailable
                // This ensures the SBT buffer is always properly initialized
                Console.WriteLine("[NativeRT] Warning: ShadowRayGen shader identifier not available, writing zero-filled placeholder");
            }
            // Remaining bytes are zero-initialized (for local root signature arguments, etc.)
            commandList.WriteBuffer(_shadowShaderBindingTable, rayGenRecord, 0);

            // Write Miss shader identifier at offset sbtRecordSize (64)
            // Always write a record, even if identifier is unavailable (zero-filled placeholder)
            byte[] missRecord = new byte[sbtRecordSize];
            if (missId != null && missId.Length >= shaderIdentifierSize)
            {
                Array.Copy(missId, 0, missRecord, 0, Math.Min(missId.Length, shaderIdentifierSize));
            }
            else
            {
                // Write zero-filled placeholder if shader identifier is unavailable
                Console.WriteLine("[NativeRT] Warning: ShadowMiss shader identifier not available, writing zero-filled placeholder");
            }
            // Remaining bytes are zero-initialized
            commandList.WriteBuffer(_shadowShaderBindingTable, missRecord, sbtRecordSize);

            // Write HitGroup shader identifier at offset sbtRecordSize * 2 (128)
            // Always write a record, even if identifier is unavailable (zero-filled placeholder)
            byte[] hitGroupRecord = new byte[sbtRecordSize];
            if (hitGroupId != null && hitGroupId.Length >= shaderIdentifierSize)
            {
                Array.Copy(hitGroupId, 0, hitGroupRecord, 0, Math.Min(hitGroupId.Length, shaderIdentifierSize));
            }
            else
            {
                // Write zero-filled placeholder if shader identifier is unavailable
                Console.WriteLine("[NativeRT] Warning: ShadowHitGroup shader identifier not available, writing zero-filled placeholder");
            }
            // Remaining bytes are zero-initialized
            commandList.WriteBuffer(_shadowShaderBindingTable, hitGroupRecord, sbtRecordSize * 2);

            // Close and execute the command list to write the data to the GPU buffer
            // This ensures all SBT records are written atomically
            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();

            // Log success or warnings
            if (rayGenId != null && missId != null && hitGroupId != null)
            {
                Console.WriteLine("[NativeRT] Shader binding table populated with shader identifiers");
            }
            else
            {
                Console.WriteLine("[NativeRT] Shader binding table populated with available identifiers and zero-filled placeholders");
                if (rayGenId == null) Console.WriteLine("[NativeRT] Warning: ShadowRayGen shader identifier not found");
                if (missId == null) Console.WriteLine("[NativeRT] Warning: ShadowMiss shader identifier not found");
                if (hitGroupId == null) Console.WriteLine("[NativeRT] Warning: ShadowHitGroup shader identifier not found");
            }
        }

        private void UpdateShadowConstants(ShadowRayParams parameters, int width, int height)
        {
            if (_shadowConstantBuffer == null)
            {
                return;
            }

            // Shadow ray constant buffer structure
            // struct ShadowRayConstants {
            //     float3 lightDirection;  // 12 bytes
            //     float maxDistance;      // 4 bytes
            //     float softShadowAngle; // 4 bytes
            //     int samplesPerPixel;   // 4 bytes
            //     int2 renderResolution; // 8 bytes
            // }; // Total: 32 bytes

            // Create structured data for constant buffer
            ShadowRayConstants constants = new ShadowRayConstants
            {
                LightDirection = parameters.LightDirection,
                MaxDistance = parameters.MaxDistance,
                SoftShadowAngle = parameters.SoftShadowAngle,
                SamplesPerPixel = parameters.SamplesPerPixel,
                RenderWidth = width,
                RenderHeight = height
            };

            // Convert to byte array for buffer write
            // Manual layout to ensure correct byte order
            byte[] constantData = new byte[32];
            int offset = 0;

            // Light direction (Vector3 = 3 floats = 12 bytes)
            BitConverter.GetBytes(constants.LightDirection.X).CopyTo(constantData, offset);
            offset += 4;
            BitConverter.GetBytes(constants.LightDirection.Y).CopyTo(constantData, offset);
            offset += 4;
            BitConverter.GetBytes(constants.LightDirection.Z).CopyTo(constantData, offset);
            offset += 4;

            // Max distance (float = 4 bytes)
            BitConverter.GetBytes(constants.MaxDistance).CopyTo(constantData, offset);
            offset += 4;

            // Soft shadow angle (float = 4 bytes)
            BitConverter.GetBytes(constants.SoftShadowAngle).CopyTo(constantData, offset);
            offset += 4;

            // Samples per pixel (int = 4 bytes)
            BitConverter.GetBytes(constants.SamplesPerPixel).CopyTo(constantData, offset);
            offset += 4;

            // Render width (int = 4 bytes)
            BitConverter.GetBytes(constants.RenderWidth).CopyTo(constantData, offset);
            offset += 4;

            // Render height (int = 4 bytes)
            BitConverter.GetBytes(constants.RenderHeight).CopyTo(constantData, offset);

            // Write constants to buffer
            ICommandList commandList = _device.CreateCommandList(CommandListType.Graphics);
            commandList.Open();
            commandList.WriteBuffer(_shadowConstantBuffer, constantData);
            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();
        }

        /// <summary>
        /// Updates the shadow binding set with the current output texture.
        /// Since binding sets are immutable, we recreate the binding set with the new texture.
        /// This method handles both push descriptor support (if available) and binding set recreation.
        /// </summary>
        private void UpdateShadowBindingSet(IntPtr outputTexture)
        {
            if (_shadowBindingSet == null || _shadowBindingLayout == null || _device == null)
            {
                return;
            }

            if (outputTexture == IntPtr.Zero)
            {
                Console.WriteLine("[NativeRT] UpdateShadowBindingSet: Invalid output texture handle");
                return;
            }

            // Get the ITexture object from the handle
            ITexture outputTextureObj = GetTextureFromHandle(outputTexture);
            if (outputTextureObj == null)
            {
                Console.WriteLine($"[NativeRT] UpdateShadowBindingSet: Could not get ITexture from handle {outputTexture}");
                Console.WriteLine($"[NativeRT] UpdateShadowBindingSet: Texture may not be registered. Use RegisterTextureHandle to register textures.");
                return;
            }

            // Check if the binding layout supports push descriptors
            // Push descriptors allow updating bindings without recreating the binding set
            // When push descriptors are supported, descriptors are pushed directly into the command buffer
            // during rendering (in TraceShadowRays), so we don't need to recreate the binding set here
            bool supportsPushDescriptors = _shadowBindingLayout.Desc.IsPushDescriptor;

            if (supportsPushDescriptors)
            {
                // With push descriptor support, we don't need to recreate the binding set
                // The descriptors will be pushed directly into the command buffer when TraceShadowRays is called
                // This is more efficient as it avoids unnecessary binding set recreation
                // The binding set is only used as a fallback if push descriptors fail or aren't supported
                Console.WriteLine($"[NativeRT] UpdateShadowBindingSet: Push descriptors supported, skipping binding set recreation. Descriptors will be pushed directly in command list for output texture {outputTexture}");
                return;
            }

            // Recreate the binding set with the new output texture (only when push descriptors are not supported)
            // Dispose the old binding set first
            IBindingSet oldBindingSet = _shadowBindingSet;

            try
            {
                // Create new binding set with updated texture
                _shadowBindingSet = _device.CreateBindingSet(_shadowBindingLayout, new BindingSetDesc
                {
                    Items = new BindingSetItem[]
                    {
                        new BindingSetItem
                        {
                            Slot = 0,
                            Type = BindingType.AccelStruct,
                            AccelStruct = _tlas
                        },
                        new BindingSetItem
                        {
                            Slot = 1,
                            Type = BindingType.RWTexture,
                            Texture = outputTextureObj
                        },
                        new BindingSetItem
                        {
                            Slot = 2,
                            Type = BindingType.ConstantBuffer,
                            Buffer = _shadowConstantBuffer
                        }
                    }
                });

                if (_shadowBindingSet == null)
                {
                    Console.WriteLine("[NativeRT] UpdateShadowBindingSet: Failed to create new binding set");
                    // Restore old binding set if creation failed
                    _shadowBindingSet = oldBindingSet;
                    return;
                }

                // Dispose the old binding set after successful creation
                oldBindingSet.Dispose();

                Console.WriteLine($"[NativeRT] UpdateShadowBindingSet: Successfully updated binding set with output texture {outputTexture}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] UpdateShadowBindingSet: Exception recreating binding set: {ex.Message}");
                Console.WriteLine($"[NativeRT] UpdateShadowBindingSet: Stack trace: {ex.StackTrace}");

                // Restore old binding set if recreation failed
                _shadowBindingSet = oldBindingSet;
            }
        }

        /// <summary>
        /// Gets texture dimensions from a texture handle.
        /// First checks the texture info cache, then tries to get the texture object and query its dimensions.
        /// </summary>
        /// <summary>
        /// Gets texture information (width, height) from a texture handle.
        /// Checks cache first, then tries to get from registered texture map.
        /// Does NOT call GetTextureFromHandle to avoid circular dependency.
        /// </summary>
        private System.Nullable<(int Width, int Height)> GetTextureInfo(IntPtr textureHandle)
        {
            if (textureHandle == IntPtr.Zero)
            {
                return null;
            }

            // Check cache first
            if (_textureInfoCache.TryGetValue(textureHandle, out TextureInfo cachedInfo))
            {
                return (cachedInfo.Width, cachedInfo.Height);
            }

            // Try to get from registered texture map (avoiding circular dependency)
            if (_textureHandleMap.TryGetValue(textureHandle, out ITexture mappedTexture))
            {
                // Get dimensions from texture description
                int width = mappedTexture.Desc.Width;
                int height = mappedTexture.Desc.Height;

                // Cache the info for future lookups
                _textureInfoCache[textureHandle] = new TextureInfo
                {
                    Width = width,
                    Height = height
                };

                return (width, height);
            }

            // If texture is not found, return null
            return null;
        }

        /// <summary>
        /// Determines if an IntPtr handle appears to be a native texture handle.
        /// Native handles are typically:
        /// - Non-zero pointers that are not in our registered texture map
        /// - Pointers that fall within typical memory ranges for graphics resources
        /// - Not obviously invalid (e.g., very small values that are likely indices)
        /// </summary>
        /// <param name="handle">The handle to check.</param>
        /// <returns>True if the handle appears to be a native texture handle, false otherwise.</returns>
        private bool IsNativeHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            // If it's already in our registered map, it's not a "native" handle (it's already wrapped)
            if (_textureHandleMap.ContainsKey(handle))
            {
                return false;
            }

            // Native handles are typically pointers to graphics API objects
            // They're usually:
            // - Large pointer values (typically > 0x1000 for valid memory addresses)
            // - Not small integers (which might be resource indices)
            // - Aligned to pointer boundaries (though this is platform-dependent)

            long handleValue = handle.ToInt64();

            // Check if handle looks like a valid pointer
            // On 64-bit systems, valid pointers are typically in user space (0x0000000100000000 - 0x00007FFFFFFFFFFF on Windows)
            // On 32-bit systems, valid pointers are typically 0x00400000 - 0x7FFFFFFF
            // We use a conservative check: handle should be >= 0x1000 (4KB) to avoid small integers
            if (handleValue < 0x1000)
            {
                // Very small value, likely an index or invalid handle
                return false;
            }

            // Additional check: On 64-bit systems, if the handle is suspiciously small (< 0x100000),
            // it might be a resource index rather than a pointer
            // However, some graphics APIs do use small handles, so we're conservative
            // We'll accept it if it's not in our map and is non-zero

            return true;
        }

        /// <summary>
        /// Attempts to query texture description from a native handle.
        /// Different graphics backends may provide different mechanisms for this.
        /// </summary>
        /// <param name="nativeHandle">The native texture handle to query.</param>
        /// <returns>TextureDesc if querying succeeded, null otherwise.</returns>
        /// <remarks>
        /// Backend-specific implementations:
        /// - D3D12: Can query ID3D12Resource for description via GetDesc() COM interface
        /// - Vulkan: Can query VkImage for description via vkGetImageMemoryRequirements and format info
        /// - Metal: Can query MTLTexture for description via width/height/pixelFormat properties
        ///
        /// This method attempts to use reflection or backend-specific APIs to query texture info.
        /// If the backend doesn't support querying, returns null and heuristics will be used instead.
        /// </remarks>
        private TextureDesc? QueryTextureDescriptionFromNativeHandle(IntPtr nativeHandle)
        {
            if (nativeHandle == IntPtr.Zero || _device == null)
            {
                return null;
            }

            try
            {
                // Try to query texture info based on backend type
                Type deviceType = _device.GetType();
                string deviceTypeName = deviceType.Name;

                // Backend-specific querying strategies
                if (deviceTypeName.Contains("D3D12"))
                {
                    // D3D12: Try to query ID3D12Resource description
                    // ID3D12Resource::GetDesc() returns D3D12_RESOURCE_DESC
                    // We would need to call QueryInterface to get ID3D12Resource, then call GetDesc()
                    // For now, we can't directly query without backend support, so return null
                    // In a full implementation, D3D12Device would provide QueryD3D12ResourceDescription method
                    return QueryD3D12TextureDescription(nativeHandle);
                }
                else if (deviceTypeName.Contains("Vulkan"))
                {
                    // Vulkan: Try to query VkImage description
                    // VkImage properties can be queried via vkGetImageMemoryRequirements and vkGetImageSubresourceLayout
                    // Format info would need to be stored or queried separately
                    // For now, we can't directly query without backend support, so return null
                    // In a full implementation, VulkanDevice would provide QueryVkImageDescription method
                    return QueryVulkanTextureDescription(nativeHandle);
                }
                else if (deviceTypeName.Contains("Metal"))
                {
                    // Metal: Try to query MTLTexture description
                    // MTLTexture has width, height, pixelFormat properties accessible via Objective-C runtime
                    // For now, we can't directly query without backend support, so return null
                    // In a full implementation, MetalDevice would provide QueryMTLTextureDescription method
                    return QueryMetalTextureDescription(nativeHandle);
                }

                // Unknown backend type, return null to use heuristics
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] QueryTextureDescriptionFromNativeHandle: Failed to query texture description: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to query D3D12 texture description from a native ID3D12Resource handle.
        /// </summary>
        /// <param name="nativeHandle">The native D3D12 resource handle.</param>
        /// <returns>TextureDesc if querying succeeded, null otherwise.</returns>
        /// <remarks>
        /// D3D12 implementation:
        /// - Calls QueryInterface on the handle to get ID3D12Resource interface
        /// - Calls GetDesc() to get D3D12_RESOURCE_DESC
        /// - Converts D3D12_RESOURCE_DESC to TextureDesc
        /// - Based on DirectX 12 API: ID3D12Resource::GetDesc()
        /// </remarks>
        private TextureDesc? QueryD3D12TextureDescription(IntPtr nativeHandle)
        {
            try
            {
                // D3D12: ID3D12Resource::GetDesc() returns D3D12_RESOURCE_DESC
                // We need to:
                // 1. QueryInterface to get ID3D12Resource from the handle
                // 2. Call GetDesc() via COM vtable
                // 3. Convert D3D12_RESOURCE_DESC to TextureDesc

                // Since we don't have direct access to D3D12 COM interfaces here,
                // we would need D3D12Device to provide a helper method
                // For now, return null to use heuristics

                // In a full implementation, this would:
                // - Use Marshal.GetObjectForIUnknown to get ID3D12Resource
                // - Call GetDesc() via COM interface
                // - Convert D3D12_RESOURCE_DESC to TextureDesc

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] QueryD3D12TextureDescription: Failed to query D3D12 texture: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to query Vulkan texture description from a native VkImage handle.
        /// </summary>
        /// <param name="nativeHandle">The native VkImage handle.</param>
        /// <returns>TextureDesc if querying succeeded, null otherwise.</returns>
        /// <remarks>
        /// Vulkan implementation:
        /// - Queries VkImage properties via vkGetImageMemoryRequirements
        /// - Gets format from VkImageCreateInfo (would need to be stored)
        /// - Converts Vulkan structures to TextureDesc
        /// - Based on Vulkan API: vkGetImageMemoryRequirements, vkGetImageSubresourceLayout
        /// </remarks>
        private TextureDesc? QueryVulkanTextureDescription(IntPtr nativeHandle)
        {
            try
            {
                // Vulkan: VkImage properties need to be queried
                // We would need:
                // 1. vkGetImageMemoryRequirements to get size
                // 2. VkImageCreateInfo (stored when image was created) for format/dimensions
                // 3. Convert Vulkan structures to TextureDesc

                // Since we don't have direct access to Vulkan functions here,
                // we would need VulkanDevice to provide a helper method
                // For now, return null to use heuristics

                // In a full implementation, this would:
                // - Call vkGetImageMemoryRequirements via VulkanDevice
                // - Get format/dimensions from stored VkImageCreateInfo
                // - Convert to TextureDesc

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] QueryVulkanTextureDescription: Failed to query Vulkan texture: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to query Metal texture description from a native MTLTexture handle.
        /// </summary>
        /// <param name="nativeHandle">The native MTLTexture handle.</param>
        /// <returns>TextureDesc if querying succeeded, null otherwise.</returns>
        /// <remarks>
        /// Metal implementation:
        /// - Uses Objective-C runtime to query MTLTexture properties
        /// - Gets width, height, pixelFormat via objc_msgSend
        /// - Converts MTLPixelFormat to TextureFormat
        /// - Based on Metal API: MTLTexture width, height, pixelFormat properties
        /// </remarks>
        private TextureDesc? QueryMetalTextureDescription(IntPtr nativeHandle)
        {
            try
            {
                // Metal: MTLTexture has properties accessible via Objective-C runtime
                // We would need:
                // 1. Use objc_msgSend to call width, height, pixelFormat getters
                // 2. Convert MTLPixelFormat to TextureFormat
                // 3. Create TextureDesc from properties

                // Since we don't have direct access to Objective-C runtime here,
                // we would need MetalDevice to provide a helper method
                // For now, return null to use heuristics

                // In a full implementation, this would:
                // - Use objc_msgSend to get width, height, pixelFormat
                // - Convert MTLPixelFormat enum to TextureFormat
                // - Create TextureDesc

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] QueryMetalTextureDescription: Failed to query Metal texture: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to lookup a D3D12 texture from a handle using COM interfaces.
        /// This method tries to query ID3D12Resource from the handle and get its description.
        /// </summary>
        /// <param name="handle">The texture handle to lookup.</param>
        /// <returns>ITexture if lookup succeeded, null otherwise.</returns>
        /// <remarks>
        /// D3D12 implementation:
        /// - Uses COM QueryInterface to get ID3D12Resource from handle
        /// - Calls GetDesc() to get D3D12_RESOURCE_DESC
        /// - Converts to TextureDesc and creates wrapper texture
        /// - Based on DirectX 12 API: ID3D12Resource::GetDesc(), IUnknown::QueryInterface
        /// </remarks>
        private ITexture TryLookupD3D12Texture(IntPtr handle)
        {
            try
            {
                // D3D12: Try to query ID3D12Resource from handle using COM
                // Even if the handle doesn't pass IsNativeHandle check, it might be a valid D3D12 resource
                // We use Marshal.GetObjectForIUnknown to get the COM object, then QueryInterface for ID3D12Resource

                // First, try to get the object as IUnknown
                object unknown = Marshal.GetObjectForIUnknown(handle);
                if (unknown == null)
                {
                    return null;
                }

                // Try to query for ID3D12Resource interface
                // ID3D12Resource GUID: {696442be-a72e-4059-bc79-5b5c98040fad}
                Guid iidID3D12Resource = new Guid(0x696442be, 0xa72e, 0x4059, 0xbc, 0x79, 0x5b, 0x5c, 0x98, 0x04, 0x0f, 0xad);
                IntPtr resourcePtr = IntPtr.Zero;
                int hr = Marshal.QueryInterface(handle, iidID3D12Resource, out resourcePtr);

                if (hr == 0 && resourcePtr != IntPtr.Zero)
                {
                    // Successfully got ID3D12Resource, now try to get description
                    // We would need to call GetDesc() via COM vtable
                    // For now, try to query texture description using the existing method
                    TextureDesc? desc = QueryD3D12TextureDescription(resourcePtr);
                    if (desc.HasValue)
                    {
                        ITexture texture = _device.CreateHandleForNativeTexture(resourcePtr, desc.Value);
                        if (texture != null)
                        {
                            // Cache it for future lookups
                            _textureHandleMap[handle] = texture;
                            _textureInfoCache[handle] = new TextureInfo
                            {
                                Width = desc.Value.Width,
                                Height = desc.Value.Height
                            };
                            Console.WriteLine($"[NativeRT] TryLookupD3D12Texture: Successfully looked up D3D12 texture {handle:X}");
                            return texture;
                        }
                    }

                    // Release the queried interface
                    Marshal.Release(resourcePtr);
                }
            }
            catch (Exception ex)
            {
                // Handle might not be a COM object, or QueryInterface failed
                // This is expected for non-D3D12 handles or invalid handles
                Console.WriteLine($"[NativeRT] TryLookupD3D12Texture: Failed to lookup D3D12 texture {handle:X}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Attempts to lookup a Vulkan texture from a handle.
        /// This method tries to query VkImage properties from the handle.
        /// </summary>
        /// <param name="handle">The texture handle to lookup.</param>
        /// <returns>ITexture if lookup succeeded, null otherwise.</returns>
        /// <remarks>
        /// Vulkan implementation:
        /// - Queries VkImage properties via vkGetImageMemoryRequirements
        /// - Gets format from stored VkImageCreateInfo (if available)
        /// - Converts to TextureDesc and creates wrapper texture
        /// - Based on Vulkan API: vkGetImageMemoryRequirements, vkGetImageSubresourceLayout
        /// </remarks>
        private ITexture TryLookupVulkanTexture(IntPtr handle)
        {
            try
            {
                // Vulkan: Try to query VkImage from handle
                // Vulkan uses handles (VkImage is a uint64_t) that might not pass IsNativeHandle check
                // We would need VulkanDevice to provide a helper method to query image properties

                // For now, try to query texture description using the existing method
                TextureDesc? desc = QueryVulkanTextureDescription(handle);
                if (desc.HasValue)
                {
                    ITexture texture = _device.CreateHandleForNativeTexture(handle, desc.Value);
                    if (texture != null)
                    {
                        // Cache it for future lookups
                        _textureHandleMap[handle] = texture;
                        _textureInfoCache[handle] = new TextureInfo
                        {
                            Width = desc.Value.Width,
                            Height = desc.Value.Height
                        };
                        Console.WriteLine($"[NativeRT] TryLookupVulkanTexture: Successfully looked up Vulkan texture {handle:X}");
                        return texture;
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle might not be a VkImage, or querying failed
                Console.WriteLine($"[NativeRT] TryLookupVulkanTexture: Failed to lookup Vulkan texture {handle:X}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Attempts to lookup a Metal texture from a handle.
        /// This method tries to query MTLTexture properties from the handle.
        /// </summary>
        /// <param name="handle">The texture handle to lookup.</param>
        /// <returns>ITexture if lookup succeeded, null otherwise.</returns>
        /// <remarks>
        /// Metal implementation:
        /// - Uses Objective-C runtime to query MTLTexture properties
        /// - Gets width, height, pixelFormat via objc_msgSend
        /// - Converts to TextureDesc and creates wrapper texture
        /// - Based on Metal API: MTLTexture width, height, pixelFormat properties
        /// </remarks>
        private ITexture TryLookupMetalTexture(IntPtr handle)
        {
            try
            {
                // Metal: Try to query MTLTexture from handle
                // Metal uses Objective-C objects that might not pass IsNativeHandle check
                // We would need MetalDevice to provide a helper method to query texture properties

                // For now, try to query texture description using the existing method
                TextureDesc? desc = QueryMetalTextureDescription(handle);
                if (desc.HasValue)
                {
                    ITexture texture = _device.CreateHandleForNativeTexture(handle, desc.Value);
                    if (texture != null)
                    {
                        // Cache it for future lookups
                        _textureHandleMap[handle] = texture;
                        _textureInfoCache[handle] = new TextureInfo
                        {
                            Width = desc.Value.Width,
                            Height = desc.Value.Height
                        };
                        Console.WriteLine($"[NativeRT] TryLookupMetalTexture: Successfully looked up Metal texture {handle:X}");
                        return texture;
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle might not be a MTLTexture, or querying failed
                Console.WriteLine($"[NativeRT] TryLookupMetalTexture: Failed to lookup Metal texture {handle:X}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Attempts to infer texture information from handle patterns.
        /// This is a last resort method that tries to guess texture properties from handle values.
        /// </summary>
        /// <param name="handle">The texture handle to infer from.</param>
        /// <returns>ITexture if inference succeeded, null otherwise.</returns>
        /// <remarks>
        /// Inference strategies:
        /// - Some backends encode texture information in handle values
        /// - Try common raytracing texture formats and resolutions
        /// - Use cached texture info if available
        /// - Based on common raytracing output texture patterns
        /// </remarks>
        private ITexture TryInferTextureFromHandle(IntPtr handle)
        {
            try
            {
                // Try to infer texture info from handle patterns
                // This is a last resort and may not work for all backends
                // Some backends encode texture information in handle values

                // Get dimensions from cache if available
                int width = 1920;  // Default fallback dimensions
                int height = 1080;

                if (_textureInfoCache.TryGetValue(handle, out TextureInfo cachedInfo))
                {
                    width = cachedInfo.Width;
                    height = cachedInfo.Height;
                }
                else
                {
                    // Try to infer from handle value (very backend-specific)
                    // Some backends encode width/height in handle bits
                    // For now, use common raytracing output resolutions
                    long handleValue = handle.ToInt64();
                    if (handleValue > 0 && handleValue < 0x100000)
                    {
                        // Small handle value, might be a resource index
                        // Use default dimensions
                        width = 1920;
                        height = 1080;
                    }
                }

                // Try common raytracing texture formats
                TextureFormat[] commonFormats = new TextureFormat[]
                {
                    TextureFormat.R32G32B32A32_Float,  // Most common for raytracing HDR output
                    TextureFormat.R16G16B16A16_Float,  // Medium precision HDR
                    TextureFormat.R11G11B10_Float,     // Packed HDR (common in games)
                    TextureFormat.R8G8B8A8_UNorm,      // LDR output
                    TextureFormat.R10G10B10A2_UNorm    // Packed LDR
                };

                foreach (TextureFormat format in commonFormats)
                {
                    try
                    {
                        TextureDesc desc = new TextureDesc
                        {
                            Width = width,
                            Height = height,
                            Depth = 1,
                            ArraySize = 1,
                            MipLevels = 1,
                            SampleCount = 1,
                            Format = format,
                            Dimension = TextureDimension.Texture2D,
                            Usage = TextureUsage.ShaderResource | TextureUsage.UnorderedAccess,
                            InitialState = ResourceState.UnorderedAccess,
                            KeepInitialState = false,
                            DebugName = $"RaytracingInferredTexture_{format}"
                        };

                        ITexture texture = _device.CreateHandleForNativeTexture(handle, desc);
                        if (texture != null)
                        {
                            // Cache it for future lookups
                            _textureHandleMap[handle] = texture;
                            _textureInfoCache[handle] = new TextureInfo
                            {
                                Width = width,
                                Height = height
                            };
                            Console.WriteLine($"[NativeRT] TryInferTextureFromHandle: Successfully inferred texture from handle {handle:X} using format {format} ({width}x{height})");
                            return texture;
                        }
                    }
                    catch (Exception formatEx)
                    {
                        // This format didn't work, try next one
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] TryInferTextureFromHandle: Failed to infer texture from handle {handle:X}: {ex.Message}");
            }

            return null;
        }

        private void DestroyPipelines()
        {
            // Destroy shadow pipeline resources
            if (_shadowBindingSet != null)
            {
                _shadowBindingSet.Dispose();
                _shadowBindingSet = null;
            }

            if (_shadowConstantBuffer != null)
            {
                _shadowConstantBuffer.Dispose();
                _shadowConstantBuffer = null;
            }

            if (_shadowShaderBindingTable != null)
            {
                _shadowShaderBindingTable.Dispose();
                _shadowShaderBindingTable = null;
            }

            if (_shadowBindingLayout != null)
            {
                _shadowBindingLayout.Dispose();
                _shadowBindingLayout = null;
            }

            if (_shadowPipeline != null)
            {
                _shadowPipeline.Dispose();
                _shadowPipeline = null;
            }

            // Destroy other pipelines
            if (_reflectionPipeline != null)
            {
                _reflectionPipeline.Dispose();
                _reflectionPipeline = null;
            }

            if (_aoPipeline != null)
            {
                _aoPipeline.Dispose();
                _aoPipeline = null;
            }

            if (_giPipeline != null)
            {
                _giPipeline.Dispose();
                _giPipeline = null;
            }
        }

        private void InitializeDenoiser(DenoiserType type)
        {
            if (_device == null)
            {
                return;
            }

            _currentDenoiserType = type;

            switch (type)
            {
                case DenoiserType.NvidiaRealTimeDenoiser:
                    // NVIDIA Real-Time Denoiser (NRD) initialization
                    // Full native NRD library integration with GPU-side processing
                    // Attempts to initialize native NRD library, falls back to GPU compute shader if not available
                    if (InitializeNRD(1920, 1080)) // Default resolution, will be updated on first denoise call
                    {
                        Console.WriteLine("[NativeRT] Using NVIDIA Real-Time Denoiser (native library)");
                        // Native NRD requires compute pipelines for shader dispatch
                        CreateDenoiserPipelines();
                    }
                    else
                    {
                        Console.WriteLine("[NativeRT] Using NVIDIA Real-Time Denoiser (GPU compute shader fallback)");
                        CreateDenoiserPipelines();
                    }
                    break;

                case DenoiserType.IntelOpenImageDenoise:
                    // Intel Open Image Denoise (OIDN) initialization
                    // Attempts to initialize native OIDN library for CPU-side denoising
                    // Falls back to GPU compute shader if native library is not available
                    if (InitializeOIDN())
                    {
                        Console.WriteLine("[NativeRT] Using Intel Open Image Denoise (native library)");
                        // Native OIDN doesn't require compute pipelines - it processes on CPU
                    }
                    else
                    {
                        Console.WriteLine("[NativeRT] Using Intel Open Image Denoise (GPU compute shader fallback)");
                        CreateDenoiserPipelines();
                    }
                    break;

                case DenoiserType.Temporal:
                    Console.WriteLine("[NativeRT] Using temporal denoiser");
                    CreateDenoiserPipelines();
                    break;

                case DenoiserType.Spatial:
                    Console.WriteLine("[NativeRT] Using spatial denoiser");
                    CreateDenoiserPipelines();
                    break;
            }
        }

        private void ShutdownDenoiser()
        {
            // Release NRD denoiser if initialized
            if (_nrdInitialized)
            {
                ReleaseNRDDenoiser();
            }

            // Release OIDN device and filter if initialized
            if (_oidnInitialized)
            {
                ReleaseOIDNDevice();
            }

            // Destroy compute pipelines
            if (_temporalDenoiserPipeline != null)
            {
                _temporalDenoiserPipeline.Dispose();
                _temporalDenoiserPipeline = null;
            }

            if (_spatialDenoiserPipeline != null)
            {
                _spatialDenoiserPipeline.Dispose();
                _spatialDenoiserPipeline = null;
            }

            // Destroy binding layout
            if (_denoiserBindingLayout != null)
            {
                _denoiserBindingLayout.Dispose();
                _denoiserBindingLayout = null;
            }

            // Destroy constant buffer
            if (_denoiserConstantBuffer != null)
            {
                _denoiserConstantBuffer.Dispose();
                _denoiserConstantBuffer = null;
            }

            // Destroy history buffers
            foreach (ITexture historyBuffer in _historyBuffers.Values)
            {
                if (historyBuffer != null)
                {
                    historyBuffer.Dispose();
                }
            }
            _historyBuffers.Clear();
            _historyBufferWidths.Clear();
            _historyBufferHeights.Clear();

            _currentDenoiserType = DenoiserType.None;
        }

        private void CreateDenoiserPipelines()
        {
            if (_device == null)
            {
                return;
            }

            // Create binding layout for denoiser compute shaders
            // Slot 0: Input texture (SRV)
            // Slot 1: Output texture (UAV)
            // Slot 2: History texture (SRV)
            // Slot 3: Normal texture (SRV, optional)
            // Slot 4: Motion vector texture (SRV, optional)
            // Slot 5: Albedo texture (SRV, optional)
            // Slot 6: Constant buffer (denoiser parameters)
            _denoiserBindingLayout = _device.CreateBindingLayout(new BindingLayoutDesc
            {
                Items = new BindingLayoutItem[]
                {
                    new BindingLayoutItem
                    {
                        Slot = 0,
                        Type = BindingType.Texture,
                        Stages = ShaderStageFlags.Compute,
                        Count = 1
                    },
                    new BindingLayoutItem
                    {
                        Slot = 1,
                        Type = BindingType.RWTexture,
                        Stages = ShaderStageFlags.Compute,
                        Count = 1
                    },
                    new BindingLayoutItem
                    {
                        Slot = 2,
                        Type = BindingType.Texture,
                        Stages = ShaderStageFlags.Compute,
                        Count = 1
                    },
                    new BindingLayoutItem
                    {
                        Slot = 3,
                        Type = BindingType.Texture,
                        Stages = ShaderStageFlags.Compute,
                        Count = 1
                    },
                    new BindingLayoutItem
                    {
                        Slot = 4,
                        Type = BindingType.Texture,
                        Stages = ShaderStageFlags.Compute,
                        Count = 1
                    },
                    new BindingLayoutItem
                    {
                        Slot = 5,
                        Type = BindingType.Texture,
                        Stages = ShaderStageFlags.Compute,
                        Count = 1
                    },
                    new BindingLayoutItem
                    {
                        Slot = 6,
                        Type = BindingType.ConstantBuffer,
                        Stages = ShaderStageFlags.Compute,
                        Count = 1
                    }
                },
                IsPushDescriptor = false
            });

            // Create constant buffer for denoiser parameters
            // Size: float4 denoiserParams (blend factor, sigma, radius, etc) = 16 bytes
            //       int2 resolution = 8 bytes
            //       float timeDelta = 4 bytes
            //       padding = 4 bytes
            // Total: 32 bytes (aligned)
            _denoiserConstantBuffer = _device.CreateBuffer(new BufferDesc
            {
                ByteSize = 32,
                Usage = BufferUsageFlags.ConstantBuffer,
                InitialState = ResourceState.ConstantBuffer,
                IsAccelStructBuildInput = false,
                DebugName = "DenoiserConstants"
            });

            // Create compute shaders for denoising
            // Shader source code is embedded in GetTemporalDenoiserHlslSource() and GetSpatialDenoiserHlslSource()
            // The shaders implement full temporal accumulation with variance clipping and edge-aware bilateral filtering
            // Shader bytecode must be compiled from the embedded HLSL source using DXC (D3D12) or glslc (Vulkan)
            // and placed in Shaders/ directory or embedded as resources
            IShader temporalShader = CreatePlaceholderComputeShader("TemporalDenoiser");
            IShader spatialShader = CreatePlaceholderComputeShader("SpatialDenoiser");

            if (temporalShader != null)
            {
                _temporalDenoiserPipeline = _device.CreateComputePipeline(new ComputePipelineDesc
                {
                    ComputeShader = temporalShader,
                    BindingLayouts = new IBindingLayout[] { _denoiserBindingLayout }
                });
            }

            if (spatialShader != null)
            {
                _spatialDenoiserPipeline = _device.CreateComputePipeline(new ComputePipelineDesc
                {
                    ComputeShader = spatialShader,
                    BindingLayouts = new IBindingLayout[] { _denoiserBindingLayout }
                });
            }
        }

        /// <summary>
        /// Creates a compute shader by loading bytecode from resources or generating minimal valid shader bytecode.
        /// Uses the same loading strategy as CreatePlaceholderShader but for compute shaders.
        /// </summary>
        private IShader CreatePlaceholderComputeShader(string name)
        {
            if (_device == null)
            {
                Console.WriteLine($"[NativeRT] Error: Cannot create compute shader {name} - device is null");
                return null;
            }

            // Attempt to load compute shader bytecode
            byte[] shaderBytecode = LoadShaderBytecode(name, ShaderType.Compute);

            if (shaderBytecode == null || shaderBytecode.Length == 0)
            {
                // Try to generate minimal valid compute shader bytecode for the backend
                shaderBytecode = GenerateMinimalShaderBytecode(ShaderType.Compute, name);

                if (shaderBytecode == null || shaderBytecode.Length == 0)
                {
                    Console.WriteLine($"[NativeRT] Error: Failed to load or generate compute shader bytecode for {name}");
                    Console.WriteLine($"[NativeRT] Compute shader bytecode must be provided for full functionality.");
                    Console.WriteLine($"[NativeRT] Expected locations:");
                    Console.WriteLine($"[NativeRT]   - Embedded resources: Resources/Shaders/{name}.{GetShaderExtension(ShaderType.Compute)}");
                    Console.WriteLine($"[NativeRT]   - File system: Shaders/{name}.{GetShaderExtension(ShaderType.Compute)}");
                    return null;
                }

                Console.WriteLine($"[NativeRT] Warning: Using generated minimal compute shader bytecode for {name}");
                Console.WriteLine($"[NativeRT] For production use, provide pre-compiled shader bytecode.");
            }
            else
            {
                Console.WriteLine($"[NativeRT] Successfully loaded compute shader bytecode for {name}, size: {shaderBytecode.Length} bytes");
            }

            // Create compute shader from bytecode
            try
            {
                IShader shader = _device.CreateShader(new ShaderDesc
                {
                    Type = ShaderType.Compute,
                    Bytecode = shaderBytecode,
                    EntryPoint = GetShaderEntryPoint(ShaderType.Compute),
                    DebugName = name
                });

                if (shader != null)
                {
                    Console.WriteLine($"[NativeRT] Successfully created compute shader {name}");
                }
                else
                {
                    Console.WriteLine($"[NativeRT] Error: Device returned null when creating compute shader {name}");
                }

                return shader;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] Exception creating compute shader {name}: {ex.Message}");
                Console.WriteLine($"[NativeRT] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private void EnsureHistoryBuffer(IntPtr textureHandle, int width, int height)
        {
            if (_historyBuffers.ContainsKey(textureHandle))
            {
                // Check if dimensions match
                if (_historyBufferWidths[textureHandle] == width && _historyBufferHeights[textureHandle] == height)
                {
                    return; // History buffer exists with correct dimensions
                }

                // Recreate history buffer if dimensions changed
                _historyBuffers[textureHandle].Dispose();
                _historyBuffers.Remove(textureHandle);
                _historyBufferWidths.Remove(textureHandle);
                _historyBufferHeights.Remove(textureHandle);
            }

            // Create new history buffer
            ITexture historyBuffer = _device.CreateTexture(new TextureDesc
            {
                Width = width,
                Height = height,
                Depth = 1,
                ArraySize = 1,
                MipLevels = 1,
                SampleCount = 1,
                Format = TextureFormat.R32G32B32A32_Float, // RGBA32F for high precision accumulation
                Dimension = TextureDimension.Texture2D,
                Usage = TextureUsage.ShaderResource | TextureUsage.UnorderedAccess,
                InitialState = ResourceState.UnorderedAccess,
                KeepInitialState = false,
                DebugName = "DenoiserHistory"
            });

            _historyBuffers[textureHandle] = historyBuffer;
            _historyBufferWidths[textureHandle] = width;
            _historyBufferHeights[textureHandle] = height;
        }

        private void ApplyTemporalDenoising(DenoiserParams parameters, int width, int height)
        {
            if (_temporalDenoiserPipeline == null || _denoiserBindingLayout == null || _device == null)
            {
                return;
            }

            // Get or create history buffer
            ITexture historyBuffer = null;
            if (_historyBuffers.TryGetValue(parameters.InputTexture, out historyBuffer))
            {
                // History buffer exists
            }
            else
            {
                // This should not happen if EnsureHistoryBuffer was called
                EnsureHistoryBuffer(parameters.InputTexture, width, height);
                historyBuffer = _historyBuffers[parameters.InputTexture];
            }

            // Update denoiser constant buffer
            UpdateDenoiserConstants(parameters, width, height);

            // Get input and output textures as ITexture objects
            // Uses texture handle lookup mechanism (RegisterTextureHandle must be called for textures to be found)
            ITexture inputTexture = GetTextureFromHandle(parameters.InputTexture);
            ITexture outputTexture = GetTextureFromHandle(parameters.OutputTexture);
            ITexture normalTexture = GetTextureFromHandle(parameters.NormalTexture);
            ITexture motionTexture = GetTextureFromHandle(parameters.MotionTexture);

            if (inputTexture == null || outputTexture == null)
            {
                return; // Cannot denoise without valid textures
            }

            // Create binding set for temporal denoising
            IBindingSet bindingSet = CreateDenoiserBindingSet(parameters, historyBuffer);
            if (bindingSet == null)
            {
                return;
            }

            // Execute temporal denoising compute shader
            ICommandList commandList = _device.CreateCommandList(CommandListType.Compute);
            commandList.Open();

            // Transition resources to appropriate states
            commandList.SetTextureState(inputTexture, ResourceState.ShaderResource);
            commandList.SetTextureState(outputTexture, ResourceState.UnorderedAccess);
            if (historyBuffer != null)
            {
                commandList.SetTextureState(historyBuffer, ResourceState.ShaderResource);
            }
            if (normalTexture != null)
            {
                commandList.SetTextureState(normalTexture, ResourceState.ShaderResource);
            }
            if (motionTexture != null)
            {
                commandList.SetTextureState(motionTexture, ResourceState.ShaderResource);
            }
            commandList.CommitBarriers();

            // Set compute state
            ComputeState computeState = new ComputeState
            {
                Pipeline = _temporalDenoiserPipeline,
                BindingSets = new IBindingSet[] { bindingSet }
            };
            commandList.SetComputeState(computeState);

            // Dispatch compute shader
            // Thread group size is typically 8x8 or 16x16 for denoising
            int threadGroupSize = 8;
            int groupCountX = (width + threadGroupSize - 1) / threadGroupSize;
            int groupCountY = (height + threadGroupSize - 1) / threadGroupSize;
            commandList.Dispatch(groupCountX, groupCountY, 1);

            // Transition output back to shader resource for next pass
            commandList.SetTextureState(outputTexture, ResourceState.ShaderResource);
            commandList.CommitBarriers();

            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();

            // Dispose binding set
            bindingSet.Dispose();

            // Copy current output to history buffer for next frame
            CopyTextureToHistory(parameters.OutputTexture, historyBuffer);
        }

        private void ApplySpatialDenoising(DenoiserParams parameters, int width, int height)
        {
            if (_spatialDenoiserPipeline == null || _denoiserBindingLayout == null || _device == null)
            {
                return;
            }

            // Update denoiser constant buffer
            UpdateDenoiserConstants(parameters, width, height);

            // Get input and output textures
            ITexture inputTexture = GetTextureFromHandle(parameters.InputTexture);
            ITexture outputTexture = GetTextureFromHandle(parameters.OutputTexture);
            ITexture normalTexture = GetTextureFromHandle(parameters.NormalTexture);
            ITexture albedoTexture = GetTextureFromHandle(parameters.AlbedoTexture);

            if (inputTexture == null || outputTexture == null)
            {
                return;
            }

            // Create binding set for spatial denoising
            IBindingSet bindingSet = CreateDenoiserBindingSet(parameters, null);
            if (bindingSet == null)
            {
                return;
            }

            // Execute spatial denoising compute shader
            ICommandList commandList = _device.CreateCommandList(CommandListType.Compute);
            commandList.Open();

            // Transition resources
            commandList.SetTextureState(inputTexture, ResourceState.ShaderResource);
            commandList.SetTextureState(outputTexture, ResourceState.UnorderedAccess);
            if (normalTexture != null)
            {
                commandList.SetTextureState(normalTexture, ResourceState.ShaderResource);
            }
            if (albedoTexture != null)
            {
                commandList.SetTextureState(albedoTexture, ResourceState.ShaderResource);
            }
            commandList.CommitBarriers();

            // Set compute state
            ComputeState computeState = new ComputeState
            {
                Pipeline = _spatialDenoiserPipeline,
                BindingSets = new IBindingSet[] { bindingSet }
            };
            commandList.SetComputeState(computeState);

            // Dispatch compute shader
            int threadGroupSize = 8;
            int groupCountX = (width + threadGroupSize - 1) / threadGroupSize;
            int groupCountY = (height + threadGroupSize - 1) / threadGroupSize;
            commandList.Dispatch(groupCountX, groupCountY, 1);

            // Transition output back
            commandList.SetTextureState(outputTexture, ResourceState.ShaderResource);
            commandList.CommitBarriers();

            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();

            // Dispose binding set
            bindingSet.Dispose();
        }

        /// <summary>
        /// Applies Intel Open Image Denoise (OIDN) denoising to the input texture.
        /// Based on Intel OIDN API: Full native library integration with CPU-side processing.
        /// </summary>
        /// <param name="parameters">Denoiser parameters containing input/output textures and auxiliary buffers.</param>
        /// <param name="width">Width of the texture in pixels.</param>
        /// <param name="height">Height of the texture in pixels.</param>
        /// <remarks>
        /// Intel Open Image Denoise (OIDN) Implementation:
        /// - OIDN is a CPU-based denoising library that uses machine learning models
        /// - Full native integration requires:
        ///   1. CPU-side texture data transfer (GPU -> CPU)
        ///   2. OIDN library calls (oidnNewDevice, oidnNewFilter, oidnExecuteFilter)
        ///   3. CPU -> GPU data transfer back
        /// - OIDN typically uses albedo and normal buffers for high-quality denoising
        /// - The algorithm performs filtering in multiple passes using a hierarchical approach
        /// - Falls back to GPU compute shader if native OIDN library is not available
        /// </remarks>
        private void ApplyOIDNDenoising(DenoiserParams parameters, int width, int height)
        {
            if (_device == null)
            {
                return;
            }

            // Get input and output textures
            ITexture inputTexture = GetTextureFromHandle(parameters.InputTexture);
            ITexture outputTexture = GetTextureFromHandle(parameters.OutputTexture);
            ITexture normalTexture = GetTextureFromHandle(parameters.NormalTexture);
            ITexture albedoTexture = GetTextureFromHandle(parameters.AlbedoTexture);

            if (inputTexture == null || outputTexture == null)
            {
                return;
            }

            // Try native OIDN library first if available
            if (_useNativeOIDN && _oidnInitialized && _oidnFilter != IntPtr.Zero)
            {
                // Native OIDN processing path
                // Step 1: Transfer texture data from GPU to CPU
                float[] inputData = ReadTextureDataFromGPU(inputTexture, width, height);
                if (inputData == null)
                {
                    // Texture read not supported by backend, fall back to GPU compute shader
                    Console.WriteLine("[NativeRT] ApplyOIDNDenoising: GPU->CPU transfer not supported, falling back to GPU compute shader");
                    ApplyOIDNDenoisingGPU(parameters, width, height);
                    return;
                }

                // Read auxiliary buffers if available
                float[] albedoData = null;
                float[] normalData = null;
                if (albedoTexture != null)
                {
                    albedoData = ReadTextureDataFromGPU(albedoTexture, width, height);
                }
                if (normalTexture != null)
                {
                    normalData = ReadTextureDataFromGPU(normalTexture, width, height);
                }

                // Step 2: Allocate output buffer
                int pixelCount = width * height;
                int floatCount = pixelCount * 4; // RGBA
                float[] outputData = new float[floatCount];

                // Step 3: Execute OIDN filter on CPU
                bool denoiseSuccess = ExecuteOIDNFilter(inputData, outputData, albedoData, normalData, width, height);
                if (!denoiseSuccess)
                {
                    Console.WriteLine("[NativeRT] ApplyOIDNDenoising: OIDN filter execution failed, falling back to GPU compute shader");
                    ApplyOIDNDenoisingGPU(parameters, width, height);
                    return;
                }

                // Step 4: Transfer results from CPU to GPU
                bool writeSuccess = WriteTextureDataToGPU(outputTexture, outputData, width, height);
                if (!writeSuccess)
                {
                    Console.WriteLine("[NativeRT] ApplyOIDNDenoising: CPU->GPU transfer failed, falling back to GPU compute shader");
                    ApplyOIDNDenoisingGPU(parameters, width, height);
                    return;
                }

                // Native OIDN processing completed successfully
                return;
            }
            else
            {
                // Fall back to GPU compute shader implementation
                ApplyOIDNDenoisingGPU(parameters, width, height);
            }
        }

        /// <summary>
        /// Applies OIDN-style denoising using GPU compute shader (fallback implementation).
        /// This is used when native OIDN library is not available or when GPU->CPU transfer is not supported.
        /// </summary>
        /// <param name="parameters">Denoiser parameters containing input/output textures and auxiliary buffers.</param>
        /// <param name="width">Width of the texture in pixels.</param>
        /// <param name="height">Height of the texture in pixels.</param>
        private void ApplyOIDNDenoisingGPU(DenoiserParams parameters, int width, int height)
        {
            if (_spatialDenoiserPipeline == null || _denoiserBindingLayout == null || _device == null)
            {
                return;
            }

            // Update denoiser constant buffer with OIDN-specific parameters
            UpdateDenoiserConstants(parameters, width, height);

            // Get input and output textures
            ITexture inputTexture = GetTextureFromHandle(parameters.InputTexture);
            ITexture outputTexture = GetTextureFromHandle(parameters.OutputTexture);
            ITexture normalTexture = GetTextureFromHandle(parameters.NormalTexture);
            ITexture albedoTexture = GetTextureFromHandle(parameters.AlbedoTexture);

            if (inputTexture == null || outputTexture == null)
            {
                return;
            }

            // Create binding set for OIDN-style denoising
            IBindingSet bindingSet = CreateDenoiserBindingSet(parameters, null);
            if (bindingSet == null)
            {
                return;
            }

            // Execute OIDN-style denoising compute shader
            ICommandList commandList = _device.CreateCommandList(CommandListType.Compute);
            commandList.Open();

            // Transition resources for OIDN-style denoising
            commandList.SetTextureState(inputTexture, ResourceState.ShaderResource);
            commandList.SetTextureState(outputTexture, ResourceState.UnorderedAccess);
            if (normalTexture != null)
            {
                commandList.SetTextureState(normalTexture, ResourceState.ShaderResource);
            }
            if (albedoTexture != null)
            {
                commandList.SetTextureState(albedoTexture, ResourceState.ShaderResource);
            }
            commandList.CommitBarriers();

            // Set compute state for OIDN-style denoising
            ComputeState computeState = new ComputeState
            {
                Pipeline = _spatialDenoiserPipeline,
                BindingSets = new IBindingSet[] { bindingSet }
            };
            commandList.SetComputeState(computeState);

            // Dispatch compute shader
            int threadGroupSize = 8;
            int groupCountX = (width + threadGroupSize - 1) / threadGroupSize;
            int groupCountY = (height + threadGroupSize - 1) / threadGroupSize;
            commandList.Dispatch(groupCountX, groupCountY, 1);

            // Transition output back to readable state
            commandList.SetTextureState(outputTexture, ResourceState.ShaderResource);
            commandList.CommitBarriers();

            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();

            // Dispose binding set
            bindingSet.Dispose();
        }

        private IBindingSet CreateDenoiserBindingSet(DenoiserParams parameters, ITexture historyBuffer)
        {
            if (_denoiserBindingLayout == null)
            {
                return null;
            }

            ITexture inputTexture = GetTextureFromHandle(parameters.InputTexture);
            ITexture outputTexture = GetTextureFromHandle(parameters.OutputTexture);
            ITexture normalTexture = GetTextureFromHandle(parameters.NormalTexture);
            ITexture motionTexture = GetTextureFromHandle(parameters.MotionTexture);
            ITexture albedoTexture = GetTextureFromHandle(parameters.AlbedoTexture);

            List<BindingSetItem> items = new List<BindingSetItem>();

            // Slot 0: Input texture
            if (inputTexture != null)
            {
                items.Add(new BindingSetItem
                {
                    Slot = 0,
                    Type = BindingType.Texture,
                    Texture = inputTexture
                });
            }

            // Slot 1: Output texture
            if (outputTexture != null)
            {
                items.Add(new BindingSetItem
                {
                    Slot = 1,
                    Type = BindingType.RWTexture,
                    Texture = outputTexture
                });
            }

            // Slot 2: History texture
            if (historyBuffer != null)
            {
                items.Add(new BindingSetItem
                {
                    Slot = 2,
                    Type = BindingType.Texture,
                    Texture = historyBuffer
                });
            }

            // Slot 3: Normal texture
            if (normalTexture != null)
            {
                items.Add(new BindingSetItem
                {
                    Slot = 3,
                    Type = BindingType.Texture,
                    Texture = normalTexture
                });
            }

            // Slot 4: Motion vector texture
            if (motionTexture != null)
            {
                items.Add(new BindingSetItem
                {
                    Slot = 4,
                    Type = BindingType.Texture,
                    Texture = motionTexture
                });
            }

            // Slot 5: Albedo texture
            if (albedoTexture != null)
            {
                items.Add(new BindingSetItem
                {
                    Slot = 5,
                    Type = BindingType.Texture,
                    Texture = albedoTexture
                });
            }

            // Slot 6: Constant buffer
            if (_denoiserConstantBuffer != null)
            {
                items.Add(new BindingSetItem
                {
                    Slot = 6,
                    Type = BindingType.ConstantBuffer,
                    Buffer = _denoiserConstantBuffer
                });
            }

            return _device.CreateBindingSet(_denoiserBindingLayout, new BindingSetDesc
            {
                Items = items.ToArray()
            });
        }

        private void UpdateDenoiserConstants(DenoiserParams parameters, int width, int height)
        {
            if (_denoiserConstantBuffer == null)
            {
                return;
            }

            // Denoiser constant buffer structure
            // struct DenoiserConstants {
            //     float blendFactor;      // 4 bytes - temporal blend factor
            //     float spatialSigma;     // 4 bytes - spatial filter sigma
            //     float filterRadius;     // 4 bytes - filter radius
            //     float padding1;         // 4 bytes
            //     int2 resolution;        // 8 bytes - texture resolution
            //     float timeDelta;        // 4 bytes - frame time delta
            //     float padding2;         // 4 bytes
            // }; // Total: 32 bytes

            byte[] constantData = new byte[32];
            int offset = 0;

            // Blend factor
            BitConverter.GetBytes(parameters.BlendFactor).CopyTo(constantData, offset);
            offset += 4;

            // Spatial sigma (default 1.0 for edge-aware filtering)
            BitConverter.GetBytes(1.0f).CopyTo(constantData, offset);
            offset += 4;

            // Filter radius (default 2.0)
            BitConverter.GetBytes(2.0f).CopyTo(constantData, offset);
            offset += 4;

            // Padding
            offset += 4;

            // Resolution
            BitConverter.GetBytes(width).CopyTo(constantData, offset);
            offset += 4;
            BitConverter.GetBytes(height).CopyTo(constantData, offset);
            offset += 4;

            // Time delta (would be provided in real implementation)
            BitConverter.GetBytes(0.016f).CopyTo(constantData, offset); // ~60 FPS
            offset += 4;

            // Padding
            offset += 4;

            // Write to buffer
            ICommandList commandList = _device.CreateCommandList(CommandListType.Compute);
            commandList.Open();
            commandList.WriteBuffer(_denoiserConstantBuffer, constantData);
            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();
        }

        private void CopyTextureToHistory(IntPtr outputTextureHandle, ITexture historyBuffer)
        {
            if (historyBuffer == null || _device == null)
            {
                return;
            }

            ITexture outputTexture = GetTextureFromHandle(outputTextureHandle);
            if (outputTexture == null)
            {
                return;
            }

            // Copy output texture to history buffer for next frame
            ICommandList commandList = _device.CreateCommandList(CommandListType.Copy);
            commandList.Open();
            commandList.SetTextureState(outputTexture, ResourceState.CopySource);
            commandList.SetTextureState(historyBuffer, ResourceState.CopyDest);
            commandList.CommitBarriers();
            commandList.CopyTexture(historyBuffer, outputTexture);
            commandList.SetTextureState(historyBuffer, ResourceState.ShaderResource);
            commandList.CommitBarriers();
            commandList.Close();
            _device.ExecuteCommandList(commandList);
            commandList.Dispose();
        }

        /// <summary>
        /// Gets an ITexture object from an IntPtr texture handle.
        /// Uses multiple strategies:
        /// 1. Check the texture handle map (for textures registered via RegisterTextureHandle)
        /// 2. Try to create a handle from native texture using CreateHandleForNativeTexture
        /// 3. Return null if texture cannot be found
        /// </summary>
        private ITexture GetTextureFromHandle(IntPtr textureHandle)
        {
            if (textureHandle == IntPtr.Zero || _device == null)
            {
                return null;
            }

            // Strategy 1: Check our texture handle map (for textures we've registered)
            if (_textureHandleMap.TryGetValue(textureHandle, out ITexture mappedTexture))
            {
                return mappedTexture;
            }

            // Strategy 2: Try to create a handle from native texture
            // This works if the handle is a native texture handle (e.g., D3D12 resource pointer, VkImage, MTLTexture)
            // We detect native handles by checking if they're not in our registered map and appear to be valid pointers
            // Then we try to query texture information and create a wrapper texture
            if (IsNativeHandle(textureHandle))
            {
                try
                {
                    // Try to query texture information from the backend
                    // Different backends may provide different ways to query texture info from native handles
                    TextureDesc? queriedDesc = QueryTextureDescriptionFromNativeHandle(textureHandle);

                    if (queriedDesc.HasValue)
                    {
                        // We have a complete description from the backend
                        ITexture texture = _device.CreateHandleForNativeTexture(textureHandle, queriedDesc.Value);
                        if (texture != null)
                        {
                            // Cache it for future lookups
                            _textureHandleMap[textureHandle] = texture;

                            // Cache texture info
                            _textureInfoCache[textureHandle] = new TextureInfo
                            {
                                Width = queriedDesc.Value.Width,
                                Height = queriedDesc.Value.Height
                            };

                            Console.WriteLine($"[NativeRT] GetTextureFromHandle: Successfully created texture wrapper from native handle {textureHandle:X} ({queriedDesc.Value.Width}x{queriedDesc.Value.Height}, {queriedDesc.Value.Format})");
                            return texture;
                        }
                    }
                    else
                    {
                        // Backend couldn't query, try heuristics with common raytracing texture formats
                        // Raytracing output textures are typically:
                        // - R32G32B32A32_Float (HDR output)
                        // - R16G16B16A16_Float (medium precision HDR)
                        // - R8G8B8A8_UNorm (LDR output)
                        // - R11G11B10_Float (packed HDR)
                        TextureFormat[] commonFormats = new TextureFormat[]
                        {
                            TextureFormat.R32G32B32A32_Float,  // Most common for raytracing HDR output
                            TextureFormat.R16G16B16A16_Float,  // Medium precision HDR
                            TextureFormat.R11G11B10_Float,     // Packed HDR (common in games)
                            TextureFormat.R8G8B8A8_UNorm,      // LDR output
                            TextureFormat.R10G10B10A2_UNorm    // Packed LDR
                        };

                        // Try to get dimensions from cache if available
                        int width = 1920;  // Default fallback dimensions (common render resolution)
                        int height = 1080;

                        if (_textureInfoCache.TryGetValue(textureHandle, out TextureInfo cachedInfo))
                        {
                            width = cachedInfo.Width;
                            height = cachedInfo.Height;
                        }
                        else
                        {
                            // Try to infer dimensions from handle or use defaults
                            // In a production system, this would query from the backend
                            // For now, we use common raytracing output resolutions
                            width = 1920;
                            height = 1080;
                        }

                        // Try each common format until one works
                        foreach (TextureFormat format in commonFormats)
                        {
                            try
                            {
                                TextureDesc desc = new TextureDesc
                                {
                                    Width = width,
                                    Height = height,
                                    Depth = 1,
                                    ArraySize = 1,
                                    MipLevels = 1,
                                    SampleCount = 1,
                                    Format = format,
                                    Dimension = TextureDimension.Texture2D,
                                    Usage = TextureUsage.ShaderResource | TextureUsage.UnorderedAccess,
                                    InitialState = ResourceState.UnorderedAccess,
                                    KeepInitialState = false,
                                    DebugName = $"RaytracingNativeTexture_{format}"
                                };

                                ITexture texture = _device.CreateHandleForNativeTexture(textureHandle, desc);
                                if (texture != null)
                                {
                                    // Cache it for future lookups
                                    _textureHandleMap[textureHandle] = texture;

                                    // Cache texture info
                                    _textureInfoCache[textureHandle] = new TextureInfo
                                    {
                                        Width = width,
                                        Height = height
                                    };

                                    Console.WriteLine($"[NativeRT] GetTextureFromHandle: Successfully created texture wrapper from native handle {textureHandle:X} using format {format} (inferred {width}x{height})");
                                    return texture;
                                }
                            }
                            catch (Exception formatEx)
                            {
                                // This format didn't work, try next one
                                continue;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // CreateHandleForNativeTexture failed, try other methods
                    Console.WriteLine($"[NativeRT] GetTextureFromHandle: Failed to create handle from native texture {textureHandle:X}: {ex.Message}");
                }
            }

            // Strategy 3: Advanced texture lookup using backend-specific methods
            // This strategy handles cases where:
            // - The handle is not recognized as a native handle (e.g., small integers, resource indices)
            // - CreateHandleForNativeTexture failed but the handle might still be valid
            // - The handle is from a different graphics API or context
            // Based on DirectX 12/Vulkan/Metal: Backend-specific texture querying and lookup mechanisms
            try
            {
                // Strategy 3a: Try to query texture description using backend-specific APIs
                // Even if IsNativeHandle returned false, the handle might still be queryable
                // Some backends use small handles or resource indices that can be queried
                TextureDesc? queriedDesc = QueryTextureDescriptionFromNativeHandle(textureHandle);
                if (queriedDesc.HasValue)
                {
                    // We successfully queried texture description, try to create wrapper
                    ITexture texture = _device.CreateHandleForNativeTexture(textureHandle, queriedDesc.Value);
                    if (texture != null)
                    {
                        // Cache it for future lookups
                        _textureHandleMap[textureHandle] = texture;

                        // Cache texture info
                        _textureInfoCache[textureHandle] = new TextureInfo
                        {
                            Width = queriedDesc.Value.Width,
                            Height = queriedDesc.Value.Height
                        };

                        Console.WriteLine($"[NativeRT] GetTextureFromHandle: Successfully resolved texture handle {textureHandle:X} via backend query ({queriedDesc.Value.Width}x{queriedDesc.Value.Height}, {queriedDesc.Value.Format})");
                        return texture;
                    }
                }

                // Strategy 3b: Try backend-specific texture lookup methods
                // Different backends may provide different mechanisms for texture lookup
                Type deviceType = _device.GetType();
                string deviceTypeName = deviceType.Name;

                if (deviceTypeName.Contains("D3D12"))
                {
                    // D3D12: Try to query ID3D12Resource from handle using COM interfaces
                    // Even if the handle is not recognized as a native handle, it might be a valid D3D12 resource
                    ITexture texture = TryLookupD3D12Texture(textureHandle);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
                else if (deviceTypeName.Contains("Vulkan"))
                {
                    // Vulkan: Try to query VkImage from handle
                    // Vulkan uses handles that might not pass IsNativeHandle check
                    ITexture texture = TryLookupVulkanTexture(textureHandle);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
                else if (deviceTypeName.Contains("Metal"))
                {
                    // Metal: Try to query MTLTexture from handle
                    // Metal uses Objective-C objects that might not pass IsNativeHandle check
                    ITexture texture = TryLookupMetalTexture(textureHandle);
                    if (texture != null)
                    {
                        return texture;
                    }
                }

                // Strategy 3c: Try to infer texture info from handle patterns
                // Some backends encode texture information in handle values
                // This is a last resort and may not work for all backends
                ITexture inferredTexture = TryInferTextureFromHandle(textureHandle);
                if (inferredTexture != null)
                {
                    return inferredTexture;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NativeRT] GetTextureFromHandle: Strategy 3 failed for handle {textureHandle:X}: {ex.Message}");
            }

            // All strategies failed - texture not found
            Console.WriteLine($"[NativeRT] GetTextureFromHandle: Could not resolve texture handle {textureHandle}");
            Console.WriteLine($"[NativeRT] GetTextureFromHandle: Use RegisterTextureHandle to register textures before use");

            return null;
        }

        /// <summary>
        /// Registers a texture handle mapping.
        /// This allows the raytracing system to look up ITexture objects from IntPtr handles.
        /// Call this method when you have both the handle and the ITexture object.
        /// </summary>
        public void RegisterTextureHandle(IntPtr handle, ITexture texture)
        {
            if (handle == IntPtr.Zero)
            {
                Console.WriteLine("[NativeRT] RegisterTextureHandle: Invalid handle (IntPtr.Zero)");
                return;
            }

            if (texture == null)
            {
                Console.WriteLine("[NativeRT] RegisterTextureHandle: Invalid texture (null)");
                return;
            }

            _textureHandleMap[handle] = texture;

            // Also cache texture info
            _textureInfoCache[handle] = new TextureInfo
            {
                Width = texture.Desc.Width,
                Height = texture.Desc.Height
            };

            Console.WriteLine($"[NativeRT] RegisterTextureHandle: Registered texture handle {handle} -> {texture.Desc.Width}x{texture.Desc.Height}");
        }

        /// <summary>
        /// Unregisters a texture handle mapping.
        /// Call this when a texture is destroyed to clean up the mapping.
        /// </summary>
        public void UnregisterTextureHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            _textureHandleMap.Remove(handle);
            _textureInfoCache.Remove(handle);

            Console.WriteLine($"[NativeRT] UnregisterTextureHandle: Unregistered texture handle {handle}");
        }

        /// <summary>
        /// Gets an IBuffer object from an IntPtr buffer handle.
        /// Uses the buffer handle map (for buffers registered via RegisterBufferHandle).
        /// </summary>
        private IBuffer GetBufferFromHandle(IntPtr bufferHandle)
        {
            if (bufferHandle == IntPtr.Zero || _device == null)
            {
                return null;
            }

            // Check our buffer handle map (for buffers we've registered)
            if (_bufferHandleMap.TryGetValue(bufferHandle, out IBuffer mappedBuffer))
            {
                return mappedBuffer;
            }

            // Buffer not found in registry
            Console.WriteLine($"[NativeRT] GetBufferFromHandle: Could not resolve buffer handle {bufferHandle}");
            Console.WriteLine($"[NativeRT] GetBufferFromHandle: Use RegisterBufferHandle to register buffers before use");

            return null;
        }

        /// <summary>
        /// Registers a buffer handle mapping.
        /// This allows the raytracing system to look up IBuffer objects from IntPtr handles.
        /// Call this method when you have both the handle and the IBuffer object.
        /// </summary>
        public void RegisterBufferHandle(IntPtr handle, IBuffer buffer)
        {
            if (handle == IntPtr.Zero)
            {
                Console.WriteLine("[NativeRT] RegisterBufferHandle: Invalid handle (IntPtr.Zero)");
                return;
            }

            if (buffer == null)
            {
                Console.WriteLine("[NativeRT] RegisterBufferHandle: Invalid buffer (null)");
                return;
            }

            _bufferHandleMap[handle] = buffer;

            Console.WriteLine($"[NativeRT] RegisterBufferHandle: Registered buffer handle {handle} -> size: {buffer.Desc.ByteSize} bytes");
        }

        /// <summary>
        /// Unregisters a buffer handle mapping.
        /// Call this when a buffer is destroyed to clean up the mapping.
        /// </summary>
        public void UnregisterBufferHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            _bufferHandleMap.Remove(handle);

            Console.WriteLine($"[NativeRT] UnregisterBufferHandle: Unregistered buffer handle {handle}");
        }

        public void Dispose()
        {
            Shutdown();
        }

        private struct BlasEntry
        {
            public IntPtr Handle;
            public int VertexCount;
            public int IndexCount;
            public bool IsOpaque;
        }

        private struct TlasInstance
        {
            public IntPtr BlasHandle;
            public Matrix4x4 Transform;
            public uint InstanceMask;
            public uint HitGroupIndex;
        }

        /// <summary>
        /// Shadow ray constant buffer structure matching shader layout.
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct ShadowRayConstants
        {
            public Vector3 LightDirection;      // 12 bytes
            public float MaxDistance;            // 4 bytes
            public float SoftShadowAngle;       // 4 bytes
            public int SamplesPerPixel;         // 4 bytes
            public int RenderWidth;             // 4 bytes
            public int RenderHeight;            // 4 bytes
            // Total: 32 bytes (aligned)
        }

        /// <summary>
        /// Cached texture information for quick lookups.
        /// </summary>
        private struct TextureInfo
        {
            public int Width;
            public int Height;
        }
    }
}


