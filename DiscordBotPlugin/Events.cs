using Discord.WebSocket;
using Discord;
using ModuleShared;
using System;
using System.Threading.Tasks;
using static DiscordBotPlugin.PluginMain;
using System.Text.RegularExpressions;
using LocalFileBackupPlugin;
using System.Linq;

namespace DiscordBotPlugin
{
    internal class Events
    {
        private readonly IApplicationWrapper application;
        private readonly Settings settings;
        private readonly ILogger log;
        private readonly IConfigSerializer config;
        private readonly Bot bot;
        private readonly Helpers helper;
        private BackupProvider backupProvider;
        private readonly InfoPanel infoPanel;

        public SocketGuildUser currentUser;

        public Events(IApplicationWrapper application, Settings settings, ILogger log, IConfigSerializer config, Bot bot, Helpers helper, BackupProvider backupProvider, InfoPanel infoPanel)
        {
            this.application = application;
            this.settings = settings;
            this.log = log;
            this.config = config;
            this.bot = bot;
            this.helper = helper;
            this.backupProvider = backupProvider;
            this.infoPanel = infoPanel;
        }

        public void SetCurrentUser(SocketGuildUser currentUser)
        {
            this.currentUser = currentUser;
        }

        public void SetBackupProvider(BackupProvider backupProvider)
        {
            this.backupProvider = backupProvider;
        }

        /// <summary>
        /// Event handler for when a user joins the server.
        /// </summary>
        public async void UserJoins(object sender, UserEventArgs args)
        {
            if (args?.User == null)
            {
                log.Error("User event argument is null.");
                return;
            }

            //if name is empty, log and return
            if (args.User.Name == "")
            {
                log.Info("User joined but name is empty. Playtime will not be logged. User ID: " + args.UserID.ToString());
                return;
            }

            if (helper.CheckIfPlayerJoinedWithinLast10Seconds(infoPanel.playerPlayTimes, args.User.Name))
            {
                log.Info("Player join event already processed.");
                return;
            }

            // Remove existing entry and log join time
            infoPanel.playerPlayTimes.RemoveAll(p => p.PlayerName == args.User.Name);
            infoPanel.playerPlayTimes.Add(new PlayerPlayTime { PlayerName = args.User.Name, JoinTime = DateTime.Now });

            // Initialize play time and last seen for the user if it doesn't exist
            if (!settings.MainSettings.PlayTime.ContainsKey(args.User.Name))
            {
                settings.MainSettings.PlayTime[args.User.Name] = TimeSpan.Zero;
                settings.MainSettings.LastSeen[args.User.Name] = DateTime.Now;
                config.Save(settings);
            }

            await bot.UpdatePresence(null, null);

            // Check if posting player events is disabled
            if (!settings.MainSettings.PostPlayerEvents)
                return;
            foreach (var (socketGuild, eventChannel) in from SocketGuild socketGuild in bot.client.Guilds
                                                        let eventChannel = bot.GetEventChannel(socketGuild.Id, settings.MainSettings.PostPlayerEventsChannel)
                                                        select (socketGuild, eventChannel))
            {
                if (eventChannel == null || !bot.CanBotSendMessageInChannel(bot.client, eventChannel.Id))
                {
                    log.Error($"No permission to post to channel: {eventChannel?.Name ?? "Unknown"}.");
                    return;
                }

                var joinColor = helper.GetColour("PlayerJoin", settings.ColourSettings.ServerPlayerJoinEventColour);
                string userName = args.User.Name;
                var embed = new EmbedBuilder
                {
                    Title = "Server Event",
                    Description = string.IsNullOrEmpty(userName) ? $"A player joined the {application.ApplicationName} server." : $"{userName} joined the {application.ApplicationName} server.",
                    ThumbnailUrl = settings.MainSettings.GameImageURL,
                    Color = joinColor != default(Color) ? joinColor : Color.Green,
                    Footer = new EmbedFooterBuilder { Text = settings.MainSettings.BotTagline },
                    Timestamp = DateTimeOffset.Now
                };
                await bot.client.GetGuild(socketGuild.Id).GetTextChannel(eventChannel.Id).SendMessageAsync(embed: embed.Build());
            }
        }

