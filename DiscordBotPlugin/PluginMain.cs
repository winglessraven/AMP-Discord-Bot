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

        public PluginMain(ILogger log, IConfigSerializer config, IPlatformInfo platform,
            IRunningTasksManager taskManager, IApplicationWrapper Application, IAMPInstanceInfo AMPInstanceInfo)
        {
            config.SaveMethod = PluginSaveMethod.KVP;
            config.KVPSeparator = "=";
            this.log = log;
            this.platform = platform;
            _settings = config.Load<Settings>(AutoSave: true); //Automatically saves settings when they're changed.
            _tasks = taskManager;
            application = Application;
            aMPInstanceInfo = AMPInstanceInfo;
            _config = config;
            _settings.SettingModified += Settings_SettingModified;

            IHasSimpleUserList hasSimpleUserList = application as IHasSimpleUserList;

            //register join and leave events
            hasSimpleUserList.UserJoins += UserJoins;
            hasSimpleUserList.UserLeaves += UserLeaves;
        }

        public override void Init(out WebMethodsBase APIMethods)
        {
            APIMethods = new WebMethods(_tasks);
        }

        void Settings_SettingModified(object sender, SettingModifiedEventArgs e)
        {
            //if bot setting activated, try starting it
            if (_settings.MainSettings.BotActive)
            {
                try
                {
                    if (_client == null || _client.ConnectionState == ConnectionState.Disconnected)
                        _ = ConnectDiscordAsync(_settings.MainSettings.BotToken);
                }
                catch (Exception exception)
                {
                    log.Error("Error with the Discord Bot : " + exception.Message);
                }
            }

            //bot deactivated, disconnect from Discord (if connected)
            if (!_settings.MainSettings.BotActive)
            {
                if (_client != null)
                {
                    if (_client.ConnectionState == ConnectionState.Connected)
                    {
                        _client.ButtonExecuted -= OnButtonPress;
                        _client.Log -= Log;
                        _client.Ready -= ClientReady;
                        _client.SlashCommandExecuted -= SlashCommandHandler;
                        _client.LogoutAsync();
                    }
                }
            }
        }

        public override bool HasFrontendContent => false;

        public override void PostInit()
        {
            //check if the bot is turned on
            if (_settings.MainSettings.BotActive)
            {
                log.Info("Discord Bot Activated");

                //check if we have a bot token and attempt to connect
                if (_settings.MainSettings.BotToken != null && _settings.MainSettings.BotToken != "")
                {
                    try
                    {
                        _ = ConnectDiscordAsync(_settings.MainSettings.BotToken);
                    }
                    catch (Exception exception)
                    {
                        log.Error("Error with the Discord Bot : " + exception.Message);
                    }
                }
            }
        }

        public override IEnumerable<SettingStore> SettingStores => Utilities.EnumerableFrom(_settings);

        /// <summary>
        /// Async task to handle the Discord connection and call the status check
        /// </summary>
        /// <param name="BotToken"></param>
        /// <returns></returns>
        public async Task ConnectDiscordAsync(string BotToken)
        {
            DiscordSocketConfig config = new DiscordSocketConfig { GatewayIntents = GatewayIntents.DirectMessages | GatewayIntents.GuildMessages | GatewayIntents.Guilds };

            //init Discord client & command service
            _client = new DiscordSocketClient(config);

            //attach logs and events
            _client.Log += Log;
            _client.ButtonExecuted += OnButtonPress;
            _client.Ready += ClientReady;
            _client.SlashCommandExecuted += SlashCommandHandler;


            await _client.LoginAsync(TokenType.Bot, BotToken);
            await _client.StartAsync();

            await SetStatus();

            // Block this task until the program is closed or bot is stopped.
            await Task.Delay(-1);
        }

        private Task Log(LogMessage msg)
        {
            log.Info(msg.ToString());
            return Task.CompletedTask;
        }

    

        private Task SendConsoleCommand(SocketSlashCommand msg)
        {
            try
            {
                //get the command to be sent
                string command = msg.Data.Options.First().Options.First().Value.ToString();

                //send the command to the instance
                IHasWriteableConsole writeableConsole = application as IHasWriteableConsole;
                writeableConsole.WriteLine(command);
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                log.Error("Cannot send command: " + exception.Message);
                return Task.CompletedTask;
            }
        }

        private async Task GetServerInfo(bool updateExisting, SocketSlashCommand msg)
        {
            if (_client.ConnectionState != ConnectionState.Connected)
                return;

            //cast to get player count / info
            IHasSimpleUserList hasSimpleUserList = application as IHasSimpleUserList;

            var onlinePlayers = hasSimpleUserList.Users.Count;
            var maximumPlayers = hasSimpleUserList.MaxUsers;

            //build bot response
            var embed = new EmbedBuilder
            {
                Title = "Server Info",
                Color = Color.LightGrey,
                ThumbnailUrl = _settings.MainSettings.GameImageURL
            };

            //server online
            if (application.State == ApplicationState.Ready)
            {
                embed.AddField("Server Status", ":white_check_mark: " + GetApplicationStateString(), false);
            }
            //server off or errored
            else if (application.State == ApplicationState.Failed || application.State == ApplicationState.Stopped)
            {
                embed.AddField("Server Status", ":no_entry: " + GetApplicationStateString(), false);
            }
            //everything else
            else
            {
                embed.AddField("Server Status", ":hourglass: " + GetApplicationStateString(), false);
            }

            embed.AddField("Server Name", "`" + _settings.MainSettings.ServerDisplayName + "`", true);
            embed.AddField("Server IP", "`" + _settings.MainSettings.ServerConnectionURL + "`", true);
            if (_settings.MainSettings.ServerPassword != "")
            {
                embed.AddField("Server Password", "`" + _settings.MainSettings.ServerPassword + "`", true);
            }
            embed.AddField("CPU Usage", application.GetCPUUsage() + "%", true);
            embed.AddField("Memory Usage", application.GetRAMUsage() + "MB", true);

            if (application.State == ApplicationState.Ready)
            {
                TimeSpan uptime = DateTime.Now.Subtract(application.StartTime);
                embed.AddField("Uptime", string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D2}", uptime.Days, uptime.Hours, uptime.Minutes, uptime.Seconds), true);
            }

            //if there is a valid player count, show the online player count
            if (_settings.MainSettings.ValidPlayerCount)
            {
                embed.AddField("Player Count", onlinePlayers + "/" + maximumPlayers, true);
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

            if (_settings.MainSettings.ModpackURL != "")
            {
                embed.AddField("Server Mod Pack", _settings.MainSettings.ModpackURL, false);
            }

            //if show playtime leaderboard is enabled
            if (_settings.MainSettings.ShowPlaytimeLeaderboard)
            {
                string leaderboard = GetPlayTimeLeaderBoard(5);
                embed.AddField("Top 5 Players by Play Time", leaderboard, false);
            }

            embed.WithFooter(_settings.MainSettings.BotTagline);
            embed.WithCurrentTimestamp();

            embed.WithThumbnailUrl(_settings.MainSettings.GameImageURL);

            //add buttons
            var builder = new ComponentBuilder();
            if (_settings.MainSettings.ShowStartButton)
                if (application.State == ApplicationState.Ready || application.State == ApplicationState.Starting || application.State == ApplicationState.Installing)
                {
                    builder.WithButton("Start", "start-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Success, disabled: true);
                }
                else
                {
                    builder.WithButton("Start", "start-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Success, disabled: false);
                }

            if (_settings.MainSettings.ShowStopButton)
                if (application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed)
                {
                    builder.WithButton("Stop", "stop-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: true);
                }
                else
                {
                    builder.WithButton("Stop", "stop-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: false);
                }

            if (_settings.MainSettings.ShowRestartButton)
                if (application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed)
                {
                    builder.WithButton("Restart", "restart-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: true);
                }
                else
                {
                    builder.WithButton("Restart", "restart-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: false);
                }

            if (_settings.MainSettings.ShowKillButton)
                if (application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed)
                {
                    builder.WithButton("Kill", "kill-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: true);
                }
                else
                {
                    builder.WithButton("Kill", "kill-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger, disabled: false);
                }

            if (_settings.MainSettings.ShowUpdateButton)
                if (application.State == ApplicationState.Installing)
                {
                    builder.WithButton("Update", "update-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Primary, disabled: true);
                }
                else
                {
                    builder.WithButton("Update", "update-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Primary, disabled: false);
                }

            if (_settings.MainSettings.ShowManageButton)
                builder.WithButton("Manage", "manage-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Primary);

            //if updating an existing message
            if (updateExisting)
            {
                foreach (string details in _settings.MainSettings.InfoMessageDetails)
                {
                    try
                    {
                        string[] split = details.Split('-');

                        var existingMsg = await _client
                            .GetGuild(Convert.ToUInt64(split[0]))
                            .GetTextChannel(Convert.ToUInt64(split[1]))
                            .GetMessageAsync(Convert.ToUInt64(split[2])) as IUserMessage;

                        if (existingMsg != null)
                        {
                            await existingMsg.ModifyAsync(x =>
                            {
                                x.Embed = embed.Build();
                                x.Components = builder.Build();
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
            else
            {
                var chnl = msg.Channel as SocketGuildChannel;
                var guild = chnl.Guild.Id;
                var channelID = msg.Channel.Id;

                //post bot reply
                var message = await _client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build(), components: builder.Build());

                log.Debug("Message ID: " + message.Id.ToString());

                _settings.MainSettings.InfoMessageDetails.Add(guild.ToString() + "-" + channelID.ToString() + "-" + message.Id.ToString());
                _config.Save(_settings);
            }
        }

        private async Task ShowPlayerPlayTime(SocketSlashCommand msg)
        {
            //build bot response
            var embed = new EmbedBuilder
            {
                Title = "Play Time Leaderboard",
                Color = Color.LightGrey,
                ThumbnailUrl = "https://freesvg.org/img/1548372247.png"
            };

            string leaderboard = GetPlayTimeLeaderBoard(15);

            embed.Description = leaderboard;

            embed.WithFooter(_settings.MainSettings.BotTagline);
            embed.WithCurrentTimestamp();

            
            //get guild
            var chnl = msg.Channel as SocketGuildChannel;
            var guild = chnl.Guild.Id;
            var channelID = msg.Channel.Id;

            //post leaderboard
            await _client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build());
        }

        //Looping task to update bot status/presence
        public async Task SetStatus()
        {
            while (_settings.MainSettings.BotActive)
            {
                try
                {
                    UserStatus status;

                    if (application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed)
                    {
                        status = UserStatus.DoNotDisturb;

                        //if there are still players listed in the timer, remove them
                        if (playerPlayTimes.Count != 0)
                            ClearAllPlayTimes();
                    }
                    else if (application.State == ApplicationState.Ready)
                    {
                        status = UserStatus.Online;
                    }
                    else
                    {
                        status = UserStatus.Idle;

                        //if there are still players listed in the timer, remove them
                        if (playerPlayTimes.Count != 0)
                            ClearAllPlayTimes();
                    }

                    IHasSimpleUserList hasSimpleUserList = application as IHasSimpleUserList;

                    var onlinePlayers = hasSimpleUserList.Users.Count;
                    var maximumPlayers = hasSimpleUserList.MaxUsers;
                    var cpuUsage = application.GetCPUUsage();
                    var memUsage = application.GetRAMUsage();
                    var instanceName = platform.PlatformName;
                    var cpuUsageString = cpuUsage + "%";

                    log.Debug("Server Status: " + application.State + " || Players: " + onlinePlayers + "/" + maximumPlayers + " || CPU: " + application.GetCPUUsage() + "% || Memory: " + application.GetPhysicalRAMUsage() + "MB");

                    if (application.State == ApplicationState.Ready)
                    {
                        await _client.SetGameAsync(OnlineBotPresenceString(onlinePlayers, maximumPlayers), null, ActivityType.Playing);
                    }
                    else
                    {
                        await _client.SetGameAsync(application.State.ToString(), null, ActivityType.Playing);
                    }


                    await _client.SetStatusAsync(status);

                    //update the embed if it exists
                    if (_settings.MainSettings.InfoMessageDetails != null)
                        if (_settings.MainSettings.InfoMessageDetails.Count > 0)
                            _ = GetServerInfo(true, null);

                }
                catch (System.Net.WebException exception)
                {
                    await _client.SetGameAsync("Server Offline", null, ActivityType.Watching);
                    await _client.SetStatusAsync(UserStatus.DoNotDisturb);
                    log.Info("Exception: " + exception.Message);
                }

                await Task.Delay(_settings.MainSettings.BotRefreshInterval * 1000);
            }
        }

        private async Task OnButtonPress(SocketMessageComponent arg)
        {
            log.Debug("Button pressed: " + arg.Data.CustomId.ToString());

            //temp bool for permission check
            bool hasServerPermission = false;

            //check if the user that pressed the button has permission
            _client.PurgeUserCache(); //try to clear cache so we can get the latest roles
            if (arg.User is SocketGuildUser user)
                //The user has the permission if either RestrictFunctions is turned off, or if they are part of the appropriate role.
                hasServerPermission = !_settings.MainSettings.RestrictFunctions || user.Roles.Any(r => r.Name == _settings.MainSettings.DiscordRole);

            if (!hasServerPermission)
            {
                //no permission, mark as responded and get out of here
                await arg.DeferAsync();
                return;
            }

            if (arg.Data.CustomId.Equals("start-server-" + aMPInstanceInfo.InstanceId))
            {
                application.Start();
                await ButtonResonse("Start", arg);
            }
            if (arg.Data.CustomId.Equals("stop-server-" + aMPInstanceInfo.InstanceId))
            {
                application.Stop();
                await ButtonResonse("Stop", arg);
            }
            if (arg.Data.CustomId.Equals("restart-server-" + aMPInstanceInfo.InstanceId))
            {
                application.Restart();
                await ButtonResonse("Restart", arg);
            }
            if (arg.Data.CustomId.Equals("kill-server-" + aMPInstanceInfo.InstanceId))
            {
                application.Kill();
                await ButtonResonse("Kill", arg);
            }
            if (arg.Data.CustomId.Equals("update-server-" + aMPInstanceInfo.InstanceId))
            {
                application.Update();
                await ButtonResonse("Update", arg);
            }
            if (arg.Data.CustomId.Equals("manage-server-" + aMPInstanceInfo.InstanceId))
            {
                await ManageServer(arg);
                await ButtonResonse("Manage", arg);
            }
        }

        private async Task ButtonResonse(string Command, SocketMessageComponent arg)
        {
            //only log if option is enabled
            if (_settings.MainSettings.LogButtonsAndCommands)
            {
                //build bot response
                var embed = new EmbedBuilder();
                if (Command == "Manage")
                {
                    embed.Title = "Manage Request";
                    embed.Description = "Manage URL Request Received";
                }
                else
                {
                    embed.Title = "Server Command Sent";
                    embed.Description = Command + " command has been sent to the " + application.ApplicationName + " server.";
                }

                embed.Color = Color.LightGrey;
                embed.ThumbnailUrl = _settings.MainSettings.GameImageURL;
                embed.AddField("Requested by", arg.User.Mention, true);
                embed.WithFooter(_settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();

                //get guild
                var chnl = arg.Message.Channel as SocketGuildChannel;
                var guild = chnl.Guild.Id;
                var logChannel = _client.GetGuild(guild).Channels.SingleOrDefault(x => x.Name == _settings.MainSettings.ButtonResponseChannel);
                var channelID = arg.Message.Channel.Id;

                if (logChannel != null)
                    channelID = logChannel.Id;

                log.Debug("Guild: " + guild + " || Channel: " + channelID);

                await _client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build());
            }
            await arg.DeferAsync();
        }

        private async Task CommandResponse(string Command, SocketSlashCommand arg)
        {
            //only log if option is enabled
            if (!_settings.MainSettings.LogButtonsAndCommands)
                return;

            //build bot response
            var embed = new EmbedBuilder();
            if (Command == "Manage")
            {
                embed.Title = "Manage Request";
                embed.Description = "Manage URL Request Received";
            }
            else
            {
                embed.Title = "Server Command Sent";
                embed.Description = Command + " command has been sent to the " + application.ApplicationName + " server.";
            }

            embed.Color = Color.LightGrey;
            embed.ThumbnailUrl = _settings.MainSettings.GameImageURL;
            embed.AddField("Requested by", arg.User.Mention, true);
            embed.WithFooter(_settings.MainSettings.BotTagline);
            embed.WithCurrentTimestamp();

            //get guild
            var chnl = arg.Channel as SocketGuildChannel;
            var guild = chnl.Guild.Id;
            var logChannel = _client.GetGuild(guild).Channels.SingleOrDefault(x => x.Name == _settings.MainSettings.ButtonResponseChannel);
            var channelID = arg.Channel.Id;

            if (logChannel != null)
                channelID = logChannel.Id;

            log.Debug("Guild: " + guild + " || Channel: " + channelID);

            await _client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build());
        }

        private async void UserJoins(object sender, UserEventArgs args)
        {
            //check if player is already in the list, if so remove it - shouldn't be there at this point
            playerPlayTimes.RemoveAll(p => p.PlayerName == args.User.Name);

            //log jointime for player
            playerPlayTimes.Add(new PlayerPlayTime() { PlayerName = args.User.Name, JoinTime = DateTime.Now });

            if (!_settings.MainSettings.PlayTime.ContainsKey(args.User.Name))
            {
                _settings.MainSettings.PlayTime.Add(args.User.Name, TimeSpan.Zero);
                _config.Save(_settings);
            }

            if (!_settings.MainSettings.PostPlayerEvents)
                return;

            foreach (SocketGuild socketGuild in _client.Guilds)
            {
                var guildID = socketGuild.Id;
                var eventChannel = _client.GetGuild(guildID).Channels.SingleOrDefault(x => x.Name == _settings.MainSettings.PostPlayerEventsChannel);
                if (eventChannel == null)
                    return; //doesn't exist so stop here

                string userName = args.User.Name;

                //build bot message
                var embed = new EmbedBuilder
                {
                    Title = "Server Event",
                    Color = Color.LightGrey,
                    ThumbnailUrl = _settings.MainSettings.GameImageURL
                };

                if (userName != "")
                {
                    embed.Description = userName + " joined the " + application.ApplicationName + " server.";
                }
                else
                {
                    embed.Description = "A player joined the " + application.ApplicationName + " server.";
                }

                embed.WithFooter(_settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();
                await _client.GetGuild(guildID).GetTextChannel(eventChannel.Id).SendMessageAsync(embed: embed.Build());
            }
        }

        private async void UserLeaves(object sender, UserEventArgs args)
        {
            try
            {
                //add leavetime for player
                playerPlayTimes.Find(p => p.PlayerName == args.User.Name).LeaveTime = DateTime.Now;

                //check entry for player, if not there add new entry
                if (!_settings.MainSettings.PlayTime.ContainsKey(args.User.Name))
                    _settings.MainSettings.PlayTime.Add(args.User.Name, new TimeSpan(0));

                TimeSpan sessionPlayTime = playerPlayTimes.Find(p => p.PlayerName == args.User.Name).LeaveTime - playerPlayTimes.Find(p => p.PlayerName == args.User.Name).JoinTime;

                //update main playtime list
                _settings.MainSettings.PlayTime[args.User.Name] += sessionPlayTime;
                _config.Save(_settings);

                //remove from 'live' list
                playerPlayTimes.RemoveAll(p => p.PlayerName == args.User.Name);

            }
            catch (Exception exception)
            {
                log.Error(exception.Message);
            }


            if (!_settings.MainSettings.PostPlayerEvents)
                return;

            foreach (SocketGuild socketGuild in _client.Guilds)
            {
                var guildID = socketGuild.Id;
                var eventChannel = _client.GetGuild(guildID).Channels.SingleOrDefault(x => x.Name == _settings.MainSettings.PostPlayerEventsChannel);
                if (eventChannel == null)
                    return; //doesn't exist so stop here

                string userName = args.User.Name;

                //build bot message
                var embed = new EmbedBuilder
                {
                    Title = "Server Event",
                    Color = Color.LightGrey,
                    ThumbnailUrl = _settings.MainSettings.GameImageURL
                };

                if (userName != "")
                {
                    embed.Description = userName + " left the " + application.ApplicationName + " server.";
                }
                else
                {
                    embed.Description = "A player left the " + application.ApplicationName + " server.";
                }

                embed.WithFooter(_settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();
                await _client.GetGuild(guildID).GetTextChannel(eventChannel.Id).SendMessageAsync(embed: embed.Build());
            }
        }

        private async Task ManageServer(SocketMessageComponent arg)
        {
            var builder = new ComponentBuilder();
            builder.WithButton("Manage Server", style: ButtonStyle.Link, url: "https://" + _settings.MainSettings.ManagementURL + "/?instance=" + aMPInstanceInfo.InstanceId);
            await arg.User.SendMessageAsync("Link to management panel:", components: builder.Build());
        }

        private string GetApplicationStateString()
        {
            //if replacement value exists, return it
            if (_settings.MainSettings.ChangeStatus.ContainsKey(application.State.ToString()))
                return _settings.MainSettings.ChangeStatus[application.State.ToString()];

            //no replacement exists so return the default value
            return application.State.ToString();
        }

        private string OnlineBotPresenceString(int onlinePlayers, int maximumPlayers)
        {
            //if valid player count and no custom value
            if (_settings.MainSettings.OnlineBotPresence == "" && _settings.MainSettings.ValidPlayerCount)
                return onlinePlayers + "/" + maximumPlayers + " players";

            //if no valid player count and no custom value
            if (_settings.MainSettings.OnlineBotPresence == "")
                return "Online";

            //get custom value
            string presence = _settings.MainSettings.OnlineBotPresence;

            //replace variables
            presence = presence.Replace("{OnlinePlayers}", onlinePlayers.ToString());
            presence = presence.Replace("{MaximumPlayers}", maximumPlayers.ToString());

            return presence;
        }

        private string GetPlayTimeLeaderBoard(int placesToShow)
        {
            //create new dictionary to hold logged time plus any current session time
            Dictionary<string, TimeSpan> playtime = new Dictionary<string, TimeSpan>(_settings.MainSettings.PlayTime);

            foreach (PlayerPlayTime player in playerPlayTimes)
            {
                TimeSpan currentSession = DateTime.Now - player.JoinTime;

                log.Debug("Player: " + player.PlayerName + " || Current Session: " + currentSession + " || Logged: " + _settings.MainSettings.PlayTime[player.PlayerName]);

                //add any current sessions to the logged playtime
                playtime[player.PlayerName] = playtime[player.PlayerName].Add(currentSession);
            }

            var sortedList = playtime.OrderByDescending(v => v.Value).ToList();

            string leaderboard = "```";
            int position = 1;

            //if nothing is logged yet return no data
            if (sortedList.Count == 0)
            {
                return "```No play time logged yet```";
            }

            foreach (KeyValuePair<string, TimeSpan> player in sortedList)
            {
                //if outside places to show, stop processing
                if (position > placesToShow)
                    break;

                leaderboard += string.Format("{0,-4}{1,-20}{2,-15}", position + ".", player.Key, string.Format("{0}d {1}h {2}m {3}s", player.Value.Days, player.Value.Hours, player.Value.Minutes, player.Value.Seconds)) + Environment.NewLine;
                position++;
            }

            leaderboard += "```";

            return leaderboard;
        }

        private void ClearAllPlayTimes()
        {
            try
            {
                foreach (PlayerPlayTime playerPlayTime in playerPlayTimes)
                {

                    log.Debug("Saving playtime for " + playerPlayTime.PlayerName);
                    //set the leave time as now
                    playerPlayTime.LeaveTime = DateTime.Now;

                    //check entry for player, if not there add new entry
                    if (!_settings.MainSettings.PlayTime.ContainsKey(playerPlayTime.PlayerName))
                        _settings.MainSettings.PlayTime.Add(playerPlayTime.PlayerName, new TimeSpan(0));

                    TimeSpan sessionPlayTime = playerPlayTime.LeaveTime - playerPlayTime.JoinTime;

                    //update main playtime list
                    _settings.MainSettings.PlayTime[playerPlayTime.PlayerName] += sessionPlayTime;
                    _config.Save(_settings);
                }
                playerPlayTimes.Clear();
            }
            catch (Exception exception)
            {
                log.Debug(exception.Message);
            }
        }

        public async Task ClientReady()
        {
            //bot name for command
            string botName = _client.CurrentUser.Username.ToLower();

            //replace any spaces with -
            botName = Regex.Replace(botName, "[^a-zA-Z0-9]", String.Empty);

            log.Info("BOTNAME: " + botName);

            // Let's do our global command
            var globalCommand = new SlashCommandBuilder()
                .WithName(botName)
        .WithDescription("Base bot command")
        .AddOption(new SlashCommandOptionBuilder()
            .WithName("info")
            .WithDescription("Create the Server Info Panel")
            .WithType(ApplicationCommandOptionType.SubCommand)
            ).AddOption(new SlashCommandOptionBuilder()
                .WithName("start-server")
                .WithDescription("Start the Server")
                .WithType(ApplicationCommandOptionType.SubCommand)
            ).AddOption(new SlashCommandOptionBuilder()
                .WithName("stop-server")
                .WithDescription("Stop the Server")
                .WithType(ApplicationCommandOptionType.SubCommand)
            ).AddOption(new SlashCommandOptionBuilder()
                .WithName("restart-server")
                .WithDescription("Restart the Server")
                .WithType(ApplicationCommandOptionType.SubCommand)
            ).AddOption(new SlashCommandOptionBuilder()
                .WithName("kill-server")
                .WithDescription("Kill the Server")
                .WithType(ApplicationCommandOptionType.SubCommand)
            ).AddOption(new SlashCommandOptionBuilder()
                .WithName("update-server")
                .WithDescription("Update the Server")
                .WithType(ApplicationCommandOptionType.SubCommand)
            ).AddOption(new SlashCommandOptionBuilder()
                .WithName("show-playtime")
                .WithDescription("Show the Playtime Leaderboard")
                .WithType(ApplicationCommandOptionType.SubCommand)
            ).AddOption(new SlashCommandOptionBuilder()
                .WithName("console")
                .WithDescription("Send a Console Command to the Application")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("value", ApplicationCommandOptionType.String, "Command text", isRequired: true)
            );

            try
            {
                // With global commands we don't need the guild.
                await _client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
                // Using the ready event is a simple implementation for the sake of the example. Suitable for testing and development.
                // For a production bot, it is recommended to only run the CreateGlobalApplicationCommandAsync() once for each command.
            }
            catch (HttpException exception)
            {
                log.Error(exception.Message);
            }
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            //init bool for permission check
            bool hasServerPermission = false;

            if (command.User is SocketGuildUser user)
                //The user has the permission if either RestrictFunctions is turned off, or if they are part of the appropriate role.
                hasServerPermission = !_settings.MainSettings.RestrictFunctions || user.Roles.Any(r => r.Name == _settings.MainSettings.DiscordRole);

            if (!hasServerPermission)
            {
                await command.RespondAsync("You do not have permission to use this command!", ephemeral: true);
                return;
            }    

            switch (command.Data.Options.First().Name)
            {
                case "info":
                    await GetServerInfo(false, command);
                    await command.RespondAsync("Info panel created", ephemeral: true);
                    break;
                case "start-server":
                    application.Start();
                    await CommandResponse("Start Server", command);
                    await command.RespondAsync("Start command sent to the application",ephemeral:true);
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
                case "show-playtime":
                    await ShowPlayerPlayTime(command);
                    await command.RespondAsync("Playtime leaderboard displayed", ephemeral: true);
                    break;
                case "console":
                    await SendConsoleCommand(command);
                    await CommandResponse("`" + command.Data.Options.First().Options.First().Value.ToString() + "` console ", command);
                    await command.RespondAsync("Command sent to the server: `" + command.Data.Options.First().Options.First().Value.ToString() + "`", ephemeral: true);
                    break;
            }
        }

        public class PlayerPlayTime
        {
            public string PlayerName { get; set; }
            public DateTime JoinTime { get; set; }
            public DateTime LeaveTime { get; set; }
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
