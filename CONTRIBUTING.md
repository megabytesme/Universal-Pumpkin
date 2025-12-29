# Contributing
Hey! Thanks for your interest in the Universal Pumpkin project.

## Guide
1.  Fork the repository.
2.  Create a new branch (`git checkout -b feature-branch`).
3.  **Important:** If modifying the Rust server, please verify your changes against the **ARM32** build script. New dependencies often introduce Desktop-only APIs that break ARM32 UWP support.
4.  Commit your changes.
5.  Create a new Pull Request.

### NOTE: Upstream vs. Universal Pumpkin
This project is a downstream port of the **[Pumpkin](https://github.com/Pumpkin-MC/Pumpkin)** server engine. To support the open-source ecosystem effectively, please direct your contributions to the right place:

*   **General Server Logic:** If you are fixing a bug in the Minecraft protocol, adding a game feature or optimizing chunk loading for example, please submit your Pull Request to the **[official Pumpkin repository](https://github.com/Pumpkin-MC/Pumpkin)** first (whilst ensuring to follow their [contribution guidelines](https://github.com/Pumpkin-MC/Pumpkin/blob/master/CONTRIBUTING.md)). I intend to sync with upstream regularly.
*   **UWP Specifics:** If your change is related to the UWP host application, the FFI bridge, any build scripts, or optimising dependencies to better suit UWP, please submit your Pull Request here.
