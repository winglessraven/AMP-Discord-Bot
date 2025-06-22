using FileManagerPlugin;
using LocalFileBackupPlugin;
using ModuleShared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiscordBotPlugin
{
    public class PluginMain : AMPPlugin
    {
        private readonly Settings _settings;
        private readonly ILogger log;
        private readonly IRunningTasksManager _tasks;
        public readonly IApplicationWrapper application;
        private readonly Bot bot;
        private BackupProvider _backupProvider;
        private readonly IFeatureManager _features;
        private readonly Commands commands;
        private readonly InfoPanel infoPanel;
        private readonly Events events;
        private readonly Helpers helper;

        public PluginMain(ILogger log, IConfigSerializer config, IPlatformInfo platform,
            IRunningTasksManager taskManager, IApplicationWrapper application, IAMPInstanceInfo AMPInstanceInfo, IFeatureManager Features)
        {
            this.log = log;
            _settings = config.Load<Settings>(AutoSave: true);
            _tasks = taskManager;
            this.application = application;
            _features = Features;

            _features.PostLoadPlugin(application, "LocalFileBackupPlugin");

            config.SaveMethod = PluginSaveMethod.KVP;
            config.KVPSeparator = "=";

            // Initialize some dependencies first
            helper = new Helpers(_settings, this.log, this.application, config, platform, null); // Temporary null for infoPanel
            commands = new Commands(this.application, _settings, this.log, null, AMPInstanceInfo, null, config); // Temporary null for events and backupprovider
            infoPanel = new InfoPanel(this.application, _settings, helper, AMPInstanceInfo, this.log, config, null, commands); // Temporary null for bot
            bot = new Bot(_settings, AMPInstanceInfo, this.application, this.log, null, helper, infoPanel, commands); // Temporary null for events

            // Pass the dependencies with fully initialized objects, except backupprovider (post-init)
            events = new Events(this.application, _settings, this.log, config, bot, helper, null, infoPanel);

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

            log.Info("Discord Bot Plugin Initialized.");
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
            _backupProvider = (BackupProvider)_features.RequestFeature<BackupProvider>();
            commands.SetBackupProvider(_backupProvider);  // Inject backupprovider
            events.SetBackupProvider(_backupProvider);    // Inject backupprovider

            // Check if the bot is turned on
            if (_settings.MainSettings.BotActive)
            {
                log.Info("Discord Bot Activated");

                // Check if we have a bot token and attempt to connect
                if (!string.IsNullOrEmpty(_settings.MainSettings.BotToken))
                {
                    try
                    {
                        log.Info("Attempting Discord connection...");
                        _ = bot.ConnectDiscordAsync(_settings.MainSettings.BotToken);
                        _ = bot.UpdatePresence(null, null, true);
                    }
                    catch (Exception exception)
                    {
                        // Log any errors that occur during bot connection
                        log.Error("Error with the Discord Bot: " + exception.Message);
                    }
                }
                else
                {
                    log.Error("Bot Token is empty or whitespace, cannot connect.");
                }

                // Schedule validation to run after a short delay to allow connection attempt
                // Run validation even if ConnectDiscordAsync throws an immediate error, as the client might still exist.
                _ = Task.Run(async () => {
                    await Task.Delay(10000); // Wait 10 seconds for connection attempt
                                             // Check events as well
                    if (bot?.client != null && events != null)
                    { // Check bot AND client AND events
                        log.Info("Running initial configuration validation...");
                        // Call the renamed async method
                        await bot.PerformInitialConfigurationValidationAsync();
                    }
                    else
                    {
                        log.Warning("Skipping initial configuration validation as Discord client was not initialized or connected.");
                    }
                });
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
