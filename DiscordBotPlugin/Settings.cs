﻿using ModuleShared;

namespace DiscordBotPlugin
{
    public class Settings : SettingStore
    {
        public class DiscordBotSettings : SettingSectionStore
        {
            [StoreEncrypted]
            [WebSetting("Discord Bot Token", "(from https://discord.com/developers/applications)", false, "", CustomFieldTypes.Password)]
            public string BotToken = "";

            [WebSetting("Bot Tagline", "(displayed at the bottom of bot embeds)", false)]
            public string BotTagline = "Powered by Wingless Ravens";

            [WebSetting("Server Display Name", "(displayed in @bot info response)", false)]
            public string ServerDisplayName = "";

            [WebSetting("Server Connection URL", "(displayed in @bot info response)", false)]
            public string ServerConnectionURL = "";

            [WebSetting("Server Password", "(displayed in @bot info response)", false)]
            public string ServerPassword = "";

            [WebSetting("Modpack URL", "(displayed in @bot info response)", false)]
            public string ModpackURL = "";

            [WebSetting("Game Image URL", "(displayed in @bot info response, needs to be a publicly accessible image URL)", false)]
            public string GameImageURL = "";

            [WebSetting("Valid Player Count?", "(if the player count reports correctly, for info and bot status)", false)]
            public bool ValidPlayerCount = false;

            [WebSetting("Bot Activated", "(turn the bot on and off)", false)]
            public bool BotActive = false;

            [WebSetting("Restrict Functions to Discord Role", "(restric server fuctions (start/stop/restart/kill/update) to Discord Role)", false)]
            public bool RestrictFunctions = false;

            [WebSetting("Discord Role Name", "(name of the role in your Discord server that should be allowed to excecute server starts/stops)", false)]
            public string DiscordRole = "";

            [WebSetting("Button Resonse Channel ID", "(channel ID of where to respond to button presses (right click on the channel in Discord and click 'Copy ID'. If left blank response will be the same channel as the info display)", false)]
            public string ButtonResponseChannel = "";
        }

        public DiscordBotSettings MainSettings = new DiscordBotSettings();
    }
}
