using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FakeWake
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FakeWakeApplication());
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    public class FakeWakeApplication : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private Icon activeIcon;
        private Icon inactiveIcon;
        private Timer keepAliveTimer;
        private Timer statsTimer;
        private Timer activityCheckTimer;
        private bool isActive = true;
        private bool isAutoPaused = false;
        private bool wasManuallyPaused = false;

        // Activity tracking
        private TimeSpan totalActiveTime;
        private DateTime sessionStartTime;
        private readonly string statsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FakeWake",
            "stats.txt"
        );

        private const int AUTO_RESUME_IDLE_SECONDS = 120; // 2 minutes

        // Win32 API imports
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        private const byte VK_SCROLL = 0x91; // Scroll Lock key
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public FakeWakeApplication()
        {
            LoadStats();
            InitializeTrayIcon();
            InitializeTimer();
            InitializeStatsTimer();
            InitializeActivityCheckTimer();
            SetActive(true);
        }

        private void InitializeTrayIcon()
        {
            // Create both active and inactive icons
            activeIcon = CreateActiveIcon();
            inactiveIcon = CreateInactiveIcon();

            trayIcon = new NotifyIcon()
            {
                Icon = activeIcon,
                Visible = true,
                Text = "FakeWake - Keeping you active!"
            };

            // Create context menu
            var contextMenu = new ContextMenuStrip();

            var statusItem = new ToolStripMenuItem("Status: Active")
            {
                Enabled = false,
                Font = new Font(contextMenu.Font, FontStyle.Bold)
            };
            contextMenu.Items.Add(statusItem);

            var statsItem = new ToolStripMenuItem(GetStatsText())
            {
                Enabled = false,
                ForeColor = Color.DarkGreen
            };
            contextMenu.Items.Add(statsItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var toggleItem = new ToolStripMenuItem("Pause", null, ToggleActive);
            contextMenu.Items.Add(toggleItem);

            var resetStatsItem = new ToolStripMenuItem("Reset Counter", null, ResetStats);
            contextMenu.Items.Add(resetStatsItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var aboutItem = new ToolStripMenuItem("About", null, ShowAbout);
            contextMenu.Items.Add(aboutItem);

            var exitItem = new ToolStripMenuItem("Exit", null, Exit);
            contextMenu.Items.Add(exitItem);

            trayIcon.ContextMenuStrip = contextMenu;

            // Double-click to toggle
            trayIcon.DoubleClick += (s, e) => ToggleActive(s, e);
        }

        private Icon CreateActiveIcon()
        {
            // Create a winking eye icon - represents "faking" being awake
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Draw the eye outline (circle)
                using (SolidBrush eyeBrush = new SolidBrush(Color.White))
                using (Pen outlinePen = new Pen(Color.Black, 2))
                using (SolidBrush irisBrush = new SolidBrush(Color.FromArgb(70, 130, 180))) // Steel blue
                using (SolidBrush pupilBrush = new SolidBrush(Color.Black))
                using (SolidBrush highlightBrush = new SolidBrush(Color.White))
                using (Pen winkPen = new Pen(Color.OrangeRed, 2.5f)) // Orange-red for active wink
                {
                    // Eye background
                    g.FillEllipse(eyeBrush, 6, 8, 20, 20);
                    g.DrawEllipse(outlinePen, 6, 8, 20, 20);

                    // Iris (blue part)
                    g.FillEllipse(irisBrush, 11, 13, 10, 10);

                    // Pupil
                    g.FillEllipse(pupilBrush, 14, 16, 4, 4);

                    // Eye highlight (sparkle)
                    g.FillEllipse(highlightBrush, 12, 14, 3, 3);

                    // Wink arc - playful curve at bottom right
                    g.DrawArc(winkPen, 20, 20, 8, 8, 180, 180);
                }
            }

            IntPtr hIcon = bitmap.GetHicon();
            Icon icon = Icon.FromHandle(hIcon);
            return icon;
        }

        private Icon CreateInactiveIcon()
        {
            // Create a sleeping/closed eye icon - represents paused/inactive
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                using (SolidBrush faceBrush = new SolidBrush(Color.LightGray))
                using (Pen outlinePen = new Pen(Color.DarkGray, 2))
                using (Pen eyelidPen = new Pen(Color.DarkSlateGray, 2.5f))
                {
                    // Face circle (dimmed)
                    g.FillEllipse(faceBrush, 6, 8, 20, 20);
                    g.DrawEllipse(outlinePen, 6, 8, 20, 20);

                    // Closed eye - curved line
                    g.DrawArc(eyelidPen, 10, 15, 12, 6, 0, 180);

                    // Eyelashes (3 small lines)
                    g.DrawLine(eyelidPen, 11, 16, 9, 14);
                    g.DrawLine(eyelidPen, 16, 15, 16, 13);
                    g.DrawLine(eyelidPen, 21, 16, 23, 14);

                    // ZZZ for sleeping
                    using (Font zzFont = new Font("Arial", 6, FontStyle.Bold))
                    using (SolidBrush zzBrush = new SolidBrush(Color.Gray))
                    {
                        g.DrawString("z", zzFont, zzBrush, 22, 6);
                        g.DrawString("z", zzFont, zzBrush, 24, 3);
                    }
                }
            }

            IntPtr hIcon = bitmap.GetHicon();
            Icon icon = Icon.FromHandle(hIcon);
            return icon;
        }

        private void InitializeTimer()
        {
            // Timer to simulate activity every 60 seconds
            keepAliveTimer = new Timer
            {
                Interval = 60000 // 60 seconds
            };
            keepAliveTimer.Tick += KeepAlive;
        }

        private void KeepAlive(object? sender, EventArgs e)
        {
            if (!isActive) return;

            // Simulate activity by toggling Scroll Lock twice (invisible, no output)
            keybd_event(VK_SCROLL, 0x45, 0, UIntPtr.Zero); // Press
            keybd_event(VK_SCROLL, 0x45, KEYEVENTF_KEYUP, UIntPtr.Zero); // Release
            System.Threading.Thread.Sleep(50);
            keybd_event(VK_SCROLL, 0x45, 0, UIntPtr.Zero); // Press again
            keybd_event(VK_SCROLL, 0x45, KEYEVENTF_KEYUP, UIntPtr.Zero); // Release again

            // Show a brief notification every 10 minutes
            if (DateTime.Now.Second < 2)
            {
                trayIcon.ShowBalloonTip(2000, "FakeWake", "Still keeping you active!", ToolTipIcon.Info);
            }
        }

        private void SetActive(bool active, bool isAutoPause = false)
        {
            if (active == isActive) return;

            if (!active)
            {
                // Save stats before pausing
                SaveStats();
                totalActiveTime = totalActiveTime + (DateTime.Now - sessionStartTime);
            }
            else
            {
                // Reset session start time when resuming
                sessionStartTime = DateTime.Now;
            }

            isActive = active;

            if (isActive)
            {
                // Switch to active icon (winking eye)
                trayIcon.Icon = activeIcon;

                // Prevent system sleep and display sleep
                SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);
                keepAliveTimer.Start();
                UpdateContextMenu("Status: Active 😉", "Pause");
                UpdateStats(null, EventArgs.Empty);

                if (!isAutoPause)
                {
                    trayIcon.ShowBalloonTip(2000, "FakeWake", "Now keeping you active!", ToolTipIcon.Info);
                }
                else
                {
                    trayIcon.ShowBalloonTip(2000, "FakeWake", "Auto-resumed - you're idle again", ToolTipIcon.Info);
                }
            }
            else
            {
                // Switch to inactive icon (sleeping eye)
                trayIcon.Icon = inactiveIcon;

                // Allow system to sleep normally
                SetThreadExecutionState(ES_CONTINUOUS);
                keepAliveTimer.Stop();

                if (isAutoPause)
                {
                    trayIcon.Text = "FakeWake - Auto-paused (you're working) 💼";
                    UpdateContextMenu("Status: Auto-paused 💼", "Pause");
                    // Don't show balloon tip for auto-pause to avoid annoying the user
                }
                else
                {
                    trayIcon.Text = "FakeWake - Status: Paused 😴";
                    UpdateContextMenu("Status: Paused 😴", "Resume");
                    trayIcon.ShowBalloonTip(2000, "FakeWake", "Paused - you may go idle now", ToolTipIcon.Warning);
                }
            }
        }

        private void UpdateContextMenu(string statusText, string toggleText)
        {
            if (trayIcon.ContextMenuStrip != null)
            {
                trayIcon.ContextMenuStrip.Items[0].Text = statusText;
                trayIcon.ContextMenuStrip.Items[4].Text = toggleText;
            }
        }

        private void InitializeStatsTimer()
        {
            // Timer to update stats display every 10 seconds
            statsTimer = new Timer
            {
                Interval = 10000 // 10 seconds
            };
            statsTimer.Tick += UpdateStats;
            statsTimer.Start();
        }

        private void InitializeActivityCheckTimer()
        {
            // Timer to check user activity every 5 seconds
            activityCheckTimer = new Timer
            {
                Interval = 5000 // 5 seconds
            };
            activityCheckTimer.Tick += CheckUserActivity;
            activityCheckTimer.Start();
        }

        private uint GetIdleTimeSeconds()
        {
            LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

            if (GetLastInputInfo(ref lastInputInfo))
            {
                uint idleTime = (GetTickCount() - lastInputInfo.dwTime) / 1000;
                return idleTime;
            }

            return 0;
        }

        private void CheckUserActivity(object? sender, EventArgs e)
        {
            uint idleSeconds = GetIdleTimeSeconds();

            // If FakeWake is active (not manually paused)
            if (isActive && !wasManuallyPaused)
            {
                // If user is actively typing/moving mouse (idle < 5 seconds), auto-pause
                if (idleSeconds < 5 && !isAutoPaused)
                {
                    isAutoPaused = true;
                    SetActive(false, isAutoPause: true);
                }
            }
            // If auto-paused and user is idle for 2+ minutes, auto-resume
            else if (isAutoPaused && !wasManuallyPaused)
            {
                if (idleSeconds >= AUTO_RESUME_IDLE_SECONDS)
                {
                    isAutoPaused = false;
                    SetActive(true, isAutoPause: false);
                }
            }
        }

        private void UpdateStats(object? sender, EventArgs e)
        {
            if (isActive)
            {
                var currentSessionTime = DateTime.Now - sessionStartTime;
                var totalTime = totalActiveTime + currentSessionTime;

                // Update tooltip
                trayIcon.Text = $"FakeWake ☕\n{GetFunMessage(totalTime)}\n{FormatTimeSpan(totalTime)}";

                // Update context menu stats item
                if (trayIcon.ContextMenuStrip != null && trayIcon.ContextMenuStrip.Items.Count > 1)
                {
                    trayIcon.ContextMenuStrip.Items[1].Text = GetStatsText();
                }

                // Save stats every minute (6 updates)
                if (DateTime.Now.Second % 60 == 0)
                {
                    SaveStats();
                }
            }
        }

        private string GetStatsText()
        {
            var currentSessionTime = isActive ? DateTime.Now - sessionStartTime : TimeSpan.Zero;
            var totalTime = totalActiveTime + currentSessionTime;
            return $"⏱️ {GetFunMessage(totalTime)}: {FormatTimeSpan(totalTime)}";
        }

        private string GetFunMessage(TimeSpan time)
        {
            double hours = time.TotalHours;

            if (hours < 0.5) return "Rookie numbers";
            if (hours < 1) return "Getting started";
            if (hours < 2) return "Productive vibes";
            if (hours < 4) return "Going strong";
            if (hours < 8) return "Full workday dodged";
            if (hours < 12) return "Dedication level: High";
            if (hours < 24) return "You're a legend";
            if (hours < 48) return "Superhuman detected";
            if (hours < 100) return "Absolute madlad";
            if (hours < 200) return "Coffee addicted";
            if (hours < 500) return "Professional procrastinator";
            if (hours < 1000) return "Time wizard";
            return "Eternal presence achieved";
        }

        private string FormatTimeSpan(TimeSpan time)
        {
            if (time.TotalDays >= 1)
                return $"{(int)time.TotalDays}d {time.Hours}h {time.Minutes}m";
            if (time.TotalHours >= 1)
                return $"{(int)time.TotalHours}h {time.Minutes}m";
            return $"{time.Minutes}m {time.Seconds}s";
        }

        private void LoadStats()
        {
            try
            {
                if (File.Exists(statsFilePath))
                {
                    var content = File.ReadAllText(statsFilePath);
                    if (long.TryParse(content, out long ticks))
                    {
                        totalActiveTime = TimeSpan.FromTicks(ticks);
                    }
                }
            }
            catch
            {
                // If loading fails, start fresh
                totalActiveTime = TimeSpan.Zero;
            }

            sessionStartTime = DateTime.Now;
        }

        private void SaveStats()
        {
            try
            {
                var currentSessionTime = DateTime.Now - sessionStartTime;
                var totalTime = totalActiveTime + currentSessionTime;

                var directory = Path.GetDirectoryName(statsFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(statsFilePath, totalTime.Ticks.ToString());
            }
            catch
            {
                // Silently fail if we can't save stats
            }
        }

        private void ResetStats(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to reset your activity counter?\n\nCurrent time tracked: {FormatTimeSpan(totalActiveTime + (DateTime.Now - sessionStartTime))}\n\nThis cannot be undone!",
                "Reset Counter",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Yes)
            {
                totalActiveTime = TimeSpan.Zero;
                sessionStartTime = DateTime.Now;
                SaveStats();
                UpdateStats(null, EventArgs.Empty);
                trayIcon.ShowBalloonTip(2000, "FakeWake", "Counter reset! Starting fresh ☕", ToolTipIcon.Info);
            }
        }

        private void ToggleActive(object? sender, EventArgs e)
        {
            // When manually toggling, clear auto-pause state
            if (!isActive)
            {
                // Resuming manually
                wasManuallyPaused = false;
                isAutoPaused = false;
                SetActive(true);
            }
            else
            {
                // Pausing manually
                wasManuallyPaused = true;
                isAutoPaused = false;
                SetActive(false);
            }
        }

        private void ShowAbout(object? sender, EventArgs e)
        {
            MessageBox.Show(
                "FakeWake v1.3\n\n" +
                "Keeps your Windows session active and prevents Teams/Slack from marking you as Away.\n\n" +
                "Features:\n" +
                "• Smart auto-pause when you're working\n" +
                "• Auto-resume after 2 minutes idle\n" +
                "• Activity time counter with fun messages\n" +
                "• Dynamic icons (winking eye 😉 / sleeping eye 😴)\n" +
                "• Prevents system sleep\n" +
                "• Silent operation (Scroll Lock toggle)\n\n" +
                "Double-click the tray icon to pause/resume manually.",
                "About FakeWake",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void Exit(object? sender, EventArgs e)
        {
            // Save stats before exiting
            SaveStats();

            // Restore normal power management
            SetThreadExecutionState(ES_CONTINUOUS);

            keepAliveTimer.Stop();
            keepAliveTimer.Dispose();
            statsTimer.Stop();
            statsTimer.Dispose();
            activityCheckTimer.Stop();
            activityCheckTimer.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            Application.Exit();
        }
    }
}
