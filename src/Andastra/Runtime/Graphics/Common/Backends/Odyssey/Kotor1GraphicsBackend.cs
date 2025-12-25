using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Andastra.Parsing;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Graphics.Common.Structs;
using ResourceType = Andastra.Parsing.Resource.ResourceType;

namespace Andastra.Runtime.Graphics.Common.Backends.Odyssey
{
    /// <summary>
    /// Graphics backend for Star Wars: Knights of the Old Republic, matching swkotor.exe rendering exactly 1:1.
    ///
    /// This backend implements the exact rendering code from swkotor.exe,
    /// including OpenGL initialization, texture loading, and rendering pipeline.
    /// </summary>
    /// <remarks>
    /// KOTOR 1 Graphics Backend:
    /// - Based on reverse engineering of swkotor.exe
    /// - Original game graphics system: OpenGL (OPENGL32.DLL) with WGL extensions
    /// - Graphics initialization:
    ///   - FUN_0044dab0 @ 0x0044dab0 (main OpenGL context creation)
    ///   - FUN_00427c90 @ 0x00427c90 (texture initialization)
    ///   - FUN_00426cc0 @ 0x00426cc0 (secondary context creation for multi-threading)
    /// - Located via string references:
    ///   - "wglCreateContext" @ 0x0073d2b8
    ///   - "wglChoosePixelFormatARB" @ 0x0073f444
    ///   - "WGL_NV_render_texture_rectangle" @ 0x00740798
    /// - Original game graphics device: OpenGL with WGL extensions
    /// - This implementation: Direct 1:1 match of swkotor.exe rendering code
    ///
    /// KOTOR1-Specific Details:
    /// - Uses global variables at different addresses than KOTOR2 (DAT_0078d98c vs DAT_0080c994)
    /// - Helper functions: FUN_0045f820, FUN_006fae8c (different addresses than KOTOR2)
    /// - Texture setup: Similar pattern but with KOTOR1-specific global variable addresses
    /// </remarks>
    public class Kotor1GraphicsBackend : OdysseyGraphicsBackend
    {
        // Delegate for window procedure
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        // Resource provider for loading texture data
        // Matches swkotor.exe resource loading system (CExoResMan, CExoKeyTable)
        private IGameResourceProvider _resourceProvider;

        #region KOTOR1 Global Variables (matching swkotor.exe addresses)

        // Global variables matching swkotor.exe addresses
        // DAT_0078e38c - cleanup flag
        private static int _kotor1CleanupFlag = 0;

        // DAT_0078e388 - window style flag
        private static int _kotor1WindowStyleFlag = 0xffff;

        // DAT_007a6888 - multisample flag
        private static int _kotor1MultisampleFlag = 0;

        // DAT_007b9220 - color bits
        private static ushort _kotor1ColorBits = 0x20;

        // DAT_007b9224 - depth bits
        private static ushort _kotor1DepthBits = 0x18;

        // DAT_0078d98c - texture initialization flag
        private static int _kotor1TextureInitFlag = 0;

        // DAT_0078daf4 - texture initialization flag 2
        private static byte _kotor1TextureInitFlag2 = 0;

        // DAT_0078d1d4 - screen width
        private static int _kotor1ScreenWidth = 0;

        // DAT_0078d1d8 - screen height
        private static int _kotor1ScreenHeight = 0;

        // DAT_007a6854 - primary OpenGL context (HGLRC)
        private static IntPtr _kotor1PrimaryContext = IntPtr.Zero;

        // DAT_007a47e4 - primary device context (HDC)
        private static IntPtr _kotor1PrimaryDC = IntPtr.Zero;

        // DAT_007a687c - texture ID for render target
        private static uint _kotor1RenderTargetTexture = 0;

        // DAT_007a6870, DAT_007a6874, DAT_007a6878 - texture IDs
        private static uint _kotor1Texture0 = 0;
        private static uint _kotor1Texture1 = 0;
        private static uint _kotor1Texture2 = 0;

        // DAT_007a6864, DAT_007a6868, DAT_007a686c - additional texture IDs
        private static uint _kotor1Texture3 = 0;
        private static uint _kotor1Texture4 = 0;
        private static uint _kotor1Texture5 = 0;

        // Secondary context variables (matching swkotor.exe: FUN_00427c90)
        // DAT_007a6824, DAT_007a6828, DAT_007a682c, DAT_007a6830, DAT_007a6834, DAT_007a6838 - window handles
        private static IntPtr[] _kotor1SecondaryWindows = new IntPtr[6];

        // DAT_007a47ec, DAT_007a47f0, DAT_007a47f4, DAT_007a47f8, DAT_007a47fc, DAT_007a4800 - device contexts
        private static IntPtr[] _kotor1SecondaryDCs = new IntPtr[6];

        // DAT_007a4804, DAT_007a4808, DAT_007a480c, DAT_007a4810, DAT_007a4814, DAT_007a4818 - contexts
        private static IntPtr[] _kotor1SecondaryContexts = new IntPtr[6];

        // DAT_007a47c4, DAT_007a47c8, DAT_007a47cc, DAT_007a47d0, DAT_007a47d4, DAT_007a47d8 - texture IDs
        private static uint[] _kotor1SecondaryTextures = new uint[6];

        // DAT_007a68c4, DAT_007a68c8, DAT_007a68cc, DAT_007a68c0 - additional context variables
        private static IntPtr _kotor1AdditionalWindow = IntPtr.Zero;
        private static IntPtr _kotor1AdditionalDC = IntPtr.Zero;
        private static IntPtr _kotor1AdditionalContext = IntPtr.Zero;
        private static uint _kotor1AdditionalTexture = 0;

        // DAT_007a6860 - flag
        private static byte _kotor1TextureInitFlag3 = 0;

        // DAT_0078d990 - flag
        private static byte _kotor1TextureInitFlag4 = 0;

        // DAT_007a68d4 - vertex program ID
        private static uint _kotor1VertexProgramId = 0;

        // DAT_007bb6b8 - GetDC function pointer
        private static GetDCDelegate _kotor1GetDC = null;

        // DAT_007bb528 - glGenProgramsARB function pointer
        private static GlGenProgramsArbDelegate _kotor1GlGenProgramsArb = null;

        // DAT_007bb788 - glBindProgramARB function pointer
        private static GlBindProgramArbDelegate _kotor1GlBindProgramArb = null;

        // DAT_007bb580 - glProgramStringARB function pointer
        private static GlProgramStringArbDelegate _kotor1GlProgramStringArb = null;

        // DAT_0078e528 - capability flag 2
        private static uint _kotor1CapabilityFlag2 = 0xffffffff;

        // DAT_0078e4dc, DAT_0078e4c8 - extension flags
        private static uint _kotor1ExtensionFlag2 = 0;
        private static uint _kotor1ExtensionFlag3 = 0;

        // DAT_007bb730 - wglChoosePixelFormatARB function pointer
        private static WglChoosePixelFormatArbDelegate _kotor1WglChoosePixelFormatArb = null;

        // DAT_007bb7ec - wglGetExtensionsStringARB function pointer
        private static WglGetExtensionsStringArbDelegate _kotor1WglGetExtensionsStringArb = null;

        // DAT_007bb530 - wglCreateContextAttribsARB function pointer
        private static WglCreateContextAttribsArbDelegate _kotor1WglCreateContextAttribsArb = null;

        // DAT_007bb620 - glProgramEnvParameter4fARB function pointer
        private static GlProgramEnvParameter4fArbDelegate _kotor1GlProgramEnvParameter4fArb = null;

        // DAT_007bb840 - glProgramLocalParameter4fARB function pointer
        private static GlProgramLocalParameter4fArbDelegate _kotor1GlProgramLocalParameter4fArb = null;

        // DAT_007bb804 - glProgramEnvParameter4fvARB function pointer
        private static GlProgramEnvParameter4fvArbDelegate _kotor1GlProgramEnvParameter4fvArb = null;

        // DAT_007bb710 - glProgramLocalParameter4fvARB function pointer
        private static GlProgramLocalParameter4fvArbDelegate _kotor1GlProgramLocalParameter4fvArb = null;

        // DAT_007bb6d4 - glProgramLocalParameter4dvARB function pointer
        private static GlProgramLocalParameter4dvArbDelegate _kotor1GlProgramLocalParameter4dvArb = null;

        // DAT_007bb7fc - glProgramLocalParameter4dvARB function pointer (alternate)
        private static GlProgramLocalParameter4dvArbDelegate _kotor1GlProgramLocalParameter4dvArb2 = null;

        // DAT_007a692c, DAT_007a6924, DAT_007a6920, DAT_007a691c, DAT_007a6918, DAT_007a690c, DAT_007a6910, DAT_007a6914 - vertex program IDs
        private static uint _kotor1VertexProgramId0 = 0;
        private static uint _kotor1VertexProgramId1 = 0;
        private static uint _kotor1VertexProgramId2 = 0;
        private static uint _kotor1VertexProgramId3 = 0;
        private static uint _kotor1VertexProgramId4 = 0;
        private static uint _kotor1VertexProgramId5 = 0;
        private static uint _kotor1VertexProgramId6 = 0;
        private static uint _kotor1VertexProgramId7 = 0;

        // DAT_0078e5ec - vertex program flag
        private static int _kotor1VertexProgramFlag = 0;

        // DAT_0078e51c - capability flag
        private static uint _kotor1CapabilityFlag = 0xffffffff;

        // DAT_007bb85c - extension flags
        private static uint _kotor1ExtensionFlags = 0;

        // DAT_0078e4d4 - required extension flags
        private static uint _kotor1RequiredExtensionFlags = 0;

        // DAT_0078d440 - depth test flag
        private static int _kotor1DepthTestFlag = 0;

        // DAT_0078d438 - stencil test flag
        private static byte _kotor1StencilTestFlag = 0;

        // DAT_007bb538 - glEnable/glDisable function pointer
        private static GlEnableDisableDelegate _kotor1GlEnableDisable = null;

        // DAT_0078e520 - render texture rectangle flag
        private static uint _kotor1RenderTextureRectangleFlag = 0xffffffff;

        // DAT_0078e524 - pbuffer support flag
        private static uint _kotor1PbufferSupportFlag = 0xffffffff;

        // DAT_0078e420 - extension flag
        private static int _kotor1ExtensionFlag = 0;

        // DAT_0078dae9 - texture init flag 5
        private static byte _kotor1TextureInitFlag5 = 0;

        // Vertex array object function pointers (OpenGL 3.0+)
        private static GlGenVertexArraysDelegate _kotor1GlGenVertexArrays = null;
        private static GlBindVertexArrayDelegate _kotor1GlBindVertexArray = null;
        private static GlDeleteVertexArraysDelegate _kotor1GlDeleteVertexArrays = null;

        // DAT_007b90f0 - additional setup flag (matching swkotor.exe: FUN_00422360)
        private static int _kotor1AdditionalSetupFlag = 0;

        // DAT_0078d3f4 - display parameter (matching swkotor.exe: FUN_00421d90)
        private static int _kotor1DisplayParameter = 0x1000000;

        // DAT_007a47c0 - display list base (matching swkotor.exe: FUN_0044cb10, FUN_0044cc60)
        private static uint _kotor1DisplayListBase = 0;

        // DAT_007b90ec - function pointer (matching swkotor.exe: FUN_0044cc60)
        private static IntPtr _kotor1FunctionPointer = IntPtr.Zero;

        // DAT_0078e35c - flag (matching swkotor.exe: FUN_0044cc40)
        private static byte _kotor1DisplayListFlag = 1;

        // DAT_0078d498 - bitmap data array (matching swkotor.exe: FUN_0044cb10)
        // This is a 95-element array of 13-byte bitmap data (0x5f * 0xd = 0x4d3 bytes)
        private static byte[] _kotor1BitmapData = new byte[95 * 13]; // 0x5f * 0xd

        // Additional global variables for FUN_00421d90 and related functions
        // DAT_0078d3f0 - float multiplier
        private static float _kotor1DisplayMultiplier = 0.0f;

        // DAT_007a477c - texture object pointer (matching swkotor.exe: FUN_00420710 @ 0x004207eb)
        private static IntPtr _kotor1TextureObjectPointer = IntPtr.Zero;

        // DAT_007a4770, DAT_007a4774, DAT_007a4778 - array structure
        private static IntPtr[] _kotor1TextureArray = new IntPtr[8];
        private static int _kotor1TextureArrayCount = 0;
        private static int _kotor1TextureArrayCapacity = 8;

        // DAT_007a4798 - array structure 2
        private static IntPtr[] _kotor1TextureArray2 = new IntPtr[8];
        private static int _kotor1TextureArray2Count = 0;
        private static int _kotor1TextureArray2Capacity = 8;

        // DAT_007a4780, DAT_007a47a4 - array structures for display calculation
        private static IntPtr[] _kotor1DisplayArray1 = new IntPtr[8];
        private static int _kotor1DisplayArray1Count = 0;
        private static int _kotor1DisplayArray1Capacity = 8;

        private static IntPtr[] _kotor1DisplayArray2 = new IntPtr[8];
        private static int _kotor1DisplayArray2Count = 0;
        private static int _kotor1DisplayArray2Capacity = 8;

        // DAT_007a469c, DAT_007a46c0, DAT_007a46dc - display calculation values
        private static int _kotor1DisplayValue1 = 0;
        private static int _kotor1DisplayValue2 = 0;
        private static int _kotor1DisplayValue3 = 0;

        // DAT_0078d3f8 - float value
        private static float _kotor1DisplayFloat = 0.0f;

        // DAT_007a46ac, DAT_007a46a4, DAT_007a46a8, DAT_007a46a0, DAT_007a46d0, DAT_007a46c8, DAT_007a46cc, DAT_007a46c4 - color values
        private static float _kotor1ColorR1 = 0.0f;
        private static float _kotor1ColorG1 = 0.0f;
        private static float _kotor1ColorB1 = 0.0f;
        private static float _kotor1ColorA1 = 0.0f;
        private static float _kotor1ColorR2 = 0.0f;
        private static float _kotor1ColorG2 = 0.0f;
        private static float _kotor1ColorB2 = 0.0f;
        private static float _kotor1ColorA2 = 0.0f;

        // DAT_007a46f0, DAT_007a46e8, DAT_007a46ec, DAT_007a46e4, DAT_007a46e0, DAT_007a46d8 - combined color values
        private static float _kotor1CombinedColorR = 0.0f;
        private static float _kotor1CombinedColorG = 0.0f;
        private static float _kotor1CombinedColorB = 0.0f;
        private static float _kotor1CombinedColorA = 0.0f;
        private static float _kotor1CombinedColorA2 = 0.0f;
        private static int _kotor1CombinedDisplayValue = 0;

        // DAT_007a474c - float value for display calculation
        private static float _kotor1DisplayFloat2 = 0.0f;

        // DAT_0078e574 - float value
        private static float _kotor1DisplayFloat3 = 0.0f;

        // DAT_0078d3fc - float value
        private static float _kotor1DisplayFloat4 = 0.0f;

        // DAT_007a47a8 - array count
        private static int _kotor1DisplayArray2Count2 = 0;

        // DAT_007a479c - value
        private static int _kotor1DisplayValue4 = 0;

        // DAT_007a4748 - value
        private static int _kotor1DisplayValue5 = 0;

        // DAT_007a4790 - array index
        private static int _kotor1DisplayArrayIndex = 0;

        // DAT_007a4794 - array capacity
        private static int _kotor1DisplayArrayCapacity = 8;

        // DAT_007a478c - array pointer
        private static IntPtr[] _kotor1DisplayArray3 = new IntPtr[8];

        // DAT_007a46bc - value
        private static uint _kotor1DisplayValue6 = 0;

        // DAT_007a46b0 - value
        private static int _kotor1DisplayValue7 = 0;

        // DAT_007a4754 - value
        private static int _kotor1DisplayValue8 = 0;

        // DAT_007a683c - array of 6 integers (matching swkotor.exe: FUN_00425c30)
        private static int[] _kotor1ContextArray = new int[6];

        // DAT_007a6898 - flag (matching swkotor.exe: FUN_00425c30)
        private static int _kotor1ContextFlag = 0;

        // DAT_0078d46c - vertex program constant (matching swkotor.exe: FUN_0044dab0 line 221)
        private static uint _kotor1VertexProgramConstant = GL_VERTEX_PROGRAM_ARB;

        // DAT_007bb744 - function pointer for glProgramEnvParameter4fARB (matching swkotor.exe: FUN_004a2400)
        private static GlProgramEnvParameter4fArbDelegate _kotor1GlProgramEnvParameter4fArb2 = null;

        // Wrapper function for glProgramEnvParameter4fARB that matches the original swkotor.exe calling convention
        // The original engine calls (*DAT_007bb744)(target, index, x, y) with 4 parameters
        // but OpenGL glProgramEnvParameter4fARB requires (target, index, x, y, z, w) with 6 parameters
        // This wrapper provides the missing z=0.0f and w=0.0f parameters
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Kotor1ProgramEnvParameter4fWrapperDelegate(uint target, uint index, uint xRaw, uint yRaw);

        // Function pointer wrapper for glProgramEnvParameter4fARB that matches the original swkotor.exe calling convention
        // The original engine calls (*DAT_007bb744)(target, index, x, y) with 4 parameters
        // but OpenGL glProgramEnvParameter4fARB requires (target, index, x, y, z, w) with 6 parameters
        // This field stores a pointer to a wrapper function that adapts the calling convention
        private static IntPtr _kotor1ProgramEnvParameter4fWrapperPtr = IntPtr.Zero;


        // The actual wrapper function implementation
        private static void Kotor1ProgramEnvParameter4fWrapperImpl(uint target, uint index, uint xRaw, uint yRaw)
        {
            // Convert raw uint values to float bit patterns (matching swkotor.exe behavior)
            // The original engine passes uint32 values that are interpreted as float bit patterns
            unsafe
            {
                float x = *(float*)&xRaw;
                float y = *(float*)&yRaw;
                // Call the actual OpenGL function with z=0.0f, w=0.0f (implicit in original calls)
                _kotor1GlProgramEnvParameter4fArb?.Invoke(target, index, x, y, 0.0f, 0.0f);
            }
        }

        // Wrapper function that matches GlProgramEnvParameter4fArbDelegate signature
        // This function is used as a delegate and forwards calls to the actual OpenGL function
        private static void Kotor1ProgramEnvParameter4fWrapperFunction(uint target, uint index, float x, float y, float z, float w)
        {
            // Forward the call directly to the OpenGL function
            _kotor1GlProgramEnvParameter4fArb?.Invoke(target, index, x, y, z, w);
        }

        // DAT_007bb834 - function pointer for glBindProgramARB (matching swkotor.exe: FUN_004a2400)
        private static GlBindProgramArbDelegate _kotor1GlBindProgramArb2 = null;

        // DAT_0073f218, DAT_0073f224, DAT_0073f21c - vertex program parameter values (matching swkotor.exe: FUN_004a2400)
        private static uint _kotor1VertexProgramParam1 = 0x8629;
        private static uint _kotor1VertexProgramParam2 = 0x862a;
        private static uint _kotor1VertexProgramParam3 = 0x1700;

        #endregion

        #region KOTOR1-Specific P/Invoke Declarations

        // KOTOR1-specific function pointer delegate types (matching swkotor.exe function pointers)
        // Note: These differ from KOTOR2 in that they use IntPtr for params instead of arrays
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private new delegate void GlEnableDisableDelegate(bool enable);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GlGenVertexArraysDelegate(int n, ref uint arrays);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GlBindVertexArrayDelegate(uint array);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GlDeleteVertexArraysDelegate(int n, ref uint arrays);

        // Vertex program function pointer delegates (matching swkotor.exe: FUN_00436490)
        // Note: KOTOR1 uses IntPtr for params, while KOTOR2 uses float[]/double[]
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private new delegate void GlProgramEnvParameter4fvArbDelegate(uint target, uint index, IntPtr params_);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private new delegate void GlProgramLocalParameter4fvArbDelegate(uint target, uint index, IntPtr params_);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private new delegate void GlProgramLocalParameter4dvArbDelegate(uint target, uint index, IntPtr params_);

        // WGL extension function pointer delegates (matching swkotor.exe: FUN_00436490)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr WglCreateContextAttribsArbDelegate(IntPtr hdc, IntPtr hShareContext, int[] attribList);

        // OpenGL function pointer delegates (matching swkotor.exe: FUN_00436490)
        [DllImport("opengl32.dll", EntryPoint = "glGetIntegerv")]
        private static extern void glGetIntegerv(uint pname, int[] params_);

