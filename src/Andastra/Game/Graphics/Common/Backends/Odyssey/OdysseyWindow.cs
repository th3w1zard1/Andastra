using System;
using System.Runtime.InteropServices;
using Andastra.Runtime.Graphics;

namespace Andastra.Game.Graphics.Common.Backends.Odyssey
{
    /// <summary>
    /// Odyssey engine window implementation.
    /// Wraps native Windows HWND created by Kotor1/Kotor2GraphicsBackend.
    /// </summary>
    /// <remarks>
    /// Odyssey Window:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Original game window: Windows HWND with "Render Window" class
    /// - Located via string references: "Render Window" @ 0x007b5680
    /// - "AllowWindowedMode" @ 0x007c75d0 (windowed mode option)
    /// - Window creation: CreateWindowExA with WS_OVERLAPPEDWINDOW or WS_POPUP
    /// - This implementation: Wraps native window for IWindow interface
    /// </remarks>
    public class OdysseyWindow : IWindow
    {
        private readonly IntPtr _windowHandle;
        private string _title;
        private bool _isMouseVisible;
        private bool _isFullscreen;
        private int _width;
        private int _height;
        
        #region Windows P/Invoke
        
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool SetWindowTextA(IntPtr hWnd, string lpString);
        
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool bShow);
        
        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsA(ref DEVMODEA lpDevMode, uint dwFlags);
        
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsA(IntPtr lpDevMode, uint dwFlags);
        
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
        
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
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
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
        
        private const uint CDS_FULLSCREEN = 4;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020;
        
        #endregion
        
        /// <summary>
        /// Creates a new Odyssey window wrapper.
        /// </summary>
        /// <param name="windowHandle">Native window handle (HWND).</param>
        /// <param name="title">Window title.</param>
        /// <param name="width">Window width.</param>
        /// <param name="height">Window height.</param>
        /// <param name="isFullscreen">Whether the window is fullscreen.</param>
        public OdysseyWindow(IntPtr windowHandle, string title, int width, int height, bool isFullscreen)
        {
            _windowHandle = windowHandle;
            _title = title ?? "KOTOR";
            _width = width;
            _height = height;
            _isFullscreen = isFullscreen;
            _isMouseVisible = true;
        }
        
        /// <summary>
        /// Gets or sets the window title.
        /// Based on swkotor.exe/swkotor2.exe: SetWindowTextA
        /// </summary>
        public string Title
        {
            get { return _title; }
            set
            {
                _title = value ?? string.Empty;
                if (_windowHandle != IntPtr.Zero)
                {
                    SetWindowTextA(_windowHandle, _title);
                }
            }
        }
        
        /// <summary>
        /// Gets or sets whether the mouse cursor is visible.
        /// Based on swkotor.exe/swkotor2.exe: ShowCursor
        /// </summary>
        public bool IsMouseVisible
        {
            get { return _isMouseVisible; }
            set
            {
                if (_isMouseVisible != value)
                {
                    _isMouseVisible = value;
                    ShowCursor(_isMouseVisible);
                }
            }
        }
        
        /// <summary>
        /// Gets or sets whether the window is in fullscreen mode.
        /// Based on swkotor.exe/swkotor2.exe: ChangeDisplaySettingsA
        /// </summary>
        public bool IsFullscreen
        {
            get { return _isFullscreen; }
            set
            {
                if (_isFullscreen != value)
                {
                    _isFullscreen = value;
                    ApplyFullscreenMode();
                }
            }
        }
        
        /// <summary>
        /// Gets or sets the window width.
        /// </summary>
        public int Width
        {
            get { return _width; }
            set
            {
                if (_width != value)
                {
                    _width = value;
                    ApplyWindowSize();
                }
            }
        }
        
        /// <summary>
        /// Gets or sets the window height.
        /// </summary>
        public int Height
        {
            get { return _height; }
            set
            {
                if (_height != value)
                {
                    _height = value;
                    ApplyWindowSize();
                }
            }
        }
        
        /// <summary>
        /// Gets whether the window is active (has focus).
        /// Based on swkotor.exe/swkotor2.exe: GetForegroundWindow comparison
        /// </summary>
        public bool IsActive
        {
            get { return GetForegroundWindow() == _windowHandle; }
        }
        
        /// <summary>
        /// Gets the native window handle (HWND).
        /// </summary>
        public IntPtr NativeHandle => _windowHandle;
        
        /// <summary>
        /// Closes the window.
        /// Based on swkotor.exe/swkotor2.exe: DestroyWindow
        /// </summary>
        public void Close()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                // Restore display mode if fullscreen
                if (_isFullscreen)
                {
                    ChangeDisplaySettingsA(IntPtr.Zero, 0);
                }
                
                DestroyWindow(_windowHandle);
            }
        }
        
        /// <summary>
        /// Refreshes the window dimensions from the actual window.
        /// </summary>
        public void RefreshDimensions()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                if (GetClientRect(_windowHandle, out RECT rect))
                {
                    _width = rect.right - rect.left;
                    _height = rect.bottom - rect.top;
                }
            }
        }
        
        #region Private Methods
        
        private void ApplyFullscreenMode()
        {
            if (_isFullscreen)
            {
                // Change to fullscreen
                DEVMODEA devMode = new DEVMODEA();
                devMode.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODEA));
                devMode.dmPelsWidth = (uint)_width;
                devMode.dmPelsHeight = (uint)_height;
                devMode.dmBitsPerPel = 32;
                devMode.dmFields = 0x00080000 | 0x00100000 | 0x00040000; // DM_PELSWIDTH | DM_PELSHEIGHT | DM_BITSPERPEL
                
                ChangeDisplaySettingsA(ref devMode, CDS_FULLSCREEN);
            }
            else
            {
                // Restore windowed mode
                ChangeDisplaySettingsA(IntPtr.Zero, 0);
            }
        }
        
        private void ApplyWindowSize()
        {
            if (_windowHandle != IntPtr.Zero && !_isFullscreen)
            {
                SetWindowPos(_windowHandle, IntPtr.Zero, 0, 0, _width, _height, 
                    SWP_NOMOVE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
            }
        }
        
        #endregion
    }
}

