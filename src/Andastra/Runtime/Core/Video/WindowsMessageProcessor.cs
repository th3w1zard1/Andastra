using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Andastra.Runtime.Core.Video
{
    /// <summary>
    /// Windows message processing for movie playback.
    /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 35-40 (PeekMessage, GetMessage, TranslateMessage, DispatchMessage)
    /// </summary>
    internal static class WindowsMessageProcessor
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public POINT pt;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMessage(out MSG msg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool TranslateMessage(ref MSG msg);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr DispatchMessage(ref MSG msg);

        public const uint PM_NOREMOVE = 0x0000;
        public const uint PM_REMOVE = 0x0001;
        public const uint PM_NOYIELD = 0x0002;
        public const uint WM_QUIT = 0x0012;
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;
        public const uint WM_LBUTTONDOWN = 0x0201;
        public const uint WM_LBUTTONUP = 0x0202;
        public const uint WM_RBUTTONDOWN = 0x0204;
        public const uint WM_RBUTTONUP = 0x0205;
        public const uint WM_MBUTTONDOWN = 0x0207;
        public const uint WM_MBUTTONUP = 0x0208;

        /// <summary>
        /// Processes Windows messages during movie playback.
        /// Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 35-40
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to check for exit.</param>
        public static void ProcessMessages(CancellationToken cancellationToken)
        {
            // Process all pending messages
            // Based on swkotor.exe: 0x00404c80 @ 0x00404c80 line 35-40
            // PeekMessage -> GetMessage -> TranslateMessage -> DispatchMessage loop
            MSG msg;
            while (PeekMessage(out msg, IntPtr.Zero, 0, 0, PM_REMOVE))
            {
                if (msg.message == WM_QUIT)
                {
                    // Exit requested
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    // Cancellation requested
                    break;
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
    }
}

