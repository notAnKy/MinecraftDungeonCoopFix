# Bundled controller components

- `ViGEmClient.dll`: x64 client from **Nefarius.ViGEm.Client 1.21.256**, published by Nefarius on NuGet. Extracted unmodified from its `costura64.vigemclient.dll` assembly resource. [Package](https://www.nuget.org/packages/Nefarius.ViGEm.Client/1.21.256), [source](https://github.com/nefarius/ViGEmClient). MIT license: `licenses/ViGEmClient.txt` in published builds, `vendor/ViGEmClient/LICENSE` in source.
  SHA-256: `00303F99E1968D523FBD08A7C691D60D145B7335059EC593947841D124C10875`.
- `ViGEmBus_1.22.0_x64_x86_arm64.exe`: unmodified official Nefarius driver installer. [Release and download](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0). BSD-3-Clause license: `licenses/ViGEmBus.txt` in published builds, `drivers/LICENSE` in source.
  SHA-256: `89220A7865076B342892F98865F3499FB7C4CFD673159E89D352C360FD014C6A`.
  Verified Authenticode signer: **Nefarius Software Solutions e.U.**; certificate thumbprint `1F431092EC96A80B41AB5317F53AC02EA6F9B89B`.

ViGEmBus and its clients are retired. Version 1.22.0 removes the updater. DungeonCoopFix does not download, update, or silently install either component. The application checks the pinned installer hash and Windows Authenticode trust before opening its interactive setup wizard with UAC.

Portable builds also contain the Microsoft .NET runtime, under Microsoft's distributed license and third-party notices in `licenses`. Source and framework-dependent builds require a separately installed .NET 10 Windows Desktop Runtime.
