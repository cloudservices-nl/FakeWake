using System;
using System.Drawing;
using System.Windows.Forms;

namespace FakeWake.UI
{
    public class TrayMenuManager
    {
        private readonly ContextMenuStrip contextMenu;
        private readonly ToolStripMenuItem statusItem;
        private readonly ToolStripMenuItem statsItem;
        private readonly ToolStripMenuItem toggleItem;

        private int statsClickCount = 0;
        private DateTime lastStatsClick = DateTime.MinValue;
        private bool keepMenuOpen = false;

        public event EventHandler ToggleRequested;
        public event EventHandler AboutRequested;
        public event EventHandler ExitRequested;
        public event EventHandler ResetRequested;

        public ContextMenuStrip Menu => contextMenu;

        public TrayMenuManager()
        {
            contextMenu = new ContextMenuStrip();

            statusItem = new ToolStripMenuItem("Status: Active")
            {
                Enabled = false,
                Font = new Font(contextMenu.Font, FontStyle.Bold)
            };
            contextMenu.Items.Add(statusItem);

            statsItem = new ToolStripMenuItem()
            {
                ForeColor = Color.DarkGreen
            };
            statsItem.MouseDown += OnStatsClick;
            contextMenu.Items.Add(statsItem);

            contextMenu.Closing += OnMenuClosing;

            contextMenu.Items.Add(new ToolStripSeparator());

            toggleItem = new ToolStripMenuItem("Pause", null, (s, e) => ToggleRequested?.Invoke(this, e));
            contextMenu.Items.Add(toggleItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var aboutItem = new ToolStripMenuItem("About", null, (s, e) => AboutRequested?.Invoke(this, e));
            contextMenu.Items.Add(aboutItem);

            var exitItem = new ToolStripMenuItem("Exit", null, (s, e) => ExitRequested?.Invoke(this, e));
            contextMenu.Items.Add(exitItem);
        }

        public void UpdateStatus(string statusText, string toggleText)
        {
            statusItem.Text = statusText;
            toggleItem.Text = toggleText;
        }

        public void UpdateStats(string statsText)
        {
            statsItem.Text = statsText;
        }

        private void OnMenuClosing(object sender, ToolStripDropDownClosingEventArgs e)
        {
            if (keepMenuOpen && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
            {
                e.Cancel = true;
            }
        }

        private void OnStatsClick(object sender, MouseEventArgs e)
        {
            keepMenuOpen = true;

            if ((DateTime.Now - lastStatsClick).TotalSeconds > 2)
            {
                statsClickCount = 0;
            }

            lastStatsClick = DateTime.Now;
            statsClickCount++;

            if (statsClickCount >= 5)
            {
                statsClickCount = 0;
                keepMenuOpen = false;
                contextMenu.Close();
                ResetRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
