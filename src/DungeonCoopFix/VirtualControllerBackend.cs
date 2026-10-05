using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DungeonCoopFix;

internal interface IVirtualControllerBackend : IDisposable
{
    bool IsActive { get; }
    void Connect(CancellationToken cancellationToken = default);
    int? GetUserIndex();
    void CheckConnection();
    void SetLeftThumb(bool pressed);
    void SetButtons(ushort buttons);
    void Disconnect();
}

internal sealed class DriverMissingException : Exception
{
    internal DriverMissingException() : base("The virtual-controller driver is not installed or running. Install the required driver, then try again.") { }
}

internal sealed class VirtualControllerBackend : IVirtualControllerBackend
{
    private IntPtr _client, _target;
    private bool _attached;
    private bool _disposed;
    private NativeMethods.GamepadReport _report;
    private static IntPtr _library;
    private static readonly object DeviceGate = new();
    private static VirtualControllerBackend? _owner;
    public bool IsActive { get { lock (DeviceGate) return _attached; } }

    static VirtualControllerBackend()
    {
        NativeLibrary.SetDllImportResolver(typeof(VirtualControllerBackend).Assembly, ResolveLibrary);
    }

    private static IntPtr ResolveLibrary(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != "ViGEmClient.dll") return IntPtr.Zero;
        if (_library != IntPtr.Zero) return _library;
        string path = Path.Combine(AppContext.BaseDirectory, name);
        if (!File.Exists(path)) throw new DllNotFoundException("ViGEmClient.dll is missing. Extract the complete DungeonCoopFix folder again.");
        using var file = File.OpenRead(path);
        string hash = Convert.ToHexString(SHA256.HashData(file));
        if (hash != "00303F99E1968D523FBD08A7C691D60D145B7335059EC593947841D124C10875")
            throw new InvalidDataException("The bundled controller component has changed. Extract a fresh copy of DungeonCoopFix.");
        _library = NativeLibrary.Load(path);
        return _library;
    }

    public void Connect(CancellationToken cancellationToken = default)
    {
        lock (DeviceGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (_attached) return;
            if (_owner is not null && !ReferenceEquals(_owner, this))
                throw new InvalidOperationException("DungeonCoopFix already owns a virtual controller. Disable it before creating another connection.");
            _owner = this;
            try
            {
                _client = NativeMethods.vigem_alloc();
                if (_client == IntPtr.Zero) throw new InvalidOperationException("Could not allocate the virtual-controller connection.");
                Check(NativeMethods.vigem_connect(_client), "connect to the driver");
                _target = NativeMethods.vigem_target_x360_alloc();
                if (_target == IntPtr.Zero) throw new InvalidOperationException("Could not allocate the dummy controller.");
                Check(NativeMethods.vigem_target_add(_client, _target), "create the dummy controller");
                _attached = true;
                cancellationToken.ThrowIfCancellationRequested();
                CheckConnection();
            }
            catch
            {
                Disconnect();
                throw;
            }
        }
    }

    public int? GetUserIndex()
    {
        lock (DeviceGate)
        {
            if (!_attached) return null;
            uint result = NativeMethods.vigem_target_x360_get_user_index(_client, _target, out uint index);
            // Windows can take a moment to assign a slot after the device appears.
            if (result == 0xE0000014) return null;
            Check(result, "read the dummy controller's slot");
            return index < 4 ? (int)index : null;
        }
    }

    public void CheckConnection()
    {
        lock (DeviceGate)
        {
            if (_attached)
                // Preserve an in-progress L3 press if a status check runs during it.
                Check(NativeMethods.vigem_target_x360_update(_client, _target, _report), "check the dummy controller");
        }
    }

    public void SetLeftThumb(bool pressed)
    {
        lock (DeviceGate)
        {
            SetButtons(pressed ? (ushort)(_report.Buttons | 0x0040) : (ushort)(_report.Buttons & ~0x0040));
        }
    }

    public void SetButtons(ushort buttons)
    {
        lock (DeviceGate)
        {
            if (!_attached) throw new InvalidOperationException("Enable the co-op fix before sending Virtual P1 input.");
            _report = new NativeMethods.GamepadReport { Buttons = buttons };
            Check(NativeMethods.vigem_target_x360_update(_client, _target, _report), "send the Virtual P1 button press");
        }
    }

    public void Disconnect()
    {
        lock (DeviceGate)
        {
            uint result = NativeMethods.Success;
            try
            {
                if (_attached) result = NativeMethods.vigem_target_remove(_client, _target);
            }
            finally
            {
                _attached = false;
                _report = default;
                // Closing the client also releases its devices if explicit removal failed.
                if (_client != IntPtr.Zero)
                {
                    NativeMethods.vigem_disconnect(_client);
                    NativeMethods.vigem_free(_client);
                    _client = IntPtr.Zero;
                }
                if (_target != IntPtr.Zero)
                {
                    NativeMethods.vigem_target_free(_target);
                    _target = IntPtr.Zero;
                }
                if (ReferenceEquals(_owner, this)) _owner = null;
            }
            if (result != NativeMethods.Success && result != 0xE0000007 && result != NativeMethods.BusNotFound)
                Check(result, "remove the dummy controller; the driver connection was closed");
        }
    }

    private static void Check(uint result, string operation)
    {
        if (result == NativeMethods.Success) return;
        if (result == NativeMethods.BusNotFound) throw new DriverMissingException();
        string message = result switch
        {
            0xE0000002 => "All controller slots are occupied. Disconnect other controllers and stop other virtual-controller apps.",
            0xE0000008 => "The installed driver is incompatible. Use the bundled official driver installer to repair it.",
            0xE0000009 => "Windows denied access to the controller driver. Repair the driver with the installer; normal app use should not need administrator access.",
            _ => $"Could not {operation} (driver error 0x{result:X8}). Disable the fix and reconnect your controller before trying again."
        };
        throw new InvalidOperationException(message);
    }

    public void Dispose()
    {
        lock (DeviceGate)
        {
            if (_disposed) return;
            try { Disconnect(); }
            finally { _disposed = true; }
        }
    }
}
