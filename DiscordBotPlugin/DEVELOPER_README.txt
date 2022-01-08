In order to use this template, you need to make the following changes:

 - The reference to ModuleShared.dll needs removing, and setting to a copy from a local AMP instance.
 - The post-build steps in the project (Right click project -> properties) need the "PATH\TO\AMP\INSTANCE" strings changing similarly to the path for a local AMP instance.
 - In the project Debug tab, the path to the AMP instance that will be used as the debug target needs changing, along with the working directory.
 - Your default namespace must match the assembly name and the filename. Do not change one without changing the other two.