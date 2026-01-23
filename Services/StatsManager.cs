using System;
using System.IO;

namespace FakeWake.Services
{
    public class StatsManager
    {
        private readonly string statsFilePath;
        private TimeSpan totalActiveTime;
        private DateTime sessionStartTime;


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

            if (hours < 0.5) return "Rookie numbers";
            if (hours < 1) return "Getting started";
            if (hours < 2) return "Productive vibes";
            if (hours < 4) return "Going strong";
            if (hours < 8) return "Full workday dodged";
            if (hours < 12) return "Dedication level: High";
            if (hours < 24) return "You're a legend";
            if (hours < 48) return "Superhuman detected";
            if (hours < 100) return "Absolute animal";
            if (hours < 200) return "Coffee addicted";
            if (hours < 500) return "Professional procrastinator";
            if (hours < 1000) return "Time wizard";
            return "Eternal presence achieved";
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