        /// <summary>
        /// Event handler for when a user leaves the server.
        /// </summary>
        public async void UserLeaves(object sender, UserEventArgs args)
        {
            if (args?.User == null)
            {
                log.Error("User event argument is null.");
                return;
            }

            //if name is empty, log and return
            if (args.User.Name == "")
            {
                log.Info("User left but name is empty. Playtime will not be logged. User ID: " + args.UserID.ToString());
                return;
            }

            try
            {
                var player = infoPanel.playerPlayTimes.Find(p => p.PlayerName == args.User.Name);
                if (player == null)
                {
                    log.Warning($"Player {args.User.Name} not found in playtime list.");
                    return;
                }

                player.LeaveTime = DateTime.Now;

                if (!settings.MainSettings.PlayTime.ContainsKey(args.User.Name))
                {
                    settings.MainSettings.PlayTime[args.User.Name] = TimeSpan.Zero;
                }

                var sessionPlayTime = player.LeaveTime - player.JoinTime;
                settings.MainSettings.PlayTime[args.User.Name] += sessionPlayTime;
                settings.MainSettings.LastSeen[args.User.Name] = DateTime.Now;
                config.Save(settings);

                infoPanel.playerPlayTimes.RemoveAll(p => p.PlayerName == args.User.Name);

                log.Info("Player leave event processed.");
                await helper.ExecuteWithDelay(2000, action: () => _ = bot.UpdatePresence(null, null));
            }
            catch (Exception ex)
            {
                log.Error($"Error processing player leave: {ex.Message}");
            }

            // Check if posting player events is disabled
            if (!settings.MainSettings.PostPlayerEvents)
                return;
            foreach (var (socketGuild, eventChannel) in from SocketGuild socketGuild in bot.client.Guilds
                                                        let eventChannel = bot.GetEventChannel(socketGuild.Id, settings.MainSettings.PostPlayerEventsChannel)
                                                        select (socketGuild, eventChannel))
            {
                if (eventChannel == null || !bot.CanBotSendMessageInChannel(bot.client, eventChannel.Id))
                {
                    log.Error($"No permission to post to channel: {eventChannel?.Name ?? "Unknown"}.");
                    return;
                }

                var leaveColor = helper.GetColour("PlayerLeave", settings.ColourSettings.ServerPlayerLeaveEventColour);
                string userName = args.User.Name;
                var embed = new EmbedBuilder
                {
                    Title = "Server Event",
                    Description = string.IsNullOrEmpty(userName) ? $"A player left the {application.ApplicationName} server." : $"{userName} left the {application.ApplicationName} server.",
                    ThumbnailUrl = settings.MainSettings.GameImageURL,
                    Color = leaveColor != default(Color) ? leaveColor : Color.Red,
                    Footer = new EmbedFooterBuilder { Text = settings.MainSettings.BotTagline },
                    Timestamp = DateTimeOffset.Now
                };
                await bot.client.GetGuild(socketGuild.Id).GetTextChannel(eventChannel.Id).SendMessageAsync(embed: embed.Build());
            }
        }

