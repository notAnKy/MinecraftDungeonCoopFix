# DungeonCoopFix v1.3.1 — First public release

A lightweight Windows 10/11 x64 helper for Minecraft Dungeons local co-op. Confirmed in real gameplay with keyboard/mouse for Player 1 and one PS4/DualShock 4 controller for Player 2.

- One-click creation of exactly one virtual Xbox 360 controller.
- Repeated Enable calls keep the same device; Disable and clean shutdown remove it.
- One-time setup with the bundled official signed ViGEmBus 1.22.0 installer.
- Steam Input and controller ordering are required for the confirmed setup.
- No game modification, injection, telemetry, account or app background service.

## Confirmed setup

Disconnect the PS4, enable DungeonCoopFix, then reconnect the PS4. Enable Steam Input for Minecraft Dungeons and launch through Steam. Open the overlay with Shift + Tab and set Controller Order to:

1. Xbox 360 Controller — DungeonCoopFix dummy.
2. PlayStation 4 controller — real controller.
3. Any extra translated Xbox 360 entry Steam shows.

Return to the game: P1 uses keyboard/mouse; join P2 with the PS4. Leave DungeonCoopFix running.

Experimental P1 Setup Mode and the Virtual L3 pulse are labeled as experiments and are not needed for this workflow. ViGEmBus is retired/archived, not actively maintained.

## Downloads

- **DungeonCoopFix.zip** — recommended; includes the .NET runtime.
- **DungeonCoopFix-small.zip** — requires the x64 .NET 10 Windows Desktop Runtime.

Extract the full ZIP and open DungeonCoopFix.exe. If the driver is missing, click Enable once, then Install required driver… and approve the signed setup wizard.

See [README.md](README.md) for installation, controller order and troubleshooting.

## Release history

Version 1.3.1 continues the internal V1–V1.3 version history; this is the first public release. The public preparation updates documentation and UI guidance without changing drivers or controller creation behavior.

## Suggested GitHub release description

First public release of DungeonCoopFix for Windows 10/11 x64. Use keyboard/mouse as Player 1 and one PS4 controller as Player 2 in Minecraft Dungeons on Steam. Creates one virtual Xbox controller; requires Steam Input, the documented controller order and one-time ViGEmBus setup. Download DungeonCoopFix.zip for the recommended runtime-included build.
