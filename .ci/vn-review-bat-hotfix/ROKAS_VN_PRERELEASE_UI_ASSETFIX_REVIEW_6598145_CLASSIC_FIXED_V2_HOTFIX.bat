@echo off
chcp 65001 >nul
setlocal EnableExtensions
title ROKAS VN PRE-RELEASE UI ASSETFIX REVIEW - CLASSIC_FIXED V2 HOTFIX

set "ROKAS_SELF=%~f0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference='Stop';$p=$env:ROKAS_SELF;$t=[IO.File]::ReadAllText($p);$m='###ROKAS_POWERSHELL_PAYLOAD###';$i=$t.LastIndexOf($m);if($i -lt 0){Write-Error 'Payload marker not found';exit 90};try{Invoke-Expression $t.Substring($i+$m.Length);exit 0}catch{Write-Host '';Write-Host 'VN PRE-RELEASE REVIEW PREPARATION STOPPED';Write-Host $_.Exception.Message;Write-Host 'The source repository and prior review directories were not reset, cleaned, switched or deleted.';exit 1}"
set "RC=%ERRORLEVEL%"
echo.
if "%RC%"=="0" (echo VN REVIEW READY - MANUAL CHECKLIST OPEN) else (echo VN REVIEW STOPPED - ERROR %RC%)
echo This window will remain open. Press any key after reading the result.
if not "%ROKAS_HOTFIX_PROOF%"=="1" pause >nul
exit /b %RC%

###ROKAS_POWERSHELL_PAYLOAD###
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Source = 'D:\Rokas\Rokas'
$SearchRoot = 'D:\Rokas'
$Target = 'D:\Rokas\Rokas-VN-PRERELEASE-UI-ASSETFIX-Review-6598145-FIXED'
$Remote = 'https://github.com/timurkamurka11/Rokas.git'
$Branch = 'codex/vn-prerelease-plaque-ui-revision-clean'
$Commit = '65981452021b664829bbbbcb93f63d3dc0d0910a'
$Tree = 'd01942e124bc65591363926fe94d022f9bf45f14'
$CiRun = '35920995771'
$ProjectId = '3cc2bc9ec7974ea7a5f4d074683bed61'
$UnityVersion = '6000.3.19f1'
$ProjectFileName = 'ROKAS_VN_SCENE_COMPOSER_PROJECT.json'
$ProjectRelative = 'Library\ROKAS\VnSceneComposer\projects\' + $ProjectId
$GuidPattern = '(?i)"(?:assetGuid|[A-Za-z0-9_]*FontAssetGuid)"\s*:\s*"([0-9a-f]{32})"'
$MetaPattern = '(?m)^guid:\s*([0-9a-fA-F]{32})\s*$'
$UserRoots = @(
    'Assets\Rokas\Scripts\Editor\VnUiWorkshop\OnboardedAssets',
    'Assets\Rokas\Art\UI\Fonts\Imported',
    'Assets\Rokas\Resources\Fonts\TMP'
)
$GuidIndex = @{}
$SourceGuidIndex = @{}
$Manifest = New-Object 'System.Collections.Generic.List[object]'
$MigratedPaths = @{}

