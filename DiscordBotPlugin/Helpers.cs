using Discord;
using ModuleShared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static DiscordBotPlugin.PluginMain;

namespace DiscordBotPlugin
{
    internal class Helpers
    {
        private Settings settings;
        private ILogger log;
        private IApplicationWrapper application;
        private IConfigSerializer config;
        private IPlatformInfo platform;
        private InfoPanel infoPanel;

        public Helpers(Settings settings, ILogger log, IApplicationWrapper application, IConfigSerializer config, IPlatformInfo platform, InfoPanel infoPanel)
        {
            this.settings = settings;
            this.log = log;
            this.application = application;
            this.config = config;
            this.platform = platform;
            this.infoPanel = infoPanel;
        }

        public void SetInfoPanel(InfoPanel infoPanel)
        {
            this.infoPanel = infoPanel;
        }

        public string OnlineBotPresenceString(int onlinePlayers, int maximumPlayers)
        {
            if (settings?.MainSettings == null)
            {
                log.Error("Settings or MainSettings are null in OnlineBotPresenceString.");
                return "Online";
            }

            if (string.IsNullOrEmpty(settings.MainSettings.OnlineBotPresence) && settings.MainSettings.ValidPlayerCount)
            {
                return $"{onlinePlayers}/{maximumPlayers} players";
            }

            if (string.IsNullOrEmpty(settings.MainSettings.OnlineBotPresence))
            {
                return "Online";
            }

            string presence = settings.MainSettings.OnlineBotPresence;
            presence = presence.Replace("{OnlinePlayers}", onlinePlayers.ToString());
            presence = presence.Replace("{MaximumPlayers}", maximumPlayers.ToString());

            return presence;
        }

        public string GetPlayTimeLeaderBoard(int placesToShow, bool playerSpecific, string playerName, bool fullList, bool webPanel)
        {
            if (settings?.MainSettings?.PlayTime == null || infoPanel?.playerPlayTimes == null)
            {
                log.Error("PlayTime or playerPlayTimes are null in GetPlayTimeLeaderBoard.");
                return "```No play time logged yet```";
            }

            Dictionary<string, TimeSpan> playtime = new Dictionary<string, TimeSpan>(settings.MainSettings.PlayTime);

            //remove any blank names
            foreach (var key in playtime.Keys.Where(k => k == "").ToList())
            {
                playtime.Remove(key);
            }

            foreach (PlayerPlayTime player in infoPanel.playerPlayTimes)
            {
                TimeSpan currentSession = DateTime.Now - player.JoinTime;
                if (!playtime.ContainsKey(player.PlayerName))
                {
                    playtime[player.PlayerName] = new TimeSpan();
                }
                playtime[player.PlayerName] = playtime[player.PlayerName].Add(currentSession);
            }

            var sortedList = playtime.OrderByDescending(v => v.Value).ToList();

            if (sortedList.Count == 0)
            {
                return "```No play time logged yet```";
            }

            if (playerSpecific)
            {
                var playerEntry = sortedList.Find(p => p.Key == playerName);
                if (playerEntry.Key != null)
                {
                    TimeSpan time = playerEntry.Value;
                    return $"`{time.Days}d {time.Hours}h {time.Minutes}m {time.Seconds}s, position {(sortedList.FindIndex(p => p.Key == playerName) + 1)}, last seen {GetLastSeen(playerName)}`";
                }
                else
                {
                    return "```No play time logged yet```";
                }
            }
            else
            {
                string leaderboard = "";
                if (!webPanel) leaderboard += "```";

                int position = 1;

                if (fullList)
                {
                    leaderboard += $"{string.Format("{0,-4}{1,-20}{2,-15}{3,-30}", "Pos", "Player Name", "Play Time", "Last Seen")}{Environment.NewLine}";
                }

                foreach (var player in sortedList)
                {
                    if (position > placesToShow) break;

                    if (fullList)
                    {
                        leaderboard += $"{string.Format("{0,-4}{1,-20}{2,-15}{3,-30}", position + ".", player.Key, $"{player.Value.Days}d {player.Value.Hours}h {player.Value.Minutes}m {player.Value.Seconds}s", GetLastSeen(player.Key))}{Environment.NewLine}";
                    }
                    else
                    {
                        if (webPanel)
                        {
                            leaderboard += $"{player.Key} - {player.Value.Days}d {player.Value.Hours}h {player.Value.Minutes}m {player.Value.Seconds}s{Environment.NewLine}";
                        }
                        else
                        {
                            leaderboard += $"{string.Format("{0,-4}{1,-20}{2,-15}", position + ".", player.Key, $"{player.Value.Days}d {player.Value.Hours}h {player.Value.Minutes}m {player.Value.Seconds}s")}{Environment.NewLine}";
                        }
                    }

                    position++;
                }

                if (!webPanel) leaderboard += "```";

                return leaderboard;
            }
        }

        public string GetLastSeen(string playerName)
        {
            if (application is not IHasSimpleUserList hasSimpleUserList)
            {
                log.Error("Application does not implement IHasSimpleUserList in GetLastSeen.");
                return "N/A";
            }

            bool playerOnline = hasSimpleUserList.Users.Any(user => user.Name == playerName);
            string lastSeen;

            if (playerOnline)
            {
                lastSeen = DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss");
            }
            else
            {
                try
                {
                    lastSeen = settings?.MainSettings?.LastSeen != null && settings.MainSettings.LastSeen.ContainsKey(playerName)
                        ? settings.MainSettings.LastSeen[playerName].ToString("dddd, dd MMMM yyyy HH:mm:ss")
                        : "N/A";
                }
                catch (Exception ex)
                {
                    log.Error($"Error retrieving last seen for {playerName}: {ex.Message}");
                    lastSeen = "N/A";
                }
            }

            return lastSeen;
        }

