using ModuleShared;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.Linq;
using Discord.Net;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;
using DiscordBotPlugin;
using System.Threading;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Resources;

namespace DiscordBotPlugin
{
    public class PluginMain : AMPPlugin
    {
        private readonly Settings _settings;
        private readonly ILogger log;
        private readonly IPlatformInfo platform;
        private readonly IRunningTasksManager _tasks;
        private readonly IApplicationWrapper application;
        private readonly IAMPInstanceInfo aMPInstanceInfo;
        private readonly IConfigSerializer _config;
        private DiscordSocketClient _client;
        private List<PlayerPlayTime> playerPlayTimes = new List<PlayerPlayTime>();
        private List<String> consoleOutput = new List<String>();

        //webserver
        string webPanelPath = Path.Combine(Environment.CurrentDirectory, "WebRoot","WebPanel");

        //game specific variables
        private string valheimJoinCode;

        public PluginMain(ILogger log, IConfigSerializer config, IPlatformInfo platform,
            IRunningTasksManager taskManager, IApplicationWrapper application, IAMPInstanceInfo AMPInstanceInfo)
        {
            _config = config;
            this.log = log;
            this.platform = platform;
            _settings = config.Load<Settings>(AutoSave: true);
            _tasks = taskManager;
            this.application = application;
            aMPInstanceInfo = AMPInstanceInfo;

            config.SaveMethod = PluginSaveMethod.KVP;
            config.KVPSeparator = "=";

            _settings.SettingModified += Settings_SettingModified;
            log.MessageLogged += Log_MessageLogged;

            if (application is IHasSimpleUserList hasSimpleUserList)
            {
                hasSimpleUserList.UserJoins += UserJoins;
                hasSimpleUserList.UserLeaves += UserLeaves;
            }
        }

        /// <summary>
        /// Runs on MessageLogged event
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event Args</param>
        private void Log_MessageLogged(object sender, LogEventArgs e)
        {
            if (e.Level == LogLevels.Chat.ToString() &&
                _settings.MainSettings.SendChatToDiscord &&
                !string.IsNullOrEmpty(_settings.MainSettings.ChatToDiscordChannel))
            {
                // Clean the message to avoid code blocks and send it to Discord
                string clean = e.Message.Replace("`", "'");
                _ = ChatMessageSend(clean);
            }

            if ((e.Level == LogLevels.Console.ToString() || e.Level == LogLevels.Chat.ToString()) && _settings.MainSettings.SendConsoleToDiscord &&
                !string.IsNullOrEmpty(_settings.MainSettings.ConsoleToDiscordChannel))
            {
                // Clean the message to avoid code blocks and send it to Discord
                string clean = e.Message.Replace("`", "'");
                consoleOutput.Add(clean);
            }

            if (e.Level == LogLevels.Console.ToString() && _settings.GameSpecificSettings.ValheimJoinCode)
            {
                // Define the regular expression pattern to match the desired text and extract the numbers.
                string pattern = @"join code (\d+)";

                // Use Regex.Match to search for a match of the pattern in the message.
                Match match = Regex.Match(e.Message, pattern);

                // Check if a match was found.
                if (match.Success)
                {
                    // Extract the captured numbers from the match and store them in the valheimJoinCode variable.
                    valheimJoinCode = match.Groups[1].Value;
                }
            }
        }

        /// <summary>
        /// Initializes the bot and assigns an instance of WebMethods to APIMethods.
        /// </summary>
        /// <param name="APIMethods">An output parameter to hold the instance of WebMethods.</param>
        public override void Init(out WebMethodsBase APIMethods)
        {
            // Create a new instance of WebMethods and assign it to APIMethods
            APIMethods = new WebMethods(_tasks);
        }

        /// <summary>
        /// Runs on SettingsModified event
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event Args</param>
        void Settings_SettingModified(object sender, SettingModifiedEventArgs e)
        {
            if (_settings.MainSettings.BotActive)
            {
                try
                {
                    // Start the Discord bot if it's not already running
                    if (_client == null || _client.ConnectionState == ConnectionState.Disconnected)
                    {
                        _ = ConnectDiscordAsync(_settings.MainSettings.BotToken);
                    }
                }
                catch (Exception exception)
                {
                    // Log any errors that occur during bot connection
                    log.Error("Error with the Discord Bot: " + exception.Message);
                }
            }
            else
            {
                if (_client != null && _client.ConnectionState == ConnectionState.Connected)
                {
                    // Unsubscribe from events and logout from Discord if the bot is deactivated
                    _client.ButtonExecuted -= OnButtonPress;
                    _client.Log -= Log;
                    _client.Ready -= ClientReady;
                    _client.SlashCommandExecuted -= SlashCommandHandler;
                    _client.MessageReceived -= MessageHandler;

                    try
                    {
                        // Logout from Discord
                        _client.LogoutAsync();
                    }
                    catch (Exception exception)
                    {
                        // Log any errors that occur during logout
                        log.Error("Error logging out from Discord: " + exception.Message);
                    }
                }
            }
        }

        public override bool HasFrontendContent => false;

        /// <summary>
        /// Performs post-initialization actions for the bot.
        /// </summary>
        public override void PostInit()
        {
            // Check if the bot is turned on
            if (_settings.MainSettings.BotActive)
            {
                log.Info("Discord Bot Activated");

                // Check if we have a bot token and attempt to connect
                if (!string.IsNullOrEmpty(_settings.MainSettings.BotToken))
                {
                    try
                    {
                        _ = ConnectDiscordAsync(_settings.MainSettings.BotToken);
                    }
                    catch (Exception exception)
                    {
                        // Log any errors that occur during bot connection
                        log.Error("Error with the Discord Bot: " + exception.Message);
                    }
                }                
            }
        }

        public override IEnumerable<SettingStore> SettingStores => Utilities.EnumerableFrom(_settings);

        /// <summary>
        /// Async task to handle the Discord connection and call the status check
        /// </summary>
        /// <param name="BotToken">Discord Bot Token</param>
        /// <returns>Task</returns>
        public async Task ConnectDiscordAsync(string BotToken)
        {
            DiscordSocketConfig config;

            // Determine the GatewayIntents based on the chat settings
            if (_settings.MainSettings.SendChatToDiscord || _settings.MainSettings.SendDiscordChatToServer)
            {
                // Include MessageContent intent if chat is sent between Discord and the server
                config = new DiscordSocketConfig { GatewayIntents = GatewayIntents.DirectMessages | GatewayIntents.GuildMessages | GatewayIntents.Guilds | GatewayIntents.MessageContent };
            }
            else
            {
                config = new DiscordSocketConfig { GatewayIntents = GatewayIntents.DirectMessages | GatewayIntents.GuildMessages | GatewayIntents.Guilds };
            }

            if(_settings.MainSettings.DiscordDebugMode)
                config.LogLevel = LogSeverity.Debug;

            //config.GatewayHost = "wss://gateway.discord.gg";

            // Initialize Discord client with the specified configuration
            _client = new DiscordSocketClient(config);

            // Attach event handlers for logs and events
            _client.Log += Log;
            _client.ButtonExecuted += OnButtonPress;
            _client.Ready += ClientReady;
            _client.SlashCommandExecuted += SlashCommandHandler;
            if (_settings.MainSettings.SendChatToDiscord || _settings.MainSettings.SendDiscordChatToServer)
                _client.MessageReceived += MessageHandler;

            // Login and start the Discord client
            await _client.LoginAsync(TokenType.Bot, BotToken);
            await _client.StartAsync();

            // Set the bot's status
            _ = SetStatus();

            //console output
            _ = ConsoleOutputSend();

            // Web Panel
            _ = UpdateWebPanel();

            // Block this task until the program is closed or bot is stopped.
            await Task.Delay(-1);
        }

        /// <summary>
        /// Logs a message with an information level.
        /// </summary>
        /// <param name="msg">The message to be logged.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private Task Log(LogMessage msg)
        {
            // Log the message as an information level message
            log.Info(msg.ToString());

            // Return a completed task to fulfill the method signature
            return Task.CompletedTask;
        }

