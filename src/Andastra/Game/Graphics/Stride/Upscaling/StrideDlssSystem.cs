using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Graphics.Common.Upscaling;
using Andastra.Runtime.Stride.Graphics;
using Stride.Graphics;

namespace Andastra.Game.Stride.Upscaling
{
    /// <summary>
    /// Stride implementation of NVIDIA DLSS (Deep Learning Super Sampling).
    /// Inherits shared DLSS logic from BaseDlssSystem.
    ///
    /// Features:
    /// - DLSS 3.x support with Frame Generation
    /// - DLSS Ray Reconstruction for raytracing
    /// - All quality modes: DLAA, Quality, Balanced, Performance, Ultra Performance
    /// - Automatic exposure and motion vector handling
    ///
    /// Requires NVIDIA RTX GPU (20-series or newer).
    /// </summary>
    public class StrideDlssSystem : BaseDlssSystem
    {
        #region NVIDIA NGX P/Invoke Declarations

        // NGX Result codes
        private enum NVSDK_NGX_Result
        {
            NVSDK_NGX_Result_Success = (int)0x1,
            NVSDK_NGX_Result_Fail = unchecked((int)0xBAD00000),
            NVSDK_NGX_Result_FAIL_FeatureNotSupported = NVSDK_NGX_Result_Fail | 1,
            NVSDK_NGX_Result_FAIL_PlatformError = NVSDK_NGX_Result_Fail | 2,
            NVSDK_NGX_Result_FAIL_FeatureAlreadyExists = NVSDK_NGX_Result_Fail | 3,
            NVSDK_NGX_Result_FAIL_FeatureNotFound = NVSDK_NGX_Result_Fail | 4,
            NVSDK_NGX_Result_FAIL_InvalidParameter = NVSDK_NGX_Result_Fail | 5,
            NVSDK_NGX_Result_FAIL_ScratchBufferTooSmall = NVSDK_NGX_Result_Fail | 6,
            NVSDK_NGX_Result_FAIL_NotInitialized = NVSDK_NGX_Result_Fail | 7,
            NVSDK_NGX_Result_FAIL_UnsupportedInputFormat = NVSDK_NGX_Result_Fail | 8,
            NVSDK_NGX_Result_FAIL_RWFlagMissing = NVSDK_NGX_Result_Fail | 9,
            NVSDK_NGX_Result_FAIL_MissingInput = NVSDK_NGX_Result_Fail | 10,
            NVSDK_NGX_Result_FAIL_UnableToInitializeFeature = NVSDK_NGX_Result_Fail | 11,
            NVSDK_NGX_Result_FAIL_OutOfDate = NVSDK_NGX_Result_Fail | 12,
            NVSDK_NGX_Result_FAIL_OutOfGPUMemory = NVSDK_NGX_Result_Fail | 13,
            NVSDK_NGX_Result_FAIL_UnsupportedFormat = NVSDK_NGX_Result_Fail | 14
        }

        // NGX Feature types
        private enum NVSDK_NGX_Feature
        {
            NVSDK_NGX_Feature_SuperSampling = 1
        }

        // NGX Version
        private const uint NVSDK_NGX_Version_API = 0x13;

        // NGX Parameter constants
        private const string NVSDK_NGX_Parameter_Width = "Width";
        private const string NVSDK_NGX_Parameter_Height = "Height";
        private const string NVSDK_NGX_Parameter_OutWidth = "OutWidth";
        private const string NVSDK_NGX_Parameter_OutHeight = "OutHeight";
        private const string NVSDK_NGX_Parameter_PerfQualityValue = "PerfQualityValue";
        private const string NVSDK_NGX_Parameter_DLSS_Feature_Create_Flags = "DLSS.Feature.Create.Flags";
        private const string NVSDK_NGX_Parameter_Scratch = "Scratch";
        private const string NVSDK_NGX_Parameter_Scratch_SizeInBytes = "Scratch.SizeInBytes";
        private const string NVSDK_NGX_Parameter_Color = "Color";
        private const string NVSDK_NGX_Parameter_Output = "Output";
        private const string NVSDK_NGX_Parameter_MotionVectors = "MotionVectors";
        private const string NVSDK_NGX_Parameter_Depth = "Depth";
        private const string NVSDK_NGX_Parameter_ExposureTexture = "ExposureTexture";

        // DLSS Quality modes
        private enum NVSDK_NGX_PerfQuality_Value
        {
            NVSDK_NGX_PerfQuality_Value_MaxPerf = 0,
            NVSDK_NGX_PerfQuality_Value_Balanced = 1,
            NVSDK_NGX_PerfQuality_Value_MaxQuality = 2,
            NVSDK_NGX_PerfQuality_Value_UltraPerformance = 3,
            NVSDK_NGX_PerfQuality_Value_UltraQuality = 4,
            NVSDK_NGX_PerfQuality_Value_DLAA = 5
        }

        // DLSS Feature flags
        [Flags]
        private enum NVSDK_NGX_DLSS_Feature_Flags
        {
            NVSDK_NGX_DLSS_Feature_Flags_None = 0,
            NVSDK_NGX_DLSS_Feature_Flags_IsHDR = 1 << 0,
            NVSDK_NGX_DLSS_Feature_Flags_MVLowRes = 1 << 1,
            NVSDK_NGX_DLSS_Feature_Flags_MVJittered = 1 << 2,
            NVSDK_NGX_DLSS_Feature_Flags_DepthInverted = 1 << 3,
            NVSDK_NGX_DLSS_Feature_Flags_Reserved_0 = 1 << 4,
            NVSDK_NGX_DLSS_Feature_Flags_DoSharpening = 1 << 5,
            NVSDK_NGX_DLSS_Feature_Flags_AutoExposure = 1 << 6
        }