function Say([string]$Message = '') { Write-Host $Message }
function Stop-With([string]$Message) { throw $Message }
function Invoke-GitChecked([string[]]$Arguments) {
    & $script:GitExecutable @Arguments
    if ($LASTEXITCODE -ne 0) { Stop-With ('git failed: git ' + ($Arguments -join ' ')) }
}
function Get-GitOutput([string[]]$Arguments) {
    $value = (& $script:GitExecutable @Arguments 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) { Stop-With ('git failed: git ' + ($Arguments -join ' ')) }
    return $value
}
function Get-CommitTree([string]$Repo, [string]$CommitSha) {
    $raw = Get-GitOutput -Arguments @('-C',$Repo,'cat-file','-p',$CommitSha)
    $first = ($raw -split "`r?`n")[0]
    if ($first -notmatch '^tree\s+([0-9a-f]{40})$') {
        Stop-With ('Cannot read commit tree from git cat-file: ' + $CommitSha)
    }
    return $Matches[1]
}
function Hash-File([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Read-Json([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { Stop-With ('Missing JSON: ' + $Path) }
    return (Get-Content -Raw -LiteralPath $Path -Encoding UTF8 | ConvertFrom-Json)
}
function Within([string]$Root, [string]$Relative) {
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $full = [IO.Path]::GetFullPath((Join-Path $rootFull $Relative))
    if (-not $full.StartsWith(($rootFull + '\'), [StringComparison]::OrdinalIgnoreCase)) {
        Stop-With ('Path escapes review/source root: ' + $Relative)
    }
    return $full
}
function Copy-Safe([string]$From, [string]$To) {
    if (-not (Test-Path -LiteralPath $From -PathType Leaf)) { Stop-With ('Missing migration source: ' + $From) }
    if (Test-Path -LiteralPath $To -PathType Leaf) {
        if ((Hash-File $From) -ne (Hash-File $To)) { Stop-With ('Migration conflict, refusing overwrite: ' + $To) }
        return
    }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($To)) | Out-Null
    [IO.File]::Copy($From, $To, $false)
}
function Copy-Tree([string]$From, [string]$To) {
    if (-not (Test-Path -LiteralPath $From -PathType Container)) { return }
    $prefix = [IO.Path]::GetFullPath($From).TrimEnd('\', '/') + '\'
    foreach ($file in Get-ChildItem -LiteralPath $From -File -Recurse) {
        $relative = $file.FullName.Substring($prefix.Length)
        Copy-Safe $file.FullName (Within $To $relative)
    }
}
function Meta-Guid([string]$Meta) {
    $match = [regex]::Match((Get-Content -Raw -LiteralPath $Meta), $MetaPattern)
    if (-not $match.Success) { Stop-With ('Missing GUID in .meta: ' + $Meta) }
    return $match.Groups[1].Value.ToLowerInvariant()
}
function Add-Asset([string]$AssetPath, [string]$From) {
    $normalized = $AssetPath.Replace('\', '/')
    if (-not $normalized.StartsWith('Assets/', [StringComparison]::OrdinalIgnoreCase)) {
        Stop-With ('Asset path is not in Assets/: ' + $AssetPath)
    }
    $destination = Within $Target $normalized
    Copy-Safe $From $destination
    $sourceMeta = $From + '.meta'
    if (-not (Test-Path -LiteralPath $sourceMeta -PathType Leaf)) {
        Stop-With ('Referenced asset has no .meta: ' + $From)
    }
    Copy-Safe $sourceMeta ($destination + '.meta')
    $guid = Meta-Guid ($destination + '.meta')
    if ($GuidIndex.ContainsKey($guid) -and $GuidIndex[$guid] -ne $normalized) {
        Stop-With ('Duplicate GUID in review assets: ' + $guid)
    }
    $GuidIndex[$guid] = $normalized
    if (-not $MigratedPaths.ContainsKey($normalized)) {
        $MigratedPaths[$normalized] = $true
        $Manifest.Add([pscustomobject]@{
            assetPath = $normalized; guid = $guid; sourcePath = $From
            sha256 = Hash-File $destination; hasMeta = $true
        })
    }
}
function Index-Assets([string]$Root, [hashtable]$Index, [bool]$FailOnDuplicates) {
    $assets = Join-Path $Root 'Assets'
    if (-not (Test-Path -LiteralPath $assets -PathType Container)) { return }
    $prefix = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/') + '\'
    foreach ($meta in Get-ChildItem -LiteralPath $assets -Filter '*.meta' -Recurse -File) {
        $asset = $meta.FullName.Substring(0, $meta.FullName.Length - 5)
        if (-not (Test-Path -LiteralPath $asset -PathType Leaf)) { continue }
        $m = [regex]::Match((Get-Content -Raw -LiteralPath $meta.FullName), $MetaPattern)
        if (-not $m.Success) { continue }
        $guid = $m.Groups[1].Value.ToLowerInvariant()
        $relative = $asset.Substring($prefix.Length).Replace('\', '/')
        if ($Index.ContainsKey($guid)) {
            if ($FailOnDuplicates -and $Index[$guid] -ne $relative) {
                Stop-With ('Duplicate GUID in review Assets: ' + $guid + ' ' + $relative)
            }
        } else { $Index[$guid] = $relative }
    }
}
function Find-Unity {
    $candidates = New-Object 'System.Collections.Generic.List[string]'
    $hubSecondary = Join-Path $env:APPDATA 'UnityHub\secondaryInstallPath.json'
    if (Test-Path -LiteralPath $hubSecondary -PathType Leaf) {
        try {
            $root = Get-Content -Raw -LiteralPath $hubSecondary | ConvertFrom-Json
            if ($root) { $candidates.Add((Join-Path ([string]$root) ($UnityVersion + '\Editor\Unity.exe'))) }
        } catch {}
    }
    foreach ($drive in Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue) {
        foreach ($relative in @(
            "Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe",
            "Program Files\Unity Hub\Editor\$UnityVersion\Editor\Unity.exe",
            "Unity\Hub\Editor\$UnityVersion\Editor\Unity.exe",
            "UnityEditors\$UnityVersion\Editor\Unity.exe",
            "Unity\Editors\$UnityVersion\Editor\Unity.exe"
        )) { $candidates.Add((Join-Path $drive.Root $relative)) }
    }
    foreach ($root in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
                       'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
                       'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*')) {
        try {
            Get-ItemProperty $root -ErrorAction SilentlyContinue | ForEach-Object {
                $value = [string]$_.DisplayName + ' ' + [string]$_.InstallLocation
                if ($value -match [regex]::Escape($UnityVersion) -and $_.InstallLocation) {
                    $candidates.Add((Join-Path ([string]$_.InstallLocation) 'Editor\Unity.exe'))
                }
            }
        } catch {}
    }
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    return $null
}

Say 'ROKAS VN PRE-RELEASE UI / ASSETFIX - CLASSIC_FIXED V2'
Say 'PREP_START'
Say ('HEAD=' + $Commit)
Say ('TREE=' + $Tree)
Say ('CI_RUN=' + $CiRun)
if (-not (Test-Path -LiteralPath $Source -PathType Container)) { Stop-With ('Missing fixed source: ' + $Source) }
if (-not (Test-Path -LiteralPath (Join-Path $Source '.git'))) { Stop-With ('Source is not a Git checkout: ' + $Source) }
$gitCommand = Get-Command -Name git.exe -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $gitCommand) { Stop-With 'git.exe was not found on PATH.' }
$script:GitExecutable = $gitCommand.Source
if (-not (Test-Path -LiteralPath $script:GitExecutable -PathType Leaf)) {
    Stop-With ('Resolved git.exe path does not exist: ' + $script:GitExecutable)
}
$sourceStatus = Get-GitOutput -Arguments @('-C', $Source, 'status', '--porcelain=v1', '--untracked-files=all')
Say ('SOURCE_STATUS=' + $(if ($sourceStatus) { 'PRESERVED_WITH_LOCAL_CHANGES' } else { 'CLEAN' }))

if (Test-Path -LiteralPath $Target) {
    if (Test-Path -LiteralPath (Join-Path $Target '.git')) {
        $dirty = Get-GitOutput -Arguments @('-C', $Target, 'status', '--porcelain=v1', '--untracked-files=all')
        if ($dirty) { Stop-With ('DIRTY_REVIEW=STOP; existing target is unchanged: ' + $Target) }
    }
    Stop-With ('Review directory already exists; nothing was overwritten: ' + $Target)
}
$env:GIT_TERMINAL_PROMPT = '0'
Say '[1/7] Creating independent review clone and fetching the exact CI commit...'
Invoke-GitChecked -Arguments @('-c','gc.auto=0','-c','maintenance.auto=false','clone','--no-hardlinks','--no-checkout',$Source,$Target)
Invoke-GitChecked -Arguments @('-C',$Target,'remote','set-url','origin',$Remote)
Invoke-GitChecked -Arguments @('-c','gc.auto=0','-c','maintenance.auto=false','-C',$Target,'fetch','--no-tags','origin',$Branch)
$fetched = Get-GitOutput -Arguments @('-C',$Target,'rev-parse','FETCH_HEAD')
if ($fetched -ne $Commit) { Stop-With ('Remote branch changed since CI: expected ' + $Commit + ', fetched ' + $fetched) }
Say 'FETCH_PASS'
Invoke-GitChecked -Arguments @('-C',$Target,'checkout','--detach',$Commit)
Say 'CHECKOUT_PASS'
if ((Get-GitOutput -Arguments @('-C',$Target,'rev-parse','HEAD')) -ne $Commit) {
    Stop-With 'Review HEAD mismatch.'
}
Say 'HEAD_PASS'
if ((Get-CommitTree $Target $Commit) -ne $Tree) {
    Stop-With 'Review TREE mismatch.'
}
Say 'TREE_PASS'
if (Get-GitOutput -Arguments @('-C',$Target,'status','--porcelain=v1','--untracked-files=all')) {
    Stop-With 'Fresh review clone is unexpectedly dirty.'
}
foreach ($required in @('Assets','Packages\manifest.json','ProjectSettings\ProjectVersion.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $Target $required))) { Stop-With ('Incomplete Unity project: ' + $required) }
}
if ((Get-Content -Raw -LiteralPath (Join-Path $Target 'ProjectSettings\ProjectVersion.txt')) -notmatch
    ('m_EditorVersion:\s*' + [regex]::Escape($UnityVersion))) {
    Stop-With ('Expected Unity ' + $UnityVersion + ' in ProjectVersion.txt')
}

Say '[2/7] Selecting the latest saved project by identity and timestamp...'
Say 'MIGRATION_START'
$candidates = New-Object 'System.Collections.Generic.List[object]'
foreach ($root in @($SearchRoot) + @(Get-ChildItem -LiteralPath $SearchRoot -Directory | ForEach-Object FullName)) {
    if ($root -eq $Target) { continue }
    $file = Join-Path $root ($ProjectRelative + '\' + $ProjectFileName)
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { continue }
    try {
        $data = Read-Json $file
        if ([string]$data.projectId -eq $ProjectId) {
            $candidates.Add([pscustomobject]@{ Root=$root; File=$file; Modified=(Get-Item -LiteralPath $file).LastWriteTimeUtc })
        }
    } catch {}
}
if ($candidates.Count -eq 0) { Stop-With ('No saved project with ID ' + $ProjectId + ' found under ' + $SearchRoot) }
$selected = $candidates | Sort-Object -Property Modified,Root -Descending | Select-Object -First 1
$ProjectSource = [string]$selected.Root
$ProjectSourceFile = [string]$selected.File
$sourceJson = Get-Content -Raw -LiteralPath $ProjectSourceFile -Encoding UTF8
$project = $sourceJson | ConvertFrom-Json
if ([string]$project.projectId -ne $ProjectId) { Stop-With 'PROJECT_DATA=FAIL: selected identity mismatch.' }
$scenes = @($project.scenes)
$beatCount = 0
foreach ($scene in $scenes) { $beatCount += @($scene.dialogueBeats).Count }
if ($scenes.Count -ne 8 -or $beatCount -ne 33) {
    Stop-With ('PROJECT_DATA=FAIL: latest saved copy has ' + $scenes.Count + ' scenes and ' + $beatCount + ' beats; expected 8 / 33. Source=' + $ProjectSource)
}
Say ('PROJECT_SOURCE=' + $ProjectSource)
Say 'SOURCE_PROJECT_ID_AND_COUNTS=VALID (8 scenes / 33 beats)'

$ProjectDir = Join-Path $Target $ProjectRelative
Copy-Tree ([IO.Path]::GetDirectoryName($ProjectSourceFile)) $ProjectDir
$ProjectFile = Join-Path $ProjectDir $ProjectFileName
if ((Hash-File $ProjectFile) -ne (Hash-File $ProjectSourceFile)) { Stop-With 'Saved project bytes changed while copying.' }

Say '[3/7] Copying user assets and recovering referenced GUIDs with their .meta files...'
foreach ($relative in $UserRoots) {
    $root = Join-Path $ProjectSource $relative
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
    $prefix = [IO.Path]::GetFullPath($ProjectSource).TrimEnd('\', '/') + '\'
    foreach ($file in Get-ChildItem -LiteralPath $root -File -Recurse) {
        if ($file.Extension -eq '.meta') { continue }
        Add-Asset ($file.FullName.Substring($prefix.Length)) $file.FullName
    }
}
Index-Assets $Target $GuidIndex $true
$requiredGuids = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach ($match in [regex]::Matches($sourceJson, $GuidPattern)) { [void]$requiredGuids.Add($match.Groups[1].Value) }
foreach ($scene in $scenes) {
    if ([int]$scene.media.kind -eq 1) {
        $reference = [string]$scene.media.reference
        if ($reference -notmatch '^[a-fA-F0-9]{32}$') { Stop-With ('Invalid scene background GUID: ' + $reference) }
        [void]$requiredGuids.Add($reference)
    }
}

$assetRoots = New-Object 'System.Collections.Generic.List[string]'
foreach ($root in @($ProjectSource, $Source, $SearchRoot) + @(Get-ChildItem -LiteralPath $SearchRoot -Directory | ForEach-Object FullName)) {
    if ($root -ne $Target -and (Test-Path -LiteralPath (Join-Path $root 'Assets') -PathType Container) -and
        -not $assetRoots.Contains([string]$root)) { $assetRoots.Add([string]$root) }
}
$durable = Join-Path $SearchRoot ('VNProjects\' + $ProjectId)
if (Test-Path -LiteralPath $durable -PathType Container) {
    foreach ($assetsDir in Get-ChildItem -LiteralPath $durable -Directory -Filter 'Assets' -Recurse) {
        $root = $assetsDir.Parent.FullName
        if (-not $assetRoots.Contains($root)) { $assetRoots.Add($root) }
    }
}
foreach ($root in $assetRoots) {
    $map = @{}
    Index-Assets $root $map $false
    $SourceGuidIndex[$root] = $map
}
foreach ($guid in $requiredGuids) {
    if ($GuidIndex.ContainsKey($guid)) { continue }
    $found = $false
    foreach ($root in $assetRoots) {
        if (-not $SourceGuidIndex[$root].ContainsKey($guid)) { continue }
        $relative = $SourceGuidIndex[$root][$guid]
        Add-Asset $relative (Within $root $relative)
        $found = $true
        break
    }
    if (-not $found) { Stop-With ('REFERENCED_ASSET_CLOSURE=FAIL: unresolved GUID ' + $guid) }
}

foreach ($guid in $requiredGuids) {
    if (-not $GuidIndex.ContainsKey($guid)) { Stop-With ('REFERENCED_ASSET_CLOSURE=FAIL: ' + $guid) }
    $file = Within $Target $GuidIndex[$guid]
    if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or
        (Meta-Guid ($file + '.meta')) -ne $guid.ToLowerInvariant()) {
        Stop-With ('META_GUID_VALIDATION=FAIL: ' + $guid)
    }
}
foreach ($entry in $Manifest) {
    $file = Within $Target $entry.assetPath
    if ((Hash-File $file) -ne $entry.sha256 -or (Meta-Guid ($file + '.meta')) -ne $entry.guid) {
        Stop-With ('META_GUID_VALIDATION=FAIL: ' + $entry.assetPath)
    }
}
Say ('GUID_REFERENCES_RESOLVED=' + $requiredGuids.Count)
Say ('MIGRATED_META_PAIRS_VALIDATED=' + $Manifest.Count)

Say '[4/7] Resolving authored backgrounds, video, audio, characters and fonts...'
$BindingFile = Join-Path $ProjectDir 'local-media-bindings.json'
$bindings = if (Test-Path -LiteralPath $BindingFile -PathType Leaf) { Read-Json $BindingFile } else { [pscustomobject]@{ bindings=@() } }
$imageCount=0; $videoCount=0; $audioCount=0; $characterCount=0; $fontCount=0
$catalogFile = Join-Path $Target 'Assets\Rokas\Scripts\Editor\VnUiWorkshop\OnboardedAssets\ROKAS_VN_SCENE_COMPOSER_ASSET_CATALOG.json'
$catalog = if (Test-Path -LiteralPath $catalogFile -PathType Leaf) { Read-Json $catalogFile } else { [pscustomobject]@{ entries=@() } }
$states = @{}
$stateText = Get-Content -Raw -LiteralPath (Join-Path $Target 'Assets\Rokas\Scripts\Presentation\VnCharacterVisualStates.cs')
foreach ($m in [regex]::Matches($stateText, 'State\("([a-z0-9_]+)",\s*"([^\"]+)"')) {
    $states[$m.Groups[1].Value] = $m.Groups[2].Value
}
foreach ($entry in @($catalog.entries)) {
    if ([int]$entry.purpose -eq 0 -and [string]$entry.stateId) {
        $states[[string]$entry.stateId] = [string]$entry.character
        if (-not $GuidIndex.ContainsKey(([string]$entry.assetGuid).ToLowerInvariant()) -or
            $GuidIndex[([string]$entry.assetGuid).ToLowerInvariant()] -ne ([string]$entry.assetPath).Replace('\','/')) {
            Stop-With ('CHARACTER_REFERENCES=FAIL: catalog asset ' + [string]$entry.stateId)
        }
    }
}
for ($i=0; $i -lt $scenes.Count; $i++) {
    $scene = $scenes[$i]; $media = $scene.media
    if ($null -eq $media) { Stop-With ('BACKGROUND_REFERENCES=FAIL: missing media in Scene ' + ($i+1)) }
    $kind = [int]$media.kind
    if ($kind -lt 0 -or $kind -gt 4) { Stop-With ('BACKGROUND_REFERENCES=FAIL: unknown media kind in Scene ' + ($i+1)) }
    if ($kind -eq 1) {
        if (-not $GuidIndex.ContainsKey(([string]$media.reference).ToLowerInvariant())) {
            Stop-With ('BACKGROUND_REFERENCES=FAIL: missing image in Scene ' + ($i+1))
        }
        $imageCount++
    }
    if ($kind -ge 2) {
        $binding = $bindings.bindings | Where-Object {
            [string]$_.sceneId -eq [string]$scene.sceneId -and
            [string]$_.kind -eq @('None','ExistingRokasAsset','ExternalImage','ExternalVideo','ExternalGif')[$kind] -and
            (-not [string]$media.contentHash -or -not [string]$_.contentHash -or
             [string]$_.contentHash -eq [string]$media.contentHash)
        } | Select-Object -First 1
        $candidate = if ($binding) { [string]$binding.absolutePath } else { [string]$media.reference }
        if (-not [IO.Path]::IsPathRooted($candidate) -or -not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            $hash = [string]$media.contentHash
            $name = [string]$media.displayName
            if ($hash -match '^[a-fA-F0-9]{64}$' -and $name) {
                $candidate = Join-Path $durable ('ExternalMedia\' + $hash + '\' + [IO.Path]::GetFileName($name))
            }
        }
        if (-not [IO.Path]::IsPathRooted($candidate) -or -not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            Stop-With ('BACKGROUND_REFERENCES=FAIL: external media unavailable in Scene ' + ($i+1))
        }
        $hash = Hash-File $candidate
        if ([string]$media.contentHash -match '^[a-fA-F0-9]{64}$' -and $hash -ne ([string]$media.contentHash).ToLowerInvariant()) {
            Stop-With ('BACKGROUND_REFERENCES=FAIL: external hash mismatch in Scene ' + ($i+1))
        }
        $safeName = [IO.Path]::GetFileName($candidate)
        $local = Join-Path $ProjectDir ('ExternalMedia\' + $hash + '\' + $safeName)
        Copy-Safe $candidate $local
        if (-not $binding) {
            $binding = [pscustomobject]@{sceneId=[string]$scene.sceneId; kind=@('None','ExistingRokasAsset','ExternalImage','ExternalVideo','ExternalGif')[$kind]; contentHash=[string]$media.contentHash; absolutePath=$local}
            $bindings.bindings = @($bindings.bindings) + @($binding)
        } else { $binding.absolutePath = $local }
        if ($kind -eq 2) { $imageCount++ } else { $videoCount++ }
    }
    if ($i -ge 5 -and $kind -eq 0) {
        Stop-With ('SCENES_6_8_MEDIA=FAIL: no background/media in Scene ' + ($i+1))
    }
    if ([string]$scene.music.assetGuid) {
        if (-not $GuidIndex.ContainsKey(([string]$scene.music.assetGuid).ToLowerInvariant())) { Stop-With ('AUDIO_REFERENCES=FAIL: music in Scene ' + ($i+1)) }
        $audioCount++
    }
    foreach ($cue in @($scene.additionalAudioCues)) {
        if ([string]$cue.assetGuid) {
            if (-not $GuidIndex.ContainsKey(([string]$cue.assetGuid).ToLowerInvariant())) { Stop-With ('AUDIO_REFERENCES=FAIL: cue in Scene ' + ($i+1)) }
            $audioCount++
        }
    }
    foreach ($character in @($scene.characters)) {
        $stateId = [string]$character.stateId
        if (-not $states.ContainsKey($stateId) -or
            ([string]$character.characterId -and [string]$character.characterId -ne $states[$stateId])) {
            Stop-With ('CHARACTER_REFERENCES=FAIL: character state in Scene ' + ($i+1) + ': ' + $stateId)
        }
        $characterCount++
    }
    foreach ($beat in @($scene.dialogueBeats)) {
        foreach ($row in @($beat.characterStaging)) {
            $stateId = [string]$row.stateId
            if ([bool]$row.hasStateOverride -and (-not $states.ContainsKey($stateId) -or $states[$stateId] -ne [string]$row.characterId)) {
                Stop-With ('CHARACTER_REFERENCES=FAIL: staged state in Scene ' + ($i+1) + ': ' + $stateId)
            }
        }
    }
}
if ($videoCount -eq 0) { Stop-With 'VIDEO_REFERENCES=FAIL: no video/GIF media was found to review.' }
$bindings | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $BindingFile -Encoding UTF8
foreach ($match in [regex]::Matches($sourceJson, '(?i)"[A-Za-z0-9_]*FontAssetGuid"\s*:\s*"([0-9a-f]{32})"')) {
    $guid = $match.Groups[1].Value.ToLowerInvariant()
    if (-not $GuidIndex.ContainsKey($guid)) { Stop-With ('FONT_REFERENCES=FAIL: ' + $guid) }
    $fontCount++
}
if ($sourceJson -match '805df305f1f04a14684c103724481b00') {
    Stop-With 'FONT_REFERENCES=FAIL: known broken fallback GUID still appears in the saved project.'
}
$corbel = @(Get-ChildItem -LiteralPath (Join-Path $Target 'Assets') -File -Recurse |
    Where-Object { $_.Name -match '(?i)corbel.*bold|corbelb' })
if ($corbel.Count -eq 0) { Stop-With 'FONT_REFERENCES=FAIL: Corbel Bold source is missing in isolated review.' }
Say ('REFERENCES_VALIDATED images=' + $imageCount + ' video_or_gif=' + $videoCount +
    ' audio=' + $audioCount + ' characters=' + $characterCount +
    ' fonts=' + $fontCount + ' CorbelBold=' + $corbel.Count)

Say '[5/7] Revalidating copied project and writing evidence before Unity...'
$again = Read-Json $ProjectFile
$againBeats = 0
foreach ($scene in @($again.scenes)) { $againBeats += @($scene.dialogueBeats).Count }
if ([string]$again.projectId -ne $ProjectId -or @($again.scenes).Count -ne 8 -or $againBeats -ne 33) {
    Stop-With 'PROJECT_DATA=FAIL: copied project identity/count changed.'
}
$manifestPath = Join-Path $ProjectDir 'prerelease-review-validation.json'
[pscustomobject]@{projectId=$ProjectId;sourceProjectRoot=$ProjectSource;destinationProjectRoot=$Target;
    head=$Commit;tree=$Tree;ciRun=$CiRun;assets=$Manifest.ToArray()} |
    ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
foreach ($entry in $Manifest) {
    $file = Within $Target $entry.assetPath
    if ((Hash-File $file) -ne $entry.sha256 -or (Meta-Guid ($file + '.meta')) -ne $entry.guid) {
        Stop-With ('META_GUID_VALIDATION=FAIL after manifest: ' + $entry.assetPath)
    }
}
if ((Get-GitOutput -Arguments @('-C',$Target,'rev-parse','HEAD')) -ne $Commit -or
    (Get-CommitTree $Target $Commit) -ne $Tree) {
    Stop-With 'Review HEAD/TREE changed during project migration.'
}
if ((Get-GitOutput -Arguments @('-C',$Source,'status','--porcelain=v1','--untracked-files=all')) -ne $sourceStatus) {
    Stop-With 'Source status changed while preparing review; inspect the source before relying on the copy.'
}
Say 'PROJECT_DATA=PASS'
Say 'SCENE_COUNT=8'
Say 'BEAT_COUNT=33'
Say 'REFERENCED_ASSET_CLOSURE=PASS'
Say 'META_GUID_VALIDATION=PASS'
Say 'BACKGROUND_REFERENCES=PASS'
Say 'VIDEO_REFERENCES=PASS'
Say 'AUDIO_REFERENCES=PASS'
Say 'CHARACTER_REFERENCES=PASS'
Say 'FONT_REFERENCES=PASS'
Say 'SCENES_6_8_MEDIA=PASS'

Say '[6/7] Locating Unity after all required checks have passed...'
$Unity = Find-Unity
if (-not $Unity) { Stop-With ('Unity ' + $UnityVersion + ' was not found in Unity Hub, common drives or registry.') }
Say ('UNITY=' + $Unity)

$Checklist = Join-Path $Target 'VN_PRERELEASE_UI_ASSETFIX_MANUAL_CHECKLIST.txt'
@(
    'ROKAS VN PRE-RELEASE PLAQUE / TRANSITION UI - MANUAL REVIEW'
    'CLASSIC_FIXED V2'
    ('HEAD: ' + $Commit)
    ('TREE: ' + $Tree)
    ('CI: https://github.com/timurkamurka11/Rokas/actions/runs/' + $CiRun)
    'Unity EditMode: 757/757 PASS across 28 fixtures; failed 0; skipped 0.'
    'Source / Production Guard: PASS. SC-J final certification was not run.'
    ('PROJECT SOURCE: ' + $ProjectSource)
    ('PROJECT ID: ' + $ProjectId)
    'Project data, 8 scenes / 33 beats, asset closure, media and meta/GUID checks all passed before Unity launch.'
    'Mark every item PASS or FAIL in the actual VN Scene Composer Preview/playback renderer:'
    '01 [ ] All 8 Scenes present'
    '02 [ ] All 33 Beats present'
    '03 [ ] Scenes 6-8 backgrounds/media visible'
    '04 [ ] No unexplained black Scene'
    '05 [ ] Canonical blue plaque visible'
    '06 [ ] No duplicate rectangular control strip'
    '07 [ ] Back absent'
    '08 [ ] Only circular Mute / Forward / Menu visible'
    '09 [ ] Mute clickable'
    '10 [ ] Forward clickable'
    '11 [ ] Menu clickable'
    '12 [ ] Circular button hover state visible'
    '13 [ ] Circular button pressed state visible'
    '14 [ ] While text is typing, triangle hidden (state A)'
    '15 [ ] At full text reveal, BRIGHT WHITE triangle visible (state B)'
    '16 [ ] Triangle visibly animated; inspect more than one frame'
    '17 [ ] At next Beat start, triangle gone (state C)'
    '18 [ ] Forward advances exactly once per click'
    '19 [ ] Scene transition has no incoming-frame flash'
    '20 [ ] Image -> video transition'
    '21 [ ] Video -> image transition'
    '22 [ ] Video -> video transition'
    '23 [ ] Save / Reopen keeps assets and font (including Corbel Bold)'
    '24 [ ] Play Scene'
    '25 [ ] Play All'
    '26 [ ] Play From Here'
    'Compare static Preview and playback/runtime for the same plaque, controls and triangle states.'
    'Capture actual VN Preview screenshots for typing (A), fully revealed (B), next Beat (C), and a recovered Scene 6-8 background.'
    'Keep the screenshots with this checklist; renderer evidence is open until inspected.'
    'MANUAL PASS is required before any final certification. SC-J remains blocked.'
) | Set-Content -LiteralPath $Checklist -Encoding UTF8

Say '[7/7] Opening review folder and exact Unity project...'
Start-Process -FilePath explorer.exe -ArgumentList @('/select,',('"' + $Checklist + '"')) | Out-Null
Start-Process -FilePath $Unity -ArgumentList @('-projectPath', ('"' + $Target + '"')) | Out-Null
Say ('REVIEW=' + $Target)
Say ('CHECKLIST=' + $Checklist)
Say 'PRE-RELEASE PLAQUE / TRANSITION UI = AUTOMATED GREEN / MANUAL OPEN'
