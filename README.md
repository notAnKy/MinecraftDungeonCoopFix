# DungeonCoopFix

A lightweight Windows helper for Minecraft Dungeons local co-op that lets Player 1 use keyboard/mouse while Player 2 uses one real controller.

**Confirmed in real gameplay:** keyboard/mouse for P1 + a PS4/DualShock 4 for P2, using Steam Input and the controller order below.

## Why this exists

Minecraft Dungeons on PC normally expects two controllers before allowing local multiplayer. Keyboard/mouse and the first controller can both act as Player 1.

DungeonCoopFix adds one neutral virtual Xbox 360 controller so your real controller can be assigned to Player 2. You do not need a second physical controller.

## Features

- One-click virtual Xbox controller; keyboard/mouse stays P1 in the confirmed setup.
- One physical controller can join as P2.
- No game modification, injection, cheats, telemetry or account.
- No DungeonCoopFix background service; a small Windows desktop app.
- Disable or close the app to remove its virtual controller.

## Requirements

- Windows 10 or Windows 11, **64-bit (x64)**.
- Minecraft Dungeons on Steam.
- One physical controller. PS4/DualShock 4 is the confirmed test.
- Steam Input enabled for the game, with Steam Overlay available.
- ViGEmBus **1.22.0**, installed once through the app.

