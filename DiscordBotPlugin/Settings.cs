using ModuleShared;
using System.Collections.Generic;

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

            [WebSetting("Bot Refresh Interval", "(how often, in seconds, the bot should update the presence and info message.  Recommended minimum 30 seconds otherwise requests to update Discord could be throttled)", false)]
            public int BotRefreshInterval = 30;

            [WebSetting("Restrict Functions to Discord Role", "(restrict server functions (start/stop/restart/kill/update) to Discord Role)", false)]
            public bool RestrictFunctions = false;

            [WebSetting("Discord Role Name", "(name of the role in your Discord server that should be allowed to excecute server starts/stops)", false)]
            public string DiscordRole = "";

            [WebSetting("Button Log Channel Name", "(channel name of where to log button presses. If left blank response will be the same channel as the info display)", false)]
            public string ButtonResponseChannel = "";

            public List<string> InfoMessageDetails = new List<string>();

            [WebSetting("Post Join and Leave Messages", "(post player join and leave events to a Discord channel)", false)]
            public bool PostPlayerEvents = false;

            [WebSetting("Post Join and Leave Messages Channel Name", "(channel name to post player join and leave events)", false)]
            public string PostPlayerEventsChannel = "";

            [WebSetting("Base Management URL", "(address used to manage instances, should be in the format of amp.domain.com or your external IP if you do not use a domain)", false)]
            public string ManagementURL = "";

            [WebSetting("Display Start Button", "(toggle the start button visibility on the info message)", false)]
            public bool ShowStartButton = false;

            [WebSetting("Display Stop Button", "(toggle the stop button visibility on the info message)", false)]
            public bool ShowStopButton = false;

            [WebSetting("Display Restart Button", "(toggle the restart button visibility on the info message)", false)]
            public bool ShowRestartButton = false;

            [WebSetting("Display Kill Button", "(toggle the kill button visibility on the info message)", false)]
            public bool ShowKillButton = false;

            [WebSetting("Display Update Button", "(toggle the update button visibility on the info message)", false)]
            public bool ShowUpdateButton = false;

            [WebSetting("Display Manage Button", "(toggle the manage button visibility on the info message)", false)]
            public bool ShowManageButton = false;

            [WebSetting("Display Online Player List", "(display online player list in the info panel - if not compatible with the server nothing will show)", false)]
            public bool ShowOnlinePlayers = false;

            [WebSetting("Change Displayed Status", "(replace the default AMP status with your own - key is the AMP value, value is the replacement - possible values listed on the [readme](https://github.com/winglessraven/AMP-Discord-Bot))", false)]
            public Dictionary<string, string> ChangeStatus = new Dictionary<string, string>();

            //Possible options: [Stopped,PreStart,Configuring,Starting,Ready,Restarting,Stopping,PreparingForSleep,Sleeping,Waiting,Installing,Updating,AwaitingUserInput,Failed,Suspended,Maintainence,Indeterminate,Undefined]
        }

        public DiscordBotSettings MainSettings = new DiscordBotSettings();
    }
}