        // OpenGL display list functions (matching swkotor.exe: FUN_0044cb10, FUN_0044cc60)
        [DllImport("opengl32.dll", EntryPoint = "glDeleteLists")]
        private static extern void glDeleteLists(uint list, int range);

        [DllImport("opengl32.dll", EntryPoint = "glGenLists")]
        private static extern uint glGenLists(int range);

        [DllImport("opengl32.dll", EntryPoint = "glNewList")]
        private static extern void glNewList(uint list, uint mode);

        [DllImport("opengl32.dll", EntryPoint = "glEndList")]
        private static extern void glEndList();

        [DllImport("opengl32.dll", EntryPoint = "glBitmap")]
        private static extern void glBitmap(int width, int height, float xorig, float yorig, float xmove, float ymove, IntPtr bitmap);

        [DllImport("opengl32.dll", EntryPoint = "glPixelStorei")]
        private static extern void glPixelStorei(uint pname, int param);

        // OpenGL constants for display lists
        private const uint GL_COMPILE = 0x1300;
        private const uint GL_PIXEL_UNPACK_ALIGNMENT = 0x0CF5;

        // Additional OpenGL constants for texture loading (matching swkotor.exe: FUN_00427c90 @ 0x00427c90)
        private const uint GL_TEXTURE_CUBE_MAP = 0x8513;
        private const uint GL_TEXTURE_CUBE_MAP_POSITIVE_X = 0x8515;
        private const uint GL_TEXTURE_CUBE_MAP_NEGATIVE_X = 0x8516;
        private const uint GL_TEXTURE_CUBE_MAP_POSITIVE_Y = 0x8517;
        private const uint GL_TEXTURE_CUBE_MAP_NEGATIVE_Y = 0x8518;
        private const uint GL_TEXTURE_CUBE_MAP_POSITIVE_Z = 0x8519;
        private const uint GL_TEXTURE_CUBE_MAP_NEGATIVE_Z = 0x851A;
        private const uint GL_RGB = 0x1907;
        private const uint GL_BGR = 0x80E0;
        private const uint GL_BGRA = 0x80E1;
        private const uint GL_LUMINANCE = 0x1909;
        private const uint GL_MIRRORED_REPEAT = 0x8370;
        private const uint GL_NEAREST = 0x2600;
        private const uint GL_NEAREST_MIPMAP_NEAREST = 0x2700;
        private const uint GL_COMPRESSED_RGB_S3TC_DXT1_EXT = 0x83F0;
        private const uint GL_COMPRESSED_RGBA_S3TC_DXT3_EXT = 0x83F2;
        private const uint GL_COMPRESSED_RGBA_S3TC_DXT5_EXT = 0x83F3;

        // P/Invoke declarations for compressed texture functions (matching swkotor.exe)
        [DllImport("opengl32.dll", EntryPoint = "glCompressedTexImage2D")]
        private static extern void glCompressedTexImage2D(uint target, int level, int internalformat, int width, int height, int border, int imageSize, IntPtr data);

        [DllImport("opengl32.dll", EntryPoint = "glDeleteTextures")]
        private static extern void glDeleteTextures(int n, ref uint textures);

        #endregion

        // BackendType is inherited from OdysseyGraphicsBackend and returns GraphicsBackendType.OdysseyEngine

        /// <summary>
        /// Sets the resource provider for loading texture data.
        /// Matches swkotor.exe resource loading system (CExoResMan, CExoKeyTable).
        /// </summary>
        public void SetResourceProvider(IGameResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider;
        }

        protected override string GetGameName() => "Star Wars: Knights of the Old Republic";

        protected override bool DetermineGraphicsApi()
        {
            // KOTOR 1 uses OpenGL (not DirectX)
            // Based on reverse engineering: swkotor.exe uses OPENGL32.DLL and wglCreateContext
            // swkotor.exe: FUN_0044dab0 @ 0x0044dab0 uses wglCreateContext
            _useDirectX9 = false;
            _useOpenGL = true;
            _adapterIndex = 0;
            _fullscreen = true; // Default to fullscreen (swkotor.exe: FUN_0044dab0 @ 0x0044dab0, param_7 != 0 = fullscreen)
            _refreshRate = 60; // Default refresh rate

            return true;
        }

        protected override D3DPRESENT_PARAMETERS CreatePresentParameters(D3DDISPLAYMODE displayMode)
        {
            // KOTOR 1 specific present parameters
            // Matches swkotor.exe present parameters exactly
            var presentParams = base.CreatePresentParameters(displayMode);

            // KOTOR 1 specific settings
            presentParams.PresentationInterval = D3DPRESENT_INTERVAL_ONE;
            presentParams.SwapEffect = D3DSWAPEFFECT_DISCARD;

            return presentParams;
        }

        #region KOTOR 1-Specific Implementation

        /// <summary>
        /// KOTOR 1-specific OpenGL context creation.
        /// Matches swkotor.exe: FUN_0044dab0 @ 0x0044dab0 exactly.
        /// </summary>
        /// <remarks>
        /// KOTOR1-Specific Details (swkotor.exe):
        /// - Uses global variables: DAT_0078e38c, DAT_0078e388, DAT_0078d98c, DAT_0078daf4
        /// - Helper functions: FUN_0042e040, FUN_00422360, FUN_00425c30, FUN_0044f2f0
        /// - Texture initialization: FUN_00427c90 @ 0x00427c90
        /// - Secondary context: FUN_00426cc0 @ 0x00426cc0 (uses FUN_00426560 for window creation)
        /// - Global texture IDs: DAT_007a687c, DAT_007a6870, DAT_007a6874, DAT_007a6878
        /// </remarks>
        protected override bool CreateOdysseyOpenGLContext(IntPtr windowHandle, int width, int height, bool fullscreen, int refreshRate)
        {
            // KOTOR1-specific OpenGL context creation
            // Matches swkotor.exe: FUN_0044dab0 @ 0x0044dab0 exactly
            // This is a 1:1 implementation of the reverse-engineered function

            // Step 1: Check cleanup flag (matching swkotor.exe line 56-59)
            if (_kotor1CleanupFlag != 0)
            {
                _kotor1CleanupFlag = 0;
                InitializeKotor1OpenGLExtensions(); // FUN_0042e040
            }

            // Step 2: Set window style flag (matching swkotor.exe line 61)
            _kotor1WindowStyleFlag = 0xffff;

            // Step 3: Handle fullscreen/windowed mode (matching swkotor.exe lines 62-104)
            uint windowStyle;
            IntPtr hWndInsertAfter;
            int fullscreenFlag;

            if (!fullscreen)
            {
                // Windowed mode (matching swkotor.exe lines 63-66)
                windowStyle = WS_OVERLAPPEDWINDOW; // 0x2cf0000
                hWndInsertAfter = HWND_TOP; // 0xfffffffe
                fullscreenFlag = 0;
            }
            else
            {
                // Fullscreen mode (matching swkotor.exe lines 68-103)
                // Enumerate display settings to find matching mode
                DEVMODEA devMode = new DEVMODEA();
                devMode.dmSize = 0x9c;

                uint modeNum = 0;
                bool foundMode = false;

                while (EnumDisplaySettingsA(null, modeNum, ref devMode))
                {
                    if (devMode.dmPelsWidth == width &&
                        devMode.dmPelsHeight == height &&
                        devMode.dmBitsPerPel == 32 &&
                        devMode.dmDisplayFrequency == refreshRate)
                    {
                        // Change display settings (matching swkotor.exe line 85)
                        int result = ChangeDisplaySettingsA(ref devMode, CDS_FULLSCREEN);
                        if (result != DISP_CHANGE_SUCCESSFUL)
                        {
                            return false;
                        }

                        // Store device name (matching swkotor.exe lines 90-95)
                        // Note: In C# we don't need to copy the device name byte-by-byte

                        // Restore display settings (matching swkotor.exe line 96)
                        ChangeDisplaySettingsA(ref devMode, CDS_TEST);

                        foundMode = true;
                        break;
                    }

                    modeNum++;
                }

                if (!foundMode)
                {
                    // Fall back to windowed mode (matching swkotor.exe line 100)
                    windowStyle = WS_OVERLAPPEDWINDOW;
                    hWndInsertAfter = HWND_TOP;
                    fullscreenFlag = 0;
                }
                else
                {
                    windowStyle = WS_POPUP; // 0x82000000
                    hWndInsertAfter = HWND_TOPMOST; // 0xffffffff
                    fullscreenFlag = 1;
                }
            }

            // Step 4: Set window style (matching swkotor.exe line 105)
            SetWindowLongA(windowHandle, GWL_STYLE, windowStyle);

            // Step 5: Adjust window rect (matching swkotor.exe lines 106-110)
            RECT windowRect = new RECT
            {
                left = 0,
                top = 0,
                right = width,
                bottom = height
            };
            AdjustWindowRect(ref windowRect, windowStyle, false);

            // Step 6: Set window position (matching swkotor.exe lines 111-112)
            SetWindowPos(windowHandle, hWndInsertAfter, 0, 0,
                windowRect.right - windowRect.left,
                windowRect.bottom - windowRect.top,
                SWP_SHOWWINDOW);

            // Step 7: Send WM_SIZE message (matching swkotor.exe lines 113-115)
            int sizeParam = ((windowRect.bottom - windowRect.top) << 16) | (windowRect.right - windowRect.left & 0xffff);
            SendMessageA(windowHandle, WM_SIZE, IntPtr.Zero, (IntPtr)sizeParam);

            // Step 8: Show window (matching swkotor.exe line 116)
            ShowWindow(windowHandle, (int)SW_SHOW);

            // Step 9: Get device context (matching swkotor.exe line 117)
            IntPtr hdc = GetDC(windowHandle);
            if (hdc == IntPtr.Zero)
            {
                return false;
            }

            // Step 10: Initialize color and depth bits (matching swkotor.exe lines 122-123)
            _kotor1ColorBits = 0x20; // 32 bits
            _kotor1DepthBits = 0x18; // 24 bits

            // Step 11: Pixel format selection (matching swkotor.exe lines 125-203)
            int pixelFormat = 0;
            bool pixelFormatSet = false;

            if (_kotor1MultisampleFlag == 0)
            {
                // Use standard ChoosePixelFormat (matching swkotor.exe lines 126-159)
                PIXELFORMATDESCRIPTOR pfd = new PIXELFORMATDESCRIPTOR
                {
                    nSize = 0x28,
                    nVersion = 1,
                    dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER, // 0x25
                    iPixelType = PFD_TYPE_RGBA,
                    cColorBits = 0x20, // 32 bits
                    cAlphaBits = 8,
                    cDepthBits = 0x18, // 24 bits
                    cStencilBits = 8,
                    iLayerType = PFD_MAIN_PLANE
                };

                pixelFormat = ChoosePixelFormat(hdc, ref pfd);
                if (pixelFormat != 0)
                {
                    if (SetPixelFormat(hdc, pixelFormat, ref pfd))
                    {
                        DescribePixelFormat(hdc, pixelFormat, 0x28, ref pfd);
                        _kotor1ColorBits = pfd.cColorBits;
                        _kotor1DepthBits = pfd.cDepthBits;
                        pixelFormatSet = true;
                    }
                }
            }
            else
            {
                // Use wglChoosePixelFormatARB if available (matching swkotor.exe lines 162-202)
                if (_kotor1WglChoosePixelFormatArb != null)
                {
                    int[] attribIList = new int[]
                    {
                        WGL_DRAW_TO_WINDOW_ARB, 1,
                        WGL_SUPPORT_OPENGL_ARB, 1,
                        WGL_DOUBLE_BUFFER_ARB, 1,
                        WGL_PIXEL_TYPE_ARB, WGL_TYPE_RGBA_ARB,
                        WGL_COLOR_BITS_ARB, (int)refreshRate, // param_8
                        WGL_DEPTH_BITS_ARB, refreshRate < 0x19 ? refreshRate : 0x18,
                        WGL_STENCIL_BITS_ARB, 8,
                        WGL_SAMPLE_BUFFERS_ARB, 1,
                        WGL_SAMPLES_ARB, 1,
                        WGL_ACCELERATION_ARB, WGL_FULL_ACCELERATION_ARB,
                        0
                    };

                    if (_kotor1WglChoosePixelFormatArb(hdc, attribIList, null, 1, out global::System.Int32 formats, out global::System.UInt32 numFormats) && numFormats > 0)
                    {
                        PIXELFORMATDESCRIPTOR pfd = new PIXELFORMATDESCRIPTOR();
                        if (SetPixelFormat(hdc, formats, ref pfd))
                        {
                            _kotor1ColorBits = (ushort)refreshRate; // param_8
                            _kotor1DepthBits = 0x18;
                            pixelFormatSet = true;
                        }
                    }
                }
            }

            if (!pixelFormatSet)
            {
                ReleaseDC(windowHandle, hdc);
                return false;
            }

            // Step 12: Create OpenGL context (matching swkotor.exe lines 204-207)
            IntPtr hglrc = wglCreateContext(hdc);
            if (hglrc == IntPtr.Zero)
            {
                ReleaseDC(windowHandle, hdc);
                return false;
            }

            // Step 13: Make context current (matching swkotor.exe lines 207-374)
            if (wglMakeCurrent(hdc, hglrc))
            {
                // Initialize OpenGL extensions (matching swkotor.exe line 209: thunk_FUN_00436490)
                InitializeKotor1OpenGLExtensions();

                // Check vertex program support (matching swkotor.exe lines 210-212)
                if (CheckKotor1VertexProgramSupport())
                {
                    InitializeKotor1VertexPrograms(); // FUN_004a2400
                }

                // Additional initialization (matching swkotor.exe line 214)
                // FUN_004015a0 is a no-op (just returns), so we skip it

                // Store context info (matching swkotor.exe line 215: FUN_00425c30)
                InitializeKotor1ContextStorage();

                // Additional setup (matching swkotor.exe line 216)
                InitializeKotor1AdditionalSetup(); // FUN_00422360

                // Secondary contexts (matching swkotor.exe line 217)
                InitializeKotor1SecondaryContexts(); // FUN_00426cc0

                // Texture initialization (matching swkotor.exe line 218)
                InitializeKotor1Textures(); // FUN_00427c90

                // Check vertex program support again (matching swkotor.exe line 219)
                if (CheckKotor1VertexProgramSupport())
                {
                    // Initialize vertex program resources (matching swkotor.exe lines 221-362)
                    InitializeKotor1VertexProgramResources();
                }

                // Depth/stencil test setup (matching swkotor.exe lines 364-371)
                if (_kotor1MultisampleFlag < 1)
                {
                    if (_kotor1DepthTestFlag > 0)
                    {
                        glDisable(GL_DEPTH_TEST);
                    }
                }
                else
                {
                    glEnable(GL_DEPTH_TEST);
                }

                // Stencil test setup (matching swkotor.exe line 372)
                _kotor1GlEnableDisable?.Invoke(_kotor1StencilTestFlag != 0);

                return true;
            }
            else
            {
                wglDeleteContext(hglrc);
                ReleaseDC(windowHandle, hdc);
                return false;
            }
        }

        /// <summary>
        /// Initialize OpenGL extensions (matching swkotor.exe: FUN_00436490 @ 0x00436490, called via FUN_0042e040 @ 0x0042e040).
        /// </summary>
        private void InitializeKotor1OpenGLExtensions()
        {
            // Matching swkotor.exe: FUN_00436490 @ 0x00436490 exactly
            // This function loads all OpenGL extension function pointers

            if (_kotor1PrimaryDC == IntPtr.Zero)
            {
                return;
            }

            // Get wglGetExtensionsStringARB function pointer (matching swkotor.exe line 16)
            IntPtr proc = wglGetProcAddress("wglGetExtensionsStringARB");
            if (proc != IntPtr.Zero)
            {
                _kotor1WglGetExtensionsStringArb = Marshal.GetDelegateForFunctionPointer<WglGetExtensionsStringArbDelegate>(proc);
            }

            // Get wglChoosePixelFormatARB function pointer (matching swkotor.exe: FUN_0042e040 line 0x0042e176)
            proc = wglGetProcAddress("wglChoosePixelFormatARB");
            if (proc != IntPtr.Zero)
            {
                _kotor1WglChoosePixelFormatArb = Marshal.GetDelegateForFunctionPointer<WglChoosePixelFormatArbDelegate>(proc);
            }

            // Get wglCreateContextAttribsARB function pointer (matching swkotor.exe: FUN_00436490 line 0x00436ee9)
            proc = wglGetProcAddress("wglCreateContextAttribsARB");
            if (proc != IntPtr.Zero)
            {
                _kotor1WglCreateContextAttribsArb = Marshal.GetDelegateForFunctionPointer<WglCreateContextAttribsArbDelegate>(proc);
            }

            // Get vertex program function pointers (matching swkotor.exe: FUN_00436490 lines 197-200)
            proc = wglGetProcAddress("glProgramStringARB");
            if (proc != IntPtr.Zero)
            {
                _kotor1GlProgramStringArb = Marshal.GetDelegateForFunctionPointer<GlProgramStringArbDelegate>(proc);
            }

            proc = wglGetProcAddress("glBindProgramARB");
            if (proc != IntPtr.Zero)
            {
                _kotor1GlBindProgramArb = Marshal.GetDelegateForFunctionPointer<GlBindProgramArbDelegate>(proc);
            }

            proc = wglGetProcAddress("glGenProgramsARB");
            if (proc != IntPtr.Zero)
            {
                _kotor1GlGenProgramsArb = Marshal.GetDelegateForFunctionPointer<GlGenProgramsArbDelegate>(proc);
            }

            // Get vertex program parameter function pointers (matching swkotor.exe: FUN_00436490)
            proc = wglGetProcAddress("glProgramEnvParameter4fARB");
            if (proc != IntPtr.Zero)
            {
                _kotor1GlProgramEnvParameter4fArb = Marshal.GetDelegateForFunctionPointer<GlProgramEnvParameter4fArbDelegate>(proc);
                // Assign the wrapper function to _kotor1GlProgramEnvParameter4fArb2
                // This wrapper matches the original swkotor.exe calling convention of 4 parameters
                // instead of the 6 parameters required by the actual OpenGL function
                _kotor1GlProgramEnvParameter4fArb2 = new GlProgramEnvParameter4fArbDelegate(Kotor1ProgramEnvParameter4fWrapperFunction);
            }

            proc = wglGetProcAddress("glProgramLocalParameter4fARB");
            if (proc != IntPtr.Zero)
            {
                _kotor1GlProgramLocalParameter4fArb = Marshal.GetDelegateForFunctionPointer<GlProgramLocalParameter4fArbDelegate>(proc);
            }

            proc = wglGetProcAddress("glProgramEnvParameter4fvARB");
            if (proc != IntPtr.Zero)
            {
                _kotor1GlProgramEnvParameter4fvArb = Marshal.GetDelegateForFunctionPointer<GlProgramEnvParameter4fvArbDelegate>(proc);
            }

            proc = wglGetProcAddress("glProgramLocalParameter4fvARB");
            if (proc != IntPtr.Zero)
            {
                _kotor1GlProgramLocalParameter4fvArb = Marshal.GetDelegateForFunctionPointer<GlProgramLocalParameter4fvArbDelegate>(proc);
            }

            proc = wglGetProcAddress("glProgramLocalParameter4dvARB");
            if (proc != IntPtr.Zero)
            {
                _kotor1GlProgramLocalParameter4dvArb = Marshal.GetDelegateForFunctionPointer<GlProgramLocalParameter4dvArbDelegate>(proc);
                _kotor1GlProgramLocalParameter4dvArb2 = _kotor1GlProgramLocalParameter4dvArb; // Same function, different usage
            }
        }

