# ==============================================================================
#  BUILD SCRIPT: Windows UWP (x86) - 10240 SDK
# ==============================================================================

$ErrorActionPreference = "Stop"

# --- CONFIGURE BUILD ENVIRONMENT ---
$SdkVer = "10.0.10240.0"
$SdkBase = "C:\Program Files (x86)\Windows Kits\10\Lib\$SdkVer"
$HybridDir = Join-Path $PWD "libs_x86_temp"
$BuildDir = Join-Path $PWD "target_x86"

if (-not (Test-Path $SdkBase)) {
    Write-Error "Error: Windows SDK $SdkVer not found. Please install it via VS Installer."
    exit 1
}

$HybridDir = Join-Path $PWD "libs_x86_temp"

try {
    # --- SETUP IMPOSTER LIBS ---
    Write-Host "Setting up temporary build libraries..." -ForegroundColor Cyan
    if (-not (Test-Path $HybridDir)) { New-Item -Path $HybridDir -ItemType Directory | Out-Null }

    $TemplateLib = Join-Path $SdkBase "um\x86\WindowsApp.lib"
    if (-not (Test-Path $TemplateLib)) { $TemplateLib = Join-Path $SdkBase "um\x86\mincore.lib" }

    $BlockedLibs = @(
        "user32.lib", "gdi32.lib", "shell32.lib", "opengl32.lib"
    )

    foreach ($Name in $BlockedLibs) {
        Copy-Item -Path $TemplateLib -Destination (Join-Path $HybridDir $Name) -Force
    }

    $SystemLibs = @("kernel32.lib", "ws2_32.lib", "advapi32.lib", "bcrypt.lib")
    foreach ($Name in $SystemLibs) {
        $Path = Join-Path $HybridDir $Name
        if (Test-Path $Path) { Remove-Item $Path -Force }
    }

    $env:LIB = "$HybridDir;$SdkBase\um\x86;$SdkBase\ucrt\x86"

    Write-Host "Building Pumpkin (x86)..." -ForegroundColor Green
    
    cargo +nightly build -Z "build-std=std,panic_abort" `
        --target i686-uwp-windows-msvc `
        --target-dir $BuildDir `
        --release `
        --no-default-features

    $DllPath = Join-Path $BuildDir "i686-uwp-windows-msvc\release\pumpkin_uwp.dll"
    if (Test-Path $DllPath) {
        Write-Host "Build succeeded: $DllPath" -ForegroundColor Yellow
    }
}
catch {
    Write-Error "Build Failed: $_"
}
finally {
    if (Test-Path $HybridDir) {
        Write-Host "Cleaning up temporary libraries..." -ForegroundColor Gray
        Remove-Item -Path $HybridDir -Recurse -Force
    }
}