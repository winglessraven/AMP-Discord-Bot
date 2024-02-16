import os
import shutil
import urllib.request

def get_user_input(prompt, default=None):
    user_input = input(f"Enter the {prompt} (Default: {default}): ")
    return user_input if user_input else default

base_path = 'C:\\AMPDatastore\\Instances\\'
developer_license_key = ''
github_api_url = 'https://api.github.com/repos/winglessraven/AMP-Discord-Bot/releases/latest'
dll_file_path = 'DiscordBotPlugin.dll'

def execute_os_command(command):
    process = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    print(process.stdout)
    print(process.stderr)

def download_latest_dll(url, path):
    with urllib.request.urlopen(url) as response, open(path, 'wb') as out_file:
        shutil.copyfileobj(response, out_file)

def update_amp_config(instance_path):
    config_path = os.path.join(instance_path, 'AMPConfig.conf')
    backup_path = os.path.join(instance_path, 'AMPConfig.conf.bak')
    shutil.copy2(config_path, backup_path)
    with open(config_path, 'r') as f:
        lines = f.readlines()
    for i, line in enumerate(lines):
        if line.startswith('AMP.LoadPlugins='):
            plugins = line.split('=')[1].strip()
            plugins_list = eval(plugins)
            if 'DiscordBotPlugin' not in plugins_list:
                plugins_list.append('DiscordBotPlugin')
                lines[i] = 'AMP.LoadPlugins={}\n'.format(str(plugins_list))
            break

def get_instance_dirs(base_path):
    for subfolder in os.listdir(base_path):
        instance_path = os.path.join(base_path, subfolder)
        yield instance_path

if __name__ == "__main__":
    base_path = get_user_input("base path", base_path)
    developer_license_key = get_user_input("Developer license key [Empty skips activation]", developer_license_key)
    with urllib.request.urlopen(github_api_url) as response:
        release_info = json.load(response)
    for asset in release_info['assets']:
        if asset['name'] == dll_file_path:
            asset_url = asset['browser_download_url']
            break
    download_latest_dll(asset_url, dll_file_path)
    execute_os_command(['ampinstmgr', 'stop', 'ADS01'])
    execute_os_command(['ampinstmgr', 'reactivate', 'ADS01', developer_license_key])
    execute_os_command(['taskkill', '/f', '/im', 'AMP.exe'])
    for subfolder in get_instance_dirs(base_path):
        instance_name = os.path.basename(subfolder)
        discordbot_plugin_dir = os.path.join(base_path, subfolder, 'Plugins', 'DiscordBotPlugin')
        logger.info(f'Processing instance {instance_name}')
        execute_os_command(['ampinstmgr', 'stop', instance_name])
        os.makedirs(discordbot_plugin_dir, exist_ok=True)
        shutil.copy2(dll_file_path, discordbot_plugin_dir)
        update_amp_config(instance_path)
        execute_os_command(['ampinstmgr', 'start', instance_name])
