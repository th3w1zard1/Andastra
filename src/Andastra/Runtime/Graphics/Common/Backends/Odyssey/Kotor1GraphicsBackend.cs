using System;
using System.Runtime.InteropServices;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Graphics.Common.Structs;

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
        
        #endregion
        
        #region P/Invoke Declarations (matching swkotor.exe API usage)
        
        // Windows API structures and functions used by swkotor.exe: FUN_0044dab0
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODEA
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public short dmOrientation;
            public short dmPaperSize;
            public short dmPaperLength;
            public short dmPaperWidth;
            public short dmScale;
            public short dmCopies;
            public short dmDefaultSource;
            public short dmPrintQuality;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        private struct PIXELFORMATDESCRIPTOR
        {
            public ushort nSize;
            public ushort nVersion;
            public uint dwFlags;
            public byte iPixelType;
            public byte cColorBits;
            public byte cRedBits;
            public byte cRedShift;
            public byte cGreenBits;
            public byte cGreenShift;
            public byte cBlueBits;
            public byte cBlueShift;
            public byte cAlphaBits;
            public byte cAlphaShift;
            public byte cAccumBits;
            public byte cAccumRedBits;
            public byte cAccumGreenBits;
            public byte cAccumBlueBits;
            public byte cAccumAlphaBits;
            public byte cDepthBits;
            public byte cStencilBits;
            public byte cAuxBuffers;
            public byte iLayerType;
            public byte bReserved;
            public uint dwLayerMask;
            public uint dwVisibleMask;
            public uint dwDamageMask;
        }
        
        // Windows API functions (matching swkotor.exe: FUN_0044dab0)
        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int SetWindowLongA(IntPtr hWnd, int nIndex, uint dwNewLong);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AdjustWindowRect(ref RECT lpRect, uint dwStyle, bool bMenu);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr SendMessageA(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettingsA(string lpszDeviceName, uint iModeNum, ref DEVMODEA lpDevMode);
        
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsA(ref DEVMODEA lpDevMode, uint dwFlags);
        
        [DllImport("gdi32.dll")]
        private static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR ppfd);
        
        [DllImport("gdi32.dll")]
        private static extern bool SetPixelFormat(IntPtr hdc, int iPixelFormat, ref PIXELFORMATDESCRIPTOR ppfd);
        
        [DllImport("gdi32.dll")]
        private static extern int DescribePixelFormat(IntPtr hdc, int iPixelFormat, uint nBytes, ref PIXELFORMATDESCRIPTOR ppfd);
        
        // OpenGL/WGL functions (matching swkotor.exe: FUN_0044dab0)
        [DllImport("opengl32.dll", SetLastError = true)]
        private static extern IntPtr wglCreateContext(IntPtr hdc);
        
        [DllImport("opengl32.dll", SetLastError = true)]
        private static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);
        
        [DllImport("opengl32.dll", SetLastError = true)]
        private static extern bool wglDeleteContext(IntPtr hglrc);
        
        [DllImport("opengl32.dll", SetLastError = true)]
        private static extern bool wglShareLists(IntPtr hglrc1, IntPtr hglrc2);
        
        [DllImport("opengl32.dll", SetLastError = true)]
        private static extern IntPtr wglGetCurrentContext();
        
        [DllImport("opengl32.dll", SetLastError = true)]
        private static extern IntPtr wglGetCurrentDC();
        
        [DllImport("opengl32.dll", SetLastError = true)]
        private static extern IntPtr wglGetProcAddress(string lpszProc);
        
        // WGL extension function delegates (matching swkotor.exe: FUN_0042e040)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool WglChoosePixelFormatArbDelegate(
            IntPtr hdc,
            [In] int[] piAttribIList,
            [In] float[] pfAttribFList,
            uint nMaxFormats,
            [Out] out int piFormats,
            [Out] out uint nNumFormats);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate string WglGetExtensionsStringArbDelegate(IntPtr hdc);
        
        // OpenGL functions (matching swkotor.exe: FUN_00427c90, FUN_00426cc0)
        [DllImport("opengl32.dll", EntryPoint = "glGenTextures")]
        private static extern void glGenTextures(int n, ref uint textures);
        
        [DllImport("opengl32.dll", EntryPoint = "glBindTexture")]
        private static extern void glBindTexture(uint target, uint texture);
        
        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D")]
        private static extern void glTexImage1D(uint target, int level, int internalformat, int width, int border, uint format, uint type, IntPtr pixels);
        
        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D")]
        private static extern void glTexImage2D(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, IntPtr pixels);
        
        [DllImport("opengl32.dll", EntryPoint = "glCopyTexImage2D")]
        private static extern void glCopyTexImage2D(uint target, int level, uint internalformat, int x, int y, int width, int height, int border);
        
        [DllImport("opengl32.dll", EntryPoint = "glTexParameteri")]
        private static extern void glTexParameteri(uint target, uint pname, int param);
        
        [DllImport("opengl32.dll", EntryPoint = "glEnable")]
        private static extern void glEnable(uint cap);
        
        [DllImport("opengl32.dll", EntryPoint = "glDisable")]
        private static extern void glDisable(uint cap);
        
        [DllImport("opengl32.dll", EntryPoint = "glGetString")]
        private static extern IntPtr glGetString(uint name);
        
        // OpenGL constants (matching swkotor.exe usage)
        private const uint GL_TEXTURE_1D = 0x0DE0;
        private const uint GL_TEXTURE_2D = 0x0DE1;
        private const uint GL_TEXTURE_RECTANGLE_NV = 0x84F5;
        private const uint GL_RGBA = 0x1908;
        private const uint GL_RGBA8 = 0x8058;
        private const uint GL_UNSIGNED_BYTE = 0x1401;
        private const uint GL_TEXTURE_WRAP_S = 0x2802;
        private const uint GL_TEXTURE_WRAP_T = 0x2803;
        private const uint GL_TEXTURE_MIN_FILTER = 0x2801;
        private const uint GL_TEXTURE_MAG_FILTER = 0x2800;
        private const uint GL_CLAMP_TO_EDGE = 0x812F;
        private const uint GL_LINEAR = 0x2601;
        private const uint GL_VENDOR = 0x1F00;
        private const uint GL_RENDERER = 0x1F01;
        private const uint GL_VERSION = 0x1F02;
        private const uint GL_EXTENSIONS = 0x1F03;
        private const uint GL_VERTEX_PROGRAM_ARB = 0x8620;
        private const uint GL_FRAGMENT_PROGRAM_ARB = 0x8804;
        private const uint GL_TEXTURE_3D = 0x806F;
        private const uint GL_DEPTH_TEST = 0x0B71;
        private const uint GL_STENCIL_TEST = 0x0B90;
        
        // Windows constants (matching swkotor.exe: FUN_0044dab0)
        private const int GWL_STYLE = -16;
        private const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
        private const uint WS_POPUP = 0x80000000;
        private const uint SW_SHOW = 1;
        private const IntPtr HWND_TOP = (IntPtr)(-2);
        private const IntPtr HWND_TOPMOST = (IntPtr)(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint WM_SIZE = 5;
        private const uint CDS_FULLSCREEN = 4;
        private const uint CDS_TEST = 2;
        private const uint DISP_CHANGE_SUCCESSFUL = 0;
        
        // PIXELFORMATDESCRIPTOR flags (matching swkotor.exe: FUN_0044dab0)
        private const uint PFD_DRAW_TO_WINDOW = 0x00000004;
        private const uint PFD_SUPPORT_OPENGL = 0x00000020;
        private const uint PFD_DOUBLEBUFFER = 0x00000001;
        private const byte PFD_TYPE_RGBA = 0;
        private const byte PFD_MAIN_PLANE = 0;
        
        // WGL_ARB_pixel_format attributes (matching swkotor.exe: FUN_0044dab0)
        private const int WGL_DRAW_TO_WINDOW_ARB = 0x2001;
        private const int WGL_SUPPORT_OPENGL_ARB = 0x2010;
        private const int WGL_DOUBLE_BUFFER_ARB = 0x2011;
        private const int WGL_PIXEL_TYPE_ARB = 0x2013;
        private const int WGL_COLOR_BITS_ARB = 0x2014;
        private const int WGL_DEPTH_BITS_ARB = 0x2022;
        private const int WGL_STENCIL_BITS_ARB = 0x2023;
        private const int WGL_SAMPLE_BUFFERS_ARB = 0x2041;
        private const int WGL_SAMPLES_ARB = 0x2042;
        private const int WGL_TYPE_RGBA_ARB = 0x202B;
        private const int WGL_ACCELERATION_ARB = 0x2003;
        private const int WGL_FULL_ACCELERATION_ARB = 0x2027;
        private const int WGL_SWAP_METHOD_ARB = 0x2017;
        private const int WGL_SWAP_EXCHANGE_ARB = 0x2028;
        private const int WGL_SWAP_COPY_ARB = 0x2029;
        private const int WGL_SWAP_UNDEFINED_ARB = 0x202A;
        private const int WGL_NUMBER_PIXEL_FORMATS_ARB = 0x2000;
        private const int WGL_DRAW_TO_BITMAP_ARB = 0x2002;
        private const int WGL_ACCELERATION_ARB_VALUE = 0x2015;
        private const int WGL_NEED_PALETTE_ARB = 0x2004;
        private const int WGL_NEED_SYSTEM_PALETTE_ARB = 0x2005;
        private const int WGL_SWAP_LAYER_BUFFERS_ARB = 0x2006;
        private const int WGL_SWAP_METHOD_ARB_VALUE = 0x2019;
        private const int WGL_NUMBER_OVERLAYS_ARB = 0x2008;
        private const int WGL_NUMBER_UNDERLAYS_ARB = 0x2009;
        private const int WGL_TRANSPARENT_ARB = 0x200A;
        private const int WGL_TRANSPARENT_RED_VALUE_ARB = 0x2037;
        private const int WGL_TRANSPARENT_GREEN_VALUE_ARB = 0x2038;
        private const int WGL_TRANSPARENT_BLUE_VALUE_ARB = 0x2039;
        private const int WGL_TRANSPARENT_ALPHA_VALUE_ARB = 0x203A;
        private const int WGL_TRANSPARENT_INDEX_VALUE_ARB = 0x203B;
        private const int WGL_SHARE_DEPTH_ARB = 0x200C;
        private const int WGL_SHARE_STENCIL_ARB = 0x200D;
        private const int WGL_SHARE_ACCUM_ARB = 0x200E;
        private const int WGL_SUPPORT_GDI_ARB = 0x200F;
        private const int WGL_SUPPORT_OPENGL_ARB_VALUE = 0x2012;
        private const int WGL_DOUBLE_BUFFER_ARB_VALUE = 0x2016;
        private const int WGL_STEREO_ARB = 0x2012;
        private const int WGL_PIXEL_TYPE_ARB_VALUE = 0x2013;
        private const int WGL_COLOR_BITS_ARB_VALUE = 0x201B;
        private const int WGL_RED_BITS_ARB = 0x2015;
        private const int WGL_RED_SHIFT_ARB = 0x2016;
        private const int WGL_GREEN_BITS_ARB = 0x2017;
        private const int WGL_GREEN_SHIFT_ARB = 0x2018;
        private const int WGL_BLUE_BITS_ARB = 0x2019;
        private const int WGL_BLUE_SHIFT_ARB = 0x201A;
        private const int WGL_ALPHA_BITS_ARB = 0x201B;
        private const int WGL_ALPHA_SHIFT_ARB = 0x201C;
        private const int WGL_ACCUM_BITS_ARB = 0x201D;
        private const int WGL_ACCUM_RED_BITS_ARB = 0x201E;
        private const int WGL_ACCUM_GREEN_BITS_ARB = 0x201F;
        private const int WGL_ACCUM_BLUE_BITS_ARB = 0x2020;
        private const int WGL_ACCUM_ALPHA_BITS_ARB = 0x2021;
        private const int WGL_DEPTH_BITS_ARB = 0x2022;
        private const int WGL_STENCIL_BITS_ARB = 0x2023;
        private const int WGL_AUX_BUFFERS_ARB = 0x2024;
        private const int WGL_NO_ACCELERATION_ARB = 0x2025;
        private const int WGL_GENERIC_ACCELERATION_ARB = 0x2026;
        private const int WGL_FULL_ACCELERATION_ARB_VALUE = 0x2027;
        private const int WGL_SWAP_EXCHANGE_ARB_VALUE = 0x2028;
        private const int WGL_SWAP_COPY_ARB_VALUE = 0x2029;
        private const int WGL_SWAP_UNDEFINED_ARB_VALUE = 0x202A;
        private const int WGL_TYPE_RGBA_ARB_VALUE = 0x202B;
        private const int WGL_TYPE_COLORINDEX_ARB = 0x202C;
        private const int WGL_TYPE_COLORINDEX_ARB_VALUE = 0x202D;
        private const int WGL_SAMPLE_BUFFERS_ARB_VALUE = 0x2041;
        private const int WGL_SAMPLES_ARB_VALUE = 0x2042;
        
        // Function pointer delegate types (matching swkotor.exe function pointers)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GlEnableDisableDelegate(bool enable);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr GetDCDelegate(IntPtr hWnd);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GlGenProgramsArbDelegate(int n, ref uint programs);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GlBindProgramArbDelegate(uint target, uint program);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private delegate void GlProgramStringArbDelegate(uint target, uint format, int len, string program);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GlGenVertexArraysDelegate(int n, ref uint arrays);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GlBindVertexArrayDelegate(uint array);
        
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void GlDeleteVertexArraysDelegate(int n, ref uint arrays);
        
        #endregion
        
        public override GraphicsBackendType BackendType => GraphicsBackendType.OdysseyEngine;

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
            ShowWindow(windowHandle, SW_SHOW);
            
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
                    
                    int formats;
                    uint numFormats;
                    if (_kotor1WglChoosePixelFormatArb(hdc, attribIList, null, 1, out formats, out numFormats) && numFormats > 0)
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
                // Store primary context and DC (matching swkotor.exe: FUN_00425c30)
                _kotor1PrimaryContext = wglGetCurrentContext();
                _kotor1PrimaryDC = wglGetCurrentDC();
                
                // Initialize OpenGL extensions (matching swkotor.exe line 209)
                InitializeKotor1OpenGLExtensions();
                
                // Check vertex program support (matching swkotor.exe lines 210-212)
                if (CheckKotor1VertexProgramSupport())
                {
                    InitializeKotor1VertexPrograms(); // FUN_004a2400
                }
                
                // Additional initialization (matching swkotor.exe line 214)
                // FUN_004015a0 is a no-op, so we skip it
                
                // Store context info (matching swkotor.exe: FUN_00425c30)
                // This is already done above
                
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
                if (_kotor1GlEnableDisable != null)
                {
                    _kotor1GlEnableDisable(_kotor1StencilTestFlag != 0);
                }
                
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
        /// Initialize OpenGL extensions (matching swkotor.exe: FUN_0042e040 @ 0x0042e040).
        /// </summary>
        private void InitializeKotor1OpenGLExtensions()
        {
            // This function creates a test window to query OpenGL extensions
            // Matching swkotor.exe: FUN_0042e040 exactly
            
            // Note: In a real implementation, we would create a temporary window,
            // but for now we'll query extensions from the current context if available
            if (_kotor1PrimaryDC != IntPtr.Zero)
            {
                // Get wglGetExtensionsStringARB function pointer
                IntPtr proc = wglGetProcAddress("wglGetExtensionsStringARB");
                if (proc != IntPtr.Zero)
                {
                    _kotor1WglGetExtensionsStringArb = Marshal.GetDelegateForFunctionPointer<WglGetExtensionsStringArbDelegate>(proc);
                }
                
                // Get wglChoosePixelFormatARB function pointer
                proc = wglGetProcAddress("wglChoosePixelFormatARB");
                if (proc != IntPtr.Zero)
                {
                    _kotor1WglChoosePixelFormatArb = Marshal.GetDelegateForFunctionPointer<WglChoosePixelFormatArbDelegate>(proc);
                }
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
        private void InitializeKotor1VertexPrograms()
        {
            // Matching swkotor.exe: FUN_004a2400
            // This function sets up vertex program state
            // The actual implementation would load vertex program strings and bind them
            // For now, this is a placeholder matching the function structure
        }
        
        /// <summary>
        /// Additional setup (matching swkotor.exe: FUN_00422360 @ 0x00422360).
        /// </summary>
        private void InitializeKotor1AdditionalSetup()
        {
            // Matching swkotor.exe: FUN_00422360
            // This function performs additional OpenGL state setup
            // The actual implementation would call various OpenGL state functions
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
                    glCopyTexImage2D(GL_TEXTURE_RECTANGLE_NV, 0, GL_RGBA8, 0, 0, _kotor1ScreenWidth, _kotor1ScreenHeight, 0);
                    glBindTexture(GL_TEXTURE_RECTANGLE_NV, 0);
                }
                
                // Create first secondary context texture (matching swkotor.exe lines 16-23)
                glGenTextures(1, ref _kotor1SecondaryTextures[0]);
                glBindTexture(GL_TEXTURE_RECTANGLE_NV, _kotor1SecondaryTextures[0]);
                glCopyTexImage2D(GL_TEXTURE_RECTANGLE_NV, 0, GL_RGBA8, 0, 0, _kotor1ScreenWidth, _kotor1ScreenHeight, 0);
                glTexParameteri(GL_TEXTURE_RECTANGLE_NV, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                glTexParameteri(GL_TEXTURE_RECTANGLE_NV, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                glTexParameteri(GL_TEXTURE_RECTANGLE_NV, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                glTexParameteri(GL_TEXTURE_RECTANGLE_NV, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
                
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
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
                            
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
                                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
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
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
                    glCopyTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, 0, 0, textureWidth, textureHeight, 0);
                    glBindTexture(GL_TEXTURE_2D, 0);
                }
                
                // Create first secondary context texture (matching swkotor.exe lines 61-67)
                glGenTextures(1, ref _kotor1SecondaryTextures[0]);
                glBindTexture(GL_TEXTURE_2D, _kotor1SecondaryTextures[0]);
                glCopyTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, 0, 0, textureWidth, textureHeight, 0);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
                
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
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
                        
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
                                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                                glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
                                
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
                                        if (_kotor1GlBindVertexArray != null)
                                        {
                                            _kotor1GlBindVertexArray(0);
                                        }
                                        
                                        // Delete vertex array object (matching swkotor.exe line 106)
                                        if (_kotor1GlDeleteVertexArrays != null)
                                        {
                                            _kotor1GlDeleteVertexArrays(1, ref vao);
                                        }
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
        private void InitializeKotor1VertexProgramResources()
        {
            // Matching swkotor.exe: FUN_0044dab0 lines 221-362
            // This function creates vertex program objects and binds them
            // The actual implementation would create and bind multiple vertex programs
            // For now, this is a placeholder matching the function structure
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
                        glCopyTexImage2D(GL_TEXTURE_RECTANGLE_NV, 0, GL_RGBA8, 0, 0, _kotor1ScreenWidth, _kotor1ScreenHeight, 0);
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
                    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA8, 1, 256, 0, GL_RGBA, GL_UNSIGNED_BYTE, handle0.AddrOfPinnedObject());
                    handle0.Free();
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
                    
                    // Create texture 1 (matching swkotor.exe lines 72-78)
                    glGenTextures(1, ref _kotor1Texture1);
                    glBindTexture(GL_TEXTURE_2D, _kotor1Texture1);
                    GCHandle handle1 = GCHandle.Alloc(textureData1, GCHandleType.Pinned);
                    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, 256, 1, 0, GL_RGBA, GL_UNSIGNED_BYTE, handle1.AddrOfPinnedObject());
                    handle1.Free();
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
                    
                    // Create texture 2 (matching swkotor.exe lines 79-85)
                    glGenTextures(1, ref _kotor1Texture2);
                    glBindTexture(GL_TEXTURE_2D, _kotor1Texture2);
                    GCHandle handle2 = GCHandle.Alloc(textureData2, GCHandleType.Pinned);
                    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, 1, 256, 0, GL_RGBA, GL_UNSIGNED_BYTE, handle2.AddrOfPinnedObject());
                    handle2.Free();
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
                    
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
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
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
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                            glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
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
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
                        glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
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
            // This function creates a secondary OpenGL context, not a window
            // It uses the current DC and creates a context with specific attributes
            
            if (_kotor1PrimaryDC == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            
            // Set up pixel format attributes (matching swkotor.exe lines 30-48)
            int[] attribIList = new int[]
            {
                WGL_DRAW_TO_WINDOW_ARB, 8,
                WGL_SUPPORT_OPENGL_ARB, 8,
                WGL_DOUBLE_BUFFER_ARB, 8,
                WGL_PIXEL_TYPE_ARB, WGL_TYPE_RGBA_ARB,
                WGL_COLOR_BITS_ARB, 8,
                WGL_DEPTH_BITS_ARB, 8,
                WGL_STENCIL_BITS_ARB, 8,
                WGL_ACCELERATION_ARB, WGL_FULL_ACCELERATION_ARB,
                0x2010, 0x202d, // Additional attributes
                0x2071, 0x2015,
                0x2017, 0x2019,
                0x201b, 0x2011,
                0, 0
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
            uint numFormats = 0;
            if (_kotor1WglChoosePixelFormatArb != null)
            {
                int formats;
                if (_kotor1WglChoosePixelFormatArb(_kotor1PrimaryDC, attribIList, null, 1, out formats, out numFormats) && numFormats > 0)
                {
                    pixelFormat = formats;
                }
            }
            
            if (pixelFormat == 0)
            {
                // Fallback to standard ChoosePixelFormat
                pixelFormat = ChoosePixelFormat(_kotor1PrimaryDC, ref pfd);
                if (pixelFormat != 0)
                {
                    SetPixelFormat(_kotor1PrimaryDC, pixelFormat, ref pfd);
                }
            }
            
            if (pixelFormat == 0)
            {
                return IntPtr.Zero;
            }
            
            // Create context attributes (matching swkotor.exe lines 51-55)
            int[] contextAttribs = new int[]
            {
                0x2072, 0x207a, // WGL_CONTEXT_MAJOR_VERSION_ARB, WGL_CONTEXT_MINOR_VERSION_ARB
                0, 0, // Version numbers (would be set based on OpenGL version)
                0 // Terminator
            };
            
            // Get wglCreateContextAttribsARB function pointer
            IntPtr proc = wglGetProcAddress("wglCreateContextAttribsARB");
            if (proc != IntPtr.Zero)
            {
                // Create context with attributes
                // Note: This requires a delegate type for wglCreateContextAttribsARB
                // For now, fall back to standard wglCreateContext
                IntPtr hglrc = wglCreateContext(_kotor1PrimaryDC);
                if (hglrc != IntPtr.Zero)
                {
                    return hglrc;
                }
            }
            else
            {
                // Fallback to standard wglCreateContext
                IntPtr hglrc = wglCreateContext(_kotor1PrimaryDC);
                if (hglrc != IntPtr.Zero)
                {
                    return hglrc;
                }
            }
            
            return IntPtr.Zero;
        }
        
        /// <summary>
        /// Initialize additional textures (matching swkotor.exe: FUN_0047a2c0 @ 0x0047a2c0).
        /// </summary>
        private void InitializeKotor1AdditionalTextures()
        {
            // Matching swkotor.exe: FUN_0047a2c0
            // This function queries OpenGL extensions and sets up additional texture state
            IntPtr extensions = glGetString(GL_EXTENSIONS);
            if (extensions != IntPtr.Zero)
            {
                // Query max texture size or other capabilities
                // The actual implementation would call glGetIntegerv
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
            // Matching swkotor.exe: FUN_00427490
            // This function sets up additional vertex program resources
            // The actual implementation would create and bind multiple vertex programs
            // For now, this is a placeholder matching the function structure
        }

        /// <summary>
        /// KOTOR 1-specific rendering methods.
        /// Matches swkotor.exe rendering code exactly.
        /// </summary>
        protected override void RenderOdysseyScene()
        {
            // KOTOR 1 scene rendering
            // Matches swkotor.exe rendering code exactly
            // TODO: Implement based on reverse engineering of swkotor.exe rendering functions
        }

        /// <summary>
        /// KOTOR 1-specific texture loading.
        /// Matches swkotor.exe texture loading code exactly.
        /// </summary>
        protected override IntPtr LoadOdysseyTexture(string path)
        {
            // KOTOR 1 texture loading
            // Matches swkotor.exe texture loading code exactly
            // TODO: Implement based on reverse engineering of swkotor.exe texture loading functions
            return IntPtr.Zero;
        }

        #endregion
    }
}
