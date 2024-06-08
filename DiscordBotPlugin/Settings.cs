using ModuleShared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Net.Configuration;

namespace DiscordBotPlugin
{
    public class Settings : SettingStore
    {
        [Description("Discord Bot:smart_toy")]
        [SettingsGroupName("Discord Bot")]
        [Serializable]
        public class DiscordBotSettings : SettingSectionStore
        {
            [StoreEncrypted]
            [WebSetting("Discord Bot Token", "From the [Discord Developer Site](https://discord.com/developers/applications)", false, "", CustomFieldTypes.Password, Subcategory: "Discord Config:settings:1")]
            public string BotToken = "";

            [WebSetting("Bot Tagline", "Displayed at the bottom of bot embeds", false, Subcategory: "Misc:more_vert:7")]
            public string BotTagline = "Powered by AMP";

            [WebSetting("Server Display Name", "Displayed in the bot info panel", false, Subcategory: "Server Info:page_info:2")]
            public string ServerDisplayName = "";

            [WebSetting("Server Connection URL", "Displayed in bot info panel", false, Subcategory: "Server Info:page_info:2")]
            public string ServerConnectionURL = "";

            [WebSetting("Server Password", "Displayed in bot info panel", false, Subcategory: "Server Info:page_info:2")]
            public string ServerPassword = "";

            [WebSetting("Modpack URL", "Displayed in bot info panel", false, Subcategory: "Server Info:page_info:2")]
            public string ModpackURL = "";

            [WebSetting("Game Image URL", "Displayed in bot info panel, needs to be a publicly accessible image URL", false, Subcategory: "Server Info:page_info:2")]
            public string GameImageURL = "";

            [WebSetting("Valid Player Count?", "If the player count reports correctly, for info panel and bot status", false, Subcategory: "Server Info:page_info:2")]
            public bool ValidPlayerCount = false;

            [WebSetting("Bot Activated", "Turn the bot on and off", false, Subcategory: "Discord Config:settings:1")]
            public bool BotActive = false;

            [WebSetting("Bot Refresh Interval", "How often, in seconds, the bot should update the presence and info panel.  Recommended minimum 30 seconds otherwise requests to update Discord could be throttled", false, Subcategory: "Discord Config:settings:1")]
            public int BotRefreshInterval = 30;

            [WebSetting("Restrict Functions to Discord Role", "Restrict server functions (start/stop/restart/kill/update/console/manage) to Discord role", false, Subcategory: "Discord Config:settings:1")]
            public bool RestrictFunctions = false;

            [WebSetting("Discord Role Name(s)/ID(s)", "Name or ID of the role(s) in your Discord server that should be allowed to excecute server functions. For multiple roles, enter each one split by a comma (e.g Role1,Role2)", false, Subcategory: "Discord Config:settings:1")]
            public string DiscordRole = "";

            [WebSetting("Log Button Presses and Commands", "Log when buttons are pressed and commands are used", false, Subcategory: "Logging:output:4")]
            public bool LogButtonsAndCommands = false;

            [WebSetting("Button/Command Log Channel (Name OR ID)", "Channel name or ID of where to log button presses and commands. If left blank response will be the same channel as the info panel display", false, Subcategory: "Logging:output:4")]
            public string ButtonResponseChannel = "";

            public List<string> InfoMessageDetails = new List<string>();

            [WebSetting("Post Join and Leave Messages", "Post player join and leave events to a Discord channel", false, Subcategory: "Logging:output:4")]
            public bool PostPlayerEvents = false;

            [WebSetting("Post Join and Leave Messages Channel (Name OR ID)", "Channel name or ID to post player join and leave events", false, Subcategory: "Logging:output:4")]
            public string PostPlayerEventsChannel = "";

            [WebSetting("Base Management URL", "Address used to manage instances, should be in the format of amp.domain.com or your external IP if you do not use a domain", false, Subcategory: "Misc:more_vert:7")]
            public string ManagementURL = "";

            [WebSetting("Base Management URL SSL (https)", "Enable https for the manage URL. Link will be http if disabled", false, Subcategory: "Misc:more_vert:7")]
            public bool ManagementURLSSL = false;

            [WebSetting("Display Start Button", "Toggle the start button visibility on the info panel", false, Subcategory: "Buttons:radio_button_checked:3")]
            public bool ShowStartButton = false;

            [WebSetting("Display Stop Button", "Toggle the stop button visibility on the info panel", false, Subcategory: "Buttons:radio_button_checked:3")]
            public bool ShowStopButton = false;

            [WebSetting("Display Restart Button", "Toggle the restart button visibility on the info panel", false, Subcategory: "Buttons:radio_button_checked:3")]
            public bool ShowRestartButton = false;

