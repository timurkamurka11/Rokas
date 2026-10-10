# Safe on windows-latest only. Uses isolated temporary directory, never D:.
param([string]$InstallerSource)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Join-Path $env:RUNNER_TEMP ('rokas-test-'+[Guid]::NewGuid().ToString('N'))
$pack=Join-Path $root 'pack';$unity=Join-Path $root 'unity'
$pre='Assets/Rokas/Resources/LaptopCinematic/'
function D([string]$p){Join-Path $unity $p.Replace('/',[IO.Path]::DirectorySeparatorChar)}
function S([string]$p){Join-Path (Join-Path $pack 'payload') $p.Replace('/',[IO.Path]::DirectorySeparatorChar)}
function H([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
function Check([bool]$p,[string]$msg){if(-not $p){throw "FAILED: $msg"}}
function Invoke-Test([string[]]$opts,[bool]$expectSuccess){
 & pwsh -NoProfile -File (Join-Path $pack 'INSTALL_SAFE.ps1') -Target $unity @opts *> (Join-Path $root 'last.log')
 $ok=$LASTEXITCODE -eq 0
 Check ($ok -eq $expectSuccess) ("Unexpected exit $LASTEXITCODE for: "+($opts -join ' '))
}
try {
 New-Item -ItemType Directory -Path $pack,$unity -Force|Out-Null
 Copy-Item -LiteralPath $InstallerSource -Destination (Join-Path $pack 'INSTALL_SAFE.ps1')
 foreach($r in @('ProjectSettings/ProjectVersion.txt','Assets/Rokas/Scripts/Presentation/LaptopCinematicSequence.cs',$pre+'LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF.png')){
  $d=D $r;New-Item -ItemType Directory -Path (Split-Path -Parent $d) -Force|Out-Null
  [IO.File]::WriteAllText($d,('fixture HandsRight right_hand_manifest '+$r))
 }
 $prior=D ($pre+'HandsRight/Hand_0031.png')
 New-Item -ItemType Directory -Path (Split-Path -Parent $prior) -Force|Out-Null
 [IO.File]::WriteAllText($prior,'USER_OLD')
 $data=[Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+m9q0AAAAASUVORK5CYII=')
 $entries=@()
 for($i=0;$i -lt 49;$i++){
  $r=$pre+('HandsRight/Hand_{0:d4}.png' -f $i)
  $f=S $r;New-Item -ItemType Directory -Path (Split-Path -Parent $f) -Force|Out-Null
  [IO.File]::WriteAllBytes($f,$data)
  $entries+=@{relative=$r;sha256=(H $f)}
 }
 $r=$pre+'right_hand_manifest.json';$f=S $r
 [IO.File]::WriteAllText($f,'{"fps":24,"frames":49}')
 $entries+=@{relative=$r;sha256=(H $f)}
 $meta=@{sourceSha=('a'*40);validation='VISUAL_AND_UNITY_QA_APPROVED';files=$entries}
 function Save-Meta{$meta|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $pack 'RELEASE_MANIFEST.json')}
 Save-Meta
 Invoke-Test @('-DryRun') $true
 Check ((Get-Content -LiteralPath $prior -Raw) -ceq 'USER_OLD') 'dry run mutated target'
 $damaged=S ($pre+'HandsRight/Hand_0015.png')
 [IO.File]::WriteAllText($damaged,'bad')
 Invoke-Test @('-DryRun') $false
 [IO.File]::WriteAllBytes($damaged,$data)
 Remove-Item -LiteralPath $damaged
 Invoke-Test @('-DryRun') $false
 [IO.File]::WriteAllBytes($damaged,$data)
 $meta.validation='REVIEW_ONLY';Save-Meta
 Invoke-Test @('-DryRun') $false
 $meta.validation='VISUAL_AND_UNITY_QA_APPROVED';Save-Meta
 Invoke-Test @('-TestInterruptAt','35') $false
 Check ((Get-Content -LiteralPath $prior -Raw) -ceq 'USER_OLD') 'interruption damaged user file'
 Check (-not (Test-Path -LiteralPath (D ($pre+'HandsRight/Hand_0000.png')))) 'interruption left new file'
 Invoke-Test @() $true
 foreach($e in $entries){Check ((H (D $e.relative)) -ceq $e.sha256) 'installed hash mismatch'}
 $backroot=Join-Path $unity 'ROKAS_LaptopCinematic_Backups'
 $id=(Get-Content -LiteralPath (Join-Path $backroot 'last_install.txt') -Raw).Trim()
 $saved=Join-Path $backroot ($id+'/previous/'+$pre+'HandsRight/Hand_0031.png')
 Check ((Get-Content -LiteralPath $saved -Raw) -ceq 'USER_OLD') 'backup missing original'
 Invoke-Test @('-Rollback','-RunId',$id,'-DryRun') $true
 [IO.File]::WriteAllText($prior,'CHANGED_BY_USER')
 Invoke-Test @('-Rollback','-RunId',$id) $false
 Check ((Get-Content -LiteralPath $prior -Raw) -ceq 'CHANGED_BY_USER') 'unsafe rollback changed user file'
 [IO.File]::WriteAllBytes($prior,$data)
 [IO.File]::WriteAllText($saved,'BAD_BACKUP')
 Invoke-Test @('-Rollback','-RunId',$id) $false
 [IO.File]::WriteAllText($saved,'USER_OLD')
 Invoke-Test @('-Rollback','-RunId',$id) $true
 Check ((Get-Content -LiteralPath $prior -Raw) -ceq 'USER_OLD') 'rollback not restored'
 Check (-not (Test-Path -LiteralPath (D ($pre+'HandsRight/Hand_0000.png')))) 'rollback left created file'
 Write-Host 'ROKAS_WINDOWS_INSTALLER_SAFETY_PASS: dryrun, hashes, missing, gate, interrupted, backup, guarded rollback, backup integrity'
} finally {
 Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