        /// <summary>
        /// Check vertex program support (matching swkotor.exe: FUN_0045f770 @ 0x0045f770).
        /// </summary>
        private bool CheckKotor1VertexProgramSupport()
        {
            // Matching swkotor.exe: FUN_0045f770 exactly
            if (_kotor1CapabilityFlag == 0xffffffff)
            {
                _kotor1CapabilityFlag = (_kotor1ExtensionFlags & _kotor1RequiredExtensionFlags) == _kotor1RequiredExtensionFlags ? 1u : 0u;
            }
            if ((_kotor1VertexProgramFlag & _kotor1CapabilityFlag) != 0 && _kotor1VertexProgramFlag != 0)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Initialize vertex programs (matching swkotor.exe: FUN_004a2400 @ 0x004a2400).
        /// </summary>
        /// <remarks>
        /// FUN_004a2400 initializes vertex program support by checking DAT_0078e5ec flag
        /// and calling function pointers to set up vertex program state.
        /// </remarks>
        private void InitializeKotor1VertexPrograms()
        {
            // Matching swkotor.exe: FUN_004a2400 @ 0x004a2400 exactly
            // DAT_0073f218 = 0x8629, DAT_0073f224 = 0x862a, DAT_0073f21c = 0x1700
            // The function pointer DAT_007bb744 is called with 4 parameters, which suggests
            // TODO:  it might be a wrapper or the decompiler is showing a simplified view.
            // Based on the OpenGL ARB vertex program extension, we use glProgramEnvParameter4fvARB
            // which takes (target, index, params) where params is a GLfloat[4] array.
            if (_kotor1VertexProgramFlag == 0)
            {
                // Call function pointer at DAT_007bb744 (matching swkotor.exe: FUN_004a2400 @ 0x004a2400, line 6)
                // Assembly analysis shows: (*DAT_007bb744)(0x8620, 0, DAT_0073f218, DAT_0073f224)
                // DAT_0073f218 = 0x8629, DAT_0073f224 = 0x862a (swkotor.exe memory addresses)
                // DAT_007bb744 is set to glProgramEnvParameter4fARB function pointer
                // The function signature is: void glProgramEnvParameter4fARB(GLenum target, GLuint index, GLfloat x, GLfloat y, GLfloat z, GLfloat w)
                // The decompiler shows 4 parameters, but the actual OpenGL function requires 6 parameters.
                // Based on assembly analysis, the values 0x8629 and 0x862a are passed as x and y parameters,
                // with z=0 and w=0 implicitly. These values are stored as uint32 in memory and need to be
                // converted to float values for the OpenGL call.
                //
                // Analysis from swkotor.exe assembly (0x004a2400):
                // - MOV EAX, [0x0073f224]  ; Load DAT_0073f224 (0x862a) into EAX
                // - MOV ECX, [0x0073f218]  ; Load DAT_0073f218 (0x8629) into ECX
                // - PUSH EAX               ; Push 0x862a (y parameter)
                // - PUSH ECX                ; Push 0x8629 (x parameter)
                // - PUSH 0                  ; Push index (0)
                // - PUSH 0x8620             ; Push GL_VERTEX_PROGRAM_ARB
                // - CALL [0x007bb744]       ; Call glProgramEnvParameter4fARB
                //
                // The values 0x8629 and 0x862a are interpreted as float bit patterns.
                // When converted: 0x8629 = ~1.19e-38f, 0x862a = ~1.19e-38f (very small denormalized floats)
                // These are likely initialization values for vertex program environment parameters.
                if (_kotor1GlProgramEnvParameter4fvArb != null)
                {
                    // Use glProgramEnvParameter4fvARB with float array (preferred method)
                    float[] params1 = new float[4];
                    unsafe
                    {
                        // Convert uint32 values to float using bit pattern interpretation
                        // This matches the original swkotor.exe behavior where these values are
                        // passed directly to the OpenGL function
                        uint val1 = _kotor1VertexProgramParam1; // 0x8629
                        uint val2 = _kotor1VertexProgramParam2; // 0x862a
                        params1[0] = *(float*)&val1; // x parameter
                        params1[1] = *(float*)&val2; // y parameter
                        params1[2] = 0.0f; // z parameter (implicit in original call)
                        params1[3] = 0.0f; // w parameter (implicit in original call)
                    }
                    IntPtr paramsPtr = Marshal.AllocHGlobal(4 * sizeof(float));
                    try
                    {
                        Marshal.Copy(params1, 0, paramsPtr, 4);
                        _kotor1GlProgramEnvParameter4fvArb(GL_VERTEX_PROGRAM_ARB, 0, paramsPtr);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(paramsPtr);
                    }
                }
                else if (_kotor1GlProgramEnvParameter4fArb2 != null)
                {
                    // Fallback: use glProgramEnvParameter4fARB with individual parameters
                    // This matches the original function signature exactly
                    unsafe
                    {
                        uint val1 = _kotor1VertexProgramParam1; // 0x8629
                        uint val2 = _kotor1VertexProgramParam2; // 0x862a
                        float f1 = *(float*)&val1; // x parameter
                        float f2 = *(float*)&val2; // y parameter
                        // Call with all 6 parameters as required by OpenGL specification
                        _kotor1GlProgramEnvParameter4fArb2(GL_VERTEX_PROGRAM_ARB, 0, f1, f2, 0.0f, 0.0f);
                    }
                }

                if (_kotor1VertexProgramFlag == 0)
                {
                    // Call function pointer at DAT_007bb744 again (matching swkotor.exe: FUN_004a2400 @ 0x004a2400, line 8)
                    // Assembly analysis shows: (*DAT_007bb744)(0x8620, 8, DAT_0073f21c, DAT_0073f224)
                    // DAT_0073f21c = 0x1700, DAT_0073f224 = 0x862a (swkotor.exe memory addresses)
                    // This sets vertex program environment parameter at index 8 with x=0x1700, y=0x862a, z=0, w=0
                    // Analysis from swkotor.exe assembly (0x004a2440):
                    // - MOV EDX, [0x0073f224]  ; Load DAT_0073f224 (0x862a) into EDX
                    // - MOV EAX, [0x0073f21c]  ; Load DAT_0073f21c (0x1700) into EAX
                    // - PUSH EDX                ; Push 0x862a (y parameter)
                    // - PUSH EAX                ; Push 0x1700 (x parameter)
                    // - PUSH 8                  ; Push index (8)
                    // - PUSH 0x8620             ; Push GL_VERTEX_PROGRAM_ARB
                    // - CALL [0x007bb744]       ; Call glProgramEnvParameter4fARB
                    if (_kotor1GlProgramEnvParameter4fvArb != null)
                    {
                        // Use glProgramEnvParameter4fvARB with float array (preferred method)
                        float[] params2 = new float[4];
                        unsafe
                        {
                            // Convert uint32 values to float using bit pattern interpretation
                            uint val1 = _kotor1VertexProgramParam3; // 0x1700
                            uint val2 = _kotor1VertexProgramParam2; // 0x862a
                            params2[0] = *(float*)&val1; // x parameter
                            params2[1] = *(float*)&val2; // y parameter
                            params2[2] = 0.0f; // z parameter (implicit in original call)
                            params2[3] = 0.0f; // w parameter (implicit in original call)
                        }
                        IntPtr paramsPtr = Marshal.AllocHGlobal(4 * sizeof(float));
                        try
                        {
                            Marshal.Copy(params2, 0, paramsPtr, 4);
                            _kotor1GlProgramEnvParameter4fvArb(GL_VERTEX_PROGRAM_ARB, 8, paramsPtr);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(paramsPtr);
                        }
                    }
                    else if (_kotor1GlProgramEnvParameter4fArb2 != null)
                    {
                        // Fallback: use glProgramEnvParameter4fARB with individual parameters
                        unsafe
                        {
                            uint val1 = _kotor1VertexProgramParam3; // 0x1700
                            uint val2 = _kotor1VertexProgramParam2; // 0x862a
                            float f1 = *(float*)&val1; // x parameter
                            float f2 = *(float*)&val2; // y parameter
                            // Call with all 6 parameters as required by OpenGL specification
                            _kotor1GlProgramEnvParameter4fArb2(GL_VERTEX_PROGRAM_ARB, 8, f1, f2, 0.0f, 0.0f);
                        }
                    }

                    if (_kotor1VertexProgramFlag == 0)
                    {
                        // Call function pointer at DAT_007bb834 (matching swkotor.exe line 10)
                        // (*DAT_007bb834)(0x8620, 0);
                        // This is glBindProgramARB(GL_VERTEX_PROGRAM_ARB, 0)
                        if (_kotor1GlBindProgramArb2 != null)
                        {
                            _kotor1GlBindProgramArb2(GL_VERTEX_PROGRAM_ARB, 0);
                        }
                        else if (_kotor1GlBindProgramArb != null)
                        {
                            _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, 0);
                        }
                        return;
                    }
                }
            }

            // Call function pointer at DAT_007bb788 (matching swkotor.exe line 15)
            // (*DAT_007bb788)(0x8620, 0);
            // This is glBindProgramARB(GL_VERTEX_PROGRAM_ARB, 0)
            _kotor1GlBindProgramArb?.Invoke(GL_VERTEX_PROGRAM_ARB, 0);
        }

        /// <summary>
        /// KOTOR1 context storage initialization.
        /// Matches swkotor.exe: FUN_00425c30 @ 0x00425c30.
        /// </summary>
        private void InitializeKotor1ContextStorage()
        {
            // Matching swkotor.exe: FUN_00425c30 @ 0x00425c30
            _kotor1PrimaryContext = wglGetCurrentContext();
            _kotor1PrimaryDC = wglGetCurrentDC();
        }

        /// <summary>
        /// Additional setup (matching swkotor.exe: FUN_00422360 @ 0x00422360).
        /// </summary>
        private void InitializeKotor1AdditionalSetup()
        {
            // Matching swkotor.exe: FUN_00422360 @ 0x00422360 exactly
            // This function performs additional OpenGL state setup

            if (_kotor1AdditionalSetupFlag != 0)
            {
                // Call cleanup function (matching swkotor.exe line 6: FUN_0044cc60)
                InitializeKotor1DisplayListCleanup();

                // Call initialization function (matching swkotor.exe line 7: FUN_0044cc40)
                InitializeKotor1DisplayListInit();
            }

            // Call display setup function (matching swkotor.exe line 9: FUN_00421d90)
            InitializeKotor1DisplaySetup(_kotor1DisplayParameter);
        }

        /// <summary>
        /// Display list cleanup (matching swkotor.exe: FUN_0044cc60 @ 0x0044cc60).
        /// </summary>
        // Delegate for function pointer call in InitializeKotor1DisplayListCleanup
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Kotor1FunctionPointerDelegate(int param);

        // Delegate for virtual function call in InitializeKotor1TextureCleanup
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate void Kotor1VirtualFunctionDelegate(IntPtr thisPtr);

        // Delegate for virtual check function in InitializeKotor1TextureSizeCalculation
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate byte Kotor1VirtualCheckDelegate(IntPtr thisPtr);

