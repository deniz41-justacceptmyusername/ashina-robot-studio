param([Parameter(Mandatory=$true)][string]$UnityPath,[switch]$IncludeInternetRooms)
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
 function Copy-BundleTree([string]$from,[string]$to){
  New-Item -ItemType Directory -Path $to -Force | Out-Null
  foreach($item in Get-ChildItem -LiteralPath $from -Force){
   if($item.Name -match 'DoNotShip|BackUpThisFolder' -or $item.Extension -in '.pdb','.mdb'){continue}
   if($item.PSIsContainer){Copy-BundleTree $item.FullName (Join-Path $to $item.Name)}else{Copy-Item -LiteralPath $item.FullName -Destination $to -Force}
  }
 }
 Copy-BundleTree $output $bundle
 if($IncludeInternetRooms){& (Join-Path $PSScriptRoot 'prepare-internet-rooms.ps1') -PlayerDirectory $bundle}
 foreach($name in @('examples','integration')){Copy-BundleTree (Join-Path $repo $name) (Join-Path $bundle $name)}
 Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination $bundle -Force
 Copy-Item -LiteralPath (Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination $bundle -Force
 Write-Output "Player bundle: $bundle"
}finally{$env:ASHINA_UNITY_OUTPUT=$previousOutput}