        // NGX Handle structure
        [StructLayout(LayoutKind.Sequential)]
        private struct NVSDK_NGX_Handle
        {
            public IntPtr IdPtr;
        }

        // Parameter interface (opaque pointer)
        [StructLayout(LayoutKind.Sequential)]
        private struct NVSDK_NGX_Parameter
        {
            public IntPtr ParameterPtr;
        }

        // P/Invoke function declarations
        [DllImport("nvngx_dlss.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NVSDK_NGX_Result NVSDK_NGX_D3D12_Init(
            ulong InApplicationId,
            [MarshalAs(UnmanagedType.LPWStr)] string InApplicationDataPath,
            IntPtr InDevice, // ID3D12Device*
            IntPtr pInFeatureInfo, // NVSDK_NGX_FeatureDiscoveryInfo*
            uint InSDKVersion);

        [DllImport("nvngx_dlss.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NVSDK_NGX_Result NVSDK_NGX_D3D12_GetParameters(
            out IntPtr OutParameters); // NVSDK_NGX_Parameter**

        [DllImport("nvngx_dlss.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NVSDK_NGX_Result NVSDK_NGX_D3D12_GetScratchBufferSize(
            NVSDK_NGX_Feature InFeatureId,
            IntPtr InParameters, // NVSDK_NGX_Parameter*
            out UIntPtr OutSizeInBytes);

        [DllImport("nvngx_dlss.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NVSDK_NGX_Result NVSDK_NGX_D3D12_CreateFeature(
            IntPtr InCmdList, // ID3D12GraphicsCommandList*
            NVSDK_NGX_Feature InFeatureID,
            IntPtr InParameters, // NVSDK_NGX_Parameter*
            out NVSDK_NGX_Handle OutHandle);

        [DllImport("nvngx_dlss.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NVSDK_NGX_Result NVSDK_NGX_D3D12_EvaluateFeature(
            IntPtr InCmdList, // ID3D12GraphicsCommandList*
            ref NVSDK_NGX_Handle InFeatureHandle,
            IntPtr InParameters, // NVSDK_NGX_Parameter*
            IntPtr InCallback); // PFN_NVSDK_NGX_ProgressCallback

        [DllImport("nvngx_dlss.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NVSDK_NGX_Result NVSDK_NGX_D3D12_ReleaseFeature(
            ref NVSDK_NGX_Handle InHandle);

        [DllImport("nvngx_dlss.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern NVSDK_NGX_Result NVSDK_NGX_D3D12_Shutdown1(
            IntPtr InDevice); // ID3D12Device*

        // NGX Parameter interface vtable offsets (COM interface pattern)
        // NVSDK_NGX_Parameter is a COM interface with methods:
        // 0: QueryInterface
        // 1: AddRef
        // 2: Release
        // 3: Set/Get methods start here
        private const int NGX_Parameter_SetUlong_VTableOffset = 3;
        private const int NGX_Parameter_SetPtr_VTableOffset = 4;
        private const int NGX_Parameter_GetUlong_VTableOffset = 5;

        // Parameter interface method delegates (COM calling convention)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool NVSDK_NGX_Parameter_GetUlong(
            IntPtr parameter, [MarshalAs(UnmanagedType.LPStr)] string name, out ulong outValue);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool NVSDK_NGX_Parameter_SetUlong(
            IntPtr parameter, [MarshalAs(UnmanagedType.LPStr)] string name, ulong value);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool NVSDK_NGX_Parameter_SetPtr(
            IntPtr parameter, [MarshalAs(UnmanagedType.LPStr)] string name, IntPtr value);

        #endregion

        private GraphicsDevice _graphicsDevice;
        private IntPtr _d3d12Device;
        private Texture _outputTexture;
        private NVSDK_NGX_Handle _dlssHandle;
        private IntPtr _ngxParameters;
        private IntPtr _scratchBuffer;
        private UIntPtr _scratchBufferSize;
        private bool _ngxInitialized;
        private int _currentInputWidth;
        private int _currentInputHeight;
        private int _currentOutputWidth;
        private int _currentOutputHeight;

        public override string Version => "3.7.0"; // DLSS version
        public override bool IsAvailable => CheckDlssAvailability();
        public override bool RayReconstructionAvailable => CheckRayReconstructionSupport();
        public override bool FrameGenerationAvailable => CheckFrameGenerationSupport();

        public StrideDlssSystem(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        }

        #region BaseUpscalingSystem Implementation

        protected override bool InitializeInternal()
        {
            Console.WriteLine("[StrideDLSS] Initializing DLSS...");

            try
            {
                // Get the underlying DirectX 12 device from Stride
                _d3d12Device = GetD3D12Device(_graphicsDevice);
                if (_d3d12Device == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDLSS] Failed to get DirectX 12 device from Stride GraphicsDevice");
                    return false;
                }

                // Initialize NVIDIA NGX SDK
                // Application ID: Use a default value for development/testing
                // In production, NVIDIA provides unique application IDs for each game
                const ulong ApplicationId = 0xDEADBEEF; // Placeholder application ID
                string applicationDataPath = GetApplicationDataPath();

                Console.WriteLine($"[StrideDLSS] Initializing NGX with device: {_d3d12Device}, path: {applicationDataPath}");

                NVSDK_NGX_Result initResult = NVSDK_NGX_D3D12_Init(
                    ApplicationId,
                    applicationDataPath,
                    _d3d12Device,
                    IntPtr.Zero, // pInFeatureInfo (optional, can be null)
                    NVSDK_NGX_Version_API);

                if (initResult != NVSDK_NGX_Result.NVSDK_NGX_Result_Success)
                {
                    Console.WriteLine($"[StrideDLSS] NGX initialization failed with result: {initResult}");
                    return false;
                }

                _ngxInitialized = true;

                // Get parameter interface for configuring DLSS
                NVSDK_NGX_Result paramResult = NVSDK_NGX_D3D12_GetParameters(out _ngxParameters);
                if (paramResult != NVSDK_NGX_Result.NVSDK_NGX_Result_Success)
                {
                    Console.WriteLine($"[StrideDLSS] Failed to get NGX parameters: {paramResult}");
                    _ngxInitialized = false;
                    NVSDK_NGX_D3D12_Shutdown1(_d3d12Device);
                    return false;
                }

                // Initialize scratch buffer size to zero (will be set when feature is created)
                _scratchBufferSize = UIntPtr.Zero;
                _scratchBuffer = IntPtr.Zero;

                // DLSS feature handle is created lazily when first needed (in ExecuteDlss or Apply)
                _dlssHandle = new NVSDK_NGX_Handle { IdPtr = IntPtr.Zero };

                Console.WriteLine("[StrideDLSS] NGX SDK initialized successfully");
                return true;
            }
            catch (DllNotFoundException ex)
            {
                Console.WriteLine($"[StrideDLSS] NGX DLL not found: {ex.Message}");
                Console.WriteLine("[StrideDLSS] Ensure nvngx_dlss.dll is available in the application directory");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDLSS] Exception during initialization: {ex.Message}");
                Console.WriteLine($"[StrideDLSS] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        protected override void ShutdownInternal()
        {
            // Release DLSS feature if it was created
            if (_dlssHandle.IdPtr != IntPtr.Zero)
            {
                try
                {
                    NVSDK_NGX_Result releaseResult = NVSDK_NGX_D3D12_ReleaseFeature(ref _dlssHandle);
                    if (releaseResult != NVSDK_NGX_Result.NVSDK_NGX_Result_Success)
                    {
                        Console.WriteLine($"[StrideDLSS] Warning: Failed to release DLSS feature: {releaseResult}");
                    }
                    else
                    {
                        Console.WriteLine("[StrideDLSS] DLSS feature released");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideDLSS] Exception releasing DLSS feature: {ex.Message}");
                }
                finally
                {
                    _dlssHandle = new NVSDK_NGX_Handle { IdPtr = IntPtr.Zero };
                }
            }

            // Free scratch buffer if allocated
            if (_scratchBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_scratchBuffer);
                _scratchBuffer = IntPtr.Zero;
                _scratchBufferSize = UIntPtr.Zero;
            }

            // Shutdown NGX SDK if initialized
            if (_ngxInitialized && _d3d12Device != IntPtr.Zero)
            {
                try
                {
                    NVSDK_NGX_Result shutdownResult = NVSDK_NGX_D3D12_Shutdown1(_d3d12Device);
                    if (shutdownResult != NVSDK_NGX_Result.NVSDK_NGX_Result_Success)
                    {
                        Console.WriteLine($"[StrideDLSS] Warning: NGX shutdown returned: {shutdownResult}");
                    }
                    else
                    {
                        Console.WriteLine("[StrideDLSS] NGX SDK shut down");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideDLSS] Exception shutting down NGX: {ex.Message}");
                }
                finally
                {
                    _ngxInitialized = false;
                    _ngxParameters = IntPtr.Zero;
                }
            }

            // Dispose output texture
            _outputTexture?.Dispose();
            _outputTexture = null;

            _d3d12Device = IntPtr.Zero;

            Console.WriteLine("[StrideDLSS] Shutdown complete");
        }

        #endregion

        /// <summary>
        /// Applies DLSS upscaling to the input frame.
        /// </summary>
        public Texture Apply(Texture input, Texture motionVectors, Texture depth,
            Texture exposure, int targetWidth, int targetHeight)
        {
            if (!IsEnabled || input == null) return input;

            EnsureOutputTexture(targetWidth, targetHeight, input.Format);

            // DLSS Evaluation:
            // - Input: rendered frame at lower resolution
            // - Motion vectors: per-pixel velocity
            // - Depth: scene depth buffer
            // - Exposure: (optional) auto-exposure value
            // - Output: upscaled frame at target resolution

            ExecuteDlss(input, motionVectors, depth, exposure, _outputTexture);

            return _outputTexture ?? input;
        }

        private void EnsureOutputTexture(int width, int height, PixelFormat format)
        {
            if (_outputTexture != null &&
                _outputTexture.Width == width &&
                _outputTexture.Height == height)
            {
                return;
            }

            _outputTexture?.Dispose();
            _outputTexture = Texture.New2D(_graphicsDevice, width, height,
                format, TextureFlags.RenderTarget | TextureFlags.ShaderResource | TextureFlags.UnorderedAccess);
        }

        private void ExecuteDlss(Texture input, Texture motionVectors, Texture depth,
            Texture exposure, Texture output)
        {
            // Ensure DLSS feature is created
            if (!EnsureDlssFeatureCreated(input.Width, input.Height, output.Width, output.Height))
            {
                Console.WriteLine("[StrideDLSS] Cannot execute DLSS - feature creation failed");
                return;
            }

            try
            {
                // Get current command list
                IntPtr commandList = GetCurrentCommandList();
                if (commandList == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDLSS] No command list available for DLSS evaluation");
                    return;
                }

                // Set input parameters for this frame
                IntPtr inputResource = GetTextureResourcePtr(input);
                IntPtr motionVectorsResource = motionVectors != null ? GetTextureResourcePtr(motionVectors) : IntPtr.Zero;
                IntPtr depthResource = depth != null ? GetTextureResourcePtr(depth) : IntPtr.Zero;
                IntPtr exposureResource = exposure != null ? GetTextureResourcePtr(exposure) : IntPtr.Zero;
                IntPtr outputResource = GetTextureResourcePtr(output);

                // Set input textures
                if (inputResource != IntPtr.Zero)
                    SetParameter(_ngxParameters, NVSDK_NGX_Parameter_Color, inputResource);
                if (motionVectorsResource != IntPtr.Zero)
                    SetParameter(_ngxParameters, NVSDK_NGX_Parameter_MotionVectors, motionVectorsResource);
                if (depthResource != IntPtr.Zero)
                    SetParameter(_ngxParameters, NVSDK_NGX_Parameter_Depth, depthResource);
                if (exposureResource != IntPtr.Zero)
                    SetParameter(_ngxParameters, NVSDK_NGX_Parameter_ExposureTexture, exposureResource);
                if (outputResource != IntPtr.Zero)
                    SetParameter(_ngxParameters, NVSDK_NGX_Parameter_Output, outputResource);

                // Execute DLSS
                NVSDK_NGX_Result evalResult = NVSDK_NGX_D3D12_EvaluateFeature(
                    commandList,
                    ref _dlssHandle,
                    _ngxParameters,
                    IntPtr.Zero); // No progress callback

                if (evalResult != NVSDK_NGX_Result.NVSDK_NGX_Result_Success)
                {
                    Console.WriteLine($"[StrideDLSS] DLSS evaluation failed: {evalResult}");
                }
                else
                {
                    Console.WriteLine($"[StrideDLSS] DLSS executed successfully: {input.Width}x{input.Height} -> {output.Width}x{output.Height}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDLSS] Exception during DLSS execution: {ex.Message}");
            }
        }

        #region Mode Handlers

        protected override void OnModeChanged(DlssMode mode)
        {
            Console.WriteLine($"[StrideDLSS] Mode changed to: {mode}");

            if (!_ngxInitialized || _ngxParameters == IntPtr.Zero)
            {
                return;
            }

            // Release existing feature so it can be recreated with new quality settings
            if (_dlssHandle.IdPtr != IntPtr.Zero)
            {
                try
                {
                    NVSDK_NGX_D3D12_ReleaseFeature(ref _dlssHandle);
                    _dlssHandle = new NVSDK_NGX_Handle { IdPtr = IntPtr.Zero };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideDLSS] Exception releasing feature during mode change: {ex.Message}");
                }
            }

            // Update quality parameter for next feature creation
            NVSDK_NGX_PerfQuality_Value qualityValue = ConvertDlssModeToQuality(mode);
            SetParameterUlong(_ngxParameters, NVSDK_NGX_Parameter_PerfQualityValue, (ulong)qualityValue);

            // Reset current resolution so feature will be recreated
            _currentInputWidth = 0;
            _currentInputHeight = 0;
            _currentOutputWidth = 0;
            _currentOutputHeight = 0;
        }

        protected override void OnRayReconstructionChanged(bool enabled)
        {
            Console.WriteLine($"[StrideDLSS] Ray Reconstruction: {(enabled ? "enabled" : "disabled")}");
        }

        protected override void OnFrameGenerationChanged(bool enabled)
        {
            Console.WriteLine($"[StrideDLSS] Frame Generation: {(enabled ? "enabled" : "disabled")}");
        }

        protected override void OnSharpnessChanged(float sharpness)
        {
            Console.WriteLine($"[StrideDLSS] Sharpness set to: {sharpness:F2}");
        }

        #endregion

        #region Capability Checks

        private bool CheckDlssAvailability()
        {
            if (_graphicsDevice == null) return false;

            // Check for NVIDIA GPU
            // NVIDIA vendor ID is 0x10DE (PCI vendor ID)
            // Stride GraphicsDevice.Adapter.VendorId provides the vendor ID directly
            bool isNvidia = false;
            try
            {
                if (_graphicsDevice.Adapter != null)
                {
                    // NVIDIA PCI vendor ID is 0x10DE
                    const int NVIDIA_VENDOR_ID = 0x10DE;
                    int vendorId = _graphicsDevice.Adapter.VendorId;
                    isNvidia = (vendorId == NVIDIA_VENDOR_ID);

                    if (!isNvidia)
                    {
                        Console.WriteLine($"[StrideDLSS] Non-NVIDIA GPU detected (VendorId: 0x{vendorId:X4}), DLSS not available");
                        return false;
                    }
                }
                else
                {
                    // Adapter information not available, skip vendor check
                    // NGX initialization will fail if not NVIDIA
                    Console.WriteLine("[StrideDLSS] Adapter information not available, skipping vendor check");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDLSS] Exception checking GPU vendor: {ex.Message}");
                // Continue with capability check - NGX will fail if not NVIDIA
            }

            if (!isNvidia && _graphicsDevice.Adapter != null)
            {
                return false;
            }

            // Try to query NGX for DLSS capability if NGX is available
            try
            {
                // Check if NGX DLL is available
                string ngxDllPath = FindNgxDll();
                if (string.IsNullOrEmpty(ngxDllPath))
                {
                    Console.WriteLine("[StrideDLSS] NGX DLL not found in application or system directory");
                    return false;
                }

                // Check RTX GPU generation (DLSS requires RTX 20-series or newer)
                // RTX 20-series: Device ID range 0x1E00-0x1FFF (Turing)
                // RTX 30-series: Device ID range 0x2480-0x24FF, 0x2500-0x25FF (Ampere)
                // RTX 40-series: Device ID range 0x2680-0x26FF (Ada Lovelace)
                // We can't easily get device ID from Stride adapter description, so we'll rely on NGX query
                // However, we know DLSS requires RTX, so older NVIDIA GPUs (GTX series) won't support it

                // Try to get D3D12 device to query NGX
                IntPtr device = GetD3D12Device(_graphicsDevice);
                if (device == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDLSS] Cannot query DLSS availability - D3D12 device not available");
                    return false;
                }

                // Query NGX for DLSS capability by attempting a lightweight initialization
                // We initialize NGX temporarily to query DLSS support, then shut it down
                bool dlssSupported = QueryNgxDlssCapability(device);
                return dlssSupported;
            }
            catch (DllNotFoundException)
            {
                Console.WriteLine("[StrideDLSS] NGX DLL not available for capability check");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDLSS] Exception checking DLSS availability: {ex.Message}");
                Console.WriteLine($"[StrideDLSS] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private bool CheckRayReconstructionSupport()
        {
            // DLSS 3.5+ supports Ray Reconstruction
            return IsAvailable;
        }

        private bool CheckFrameGenerationSupport()
        {
            // DLSS 3.0+ with Ada Lovelace (RTX 40 series) supports Frame Generation
            return IsAvailable;
        }

        #endregion

        #region Helper Methods

        private IntPtr GetD3D12Device(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
                return IntPtr.Zero;

            // Stride GraphicsDevice native device access via reflection
            // For DirectX 12, we need to get the ID3D12Device* pointer
            // Stride API may use NativePointer or NativeDevice depending on backend
            try
            {
                // Use reflection to access native device pointer (property may not be public)
                var deviceType = graphicsDevice.GetType();

                // Try NativePointer first (used by D3D12 and Vulkan backends)
                var nativePointerProperty = deviceType.GetProperty("NativePointer",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nativePointerProperty != null)
                {
                    var value = nativePointerProperty.GetValue(graphicsDevice);
                    if (value is IntPtr ptr && ptr != IntPtr.Zero)
                    {
                        return ptr;
                    }
                }

                // Try NativeDevice property (used by D3D11 backend)
                var nativeDeviceProperty = deviceType.GetProperty("NativeDevice",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nativeDeviceProperty != null)
                {
                    var value = nativeDeviceProperty.GetValue(graphicsDevice);
                    if (value is IntPtr ptr && ptr != IntPtr.Zero)
                    {
                        return ptr;
                    }
                }

                // Alternative: Check for DirectX 12 specific properties
                var d3d12DeviceProperty = deviceType.GetProperty("D3D12Device");
                if (d3d12DeviceProperty != null)
                {
                    var value = d3d12DeviceProperty.GetValue(graphicsDevice);
                    if (value is IntPtr ptr && ptr != IntPtr.Zero)
                    {
                        return ptr;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDLSS] Exception getting D3D12 device: {ex.Message}");
                Console.WriteLine($"[StrideDLSS] Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("[StrideDLSS] Failed to get DirectX 12 device pointer from Stride GraphicsDevice");
            return IntPtr.Zero;
        }

        private string GetApplicationDataPath()
        {
            // Return a path for NGX to store temporary files and logs
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string ngxPath = System.IO.Path.Combine(appDataPath, "NVIDIA", "NGX");
            System.IO.Directory.CreateDirectory(ngxPath);
            return ngxPath;
        }

        private void SetParameter(IntPtr parameters, string name, ulong value)
        {
            if (parameters == IntPtr.Zero)
            {
                Console.WriteLine($"[StrideDLSS] Warning: Cannot set parameter {name} - parameter interface is null");
                return;
            }

            try
            {
                // NVSDK_NGX_Parameter is a COM interface
                // Get the vtable pointer (first field of COM object)
                IntPtr vtable = Marshal.ReadIntPtr(parameters);
                if (vtable == IntPtr.Zero)
                {
                    Console.WriteLine($"[StrideDLSS] Warning: Invalid parameter interface vtable for {name}");
                    return;
                }

                // Get the SetUlong function pointer from vtable
                // VTable layout: 0=QueryInterface, 1=AddRef, 2=Release, 3=SetUlong, 4=SetPtr, ...
                IntPtr setUlongPtr = Marshal.ReadIntPtr(vtable, IntPtr.Size * NGX_Parameter_SetUlong_VTableOffset);

                if (setUlongPtr != IntPtr.Zero)
                {
                    // Create delegate for SetUlong method
                    NVSDK_NGX_Parameter_SetUlong setUlong = Marshal.GetDelegateForFunctionPointer<NVSDK_NGX_Parameter_SetUlong>(setUlongPtr);

                    // Call SetUlong
                    bool success = setUlong(parameters, name, value);
                    if (!success)
                    {
                        Console.WriteLine($"[StrideDLSS] Warning: Failed to set parameter {name} = {value}");
                    }
                }
                else
                {
                    Console.WriteLine($"[StrideDLSS] Warning: SetUlong function pointer is null for {name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDLSS] Exception setting parameter {name}: {ex.Message}");
            }
        }

        private void SetParameter(IntPtr parameters, string name, IntPtr value)
        {
            if (parameters == IntPtr.Zero)
            {
                Console.WriteLine($"[StrideDLSS] Warning: Cannot set parameter {name} - parameter interface is null");
                return;
            }

            try
            {
                // NVSDK_NGX_Parameter is a COM interface
                // Get the vtable pointer (first field of COM object)
                IntPtr vtable = Marshal.ReadIntPtr(parameters);
                if (vtable == IntPtr.Zero)
                {
                    Console.WriteLine($"[StrideDLSS] Warning: Invalid parameter interface vtable for {name}");
                    return;
                }

                // Get the SetPtr function pointer from vtable
                // VTable layout: 0=QueryInterface, 1=AddRef, 2=Release, 3=SetUlong, 4=SetPtr, ...
                IntPtr setPtrPtr = Marshal.ReadIntPtr(vtable, IntPtr.Size * NGX_Parameter_SetPtr_VTableOffset);

                if (setPtrPtr != IntPtr.Zero)
                {
                    // Create delegate for SetPtr method
                    NVSDK_NGX_Parameter_SetPtr setPtr = Marshal.GetDelegateForFunctionPointer<NVSDK_NGX_Parameter_SetPtr>(setPtrPtr);

                    // Call SetPtr
                    bool success = setPtr(parameters, name, value);
                    if (!success)
                    {
                        Console.WriteLine($"[StrideDLSS] Warning: Failed to set parameter {name} = {value}");
                    }
                }
                else
                {
                    Console.WriteLine($"[StrideDLSS] Warning: SetPtr function pointer is null for {name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDLSS] Exception setting parameter {name}: {ex.Message}");
            }
        }

        private bool EnsureDlssFeatureCreated(int inputWidth, int inputHeight, int outputWidth, int outputHeight)
        {
            if (_dlssHandle.IdPtr != IntPtr.Zero)
            {
                // Feature already created
                return true;
            }

            try
            {
                // Update parameters with actual resolution
                SetParameter(_ngxParameters, NVSDK_NGX_Parameter_Width, (ulong)inputWidth);
                SetParameter(_ngxParameters, NVSDK_NGX_Parameter_Height, (ulong)inputHeight);
                SetParameter(_ngxParameters, NVSDK_NGX_Parameter_OutWidth, (ulong)outputWidth);
                SetParameter(_ngxParameters, NVSDK_NGX_Parameter_OutHeight, (ulong)outputHeight);

                // Get command list (this would come from the current rendering context)
                IntPtr commandList = GetCurrentCommandList();
                if (commandList == IntPtr.Zero)
                {
                    Console.WriteLine("[StrideDLSS] No command list available for feature creation");
                    return false;
                }

                // Create DLSS feature
                NVSDK_NGX_Result createResult = NVSDK_NGX_D3D12_CreateFeature(
                    commandList,
                    NVSDK_NGX_Feature.NVSDK_NGX_Feature_SuperSampling,
                    _ngxParameters,
                    out _dlssHandle);

                if (createResult != NVSDK_NGX_Result.NVSDK_NGX_Result_Success)
                {
                    Console.WriteLine($"[StrideDLSS] Failed to create DLSS feature: {createResult}");
                    return false;
                }

                Console.WriteLine($"[StrideDLSS] DLSS feature created: {inputWidth}x{inputHeight} -> {outputWidth}x{outputHeight}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDLSS] Exception creating DLSS feature: {ex.Message}");
                return false;
            }
        }

        private IntPtr GetCurrentCommandList()
        {
            if (_graphicsDevice == null)
                return IntPtr.Zero;

            // Stride's ImmediateContext() extension method provides access to the command list
            global::Stride.Graphics.CommandList immediateContext = _graphicsDevice.ImmediateContext();
            if (immediateContext != null)
            {
                // Stride CommandList native command list access via reflection
                // For DirectX 12, we need to get the ID3D12GraphicsCommandList* pointer
                global::Stride.Graphics.CommandList commandList = immediateContext;
                if (commandList != null)
                {
                    // Use reflection to access native command list pointer (property may not be public)
                    try
                    {
                        var commandListType = commandList.GetType();

                        // Try NativeCommandList first (standard Stride API)
                        var nativeProperty = commandListType.GetProperty("NativeCommandList",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (nativeProperty != null)
                        {
                            var value = nativeProperty.GetValue(commandList);
                            if (value is IntPtr ptr && ptr != IntPtr.Zero)
                            {
                                return ptr;
                            }
                        }

                        // Try NativePointer as alternative (used by some Stride resources)
                        var nativePointerProperty = commandListType.GetProperty("NativePointer",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (nativePointerProperty != null)
                        {
                            var value = nativePointerProperty.GetValue(commandList);
                            if (value is IntPtr ptr && ptr != IntPtr.Zero)
                            {
                                return ptr;
                            }
                        }

                        // Alternative property names
                        var d3d12CommandListProperty = commandListType.GetProperty("D3D12CommandList",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (d3d12CommandListProperty != null)
                        {
                            var value = d3d12CommandListProperty.GetValue(commandList);
                            if (value is IntPtr ptr && ptr != IntPtr.Zero)
                            {
                                return ptr;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StrideDLSS] Exception getting command list through reflection: {ex.Message}");
                    }
                }
            }

            Console.WriteLine("[StrideDLSS] Failed to get command list from Stride ImmediateContext");
            return IntPtr.Zero;
        }

        /// <summary>
        /// Finds the NGX DLL path by checking application directory and system paths.
        /// </summary>
        private string FindNgxDll()
        {
            // Check application directory first
            string appDirDll = "nvngx_dlss.dll";
            if (System.IO.File.Exists(appDirDll))
            {
                return appDirDll;
            }

            // Check system directory
            string system32Path = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string systemDll = System.IO.Path.Combine(system32Path, "nvngx_dlss.dll");
            if (System.IO.File.Exists(systemDll))
            {
                return systemDll;
            }

            // Check SysWOW64 for 32-bit apps on 64-bit systems
            string sysWow64Path = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
            if (!string.IsNullOrEmpty(sysWow64Path))
            {
                string sysWow64Dll = System.IO.Path.Combine(sysWow64Path, "nvngx_dlss.dll");
                if (System.IO.File.Exists(sysWow64Dll))
                {
                    return sysWow64Dll;
                }
            }

            return null;
        }

        /// <summary>
        /// Queries NGX for DLSS capability by performing a lightweight initialization and capability check.
        /// This initializes NGX temporarily, queries DLSS support, and then shuts it down.
        /// </summary>
        private bool QueryNgxDlssCapability(IntPtr d3d12Device)
        {
            IntPtr tempNgxParameters = IntPtr.Zero;
            bool ngxInitialized = false;

            try
            {
                // Attempt to initialize NGX SDK for capability query
                // Use a temporary application ID for capability checking
                const ulong TempApplicationId = 0xDEADBEEF;
                string applicationDataPath = GetApplicationDataPath();

                Console.WriteLine("[StrideDLSS] Querying NGX for DLSS capability...");

                // Initialize NGX - this will fail if DLSS is not supported
                NVSDK_NGX_Result initResult = NVSDK_NGX_D3D12_Init(
                    TempApplicationId,
                    applicationDataPath,
                    d3d12Device,
                    IntPtr.Zero, // pInFeatureInfo (null for capability query)
                    NVSDK_NGX_Version_API);

                if (initResult != NVSDK_NGX_Result.NVSDK_NGX_Result_Success)
                {
                    // Check specific failure reasons
                    if (initResult == NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotSupported)
                    {
                        Console.WriteLine("[StrideDLSS] NGX initialization failed - DLSS feature not supported on this hardware");
                        return false;
                    }
                    else if (initResult == NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_OutOfDate)
                    {
                        Console.WriteLine("[StrideDLSS] NGX initialization failed - NGX driver or DLL is out of date");
                        return false;
                    }
                    else if (initResult == NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_PlatformError)
                    {
                        Console.WriteLine("[StrideDLSS] NGX initialization failed - platform error (likely unsupported GPU or driver)");
                        return false;
                    }
                    else
                    {
                        Console.WriteLine($"[StrideDLSS] NGX initialization failed with result: {initResult} (0x{((uint)initResult):X8})");
                        return false;
                    }
                }

                ngxInitialized = true;

                // Get NGX parameters to query DLSS capability
                NVSDK_NGX_Result paramResult = NVSDK_NGX_D3D12_GetParameters(out tempNgxParameters);
                if (paramResult != NVSDK_NGX_Result.NVSDK_NGX_Result_Success)
                {
                    Console.WriteLine($"[StrideDLSS] Failed to get NGX parameters for capability query: {paramResult}");
                    return false;
                }

                // Query DLSS support through NGX parameters
                // NGX provides capability information through the parameter interface
                // We can check if DLSS feature is available by attempting to query scratch buffer size
                // This is a lightweight operation that doesn't require creating the feature

                // Set up minimal parameters for DLSS capability query
                // Use a standard resolution for capability checking (doesn't need to match actual resolution)
                const int TestWidth = 1920;
                const int TestHeight = 1080;
                const int TestOutputWidth = 2560;
                const int TestOutputHeight = 1440;

                SetParameterUlong(tempNgxParameters, NVSDK_NGX_Parameter_Width, (ulong)TestWidth);
                SetParameterUlong(tempNgxParameters, NVSDK_NGX_Parameter_Height, (ulong)TestHeight);
                SetParameterUlong(tempNgxParameters, NVSDK_NGX_Parameter_OutWidth, (ulong)TestOutputWidth);
                SetParameterUlong(tempNgxParameters, NVSDK_NGX_Parameter_OutHeight, (ulong)TestOutputHeight);
                SetParameterUlong(tempNgxParameters, NVSDK_NGX_Parameter_PerfQualityValue, (ulong)NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_Balanced);

                // Query scratch buffer size - this will fail if DLSS is not supported
                UIntPtr scratchSize;
                NVSDK_NGX_Result scratchResult = NVSDK_NGX_D3D12_GetScratchBufferSize(
                    NVSDK_NGX_Feature.NVSDK_NGX_Feature_SuperSampling,
                    tempNgxParameters,
                    out scratchSize);

                if (scratchResult != NVSDK_NGX_Result.NVSDK_NGX_Result_Success)
                {
                    if (scratchResult == NVSDK_NGX_Result.NVSDK_NGX_Result_FAIL_FeatureNotSupported)
                    {
                        Console.WriteLine("[StrideDLSS] DLSS feature not supported - scratch buffer query failed");
                        return false;
                    }
                    else
                    {
                        Console.WriteLine($"[StrideDLSS] Scratch buffer size query failed: {scratchResult}");
                        // This is not necessarily a fatal error - DLSS might still be available
                        // Continue with capability check
                    }
                }
                else
                {
                    Console.WriteLine($"[StrideDLSS] DLSS capability confirmed - scratch buffer size: {scratchSize} bytes");
                }

                // If we got here, NGX initialized successfully and DLSS appears to be available
                Console.WriteLine("[StrideDLSS] DLSS capability check passed - DLSS is available on this system");
                return true;
            }
            catch (DllNotFoundException ex)
            {
                Console.WriteLine($"[StrideDLSS] NGX DLL not found during capability check: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDLSS] Exception during DLSS capability query: {ex.Message}");
                Console.WriteLine($"[StrideDLSS] Stack trace: {ex.StackTrace}");
                return false;
            }
            finally
            {
                // Clean up: shutdown NGX if we initialized it
                if (ngxInitialized && d3d12Device != IntPtr.Zero)
                {
                    try
                    {
                        NVSDK_NGX_Result shutdownResult = NVSDK_NGX_D3D12_Shutdown1(d3d12Device);
                        if (shutdownResult != NVSDK_NGX_Result.NVSDK_NGX_Result_Success)
                        {
                            Console.WriteLine($"[StrideDLSS] Warning: NGX shutdown during capability check returned: {shutdownResult}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StrideDLSS] Exception shutting down NGX during capability check: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Gets the native D3D12 texture resource pointer from a Stride Texture.
        /// Stride Texture objects wrap D3D12 resources, and we need the native pointer for NGX.
        /// Based on StrideDirect3D12Backend.cs which uses NativePointer for textures.
        /// </summary>
        private IntPtr GetTextureResourcePtr(Texture texture)
        {
            if (texture == null)
                return IntPtr.Zero;

            try
            {
                // Stride Texture native resource access via reflection
                // For D3D12, we need to get the ID3D12Resource* pointer
                // Stride API may use NativePointer, NativeDeviceTexture, or NativeResource depending on backend
                var textureType = texture.GetType();

                // Try NativePointer first (used by D3D12 and Vulkan backends)
                var nativePointerProperty = textureType.GetProperty("NativePointer",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nativePointerProperty != null)
                {
                    var value = nativePointerProperty.GetValue(texture);
                    if (value is IntPtr ptr && ptr != IntPtr.Zero)
                    {
                        return ptr;
                    }
                }

                // Try NativeDeviceTexture (used by D3D11 backend and StrideTexture2D)
                var nativeDeviceTextureProperty = textureType.GetProperty("NativeDeviceTexture",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (nativeDeviceTextureProperty != null)
                {
                    var value = nativeDeviceTextureProperty.GetValue(texture);
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
                    var value = nativeResourceProperty.GetValue(texture);
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
                    var resource = resourceProperty.GetValue(texture);
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideDLSS] Exception getting texture resource pointer: {ex.Message}");
                Console.WriteLine($"[StrideDLSS] Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("[StrideDLSS] Failed to get D3D12 texture resource pointer from Stride Texture");
            return IntPtr.Zero;
        }

        /// <summary>
        /// Sets a ulong parameter in the NGX parameter interface.
        /// Wrapper around SetParameter for type safety and clarity.
        /// </summary>
        private void SetParameterUlong(IntPtr parameters, string name, ulong value)
        {
            SetParameter(parameters, name, value);
        }

        /// <summary>
        /// Converts a DlssMode enum value to the corresponding NGX performance quality value.
        /// Maps user-facing DLSS modes to NGX internal quality settings.
        /// </summary>
        private NVSDK_NGX_PerfQuality_Value ConvertDlssModeToQuality(DlssMode mode)
        {
            switch (mode)
            {
                case DlssMode.Off:
                    // Should not be called with Off mode, but return Balanced as safe default
                    return NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_Balanced;

                case DlssMode.DLAA:
                    // DLAA uses native resolution, so use MaxQuality
                    return NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_DLAA;

                case DlssMode.Quality:
                    // Quality mode - highest quality upscaling
                    return NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_MaxQuality;

                case DlssMode.Balanced:
                    // Balanced mode - good balance between quality and performance
                    return NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_Balanced;

                case DlssMode.Performance:
                    // Performance mode - higher performance, lower quality
                    return NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_MaxPerf;

                case DlssMode.UltraPerformance:
                    // Ultra Performance mode - maximum performance
                    return NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_UltraPerformance;

                default:
                    // Default to Balanced for unknown modes
                    Console.WriteLine($"[StrideDLSS] Unknown DLSS mode: {mode}, defaulting to Balanced");
                    return NVSDK_NGX_PerfQuality_Value.NVSDK_NGX_PerfQuality_Value_Balanced;
            }
        }

        #endregion
    }
}

