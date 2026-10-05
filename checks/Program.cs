using System.Diagnostics;
using System.Runtime.InteropServices;
using DungeonCoopFix;

internal static class Program
{
    private static int _passed;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--lifecycle-only"))
            {
                ProbeOneDeviceLifecycle().GetAwaiter().GetResult();
                return 0;
            }
            RunOneControllerCheck().GetAwaiter().GetResult();
            RunCancelledEnableCheck().GetAwaiter().GetResult();
            RunServiceChecks().GetAwaiter().GetResult();
            RunSetupChecks().GetAwaiter().GetResult();
            Check(Marshal.SizeOf<NativeMethods.GamepadReport>() == 12 &&
                  Marshal.SizeOf<NativeMethods.XInputState>() == 16, "Native report layouts match the Windows ABI");
            Check(Marshal.SizeOf<SetupKeyboardInput.RawInputDevice>() == 16 &&
                  Marshal.SizeOf<SetupKeyboardInput.RawInputHeader>() == 24 &&
                  Marshal.SizeOf<SetupKeyboardInput.RawKeyboard>() == 16, "Raw keyboard structures match the Windows x64 ABI");
            DriverInstaller.Verify(DriverInstaller.InstallerPath);
            Check(true, "Pinned official installer passes offline Authenticode verification");
            if (args.Contains("--probe-driver")) ProbeDriver();
            RunWindowChecks();
            RunSetupCloseCheck();
            Console.WriteLine($"PASS: {_passed} focused checks.");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static async Task RunServiceChecks()
    {
        var slots = new Slots();
        var backend = new Backend(slots);
        using (var service = new ControllerService(backend, slots))
        {
            Check(!service.GetStatus().Enabled, "Starts disabled");
            try
            {
                await service.ActivateVirtualP1Async();
                throw new InvalidOperationException("Activation should require an enabled dummy.");
            }
            catch (InvalidOperationException e) when (e.Message.Contains("Enable the co-op fix"))
            {
                Check(backend.ThumbReports.Count == 0, "Activation while disabled sends no input");
            }
            await service.EnableAsync();
            Check(service.Enabled && backend.Created == 1 && !service.GetStatus().Ready,
                "Creates one dummy and waits for the real controller");
            await service.EnableAsync();
            Check(backend.Created == 1, "Repeated enable never creates another dummy");
            var pulseTime = Stopwatch.StartNew();
            Task pulse = service.ActivateVirtualP1Async();
            Check(backend.LeftThumb && !pulse.IsCompleted, "Virtual P1 activation sends LeftThumb DOWN");
            service.GetStatus();
            Check(backend.LeftThumb, "Status polling does not release an in-progress press");
            await pulse;
            Check(!backend.LeftThumb && backend.ThumbReports.SequenceEqual(new[] { true, false }) && pulseTime.ElapsedMilliseconds >= 100,
                "Virtual P1 activation releases LeftThumb after a short pulse");
            Check(service.Enabled && backend.Created == 1 && backend.Removed == 0,
                "Activation keeps the same virtual controller connected");
            slots.Connected = [0, 1];
            ControllerStatus status = service.GetStatus();
            Check(status.Ready && status.OtherControllerCount == 1 && status.DummySlot == 0,
                "Requires Controller 2 and excludes own dummy from count");
            slots.Connected = [0, 2];
            Check(!service.GetStatus().Ready, "A controller in slot 3 cannot report ready");
            slots.Connected = [0];
            Check(service.GetStatus().OtherControllerCount == 0 && !service.GetStatus().Ready,
                "Detects real-controller removal");
            service.Disable();
            Check(!service.Enabled && backend.Removed == 1, "Disable removes the dummy");
            service.Disable();
            Check(backend.Removed == 1, "Repeated disable is harmless");
            await service.EnableAsync();
        }
        Check(backend.Removed == 2 && !backend.IsActive, "Dispose removes an active dummy");

        slots = new Slots { Connected = [0] };
        backend = new Backend(slots);
        using (var service = new ControllerService(backend, slots))
        {
            await service.EnableAsync();
            Check(!service.Enabled && backend.Created == 0 && service.GetStatus().Message.Contains("Disconnect"),
                "Already-connected controller blocks enable with reconnect guidance");
        }

        slots = new Slots();
        backend = new Backend(slots) { Index = 2 };
        using (var service = new ControllerService(backend, slots))
        {
            await service.EnableAsync();
            Check(!service.Enabled && backend.Removed == 1 && service.GetStatus().Message.Contains("Controller 1"),
                "Wrong dummy slot fails and cleans up");
        }

        slots = new Slots();
        backend = new Backend(slots) { IndexReadsBeforeReady = 2 };
        using (var service = new ControllerService(backend, slots))
        {
            await service.EnableAsync();
            Check(service.Enabled, "Waits for asynchronous Windows slot assignment");
            backend.Index = 3;
            Check(!service.GetStatus().Enabled && backend.Removed == 1,
                "Lost slot ownership disconnects the dummy");
        }

        slots = new Slots();
        backend = new Backend(slots) { ConnectionError = new DriverMissingException() };
        using (var service = new ControllerService(backend, slots))
        {
            await service.EnableAsync();
            Check(service.DriverMissing && !service.Enabled, "Missing driver exposes the one-time installer action");
        }

        slots = new Slots();
        backend = new Backend(slots);
        using (var service = new ControllerService(backend, slots))
        {
            await service.EnableAsync();
            backend.HealthError = new InvalidOperationException("Driver disconnected.");
            Check(!service.GetStatus().Enabled && backend.Removed == 1,
                "Polling failure cleans up without retaining an active state");
        }

        foreach (bool failOnRelease in new[] { false, true })
        {
            slots = new Slots();
            backend = new Backend(slots) { FailThumbDown = !failOnRelease, FailThumbUp = failOnRelease };
            using var service = new ControllerService(backend, slots);
            await service.EnableAsync();
            try
            {
                await service.ActivateVirtualP1Async();
                throw new InvalidOperationException("Expected an input report failure.");
            }
            catch (InvalidOperationException e) when (e.Message == "Input report failed.")
            {
                Check(backend.ThumbReports.SequenceEqual(new[] { true, false }) && !service.Enabled && !backend.LeftThumb,
                    failOnRelease ? "Failed UP report cleans up rather than retaining a stuck button" : "Failed DOWN report still attempts UP and cleans up");
            }
        }
    }

