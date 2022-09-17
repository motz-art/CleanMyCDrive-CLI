# '==================================================================================================================================================================
# 'Disclaimer
# 'The sample scripts are not supported under any N-able support program or service.
# 'The sample scripts are provided AS IS without warranty of any kind.
# 'N-able further disclaims all implied warranties including, without limitation, any implied warranties of merchantability or of fitness for a particular purpose.
# 'The entire risk arising out of the use or performance of the sample scripts and documentation stays with you.
# 'In no event shall N-able or anyone else involved in the creation, production, or delivery of the scripts be liable for any damages whatsoever
# '(including, without limitation, damages for loss of business profits, business interruption, loss of business information, or other pecuniary loss)
# 'arising out of the use of or inability to use the sample scripts or documentation.
# '==================================================================================================================================================================

Param (
    [string]$verbose = "Y"
)

function setupLogging() {
	$script:logFilePath = "C:\ProgramData\MspPlatform\Tech Tribes\Clear PME Cache\debug.log"

    $script:logFolder = Split-Path $logFilePath
    $script:logFile = Split-Path $logFilePath -Leaf

    $logFolderExists = Test-Path $logFolder
    $logFileExists = Test-Path $logFilePath

    If ($logFolderExists -eq $false) {
        New-Item -ItemType "directory" -Path $logFolder | Out-Null
    }
    
    If ($logFileExists -eq $true) {
        Remove-Item $logFilePath -ErrorAction SilentlyContinue
        Start-Sleep 2
        New-Item -ItemType "file" -Path $logFolder -Name $logFile | Out-Null
    } Else {
        New-Item -ItemType "file" -Path $logFolder -Name $logFile | Out-Null
    }
    
    If (($logFolder -match '.+?\\$') -eq $false) {
        $script:logFolder = $logFolder + "\"
    }

	writeToLog I "Started processing the Clear PME Cache script."
	writeToLog I "Running script version: 1.00."
}

function validateUserInput() {
    # Ensures the provided input from user is valid
        If ($verbose.ToLower() -eq "y") {
            $script:verboseMode = $true
            writeToLog V "You have defined to have the script output the verbose log entries."
        } Else {
            $script:verboseMode = $false
            writeToLog I "Will output logs in regular mode."
        }
    
        writeToLog V "Input Parameters have been successfully validated."
        writeToLog V ("Completed running {0} function." -f $MyInvocation.MyCommand)
}

function getAgentPath() {
	writeToLog V ("Started running {0} function." -f $MyInvocation.MyCommand)
	
    try {
        $Keys = Get-ChildItem HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall -ErrorAction Stop
    } catch {
        writeToLog F "Error during the lookup of the CurrentVersion\Uninstall Path in the registry:"
        writeToLog F $_
		postRuntime
        Exit 1001
    }

    $Items = $Keys | Foreach-Object {
        Get-ItemProperty $_.PsPath
    }

    ForEach ($Item in $Items) {
        If ($Item.DisplayName -like "Patch Management Service Controller") {
			$script:localFolder = $Item.installLocation
            break
        }
    }

    try {
        $Keys = Get-ChildItem HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall -ErrorAction Stop
    } catch {
        writeToLog F "Error during the lookup of the WOW6432Node Path in the registry:"
        writeToLog F $_
    }
    
    $Items = $Keys | Foreach-Object {
        Get-ItemProperty $_.PsPath
    }
    
    ForEach ($Item in $Items) {
        If ($Item.DisplayName -like "*Patch Management Service Controller*") {
			$script:localFolder = $Item.installLocation
            break
        }
    }
    
    If (!$localFolder) {
		writeToLog F "PME installation not found."
		writeToLog F "Will do post-cleanup but marking script as failed."
		removeProcesses
		removePMEServices
		removePMEFoldersAndKeys
		postRuntime
 		Exit 1001
	}

   If (!(Test-Path $localFolder)) {
    	writeToLog F "The PME install location is pointing to a path that doesn't exist."
		writeToLog F "Failing script."
		postRuntime
		Exit 1001
	}

    If (($localFolder -match '.+?\\$') -eq $false) {
        $script:localFolder = $script:localFolder + "\"
	}

	$script:pmeFolder = (Split-Path $localFolder) + "\"

	writeToLog V "PME Folder located:`r`n`t$pmeFolder"

	writeToLog V ("Completed running {0} function." -f $MyInvocation.MyCommand)
}

