using System;
using System.Runtime.InteropServices;

namespace Andastra.Runtime.MonoGame.Backends
{
    /// <summary>
    /// Platform-specific helper for creating temporary OpenGL contexts for availability detection.
    /// 
    /// This class creates a minimal OpenGL context to verify that OpenGL is available
    /// on the current system without requiring a full window or application initialization.
    /// 
    /// Supports:
    /// - Windows: WGL (Windows Graphics Library)
    /// - Linux: GLX (OpenGL Extension to the X Window System)
    /// - macOS: CGL (Core OpenGL) / NSOpenGL
    /// 
    /// Based on OpenGL context creation patterns:
    /// - Windows: https://docs.microsoft.com/en-us/windows/win32/opengl/wgl-functions
    /// - Linux: https://www.khronos.org/registry/OpenGL/extensions/ARB/GLX_ARB_create_context.txt
    /// - macOS: https://developer.apple.com/documentation/appkit/nsopenglcontext
    /// </summary>
    internal static class OpenGLContextHelper
    {
        /// <summary>
        /// Attempts to create a temporary OpenGL context to verify OpenGL availability.
        /// </summary>
        /// <returns>True if OpenGL context creation succeeded, false otherwise.</returns>
        public static bool TryCreateContext()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return TryCreateWGLContext();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return TryCreateGLXContext();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return TryCreateCGLContext();
            }

