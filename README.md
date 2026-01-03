# Universal Pumpkin


| Lumia 950 XL | Surface Duo | PC |
|--------------|-------------|-------------|
| <img width="24" height="24" alt="Pumpkin - 1607" src="https://github.com/user-attachments/assets/67000671-9a48-461f-b498-1ec5d1dcc56b" /> **Universal Pumpkin (1507)** | <img width="24" height="24" alt="Segoe UI Emoji Regular - Jack-O-Lantern" src="https://github.com/user-attachments/assets/5592d52d-37e8-4fa7-ab9c-bce6b8e9c5f8" /> **Universal Pumpkin** | <img width="24" height="24" alt="Segoe UI Emoji Regular - Jack-O-Lantern" src="https://github.com/user-attachments/assets/5592d52d-37e8-4fa7-ab9c-bce6b8e9c5f8" /> **Universal Pumpkin** |
| ![Lumia950XL-Windows10Mobile](https://github.com/user-attachments/assets/1706fa3b-c884-4238-a43a-ad2bce1b9ced) | ![SurfaceDuo-Andromeda](https://github.com/user-attachments/assets/c0d79ead-ee6e-4fd6-90b4-b744135b40b9) | <img width="1884" height="1401" alt="Screenshot 2026-01-03 041941" src="https://github.com/user-attachments/assets/1346dd3c-1c3d-41c9-91d2-51bfa9113326" /> |
| _Windows 10 Mobile – 15254.603, ARM_ | _Andromeda OS (8828080) – 18236.1000, ARM64_ | _Windows 11 - 26220.7523, X64_ |

**Universal Pumpkin** is a native, high-performance Minecraft server client for the Universal Windows Platform (UWP). It wraps the fast **Pumpkin** server engine (Rust) into a portable application that runs on everything supported by the Universal Windows Platform (UWP).

**Disclaimer:** This is an unofficial, third-party server implementation. This project is not affiliated with, endorsed, or sponsored by Mojang Studios, Microsoft, or the official Pumpkin project. "Minecraft" is a trademark of Mojang Synergies AB.

## Features

*   **Native & Lightweight:** A fully functional Minecraft server running as a UWP app - No Java needed.
*   **Universal:** Runs natively on **x64**, **x86**, **ARM64**, and **ARM32**.
*   **Latest Version Support:** Targets Minecraft **1.21.11**.
*   **Hybrid Protocol:** Supports **Java Edition** (TCP) connections, with support for **Bedrock Edition** (UDP).
*   **High Performance:** Leverages Rust’s multi-threading and memory safety for maximum tick speeds.

## Download
### See the [Latest Release](https://github.com/megabytesme/Universal-Pumpkin/releases/latest).
#### Windows Store releases are upcoming...

## Build Guide

Building Universal Pumpkin is unique because it combines a C# UWP Host with a Rust Dynamic Library (`pumpkin_uwp.dll`).

### Prerequisites

*   **Rust Nightly Toolchain:** Required for the `-Z build-std` flag.
*   **Visual Studio 2017 (or newer):**
    *   **Workload:** Universal Windows Platform development.
    *   **Workload:** Desktop development with C++.
*   **Windows SDKs:** You **must** install these specific SDK versions via the VS Installer:
    *   **10.0.10240.0** (Required for x86, x64, and ARM32 compatibility).
    *   **10.0.16299.0** (Required for ARM64 compatibility).


## **1. Building the Pumpkin Server Core (Rust)**  
Pumpkin Core is written in Rust and compiled as a **UWP‑safe DLL** (`pumpkin_uwp.dll`).  

### **Run the appropriate build script from the `/server` directory:**

| Target | Script | Typical Devices |
|--------|--------|-------|
| **ARM32** | `.\build_arm32.ps1` | Windows 10 Mobile, Windows RT Devices |
| **ARM64** | `.\build_arm64.ps1` | Copilot Plus PCs, HoloLens 2, Surface Duo, WoA, Snapdragon or Microsoft SQ1-SQ3 |
| **x86** | `.\build_x86.ps1` | Older PCs (typically with 4GB RAM or less), emulators, HoloLens 1 |
| **x64** | `.\build_x64.ps1` | Modern PCs |

Each script outputs a DLL to:

```
server/target_X/release/pumpkin_uwp.dll
```

## **2. Building the UWP Host App (C#)**  
Open the solution:

```
UniversalPumpkin.sln
```

### **Steps:**
1. Copy the generated `pumpkin_uwp.dll` into the **root directory** of the UWP project you intend to run.  
2. In Visual Studio, set **Solution Platform** to match your target device:  
   - `ARM` → Windows 10 Mobile  
   - `ARM64` → HoloLens 2, Windows on ARM  
   - `x86` → Emulators, older PCs  
   - `x64` → Modern PCs  
3. Press **F5** to deploy and run.

# **Architectural Overview**

Universal Pumpkin runs Rust logic inside a UWP sandbox.

### **Pumpkin Server (Rust DLL)**
- Compiled as `pumpkin_uwp.dll`
- Exposes **C‑compatible FFI entry points**
- Built with `--no-default-features` to avoid forbidden APIs
- Handles world logic, chunk generation, heightmaps, etc.

### **UWP Client (C#)**
- Loads the Rust DLL at runtime
- Passes configuration paths into the Rust core
- Receives log messages via callback delegates
- Renders UI, handles input, manages app lifecycle

This separation allows full Rust performance while staying within UWP’s sandbox rules.

# **Client Projects**

There are **two** UWP client apps, each targeting different OS generations.

## **1. Universal Pumpkin (Recommended)**
- **Minimum OS:** Windows 10 **16299**  
- **Supports:** x86, x64, ARM, ARM64  
- **Features:**  
  - Acrylic  
  - Fluent Design  
  - Modern APIs (post‑Fall Creators Update)

Use this version unless you specifically need legacy support.


## **2. Universal Pumpkin (1507)**
- **Minimum OS:** Windows 10 **10240**  
- **Supports:** x86, x64, ARM  
- **Features:**  
  - Classic UWP styling  
  - No modern WinRT APIs - Maximum compatibility  
- **Intended for:**  
  - Windows 10 Mobile  
  - Early Windows 10 builds  
  - Legacy devices

# **Platform Selection Guide**

Here’s a quick grid to help users choose the correct build + client:

| Device / OS | DLL Architecture | UWP Project |
|-------------|------------------|-------------|
| **Windows 10 Mobile (10240–15254)** | ARM32 | **Universal Pumpkin (1507)** |
| **Windows 10 Desktop (10240–16298)** | x86/x64 | **Universal Pumpkin (1507)** |
| **Windows 10 Desktop (16299+)** | x86/x64 | **Universal Pumpkin** |
| **Windows 11 Desktop** | x64 | **Universal Pumpkin** |
| **Windows on ARM (Surface Pro X, etc.)** | ARM64 | **Universal Pumpkin** |
| **HoloLens 2** | ARM64 | **Universal Pumpkin** |
| **Windows 10X / CoreOS variants** | ARM64/x64 | **Universal Pumpkin** |

## Contributing

1.  Fork the repository.
2.  Create a new branch (`git checkout -b feature-branch`).
3.  **Important:** If modifying the Rust server, please verify your changes against the **ARM32** build script. New dependencies often introduce Desktop-only APIs that break ARM32 UWP support.
4.  Commit your changes.
5.  Create a new Pull Request.

### Upstream vs. Universal Pumpkin
This project is a downstream port of the **[Pumpkin](https://github.com/Pumpkin-MC/Pumpkin)** server engine. To support the open-source ecosystem effectively, please direct your contributions to the right place:

*   **General Server Logic:** If you are fixing a bug in the Minecraft protocol, adding a game feature or optimizing chunk loading for example, please submit your Pull Request to the **[official Pumpkin repository](https://github.com/Pumpkin-MC/Pumpkin)** first (whilst ensuring to follow their [contribution guidelines](https://github.com/Pumpkin-MC/Pumpkin/blob/master/CONTRIBUTING.md)). I intend to sync with upstream regularly.
*   **UWP Specifics:** If your change is related to the UWP host application, the FFI bridge, any build scripts, or optimising dependencies to better suit UWP, please submit your Pull Request here.

# **Bringing in Changes From Upstream**

This workflow syncs the upstream Pumpkin repository into your `/server` subfolder using `git subtree`.

## **1. Add and Fetch Upstream**

```bash
git remote add upstream https://github.com/Pumpkin-MC/Pumpkin.git
git fetch upstream
```

## **2. Create a Working Branch**

```bash
git checkout -b update-pumpkin-server
```

## **3. Pull Upstream Into the `/server` Subfolder**

```bash
git subtree pull --prefix server upstream master
```

Resolve any merge conflicts if they appear.

## **4. Rebuild Pumpkin (UWP-Compatible Targets)**

```bash
cargo clean
.\build_arm32.ps1
```

*(Or build/test any other architecture as needed.)*

## **5. Stage and Commit the Merge**

```bash
git status
git add .
git commit
```

If Git opens an editor for the commit message, **leave it blank**.


## **6. Merge Into `master` With a Merge Commit**

```bash
git checkout master
git merge --no-ff update-pumpkin-server
```

## **7. Commit Message Template**

Use this exact structure for the merge commit:

```
Server: Update Pumpkin Server (Sync with Upstream UPSTREAM COMMIT ID)

Synchronised the local /server directory with the official Pumpkin-MC/Pumpkin repository (Master).

Key Integration Changes for UWP:
- Only include brief details on required changes made. Otherwise remove this section.

Upstream Changelog:
-------------------
1. example: issuelabel (#issue number) - user1
   * summary 1
   * summary 2
   * summary 3

2. example: issuelabel (#issue number) - user2
   * summary 1
   * summary 2
   * summary 3
   * summary 4
```

## **Updating Client Version Metadata After Syncing Upstream**

When synchronizing the Pumpkin server core into the `/server` directory, you must also update the version metadata used by the UWP client. These values are displayed in the **About** dialog and help identify exactly which upstream commit the bundled server core is based on.

After completing the upstream sync, open:

```
client/Universal_Pumpkin.Shared/ViewModels/SettingsViewModel.cs
```

and update the following helper methods:

- `GetPumpkinVersion()`  
- `GetMinecraftVersion()`  
- `GetProtocolVersion()`  
- `GetPumpkinCommitId()`  

These should reflect the **exact upstream commit** you pulled into `/server`, along with the corresponding Pumpkin, Minecraft, and protocol versions (these can be sourced via running the `pumpkin` command in the server).

## License

This project operates under a dual-license structure:

*   **Universal Pumpkin (`/client`):** Licensed under **CC BY-NC-SA 4.0** (Attribution-NonCommercial-ShareAlike 4.0 International). See [LICENSE](LICENSE) for details.
*   **Pumpkin Server (`/server`):** The underlying server logic is based on [Pumpkin](https://github.com/Pumpkin-MC/Pumpkin) and remains licensed under the **MIT License**. See [server/LICENSE](server/LICENSE) for details.
