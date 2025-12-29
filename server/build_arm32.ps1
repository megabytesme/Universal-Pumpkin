# ==============================================================================
#  BUILD SCRIPT: Windows UWP (ARM32) - 10240 SDK
# ==============================================================================
#  1. Patches global Rust registry (windows-sys) to enable ARM32 structs.
#  2. Creates temporary "Imposter Libs" to block Desktop APIs.
#  3. Builds with SDK 10240.
#  4. Cleans up temporary libs.
# ==============================================================================

$ErrorActionPreference = "Stop"

# --- PATCH RUST SOURCES (windows-sys) ---
Write-Host "Checking Rust Registry sources..." -ForegroundColor Cyan

$RegistryBase = Join-Path $env:USERPROFILE ".cargo\registry\src"
# Find the crates.io registry folder
$RegistrySrc = Get-ChildItem -Path $RegistryBase -Filter "index.crates.io-*" | Select-Object -First 1

if ($RegistrySrc) {
    $StructsToPatch = @("WSADATA", "SERVENT", "DELAYLOAD_INFO")
    $PackageDirs = Get-ChildItem -Path $RegistrySrc.FullName -Directory -Filter "windows-sys-*"
    $PatchCount = 0

    foreach ($PackageDir in $PackageDirs) {
        $ChecksumFile = Join-Path $PackageDir.FullName ".cargo-checksum.json"
        $ChecksumJson = $null
        $ChecksumModified = $false

        if (Test-Path $ChecksumFile) {
            $ChecksumJson = Get-Content $ChecksumFile -Raw | ConvertFrom-Json
        }

        # Find candidate files
        $SourceFiles = Get-ChildItem -Path $PackageDir.FullName -Recurse -Filter "*.rs" | 
        Select-String -Pattern ($StructsToPatch -join "|") -List | 
        Select-Object -ExpandProperty Path

        foreach ($File in $SourceFiles) {
            $Content = Get-Content $File -Raw
            $FileModified = $false
            
            foreach ($StructName in $StructsToPatch) {
                # Look for the specific x86-only definition
                $Pattern = '(?s)(#\[cfg\(target_arch = "x86"\)\])(\s*(?:#\[[^\]]+\]\s*)*)(pub struct ' + $StructName + '\b|impl Default for ' + $StructName + '\b)'

                if ($Content -match $Pattern) {
                    # Inject 'arm' support
                    $Content = [regex]::Replace($Content, $Pattern, {
                            param($match)
                            return '#[cfg(any(target_arch = "x86", target_arch = "arm"))]' + $match.Groups[2].Value + $match.Groups[3].Value
                        })
                    $FileModified = $true
                    $PatchCount++
                }
            }

            if ($FileModified) {
                Set-Content -Path $File -Value $Content -NoNewline
                # Bypass Checksum
                if ($ChecksumJson) {
                    $RelativePath = $File.Substring($PackageDir.FullName.Length + 1).Replace('\', '/')
                    if ($ChecksumJson.files.$RelativePath) {
                        $ChecksumJson.files.PSObject.Properties.Remove($RelativePath)
                        $ChecksumModified = $true
                    }
                }
            }
        }

        if ($ChecksumModified) {
            $ChecksumJson | ConvertTo-Json -Depth 100 | Set-Content $ChecksumFile
        }
    }
    if ($PatchCount -gt 0) { Write-Host "  Patched $PatchCount instances in windows-sys." -ForegroundColor Green }
}
else {
    Write-Warning "Could not locate Cargo registry. Skipping patch step."
}

# --- CONFIGURE BUILD ENVIRONMENT ---
$SdkBase = "C:\Program Files (x86)\Windows Kits\10\Lib\10.0.10240.0"

if (-not (Test-Path $SdkBase)) {
    Write-Error "Error: Windows SDK 10.0.10240.0 not found. Please install it via VS Installer."
    exit 1
}

$HybridDir = Join-Path $PWD "libs_arm32_temp"

try {
    # --- SETUP IMPOSTER LIBS ---
    Write-Host "Setting up temporary build libraries..." -ForegroundColor Cyan
    if (-not (Test-Path $HybridDir)) { New-Item -Path $HybridDir -ItemType Directory | Out-Null }

    # Use WindowsApp.lib as the replacement libs
    $TemplateLib = Join-Path $SdkBase "um\arm\WindowsApp.lib"
    if (-not (Test-Path $TemplateLib)) { $TemplateLib = Join-Path $SdkBase "um\arm\mincore.lib" }

    # List of libraries to BLOCK (replaced with WindowsApp.lib)
    $BlockedLibs = @(
        "user32.lib",         # Blocks GUI/Input APIs
        "gdi32.lib",          # Blocks Graphics APIs
        "shell32.lib",        # Blocks Shell APIs
        "opengl32.lib",       # Blocks OpenGL
        "windows.0.52.0.lib", # Rust stub replacement
        "windows.0.48.0.lib", # Rust stub replacement
        "windows.0.53.0.lib"  # Rust stub replacement
    )

    foreach ($Name in $BlockedLibs) {
        Copy-Item -Path $TemplateLib -Destination (Join-Path $HybridDir $Name) -Force
    }

    $SystemLibs = @("kernel32.lib", "ws2_32.lib", "advapi32.lib", "bcrypt.lib")
    foreach ($Name in $SystemLibs) {
        $Path = Join-Path $HybridDir $Name
        if (Test-Path $Path) { Remove-Item $Path -Force }
    }

    $env:LIB = "$HybridDir;$SdkBase\um\arm;$SdkBase\ucrt\arm"

    # --- BUILD ---
    Write-Host "Building Pumpkin (thumbv7a-uwp-windows-msvc - Release)..." -ForegroundColor Green
    
    cargo +nightly build -Z "build-std=std,panic_abort" `
        --target thumbv7a-uwp-windows-msvc `
        --release `
        --no-default-features

    # --- REPORT DLL LOCATION ---
    $DllPath = Join-Path $PWD "target\thumbv7a-uwp-windows-msvc\release\pumpkin.dll"

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
    # --- CLEANUP ---
    if (Test-Path $HybridDir) {
        Write-Host "Cleaning up temporary libraries..." -ForegroundColor Gray
        Remove-Item -Path $HybridDir -Recurse -Force
    }
}
