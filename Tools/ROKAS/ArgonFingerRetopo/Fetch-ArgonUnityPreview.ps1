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
# The previous version queried /actions/runs/{id}/artifacts for EACH run.
# In the user's portable CLI this returned HTTP 404 even though the
# confirmed original 25-bone FBX artifact is present and unexpired.
# Download the known-good artifact by exact GitHub Actions run+name.
$verifiedRunId = '39117897539'
$verifiedArtifactId = '12133640655'
$dir = Join-Path $env:TEMP ("ROKAS_ARGON_UNITY_QA_" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $dir -Force | Out-Null
try {
    Write-Host "Downloading original 25-bone Unity-tested hand from confirmed Actions run $verifiedRunId"
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    & gh run download $verifiedRunId -R $repo -n $artifactName -D $dir
    $downloadStatus = $LASTEXITCODE
    $ErrorActionPreference = $previous
    if ($downloadStatus -ne 0) {
        Write-Host 'gh run download failed; trying authenticated artifact ZIP directly.' -ForegroundColor Yellow
        $tokenLines = & gh auth token -h github.com
        if ($LASTEXITCODE -ne 0 -or -not $tokenLines) {
            throw 'Unable to obtain GitHub CLI authentication for official artifact download.'
        }
        $token = ($tokenLines | Select-Object -First 1).Trim()
        $url = "https://api.github.com/repos/$repo/actions/artifacts/$verifiedArtifactId/zip"
        $zip = Join-Path $dir 'arg-hand.zip'
        $headers = @{ Authorization="Bearer $token"; Accept='application/vnd.github+json'; 'User-Agent'='ROKAS-Unity-QA' }
        Invoke-WebRequest -Uri $url -Headers $headers -OutFile $zip -UseBasicParsing -MaximumRedirection 10
        $token = $null
        Expand-Archive -LiteralPath $zip -DestinationPath $dir -Force
    }
    $source = Get-ChildItem -LiteralPath $dir -File -Recurse -Filter $fbxName | Select-Object -First 1
    if (-not $source -or $source.Length -lt 100000) {
        throw 'Authenticated QA archive did not include the real connected 25-bone FBX.'
    }
    $reader = [IO.File]::OpenRead($source.FullName)
    try {
        $buf = New-Object byte[] 24
        $count = $reader.Read($buf,0,$buf.Length)
        $header = [Text.Encoding]::ASCII.GetString($buf,0,$count)
    } finally { $reader.Dispose() }
    if (-not $header.StartsWith('Kaydara FBX Binary') -and -not $header.StartsWith('; FBX')) {
        throw 'Downloaded QA file is not a genuine FBX model.'
    }
    $targetDir = Join-Path $repoRoot 'Assets/Rokas/BedSleep/ArgonIntegration/LocalRootBridgeQA'
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    $dest = Join-Path $targetDir $fbxName
    Copy-Item -LiteralPath $source.FullName -Destination $dest -Force
    Write-Host ''
    Write-Host 'UNITY PREVIEW INSTALLED:'
    Write-Host $dest
    Write-Host "CI source run: $verifiedRunId"
    Write-Host 'In Unity: ROKAS > Bed Sleep > Argon > Open latest connected hand in Unity'
    Write-Host 'Original Bed Sleep V10 remains unchanged.'
} finally {
    Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
}
