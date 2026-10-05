using System.ComponentModel;
using System.Runtime.InteropServices;

namespace DungeonCoopFix;

// Windows' ordinary Raw Input message API; no hook, input suppression or logging.
internal static class SetupKeyboardInput
{
    internal const int InputMessage = 0x00FF, DeviceChangeMessage = 0x00FE;
    private const uint InputCommand = 0x10000003;

    internal static void Register(IntPtr window, bool enabled)
    {
        var device = new RawInputDevice
        {
            UsagePage = 1, Usage = 6, // keyboard
            Flags = enabled ? 0x2100u : 1u, // INPUTSINK + DEVNOTIFY, or REMOVE
            Window = enabled ? window : IntPtr.Zero
        };
        if (!RegisterRawInputDevices([device], 1, (uint)Marshal.SizeOf<RawInputDevice>()))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not change P1 Setup Mode keyboard capture.");
    }

    internal static (Keys Key, bool Pressed)? Read(IntPtr input)
    {
        uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        uint size = 0;
        if (GetRawInputData(input, InputCommand, IntPtr.Zero, ref size, headerSize) == uint.MaxValue)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (size < headerSize + Marshal.SizeOf<RawKeyboard>()) return null;
        IntPtr buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            uint read = GetRawInputData(input, InputCommand, buffer, ref size, headerSize);
            if (read == uint.MaxValue) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (read < headerSize + Marshal.SizeOf<RawKeyboard>()) return null;
            if (Marshal.PtrToStructure<RawInputHeader>(buffer).Type != 1) return null;
            RawKeyboard keyboard = Marshal.PtrToStructure<RawKeyboard>(IntPtr.Add(buffer, (int)headerSize));
            if (keyboard.MakeCode == 0xFF || keyboard.VirtualKey >= 0xFF) return null;
            return ((Keys)keyboard.VirtualKey, (keyboard.Flags & 1) == 0);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputDevice
    {
        public ushort UsagePage, Usage;
        public uint Flags;
        public IntPtr Window;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct RawInputHeader
    {
        public uint Type, Size;
        public IntPtr Device, WParam;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal struct RawKeyboard
    {
        public ushort MakeCode, Flags, Reserved, VirtualKey;
        public uint Message, ExtraInformation;
    }
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
}