**ViGEmBus is retired/archived and is not actively maintained.** Its official signed final installer is bundled because it remains suitable for this specific V1 utility. [Official release](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0) · [Retirement notice](https://docs.nefarius.at/projects/ViGEm/End-of-Life/).

## Installation

1. Download **DungeonCoopFix.zip** from this repository's latest GitHub Release.
2. Extract the entire ZIP to a folder.
3. Open **DungeonCoopFix.exe**.
4. On a PC without the driver, click **Enable Co-op Fix** once to reveal **Install required driver…**.
5. Click that link, confirm the prompt, and approve the signed **Nefarius ViGEmBus** installer when Windows asks for administrator permission.
6. Finish the setup wizard. Restart Windows if requested, then open the app again.

Driver installation is required only once. Normal app use does not require administrator permission.

| Download | Which should I choose? |
| --- | --- |
| **DungeonCoopFix.zip** | Recommended. Includes the .NET runtime. |
| **DungeonCoopFix-small.zip** | Smaller download; requires the x64 **.NET 10 Windows Desktop Runtime** from [Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/10.0). |

Keep the EXE, DLL, driver folder and other included files together.

## How to use

Start with Minecraft Dungeons completely closed.

1. Disconnect the PS4 controller.
2. Open DungeonCoopFix.
3. Click **Enable Co-op Fix**.
4. Confirm **Co-op Fix: ON** and **Dummy Controller: Active**.
5. Connect the PS4 controller.
6. In Steam, open Minecraft Dungeons **Properties → Controller** and choose **Enable Steam Input**.
7. Launch Minecraft Dungeons through Steam.
8. Press **Shift + Tab** to open Steam Overlay.
9. Open **Controller Settings / Controller Order** (wording can vary between Steam views).
10. Set the order to:

    | Order | Controller |
    | --- | --- |
    | 1 | **Xbox 360 Controller** — DungeonCoopFix dummy |
    | 2 | **PlayStation 4 controller** — real controller |
    | 3 | Any **extra Xbox 360 entry** Steam shows, if present |

11. Return to the game. Player 1 uses keyboard/mouse.
12. At the local co-op prompt, press the join input on the PS4 controller (left-stick click/L3 when prompted) to join as **Player 2**.

**Leave DungeonCoopFix running while playing.** Disable the fix or close the app when finished.

**Do not use the experimental controls for this workflow.** Leave **Experimental: P1 Setup Mode** unchecked and do not click **Experimental: Virtual L3 pulse** (formerly “Activate / Join Virtual P1”). These development experiments are retained for diagnostics and are not needed for the confirmed setup.

## Steam Input / Controller Order

Steam may list the real PS4 pad, the DungeonCoopFix Xbox pad and an additional translated Xbox entry. [Steam Input can expose emulated Xbox input](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices), so two Xbox names in Steam do not necessarily mean the app created two devices.

Keep the dummy first, PS4 second and any extra Xbox entry third. This is the order confirmed in real Minecraft Dungeons testing.

Windows **joy.cpl** may still show only one virtual Xbox controller, which is normal. The app creates exactly one dummy; its XInput physical-controller counter may show 0 for a working PS4 managed through Steam.

## Troubleshooting

### “Connect two or more controllers”

Make sure DungeonCoopFix is ON before launching or joining, the dummy is active, and the real controller is connected.

### PS4 controls Player 1

Open Steam Controller Order. Put the dummy **Xbox 360 Controller first** and **PlayStation 4 controller second**. Put an extra Xbox entry third.

### Steam shows two Xbox 360 controllers

This can happen with Steam Input translation. Use **Xbox dummy → PS4 → extra Xbox**. Check Windows separately with joy.cpl if unsure.

### App says the driver is missing

Click **Install required driver…** and complete the bundled signed setup wizard. Restart if requested.

### Controller does not appear

Disconnect the real controller, enable DungeonCoopFix, then reconnect it. Confirm Steam detects it and Steam Input is enabled for the game. Close other virtual-controller apps if the dummy cannot take the first XInput slot.

### Want to verify Windows sees the dummy?

Press **Win + R**, type **joy.cpl**, and press Enter. With the fix ON, look for **Controller (XBOX 360 For Windows)**. Disable the fix: the app's dummy should disappear.

## Safety / Privacy

DungeonCoopFix does not modify Minecraft Dungeons, inject into the game, read/write game memory, bypass anti-cheat, send telemetry or require an account. It needs no internet during normal use and installs no app background service.

The normal workflow only creates/removes a virtual Xbox controller. If you explicitly enable experimental setup mode, it forwards a small fixed set of keyboard buttons; normal keyboard input remains usable and no text is stored or logged.

ViGEmBus is a separate Windows driver. Its one-time installation requires administrator permission; it remains installed after closing the app and can be removed through Windows Installed apps when no other controller software needs it.

## Technical overview

C# / **.NET 10**, Windows Forms, ViGEmBus and XInput. The app owns one virtual Xbox 360 target, shares repeated Enable operations, and removes it on disable or clean shutdown. Windows controller slots and game player assignment are different.

[Backend details](docs/backend-decision.md) · [Verification history](docs/verification.md) · [Bundled components](THIRD-PARTY-NOTICES.md).

## Build from source

On Windows, install the **.NET 10 SDK**. The application targets `net10.0-windows` and `win-x64`.

From the repository root, run in PowerShell:

```powershell
dotnet restore .\src\DungeonCoopFix\DungeonCoopFix.csproj
dotnet build .\src\DungeonCoopFix\DungeonCoopFix.csproj -c Release --no-restore
dotnet run --project .\checks\DungeonCoopFix.Checks.csproj -c Release
.\build.ps1
```

The script builds, runs focused checks and publishes both folders and ZIPs under **dist/**. It does not install the driver or create native probe controllers. Restore/publish may download Microsoft build/runtime packages.

To publish only the recommended portable build:

```powershell
dotnet publish .\src\DungeonCoopFix\DungeonCoopFix.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o .\dist\DungeonCoopFix
```

Use **build.ps1** for a complete release package, including documentation and licenses. Source layout: **src/** app, **checks/** verification, **docs/** technical notes, **vendor/** pinned native client, **drivers/** official installer and license.

## Releases

Normal users should download **DungeonCoopFix.zip**, not GitHub's “Source code” archive. See [release notes](RELEASE_NOTES.md).

Attach the two generated ZIPs to GitHub Releases. Keep **dist/**, **bin/**, **obj/**, local tools and generated screenshots out of the source branch; the .gitignore already excludes them. The native client and driver installer are intentional source assets required to build offline-ready packages.

## Known limitations

- Windows x64 only; designed/tested primarily with Minecraft Dungeons on Steam.
- Controller order depends on Steam Input.
- PS4/DualShock 4 is the confirmed real-world controller; other controllers may work.
- Steam may show translated/duplicate Xbox names.
- ViGEmBus is retired and receives no updates.
- Experimental setup/L3 controls are not part of the supported normal workflow.

## License

DungeonCoopFix source is under the [MIT license](LICENSE). Bundled third-party components keep their own licenses; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Unofficial community utility; not affiliated with Mojang, Microsoft, Valve or Sony.