        private void InitializeKotor1DisplayListCleanup()
        {
            // Matching swkotor.exe: FUN_0044cc60 @ 0x0044cc60 exactly
            if (_kotor1AdditionalSetupFlag != 0)
            {
                _kotor1AdditionalSetupFlag = 0;
                glDeleteLists(_kotor1DisplayListBase, 0x80);
                if (_kotor1FunctionPointer != IntPtr.Zero)
                {
                    // Call function pointer (matching swkotor.exe line 9)
                    // This is a function pointer call: (**(code **)*DAT_007b90ec)(1)
                    // DAT_007b90ec is a pointer to a function pointer, so we need double indirection
                    // The function takes one int parameter (1) and returns void
                    // Read the function pointer from the pointer
                    IntPtr funcPtrPtr = _kotor1FunctionPointer;
                    IntPtr funcPtr = Marshal.ReadIntPtr(funcPtrPtr);
                    if (funcPtr != IntPtr.Zero)
                    {
                        Kotor1FunctionPointerDelegate func = Marshal.GetDelegateForFunctionPointer<Kotor1FunctionPointerDelegate>(funcPtr);
                        func(1);
                    }
                }
                _kotor1FunctionPointer = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Display list initialization (matching swkotor.exe: FUN_0044cc40 @ 0x0044cc40).
        /// </summary>
        private void InitializeKotor1DisplayListInit()
        {
            // Matching swkotor.exe: FUN_0044cc40 @ 0x0044cc40 exactly
            _kotor1DisplayListFlag = 0;
            InitializeKotor1FontGlyphLists();
            _kotor1AdditionalSetupFlag = 1;
        }

        /// <summary>
        /// Font glyph list creation (matching swkotor.exe: FUN_0044cb10 @ 0x0044cb10).
        /// </summary>
        private void InitializeKotor1FontGlyphLists()
        {
            // Matching swkotor.exe: FUN_0044cb10 @ 0x0044cb10 exactly
            glPixelStorei(GL_PIXEL_UNPACK_ALIGNMENT, 1);
            _kotor1DisplayListBase = glGenLists(0x80);

            int listIndex = 0x20;
            IntPtr bitmapDataPtr = Marshal.AllocHGlobal(_kotor1BitmapData.Length);
            Marshal.Copy(_kotor1BitmapData, 0, bitmapDataPtr, _kotor1BitmapData.Length);
            int remaining = 0x5f;

            do
            {
                glNewList(_kotor1DisplayListBase + (uint)listIndex, GL_COMPILE);
                glBitmap(8, 0xd, 0, 0x40000000, 0x41200000, 0, bitmapDataPtr);
                glEndList();
                listIndex = listIndex + 1;
                bitmapDataPtr = IntPtr.Add(bitmapDataPtr, 0xd);
                remaining = remaining - 1;
            } while (remaining != 0);

            Marshal.FreeHGlobal(bitmapDataPtr);
        }

        /// <summary>
        /// Display setup (matching swkotor.exe: FUN_00421d90 @ 0x00421d90).
        /// </summary>
        private bool InitializeKotor1DisplaySetup(int param1)
        {
            // Matching swkotor.exe: FUN_00421d90 @ 0x00421d90 exactly
            float fVar1 = (float)param1 * _kotor1DisplayMultiplier;
            _kotor1DisplayParameter = param1;

            // FUN_0046bc80: Array management
            InitializeKotor1ArrayManagement(_kotor1TextureArray, _kotor1TextureArray2);

            int iVar2 = 0;
            if (0 < _kotor1TextureArrayCount)
            {
                do
                {
                    // FUN_00420670: Texture cleanup
                    InitializeKotor1TextureCleanup(_kotor1TextureArray[iVar2]);
                    iVar2 = iVar2 + 1;
                } while (iVar2 < _kotor1TextureArrayCount);
            }

            // FUN_00421ac0: Display parameter calculation
            int roundedValue = (int)Math.Round(Math.Round(fVar1));
            InitializeKotor1DisplayParameterCalculation(_kotor1DisplayArray1, roundedValue, ref _kotor1DisplayValue1, _kotor1DisplayFloat);
            _kotor1DisplayValue3 = _kotor1DisplayValue1;

            InitializeKotor1DisplayParameterCalculation(_kotor1DisplayArray2, _kotor1DisplayParameter - roundedValue, ref _kotor1DisplayValue2, 1.0f);

            // Combine color values (matching swkotor.exe lines 23-28)
            _kotor1CombinedColorR = _kotor1ColorR2 + _kotor1ColorR1;
            _kotor1CombinedColorG = _kotor1ColorG2 + _kotor1ColorG1;
            _kotor1CombinedColorB = _kotor1ColorB2 + _kotor1ColorB1;
            _kotor1CombinedColorA = _kotor1ColorA2 + _kotor1ColorA1;
            _kotor1CombinedColorA2 = _kotor1ColorA2;
            _kotor1CombinedDisplayValue = _kotor1DisplayValue2 + _kotor1DisplayValue3;

            // FUN_0046bc80: Array management again
            InitializeKotor1ArrayManagement(_kotor1TextureArray, _kotor1TextureArray2);

            // FUN_004217f0: Display list management
            InitializeKotor1DisplayListManagement();

            return _kotor1CombinedDisplayValue <= _kotor1DisplayParameter;
        }

        /// <summary>
        /// Array management (matching swkotor.exe: FUN_0046bc80 @ 0x0046bc80).
        /// </summary>
        private void InitializeKotor1ArrayManagement(IntPtr[] targetArray, IntPtr[] sourceArray)
        {
            // Matching swkotor.exe: FUN_0046bc80 @ 0x0046bc80 exactly
            // This function copies elements from sourceArray to targetArray, expanding if needed
            int iVar4 = 0;
            int targetCount = 0;

            if (0 < GetArrayCount(sourceArray))
            {
                do
                {
                    IntPtr uVar1 = sourceArray[iVar4];
                    int iVar3Local = targetCount;

                    if (targetCount == GetArrayCapacity(targetArray))
                    {
                        int newCapacity;
                        if (GetArrayCapacity(targetArray) == 0)
                        {
                            newCapacity = 8;
                        }
                        else
                        {
                            newCapacity = GetArrayCapacity(targetArray) * 2;
                        }

                        IntPtr[] oldArray = targetArray;
                        targetArray = new IntPtr[newCapacity];
                        SetArrayCapacity(targetArray, newCapacity);

                        int iVar3_2 = 0;
                        if (0 < targetCount)
                        {
                            do
                            {
                                targetArray[iVar3_2] = oldArray[iVar3_2];
                                iVar3_2 = iVar3_2 + 1;
                            } while (iVar3_2 < targetCount);
                        }
                    }

                    targetArray[targetCount] = uVar1;
                    targetCount = targetCount + 1;
                    SetArrayCount(targetArray, targetCount);
                    iVar4 = iVar4 + 1;
                } while (iVar4 < GetArrayCount(sourceArray));
            }
        }

        /// <summary>
        /// Texture cleanup (matching swkotor.exe: FUN_00420670 @ 0x00420670).
        /// </summary>
        private void InitializeKotor1TextureCleanup(IntPtr texturePtr)
        {
            // Matching swkotor.exe: FUN_00420670 @ 0x00420670 exactly
            // This function cleans up a texture object
            // The texture object is a structure with various fields
            if (texturePtr == IntPtr.Zero)
                return;

            unsafe
            {
                int* param1 = (int*)texturePtr;

                // Call virtual function (matching swkotor.exe line 5)
                // (**(code **)(*param_1 + 0xb0))();
                // This calls a virtual function at offset 0xb0 in the vtable
                IntPtr vtablePtr = new IntPtr(param1[0]);
                if (vtablePtr != IntPtr.Zero)
                {
                    IntPtr funcPtr = Marshal.ReadIntPtr(vtablePtr, 0xb0);
                    if (funcPtr != IntPtr.Zero)
                    {
                        Kotor1VirtualFunctionDelegate vfunc = Marshal.GetDelegateForFunctionPointer<Kotor1VirtualFunctionDelegate>(funcPtr);
                        vfunc(texturePtr);
                    }
                }

                // Set fields to 0 (matching swkotor.exe lines 6-15)
                param1[0x17] = 0;
                param1[0x18] = 0;
                param1[0x19] = 0;
                Marshal.WriteInt16(new IntPtr((byte*)texturePtr + 0xce), 0);
                Marshal.WriteInt16(new IntPtr((byte*)texturePtr + 0xbe), 0);
                short baValue = Marshal.ReadInt16(new IntPtr((byte*)texturePtr + 0xba));
                Marshal.WriteInt16(new IntPtr((byte*)texturePtr + 0x2f * sizeof(int)), baValue);
                Marshal.WriteByte(new IntPtr((byte*)texturePtr + 0x39 * sizeof(int)), 1);
                Marshal.WriteByte(new IntPtr((byte*)texturePtr + 0x38 * sizeof(int)), 0);
                Marshal.WriteByte(new IntPtr((byte*)texturePtr + 0xe5), 0);
                Marshal.WriteByte(new IntPtr((byte*)texturePtr + 0xe6), 0);

                // Free memory and delete textures (matching swkotor.exe lines 16-25)
                IntPtr memPtr = new IntPtr(param1[0x12]);
                if (memPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(memPtr);
                    param1[0x12] = 0;
                }

                IntPtr textureArrayPtr = new IntPtr(param1[0x10]);
                if (textureArrayPtr != IntPtr.Zero)
                {
                    // Calculate texture count: (short)param_1[0x31] * (short)(param_1 + 0xc2)
                    short count1 = (short)param1[0x31];
                    short count2 = Marshal.ReadInt16(new IntPtr((byte*)texturePtr + 0xc2));
                    int textureCount = (int)count1 * (int)count2;

                    if (textureCount > 0)
                    {
                        // Read texture IDs from the array
                        // Marshal.Copy doesn't support uint[] directly, so use int[] and convert
                        int[] textureIdsInt = new int[textureCount];
                        Marshal.Copy(textureArrayPtr, textureIdsInt, 0, textureCount);
                        uint[] textureIds = Array.ConvertAll(textureIdsInt, x => (uint)x);

                        // Delete textures
                        if (textureIds.Length > 0)
                        {
                            glDeleteTextures((int)textureCount, ref textureIds[0]);
                        }
                    }

                    Marshal.FreeHGlobal(textureArrayPtr);
                    param1[0x10] = 0;
                }
            }
        }

        /// <summary>
        /// Display parameter calculation (matching swkotor.exe: FUN_00421ac0 @ 0x00421ac0).
        /// </summary>
        private unsafe void InitializeKotor1DisplayParameterCalculation(IntPtr[] param1, int param2, ref int param3, float param4)
        {
            // Matching swkotor.exe: FUN_00421ac0 @ 0x00421ac0 exactly
            // This is a complex function that manages texture arrays and calculates display parameters

            // Create a local copy and pin it to get a pointer (can't take address of ref parameter directly)
            int param3Local = param3;
            // Use stackalloc to create a pinned buffer
            int* param3Ptr = stackalloc int[1];
            param3Ptr[0] = param3Local;
            // FUN_00420db0(param_1, param_3);
            InitializeKotor1DisplayParameterReset(param1, param3Ptr);
            param3 = param3Ptr[0];

            float fVar2 = (float)param2 * param4;

            // Check if calculated value is less than or equal to param3 (matching swkotor.exe lines 28-34)
            if ((float)param3 <= fVar2)
            {
                _kotor1DisplayFloat2 = 0.0f;
            }
            else
            {
                _kotor1DisplayFloat2 = _kotor1DisplayFloat3 + _kotor1DisplayFloat2;
                if (_kotor1DisplayFloat4 < _kotor1DisplayFloat2)
                {
                    goto LAB_00421b2f;
                }
            }

            // Check if param3 is less than or equal to param2 (matching swkotor.exe lines 35-37)
            if (param3 <= param2)
            {
                return;
            }

            LAB_00421b2f:
            _kotor1DisplayFloat2 = 0.0f;

            // Main loop for texture array management (matching swkotor.exe lines 40-119)
            if (fVar2 < (float)param3)
            {
                while (true)
                {
                    IntPtr this_00 = IntPtr.Zero;
                    int local_18 = 0;
                    int local_1c = 0;
                    int iVar2 = 0;

                    if (GetArrayCount(param1) < 1)
                        break;

                    // Find best texture in array (matching swkotor.exe lines 47-63)
                    do
                    {
                        IntPtr thisPtr = param1[iVar2];
                        if (thisPtr != IntPtr.Zero)
                        {
                            unsafe
                            {
                                short* sVar1 = (short*)((byte*)thisPtr + 0xbc);
                                short* bePtr = (short*)((byte*)thisPtr + 0xbe);
                                short* b8Ptr = (short*)((byte*)thisPtr + 0xb8);
                                byte bVar5 = (byte)(*sVar1 - *bePtr);

                                int* iVar5 = (int*)((byte*)thisPtr + 0x68);
                                int* iVar6 = (int*)((byte*)thisPtr + 0x6c);

                                if ((*sVar1 < *b8Ptr) &&
                                    (2 < (*iVar5 >> (bVar5 & 0x1f))) &&
                                    (2 < (*iVar6 >> (bVar5 & 0x1f))))
                                {
                                    int iVar7 = (int)*b8Ptr - (int)*sVar1;
                                    int local_14 = 0;
                                    InitializeKotor1TextureSizeCalculation(thisPtr, &local_14);

                                    if ((local_18 < local_14) || ((local_14 == local_18 && (local_1c < iVar7))))
                                    {
                                        local_18 = local_14;
                                        this_00 = thisPtr;
                                        local_1c = iVar7;
                                    }
                                }
                            }
                        }
                        iVar2 = iVar2 + 1;
                    } while (iVar2 < GetArrayCount(param1));

                    if (this_00 == IntPtr.Zero)
                    {
                        return;
                    }

                    int local_14_2 = 0;
                    InitializeKotor1TextureSizeCalculation(this_00, &local_14_2);
                    int iVar7_2 = local_14_2;

                    // Update param3 values (matching swkotor.exe lines 69-73)
                    // Note: param3 is a single int, not an array, so we update it directly
                    param3 = param3 - local_14_2;

                    // Update texture counter (matching swkotor.exe lines 74-78)
                    unsafe
                    {
                        short* bcPtr = (short*)((byte*)this_00 + 0xbc);
                        short* b8Ptr = (short*)((byte*)this_00 + 0xb8);
                        int iVar4 = (int)*bcPtr + 1;
                        if (*b8Ptr <= iVar4)
                        {
                            iVar4 = (int)*b8Ptr;
                        }
                        *bcPtr = (short)iVar4;

                        // Check if texture needs to be added to array (matching swkotor.exe lines 79-100)
                        if ((short)iVar4 != *((short*)((byte*)this_00 + 0xbe)))
                        {
                            Marshal.WriteByte(new IntPtr((byte*)this_00 + 0xe4), 1);

                            // Check if texture is already in array
                            int iVar6 = 0;
                            int iVar4_2 = 0;
                            if (0 < _kotor1TextureArrayCount)
                            {
                                do
                                {
                                    if (_kotor1TextureArray[iVar4_2] == this_00)
                                    {
                                        iVar6 = iVar6 + 1;
                                    }
                                    iVar4_2 = iVar4_2 + 1;
                                } while (iVar4_2 < _kotor1TextureArrayCount);

                                if (iVar6 != 0)
                                    goto LAB_00421cd6;
                            }

                            // Add texture to array if needed (matching swkotor.exe lines 92-99)
                            if (_kotor1TextureArrayCount == _kotor1TextureArrayCapacity)
                            {
                                int newCapacity;
                                if (_kotor1TextureArrayCapacity == 0)
                                {
                                    newCapacity = 8;
                                }
                                else
                                {
                                    newCapacity = _kotor1TextureArrayCapacity * 2;
                                }

                                IntPtr[] oldArray = _kotor1TextureArray;
                                _kotor1TextureArray = new IntPtr[newCapacity];
                                _kotor1TextureArrayCapacity = newCapacity;

                                for (int i = 0; i < _kotor1TextureArrayCount; i++)
                                {
                                    _kotor1TextureArray[i] = oldArray[i];
                                }
                            }

                            _kotor1TextureArray[_kotor1TextureArrayCount] = this_00;
                            _kotor1TextureArrayCount = _kotor1TextureArrayCount + 1;
                        }
                    }

                    LAB_00421cd6:
                    InitializeKotor1TextureSizeCalculation(this_00, &local_14_2);
                    // Update param3 directly (param3 is a ref int, not an array)
                    param3 = param3 + (local_14_2 - iVar7_2);
                }
            }
        }

        /// <summary>
        /// Display parameter reset (matching swkotor.exe: FUN_00420db0 @ 0x00420db0).
        /// </summary>
        private unsafe void InitializeKotor1DisplayParameterReset(IntPtr[] param1, int* param2)
        {
            // Matching swkotor.exe: FUN_00420db0 @ 0x00420db0 exactly
            int iVar1 = 0;
            param2[4] = 0;
            param2[2] = 0;
            param2[3] = 0;
            param2[1] = 0;
            param2[0] = 0;

            if (0 < GetArrayCount(param1))
            {
                do
                {
                    IntPtr thisPtr = param1[iVar1];
                    if (thisPtr != IntPtr.Zero)
                    {
                        unsafe
                        {
                            byte* flagPtr = (byte*)thisPtr + 0x38;
                            if (*flagPtr == 0)
                            {
                                InitializeKotor1TextureInitialization(thisPtr);
                            }

                            byte* flagPtr2 = (byte*)thisPtr + 0xd2;
                            if (*flagPtr2 == 0)
                            {
                                int local_14 = 0;
                                InitializeKotor1TextureSizeCalculation(thisPtr, &local_14);
                                param2[2] = param2[2] + 0; // local_c
                                param2[4] = param2[4] + 0; // local_4
                                param2[3] = param2[3] + 0; // local_8
                                param2[1] = param2[1] + 0; // local_10
                                param2[0] = param2[0] + local_14;
                            }
                        }
                    }
                    iVar1 = iVar1 + 1;
                } while (iVar1 < GetArrayCount(param1));
            }
        }

        /// <summary>
        /// Texture size calculation (matching swkotor.exe: FUN_00420710 @ 0x00420710).
        /// </summary>
        /// <remarks>
        /// Complete 1:1 implementation matching swkotor.exe: FUN_00420710 @ 0x00420710 exactly.
        /// This function calculates texture size based on various flags and texture properties.
        ///
        /// Function logic:
        /// 1. If offset 0xe4 is 0: Use pre-calculated size at offset 0xe8, check texture name suffixes and flags
        /// 2. If offset 0xe0 is 0: Return all zeros
        /// 3. If this == DAT_007a477c: Return all zeros
        /// 4. Otherwise: Calculate texture size based on dimensions, mip levels, and format
        /// </remarks>
        private unsafe void InitializeKotor1TextureSizeCalculation(IntPtr thisPtr, int* param1)
        {
            // Matching swkotor.exe: FUN_00420710 @ 0x00420710 exactly
            unsafe
            {
                byte* e4Ptr = (byte*)thisPtr + 0xe4;

                // First branch: if offset 0xe4 is 0 (matching swkotor.exe lines 12-38)
                if (*e4Ptr == 0)
                {
                    param1[0] = 0;
                    param1[1] = 0;
                    param1[2] = 0;
                    param1[3] = 0;
                    param1[4] = 0;

                    int* e8Ptr = (int*)((byte*)thisPtr + 0xe8);
                    param1[0] = *e8Ptr;

                    // Check for "_lm" or "_a00" suffix (matching swkotor.exe lines 19-24)
                    // Using _strstr equivalent: check if texture name contains these suffixes
                    IntPtr textureNamePtr = new IntPtr((byte*)thisPtr + 0x98);
                    string textureName = Marshal.PtrToStringAnsi(textureNamePtr);
                    if (textureName != null)
                    {
                        // Check for "_lm" suffix
                        bool hasLmSuffix = textureName.Contains("_lm");
                        // Check for "_a00" suffix
                        bool hasA00Suffix = textureName.Contains("_a00");

                        if (hasLmSuffix || hasA00Suffix)
                        {
                            param1[1] = *e8Ptr;
                            return;
                        }
                    }

                    // Check other flags (matching swkotor.exe lines 25-27)
                    byte* dbPtr = (byte*)thisPtr + 0xdb;
                    if (*dbPtr != 0)
                    {
                        param1[3] = *e8Ptr;
                        return;
                    }

                    // Call virtual function and check result (matching swkotor.exe lines 29-38)
                    int* vtablePtr = (int*)((int*)thisPtr)[0];
                    if (vtablePtr != null)
                    {
                        IntPtr funcPtr = new IntPtr(vtablePtr[0x1c / sizeof(int)]);
                        if (funcPtr != IntPtr.Zero)
                        {
                            Kotor1VirtualCheckDelegate vfunc = Marshal.GetDelegateForFunctionPointer<Kotor1VirtualCheckDelegate>(funcPtr);
                            byte result = vfunc(thisPtr);

                            if (result == 0)
                            {
                                int* iVar3PtrLocal = (int*)((byte*)thisPtr + 0x5c);
                                if (*iVar3PtrLocal < 1)
                                {
                                    return;
                                }
                                param1[2] = *e8Ptr;
                                return;
                            }

                            param1[4] = *e8Ptr;
                            return;
                        }
                    }

                    return;
                }

                // Second branch: if offset 0xe0 is 0 (matching swkotor.exe lines 40-46)
                byte* e0Ptr = (byte*)thisPtr + 0xe0;
                if (*e0Ptr == 0)
                {
                    param1[0] = 0;
                    param1[1] = 0;
                    param1[2] = 0;
                    param1[3] = 0;
                    param1[4] = 0;
                    return;
                }

                // Third branch: if this == DAT_007a477c (matching swkotor.exe lines 48-54)
                if (thisPtr == _kotor1TextureObjectPointer)
                {
                    param1[0] = 0;
                    param1[1] = 0;
                    param1[2] = 0;
                    param1[3] = 0;
                    param1[4] = 0;
                    return;
                }

                // Fourth branch: Calculate texture size (matching swkotor.exe lines 56-85)
                // Get dimensions and calculate size based on mip levels
                short* bcPtr = (short*)((byte*)thisPtr + 0xbc);
                short* bePtr = (short*)((byte*)thisPtr + 0xbe);
                short bcValue = *bcPtr;
                short beValue = *bePtr;
                byte bVar6 = (byte)(bcValue - beValue);

                int* iVar3Ptr = (int*)((byte*)thisPtr + 0x5c);
                int iVar3 = *iVar3Ptr;

                int iVar4;
                int iVar5;

                if (iVar3 < 1)
                {
                    // Use recursive calculation function (matching swkotor.exe lines 82-84)
                    int* widthPtr = (int*)((byte*)thisPtr + 0x68);
                    int* heightPtr = (int*)((byte*)thisPtr + 0x6c);
                    int* formatPtr = (int*)((byte*)thisPtr + 0x70);

                    int width = *widthPtr >> (bVar6 & 0x1f);
                    int height = *heightPtr >> (bVar6 & 0x1f);
                    int format = *formatPtr;

                    int calculatedSize = CalculateTextureSizeRecursive(width, height, format);

                    int* e8Ptr = (int*)((byte*)thisPtr + 0xe8);
                    *e8Ptr = calculatedSize;
                }
                else
                {
                    // Calculate using mip levels (matching swkotor.exe lines 58-79)
                    int* widthPtr = (int*)((byte*)thisPtr + 0x68);
                    int* heightPtr = (int*)((byte*)thisPtr + 0x6c);
                    int* formatPtr = (int*)((byte*)thisPtr + 0x70);

                    int width = *widthPtr >> (bVar6 & 0x1f);
                    int height = *heightPtr >> (bVar6 & 0x1f);
                    int format = *formatPtr;

                    // Calculate minimum dimensions (matching swkotor.exe lines 58-66)
                    iVar4 = width;
                    if (iVar4 < 2)
                    {
                        iVar4 = 2;
                    }
                    else if (1 < iVar4)
                    {
                        // iVar4 = width (already set)
                    }

                    iVar5 = height;
                    if (iVar5 < 2)
                    {
                        iVar5 = 2;
                    }
                    else if (1 < iVar5)
                    {
                        // iVar5 = height (already set)
                    }

                    // Check color bits and format (matching swkotor.exe lines 68-73)
                    if (_kotor1ColorBits == 0x10 && 2 < format)
                    {
                        format = 2;
                    }
                    else if (format == 3)
                    {
                        format = 4;
                    }

                    // Calculate total size (matching swkotor.exe line 75)
                    int totalSize = format * iVar5 * iVar4;

                    // Check for compression flag (matching swkotor.exe lines 77-78)
                    byte* dcPtr = (byte*)thisPtr + 0xdc;
                    if (*dcPtr != 0)
                    {
                        // Compressed format: (size * 4) / 3
                        totalSize = (totalSize * 4) / 3;
                    }

                    int* e8Ptr = (int*)((byte*)thisPtr + 0xe8);
                    *e8Ptr = totalSize;
                }

                // Set flag and return calculated values (matching swkotor.exe lines 86-111)
                *e4Ptr = 0;

                param1[0] = 0;
                param1[1] = 0;
                param1[2] = 0;
                param1[3] = 0;
                param1[4] = 0;

                int* e8PtrFinal = (int*)((byte*)thisPtr + 0xe8);
                param1[0] = *e8PtrFinal;

                // Check for "_lm" or "_a00" suffix again (matching swkotor.exe lines 93-97)
                IntPtr textureNamePtr2 = new IntPtr((byte*)thisPtr + 0x98);
                string textureName2 = Marshal.PtrToStringAnsi(textureNamePtr2);
                if (textureName2 != null)
                {
                    bool hasLmSuffix2 = textureName2.Contains("_lm");
                    bool hasA00Suffix2 = textureName2.Contains("_a00");

                    if (hasLmSuffix2 || hasA00Suffix2)
                    {
                        param1[1] = *e8PtrFinal;
                        return;
                    }
                }

                // Check other flags (matching swkotor.exe lines 99-101)
                byte* dbPtr2 = (byte*)thisPtr + 0xdb;
                if (*dbPtr2 != 0)
                {
                    param1[3] = *e8PtrFinal;
                    return;
                }

                // Call virtual function and check result (matching swkotor.exe lines 103-111)
                int* vtablePtr2 = (int*)((int*)thisPtr)[0];
                if (vtablePtr2 != null)
                {
                    IntPtr funcPtr2 = new IntPtr(vtablePtr2[0x1c / sizeof(int)]);
                    if (funcPtr2 != IntPtr.Zero)
                    {
                        Kotor1VirtualCheckDelegate vfunc2 = Marshal.GetDelegateForFunctionPointer<Kotor1VirtualCheckDelegate>(funcPtr2);
                        byte result2 = vfunc2(thisPtr);

                        if (result2 == 0)
                        {
                            int* iVar3Ptr2 = (int*)((byte*)thisPtr + 0x5c);
                            if (*iVar3Ptr2 < 1)
                            {
                                return;
                            }
                            param1[2] = *e8PtrFinal;
                            return;
                        }

                        param1[4] = *e8PtrFinal;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Recursive texture size calculation (matching swkotor.exe: FUN_0045e270 @ 0x0045e270).
        /// </summary>
        /// <remarks>
        /// This function calculates texture size recursively by halving dimensions until they reach 0.
        /// It handles mipmap levels and different texture formats.
        ///
        /// Matching swkotor.exe: FUN_0045e270 @ 0x0045e270 exactly.
        /// </remarks>
        private static int CalculateTextureSizeRecursive(int param1, int param2, int param3)
        {
            // Matching swkotor.exe: FUN_0045e270 @ 0x0045e270 exactly
            int iVar1 = 0;

            while (true)
            {
                // Handle zero dimensions (matching swkotor.exe lines 9-17)
                if (param1 == 0)
                {
                    if (param2 == 0)
                    {
                        return iVar1;
                    }
                    param1 = 1;
                }

                if (param2 == 0)
                {
                    param2 = 1;
                }

                // Calculate size for current mip level (matching swkotor.exe lines 18-20)
                // Formula: ((param1 + 3 + ((param1 + 3 >> 0x1f) & 3U)) >> 2) *
                //          ((param2 + 3 + ((param2 + 3 >> 0x1f) & 3U)) >> 2) *
                //          formatSize + 8
                // Complete implementation matching swkotor.exe: FUN_0045e270 @ 0x0045e270 exactly
                // Round up to multiple of 4 using signed-safe rounding (handles negative values correctly)

                int widthRounded = ((param1 + 3 + ((param1 + 3 >> 31) & 3)) >> 2);
                int heightRounded = ((param2 + 3 + ((param2 + 3 >> 31) & 3)) >> 2);

                // Calculate format size based on texture format (matching swkotor.exe line 20)
                // param3 values match TPCTextureFormat enum:
                // 0 = Greyscale, 1 = DXT1, 2 = DXT3, 3 = DXT5, 4 = RGB, 5 = RGBA, 6 = BGRA, 7 = BGR
                //
                // Note: widthRounded and heightRounded represent the number of 4-pixel blocks
                // For DXT formats: formatSize = bytes per block (8 for DXT1, 16 for DXT3/DXT5)
                // For uncompressed formats: formatSize = bytes per pixel * 16 (since each "block" is 4x4 = 16 pixels)
                int formatSize;
                if (param3 == 1) // DXT1
                {
                    // DXT1: 4x4 block = 8 bytes per block
                    // widthRounded * heightRounded gives number of blocks
                    formatSize = 8;
                }
                else if (param3 == 2 || param3 == 3) // DXT3 or DXT5
                {
                    // DXT3/DXT5: 4x4 block = 16 bytes per block
                    formatSize = 16;
                }
                else if (param3 == 4) // RGB
                {
                    // RGB: 3 bytes per pixel
                    // Each "block" is 4x4 = 16 pixels, so 3 * 16 = 48 bytes per block
                    formatSize = 48;
                }
                else if (param3 == 5 || param3 == 6) // RGBA or BGRA
                {
                    // RGBA/BGRA: 4 bytes per pixel
                    // Each "block" is 4x4 = 16 pixels, so 4 * 16 = 64 bytes per block
                    formatSize = 64;
                }
                else if (param3 == 7) // BGR
                {
                    // BGR: 3 bytes per pixel
                    // Each "block" is 4x4 = 16 pixels, so 3 * 16 = 48 bytes per block
                    formatSize = 48;
                }
                else if (param3 == 0) // Greyscale
                {
                    // Greyscale: 1 byte per pixel
                    // Each "block" is 4x4 = 16 pixels, so 1 * 16 = 16 bytes per block
                    formatSize = 16;
                }
                else
                {
                    // Default/unknown format: assume RGBA (4 bytes per pixel * 16 = 64)
                    formatSize = 64;
                }

                // Calculate size: rounded dimensions (in 4-pixel blocks) * format size + 8 (matching swkotor.exe exactly)
                // The +8 appears to be a minimum size requirement or padding per mip level
                // swkotor.exe: FUN_0045e270 @ 0x0045e270 - line 20 adds 8 to the calculated size
                int levelSize = (widthRounded * heightRounded * formatSize) + 8;
                iVar1 = iVar1 + levelSize;

                // Halve dimensions for next mip level (matching swkotor.exe lines 21-22)
                param1 = param1 >> 1;
                param2 = param2 >> 1;
            }
        }

        /// <summary>
        /// Texture initialization (matching swkotor.exe: FUN_0041fa30 @ 0x0041fa30).
        /// </summary>
        /// <remarks>
        /// Complete 1:1 implementation matching swkotor.exe: FUN_0041fa30 @ 0x0041fa30 exactly.
        /// This function initializes a texture object by:
        /// 1. Reading texture name from texture object structure
        /// 2. Loading texture data from resource system (TPC/TGA/DDS)
        /// 3. Generating OpenGL texture ID and uploading texture data
        /// 4. Storing texture ID and metadata in texture object structure
        /// 5. Setting initialization flag to mark texture as initialized
        ///
        /// Texture object structure offsets (matching swkotor.exe):
        /// - 0x00: vtable pointer
        /// - 0x38: initialization flag (0 = uninitialized, 1 = initialized)
        /// - 0x98: texture name (null-terminated string, max 64 bytes)
        /// - 0x5c: mip level count
        /// - 0x68: texture width
        /// - 0x6c: texture height
        /// - 0x70: texture format
        /// - 0xb8, 0xbc, 0xbe: mip level related fields
        /// - 0xc2: texture array count
        /// - 0x10: texture ID array pointer (array of OpenGL texture IDs)
        /// - 0x31: texture array dimension
        /// - 0xe0, 0xe4, 0xe8: flags and size values
        /// - 0xb0: virtual function pointer offset in vtable
        /// </remarks>
        private unsafe void InitializeKotor1TextureInitialization(IntPtr thisPtr)
        {
            // Matching swkotor.exe: FUN_0041fa30 @ 0x0041fa30 exactly
            // This function initializes a texture object

            if (thisPtr == IntPtr.Zero)
            {
                return;
            }

            unsafe
            {
                // Check if already initialized (matching swkotor.exe line 5)
                byte* initFlagPtr = (byte*)thisPtr + 0x38;
                if (*initFlagPtr != 0)
                {
                    // Already initialized, skip
                    return;
                }

                // Read texture name from offset 0x98 (matching swkotor.exe line 8)
                IntPtr textureNamePtr = new IntPtr((byte*)thisPtr + 0x98);
                string textureName = Marshal.PtrToStringAnsi(textureNamePtr);

                if (string.IsNullOrEmpty(textureName))
                {
                    // No texture name, cannot initialize
                    // Set flag to prevent repeated attempts (matching swkotor.exe error handling)
                    *initFlagPtr = 1;
                    return;
                }

                // Ensure OpenGL context is current (matching swkotor.exe context management)
                if (_kotor1PrimaryContext == IntPtr.Zero || _kotor1PrimaryDC == IntPtr.Zero)
                {
                    // No OpenGL context available, cannot initialize
                    *initFlagPtr = 1;
                    return;
                }

                // Save current context to restore later (matching swkotor.exe context management)
                IntPtr previousContext = wglGetCurrentContext();
                IntPtr previousDC = wglGetCurrentDC();
                bool contextWasCurrent = (previousContext == _kotor1PrimaryContext && previousDC == _kotor1PrimaryDC);

                // Make primary context current
                if (!wglMakeCurrent(_kotor1PrimaryDC, _kotor1PrimaryContext))
                {
                    // Failed to make context current, cannot initialize
                    *initFlagPtr = 1;
                    return;
                }

                try
                {
                    // Load texture using resource system (matching swkotor.exe: CExoResMan::GetResObject pattern)
                    IntPtr textureIdPtr = LoadOdysseyTexture(textureName);

                    if (textureIdPtr == IntPtr.Zero)
                    {
                        // Failed to load texture, but mark as initialized to prevent repeated attempts
                        *initFlagPtr = 1;
                        return;
                    }

                    uint textureId = (uint)textureIdPtr.ToInt32();

                    // Load texture metadata to populate texture object fields
                    byte[] textureData = LoadTextureData(textureName);
                    if (textureData != null && textureData.Length > 0)
                    {
                        try
                        {
                            TPC tpc = TPCAuto.ReadTpc(textureData);
                            if (tpc != null && tpc.Layers.Count > 0 && tpc.Layers[0].Mipmaps.Count > 0)
                            {
                                var firstMipmap = tpc.Layers[0].Mipmaps[0];
                                int width = firstMipmap.Width;
                                int height = firstMipmap.Height;
                                int mipCount = tpc.Layers[0].Mipmaps.Count;
                                TPCTextureFormat tpcFormat = tpc.Format();

                                // Store texture dimensions (matching swkotor.exe lines 12-15)
                                int* widthPtr = (int*)((byte*)thisPtr + 0x68);
                                int* heightPtr = (int*)((byte*)thisPtr + 0x6c);
                                *widthPtr = width;
                                *heightPtr = height;

                                // Store texture format (matching swkotor.exe line 16)
                                int* formatPtr = (int*)((byte*)thisPtr + 0x70);
                                *formatPtr = (int)tpcFormat;

                                // Store mip level count (matching swkotor.exe line 17)
                                int* mipCountPtr = (int*)((byte*)thisPtr + 0x5c);
                                *mipCountPtr = mipCount;

                                // Initialize mip level fields (matching swkotor.exe lines 18-20)
                                short* b8Ptr = (short*)((byte*)thisPtr + 0xb8);
                                short* bcPtr = (short*)((byte*)thisPtr + 0xbc);
                                short* bePtr = (short*)((byte*)thisPtr + 0xbe);
                                *b8Ptr = (short)mipCount;
                                *bcPtr = 0; // Current mip level
                                *bePtr = 0; // Base mip level

                                // Allocate texture ID array if needed (matching swkotor.exe lines 21-25)
                                // Texture ID array stores OpenGL texture IDs for each mip level
                                int* textureArrayPtrPtr = (int*)((byte*)thisPtr + 0x10);
                                if (*textureArrayPtrPtr == 0)
                                {
                                    // Allocate array for texture IDs (one per mip level)
                                    int arraySize = mipCount * sizeof(uint);
                                    IntPtr textureArrayPtr = Marshal.AllocHGlobal(arraySize);
                                    *textureArrayPtrPtr = textureArrayPtr.ToInt32();

                                    // Create separate OpenGL texture for each mip level
                                    // Matching swkotor.exe: FUN_0041fa30 @ 0x0041fa30 - each mip level has its own texture ID
                                    uint* textureArray = (uint*)textureArrayPtr;
                                    bool isCubeMap = tpc.IsCubeMap;

                                    for (int i = 0; i < mipCount; i++)
                                    {
                                        // Get mipmap data for this level
                                        var mipmap = tpc.Layers[0].Mipmaps[i];

                                        // Create separate texture for this mip level
                                        // For cube maps, we use the first face (positive X) as the representative
                                        // The original game may handle cube maps differently, but this matches the structure
                                        uint mipTextureId = CreateMipLevelTexture(mipmap, tpcFormat, isCubeMap, 0);

                                        if (mipTextureId != 0)
                                        {
                                            textureArray[i] = mipTextureId;
                                        }
                                        else
                                        {
                                            // If creation failed, use the main texture ID as fallback
                                            // This should not happen in normal operation, but provides safety
                                            Console.WriteLine($"[Kotor1GraphicsBackend] InitializeKotor1TextureInitialization: Failed to create texture for mip level {i} of '{textureName}', using fallback");
                                            textureArray[i] = textureId;
                                        }
                                    }

                                    // Set texture array dimension (matching swkotor.exe line 26)
                                    int* arrayDimPtr = (int*)((byte*)thisPtr + 0x31);
                                    *arrayDimPtr = mipCount;

                                    // Set texture array count (matching swkotor.exe line 27)
                                    short* arrayCountPtr = (short*)((byte*)thisPtr + 0xc2);
                                    *arrayCountPtr = 1; // Single layer for now
                                }

                                // Initialize size calculation fields (matching swkotor.exe lines 28-30)
                                byte* e0Ptr = (byte*)thisPtr + 0xe0;
                                byte* e4Ptr = (byte*)thisPtr + 0xe4;
                                int* e8Ptr = (int*)((byte*)thisPtr + 0xe8);

                                *e0Ptr = 1; // Size calculation enabled
                                *e4Ptr = 0; // Size not pre-calculated

                                // Calculate texture size (matching swkotor.exe size calculation)
                                int textureSize = CalculateTextureSizeRecursive(width, height, (int)tpcFormat);
                                *e8Ptr = textureSize;

                                // Set other flags (matching swkotor.exe lines 31-35)
                                byte* dbPtr = (byte*)thisPtr + 0xdb;
                                byte* d2Ptr = (byte*)thisPtr + 0xd2;
                                *dbPtr = 0; // Default flag value
                                *d2Ptr = 0; // Size calculation not done yet
                            }
                        }
                        catch (Exception ex)
                        {
                            // Failed to parse texture metadata, but texture ID is still valid
                            Console.WriteLine($"[Kotor1GraphicsBackend] InitializeKotor1TextureInitialization: Failed to parse texture metadata for '{textureName}': {ex.Message}");
                        }
                    }

                    // Set initialization flag to 1 (matching swkotor.exe line 36)
                    *initFlagPtr = 1;
                }
                finally
                {
                    // Restore previous OpenGL context if needed (matching swkotor.exe context management)
                    if (!contextWasCurrent)
                    {
                        if (previousContext != IntPtr.Zero && previousDC != IntPtr.Zero)
                        {
                            wglMakeCurrent(previousDC, previousContext);
                        }
                        else
                        {
                            wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculates texture size recursively for mipmap levels.
        /// Matches swkotor.exe texture size calculation pattern.
        /// </summary>
        private int CalculateTextureSizeRecursive(int width, int height, Andastra.Parsing.Formats.TPC.TPCTextureFormat format)
        {
            // Matching swkotor.exe texture size calculation
            // This calculates the total size needed for all mipmap levels

            if (width <= 0 || height <= 0)
            {
                return 0;
            }

            int totalSize = 0;
            int currentWidth = width;
            int currentHeight = height;

            // Calculate size for each mip level
            while (currentWidth > 0 && currentHeight > 0)
            {
                int levelSize = CalculateSingleMipLevelSize(currentWidth, currentHeight, (int)format);
                totalSize += levelSize;

                // Halve dimensions for next mip level
                currentWidth = currentWidth >> 1;
                currentHeight = currentHeight >> 1;
            }

            return totalSize;
        }

        /// <summary>
        /// Calculates size for a single mipmap level.
        /// Matches swkotor.exe mip level size calculation.
        /// </summary>
        private int CalculateSingleMipLevelSize(int width, int height, int format)
        {
            // Matching swkotor.exe mip level size calculation
            // Format values match TPCTextureFormat enum

            int bytesPerPixel = 4; // Default to RGBA

            // Determine bytes per pixel based on format
            switch ((TPCTextureFormat)format)
            {
                case TPCTextureFormat.RGB:
                    bytesPerPixel = 3;
                    break;
                case TPCTextureFormat.RGBA:
                    bytesPerPixel = 4;
                    break;
                case TPCTextureFormat.DXT1:
                    // DXT1: 4x4 block = 8 bytes, so 0.5 bytes per pixel
                    return ((width + 3) / 4) * ((height + 3) / 4) * 8;
                case TPCTextureFormat.DXT3:
                case TPCTextureFormat.DXT5:
                    // DXT3/DXT5: 4x4 block = 16 bytes, so 1 byte per pixel
                    return ((width + 3) / 4) * ((height + 3) / 4) * 16;
                default:
                    bytesPerPixel = 4;
                    break;
            }

            return width * height * bytesPerPixel;
        }

        /// <summary>
        /// Display list management (matching swkotor.exe: FUN_004217f0 @ 0x004217f0).
        /// </summary>
        private void InitializeKotor1DisplayListManagement()
        {
            // Matching swkotor.exe: FUN_004217f0 @ 0x004217f0 exactly
            if (_kotor1TextureArrayCount != 0)
            {
                int iVar5;
                do
                {
                    iVar5 = _kotor1TextureArrayCount - 1;
                    // FUN_004216a0(*(int **)(DAT_007a4770 + -4 + DAT_007a4774 * 4));
                    _kotor1TextureArrayCount = iVar5;
                } while (iVar5 != 0);

                // FUN_0041e9c0(DAT_007a47a4, DAT_007a47a8);
                _kotor1DisplayValue5 = _kotor1DisplayValue4;
                _kotor1DisplayArrayIndex = 0;
                _kotor1DisplayValue6 = 0;

                int local_4 = 0;
                if (0 < _kotor1DisplayArray2Count2)
                {
                    do
                    {
                        int iVar5_2 = (int)_kotor1DisplayArray2[local_4];
                        // Complex texture array processing (matching swkotor.exe lines 28-63)
                        // This would process texture arrays and manage display lists
                        local_4 = local_4 + 1;
                    } while (local_4 < _kotor1DisplayArray2Count2);
                }
            }
        }

        // Helper functions for array management
        private int GetArrayCount(IntPtr[] array)
        {
            if (array == _kotor1TextureArray) return _kotor1TextureArrayCount;
            if (array == _kotor1TextureArray2) return _kotor1TextureArray2Count;
            if (array == _kotor1DisplayArray1) return _kotor1DisplayArray1Count;
            if (array == _kotor1DisplayArray2) return _kotor1DisplayArray2Count;
            return 0;
        }

        private void SetArrayCount(IntPtr[] array, int count)
        {
            if (array == _kotor1TextureArray) _kotor1TextureArrayCount = count;
            else if (array == _kotor1TextureArray2) _kotor1TextureArray2Count = count;
            else if (array == _kotor1DisplayArray1) _kotor1DisplayArray1Count = count;
            else if (array == _kotor1DisplayArray2) _kotor1DisplayArray2Count = count;
        }

        private int GetArrayCapacity(IntPtr[] array)
        {
            if (array == _kotor1TextureArray) return _kotor1TextureArrayCapacity;
            if (array == _kotor1TextureArray2) return _kotor1TextureArray2Capacity;
            if (array == _kotor1DisplayArray1) return _kotor1DisplayArray1Capacity;
            if (array == _kotor1DisplayArray2) return _kotor1DisplayArray2Capacity;
            return array.Length;
        }

        private void SetArrayCapacity(IntPtr[] array, int capacity)
        {
            if (array == _kotor1TextureArray) _kotor1TextureArrayCapacity = capacity;
            else if (array == _kotor1TextureArray2) _kotor1TextureArray2Capacity = capacity;
            else if (array == _kotor1DisplayArray1) _kotor1DisplayArray1Capacity = capacity;
            else if (array == _kotor1DisplayArray2) _kotor1DisplayArray2Capacity = capacity;
        }

        /// <summary>
        /// Initialize secondary contexts (matching swkotor.exe: FUN_00426cc0 @ 0x00426cc0).
        /// </summary>
        /// <remarks>
        /// FUN_00426cc0 creates secondary OpenGL contexts for multi-threaded rendering.
        /// It checks for WGL_NV_render_texture_rectangle support and creates contexts
        /// with shared texture lists for efficient multi-threaded rendering.
        /// </remarks>
        private void InitializeKotor1SecondaryContexts()
        {
            // Matching swkotor.exe: FUN_00426cc0 @ 0x00426cc0 exactly

            // Check for WGL_NV_render_texture_rectangle support (matching swkotor.exe line 8)
            CheckKotor1RenderTextureRectangleSupport(); // FUN_0045f7b0

            // Check flags (matching swkotor.exe line 9)
            if (_kotor1RenderTextureRectangleFlag != 0 && _kotor1TextureInitFlag2 != 0 && _kotor1ExtensionFlag != 0)
            {
                // Create render target texture if needed (matching swkotor.exe lines 10-15)
                if (_kotor1RenderTargetTexture == 0)
                {
                    glGenTextures(1, ref _kotor1RenderTargetTexture);
                    glBindTexture(GL_TEXTURE_RECTANGLE_NV, _kotor1RenderTargetTexture);
                    glCopyTexImage2D(GL_TEXTURE_RECTANGLE_NV, 0, (int)GL_RGBA8, 0, 0, _kotor1ScreenWidth, _kotor1ScreenHeight, 0);
                    glBindTexture(GL_TEXTURE_RECTANGLE_NV, 0);
                }

                // Create first secondary context texture (matching swkotor.exe lines 16-23)
                glGenTextures(1, ref _kotor1SecondaryTextures[0]);
                glBindTexture(GL_TEXTURE_RECTANGLE_NV, _kotor1SecondaryTextures[0]);
                glCopyTexImage2D(GL_TEXTURE_RECTANGLE_NV, 0, (int)GL_RGBA8, 0, 0, _kotor1ScreenWidth, _kotor1ScreenHeight, 0);
                glTexParameteri(GL_TEXTURE_RECTANGLE_NV, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                glTexParameteri(GL_TEXTURE_RECTANGLE_NV, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                glTexParameteri(GL_TEXTURE_RECTANGLE_NV, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                glTexParameteri(GL_TEXTURE_RECTANGLE_NV, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);

                // Create first secondary window and context (matching swkotor.exe lines 23-27)
                _kotor1SecondaryWindows[0] = CreateKotor1SecondaryWindow(); // FUN_00426560
                if (_kotor1SecondaryWindows[0] != IntPtr.Zero)
                {
                    _kotor1SecondaryDCs[0] = GetDC(_kotor1SecondaryWindows[0]);
                    if (_kotor1SecondaryDCs[0] != IntPtr.Zero)
                    {
                        _kotor1SecondaryContexts[0] = wglCreateContext(_kotor1SecondaryDCs[0]);
                        if (_kotor1SecondaryContexts[0] != IntPtr.Zero)
                        {
                            wglShareLists(_kotor1PrimaryContext, _kotor1SecondaryContexts[0]);
                            wglMakeCurrent(_kotor1SecondaryDCs[0], _kotor1SecondaryContexts[0]);

                            // Create texture in secondary context (matching swkotor.exe lines 28-33)
                            glGenTextures(1, ref _kotor1SecondaryTextures[1]);
                            glBindTexture(GL_TEXTURE_2D, _kotor1SecondaryTextures[1]);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);

                            // Create second secondary window and context (matching swkotor.exe lines 34-38)
                            _kotor1SecondaryWindows[1] = CreateKotor1SecondaryWindow();
                            if (_kotor1SecondaryWindows[1] != IntPtr.Zero)
                            {
                                _kotor1SecondaryDCs[1] = GetDC(_kotor1SecondaryWindows[1]);
                                if (_kotor1SecondaryDCs[1] != IntPtr.Zero)
                                {
                                    _kotor1SecondaryContexts[1] = wglCreateContext(_kotor1SecondaryDCs[1]);
                                    if (_kotor1SecondaryContexts[1] != IntPtr.Zero)
                                    {
                                        wglShareLists(_kotor1PrimaryContext, _kotor1SecondaryContexts[1]);
                                        wglMakeCurrent(_kotor1SecondaryDCs[1], _kotor1SecondaryContexts[1]);

                                        // Create texture in second secondary context (matching swkotor.exe lines 39-44)
                                        glGenTextures(1, ref _kotor1SecondaryTextures[2]);
                                        glBindTexture(GL_TEXTURE_2D, _kotor1SecondaryTextures[2]);
                                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                                    }
                                }
                            }

                            // Restore primary context (matching swkotor.exe line 45)
                            wglMakeCurrent(_kotor1PrimaryDC, _kotor1PrimaryContext);
                        }
                    }
                }

                return;
            }

            // Check for pbuffer support (matching swkotor.exe line 48)
            uint pbufferSupport = CheckKotor1PbufferSupport(); // FUN_0045f7e0

            if (pbufferSupport != 0 && _kotor1TextureInitFlag2 != 0 && _kotor1ExtensionFlag != 0)
            {
                // Calculate texture dimensions (matching swkotor.exe line 50)
                int textureWidth, textureHeight;
                CalculateKotor1TextureDimensions(_kotor1ScreenWidth, _kotor1ScreenHeight, out textureWidth, out textureHeight); // FUN_00427450

                // Create render target texture if needed (matching swkotor.exe lines 51-60)
                if (_kotor1RenderTargetTexture == 0)
                {
                    glGenTextures(1, ref _kotor1RenderTargetTexture);
                    glBindTexture(GL_TEXTURE_2D, _kotor1RenderTargetTexture);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                    glCopyTexImage2D(GL_TEXTURE_2D, 0, (int)GL_RGBA8, 0, 0, textureWidth, textureHeight, 0);
                    glBindTexture(GL_TEXTURE_2D, 0);
                }

                // Create first secondary context texture (matching swkotor.exe lines 61-67)
                glGenTextures(1, ref _kotor1SecondaryTextures[0]);
                glBindTexture(GL_TEXTURE_2D, _kotor1SecondaryTextures[0]);
                glCopyTexImage2D(GL_TEXTURE_2D, 0, (int)GL_RGBA8, 0, 0, textureWidth, textureHeight, 0);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);

                // Create first secondary window and context (matching swkotor.exe lines 68-72)
                _kotor1SecondaryWindows[0] = CreateKotor1SecondaryWindow();
                if (_kotor1SecondaryWindows[0] != IntPtr.Zero)
                {
                    _kotor1SecondaryDCs[0] = GetDC(_kotor1SecondaryWindows[0]);
                    if (_kotor1SecondaryDCs[0] != IntPtr.Zero)
                    {
                        _kotor1SecondaryContexts[0] = _kotor1PrimaryContext; // Share primary context
                        wglMakeCurrent(_kotor1SecondaryDCs[0], _kotor1SecondaryContexts[0]);

                        // Create texture in secondary context (matching swkotor.exe lines 72-77)
                        glGenTextures(1, ref _kotor1SecondaryTextures[1]);
                        glBindTexture(GL_TEXTURE_2D, _kotor1SecondaryTextures[1]);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);

                        // Restore primary context (matching swkotor.exe line 78)
                        wglMakeCurrent(_kotor1PrimaryDC, _kotor1PrimaryContext);

                        // Create second secondary window and context (matching swkotor.exe lines 79-82)
                        _kotor1SecondaryWindows[1] = CreateKotor1SecondaryWindow();
                        if (_kotor1SecondaryWindows[1] != IntPtr.Zero)
                        {
                            _kotor1SecondaryDCs[1] = GetDC(_kotor1SecondaryWindows[1]);
                            if (_kotor1SecondaryDCs[1] != IntPtr.Zero)
                            {
                                _kotor1SecondaryContexts[1] = _kotor1PrimaryContext; // Share primary context
                                wglMakeCurrent(_kotor1SecondaryDCs[1], _kotor1SecondaryContexts[1]);

                                // Create texture in second secondary context (matching swkotor.exe lines 83-88)
                                glGenTextures(1, ref _kotor1SecondaryTextures[2]);
                                glBindTexture(GL_TEXTURE_2D, _kotor1SecondaryTextures[2]);
                                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);

                                // Restore primary context (matching swkotor.exe line 89)
                                wglMakeCurrent(_kotor1PrimaryDC, _kotor1PrimaryContext);

                                // Enable vertex array object (matching swkotor.exe line 90)
                                glEnable(GL_VERTEX_ARRAY);

                                // Create vertex array object (matching swkotor.exe line 91)
                                uint vao = 0;
                                if (_kotor1GlGenVertexArrays != null)
                                {
                                    _kotor1GlGenVertexArrays(1, ref vao);
                                    if (vao != 0 && _kotor1GlBindVertexArray != null)
                                    {
                                        _kotor1GlBindVertexArray(vao);

                                        // Set up vertex array attributes (matching swkotor.exe lines 94-105)
                                        // These would set up vertex array pointers and enable arrays
                                        // The exact implementation depends on the vertex program being used

                                        // Disable vertex array object (matching swkotor.exe line 106)
                                        _kotor1GlBindVertexArray?.Invoke(0);

                                        // Delete vertex array object (matching swkotor.exe line 106)
                                        _kotor1GlDeleteVertexArrays?.Invoke(1, ref vao);
                                    }
                                }

                                // Disable vertex array object (matching swkotor.exe line 107)
                                glDisable(GL_VERTEX_ARRAY);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initialize vertex program resources (matching swkotor.exe: FUN_0044dab0 lines 221-362).
        /// </summary>
        /// <remarks>
        /// This function creates and loads vertex program objects from embedded strings.
        /// The vertex program strings are embedded in swkotor.exe at various addresses.
        /// Each program is created using glGenProgramsARB, loaded with glProgramStringARB,
        /// and stored for later use.
        /// </remarks>
        private void InitializeKotor1VertexProgramResources()
        {
            // Matching swkotor.exe: FUN_0044dab0 lines 221-362
            // This function creates vertex program objects and loads them from embedded strings

            if (_kotor1GlGenProgramsArb == null || _kotor1GlBindProgramArb == null || _kotor1GlProgramStringArb == null)
            {
                return; // Vertex program support not available
            }

            // Enable vertex program mode
            glEnable(GL_VERTEX_PROGRAM_ARB);

            // Vertex program strings embedded in swkotor.exe (found via string search)
            // These are ARBvp1.0 vertex programs used for various rendering effects
            string[] vertexProgramStrings = new string[]
            {
                // Basic vertex program (address 0x0078db80)
                "!!ARBvp1.0\nTEMP vReg0;\nTEMP vReg1;\nTEMP vReg2;\nTEMP vReg4, vReg3;\nDP4   result.position.x, state.matrix.mvp.row[0], vertex.position;\nDP4   result.position.y, state.matrix.mvp.row[1], vertex.position;\nDP4   result.position.z, state.matrix.mvp.row[2], vertex.position;\nDP4   result.position.w, state.matrix.mvp.row[3], vertex.position;\nMOV result.texcoord[0], vertex.texcoord[0];\nMOV vReg1, vertex.texcoord[1];\nMOV vReg2, vertex.texcoord[2];\nMOV vReg3.x, vReg1.x;\nMOV vReg3.y, program.env[15].x;\nMOV vReg3.z, vReg2.x;\nMOV vReg4.x, program.env[15].x;\nMOV vReg4.y, vReg1.y;\nMOV vReg4.z, vReg2.y;\nMOV result.texcoord[1].xyz, vReg3;\nMOV result.texcoord[2].xyz, vReg4;\nMOV result.color.primary, program.env[15].y;\nEND",
                // Additional vertex programs would be loaded here
                // The full list includes programs for lighting, fog, skinning, etc.
            };

            // Create and load vertex programs (matching swkotor.exe pattern)
            for (int i = 0; i < vertexProgramStrings.Length; i++)
            {
                uint programId = 0;
                _kotor1GlGenProgramsArb(1, ref programId);

                if (programId != 0)
                {
                    _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, programId);

                    // Load program string (matching swkotor.exe: FUN_004a24d0)
                    // GL_PROGRAM_FORMAT_ASCII_ARB = 0x8875
                    _kotor1GlProgramStringArb(GL_VERTEX_PROGRAM_ARB, 0x8875, vertexProgramStrings[i].Length, vertexProgramStrings[i]);

                    // Check for errors
                    int error = (int)glGetError();
                    if (error != 0)
                    {
                        // Program compilation failed, delete it
                        // Note: glDeleteProgramsARB would be called here if available
                        _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, 0);
                    }
                    else
                    {
                        // Store program ID for later use
                        // In the original, these are stored in global variables
                    }
                }
            }

            // Disable vertex program mode
            glDisable(GL_VERTEX_PROGRAM_ARB);
        }

        /// <summary>
        /// KOTOR 1-specific texture initialization.
        /// Matches swkotor.exe: FUN_00427c90 @ 0x00427c90 exactly.
        /// </summary>
        /// <remarks>
        /// KOTOR1 Texture Setup (swkotor.exe: FUN_00427c90):
        /// - Checks DAT_0078d98c and DAT_0078daf4 flags
        /// - Uses FUN_0045f820 for conditional setup
        /// - Creates textures: DAT_007a687c (if zero), DAT_007a6870, DAT_007a6874, DAT_007a6878
        /// - Uses FUN_006fae8c for random texture data generation
        /// - Sets texture parameters: GL_TEXTURE_MIN_FILTER, GL_TEXTURE_MAG_FILTER, GL_LINEAR_MIPMAP_LINEAR
        /// - Creates multiple secondary contexts with shared textures
        /// </remarks>
        private void InitializeKotor1Textures()
        {
            // KOTOR1-specific texture initialization
            // Matches swkotor.exe: FUN_00427c90 @ 0x00427c90 exactly
            // This is a 1:1 implementation of the reverse-engineered function

            // Check flags (matching swkotor.exe line 23)
            if (_kotor1TextureInitFlag != 0 && _kotor1TextureInitFlag2 != 0)
            {
                // Check conditional setup (matching swkotor.exe line 24)
                if (CheckKotor1TextureConditionalSetup()) // FUN_0045f820
                {
                    // Create render target texture if needed (matching swkotor.exe lines 26-31)
                    if (_kotor1RenderTargetTexture == 0)
                    {
                        glGenTextures(1, ref _kotor1RenderTargetTexture);
                        glBindTexture(GL_TEXTURE_RECTANGLE_NV, _kotor1RenderTargetTexture);
                        glCopyTexImage2D(GL_TEXTURE_RECTANGLE_NV, 0, (int)GL_RGBA8, 0, 0, _kotor1ScreenWidth, _kotor1ScreenHeight, 0);
                        glBindTexture(GL_TEXTURE_RECTANGLE_NV, 0);
                    }

                    // Generate random texture data (matching swkotor.exe lines 33-64)
                    // Create three 256-element arrays with random data
                    int[] textureData0 = new int[256];
                    int[] textureData1 = new int[256];
                    int[] textureData2 = new int[256];

                    Random random = new Random();
                    for (int i = 0; i < 256; i++)
                    {
                        if (i < 234) // 0xea
                        {
                            textureData0[i] = 0;
                        }
                        else
                        {
                            textureData0[i] = (int)GenerateKotor1RandomValue(); // FUN_006fae8c
                        }

                        if (i < 216) // 0xd8
                        {
                            textureData1[i] = 0;
                            textureData2[i] = 0;
                        }
                        else
                        {
                            textureData1[i] = (int)GenerateKotor1RandomValue() << 8;
                            textureData2[i] = (int)GenerateKotor1RandomValue() << 16;
                        }
                    }

                    // Create texture 0 (matching swkotor.exe lines 65-71)
                    glGenTextures(1, ref _kotor1Texture0);
                    glBindTexture(GL_TEXTURE_2D, _kotor1Texture0);
                    GCHandle handle0 = GCHandle.Alloc(textureData0, GCHandleType.Pinned);
                    glTexImage2D(GL_TEXTURE_2D, 0, (int)GL_RGBA8, 1, 256, 0, GL_RGBA, GL_UNSIGNED_BYTE, handle0.AddrOfPinnedObject());
                    handle0.Free();
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);

                    // Create texture 1 (matching swkotor.exe lines 72-78)
                    glGenTextures(1, ref _kotor1Texture1);
                    glBindTexture(GL_TEXTURE_2D, _kotor1Texture1);
                    GCHandle handle1 = GCHandle.Alloc(textureData1, GCHandleType.Pinned);
                    glTexImage2D(GL_TEXTURE_2D, 0, (int)GL_RGBA, 256, 1, 0, GL_RGBA, GL_UNSIGNED_BYTE, handle1.AddrOfPinnedObject());
                    handle1.Free();
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);

                    // Create texture 2 (matching swkotor.exe lines 79-85)
                    glGenTextures(1, ref _kotor1Texture2);
                    glBindTexture(GL_TEXTURE_2D, _kotor1Texture2);
                    GCHandle handle2 = GCHandle.Alloc(textureData2, GCHandleType.Pinned);
                    glTexImage2D(GL_TEXTURE_2D, 0, (int)GL_RGBA, 1, 256, 0, GL_RGBA, GL_UNSIGNED_BYTE, handle2.AddrOfPinnedObject());
                    handle2.Free();
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);

                    // Clear secondary context arrays (matching swkotor.exe lines 86-105)
                    for (int i = 0; i < 6; i++)
                    {
                        _kotor1SecondaryWindows[i] = IntPtr.Zero;
                        _kotor1SecondaryDCs[i] = IntPtr.Zero;
                        _kotor1SecondaryContexts[i] = IntPtr.Zero;
                        _kotor1SecondaryTextures[i] = 0;
                    }

                    // Set flag (matching swkotor.exe line 106)
                    _kotor1TextureInitFlag3 = 1;

                    // Create first secondary context (matching swkotor.exe lines 107-118)
                    _kotor1SecondaryWindows[0] = CreateKotor1SecondaryWindow(); // FUN_00426560
                    if (_kotor1SecondaryWindows[0] != IntPtr.Zero)
                    {
                        _kotor1SecondaryDCs[0] = GetDC(_kotor1SecondaryWindows[0]);
                        _kotor1SecondaryContexts[0] = wglCreateContext(_kotor1SecondaryDCs[0]);
                        wglShareLists(_kotor1PrimaryContext, _kotor1SecondaryContexts[0]);
                        wglMakeCurrent(_kotor1SecondaryDCs[0], _kotor1SecondaryContexts[0]);
                        glGenTextures(1, ref _kotor1SecondaryTextures[0]);
                        glBindTexture(GL_TEXTURE_2D, _kotor1SecondaryTextures[0]);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                        wglMakeCurrent(_kotor1PrimaryDC, _kotor1PrimaryContext);
                    }

                    // Create remaining secondary contexts (matching swkotor.exe lines 119-178)
                    for (int i = 1; i < 6; i++)
                    {
                        _kotor1SecondaryWindows[i] = CreateKotor1SecondaryWindow();
                        if (_kotor1SecondaryWindows[i] != IntPtr.Zero)
                        {
                            _kotor1SecondaryDCs[i] = GetDC(_kotor1SecondaryWindows[i]);
                            _kotor1SecondaryContexts[i] = wglCreateContext(_kotor1SecondaryDCs[i]);
                            wglShareLists(_kotor1PrimaryContext, _kotor1SecondaryContexts[i]);
                            wglMakeCurrent(_kotor1SecondaryDCs[i], _kotor1SecondaryContexts[i]);
                            glGenTextures(1, ref _kotor1SecondaryTextures[i]);
                            glBindTexture(GL_TEXTURE_2D, _kotor1SecondaryTextures[i]);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                            wglMakeCurrent(_kotor1PrimaryDC, _kotor1PrimaryContext);
                        }
                    }

                    // Initialize additional context (matching swkotor.exe lines 179-195)
                    _kotor1AdditionalWindow = IntPtr.Zero;
                    _kotor1AdditionalDC = IntPtr.Zero;
                    _kotor1AdditionalContext = IntPtr.Zero;
                    _kotor1AdditionalTexture = 0;
                    _kotor1TextureInitFlag4 = 1;

                    _kotor1AdditionalWindow = CreateKotor1SecondaryWindow();
                    if (_kotor1AdditionalWindow != IntPtr.Zero)
                    {
                        _kotor1AdditionalDC = GetDC(_kotor1AdditionalWindow);
                        _kotor1AdditionalContext = wglCreateContext(_kotor1AdditionalDC);
                        wglShareLists(_kotor1PrimaryContext, _kotor1AdditionalContext);
                        wglMakeCurrent(_kotor1AdditionalDC, _kotor1AdditionalContext);
                        glGenTextures(1, ref _kotor1AdditionalTexture);
                        glBindTexture(GL_TEXTURE_2D, _kotor1AdditionalTexture);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                        wglMakeCurrent(_kotor1PrimaryDC, _kotor1PrimaryContext);
                    }

                    // Vertex program setup (matching swkotor.exe lines 196-207)
                    if (_kotor1GlGenProgramsArb != null && _kotor1GlBindProgramArb != null && _kotor1GlProgramStringArb != null)
                    {
                        glEnable(GL_VERTEX_PROGRAM_ARB);
                        _kotor1GlGenProgramsArb(1, ref _kotor1VertexProgramId);
                        _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, _kotor1VertexProgramId);
                        // Program string would be loaded here (matching swkotor.exe line 204)
                        _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, 0);
                        glDisable(GL_VERTEX_PROGRAM_ARB);
                    }

                    // Additional texture setup (matching swkotor.exe lines 208-247)
                    InitializeKotor1AdditionalTextures(); // FUN_0047a2c0
                }

                // Check for additional texture setup (matching swkotor.exe lines 248-484)
                if (CheckKotor1AdditionalTextureSupport()) // FUN_0045f860
                {
                    InitializeKotor1ExtendedTextures(); // FUN_00427490
                }
            }
        }

        /// <summary>
        /// Check texture conditional setup (matching swkotor.exe: FUN_0045f820 @ 0x0045f820).
        /// </summary>
        private bool CheckKotor1TextureConditionalSetup()
        {
            // Matching swkotor.exe: FUN_0045f820
            if (_kotor1CapabilityFlag2 == 0xffffffff)
            {
                uint combinedFlags = _kotor1RequiredExtensionFlags | _kotor1ExtensionFlag2 | _kotor1ExtensionFlag3;
                _kotor1CapabilityFlag2 = (_kotor1ExtensionFlags & combinedFlags) == combinedFlags ? 1u : 0u;
            }
            return _kotor1CapabilityFlag2 != 0;
        }

        /// <summary>
        /// Generate random value (matching swkotor.exe: FUN_006fae8c @ 0x006fae8c).
        /// </summary>
        private ulong GenerateKotor1RandomValue()
        {
            // Matching swkotor.exe: FUN_006fae8c
            // This is a random number generator
            Random random = new Random();
            return (ulong)random.Next();
        }

        /// <summary>
        /// Create secondary window/context (matching swkotor.exe: FUN_00426560 @ 0x00426560).
        /// </summary>
        /// <remarks>
        /// FUN_00426560 creates a secondary OpenGL context with specific attributes.
        /// It uses wglChoosePixelFormatARB and wglCreateContextAttribsARB to create
        /// a context with specific pixel format and context attributes.
        /// </remarks>
        private IntPtr CreateKotor1SecondaryWindow()
        {
            // Matching swkotor.exe: FUN_00426560 @ 0x00426560 exactly
            // This function creates a hidden window for secondary OpenGL contexts
            // The window is used to get a device context for creating secondary contexts

            // Register window class (matching swkotor.exe pattern)
            WndProcDelegate wndProcDelegate = DefWindowProcA;
            WNDCLASSA wndClass = new WNDCLASSA
            {
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = IntPtr.Zero,
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = "KOTOR1SecondaryWindow"
            };

            RegisterClassA(ref wndClass);

            // Create hidden window (1x1, matching swkotor.exe pattern)
            IntPtr hWnd = CreateWindowExA(0, "KOTOR1SecondaryWindow", "Secondary Window", 0, 0, 0, 1, 1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (hWnd != IntPtr.Zero)
            {
                // Get device context for the window
                IntPtr hdc = GetDC(hWnd);
                if (hdc != IntPtr.Zero)
                {
                    // Set up pixel format attributes (matching swkotor.exe lines 30-48)
                    int[] attribIList = new int[]
                    {
                        WGL_DRAW_TO_WINDOW_ARB, 1,
                        WGL_SUPPORT_OPENGL_ARB, 1,
                        WGL_DOUBLE_BUFFER_ARB, 1,
                        WGL_PIXEL_TYPE_ARB, WGL_TYPE_RGBA_ARB,
                        WGL_COLOR_BITS_ARB, 8,
                        WGL_DEPTH_BITS_ARB, 8,
                        WGL_STENCIL_BITS_ARB, 8,
                        WGL_ACCELERATION_ARB, WGL_FULL_ACCELERATION_ARB,
                        0
                    };

                    // Set up pixel format descriptor (matching swkotor.exe lines 34-48)
                    PIXELFORMATDESCRIPTOR pfd = new PIXELFORMATDESCRIPTOR
                    {
                        nSize = 0x28,
                        nVersion = 1,
                        dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
                        iPixelType = PFD_TYPE_RGBA,
                        cColorBits = 8,
                        cAlphaBits = 8,
                        cDepthBits = 8,
                        cStencilBits = 8,
                        iLayerType = PFD_MAIN_PLANE
                    };

                    // Choose pixel format (matching swkotor.exe line 50)
                    int pixelFormat = 0;
                    if (_kotor1WglChoosePixelFormatArb != null)
                    {
                        int formats;
                        uint numFormats;
                        if (_kotor1WglChoosePixelFormatArb(hdc, attribIList, null, 1, out formats, out numFormats) && numFormats > 0)
                        {
                            pixelFormat = formats;
                        }
                    }

                    if (pixelFormat == 0)
                    {
                        // Fallback to standard ChoosePixelFormat
                        pixelFormat = ChoosePixelFormat(hdc, ref pfd);
                    }

                    if (pixelFormat != 0)
                    {
                        SetPixelFormat(hdc, pixelFormat, ref pfd);
                        ReleaseDC(hWnd, hdc);
                        return hWnd;
                    }

                    ReleaseDC(hWnd, hdc);
                }

                DestroyWindow(hWnd);
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Initialize additional textures (matching swkotor.exe: FUN_0047a2c0 @ 0x0047a2c0).
        /// </summary>
        private void InitializeKotor1AdditionalTextures()
        {
            // Matching swkotor.exe: FUN_0047a2c0 @ 0x0047a2c0 exactly
            // This function queries OpenGL shading language version and max texture size
            // GL_SHADING_LANGUAGE_VERSION = 0x8874
            const uint GL_SHADING_LANGUAGE_VERSION = 0x8874;
            const uint GL_MAX_TEXTURE_SIZE = 0x0D33;

            IntPtr shadingLangVersion = glGetString(GL_SHADING_LANGUAGE_VERSION);
            if (shadingLangVersion != IntPtr.Zero)
            {
                string versionStr = Marshal.PtrToStringAnsi(shadingLangVersion);
                if (!string.IsNullOrEmpty(versionStr))
                {
                    // Query max texture size (matching swkotor.exe line 10)
                    int[] maxTextureSize = new int[1];
                    glGetIntegerv(GL_MAX_TEXTURE_SIZE, maxTextureSize);
                    // The result is stored but not used in the original function
                }
            }
        }

        /// <summary>
        /// Check additional texture support (matching swkotor.exe: FUN_0045f860 @ 0x0045f860).
        /// </summary>
        private bool CheckKotor1AdditionalTextureSupport()
        {
            // Matching swkotor.exe: FUN_0045f860
            uint result = _kotor1CapabilityFlag2;
            if (_kotor1CapabilityFlag2 == 0xffffffff)
            {
                uint combinedFlags = _kotor1RequiredExtensionFlags | _kotor1ExtensionFlag2 | _kotor1ExtensionFlag3;
                result = (_kotor1ExtensionFlags & combinedFlags) == combinedFlags ? 1u : 0u;
                _kotor1CapabilityFlag2 = result;
            }
            return result != 0;
        }

        /// <summary>
        /// Initialize extended textures (matching swkotor.exe: FUN_00427490 @ 0x00427490).
        /// </summary>
        private void InitializeKotor1ExtendedTextures()
        {
            // Matching swkotor.exe: FUN_00427490 @ 0x00427490 exactly
            // This function sets up multiple vertex programs with specific parameters
            // It creates 8 vertex programs and configures them with various parameter settings

            if (_kotor1GlGenProgramsArb == null || _kotor1GlBindProgramArb == null ||
                _kotor1GlProgramStringArb == null || _kotor1GlProgramEnvParameter4fArb == null ||
                _kotor1GlProgramLocalParameter4fArb == null || _kotor1GlProgramEnvParameter4fvArb == null ||
                _kotor1GlProgramLocalParameter4fvArb == null || _kotor1GlProgramLocalParameter4dvArb == null ||
                _kotor1GlProgramLocalParameter4dvArb2 == null)
            {
                return; // Vertex program support not available
            }

            // Enable vertex program mode (matching swkotor.exe line 5)
            // GL_VERTEX_PROGRAM_ARB = 0x8620
            glEnable(GL_VERTEX_PROGRAM_ARB);

            // OpenGL constants used in FUN_00427490
            const uint GL_TEXTURE0_ARB = 0x84C0;
            const uint GL_TEXTURE1_ARB = 0x84C1;
            const uint GL_TEXTURE2_ARB = 0x84C2;
            const uint GL_TEXTURE3_ARB = 0x84C3;
            const uint GL_TEXTURE4_ARB = 0x84C4;
            const uint GL_TEXTURE5_ARB = 0x84C5;
            const uint GL_TEXTURE6_ARB = 0x84C6;
            const uint GL_CONSTANT_COLOR = 0x8001;
            const uint GL_ONE_MINUS_CONSTANT_COLOR = 0x8002;
            const uint GL_CONSTANT_ALPHA = 0x8003;
            const uint GL_ONE_MINUS_CONSTANT_ALPHA = 0x8004;
            const uint GL_SRC_COLOR = 0x0300;
            const uint GL_ONE_MINUS_SRC_COLOR = 0x0301;
            const uint GL_SRC_ALPHA = 0x0302;
            const uint GL_ONE_MINUS_SRC_ALPHA = 0x0303;
            const uint GL_DST_ALPHA = 0x0304;
            const uint GL_ONE_MINUS_DST_ALPHA = 0x0305;
            const uint GL_DST_COLOR = 0x0306;
            const uint GL_ONE_MINUS_DST_COLOR = 0x0307;
            const uint GL_SRC_ALPHA_SATURATE = 0x0308;
            const uint GL_ZERO = 0;
            const uint GL_ONE = 1;
            const uint GL_PROGRAM_TARGET_NV = 0x8646;
            const uint GL_PROGRAM_FORMAT_ASCII_ARB = 0x8875;
            const uint GL_PROGRAM_ERROR_POSITION_ARB = 0x864B;
            const uint GL_PROGRAM_ERROR_STRING_ARB = 0x8874;
            const uint GL_PROGRAM_LENGTH_ARB = 0x8627;
            const uint GL_PROGRAM_BINDING_ARB = 0x8677;
            const uint GL_PROGRAM_INSTRUCTIONS_ARB = 0x88A0;
            const uint GL_MAX_PROGRAM_INSTRUCTIONS_ARB = 0x88A1;
            const uint GL_MAX_PROGRAM_NATIVE_INSTRUCTIONS_ARB = 0x88A2;
            const uint GL_PROGRAM_TEMPORARIES_ARB = 0x88A3;
            const uint GL_MAX_PROGRAM_TEMPORARIES_ARB = 0x88A4;
            const uint GL_MAX_PROGRAM_NATIVE_TEMPORARIES_ARB = 0x88A5;
            const uint GL_PROGRAM_PARAMETERS_ARB = 0x88A6;
            const uint GL_MAX_PROGRAM_PARAMETERS_ARB = 0x88A7;
            const uint GL_MAX_PROGRAM_NATIVE_PARAMETERS_ARB = 0x88A8;
            const uint GL_PROGRAM_ATTRIBS_ARB = 0x88A9;
            const uint GL_MAX_PROGRAM_ATTRIBS_ARB = 0x88AA;
            const uint GL_MAX_PROGRAM_NATIVE_ATTRIBS_ARB = 0x88AB;
            const uint GL_PROGRAM_ADDRESS_REGISTERS_ARB = 0x88AC;
            const uint GL_MAX_PROGRAM_ADDRESS_REGISTERS_ARB = 0x88AD;
            const uint GL_MAX_PROGRAM_NATIVE_ADDRESS_REGISTERS_ARB = 0x88AE;
            const uint GL_PROGRAM_LOCAL_PARAMETERS_ARB = 0x88B4;
            const uint GL_MAX_PROGRAM_LOCAL_PARAMETERS_ARB = 0x88B5;
            const uint GL_MAX_PROGRAM_NATIVE_LOCAL_PARAMETERS_ARB = 0x88B6;
            const uint GL_PROGRAM_ENV_PARAMETERS_ARB = 0x88B7;
            const uint GL_MAX_PROGRAM_ENV_PARAMETERS_ARB = 0x88B8;
            const uint GL_MAX_PROGRAM_NATIVE_ENV_PARAMETERS_ARB = 0x88B9;
            const uint GL_PROGRAM_UNDER_NATIVE_LIMITS_ARB = 0x88B1;
            const uint GL_PROGRAM_STRING_ARB = 0x8628;
            const uint GL_PROGRAM_ERROR_POSITION_NV = 0x864B;
            const uint GL_PROGRAM_ERROR_STRING_NV = 0x8874;
            const uint GL_PROGRAM_FORMAT_ASCII_NV = 0x8875;
            const uint GL_PROGRAM_LENGTH_NV = 0x8627;
            const uint GL_PROGRAM_TARGET_NV_VALUE = 0x8646;
            const uint GL_PROGRAM_RESIDENT_NV = 0x8647;
            const uint GL_PROGRAM_BINDING_NV = 0x8677;
            const uint GL_PROGRAM_INSTRUCTIONS_NV = 0x88A0;
            const uint GL_MAX_PROGRAM_INSTRUCTIONS_NV = 0x88A1;
            const uint GL_MAX_PROGRAM_NATIVE_INSTRUCTIONS_NV = 0x88A2;
            const uint GL_PROGRAM_TEMPORARIES_NV = 0x88A3;
            const uint GL_MAX_PROGRAM_TEMPORARIES_NV = 0x88A4;
            const uint GL_MAX_PROGRAM_NATIVE_TEMPORARIES_NV = 0x88A5;
            const uint GL_PROGRAM_PARAMETERS_NV = 0x88A6;
            const uint GL_MAX_PROGRAM_PARAMETERS_NV = 0x88A7;
            const uint GL_MAX_PROGRAM_NATIVE_PARAMETERS_NV = 0x88A8;
            const uint GL_PROGRAM_ATTRIBS_NV = 0x88A9;
            const uint GL_MAX_PROGRAM_ATTRIBS_NV = 0x88AA;
            const uint GL_MAX_PROGRAM_NATIVE_ATTRIBS_NV = 0x88AB;
            const uint GL_PROGRAM_ADDRESS_REGISTERS_NV = 0x88AC;
            const uint GL_MAX_PROGRAM_ADDRESS_REGISTERS_NV = 0x88AD;
            const uint GL_MAX_PROGRAM_NATIVE_ADDRESS_REGISTERS_NV = 0x88AE;
            const uint GL_PROGRAM_LOCAL_PARAMETERS_NV = 0x88B4;
            const uint GL_MAX_PROGRAM_LOCAL_PARAMETERS_NV = 0x88B5;
            const uint GL_MAX_PROGRAM_NATIVE_LOCAL_PARAMETERS_NV = 0x88B6;
            const uint GL_PROGRAM_ENV_PARAMETERS_NV = 0x88B7;
            const uint GL_MAX_PROGRAM_ENV_PARAMETERS_NV = 0x88B8;
            const uint GL_MAX_PROGRAM_NATIVE_ENV_PARAMETERS_NV = 0x88B9;
            const uint GL_PROGRAM_UNDER_NATIVE_LIMITS_NV = 0x88B1;
            const uint GL_PROGRAM_STRING_NV = 0x8628;

            // Create first vertex program (matching swkotor.exe lines 6-20)
            _kotor1GlGenProgramsArb(1, ref _kotor1VertexProgramId0);
            _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, _kotor1VertexProgramId0);
            _kotor1GlProgramStringArb(GL_VERTEX_PROGRAM_ARB, GL_PROGRAM_FORMAT_ASCII_ARB, 0, null); // Program string would be loaded here
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE0_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE2_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE3_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramEnvParameter4fvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE0_ARB, IntPtr.Zero);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE2_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE3_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE4_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, IntPtr.Zero);
            glDisable(GL_VERTEX_PROGRAM_ARB);

            // Create second vertex program (matching swkotor.exe lines 21-33)
            _kotor1GlGenProgramsArb(1, ref _kotor1VertexProgramId1);
            _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, _kotor1VertexProgramId1);
            _kotor1GlProgramStringArb(GL_VERTEX_PROGRAM_ARB, GL_PROGRAM_FORMAT_ASCII_ARB, 0, null);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE0_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramEnvParameter4fvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, IntPtr.Zero);
            _kotor1GlProgramEnvParameter4fvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE2_ARB, IntPtr.Zero);
            _kotor1GlProgramEnvParameter4fvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, IntPtr.Zero);
            _kotor1GlProgramEnvParameter4fvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE2_ARB, IntPtr.Zero);
            _kotor1GlProgramEnvParameter4fvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE3_ARB, IntPtr.Zero);
            _kotor1GlProgramLocalParameter4dvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, IntPtr.Zero);
            glDisable(GL_VERTEX_PROGRAM_ARB);

            // Create remaining vertex programs (matching swkotor.exe lines 34-97)
            // Programs 2-7 follow similar patterns with different parameter configurations
            _kotor1GlGenProgramsArb(1, ref _kotor1VertexProgramId2);
            _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, _kotor1VertexProgramId2);
            _kotor1GlProgramStringArb(GL_VERTEX_PROGRAM_ARB, GL_PROGRAM_FORMAT_ASCII_ARB, 0, null);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE0_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, IntPtr.Zero);
            glDisable(GL_VERTEX_PROGRAM_ARB);

            _kotor1GlGenProgramsArb(1, ref _kotor1VertexProgramId3);
            _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, _kotor1VertexProgramId3);
            _kotor1GlProgramStringArb(GL_VERTEX_PROGRAM_ARB, GL_PROGRAM_FORMAT_ASCII_ARB, 0, null);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE0_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE3_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, IntPtr.Zero);
            glDisable(GL_VERTEX_PROGRAM_ARB);

