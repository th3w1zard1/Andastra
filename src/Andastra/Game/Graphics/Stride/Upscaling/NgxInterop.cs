using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Andastra.Game.Stride.Upscaling
{
    /// <summary>
    /// Native interop declarations for NVIDIA NGX SDK (Neural Graphics Framework).
    /// Provides P/Invoke bindings for DLSS, DLAA, and other NGX features.
    ///
    /// Based on NVIDIA NGX SDK API:
    /// - NVSDK_NGX_Init
    /// - NVSDK_NGX_CreateFeature
    /// - NVSDK_NGX_EvaluateFeature
    /// - NVSDK_NGX_ReleaseFeature
    /// - NVSDK_NGX_Shutdown
    ///
    /// Documentation: https://developer.nvidia.com/dlss
    /// </summary>
    internal static class NgxInterop
    {
        #region Constants

        /// <summary>
        /// Application ID for Andastra - must be unique per application using NGX.
        /// </summary>
        public const string ApplicationId = "Andastra.NET";

        /// <summary>
        /// Engine type identifier for custom engines.
        /// </summary>
        public const uint EngineType = 0x00000001; // NVSDK_NGX_ENGINE_TYPE_CUSTOM

        /// <summary>
        /// Maximum path length for NGX logging directory.
        /// </summary>
        private const int MaxPathLength = 260;

        #endregion

        #region Enums

        /// <summary>
        /// NGX result codes.
        /// </summary>
        public enum NgxResult
        {
            Success = 0,
            Fail = -1,
            InvalidParameter = -2,
            OutOfMemory = -3,
            IncompatibleStructureVersion = -4,
            FeatureNotFound = -5,
            InvalidFeature = -6,
            IncompatibleParameter = -7,
            FeatureAlreadyExists = -8,
            UnsupportedInputFormat = -9,
            IncompleteInput = -10
        }

        /// <summary>
        /// NGX feature identifier.
        /// </summary>
        public enum NgxFeature
        {
            DLSS = 0x00000001,
            DLAA = 0x00000002,
            RealTimeDenoiser = 0x00000003
        }

        /// <summary>
        /// NGX rendering backend type.
        /// </summary>
        public enum NgxBackend
        {
            DirectX11 = 0,
            DirectX12 = 1,
            Vulkan = 2
        }

        /// <summary>
        /// DLSS quality/performance mode.
        /// </summary>
        public enum NgxDlssMode
        {
            Off = 0,
            MaxQuality = 1,
            Quality = 2,
            Balanced = 3,
            Performance = 4,
            UltraPerformance = 5,
            DLAA = 6
        }

        #endregion

        #region Structures

        /// <summary>
        /// NGX initialization parameters.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NgxInitParameters
        {
            public uint Version;
            public IntPtr ApplicationId; // UTF-8 string
            public IntPtr LogDirectory; // UTF-8 string, can be null
            public uint FeatureInfoWidthInPixels;
            public uint FeatureInfoHeightInPixels;
            public uint Flags; // Reserved, must be 0
        }

        /// <summary>
        /// NGX feature parameters.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NgxFeatureParameters
        {
            public uint Version;
            public uint Flags; // Feature-specific flags
            public IntPtr InWidth;
            public IntPtr InHeight;
            public IntPtr InTargetWidth;
            public IntPtr InTargetHeight;
            public IntPtr InPerfQualityValue; // Quality mode pointer
            public IntPtr InFeatureCreateFlags; // DLSS-specific flags
            public IntPtr InEnableOutputSubrects; // For tiled rendering
        }

        /// <summary>
        /// NGX DLSS creation parameters.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NgxDlssCreateParameters
        {
            public uint FeatureCommandListSize;
            public IntPtr FeatureCommandList;
            public uint FeatureDescriptorHeapSize;
            public IntPtr FeatureDescriptorHeap;
            public uint FeatureInitParams;
            public IntPtr FeatureInitParamsPtr;
        }

        /// <summary>
        /// NGX DLSS evaluation parameters.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NgxDlssEvaluateParameters
        {
            public uint FeatureCommandListSize;
            public IntPtr FeatureCommandList;
            public uint FeatureDescriptorHeapSize;
            public IntPtr FeatureDescriptorHeap;
            public IntPtr InColor; // Input color texture SRV
            public IntPtr InMotionVectors; // Motion vectors SRV
            public IntPtr InDepth; // Depth buffer SRV
            public IntPtr InTranslucency; // Optional translucency SRV
            public IntPtr InExposure; // Optional exposure texture SRV
            public IntPtr InBiasCurrentColorMask; // Optional bias mask SRV
            public IntPtr OutColor; // Output color texture UAV
            public uint InJitterOffsetX; // Subpixel jitter offset X
            public uint InJitterOffsetY; // Subpixel jitter offset Y
            public float InReset; // 1.0f to reset history, 0.0f otherwise
            public float InSharpness; // Sharpness value (0.0-1.0)
            public float InFrameTimeDeltaInMsec; // Frame time delta in milliseconds
            public float InPreExposure; // Pre-exposure value
            public uint InMVScaleX; // Motion vector scale X
            public uint InMVScaleY; // Motion vector scale Y
            public IntPtr InColorSubrectBase; // Subrect base for tiled rendering
            public IntPtr InDepthSubrectBase;
            public IntPtr InMVSubrectBase;
            public IntPtr InTranslucencySubrectBase;
            public IntPtr InOutputSubrectBase;
        }

        /// <summary>
        /// NGX feature info structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NgxFeatureInfo
        {
            public uint Version;
            public uint FeatureID;
            public IntPtr FeatureName; // Output: feature name
            public uint FeatureNameLength;
        }

        #endregion

        #region Native DLL Imports

        private const string NgxDllName = "nvngx.dll";

        /// <summary>
        /// Initialize NVIDIA NGX SDK.
        /// </summary>
        /// <param name="applicationId">UTF-8 application identifier</param>
        /// <param name="logDirectory">UTF-8 path to log directory (can be null)</param>
        /// <param name="device">Native graphics device pointer (D3D12 device, D3D11 device, or VkDevice)</param>
        /// <param name="backend">Rendering backend type</param>
        /// <param name="initParams">Optional initialization parameters (can be null)</param>
        /// <returns>NGX result code</returns>
        [DllImport(NgxDllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "NVSDK_NGX_Init")]
        public static extern NgxResult NgxInit(
            [MarshalAs(UnmanagedType.LPStr)] string applicationId,
            [MarshalAs(UnmanagedType.LPStr)] string logDirectory,
            IntPtr device,
            NgxBackend backend,
            IntPtr initParams);

        /// <summary>
        /// Create an NGX feature (DLSS, DLAA, etc.).
        /// </summary>
        /// <param name="featureId">Feature identifier</param>
        /// <param name="createParams">Feature creation parameters</param>
        /// <param name="featureHandle">Output: handle to created feature</param>
        /// <returns>NGX result code</returns>
        [DllImport(NgxDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NVSDK_NGX_CreateFeature")]
        public static extern NgxResult NgxCreateFeature(
            IntPtr commandList,
            NgxFeature featureId,
            ref NgxFeatureParameters featureParams,
            out IntPtr featureHandle);

        /// <summary>
        /// Evaluate/execute an NGX feature (run DLSS, DLAA, etc.).
        /// </summary>
        /// <param name="commandList">Command list for GPU commands</param>
        /// <param name="featureHandle">Handle to feature created with NgxCreateFeature</param>
        /// <param name="evaluateParams">Evaluation parameters</param>
        /// <returns>NGX result code</returns>
        [DllImport(NgxDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NVSDK_NGX_EvaluateFeature")]
        public static extern NgxResult NgxEvaluateFeature(
            IntPtr commandList,
            IntPtr featureHandle,
            ref NgxDlssEvaluateParameters evaluateParams);

        /// <summary>
        /// Release/destroy an NGX feature.
        /// </summary>
        /// <param name="featureHandle">Handle to feature to release</param>
        /// <returns>NGX result code</returns>
        [DllImport(NgxDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NVSDK_NGX_ReleaseFeature")]
        public static extern NgxResult NgxReleaseFeature(IntPtr featureHandle);

        /// <summary>
        /// Shutdown NGX SDK and release resources.
        /// </summary>
        /// <returns>NGX result code</returns>
        [DllImport(NgxDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NVSDK_NGX_Shutdown")]
        public static extern NgxResult NgxShutdown();

        /// <summary>
        /// Get feature requirements and capabilities.
        /// </summary>
        /// <param name="featureId">Feature identifier</param>
        /// <param name="featureInfo">Output: feature information</param>
        /// <returns>NGX result code</returns>
        [DllImport(NgxDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NVSDK_NGX_GetFeatureRequirements")]
        public static extern NgxResult NgxGetFeatureRequirements(
            IntPtr device,
            NgxBackend backend,
            NgxFeature featureId,
            ref NgxFeatureInfo featureInfo);

        /// <summary>
        /// Get DLSS optimal render resolution for target output resolution and quality mode.
        /// </summary>
        /// <param name="targetWidth">Target output width in pixels</param>
        /// <param name="targetHeight">Target output height in pixels</param>
        /// <param name="perfQualityValue">Quality mode (0=Off, 1=MaxQuality, 2=Quality, 3=Balanced, 4=Performance, 5=UltraPerformance)</param>
        /// <param name="optimalRenderWidth">Output: optimal render width</param>
        /// <param name="optimalRenderHeight">Output: optimal render height</param>
        /// <param name="maxRenderWidth">Output: maximum supported render width</param>
        /// <param name="maxRenderHeight">Output: maximum supported render height</param>
        /// <param name="minRenderWidth">Output: minimum supported render width</param>
        /// <param name="minRenderHeight">Output: minimum supported render height</param>
        /// <returns>NGX result code</returns>
        [DllImport(NgxDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NVSDK_NGX_DLSS_GetOptimalSettings")]
        public static extern NgxResult NgxDlssGetOptimalSettings(
            uint targetWidth,
            uint targetHeight,
            int perfQualityValue,
            out uint optimalRenderWidth,
            out uint optimalRenderHeight,
            out uint maxRenderWidth,
            out uint maxRenderHeight,
            out uint minRenderWidth,
            out uint minRenderHeight);

        /// <summary>
        /// Check if DLSS is supported on the current hardware.
        /// </summary>
        /// <param name="backend">Rendering backend</param>
        /// <param name="minDriverVersionMajor">Output: minimum required driver version major</param>
        /// <param name="minDriverVersionMinor">Output: minimum required driver version minor</param>
        /// <returns>True if DLSS is supported, false otherwise</returns>
        [DllImport(NgxDllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "NVSDK_NGX_DLSS_GetCapabilityParameters")]
        public static extern NgxResult NgxDlssGetCapabilityParameters(
            IntPtr device,
            NgxBackend backend,
            out int isDlssSupported,
            out int minDriverVersionMajor,
            out int minDriverVersionMinor);

        #endregion

        #region COM Interface GUIDs

        /// <summary>
        /// COM interface GUID for ID3D11Device (DirectX 11 device interface).
        /// Used for QueryInterface to detect DirectX 11 backend.
        /// GUID: {db6f6ddb-ac77-4e88-8253-819df9bbf140}
        /// </summary>
        private static readonly Guid IID_ID3D11Device = new Guid(0xdb6f6ddb, 0xac77, 0x4e88, 0x82, 0x53, 0x81, 0x9d, 0xf9, 0xbb, 0xf1, 0x40);

        /// <summary>
        /// COM interface GUID for ID3D12Device (DirectX 12 device interface).
        /// Used for QueryInterface to detect DirectX 12 backend.
        /// GUID: {189819f1-1db6-4b57-be54-1821339b85f7}
        /// </summary>
        private static readonly Guid IID_ID3D12Device = new Guid(0x189819f1, 0x1db6, 0x4b57, 0xbe, 0x54, 0x18, 0x21, 0x33, 0x9b, 0x85, 0xf7);

        /// <summary>
        /// COM interface method delegate for IUnknown::QueryInterface.
        /// Used to query COM objects for specific interface types.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int QueryInterfaceDelegate(IntPtr comObject, ref Guid riid, out IntPtr ppvObject);

        #endregion

        #region Helper Methods

        /// <summary>
        /// Convert Andastra DlssMode to NGX DLSS mode value.
        /// </summary>
        public static int DlssModeToNgxValue(Runtime.Graphics.Common.Enums.DlssMode mode)
        {
            switch (mode)
            {
                case Runtime.Graphics.Common.Enums.DlssMode.Off:
                    return (int)NgxDlssMode.Off;
                case Runtime.Graphics.Common.Enums.DlssMode.DLAA:
                    return (int)NgxDlssMode.DLAA;
                case Runtime.Graphics.Common.Enums.DlssMode.Quality:
                    return (int)NgxDlssMode.Quality;
                case Runtime.Graphics.Common.Enums.DlssMode.Balanced:
                    return (int)NgxDlssMode.Balanced;
                case Runtime.Graphics.Common.Enums.DlssMode.Performance:
                    return (int)NgxDlssMode.Performance;
                case Runtime.Graphics.Common.Enums.DlssMode.UltraPerformance:
                    return (int)NgxDlssMode.UltraPerformance;
                default:
                    return (int)NgxDlssMode.Off;
            }
        }

        /// <summary>
        /// Get NGX backend type from Stride GraphicsDevice.
        /// Detects the underlying graphics API (DirectX 11, DirectX 12, or Vulkan) using multiple detection strategies.
        /// 
        /// Detection Strategy (in order of priority):
        /// 1. Property-based detection: Checks for backend-specific properties (D3D12Device, D3D11Device, VkDevice, etc.)
        /// 2. COM QueryInterface detection: Uses NativeDevice with COM QueryInterface to identify DirectX device types
        /// 3. Type name analysis: Examines device type name for backend hints (D3D12, D3D11, Vulkan)
        /// 4. Fallback: Returns DirectX12 if all detection methods fail (most common backend for DLSS)
        /// 
        /// Based on Stride Graphics API: GraphicsDevice exposes backend-specific properties via reflection.
        /// Based on NVIDIA NGX SDK documentation: Each backend requires different initialization functions.
        /// Backend type must match the actual graphics API in use for proper NGX initialization.
        /// </summary>
        /// <param name="device">Stride GraphicsDevice to query</param>
        /// <returns>Detected NGX backend type, or DirectX12 as fallback if detection fails</returns>
        public static NgxBackend GetNgxBackend(global::Stride.Graphics.GraphicsDevice device)
        {
            if (device == null)
            {
                // Null device - return default fallback
                return NgxBackend.DirectX12;
            }

            try
            {
                Type deviceType = device.GetType();
                string deviceTypeName = deviceType.FullName ?? deviceType.Name;

                // Strategy 1: Check for backend-specific device properties via reflection
                // DirectX 12 devices expose a "D3D12Device" property
                PropertyInfo d3d12DeviceProperty = deviceType.GetProperty("D3D12Device", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (d3d12DeviceProperty != null)
                {
                    object d3d12DeviceValue = d3d12DeviceProperty.GetValue(device);
                    if (d3d12DeviceValue != null && !(d3d12DeviceValue is IntPtr && (IntPtr)d3d12DeviceValue == IntPtr.Zero))
                    {
                        // Property exists and has a valid value - this is a DirectX 12 device
                        return NgxBackend.DirectX12;
                    }
                }

                // Check for Vulkan backend properties
                PropertyInfo vkDeviceProperty = deviceType.GetProperty("VkDevice", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (vkDeviceProperty != null)
                {
                    object vkDeviceValue = vkDeviceProperty.GetValue(device);
                    if (vkDeviceValue != null && !(vkDeviceValue is IntPtr && (IntPtr)vkDeviceValue == IntPtr.Zero))
                    {
                        return NgxBackend.Vulkan;
                    }
                }

                PropertyInfo vulkanDeviceProperty = deviceType.GetProperty("VulkanDevice", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (vulkanDeviceProperty != null)
                {
                    object vulkanDeviceValue = vulkanDeviceProperty.GetValue(device);
                    if (vulkanDeviceValue != null && !(vulkanDeviceValue is IntPtr && (IntPtr)vulkanDeviceValue == IntPtr.Zero))
                    {
                        return NgxBackend.Vulkan;
                    }
                }

                // Check for DirectX 11 backend property
                PropertyInfo d3d11DeviceProperty = deviceType.GetProperty("D3D11Device", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (d3d11DeviceProperty != null)
                {
                    object d3d11DeviceValue = d3d11DeviceProperty.GetValue(device);
                    if (d3d11DeviceValue != null && !(d3d11DeviceValue is IntPtr && (IntPtr)d3d11DeviceValue == IntPtr.Zero))
                    {
                        return NgxBackend.DirectX11;
                    }
                }

                // Strategy 2: Use COM QueryInterface on NativeDevice to detect DirectX backend type
                // This is more reliable than property checks as it directly queries the COM interface
                PropertyInfo nativeDeviceProperty = deviceType.GetProperty("NativeDevice", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (nativeDeviceProperty != null)
                {
                    object nativeDeviceValue = nativeDeviceProperty.GetValue(device);
                    if (nativeDeviceValue is IntPtr nativeDevice && nativeDevice != IntPtr.Zero)
                    {
                        // Try to query for ID3D12Device interface (DirectX 12)
                        NgxBackend comDetectedBackend = DetectBackendViaComQueryInterface(nativeDevice);
                        if (comDetectedBackend != NgxBackend.DirectX12) // If not fallback, return detected backend
                        {
                            return comDetectedBackend;
                        }
                    }
                }

                // Strategy 3: Analyze device type name for backend hints
                // Stride backend implementations often have backend names in their type names
                if (deviceTypeName.IndexOf("D3D12", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    deviceTypeName.IndexOf("Direct3D12", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    deviceTypeName.IndexOf("DirectX12", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return NgxBackend.DirectX12;
                }

                if (deviceTypeName.IndexOf("D3D11", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    deviceTypeName.IndexOf("Direct3D11", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    deviceTypeName.IndexOf("DirectX11", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return NgxBackend.DirectX11;
                }

                if (deviceTypeName.IndexOf("Vulkan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    deviceTypeName.IndexOf("Vk", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return NgxBackend.Vulkan;
                }

                // Strategy 4: Fallback to DirectX 12
                // DirectX 12 is the most common backend for DLSS on Windows and provides the best feature support
                // This fallback ensures the function always returns a valid backend type even if detection fails
                // DLSS is primarily used on Windows with DirectX 12, making this a reasonable default
                return NgxBackend.DirectX12;
            }
            catch (Exception ex)
            {
                // If detection fails for any reason, log and return default fallback
                // This ensures the function never throws and always returns a valid backend type
                System.Console.WriteLine($"[NgxInterop] Exception detecting backend type: {ex.Message}");
                return NgxBackend.DirectX12;
            }
        }

        /// <summary>
        /// Detects graphics backend type by using COM QueryInterface on a native device pointer.
        /// Attempts to query for ID3D12Device and ID3D11Device interfaces to determine the backend.
        /// </summary>
        /// <param name="nativeDevice">Native device pointer (should be a COM interface pointer)</param>
        /// <returns>Detected backend type, or DirectX12 as fallback if detection fails</returns>
        private static NgxBackend DetectBackendViaComQueryInterface(IntPtr nativeDevice)
        {
            if (nativeDevice == IntPtr.Zero)
            {
                return NgxBackend.DirectX12; // Fallback
            }

            try
            {
                // Get the COM vtable pointer (first field of COM object)
                IntPtr vtable = Marshal.ReadIntPtr(nativeDevice);
                if (vtable == IntPtr.Zero)
                {
                    // Not a valid COM interface - could be Vulkan (non-COM) or invalid pointer
                    return NgxBackend.DirectX12; // Fallback
                }

                // Get QueryInterface function pointer from vtable (index 0)
                IntPtr queryInterfacePtr = Marshal.ReadIntPtr(vtable, 0);
                if (queryInterfacePtr == IntPtr.Zero)
                {
                    return NgxBackend.DirectX12; // Fallback
                }

                // Create delegate for QueryInterface
                QueryInterfaceDelegate queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(queryInterfacePtr);

                // Try to query for ID3D12Device interface (DirectX 12)
                IntPtr d3d12Device;
                Guid riidD3d12 = IID_ID3D12Device;
                int hr = queryInterface(nativeDevice, ref riidD3d12, out d3d12Device);
                if (hr >= 0 && d3d12Device != IntPtr.Zero) // S_OK or S_FALSE indicates success
                {
                    // QueryInterface succeeded - this is a DirectX 12 device
                    // Release the queried interface (we only needed it for detection)
                    Marshal.Release(d3d12Device);
                    return NgxBackend.DirectX12;
                }

                // Try to query for ID3D11Device interface (DirectX 11)
                IntPtr d3d11Device;
                Guid riidD3d11 = IID_ID3D11Device;
                hr = queryInterface(nativeDevice, ref riidD3d11, out d3d11Device);
                if (hr >= 0 && d3d11Device != IntPtr.Zero) // S_OK or S_FALSE indicates success
                {
                    // QueryInterface succeeded - this is a DirectX 11 device
                    // Release the queried interface (we only needed it for detection)
                    Marshal.Release(d3d11Device);
                    return NgxBackend.DirectX11;
                }

                // Neither DirectX 11 nor DirectX 12 interface found
                // Could be Vulkan (non-COM), or an unsupported backend
                // Return fallback
                return NgxBackend.DirectX12;
            }
            catch (Exception ex)
            {
                // COM QueryInterface failed - return fallback
                System.Console.WriteLine($"[NgxInterop] Exception during COM QueryInterface backend detection: {ex.Message}");
                return NgxBackend.DirectX12;
            }
        }

        /// <summary>
        /// Check if NGX DLL is available and can be loaded.
        /// </summary>
        public static bool IsNgxAvailable()
        {
            try
            {
                // Try to load the DLL to check if it's available
                IntPtr hModule = LoadLibrary(NgxDllName);
                if (hModule != IntPtr.Zero)
                {
                    FreeLibrary(hModule);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);

        #endregion
    }
}

