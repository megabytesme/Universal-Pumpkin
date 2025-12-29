# ==============================================================================
#  BUILD SCRIPT: Windows UWP (ARM64) - 16299 SDK
# ==============================================================================

$ErrorActionPreference = "Stop"

# --- CONFIGURE BUILD ENVIRONMENT ---
# ARM64 requires a minimum SDK of 16299.
$SdkVer = "10.0.16299.0" 
$SdkBase = "C:\Program Files (x86)\Windows Kits\10\Lib\$SdkVer"

if (-not (Test-Path $SdkBase)) {
    Write-Error "Error: Windows SDK $SdkVer not found. Please install it via VS Installer."
    exit 1
}

$HybridDir = Join-Path $PWD "libs_arm64_temp"

try {
    # --- SETUP IMPOSTER LIBS ---
    Write-Host "Setting up temporary build libraries..." -ForegroundColor Cyan
    if (-not (Test-Path $HybridDir)) { New-Item -Path $HybridDir -ItemType Directory | Out-Null }

    $TemplateLib = Join-Path $SdkBase "um\arm64\WindowsApp.lib"
    if (-not (Test-Path $TemplateLib)) { $TemplateLib = Join-Path $SdkBase "um\arm64\mincore.lib" }

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

    $env:LIB = "$HybridDir;$SdkBase\um\arm64;$SdkBase\ucrt\arm64"

    # --- BUILD ---
    Write-Host "Building Pumpkin (aarch64-uwp-windows-msvc - Release)..." -ForegroundColor Green
    
    cargo +nightly build -Z "build-std=std,panic_abort" `
        --target aarch64-uwp-windows-msvc `
        --release `
        --no-default-features

    # --- REPORT DLL LOCATION ---
    $DllPath = Join-Path $PWD "target\aarch64-uwp-windows-msvc\release\pumpkin.dll"

    if (Test-Path $DllPath) {
        Write-Host "Build succeeded. DLL generated at:" -ForegroundColor Green
        Write-Host "  $DllPath" -ForegroundColor Yellow
    }
    else {
        Write-Warning "Build completed but DLL not found at expected location."
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