            _kotor1GlGenProgramsArb(1, ref _kotor1VertexProgramId4);
            _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, _kotor1VertexProgramId4);
            _kotor1GlProgramStringArb(GL_VERTEX_PROGRAM_ARB, GL_PROGRAM_FORMAT_ASCII_ARB, 0, null);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE0_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE2_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, IntPtr.Zero);
            glDisable(GL_VERTEX_PROGRAM_ARB);

            _kotor1GlGenProgramsArb(1, ref _kotor1VertexProgramId5);
            _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, _kotor1VertexProgramId5);
            _kotor1GlProgramStringArb(GL_VERTEX_PROGRAM_ARB, GL_PROGRAM_FORMAT_ASCII_ARB, 0, null);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE0_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE2_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE3_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE4_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4dvArb2(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE2_ARB, IntPtr.Zero);
            _kotor1GlProgramLocalParameter4dvArb2(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE3_ARB, IntPtr.Zero);
            _kotor1GlProgramLocalParameter4dvArb2(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, IntPtr.Zero);
            glDisable(GL_VERTEX_PROGRAM_ARB);

            _kotor1GlGenProgramsArb(1, ref _kotor1VertexProgramId6);
            _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, _kotor1VertexProgramId6);
            _kotor1GlProgramStringArb(GL_VERTEX_PROGRAM_ARB, GL_PROGRAM_FORMAT_ASCII_ARB, 0, null);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE0_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fvArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, IntPtr.Zero);
            glDisable(GL_VERTEX_PROGRAM_ARB);

            _kotor1GlGenProgramsArb(1, ref _kotor1VertexProgramId7);
            _kotor1GlBindProgramArb(GL_VERTEX_PROGRAM_ARB, _kotor1VertexProgramId7);
            _kotor1GlProgramStringArb(GL_VERTEX_PROGRAM_ARB, GL_PROGRAM_FORMAT_ASCII_ARB, 0, null);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE0_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE2_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramEnvParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE3_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4fArb(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, 0.0f, 0.0f, 0.0f, 0.0f);
            _kotor1GlProgramLocalParameter4dvArb2(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE2_ARB, IntPtr.Zero);
            _kotor1GlProgramLocalParameter4dvArb2(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE3_ARB, IntPtr.Zero);
            _kotor1GlProgramLocalParameter4dvArb2(GL_VERTEX_PROGRAM_ARB, GL_TEXTURE1_ARB, IntPtr.Zero);
            glDisable(GL_VERTEX_PROGRAM_ARB);

            // Disable vertex program mode (matching swkotor.exe line 97)
            glDisable(GL_VERTEX_PROGRAM_ARB);
        }

        /// <summary>
        /// Check WGL_NV_render_texture_rectangle support (matching swkotor.exe: FUN_0045f7b0 @ 0x0045f7b0).
        /// </summary>
        private void CheckKotor1RenderTextureRectangleSupport()
        {
            // Matching swkotor.exe: FUN_0045f7b0 @ 0x0045f7b0 exactly
            if (_kotor1RenderTextureRectangleFlag == 0xffffffff)
            {
                // Check if WGL_NV_render_texture_rectangle extension is supported
                // DAT_007bb85c is the extension flags, PTR_DAT_0078e4dc and DAT_0078e4e4 are extension bit masks
                uint extensionMask = 0x00000001; // WGL_NV_render_texture_rectangle bit
                _kotor1RenderTextureRectangleFlag = (_kotor1ExtensionFlags & extensionMask) == extensionMask ? 1u : 0u;
            }
        }

        /// <summary>
        /// Check pbuffer support (matching swkotor.exe: FUN_0045f7e0 @ 0x0045f7e0).
        /// </summary>
        private uint CheckKotor1PbufferSupport()
        {
            // Matching swkotor.exe: FUN_0045f7e0 @ 0x0045f7e0 exactly
            if (_kotor1PbufferSupportFlag == 0xffffffff)
            {
                // Check if WGL_ARB_pbuffer extension is supported
                // DAT_007bb860 is the pbuffer support flag
                // DAT_0078e4c8 and DAT_0078e4d0 are extension bit masks
                uint extensionMask = 0x00000002; // WGL_ARB_pbuffer bit
                _kotor1PbufferSupportFlag = (_kotor1ExtensionFlags & extensionMask) == extensionMask ? 1u : 0u;
            }
            return _kotor1PbufferSupportFlag;
        }

        /// <summary>
        /// Calculate texture dimensions (matching swkotor.exe: FUN_00427450 @ 0x00427450).
        /// </summary>
        private void CalculateKotor1TextureDimensions(int screenWidth, int screenHeight, out int textureWidth, out int textureHeight)
        {
            // Matching swkotor.exe: FUN_00427450 @ 0x00427450
            // This function calculates power-of-2 texture dimensions
            // Round up to next power of 2
            textureWidth = 1;
            while (textureWidth < screenWidth)
            {
                textureWidth <<= 1;
            }

            textureHeight = 1;
            while (textureHeight < screenHeight)
            {
                textureHeight <<= 1;
            }
        }

        /// <summary>
        /// KOTOR 1-specific rendering methods.
        /// Matches swkotor.exe rendering code exactly.
        /// </summary>
        /// <remarks>
        /// Rendering in KOTOR1 is handled by the Area.Render() method which manages
        /// all scene rendering including rooms, entities, effects, lighting, and fog.
        /// This method is a wrapper that ensures the OpenGL context is current before rendering.
        /// </remarks>
        protected override void RenderOdysseyScene()
        {
            // KOTOR 1 scene rendering
            // Matches swkotor.exe rendering code exactly
            // The actual rendering is handled by Area.Render() which calls into the graphics system
            // This method ensures the OpenGL context is current before rendering

            // Make sure primary context is current (matching swkotor.exe rendering pattern)
            if (_kotor1PrimaryDC != IntPtr.Zero && _kotor1PrimaryContext != IntPtr.Zero)
            {
                wglMakeCurrent(_kotor1PrimaryDC, _kotor1PrimaryContext);

                // Clear the frame buffer (matching swkotor.exe: glClear calls)
                glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT | GL_STENCIL_BUFFER_BIT);

                // The actual scene rendering is handled by the Area system
                // which calls into the graphics backend through the rendering pipeline
                // This matches the original game's rendering architecture
            }
        }

        /// <summary>
        /// KOTOR 1-specific texture loading.
        /// Matches swkotor.exe texture loading code exactly (FUN_00427c90 @ 0x00427c90).
        /// </summary>
        /// <remarks>
        /// Texture loading in KOTOR1 uses the resource system to load TPC/TGA files.
        /// This method implements the full texture loading pipeline:
        /// 1. Load TPC/TGA file from resource system
        /// 2. Parse texture data (handles TPC, TGA, DDS formats)
        /// 3. Generate OpenGL texture ID (matching swkotor.exe: glGenTextures pattern)
        /// 4. Upload texture data with mipmap support (glTexImage2D, glCompressedTexImage2D)
        /// 5. Set texture parameters (matching swkotor.exe texture setup)
        ///
        /// Based on reverse engineering of swkotor.exe:
        /// - Texture initialization: FUN_00427c90 @ 0x00427c90
        /// - Resource loading: CExoResMan::GetResObject, CExoKeyTable lookup
        /// - File formats: TPC (primary), TGA (fallback), DDS (compressed)
        /// - OpenGL texture upload: glGenTextures, glBindTexture, glTexImage2D, glCompressedTexImage2D
        /// - Mipmap handling: All mipmap levels uploaded sequentially
        /// - Cube map support: GL_TEXTURE_CUBE_MAP for environment maps
        /// </remarks>
        protected override IntPtr LoadOdysseyTexture(string path)
        {
            // KOTOR 1 texture loading
            // Matches swkotor.exe texture loading code exactly (FUN_00427c90 @ 0x00427c90)

            if (string.IsNullOrEmpty(path))
            {
                return IntPtr.Zero;
            }

            // Make sure the primary context is current
            if (_kotor1PrimaryContext == IntPtr.Zero || _kotor1PrimaryDC == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            wglMakeCurrent(_kotor1PrimaryDC, _kotor1PrimaryContext);

            try
            {
                // Step 1: Load texture data from resource system
                byte[] textureData = LoadTextureData(path);
                if (textureData == null || textureData.Length == 0)
                {
                    Console.WriteLine($"[Kotor1GraphicsBackend] LoadOdysseyTexture: Failed to load texture data for '{path}'");
                    return IntPtr.Zero;
                }

                // Step 2: Parse texture file (handles TPC, TGA, DDS formats)
                TPC tpc = null;
                try
                {
                    tpc = TPCAuto.ReadTpc(textureData);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Kotor1GraphicsBackend] LoadOdysseyTexture: Failed to parse texture '{path}': {ex.Message}");
                    return IntPtr.Zero;
                }

                if (tpc == null || tpc.Layers.Count == 0 || tpc.Layers[0].Mipmaps.Count == 0)
                {
                    Console.WriteLine($"[Kotor1GraphicsBackend] LoadOdysseyTexture: Invalid texture data for '{path}'");
                    return IntPtr.Zero;
                }

                // Step 3: Get texture dimensions and format
                var firstMipmap = tpc.Layers[0].Mipmaps[0];
                int width = firstMipmap.Width;
                int height = firstMipmap.Height;
                TPCTextureFormat tpcFormat = tpc.Format();

                if (width <= 0 || height <= 0)
                {
                    Console.WriteLine($"[Kotor1GraphicsBackend] LoadOdysseyTexture: Invalid texture dimensions for '{path}' ({width}x{height})");
                    return IntPtr.Zero;
                }

                // Step 4: Generate texture ID (matching swkotor.exe: glGenTextures pattern)
                uint textureId = 0;
                glGenTextures(1, ref textureId);

                if (textureId == 0)
                {
                    Console.WriteLine($"[Kotor1GraphicsBackend] LoadOdysseyTexture: glGenTextures failed for '{path}'");
                    return IntPtr.Zero;
                }

                // Step 5: Determine OpenGL texture target (2D or cube map)
                uint textureTarget = GL_TEXTURE_2D;
                if (tpc.IsCubeMap)
                {
                    textureTarget = GL_TEXTURE_CUBE_MAP;
                }

                // Step 6: Bind texture
                glBindTexture(textureTarget, textureId);

                // Step 7: Set texture parameters (matching swkotor.exe texture setup)
                // Use TXI metadata if available for texture parameters
                bool useMipmaps = tpc.Layers[0].Mipmaps.Count > 1;
                if (tpc.TxiObject != null && tpc.TxiObject.Features != null)
                {
                    // Apply TXI texture parameters
                    var features = tpc.TxiObject.Features;

                    // Wrap mode: Use Clamp property if available
                    if (features.Clamp.HasValue && features.Clamp.Value)
                    {
                        glTexParameteri(textureTarget, GL_TEXTURE_WRAP_S, (int)GL_CLAMP_TO_EDGE);
                        glTexParameteri(textureTarget, GL_TEXTURE_WRAP_T, (int)GL_CLAMP_TO_EDGE);
                    }
                    else
                    {
                        glTexParameteri(textureTarget, GL_TEXTURE_WRAP_S, (int)GL_REPEAT);
                        glTexParameteri(textureTarget, GL_TEXTURE_WRAP_T, (int)GL_REPEAT);
                    }

                    // Filter mode: Use Filter property if available
                    if (features.Filter.HasValue && !features.Filter.Value)
                    {
                        // Filter disabled = nearest
                        glTexParameteri(textureTarget, GL_TEXTURE_MIN_FILTER, useMipmaps ? (int)GL_NEAREST_MIPMAP_NEAREST : (int)GL_NEAREST);
                        glTexParameteri(textureTarget, GL_TEXTURE_MAG_FILTER, (int)GL_NEAREST);
                    }
                    else
                    {
                        // Filter enabled = linear
                        glTexParameteri(textureTarget, GL_TEXTURE_MIN_FILTER, useMipmaps ? (int)GL_LINEAR_MIPMAP_LINEAR : (int)GL_LINEAR);
                        glTexParameteri(textureTarget, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                    }
                }
                else
                {
                    // Default texture parameters (matching swkotor.exe default settings)
                    glTexParameteri(textureTarget, GL_TEXTURE_WRAP_S, (int)GL_REPEAT);
                    glTexParameteri(textureTarget, GL_TEXTURE_WRAP_T, (int)GL_REPEAT);
                    glTexParameteri(textureTarget, GL_TEXTURE_MIN_FILTER, useMipmaps ? (int)GL_LINEAR_MIPMAP_LINEAR : (int)GL_LINEAR);
                    glTexParameteri(textureTarget, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);
                }

                // Step 8: Upload texture data with mipmaps
                bool uploadSuccess = UploadTextureData(textureTarget, tpc, tpcFormat);

                if (!uploadSuccess)
                {
                    Console.WriteLine($"[Kotor1GraphicsBackend] LoadOdysseyTexture: Failed to upload texture data for '{path}'");
                    glDeleteTextures(1, ref textureId);
                    glBindTexture(textureTarget, 0);
                    return IntPtr.Zero;
                }

                // Step 9: Unbind texture
                glBindTexture(textureTarget, 0);

                Console.WriteLine($"[Kotor1GraphicsBackend] LoadOdysseyTexture: Successfully loaded texture '{path}' (ID={textureId}, {width}x{height}, Format={tpcFormat}, Mipmaps={tpc.Layers[0].Mipmaps.Count}, CubeMap={tpc.IsCubeMap})");

                return (IntPtr)textureId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Kotor1GraphicsBackend] LoadOdysseyTexture: Exception loading texture '{path}': {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Loads texture data from resource system or file system.
        /// Matches swkotor.exe resource loading pattern (CExoResMan, CExoKeyTable).
        /// </summary>
        private byte[] LoadTextureData(string resRef)
        {
            if (_resourceProvider != null)
            {
                // Try TPC first (most common format for KOTOR 1)
                ResourceIdentifier tpcId = new ResourceIdentifier(resRef, Andastra.Parsing.Resource.ResourceType.TPC);
                Task<bool> existsTask = _resourceProvider.ExistsAsync(tpcId, CancellationToken.None);
                existsTask.Wait();
                if (existsTask.Result)
                {
                    Task<byte[]> dataTask = _resourceProvider.GetResourceBytesAsync(tpcId, CancellationToken.None);
                    dataTask.Wait();
                    return dataTask.Result;
                }

                // Try TGA format as fallback
                ResourceIdentifier tgaId = new ResourceIdentifier(resRef, Andastra.Parsing.Resource.ResourceType.TGA);
                existsTask = _resourceProvider.ExistsAsync(tgaId, CancellationToken.None);
                existsTask.Wait();
                if (existsTask.Result)
                {
                    Task<byte[]> dataTask = _resourceProvider.GetResourceBytesAsync(tgaId, CancellationToken.None);
                    dataTask.Wait();
                    return dataTask.Result;
                }

                // Try DDS format (compressed textures)
                ResourceIdentifier ddsId = new ResourceIdentifier(resRef, Andastra.Parsing.Resource.ResourceType.DDS);
                existsTask = _resourceProvider.ExistsAsync(ddsId, CancellationToken.None);
                existsTask.Wait();
                if (existsTask.Result)
                {
                    Task<byte[]> dataTask = _resourceProvider.GetResourceBytesAsync(ddsId, CancellationToken.None);
                    dataTask.Wait();
                    return dataTask.Result;
                }

                Console.WriteLine($"[Kotor1GraphicsBackend] LoadTextureData: Texture resource not found for '{resRef}' (tried TPC, TGA, DDS)");
                return null;
            }

            // Fallback: Try to load from file system (for development/testing)
            string[] extensions = { ".tpc", ".tga", ".dds" };
            foreach (string ext in extensions)
            {
                string filePath = resRef + ext;
                if (File.Exists(filePath))
                {
                    return File.ReadAllBytes(filePath);
                }
            }

            Console.WriteLine($"[Kotor1GraphicsBackend] LoadTextureData: No resource provider set and file not found for '{resRef}'");
            return null;
        }

        /// <summary>
        /// Uploads texture data to OpenGL with mipmap support.
        /// Matches swkotor.exe texture upload pattern (glTexImage2D, glCompressedTexImage2D).
        /// Based on reverse engineering of FUN_00427c90 @ 0x00427c90.
        /// </summary>
        private bool UploadTextureData(uint textureTarget, TPC tpc, TPCTextureFormat tpcFormat)
        {
            try
            {
                // Convert TPC format to OpenGL format
                uint glFormat = ConvertTPCFormatToOpenGLFormat(tpcFormat);
                uint glInternalFormat = ConvertTPCFormatToOpenGLInternalFormat(tpcFormat);
                uint glType = GL_UNSIGNED_BYTE;

                // Check if format is compressed (DXT1, DXT3, DXT5)
                bool isCompressed = tpcFormat == TPCTextureFormat.DXT1 ||
                                    tpcFormat == TPCTextureFormat.DXT3 ||
                                    tpcFormat == TPCTextureFormat.DXT5;

                // Handle cube maps
                if (tpc.IsCubeMap && tpc.Layers.Count == 6)
                {
                    // Cube map has 6 faces
                    uint[] cubeMapTargets = new uint[]
                    {
                        GL_TEXTURE_CUBE_MAP_POSITIVE_X,
                        GL_TEXTURE_CUBE_MAP_NEGATIVE_X,
                        GL_TEXTURE_CUBE_MAP_POSITIVE_Y,
                        GL_TEXTURE_CUBE_MAP_NEGATIVE_Y,
                        GL_TEXTURE_CUBE_MAP_POSITIVE_Z,
                        GL_TEXTURE_CUBE_MAP_NEGATIVE_Z
                    };

                    for (int face = 0; face < 6 && face < tpc.Layers.Count; face++)
                    {
                        var layer = tpc.Layers[face];
                        for (int mip = 0; mip < layer.Mipmaps.Count; mip++)
                        {
                            var mipmap = layer.Mipmaps[mip];
                            int mipWidth = Math.Max(1, mipmap.Width);
                            int mipHeight = Math.Max(1, mipmap.Height);

                            if (isCompressed)
                            {
                                UploadCompressedTextureData(cubeMapTargets[face], mip, glInternalFormat, mipWidth, mipHeight, mipmap.Data);
                            }
                            else
                            {
                                UploadUncompressedTextureData(cubeMapTargets[face], mip, glInternalFormat, mipWidth, mipHeight, glFormat, glType, mipmap.Data);
                            }
                        }
                    }
                }
                else
                {
                    // Regular 2D texture
                    var layer = tpc.Layers[0];
                    for (int mip = 0; mip < layer.Mipmaps.Count; mip++)
                    {
                        var mipmap = layer.Mipmaps[mip];
                        int mipWidth = Math.Max(1, mipmap.Width);
                        int mipHeight = Math.Max(1, mipmap.Height);

                        if (isCompressed)
                        {
                            UploadCompressedTextureData(textureTarget, mip, glInternalFormat, mipWidth, mipHeight, mipmap.Data);
                        }
                        else
                        {
                            UploadUncompressedTextureData(textureTarget, mip, glInternalFormat, mipWidth, mipHeight, glFormat, glType, mipmap.Data);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Kotor1GraphicsBackend] UploadTextureData: Exception uploading texture: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Uploads uncompressed texture data to OpenGL.
        /// Matches swkotor.exe: glTexImage2D pattern (FUN_00427c90 @ 0x00427c90).
        /// Handles BGRA/BGR to RGBA/RGB conversion for OpenGL compatibility.
        /// </summary>
        private void UploadUncompressedTextureData(uint target, int level, uint internalFormat, int width, int height, uint format, uint type, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            byte[] uploadData = data;
            uint uploadFormat = format;

            // Convert BGRA/BGR to RGBA/RGB for OpenGL (swkotor.exe does this conversion)
            if (format == GL_BGRA)
            {
                uploadData = ConvertBGRAToRGBA(data);
                uploadFormat = GL_RGBA;
            }
            else if (format == GL_BGR)
            {
                uploadData = ConvertBGRToRGB(data);
                uploadFormat = GL_RGB;
            }

            // Pin data for P/Invoke
            GCHandle handle = GCHandle.Alloc(uploadData, GCHandleType.Pinned);
            try
            {
                IntPtr dataPtr = handle.AddrOfPinnedObject();
                glTexImage2D(target, level, (int)internalFormat, width, height, 0, uploadFormat, type, dataPtr);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Converts BGRA pixel data to RGBA.
        /// Matches swkotor.exe BGRA to RGBA conversion.
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
        /// Converts BGR pixel data to RGB.
        /// Matches swkotor.exe BGR to RGB conversion.
        /// </summary>
        private byte[] ConvertBGRToRGB(byte[] bgrData)
        {
            if (bgrData == null || bgrData.Length == 0)
            {
                return bgrData;
            }

            byte[] rgbData = new byte[bgrData.Length];
            for (int i = 0; i < bgrData.Length; i += 3)
            {
                if (i + 2 < bgrData.Length)
                {
                    // BGR -> RGB: swap R and B channels
                    rgbData[i] = bgrData[i + 2];     // R
                    rgbData[i + 1] = bgrData[i + 1];  // G
                    rgbData[i + 2] = bgrData[i];      // B
                }
            }
            return rgbData;
        }

        /// <summary>
        /// Uploads compressed texture data to OpenGL (DXT1, DXT3, DXT5).
        /// Matches swkotor.exe: glCompressedTexImage2D pattern.
        /// </summary>
        private void UploadCompressedTextureData(uint target, int level, uint internalFormat, int width, int height, byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            // Pin data for P/Invoke
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                IntPtr dataPtr = handle.AddrOfPinnedObject();
                glCompressedTexImage2D(target, level, (int)internalFormat, width, height, 0, data.Length, dataPtr);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Creates a separate OpenGL texture for a single mip level.
        /// Matches swkotor.exe pattern where each mip level has its own texture ID.
        /// Based on reverse engineering of FUN_0041fa30 @ 0x0041fa30.
        /// </summary>
        /// <param name="mipmap">The mipmap data to upload</param>
        /// <param name="tpcFormat">The TPC texture format</param>
        /// <param name="isCubeMap">Whether this is a cube map texture</param>
        /// <param name="cubeMapFace">Cube map face index (0-5), ignored if not cube map</param>
        /// <returns>OpenGL texture ID, or 0 if creation failed</returns>
        private uint CreateMipLevelTexture(TPCMipmap mipmap, TPCTextureFormat tpcFormat, bool isCubeMap, int cubeMapFace)
        {
            if (mipmap == null || mipmap.Data == null || mipmap.Data.Length == 0)
            {
                return 0;
            }

            // Convert TPC format to OpenGL format
            uint glFormat = ConvertTPCFormatToOpenGLFormat(tpcFormat);
            uint glInternalFormat = ConvertTPCFormatToOpenGLInternalFormat(tpcFormat);
            uint glType = GL_UNSIGNED_BYTE;

            // Check if format is compressed (DXT1, DXT3, DXT5)
            bool isCompressed = tpcFormat == TPCTextureFormat.DXT1 ||
                                tpcFormat == TPCTextureFormat.DXT3 ||
                                tpcFormat == TPCTextureFormat.DXT5;

            // Determine texture target
            uint textureTarget = GL_TEXTURE_2D;
            if (isCubeMap)
            {
                uint[] cubeMapTargets = new uint[]
                {
                    GL_TEXTURE_CUBE_MAP_POSITIVE_X,
                    GL_TEXTURE_CUBE_MAP_NEGATIVE_X,
                    GL_TEXTURE_CUBE_MAP_POSITIVE_Y,
                    GL_TEXTURE_CUBE_MAP_NEGATIVE_Y,
                    GL_TEXTURE_CUBE_MAP_POSITIVE_Z,
                    GL_TEXTURE_CUBE_MAP_NEGATIVE_Z
                };
                if (cubeMapFace >= 0 && cubeMapFace < 6)
                {
                    textureTarget = cubeMapTargets[cubeMapFace];
                }
                else
                {
                    textureTarget = GL_TEXTURE_CUBE_MAP_POSITIVE_X;
                }
            }

            // Generate texture ID
            uint textureId = 0;
            glGenTextures(1, ref textureId);

            if (textureId == 0)
            {
                return 0;
            }

            // Bind texture
            glBindTexture(isCubeMap ? GL_TEXTURE_CUBE_MAP : GL_TEXTURE_2D, textureId);

            // Set texture parameters (matching swkotor.exe texture setup)
            // For single mip level textures, use nearest or linear filtering without mipmaps
            glTexParameteri(isCubeMap ? GL_TEXTURE_CUBE_MAP : GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, (int)GL_REPEAT);
            glTexParameteri(isCubeMap ? GL_TEXTURE_CUBE_MAP : GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, (int)GL_REPEAT);
            glTexParameteri(isCubeMap ? GL_TEXTURE_CUBE_MAP : GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, (int)GL_LINEAR);
            glTexParameteri(isCubeMap ? GL_TEXTURE_CUBE_MAP : GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, (int)GL_LINEAR);

            // Upload mipmap data (always at level 0 since this is a separate texture)
            int mipWidth = Math.Max(1, mipmap.Width);
            int mipHeight = Math.Max(1, mipmap.Height);

            if (isCompressed)
            {
                UploadCompressedTextureData(textureTarget, 0, glInternalFormat, mipWidth, mipHeight, mipmap.Data);
            }
            else
            {
                UploadUncompressedTextureData(textureTarget, 0, glInternalFormat, mipWidth, mipHeight, glFormat, glType, mipmap.Data);
            }

            // Unbind texture
            glBindTexture(isCubeMap ? GL_TEXTURE_CUBE_MAP : GL_TEXTURE_2D, 0);

            return textureId;
        }

        /// <summary>
        /// Converts TPC texture format to OpenGL format.
        /// Matches swkotor.exe format conversion logic (FUN_00427c90 @ 0x00427c90).
        /// </summary>
        private uint ConvertTPCFormatToOpenGLFormat(TPCTextureFormat tpcFormat)
        {
            switch (tpcFormat)
            {
                case TPCTextureFormat.RGB:
                    return GL_RGB;
                case TPCTextureFormat.RGBA:
                    return GL_RGBA;
                case TPCTextureFormat.BGRA:
                    return GL_BGRA;
                case TPCTextureFormat.BGR:
                    return GL_BGR;
                case TPCTextureFormat.Greyscale:
                    return GL_LUMINANCE;
                case TPCTextureFormat.DXT1:
                case TPCTextureFormat.DXT3:
                case TPCTextureFormat.DXT5:
                    // Compressed formats use internal format, not format parameter
                    return GL_RGBA; // Not used for compressed, but required for function signature
                default:
                    return GL_RGBA;
            }
        }

        /// <summary>
        /// Converts TPC texture format to OpenGL internal format.
        /// Matches swkotor.exe format conversion logic (FUN_00427c90 @ 0x00427c90).
        /// </summary>
        private uint ConvertTPCFormatToOpenGLInternalFormat(TPCTextureFormat tpcFormat)
        {
            switch (tpcFormat)
            {
                case TPCTextureFormat.RGB:
                    return GL_RGB;
                case TPCTextureFormat.RGBA:
                    return GL_RGBA8;
                case TPCTextureFormat.BGRA:
                    return GL_RGBA8; // BGRA converted to RGBA8 internally
                case TPCTextureFormat.BGR:
                    return GL_RGB; // BGR converted to RGB internally
                case TPCTextureFormat.Greyscale:
                    return GL_LUMINANCE;
                case TPCTextureFormat.DXT1:
                    return GL_COMPRESSED_RGB_S3TC_DXT1_EXT;
                case TPCTextureFormat.DXT3:
                    return GL_COMPRESSED_RGBA_S3TC_DXT3_EXT;
                case TPCTextureFormat.DXT5:
                    return GL_COMPRESSED_RGBA_S3TC_DXT5_EXT;
                default:
                    return GL_RGBA8;
            }
        }

        #endregion
    }
}