            [WebSetting("Display Kill Button", "Toggle the kill button visibility on the info panel", false, Subcategory: "Buttons:radio_button_checked:3")]
            public bool ShowKillButton = false;

            [WebSetting("Display Update Button", "Toggle the update button visibility on the info panel", false, Subcategory: "Buttons:radio_button_checked:3")]
            public bool ShowUpdateButton = false;

            [WebSetting("Display Manage Button", "Toggle the manage button visibility on the info panel", false, Subcategory: "Buttons:radio_button_checked:3")]
            public bool ShowManageButton = false;

            [WebSetting("Display Backup Button", "Toggle the backup button visibility on the info panel", false, Subcategory: "Buttons:radio_button_checked:3")]
            public bool ShowBackupButton = false;

            [WebSetting("Display Online Player List", "Display online player list in the info panel - if not compatible with the server nothing will show", false, Subcategory: "Server Info:page_info:2")]
            public bool ShowOnlinePlayers = false;

            [WebSetting("Online Server Bot Presence Text", "Change the presence text when application is running, use {OnlinePlayers} and {MaximumPlayers} as variables", false, Subcategory: "Misc:more_vert:7")]
            public string OnlineBotPresence = "";

            [WebSetting("Change Displayed Status", "Replace the default AMP status with your own - key is the AMP value, value is the replacement - possible values listed on the [Wiki](https://github.com/winglessraven/AMP-Discord-Bot/wiki/Changing-Application-State-Values-to-Custom-Text)", false, Subcategory: "Misc:more_vert:7")]
            public Dictionary<string, string> ChangeStatus = new Dictionary<string, string>();

            public Dictionary<string, TimeSpan> PlayTime = new Dictionary<string, TimeSpan>();

            [WebSetting("Display Playtime Leaderboard", "Display the playtime leaderboard on the info panel - top 5 are shown", false, Subcategory: "Server Info:page_info:2")]
            public bool ShowPlaytimeLeaderboard = false;

            [WebSetting("Remove Bot Name", "Remove [BOT NAME] from the command", false, Subcategory: "Discord Config:settings:1")]
            public bool RemoveBotName = false;

            [WebSetting("Additional Embed Field Title", "Add an additional field in the info panel embed, put your title here", false, Subcategory: "Misc:more_vert:7")]
            public string AdditionalEmbedFieldTitle = "";

            [WebSetting("Additional Embed Field Text", "Add an additional field in the info panel embed, put your content here", false, Subcategory: "Misc:more_vert:7")]
            public string AdditionalEmbedFieldText = "";

            [WebSetting("Send Chat to Discord", "Send chat messages to a Discord channel", false, Subcategory: "Logging:output:4")]
            public bool SendChatToDiscord = false;

            [WebSetting("Chat Discord Channel", "Discord channel name (or ID) to send chat messages to (if enabled)", false, Subcategory: "Logging:output:4")]
            public string ChatToDiscordChannel = "";

            [WebSetting("Send Chat from Discord to Server", "Attempt to send chat messages from Discord chat channel to the server (currently only supported for Minecraft)", false, Subcategory: "Logging:output:4")]
            public bool SendDiscordChatToServer = false;

            [WebSetting("Send Console to Discord", "Send console output to a Discord channel", false, Subcategory: "Logging:output:4")]
            public bool SendConsoleToDiscord;

            [WebSetting("Console Discord Channel", "Discord channel name (or ID) to send console output to (if enabled)", false, Subcategory: "Logging:output:4")]
            public string ConsoleToDiscordChannel = "";

            [WebSetting("Discord Debug Mode", "Enable verbose logging on the Discord bot for debugging", false, Subcategory: "Discord Config:settings:1")]
            public bool DiscordDebugMode = false;

            [WebSetting("Enable Web Panel", "Enable a web panel on the specified port for embedding onto a website. Info on further configuration [here](https://github.com/winglessraven/AMP-Discord-Bot/wiki/Configure-the-Web-Panel)", false, Subcategory: "Misc:more_vert:7")]
            public bool EnableWebPanel = false;

            [WebSetting("Show Maximum RAM", "Show maximum RAM on the info panel", false, Subcategory: "Misc:more_vert:7")]
            public bool ShowMaximumRAM = false;

            public Dictionary<string, DateTime> LastSeen = new Dictionary<string, DateTime>();

        }

        public DiscordBotSettings MainSettings = new DiscordBotSettings();
        
