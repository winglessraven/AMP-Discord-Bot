using Discord.WebSocket;
using Discord;
using ModuleShared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static DiscordBotPlugin.PluginMain;

namespace DiscordBotPlugin
{
    internal class InfoPanel
    {
        private readonly IApplicationWrapper application;
        private readonly Settings settings;
        private readonly Helpers helper;
        private readonly IAMPInstanceInfo aMPInstanceInfo;
        private readonly ILogger log;
        private readonly IConfigSerializer config;
        private Bot bot;
        private readonly Commands commands;

        public InfoPanel(IApplicationWrapper application, Settings settings, Helpers helper, IAMPInstanceInfo aMPInstanceInfo, ILogger log, IConfigSerializer config, Bot bot, Commands commands)
        {
            this.application = application;
            this.settings = settings;
            this.helper = helper;
            this.aMPInstanceInfo = aMPInstanceInfo;
            this.log = log;
            this.config = config;
            this.bot = bot;
            this.commands = commands;
        }

        public void SetBot(Bot bot)
        {
            this.bot = bot;
        }

        public string valheimJoinCode;
        public List<PlayerPlayTime> playerPlayTimes = new List<PlayerPlayTime>();

        /// <summary>
        /// Task to get current server info and create or update an embedded message
        /// </summary>
        /// <param name="updateExisting">Embed already exists?</param>
        /// <param name="msg">Command from Discord</param>
        /// <param name="Buttonless">Should the embed be buttonless?</param>
        /// <returns></returns>
        public async Task GetServerInfo(bool updateExisting, SocketSlashCommand msg, bool Buttonless)
        {
            if (bot.client?.ConnectionState != ConnectionState.Connected)
            {
                log.Warning("Client is not connected.");
                return;
            }

            if (application is not IHasSimpleUserList hasSimpleUserList)
            {
                log.Error("Application does not implement IHasSimpleUserList.");
                return;
            }

            var onlinePlayers = hasSimpleUserList.Users.Count;
            var maximumPlayers = hasSimpleUserList.MaxUsers;

            var embed = new EmbedBuilder
            {
                Title = "Server Info",
                ThumbnailUrl = settings?.MainSettings?.GameImageURL
            };

            if (!string.IsNullOrEmpty(settings?.ColourSettings?.InfoPanelColour))
            {
                embed.Color = helper.GetColour("Info", settings.ColourSettings.InfoPanelColour);
            }
            else
            {
                embed.Color = Color.DarkGrey;
            }

            switch (application.State)
            {
                case ApplicationState.Ready:
                    embed.AddField("Server Status", ":white_check_mark: " + helper.GetApplicationStateString(), false);
                    break;
                case ApplicationState.Failed:
                case ApplicationState.Stopped:
                    embed.AddField("Server Status", ":no_entry: " + helper.GetApplicationStateString(), false);
                    break;
                default:
                    embed.AddField("Server Status", ":hourglass: " + helper.GetApplicationStateString(), false);
                    break;
            }

            embed.AddField("Server Name", "```" + settings?.MainSettings?.ServerDisplayName + "```", false);
            string connectionURL = settings.MainSettings.ServerConnectionURL;
            if (connectionURL.ToLower().Contains("{publicip}"))
            {
                string ipAddress = await helper.GetExternalIpAddressAsync();
                connectionURL = connectionURL.ToLower().Replace("{publicip}", ipAddress);
            }
            embed.AddField("Server IP", "```" + connectionURL + "```", false);

            if (!string.IsNullOrEmpty(settings?.MainSettings?.ServerPassword))
            {
                embed.AddField("Server Password", "```" + settings.MainSettings.ServerPassword + "```", false);
            }

            embed.AddField("CPU Usage", application.GetCPUUsage() + "%", true);
            embed.AddField("Memory Usage", helper.GetMemoryUsage(), true);

            if (application.State == ApplicationState.Ready)
            {
                TimeSpan uptime = DateTime.Now.Subtract(application.StartTime.ToLocalTime());
                embed.AddField("Uptime", string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D2}", uptime.Days, uptime.Hours, uptime.Minutes, uptime.Seconds), true);
            }

            if (settings?.MainSettings?.ValidPlayerCount == true)
            {
                embed.AddField("Player Count", onlinePlayers + "/" + maximumPlayers, true);
            }

            if (settings?.MainSettings?.ShowOnlinePlayers == true)
            {
                List<string> onlinePlayerNames = hasSimpleUserList.Users.Where(u => !string.IsNullOrEmpty(u.Name)).Select(u => u.Name).ToList();

                //sort alphabetically
                onlinePlayerNames.Sort(StringComparer.OrdinalIgnoreCase);

                if (onlinePlayerNames.Count > 0)
                {
                    embed.AddField("Online Players", string.Join(Environment.NewLine, onlinePlayerNames), false);
                }
            }

            if (!string.IsNullOrEmpty(settings?.MainSettings?.ModpackURL))
            {
                embed.AddField("Server Mod Pack", settings.MainSettings.ModpackURL, false);
            }

            if (settings?.MainSettings?.ShowPlaytimeLeaderboard == true)
            {
                string leaderboard = helper.GetPlayTimeLeaderBoard(settings.MainSettings.PlaytimeLeaderboardPlaces, false, null, false, false);
                embed.AddField("Top " + settings.MainSettings.PlaytimeLeaderboardPlaces + " Players by Play Time", leaderboard, false);
            }

            if (settings?.GameSpecificSettings?.ValheimJoinCode == true && !string.IsNullOrEmpty(valheimJoinCode) && application.State == ApplicationState.Ready)
            {
                embed.AddField("Server Join Code", "```" + valheimJoinCode + "```");
            }

            if (!string.IsNullOrEmpty(settings?.MainSettings?.AdditionalEmbedFieldTitle))
            {
                embed.AddField(settings.MainSettings.AdditionalEmbedFieldTitle, settings.MainSettings.AdditionalEmbedFieldText);
            }

            embed.WithFooter(settings?.MainSettings?.BotTagline).WithCurrentTimestamp();

            var builder = new ComponentBuilder();

            if (settings?.MainSettings?.ShowStartButton == true)
            {
                builder.WithButton("Start", "start-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Success, disabled: application.State == ApplicationState.Ready || application.State == ApplicationState.Starting || application.State == ApplicationState.Installing);
            }

            if (settings?.MainSettings?.ShowStopButton == true)
            {
                builder.WithButton("Stop", "stop-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed);
            }

            if (settings?.MainSettings?.ShowRestartButton == true)
            {
                builder.WithButton("Restart", "restart-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed);
            }

            if (settings?.MainSettings?.ShowKillButton == true)
            {
                builder.WithButton("Kill", "kill-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed);
            }

            if (settings?.MainSettings?.ShowUpdateButton == true)
            {
                builder.WithButton("Update", "update-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Primary, disabled: application.State == ApplicationState.Installing);
            }

            if (settings?.MainSettings?.ShowManageButton == true)
            {
                builder.WithButton("Manage", "manage-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Primary);
            }

            if (settings?.MainSettings?.ShowBackupButton == true)
            {
                builder.WithButton("Backup", "backup-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Secondary);
            }

            if (updateExisting)
            {
                foreach (string details in settings?.MainSettings?.InfoMessageDetails ?? new List<string>())
                {
                    try
                    {
                        string[] split = details.Split('-');
                        var existingMsg = await bot.client.GetGuild(Convert.ToUInt64(split[0])).GetTextChannel(Convert.ToUInt64(split[1])).GetMessageAsync(Convert.ToUInt64(split[2])) as IUserMessage;

                        if (existingMsg != null)
                        {
                            await existingMsg.ModifyAsync(x =>
                            {
                                x.Embed = embed.Build();
                                if (split.Length <= 3 || !split[3].ToString().Equals("True"))
                                {
                                    x.Components = builder.Build();
                                }
                            });
                        }
                        else
                        {
                            settings?.MainSettings?.InfoMessageDetails.Remove(details);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error updating message: {ex.Message}");
                    }
                }
            }
            else
            {
                var chnl = msg.Channel as SocketGuildChannel;
                var guild = chnl?.Guild?.Id ?? 0;
                var channelID = msg.Channel?.Id ?? 0;

                //create the embed according to the request
                if (Buttonless)
                {
                    var message = await bot.client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build());
                    log.Debug("Message ID: " + message.Id.ToString());
                    settings.MainSettings.InfoMessageDetails.Add(guild.ToString() + "-" + channelID.ToString() + "-" + message.Id.ToString() + "-" + Buttonless);
                }
                else
                {
                    var message = await bot.client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build(), components: builder.Build());
                    log.Debug("Message ID: " + message.Id.ToString());
                    settings.MainSettings.InfoMessageDetails.Add(guild.ToString() + "-" + channelID.ToString() + "-" + message.Id.ToString() + "-" + Buttonless);
                }
                config.Save(settings);
            }
        }

        public async Task UpdateWebPanel(string webPanelPath)
        {
            if (string.IsNullOrEmpty(webPanelPath))
            {
                log.Error("WebPanel path is null or empty.");
                return;
            }

            log.Info(webPanelPath);

            while (settings?.MainSettings?.EnableWebPanel == true)
            {
                try
                {
                    Directory.CreateDirectory(webPanelPath);

                    string scriptFilePath = Path.Combine(webPanelPath, "script.js");
                    string stylesFilePath = Path.Combine(webPanelPath, "styles.css");
                    string panelFilePath = Path.Combine(webPanelPath, "panel.html");
                    string jsonFilePath = Path.Combine(webPanelPath, "panel.json");

                    ResourceReader reader = new ResourceReader();

                    if (!File.Exists(scriptFilePath))
                        await File.WriteAllTextAsync(scriptFilePath, reader.ReadResource("script.js"));

                    if (!File.Exists(stylesFilePath))
                        await File.WriteAllTextAsync(stylesFilePath, reader.ReadResource("styles.css"));

                    if (!File.Exists(panelFilePath))
                        await File.WriteAllTextAsync(panelFilePath, reader.ReadResource("panel.html"));

                    var cpuUsage = application.GetCPUUsage() + "%";
                    var memoryUsage = helper.GetMemoryUsage();

                    IHasSimpleUserList hasSimpleUserList = application as IHasSimpleUserList;
                    var onlinePlayerCount = hasSimpleUserList?.Users?.Count ?? 0;
                    var maximumPlayers = hasSimpleUserList?.MaxUsers ?? 0;

                    var serverStatus = application.State == ApplicationState.Ready ? "✅ " + helper.GetApplicationStateString() : application.State == ApplicationState.Failed || application.State == ApplicationState.Stopped ? "⛔ " + helper.GetApplicationStateString() : "⏳ " + helper.GetApplicationStateString();
                    var serverStatusClass = application.State == ApplicationState.Ready ? "ready" : application.State == ApplicationState.Failed || application.State == ApplicationState.Stopped ? "stopped" : "pending";

                    var uptime = application.State == ApplicationState.Ready ? string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D2}", DateTime.Now.Subtract(application.StartTime.ToLocalTime()).Days, DateTime.Now.Subtract(application.StartTime.ToLocalTime()).Hours, DateTime.Now.Subtract(application.StartTime.ToLocalTime()).Minutes, DateTime.Now.Subtract(application.StartTime.ToLocalTime()).Seconds) : "00:00:00:00";

                    var onlinePlayers = settings?.MainSettings?.ShowOnlinePlayers == true ? hasSimpleUserList?.Users?.Where(u => !string.IsNullOrEmpty(u.Name)).Select(u => u.Name).ToArray() : new string[] { };

                    var playerCount = settings?.MainSettings?.ValidPlayerCount == true ? $"{onlinePlayerCount}/{maximumPlayers}" : string.Empty;

                    var playtimeLeaderBoard = settings?.MainSettings?.ShowPlaytimeLeaderboard == true ? helper.GetPlayTimeLeaderBoard(5, false, null, false, true).Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries) : new string[] { };

                    ServerInfo serverInfo = new ServerInfo
                    {
                        ServerName = settings?.MainSettings?.ServerDisplayName,
                        ServerIP = settings?.MainSettings?.ServerConnectionURL,
                        ServerStatus = serverStatus,
                        ServerStatusClass = serverStatusClass,
                        CPUUsage = cpuUsage,
                        MemoryUsage = memoryUsage,
                        Uptime = uptime,
                        OnlinePlayers = onlinePlayers,
                        PlayerCount = playerCount,
                        PlaytimeLeaderBoard = playtimeLeaderBoard
                    };

                    string json = JsonConvert.SerializeObject(serverInfo, Formatting.Indented);

                    await File.WriteAllTextAsync(jsonFilePath, json);
                }
                catch (Exception ex)
                {
                    log.Error($"Error updating web panel: {ex.Message}");
                }

                await Task.Delay(10000);
            }
        }

        public async Task OnButtonPress(SocketMessageComponent arg)
        {
            log.Debug($"Button pressed: {arg.Data.CustomId}");

            if (arg.User is not SocketGuildUser user)
            {
                log.Warning("Invalid user pressing the button.");
                return;
            }

            bool hasServerPermission = bot?.HasServerPermission(user) ?? false;
            if (!hasServerPermission)
            {
                await arg.DeferAsync();
                return;
            }

            var buttonId = arg.Data.CustomId.Replace("-" + aMPInstanceInfo?.InstanceId, string.Empty);
            switch (buttonId)
            {
                case "start-server":
                    application?.Start();
                    break;
                case "stop-server":
                    application?.Stop();
                    break;
                case "restart-server":
                    application?.Restart();
                    break;
                case "kill-server":
                    application?.Kill();
                    break;
                case "update-server":
                    application?.Update();
                    break;
                case "manage-server":
                    await commands.ManageServer(arg);
                    break;
                case "backup-server":
                    commands?.BackupServer(user);
                    break;
                default:
                    log.Warning("Unknown button ID pressed.");
                    return;
            }

            var capitalizedButtonResponse = char.ToUpper(buttonId[0]) + buttonId.Substring(1).Replace("-server", "");
            await bot.ButtonResponse(capitalizedButtonResponse, arg);
        }
    }
}
