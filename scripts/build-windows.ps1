param([Parameter(Mandatory=$true)][string]$UnityPath)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$project=Join-Path $repo 'Unity'
$output=Join-Path $repo 'build/Windows'
if(-not(Test-Path -LiteralPath $UnityPath)){throw 'Unity executable not found.'}
New-Item -ItemType Directory -Path $output -Force | Out-Null
$previousOutput=$env:ASHINA_UNITY_OUTPUT
try {
 $env:ASHINA_UNITY_OUTPUT=Join-Path $output 'RobotStudio.exe'
 $log=Join-Path $repo 'build/unity-build.log'
 $arguments=@('-batchmode','-nographics','-quit','-projectPath',('"'+$project+'"'),'-executeMethod','AshinaStudio.Editor.BatchBuild.Run','-logFile',('"'+$log+'"'))
 $process=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WorkingDirectory $project -WindowStyle Hidden -PassThru -Wait
 if($process.ExitCode -ne 0){throw "Unity build failed. See $log"}
 $bundle=Join-Path $repo 'build/Ashina-Robot-Studio'
 New-Item -ItemType Directory -Path $bundle -Force | Out-Null
 foreach($entry in @(@{Source=$output;Name='Windows'},@{Source=(Join-Path $repo 'examples');Name='examples'},@{Source=(Join-Path $repo 'integration');Name='integration'})){
  $destination=Join-Path $bundle $entry.Name
  New-Item -ItemType Directory -Path $destination -Force | Out-Null
  Get-ChildItem -LiteralPath $entry.Source -Force | Copy-Item -Destination $destination -Recurse -Force
 }
 Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination $bundle -Force
 Copy-Item -LiteralPath (Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination $bundle -Force
 @'
@echo off
start "" "%~dp0Windows\RobotStudio.exe" --project "%~dp0examples\REBUILT-Training.ashina.json"
'@ | Set-Content -LiteralPath (Join-Path $bundle 'Start.cmd') -Encoding ascii
 Copy-Item -LiteralPath (Join-Path $bundle 'Start.cmd') -Destination (Join-Path $bundle 'Baslat.cmd') -Force
 @'
@echo off
start "" "%~dp0Windows\RobotStudio.exe" --project "%~dp0examples\REEFSCAPE-Coral-Lab.ashina.json"
'@ | Set-Content -LiteralPath (Join-Path $bundle 'Coral-Lab.cmd') -Encoding ascii
 Write-Output "Player bundle: $bundle"
}finally{$env:ASHINA_UNITY_OUTPUT=$previousOutput}