    private static async Task RunOneControllerCheck()
    {
        var slots = new Slots();
        using var entered = new ManualResetEventSlim();
        using var allow = new ManualResetEventSlim();
        var backend = new Backend(slots) { ConnectEntered = entered, AllowConnect = allow };
        using var service = new ControllerService(backend, slots);
        Task first = service.EnableAsync();
        try
        {
            if (!await Task.Run(() => entered.Wait(2000))) throw new InvalidOperationException("Enable did not enter connection.");
            Task again = service.EnableAsync();
            Check(ReferenceEquals(first, again), "Overlapping Enable calls share one in-flight operation");
            allow.Set();
            await Task.WhenAll(first, again);
            Check(backend.Created == 1 && backend.Created - backend.Removed == 1,
                "Concurrent Enable and Enable again create exactly one device");
            await service.EnableAsync();
            Check(backend.Created == 1 && backend.Created - backend.Removed == 1,
                "Enable again after completion still has exactly one device");
            service.Disable();
            Check(backend.Created - backend.Removed == 0, "Disable leaves zero virtual devices");
        }
        finally { allow.Set(); await first; }
    }

    private static async Task RunCancelledEnableCheck()
    {
        foreach (bool dispose in new[] { false, true })
        {
            var slots = new Slots();
            using var entered = new ManualResetEventSlim();
            using var allow = new ManualResetEventSlim();
            var backend = new Backend(slots) { ConnectEntered = entered, AllowConnect = allow };
            using var service = new ControllerService(backend, slots);
            Task enabling = service.EnableAsync();
            try
            {
                if (!await Task.Run(() => entered.Wait(2000))) throw new InvalidOperationException("Enable did not enter connection.");
                if (dispose) service.Dispose(); else service.Disable();
                allow.Set();
                await enabling;
                Check(!service.Enabled && backend.Created == 0,
                    dispose ? "Close/dispose cancels a pending connection without creating a late device" : "Disable cancels a pending connection without creating a late device");
            }
            finally { allow.Set(); await enabling; }
        }
    }

