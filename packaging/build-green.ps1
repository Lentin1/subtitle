param(
    [string]$PythonPath = "python",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "v2"
)

$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$distRoot = Join-Path $repo "dist\RealtimeSubtitle"
$artifactsRoot = Join-Path $repo "artifacts"
$workerSrc = Join-Path $repo "src\RealtimeSubtitle.Worker"
$workerDist = Join-Path $artifactsRoot "worker-dist"
$pyiWork = Join-Path $artifactsRoot "pyinstaller-work"
$appProject = Join-Path $repo "src\RealtimeSubtitle.App\RealtimeSubtitle.App.csproj"
$greenZipPath = Join-Path $artifactsRoot "RealtimeSubtitle-$Version-green.zip"
$cudaZipPath = Join-Path $artifactsRoot "RealtimeSubtitle-$Version-cuda.zip"
$cudaPackageRoot = Join-Path $artifactsRoot "cuda-package"

New-Item -ItemType Directory -Force $artifactsRoot | Out-Null
Remove-Item -Recurse -Force $distRoot,$workerDist,$pyiWork,$cudaPackageRoot -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $distRoot,$workerDist,$pyiWork,$cudaPackageRoot | Out-Null

Invoke-Checked -Name "dotnet publish" -Command {
    dotnet publish $appProject `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -o $distRoot
}

Invoke-Checked -Name "PyInstaller" -Command {
    & $PythonPath -m PyInstaller `
        --noconfirm `
        --clean `
        --onedir `
        --name RealtimeSubtitle.Worker `
        --distpath $workerDist `
        --workpath $pyiWork `
        --specpath $artifactsRoot `
        --paths $workerSrc `
        --collect-all faster_whisper `
        --collect-all ctranslate2 `
        --collect-all tokenizers `
        --collect-all huggingface_hub `
        --collect-all nvidia `
        (Join-Path $workerSrc "main.py")
}

$packagedWorkerDir = Join-Path $distRoot "worker"
New-Item -ItemType Directory -Force $packagedWorkerDir | Out-Null
Copy-Item -Recurse -Force (Join-Path $workerDist "RealtimeSubtitle.Worker\*") $packagedWorkerDir

$nvidiaRoot = & $PythonPath -c "import os, site; print(next((os.path.join(p, 'nvidia') for p in site.getsitepackages() if os.path.isdir(os.path.join(p, 'nvidia'))), ''))"
if (Test-Path $nvidiaRoot) {
    $cudaWorkerDir = Join-Path $cudaPackageRoot "worker"
    New-Item -ItemType Directory -Force $cudaWorkerDir | Out-Null
    Copy-Item -Recurse -Force $nvidiaRoot (Join-Path $cudaWorkerDir "nvidia")
} else {
    Write-Warning "No Python nvidia package directory found; CUDA DLLs will not be copied."
}

$configDir = Join-Path $distRoot "config"
New-Item -ItemType Directory -Force $configDir | Out-Null
$config = Get-Content (Join-Path $repo "config\default_config.json") -Raw | ConvertFrom-Json
$config.worker.pythonPath = ""
$config.worker.scriptPath = ""
$config.worker.executablePath = "worker\RealtimeSubtitle.Worker.exe"
$config.asr.modelCacheDir = "models"
$config.translate.apiKey = ""
$config | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 (Join-Path $configDir "config.json")

New-Item -ItemType Directory -Force (Join-Path $distRoot "models") | Out-Null
New-Item -ItemType Directory -Force (Join-Path $distRoot "logs") | Out-Null
Copy-Item -Force (Join-Path $repo "README.md") $distRoot

Remove-Item -Force $greenZipPath,$cudaZipPath -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $distRoot "*") -DestinationPath $greenZipPath
if (Test-Path (Join-Path $cudaPackageRoot "worker\nvidia")) {
    Compress-Archive -Path (Join-Path $cudaPackageRoot "*") -DestinationPath $cudaZipPath
}

Write-Host "Green package ready: $greenZipPath"
if (Test-Path $cudaZipPath) {
    Write-Host "CUDA package ready: $cudaZipPath"
}
