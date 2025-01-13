import shutil
import urllib.request
import subprocess
import json
import logging
import os
from copy import copy
from datetime import datetime
from pathlib import Path
from typing import Any, Optional, Union
import argparse

PathLike = Union[str, Path]

self: Path = Path(__file__).resolve()

log_format = '%(asctime)s - %(levelname)s - %(message)s'
file_handler = logging.FileHandler(self.parent / f'{self.name}.log')
file_handler.setLevel(logging.DEBUG)
file_handler.setFormatter(logging.Formatter(log_format))
stream_handler = logging.StreamHandler()
stream_handler.setLevel(logging.INFO)
stream_handler.setFormatter(logging.Formatter(log_format))
logging.basicConfig(level=logging.DEBUG, handlers=[file_handler, stream_handler])
logger = logging.getLogger(__name__)

logger.debug(f'Logging started at {datetime.now()}')
logger.debug('WARNING: This log might contain sensitive information, like license keys, instance names and paths.')
logger.debug('Do not share this log unaltered!')
logger.debug(f'Script location: {self}')

def get_user_input(prompt: str, default: Union[str, None] = None) -> str:
    user_input = input(f"{prompt} (Default: {default}): ")
    return user_input if user_input else default

if os.name == 'nt': # Windows
    base_path: Path = Path('C:\\AMPDatastore\\Instances\\')
elif os.name == 'posix': # Linux
    base_path: Path = Path('/home/amp/.ampdata/instances/')

developer_license_key: str = ''
github_api_url: str = 'https://api.github.com/repos/winglessraven/AMP-Discord-Bot/releases/latest'
plugin_name: str = 'DiscordBotPlugin'
dll_file_name: str = f'{plugin_name}.dll'
dll_file_path: Path = self.parent / dll_file_name
plugin_dll_dir: Path = Path('Plugins') / plugin_name
plugin_config_file_name: str = f'{plugin_name}.kvp'
ampconfig_name: str = 'AMPConfig.conf'
ads_instance_name: str = 'ADS01'

def execute_os_command(command: str) -> None:
    logger.debug(f'Executing command: {command}')
    process = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    logger.debug(process.stdout)
    logger.debug(process.stderr)

def download_latest_dll(url: str, path: Path) -> None:
    with urllib.request.urlopen(url) as response, open(path, 'wb') as out_file:
        shutil.copyfileobj(response, out_file)

def update_amp_config(instance_path: Path) -> None:
    config_path = instance_path / ampconfig_name
    backup_path = instance_path / f'{ampconfig_name}.bak'
    logger.info(f'Updating {ampconfig_name} at {config_path}')
    if not config_path.is_file():
        logger.error(f'{ampconfig_name} not found at {config_path}!')
        return
    shutil.copy2(config_path, backup_path)
    with open(config_path, 'r') as f:
        lines = f.readlines()
        for i, line in enumerate(lines):
            if line.startswith('AMP.LoadPlugins='):
                plugins = line.split('=')[1].strip()
                plugins_list = eval(plugins)
                if plugin_name not in plugins_list:
                    plugins_list.append(plugin_name)
                    lines[i] = 'AMP.LoadPlugins={}\n'.format(str(plugins_list))
                    with open(config_path, 'w') as f:
                        f.writelines(lines)
                    logger.info(f'Updated AMP.LoadPlugins in {config_path}')
                else:
                    logger.info(f'{plugin_name} already in AMP.LoadPlugins')
                break

def copy_dll_to_plugin_folder(instance_path: Path) -> None:
    discordbot_plugin_dir = instance_path / plugin_dll_dir
    discordbot_plugin_file = discordbot_plugin_dir / dll_file_name
    if not discordbot_plugin_dir.is_dir():
        logger.info(f'Creating DiscordBotPlugin folder at {discordbot_plugin_dir}')
        discordbot_plugin_dir.mkdir(exist_ok=True)
    logger.info(f'Copying {dll_file_path} to {discordbot_plugin_dir}')
    shutil.copy2(dll_file_path, discordbot_plugin_dir)

def get_instance_dirs(base_path: Path) -> list[Path]:
    return [path for path in base_path.iterdir() if path.is_dir()]

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="AMP Discord Bot Install Script")
    parser.add_argument('--update-only', action='store_true', help="Update only existing instances")
    args = parser.parse_args()

    # Get user input
    base_path = Path(get_user_input("Set base path", base_path))
    ads_instance_name = get_user_input("Set ADS name", ads_instance_name)

    if not args.update_only:
        developer_license_key = get_user_input("Set developer license key [Empty skips activation]", developer_license_key)

    # Download latest plugin dll
    with urllib.request.urlopen(github_api_url) as response:
        release_info = json.load(response)
        for asset in release_info['assets']:
            if asset['name'] == dll_file_name:
                asset_url = asset['browser_download_url']
                break
        download_latest_dll(asset_url, dll_file_path)

    # Get instance folders
    instance_dirs: list[Path] = get_instance_dirs(base_path)
    logger.info(f'Found instances: {[path.name for path in instance_dirs]}')

    # Remove ADS instance
    filtered_instance_dirs = [d for d in instance_dirs if d.name != ads_instance_name]
    logger.info(f'Instances after excluding {ads_instance_name}: {[path.name for path in filtered_instance_dirs]}')

    if not args.update_only:
        # Select instances to process
        for instance_folder in list(filtered_instance_dirs):
            choice = get_user_input(f'Process instance {instance_folder.name}?', 'y')
            if choice.lower() != 'y':
                logger.info(f'Skipping instance {instance_folder.name}')
                filtered_instance_dirs.remove(instance_folder)
    else:
        filtered_instance_dirs = [d for d in filtered_instance_dirs if (d / plugin_dll_dir / dll_file_name).is_file()]
        logger.info(f'Selected instances for update: {[path.name for path in filtered_instance_dirs]}')

    # Stop selected instances
    for instance_folder in filtered_instance_dirs:
        logger.info(f'Stopping instance {instance_folder.name}')
        execute_os_command(['ampinstmgr', 'stop', instance_folder.name])

    # Copy plugin dll and update amp config
    for instance_folder in filtered_instance_dirs:
        copy_dll_to_plugin_folder(instance_folder)
        if not args.update_only:
            update_amp_config(instance_folder)

    if not args.update_only and developer_license_key:
        # (Re)activate ADS instance
        logger.info(f'Stopping {ads_instance_name}')
        execute_os_command(['ampinstmgr', 'stop', ads_instance_name])
        for instance_folder in filtered_instance_dirs:
            logger.info(f'Reactivating instance {instance_folder.name}')
            execute_os_command(['ampinstmgr', 'reactivate', instance_folder.name, developer_license_key])
        logger.info(f'Starting {ads_instance_name}')
        execute_os_command(['ampinstmgr', 'start', ads_instance_name])
    elif not args.update_only:
        logger.info("Skipping activation, make sure you're using a developer license key for this instance.")

    # Start selected instances
    for instance_folder in filtered_instance_dirs:
        logger.info(f'Starting instance {instance_folder.name}')
        execute_os_command(['ampinstmgr', 'start', instance_folder.name])
