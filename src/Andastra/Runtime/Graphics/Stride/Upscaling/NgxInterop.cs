using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Andastra.Runtime.Stride.Upscaling
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

        #region Helper Methods

        /// <summary>
        /// Convert Andastra DlssMode to NGX DLSS mode value.
        /// </summary>
        public static int DlssModeToNgxValue(Andastra.Runtime.Graphics.Common.Enums.DlssMode mode)
        {
            switch (mode)
            {
                case Andastra.Runtime.Graphics.Common.Enums.DlssMode.Off:
                    return (int)NgxDlssMode.Off;
                case Andastra.Runtime.Graphics.Common.Enums.DlssMode.DLAA:
                    return (int)NgxDlssMode.DLAA;
                case Andastra.Runtime.Graphics.Common.Enums.DlssMode.Quality:
                    return (int)NgxDlssMode.Quality;
                case Andastra.Runtime.Graphics.Common.Enums.DlssMode.Balanced:
                    return (int)NgxDlssMode.Balanced;
                case Andastra.Runtime.Graphics.Common.Enums.DlssMode.Performance:
                    return (int)NgxDlssMode.Performance;
                case Andastra.Runtime.Graphics.Common.Enums.DlssMode.UltraPerformance:
                    return (int)NgxDlssMode.UltraPerformance;
                default:
                    return (int)NgxDlssMode.Off;
            }
        }

        /// <summary>
        /// Get NGX backend type from Stride GraphicsDevice.
        /// Detects the underlying graphics API (DirectX 11, DirectX 12, or Vulkan) by querying device properties.
        /// Based on Stride Graphics API: GraphicsDevice exposes backend-specific properties via reflection.
        /// </summary>
        /// <param name="device">Stride GraphicsDevice to query</param>
        /// <returns>Detected NGX backend type, or DirectX12 as fallback if detection fails</returns>
        /// <remarks>
        /// Backend Detection Strategy:
        /// - DirectX 12: Checks for "D3D12Device" property (ID3D12Device* pointer)
        /// - Vulkan: Checks for "VkDevice" or "VulkanDevice" property (VkDevice handle)
        /// - DirectX 11: Checks for "D3D11Device" property (ID3D11Device* pointer)
        /// - Fallback: Returns DirectX12 if no backend-specific properties are found
        /// 
        /// Detection uses reflection to query device type properties, following the pattern established
        /// in StrideDlssSystem.GetD3D12Device() for consistency with existing codebase.
        /// 
        /// Based on NVIDIA NGX SDK documentation:
        /// - NGX supports all three backends: DirectX 11, DirectX 12, and Vulkan
        /// - Each backend requires different initialization functions and device pointers
        /// - Backend type must match the actual graphics API in use for proper NGX initialization
        /// </remarks>
        public static NgxBackend GetNgxBackend(Stride.Graphics.GraphicsDevice device)
        {
            if (device == null)
            {
                // Null device - return default fallback
                return NgxBackend.DirectX12;
            }

            // Use reflection to detect backend by checking for backend-specific device properties
            // Stride GraphicsDevice exposes different properties depending on the underlying graphics API
            try
            {
                Type deviceType = device.GetType();
                
                // Check for DirectX 12 backend (highest priority - most common for DLSS)
                // DirectX 12 devices expose a "D3D12Device" property
                PropertyInfo d3d12DeviceProperty = deviceType.GetProperty("D3D12Device", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (d3d12DeviceProperty != null)
                {
                    object d3d12DeviceValue = d3d12DeviceProperty.GetValue(device);
                    if (d3d12DeviceValue != null)
                    {
                        // Property exists and has a value - this is a DirectX 12 device
                        return NgxBackend.DirectX12;
                    }
                }

                // Check for Vulkan backend
                // Vulkan devices expose a "VkDevice" or "VulkanDevice" property
                PropertyInfo vkDeviceProperty = deviceType.GetProperty("VkDevice", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (vkDeviceProperty != null)
                {
                    object vkDeviceValue = vkDeviceProperty.GetValue(device);
                    if (vkDeviceValue != null)
                    {
                        // Property exists and has a value - this is a Vulkan device
                        return NgxBackend.Vulkan;
                    }
                }

                // Alternative Vulkan property name check
                PropertyInfo vulkanDeviceProperty = deviceType.GetProperty("VulkanDevice", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (vulkanDeviceProperty != null)
                {
                    object vulkanDeviceValue = vulkanDeviceProperty.GetValue(device);
                    if (vulkanDeviceValue != null)
                    {
                        // Property exists and has a value - this is a Vulkan device
                        return NgxBackend.Vulkan;
                    }
                }

                // Check for DirectX 11 backend
                // DirectX 11 devices expose a "D3D11Device" property
                PropertyInfo d3d11DeviceProperty = deviceType.GetProperty("D3D11Device", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (d3d11DeviceProperty != null)
                {
                    object d3d11DeviceValue = d3d11DeviceProperty.GetValue(device);
                    if (d3d11DeviceValue != null)
                    {
                        // Property exists and has a value - this is a DirectX 11 device
                        return NgxBackend.DirectX11;
                    }
                }

                // Fallback: If no backend-specific properties are found, default to DirectX 12
                // DirectX 12 is the most common backend for DLSS and provides the best feature support
                // This matches the original stub behavior for backward compatibility
                return NgxBackend.DirectX12;
            }
            catch (Exception ex)
            {
                // If reflection fails for any reason, log and return default fallback
                // This ensures the function never throws and always returns a valid backend type
                System.Console.WriteLine($"[NgxInterop] Exception detecting backend type: {ex.Message}");
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