function determinePMEVersion() {
	writeToLog V ("Started running {0} function." -f $MyInvocation.MyCommand)

    try {
        $pmeVersionRaw = Get-Process -Name *PME.Agent -FileVersionInfo | Select-Object ProductName,ProductVersion,FileVersion | Sort-Object -unique -ErrorAction Stop
    } catch {
        $msg = $_.Exception.Message
        $line = $_.InvocationInfo.ScriptLineNumber
        writeToLog F "Error occurred locating an applicable PME Agent process, due to:`r`n`t$msg"
        writeToLog V "This occurred on line number: $line"
        writeToLog F "Failing script."
		postRuntime
		Exit 1001
    }

	$pmeProductName = $pmeVersionRaw.ProductName
	$pmeProductVersion = $pmeVersionRaw.ProductVersion

	writeToLog V "Detected PME Version: $pmeProductVersion"

	If ($pmeProductName -eq "SolarWinds.MSP.PME.Agent") {
		writeToLog I "Detected installed PME Version is: $pmeProductVersion"
		$script:legacyPME = $true
	} ElseIf ($pmeProductName -eq "PME.Agent") {
		writeToLog I "Detected installed PME Version is: $pmeProductVersion"
		$script:legacyPME = $false
	} Else {
        writeToLog F "Failed to determind PME version."
        writeToLog F "Failing script."
        Exit 1001
    }

	writeToLog V ("Completed running {0} function." -f $MyInvocation.MyCommand)
}

function validateXml() {
    writeToLog V ("Started running {0} function." -f $MyInvocation.MyCommand)

    If ($legacyPME -eq $true) {
        $script:configFile = "C:\ProgramData\SolarWinds MSP\SolarWinds.MSP.CacheService\config\CacheService.xml"
    } Else {
        $script:configFile = "C:\ProgramData\MspPlatform\FileCacheServiceAgent\config\FileCacheServiceAgent.xml"

    }

    writeToLog V "Location of xml defined as:`r`n$configFile"
    writeToLog V "Testing to see if xml file exists."
    $testXml = Test-Path $configFile
    writeToLog V "Location test returned as: $testXml"

    If ($testXml -eq $false) {
        writeToLog F "The xml file was not found under:`r`n$configFile"
        writeToLog F "Failing script."
        Exit 1001
    }

    writeToLog I "The xml file is present on the device."
    writeToLog V ("Completed running {0} function." -f $MyInvocation.MyCommand)
}

function getPMEServices() {
    writeToLog V ("Started running {0} function." -f $MyInvocation.MyCommand)

    If ($legacyPME -eq $true) {
        $script:pmeAgentService = "SolarWinds.MSP.PME.Agent.PmeService"
        $script:cacheService = "SolarWinds.MSP.CacheService"
        $script:rpcServerService = "SolarWinds.MSP.RpcServerService"
    } Else {
        $script:pmeAgentService = "PME.Agent.PmeService"
        $script:cacheService = "SolarWinds.MSP.CacheService"
        $script:rpcServerService = "SolarWinds.MSP.RpcServerService"
    }

    writeToLog V "Will check PME Agent Service with the name: $pmeAgentService"
    writeToLog V "Will check PME Cache Service with the name: $cacheService"
    writeToLog V "Will check PME RPC Server Service with the name: $rpcServerService"

    try {
        $script:pmeAgentServiceStatus = Get-Service $pmeAgentService -ErrorAction Stop
        $script:cacheServiceStatus = Get-Service $cacheService -ErrorAction Stop
        $script:rpcServerServiceStatus = Get-Service $rpcServerService -ErrorAction Stop
    } catch {
        writeToLog F "Was unable to determine the PME services, due to:"
        writeToLog F $_
        writeToLog F "Failing script."
        Exit 1001
    }

    writeToLog I "Was successful in detecting the PME Windows Services on the device."
    writeToLog V ("Completed running {0} function." -f $MyInvocation.MyCommand)
}

function stopPMEServices() {
    writeToLog V ("Started running {0} function." -f $MyInvocation.MyCommand)

    writeToLog V "Attempting to stop the PME Services."

    try {
        Stop-Service $pmeAgentService -ErrorAction Stop | Out-Null
        Stop-Service $cacheService -ErrorAction Stop  | Out-Null
        Stop-Service $rpcServerService -ErrorAction Stop | Out-Null
    } catch {
        writeToLog F "Failed to stop the PME Windows Services, due to:"
        writeToLog F $_
        writeToLog F "Failing script."
        Exit 1001
    }

    writeToLog I "PME Windows Services have been stopped successfully."
    writeToLog V ("Completed running {0} function." -f $MyInvocation.MyCommand)
}

