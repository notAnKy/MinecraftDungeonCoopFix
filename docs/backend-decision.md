# Backend decision — 2026-10-05

Current gameplay result: the project owner confirmed keyboard/mouse P1 + PS4 P2 through Steam Input with Controller Order set to dummy Xbox, PS4, then any extra Xbox entry. Follow the [README](../README.md) for normal use. The V1.1/V1.2 sections below describe retained experimental controls, not the recommended workflow.

Choose the official signed **ViGEmBus 1.22.0** and a minimal C# adapter to the official ViGEmClient native API. This is a deliberate legacy compromise, not a claim that ViGEmBus is maintained.

Alternatives checked against primary project sources:

| Option | Finding | V1 decision |
| --- | --- | --- |
| [LizardByte Virtual HID Driver](https://app.lizardbyte.dev/2026-08-16-introducing-libvirtualhid-and-virtual-hid-driver/) | Actively maintained, user mode, paid license; an internet connection is required when creating a gamepad to check the machine license. | Conflicts with offline normal use and introduces paid account setup. |
| [HIDMaestro](https://github.com/hifihedgehog/HIDMaestro) | Active MIT project; UMDF2; uses a locally trusted self-signed certificate. | Avoid adding a locally generated certificate to the user's trust stores; does not provide the signed trusted release model requested here. |
| [LuminalVGBus](https://github.com/NortheBridge/LuminalVGBus) | Maintained ViGEm fork; project describes alpha status, not yet signed or loaded on a machine. | No deployable signed release verified. |
| [Nefarius VirtualPad](https://docs.nefarius.at/projects/VirtualPad/) | Commercial framework available to business partners. | Licensing/integration exceeds this tiny utility. |
| [ViGEmBus 1.22.0](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0) | Official final release, updater removed. The downloaded installer validates as signed by Nefarius. [Retirement statement](https://docs.nefarius.at/projects/ViGEm/End-of-Life/). | Small, documented, offline Xbox 360 emulation. Explicitly disclose retirement and isolate it behind one backend class. |

This evaluation does not establish that no suitable alternative exists anywhere. These are the current publicly documented options checked for the stated signing, offline, setup and footprint requirements.

## Slot ordering and lifecycle

[Microsoft's XInput documentation](https://learn.microsoft.com/en-us/windows/win32/xinput/getting-started-with-xinput) says controller indices 0–3 are automatically assigned and cannot be changed through XInput. Creating a dummy after a pad is already connected cannot be assumed to reorder it.

The app therefore requires the XInput slots to be empty before enabling, creates one Xbox 360 target, submits a neutral report, and polls the upstream [`vigem_target_x360_get_user_index`](https://github.com/nefarius/ViGEmClient/blob/master/include/ViGEm/Client.h) query. Only actual slot 0 plus an XInput-visible device at that slot counts as successful enable. An unconfirmed or wrong slot triggers removal, with reconnect/restart guidance. Readiness also requires an additional controller at slot 1. This checks Windows; it makes no untested promise about a particular game's enumeration behavior.

The UI polls every 1.5 seconds, removes the dummy if slot ownership is lost, and excludes it from the other-controller count. Native connection work runs away from the UI; disable/close never races it. Closing while connecting waits for the operation to complete, then removes the target and closes/frees the client. Normal close, disable, failed enable and polling errors all run cleanup. No callback threads, continuous input mappings, device disabling, registry changes, game hooks or custom driver code are used.

The client and installer bytes are pinned by SHA-256. Native code is loaded by absolute path from the app folder. Before UAC setup, the installer is also checked with Windows `WinVerifyTrust`, cache-only URL retrieval. Setup has its normal visible wizard, with no silent-install flags. Driver installation remains separate from controller creation.

V1 ships x64 application binaries only. The signed driver's installer also contains other architectures; this does not imply ARM64/x86 app support. Windows 10/11 behavior on real hardware remains part of the manual verification checklist.

## V1.1 input patch

The existing target can now send a single LeftThumb/L3 press (`0x0040`), held for approximately 125 ms and released in `finally`. Successful activation preserves the connection and slot; no new target, backend, driver or mapping system is involved. The UI pauses polling and blocks other operations during the pulse. Health reports also preserve the current button state. Closing during the pulse waits for release, then runs normal cleanup. If an input report fails, release is attempted and the connection is closed to avoid a stuck button. The input action does not require a physical-controller count, so PS4/Steam users can use it even when their XInput count is 0.

## V1.2 keyboard setup mode

Real game testing showed that slot order and the L3 pulse do not establish the dummy as the game's first player. Setup mode forwards a small fixed set of held keyboard buttons to the existing target: Enter/A, Escape/B, arrows or WASD/D-pad, F8/L3. No target recreation, driver or configuration system is added.

Ordinary WinForms KeyDown/KeyUp capture depends on window focus. `RegisterHotKey` supplies hotkey notifications, not the key-up events needed for held navigation buttons. [Windows Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input) with [RIDEV_INPUTSINK](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawinputdevice) supports background key-down/up messages without a hook. The app registers keyboard usage 1/6 only while setup is active, with INPUTSINK and DEVNOTIFY; it never sets NOLEGACY, NOHOTKEYS or keyboard suppression flags.

Only mapped held-key identifiers are retained in memory; there is no text capture, key logging, storage, network activity or input injection. Repeated keydown is ignored, aliases retain a D-pad direction until both keys are released, and opposite directions cancel. Mode-off sends a zero-button report and unregisters capture; keyboard removal releases held keys. Fix-off, native report failure and shutdown clear mode state. Health polling preserves held reports. UI L3 pulses are disabled during setup to avoid conflicting ownership of the L3 bit. Windows device slot status is explicitly distinguished from game player assignment. Background physical-key delivery and the game's P1 setup workflow require in-game confirmation.

## V1.3 single-controller safeguards

The service shares a pending Enable task so overlapping calls cannot both enter connection creation. The backend serializes native access and permits one owner; repeated Connect retains its existing target and a second backend cannot allocate a target. Disable/close cancel queued connection work and disconnect the owned target. Slot/health verification continues to query/update that target without creating a probe. The existing app instance mutex also protects opt-in native development probes. Driver components, UI layout and keyboard mappings are unchanged.
