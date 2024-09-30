using Discord.WebSocket;
using LocalFileBackupPlugin;
using ModuleShared;
using System;
using System.Collections.Generic;

namespace DiscordBotPlugin
{
    public class PluginMain : AMPPlugin
    {
        private readonly Settings _settings;
        private readonly ILogger log;
        private readonly IPlatformInfo platform;
        private readonly IRunningTasksManager _tasks;
        public readonly IApplicationWrapper application;
        private readonly IAMPInstanceInfo aMPInstanceInfo;
        private readonly IConfigSerializer _config;
        private readonly IFeatureManager features;
        private readonly BackupProvider backupProvider;
       
        private Events events;
        private Bot bot;
        private Helpers helper;
        private Commands commands;
        private InfoPanel infoPanel;

        public PluginMain(ILogger log, IConfigSerializer config, IPlatformInfo platform,
            IRunningTasksManager taskManager, IApplicationWrapper application, IAMPInstanceInfo AMPInstanceInfo, IFeatureManager Features, BackupProvider BackupProvider)
        {
            _config = config;
            this.log = log;
            this.platform = platform;
            _settings = config.Load<Settings>(AutoSave: true);
            _tasks = taskManager;
            this.application = application;
            aMPInstanceInfo = AMPInstanceInfo;
            features = Features;

            features.PostLoadPlugin(application, "LocalFileBackupPlugin");
            features.RegisterFeature(BackupProvider);
            backupProvider = BackupProvider;

            config.SaveMethod = PluginSaveMethod.KVP;
            config.KVPSeparator = "=";

            // Initialize some dependencies first
            helper = new Helpers(_settings, this.log, this.application, _config, this.platform, null); // Temporary null for infoPanel
            commands = new Commands(this.application, _settings, this.log, backupProvider, aMPInstanceInfo, null); // Temporary null for events
            infoPanel = new InfoPanel(this.application, _settings, helper, aMPInstanceInfo, this.log, _config, null, commands); // Temporary null for bot
            bot = new Bot(_settings, aMPInstanceInfo, this.application, this.log, null, helper, infoPanel, commands, this.platform); // Temporary null for events

            // Pass the dependencies with fully initialized objects
            events = new Events(this.application, _settings, this.log, _config, bot, helper, backupProvider, infoPanel);

            // Complete the object initialization
            helper.SetInfoPanel(infoPanel); // Set the previously null dependency
            commands.SetEvents(events);     // Inject events
            infoPanel.SetBot(bot);          // Inject bot
            bot.SetEvents(events);          // Inject events

            _settings.SettingModified += events.Settings_SettingModified;
            log.MessageLogged += events.Log_MessageLogged;
            application.StateChanged += events.ApplicationStateChange;
            if (application is IHasSimpleUserList hasSimpleUserList)
            {
                hasSimpleUserList.UserJoins += events.UserJoins;
                hasSimpleUserList.UserLeaves += events.UserLeaves;
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
                        _ = bot.ConnectDiscordAsync(_settings.MainSettings.BotToken);
                        bot.UpdatePresence(null, null, true);
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
        /// Represents player playtime information.
        /// </summary>
        public class PlayerPlayTime
        {
            public string PlayerName { get; set; }
            public DateTime JoinTime { get; set; }
            public DateTime LeaveTime { get; set; }
        }

        public class ServerInfo
        {
            public string ServerName { get; set; }
            public string ServerIP { get; set; }
            public string ServerStatus { get; set; }
            public string ServerStatusClass { get; set; }
            public string CPUUsage { get; set; }
            public string MemoryUsage { get; set; }
            public string Uptime { get; set; }
            public string[] OnlinePlayers { get; set; }
            public string PlayerCount { get; set; }
            public string[] PlaytimeLeaderBoard { get; set; }
        }
    }
}
