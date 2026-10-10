# Safe on windows-latest only. Uses isolated temporary directory, never D:.
param([string]$InstallerSource)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Join-Path $env:RUNNER_TEMP ('rokas-test-'+[Guid]::NewGuid().ToString('N'))
$outScope=Join-Path $root 'OUT_OF_SCOPE_SENTINEL.txt'
$pack=Join-Path $root 'pack';$unity=Join-Path $root 'unity'
$pre='Assets/Rokas/Resources/LaptopCinematic/'
function D([string]$p){Join-Path $unity $p.Replace('/',[IO.Path]::DirectorySeparatorChar)}
function S([string]$p){Join-Path (Join-Path $pack 'payload') $p.Replace('/',[IO.Path]::DirectorySeparatorChar)}
function FileSha([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
function Check([bool]$p,[string]$msg){if(-not $p){throw "FAILED: $msg"}}
function Invoke-Test([string[]]$opts,[bool]$expectSuccess){
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $pack 'INSTALL_SAFE.ps1') -Target $unity @opts *> (Join-Path $root 'last.log')
 $ok=$LASTEXITCODE -eq 0
 if ($ok -ne $expectSuccess) { Write-Host ('INSTALLER_DIAGNOSTIC: ' + (Get-Content -LiteralPath (Join-Path $root 'last.log') -Raw)) }
 Check ($ok -eq $expectSuccess) ("Unexpected exit $LASTEXITCODE for: "+($opts -join ' '))
}
try {
 New-Item -ItemType Directory -Path $pack,$unity -Force|Out-Null
 [IO.File]::WriteAllText($outScope,'NEVER_TOUCH')
 Copy-Item -LiteralPath $InstallerSource -Destination (Join-Path $pack 'INSTALL_SAFE.ps1')
 foreach($r in @('ProjectSettings/ProjectVersion.txt','Assets/Rokas/Scripts/Presentation/LaptopCinematicSequence.cs',($pre+'LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF.png'))){
  $d=D $r;New-Item -ItemType Directory -Path (Split-Path -Parent $d) -Force|Out-Null
  $fixture='fixture '+$r
  if($r -like '*LaptopCinematicSequence.cs'){$fixture='var json = Resources.Load<TextAsset>("LaptopCinematic/right_hand_manifest"); frames[i] = Resources.Load<Texture2D>(frame.resource);'}
  [IO.File]::WriteAllText($d,$fixture)
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
  $entries+=@{relative=$r;sha256=(FileSha $f)}
 }
 $r=$pre+'right_hand_manifest.json';$f=S $r
 $innerFrames=@()
 for($k=0;$k -lt 49;$k++){
  $innerFrames+=@{resource=('LaptopCinematic/HandsRight/Hand_{0:d4}' -f $k);x=0;y=0;w=1;h=1}
 }
 $inner=@{fps=24;handedness='right';frames=$innerFrames}
 $inner|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $f -Encoding UTF8
 $entries+=@{relative=$r;sha256=(FileSha $f)}
 $meta=@{sourceSha=('a'*40);validation='VISUAL_AND_UNITY_QA_APPROVED';files=$entries}
 function Save-Meta{$meta|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $pack 'RELEASE_MANIFEST.json')}
 Save-Meta
 Invoke-Test @('-DryRun') $true
 # Regression: the real C# loader has NO literal HandsRight and must still be accepted.
 $csPath=D 'Assets/Rokas/Scripts/Presentation/LaptopCinematicSequence.cs'
 $csBefore=Get-Content -LiteralPath $csPath -Raw
 Check (-not $csBefore.Contains('HandsRight')) 'fixture must reproduce real data-driven loader'
 [IO.File]::WriteAllText($csPath,'// old runtime without dynamic frame loader')
 Invoke-Test @('-DryRun') $false
 [IO.File]::WriteAllText($csPath,$csBefore)
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
 # Path traversal, absolute and UNC injection must all be refused in the
 # release JSON BEFORE writing to disk.
 $originalRel=$meta.files[0].relative
 foreach($candidate in @('../OUTSIDE.txt','C:/Windows/Temp/wrong.png','\\\\server\\share\\evil.png','assets/Rokas/Resources/LaptopCinematic/HandsRight/Hand_0000.png')) {
  $meta.files[0].relative=$candidate
  Save-Meta
  Invoke-Test @('-DryRun') $false
  Check ((Get-Content -LiteralPath $prior -Raw) -ceq 'USER_OLD') 'unsafe manifest modified target'
 }
 $meta.files[0].relative=$originalRel;Save-Meta
 # The second, inner manifest must also be a truthful 49-frame descriptor.
 $innerFile=S ($pre+'right_hand_manifest.json')
 $originalInner=[IO.File]::ReadAllBytes($innerFile)
 $inner.frames[2].resource='LaptopCinematic/HandsRight/Hand_0048'
 $inner|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $innerFile -Encoding UTF8
 foreach($e in $meta.files){if($e.relative -ceq ($pre+'right_hand_manifest.json')){$e.sha256=FileSha $innerFile}}
 Save-Meta
 Invoke-Test @('-DryRun') $false
 [IO.File]::WriteAllBytes($innerFile,$originalInner)
 foreach($e in $meta.files){if($e.relative -ceq ($pre+'right_hand_manifest.json')){$e.sha256=FileSha $innerFile}}
 Save-Meta
 Invoke-Test @('-TestInterruptAt','35') $false
 Check ((Get-Content -LiteralPath $prior -Raw) -ceq 'USER_OLD') 'interruption damaged user file'
 Check (-not (Test-Path -LiteralPath (D ($pre+'HandsRight/Hand_0000.png')))) 'interruption left new file'
 Invoke-Test @() $true
 foreach($e in $entries){Check ((FileSha (D $e.relative)) -ceq $e.sha256) 'installed hash mismatch'}
 $backroot=Join-Path $unity 'ROKAS_LaptopCinematic_Backups'
 $id=(Get-Content -LiteralPath (Join-Path $backroot 'last_install.txt') -Raw).Trim()
 $saved=Join-Path $backroot ($id+'/previous/'+$pre+'HandsRight/Hand_0031.png')
 Check ((Get-Content -LiteralPath $saved -Raw) -ceq 'USER_OLD') 'backup missing original'
 # Idempotency: a second approved install must keep every current hash and
 # create its own backup without corrupting the older reversible revision.
 Invoke-Test @() $true
 $second=(Get-Content -LiteralPath (Join-Path $backroot 'last_install.txt') -Raw).Trim()
 Check ($second -cne $id) 'reinstall must have a separate backup run ID'
 foreach($e in $entries){Check ((FileSha (D $e.relative)) -ceq $e.sha256) 'idempotent reinstall changed bytes'}
 Invoke-Test @('-Rollback','-RunId',$second) $true
 foreach($e in $entries){Check ((FileSha (D $e.relative)) -ceq $e.sha256) 'rollback of second install damaged first install'}
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
 # Reject a wrong target even when it is a writable directory.
 $wrong=Join-Path $root 'wrong_project'
 New-Item -ItemType Directory -Path $wrong -Force | Out-Null
 & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $pack 'INSTALL_SAFE.ps1') -Target $wrong -DryRun *> (Join-Path $root 'wrong.log')
 Check ($LASTEXITCODE -ne 0) 'wrong Unity root was accepted'
 # Reject directory junction pointing outside the isolated Unity tree.
 $hands=D ($pre+'HandsRight')
 Remove-Item -LiteralPath $hands -Recurse -Force
 $outside=Join-Path $root 'outside'
 New-Item -ItemType Directory -Path $outside | Out-Null
 New-Item -ItemType Junction -Path $hands -Target $outside | Out-Null
 Invoke-Test @('-DryRun') $false
 Check (@(Get-ChildItem -LiteralPath $outside -Force).Count -eq 0) 'junction escaped Unity'
 Remove-Item -LiteralPath $hands -Force
 Check ((Get-Content -LiteralPath $outScope -Raw) -ceq 'NEVER_TOUCH') 'unexpected file write outside temp Unity'
 Write-Host 'ROKAS_WINDOWS_INSTALLER_SAFETY_PASS_V10: PS5.1, SHA, 49-frame JSON, traversal, absolute, UNC, case, idempotency, backup, rollback, interruption, junction, no escape'
} finally {
 Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}

# Reset the last expected negative child-process exit code after all assertions pass.
exit 0