        /// <summary>
        /// Send a command to the AMP instance
        /// </summary>
        /// <param name="msg">Command to send to the server</param>
        /// <returns>Task</returns>
        private Task SendConsoleCommand(SocketSlashCommand msg)
        {
            try
            {
                // Initialize the command string
                string command = "";

                // Get the command to be sent based on the bot name removal setting
                if (_settings.MainSettings.RemoveBotName)
                {
                    command = msg.Data.Options.First().Value.ToString();
                }
                else
                {
                    command = msg.Data.Options.First().Options.First().Value.ToString();
                }

                // Send the command to the AMP instance
                IHasWriteableConsole writeableConsole = application as IHasWriteableConsole;
                writeableConsole.WriteLine(command);

                // Return a completed task to fulfill the method signature
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                // Log any errors that occur during command sending
                log.Error("Cannot send command: " + exception.Message);

                // Return a completed task to fulfill the method signature
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Send a chat message to the AMP instance, only for Minecraft for now
        /// </summary>
        /// <param name="author">Discord name of the sender</param>
        /// <param name="msg">Message to send</param>
        /// <returns>Task</returns>
        private Task SendChatCommand(string author, string msg)
        {
            try
            {
                // Construct the command to send
                string command = "say <" + author + "> " + msg;

                // Send the chat command to the AMP instance
                IHasWriteableConsole writeableConsole = application as IHasWriteableConsole;
                writeableConsole.WriteLine(command);

                // Return a completed task to fulfill the method signature
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                // Log any errors that occur during chat message sending
                log.Error("Cannot send chat message: " + exception.Message);

                // Return a completed task to fulfill the method signature
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Task to get current server info and create or update an embeded message
        /// </summary>
        /// <param name="updateExisting">Embed already exists?</param>
        /// <param name="msg">Command from Discord</param>
        /// <param name="Buttonless">Should the embed be buttonless?</param>
        /// <returns></returns>
        private async Task GetServerInfo(bool updateExisting, SocketSlashCommand msg, bool Buttonless)
        {
            //if bot isn't connected, stop any further action
            if (_client.ConnectionState != ConnectionState.Connected)
                return;

            //get count of players online and maximum slots
            IHasSimpleUserList hasSimpleUserList = application as IHasSimpleUserList;
            var onlinePlayers = hasSimpleUserList.Users.Count;
            var maximumPlayers = hasSimpleUserList.MaxUsers;

            //build bot response
            var embed = new EmbedBuilder
            {
                Title = "Server Info",
                ThumbnailUrl = _settings.MainSettings.GameImageURL
            };

            //if a custom colour is set use it, otherwise use default
            if (!_settings.ColourSettings.InfoPanelColour.Equals(""))
            {
                embed.Color = GetColour("Info", _settings.ColourSettings.InfoPanelColour);
            }
            else
            {
                embed.Color = Color.DarkGrey;
            }

            //if server is online
            if (application.State == ApplicationState.Ready)
            {
                embed.AddField("Server Status", ":white_check_mark: " + GetApplicationStateString(), false);
            }
            //if server is off or errored
            else if (application.State == ApplicationState.Failed || application.State == ApplicationState.Stopped)
            {
                embed.AddField("Server Status", ":no_entry: " + GetApplicationStateString(), false);
            }
            //everything else
            else
            {
                embed.AddField("Server Status", ":hourglass: " + GetApplicationStateString(), false);
            }

            //set server name field
            embed.AddField("Server Name", "```" + _settings.MainSettings.ServerDisplayName + "```", false);
            
            //set server IP field
            embed.AddField("Server IP", "```" + _settings.MainSettings.ServerConnectionURL + "```", false);

            //set password field if populated in setttings
            if (_settings.MainSettings.ServerPassword != "")
            {
                embed.AddField("Server Password", "```" + _settings.MainSettings.ServerPassword + "```", false);
            }

            //set CPU usage field
            embed.AddField("CPU Usage", application.GetCPUUsage() + "%", true);

            //set mem usage field
            embed.AddField("Memory Usage", application.GetRAMUsage().ToString("N0") + "MB", true);

            //if server is online, get the uptime info and set the field accordingly
            if (application.State == ApplicationState.Ready)
            {
                TimeSpan uptime = DateTime.Now.Subtract(application.StartTime);
                embed.AddField("Uptime", string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D2}", uptime.Days, uptime.Hours, uptime.Minutes, uptime.Seconds), true);
            }

            //if there is a valid player count, show the online player count
            if (_settings.MainSettings.ValidPlayerCount)
            {
                embed.AddField("Player Count",onlinePlayers + "/" + maximumPlayers, true);
            }

            //if show online players is enabled, attempt to get the player names and show them if available
            if (_settings.MainSettings.ShowOnlinePlayers)
            {
                List<string> onlinePlayerNames = new List<string>();
                foreach (SimpleUser user in hasSimpleUserList.Users)
                {
                    if (user.Name != null && user.Name != "")
                        onlinePlayerNames.Add(user.Name);
                }

                if (onlinePlayerNames.Count != 0)
                {
                    string names = "";
                    foreach (string s in onlinePlayerNames)
                    {
                        names += s + Environment.NewLine;
                    }
                    embed.AddField("Online Players", names, false);
                }
            }

            //set modpack url field if populated in settings
            if (_settings.MainSettings.ModpackURL != "")
            {
                embed.AddField("Server Mod Pack", _settings.MainSettings.ModpackURL, false);
            }

            //if show playtime leaderboard is enabled
            if (_settings.MainSettings.ShowPlaytimeLeaderboard)
            {
                string leaderboard = GetPlayTimeLeaderBoard(5, false, null, false);
                embed.AddField("Top 5 Players by Play Time", leaderboard, false);
            }

            //if valheim join code enabled and code is logged
            if(_settings.GameSpecificSettings.ValheimJoinCode && valheimJoinCode != "" && application.State == ApplicationState.Ready)
            {
                embed.AddField("Server Join Code", "```" + valheimJoinCode + "```");
            }

            //if user has added an additonal embed field, add it
            if(_settings.MainSettings.AdditionalEmbedFieldTitle.Length > 0)
            {
                embed.AddField(_settings.MainSettings.AdditionalEmbedFieldTitle, _settings.MainSettings.AdditionalEmbedFieldText);
            }

            //add the footer
            embed.WithFooter(_settings.MainSettings.BotTagline);

            //set the update time
            embed.WithCurrentTimestamp();

            //set the thumbnail
            embed.WithThumbnailUrl(_settings.MainSettings.GameImageURL);

            //create new component builder for buttons
            var builder = new ComponentBuilder();

            //if start button required, add it and set the state depending on the server status (disabled if ready or starting or installing/updating)
            if (_settings.MainSettings.ShowStartButton)
                if (application.State == ApplicationState.Ready || application.State == ApplicationState.Starting || application.State == ApplicationState.Installing)
                {
                    builder.WithButton("Start", "start-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Success, disabled: true);
                }
                else
                {
                    builder.WithButton("Start", "start-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Success, disabled: false);
                }

            //if stop button required, add it and set the state depending on the server status (disabled if stopped or failed)
            if (_settings.MainSettings.ShowStopButton)
                if (application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed)
                {
                    builder.WithButton("Stop", "stop-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: true);
                }
                else
                {
                    builder.WithButton("Stop", "stop-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: false);
                }

            //if restart button required, add it and set the state depending on the server status (disabled if stopper or failed)
            if (_settings.MainSettings.ShowRestartButton)
                if (application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed)
                {
                    builder.WithButton("Restart", "restart-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: true);
                }
                else
                {
                    builder.WithButton("Restart", "restart-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: false);
                }

            //if kill button required, add it and set the state depending on the server status (disabled if stopped or failed)
            if (_settings.MainSettings.ShowKillButton)
                if (application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed)
                {
                    builder.WithButton("Kill", "kill-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: true);
                }
                else
                {
                    builder.WithButton("Kill", "kill-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: false);
                }

            //if update button required, add it and set the state depending on the server status (disabled if installing/updating)
            if (_settings.MainSettings.ShowUpdateButton)
                if (application.State == ApplicationState.Installing)
                {
                    builder.WithButton("Update", "update-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Primary, disabled: true);
                }
                else
                {
                    builder.WithButton("Update", "update-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Primary, disabled: false);
                }

            //if manage button required, add it
            if (_settings.MainSettings.ShowManageButton)
                builder.WithButton("Manage", "manage-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Primary);

            //if the message already exists, try to update it
            if (updateExisting)
            {
                //cycle through each stored embed message ID (could be multiple across different channels/servers)
                foreach (string details in _settings.MainSettings.InfoMessageDetails)
                {
                    try
                    {
                        string[] split = details.Split('-');

                        //get the IUserMessage
                        var existingMsg = await _client
                            .GetGuild(Convert.ToUInt64(split[0]))
                            .GetTextChannel(Convert.ToUInt64(split[1]))
                            .GetMessageAsync(Convert.ToUInt64(split[2])) as IUserMessage;

                        //if it's not null then continue
                        if (existingMsg != null)
                        {
                            await existingMsg.ModifyAsync(x =>
                            {
                                x.Embed = embed.Build();
                                if (split.Length > 3)
                                {
                                    //check if it's configured as a buttonless panel
                                    if (split[3].ToString().Equals("True"))
                                    {
                                        //buttonless panel - do not build buttons
                                    }
                                    else
                                    {
                                        x.Components = builder.Build();
                                    }
                                }
                                else
                                {
                                    //panel created before buttonless option
                                    x.Components = builder.Build();
                                }
                            });
                        }
                        else
                        {
                            //message doesn't exist, remove from update list
                            _settings.MainSettings.InfoMessageDetails.Remove(details);
                        }
                    }
                    catch (Exception ex)
                    {
                        //error updating message
                        log.Error(ex.Message);
                    }
                }
            }
            //create a new embedded message and store the ID for updating later on
            else
            {
                var chnl = msg.Channel as SocketGuildChannel;
                var guild = chnl.Guild.Id;
                var channelID = msg.Channel.Id;

                //create the embed according to the request
                if (Buttonless)
                {
                    var message = await _client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build());
                    log.Debug("Message ID: " + message.Id.ToString());
                    _settings.MainSettings.InfoMessageDetails.Add(guild.ToString() + "-" + channelID.ToString() + "-" + message.Id.ToString() + "-" + Buttonless);
                }
                else
                {
                    var message = await _client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build(), components: builder.Build());
                    log.Debug("Message ID: " + message.Id.ToString());
                    _settings.MainSettings.InfoMessageDetails.Add(guild.ToString() + "-" + channelID.ToString() + "-" + message.Id.ToString() + "-" + Buttonless);
                }

                //save the newly added info message details
                _config.Save(_settings);
            }
        }

        private async Task UpdateWebPanel()
        {
            //Update web panel if it is enabled
            while (_settings.WebPanelSettings.enableWebPanel)
            {
                if (!Directory.Exists(webPanelPath))
                {
                    // Create the directory if it doesn't exist
                    Directory.CreateDirectory(webPanelPath);
                }

                // Define the file paths
                string scriptFilePath = Path.Combine(webPanelPath, "script.js");
                string stylesFilePath = Path.Combine(webPanelPath, "styles.css");
                string panelFilePath = Path.Combine(webPanelPath, "panel.html");

                // Write content to the files
                ResourceReader reader = new ResourceReader();

                File.WriteAllText(scriptFilePath, reader.ReadResource("script.js"));
                File.WriteAllText(stylesFilePath, reader.ReadResource("styles.css"));
                File.WriteAllText(panelFilePath, reader.ReadResource("panel.html"));

                // Read the template
                string htmlTemplate = reader.ReadResource("panel.html");

                //variables
                // Get the CPU usage and memory usage
                var cpuUsage = application.GetCPUUsage();
                var cpuUsageString = cpuUsage + "%";
                var memUsage = application.GetRAMUsage();

                //get count of players online and maximum slots
                IHasSimpleUserList hasSimpleUserList = application as IHasSimpleUserList;
                var onlinePlayers = hasSimpleUserList.Users.Count;
                var maximumPlayers = hasSimpleUserList.MaxUsers;

                //if server is online
                if (application.State == ApplicationState.Ready)
                {
                    htmlTemplate = htmlTemplate.Replace($"{{{{status}}}}", "✅ " + GetApplicationStateString());
                    htmlTemplate = htmlTemplate.Replace($"{{{{statusClass}}}}", "ready");
                }
                //if server is off or errored
                else if (application.State == ApplicationState.Failed || application.State == ApplicationState.Stopped)
                {
                    htmlTemplate = htmlTemplate.Replace($"{{{{status}}}}", "⛔ " + GetApplicationStateString());
                    htmlTemplate = htmlTemplate.Replace($"{{{{statusClass}}}}", "stopped");
                }
                //everything else
                else
                {
                    htmlTemplate = htmlTemplate.Replace($"{{{{status}}}}", "⏳ " + GetApplicationStateString());
                    htmlTemplate = htmlTemplate.Replace($"{{{{statusClass}}}}", "pending");
                }

                //set server name field
                htmlTemplate = htmlTemplate.Replace($"{{{{serverName}}}}", _settings.MainSettings.ServerDisplayName);

                //set server IP field
                htmlTemplate = htmlTemplate.Replace($"{{{{serverIP}}}}", _settings.MainSettings.ServerConnectionURL);

                //set CPU usage field
                htmlTemplate = htmlTemplate.Replace($"{{{{cpuUsage}}}}", application.GetCPUUsage() + "%");

                //set mem usage field
                htmlTemplate = htmlTemplate.Replace($"{{{{memoryUsage}}}}", application.GetRAMUsage().ToString("N0") + "MB");

                //if server is online, get the uptime info and set the field accordingly
                if (application.State == ApplicationState.Ready)
                {
                    TimeSpan uptime = DateTime.Now.Subtract(application.StartTime);
                    htmlTemplate = htmlTemplate.Replace($"{{{{uptime}}}}",string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D2}", uptime.Days, uptime.Hours, uptime.Minutes, uptime.Seconds));
                }

                //if there is a valid player count, show the online player count
                if (_settings.MainSettings.ValidPlayerCount)
                {
                    htmlTemplate = htmlTemplate.Replace($"{{{{playerCount}}}}", onlinePlayers + "/" + maximumPlayers);
                }

                try
                {
                    File.WriteAllText(panelFilePath, htmlTemplate);
                }
                catch(Exception ex)
                {
                    log.Error("Exception writing html:" + ex.Message);
                }
                // Delay the execution for 30 seconds
                await Task.Delay(30000);
            }
        }

        /// <summary>
        /// Show play time on the server
        /// </summary>
        /// <param name="msg">Command from Discord</param>
        /// <returns></returns>
        private async Task ShowPlayerPlayTime(SocketSlashCommand msg)
        {
            // Build the bot response
            var embed = new EmbedBuilder
            {
                Title = "Play Time Leaderboard",
                ThumbnailUrl = _settings.MainSettings.GameImageURL,
                Description = GetPlayTimeLeaderBoard(15, false, null, false),
                Color = !string.IsNullOrEmpty(_settings.ColourSettings.PlaytimeLeaderboardColour)
                    ? GetColour("Leaderboard", _settings.ColourSettings.PlaytimeLeaderboardColour)
                    : Color.DarkGrey
            };

            // Set the footer and the current timestamp
            embed.WithFooter(_settings.MainSettings.BotTagline)
                 .WithCurrentTimestamp();

            // Get the guild and channel IDs
            var guildId = (msg.Channel as SocketGuildChannel)?.Guild.Id;
            var channelId = msg.Channel.Id;

            // Post the leaderboard in the specified channel
            await _client.GetGuild(guildId.Value)?.GetTextChannel(channelId)?.SendMessageAsync(embed: embed.Build());
        }

        /// <summary>
        /// Looping task to update bot status/presence
        /// </summary>
        /// <returns></returns>
        public async Task SetStatus()
        {
            // While the bot is active, update its status
            while (_settings.MainSettings.BotActive)
            {
                try
                {
                    UserStatus status;

                    // If the server is stopped or in a failed state, set the presence to DoNotDisturb
                    if (application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed)
                    {
                        status = UserStatus.DoNotDisturb;

                        // If there are still players listed in the timer, remove them
                        if (playerPlayTimes.Count != 0)
                            ClearAllPlayTimes();
                    }
                    // If the server is running, set presence to Online
                    else if (application.State == ApplicationState.Ready)
                    {
                        status = UserStatus.Online;
                    }
                    // For everything else, set to Idle
                    else
                    {
                        status = UserStatus.Idle;

                        // If there are still players listed in the timer, remove them
                        if (playerPlayTimes.Count != 0)
                            ClearAllPlayTimes();
                    }

                    // Get the current user and max user count
                    IHasSimpleUserList hasSimpleUserList = application as IHasSimpleUserList;
                    var onlinePlayers = hasSimpleUserList.Users.Count;
                    var maximumPlayers = hasSimpleUserList.MaxUsers;

                    // Get the CPU usage and memory usage
                    var cpuUsage = application.GetCPUUsage();
                    var cpuUsageString = cpuUsage + "%";
                    var memUsage = application.GetRAMUsage();

                    // Get the name of the instance
                    var instanceName = platform.PlatformName;

                    log.Debug("Server Status: " + application.State + " || Players: " + onlinePlayers + "/" + maximumPlayers + " || CPU: " + application.GetCPUUsage() + "% || Memory: " + application.GetPhysicalRAMUsage() + "MB, Bot Connection Status: " + _client.ConnectionState);

                    // Set the presence/activity based on the server state
                    if (application.State == ApplicationState.Ready)
                    {
                        //await _client.SetGameAsync(OnlineBotPresenceString(onlinePlayers, maximumPlayers), null, ActivityType.CustomStatus);
                        await _client.SetActivityAsync(new CustomStatusGame(OnlineBotPresenceString(onlinePlayers, maximumPlayers)));
                    }
                    else
                    {
                        //await _client.SetGameAsync(application.State.ToString(), null, ActivityType.Playing);
                        await _client.SetActivityAsync(new CustomStatusGame(application.State.ToString()));
                    }

                    await _client.SetStatusAsync(status);

                    // Update the embed if it exists
                    if (_settings.MainSettings.InfoMessageDetails != null && _settings.MainSettings.InfoMessageDetails.Count > 0)
                    {
                        _ = GetServerInfo(true, null, false);
                    }
                }
                catch (System.Net.WebException exception)
                {
                    await _client.SetGameAsync("Server Offline", null, ActivityType.Watching);
                    await _client.SetStatusAsync(UserStatus.DoNotDisturb);
                    log.Info("Exception: " + exception.Message);
                }

                // Loop the task according to the bot refresh interval setting
                await Task.Delay(_settings.MainSettings.BotRefreshInterval * 1000);
            }
        }

        /// <summary>
        /// Task that runs when a button is pressed on the info panel
        /// </summary>
        /// <param name="arg">Component info of the button that was pressed</param>
        /// <returns></returns>
        private async Task OnButtonPress(SocketMessageComponent arg)
        {
            log.Debug("Button pressed: " + arg.Data.CustomId.ToString());

            // Check if the user that pressed the button has permission
            bool hasServerPermission = false;

            // Check if the user has the appropriate role
            if (arg.User is SocketGuildUser user)
            {
                _client.PurgeUserCache(); // Try to clear cache so we can get the latest roles
                hasServerPermission = !_settings.MainSettings.RestrictFunctions || user.Roles.Any(r => r.Name == _settings.MainSettings.DiscordRole);
            }

            if (!hasServerPermission)
            {
                // No permission, mark as responded and exit the method
                await arg.DeferAsync();
                return;
            }

            // Get the button ID without the instance ID suffix
            var buttonId = arg.Data.CustomId.Replace("-" + aMPInstanceInfo.InstanceId, "");

            // Perform the appropriate action based on the button ID
            switch (buttonId)
            {
                case "start-server":
                    application.Start();
                    break;
                case "stop-server":
                    application.Stop();
                    break;
                case "restart-server":
                    application.Restart();
                    break;
                case "kill-server":
                    application.Kill();
                    break;
                case "update-server":
                    application.Update();
                    break;
                case "manage-server":
                    await ManageServer(arg);
                    break;
                default:
                    // Invalid button ID, exit the method
                    return;
            }

            // Capitalize the first letter of the button response
            var capitalizedButtonResponse = char.ToUpper(buttonId[0]) + buttonId.Substring(1).Replace("-server", "");

            // Send button response
            await ButtonResponse(capitalizedButtonResponse, arg);
        }

        /// <summary>
        /// Sends a chat message to the specified text channel in each guild the bot is a member of.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ChatMessageSend(string Message)
        {
            // Get all guilds the bot is a member of
            var guilds = _client.Guilds;

            // Iterate over each guild
            foreach (SocketGuild guild in guilds)
            {
                // Find the text channel with the specified name
                var guildID = guild.Id;
                var eventChannel = GetEventChannel(guildID, _settings.MainSettings.ChatToDiscordChannel);

                if (eventChannel != null)
                {
                    // Send the message to the channel
                    await _client.GetGuild(guildID).GetTextChannel(eventChannel.Id).SendMessageAsync("`" + Message + "`");
                }
            }
        }

        /// <summary>
        /// Sends console output to the specified text channel in each guild the bot is a member of.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ConsoleOutputSend()
        {
            while (_settings.MainSettings.SendConsoleToDiscord && _settings.MainSettings.BotActive)
            {
                if (consoleOutput.Count > 0)
                {
                    try
                    {
                        // Create a duplicate list of console output messages
                        List<string> messages = new List<string>(consoleOutput);
                        consoleOutput.Clear();

                        // Split the output into multiple strings, each presented within a code block
                        List<string> outputStrings = SplitOutputIntoCodeBlocks(messages);

                        // Get all guilds the bot is a member of
                        var guilds = _client.Guilds;

                        // Iterate over each output string
                        foreach (string output in outputStrings)
                        {
                            // Iterate over each guild
                            foreach (SocketGuild guild in guilds)
                            {
                                // Find the text channel with the specified name
                                var guildID = guild.Id;
                                var eventChannel = GetEventChannel(guildID, _settings.MainSettings.ConsoleToDiscordChannel);

                                if (eventChannel != null)
                                {
                                    // Send the message to the channel
                                    await _client.GetGuild(guildID).GetTextChannel(eventChannel.Id).SendMessageAsync(output);
                                }
                            }
                        }

                        // Clear the duplicate list
                        messages.Clear();
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);
                    }
                }

                // Delay the execution for 10 seconds
                await Task.Delay(10000);
            }
        }

        private List<string> SplitOutputIntoCodeBlocks(List<string> messages)
        {
            const int MaxCodeBlockLength = 2000; // Maximum length of a code block in Discord

            List<string> outputStrings = new List<string>(); // List to store the split output strings

            string currentString = ""; // Current string being built
            foreach (string message in messages)
            {
                // Check if adding the next message will exceed the maximum code block length
                if (currentString.Length + Environment.NewLine.Length + message.Length + 6 > MaxCodeBlockLength)
                {
                    // Add the current string to the list of output strings
                    outputStrings.Add($"```{currentString}```");

                    // Reset the current string to start building a new one
                    currentString = "";
                }

                // Add the message to the current string, separated by a newline character
                if (!string.IsNullOrEmpty(currentString))
                {
                    currentString += Environment.NewLine;
                }
                currentString += message;
            }

            // Add the last current string to the list of output strings
            if (!string.IsNullOrEmpty(currentString))
            {
                outputStrings.Add($"```{currentString}```");
            }

            return outputStrings;
        }


        /// <summary>
        /// Handles button response and logs the command if enabled in settings.
        /// </summary>
        /// <param name="Command">Command received from the button.</param>
        /// <param name="arg">SocketMessageComponent object containing information about the button click.</param>
        private async Task ButtonResponse(string Command, SocketMessageComponent arg)
        {
            // Only log if option is enabled
            if (_settings.MainSettings.LogButtonsAndCommands)
            {
                var embed = new EmbedBuilder();

                if (Command == "Manage")
                {
                    embed.Title = "Manage Request";
                    embed.Description = "Manage URL Request Received";
                }
                else
                {
                    embed.Title = "Server Command Sent";
                    embed.Description = $"{Command} command has been sent to the {application.ApplicationName} server.";
                }

                // Start command
                if (Command.Equals("Start"))
                {
                    embed.Color = !string.IsNullOrEmpty(_settings.ColourSettings.ServerStartColour)
                        ? GetColour("Start", _settings.ColourSettings.ServerStartColour)
                        : Color.Green;
                }
                // Stop command
                else if (Command.Equals("Stop"))
                {
                    embed.Color = !string.IsNullOrEmpty(_settings.ColourSettings.ServerStopColour)
                        ? GetColour("Stop", _settings.ColourSettings.ServerStopColour)
                        : Color.Red;
                }
                // Restart command
                else if (Command.Equals("Restart"))
                {
                    embed.Color = !string.IsNullOrEmpty(_settings.ColourSettings.ServerRestartColour)
                        ? GetColour("Restart", _settings.ColourSettings.ServerRestartColour)
                        : Color.Orange;
                }
                // Kill command
                else if (Command.Equals("Kill"))
                {
                    embed.Color = !string.IsNullOrEmpty(_settings.ColourSettings.ServerKillColour)
                        ? GetColour("Kill", _settings.ColourSettings.ServerKillColour)
                        : Color.Red;
                }
                // Update command
                else if (Command.Equals("Update"))
                {
                    embed.Color = !string.IsNullOrEmpty(_settings.ColourSettings.ServerUpdateColour)
                        ? GetColour("Update", _settings.ColourSettings.ServerUpdateColour)
                        : Color.Blue;
                }
                // Manage command
                else if (Command.Equals("Manage"))
                {
                    embed.Color = !string.IsNullOrEmpty(_settings.ColourSettings.ManageLinkColour)
                        ? GetColour("Manage", _settings.ColourSettings.ManageLinkColour)
                        : Color.Blue;
                }

                embed.ThumbnailUrl = _settings.MainSettings.GameImageURL;
                embed.AddField("Requested by", arg.User.Mention, true);
                embed.WithFooter(_settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();

                var chnl = arg.Message.Channel as SocketGuildChannel;
                var guild = chnl.Guild.Id;
                var logChannel = GetEventChannel(guild, _settings.MainSettings.ButtonResponseChannel);
                var channelID = arg.Message.Channel.Id;

                if (logChannel != null)
                    channelID = logChannel.Id;

                log.Debug($"Guild: {guild} || Channel: {channelID}");

                await _client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build());
            }

            await arg.DeferAsync();
        }

        /// <summary>
        /// Retrieves the color based on the given command and hexadecimal color code.
        /// </summary>
        /// <param name="command">Command associated with the color.</param>
        /// <param name="hexColour">Hexadecimal color code.</param>
        /// <returns>The Color object corresponding to the command and color code.</returns>
        private Color GetColour(string command, string hexColour)
        {
            try
            {
                // Remove '#' if it's present in the string
                string tmp = hexColour.Replace("#", "");

                // Convert to uint
                uint colourCode = uint.Parse(tmp, System.Globalization.NumberStyles.HexNumber);

                return new Color(colourCode);
            }
            catch
            {
                // Log an error message when the color code is invalid and revert to default color

                log.Info($"Colour code for {command} is invalid, reverting to default");

                // Return default colors based on the command
                switch (command)
                {
                    case "Info":
                        return Color.DarkGrey;
                    case "Start":
                    case "PlayerJoin":
                        return Color.Green;
                    case "Stop":
                    case "Kill":
                    case "PlayerLeave":
                        return Color.Red;
                    case "Restart":
                        return Color.Orange;
                    case "Update":
                    case "Manage":
                        return Color.Blue;
                    case "Console":
                        return Color.DarkGreen;
                    case "Leaderboard":
                        return Color.DarkGrey;
                }
            }

            return Color.DarkerGrey;
        }

        /// <summary>
        /// Sends a command response with an embed message.
        /// </summary>
        /// <param name="command">The command received.</param>
        /// <param name="arg">The received command arguments.</param>
        private async Task CommandResponse(string command, SocketSlashCommand arg)
        {
            // Only log if option is enabled
            if (!_settings.MainSettings.LogButtonsAndCommands)
                return;

            var embed = new EmbedBuilder();

            // Set the title and description of the embed based on the command
            if (command == "Manage")
            {
                embed.Title = "Manage Request";
                embed.Description = "Manage URL Request Received";
            }
            else
            {
                embed.Title = "Server Command Sent";
                embed.Description = $"{command} command has been sent to the {application.ApplicationName} server.";
            }

            // Set the embed color based on the command
            if (command.Equals("Start Server"))
            {
                embed.Color = GetColour("Start", _settings.ColourSettings.ServerStartColour);
                if (embed.Color == null)
                    embed.Color = Color.Green;
            }
            else if (command.Equals("Stop Server"))
            {
                embed.Color = GetColour("Stop", _settings.ColourSettings.ServerStopColour);
                if (embed.Color == null)
                    embed.Color = Color.Red;
            }
            else if (command.Equals("Restart Server"))
            {
                embed.Color = GetColour("Restart", _settings.ColourSettings.ServerRestartColour);
                if (embed.Color == null)
                    embed.Color = Color.Orange;
            }
            else if (command.Equals("Kill Server"))
            {
                embed.Color = GetColour("Kill", _settings.ColourSettings.ServerKillColour);
                if (embed.Color == null)
                    embed.Color = Color.Red;
            }
            else if (command.Equals("Update Server"))
            {
                embed.Color = GetColour("Update", _settings.ColourSettings.ServerUpdateColour);
                if (embed.Color == null)
                    embed.Color = Color.Blue;
            }
            else if (command.Equals("Manage Server"))
            {
                embed.Color = GetColour("Manage", _settings.ColourSettings.ManageLinkColour);
                if (embed.Color == null)
                    embed.Color = Color.Blue;
            }
            else if (command.Contains("console"))
            {
                embed.Color = GetColour("Console", _settings.ColourSettings.ConsoleCommandColour);
                if (embed.Color == null)
                    embed.Color = Color.DarkGreen;
            }

            embed.ThumbnailUrl = _settings.MainSettings.GameImageURL;
            embed.AddField("Requested by", arg.User.Mention, true);
            embed.WithFooter(_settings.MainSettings.BotTagline);
            embed.WithCurrentTimestamp();

            var chnl = arg.Channel as SocketGuildChannel;
            var guild = chnl.Guild.Id;
            var logChannel = GetEventChannel(guild, _settings.MainSettings.ButtonResponseChannel);
            var channelID = arg.Channel.Id;

            if (logChannel != null)
                channelID = logChannel.Id;

            log.Debug($"Guild: {guild} || Channel: {channelID}");

            await _client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build());
        }

        /// <summary>
        /// Event handler for when a user joins the server.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="args">The event arguments containing user information.</param>
        private async void UserJoins(object sender, UserEventArgs args)
        {
            // Remove the player from the list if they are already present
            playerPlayTimes.RemoveAll(p => p.PlayerName == args.User.Name);

            // Log the join time for the player
            playerPlayTimes.Add(new PlayerPlayTime() { PlayerName = args.User.Name, JoinTime = DateTime.Now });

            // Initialize play time and last seen information for the user if it doesn't exist
            if (!_settings.MainSettings.PlayTime.ContainsKey(args.User.Name))
            {
                _settings.MainSettings.PlayTime.Add(args.User.Name, TimeSpan.Zero);
                _settings.MainSettings.LastSeen.Add(args.User.Name, DateTime.Now);
                _config.Save(_settings);
            }

            // Check if posting player events is disabled
            if (!_settings.MainSettings.PostPlayerEvents)
                return;

            foreach (SocketGuild socketGuild in _client.Guilds)
            {
                var guildID = socketGuild.Id;
                var eventChannel = GetEventChannel(guildID, _settings.MainSettings.PostPlayerEventsChannel);

                if (eventChannel == null)
                    break; // Event channel doesn't exist, stop processing

                string userName = args.User.Name;

                // Build the embed message for the event
                var embed = new EmbedBuilder
                {
                    Title = "Server Event",
                    ThumbnailUrl = _settings.MainSettings.GameImageURL
                };

                if (string.IsNullOrEmpty(userName))
                {
                    embed.Description = "A player joined the " + application.ApplicationName + " server.";
                }
                else
                {
                    embed.Description = userName + " joined the " + application.ApplicationName + " server.";
                }

                // Set the embed color based on the configuration
                if (!string.IsNullOrEmpty(_settings.ColourSettings.ServerPlayerJoinEventColour))
                {
                    embed.Color = GetColour("PlayerJoin", _settings.ColourSettings.ServerPlayerJoinEventColour);
                }
                else
                {
                    embed.Color = Color.Green;
                }

                embed.WithFooter(_settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();
                await _client.GetGuild(guildID).GetTextChannel(eventChannel.Id).SendMessageAsync(embed: embed.Build());
            }
        }

        /// <summary>
        /// Event handler for when a user leaves the server.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="args">The event arguments containing user information.</param>
        private async void UserLeaves(object sender, UserEventArgs args)
        {
            try
            {
                // Add leave time for the player
                playerPlayTimes.Find(p => p.PlayerName == args.User.Name).LeaveTime = DateTime.Now;

                // Check if the player's entry exists in the playtime dictionary, if not, add a new entry
                if (!_settings.MainSettings.PlayTime.ContainsKey(args.User.Name))
                    _settings.MainSettings.PlayTime.Add(args.User.Name, TimeSpan.Zero);

                // Calculate the session playtime for the player
                TimeSpan sessionPlayTime = playerPlayTimes.Find(p => p.PlayerName == args.User.Name).LeaveTime - playerPlayTimes.Find(p => p.PlayerName == args.User.Name).JoinTime;

                // Update the main playtime list
                _settings.MainSettings.PlayTime[args.User.Name] += sessionPlayTime;
                _settings.MainSettings.LastSeen[args.User.Name] = DateTime.Now;
                _config.Save(_settings);

                // Remove the player from the 'live' list
                playerPlayTimes.RemoveAll(p => p.PlayerName == args.User.Name);
            }
            catch (Exception exception)
            {
                log.Error(exception.Message);
            }

            // Check if posting player events is disabled
            if (!_settings.MainSettings.PostPlayerEvents)
                return;

            foreach (SocketGuild socketGuild in _client.Guilds)
            {
                var guildID = socketGuild.Id;
                var eventChannel = GetEventChannel(guildID, _settings.MainSettings.PostPlayerEventsChannel);
                if (eventChannel == null)
                    return; // Event channel doesn't exist, stop processing

                string userName = args.User.Name;

                // Build the embed message for the event
                var embed = new EmbedBuilder
                {
                    Title = "Server Event",
                    ThumbnailUrl = _settings.MainSettings.GameImageURL
                };

                if (string.IsNullOrEmpty(userName))
                {
                    embed.Description = "A player left the " + application.ApplicationName + " server.";
                }
                else
                {
                    embed.Description = userName + " left the " + application.ApplicationName + " server.";
                }

                // Set the embed color based on the configuration
                if (!string.IsNullOrEmpty(_settings.ColourSettings.ServerPlayerLeaveEventColour))
                {
                    embed.Color = GetColour("PlayerLeave", _settings.ColourSettings.ServerPlayerLeaveEventColour);
                }
                else
                {
                    embed.Color = Color.Red;
                }

                embed.WithFooter(_settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();
                await _client.GetGuild(guildID).GetTextChannel(eventChannel.Id).SendMessageAsync(embed: embed.Build());
            }
        }

        /// <summary>
        /// Manages the server by sending a private message to the user with a link to the management panel.
        /// </summary>
        /// <param name="arg">The SocketMessageComponent argument.</param>
        private async Task ManageServer(SocketMessageComponent arg)
        {
            var builder = new ComponentBuilder();
            string managementProtocol = "http://";

            // Check if SSL is enabled in the main settings and update the managementProtocol accordingly
            if (_settings.MainSettings.ManagementURLSSL)
            {
                managementProtocol = "https://";
            }

            // Build the button with the management panel link using the appropriate protocol and instance ID
            string managementPanelLink = $"{managementProtocol}{_settings.MainSettings.ManagementURL}/?instance={aMPInstanceInfo.InstanceId}";
            builder.WithButton("Manage Server", style: ButtonStyle.Link, url: managementPanelLink);

            // Send a private message to the user with the link to the management panel
            await arg.User.SendMessageAsync("Link to management panel:", components: builder.Build());
        }

        /// <summary>
        /// Gets the string representation of the application state, with the option to use a replacement value if available.
        /// </summary>
        /// <returns>The string representation of the application state.</returns>
        private string GetApplicationStateString()
        {
            // Check if a replacement value exists for the current application state
            if (_settings.MainSettings.ChangeStatus.ContainsKey(application.State.ToString()))
            {
                // Return the replacement value
                return _settings.MainSettings.ChangeStatus[application.State.ToString()];
            }

            // No replacement value exists, so return the default value (the application state as string)
            return application.State.ToString();
        }

        /// <summary>
        /// Generates the string representation of the bot's online presence based on the number of online players and maximum players.
        /// </summary>
        /// <param name="onlinePlayers">The number of online players.</param>
        /// <param name="maximumPlayers">The maximum number of players.</param>
        /// <returns>The string representation of the bot's online presence.</returns>
        private string OnlineBotPresenceString(int onlinePlayers, int maximumPlayers)
        {
            // Check if there is a valid player count and no custom value
            if (_settings.MainSettings.OnlineBotPresence == "" && _settings.MainSettings.ValidPlayerCount)
            {
                // Return the default representation of online players and maximum players
                return $"{onlinePlayers}/{maximumPlayers} players";
            }

            // Check if there is no valid player count and no custom value
            if (_settings.MainSettings.OnlineBotPresence == "")
            {
                // Return the default "Online" presence
                return "Online";
            }

            // Get the custom value for the online bot presence
            string presence = _settings.MainSettings.OnlineBotPresence;

            // Replace variables in the custom value with the actual values
            presence = presence.Replace("{OnlinePlayers}", onlinePlayers.ToString());
            presence = presence.Replace("{MaximumPlayers}", maximumPlayers.ToString());

            return presence;
        }

        /// <summary>
        /// Generates a leaderboard of players based on their playtime.
        /// </summary>
        /// <param name="placesToShow">The number of leaderboard positions to show.</param>
        /// <param name="playerSpecific">Flag indicating if the leaderboard is player-specific.</param>
        /// <param name="playerName">The name of the player (used when playerSpecific is true).</param>
        /// <param name="fullList">Flag indicating if the full leaderboard list should be shown.</param>
        /// <returns>The string representation of the playtime leaderboard.</returns>
        private string GetPlayTimeLeaderBoard(int placesToShow, bool playerSpecific, string playerName, bool fullList)
        {
            // Create a new dictionary to hold the logged playtime plus any current session time
            Dictionary<string, TimeSpan> playtime = new Dictionary<string, TimeSpan>(_settings.MainSettings.PlayTime);

            // Calculate current session time for each player and add it to the logged playtime
            foreach (PlayerPlayTime player in playerPlayTimes)
            {
                TimeSpan currentSession = DateTime.Now - player.JoinTime;
                playtime[player.PlayerName] = playtime[player.PlayerName].Add(currentSession);
            }

            // Sort the playtime dictionary in descending order of playtime
            var sortedList = playtime.OrderByDescending(v => v.Value).ToList();

            // If no playtime is logged yet, return a message indicating so
            if (sortedList.Count == 0)
            {
                return "```No play time logged yet```";
            }

            if (playerSpecific)
            {
                // Check if the specified player is found in the leaderboard
                if (sortedList.FindAll(p => p.Key == playerName).Count > 0)
                {
                    TimeSpan time = sortedList.Find(p => p.Key == playerName).Value;

                    // Return the playtime, position, and last seen information for the specific player
                    return $"`{time.Days}d {time.Hours}h {time.Minutes}m {time.Seconds}s, position {(sortedList.FindIndex(p => p.Key == playerName) + 1)}, last seen {GetLastSeen(playerName)}`";
                }
                else
                {
                    // If the specified player is not found in the leaderboard, return a message indicating so
                    return "```No play time logged yet```";
                }
            }
            else
            {
                string leaderboard = "```";
                int position = 1;

                if (fullList)
                {
                    leaderboard += $"{string.Format("{0,-4}{1,-20}{2,-15}{3,-30}", "Pos", "Player Name", "Play Time", "Last Seen")}{Environment.NewLine}";
                }

                // Generate the leaderboard string with the specified number of positions to show
                foreach (KeyValuePair<string, TimeSpan> player in sortedList)
                {
                    // If outside the specified places to show, stop processing
                    if (position > placesToShow)
                        break;

                    if (fullList)
                    {
                        leaderboard += $"{string.Format("{0,-4}{1,-20}{2,-15}{3,-30}", position + ".", player.Key, string.Format("{0}d {1}h {2}m {3}s", player.Value.Days, player.Value.Hours, player.Value.Minutes, player.Value.Seconds), GetLastSeen(player.Key))}{Environment.NewLine}";
                    }
                    else
                    {
                        leaderboard += $"{string.Format("{0,-4}{1,-20}{2,-15}", position + ".", player.Key, string.Format("{0}d {1}h {2}m {3}s", player.Value.Days, player.Value.Hours, player.Value.Minutes, player.Value.Seconds))}{Environment.NewLine}";
                    }
                    position++;
                }

                leaderboard += "```";

                return leaderboard;
            }
        }

        /// <summary>
        /// Gets the last seen timestamp for a player.
        /// </summary>
        /// <param name="playerName">The name of the player.</param>
        /// <returns>The last seen timestamp.</returns>
        private string GetLastSeen(string playerName)
        {
            IHasSimpleUserList hasSimpleUserList = application as IHasSimpleUserList;
            bool playerOnline = false;

            // Check if the player is online by iterating through the user list
            foreach (SimpleUser user in hasSimpleUserList.Users)
            {
                if (user.Name == playerName)
                {
                    playerOnline = true;
                }
            }

            string lastSeen;

            if (playerOnline)
            {
                // If the player is online, set the last seen timestamp to the current time
                lastSeen = DateTime.Now.ToString("dddd, dd MMMM yyyy HH:mm:ss");
            }
            else
            {
                try
                {
                    // Get the last seen timestamp from the settings if available
                    lastSeen = _settings.MainSettings.LastSeen[playerName].ToString("dddd, dd MMMM yyyy HH:mm:ss");
                }
                catch (KeyNotFoundException)
                {
                    // If the player is not found in the last seen list, set the last seen timestamp to "N/A"
                    lastSeen = "N/A";
                }
            }

            return lastSeen;
        }

        /// <summary>
        /// Clears all playtimes and updates the main playtime list and last seen timestamps.
        /// </summary>
        private void ClearAllPlayTimes()
        {
            try
            {
                // Iterate through the playerPlayTimes list
                foreach (PlayerPlayTime playerPlayTime in playerPlayTimes)
                {
                    log.Debug("Saving playtime for " + playerPlayTime.PlayerName);

                    // Set the leave time as now
                    playerPlayTime.LeaveTime = DateTime.Now;

                    // Check if the player is already in the playtime list, if not, add a new entry
                    if (!_settings.MainSettings.PlayTime.ContainsKey(playerPlayTime.PlayerName))
                    {
                        _settings.MainSettings.PlayTime.Add(playerPlayTime.PlayerName, new TimeSpan(0));
                    }

                    // Calculate the session playtime
                    TimeSpan sessionPlayTime = playerPlayTime.LeaveTime - playerPlayTime.JoinTime;

                    // Update the main playtime list by adding the session playtime
                    _settings.MainSettings.PlayTime[playerPlayTime.PlayerName] += sessionPlayTime;

                    // Update the last seen timestamp for the player
                    _settings.MainSettings.LastSeen[playerPlayTime.PlayerName] = DateTime.Now;

                    // Save the updated settings to the configuration file
                    _config.Save(_settings);
                }

                // Clear the playerPlayTimes list
                playerPlayTimes.Clear();
            }
            catch (Exception exception)
            {
                log.Debug(exception.Message);
            }
        }

        /// <summary>
        /// Sets up and registers application commands for the client.
        /// </summary>
        public async Task ClientReady()
        {
            // Create lists to store command properties and command builders
            List<ApplicationCommandProperties> applicationCommandProperties = new List<ApplicationCommandProperties>();
            List<SlashCommandBuilder> commandList = new List<SlashCommandBuilder>();

            if (_settings.MainSettings.RemoveBotName)
            {
                // Add individual commands to the command list
                commandList.Add(new SlashCommandBuilder()
                    .WithName("info")
                    .WithDescription("Create the Server Info Panel")
                    .AddOption("nobuttons", ApplicationCommandOptionType.Boolean, "Hide buttons for this panel?", isRequired: false));

                commandList.Add(new SlashCommandBuilder()
                    .WithName("start-server")
                    .WithDescription("Start the Server"));

                commandList.Add(new SlashCommandBuilder()
                    .WithName("stop-server")
                    .WithDescription("Stop the Server"));

                commandList.Add(new SlashCommandBuilder()
                    .WithName("restart-server")
                    .WithDescription("Restart the Server"));

                commandList.Add(new SlashCommandBuilder()
                    .WithName("kill-server")
                    .WithDescription("Kill the Server"));

                commandList.Add(new SlashCommandBuilder()
                    .WithName("update-server")
                    .WithDescription("Update the Server"));

                commandList.Add(new SlashCommandBuilder()
                    .WithName("show-playtime")
                    .WithDescription("Show the Playtime Leaderboard")
                    .AddOption("playername", ApplicationCommandOptionType.String, "Get playtime for a specific player", isRequired: false));

                commandList.Add(new SlashCommandBuilder()
                    .WithName("console")
                    .WithDescription("Send a Console Command to the Application")
                    .AddOption("value", ApplicationCommandOptionType.String, "Command text", isRequired: true));

                commandList.Add(new SlashCommandBuilder()
                    .WithName("full-playtime-list")
                    .WithDescription("Full Playtime List")
                    .AddOption("playername", ApplicationCommandOptionType.String, "Get info for a specific player", isRequired: false));
            }
            else
            {
                // Get bot name for command
                string botName = _client.CurrentUser.Username.ToLower();

                // Replace any spaces with '-'
                botName = Regex.Replace(botName, "[^a-zA-Z0-9]", String.Empty);

                log.Info("Base command for bot: " + botName);

                // Create the base bot command with subcommands
                SlashCommandBuilder baseCommand = new SlashCommandBuilder()
                    .WithName(botName)
                    .WithDescription("Base bot command");

                // Add subcommands to the base command
                baseCommand.AddOption(new SlashCommandOptionBuilder()
                    .WithName("info")
                    .WithDescription("Create the Server Info Panel")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("nobuttons", ApplicationCommandOptionType.Boolean, "Hide buttons for this panel?", isRequired: false))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("start-server")
                    .WithDescription("Start the Server")
                    .WithType(ApplicationCommandOptionType.SubCommand))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("stop-server")
                    .WithDescription("Stop the Server")
                    .WithType(ApplicationCommandOptionType.SubCommand))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("restart-server")
                    .WithDescription("Restart the Server")
                    .WithType(ApplicationCommandOptionType.SubCommand))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("kill-server")
                    .WithDescription("Kill the Server")
                    .WithType(ApplicationCommandOptionType.SubCommand))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("update-server")
                    .WithDescription("Update the Server")
                    .WithType(ApplicationCommandOptionType.SubCommand))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("show-playtime")
                    .WithDescription("Show the Playtime Leaderboard")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("playername", ApplicationCommandOptionType.String, "Get playtime for a specific player", isRequired: false))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("console")
                    .WithDescription("Send a Console Command to the Application")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("value", ApplicationCommandOptionType.String, "Command text", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("full-playtime-list")
                    .WithDescription("Full Playtime List")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("playername", ApplicationCommandOptionType.String, "Get info for a specific player", isRequired: false));

                // Add the base command to the command list
                commandList.Add(baseCommand);
            }

            try
            {
                // Build the application command properties from the command builders
                foreach (SlashCommandBuilder command in commandList)
                {
                    applicationCommandProperties.Add(command.Build());
                }

                // Bulk overwrite the global application commands with the built command properties
                await _client.BulkOverwriteGlobalApplicationCommandsAsync(applicationCommandProperties.ToArray());
            }
            catch (HttpException exception)
            {
                log.Error(exception.Message);
            }
        }

        /// <summary>
        /// Handles incoming socket messages.
        /// </summary>
        /// <param name="message">The incoming socket message.</param>
        private async Task MessageHandler(SocketMessage message)
        {
            // If sending Discord chat to server is disabled or the message is from a bot, return and do nothing further
            if (!_settings.MainSettings.SendDiscordChatToServer || message.Author.IsBot)
                return;

            // Check if the message is in the specified chat-to-Discord channel
            if (message.Channel.Name.Equals(_settings.MainSettings.ChatToDiscordChannel))
            {
                // Send the chat command to the server
                await SendChatCommand(message.Author.Username, message.CleanContent);
            }
        }

        /// <summary>
        /// Handles incoming socket slash commands.
        /// </summary>
        /// <param name="command">The incoming socket slash command.</param>
        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            // Using bot name as the base command
            if (!_settings.MainSettings.RemoveBotName)
            {
                // Leaderboard permissionless
                if (command.Data.Options.First().Name.Equals("show-playtime"))
                {
                    if (command.Data.Options.First().Options.Count > 0)
                    {
                        string playerName = command.Data.Options.First().Options.First().Value.ToString();
                        string playTime = GetPlayTimeLeaderBoard(1, true, playerName, false);
                        await command.RespondAsync("Playtime for " + playerName + ": " + playTime, ephemeral: true);
                    }
                    else
                    {
                        await ShowPlayerPlayTime(command);
                        await command.RespondAsync("Playtime leaderboard displayed", ephemeral: true);
                    }
                    return;
                }

                // Initialize bool for permission check
                bool hasServerPermission = false;

                if (command.User is SocketGuildUser user)
                {
                    // The user has the permission if either RestrictFunctions is turned off, or if they are part of the appropriate role.
                    hasServerPermission = !_settings.MainSettings.RestrictFunctions || user.Roles.Any(r => r.Name == _settings.MainSettings.DiscordRole);
                }

                if (!hasServerPermission)
                {
                    await command.RespondAsync("You do not have permission to use this command!", ephemeral: true);
                    return;
                }

                switch (command.Data.Options.First().Name)
                {
                    case "info":
                        bool buttonless = command.Data.Options.First().Options.Count > 0 && Convert.ToBoolean(command.Data.Options.First().Options.First().Value.ToString());
                        await GetServerInfo(false, command, buttonless);
                        await command.RespondAsync("Info panel created", ephemeral: true);
                        break;
                    case "start-server":
                        application.Start();
                        await CommandResponse("Start Server", command);
                        await command.RespondAsync("Start command sent to the application", ephemeral: true);
                        break;
                    case "stop-server":
                        application.Stop();
                        await CommandResponse("Stop Server", command);
                        await command.RespondAsync("Stop command sent to the application", ephemeral: true);
                        break;
                    case "restart-server":
                        application.Restart();
                        await CommandResponse("Restart Server", command);
                        await command.RespondAsync("Restart command sent to the application", ephemeral: true);
                        break;
                    case "kill-server":
                        application.Restart();
                        await CommandResponse("Kill Server", command);
                        await command.RespondAsync("Kill command sent to the application", ephemeral: true);
                        break;
                    case "update-server":
                        application.Update();
                        await CommandResponse("Update Server", command);
                        await command.RespondAsync("Update command sent to the application", ephemeral: true);
                        break;
                    case "console":
                        await SendConsoleCommand(command);
                        string consoleCommand = command.Data.Options.First().Options.First().Value.ToString();
                        await CommandResponse("`" + consoleCommand + "` console ", command);
                        await command.RespondAsync("Command sent to the server: `" + consoleCommand + "`", ephemeral: true);
                        break;
                    case "full-playtime-list":
                        if (command.Data.Options.First().Options.Count > 0)
                        {
                            string playerName = command.Data.Options.First().Options.First().Value.ToString();
                            string playTime = GetPlayTimeLeaderBoard(1, true, playerName, true);
                            await command.RespondAsync("Playtime for " + playerName + ": " + playTime, ephemeral: true);
                        }
                        else
                        {
                            string playTime = GetPlayTimeLeaderBoard(1000, false, null, true);
                            if (playTime.Length > 2000)
                            {
                                string path = Path.Combine(application.BaseDirectory, "full-playtime-list.txt");
                                try
                                {
                                    playTime = playTime.Replace("```", "");
                                    using (FileStream fileStream = File.Create(path))
                                    {
                                        byte[] text = new UTF8Encoding(true).GetBytes(playTime);
                                        fileStream.Write(text, 0, text.Length);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    log.Error("Error creating file: " + ex.Message);
                                }

                                await command.RespondWithFileAsync(path, ephemeral: true);

                                try
                                {
                                    File.Delete(path);
                                }
                                catch (Exception ex)
                                {
                                    log.Error("Error deleting file: " + ex.Message);
                                }
                            }
                            else
                            {
                                await command.RespondAsync(playTime, ephemeral: true);
                            }

                            // await command.User.SendMessageAsync(playTime);
                        }
                        break;
                }
            }
            else
            {
                // No bot prefix
                // Leaderboard permissionless
                if (command.Data.Name.Equals("show-playtime"))
                {
                    if (command.Data.Options.Count > 0)
                    {
                        string playerName = command.Data.Options.First().Value.ToString();
                        string playTime = GetPlayTimeLeaderBoard(1, true, playerName, false);
                        await command.RespondAsync("Playtime for " + playerName + ": " + playTime, ephemeral: true);
                    }
                    else
                    {
                        await ShowPlayerPlayTime(command);
                        await command.RespondAsync("Playtime leaderboard displayed", ephemeral: true);
                    }
                    return;
                }

                // Initialize bool for permission check
                bool hasServerPermission = false;

                if (command.User is SocketGuildUser user)
                {
                    // The user has the permission if either RestrictFunctions is turned off, or if they are part of the appropriate role.
                    hasServerPermission = !_settings.MainSettings.RestrictFunctions || user.Roles.Any(r => r.Name == _settings.MainSettings.DiscordRole);
                }

                if (!hasServerPermission)
                {
                    await command.RespondAsync("You do not have permission to use this command!", ephemeral: true);
                    return;
                }

                switch (command.Data.Name)
                {
                    case "info":
                        bool buttonless = command.Data.Options.Count > 0 && Convert.ToBoolean(command.Data.Options.First().Value.ToString());
                        await GetServerInfo(false, command, buttonless);
                        await command.RespondAsync("Info panel created", ephemeral: true);
                        break;
                    case "start-server":
                        application.Start();
                        await CommandResponse("Start Server", command);
                        await command.RespondAsync("Start command sent to the application", ephemeral: true);
                        break;
                    case "stop-server":
                        application.Stop();
                        await CommandResponse("Stop Server", command);
                        await command.RespondAsync("Stop command sent to the application", ephemeral: true);
                        break;
                    case "restart-server":
                        application.Restart();
                        await CommandResponse("Restart Server", command);
                        await command.RespondAsync("Restart command sent to the application", ephemeral: true);
                        break;
                    case "kill-server":
                        application.Restart();
                        await CommandResponse("Kill Server", command);
                        await command.RespondAsync("Kill command sent to the application", ephemeral: true);
                        break;
                    case "update-server":
                        application.Update();
                        await CommandResponse("Update Server", command);
                        await command.RespondAsync("Update command sent to the application", ephemeral: true);
                        break;
                    case "console":
                        await SendConsoleCommand(command);
                        await CommandResponse("`" + command.Data.Options.First().Value.ToString() + "` console ", command);
                        await command.RespondAsync("Command sent to the server: `" + command.Data.Options.First().Value.ToString() + "`", ephemeral: true);
                        break;
                    case "full-playtime-list":
                        if (command.Data.Options.Count > 0)
                        {
                            string playerName = command.Data.Options.First().Value.ToString();
                            string playTime = GetPlayTimeLeaderBoard(1, true, playerName, true);
                            await command.RespondAsync("Playtime for " + playerName + ": " + playTime, ephemeral: true);
                        }
                        else
                        {
                            string playTime = GetPlayTimeLeaderBoard(1000, false, null, true);
                            if (playTime.Length > 2000)
                            {
                                string path = Path.Combine(application.BaseDirectory, "full-playtime-list.txt");
                                try
                                {
                                    playTime = playTime.Replace("```", "");
                                    using (FileStream fileStream = File.Create(path))
                                    {
                                        byte[] text = new UTF8Encoding(true).GetBytes(playTime);
                                        fileStream.Write(text, 0, text.Length);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    log.Error("Error creating file: " + ex.Message);
                                }

                                await command.RespondWithFileAsync(path, ephemeral: true);

                                try
                                {
                                    File.Delete(path);
                                }
                                catch (Exception ex)
                                {
                                    log.Error("Error deleting file: " + ex.Message);
                                }
                            }
                            else
                            {
                                await command.RespondAsync(playTime, ephemeral: true);
                            }

                            // await command.User.SendMessageAsync(playTime);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Represents player playtime information.
        /// </summary>
        public class PlayerPlayTime
        {
            public string PlayerName { get; set; }
            public DateTime JoinTime { get; set; }
            public DateTime LeaveTime { get; set; }
        }

        /// <summary>
        /// Retrieves the event channel from the specified guild by ID or name.
        /// </summary>
        /// <param name="guildID">The ID of the guild.</param>
        /// <param name="channel">The ID or name of the channel.</param>
        /// <returns>The event channel if found; otherwise, null.</returns>
        private SocketGuildChannel GetEventChannel(ulong guildID, string channel)
        {
            SocketGuildChannel eventChannel;

            // Try by ID first
            try
            {
                eventChannel = _client.GetGuild(guildID).Channels.FirstOrDefault(x => x.Id == Convert.ToUInt64(channel));
            }
            catch
            {
                // If the ID retrieval fails, try by name
                eventChannel = _client.GetGuild(guildID).Channels.FirstOrDefault(x => x.Name == channel);
            }

            return eventChannel;
        }
    }
}

public class CommandHandler
{
    private readonly DiscordSocketClient _client;
    private readonly CommandService _commands;

    // Retrieve client and CommandService instance via ctor
    public CommandHandler(DiscordSocketClient client, CommandService commands)
    {
        _commands = commands;
        _client = client;
    }

    public async Task InstallCommandsAsync()
    {
        // Hook the MessageReceived event into our command handler
        _client.MessageReceived += HandleCommandAsync;

        // Here we discover all of the command modules in the entry 
        // assembly and load them. Starting from Discord.NET 2.0, a
        // service provider is required to be passed into the
        // module registration method to inject the 
        // required dependencies.
        //
        // If you do not use Dependency Injection, pass null.
        // See Dependency Injection guide for more information.
        await _commands.AddModulesAsync(assembly: Assembly.GetEntryAssembly(),
                                        services: null);
    }

    private async Task HandleCommandAsync(SocketMessage messageParam)
    {
        // Don't process the command if it was a system message
        var message = messageParam as SocketUserMessage;
        if (message == null) return;

        // Create a number to track where the prefix ends and the command begins
        int argPos = 0;

        // Determine if the message is a command based on the prefix and make sure no bots trigger commands
        if (!(message.HasCharPrefix('!', ref argPos) ||
            message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
            message.Author.IsBot)
            return;

        // Create a WebSocket-based command context based on the message
        var context = new SocketCommandContext(_client, message);

        // Execute the command with the command context we just
        // created, along with the service provider for precondition checks.
        await _commands.ExecuteAsync(
            context: context,
            argPos: argPos,
            services: null);
    }
}
