using System;
using System.IO;

namespace FakeWake.Services
{
    public class StatsManager
    {
        private readonly string statsFilePath;
        private TimeSpan totalActiveTime;
        private DateTime sessionStartTime;

        private static readonly (double MaxHours, string Message)[] Achievements =
        {
            (0.5, "Rookie numbers"),
            (1, "Getting started"),
            (2, "Productive vibes"),
            (4, "Going strong"),
            (8, "Full workday dodged"),
            (12, "Dedication level: High"),
            (24, "You're a legend"),
            (48, "Superhuman detected"),
            (100, "Absolute madlad"),
            (200, "Coffee addicted"),
            (500, "Professional procrastinator"),
            (1000, "Time wizard"),
            (double.MaxValue, "Eternal presence achieved")
        };

        public StatsManager()
        {
            statsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FakeWake",
                "stats.txt"
            );
            Load();
        }

        public TimeSpan TotalTime => totalActiveTime + CurrentSessionTime;

        public TimeSpan CurrentSessionTime => DateTime.Now - sessionStartTime;

        public void Load()
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
                totalActiveTime = TimeSpan.Zero;
            }

            sessionStartTime = DateTime.Now;
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(statsFilePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(statsFilePath, TotalTime.Ticks.ToString());
            }
            catch
            {
                // Silently fail if we can't save stats
            }
        }

        public void Reset()
        {
            totalActiveTime = TimeSpan.Zero;
            sessionStartTime = DateTime.Now;
            Save();
        }

        public void PauseSession()
        {
            Save();
            totalActiveTime = TotalTime;
        }

        public void ResumeSession()
        {
            sessionStartTime = DateTime.Now;
        }

        public string GetAchievement()
        {
            var hours = TotalTime.TotalHours;
            foreach (var (maxHours, message) in Achievements)
            {
                if (hours < maxHours)
                    return message;
            }
            return Achievements[Achievements.Length - 1].Message;
        }

        public string GetFormattedTime()
        {
            return FormatTimeSpan(TotalTime);
        }

        public string GetStatsText()
        {
            return $"🏆 {GetAchievement()}\n⏱️ Time: {GetFormattedTime()}";
        }

        public static string FormatTimeSpan(TimeSpan time)
        {
            if (time.TotalDays >= 1)
                return $"{(int)time.TotalDays}d {time.Hours}h {time.Minutes}m";
            if (time.TotalHours >= 1)
                return $"{(int)time.TotalHours}h {time.Minutes}m";
            return $"{time.Minutes}m {time.Seconds}s";
        }
    }
}
