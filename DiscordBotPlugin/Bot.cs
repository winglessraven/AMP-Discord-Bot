using Discord.WebSocket;
using Discord;
using ModuleShared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace DiscordBotPlugin
{
    internal class Bot
    {
        public DiscordSocketClient client;
        private readonly Settings settings;
        private readonly IAMPInstanceInfo aMPInstanceInfo;
        private readonly IApplicationWrapper application;
        private readonly ILogger log;
        private Events events;
        private readonly Helpers helper;
        private readonly InfoPanel infoPanel;
        private readonly Commands commands;

        public Bot(Settings settings, IAMPInstanceInfo aMPInstanceInfo, IApplicationWrapper application, ILogger log, Events events, Helpers helper, InfoPanel infoPanel, Commands commands)
        {
            this.settings = settings;
            this.aMPInstanceInfo = aMPInstanceInfo;
            this.application = application;
            this.log = log;
            this.events = events;
            this.helper = helper;
            this.infoPanel = infoPanel;
            this.commands = commands;
        }

        public void SetEvents(Events events)
        {
            this.events = events;
        }

        public List<String> consoleOutput = new List<String>();

        /// <summary>
        /// Async task to handle the Discord connection and call the status check
        /// </summary>
        /// <param name="BotToken">Discord Bot Token</param>
        /// <returns>Task</returns>
        public async Task ConnectDiscordAsync(string BotToken)
        {
            if (string.IsNullOrEmpty(BotToken))
            {
                log.Error("Bot token is not provided.");
                return;
            }

            DiscordSocketConfig config;

            if (client == null)
            {
                // Initialize DiscordSocketClient with the necessary config
                config = settings.MainSettings.SendChatToDiscord || settings.MainSettings.SendDiscordChatToServer
                    ? new DiscordSocketConfig { GatewayIntents = GatewayIntents.DirectMessages | GatewayIntents.GuildMessages | GatewayIntents.Guilds | GatewayIntents.MessageContent }
                    : new DiscordSocketConfig { GatewayIntents = GatewayIntents.DirectMessages | GatewayIntents.GuildMessages | GatewayIntents.Guilds };

                // Handle mismatch timezones
                config.UseInteractionSnowflakeDate = false;

                if (settings.MainSettings.DiscordDebugMode)
                    config.LogLevel = LogSeverity.Debug;

                client = new DiscordSocketClient(config);

                // Attach event handlers for logs and events
                client.Log += events.Log;
                client.ButtonExecuted += infoPanel.OnButtonPress;
                client.Ready += ClientReady;
                client.SlashCommandExecuted += SlashCommandHandler;
                if (settings.MainSettings.SendChatToDiscord || settings.MainSettings.SendDiscordChatToServer)
                    client.MessageReceived += MessageHandler;
            }

            try
            {
                await client.LoginAsync(TokenType.Bot, BotToken);
                await client.StartAsync();
                log.Info("Bot successfully connected.");

                _ = SetStatus();
                _ = ConsoleOutputSend();
                _ = infoPanel.UpdateWebPanel(Path.Combine(Environment.CurrentDirectory, "WebPanel-" + aMPInstanceInfo.InstanceName));

                await Task.Delay(-1); // Blocks task until stopped
            }
            catch (Exception ex)
            {
                log.Error("Error during bot connection: " + ex.Message);
            }
        }

        public async Task UpdatePresence(object sender, ApplicationStateChangeEventArgs args, bool force = false)
        {
            if (client == null)
            {
                log.Warning("Bot client is not connected. Cannot update presence.");
                return;
            }

            if (settings.MainSettings.BotActive && (args == null || args.PreviousState != args.NextState || force) && client.ConnectionState == ConnectionState.Connected)
            {
                try
                {

                    string currentActivity = client.Activity?.Name ?? "";
                    if (currentActivity != "")
                    {
                        var customStatus = client.Activity as CustomStatusGame;
                        if (customStatus != null)
                        {
                            currentActivity = customStatus.State;
                        }
                    }
                    UserStatus currentStatus = client.Status;
                    UserStatus status;

                    // Get the current user and max user count
                    IHasSimpleUserList hasSimpleUserList = application as IHasSimpleUserList;
                    var onlinePlayers = hasSimpleUserList.Users.Count;
                    var maximumPlayers = hasSimpleUserList.MaxUsers;

                    // If the server is stopped or in a failed state, set the presence to DoNotDisturb
                    if (application.State == ApplicationState.Stopped || application.State == ApplicationState.Failed)
                    {
                        status = UserStatus.DoNotDisturb;

                        // If there are still players listed in the timer, remove them
                        if (infoPanel.playerPlayTimes.Count != 0)
                            helper.ClearAllPlayTimes();
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
                        if (infoPanel.playerPlayTimes.Count != 0)
                            helper.ClearAllPlayTimes();
                    }

                    if (status != currentStatus)
                    {
                        await client.SetStatusAsync(status);
                    }

                    string presenceString = helper.OnlineBotPresenceString(onlinePlayers, maximumPlayers);

                    // Set the presence/activity based on the server state
                    if (application.State == ApplicationState.Ready)
                    {
                        if (currentActivity != presenceString)
                        {
                            await client.SetActivityAsync(new CustomStatusGame(helper.OnlineBotPresenceString(onlinePlayers, maximumPlayers)));
                        }
                    }
                    else
                    {
                        if (presenceString != application.State.ToString())
                        {
                            await client.SetActivityAsync(new CustomStatusGame(application.State.ToString()));
                        }
                    }
                }
                catch (System.Net.WebException exception)
                {
                    await client.SetGameAsync("Server Offline", null, ActivityType.Watching);
                    await client.SetStatusAsync(UserStatus.DoNotDisturb);
                    log.Error("Exception: " + exception.Message);
                }
            }
        }

        /// <summary>
        /// Looping task to update bot status/presence
        /// </summary>
        /// <returns></returns>
        public async Task SetStatus()
        {
            // While the bot is active, update its status
            while (settings != null && settings.MainSettings != null && settings.MainSettings.BotActive && client != null)
            {
                try
                {

                    // Get the current user and max user count
                    IHasSimpleUserList hasSimpleUserList = application as IHasSimpleUserList;
                    var onlinePlayers = hasSimpleUserList.Users.Count;
                    var maximumPlayers = hasSimpleUserList.MaxUsers;

                    var clientConnectionState = client.ConnectionState.ToString();
                    log.Debug("Server Status: " + application.State + " || Players: " + onlinePlayers + "/" + maximumPlayers + " || CPU: " + application.GetCPUUsage() + "% || Memory: " + helper.GetMemoryUsage() + ", Bot Connection Status: " + clientConnectionState);

                    // Update the embed if it exists
                    if (settings.MainSettings.InfoMessageDetails != null && settings.MainSettings.InfoMessageDetails.Count > 0)
                    {
                        _ = infoPanel.GetServerInfo(true, null, false);
                    }

                    //change presence if required
                    _ = UpdatePresence(null, null, true);
                }
                catch (System.Net.WebException exception)
                {
                    await client.SetGameAsync("Server Offline", null, ActivityType.Watching);
                    await client.SetStatusAsync(UserStatus.DoNotDisturb);
                    log.Error("Exception: " + exception.Message);
                }

                // Loop the task according to the bot refresh interval setting
                await Task.Delay(settings.MainSettings.BotRefreshInterval * 1000);
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

            if (settings.MainSettings.RemoveBotName)
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

                commandList.Add(new SlashCommandBuilder()
                    .WithName("take-backup")
                    .WithDescription("Take a backup of the instance"));
            }
            else
            {
                if (client != null && client.CurrentUser != null)
                {
                    string botName = client.CurrentUser.Username.ToLower();

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
                        .AddOption("playername", ApplicationCommandOptionType.String, "Get info for a specific player", isRequired: false))
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("take-backup")
                        .WithDescription("Take a backup of the instance")
                        .WithType(ApplicationCommandOptionType.SubCommand));

                    // Add the base command to the command list
                    commandList.Add(baseCommand);
                }
                else
                {
                    log.Error("Client or CurrentUser is null in ClientReady method.");
                    return;
                }
            }

            try
            {
                // Build the application command properties from the command builders
                foreach (SlashCommandBuilder command in commandList)
                {
                    applicationCommandProperties.Add(command.Build());
                }

                // Bulk overwrite the global application commands with the built command properties
                await client.BulkOverwriteGlobalApplicationCommandsAsync(applicationCommandProperties.ToArray());
            }
            catch (Exception exception)
            {
                log.Error(exception.Message);
            }
        }

        /// <summary>
        /// Handles incoming socket messages.
        /// </summary>
        /// <param name="message">The incoming socket message.</param>
        public async Task MessageHandler(SocketMessage message)
        {
            // If sending Discord chat to server is disabled or the message is from a bot, return and do nothing further
            if (!settings.MainSettings.SendDiscordChatToServer || message.Author.IsBot)
                return;

            // Check if the message is in the specified chat-to-Discord channel
            if (message.Channel.Name.Equals(settings.MainSettings.ChatToDiscordChannel) || message.Channel.Id.ToString().Equals(settings.MainSettings.ChatToDiscordChannel))
            {
                // Send the chat command to the server
                await commands.SendChatCommand(message.Author.Username, message.CleanContent);
            }
        }

        /// <summary>
        /// Handles incoming socket slash commands.
        /// </summary>
        /// <param name="command">The incoming socket slash command.</param>
        public async Task SlashCommandHandler(SocketSlashCommand command)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                log.Info($"[CMD] Received slash command interaction: User={command.User.Username}({command.User.Id}), Guild={command.GuildId}, Channel={command.ChannelId}, CommandId={command.CommandId}, CommandName={command.Data.Name}, Options={JsonConvert.SerializeObject(command.Data.Options)}");

                log.Debug($"[CMD] Attempting to defer command {command.CommandId} (Ephemeral=True)...");
                var deferTime = DateTime.UtcNow;
                await command.DeferAsync(ephemeral: true);
                log.Debug($"[CMD] Successfully deferred command {command.CommandId}. Time taken: {(deferTime - startTime).TotalMilliseconds}ms.");

                string commandName = command.Data.Name;
                if (!settings.MainSettings.RemoveBotName && command.Data.Options.Count > 0)
                {
                    commandName = command.Data.Options.First().Name;
                }

                // Using bot name as the base command
                if (!settings.MainSettings.RemoveBotName)
                {
                    // Leaderboard permissionless
                    if (command.Data.Options != null && command.Data.Options.First().Name.Equals("show-playtime"))
                    {
                        log.Debug($"[CMD] Permission not required, start processing interaction for command '{commandName}'");
                        if (command.Data.Options.First().Options.Count > 0)
                        {
                            string playerName = command.Data.Options.First().Options.First().Value.ToString();
                            string playTime = helper.GetPlayTimeLeaderBoard(1, true, playerName, false, false);
                            await command.FollowupAsync("Playtime for " + playerName + ": " + playTime, ephemeral: true);
                        }
                        else
                        {
                            await ShowPlayerPlayTime(command);
                            await command.FollowupAsync("Playtime leaderboard displayed", ephemeral: true);
                        }
                        log.Info($"[CMD] Completed processing interaction for command '{commandName}'");
                        return;
                    }

                    // Initialize bool for permission check
                    bool hasServerPermission = false;

                    log.Debug($"[CMD] Performing permission check for user {command.User.Username} (ID: {command.User.Id}) for command '{commandName}'. Required Role(s): '{settings.MainSettings.DiscordRole}', Restrict Functions: {settings.MainSettings.RestrictFunctions}");

                    if (command.User is SocketGuildUser user)
                    {
                        hasServerPermission = HasServerPermission(user);
                        log.Debug($"[CMD]] Permission check result for user {command.User.Username}: {hasServerPermission}");
                    }

                    if (!hasServerPermission)
                    {
                        log.Warning($"[CMD] User {command.User.Username} lacks permission for command '{commandName}'. Responding with permission denied.");
                        await command.FollowupAsync("You do not have permission to use this command!", ephemeral: true);
                        log.Info($"[CMD] Permission denied response sent for command {command.CommandId}. Total time: {(DateTime.UtcNow - startTime).TotalMilliseconds}ms.");
                        return;
                    }


                    log.Debug($"[CMD] Start processing interaction for command '{commandName}'");
                    switch (command.Data.Options.First().Name)
                    {
                        case "info":
                            bool buttonless = command.Data.Options.First().Options.Count > 0 && Convert.ToBoolean(command.Data.Options.First().Options.First().Value.ToString());
                            await infoPanel.GetServerInfo(false, command, buttonless);
                            await command.FollowupAsync("Info panel created", ephemeral: true);
                            break;
                        case "start-server":
                            application.Start();
                            await CommandResponse("Start Server", command);
                            await command.FollowupAsync("Start command sent to the application", ephemeral: true);
                            break;
                        case "stop-server":
                            application.Stop();
                            await CommandResponse("Stop Server", command);
                            await command.FollowupAsync("Stop command sent to the application", ephemeral: true);
                            break;
                        case "restart-server":
                            application.Restart();
                            await CommandResponse("Restart Server", command);
                            await command.FollowupAsync("Restart command sent to the application", ephemeral: true);
                            break;
                        case "kill-server":
                            application.Kill();
                            await CommandResponse("Kill Server", command);
                            await command.FollowupAsync("Kill command sent to the application", ephemeral: true);
                            break;
                        case "update-server":
                            application.Update();
                            await CommandResponse("Update Server", command);
                            await command.FollowupAsync("Update command sent to the application", ephemeral: true);
                            break;
                        case "console":
                            await commands.SendConsoleCommand(command);
                            string consoleCommand = command.Data.Options.First().Options.First().Value.ToString();
                            await CommandResponse("`" + consoleCommand + "` console ", command);
                            await command.FollowupAsync("Command sent to the server: `" + consoleCommand + "`", ephemeral: true);
                            break;
                        case "full-playtime-list":
                            if (command.Data.Options.First().Options.Count > 0)
                            {
                                string playerName = command.Data.Options.First().Options.First().Value.ToString();
                                string playTime = helper.GetPlayTimeLeaderBoard(1, true, playerName, true, false);
                                await command.FollowupAsync("Playtime for " + playerName + ": " + playTime, ephemeral: true);
                            }
                            else
                            {
                                string playTime = helper.GetPlayTimeLeaderBoard(1000, false, null, true, false);
                                if (playTime.Length > 2000)
                                {
                                    string path = Path.Combine(application.BaseDirectory, "full-playtime-list.txt");
                                    try
                                    {
                                        playTime = playTime.Replace("```", "");
                                        using (FileStream fileStream = File.Create(path))
                                        {
                                            byte[] text = new UTF8Encoding(true).GetBytes(playTime);
                                            await fileStream.WriteAsync(text, 0, text.Length);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        log.Error("Error creating file: " + ex.Message);
                                    }

                                    await command.FollowupWithFileAsync(path, ephemeral: true);

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
                            }
                            break;
                        case "take-backup":
                            commands.BackupServer((SocketGuildUser)command.User);
                            await CommandResponse("Backup Server", command);
                            await command.FollowupAsync("Backup command sent to the panel", ephemeral: true);
                            break;
                    }
                    log.Info($"[CMD] Completed processing interaction for command '{commandName}'");
                }
                else
                {
                    // No bot prefix
                    // Leaderboard permissionless
                    if (command.Data.Name.Equals("show-playtime"))
                    {
                        log.Debug($"[CMD] Permission not required, start processing interaction for command '{commandName}'");
                        if (command.Data.Options.Count > 0)
                        {
                            string playerName = command.Data.Options.First().Value.ToString();
                            string playTime = helper.GetPlayTimeLeaderBoard(1, true, playerName, false, false);
                            await command.FollowupAsync("Playtime for " + playerName + ": " + playTime, ephemeral: true);
                        }
                        else
                        {
                            await ShowPlayerPlayTime(command);
                            await command.FollowupAsync("Playtime leaderboard displayed", ephemeral: true);
                        }
                        log.Info($"[CMD] Completed processing interaction for command '{commandName}'");
                        return;
                    }

                    // Initialize bool for permission check
                    bool hasServerPermission = false;

                    log.Debug($"[CMD] Performing permission check for user {command.User.Username} (ID: {command.User.Id}) for command '{commandName}'. Required Role(s): '{settings.MainSettings.DiscordRole}', Restrict Functions: {settings.MainSettings.RestrictFunctions}");
                    if (command.User is SocketGuildUser user)
                    {
                        hasServerPermission = HasServerPermission(user);
                        log.Debug($"[CMD]] Permission check result for user {command.User.Username}: {hasServerPermission}");
                    }

                    if (!hasServerPermission)
                    {
                        log.Warning($"[CMD] User {command.User.Username} lacks permission for command '{commandName}'. Responding with permission denied.");
                        await command.FollowupAsync("You do not have permission to use this command!", ephemeral: true);
                        log.Info($"[CMD] Permission denied response sent for command {command.CommandId}. Total time: {(DateTime.UtcNow - startTime).TotalMilliseconds}ms.");
                        return;
                    }

                    log.Debug($"[CMD] Start processing interaction for command '{commandName}'");
                    switch (command.Data.Name)
                    {
                        case "info":
                            bool buttonless = command.Data.Options.Count > 0 && Convert.ToBoolean(command.Data.Options.First().Value.ToString());
                            await infoPanel.GetServerInfo(false, command, buttonless);
                            await command.FollowupAsync("Info panel created", ephemeral: true);
                            break;
                        case "start-server":
                            application.Start();
                            await CommandResponse("Start Server", command);
                            await command.FollowupAsync("Start command sent to the application", ephemeral: true);
                            break;
                        case "stop-server":
                            application.Stop();
                            await CommandResponse("Stop Server", command);
                            await command.FollowupAsync("Stop command sent to the application", ephemeral: true);
                            break;
                        case "restart-server":
                            application.Restart();
                            await CommandResponse("Restart Server", command);
                            await command.FollowupAsync("Restart command sent to the application", ephemeral: true);
                            break;
                        case "kill-server":
                            application.Kill();
                            await CommandResponse("Kill Server", command);
                            await command.FollowupAsync("Kill command sent to the application", ephemeral: true);
                            break;
                        case "update-server":
                            application.Update();
                            await CommandResponse("Update Server", command);
                            await command.FollowupAsync("Update command sent to the application", ephemeral: true);
                            break;
                        case "console":
                            await commands.SendConsoleCommand(command);
                            await CommandResponse("`" + command.Data.Options.First().Value.ToString() + "` console ", command);
                            await command.FollowupAsync("Command sent to the server: `" + command.Data.Options.First().Value.ToString() + "`", ephemeral: true);
                            break;
                        case "full-playtime-list":
                            if (command.Data.Options.Count > 0)
                            {
                                string playerName = command.Data.Options.First().Value.ToString();
                                string playTime = helper.GetPlayTimeLeaderBoard(1, true, playerName, true, false);
                                await command.FollowupAsync("Playtime for " + playerName + ": " + playTime, ephemeral: true);
                            }
                            else
                            {
                                string playTime = helper.GetPlayTimeLeaderBoard(1000, false, null, true, false);
                                if (playTime.Length > 2000)
                                {
                                    string path = Path.Combine(application.BaseDirectory, "full-playtime-list.txt");
                                    try
                                    {
                                        playTime = playTime.Replace("```", "");
                                        using FileStream fileStream = File.Create(path);
                                        byte[] text = new UTF8Encoding(true).GetBytes(playTime);
                                        await fileStream.WriteAsync(text, 0, text.Length);
                                    }
                                    catch (Exception ex)
                                    {
                                        log.Error("Error creating file: " + ex.Message);
                                    }

                                    await command.FollowupWithFileAsync(path, ephemeral: true);

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
                                    await command.FollowupAsync(playTime, ephemeral: true);
                                }
                            }
                            break;
                        case "take-backup":
                            commands.BackupServer((SocketGuildUser)command.User);
                            await CommandResponse("Backup Server", command);
                            await command.FollowupAsync("Backup command sent to the panel", ephemeral: true);
                            break;
                    }
                    log.Info($"[CMD] Completed processing interaction for command '{commandName}'");
                }
            }
            catch (Exception ex)
            {
                log.Error($"Error during SlashCommandHandler: {ex.Message}");
                log.Error($"Full exception details: {ex}");
            }


        }

        /// <summary>
        /// Retrieves the event channel from the specified guild by ID or name.
        /// </summary>
        /// <param name="guildID">The ID of the guild.</param>
        /// <param name="channel">The ID or name of the channel.</param>
        /// <returns>The event channel if found; otherwise, null.</returns>
        public SocketGuildChannel GetEventChannel(ulong guildID, string channel)
        {
            if (client == null)
            {
                log.Error("Client is null in GetEventChannel.");
                return null;
            }

            var guild = client.GetGuild(guildID);

            if (guild == null)
            {
                log.Error($"Guild with ID {guildID} not found.");
                return null;
            }

            SocketGuildChannel eventChannel;

            // Try by ID first
            try
            {
                eventChannel = client.GetGuild(guildID).Channels.FirstOrDefault(x => x.Id == Convert.ToUInt64(channel));
            }
            catch
            {
                // If the ID retrieval fails, try by name
                eventChannel = client.GetGuild(guildID).Channels.FirstOrDefault(x => x.Name == channel);
            }

            return eventChannel;
        }

        public bool HasServerPermission(SocketGuildUser user)
        {
            if (client != null)
            {
                client.PurgeUserCache();
            }
            else
            {
                log.Warning("Client is null in HasServerPermission.");
            }

            if (settings != null)
            {
                // The user has the permission if either RestrictFunctions is turned off, or if they are part of the appropriate role.
                string[] roles = settings.MainSettings.DiscordRole.Split(',');
                return !settings.MainSettings.RestrictFunctions || user.Roles.Any(r => roles.Contains(r.Name)) || user.Roles.Any(r => roles.Contains(r.Id.ToString()));
            }
            else
            {
                log.Warning("Settings is null in HasServerPermission.");
                return false;
            }
        }

        public bool CanBotSendMessageInChannel(DiscordSocketClient client, ulong channelId)
        {
            if (client == null)
            {
                log.Error("Client is null in CanBotSendMessageInChannel");
                return false;
            }
            // Get the channel object from the channel ID
            var channel = client.GetChannel(channelId) as SocketTextChannel;

            if (channel == null)
            {
                Console.WriteLine("Channel not found or is not a text channel.");
                return false;
            }

            // Get the current user (the bot) as a user object within the context of the guild
            var botUser = channel.Guild.GetUser(client.CurrentUser.Id);

            // Get the bot's permissions in the channel
            var permissions = botUser.GetPermissions(channel);

            // Check if the bot has SendMessage permission in the channel
            return permissions.Has(ChannelPermission.SendMessages);
        }

        /// <summary>
        /// Handles button response and logs the command if enabled in settings.
        /// </summary>
        /// <param name="Command">Command received from the button.</param>
        /// <param name="arg">SocketMessageComponent object containing information about the button click.</param>
        public async Task ButtonResponse(string Command, SocketMessageComponent arg)
        {
            log.Debug($"[BTN_LOG] Preparing button log response for action '{Command}' by user {arg.User.Username} (ButtonId: {arg.Data.CustomId})");

            // Only log if option is enabled
            if (settings.MainSettings.LogButtonsAndCommands)
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
                    embed.Color = !string.IsNullOrEmpty(settings.ColourSettings.ServerStartColour)
                        ? helper.GetColour("Start", settings.ColourSettings.ServerStartColour)
                        : Color.Green;
                }
                // Stop command
                else if (Command.Equals("Stop"))
                {
                    embed.Color = !string.IsNullOrEmpty(settings.ColourSettings.ServerStopColour)
                        ? helper.GetColour("Stop", settings.ColourSettings.ServerStopColour)
                        : Color.Red;
                }
                // Restart command
                else if (Command.Equals("Restart"))
                {
                    embed.Color = !string.IsNullOrEmpty(settings.ColourSettings.ServerRestartColour)
                        ? helper.GetColour("Restart", settings.ColourSettings.ServerRestartColour)
                        : Color.Orange;
                }
                // Kill command
                else if (Command.Equals("Kill"))
                {
                    embed.Color = !string.IsNullOrEmpty(settings.ColourSettings.ServerKillColour)
                        ? helper.GetColour("Kill", settings.ColourSettings.ServerKillColour)
                        : Color.Red;
                }
                // Update command
                else if (Command.Equals("Update"))
                {
                    embed.Color = !string.IsNullOrEmpty(settings.ColourSettings.ServerUpdateColour)
                        ? helper.GetColour("Update", settings.ColourSettings.ServerUpdateColour)
                        : Color.Blue;
                }
                // Manage command
                else if (Command.Equals("Manage"))
                {
                    embed.Color = !string.IsNullOrEmpty(settings.ColourSettings.ManageLinkColour)
                        ? helper.GetColour("Manage", settings.ColourSettings.ManageLinkColour)
                        : Color.Blue;
                }

                embed.ThumbnailUrl = settings.MainSettings.GameImageURL;
                embed.AddField("Requested by", arg.User.Mention, true);
                embed.WithFooter(settings.MainSettings.BotTagline);
                embed.WithCurrentTimestamp();

                var chnl = arg.Message.Channel as SocketGuildChannel;
                var guild = chnl.Guild.Id;
                var logChannel = GetEventChannel(guild, settings.MainSettings.ButtonResponseChannel);
                var channelID = arg.Message.Channel.Id;

                if (logChannel != null)
                    channelID = logChannel.Id;

                log.Debug($"Guild: {guild} || Channel: {channelID}");

                if (!CanBotSendMessageInChannel(client, channelID))
                {
                    log.Error("No permission to post to channel: " + logChannel);
                    return;
                }

                await client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build());
            }
        }

        /// <summary>
        /// Sends a chat message to the specified text channel in each guild the bot is a member of.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ChatMessageSend(string Message)
        {
            if (client == null)
            {
                log.Error("Client is null in ChatMessageSend");
                return;
            }

            // Get all guilds the bot is a member of
            var guilds = client.Guilds;
            foreach (var (guildID, eventChannel) in
            // Iterate over each guild
            from SocketGuild guild in guilds// Find the text channel with the specified name
            let guildID = guild.Id
            let eventChannel = GetEventChannel(guildID, settings.MainSettings.ChatToDiscordChannel)
            where eventChannel != null
            select (guildID, eventChannel))
            {
                // Send the message to the channel
                await client.GetGuild(guildID).GetTextChannel(eventChannel.Id).SendMessageAsync("`" + Message + "`");
            }
        }

        /// <summary>
        /// Sends console output to the specified text channel in each guild the bot is a member of.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task ConsoleOutputSend()
        {
            while (settings != null &&
                   settings.MainSettings != null &&
                   settings.MainSettings.SendConsoleToDiscord &&
                   settings.MainSettings.BotActive)
            {
                if (consoleOutput.Count > 0)
                {
                    try
                    {
                        // Create a duplicate list of console output messages
                        List<string> messages = new List<string>(consoleOutput);
                        consoleOutput.Clear();

                        // Split the output into multiple strings, each presented within a code block
                        List<string> outputStrings = helper.SplitOutputIntoCodeBlocks(messages);

                        // Get all guilds the bot is a member of, ensuring it's not null
                        var guilds = client.Guilds;

                        // Iterate over each output string
                        foreach (string output in outputStrings)
                        {

                            //sanitize possible passwords
                            string pattern = @"(""Password"":\s*"")(.*?)("")";
                            string redacted = Regex.Replace(output, pattern, "$1[REDACTED]$3");

                            // Use LINQ to select non-null text channels
                            var textChannels = (from SocketGuild guild in guilds
                                                let eventChannel = GetEventChannel(guild.Id, settings.MainSettings.ConsoleToDiscordChannel)
                                                where eventChannel != null
                                                let textChannel = guild.GetTextChannel(eventChannel.Id)
                                                where textChannel != null
                                                select textChannel)?.ToList() ?? new List<SocketTextChannel>();

                            foreach (var textChannel in textChannels)
                            {
                                await textChannel.SendMessageAsync(redacted);
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
                ThumbnailUrl = settings.MainSettings.GameImageURL,
                Description = helper.GetPlayTimeLeaderBoard(15, false, null, false, false),
                Color = !string.IsNullOrEmpty(settings.ColourSettings.PlaytimeLeaderboardColour)
                    ? helper.GetColour("Leaderboard", settings.ColourSettings.PlaytimeLeaderboardColour)
                    : Color.DarkGrey
            };

            // Set the footer and the current timestamp
            embed.WithFooter(settings.MainSettings.BotTagline)
                 .WithCurrentTimestamp();

            // Get the guild and channel IDs
            var guildId = (msg.Channel as SocketGuildChannel).Guild.Id;
            var channelId = msg.Channel.Id;

            // Post the leaderboard in the specified channel
            await client.GetGuild(guildId).GetTextChannel(channelId).SendMessageAsync(embed: embed.Build());

        }

        /// <summary>
        /// Sends a command response with an embed message.
        /// </summary>
        /// <param name="command">The command received.</param>
        /// <param name="arg">The received command arguments.</param>
        private async Task CommandResponse(string command, SocketSlashCommand arg)
        {
            // Only log if option is enabled
            if (!settings.MainSettings.LogButtonsAndCommands)
                return;

            if (arg == null || arg.Channel == null || arg.User == null)
            {
                log.Error("Invalid arguments in CommandResponse.");
                return;
            }

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
                embed.Color = helper.GetColour("Start", settings.ColourSettings.ServerStartColour);
                if (embed.Color == null)
                    embed.Color = Color.Green;
            }
            else if (command.Equals("Stop Server"))
            {
                embed.Color = helper.GetColour("Stop", settings.ColourSettings.ServerStopColour);
                if (embed.Color == null)
                    embed.Color = Color.Red;
            }
            else if (command.Equals("Restart Server"))
            {
                embed.Color = helper.GetColour("Restart", settings.ColourSettings.ServerRestartColour);
                if (embed.Color == null)
                    embed.Color = Color.Orange;
            }
            else if (command.Equals("Kill Server"))
            {
                embed.Color = helper.GetColour("Kill", settings.ColourSettings.ServerKillColour);
                if (embed.Color == null)
                    embed.Color = Color.Red;
            }
            else if (command.Equals("Update Server"))
            {
                embed.Color = helper.GetColour("Update", settings.ColourSettings.ServerUpdateColour);
                if (embed.Color == null)
                    embed.Color = Color.Blue;
            }
            else if (command.Equals("Manage Server"))
            {
                embed.Color = helper.GetColour("Manage", settings.ColourSettings.ManageLinkColour);
                if (embed.Color == null)
                    embed.Color = Color.Blue;
            }
            else if (command.Contains("console"))
            {
                embed.Color = helper.GetColour("Console", settings.ColourSettings.ConsoleCommandColour);
                if (embed.Color == null)
                    embed.Color = Color.DarkGreen;
            }

            embed.ThumbnailUrl = settings.MainSettings.GameImageURL;
            embed.AddField("Requested by", arg.User.Mention, true);
            embed.WithFooter(settings.MainSettings.BotTagline);
            embed.WithCurrentTimestamp();

            var chnl = arg.Channel as SocketGuildChannel;
            var guild = chnl.Guild.Id;
            var logChannel = GetEventChannel(guild, settings.MainSettings.ButtonResponseChannel);
            var channelID = arg.Channel.Id;

            if (logChannel != null)
                channelID = logChannel.Id;

            log.Debug($"Guild: {guild} || Channel: {channelID}");

            await client.GetGuild(guild).GetTextChannel(channelID).SendMessageAsync(embed: embed.Build());
        }

        /// <summary>
        /// Performs validation of critical configuration settings after the bot is ready.
        /// </summary>
        public async Task PerformInitialConfigurationValidationAsync()
        {
            if (client?.ConnectionState != ConnectionState.Connected)
            {
                log.Warning("[VALIDATE] Cannot perform configuration validation - client not connected.");
                return;
            }
            if (!settings.MainSettings.BotActive)
            {
                log.Info("[VALIDATE] Skipping configuration validation - Bot is not set to Active.");
                return;
            }

            log.Info("[VALIDATE] Performing initial configuration validation...");
            bool allValid = true;

            // Validate Log Channel
            if (settings.MainSettings.LogButtonsAndCommands)
            {
                if (!ValidateChannelSetting(settings.MainSettings.ButtonResponseChannel, "Button/Command Log Channel")) allValid = false;
            }
            // Validate Player Event Channel
            if (settings.MainSettings.PostPlayerEvents)
            {
                if (!ValidateChannelSetting(settings.MainSettings.PostPlayerEventsChannel, "Player Events Channel")) allValid = false;
            }
            // Validate Chat Channel
            if (settings.MainSettings.SendChatToDiscord || settings.MainSettings.SendDiscordChatToServer)
            { // If either chat feature is enabled, channel must be valid
                if (!ValidateChannelSetting(settings.MainSettings.ChatToDiscordChannel, "Chat Discord Channel")) allValid = false;
            }
            // Validate Console Channel
            if (settings.MainSettings.SendConsoleToDiscord)
            {
                if (!ValidateChannelSetting(settings.MainSettings.ConsoleToDiscordChannel, "Console Discord Channel")) allValid = false;
            }
            // Validate Roles
            if (settings.MainSettings.RestrictFunctions)
            {
                if (!ValidateRoleSetting(settings.MainSettings.DiscordRole, "Discord Role Name(s)/ID(s)")) allValid = false;
            }

            if (allValid)
            {
                log.Info("[VALIDATE] Initial configuration validation complete. All checked settings appear valid.");
            }
            else
            {
                log.Error("[VALIDATE] Initial configuration validation FAILED. One or more critical settings (Channels/Roles) could not be validated. Please review settings and previous log messages.");
                // Consider adding a notification mechanism here if desired (e.g., DM to owner, post in a default channel)
            }
        }

        /// <summary>
        /// Validates a channel ID or name setting.
        /// </summary>
        /// <returns>True if valid, False otherwise.</returns>
        // Ensure this is public
        public bool ValidateChannelSetting(string? channelNameOrId, string settingName)
        {
            if (string.IsNullOrWhiteSpace(channelNameOrId))
            {
                log.Error($"[VALIDATE] {settingName} is not configured.");
                return false;
            }

            if (client == null || client.ConnectionState != ConnectionState.Connected)
            {
                log.Warning($"[VALIDATE] Cannot validate setting '{settingName}' ('{channelNameOrId}') - Discord client not connected.");
                return false; // Cannot validate
            }

            log.Debug($"[VALIDATE] Validating channel setting '{settingName}' ('{channelNameOrId}')...");
            bool found = false;
            // Use a temporary list to avoid issues if Guilds collection changes during iteration
            var guilds = client?.Guilds.ToList() ?? new List<SocketGuild>();
            foreach (var guild in guilds)
            {
                SocketGuildChannel? channel = null;
                // Try parsing as ID first
                if (ulong.TryParse(channelNameOrId, out ulong channelId))
                {
                    try
                    {
                        channel = guild.GetChannel(channelId);
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"[VALIDATE] Exception checking channel ID {channelId} in guild {guild.Name}: {ex.Message}");
                    }
                }

                // If not found by ID, try by name
                if (channel == null)
                {
                    try
                    {
                        channel = guild.Channels.FirstOrDefault(c => c.Name.Equals(channelNameOrId, StringComparison.OrdinalIgnoreCase));
                    }
                    catch (Exception ex)
                    {
                        log.Debug($"[VALIDATE] Exception checking channel name '{channelNameOrId}' in guild {guild.Name}: {ex.Message}");
                    }
                }

                if (channel != null)
                {
                    log.Debug($"[VALIDATE] Found channel '{channel.Name}' ({channel.Id}) for setting '{settingName}' in Guild '{guild.Name}' ({guild.Id}). Validation successful for this setting.");
                    found = true;
                    break; // Found in at least one guild, that's enough
                }
            }

            if (!found)
            {
                log.Error($"[VALIDATE] FAILED: Could not find channel '{channelNameOrId}' (for setting '{settingName}') in ANY connected guilds. Please check configuration.");
            }
            return found;
        }

        /// <summary>
        /// Validates a role name or ID setting.
        /// </summary>
        /// <returns>True if valid, False otherwise.</returns>
        // Ensure this is public
        public bool ValidateRoleSetting(string? roleNameOrId, string settingName)
        {
            if (string.IsNullOrWhiteSpace(roleNameOrId))
            {
                log.Error($"[VALIDATE] {settingName} is not configured (required because Restrict Functions is enabled).");
                return false;
            }

            if (client == null || client.ConnectionState != ConnectionState.Connected)
            {
                log.Warning($"[VALIDATE] Cannot validate setting '{settingName}' ('{roleNameOrId}') - Discord client not connected.");
                return false; // Cannot validate
            }

            log.Debug($"[VALIDATE] Validating role setting '{settingName}' ('{roleNameOrId}')...");
            var rolesToFind = roleNameOrId.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).Distinct().ToList();
            if (!rolesToFind.Any())
            {
                log.Debug($"[VALIDATE] No roles specified for setting '{settingName}'. Skipping detailed validation as empty is allowed.");
                return true; // No roles listed is valid
            }

            List<string> rolesNotFound = new List<string>(rolesToFind);
            var guilds = client?.Guilds.ToList() ?? new List<SocketGuild>();

            foreach (var roleIdentifier in rolesToFind)
            {
                bool foundCurrentRole = false;
                foreach (var guild in guilds)
                {
                    SocketRole? role = null;
                    // Try parsing as ID first
                    if (ulong.TryParse(roleIdentifier, out ulong roleId))
                    {
                        try
                        {
                            role = guild.GetRole(roleId);
                        }
                        catch (Exception ex)
                        {
                            log.Debug($"[VALIDATE] Exception checking role ID {roleId} in guild {guild.Name}: {ex.Message}");
                        }
                    }

                    // If not found by ID, try by name
                    if (role == null)
                    {
                        try
                        {
                            role = guild.Roles.FirstOrDefault(r => r.Name.Equals(roleIdentifier, StringComparison.OrdinalIgnoreCase));
                        }
                        catch (Exception ex)
                        {
                            log.Debug($"[VALIDATE] Exception checking role name '{roleIdentifier}' in guild {guild.Name}: {ex.Message}");
                        }
                    }

                    if (role != null)
                    {
                        log.Debug($"[VALIDATE] Found role '{role.Name}' ({role.Id}) matching identifier '{roleIdentifier}' in Guild '{guild.Name}' ({guild.Id}).");
                        foundCurrentRole = true;
                        rolesNotFound.Remove(roleIdentifier); // Found it, remove from missing list
                        break; // Stop checking guilds for *this* role identifier
                    }
                }
                if (!foundCurrentRole)
                {
                    log.Warning($"[VALIDATE] Could not find role matching identifier '{roleIdentifier}' in ANY connected guilds.");
                }
            }

            if (rolesNotFound.Any())
            {
                log.Error($"[VALIDATE] FAILED: Could not find the following role(s) (for setting '{settingName}') in ANY connected guilds: {string.Join(", ", rolesNotFound)}. Please check configuration.");
                return false;
            }
            else
            {
                log.Debug($"[VALIDATE] All specified roles for setting '{settingName}' were found in at least one guild. Validation successful for this setting.");
                return true;
            }
        }

    }
}
