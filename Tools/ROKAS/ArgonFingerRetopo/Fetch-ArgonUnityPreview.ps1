# ROKAS — authenticated one-click connected hand QA importer.
# Run from ROKAS_ARGON_UNITY_PREVIEW.bat in the EXISTING Unity project.
# No source asset is written to the public GitHub repository.
$ErrorActionPreference = 'Stop'
$repo = 'timurkamurka11/Rokas'
$workflow = 'argon-finger-local-root-bridge-qa.yml'
$branch = 'codex/bedsleep-argon-import-candidate'
$artifactName = 'ROKAS-Argon-Unity-PrivateHandModel'
$fbxName = 'ROKAS_Argon_LocalRootBridge_RIG_QA_ONLY.fbx'

function Invoke-Checked([string]$command, [string[]]$parameters) {
    & $command @parameters
    if ($LASTEXITCODE -ne 0) {
        throw "$command failed with exit code $LASTEXITCODE."
    }
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) is required. Install via winget install --id GitHub.cli -e, then gh auth login.'
}
$repoRoot = (& git -C $PSScriptRoot rev-parse --show-toplevel)
if ($LASTEXITCODE -ne 0 -or -not $repoRoot) {
    throw 'This script must be in the existing ROKAS Git Unity checkout.'
}
$repoRoot = ($repoRoot | Select-Object -First 1).Trim()
Invoke-Checked 'gh' @('auth', 'status')
$runsText = & gh run list -R $repo --workflow $workflow --branch $branch --limit 15 --json databaseId,status,conclusion,headSha,createdAt
if ($LASTEXITCODE -ne 0 -or -not $runsText) {
    throw 'Cannot list authenticated GitHub Actions runs.'
}
$runs = @($runsText | ConvertFrom-Json)
$good = $runs | Where-Object {
    $_.status -eq 'completed' -and $_.conclusion -eq 'success'
} | Select-Object -First 1
if (-not $good) { throw 'No successful candidate Unity QA run yet. Wait for GitHub Actions to finish.' }
$dir = Join-Path $env:TEMP ("ROKAS_ARGON_UNITY_QA_" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir -Force | Out-Null
try {
    Write-Host "Downloading authentic skinned Argon Unity candidate from run $($good.databaseId)"
    Invoke-Checked 'gh' @('run','download',[string]$good.databaseId,'-R',$repo,'-n',$artifactName,'-D',$dir)
    $source = Get-ChildItem -LiteralPath $dir -File -Recurse -Filter $fbxName | Select-Object -First 1
    if (-not $source -or $source.Length -lt 100000) {
        throw 'No valid 25-bone hand FBX in the successful run. Refusing fake/empty preview.'
    }
    $targetDir = Join-Path $repoRoot 'Assets/Rokas/BedSleep/ArgonIntegration/LocalRootBridgeQA'
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    $dest = Join-Path $targetDir $fbxName
    Copy-Item -LiteralPath $source.FullName -Destination $dest -Force
    Write-Host ''
    Write-Host 'UNITY PREVIEW INSTALLED:'
    Write-Host $dest
    Write-Host "CI source: $($good.headSha); run: $($good.databaseId)"
    Write-Host 'In Unity: ROKAS > Bed Sleep > Argon > Open latest connected hand in Unity'
    Write-Host 'Original Bed Sleep V10 remains unchanged.'
} finally {
    Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
}