    private static async Task ProbeOneDeviceLifecycle()
    {
        using var singleInstance = new Mutex(true, @"Local\DungeonCoopFix.V1", out bool firstInstance);
        if (!firstInstance)
        {
            Console.WriteLine("SKIP: lifecycle probe; the app or another probe is already open.");
            return;
        }
        var slots = new XInputControllers();
        string[] before = ReadXbox360Devices();
        if (before.Length != 0 || slots.GetConnectedSlots().Length != 0)
        {
            Console.WriteLine("SKIP: lifecycle probe; existing Xbox/XInput controllers are connected.");
            return;
        }
        using var backend = new VirtualControllerBackend();
        using var service = new ControllerService(backend, slots);
        Task enabling = service.EnableAsync();
        await Task.WhenAll(enabling, service.EnableAsync());
        Check(service.Enabled, "Real Enable succeeds using one backend");
        string[] enabled = ReadXbox360Devices();
        Console.WriteLine("Enabled Windows Xbox device IDs: " + string.Join(", ", enabled));
        Check(enabled.Length == 1 && slots.GetConnectedSlots().SequenceEqual(new[] { 0 }),
            "Real Enable: exactly one Windows Xbox 360 device and one XInput slot");
        await service.EnableAsync();
        backend.Connect(); // Direct backend repetition must also be idempotent.
        string[] again = ReadXbox360Devices();
        Check(again.SequenceEqual(enabled) && slots.GetConnectedSlots().SequenceEqual(new[] { 0 }),
            "Real Enable again: same one Windows device and same one XInput slot");
        using (var other = new VirtualControllerBackend())
        {
            try
            {
                other.Connect();
                throw new InvalidOperationException("A second backend must not create a target.");
            }
            catch (InvalidOperationException e) when (e.Message.Contains("already owns"))
            {
                Check(backend.IsActive && !other.IsActive,
                    "A second backend instance is rejected before native controller allocation");
            }
        }
        service.Disable();
        Check(ReadXbox360Devices().Length == 0 && slots.GetConnectedSlots().Length == 0,
            "Real Disable: zero Windows Xbox 360 devices and zero XInput slots");
        await service.EnableAsync();
        Check(ReadXbox360Devices().Length == 1, "Real re-enable creates one device");
        service.Dispose();
        Check(ReadXbox360Devices().Length == 0 && slots.GetConnectedSlots().Length == 0,
            "Real close/dispose: zero Windows Xbox 360 devices and zero XInput slots");
    }

    private static string[] ReadXbox360Devices()
    {
        // Read-only PnP enumeration. USB device nodes count pads, not their HID interfaces.
        string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
        using var process = Process.Start(new ProcessStartInfo(shell)
        {
            Arguments = "-NoProfile -NonInteractive -Command \"$ErrorActionPreference = 'Stop'; Get-PnpDevice -PresentOnly | Where-Object { $_.InstanceId -like 'USB\\VID_045E&PID_028E\\*' } | Select-Object -ExpandProperty InstanceId\"",
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true
        }) ?? throw new InvalidOperationException("Could not start the read-only device enumeration.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Windows device enumeration failed: " + error);
        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Order().ToArray();
    }

