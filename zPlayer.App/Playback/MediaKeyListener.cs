using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace zPlayer.App.Playback
{
    public sealed class MediaKeyListener : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private const int VK_MEDIA_NEXT_TRACK = 0xB0;
        private const int VK_MEDIA_PREV_TRACK = 0xB1;
        private const int VK_MEDIA_PLAY_PAUSE = 0xB3;

        private readonly HookProc hookCallback;
        private IntPtr hookHandle;

        public event Action? PlayPausePressed;
        public event Action? NextPressed;
        public event Action? PreviousPressed;

        public MediaKeyListener() {
            hookCallback = KeyboardHook;
            hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, hookCallback, IntPtr.Zero, 0);

            if (hookHandle == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        private IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)) {
                int key = Marshal.ReadInt32(lParam);
                switch (key) {
                    case VK_MEDIA_PLAY_PAUSE:
                        Application.Current.Dispatcher.BeginInvoke(() => PlayPausePressed?.Invoke());
                        break;
                    case VK_MEDIA_NEXT_TRACK:
                        Application.Current.Dispatcher.BeginInvoke(() => NextPressed?.Invoke());
                        break;
                    case VK_MEDIA_PREV_TRACK:
                        Application.Current.Dispatcher.BeginInvoke(() => PreviousPressed?.Invoke());
                        break;
                }
            }
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        public void Dispose() {
            if (hookHandle == IntPtr.Zero) return;
            UnhookWindowsHookEx(hookHandle);
            hookHandle = IntPtr.Zero;
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn,  IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hHook);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    }
}
