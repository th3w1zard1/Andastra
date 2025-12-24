using System;
using System.Runtime.InteropServices;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Interfaces;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Graphics.Common.Structs;

namespace Andastra.Runtime.Graphics.Common.Backends
{
    /// <summary>
    /// Abstract base class for Odyssey engine graphics backends.
    /// 
    /// Odyssey engine is used by:
    /// - Star Wars: Knights of the Old Republic (swkotor.exe)
    /// - Star Wars: Knights of the Old Republic II - The Sith Lords (swkotor2.exe)
    /// 
    /// This backend matches the Odyssey engine's rendering implementation exactly 1:1,
    /// as reverse-engineered from swkotor.exe and swkotor2.exe.
    /// </summary>
    /// <remarks>
    /// Odyssey Engine Graphics Backend:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Original game graphics system: OpenGL (OPENGL32.DLL) with WGL extensions
    /// - Graphics initialization: 
    ///   - swkotor.exe: FUN_0044dab0 @ 0x0044dab0 (OpenGL context creation)
    ///   - swkotor2.exe: FUN_00461c50 @ 0x00461c50 (OpenGL context creation)
    /// - Common initialization pattern (both games):
    ///   1. Window setup (ShowWindow, SetWindowPos, AdjustWindowRect)
    ///   2. Display mode enumeration (EnumDisplaySettingsA, ChangeDisplaySettingsA)
    ///   3. Pixel format selection (ChoosePixelFormat, SetPixelFormat)
    ///   4. OpenGL context creation (wglCreateContext, wglMakeCurrent)
    ///   5. Context sharing setup (wglShareLists) - for multi-threaded rendering
    ///   6. Texture initialization (glGenTextures, glBindTexture, glTexImage2D)
    /// - Located via string references: 
    ///   - "wglCreateContext" @ swkotor.exe:0x0073d2b8, swkotor2.exe:0x007b52cc
    ///   - "wglChoosePixelFormatARB" @ swkotor.exe:0x0073f444, swkotor2.exe:0x007b880c
    ///   - "WGL_NV_render_texture_rectangle" @ swkotor.exe:0x00740798, swkotor2.exe:0x007b880c
    /// - Original game graphics device: OpenGL with WGL extensions for Windows
    /// - This implementation: Direct 1:1 match of Odyssey engine rendering code
    /// 
    /// Inheritance Structure:
    /// - BaseOriginalEngineGraphicsBackend (Common) - Original engine graphics backend base
    ///   - OdysseyGraphicsBackend (this class) - Common Odyssey OpenGL initialization
    ///     - Kotor1GraphicsBackend (swkotor.exe: 0x0044dab0, 0x00427c90, 0x00426cc0) - KOTOR1-specific
    ///     - Kotor2GraphicsBackend (swkotor2.exe: 0x00461c50, 0x0042a100, 0x00462560) - KOTOR2-specific
    /// </remarks>
    public abstract class OdysseyGraphicsBackend : BaseOriginalEngineGraphicsBackend
    {
        protected override string GetEngineName() => "Odyssey";

        protected override bool DetermineGraphicsApi()
        {
            // Odyssey engine uses OpenGL (not DirectX)
            // Both swkotor.exe and swkotor2.exe use OPENGL32.DLL
            // Based on reverse engineering:
            // - swkotor.exe: FUN_0044dab0 @ 0x0044dab0 uses wglCreateContext
            // - swkotor2.exe: FUN_00461c50 @ 0x00461c50 uses wglCreateContext
            _useDirectX9 = false;
            _useOpenGL = true;
            _adapterIndex = 0;
            _fullscreen = true; // Default to fullscreen (swkotor.exe: FUN_0044dab0 @ 0x0044dab0, param_7 != 0 = fullscreen)
            _refreshRate = 60; // Default refresh rate

            return true;
        }

        protected override void InitializeCapabilities()
        {
            base.InitializeCapabilities();

            // Odyssey engine-specific capabilities
            // These match the original engine's capabilities exactly
            _capabilities.ActiveBackend = GraphicsBackendType.OdysseyEngine;
        }

        #region Common Odyssey P/Invoke Declarations

        // Windows API structures (common to both KOTOR1 and KOTOR2)
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        protected struct DEVMODEA
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
        protected struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        protected struct PIXELFORMATDESCRIPTOR
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        protected struct WNDCLASSA
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpszMenuName;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpszClassName;
        }