    private static void ProbeDriver()
    {
        // Native probes must never run alongside the app, including while it is OFF.
        using var singleInstance = new Mutex(true, @"Local\DungeonCoopFix.V1", out bool firstInstance);
        if (!firstInstance)
        {
            Console.WriteLine("SKIP: native probe; DungeonCoopFix or another probe is already open.");
            return;
        }
        int[] before = new XInputControllers().GetConnectedSlots();
        Console.WriteLine($"Actual XInput slots before probe: [{string.Join(", ", before)}]");
        if (before.Length != 0)
        {
            Console.WriteLine("SKIP: controller creation probe; controllers already connected.");
            return;
        }
        using var backend = new VirtualControllerBackend();
        try
        {
            backend.Connect();
            int? slot = null;
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < TimeSpan.FromSeconds(4))
            {
                slot = backend.GetUserIndex();
                if (slot.HasValue && NativeMethods.XInputGetState((uint)slot, out _) == 0) break;
                Thread.Sleep(100);
            }
            Check(slot == 0, "Real driver assigns dummy to Controller 1");
            backend.CheckConnection();
            Thread.Sleep(50);
            NativeMethods.XInputGetState(0, out var initialState);
            Console.WriteLine($"Actual initial report: buttons=0x{initialState.Gamepad.Buttons:X4}, triggers={initialState.Gamepad.LeftTrigger}/{initialState.Gamepad.RightTrigger}, sticks={initialState.Gamepad.LeftX}/{initialState.Gamepad.LeftY}/{initialState.Gamepad.RightX}/{initialState.Gamepad.RightY}");
            Check(NativeMethods.XInputGetState(0, out var state) == 0 &&
                  state.Gamepad.Buttons == 0 && state.Gamepad.LeftTrigger == 0 && state.Gamepad.RightTrigger == 0 &&
                  Math.Abs((int)state.Gamepad.LeftX) < 7849 && Math.Abs((int)state.Gamepad.LeftY) < 7849 &&
                  Math.Abs((int)state.Gamepad.RightX) < 8689 && Math.Abs((int)state.Gamepad.RightY) < 8689,
                "Real dummy has no buttons/triggers pressed and sticks within the XInput dead zones");
            backend.SetLeftThumb(true);
            try
            {
                deadline.Restart();
                while (deadline.Elapsed < TimeSpan.FromSeconds(1) &&
                       (NativeMethods.XInputGetState(0, out state) != 0 || state.Gamepad.Buttons != 0x0040))
                    Thread.Sleep(5);
                Check(NativeMethods.XInputGetState(0, out state) == 0 && state.Gamepad.Buttons == 0x0040,
                    "Real virtual Xbox controller exposes LeftThumb DOWN through XInput");
                backend.CheckConnection();
                Check(NativeMethods.XInputGetState(0, out state) == 0 && state.Gamepad.Buttons == 0x0040,
                    "Real status check preserves LeftThumb DOWN");
                Thread.Sleep(125);
            }
            finally { backend.SetLeftThumb(false); }
            deadline.Restart();
            while (deadline.Elapsed < TimeSpan.FromSeconds(1) &&
                   (NativeMethods.XInputGetState(0, out state) != 0 || state.Gamepad.Buttons != 0))
                Thread.Sleep(5);
            Check(NativeMethods.XInputGetState(0, out state) == 0 && state.Gamepad.Buttons == 0 && backend.IsActive && backend.GetUserIndex() == 0,
                "Real virtual Xbox controller releases LeftThumb and stays connected at Controller 1");
            using (var setupService = new ControllerService(backend, new XInputControllers()))
            {
                setupService.SetP1SetupMode(true);
                foreach (var mapping in new (Keys Key, ushort Button)[]
                {
                    (Keys.Enter, 0x1000), (Keys.Escape, 0x2000), (Keys.Up, 1), (Keys.W, 1),
                    (Keys.Down, 2), (Keys.S, 2), (Keys.Left, 4), (Keys.A, 4), (Keys.Right, 8), (Keys.D, 8), (Keys.F8, 0x0040)
                })
                {
                    setupService.ForwardSetupKey(mapping.Key, true);
                    Thread.Sleep(10);
                    Check(NativeMethods.XInputGetState(0, out state) == 0 && state.Gamepad.Buttons == mapping.Button,
                        $"Real XInput receives {mapping.Key} setup mapping");
                    setupService.ForwardSetupKey(mapping.Key, false);
                    Thread.Sleep(10);
                    Check(NativeMethods.XInputGetState(0, out state) == 0 && state.Gamepad.Buttons == 0,
                        $"Real XInput releases {mapping.Key} setup mapping");
                }
                setupService.ForwardSetupKey(Keys.Enter, true);
                setupService.SetP1SetupMode(false);
                Thread.Sleep(10);
                Check(backend.IsActive && backend.GetUserIndex() == 0 && NativeMethods.XInputGetState(0, out state) == 0 && state.Gamepad.Buttons == 0,
                    "Turning setup mode off releases real input and preserves the same dummy");
                // Dispose this wrapper only after the original removal check below.
                backend.Disconnect();
            }
            backend.Disconnect();
            deadline.Restart();
            while (NativeMethods.XInputGetState(0, out _) == 0 && deadline.Elapsed < TimeSpan.FromSeconds(4))
                Thread.Sleep(100);
            Check(NativeMethods.XInputGetState(0, out _) != 0, "Real dummy disappears after removal");
        }
        catch (DriverMissingException e)
        {
            Check(!backend.IsActive, "Missing-driver native path releases its allocation");
            Console.WriteLine($"SKIP: real controller creation/removal; {e.Message}");
        }
    }

    private static void RunWindowChecks()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var slots = new Slots();
        var backend = new Backend(slots);
        using var service = new ControllerService(backend, slots);
        using var form = new MainForm(service);
        using var timer = new System.Windows.Forms.Timer { Interval = 100 };
        Exception? error = null;
        form.Shown += (_, _) => timer.Start();
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            try
            {
                Check(form.Visible && form.Text == "DungeonCoopFix", "Basic desktop window starts");
                string taskRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
                string output = Path.Combine(taskRoot, "artifacts");
                Directory.CreateDirectory(output);
                using var image = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(image, form.ClientRectangle with { Width = form.Width, Height = form.Height });
                image.Save(Path.Combine(output, "startup.png"));
                var controls = form.Controls[0].Controls.Cast<Control>();
                foreach (Control control in controls.Where(c => c.Visible))
                    Check(control.Bottom <= form.Controls[0].Height - 16,
                        $"Startup content fits: {control.Text}");
                Button activate = controls.OfType<Button>().Single(b => b.Text == "Experimental: Virtual L3 pulse");
                Check(!activate.Enabled, "Virtual P1 button is disabled while the fix is off");
                controls.OfType<Button>().Single(b => b.Text == "Enable Co-op Fix").PerformClick();
                var deadline = Stopwatch.StartNew();
                while (!activate.Enabled && deadline.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(10);
                Check(activate.Enabled, "Virtual P1 button enables with the existing dummy even without a detected physical pad");
                activate.PerformClick();
                Check(backend.LeftThumb && !activate.Enabled, "Clicking Virtual P1 sends DOWN and blocks duplicate clicks");
                deadline.Restart();
                while (!activate.Enabled && deadline.Elapsed < TimeSpan.FromSeconds(2)) await Task.Delay(10);
                Check(activate.Enabled && !backend.LeftThumb && service.Enabled && backend.Created == 1 && backend.Removed == 0,
                    "UI click releases L3 and keeps the same controller connected");
                CheckBox setup = controls.OfType<CheckBox>().Single(c => c.Text == "Experimental: P1 Setup Mode");
                setup.Checked = true;
                Check(service.P1SetupMode && !activate.Enabled && form.AcceptButton is null,
                    "Setup checkbox activates mode and avoids conflicting UI L3/default Enter actions");
                var registrations = new SetupKeyboardInput.RawInputDevice[8];
                uint registrationCount = (uint)registrations.Length;
                uint registered = GetRegisteredRawInputDevices(registrations, ref registrationCount, 16);
                Console.WriteLine($"Raw registrations: result={registered}, count={registrationCount}, window={form.Handle}; " +
                    string.Join("; ", registrations.Take((int)Math.Min(registered, (uint)registrations.Length)).Select(d => $"{d.UsagePage}/{d.Usage}: flags=0x{d.Flags:X}, window={d.Window}")));
                Check(registered != uint.MaxValue && registrations.Take((int)registered).Any(d => d.UsagePage == 1 && d.Usage == 6 && d.Window == form.Handle && (d.Flags & 0x100) != 0 && (d.Flags & 0x30) == 0),
                    "Windows registers background keyboard input without NOLEGACY/suppression");
                service.ForwardSetupKey(Keys.Enter, true);
                Check(backend.Buttons == 0x1000, "Setup mode forwards keyboard-backed A while the dummy remains connected");
                using var setupImage = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(setupImage, new Rectangle(0, 0, form.Width, form.Height));
                setupImage.Save(Path.Combine(output, "v1.2-setup.png"));
                setup.Checked = false;
                registrationCount = (uint)registrations.Length;
                registered = GetRegisteredRawInputDevices(registrations, ref registrationCount, 16);
                Check(backend.Buttons == 0 && service.Enabled && registered != uint.MaxValue &&
                      !registrations.Take((int)registered).Any(d => d.UsagePage == 1 && d.Usage == 6),
                    "UI mode off releases A, unregisters raw keyboard capture and keeps the dummy");
                using var activeImage = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(activeImage, new Rectangle(0, 0, form.Width, form.Height));
                activeImage.Save(Path.Combine(output, "v1.1-active.png"));
                // Close during another pulse: cleanup must wait for the release.
                activate.PerformClick();
                form.Close();
                Check(form.Visible && backend.LeftThumb, "Closing during a pulse waits for release");
            }
            catch (Exception e) { error = e; }
            finally { form.Close(); }
        };
        Application.Run(form);
        if (error is not null) throw error;
        Check(!backend.LeftThumb && !service.Enabled, "Deferred close releases the button and removes the dummy");
        Check(!service.Enabled, "Window closes without retaining a dummy");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException($"FAIL: {description}");
        _passed++;
        Console.WriteLine($"PASS: {description}");
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRegisteredRawInputDevices([Out] SetupKeyboardInput.RawInputDevice[] devices, ref uint count, uint size);

    private static void RunSetupCloseCheck()
    {
        var slots = new Slots();
        var backend = new Backend(slots);
        backend.Connect();
        using var service = new ControllerService(backend, slots);
        using var form = new MainForm(service);
        form.Shown += (_, _) =>
        {
            form.Controls[0].Controls.OfType<CheckBox>().Single().Checked = true;
            service.ForwardSetupKey(Keys.Enter, true);
            service.ForwardSetupKey(Keys.F8, true);
            form.Close();
        };
        Application.Run(form);
        var registrations = new SetupKeyboardInput.RawInputDevice[8];
        uint count = (uint)registrations.Length;
        uint result = GetRegisteredRawInputDevices(registrations, ref count, 16);
        Check(!service.P1SetupMode && !backend.IsActive && backend.Buttons == 0 && result != uint.MaxValue &&
              !registrations.Take((int)result).Any(d => d.UsagePage == 1 && d.Usage == 6),
            "Closing with setup mode active clears held A/L3 and unregisters background keyboard capture");
    }

    private static async Task RunSetupChecks()
    {
        var slots = new Slots();
        var backend = new Backend(slots);
        using var service = new ControllerService(backend, slots);
        service.ForwardSetupKey(Keys.Enter, true);
        Check(backend.ButtonReports.Count == 0 && !service.P1SetupMode, "No keyboard mapping while the fix/mode is off");
        await service.EnableAsync();
        service.SetP1SetupMode(true);
        service.ForwardSetupKey(Keys.Enter, true);
        service.ForwardSetupKey(Keys.Escape, true);
        service.ForwardSetupKey(Keys.Up, true);
        service.ForwardSetupKey(Keys.Right, true);
        service.ForwardSetupKey(Keys.F8, true);
        Check(backend.Buttons == 0x3049, "Setup forwards simultaneous A/B/diagonal D-pad/L3 input");
        int count = backend.ButtonReports.Count;
        service.ForwardSetupKey(Keys.Enter, true);
        service.ForwardSetupKey(Keys.Space, true);
        Check(backend.ButtonReports.Count == count, "Repeats and unmapped keys add no reports");
        service.ForwardSetupKey(Keys.W, true);
        service.ForwardSetupKey(Keys.Up, false);
        Check((backend.Buttons & 1) != 0, "Releasing an arrow preserves its held WASD alias");
        service.ForwardSetupKey(Keys.Down, true);
        Check((backend.Buttons & 3) == 0, "Opposite D-pad directions cancel");
        service.ReleaseSetupKeys();
        Check(backend.Buttons == 0 && service.P1SetupMode, "Keyboard unplug/reset releases all forwarded input");
        service.ForwardSetupKey(Keys.Enter, true);
        service.SetP1SetupMode(false);
        count = backend.ButtonReports.Count;
        service.ForwardSetupKey(Keys.F8, true);
        Check(backend.Buttons == 0 && backend.ButtonReports.Count == count && service.Enabled && backend.Created == 1 && backend.Removed == 0,
            "Mode off releases held input, ignores further keys, and keeps the same dummy");
        service.SetP1SetupMode(true);
        service.ForwardSetupKey(Keys.Left, true);
        service.Disable();
        Check(!service.P1SetupMode && backend.Buttons == 0 && !service.Enabled, "Disabling the fix also clears setup mode and held input");
        await service.EnableAsync();
        Check(!service.P1SetupMode && backend.Buttons == 0, "Re-enable starts neutral with setup mode off");
    }

    private sealed class Slots : IControllerSlots
    {
        internal int[] Connected = [];
        public int[] GetConnectedSlots() => Connected;
    }

    private sealed class Backend(Slots slots) : IVirtualControllerBackend
    {
        public bool IsActive { get; private set; }
        internal int Index = 0, Created, Removed, IndexReadsBeforeReady;
        internal Exception? ConnectionError, HealthError;
        internal bool LeftThumb, FailThumbDown, FailThumbUp;
        internal readonly List<bool> ThumbReports = [];
        internal ushort Buttons;
        internal readonly List<ushort> ButtonReports = [];
        internal ManualResetEventSlim? ConnectEntered, AllowConnect;
        public void Connect(CancellationToken cancellationToken = default)
        {
            ConnectEntered?.Set();
            if (AllowConnect is not null && !AllowConnect.Wait(5000, cancellationToken)) throw new InvalidOperationException("Test connection gate timed out.");
            cancellationToken.ThrowIfCancellationRequested();
            if (ConnectionError is not null) throw ConnectionError;
            IsActive = true;
            Interlocked.Increment(ref Created);
            slots.Connected = [Index];
        }
        public int? GetUserIndex() => IndexReadsBeforeReady-- > 0 ? null : Index;
        public void CheckConnection() { if (HealthError is not null) throw HealthError; }
        public void SetLeftThumb(bool pressed)
        {
            ThumbReports.Add(pressed);
            if (pressed ? FailThumbDown : FailThumbUp) throw new InvalidOperationException("Input report failed.");
            LeftThumb = pressed;
        }
        public void SetButtons(ushort buttons)
        {
            Buttons = buttons;
            LeftThumb = (buttons & 0x0040) != 0;
            ButtonReports.Add(buttons);
        }
        public void Disconnect()
        {
            if (!IsActive) return;
            IsActive = false;
            LeftThumb = false;
            Buttons = 0;
            Removed++;
            slots.Connected = [];
        }
        public void Dispose() => Disconnect();
    }
}
