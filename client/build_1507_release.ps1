# ============================
#  Universal Pumpkin (1507)
#  Automated Multi-Arch Release Build
# ============================

$ErrorActionPreference = "Stop"

function Find-MSBuild1507 {
    Write-Host "Locating MSBuild 15.0 (VS 2017)..."

    $vs2017Root = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin"

    $candidates = @(
        "$vs2017Root\MSBuild.exe",
        "$vs2017Root\amd64\MSBuild.exe"
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            Write-Host "Using MSBuild 15.0: $path"
            return $path
        }
    }

    throw "MSBuild 15.0 not found. Install VS 2017 with UWP 1507 (10.0.10240.0) workload."
}

$msbuild = Find-MSBuild1507

Write-Host "`n=== Collecting Rust-built DLLs ==="

$serverRoot = "..\server"
$clientRoot = ".\Universal Pumpkin (1507)"

$dllMap = @{
    "ARM" = "$serverRoot\target_arm32\thumbv7a-uwp-windows-msvc\release\pumpkin_uwp.dll"
    "x86" = "$serverRoot\target_x86\i686-uwp-windows-msvc\release\pumpkin_uwp.dll"
    "x64" = "$serverRoot\target_x64\x86_64-uwp-windows-msvc\release\pumpkin_uwp.dll"
}

foreach ($arch in $dllMap.Keys) {
    $src = $dllMap[$arch]
    $dst = Join-Path $clientRoot "pumpkin_uwp.dll.$arch"

    if (Test-Path $src) {
        Copy-Item $src $dst -Force
        Write-Host "Copied $src → $dst"
    }
    else {
        Write-Warning "Missing DLL for $arch at $src"
    }
}

Write-Host "`n=== Starting UWP Release builds (1507) ==="

$projectFile = Join-Path $clientRoot "Universal Pumpkin (1507).csproj"
$dllTarget = Join-Path $clientRoot "pumpkin_uwp.dll"

foreach ($arch in $dllMap.Keys) {

    Write-Host "`n--- Building $arch (Release) ---"

    if (Test-Path $dllTarget) {
        Remove-Item $dllTarget -Force
    }

    $dllSource = Join-Path $clientRoot "pumpkin_uwp.dll.$arch"
    Copy-Item $dllSource $dllTarget -Force

    & $msbuild $projectFile `
        /p:Configuration=Release `
        /p:Platform=$arch `
        /t:Rebuild `
        /verbosity:minimal

    Write-Host "Release build complete for $arch"
}

Write-Host "`n=== Completed Release builds for Universal Pumpkin (1507) ==="