        /// <summary>
        /// Logs a message with an information level.
        /// </summary>
        public Task Log(LogMessage msg)
        {
            log.Info(msg.ToString() ?? "Unknown log message");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Runs on SettingsModified event.
        /// </summary>
        public void Settings_SettingModified(object sender, SettingModifiedEventArgs e)
        {
            if (settings.MainSettings.BotActive)
            {
                try
                {
                    if (bot.client == null || bot.client.ConnectionState == ConnectionState.Disconnected)
                    {
                        _ = bot.ConnectDiscordAsync(settings.MainSettings.BotToken);

                        log.MessageLogged += Log_MessageLogged;
                        application.StateChanged += ApplicationStateChange;
                        if (application is IHasSimpleUserList hasSimpleUserList)
                        {
                            hasSimpleUserList.UserJoins += UserJoins;
                            hasSimpleUserList.UserLeaves += UserLeaves;
                        }
                    }
                }
                catch (Exception exception)
                {
                    log.Error($"Error with the Discord Bot: {exception.Message}");
                }
            }
            else
            {
                if (bot.client?.ConnectionState == ConnectionState.Connected)
                {
                    bot.client.ButtonExecuted -= infoPanel.OnButtonPress;
                    bot.client.Log -= Log;
                    bot.client.Ready -= bot.ClientReady;
                    bot.client.SlashCommandExecuted -= bot.SlashCommandHandler;
                    bot.client.MessageReceived -= bot.MessageHandler;

                    try
                    {
                        _ = bot.client.LogoutAsync();
                    }
                    catch (Exception exception)
                    {
                        log.Error($"Error logging out from Discord: {exception.Message}");
                    }
                }

                log.MessageLogged -= Log_MessageLogged;
                application.StateChanged -= ApplicationStateChange;
                if (application is IHasSimpleUserList hasSimpleUserList)
                {
                    hasSimpleUserList.UserJoins -= UserJoins;
                    hasSimpleUserList.UserLeaves -= UserLeaves;
                }
            }
        }

        /// <summary>
        /// Runs on MessageLogged event.
        /// </summary>
        public void Log_MessageLogged(object sender, LogEventArgs e)
        {
            if (e == null)
            {
                log.Error("LogEventArgs is null.");
                return;
            }

            string cleanMessage = e.Message.Replace("`", "'");

            if (e.Level == LogLevels.Chat.ToString() && settings.MainSettings.SendChatToDiscord && !string.IsNullOrEmpty(settings.MainSettings.ChatToDiscordChannel))
            {
                _ = bot.ChatMessageSend(cleanMessage);
            }

            if ((e.Level == LogLevels.Console.ToString() || e.Level == LogLevels.Chat.ToString()) && settings.MainSettings.SendConsoleToDiscord && !string.IsNullOrEmpty(settings.MainSettings.ConsoleToDiscordChannel))
            {
                bot.consoleOutput.Add(cleanMessage);
            }

            if (e.Level == LogLevels.Console.ToString() && settings.GameSpecificSettings.ValheimJoinCode)
            {
                string pattern = @"join code (\d+)";
                Match match = Regex.Match(e.Message, pattern);

                if (match.Success)
                {
                    infoPanel.valheimJoinCode = match.Groups[1].Value;
                }
            }
        }

        /// <summary>
        /// Updates the bot's presence when the application state changes.
        /// </summary>
        public async void ApplicationStateChange(object sender, ApplicationStateChangeEventArgs args)
        {
            _ = bot.UpdatePresence(sender, args);

            if (!settings.MainSettings.PostStatusEvents)
                return;
            foreach (var (socketGuild, eventChannel) in from SocketGuild socketGuild in bot.client.Guilds
                                                        let eventChannel = bot.GetEventChannel(socketGuild.Id, settings.MainSettings.PostStatusEventsChannel)
                                                        select (socketGuild, eventChannel))
            {
                if (eventChannel == null || !bot.CanBotSendMessageInChannel(bot.client, eventChannel.Id))
                {
                    log.Error($"No permission to post to channel: {eventChannel?.Name ?? "Unknown"}.");
                    return;
                }

                Color statusColour = Color.Orange;

                if (application.State == ApplicationState.Ready)
                {
                    statusColour = Color.Green;
                }

                if (application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed)
                {
                    statusColour = Color.Red;
                }

                var embed = new EmbedBuilder
                {
                    Title = "Server Event",
                    Description = $"{application.ApplicationName} status change : " + application.State.ToString(),
                    ThumbnailUrl = settings.MainSettings.GameImageURL,
                    Color = statusColour,
                    Footer = new EmbedFooterBuilder { Text = settings.MainSettings.BotTagline },
                    Timestamp = DateTimeOffset.Now
                };
                await bot.client.GetGuild(socketGuild.Id).GetTextChannel(eventChannel.Id).SendMessageAsync(embed: embed.Build());
            }
        }

        /// <summary>
        /// Handles backup completion event.
        /// </summary>
        public void OnBackupComplete(object sender, EventArgs e)
        {
            UnregisterBackupEvents();
            currentUser?.SendMessageAsync("Backup completed successfully.");
        }

        /// <summary>
        /// Handles backup failure event.
        /// </summary>
        public void OnBackupFailed(object sender, EventArgs e)
        {
            UnregisterBackupEvents();
            currentUser?.SendMessageAsync("Backup failed, check the AMP logs for information.");
        }

        /// <summary>
        /// Handles backup starting event.
        /// </summary>
        public void OnBackupStarting(object sender, EventArgs e)
        {
            currentUser?.SendMessageAsync("Backup is starting.");
        }

        private void UnregisterBackupEvents()
        {
            backupProvider.BackupActionComplete -= OnBackupComplete;
            backupProvider.BackupActionFailed -= OnBackupFailed;
            backupProvider.BackupActionStarting -= OnBackupStarting;
        }
    }
}
