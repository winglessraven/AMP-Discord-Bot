import os
import shutil
import urllib.request
from configparser import ConfigParser

# Function to ask the user for input and use a default value if the input is empty
def get_user_input(prompt, default=None):
    user_input = input(f"Enter the {prompt} (Default: {default}): ")
    return user_input if user_input else default

# Ask the user for input and use default values
base_path = 'C:\\AMPDatastore\\Instances\\'
developer_license_key = ''

github_api_url = 'https://api.github.com/repos/winglessraven/AMP-Discord-Bot/releases/latest'
dll_file_path = 'DiscordBotPlugin.dll'

# Function to execute OS command and print its output
def execute_os_command(command):
    process = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    print(process.stdout)
    print(process.stderr)

# Function to download the latest DLL file from the GitHub repository
def download_latest_dll(url, path):
    with urllib.request.urlopen(url) as response, open(path, 'wb') as out_file:
        shutil.copyfileobj(response, out_file)

# Function to update the AMPConfig.conf file
def update_amp_config(instance_path):
    config_path = os.path.join(instance_path, 'AMPConfig.conf')
    backup_path = os.path.join(instance_path, 'AMPConfig.conf.bak')
    
    # Create a backup of the original config file
    shutil.copy2(config_path, backup_path)
    
    # Load the config file
    config = ConfigParser(inline_comment_prefixes=('#'), allow_no_value=True)
    config.read(config_path)
    
    # Update the LoadPlugins array
    if config.has_option('AMP', 'LoadPlugins'):
        plugins = config.get('AMP', 'LoadPlugins')
        plugins_list = eval(plugins)
        if 'DiscordBotPlugin' not in plugins_list:
            plugins_list.append('DiscordBotPlugin')
            config.set('AMP', 'LoadPlugins', str(plugins_list))
            with open(config_path, 'w') as f:
                config.write(f)

def get_instance_dirs(base_path):
    for subfolder in os.listdir(base_path):
        instance_path = os.path.join(base_path, subfolder)
        yield instance_path

# Main script
if __name__ == "__main__":
    base_path = get_user_input("base path", base_path)
    developer_license_key = get_user_input("Developer license key [Empty skips activation]", developer_license_key)
    # github_api_url = get_user_input("GitHub API URL", github_api_url)
    # dll_file_path = get_user_input("DLL file path: ", dll_file_path)

    # Get the latest release information from the GitHub API
    with urllib.request.urlopen(github_api_url) as response:
        release_info = json.load(response)
    
    # Find the asset URL for the DLL file
    for asset in release_info['assets']:
        if asset['name'] == dll_file_path:
            asset_url = asset['browser_download_url']
            break
    
    # Download the latest DLL file
    download_latest_dll(asset_url, dll_file_path)
    
    # Before processing any instances
    execute_os_command(['ampinstmgr', 'stop', 'ADS01'])
    execute_os_command(['ampinstmgr', 'reactivate', 'ADS01', developer_license_key])
    execute_os_command(['taskkill', '/f', '/im', 'AMP.exe'])

    # Iterate over each subfolder in the base path
    for subfolder in get_instance_dirs(base_path):
        instance_name = os.path.basename(subfolder)
        discordbot_plugin_dir = os.path.join(base_path, subfolder, 'Plugins', 'DiscordBotPlugin')
        logger.info(f'Processing instance {instance_name}')

        # Before processing a specific instance
        execute_os_command(['ampinstmgr', 'stop', instance_name])
        
        # Create the instance path if it doesn't exist
        os.makedirs(discordbot_plugin_dir, exist_ok=True)
        
        # Copy the DLL file to the instance's Plugins folder
        shutil.copy2(dll_file_path, discordbot_plugin_dir)
        
        # Update the AMPConfig.conf file for the instance
        update_amp_config(instance_path)

        # After processing a specific instance
        execute_os_command(['ampinstmgr', 'start', instance_name])
