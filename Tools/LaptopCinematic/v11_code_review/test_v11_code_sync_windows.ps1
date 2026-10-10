# Executed only on disposable windows-latest Unity-shaped fixture. NEVER D:.
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PackageRoot,
      [Parameter(Mandatory=$true)][string]$BaseRoot)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$names=@('LaptopCinematicSequence.cs','LaptopPhysicalDesktop.cs','LaptopView.cs','RokasAudio.cs','RokasView.cs')
$prefix='Assets/Rokas/Scripts/Presentation/'
$root=Join-Path $env:RUNNER_TEMP ('ROKAS_V11_SCOPED_TEST_'+[Guid]::NewGuid().ToString('N'))
$unity=Join-Path $root 'unity'
$package=Join-Path $root 'package'
$sentinel=Join-Path $root 'UNTOUCHED.txt'
function Need([bool]$ok,[string]$msg){if(-not $ok){throw "V11_TEST_FAILED: $msg"}}
function Hash([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
function P([string]$base,[string]$rel){Join-Path $base ($rel.Replace('/',[IO.Path]::DirectorySeparatorChar))}
function Invoke-QA([string[]]$opts,[bool]$shouldPass){
 $log=Join-Path $root 'sync.log'
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $package 'V11_CODE_SYNC.ps1') -Target $unity @opts *> $log
 $status=$LASTEXITCODE -eq 0
 if($status -ne $shouldPass){Write-Host (Get-Content -LiteralPath $log -Raw)}
 Need ($status -eq $shouldPass) "Unexpected code sync result for $($opts -join ' '); code=$LASTEXITCODE"
}
function Check-Baseline {
 foreach($name in $names) {
  $r=$prefix+$name
  Need ((Hash (P $unity $r)) -ceq (Hash (P $BaseRoot $r))) "Baseline altered: $r"
 }
}
function Check-Target {
 foreach($name in $names) {
  $r=$prefix+$name
  Need ((Hash (P $unity $r)) -ceq (Hash (P (Join-Path $package 'payload') $r))) "V11 mismatch: $r"
 }
}
try {
 New-Item -ItemType Directory -Path $unity,$package -Force | Out-Null
 Copy-Item -Path (Join-Path $PackageRoot '*') -Destination $package -Recurse -Force
 [IO.File]::WriteAllText($sentinel,'SAFE_SENTINEL')
 foreach($name in $names) {
  $r=$prefix+$name
  $dest=P $unity $r
  New-Item -ItemType Directory -Path (Split-Path -Parent $dest) -Force|Out-Null
  Copy-Item -LiteralPath (P $BaseRoot $r) -Destination $dest -Force
 }
 $project=P $unity 'ProjectSettings/ProjectVersion.txt'
 New-Item -ItemType Directory -Path (Split-Path -Parent $project) -Force|Out-Null
 [IO.File]::WriteAllText($project,'m_EditorVersion: 6000.3.19f1')
 $bad=$prefix+'LaptopPhysicalDesktop.cs'
 $badPath=P $unity $bad
 $original=[IO.File]::ReadAllBytes($badPath)
 # Read-only default audit and explicit apply guard
 Invoke-QA @() $true
 Check-Baseline
 Invoke-QA @('-Mode','Apply') $false
 Check-Baseline
 [IO.File]::WriteAllText($badPath,'USER UNCOMMITTED CHANGE')
 Invoke-QA @() $false
 Invoke-QA @('-Mode','Apply','-ConfirmCodeSync') $false
 Need ((Get-Content -LiteralPath $badPath -Raw) -ceq 'USER UNCOMMITTED CHANGE') 'User divergent code overwritten'
 [IO.File]::WriteAllBytes($badPath,$original)
 # Tampered payload and manifest are all rejected before writes.
 $file=P (Join-Path $package 'payload') ($prefix+'LaptopView.cs')
 $bytes=[IO.File]::ReadAllBytes($file)
 [IO.File]::WriteAllText($file,'MALICIOUS PAYLOAD')
 Invoke-QA @() $false
 [IO.File]::WriteAllBytes($file,$bytes)
 $manifest=Join-Path $package 'V11_CODE_MANIFEST.json'
 $clean=[IO.File]::ReadAllBytes($manifest)
 $m=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json
 $m.files[0].relative='../escape.cs'
 $m|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $manifest -Encoding UTF8
 Invoke-QA @() $false
 [IO.File]::WriteAllBytes($manifest,$clean)
 $m=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json
 $m.validation='VISUAL_AND_UNITY_QA_APPROVED'
 $m|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $manifest -Encoding UTF8
 Invoke-QA @() $false
 [IO.File]::WriteAllBytes($manifest,$clean)
 # Simulated interruption must restore ALL modified source files.
 Invoke-QA @('-Mode','Apply','-ConfirmCodeSync','-SimulateInterruptAfter','3') $false
 Check-Baseline
 # Success: exactly five allowlisted C# replacements, all SHA verified.
 Invoke-QA @('-Mode','Apply','-ConfirmCodeSync') $true
 Check-Target
 $backupRoot=Join-Path $unity 'ROKAS_LaptopV11_Code_Backups'
 $recordId=(Get-Content -LiteralPath (Join-Path $backupRoot 'last_install.txt') -Raw).Trim()
 Need ($recordId -cmatch '^[0-9]{8}_[0-9]{6}_[0-9a-f]{8}$') 'Unsafe generated backup ID'
 $beforeCount=@(Get-ChildItem -LiteralPath $backupRoot -Directory).Count
 Invoke-QA @('-Mode','Apply','-ConfirmCodeSync') $true
 Need (@(Get-ChildItem -LiteralPath $backupRoot -Directory).Count -eq $beforeCount) 'Idempotent apply created backup'
 Check-Target
 # Postinstall user edits must BLOCK rollback (no rewrite).
 $target=P $unity ($prefix+'LaptopPhysicalDesktop.cs')
 $v11=[IO.File]::ReadAllBytes($target)
 [IO.File]::WriteAllText($target,'LOCAL POST-V11 EDIT')
 Invoke-QA @('-Mode','Rollback','-ConfirmCodeSync') $false
 Need ((Get-Content -LiteralPath $target -Raw) -ceq 'LOCAL POST-V11 EDIT') 'Rollback modified user-edited source'
 [IO.File]::WriteAllBytes($target,$v11)
 Invoke-QA @('-Mode','Rollback') $true
 Check-Target
 Invoke-QA @('-Mode','Rollback','-ConfirmCodeSync') $true
 Check-Baseline
 # Wrong Unity destination is refused.
 $wrong=Join-Path $root 'not_Unity'
 New-Item -ItemType Directory -Path $wrong -Force | Out-Null
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $package 'V11_CODE_SYNC.ps1') -Target $wrong *> (Join-Path $root 'wrong.log')
 Need ($LASTEXITCODE -ne 0) 'Incorrect Unity root accepted'
 # Junction to path OUTSIDE test fixture cannot be followed.
 $parent=P $unity 'Assets/Rokas/Scripts/Presentation'
 $tmp=Join-Path $root 'source_backup'
 Move-Item -LiteralPath $parent -Destination $tmp
 $outside=Join-Path $root 'evil_external'
 New-Item -ItemType Directory -Path $outside | Out-Null
 New-Item -ItemType Junction -Path $parent -Target $outside | Out-Null
 Invoke-QA @() $false
 Need (@(Get-ChildItem -LiteralPath $outside -Force).Count -eq 0) 'Junction leaked outside'
 Remove-Item -LiteralPath $parent -Force
 Move-Item -LiteralPath $tmp -Destination $parent
 Need ((Get-Content -LiteralPath $sentinel -Raw) -ceq 'SAFE_SENTINEL') 'Wrote outside allowlist'
 Write-Host 'ROKAS_V11_CODE_SYNC_WINDOWS_PASS: PS5.1, audit-readonly, exact-5-SHA, divergence, review-gate, payload tamper, traversal, interruption, backup, idempotency, modified-user rollback, wrong-root, junction and scope.'
} finally {
 Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}
exit 0