        [Description("Discord Bot:smart_toy")]
        [SettingsGroupName("Discord Bot")]
        [Serializable]
        public class DiscordBotColoursSettings : SettingSectionStore
        {
            [WebSetting("Info Panel Colour", "Colour of the info panel embed message. Get the HEX colour from [here](https://www.color-hex.com/color-wheel/)", false, Subcategory: "Colours:palette:5")]
            public string InfoPanelColour = "";

            [WebSetting("Start Server Colour", "Colour of the embed message when starting server if logging is enabled. Get the HEX colour from [here](https://www.color-hex.com/color-wheel/)", false, Subcategory: "Colours:palette:5")]
            public string ServerStartColour = "";

            [WebSetting("Stop Server Colour", "Colour of the embed message when stopping server if logging is enabled. Get the HEX colour from [here](https://www.color-hex.com/color-wheel/)", false, Subcategory: "Colours:palette:5")]
            public string ServerStopColour = "";

            [WebSetting("Restart Server Colour", "Colour of the embed message when restarting server if logging is enabled. Get the HEX colour from [here](https://www.color-hex.com/color-wheel/)", false, Subcategory: "Colours:palette:5")]
            public string ServerRestartColour = "";

            [WebSetting("Kill Server Colour", "Colour of the embed message when killing server if logging is enabled. Get the HEX colour from [here](https://www.color-hex.com/color-wheel/)", false, Subcategory: "Colours:palette:5")]
            public string ServerKillColour = "";

            [WebSetting("Update Server Colour", "Colour of the embed message when updating server if logging is enabled. Get the HEX colour from [here](https://www.color-hex.com/color-wheel/)", false, Subcategory: "Colours:palette:5")]
            public string ServerUpdateColour = "";

            [WebSetting("Manage Link Colour", "Colour of the embed message when someone requests the manage link if logging is enabled. Get the HEX colour from [here](https://www.color-hex.com/color-wheel/)", false, Subcategory: "Colours:palette:5")]
            public string ManageLinkColour = "";

            [WebSetting("Console Command Colour", "Colour of the embed message when a console message is sent to the server if logging is enabled. Get the HEX colour from [here](https://www.color-hex.com/color-wheel/)", false, Subcategory: "Colours:palette:5")]
            public string ConsoleCommandColour = "";

            [WebSetting("Player Join Event Colour", "Colour of the embed message when a player joins the server if logging is enabled. Get the HEX colour from [here](https://www.color-hex.com/color-wheel/)", false, Subcategory: "Colours:palette:5")]
            public string ServerPlayerJoinEventColour = "";

            [WebSetting("Player Leave Event Colour", "Colour of the embed message when a player leaves the server if logging is enabled. Get the HEX colour from [here](https://www.color-hex.com/color-wheel/)", false, Subcategory: "Colours:palette:5")]
            public string ServerPlayerLeaveEventColour = "";

            [WebSetting("Playtime Leaderboard Colour", "Colour of the embed message for the playtime leaderboard. Get the HEX colour from [here](https://www.color-hex.com/color-wheel/)", false, Subcategory: "Colours:palette:5")]
            public string PlaytimeLeaderboardColour = "";
        }

        public DiscordBotColoursSettings ColourSettings = new DiscordBotColoursSettings();

        [Description("Discord Bot:smart_toy")]
        [SettingsGroupName("Discord Bot")]
        [Serializable]
        public class DiscordBotGameSpecificSettings : SettingSectionStore
        {
            [WebSetting("Valheim Join Code", "Look for Valheim join code and display it in the info panel (restart the server after enabling)", false, Subcategory: "Game Specific:sports_esports:6")]
                public bool ValheimJoinCode = false;
        }

        public DiscordBotGameSpecificSettings GameSpecificSettings = new DiscordBotGameSpecificSettings();

        [Description("Discord Bot:smart_toy")]
        [SettingsGroupName("Discord Bot")]
        [Serializable]
        public class DiscordBotAboutSettings : SettingSectionStore
        {
            [WebSetting("Support", "For support, or to submit issues and feature requests visit the [GitHub](https://github.com/winglessraven/AMP-Discord-Bot) page.", false, Subcategory: "About:info:8")]
            public int? AboutMessage = null;

            [WebSetting("Donations", "If you appreciate my work, donations are gratefully received! Visit my [Sponsor](https://github.com/sponsors/winglessraven) page.", false, Subcategory: "About:info:8")]                
            public int? DonationsMessage = null;

            [WebSetting("AMP Discord Bot", "Created by winglessraven", false, Subcategory: "About:info:8")]
            public int? AMPDiscordBot = null;
        }

        public DiscordBotAboutSettings AboutSettings = new DiscordBotAboutSettings();
    }
}
