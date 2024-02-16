import os
import shutil
import urllib.request
import subprocess
import json
import logging
from copy import copy

script_location = os.path.dirname(os.path.abspath(__file__)) # todo use pathlib instead
script_dir = os.path.dirname(script_location)
script_name = os.path.basename(__file__)

logging.basicConfig(level=logging.INFO,format='%(asctime)s - %(levelname)s - %(message)s',
    handlers=[logging.FileHandler(os.path.join(script_dir, f'{script_name}.log')), logging.StreamHandler()]
)
for handler in logging.root.handlers:
    if isinstance(handler, logging.StreamHandler):
        handler.setFormatter(logging.Formatter('%(asctime)s - %(levelname)s - %(message)s'))

logger = logging.getLogger(__name__)


def get_user_input(prompt, default=None):
    user_input = input(f"{prompt} (Default: {default}): ")
    return user_input if user_input else default

base_path = 'C:\\AMPDatastore\\Instances\\'
developer_license_key = ''
github_api_url = 'https://api.github.com/repos/winglessraven/AMP-Discord-Bot/releases/latest'
dll_file_name = 'DiscordBotPlugin.dll'
dll_file_path = os.path.join(script_dir, dll_file_name)

def execute_os_command(command):
    process = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    logger.info(process.stdout)
    logger.info(process.stderr)

def download_latest_dll(url, path):
    with urllib.request.urlopen(url) as response, open(path, 'wb') as out_file:
        shutil.copyfileobj(response, out_file)

def update_amp_config(instance_path):
    config_path = os.path.join(instance_path, 'AMPConfig.conf')
    backup_path = os.path.join(instance_path, 'AMPConfig.conf.bak')
    logger.info(f'Updating AMPConfig.conf at {config_path}')
    if not os.path.exists(config_path):
        logger.error(f'AMPConfig.conf not found at {config_path}')
        return
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
                with open(config_path, 'w') as f:
                    f.writelines(lines)
                logger.info(f'Updated AMP.LoadPlugins in AMPConfig.conf')
            break

def get_instance_dirs(base_path):
    for subfolder in os.listdir(base_path):
        path = os.path.join(base_path, subfolder)
        if not os.path.isdir(path): continue
        yield path

if __name__ == "__main__":
    base_path = get_user_input("Set base path", base_path)
    developer_license_key = get_user_input("Set developer license key [Empty skips activation]", developer_license_key)
    with urllib.request.urlopen(github_api_url) as response:
        release_info = json.load(response)
    for asset in release_info['assets']:
        if asset['name'] == dll_file_name:
            asset_url = asset['browser_download_url']
            break
    download_latest_dll(asset_url, dll_file_path)

    instance_dirs = list(get_instance_dirs(base_path))
    logger.info(f'Found instances: {[os.path.basename(x) for x in instance_dirs]}')
    for instance_folder in copy(instance_dirs):
        instance_name = os.path.basename(instance_folder)
        choice = get_user_input(f'Process instance {instance_name}?', 'y')
        if choice.lower() != 'y':
            logger.info(f'Skipping instance {instance_name}')
            instance_dirs.remove(instance_folder)
    logger.info(f'Selected instances: {[os.path.basename(x) for x in instance_dirs]}')

    for instance_folder in instance_dirs:
        instance_name = os.path.basename(instance_folder)
        logger.info(f'Stopping instance {instance_name}')
        execute_os_command(['ampinstmgr', 'stop', instance_name])

    execute_os_command(['ampinstmgr', 'reactivate', 'ADS01', developer_license_key])
    execute_os_command(['taskkill', '/f', '/im', 'AMP.exe'])
    for instance_folder in instance_dirs:
        instance_name = os.path.basename(instance_folder)
        discordbot_plugin_dir = os.path.join(instance_folder, 'Plugins', 'DiscordBotPlugin')
        logger.info(f'Processing instance {instance_name}')
        os.makedirs(discordbot_plugin_dir, exist_ok=True)
        shutil.copy2(dll_file_path, discordbot_plugin_dir)
        update_amp_config(instance_folder)

    for instance_folder in instance_dirs:
        instance_name = os.path.basename(instance_folder)
        logger.info(f'Starting instance {instance_name}')
        execute_os_command(['ampinstmgr', 'start', instance_name])
