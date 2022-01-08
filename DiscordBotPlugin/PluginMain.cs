using ModuleShared;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Discord.Commands;
using System.Linq;

//Your namespace must match the assembly name and the filename. Do not change one without changing the other two.
namespace DiscordBotPlugin
{
    //The first class must be called PluginName
    public class PluginMain : AMPPlugin
    {
        private readonly Settings _settings;
        private readonly ILogger log;
        private readonly IConfigSerializer _config;
        private readonly IPlatformInfo platform;
        private readonly IRunningTasksManager _tasks;
        private readonly IApplicationWrapper application;
        private readonly IPluginMessagePusher message;
        private readonly IFeatureManager features;
        private DiscordSocketClient _client;
        private CommandService _commands;
        private Emoji emoji = new Emoji("👍"); //bot reaction emoji

        //All constructor arguments after currentPlatform are optional, and you may ommit them if you don't
        //need that particular feature. The features you request don't have to be in any particular order.
        //Warning: Do not add new features to the feature manager here, only do that in Init();
        public PluginMain(ILogger log, IConfigSerializer config, IPlatformInfo platform,
            IRunningTasksManager taskManager, IApplicationWrapper Application,
            IPluginMessagePusher Message, IFeatureManager Features, IApplicationWrapper App)
        {
            //These are the defaults, but other mechanisms are available.
            config.SaveMethod = PluginSaveMethod.KVP;
            config.KVPSeparator = "=";
            this.log = log;
            _config = config;
            this.platform = platform;
            _settings = config.Load<Settings>(AutoSave: true); //Automatically saves settings when they're changed.
            _tasks = taskManager;
            application = Application;
            message = Message;
            features = Features;
            _settings.SettingModified += Settings_SettingModified;
        }

        /*
            Rundown of the different interfaces you can ask for in your constructor:
            IRunningTasksManager - Used to put tasks in the left hand side of AMP to update the user on progress.
            IApplicationWrapper - A reference to the running application from the running module.
            IPluginMessagePusher - For 'push' type notifications that your front-end code can react to via PushedMessage in Plugin.js
            IFeatureManager - To expose/consume features to/from other plugins.
        */

        //Your init function should not invoke any code that depends on other plugins.
        //You may expose functionality via IFeatureManager.RegisterFeature, but you cannot yet use RequestFeature.
        public override void Init(out WebMethodsBase APIMethods)
        {
            APIMethods = new WebMethods(_tasks);
        }

        void Settings_SettingModified(object sender, SettingModifiedEventArgs e)
        {
            //If you need to export settings to a different application, this is where you'd do it.

            //if bot setting activated, try starting it
            if (_settings.MainSettings.BotActive)
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

            //bot deactivated, disconnect from Discord (if connected)
            if (!_settings.MainSettings.BotActive)
            {
                if (_client != null)
                {
                    if (_client.ConnectionState == ConnectionState.Connected)
                    {
                        _client.LogoutAsync();
                    }
                }
            }
        }

        public override bool HasFrontendContent => true;

        //This gets called after every plugin is loaded. From here on it's safe
        //to use code that depends on other plugins and use IFeatureManager.RequestFeature
        public override void PostInit()
        {

            //init Discord client & command service
            _client = new DiscordSocketClient();
            _commands = new CommandService();

            //attach logs and events
            _client.Log += Log;
            _client.MessageReceived += OnMessageReceived;

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
            if (!msg.Author.IsBot)
            {
                //temp bool for permission check
                bool hasServerPermission = false;

                var context = new SocketCommandContext(_client, msg);

                int pos = 0;

                //is bot mentioned?
                if (msg.HasMentionPrefix(_client.CurrentUser, ref pos))
                {
                    //check permission for server commands
                    if (context.User is SocketGuildUser user)
                    {
                        //note: dicord.net caches this info, meaning role changes are not picked up instantly. Turn on SERVER MEMBERS INTENT on the bot page to fix.
                        if(_settings.MainSettings.RestrictFunctions)
                        {
                            if (user.Roles.Any(r => r.Name == _settings.MainSettings.DiscordRole))
                            {
                                hasServerPermission = true;
                            }
                        }
                        else
                        {
                            //not role restricted, grant permission
                            hasServerPermission = true;
                        }
                    }

                    //restart server command
                    if (msg.Content.ToLower().Contains("restart server") && hasServerPermission)
                    {
                        //bot reaction
                        await msg.AddReactionAsync(emoji);

                        //restart server method
                        RestartServer();

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

                    //stop server command
                    if (msg.Content.ToLower().Contains("stop server") && hasServerPermission)
                    {
                        //bot reaction
                        await msg.AddReactionAsync(emoji);

                        //stop server method
                        StopServer();

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

                    //start server command
                    if (msg.Content.ToLower().Contains("start server") && !msg.Content.ToLower().Contains("restart") && hasServerPermission)
                    {
                        //bot reaction
                        await msg.AddReactionAsync(emoji);

                        //start server method
                        StartServer();

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

                    //update server command
                    if (msg.Content.ToLower().Contains("update server") && hasServerPermission)
                    {
                        //bot reaction
                        await msg.AddReactionAsync(emoji);

                        //start server method
                        UpdateServer();

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

                    //kill server command
                    if (msg.Content.ToLower().Contains("kill server") && hasServerPermission)
                    {
                        //bot reaction
                        await msg.AddReactionAsync(emoji);

                        //kill server method
                        KillServer();

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

                    //info command
                    if (msg.Content.ToLower().Contains("info"))
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
                            embed.AddField("Uptime", string.Format("{0:D2}:{1:D2}:{2:D2}:{3:D2}",uptime.Days,uptime.Hours,uptime.Minutes,uptime.Seconds), true);
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

                        //post bot reply
                        await msg.ReplyAsync(embed: embed.Build());
                    }
                }
            }
        }

        private Task Log(LogMessage msg)
        {
            log.Info(msg.ToString());
            return Task.CompletedTask;
        }

        private void RestartServer()
        {
            try
            {
                application.Restart();
            }
            catch (Exception exception)
            {
                log.Error("Cannot restart application: " + exception.Message);
            }
        }

        private void StartServer()
        {
            try
            {
                application.Start();
            }
            catch (Exception exception)
            {
                log.Error("Cannot start application: " + exception.Message);
            }
        }

        private void StopServer()
        {
            try
            {
                application.Stop();
            }
            catch (Exception exception)
            {
                log.Error("Cannot stop application: " + exception.Message);
            }
        }

        private void UpdateServer()
        {
            try
            {
                application.Update();
            }
            catch(Exception exception)
            {
                log.Error("Cannot update application: " + exception.Message);
            }
        }

        private void KillServer()
        {
            try
            {
                application.Kill();
            }
            catch (Exception exception)
            {
                log.Error("Cannot kill application: " + exception.Message);
            }
        }

        /// <summary>
        /// Looping task to update bot status/presence
        /// </summary>
        /// <returns></returns>
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

