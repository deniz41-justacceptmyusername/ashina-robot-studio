param([Parameter(Mandatory=$true)][string]$PlayerDirectory)

$ErrorActionPreference='Stop'
$player=(Resolve-Path -LiteralPath $PlayerDirectory).Path
if(-not (Test-Path -LiteralPath (Join-Path $player 'RobotStudio.exe'))){throw 'RobotStudio.exe was not found.'}

$toolsDir=Join-Path $player 'tools'
New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
$helper=Join-Path $toolsDir 'cloudflared.exe'
$digest='555b46c85582461d189cac30d14ad05480d612613801be9f110b8ce08c5b2113'
if(-not (Test-Path -LiteralPath $helper) -or (Get-FileHash -LiteralPath $helper -Algorithm SHA256).Hash -ne $digest){
 $download=Join-Path $toolsDir 'cloudflared.download'
 try {
  Invoke-WebRequest -Uri 'https://github.com/cloudflare/cloudflared/releases/download/2026.11.2/cloudflared-windows-amd64.exe' -OutFile $download -TimeoutSec 120
  if((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $digest){throw 'cloudflared download hash mismatch.'}
  Move-Item -LiteralPath $download -Destination $helper -Force
 }finally{if(Test-Path -LiteralPath $download){Remove-Item -LiteralPath $download}}
}
Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/cloudflare/cloudflared/2026.11.2/LICENSE' -OutFile (Join-Path $toolsDir 'cloudflared-LICENSE.txt') -TimeoutSec 30
