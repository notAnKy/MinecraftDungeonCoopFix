# Focused verification — 2026-10-05

## Public release v1.3.1

The project owner confirmed successful real Minecraft Dungeons local co-op: keyboard/mouse P1 and PS4 P2, using Steam Input with the dummy Xbox first, PS4 second, and any extra Xbox entry third. This is the normal workflow documented in the README. P1 Setup Mode and the L3 action remain development experiments; the UI labels them explicitly and no longer recommends setup mode in its active-state guidance.

Earlier sections below are historical development observations, including experiments and limitations recorded before the confirmed Steam workflow. They do not replace the current README instructions.

Public release validation: Release build completed with zero warnings/errors; all 64 focused checks passed. Rendered OFF and ON windows fit and show the experimental labels plus the current Steam-order guidance. Both ZIPs contain version 1.3.1, README, release notes and project/component licenses. The recommended ZIP was extracted, started without a separately installed .NET runtime, responded, and closed normally with exit code 0. Driver/client SHA-256 hashes are unchanged. Public source text was scanned for local machine paths, credential/private-key markers and personal attachment references; no matches were found. Generated builds, tools and screenshots are excluded by .gitignore.

## V1.3 update

The two Steam Xbox entries were reported with the physical PS4 connected. This does not establish that both came from DungeonCoopFix. [Valve's Steam Input documentation](https://partner.steamgames.com/doc/features/steam_controller/steam_input_gamepad_emulation_bestpractices) explains that Steam Input can expose an emulated Xbox controller through Windows game input APIs. A translated PS4 plus the dummy is a possible explanation; the identities of the user's two Steam entries have not been confirmed.

Code inspection found one production native target-add call, one backend created at startup, and one registration of the Enable click handler. Slot verification reads the existing target; it creates no temporary/probe target. Native probes exist only in the separate checks executable and are opt-in. The previous service guarded only the attached state, leaving overlapping Enable operations able to enter Connect while attachment was pending. A gated fake-backend regression exposed this race before the fix.

V1.3 shares the in-flight Enable task, serializes native lifecycle operations, rejects a second backend owner before allocation, and ignores repeated UI clicks while busy. Disable/close cancel pending connection work; the disposed backend cannot reconnect. Development native probes also use the app's existing instance mutex, preventing a probe from creating a pad alongside a running app.

The focused suite passed 64 checks with zero build warnings/errors, including concurrent Enable, repeated Enable, Disable, and cancellation during a queued connection. Existing input, setup-mode, UI and clean-close checks passed without changes to their behavior.

An additional real-driver lifecycle test counted present Windows USB Xbox 360 device roots (`VID_045E&PID_028E`) and XInput slots with physical controllers disconnected. Enable produced exactly one root, `USB\VID_045E&PID_028E\01`, and XInput slot 0. Concurrent/repeated service Enable and direct repeated backend Connect retained that exact same device. A distinct second backend was rejected before allocation. Disable returned both counts to zero. Re-enable returned to one; dispose returned both counts to zero. No driver installation or driver changes were performed.

To repeat this focused hardware check with the app closed and physical controllers disconnected: `dotnet run --project checks/DungeonCoopFix.Checks.csproj -c Release -- --lifecycle-only`. Normal builds never create a native probe controller. For the user's Steam retest, first disconnect the PS4, enable and confirm only one new pad; disable and confirm it disappears. The reported two-entry Steam case and Minecraft Dungeons player assignment still need testing on the user's game setup.

## V1.2 update

The user's in-game test established that XInput slot order is insufficient: the physical PS4 pad still controlled P1, and the dummy became the extra/P2 pad even after L3. V1.2 therefore adds explicit keyboard-backed setup before connecting the physical controller. Neither the UI nor these checks claim confirmed game player ownership.

Compilation: zero warnings/errors. The targeted checks passed for fixed key mappings, held/combined buttons, key-up, repeats, unmapped keys, WASD/arrow aliases, opposite directions, keyboard-removal reset, mode-off neutralization, fix-off cleanup and neutral re-enable. The native probe verified each mapped DOWN and UP bit through actual XInput and confirmed mode-off keeps the same dummy connected. The UI check queried Windows' actual Raw Input registration: INPUTSINK present, no NOLEGACY suppression, expected window handle; unchecked mode removes registration and clears a held A report. Closing with setup mode active also clears held A/L3 and removes registration. The existing L3 pulse and deferred-close checks still pass. Installer/client hashes are unchanged.

Startup and active setup windows were rendered and inspected: `artifacts/startup.png` and `artifacts/v1.2-setup.png`. Background delivery of physical keyboard events while Minecraft Dungeons has focus, actual P1 establishment and keyboard/mouse handover remain unverified until the user's game retest. The native mapping probe invokes the forwarding service directly; it is not evidence of real keyboard capture in the game.

Final V1.2 result: 87 focused checks passed, including the native probe and closing with setup active. Both published EXEs report version 1.2.0.

## V1.1 update

The user confirmed V1's dummy is Controller 1, Steam sees an Xbox 360 pad, and Minecraft Dungeons advances to its P2 join prompt when a physical DualShock 4 is connected. The new activation addresses the previously missing input event; gameplay after activation still needs the user's retest.

With the driver now available in this development environment, the native probe verified the real virtual controller exposes LeftThumb DOWN (`0x0040`) through XInput, preserves DOWN during a health check, releases to zero buttons, remains connected at Controller 1, and disappears after explicit removal. The service/UI checks also verified approximately 125 ms timing, no controller recreation, no physical-count requirement, duplicate-click prevention, release attempts on report failure, and deferred close during a pulse. Compilation succeeded with zero warnings/errors; 46 focused checks passed including the real-driver probe. No driver installation or installer changes were performed in this patch.

The native probe initially expected exact zero stick axes, but this installed input stack reports small offsets (`-3356/-1869/-3255/-848`) at rest. The baseline check now requires no pressed buttons/triggers and axes inside the standard XInput dead zones. The L3 check still requires the exact DOWN bit and a zero-button UP state; this patch does not alter analog behavior.

Rendered startup and enabled windows: `artifacts/startup.png` and `artifacts/v1.1-active.png`.

## Original V1 verification

Development environment: Windows x64; official Microsoft .NET SDK 10.0.401, downloaded to `.tools/dotnet` and checked against Microsoft's published SHA-512. No driver installation or other system configuration changes were performed.

## Completed

- Release application compilation: successful, zero warnings/errors.
- Focused service checks with a small fake backend: single dummy, idempotent enable/disable, dispose cleanup, preconnected-controller guidance, wrong-slot rollback, delayed slot assignment, slot loss, physical-controller reconnect/removal, missing-driver state and polling-error cleanup.
- Native interop structure sizes checked against the XUSB/XInput ABI.
- Bundled official installer SHA-256 and offline Windows Authenticode verification: passed. Downloaded installer signer: Nefarius Software Solutions e.U.
- Actual native client library loaded from the app directory and queried the bus: driver missing, useful error reported, no active dummy retained. XInput reported no connected controllers before the probe.
- Windows Forms window started, rendered and closed. Visible startup content fits within the window. Startup rendering: `artifacts/startup.png`.
- Published portable executable: starts without an installed .NET runtime, enters its Windows message loop and responds. Checked with a hidden window; normal form close is covered by the verification harness. Portable EXE is approximately 49.25 MiB including .NET; the small framework-dependent EXE is approximately 193 KiB. Each release also bundles approximately 6 MiB of controller client/installer files.

**Not verified:** actual dummy creation/removal, crash cleanup on an installed driver, real physical-controller ordering, Windows 10 hardware, and Minecraft Dungeons gameplay. The bus driver is absent here. Fake-backend checks do not establish those outcomes.

## Short real-hardware check

1. On a Windows 10/11 x64 PC, open the release EXE and enable. Use the app's explicit driver setup action if needed. Confirm UAC and complete the official wizard; restart if requested.
2. With all controllers disconnected, enable. Confirm the dummy shows Controller 1.
3. Keep the physical controller disconnected, check **P1 Setup Mode**, and launch Minecraft Dungeons. Use Enter, Escape, arrows/WASD and F8 to establish the virtual pad as the game's first active controller. Connect the PS4 controller only afterward and join P2. Turn setup mode off and confirm whether the game permits keyboard/mouse handover to P1. A PS4/Steam Input pad may show XInput count 0.
4. Disable: confirm the dummy disappears from Windows Game Controllers (`joy.cpl`). Repeat enable and close the window: confirm it disappears again. The user does not need this panel in normal use.
5. Check preconnected-controller guidance, physical-controller unplug/replug, and starting the app a second time. Confirm no extra dummy is created.

For a focused native probe after explicitly installing the driver, run `dotnet run --project checks/DungeonCoopFix.Checks.csproj -c Release -- --probe-driver` from development tools. It skips creation if other XInput controllers are present; otherwise it checks slot 0, a neutral report, and removal. Normal builds/checks never install the driver.
