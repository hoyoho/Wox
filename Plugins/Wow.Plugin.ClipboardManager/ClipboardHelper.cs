using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Wow.Plugin.ClipboardManager
{
    /// <summary>
    /// Sets clipboard text in a way that works no matter which thread/apartment the
    /// plugin action runs on.
    /// </summary>
    internal static class ClipboardHelper
    {
        private const uint CfUnicodeText = 13;
        private const uint GmemMoveable = 0x0002;
        private const uint GmemZeroinit = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        public static bool SetText(string text)
        {
            if (text == null)
            {
                return false;
            }

            try
            {
                System.Windows.Clipboard.SetText(text);
                return true;
            }
            catch (ExternalException)
            {
                return SetUnicodeText(text);
            }
            catch (Exception)
            {
                return SetUnicodeText(text);
            }
        }

        private static bool SetUnicodeText(string text)
        {
            var chars = (text + "\0").ToCharArray();
            var bytes = (uint)(chars.Length * 2);

            for (var i = 0; i < 10; i++)
            {
                if (!OpenClipboard(IntPtr.Zero))
                {
                    Thread.Sleep(30);
                    continue;
                }

                try
                {
                    EmptyClipboard();

                    var hGlobal = GlobalAlloc(GmemMoveable | GmemZeroinit, new UIntPtr(bytes));
                    if (hGlobal == IntPtr.Zero)
                    {
                        return false;
                    }

                    var target = GlobalLock(hGlobal);
                    if (target == IntPtr.Zero)
                    {
                        GlobalFree(hGlobal);
                        return false;
                    }

                    Marshal.Copy(chars, 0, target, chars.Length);
                    GlobalUnlock(hGlobal);

                    if (SetClipboardData(CfUnicodeText, hGlobal) == IntPtr.Zero)
                    {
                        GlobalFree(hGlobal);
                        return false;
                    }

                    return true;
                }
                finally
                {
                    CloseClipboard();
                }
            }

            return false;
        }
    }
}
