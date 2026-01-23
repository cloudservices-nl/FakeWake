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
        private int statsClickCount = 0;
        private DateTime lastStatsClick = DateTime.MinValue;
        private bool keepMenuOpen = false;
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
                ForeColor = Color.DarkGreen
            };
            statsItem.MouseDown += OnStatsClick;
            contextMenu.Items.Add(statsItem);

            // Prevent menu from closing when clicking stats item
            contextMenu.Closing += (s, e) =>
            {
                if (keepMenuOpen && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
                {
                    e.Cancel = true;
                }
            };

            contextMenu.Items.Add(new ToolStripSeparator());

            var toggleItem = new ToolStripMenuItem("Pause", null, ToggleActive);
            contextMenu.Items.Add(toggleItem);

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
            // Create a bed with coffee cup - represents "faking" being awake
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Bright green background for visibility
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(50, 205, 50))) // Lime green
                using (SolidBrush bedBrush = new SolidBrush(Color.White))
                using (Pen bedPen = new Pen(Color.White, 2f))
                using (SolidBrush coffeeBrush = new SolidBrush(Color.White))
                {
                    // Green circle background
                    g.FillEllipse(bgBrush, 1, 1, 30, 30);

                    // Bed frame - simple rectangle
                    g.FillRectangle(bedBrush, 4, 18, 18, 8);

                    // Headboard
                    g.FillRectangle(bedBrush, 4, 14, 4, 12);

                    // Pillow
                    g.FillEllipse(bedBrush, 6, 15, 6, 4);

                    // Coffee cup (top right) - shows "awake"
                    g.FillRectangle(coffeeBrush, 22, 16, 6, 8);
                    // Cup handle
                    g.DrawArc(bedPen, 26, 17, 4, 5, -90, 180);
                    // Steam lines
                    g.DrawLine(bedPen, 24, 14, 24, 11);
                    g.DrawLine(bedPen, 26, 13, 26, 10);
                }
            }

            IntPtr hIcon = bitmap.GetHicon();
            Icon icon = Icon.FromHandle(hIcon);
            return icon;
        }

        private Icon CreateInactiveIcon()
        {
            // Create a bed with ZZZ - represents paused/sleeping
            Bitmap bitmap = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // Gray background for inactive
                using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(128, 128, 128))) // Gray
                using (SolidBrush bedBrush = new SolidBrush(Color.White))
                using (Font zzFont = new Font("Arial", 7, FontStyle.Bold))
                using (SolidBrush zzBrush = new SolidBrush(Color.White))
                {
                    // Gray circle background
                    g.FillEllipse(bgBrush, 1, 1, 30, 30);

                    // Bed frame - simple rectangle
                    g.FillRectangle(bedBrush, 4, 18, 18, 8);

                    // Headboard
                    g.FillRectangle(bedBrush, 4, 14, 4, 12);

                    // Pillow
                    g.FillEllipse(bedBrush, 6, 15, 6, 4);

                    // ZZZ for sleeping
                    g.DrawString("z", zzFont, zzBrush, 20, 14);
                    g.DrawString("z", zzFont, zzBrush, 23, 8);
                    g.DrawString("z", zzFont, zzBrush, 25, 2);
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

        private void KeepAlive(object sender, EventArgs e)
        {
            if (!isActive) return;

            // Simulate activity by toggling Scroll Lock twice (invisible, no output)
            keybd_event(VK_SCROLL, 0x45, 0, UIntPtr.Zero); // Press
            keybd_event(VK_SCROLL, 0x45, KEYEVENTF_KEYUP, UIntPtr.Zero); // Release
            System.Threading.Thread.Sleep(50);
            keybd_event(VK_SCROLL, 0x45, 0, UIntPtr.Zero); // Press again
            keybd_event(VK_SCROLL, 0x45, KEYEVENTF_KEYUP, UIntPtr.Zero); // Release again

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
                UpdateContextMenu("Status: Active", "Pause");
                UpdateStats(null, EventArgs.Empty);

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
                    trayIcon.Text = "FakeWake - Auto-paused";
                    UpdateContextMenu("Status: Auto-paused", "Pause");
                    // Don't show balloon tip for auto-pause to avoid annoying the user
                }
                else
                {
                    trayIcon.Text = "FakeWake - Status: Paused";
                    UpdateContextMenu("Status: Paused", "Resume");
                }
            }
        }

        private void UpdateContextMenu(string statusText, string toggleText)
        {
            if (trayIcon.ContextMenuStrip != null)
            {
                trayIcon.ContextMenuStrip.Items[0].Text = statusText;
                trayIcon.ContextMenuStrip.Items[3].Text = toggleText;
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

        private void CheckUserActivity(object sender, EventArgs e)
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

        private void UpdateStats(object sender, EventArgs e)
        {
            if (isActive)
            {
                var currentSessionTime = DateTime.Now - sessionStartTime;
                var totalTime = totalActiveTime + currentSessionTime;

                // Update tooltip
                trayIcon.Text = $"FakeWake - {FormatTimeSpan(totalTime)}";

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
            return $"🏆 {GetFunMessage(totalTime)}\n⏱️ Time: {FormatTimeSpan(totalTime)}";
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

        private void OnStatsClick(object sender, MouseEventArgs e)
        {
            // Keep menu open by default
            keepMenuOpen = true;

            // Reset click count if more than 2 seconds since last click
            if ((DateTime.Now - lastStatsClick).TotalSeconds > 2)
            {
                statsClickCount = 0;
            }

            lastStatsClick = DateTime.Now;
            statsClickCount++;

            if (statsClickCount >= 5)
            {
                statsClickCount = 0;
                keepMenuOpen = false; // Allow menu to close for dialog
                trayIcon.ContextMenuStrip.Close();
                ResetStats();
            }
        }

        private void ResetStats()
        {
            var totalTime = totalActiveTime + (DateTime.Now - sessionStartTime);
            var funnyMessages = new[]
            {
                "Whoa there! You've mass-clicked your way to the nuclear option!",
                "So you want to pretend none of this ever happened?",
                "Erasing evidence of your 'productivity', are we?",
                "Starting fresh? Bold move, cotton.",
                "You sure? Those fake hours won't fake themselves again!"
            };
            var random = new Random();
            var message = funnyMessages[random.Next(funnyMessages.Length)];

            var result = MessageBox.Show(
                $"{message}\n\nYou're about to reset {FormatTimeSpan(totalTime)} of tracked time.\n\nNo takebacks!",
                "Reset Counter?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                totalActiveTime = TimeSpan.Zero;
                sessionStartTime = DateTime.Now;
                SaveStats();
                UpdateStats(null, EventArgs.Empty);
            }
        }

        private void ToggleActive(object sender, EventArgs e)
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

        private void ShowAbout(object sender, EventArgs e)
        {
            MessageBox.Show(
                "FakeWake v1.4\n\n" +
                "Keeps your Windows session active and prevents Teams/Slack from marking you as Away.\n\n" +
                "Features:\n" +
                "• Smart auto-pause when you're working\n" +
                "• Auto-resume after 2 minutes idle\n" +
                "• Activity time counter\n" +
                "• Prevents system sleep\n" +
                "• Silent operation\n\n" +
                "Double-click the tray icon to pause/resume.",
                "About FakeWake",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void Exit(object sender, EventArgs e)
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
