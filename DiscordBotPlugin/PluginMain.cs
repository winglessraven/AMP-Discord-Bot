using ModuleShared;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.Linq;

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
        private DiscordSocketClient _client;
        private Emoji emoji = new Emoji("👍"); //bot reaction emoji

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
            _settings.SettingModified += Settings_SettingModified;
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
                    if(_client == null || _client.ConnectionState == ConnectionState.Disconnected)
                    {
                        _ = ConnectDiscordAsync(_settings.MainSettings.BotToken);
                    }
                    
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
                        _client.MessageReceived -= OnMessageReceived;
                        _client.Log -= Log;
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
            //init Discord client & command service
            _client = new DiscordSocketClient();

            //attach logs and events
            _client.Log += Log;
            _client.MessageReceived += OnMessageReceived;
            _client.ButtonExecuted += OnButtonPress;

            await _client.LoginAsync(TokenType.Bot, BotToken);
            await _client.StartAsync();

            await SetStatus();

            // Block this task until the program is closed or bot is stopped.
            await Task.Delay(-1);
        }

        //listen for messages in Discord
        private async Task OnMessageReceived(SocketMessage arg)
        {
            var msg = arg as SocketUserMessage;

            //if messasge not posted by a bot
            if (msg.Author.IsBot)
            {
                return;
            }

            //temp bool for permission check
            bool hasServerPermission = false;

            var context = new SocketCommandContext(_client, msg);

            int pos = 0;

            //is bot mentioned?
            if (msg.HasMentionPrefix(_client.CurrentUser, ref pos))
            {
                _client.PurgeUserCache(); //try to clear cache so we can get the latest roles
                if (context.User is SocketGuildUser user)
                    //The user has the permission if either RestrictFunctions is turned off, or if they are part of the appropriate role.
                    hasServerPermission = !_settings.MainSettings.RestrictFunctions || user.Roles.Any(r => r.Name == _settings.MainSettings.DiscordRole);

                //help command
                if (msg.Content.ToLower().Contains("help"))
                    await ShowHelp(msg);

                //info command
                if (msg.Content.ToLower().Contains("info"))
                    await GetServerInfo(msg);

                //following commands require permission so don't bother checking for matches if not allowed
                if (!hasServerPermission)
                    return;

                //restart server command
                if (msg.Content.ToLower().Contains("restart server") && hasServerPermission)
                    await RestartServer(msg);

                //stop server command
                if (msg.Content.ToLower().Contains("stop server") && hasServerPermission)
                    await StopServer(msg);

                //start server command
                if (msg.Content.ToLower().Contains("start server") && !msg.Content.ToLower().Contains("restart") && hasServerPermission)
                    await StartServer(msg);

                //update server command
                if (msg.Content.ToLower().Contains("update server") && hasServerPermission)
                    await UpdateServer(msg);

                //kill server command
                if (msg.Content.ToLower().Contains("kill server") && hasServerPermission)
                    await KillServer(msg);
            }
        }

        private Task Log(LogMessage msg)
        {
            log.Info(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task RestartServer(SocketUserMessage msg)
        {
            try
            {
                //bot reaction
                await msg.AddReactionAsync(emoji);

                //send the command to the instance
                application.Restart();

                //build bot response
                var embed = new EmbedBuilder
                {
                    Title = "Command Sent",
                    Description = "Restart command has been sent to the " + application.ApplicationName + " server.  It will be back online shortly!",
                    Color = Color.Blue,
                };

                embed.WithFooter(_settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();
                embed.WithThumbnailUrl("https://i.gifer.com/5ug7.gif");

                //post bot response
                await msg.ReplyAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                log.Error("Cannot restart application: " + exception.Message);
            }
        }

        private async Task StartServer(SocketUserMessage msg)
        {
            try
            {
                //bot reaction
                await msg.AddReactionAsync(emoji);

                //send the start command to the instance
                application.Start();

                //build bot response
                var embed = new EmbedBuilder
                {
                    Title = "Command Sent",
                    Description = "Start command has been sent to the " + application.ApplicationName + " server.  Should be online soon!",
                    Color = Color.Green,
                };

                embed.WithFooter(_settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();
                embed.WithThumbnailUrl("https://i.gifer.com/6tIB.gif");

                //post bot response
                await msg.ReplyAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                log.Error("Cannot start application: " + exception.Message);
            }
        }

        private async Task StopServer(SocketUserMessage msg)
        {
            try
            {
                //bot reaction
                await msg.AddReactionAsync(emoji);

                //send the stop command to the instance
                application.Stop();

                //build bot response
                var embed = new EmbedBuilder
                {
                    Title = "Command Sent",
                    Description = "Stop command has been sent to the " + application.ApplicationName + " server.  Shutting down now!",
                    Color = Color.Red,
                };

                embed.WithFooter(_settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();
                embed.WithThumbnailUrl("https://i.gifer.com/3tes.gif");

                //post bot response
                await msg.ReplyAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                log.Error("Cannot stop application: " + exception.Message);
            }
        }

        private async Task UpdateServer(SocketUserMessage msg)
        {
            try
            {
                //bot reaction
                await msg.AddReactionAsync(emoji);

                //send the update command to the instance
                application.Update();

                //build bot response
                var embed = new EmbedBuilder
                {
                    Title = "Command Sent",
                    Description = "Update command has been sent to the " + application.ApplicationName + " server.  If an update is available it will be updated shortly!",
                    Color = Color.Green,
                };

                embed.WithFooter(_settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();
                embed.WithThumbnailUrl("https://i.gifer.com/UGD4.gif");

                //post bot response
                await msg.ReplyAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                log.Error("Cannot update application: " + exception.Message);
            }
        }

        private async Task KillServer(SocketUserMessage msg)
        {
            try
            {
                //bot reaction
                await msg.AddReactionAsync(emoji);

                //send the kill command to the instance
                application.Kill();

                //build bot response
                var embed = new EmbedBuilder
                {
                    Title = "Command Sent",
                    Description = "Kill command has been sent to the " + application.ApplicationName + " server.  Should be dead soon!",
                    Color = Color.Green,
                };

                embed.WithFooter(_settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();
                embed.WithThumbnailUrl("https://i.gifer.com/sdZ.gif");

                //post bot response
                await msg.ReplyAsync(embed: embed.Build());
            }
            catch (Exception exception)
            {
                log.Error("Cannot kill application: " + exception.Message);
            }
        }

        private async Task GetServerInfo(SocketUserMessage msg)
        {
            //bot reaction
            await msg.AddReactionAsync(emoji);

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
                embed.AddField("Server Status", ":white_check_mark: Online", false);
            }
            //server off or errored
            else if (application.State == ApplicationState.Failed || application.State == ApplicationState.Stopped)
            {
                embed.AddField("Server Status", ":no_entry: Offline | Type `@" + _client.CurrentUser.Username + " start server` to start the server", false);
            }
            //everything else
            else
            {
                embed.AddField("Server Status", ":hourglass: " + application.State, false);
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

            if (_settings.MainSettings.ValidPlayerCount)
            {
                embed.AddField("Player Count", onlinePlayers + "/" + maximumPlayers, true);
            }

            embed.WithFooter(_settings.MainSettings.BotTagline);
            embed.WithCurrentTimestamp();
            if (_settings.MainSettings.ModpackURL != "")
            {
                embed.AddField("Server Mod Pack", _settings.MainSettings.ModpackURL, false);
            }
            embed.WithThumbnailUrl(_settings.MainSettings.GameImageURL);

            var builder = new ComponentBuilder();

            builder.WithButton("Start Server", "start-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Success);
            builder.WithButton("Stop Server", "stop-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger);
            builder.WithButton("Restart Server", "restart-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Secondary);
            builder.WithButton("Kill Server", "kill-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Danger);
            builder.WithButton("Update Server", "update-server-" + aMPInstanceInfo.InstanceId, ButtonStyle.Primary);

            //post bot reply
            await msg.ReplyAsync(embed: embed.Build(),components: builder.Build());
        }

        private async Task ShowHelp(SocketUserMessage msg)
        {
            //bot reaction
            await msg.AddReactionAsync(emoji);

            //build bot response
            var embed = new EmbedBuilder
            {
                Title = "Bot Help",
                Color = Color.LightGrey,
                ThumbnailUrl = "https://freesvg.org/img/1527172379.png"
            };

            embed.AddField("Bot Commands", "`@" + _client.CurrentUser.Username + " info` - Show server info" + Environment.NewLine +
                "`@" + _client.CurrentUser.Username + " start server` - Start the Server" + Environment.NewLine +
                "`@" + _client.CurrentUser.Username + " stop server` - Stop the Server" + Environment.NewLine +
                "`@" + _client.CurrentUser.Username + " restart server` - Restart the Server" + Environment.NewLine +
                "`@" + _client.CurrentUser.Username + " kill server` - Kill the Server" + Environment.NewLine +
                "`@" + _client.CurrentUser.Username + " update server` - Update the Server" + Environment.NewLine +
                "`@" + _client.CurrentUser.Username + " help` - Show this message" + Environment.NewLine);

            //post bot reply
            await msg.ReplyAsync(embed: embed.Build());
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
                    }
                    else if (application.State == ApplicationState.Ready)
                    {
                        status = UserStatus.Online;
                    }
                    else
                    {
                        status = UserStatus.Idle;
                    }

                    IHasSimpleUserList hasSimpleUserList = application as IHasSimpleUserList;

                    var onlinePlayers = hasSimpleUserList.Users.Count;
                    var maximumPlayers = hasSimpleUserList.MaxUsers;
                    var cpuUsage = application.GetCPUUsage();
                    var memUsage = application.GetRAMUsage();
                    var instanceName = platform.PlatformName;
                    var cpuUsageString = cpuUsage + "%";
                    var playerCountString = onlinePlayers + "/" + maximumPlayers;

                    log.Debug("Server Status: " + application.State + " || Players: " + playerCountString + " || CPU: " + application.GetCPUUsage() + "% || Memory: " + application.GetPhysicalRAMUsage() + "MB");

                    if (_settings.MainSettings.ValidPlayerCount && application.State == ApplicationState.Ready)
                    {
                        await _client.SetGameAsync(playerCountString + " players", null, ActivityType.Playing);
                    }
                    else
                    {
                        await _client.SetGameAsync(application.State.ToString(), null, ActivityType.Playing);
                    }
                    await _client.SetStatusAsync(status);
                }
                catch (System.Net.WebException exception)
                {
                    await _client.SetGameAsync("Server Offline", null, ActivityType.Watching);
                    await _client.SetStatusAsync(UserStatus.DoNotDisturb);
                    log.Info("Exception: " + exception.Message);
                }

                await Task.Delay(10000);
            }
        }

        private async Task OnButtonPress(SocketMessageComponent arg)
        {
            log.Info("Button pressed: " + arg.Data.CustomId.ToString());

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
                
            if(arg.Data.CustomId.Equals("start-server-" + aMPInstanceInfo.InstanceId))
            {
                application.Start();
                await ButtonResonse("Start", arg);
            }
            if (arg.Data.CustomId.Equals("stop-server-" + aMPInstanceInfo.InstanceId))
            {
                 application.Stop();
                await ButtonResonse("Stop", arg);
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

        }

        private async Task ButtonResonse(string Command, SocketMessageComponent arg)
        {
            //build bot response
            var embed = new EmbedBuilder
            {
                Title = "Server Command Sent",
                Description = Command + " command has been sent to the " + application.ApplicationName + " server.",
                Color = Color.LightGrey,
                ThumbnailUrl = _settings.MainSettings.GameImageURL
            };

            embed.AddField("Requested by", arg.User.Mention, true);
            embed.WithFooter(_settings.MainSettings.BotTagline);
            embed.WithCurrentTimestamp();
            //await arg.RespondAsync(embed:embed.Build());

            //get guild
            var chnl = arg.Message.Channel as SocketGuildChannel;
            var guild = chnl.Guild.Id;
            var channelID = arg.Message.Channel.Id;

            if (_settings.MainSettings.ButtonResponseChannel != "")
            {
                channelID = Convert.ToUInt64(_settings.MainSettings.ButtonResponseChannel);
            }
            
            log.Debug("Guild: " + guild + " || Channel: " + channelID);

            await _client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build());
            await arg.DeferAsync();
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