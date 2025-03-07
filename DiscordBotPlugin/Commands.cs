using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LocalFileBackupPlugin;
using ModuleShared;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DiscordBotPlugin
{
    internal class Commands
    {
        private IApplicationWrapper application;
        private Settings settings;
        private ILogger log;
        private BackupProvider backupProvider;
        private IAMPInstanceInfo aMPInstanceInfo;
        private Events events;

        public Commands(IApplicationWrapper application, Settings settings, ILogger log, BackupProvider backupProvider, IAMPInstanceInfo aMPInstanceInfo, Events events)
        {
            this.application = application;
            this.settings = settings;
            this.log = log;
            this.backupProvider = backupProvider;
            this.aMPInstanceInfo = aMPInstanceInfo;
            this.events = events;
        }

        public void SetEvents(Events events)
        {
            this.events = events;
        }

        public void SetBackupProvider(BackupProvider backupProvider)
        {
            this.backupProvider = backupProvider;
        }

        /// <summary>
        /// Send a command to the AMP instance
        /// </summary>
        /// <param name="msg">Command to send to the server</param>
        /// <returns>Task</returns>
        public Task SendConsoleCommand(SocketSlashCommand msg)
        {
            try
            {
                // Check if message data options and application are valid before proceeding
                if (msg?.Data?.Options == null || application == null)
                {
                    log.Error("Cannot send command: Invalid message data or application.");
                    return Task.CompletedTask;
                }

                // Initialize the command string
                string command = "";

                // Get the command to be sent based on the bot name removal setting
                if (settings.MainSettings.RemoveBotName)
                {
                    command = msg.Data.Options.FirstOrDefault()?.Value?.ToString() ?? string.Empty;
                }
                else
                {
                    command = msg.Data.Options.FirstOrDefault()?.Options.FirstOrDefault()?.Value?.ToString() ?? string.Empty;
                }

                if (string.IsNullOrEmpty(command))
                {
                    log.Error("Cannot send command: Command is empty.");
                    return Task.CompletedTask;
                }

                // Send the command to the AMP instance
                IHasWriteableConsole writeableConsole = application as IHasWriteableConsole;
                writeableConsole?.WriteLine(command);

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
        public Task SendChatCommand(string author, string msg)
        {
            try
            {
                // Ensure the message and author are not null or empty
                if (string.IsNullOrEmpty(author) || string.IsNullOrEmpty(msg))
                {
                    log.Error("Cannot send chat message: Author or message is empty.");
                    return Task.CompletedTask;
                }

                string wrapper = "";

                if (application.ApplicationName == "Seven Days To Die")
                    wrapper = "\"";


                // Construct the command to send
                string command = $"say {wrapper}<{author}> {msg}{wrapper}";

                // Send the chat command to the AMP instance
                IHasWriteableConsole writeableConsole = application as IHasWriteableConsole;
                writeableConsole?.WriteLine(command);

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
        /// Hook into LocalFileBackupPlugin events and request a backup
        /// </summary>
        /// <param name="user">The SocketGuildUser argument</param>
        public void BackupServer(SocketGuildUser user)
        {
            if (user == null)
            {
                log.Error("Cannot perform backup: User is null.");
                return;
            }

            BackupManifest manifest = new BackupManifest
            {
                ModuleName = aMPInstanceInfo.ModuleName,
                TakenBy = "DiscordBot",
                CreatedAutomatically = true,
                Name = "Backup Triggered by Discord Bot",
                Description = "Requested by " + user.Username
            };

            events.SetCurrentUser(user);

            // Register event handlers
            backupProvider.BackupActionComplete += events.OnBackupComplete;
            backupProvider.BackupActionFailed += events.OnBackupFailed;
            backupProvider.BackupActionStarting += events.OnBackupStarting;

            // Initiate backup
            log.Info("Backup requested by " + user.Username + " - attempting to start");
            backupProvider.TakeBackup(manifest);
        }

        /// <summary>
        /// Manages the server by sending a private message to the user with a link to the management panel.
        /// </summary>
        /// <param name="arg">The SocketMessageComponent argument.</param>
        public async Task ManageServer(SocketMessageComponent arg)
        {
            if (arg == null || arg.User == null)
            {
                log.Error("Cannot manage server: Argument or user is null.");
                return;
            }

            var builder = new ComponentBuilder();
            string managementProtocol = settings.MainSettings.ManagementURLSSL ? "https://" : "http://";

            // Build the button with the management panel link using the appropriate protocol and instance ID
            string managementPanelLink = $"{managementProtocol}{settings.MainSettings.ManagementURL}/?instance={aMPInstanceInfo.InstanceId}";
            builder.WithButton("Manage Server", style: ButtonStyle.Link, url: managementPanelLink);

            // Send a private message to the user with the link to the management panel
            await arg.User.SendMessageAsync("Link to management panel:", components: builder.Build());
        }
    }

    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;

        // Retrieve client and CommandService instance via constructor
        public CommandHandler(DiscordSocketClient client, CommandService commands)
        {
            _commands = commands;
            _client = client;
        }

        public async Task InstallCommandsAsync()
        {
            // Hook the MessageReceived event into our command handler
            _client.MessageReceived += HandleCommandAsync;

            // Discover all of the command modules in the entry 
            // assembly and load them
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), services: null);
        }

        private async Task HandleCommandAsync(SocketMessage messageParam)
        {
            // Don't process the command if it was a system message
            if (messageParam is not SocketUserMessage message) return;

            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;

            // Determine if the message is a command based on the prefix and make sure no bots trigger commands
            if (!(message.HasCharPrefix('!', ref argPos) ||
                message.HasMentionPrefix(_client.CurrentUser, ref argPos)) ||
                message.Author.IsBot)
                return;

            // Create a WebSocket-based command context based on the message
            var context = new SocketCommandContext(_client, message);

            // Execute the command with the command context we just created
            await _commands.ExecuteAsync(context, argPos, services: null);
        }
    }
}