        public void ClearAllPlayTimes()
        {
            if (infoPanel?.playerPlayTimes == null || settings?.MainSettings?.PlayTime == null || config == null)
            {
                log.Error("InfoPanel, PlayTime, or Config is null in ClearAllPlayTimes.");
                return;
            }

            try
            {
                foreach (var playerPlayTime in infoPanel.playerPlayTimes)
                {
                    log.Debug($"Saving playtime for {playerPlayTime.PlayerName}");

                    playerPlayTime.LeaveTime = DateTime.Now;

                    if (!settings.MainSettings.PlayTime.ContainsKey(playerPlayTime.PlayerName))
                    {
                        settings.MainSettings.PlayTime.Add(playerPlayTime.PlayerName, new TimeSpan(0));
                    }

                    TimeSpan sessionPlayTime = playerPlayTime.LeaveTime - playerPlayTime.JoinTime;
                    settings.MainSettings.PlayTime[playerPlayTime.PlayerName] += sessionPlayTime;

                    settings.MainSettings.LastSeen[playerPlayTime.PlayerName] = DateTime.Now;
                    config.Save(settings);
                }

                infoPanel.playerPlayTimes.Clear();
            }
            catch (Exception ex)
            {
                log.Error($"Error clearing playtimes: {ex.Message}");
            }
        }

        public string GetMemoryUsage()
        {
            if (application == null || platform == null || settings?.MainSettings == null)
            {
                log.Error("Application, Platform, or Settings is null in GetMemoryUsage.");
                return "Unknown";
            }

            double totalAvailable = application.MaxRAMUsage > 0 ? application.MaxRAMUsage : platform.InstalledRAMMB;
            double usage = application.GetPhysicalRAMUsage();
            bool isGB = usage >= 1024 || (totalAvailable > 1024 && settings.MainSettings.ShowMaximumRAM);

            if (isGB)
            {
                usage /= 1024;
                totalAvailable /= 1024;
            }

            return settings.MainSettings.ShowMaximumRAM
                ? $"{usage:N2}/{totalAvailable:N2} {(isGB ? "GB" : "MB")}"
                : $"{usage:N2} {(isGB ? "GB" : "MB")}";
        }

        public string GetApplicationStateString()
        {
            if (settings?.MainSettings == null)
            {
                log.Error("Settings or MainSettings are null in GetApplicationStateString.");
                return "Unknown";
            }

            return settings.MainSettings.ChangeStatus.TryGetValue(application.State.ToString(), out var stateString)
                ? stateString
                : application.State.ToString();
        }

        public bool CheckIfPlayerJoinedWithinLast10Seconds(List<PlayerPlayTime> players, string playerName)
        {
            if (players == null)
            {
                log.Error("Players list is null in CheckIfPlayerJoinedWithinLast10Seconds.");
                return false;
            }

            DateTime tenSecondsAgo = DateTime.Now.AddSeconds(-10);
            return players.Any(p => p.PlayerName == playerName && p.JoinTime >= tenSecondsAgo);
        }

        public async Task ExecuteWithDelay(int delay, Action action)
        {
            if (action == null)
            {
                log.Error("Action is null in ExecuteWithDelay.");
                return;
            }

            await Task.Delay(delay);
            action();
        }

        public Color GetColour(string command, string hexColour)
        {
            try
            {
                string cleanedHex = hexColour.Replace("#", "");
                uint colourCode = uint.Parse(cleanedHex, System.Globalization.NumberStyles.HexNumber);
                return new Color(colourCode);
            }
            catch
            {
                log.Info($"Invalid colour code for {command}, using default colour.");
                return command switch
                {
                    "Info" => Color.DarkGrey,
                    "Start" or "PlayerJoin" => Color.Green,
                    "Stop" or "Kill" or "PlayerLeave" => Color.Red,
                    "Restart" => Color.Orange,
                    "Update" or "Manage" => Color.Blue,
                    "Console" => Color.DarkGreen,
                    "Leaderboard" => Color.DarkGrey,
                    _ => Color.DarkerGrey
                };
            }
        }

        public List<string> SplitOutputIntoCodeBlocks(List<string> messages)
        {
            if (messages == null)
            {
                log.Error("Messages list is null in SplitOutputIntoCodeBlocks.");
                return new List<string> { "No messages to display" };
            }

            const int MaxCodeBlockLength = 2000;
            List<string> outputStrings = new List<string>();
            string currentString = "";

            foreach (string message in messages)
            {
                if (currentString.Length + message.Length + Environment.NewLine.Length + 6 > MaxCodeBlockLength)
                {
                    //remove previous code block formatting
                    outputStrings.Add(currentString);
                    currentString = "";
                }

                if (!string.IsNullOrEmpty(currentString))
                {
                    currentString += Environment.NewLine;
                }
                currentString += message;
            }

            if (!string.IsNullOrEmpty(currentString))
            {
                outputStrings.Add(currentString);
            }

            return outputStrings;
        }

        public async Task<string> GetExternalIpAddressAsync()
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // Use a service that returns the IP address in plain text
                    HttpResponseMessage response = await client.GetAsync("https://api.ipify.org");
                    response.EnsureSuccessStatusCode();

                    string ipAddress = await response.Content.ReadAsStringAsync();
                    return ipAddress;
                }
                catch (Exception ex)
                {
                    log.Error("Error fetching IP: " + ex.Message);
                    return null;
                }
            }
        }
    }
}
