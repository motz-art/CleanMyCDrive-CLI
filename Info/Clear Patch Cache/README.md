# PME Cache Folder Cleaner
## Description
This script detects the current cache location on the device and removes non-PME related files.

## Pre-Requisites
The scripts runs through Powershell, ideally using Powershell version 5.1.

## Parameters
Details on the input parameters are as follows:

$verbose
    This is only required if you would like to have a verbose log output.
    This is a not a mandatory parameter.
    If left blank, log output would be in regular/INFO mode.
    
When defining the Command Line Parameters, please use the following format:
    -verbose "Y"

## Output
If this runs successfully it should output that the cache folder has been cleaned successfully.

Also show the new size value after doing the cleanup.

Also if it runs into any errors, this should be reported back.

For full logs, this is outputted to:
    C:\ProgramData\MspPlatform\Tech Tribes\Clear PME Cache\debug.log