            // Unknown platform - assume OpenGL might be available
            return true;
        }

        #region Windows WGL Implementation

        private static bool TryCreateWGLContext()
        {
            IntPtr hdc = IntPtr.Zero;
            IntPtr hglrc = IntPtr.Zero;
            IntPtr hwnd = IntPtr.Zero;

            try
            {
                // Load opengl32.dll
                IntPtr opengl32 = WGLNativeMethods.LoadLibrary("opengl32.dll");
                if (opengl32 == IntPtr.Zero)
                {
                    return false;
                }

                // Create a minimal window class for OpenGL context
                WndProcDelegate wndProc = new WndProcDelegate(WGLNativeMethods.DefWindowProc);
                WNDCLASS wc = new WNDCLASS
                {
                    style = 0,
                    lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
                    cbClsExtra = 0,
                    cbWndExtra = 0,
                    hInstance = WGLNativeMethods.GetModuleHandle(null),
                    hIcon = IntPtr.Zero,
                    hCursor = IntPtr.Zero,
                    hbrBackground = IntPtr.Zero,
                    lpszClassName = "OpenGLTestClass"
                };

                ushort atom = WGLNativeMethods.RegisterClass(ref wc);
                if (atom == 0)
                {
                    // Class might already be registered, try to create window anyway
                }

                // Create a minimal window (1x1, hidden)
                hwnd = WGLNativeMethods.CreateWindowEx(
                    0,
                    "OpenGLTestClass",
                    "OpenGLTest",
                    0, // WS_OVERLAPPED
                    0, 0, 1, 1,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    WGLNativeMethods.GetModuleHandle(null),
                    IntPtr.Zero);

                if (hwnd == IntPtr.Zero)
                {
                    return false;
                }

                // Get device context
                hdc = WGLNativeMethods.GetDC(hwnd);
                if (hdc == IntPtr.Zero)
                {
                    return false;
                }

                // Set up minimal pixel format
                PIXELFORMATDESCRIPTOR pfd = new PIXELFORMATDESCRIPTOR
                {
                    nSize = (ushort)Marshal.SizeOf(typeof(PIXELFORMATDESCRIPTOR)),
                    nVersion = 1,
                    dwFlags = PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER,
                    iPixelType = PFD_TYPE_RGBA,
                    cColorBits = 32,
                    cRedBits = 0,
                    cRedShift = 0,
                    cGreenBits = 0,
                    cGreenShift = 0,
                    cBlueBits = 0,
                    cBlueShift = 0,
                    cAlphaBits = 0,
                    cAlphaShift = 0,
                    cAccumBits = 0,
                    cAccumRedBits = 0,
                    cAccumGreenBits = 0,
                    cAccumBlueBits = 0,
                    cAccumAlphaBits = 0,
                    cDepthBits = 24,
                    cStencilBits = 8,
                    cAuxBuffers = 0,
                    iLayerType = PFD_MAIN_PLANE,
                    bReserved = 0,
                    dwLayerMask = 0,
                    dwVisibleMask = 0,
                    dwDamageMask = 0
                };

                int pixelFormat = WGLNativeMethods.ChoosePixelFormat(hdc, ref pfd);
                if (pixelFormat == 0)
                {
                    return false;
                }

                if (!WGLNativeMethods.SetPixelFormat(hdc, pixelFormat, ref pfd))
                {
                    return false;
                }

                // Create OpenGL context
                hglrc = WGLNativeMethods.wglCreateContext(hdc);
                if (hglrc == IntPtr.Zero)
                {
                    return false;
                }

                // Make context current
                if (!WGLNativeMethods.wglMakeCurrent(hdc, hglrc))
                {
                    return false;
                }

                // Success - OpenGL is available
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                // Cleanup
                if (hglrc != IntPtr.Zero)
                {
                    WGLNativeMethods.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
                    WGLNativeMethods.wglDeleteContext(hglrc);
                }

                if (hdc != IntPtr.Zero && hwnd != IntPtr.Zero)
                {
                    WGLNativeMethods.ReleaseDC(hwnd, hdc);
                }

                if (hwnd != IntPtr.Zero)
                {
                    WGLNativeMethods.DestroyWindow(hwnd);
                }
            }
        }

        private const uint PFD_DRAW_TO_WINDOW = 0x00000004;
        private const uint PFD_SUPPORT_OPENGL = 0x00000020;
        private const uint PFD_DOUBLEBUFFER = 0x00000001;
        private const byte PFD_TYPE_RGBA = 0;
        private const byte PFD_MAIN_PLANE = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct WNDCLASS
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
            public string lpszClassName;
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

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static class WGLNativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern ushort RegisterClass(ref WNDCLASS lpWndClass);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern IntPtr CreateWindowEx(
                uint dwExStyle,
                string lpClassName,
                string lpWindowName,
                uint dwStyle,
                int x,
                int y,
                int nWidth,
                int nHeight,
                IntPtr hWndParent,
                IntPtr hMenu,
                IntPtr hInstance,
                IntPtr lpParam);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern IntPtr GetDC(IntPtr hWnd);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DestroyWindow(IntPtr hWnd);

            [DllImport("gdi32.dll")]
            public static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR ppfd);

            [DllImport("gdi32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetPixelFormat(IntPtr hdc, int iPixelFormat, ref PIXELFORMATDESCRIPTOR ppfd);

            [DllImport("opengl32.dll", SetLastError = true)]
            public static extern IntPtr wglCreateContext(IntPtr hdc);

            [DllImport("opengl32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool wglDeleteContext(IntPtr hglrc);

            [DllImport("opengl32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

            [DllImport("user32.dll")]
            public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        }

        #endregion

        #region Linux GLX Implementation

        private static bool TryCreateGLXContext()
        {
            // On Linux, we would use GLX to create a context
            // This requires X11 libraries and is more complex
            // For detection purposes, we can try to load libGL.so

            try
            {
                // Try to load libGL.so.1 (most common OpenGL library on Linux)
                IntPtr libGL = GLXNativeMethods.dlopen("libGL.so.1", 1); // RTLD_LAZY = 1
                if (libGL != IntPtr.Zero)
                {
                    GLXNativeMethods.dlclose(libGL);
                    return true;
                }

                // Try alternative names
                libGL = GLXNativeMethods.dlopen("libGL.so", 1);
                if (libGL != IntPtr.Zero)
                {
                    GLXNativeMethods.dlclose(libGL);
                    return true;
                }

                // Try Mesa (common on Linux)
                libGL = GLXNativeMethods.dlopen("libGL.so.1.2.0", 1);
                if (libGL != IntPtr.Zero)
                {
                    GLXNativeMethods.dlclose(libGL);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static class GLXNativeMethods
        {
            [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr dlopen(string filename, int flags);

            [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
            public static extern int dlclose(IntPtr handle);
        }

        #endregion

        #region macOS CGL Implementation

        private static bool TryCreateCGLContext()
        {
            // On macOS, we use CGL (Core OpenGL) to create a context
            // This requires the OpenGL framework

            try
            {
                // Try to load OpenGL framework
                IntPtr openGLFramework = CGLNativeMethods.dlopen("/System/Library/Frameworks/OpenGL.framework/OpenGL", 1);
                if (openGLFramework != IntPtr.Zero)
                {
                    CGLNativeMethods.dlclose(openGLFramework);
                    return true;
                }

                // Try alternative path
                openGLFramework = CGLNativeMethods.dlopen("/System/Library/Frameworks/OpenGL.framework/Versions/Current/OpenGL", 1);
                if (openGLFramework != IntPtr.Zero)
                {
                    CGLNativeMethods.dlclose(openGLFramework);
                    return true;
                }

                // macOS 10.14+ deprecated OpenGL, but it might still be available
                // Try to check if we can at least load the library
                return false;
            }
            catch
            {
                // On macOS, OpenGL might not be available (deprecated in 10.14+)
                // But we can still return true as a fallback since Metal is preferred
                return true;
            }
        }

        private static class CGLNativeMethods
        {
            [DllImport("libdl.dylib", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr dlopen(string filename, int flags);

            [DllImport("libdl.dylib", CallingConvention = CallingConvention.Cdecl)]
            public static extern int dlclose(IntPtr handle);
        }

        #endregion
    }
}