function startPMEServices() {
    writeToLog V ("Started running {0} function." -f $MyInvocation.MyCommand)

    writeToLog V "Attempting to start the PME Services."

    try {
        Start-Service $pmeAgentService -ErrorAction Stop | Out-Null
        Start-Service $cacheService -ErrorAction Stop | Out-Null
        Start-Service $rpcServerService -ErrorAction Stop | Out-Null
    } catch {
        writeToLog F "Failed to stop the PME Windows Services, due to:"
        writeToLog F $_
        writeToLog F "Failing script."
        Exit 1001
    }

    writeToLog I "PME Windows Services have been started successfully."
    writeToLog V ("Completed running {0} function." -f $MyInvocation.MyCommand)
}

function getXmlContent() {
    writeToLog V ("Started running {0} function." -f $MyInvocation.MyCommand)

    writeToLog V "Extracting content of the Cache Service xml"
    [xml]$script:xmlContent = Get-Content $configFile

    $script:xmlApplianceVersion = $xmlContent.Configuration.ApplianceVersion
    $script:xmlCacheLocation = $xmlContent.Configuration.CacheLocation
    $script:xmlCacheSize = $xmlContent.Configuration.CacheSizeInMB
    
    writeToLog I "Values determined in xml:`r`n`tAppliance Version: $xmlApplianceVersion`r`n`tCache Folder Size: $xmlCacheSize MB`r`n`tCache Location: $xmlCacheLocation"
    
    writeToLog V ("Completed running {0} function." -f $MyInvocation.MyCommand)
}

function getCacheFolderDetails() {
    writeToLog V ("Started running {0} function." -f $MyInvocation.MyCommand)

    $fullCacheSize = ((Get-ChildItem $xmlCacheLocation -Recurse | Measure-Object -Property Length -Sum -ErrorAction Stop).Sum / 1MB)
    writeToLog V "Full size of contents in cache folder: $fullCacheSize MB"

    $script:cacheSize = ((Get-ChildItem $xmlCacheLocation -Recurse -Exclude ".zip, *.xml" | Measure-Object -Property Length -Sum -ErrorAction Stop).Sum / 1MB)
    writeToLog I "Size of patch cache in cache folder: $cacheSize MB"

    writeToLog V ("Completed running {0} function." -f $MyInvocation.MyCommand)
}

function clearCacheFolder() {
    writeToLog V ("Started running {0} function." -f $MyInvocation.MyCommand)

    writeToLog V "Will now remove contents from cache folder."
    writeToLog V "Will be keeping any files used by PME for functionality."

    try {
        Remove-Item $xmlCacheLocation\* -Exclude *.xml, *.zip -recurse -force
    } catch {
        writeToLog W "Was unable to remove some items from cache folder, due to:"
        writeToLog W $_
    }

    writeToLog I "Now successfully cleared cache contents."
    writeToLog I "Will now do another check on cache folder size."

    writeToLog V ("Completed running {0} function." -f $MyInvocation.MyCommand)
}

function writeToLog($state, $message) {

    $script:timestamp = "[{0:dd/MM/yy} {0:HH:mm:ss}]" -f (Get-Date)

	switch -regex -Wildcard ($state) {
		"I" {
			$state = "INFO"
            $colour = "Cyan"
		}
		"E" {
			$state = "ERROR"
            $colour = "Red"
		}
		"W" {
			$state = "WARNING"
            $colour = "Yellow"
		}
		"F"  {
			$state = "FAILURE"
            $colour = "Red"
        }
        "C"  {
			$state = "COMPLETE"
            $colour = "Green"
        }
        "V"  {
            If ($verboseMode -eq $true) {
                $state = "VERBOSE"
                $colour = "Magenta"
            } Else {
                return
            }
		}
		""  {
			$state = "INFO"
		}
		Default {
			$state = "INFO"
		}
     }

    Write-Host "$($timeStamp) - [$state]: $message" -ForegroundColor $colour
    Write-Output "$($timeStamp) - [$state]: $message" | Out-file $logFilePath -Append -ErrorAction SilentlyContinue
}

function main() {
    setupLogging
    validateUserInput
    getAgentPath
    determinePMEVersion
    validateXml
    getPMEServices
    stopPMEServices
    getXmlContent
    getCacheFolderDetails
    clearCacheFolder
    getCacheFolderDetails
    startPMEServices
}
main