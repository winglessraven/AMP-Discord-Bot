
# AMP Discord Bot Plugin

A Discord bot plugin for [AMP by Cubecoders](https://cubecoders.com/AMP) that can be used to display the server status in a dynamically updating info panel along with the ability to manage the server directly from Discord (start / stop / restart / kill / update) via buttons and commands.

**Submit any bug reports or feature requests [here](https://github.com/winglessraven/AMP-Discord-Bot/issues)**

**Any known issues and workarounds can be found [here](https://github.com/winglessraven/AMP-Discord-Bot/wiki/Known-Issues)**

**If you appreciate my work on this plugin, feel free to buy me a beer to keep me fueled up [here](https://www.buymeacoffee.com/winglessraven)**



![Bot Info Example](https://images2.imgbox.com/69/1b/o2IQILvX_o.png "Bot Info Example")

# Command Reference
| Command | Description                    |
| ------------- | ------------------------------ |
| `/[BOT NAME] info`      | Display Server Info  |
| `/[BOT NAME] start-server`   | Start the Server |
| `/[BOT NAME] stop-server`   | Stop the Server |
| `/[BOT NAME] restart-server`   | Restart the Server |
| `/[BOT NAME] kill-server`   | Kill the Server |
| `/[BOT NAME] update-server`   | Update the Server |
| `/[BOT NAME] console [command]`   | Send a command to the server |
| `/[BOT NAME] playtime`   | Show playtime leaderboard |
| `/[BOT NAME] full-playtime-list`   | Show full playtime list with last seen value |

*Note: If `Remove Bot Name` is enabled in settings then the commands will exclude the name.*

# Configuration Steps
## Configure AMP
Before the plugin can be used you need to configure AMP in a specific way.  **NOTE: The plugin cannot be used on ADS. It can only run on non-ADS instances.**
### Activate with a Developer Licence
* Request a developer licence from AMP via the licence portal https://manage.cubecoders.com/Login
* Once you have received the licence log into your server console and as the amp user run `ampinstmgr stop [ADS INSTANCE NAME]` followed by `ampinstmgr reactivate [INSTANCE NAME] [DEVELOPER KEY]`  where `[INSTANCE NAME]` is the name of your instance you want to set up a bot for and `[DEVELOPER KEY]` is the key received from CubeCoders.
* Start your ADS instance again with `ampinstmgr start [ADS INSTANCE NAME]`.

### Installing and Enabling the Plugin
* Edit the AMPConfig.conf file in the root folder of your instance (e.g. `/home/amp/.ampdata/instances/INSTANCENAME01/AMPConfig.conf`)
* Under AMP.LoadPlugins add `DiscordBotPlugin` to the list (e.g. `AMP.LoadPlugins=["FileManagerPlugin","EmailSenderPlugin","WebRequestPlugin","LocalFileBackupPlugin","CommonCorePlugin","DiscordBotPlugin"]`)
* In the plugins folder for your instance (e.g. `/home/amp/.ampdata/instances/INSTANCENAME01/Plugins/`) create a new folder called `DiscordBotPlugin` and insert the .dll file from the current [release](https://github.com/winglessraven/AMP-Discord-Bot/releases/latest "release")
* Restart your instance with `ampinstmgr restart [INSTANCE NAME]`
* Log in to your instance and you will see a new menu item under Configuration for the Discord Bot

![Menu Item](https://images2.imgbox.com/1f/1f/VHRYDACX_o.png "Menu Item")

## Configure your Discord Bot
### Create the Application
* Log in to https://discord.com/developers/applications with your Discord account.
* Click `New Application`
* Enter a name for your application and click `Create`

### Create the Bot
* Click `Bot` on the menu bar along the left, then click `Add Bot`
* Give your bot an icon and enable `Server Members Intent` – this is to ensure that cached member groups are not used if using permissions for controlling servers (start/stop/update/restart/kill)
* Save your changes
* Copy your bot token and paste it in the token field of the Discord Bot menu in AMP

### Configure Permissions and Add to your Discord Server
* Click the `OAuth2` option on the left hand menu followed by `URL Generator`
* Select `bot` and `applications.commands` followed by `Send Messages`,`Manage Messages`,`Embed Links`,`Read Message History`, and `Add Reactions`
* Copy the resulting URL and paste into a new browser tab
* Log in if nedded and add the bot to your Discord server by selecting it in the list
* If you plan to use pemissions for the server commands, create a new role on your Discord server to use later on.

## AMP Menu Settings
| Option | Description                    |
| ------------- | ------------------------------ |
|Discord Bot Token|Enter your Discord bot token here|
|Bot Tagline|This is in the footer of the messages from the bot|
|Server Display Name|Name of the server displayed in the info panel|
|Server Connection URL|URL that players can use to join the game server|
|Server Password|If you have a server password that you want your users to see enter it here|
|Modpack URL|This will show as a link in the info panel, useful if you are running a modpack or have a steam workshop collection for the server mods being used|
|Game Image URL|A publicly accessible image to be shown in the info panel|
|Valid Player Count|Set to true if the server has a valid player count, this will determine if the online count is shown in Discord|
|Bot Activated|If the bot should be activated|
|Bot Refresh Interval|How often, in seconds, the bot should update the presence and info message. Recommended minimum 30 seconds otherwise requests to Discord could be throttled|
|Restrict Functions to Discord Role|If server commands (start/stop/restart/kill/update) should be restricted to a Discord Role|
|Discord Role Name|Role name associated with the permissions for the previous setting|
|Log Button Presses and Commands|Toggle if button presses and command use should be logged|
|Button Log Channel (Name OR ID)|Name or ID of the channel to log button presses in. If blank it will be logged to the same channel as the info panel|
|Post Join and Leave Messages|If the bot should post user join and leave events to a Discord channel|
|Post Join and Leave Messages Channel (Name OR ID)|Channel name or ID for the bot to post join and leave events to (only if enabled)|
|Base Management URL|Address to be used to manage instances.  Should be in the format of `amp.domain.com` or your external IP address|
|Display Start Button|Toggle the start button on the info panel|
|Display Stop Button|Toggle the stop button on the info panel|
|Display Restart Button|Toggle the restart button on the info panel|
|Display Kill Button|Toggle the kill button on the info panel|
|Display Update Button|Toggle the update button on the info panel|
|Display Manage Button|Toggle the manage button on the info panel|
|Display Online Player List|Show a list of online players in the info panel (if supported)|
|Change Displayed Status|Change default AMP status text to custom (see [here](https://github.com/winglessraven/AMP-Discord-Bot/wiki/Changing-Application-State-Values-to-Custom-Text)|
|Online Server Bot Presence Text|Change the presence text when the application is running.  Use `{OnlinePlayers}` and `{MaximumPlayers}` as variables|
|Display Playtime Leaderboard|Toggle the playtime leaderboard on the info panel|
|Remove Bot Name|Removes the bot name from the base command to allow granular permissions from within the Discord server settings|

## AMP Discord Bot Colours
The `Discord Bot Colours` section give you the ability to change the colour of your embedded messages in Discord.  For each option you want to change insert the hex colour code required.
