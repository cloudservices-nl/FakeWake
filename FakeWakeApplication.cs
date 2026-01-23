using System;
using System.Drawing;
using System.Windows.Forms;
using FakeWake.Models;
using FakeWake.Services;
using FakeWake.UI;

namespace FakeWake
{
    public class FakeWakeApplication : ApplicationContext
    {
        private readonly NotifyIcon trayIcon;
        private readonly Icon activeIcon;
        private readonly Icon inactiveIcon;
        private readonly TrayMenuManager menuManager;
        private readonly StatsManager statsManager;
        private readonly ActivityMonitor activityMonitor;
        private readonly KeepAliveService keepAliveService;

        private readonly Timer keepAliveTimer;
        private readonly Timer statsTimer;
        private readonly Timer activityCheckTimer;

        private AppState state = AppState.Active;

        private static readonly string[] FunnyResetMessages =
        {
            "Whoa there! You've mass-clicked your way to the nuclear option!",
            "So you want to pretend none of this ever happened?",
            "Erasing evidence of your 'productivity', are we?",
            "Starting fresh? Bold move, cotton.",
            "You sure? Those fake hours won't fake themselves again!"
        };

        public FakeWakeApplication()
        {
            // Initialize services
            statsManager = new StatsManager();
            activityMonitor = new ActivityMonitor();
            keepAliveService = new KeepAliveService();

            // Initialize icons
            activeIcon = IconFactory.CreateActiveIcon();
            inactiveIcon = IconFactory.CreateInactiveIcon();

            // Initialize menu
            menuManager = new TrayMenuManager();
            menuManager.ToggleRequested += OnToggleRequested;
            menuManager.AboutRequested += OnAboutRequested;
            menuManager.ExitRequested += OnExitRequested;
            menuManager.ResetRequested += OnResetRequested;
            menuManager.UpdateStats(statsManager.GetStatsText());

            // Initialize tray icon
            trayIcon = new NotifyIcon
            {
                Icon = activeIcon,
                Visible = true,
                Text = "FakeWake - Keeping you active!",
                ContextMenuStrip = menuManager.Menu
            };
            trayIcon.DoubleClick += (s, e) => OnToggleRequested(s, e);

            // Initialize timers
            keepAliveTimer = new Timer { Interval = 60000 };
            keepAliveTimer.Tick += OnKeepAliveTick;

            statsTimer = new Timer { Interval = 10000 };
            statsTimer.Tick += OnStatsTimerTick;
            statsTimer.Start();

            activityCheckTimer = new Timer { Interval = 5000 };
            activityCheckTimer.Tick += OnActivityCheckTick;
            activityCheckTimer.Start();

            // Start in active state
            SetState(AppState.Active);
        }

        private void SetState(AppState newState)
        {
            if (newState == state && state != AppState.Active) return;

            // Handle state exit
            if (state == AppState.Active && newState != AppState.Active)
            {
                statsManager.PauseSession();
            }

            state = newState;

            // Handle state enter
            switch (state)
            {
                case AppState.Active:
                    trayIcon.Icon = activeIcon;
                    keepAliveService.PreventSleep();
                    keepAliveTimer.Start();
                    statsManager.ResumeSession();
                    menuManager.UpdateStatus("Status: Active", "Pause");
                    UpdateStatsDisplay();
                    break;

                case AppState.AutoPaused:
                    trayIcon.Icon = inactiveIcon;
                    keepAliveService.AllowSleep();
                    keepAliveTimer.Stop();
                    trayIcon.Text = "FakeWake - Auto-paused";
                    menuManager.UpdateStatus("Status: Auto-paused", "Pause");
                    break;

                case AppState.ManuallyPaused:
                    trayIcon.Icon = inactiveIcon;
                    keepAliveService.AllowSleep();
                    keepAliveTimer.Stop();
                    trayIcon.Text = "FakeWake - Paused";
                    menuManager.UpdateStatus("Status: Paused", "Resume");
                    break;
            }
        }

        private void OnKeepAliveTick(object sender, EventArgs e)
        {
            if (state == AppState.Active)
            {
                keepAliveService.SimulateActivity();
            }
        }

        private void OnStatsTimerTick(object sender, EventArgs e)
        {
            if (state == AppState.Active)
            {
                UpdateStatsDisplay();

                if (DateTime.Now.Second % 60 == 0)
                {
                    statsManager.Save();
                }
            }
        }

        private void OnActivityCheckTick(object sender, EventArgs e)
        {
            if (state == AppState.Active)
            {
                if (activityMonitor.IsUserActive)
                {
                    SetState(AppState.AutoPaused);
                }
            }
            else if (state == AppState.AutoPaused)
            {
                if (activityMonitor.IsUserIdle)
                {
                    SetState(AppState.Active);
                }
            }
        }

        private void UpdateStatsDisplay()
        {
            trayIcon.Text = $"FakeWake - {statsManager.GetFormattedTime()}";
            menuManager.UpdateStats(statsManager.GetStatsText());
        }

        private void OnToggleRequested(object sender, EventArgs e)
        {
            switch (state)
            {
                case AppState.Active:
                case AppState.AutoPaused:
                    SetState(AppState.ManuallyPaused);
                    break;
                case AppState.ManuallyPaused:
                    SetState(AppState.Active);
                    break;
            }
        }

        private void OnResetRequested(object sender, EventArgs e)
        {
            var random = new Random();
            var message = FunnyResetMessages[random.Next(FunnyResetMessages.Length)];

            var result = MessageBox.Show(
                $"{message}\n\nYou're about to reset {statsManager.GetFormattedTime()} of tracked time.\n\nNo takebacks!",
                "Reset Counter?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result == DialogResult.Yes)
            {
                statsManager.Reset();
                UpdateStatsDisplay();
            }
        }

        private void OnAboutRequested(object sender, EventArgs e)
        {
            MessageBox.Show(
                "FakeWake v1.5\n\n" +
                "Keeps your Windows session active and prevents Teams/Slack from marking you as Away.\n\n" +
                "Features:\n" +
                "• Smart auto-pause when you're working\n" +
                "• Auto-resume after 2 minutes idle\n" +
                "• Activity time counter with achievements\n" +
                "• Prevents system sleep\n" +
                "• Silent operation\n\n" +
                "Double-click the tray icon to pause/resume.\n" +
                "Click stats 5 times to reset counter.",
                "About FakeWake",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private void OnExitRequested(object sender, EventArgs e)
        {
            statsManager.Save();
            keepAliveService.AllowSleep();

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
