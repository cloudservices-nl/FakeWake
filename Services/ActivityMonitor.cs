using System;
using FakeWake.Native;

namespace FakeWake.Services
{
    public class ActivityMonitor
    {
        private const uint ActiveThresholdSeconds = 5;
        private const uint IdleThresholdSeconds = 120; // 2 minutes

        public event EventHandler UserBecameActive;
        public event EventHandler UserBecameIdle;

        public uint IdleTimeSeconds => Win32Api.GetIdleTimeSeconds();

        public bool IsUserActive => IdleTimeSeconds < ActiveThresholdSeconds;

        public bool IsUserIdle => IdleTimeSeconds >= IdleThresholdSeconds;

        public void Check()
        {
            if (IsUserActive)
            {
                UserBecameActive?.Invoke(this, EventArgs.Empty);
            }
            else if (IsUserIdle)
            {
                UserBecameIdle?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
