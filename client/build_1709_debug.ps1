# ============================
#  Universal Pumpkin (1709)
#  Automated Multi-Arch Debug Build
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

    throw "MSBuild 15.0 not found. Install VS 2017 with UWP 1709 (10.0.16299.0) workload."
}

$msbuild = Find-MSBuild1507

Write-Host "`n=== Collecting Rust-built DLLs ==="

$serverRoot = "..\server"
$clientRoot = ".\Universal Pumpkin"

$dllMap = @{
    "ARM"   = "$serverRoot\target_arm32\thumbv7a-uwp-windows-msvc\release\pumpkin_uwp.dll"
    "ARM64" = "$serverRoot\target_arm64\aarch64-uwp-windows-msvc\release\pumpkin_uwp.dll"
    "x86"   = "$serverRoot\target_x86\i686-uwp-windows-msvc\release\pumpkin_uwp.dll"
    "x64"   = "$serverRoot\target_x64\x86_64-uwp-windows-msvc\release\pumpkin_uwp.dll"
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

Write-Host "`n=== Starting UWP Debug builds (1709) ==="

$projectFile = Join-Path $clientRoot "Universal Pumpkin.csproj"
$dllTarget = Join-Path $clientRoot "pumpkin_uwp.dll"

foreach ($arch in $dllMap.Keys) {

    Write-Host "`n--- Building $arch ---"

    if (Test-Path $dllTarget) {
        Remove-Item $dllTarget -Force
    }

    $dllSource = Join-Path $clientRoot "pumpkin_uwp.dll.$arch"
    Copy-Item $dllSource $dllTarget -Force

    & $msbuild $projectFile `
        /p:Configuration=Debug `
        /p:Platform=$arch `
        /t:Rebuild `
        /verbosity:minimal

    Write-Host "Build complete for $arch"
}

Write-Host "`n=== Completed Debug builds for Universal Pumpkin (1709) ==="