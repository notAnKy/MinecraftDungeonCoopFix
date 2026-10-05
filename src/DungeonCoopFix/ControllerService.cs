namespace DungeonCoopFix;

internal record ControllerStatus(bool Enabled, int? DummySlot, int OtherControllerCount, bool Ready, string Message);

internal sealed class ControllerService(IVirtualControllerBackend backend, IControllerSlots controllers) : IDisposable
{
    private bool _disposed;
    private bool _driverMissing;
    private string? _error;
    private readonly HashSet<Keys> _setupKeys = [];
    private readonly object _enableGate = new();
    private Task? _enableTask;
    private CancellationTokenSource? _enableCancellation;
    internal bool P1SetupMode { get; private set; }
    internal bool DriverMissing => _driverMissing;
    internal bool Enabled => backend.IsActive;

    internal Task EnableAsync()
    {
        lock (_enableGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // Share the whole in-flight operation, not just the attached state.
            if (_enableTask is { IsCompleted: false }) return _enableTask;
            if (Enabled) return Task.CompletedTask;
            _enableCancellation?.Dispose();
            _enableCancellation = new CancellationTokenSource();
            return _enableTask = EnableCoreAsync(_enableCancellation.Token);
        }
    }

    private async Task EnableCoreAsync(CancellationToken cancellationToken)
    {
        _error = null;
        _driverMissing = false;
        if (controllers.GetConnectedSlots().Length != 0)
        {
            _error = "Disconnect your controller, enable the fix, then reconnect it. Close other virtual-controller apps too.";
            return;
        }

        try
        {
            // Driver enumeration can block. The UI awaits this and delays shutdown
            // until it finishes, so no connect/remove native calls run concurrently.
            await Task.Run(() => backend.Connect(cancellationToken), cancellationToken);
            for (int attempt = 0; attempt < 30; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int? index = backend.GetUserIndex();
                if (index.HasValue)
                {
                    if (index != 0)
                        throw new InvalidOperationException("The dummy did not become Controller 1. Disconnect all controllers, close other controller apps, and try again. If Windows remembers a slot, restart Windows with controllers disconnected.");
                    if (controllers.GetConnectedSlots().Contains(0)) return;
                }
                await Task.Delay(100, cancellationToken);
            }
            throw new InvalidOperationException("Windows did not confirm the dummy controller's slot. Restart Windows with controllers disconnected, then try again.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception e)
        {
            if (cancellationToken.IsCancellationRequested) return;
            _driverMissing = e is DriverMissingException;
            _error = e.Message;
            StopBackend();
        }
    }

    internal void Disable()
    {
        _error = null;
        StopBackend();
    }

    internal async Task ActivateVirtualP1Async()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!Enabled || backend.GetUserIndex() != 0)
            throw new InvalidOperationException("Enable the co-op fix with the dummy as Controller 1 first.");
        if (P1SetupMode)
            throw new InvalidOperationException("Use F8 for L3 during P1 Setup Mode, or turn setup mode off first.");
        try
        {
            try
            {
                backend.SetLeftThumb(true);
                await Task.Delay(125);
            }
            finally
            {
                // Always attempt release, including when the DOWN report fails.
                backend.SetLeftThumb(false);
            }
        }
        catch
        {
            // A failed release must never leave a stuck virtual button.
            StopBackend();
            throw;
        }
    }

    private void StopBackend()
    {
        lock (_enableGate) _enableCancellation?.Cancel();
        P1SetupMode = false;
        _setupKeys.Clear();
        try { backend.Disconnect(); }
        catch (Exception e) { _error = $"{_error}\n{e.Message}".Trim(); }
    }

    internal void SetP1SetupMode(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (enabled && !Enabled)
            throw new InvalidOperationException("Enable the co-op fix before turning on P1 Setup Mode.");
        if (P1SetupMode == enabled) return;
        _setupKeys.Clear();
        try
        {
            if (Enabled) backend.SetButtons(0);
            P1SetupMode = enabled;
        }
        catch { StopBackend(); throw; }
    }

    internal void ForwardSetupKey(Keys key, bool pressed)
    {
        if (!P1SetupMode || !Enabled || SetupButton(key) == 0) return;
        if (!(pressed ? _setupKeys.Add(key) : _setupKeys.Remove(key))) return;
        ushort buttons = 0;
        foreach (Keys held in _setupKeys) buttons |= SetupButton(held);
        // Opposite directions cancel; aliases retain a button until both are released.
        if ((buttons & 0x0003) == 0x0003) buttons &= 0xFFFC;
        if ((buttons & 0x000C) == 0x000C) buttons &= 0xFFF3;
        try { backend.SetButtons(buttons); }
        catch { StopBackend(); throw; }
    }

    internal void ReleaseSetupKeys()
    {
        _setupKeys.Clear();
        if (!P1SetupMode || !Enabled) return;
        try { backend.SetButtons(0); }
        catch { StopBackend(); throw; }
    }

    private static ushort SetupButton(Keys key) => key switch
    {
        Keys.Enter => 0x1000, // A
        Keys.Escape => 0x2000, // B
        Keys.Up or Keys.W => 0x0001,
        Keys.Down or Keys.S => 0x0002,
        Keys.Left or Keys.A => 0x0004,
        Keys.Right or Keys.D => 0x0008,
        Keys.F8 => 0x0040, // L3
        _ => 0
    };

    internal ControllerStatus GetStatus()
    {
        try
        {
            int[] slots = controllers.GetConnectedSlots();
            int? dummy = null;
            if (Enabled)
            {
                backend.CheckConnection();
                dummy = backend.GetUserIndex();
                if (dummy != 0 || !slots.Contains(0))
                {
                    _error = "The dummy lost Controller 1. Disconnect your controller, disable the fix, enable it again, then reconnect.";
                    StopBackend();
                    slots = controllers.GetConnectedSlots();
                    dummy = null;
                }
            }
            int count = slots.Count(index => index != dummy);
            bool ready = Enabled && dummy == 0 && slots.Contains(1);
            string message = _error ?? (ready
                ? "Two XInput controllers detected. Game player assignment must be checked in the lobby."
                : Enabled
                    ? count > 0
                        ? "Another XInput controller is detected. Game player assignment must be checked in the lobby."
                        : "Connect your PS4 controller and enable Steam Input. In Steam Controller Order, put the dummy Xbox first and PS4 second."
                    : count > 0
                        ? "Disconnect your controller, enable the fix, then reconnect it."
                        : "Enable the fix before connecting your controller or launching the game.");
            return new(Enabled, dummy, count, ready, message);
        }
        catch (Exception e)
        {
            _driverMissing = e is DriverMissingException;
            _error = e.Message;
            StopBackend();
            return new(false, null, 0, false, _error);
        }
    }

    public void Dispose()
    {
        lock (_enableGate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        StopBackend();
        backend.Dispose();
    }
}
