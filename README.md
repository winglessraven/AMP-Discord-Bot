
# AMP Discord Bot Plugin

[![Github All Releases](https://img.shields.io/github/downloads/winglessraven/AMP-Discord-Bot/total.svg)]()

A full featured Discord bot plugin for [AMP by Cubecoders](https://cubecoders.com/AMP) that can be used to display the server status in a dynamically updating info panel along with the ability to manage the server directly from Discord. 
* Use buttons or commands to start/stop/restart/kill/update/manage.
* Send console commands directly from Discord to your server.
* Send server output to a Discord channel.
* Track playtime of players.
* Assign permissions to a role so only select members can manage the server.

> [!IMPORTANT]
> The plugin cannot be used on ADS. It can only run on non-ADS instances.

**Submit any bug reports or feature requests [here](https://github.com/winglessraven/AMP-Discord-Bot/issues)**

**Any known issues and workarounds can be found [here](https://github.com/winglessraven/AMP-Discord-Bot/wiki/Known-Issues)**

**If you appreciate my work on this plugin, feel free to buy me a beer to keep me fueled up [here](https://www.paypal.com/donate/?business=JAYRTVPHT5CG8&no_recurring=0&currency_code=GBP)**

Discord Info Panel:

![Bot Info Example](https://github.com/winglessraven/AMP-Discord-Bot/assets/4540397/065570c1-df8b-45b0-bba8-0879d0c38795 "Bot Info Example")

Web Panel:

![Web Panel Example](https://github.com/winglessraven/AMP-Discord-Bot/assets/4540397/0c5a46d7-5d10-4b6d-a7e1-866e38df70ab)


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
| `/[BOT NAME] take-backup`   | Send a request to the AMP Panel to create an instance backup |

*Note: If `Remove Bot Name` is enabled in settings then the commands will exclude the name.*

# Message Command Reference (self explanitory)
```
!botname start-server
!botname stop-server
!botname restart-server
!botname kill-server
!botname update-server
!botname full-playtime-list
!botname take-backup
!botname remove-all-playtime
```

# Configuration Steps

## Install Script
Thanks to [Bluscream](https://github.com/Bluscream) there is now a handy install script to take care of activating instances, downloading the plugin, and enabling it.

The script can be found [here](https://github.com/winglessraven/AMP-Discord-Bot/blob/master/install_script.py).

For instructions to run the script, see [here](https://github.com/winglessraven/AMP-Discord-Bot/wiki/How-to-Run-the-Install-Script).

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

### Change Installation Settings
* Click `Installation` on the menu bar along the left, then deselect `User Install` and set `Install Link` to `None`
* Save your changes

### Bot Settings
* Click `Bot` on the menu bar along the left, then disable `Public Bot`
* Give your bot an icon and enable `Server Members Intent` (this is to ensure that cached member groups are not used if using permissions for controlling servers (start/stop/update/restart/kill)) AND `Message Content Intent` (this is for writing chat messages back to Minecraft servers if enabled)
* Save your changes
* Copy your bot token and paste it in the token field of the Discord Bot menu in AMP

### Configure Permissions and Add to your Discord Server
* Click the `OAuth2` option on the left hand menu
* Select `bot` and `applications.commands` followed by `Send Messages`,`Manage Messages`,`Embed Links`, and `Read Message History`
* Copy the resulting URL and paste into a new browser tab
> [!TIP]
> The resulting URL will be similar to this, but with your application ID under `client_id`:
> 
> https://discord.com/oauth2/authorize?client_id=12345&permissions=92160&integration_type=0&scope=bot+applications.commands
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
|Display Backup Button|Toggle the backup button on the info panel|
|Display Whitelist Request Button|Toggle the whitelist request button on the info panel|
|Display Online Player List|Show a list of online players in the info panel (if supported)|
|Change Displayed Status|Change default AMP status text to custom (see [here](https://github.com/winglessraven/AMP-Discord-Bot/wiki/Changing-Application-State-Values-to-Custom-Text))|
|Online Server Bot Presence Text|Change the presence text when the application is running.  Use `{OnlinePlayers}` and `{MaximumPlayers}` as variables|
|Display Playtime Leaderboard|Toggle the playtime leaderboard on the info panel|
|Remove Bot Name|Removes the bot name from the base command to allow granular permissions from within the Discord server settings|
|Additional Embed Field Title|Add an additional field in the info panel embed, put your title here|
|Additional Embed Field Text|Add an additional field in the info panel embed, put your content here|
|Send Chat to Discord|Send chat messages to a Discord channel|
|Chat Discord Channel|Discord channel name to send chat messages to (if enabled)|
|Send Chat from Discord to Server|Attempt to send chat messages from Discord chat channel to the server (currently only supported for Minecraft)|
|Send Console to Discord|Send console output to a Discord channel|
|Console Discord Channel|Discord channel name to send console output to (if enabled)|
|Exclude Console Output|Text to exclude from console output, useful for removing spammy messages. Use * for wildcard, e.g. \*message to ignore\*|
|Whitelist Request Channel|Discord channel name (or ID) to send whitelist requests to (if enabled)|
|Whitelist Approval Role|Discord role that is allowed to approve whitelist requests|
|Custom Whitelist Command|Custom whitelist command, if blank will use default `/whitelist add`. Enter without `/` (e.g. `globalwhitelist add`)|
|Enable Web Panel|Enable the web panel. This will create a html file in a similar format to the Discord info panel for website embeds. Additional steps are required to map the html file to make it accessible. See the [Wiki](https://github.com/winglessraven/AMP-Discord-Bot/wiki/Configure-the-Web-Panel)|
|Commmands Tab Options|Enable/Disable specific commands, regardless of roles|

## AMP Discord Bot Colours
The `Discord Bot Colours` section give you the ability to change the colour of your embedded messages in Discord.  For each option you want to change insert the hex colour code required.

## AMP Discord Bot Game Specific
The `Discord Bot Game Specific` section is for game specific settings. These will be added over time as requested.

| Option | Description                    |
| ------------- | ------------------------------ |
|Valheim Join Code|Keep track of the Valheim join code in the console output and add it to the info panel accordingly|

## Whitelist Request Process (for Minecraft)
Enable *Display Whitelist Request Button*
Set Whitelist Request Channel (the channel to send whitelist requests to for approval)
Set the Whitelist Approval Role (roles that can approve whitelist requests)
If required, add custom whitelist command (if not default `/whitelist add [playername]`)

User requests access...

<img width="772" height="602" alt="517426921-e8201d2b-1a01-44ee-b5d4-f6cbea7674f8" src="https://github.com/user-attachments/assets/2d878f20-bf9b-4a5b-92e9-cf87e530c304" />

Approval request is created...

<img width="438" height="201" alt="517427092-950653d6-97c1-4791-aa54-ed537bf488e6" src="https://github.com/user-attachments/assets/0b1e9599-df2a-45c0-b7ca-6673fc8e2bf4" />

When actioned, message is changed to show status and user is DM'd...

<img width="413" height="216" alt="517427282-deff83c2-33a9-4461-a27f-073c70607d51" src="https://github.com/user-attachments/assets/4be0937d-33ac-4335-9a73-f408674fbab4" />

<img width="554" height="39" alt="517427389-f7560ab5-b87b-4dd6-8f22-bb925f888e10" src="https://github.com/user-attachments/assets/cd683d15-ab81-4812-ad8c-c039a66c2257" />