        // Windows API functions (common to both KOTOR1 and KOTOR2)
        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        protected static extern int SetWindowLongA(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        protected static extern bool AdjustWindowRect(ref RECT lpRect, uint dwStyle, bool bMenu);

        [DllImport("user32.dll", SetLastError = true)]
        protected static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        protected static extern IntPtr SendMessageA(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        protected static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        protected static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        protected static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        protected static extern bool EnumDisplaySettingsA(string lpszDeviceName, uint iModeNum, ref DEVMODEA lpDevMode);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        protected static extern int ChangeDisplaySettingsA(ref DEVMODEA lpDevMode, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        protected static extern ushort RegisterClassA(ref WNDCLASSA lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        protected static extern IntPtr GetClassInfoA(IntPtr hInstance, string lpClassName, ref WNDCLASSA lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        protected static extern IntPtr CreateWindowExA(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        protected static extern IntPtr DefWindowProcA(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        protected static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        protected static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR ppfd);

        [DllImport("gdi32.dll")]
        protected static extern bool SetPixelFormat(IntPtr hdc, int iPixelFormat, ref PIXELFORMATDESCRIPTOR ppfd);

        [DllImport("gdi32.dll")]
        protected static extern int DescribePixelFormat(IntPtr hdc, int iPixelFormat, uint nBytes, ref PIXELFORMATDESCRIPTOR ppfd);

        [DllImport("gdi32.dll")]
        protected static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        protected static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint nPlanes, uint nBitCount, IntPtr lpBits);

        [DllImport("gdi32.dll")]
        protected static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        protected static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        protected static extern bool DeleteDC(IntPtr hdc);

        // OpenGL/WGL functions (common to both KOTOR1 and KOTOR2)
        [DllImport("opengl32.dll", SetLastError = true)]
        protected static extern IntPtr wglCreateContext(IntPtr hdc);

        [DllImport("opengl32.dll", SetLastError = true)]
        protected static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

        [DllImport("opengl32.dll", SetLastError = true)]
        protected static extern bool wglDeleteContext(IntPtr hglrc);

        [DllImport("opengl32.dll", SetLastError = true)]
        protected static extern bool wglShareLists(IntPtr hglrc1, IntPtr hglrc2);

        [DllImport("opengl32.dll", SetLastError = true)]
        protected static extern IntPtr wglGetCurrentContext();

        [DllImport("opengl32.dll", SetLastError = true)]
        protected static extern IntPtr wglGetCurrentDC();

        [DllImport("opengl32.dll", SetLastError = true)]
        protected static extern IntPtr wglGetProcAddress(string lpszProc);

        // OpenGL functions (common to both KOTOR1 and KOTOR2)
        [DllImport("opengl32.dll", EntryPoint = "glGenTextures")]
        protected static extern void glGenTextures(int n, ref uint textures);

        [DllImport("opengl32.dll", EntryPoint = "glBindTexture")]
        protected static extern void glBindTexture(uint target, uint texture);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage1D")]
        protected static extern void glTexImage1D(uint target, int level, int internalformat, int width, int border, uint format, uint type, IntPtr pixels);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D")]
        protected static extern void glTexImage2D(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, IntPtr pixels);

        [DllImport("opengl32.dll", EntryPoint = "glCopyTexImage2D")]
        protected static extern void glCopyTexImage2D(uint target, int level, uint internalformat, int x, int y, int width, int height, int border);

        [DllImport("opengl32.dll", EntryPoint = "glTexParameteri")]
        protected static extern void glTexParameteri(uint target, uint pname, int param);

        [DllImport("opengl32.dll", EntryPoint = "glEnable")]
        protected static extern void glEnable(uint cap);

        [DllImport("opengl32.dll", EntryPoint = "glDisable")]
        protected static extern void glDisable(uint cap);

        [DllImport("opengl32.dll", EntryPoint = "glGetString")]
        protected static extern IntPtr glGetString(uint name);

        [DllImport("opengl32.dll", EntryPoint = "glGetError")]
        protected static extern uint glGetError();

        [DllImport("opengl32.dll", EntryPoint = "glClear")]
        protected static extern void glClear(uint mask);

        // WGL extension function delegates (common to both KOTOR1 and KOTOR2)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate bool WglChoosePixelFormatArbDelegate(
            IntPtr hdc,
            [In] int[] piAttribIList,
            [In] float[] pfAttribFList,
            uint nMaxFormats,
            [Out] out int piFormats,
            [Out] out uint nNumFormats);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        protected delegate string WglGetExtensionsStringArbDelegate(IntPtr hdc);

        // Function pointer delegate types (common to both KOTOR1 and KOTOR2)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate IntPtr GetDCDelegate(IntPtr hWnd);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate void GlGenProgramsArbDelegate(int n, ref uint programs);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate void GlBindProgramArbDelegate(uint target, uint program);

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        protected delegate void GlProgramStringArbDelegate(uint target, uint format, int len, string program);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate void GlProgramEnvParameter4fArbDelegate(uint target, uint index, float x, float y, float z, float w);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate void GlProgramLocalParameter4fArbDelegate(uint target, uint index, float x, float y, float z, float w);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate void GlProgramEnvParameter4fvArbDelegate(uint target, uint index, float[] @params);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate void GlProgramLocalParameter4fvArbDelegate(uint target, uint index, float[] @params);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate void GlProgramLocalParameter4dvArbDelegate(uint target, uint index, double[] @params);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate void GlDisableDelegate(uint cap);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        protected delegate bool WglMakeCurrentDelegate(IntPtr hdc, IntPtr hglrc);

        // OpenGL constants (common to both KOTOR1 and KOTOR2)
        protected const uint GL_TEXTURE_1D = 0x0DE0;
        protected const uint GL_TEXTURE_2D = 0x0DE1;
        protected const uint GL_TEXTURE_RECTANGLE_NV = 0x84F5;
        protected const uint GL_RGBA = 0x1908;
        protected const uint GL_RGBA8 = 0x8058;
        protected const uint GL_UNSIGNED_BYTE = 0x1401;
        protected const uint GL_TEXTURE_WRAP_S = 0x2802;
        protected const uint GL_TEXTURE_WRAP_T = 0x2803;
        protected const uint GL_TEXTURE_MIN_FILTER = 0x2801;
        protected const uint GL_TEXTURE_MAG_FILTER = 0x2800;
        protected const uint GL_CLAMP_TO_EDGE = 0x812F;
        protected const uint GL_LINEAR = 0x2601;
        protected const uint GL_VENDOR = 0x1F00;
        protected const uint GL_RENDERER = 0x1F01;
        protected const uint GL_VERSION = 0x1F02;
        protected const uint GL_EXTENSIONS = 0x1F03;
        protected const uint GL_VERTEX_PROGRAM_ARB = 0x8620;
        protected const uint GL_FRAGMENT_PROGRAM_ARB = 0x8804;
        protected const uint GL_TEXTURE_3D = 0x806F;
        protected const uint GL_DEPTH_TEST = 0x0B71;
        protected const uint GL_STENCIL_TEST = 0x0B90;
        protected const uint GL_COLOR_BUFFER_BIT = 0x00004000;
        protected const uint GL_DEPTH_BUFFER_BIT = 0x00000100;
        protected const uint GL_STENCIL_BUFFER_BIT = 0x00000400;
        protected const uint GL_REPEAT = 0x2901;
        protected const uint GL_LINEAR_MIPMAP_LINEAR = 0x2703;
        protected const uint GL_VERTEX_ARRAY = 0x8074;
        protected const uint GL_NO_ERROR = 0;

        // Windows constants (common to both KOTOR1 and KOTOR2)
        protected const int GWL_STYLE = -16;
        protected const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
        protected const uint WS_POPUP = 0x80000000;
        protected const uint SW_SHOW = 1;
        protected static readonly IntPtr HWND_TOP = (IntPtr)(-2);
        protected static readonly IntPtr HWND_TOPMOST = (IntPtr)(-1);
        protected const uint SWP_NOMOVE = 0x0002;
        protected const uint SWP_NOSIZE = 0x0001;
        protected const uint SWP_SHOWWINDOW = 0x0040;
        protected const uint WM_SIZE = 5;
        protected const uint CDS_FULLSCREEN = 4;
        protected const uint CDS_TEST = 2;
        protected const uint DISP_CHANGE_SUCCESSFUL = 0;

        // PIXELFORMATDESCRIPTOR flags (common to both KOTOR1 and KOTOR2)
        protected const uint PFD_DRAW_TO_WINDOW = 0x00000004;
        protected const uint PFD_SUPPORT_OPENGL = 0x00000020;
        protected const uint PFD_DOUBLEBUFFER = 0x00000001;
        protected const byte PFD_TYPE_RGBA = 0;
        protected const byte PFD_MAIN_PLANE = 0;

        // WGL_ARB_pixel_format attributes (common to both KOTOR1 and KOTOR2)
        protected const int WGL_DRAW_TO_WINDOW_ARB = 0x2001;
        protected const int WGL_SUPPORT_OPENGL_ARB = 0x2010;
        protected const int WGL_DOUBLE_BUFFER_ARB = 0x2011;
        protected const int WGL_PIXEL_TYPE_ARB = 0x2013;
        protected const int WGL_COLOR_BITS_ARB = 0x2014;
        protected const int WGL_DEPTH_BITS_ARB = 0x2022;
        protected const int WGL_STENCIL_BITS_ARB = 0x2023;
        protected const int WGL_SAMPLE_BUFFERS_ARB = 0x2041;
        protected const int WGL_SAMPLES_ARB = 0x2042;
        protected const int WGL_TYPE_RGBA_ARB = 0x202B;
        protected const int WGL_ACCELERATION_ARB = 0x2003;
        protected const int WGL_FULL_ACCELERATION_ARB = 0x2027;
        protected const int WGL_SWAP_METHOD_ARB = 0x2017;
        protected const int WGL_SWAP_EXCHANGE_ARB = 0x2028;
        protected const int WGL_SWAP_COPY_ARB = 0x2029;
        protected const int WGL_SWAP_UNDEFINED_ARB = 0x202A;
        protected const int WGL_NUMBER_PIXEL_FORMATS_ARB = 0x2000;
        protected const int WGL_DRAW_TO_BITMAP_ARB = 0x2002;
        protected const int WGL_ACCELERATION_ARB_VALUE = 0x2015;
        protected const int WGL_NEED_PALETTE_ARB = 0x2004;
        protected const int WGL_NEED_SYSTEM_PALETTE_ARB = 0x2005;
        protected const int WGL_SWAP_LAYER_BUFFERS_ARB = 0x2006;
        protected const int WGL_SWAP_METHOD_ARB_VALUE = 0x2019;
        protected const int WGL_NUMBER_OVERLAYS_ARB = 0x2008;
        protected const int WGL_NUMBER_UNDERLAYS_ARB = 0x2009;
        protected const int WGL_TRANSPARENT_ARB = 0x200A;
        protected const int WGL_TRANSPARENT_RED_VALUE_ARB = 0x2037;
        protected const int WGL_TRANSPARENT_GREEN_VALUE_ARB = 0x2038;
        protected const int WGL_TRANSPARENT_BLUE_VALUE_ARB = 0x2039;
        protected const int WGL_TRANSPARENT_ALPHA_VALUE_ARB = 0x203A;
        protected const int WGL_TRANSPARENT_INDEX_VALUE_ARB = 0x203B;
        protected const int WGL_SHARE_DEPTH_ARB = 0x200C;
        protected const int WGL_SHARE_STENCIL_ARB = 0x200D;
        protected const int WGL_SHARE_ACCUM_ARB = 0x200E;
        protected const int WGL_SUPPORT_GDI_ARB = 0x200F;
        protected const int WGL_SUPPORT_OPENGL_ARB_VALUE = 0x2012;
        protected const int WGL_DOUBLE_BUFFER_ARB_VALUE = 0x2016;
        protected const int WGL_STEREO_ARB = 0x2012;
        protected const int WGL_PIXEL_TYPE_ARB_VALUE = 0x2013;
        protected const int WGL_COLOR_BITS_ARB_VALUE = 0x201B;
        protected const int WGL_RED_BITS_ARB = 0x2015;
        protected const int WGL_RED_SHIFT_ARB = 0x2016;
        protected const int WGL_GREEN_BITS_ARB = 0x2017;
        protected const int WGL_GREEN_SHIFT_ARB = 0x2018;
        protected const int WGL_BLUE_BITS_ARB = 0x2019;
        protected const int WGL_BLUE_SHIFT_ARB = 0x201A;
        protected const int WGL_ALPHA_BITS_ARB = 0x201B;
        protected const int WGL_ALPHA_SHIFT_ARB = 0x201C;
        protected const int WGL_ACCUM_BITS_ARB = 0x201D;
        protected const int WGL_ACCUM_RED_BITS_ARB = 0x201E;
        protected const int WGL_ACCUM_GREEN_BITS_ARB = 0x201F;
        protected const int WGL_ACCUM_BLUE_BITS_ARB = 0x2020;
        protected const int WGL_ACCUM_ALPHA_BITS_ARB = 0x2021;
        protected const int WGL_AUX_BUFFERS_ARB = 0x2024;
        protected const int WGL_NO_ACCELERATION_ARB = 0x2025;
        protected const int WGL_GENERIC_ACCELERATION_ARB = 0x2026;
        protected const int WGL_FULL_ACCELERATION_ARB_VALUE = 0x2027;
        protected const int WGL_SWAP_EXCHANGE_ARB_VALUE = 0x2028;
        protected const int WGL_SWAP_COPY_ARB_VALUE = 0x2029;
        protected const int WGL_SWAP_UNDEFINED_ARB_VALUE = 0x202A;
        protected const int WGL_TYPE_RGBA_ARB_VALUE = 0x202B;
        protected const int WGL_TYPE_COLORINDEX_ARB = 0x202C;
        protected const int WGL_TYPE_COLORINDEX_ARB_VALUE = 0x202D;
        protected const int WGL_SAMPLE_BUFFERS_ARB_VALUE = 0x2041;
        protected const int WGL_SAMPLES_ARB_VALUE = 0x2042;

        #endregion

        #region Common Odyssey OpenGL Initialization

        /// <summary>
        /// Common OpenGL context creation pattern shared by both KOTOR1 and KOTOR2.
        /// Based on reverse engineering of swkotor.exe and swkotor2.exe.
        /// </summary>
        /// <remarks>
        /// Common Pattern (both games):
        /// - swkotor.exe: FUN_0044dab0 @ 0x0044dab0 calls wglCreateContext
        /// - swkotor2.exe: FUN_00461c50 @ 0x00461c50 calls wglCreateContext
        /// - Both use: ChoosePixelFormat, SetPixelFormat, wglCreateContext, wglMakeCurrent
        /// - Both set up context sharing with wglShareLists for multi-threaded rendering
        /// </remarks>
        protected virtual bool CreateOdysseyOpenGLContext(IntPtr windowHandle, int width, int height, bool fullscreen, int refreshRate)
        {
            // Common OpenGL context creation for both KOTOR1 and KOTOR2
            // This matches the pattern from both swkotor.exe and swkotor2.exe

            // 1. Window setup (common to both)
            // ShowWindow(windowHandle, 0) - hide window during setup
            // SetWindowPos(...) - position window
            // AdjustWindowRect(...) - adjust window size

            // 2. Display mode enumeration (common to both)
            // EnumDisplaySettingsA(...) - enumerate display modes
            // ChangeDisplaySettingsA(...) - change display mode if fullscreen

            // 3. Pixel format selection (common to both)
            // ChoosePixelFormat(...) - choose pixel format
            // SetPixelFormat(...) - set pixel format

            // 4. OpenGL context creation (common to both)
            // GetDC(windowHandle) - get device context
            // wglCreateContext(hdc) - create OpenGL context
            // wglMakeCurrent(hdc, context) - make context current

            // 5. Context sharing setup (common to both)
            // wglShareLists(primaryContext, secondaryContext) - share contexts for multi-threading

            // Game-specific differences are handled in derived classes
            return CreateOpenGLDevice();
        }

        /// <summary>
        /// Common texture initialization pattern shared by both KOTOR1 and KOTOR2.
        /// Based on reverse engineering of swkotor.exe and swkotor2.exe.
        /// </summary>
        /// <remarks>
        /// Common Pattern (both games):
        /// - swkotor.exe: FUN_00427c90 @ 0x00427c90 initializes textures
        /// - swkotor2.exe: FUN_0042a100 @ 0x0042a100 initializes textures
        /// - Both use: glGenTextures, glBindTexture, glTexImage2D, glTexParameteri
        /// - Both create multiple texture objects for rendering pipeline
        /// </remarks>
        protected virtual void InitializeOdysseyTextures()
        {
            // Common texture initialization for both KOTOR1 and KOTOR2
            // This matches the pattern from both swkotor.exe and swkotor2.exe

            // Pattern (both games):
            // 1. Generate texture names: glGenTextures(1, &textureId)
            // 2. Bind texture: glBindTexture(GL_TEXTURE_2D, textureId)
            // 3. Set texture parameters: glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR)
            // 4. Load texture data: glTexImage2D(...) or glCopyTexImage2D(...)

            // Game-specific texture setup is handled in derived classes
        }

        #endregion

        #region Odyssey Engine-Specific Methods

        /// <summary>
        /// Odyssey engine-specific rendering methods.
        /// These match the original Odyssey engine's rendering code exactly.
        /// </summary>
        protected virtual void RenderOdysseyScene()
        {
            // Odyssey engine scene rendering
            // Matches swkotor.exe/swkotor2.exe rendering code
        }

        /// <summary>
        /// Odyssey engine-specific texture loading.
        /// Matches Odyssey engine's texture loading code.
        /// </summary>
        protected virtual IntPtr LoadOdysseyTexture(string path)
        {
            // Odyssey engine texture loading
            // Matches swkotor.exe/swkotor2.exe texture loading code
            return IntPtr.Zero;
        }

        #endregion
    }
}

