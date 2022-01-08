
# AMP Discord Bot Plugin

A basic Discord bot plugin for AMP that can be used to display the server status along with the ability to manage the server directly from Discord (start / stop / restart / kill / update).

**Submit any bug reports or feature requests [here](https://github.com/winglessraven/AMP-Discord-Bot/issues)**

![Bot Info Example](https://images2.imgbox.com/47/7f/T8HcWlrZ_o.png "Bot Info Example")

# Command Reference
| Command | Description                    |
| ------------- | ------------------------------ |
| `@[BOT NAME] info`      | Display Server Info  |
| `@[BOT NAME] start server`   | Start the Server |
| `@[BOT NAME] stop server`   | Stop the Server |
| `@[BOT NAME] restart server`   | Restart the Server |
| `@[BOT NAME] kill server`   | Kill the Server |
| `@[BOT NAME] update server`   | Update the Server |
| `@[BOT NAME] help`   | Show possible bot commands |
# Configuration Steps
## Configure AMP
Before the plugin can be used you need to configure AMP in a specific way.
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
* Select `bot` followed by `Send Messages`,`Embed Links`,`Read Message History`, and `Add Reactions`
* Copy the resulting URL and paste into a new browser tab
* Log in if nedded and add the bot to your Discord server by selecting it in the list
* If you plan to use pemissions for the server commands, create a new role on your Discord server to use later on.

## AMP Menu Settings
| Option | Description                    |
| ------------- | ------------------------------ |
|Discord Bot Token|Enter your Discord bot token here|
|Bot Tagline|This is in the footer of the messages from the bot|
|Server Display Name|Name of the server displayed in the `@[BOT NAME]` info response|
|Server Connection URL|URL that players can use to join the game server|
|Server Password|If you have a server password that you want your users to see enter it here|
|Modpack URL|This will show as a link in the `@[BOT NAME]` info command, useful if you are running a modpack or have a steam workshop collection for the server mods being used|
|Game Image URL|A publicly accessible image to be shown in the `@[BOT NAME]` info response|
|Valid Player Count|Set to true if the server has a valid player count, this will determine if the online count is shown in Discord|
|Bot Activated|If the bot should be activated|
|Restrict Functions to Discord Role|If server commands (start/stop/restart/kill/update) should be restricted to a Discord Role|
|Discord Role Name|Role name associated with the permissions for the previous setting.|
