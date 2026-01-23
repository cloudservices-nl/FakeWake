using System;
using System.Threading;
using FakeWake.Native;

namespace FakeWake.Services
{
    public class KeepAliveService
    {
        public void PreventSleep()
        {
            Win32Api.SetThreadExecutionState(
                Win32Api.ES_CONTINUOUS |
                Win32Api.ES_SYSTEM_REQUIRED |
                Win32Api.ES_DISPLAY_REQUIRED
            );
        }

        public void AllowSleep()
        {
            Win32Api.SetThreadExecutionState(Win32Api.ES_CONTINUOUS);
        }

        public void SimulateActivity()
        {
            // Toggle Scroll Lock twice (invisible, no output)
            Win32Api.keybd_event(Win32Api.VK_SCROLL, 0x45, 0, UIntPtr.Zero);
            Win32Api.keybd_event(Win32Api.VK_SCROLL, 0x45, Win32Api.KEYEVENTF_KEYUP, UIntPtr.Zero);
            Thread.Sleep(50);
            Win32Api.keybd_event(Win32Api.VK_SCROLL, 0x45, 0, UIntPtr.Zero);
            Win32Api.keybd_event(Win32Api.VK_SCROLL, 0x45, Win32Api.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
