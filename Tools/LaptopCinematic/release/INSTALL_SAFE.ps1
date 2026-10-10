# ROKAS Laptop Cinematic: scoped SHA-256 install with safe backup/rollback.
[CmdletBinding()]
param(
 [string]$Target='D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY',
 [switch]$Rollback,
 [string]$RunId,
 [switch]$DryRun,
 [int]$TestInterruptAt=0
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$here=Split-Path -Parent $MyInvocation.MyCommand.Path
$prefix='Assets/Rokas/Resources/LaptopCinematic/'
$manifestRel=$prefix+'right_hand_manifest.json'
function Allowed([string]$p) {
 $s=$p.Replace('\','/')
 if ($s -cne $manifestRel -and $s -cnotmatch '^Assets/Rokas/Resources/LaptopCinematic/HandsRight/Hand_[0-9]{4}\.png$') {throw "Disallowed file path: $p"}
 return $s
}
function P([string]$root,[string]$rel) { Join-Path $root $rel.Replace('/',[IO.Path]::DirectorySeparatorChar) }
function H([string]$file) { (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() }
function NoLinks([string]$root,[string]$rel) {
 $cursor=$root
 foreach($part in $rel.Replace('\','/').Split('/')) {
  $cursor=Join-Path $cursor $part
  if(Test-Path -LiteralPath $cursor) {
   if(((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {throw "Symlink/junction refused: $cursor"}
  }
 }
}
if(-not (Test-Path -LiteralPath $Target -PathType Container)){throw 'Target folder is missing'}
$Target=(Resolve-Path -LiteralPath $Target).Path
$backups=Join-Path $Target 'ROKAS_LaptopCinematic_Backups'
NoLinks $Target 'ROKAS_LaptopCinematic_Backups'
if($Rollback) {
 if(-not (Test-Path -LiteralPath $backups -PathType Container)){throw 'No backups'}
 if(-not $RunId) {
  $last=Join-Path $backups 'last_install.txt'
  if(-not (Test-Path -LiteralPath $last)){throw 'No last install'}
  $RunId=(Get-Content -LiteralPath $last -Raw).Trim()
 }
 if($RunId -cnotmatch '^[0-9]{8}_[0-9]{6}_[0-9a-f]{8}$'){throw 'Invalid RunId'}
 NoLinks $backups $RunId
 $backup=Join-Path $backups $RunId
 $record=Get-Content -LiteralPath (Join-Path $backup 'record.json') -Raw | ConvertFrom-Json
 if([string]$record.target -cne $Target -or @($record.files).Count -ne 50){throw 'Backup record mismatch'}
 $seen=@{}
 # Mandatory full preflight. NO disk mutation before all installed/backup hashes validate.
 foreach($f in $record.files) {
  $r=Allowed ([string]$f.relative)
  if($seen.ContainsKey($r)){throw "Duplicate record: $r"}; $seen[$r]=$true
  NoLinks $Target $r
  $dest=P $Target $r
  if(-not (Test-Path -LiteralPath $dest -PathType Leaf) -or (H $dest) -cne ([string]$f.installedSha).ToLowerInvariant()) {throw "User modified installed resource: $r"}
  if([bool]$f.existed) {
   NoLinks $backup ('previous/'+$r)
   $old=P $backup ('previous/'+$r)
   if(-not (Test-Path -LiteralPath $old -PathType Leaf) -or (H $old) -cne ([string]$f.previousSha).ToLowerInvariant()){throw "Backup damaged: $r"}
  }
 }
 if($DryRun){Write-Host 'ROLLBACK_DRYRUN_PASS';return}
 foreach($f in $record.files) {
  $r=Allowed ([string]$f.relative);$dest=P $Target $r
  if([bool]$f.existed){Copy-Item -LiteralPath (P $backup ('previous/'+$r)) -Destination $dest -Force}
  else{Remove-Item -LiteralPath $dest -Force}
 }
 Write-Host "ROLLBACK_PASS $RunId"
 return
}
foreach($r in @(
 'ProjectSettings/ProjectVersion.txt',
 'Assets/Rokas/Scripts/Presentation/LaptopCinematicSequence.cs',
 $prefix+'LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF.png'
)) {
 NoLinks $Target $r
 if(-not (Test-Path -LiteralPath (P $Target $r) -PathType Leaf)){throw "Wrong ROKAS Unity target, missing $r"}
}
$cs=Get-Content -LiteralPath (P $Target 'Assets/Rokas/Scripts/Presentation/LaptopCinematicSequence.cs') -Raw
if(-not $cs.Contains('HandsRight') -or -not $cs.Contains('right_hand_manifest')){throw 'Incompatible Unity runtime'}
if(@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count -gt 0){throw 'Close Unity first'}
$release=Join-Path $here 'RELEASE_MANIFEST.json'
if(-not (Test-Path -LiteralPath $release)){throw 'Release manifest missing'}
$m=Get-Content -LiteralPath $release -Raw | ConvertFrom-Json
if([string]$m.validation -cne 'VISUAL_AND_UNITY_QA_APPROVED'){throw 'Release gates not approved'}
if([string]$m.sourceSha -cnotmatch '^[a-fA-F0-9]{40}$' -or @($m.files).Count -ne 50){throw 'Bad manifest metadata'}
$expected=@{}
for($i=0;$i -lt 49;$i++){$expected[$prefix+('HandsRight/Hand_{0:d4}.png' -f $i)]=$true}
$expected[$manifestRel]=$true
foreach($f in $m.files) {
 $r=Allowed ([string]$f.relative)
 if(-not $expected.ContainsKey($r)){throw "Unexpected/duplicate entry: $r"}
 $expected.Remove($r)
 if([string]$f.sha256 -cnotmatch '^[a-fA-F0-9]{64}$'){throw 'Invalid SHA format'}
 $src=P (Join-Path $here 'payload') $r
 NoLinks (Join-Path $here 'payload') $r
 NoLinks $Target $r
 if(-not (Test-Path -LiteralPath $src -PathType Leaf) -or (H $src) -cne ([string]$f.sha256).ToLowerInvariant()){throw "Missing or corrupted payload: $r"}
}
if($expected.Count -ne 0){throw 'Missing expected hand resources'}
if($DryRun){Write-Host 'INSTALL_DRYRUN_PASS';return}
New-Item -ItemType Directory -Path $backups -Force|Out-Null
$id=(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$backup=Join-Path $backups $id
New-Item -ItemType Directory -Path (Join-Path $backup 'previous') -Force|Out-Null
$record=[ordered]@{target=$Target;runId=$id;sourceSha=[string]$m.sourceSha;files=@()}
# Back up all existing files before making any live changes.
foreach($f in $m.files) {
 $r=Allowed ([string]$f.relative);$dest=P $Target $r
 $exists=Test-Path -LiteralPath $dest -PathType Leaf
 $prev=$null
 if($exists){
  $prev=H $dest;$saved=P $backup ('previous/'+$r)
  New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force|Out-Null
  Copy-Item -LiteralPath $dest -Destination $saved
  if((H $saved) -cne $prev){throw "Backup SHA mismatch: $r"}
 }
 $record.files+=@{relative=$r;existed=$exists;previousSha=$prev;installedSha=([string]$f.sha256).ToLowerInvariant()}
}
$record|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $backup 'record.json') -Encoding UTF8
$written=@()
try {
 foreach($f in $m.files) {
  $r=Allowed ([string]$f.relative);$dest=P $Target $r
  New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force|Out-Null
  $written+= $r
  Copy-Item -LiteralPath (P (Join-Path $here 'payload') $r) -Destination $dest -Force
  if((H $dest) -cne ([string]$f.sha256).ToLowerInvariant()){throw "Postcopy SHA mismatch: $r"}
  if($TestInterruptAt -gt 0 -and $written.Count -eq $TestInterruptAt){throw 'Simulated interruption in CI test'}
 }
} catch {
 $cause=$_.Exception.Message
 [array]::Reverse($written)
 foreach($r in $written) {
  $entry=@($record.files|Where-Object {$_.relative -ceq $r})[0]
  $dest=P $Target $r
  if([bool]$entry.existed){Copy-Item -LiteralPath (P $backup ('previous/'+$r)) -Destination $dest -Force}
  elseif(Test-Path -LiteralPath $dest){Remove-Item -LiteralPath $dest -Force}
 }
 throw "Install failed; changed files restored: $cause"
}
[IO.File]::WriteAllText((Join-Path $backups 'last_install.txt'),$id)
Write-Host "INSTALL_PASS $id"
