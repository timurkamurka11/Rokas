# V11 CODE-ONLY REVIEW SYNC; NOT an approved hand-art or gameplay release.
# Default AUDIT does not modify the user's Unity project.
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$Target,
 [ValidateSet('Audit','Apply','Rollback')][string]$Mode='Audit',
 [switch]$ConfirmCodeSync,
 [string]$RunId,
 [int]$SimulateInterruptAfter=0
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$Pinned='f7fc0779bbe16c1b2dd4ffc25dc4ddc2d69d74dc'
$Base='c863b588a88eba8398ffdc785ac9a1c8f0ce083f'
$Pre='Assets/Rokas/Scripts/Presentation/'
$Names=@('LaptopCinematicSequence.cs','LaptopPhysicalDesktop.cs','LaptopView.cs','RokasAudio.cs','RokasView.cs')
$Allowed=@($Names | ForEach-Object { $Pre+$_ })
$Here=Split-Path -Parent $MyInvocation.MyCommand.Path
function Need([bool]$ok,[string]$msg) { if(-not $ok){throw $msg} }
function Sha([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant() }
function LocalPath([string]$root,[string]$rel) { Join-Path $root ($rel.Replace('/',[IO.Path]::DirectorySeparatorChar)) }
function LinkGuard([string]$root,[string]$rel) {
 $where=$root
 Need (Test-Path -LiteralPath $root -PathType Container) "Root missing: $root"
 Need (((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) "Root junction/symlink refused: $root"
 foreach($part in $rel.Replace('\','/').Split('/')) {
  Need ($part -ne '' -and $part -ne '.' -and $part -ne '..') 'Traversal refused'
  $where=Join-Path $where $part
  if(Test-Path -LiteralPath $where) {
   Need (((Get-Item -LiteralPath $where -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) "Junction/symlink refused: $where"
  }
 }
}
Need ([IO.Path]::IsPathRooted($Target)) 'Only absolute target paths are allowed'
Need (-not $Target.StartsWith('\\') -and -not $Target.StartsWith('//')) 'UNC/network targets refused'
Need (Test-Path -LiteralPath $Target -PathType Container) 'Target project missing'
$Target=(Resolve-Path -LiteralPath $Target).Path
LinkGuard $Target 'ProjectSettings/ProjectVersion.txt'
Need (Test-Path -LiteralPath (LocalPath $Target 'ProjectSettings/ProjectVersion.txt') -PathType Leaf) 'Not a Unity project'
$BackupRel='ROKAS_LaptopV11_Code_Backups'
LinkGuard $Target $BackupRel
$BackupRoot=Join-Path $Target $BackupRel

if($Mode -eq 'Rollback') {
 if(-not $RunId) {
  $last=Join-Path $BackupRoot 'last_install.txt'
  LinkGuard $Target ($BackupRel+'/last_install.txt')
  Need (Test-Path -LiteralPath $last -PathType Leaf) 'No completed V11 install to revert'
  $RunId=(Get-Content -LiteralPath $last -Raw).Trim()
 }
 Need ($RunId -cmatch '^[0-9]{8}_[0-9]{6}_[a-f0-9]{8}$') 'Unsafe backup ID'
 LinkGuard $Target ($BackupRel+'/'+$RunId+'/record.json')
 $folder=Join-Path $BackupRoot $RunId
 $recordPath=Join-Path $folder 'record.json'
 Need (Test-Path -LiteralPath $recordPath -PathType Leaf) 'Backup record not found'
 $record=Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
 Need ([string]$record.target -ceq $Target -and [string]$record.sourceSha -ceq $Pinned) 'Backup identity mismatch'
 Need (@($record.files).Count -eq 5) 'Wrong backup file count'
 $seen=@{}
 foreach($entry in $record.files) {
  $rel=[string]$entry.relative
  Need (($Allowed -ccontains $rel) -and -not $seen.ContainsKey($rel)) "Unsafe rollback path: $rel"
  $seen[$rel]=$true
  LinkGuard $Target $rel
  LinkGuard $Target ($BackupRel+'/'+$RunId+'/previous/'+$rel)
  $dest=LocalPath $Target $rel
  $saved=LocalPath $folder ('previous/'+$rel)
  Need ((Test-Path -LiteralPath $dest -PathType Leaf) -and (Sha $dest) -ceq [string]$entry.installedSha) "Modified after install, rollback refused: $rel"
  Need ((Test-Path -LiteralPath $saved -PathType Leaf) -and (Sha $saved) -ceq [string]$entry.previousSha) "Damaged backup, rollback refused: $rel"
 }
 Need ($seen.Count -eq 5) 'Rollback record incomplete'
 if($ConfirmCodeSync -eq $false) { Write-Host "ROLLBACK_AUDIT_PASS $RunId (add -ConfirmCodeSync to restore)";return }
 Need (@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count -eq 0) 'Close Unity before rollback'
 foreach($entry in $record.files) {
  $rel=[string]$entry.relative
  Copy-Item -LiteralPath (LocalPath $folder ('previous/'+$rel)) -Destination (LocalPath $Target $rel) -Force
  Need ((Sha (LocalPath $Target $rel)) -ceq [string]$entry.previousSha) "Restore hash mismatch: $rel"
 }
 Write-Host "V11_CODE_ROLLBACK_PASS $RunId";return
}

$Manifest=Join-Path $Here 'V11_CODE_MANIFEST.json'
Need (Test-Path -LiteralPath $Manifest -PathType Leaf) 'V11 code manifest missing'
$m=Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
Need ([string]$m.validation -ceq 'V11_CODE_REVIEW_ONLY_NOT_FINAL_RELEASE') 'Final-release masquerade or invalid review manifest'
Need ([string]$m.sourceSha -ceq $Pinned -and [string]$m.baseSha -ceq $Base) 'Wrong pinned source revision'
Need (@($m.files).Count -eq 5) 'Exactly five C# files required'
$seen=@{}
$states=@()
foreach($e in $m.files) {
 $rel=[string]$e.relative
 Need (($Allowed -ccontains $rel) -and -not $seen.ContainsKey($rel)) "Unexpected/duplicate C# file: $rel"
 $seen[$rel]=$true
 Need ([string]$e.beforeSha -cmatch '^[a-f0-9]{64}$' -and [string]$e.afterSha -cmatch '^[a-f0-9]{64}$') "Bad checksum metadata: $rel"
 LinkGuard $Here ('payload/'+$rel)
 LinkGuard $Target $rel
 $payload=LocalPath (Join-Path $Here 'payload') $rel
 $dest=LocalPath $Target $rel
 Need ((Test-Path -LiteralPath $payload -PathType Leaf) -and (Sha $payload) -ceq [string]$e.afterSha) "Payload integrity failure: $rel"
 Need (Test-Path -LiteralPath $dest -PathType Leaf) "Expected project source missing: $rel"
 $current=Sha $dest
 $state=if($current -ceq [string]$e.beforeSha){'BASELINE'}elseif($current -ceq [string]$e.afterSha){'V11'}else{'DIVERGED'}
 $states+=@{relative=$rel;beforeSha=[string]$e.beforeSha;afterSha=[string]$e.afterSha;currentSha=$current;state=$state}
 Write-Host "$state $rel SHA256=$current"
}
Need ($seen.Count -eq 5) 'Missing source file entry'
$divergent=@($states | Where-Object {$_.state -eq 'DIVERGED'}).Count
if($Mode -eq 'Audit') {
 if($divergent -gt 0) {throw 'AUDIT_BLOCKED: local C# differs from pinned base and V11; refuse overwriting user edits'}
 Write-Host 'V11_CODE_AUDIT_PASS (read-only; no project changes)'
 return
}
Need ($Mode -eq 'Apply') 'Invalid mode'
Need ($ConfirmCodeSync) 'Explicit -ConfirmCodeSync required. Audit is default'
Need ($divergent -eq 0) 'LOCAL_SOURCE_DIVERGED: no overwrite allowed'
if(@($states|Where-Object {$_.state -eq 'V11'}).Count -eq 5){Write-Host 'V11_CODE_ALREADY_INSTALLED: no file writes';return}
Need (@($states|Where-Object {$_.state -eq 'BASELINE'}).Count -eq 5) 'Mixed BASELINE/V11 state refused; manually review changes'
Need (@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count -eq 0) 'Close Unity before applying'
# Preflight ALL paths, original SHA and payload SHA before writing anything.
foreach($s in $states){Need ((Sha (LocalPath $Target $s.relative)) -ceq $s.beforeSha) "TOCTOU before backup: $($s.relative)"}
New-Item -ItemType Directory -Path $BackupRoot -Force|Out-Null
$id=(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$folder=Join-Path $BackupRoot $id
New-Item -ItemType Directory -Path (Join-Path $folder 'previous') -Force|Out-Null
foreach($s in $states) {
 $backup=LocalPath $folder ('previous/'+$s.relative)
 New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force|Out-Null
 Copy-Item -LiteralPath (LocalPath $Target $s.relative) -Destination $backup
 Need ((Sha $backup) -ceq $s.beforeSha) "Backup hash mismatch $($s.relative)"
}
$record=[ordered]@{target=$Target;sourceSha=$Pinned;runId=$id;files=@($states|ForEach-Object{
 @{relative=$_.relative;previousSha=$_.beforeSha;installedSha=$_.afterSha}
})}
$record|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $folder 'record.json') -Encoding UTF8
$changed=@()
try {
 foreach($s in $states) {
  $rel=$s.relative; $dest=LocalPath $Target $rel
  Need ((Sha $dest) -ceq $s.beforeSha) "Local change during install: $rel"
  $changed+= $rel
  Copy-Item -LiteralPath (LocalPath (Join-Path $Here 'payload') $rel) -Destination $dest -Force
  Need ((Sha $dest) -ceq $s.afterSha) "Installed code SHA mismatch: $rel"
  if($SimulateInterruptAfter -gt 0 -and $changed.Count -eq $SimulateInterruptAfter){throw 'Simulated interrupted copy'}
 }
} catch {
 $cause=$_.Exception.Message
 foreach($rel in $changed) {
  $source=LocalPath $folder ('previous/'+$rel)
  Copy-Item -LiteralPath $source -Destination (LocalPath $Target $rel) -Force
 }
 throw "V11_CODE_INSTALL_ABORTED_AND_RESTORED: $cause"
}
[IO.File]::WriteAllText((Join-Path $BackupRoot 'last_install.txt'),$id)
Write-Host "V11_CODE_REVIEW_APPLY_PASS $id (NOT final release; native visual/audio QA still required)"
